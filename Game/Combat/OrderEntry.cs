using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a member can be told to do, as the combat menu lists it.</summary>
public enum CombatCommand
{
	Attack,
	Defend,
	Focus,
	Meditate,
	Flee
}

/// <summary>Where the giving of a round's orders stands.</summary>
public enum OrderStep
{
	/// <summary><see cref="OrderEntry.Member"/> is choosing a command.</summary>
	Command,

	/// <summary><see cref="OrderEntry.Member"/> is choosing whom to attack.</summary>
	Target,

	/// <summary>Every member has an order, in <see cref="OrderEntry.Orders"/>.</summary>
	Complete
}

/// <summary>
/// The way through the combat menu for one round: a command for each member who needs an
/// order, then a target, then the next member, with a way back.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free, so the menu's rules are tested without the screen, which only shows the
/// step it is at and calls what the player chooses.
/// </para>
/// <para>
/// <b>The commands</b> (<see cref="Choose"/>): attacking goes on to choose a target;
/// defending, focusing and meditating are an order already, and the next member chooses.
/// Fleeing is the whole party's, so it can be chosen from any member's menu: it forgets the
/// orders given so far, and the orders are complete with the flee alone. Meditating is
/// offered only to the members who can, and fleeing only in a fight that can be fled
/// (<see cref="CanChoose"/>): the screen shows the others disabled.
/// </para>
/// <para>
/// <b>Going back</b> (<see cref="Back"/>, on <c>cancel</c>): from a target to the command
/// of the same member; from a command to the member before, whose order is forgotten, on
/// their target if the order had one and on their command if not. On the first member's
/// command there is nowhere to go, and nothing happens.
/// </para>
/// </remarks>
public sealed class OrderEntry
{
	private readonly IReadOnlyList<CombatantId> _members;
	private readonly IReadOnlySet<CombatantId> _canMeditate;
	private readonly bool _canFlee;
	private readonly List<CombatOrder> _orders = new();

	/// <param name="members">Who needs an order, in order: the runner's <see cref="CombatRunner.MembersToOrder"/>.</param>
	/// <param name="targets">Whom they can attack: the enemies standing, in the group's order.</param>
	/// <param name="canMeditate">Which of the members can meditate: those the runner's <see cref="CombatRunner.CanMeditate"/> lets.</param>
	/// <param name="canFlee">Whether the fight can be fled: the runner's <see cref="CombatRunner.CanFlee"/>.</param>
	public OrderEntry(IEnumerable<CombatantId> members, IEnumerable<CombatantId> targets, IEnumerable<CombatantId> canMeditate, bool canFlee)
	{
		ArgumentNullException.ThrowIfNull(members);
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(canMeditate);

		CombatantId[] ordered = members.ToArray();
		CombatantId[] attackable = targets.ToArray();
		var meditators = new HashSet<CombatantId>(canMeditate);

		if (ordered.Length == 0)
			throw new ArgumentException("A round's orders need someone to give them to.", nameof(members));

		if (ordered.Any(member => member.Side != CombatSide.Party) || ordered.Distinct().Count() != ordered.Length)
			throw new ArgumentException("The members are each a member of the party, once.", nameof(members));

		if (attackable.Length == 0)
			throw new ArgumentException("A round's orders need someone to attack.", nameof(targets));

		if (attackable.Any(target => target.Side != CombatSide.Enemy) || attackable.Distinct().Count() != attackable.Length)
			throw new ArgumentException("The targets are each an enemy, once.", nameof(targets));

		if (!meditators.IsSubsetOf(ordered))
			throw new ArgumentException("Whoever can meditate is one of the members who need an order.", nameof(canMeditate));

		_members = Array.AsReadOnly(ordered);
		Targets = Array.AsReadOnly(attackable);
		_canMeditate = meditators;
		_canFlee = canFlee;
	}

	public OrderStep Step { get; private set; } = OrderStep.Command;

	/// <summary>The member choosing now. Not there once the orders are complete.</summary>
	/// <exception cref="InvalidOperationException">The orders are complete.</exception>
	public CombatantId Member => Step != OrderStep.Complete
		? _members[_orders.Count]
		: throw new InvalidOperationException("Every member has an order already.");

	/// <summary>Whom a member can attack.</summary>
	public IReadOnlyList<CombatantId> Targets { get; }

	/// <summary>Whether <see cref="Back"/> has somewhere to go.</summary>
	public bool CanGoBack => Step == OrderStep.Target || (Step == OrderStep.Command && _orders.Count > 0);

	/// <summary>The orders given so far, in the members' order; all of them once <see cref="Step"/> is <see cref="OrderStep.Complete"/>.</summary>
	public IReadOnlyList<CombatOrder> Orders => _orders.AsReadOnly();

	/// <summary>Whether the member choosing a command can choose this one.</summary>
	/// <exception cref="InvalidOperationException">The member is not choosing a command.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The command is not one of the menu's.</exception>
	public bool CanChoose(CombatCommand command)
	{
		Require(OrderStep.Command, "ask about a command");

		return command switch
		{
			CombatCommand.Attack or CombatCommand.Defend or CombatCommand.Focus => true,
			CombatCommand.Meditate => _canMeditate.Contains(Member),
			CombatCommand.Flee => _canFlee,
			_ => throw new ArgumentOutOfRangeException(nameof(command), command, "The menu has no such command.")
		};
	}

	/// <summary>
	/// The member chooses a command: an attack goes on to choose whom, a flee completes the
	/// orders alone, and the rest are the member's order, so the next member chooses.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The member is not choosing a command, or cannot choose this one.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">The command is not one of the menu's.</exception>
	public void Choose(CombatCommand command)
	{
		if (!CanChoose(command))
			throw new InvalidOperationException($"{Member} cannot choose to {command} now.");

		CombatantId member = Member;

		switch (command)
		{
			case CombatCommand.Attack:
				Step = OrderStep.Target;
				break;
			case CombatCommand.Defend:
				Give(new DefendOrder(member));
				break;
			case CombatCommand.Focus:
				Give(new FocusOrder(member));
				break;
			case CombatCommand.Meditate:
				Give(new MeditateOrder(member));
				break;
			case CombatCommand.Flee:
				_orders.Clear();
				_orders.Add(new FleeOrder(member));
				Step = OrderStep.Complete;
				break;
		}
	}

	/// <summary>The member attacks this target. The next member chooses, or the orders are complete.</summary>
	/// <exception cref="InvalidOperationException">The member is not choosing a target.</exception>
	/// <exception cref="ArgumentException">The target is not one of <see cref="Targets"/>.</exception>
	public void ChooseTarget(CombatantId target)
	{
		Require(OrderStep.Target, "choose a target");

		if (!Targets.Contains(target))
			throw new ArgumentException($"{target} is not an enemy that can be attacked.", nameof(target));

		Give(new AttackOrder(Member, target));
	}

	/// <summary>Goes one step back, if there is a step to go back to.</summary>
	/// <returns>Whether it went back. On the first member's command it does not, and nothing changes.</returns>
	/// <exception cref="InvalidOperationException">The orders are complete.</exception>
	public bool Back()
	{
		if (Step == OrderStep.Complete)
			throw new InvalidOperationException("The orders are complete, and are not taken back.");

		if (!CanGoBack)
			return false;

		if (Step == OrderStep.Target)
		{
			Step = OrderStep.Command;
			return true;
		}

		CombatOrder forgotten = _orders[^1];
		_orders.RemoveAt(_orders.Count - 1);
		Step = forgotten is AttackOrder ? OrderStep.Target : OrderStep.Command;
		return true;
	}

	/// <summary>Keeps the member's order, and the next member chooses, or the orders are complete.</summary>
	private void Give(CombatOrder order)
	{
		_orders.Add(order);
		Step = _orders.Count == _members.Count ? OrderStep.Complete : OrderStep.Command;
	}

	private void Require(OrderStep step, string what)
	{
		if (Step != step)
			throw new InvalidOperationException($"Cannot {what} at {Step}.");
	}
}
