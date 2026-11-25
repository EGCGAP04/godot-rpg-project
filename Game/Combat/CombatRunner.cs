using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a running fight is waiting for.</summary>
public enum CombatStep
{
	/// <summary>
	/// An order for each of <see cref="CombatRunner.MembersToOrder"/>. Continue with
	/// <see cref="CombatRunner.SubmitOrders"/>.
	/// </summary>
	AwaitingOrders,

	/// <summary>
	/// The damage taken from the attack in <see cref="CombatRunner.PendingAttack"/>.
	/// Continue with <see cref="CombatRunner.AnswerEnemyAttack"/>.
	/// </summary>
	EnemyAttackPending,

	/// <summary>Nothing: the fight is over, and <see cref="CombatRunner.Result"/> says how.</summary>
	Finished
}

/// <summary>How a fight stands.</summary>
public enum CombatResult
{
	Ongoing,

	/// <summary>Every enemy is down.</summary>
	Won,

	/// <summary>The whole party is down.</summary>
	Lost,

	/// <summary>The party fled.</summary>
	Fled
}

/// <summary>
/// An enemy's attack, worked out and not applied yet: who attacks, whom, and what the
/// damage pipeline answered, with armor and the target's defence already taken out of
/// the damage.
/// </summary>
/// <remarks>
/// A fight where the player dodges in real time answers with what got through, as it is.
/// Such a scene will need to know whether the target defends, to take the defence out of
/// what it lets through; the day it exists, this record carries that too.
/// </remarks>
public sealed record PendingEnemyAttack(CombatantId Attacker, CombatantId Target, AttackResult Result);

/// <summary>
/// Runs one fight, a round at a time: it takes an order for every member, works the
/// round out in turn order, and stops whenever it needs something from outside.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free and passive, like <c>DialogueRunner</c>. Whoever drives it, the combat
/// screen or a test, asks which <see cref="Step"/> it is at and answers that. It never
/// waits, draws or reads input itself. What happened in between is in
/// <see cref="TakeEvents"/>, to be shown at the driver's own pace.
/// </para>
/// <para>
/// <b>An enemy's attack is a step, not a result.</b> The runner picks the target, works
/// the attack out and stops, on every enemy attack, the ones that miss included, so the
/// stop is always the same. The driver answers with the damage that was taken. Today that
/// is the damage in <see cref="PendingAttack"/>; a fight where the player dodges in real
/// time answers with what got through instead. The answer is taken out of the target's
/// health as it is: it does not go through armor again.
/// </para>
/// <para>
/// <b>The rolls of a round, in order,</b> on top of the damage pipeline's own:
/// </para>
/// <list type="bullet">
/// <item>When the round starts, <see cref="TurnOrder"/>'s rolls for the ties.</item>
/// <item>
/// On a member's attack, an index roll among the standing enemies, only if the ordered
/// target went down earlier in the round.
/// </item>
/// <item>
/// On an enemy's attack, an index roll among the standing members, always, even when one
/// is left.
/// </item>
/// <item>
/// In a round the party flees, one roll for the escape, after the turn order's and before
/// any turn. It is the only roll an order other than an attack makes.
/// </item>
/// </list>
/// <para>
/// Whoever goes down before its turn loses it. The fight ends the moment a whole side is
/// down, and nobody after that acts.
/// </para>
/// <para>
/// <b>A member who defends is protected from the round's start,</b> so an enemy faster
/// than them is no reason to defend in vain: a <see cref="Defended"/> comes right after
/// <see cref="RoundStarted"/>, for each in the party's order, and their own turn passes
/// with nothing. The defence lasts until the round ends. It rolls nothing.
/// </para>
/// <para>
/// <b>A member who focuses does so on their turn,</b> like an attack, and their attack is
/// raised for the <see cref="CombatTuningValues.FocusRounds"/> rounds after it: the
/// stats the pipeline gets are a copy with <see cref="BaseStats.AttackDamage"/> raised by
/// <see cref="CombatTuningValues.FocusAttackPercent"/>, rounded down, so everything that
/// reads the attack, the critical damage included, sees it raised. Focusing again starts
/// the count again instead of adding to it, and going down loses it. It rolls nothing.
/// </para>
/// <para>
/// <b>A member who meditates does so on their turn,</b> and gets back a share of their most
/// mana (<see cref="Meditated"/>). Only a member below their most can be ordered to
/// (<see cref="CanMeditate"/>): nothing takes mana away during a round, so a meditation
/// ordered at the most would always be wasted, and one whose most is 0 has nothing to get
/// back. It rolls nothing.
/// </para>
/// <para>
/// <b>Fleeing is the party's,</b> a <see cref="FleeOrder"/> that is the round's only
/// order, and only in a fight that can be fled (<see cref="CanFlee"/>). The round starts
/// as any other, and before any turn one roll is made against
/// <see cref="StatFormulas.EscapeRate"/> over those standing on each side. If it comes up
/// the fight ends, <see cref="CombatResult.Fled"/>, and nobody acts. If not, the party
/// loses the round: the members' turns pass with nothing, and the enemies act.
/// </para>
/// </remarks>
public sealed class CombatRunner
{
	private static readonly IReadOnlyList<CombatantId> NoOne = Array.Empty<CombatantId>();

	private readonly List<Combatant> _party;
	private readonly List<Combatant> _enemies;
	private readonly DamagePipeline _pipeline;
	private readonly StatFormulas _formulas;
	private readonly CombatTuningValues _tuning;
	private readonly ICombatRandom _random;
	private readonly List<CombatEvent> _events = new();

	// The round being worked out: its orders, who acts when, and whose turn it is.
	private Dictionary<CombatantId, CombatOrder> _orders = new();
	private IReadOnlyList<Combatant> _turns = Array.Empty<Combatant>();
	private int _turn;

	private CombatRunner(List<Combatant> party, List<Combatant> enemies, bool canFlee, DamagePipeline pipeline, StatFormulas formulas, CombatTuningValues tuning, ICombatRandom random)
	{
		_party = party;
		_enemies = enemies;
		CanFlee = canFlee;
		_pipeline = pipeline;
		_formulas = formulas;
		_tuning = tuning;
		_random = random;
	}

	/// <summary>What the fight is waiting for.</summary>
	public CombatStep Step { get; private set; } = CombatStep.AwaitingOrders;

	/// <summary>How the fight stands. <see cref="CombatResult.Ongoing"/> until <see cref="Step"/> is <see cref="CombatStep.Finished"/>.</summary>
	public CombatResult Result { get; private set; } = CombatResult.Ongoing;

	/// <summary>Whether the party can be ordered to flee this fight. Some groups cannot be fled from.</summary>
	public bool CanFlee { get; }

	/// <summary>The round being ordered or worked out, from 1. It stays at the last one when the fight ends.</summary>
	public int Round { get; private set; } = 1;

	/// <summary>The party, in the order it was given, downed members included.</summary>
	public IReadOnlyList<Combatant> Party => _party;

	/// <summary>The enemies, in the order they were given, downed ones included.</summary>
	public IReadOnlyList<Combatant> Enemies => _enemies;

	/// <summary>
	/// The members who need an order: those standing, in the party's order. Empty unless
	/// <see cref="Step"/> is <see cref="CombatStep.AwaitingOrders"/>.
	/// </summary>
	public IReadOnlyList<CombatantId> MembersToOrder =>
		Step == CombatStep.AwaitingOrders ? Standing(_party).Select(member => member.Id).ToArray() : NoOne;

	/// <summary>The enemy attack waiting for its answer. Null unless <see cref="Step"/> is <see cref="CombatStep.EnemyAttackPending"/>.</summary>
	public PendingEnemyAttack PendingAttack { get; private set; }

	/// <summary>
	/// Whether a member can be ordered to meditate: standing, with less mana than their
	/// most. A most of 0 is never below it.
	/// </summary>
	/// <exception cref="ArgumentException">The member is not a party member in this fight.</exception>
	public bool CanMeditate(CombatantId member)
	{
		if (member.Side != CombatSide.Party || !Exists(member))
			throw new ArgumentException($"{member} is not a party member in this fight.", nameof(member));

		Combatant found = Find(member);
		return !found.IsDown && found.ManaPoints < found.Stats.ManaPoints;
	}

	/// <summary>
	/// How many rounds, from <see cref="Round"/> on and counting it, a combatant's attack is
	/// raised by a focus: 0 when it is not focused. Meant to be read while the fight waits
	/// for orders; during the round a member focused in, that round is counted too.
	/// </summary>
	/// <exception cref="ArgumentException">The combatant is not in this fight.</exception>
	public int FocusRoundsLeft(CombatantId combatant)
	{
		if (!Exists(combatant))
			throw new ArgumentException($"{combatant} is not in this fight.", nameof(combatant));

		return Math.Max(0, Find(combatant).FocusedThrough - Round + 1);
	}

	/// <summary>
	/// Sets a fight up and waits for the first round's orders. Nothing is rolled yet.
	/// </summary>
	/// <remarks>
	/// The party has from one member to <see cref="PartyState.MaximumSize"/>, there is at
	/// least one enemy, and each side has someone standing. How many enemies fit is not
	/// decided here.
	/// </remarks>
	/// <param name="canFlee">
	/// Whether the party may flee. Asked of every caller, never defaulted, so a group that
	/// cannot be fled from cannot be fled from by a caller's omission.
	/// </param>
	public static CombatRunner Start(
		IReadOnlyList<CombatantSetup> party,
		IReadOnlyList<CombatantSetup> enemies,
		bool canFlee,
		StatFormulas formulas,
		CombatTuningValues tuning,
		ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(party);
		ArgumentNullException.ThrowIfNull(enemies);
		ArgumentNullException.ThrowIfNull(random);

		var pipeline = new DamagePipeline(formulas, tuning);

		if (party.Count < 1 || party.Count > PartyState.MaximumSize)
			throw new ArgumentException($"A fight needs from 1 to {PartyState.MaximumSize} party members, and {party.Count} were given.", nameof(party));

		if (enemies.Count < 1)
			throw new ArgumentException("A fight needs at least one enemy.", nameof(enemies));

		List<Combatant> members = Enter(party, CombatSide.Party, nameof(party));
		List<Combatant> foes = Enter(enemies, CombatSide.Enemy, nameof(enemies));

		if (members.All(member => member.IsDown))
			throw new ArgumentException("Nobody in the party is standing.", nameof(party));

		if (foes.All(foe => foe.IsDown))
			throw new ArgumentException("No enemy is standing.", nameof(enemies));

		return new CombatRunner(members, foes, canFlee, pipeline, formulas, tuning, random);
	}

	/// <summary>
	/// Takes the round's orders, one for each of <see cref="MembersToOrder"/>, and works
	/// the round out up to the next stop.
	/// </summary>
	/// <remarks>
	/// Orders that do not fit are refused with <see cref="ArgumentException"/> before
	/// anything changes or is rolled: one missing, one for somebody who is down or is not
	/// a member, two for the same member, a kind of order the fight does not know, an
	/// attack whose target is not an enemy or is already down, a meditation for a member
	/// who cannot meditate, and a flee that does not come alone or in a fight that cannot
	/// be fled. A flee is the one order that leaves the other members without one.
	/// </remarks>
	public void SubmitOrders(IEnumerable<CombatOrder> orders)
	{
		ArgumentNullException.ThrowIfNull(orders);
		Require(CombatStep.AwaitingOrders, "take orders");

		Dictionary<CombatantId, CombatOrder> checkedOrders = Check(orders);

		_orders = checkedOrders;
		_turns = TurnOrder.For(_party.Concat(_enemies), _random);
		_turn = 0;
		_events.Add(new RoundStarted(Round, _turns.Select(combatant => combatant.Id).ToArray()));

		foreach (Combatant member in Standing(_party).Where(Defends))
			_events.Add(new Defended(member.Id));

		// Check lets a flee through only as the round's one order.
		if (_orders.Values.Any(order => order is FleeOrder) && TryToFlee())
			return;

		Continue();
	}

	/// <summary>
	/// Takes <paramref name="damageTaken"/> out of the health of
	/// <see cref="PendingAttack"/>'s target, as it is, and goes on to the next stop.
	/// </summary>
	public void AnswerEnemyAttack(int damageTaken)
	{
		Require(CombatStep.EnemyAttackPending, "take the answer to an enemy attack");

		if (damageTaken < 0)
			throw new ArgumentOutOfRangeException(nameof(damageTaken), damageTaken, "The damage taken cannot be negative.");

		PendingEnemyAttack attack = PendingAttack;
		PendingAttack = null;

		Apply(attack.Attacker, Find(attack.Target), attack.Result, damageTaken);

		if (Step == CombatStep.Finished)
			return;

		_turn++;
		Continue();
	}

	/// <summary>Hands over what happened since the last call, in order, and forgets it.</summary>
	public IReadOnlyList<CombatEvent> TakeEvents()
	{
		CombatEvent[] taken = _events.ToArray();
		_events.Clear();
		return taken;
	}

	private static List<Combatant> Enter(IReadOnlyList<CombatantSetup> setups, CombatSide side, string name)
	{
		var combatants = new List<Combatant>();

		for (int position = 0; position < setups.Count; position++)
		{
			if (setups[position] == null)
				throw new ArgumentException($"The combatant at position {position} is missing.", name);

			combatants.Add(new Combatant(new CombatantId(side, position), setups[position]));
		}

		return combatants;
	}

	private static List<Combatant> Standing(List<Combatant> side) =>
		side.Where(combatant => !combatant.IsDown).ToList();

	private void Require(CombatStep step, string action)
	{
		if (Step != step)
			throw new InvalidOperationException($"The fight cannot {action} now: it is at {Step}.");
	}

	private Dictionary<CombatantId, CombatOrder> Check(IEnumerable<CombatOrder> orders)
	{
		var checkedOrders = new Dictionary<CombatantId, CombatOrder>();

		foreach (CombatOrder order in orders)
		{
			if (order == null)
				throw new ArgumentException("An order is missing.", nameof(orders));

			if (order.Actor.Side != CombatSide.Party || !Exists(order.Actor))
				throw new ArgumentException($"An order is for {order.Actor}, who is not a party member in this fight.", nameof(orders));

			if (Find(order.Actor).IsDown)
				throw new ArgumentException($"An order is for {order.Actor}, who is down.", nameof(orders));

			switch (order)
			{
				case AttackOrder attack:
					if (attack.Target.Side != CombatSide.Enemy || !Exists(attack.Target))
						throw new ArgumentException($"{order.Actor} is ordered to attack {attack.Target}, who is not an enemy in this fight.", nameof(orders));

					if (Find(attack.Target).IsDown)
						throw new ArgumentException($"{order.Actor} is ordered to attack {attack.Target}, who is already down.", nameof(orders));

					break;

				case DefendOrder or FocusOrder:
					break;

				case MeditateOrder when !CanMeditate(order.Actor):
					throw new ArgumentException($"{order.Actor} is ordered to meditate with their mana at its most.", nameof(orders));

				case MeditateOrder:
					break;

				case FleeOrder when !CanFlee:
					throw new ArgumentException($"{order.Actor} is ordered to flee a fight that cannot be fled.", nameof(orders));

				case FleeOrder:
					break;

				default:
					throw new ArgumentException($"The fight does not know the kind of order given to {order.Actor}, {order.GetType().Name}.", nameof(orders));
			}

			if (!checkedOrders.TryAdd(order.Actor, order))
				throw new ArgumentException($"{order.Actor} was given two orders.", nameof(orders));
		}

		if (checkedOrders.Values.Any(order => order is FleeOrder))
		{
			if (checkedOrders.Count > 1)
				throw new ArgumentException("A flee is the whole party's order, and comes alone.", nameof(orders));

			return checkedOrders;
		}

		foreach (Combatant member in Standing(_party))
		{
			if (!checkedOrders.ContainsKey(member.Id))
				throw new ArgumentException($"{member.Id} was given no order.", nameof(orders));
		}

		return checkedOrders;
	}

	/// <summary>Plays turns from the current one until the fight needs something from outside.</summary>
	private void Continue()
	{
		while (_turn < _turns.Count)
		{
			Combatant actor = _turns[_turn];

			if (actor.IsDown)
			{
				_turn++;
				continue;
			}

			if (actor.Id.Side == CombatSide.Enemy)
			{
				Combatant victim = PickStanding(_party);
				AttackResult worked = _pipeline.Resolve(actor.Stats, victim.Stats, ActionKind.Attack, _random, defending: Defends(victim));

				PendingAttack = new PendingEnemyAttack(actor.Id, victim.Id, worked);
				Step = CombatStep.EnemyAttackPending;
				return;
			}

			// In a round the party failed to flee, the members have no order of their own.
			switch (_orders.GetValueOrDefault(actor.Id))
			{
				case AttackOrder order:
					Attack(actor, order);
					break;

				// Defending acted when the round started.
				case DefendOrder:
					break;

				case FocusOrder:
					actor.FocusThrough(Round + _tuning.FocusRounds);
					_events.Add(new Focused(actor.Id, _tuning.FocusRounds));
					break;

				case MeditateOrder:
					Meditate(actor);
					break;
			}

			if (Step == CombatStep.Finished)
				return;

			_turn++;
		}

		Round++;
		Step = CombatStep.AwaitingOrders;
	}

	/// <summary>A member's attack, on the ordered target or, if it went down, on another standing enemy.</summary>
	private void Attack(Combatant actor, AttackOrder order)
	{
		Combatant target = Find(order.Target);

		if (target.IsDown)
			target = PickStanding(_enemies);

		// Enemies only attack: none of them defends.
		AttackResult result = _pipeline.Resolve(AttackingStats(actor), target.Stats, ActionKind.Attack, _random, defending: false);
		Apply(actor.Id, target, result, result.Damage);
	}

	/// <summary>
	/// The party's attempt to flee, before any turn: one roll against the escape rate of
	/// those standing on each side. Whether it ended the fight.
	/// </summary>
	private bool TryToFlee()
	{
		BaseStats[] allies = Standing(_party).Select(member => member.Stats).ToArray();
		BaseStats[] foes = Standing(_enemies).Select(foe => foe.Stats).ToArray();
		bool escaped = _random.Percent() < _formulas.EscapeRate(allies, foes);

		_events.Add(new EscapeAttempted(escaped));

		if (!escaped)
			return false;

		Result = CombatResult.Fled;
		Step = CombatStep.Finished;
		_events.Add(new FightEnded(Result));
		return true;
	}

	/// <summary>
	/// A member's meditation: the tuning's share of the most mana, rounded down and never
	/// below its minimum, up to the most. In <c>long</c>, so the largest most cannot overflow.
	/// </summary>
	private void Meditate(Combatant actor)
	{
		int most = actor.Stats.ManaPoints;
		long share = Math.Max(_tuning.MeditateMinimum, (long)most * _tuning.MeditatePercent / 100);
		int recovered = (int)Math.Min(share, most - actor.ManaPoints);

		actor.RecoverMana(recovered);
		_events.Add(new Meditated(actor.Id, recovered, actor.ManaPoints));
	}

	/// <summary>
	/// The stats a member attacks with: their own, with the attack raised while a focus
	/// lasts. In <c>long</c>, so a huge attack stops at the largest int instead of overflowing.
	/// </summary>
	private BaseStats AttackingStats(Combatant actor)
	{
		BaseStats stats = actor.Stats;

		if (Round > actor.FocusedThrough)
			return stats;

		long raised = stats.AttackDamage * (100L + _tuning.FocusAttackPercent) / 100;

		return new BaseStats(stats.HealthPoints, (int)Math.Min(raised, int.MaxValue), stats.Armor, stats.ManaPoints, stats.Agility, stats.Luck, stats.HitRate);
	}

	/// <summary>Whether a member defends in the round being worked out.</summary>
	private bool Defends(Combatant member) =>
		_orders.TryGetValue(member.Id, out CombatOrder order) && order is DefendOrder;

	/// <summary>One of the side's standing combatants, by a roll that is made even when one is left.</summary>
	private Combatant PickStanding(List<Combatant> side)
	{
		List<Combatant> standing = Standing(side);
		return standing[_random.Index(standing.Count)];
	}

	private void Apply(CombatantId attacker, Combatant target, AttackResult result, int damageTaken)
	{
		target.TakeDamage(damageTaken);
		_events.Add(new AttackResolved(attacker, target.Id, result, damageTaken, target.HealthPoints));

		if (!target.IsDown)
			return;

		_events.Add(new CombatantDowned(target.Id));

		List<Combatant> side = target.Id.Side == CombatSide.Party ? _party : _enemies;

		if (side.Any(combatant => !combatant.IsDown))
			return;

		Result = target.Id.Side == CombatSide.Party ? CombatResult.Lost : CombatResult.Won;
		Step = CombatStep.Finished;
		_events.Add(new FightEnded(Result));
	}

	private bool Exists(CombatantId id) =>
		id.Position < (id.Side == CombatSide.Party ? _party : _enemies).Count;

	private Combatant Find(CombatantId id) =>
		(id.Side == CombatSide.Party ? _party : _enemies)[id.Position];
}
