using System;
using System.Collections.Generic;
using System.Linq;

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
/// step it is at and calls what the player chooses. The only command today is to attack.
/// </para>
/// <para>
/// <b>Going back</b> (<see cref="Back"/>, on <c>cancel</c>): from a target to the command
/// of the same member; from a command to the target of the member before, whose order is
/// forgotten. On the first member's command there is nowhere to go, and nothing happens.
/// </para>
/// </remarks>
public sealed class OrderEntry
{
	private readonly IReadOnlyList<CombatantId> _members;
	private readonly List<CombatOrder> _orders = new();

	/// <param name="members">Who needs an order, in order: the runner's <see cref="CombatRunner.MembersToOrder"/>.</param>
	/// <param name="targets">Whom they can attack: the enemies standing, in the group's order.</param>
	public OrderEntry(IEnumerable<CombatantId> members, IEnumerable<CombatantId> targets)
	{
		ArgumentNullException.ThrowIfNull(members);
		ArgumentNullException.ThrowIfNull(targets);

		CombatantId[] ordered = members.ToArray();
		CombatantId[] attackable = targets.ToArray();

		if (ordered.Length == 0)
			throw new ArgumentException("A round's orders need someone to give them to.", nameof(members));

		if (ordered.Any(member => member.Side != CombatSide.Party) || ordered.Distinct().Count() != ordered.Length)
			throw new ArgumentException("The members are each a member of the party, once.", nameof(members));

		if (attackable.Length == 0)
			throw new ArgumentException("A round's orders need someone to attack.", nameof(targets));

		if (attackable.Any(target => target.Side != CombatSide.Enemy) || attackable.Distinct().Count() != attackable.Length)
			throw new ArgumentException("The targets are each an enemy, once.", nameof(targets));

		_members = Array.AsReadOnly(ordered);
		Targets = Array.AsReadOnly(attackable);
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

	/// <summary>The member chooses to attack, and goes on to choose whom.</summary>
	/// <exception cref="InvalidOperationException">The member is not choosing a command.</exception>
	public void ChooseAttack()
	{
		Require(OrderStep.Command, "choose a command");
		Step = OrderStep.Target;
	}

	/// <summary>The member attacks this target. The next member chooses, or the orders are complete.</summary>
	/// <exception cref="InvalidOperationException">The member is not choosing a target.</exception>
	/// <exception cref="ArgumentException">The target is not one of <see cref="Targets"/>.</exception>
	public void ChooseTarget(CombatantId target)
	{
		Require(OrderStep.Target, "choose a target");

		if (!Targets.Contains(target))
			throw new ArgumentException($"{target} is not an enemy that can be attacked.", nameof(target));

		_orders.Add(new AttackOrder(Member, target));
		Step = _orders.Count == _members.Count ? OrderStep.Complete : OrderStep.Command;
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

		_orders.RemoveAt(_orders.Count - 1);
		Step = OrderStep.Target;
		return true;
	}

	private void Require(OrderStep step, string what)
	{
		if (Step != step)
			throw new InvalidOperationException($"Cannot {what} at {Step}.");
	}
}
