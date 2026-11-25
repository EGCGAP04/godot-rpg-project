using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>The rolls a fight's tests write over and over, by name.</summary>
internal static class CombatRolls
{
	public const double Max = 99.999999;

	/// <summary>The four rolls of a plain hit: it lands, is not evaded, is not critical, and does not vary.</summary>
	public static ScriptedCombatRandom Hit(this ScriptedCombatRandom random) =>
		random.Percent(0).Percent(Max).Percent(Max).Percent(50);

	/// <summary>The one roll of a miss, for the 90 hit rate every fighter here has.</summary>
	public static ScriptedCombatRandom Miss(this ScriptedCombatRandom random) => random.Percent(95);

	/// <summary>The index roll that picks a target among those standing.</summary>
	public static ScriptedCombatRandom Target(this ScriptedCombatRandom random, int index) => random.Index(index);

	/// <summary>The same roll, required to be over exactly <paramref name="among"/> standing.</summary>
	public static ScriptedCombatRandom Target(this ScriptedCombatRandom random, int index, int among) => random.Index(index, among);

	/// <summary>The rolls of an enemy's attack that misses the only member standing.</summary>
	public static ScriptedCombatRandom EnemyMisses(this ScriptedCombatRandom random) => random.Target(0, among: 1).Miss();
}

/// <summary>
/// Exercises <see cref="CombatRunner"/> with scripted rolls. Fighters have no luck and,
/// unless a test says so, no armor, so a plain hit does the attacker's attack damage.
/// Every test requires its script to be used up, so the rolls a round takes are checked
/// along with what it does.
/// </summary>
public class CombatRunnerTests
{
	private static CombatantId P(int position) => CombatantId.Party(position);

	private static CombatantId E(int position) => CombatantId.Enemy(position);

	private static AttackOrder Attack(int member, int enemy) => new(P(member), E(enemy));

	private static DefendOrder Defend(int member) => new(P(member));

	private static FocusOrder Focus(int member) => new(P(member));

	private static MeditateOrder Meditate(int member) => new(P(member));

	private static FleeOrder Flee(int member) => new(P(member));

	/// <summary>A member with mana: a most of <paramref name="mostMana"/>, and <paramref name="manaLeft"/> of it now.</summary>
	private static CombatantSetup Mage(int mostMana, int manaLeft, int agility = 30, int healthPoints = 100) =>
		new(
			"speaker.someone",
			TestTuning.Stats(healthPoints: healthPoints, attackDamage: 10, manaPoints: mostMana, agility: agility, hitRate: 90),
			healthPoints,
			manaLeft);

	private static CombatantSetup Fighter(int healthPoints, int attackDamage, int agility, int armor = 0, int? healthLeft = null) =>
		new(
			"speaker.someone",
			TestTuning.Stats(healthPoints: healthPoints, attackDamage: attackDamage, armor: armor, agility: agility, hitRate: 90),
			healthLeft ?? healthPoints,
			0);

	private static CombatRunner Start(ScriptedCombatRandom random, CombatantSetup[] party, CombatantSetup[] enemies, CombatTuningValues tuning = null, bool canFlee = true) =>
		CombatRunner.Start(party, enemies, canFlee, TestTuning.Formulas(), tuning ?? TestTuning.Combat(), random);

	/// <summary>Submits the orders, answers every enemy attack as worked out, and hands over the round's events.</summary>
	private static IReadOnlyList<CombatEvent> PlayRound(CombatRunner runner, params CombatOrder[] orders)
	{
		runner.SubmitOrders(orders);
		AnswerAsWorkedOut(runner);
		return runner.TakeEvents();
	}

	/// <summary>The damage the member at <paramref name="member"/> did in a round's events.</summary>
	private static int[] DamageBy(int member, IReadOnlyList<CombatEvent> events) =>
		events.OfType<AttackResolved>().Where(attack => attack.Attacker == P(member)).Select(attack => attack.DamageTaken).ToArray();

	/// <summary>Answers every enemy attack of the round with the damage the pipeline worked out.</summary>
	private static void AnswerAsWorkedOut(CombatRunner runner)
	{
		while (runner.Step == CombatStep.EnemyAttackPending)
			runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);
	}

	private static int[] Health(IReadOnlyList<Combatant> side) => side.Select(combatant => combatant.HealthPoints).ToArray();

	// --- Starting ---

	[Fact]
	public void Start_WaitsForTheFirstRoundsOrders_WithoutRolling()
	{
		var random = new ScriptedCombatRandom();

		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 20), Fighter(20, 5, 10, healthLeft: 7) },
			new[] { Fighter(25, 7, 15) });

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(CombatResult.Ongoing, runner.Result);
		Assert.Equal(1, runner.Round);
		Assert.Null(runner.PendingAttack);
		Assert.Empty(runner.TakeEvents());
		Assert.Equal(new[] { P(0), P(1) }, runner.Party.Select(member => member.Id));
		Assert.Equal(new[] { E(0) }, runner.Enemies.Select(enemy => enemy.Id));
		Assert.Equal(new[] { 30, 7 }, Health(runner.Party));
		Assert.Equal(new[] { 25 }, Health(runner.Enemies));
		Assert.Equal(new[] { P(0), P(1) }, runner.MembersToOrder);
	}

	[Fact]
	public void Start_AcceptsFourMembers_AndSomeoneWhoComesInDown()
	{
		CombatRunner runner = Start(
			new ScriptedCombatRandom(),
			new[] { Fighter(30, 10, 20), Fighter(30, 10, 20, healthLeft: 0), Fighter(30, 10, 20), Fighter(30, 10, 20) },
			new[] { Fighter(25, 7, 15, healthLeft: 0), Fighter(25, 7, 15) });

		Assert.Equal(new[] { P(0), P(2), P(3) }, runner.MembersToOrder);
		Assert.True(runner.Party[1].IsDown);
		Assert.True(runner.Enemies[0].IsDown);
	}

	[Fact]
	public void Start_RefusesAFightThatCannotBeFought()
	{
		var random = new ScriptedCombatRandom();
		CombatantSetup[] one = { Fighter(30, 10, 20) };
		CombatantSetup[] five = Enumerable.Repeat(Fighter(30, 10, 20), 5).ToArray();
		CombatantSetup[] none = Array.Empty<CombatantSetup>();
		CombatantSetup[] allDown = { Fighter(30, 10, 20, healthLeft: 0), Fighter(30, 10, 20, healthLeft: 0) };

		Assert.Throws<ArgumentException>(() => Start(random, none, one));
		Assert.Throws<ArgumentException>(() => Start(random, five, one));
		Assert.Throws<ArgumentException>(() => Start(random, one, none));
		Assert.Throws<ArgumentException>(() => Start(random, allDown, one));
		Assert.Throws<ArgumentException>(() => Start(random, one, allDown));
		Assert.Throws<ArgumentException>(() => Start(random, new[] { one[0], null }, one));
		Assert.Throws<ArgumentException>(() => Start(random, one, new[] { one[0], null }));
	}

	[Fact]
	public void Start_RefusesAMissingPart()
	{
		var random = new ScriptedCombatRandom();
		CombatantSetup[] one = { Fighter(30, 10, 20) };

		Assert.Throws<ArgumentNullException>(() => CombatRunner.Start(null, one, true, TestTuning.Formulas(), TestTuning.Combat(), random));
		Assert.Throws<ArgumentNullException>(() => CombatRunner.Start(one, null, true, TestTuning.Formulas(), TestTuning.Combat(), random));
		Assert.Throws<ArgumentNullException>(() => CombatRunner.Start(one, one, true, null, TestTuning.Combat(), random));
		Assert.Throws<ArgumentNullException>(() => CombatRunner.Start(one, one, true, TestTuning.Formulas(), null, random));
		Assert.Throws<ArgumentNullException>(() => CombatRunner.Start(one, one, true, TestTuning.Formulas(), TestTuning.Combat(), null));
	}

	// --- A round ---

	[Fact]
	public void ARound_PlaysTheMembersAttack_ThenStopsOnTheEnemys_UntilItIsAnswered()
	{
		var random = new ScriptedCombatRandom().Hit().Target(0, among: 1).Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 20) }, new[] { Fighter(25, 7, 10) });

		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.Equal(new PendingEnemyAttack(E(0), P(0), AttackResult.Hit(7, isCritical: false)), runner.PendingAttack);
		Assert.Equal(new[] { 15 }, Health(runner.Enemies));
		Assert.Equal(new[] { 30 }, Health(runner.Party));
		Assert.Equal(1, runner.Round);
		Assert.Empty(runner.MembersToOrder);

		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.Equal(2, events.Count);
		var started = Assert.IsType<RoundStarted>(events[0]);
		Assert.Equal(1, started.Round);
		Assert.Equal(new[] { P(0), E(0) }, started.TurnOrder);
		Assert.Equal(new AttackResolved(P(0), E(0), AttackResult.Hit(10, isCritical: false), 10, 15), events[1]);

		runner.AnswerEnemyAttack(7);

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(CombatResult.Ongoing, runner.Result);
		Assert.Equal(2, runner.Round);
		Assert.Null(runner.PendingAttack);
		Assert.Equal(new[] { 23 }, Health(runner.Party));
		Assert.Equal(new[] { P(0) }, runner.MembersToOrder);
		Assert.Equal(
			new CombatEvent[] { new AttackResolved(E(0), P(0), AttackResult.Hit(7, isCritical: false), 7, 23) },
			runner.TakeEvents());
		random.RequireUsedUp();
	}

	[Fact]
	public void AFasterEnemy_StopsTheRound_BeforeTheMemberActs()
	{
		var random = new ScriptedCombatRandom().Target(0).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.Equal(new[] { 25 }, Health(runner.Enemies));
		Assert.Single(runner.TakeEvents());

		runner.AnswerEnemyAttack(7);

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(new[] { 15 }, Health(runner.Enemies));
		Assert.Equal(new[] { 23 }, Health(runner.Party));
		random.RequireUsedUp();
	}

	[Fact]
	public void ATieInAgility_IsRolledFor_WhenTheRoundStarts()
	{
		// Index 1 of Party 0, Enemy 0 puts the enemy first.
		var random = new ScriptedCombatRandom().Index(1).Target(0).Miss().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 20) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0) });

		var started = Assert.IsType<RoundStarted>(runner.TakeEvents()[0]);
		Assert.Equal(new[] { E(0), P(0) }, started.TurnOrder);
		Assert.Equal(E(0), runner.PendingAttack.Attacker);

		runner.AnswerEnemyAttack(0);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheTurnOrder_IsWorkedOutAgain_EveryRound()
	{
		var random = new ScriptedCombatRandom()
			.Index(0).Hit().Target(0).Miss()
			.Index(1).Target(0).Miss().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 20) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0) });
		runner.AnswerEnemyAttack(0);
		runner.SubmitOrders(new[] { Attack(0, 0) });
		runner.AnswerEnemyAttack(0);

		RoundStarted[] rounds = runner.TakeEvents().OfType<RoundStarted>().ToArray();
		Assert.Equal(new[] { 1, 2 }, rounds.Select(round => round.Round));
		Assert.Equal(new[] { P(0), E(0) }, rounds[0].TurnOrder);
		Assert.Equal(new[] { E(0), P(0) }, rounds[1].TurnOrder);
		Assert.Equal(3, runner.Round);
		random.RequireUsedUp();
	}

	// --- The enemy's step ---

	[Fact]
	public void AnEnemyAttackThatMisses_StillStops()
	{
		var random = new ScriptedCombatRandom().Target(0).Miss().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.Equal(new PendingEnemyAttack(E(0), P(0), AttackResult.Missed), runner.PendingAttack);

		runner.AnswerEnemyAttack(0);

		Assert.Equal(new[] { 30 }, Health(runner.Party));
		Assert.Contains(new AttackResolved(E(0), P(0), AttackResult.Missed, 0, 30), runner.TakeEvents());
		random.RequireUsedUp();
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	public void AnEnemy_PicksItsTarget_ByARollAmongTheMembers(int roll, int expectedMember)
	{
		var random = new ScriptedCombatRandom().Target(roll).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 10), Fighter(30, 10, 9), Fighter(30, 10, 8) },
			new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0), Attack(2, 0) });

		Assert.Equal(P(expectedMember), runner.PendingAttack.Target);
		random.RequireUsedUp();
	}

	[Fact]
	public void AnEnemy_PicksOnlyAmongTheMembersStanding()
	{
		// Party 1 is down, so the roll is over two, and index 1 of them is Party 2.
		var random = new ScriptedCombatRandom().Target(1, among: 2).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 10), Fighter(30, 10, 9, healthLeft: 0), Fighter(30, 10, 8) },
			new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(2, 0) });

		Assert.Equal(P(2), runner.PendingAttack.Target);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheAnswer_IsTakenOutOfTheHealthAsItIs_WithoutArmorASecondTime()
	{
		// 100 armor takes 45% off the 20: the pipeline says 11, and 11 is what comes off.
		var random = new ScriptedCombatRandom().Target(0).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10, armor: 100) }, new[] { Fighter(25, 20, 20) });
		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(11, runner.PendingAttack.Result.Damage);
		runner.AnswerEnemyAttack(11);

		Assert.Equal(19, runner.Party[0].HealthPoints);
		random.RequireUsedUp();
	}

	[Theory]
	[InlineData(0, 30)]
	[InlineData(5, 25)]
	[InlineData(29, 1)]
	public void AnotherAnswer_ThanTheOneWorkedOut_IsWhatCounts(int answer, int healthLeft)
	{
		var random = new ScriptedCombatRandom().Target(0).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 20, 20) });
		runner.SubmitOrders(new[] { Attack(0, 0) });
		runner.TakeEvents();

		runner.AnswerEnemyAttack(answer);

		Assert.Equal(healthLeft, runner.Party[0].HealthPoints);
		Assert.Equal(new AttackResolved(E(0), P(0), AttackResult.Hit(20, isCritical: false), answer, healthLeft), runner.TakeEvents()[0]);
		random.RequireUsedUp();
	}

	[Fact]
	public void AnAnswer_CanCountOnAnAttackThatMissed()
	{
		var random = new ScriptedCombatRandom().Target(0).Miss().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 20, 20) });
		runner.SubmitOrders(new[] { Attack(0, 0) });

		runner.AnswerEnemyAttack(4);

		Assert.Equal(26, runner.Party[0].HealthPoints);
	}

	[Fact]
	public void ANegativeAnswer_IsRefused_AndTheAttackStaysPending()
	{
		var random = new ScriptedCombatRandom().Target(0).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 20, 20) });
		runner.SubmitOrders(new[] { Attack(0, 0) });
		PendingEnemyAttack pending = runner.PendingAttack;
		runner.TakeEvents();

		Assert.Throws<ArgumentOutOfRangeException>(() => runner.AnswerEnemyAttack(-1));

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.Equal(pending, runner.PendingAttack);
		Assert.Equal(30, runner.Party[0].HealthPoints);
		Assert.Empty(runner.TakeEvents());

		runner.AnswerEnemyAttack(20);
		Assert.Equal(10, runner.Party[0].HealthPoints);
		random.RequireUsedUp();
	}

	[Fact]
	public void AnAnswer_WithNoAttackPending_IsRefused()
	{
		CombatRunner runner = Start(new ScriptedCombatRandom(), new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 20, 20) });

		Assert.Throws<InvalidOperationException>(() => runner.AnswerEnemyAttack(3));

		Assert.Equal(30, runner.Party[0].HealthPoints);
	}

	// --- Going down ---

	[Fact]
	public void AMemberDownedBeforeTheirTurn_LosesIt()
	{
		// The enemy is first and takes Party 1 down; only Party 0 attacks after it.
		var random = new ScriptedCombatRandom().Target(1).Hit().Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 10), Fighter(15, 10, 5) },
			new[] { Fighter(40, 20, 20) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });
		runner.AnswerEnemyAttack(20);

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(new[] { 30, 0 }, Health(runner.Party));
		Assert.Equal(new[] { 30 }, Health(runner.Enemies));
		Assert.Equal(new[] { P(0) }, runner.MembersToOrder);
		Assert.Equal(
			new CombatEvent[]
			{
				new AttackResolved(E(0), P(1), AttackResult.Hit(20, isCritical: false), 20, 0),
				new CombatantDowned(P(1)),
				new AttackResolved(P(0), E(0), AttackResult.Hit(10, isCritical: false), 10, 30)
			},
			runner.TakeEvents().Skip(1));
		random.RequireUsedUp();
	}

	[Fact]
	public void AnEnemyDownedBeforeItsTurn_LosesIt_AndTheRoundDoesNotStopForIt()
	{
		var random = new ScriptedCombatRandom().Hit().Target(0).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 50, 30) },
			new[] { Fighter(10, 7, 20), Fighter(10, 7, 10) });

		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(E(1), runner.PendingAttack.Attacker);
		runner.AnswerEnemyAttack(0);
		Assert.Equal(new[] { 0, 10 }, Health(runner.Enemies));
		random.RequireUsedUp();
	}

	[Fact]
	public void AnAttackWhoseTargetWentDown_MovesToAStandingEnemy_ByARoll()
	{
		// Both attack Enemy 0 and Party 0 takes it down. Party 1's roll of 1 is over
		// Enemy 1, Enemy 2.
		var random = new ScriptedCombatRandom()
			.Hit()
			.Target(1, among: 2).Hit()
			.Target(0, among: 2).Miss()
			.Target(1, among: 2).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 50, 30), Fighter(30, 5, 20) },
			new[] { Fighter(10, 7, 1), Fighter(10, 7, 2), Fighter(10, 7, 3) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });
		AnswerAsWorkedOut(runner);

		Assert.Equal(new[] { 0, 10, 5 }, Health(runner.Enemies));
		Assert.Contains(new AttackResolved(P(1), E(2), AttackResult.Hit(5, isCritical: false), 5, 5), runner.TakeEvents());
		random.RequireUsedUp();
	}

	[Fact]
	public void AnAttackWhoseTargetWentDown_StillRolls_WhenOneEnemyIsLeft()
	{
		var random = new ScriptedCombatRandom().Hit().Target(0, among: 1).Hit().Target(0, among: 2).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 50, 30), Fighter(30, 5, 20) },
			new[] { Fighter(10, 7, 1), Fighter(10, 7, 2) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });
		AnswerAsWorkedOut(runner);

		Assert.Equal(new[] { 0, 5 }, Health(runner.Enemies));
		random.RequireUsedUp();
	}

	[Fact]
	public void AnAttackOnAStandingTarget_TakesNoRollToPickIt()
	{
		var random = new ScriptedCombatRandom().Hit().Hit().Target(0).Miss().Target(0).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 3, 30), Fighter(30, 5, 20) },
			new[] { Fighter(10, 7, 1), Fighter(10, 7, 2) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });
		AnswerAsWorkedOut(runner);

		Assert.Equal(new[] { 2, 10 }, Health(runner.Enemies));
		random.RequireUsedUp();
	}

	// --- Ending ---

	[Fact]
	public void TheFightIsWon_TheMomentTheLastEnemyGoesDown_AndNobodyElseActs()
	{
		// Party 1 is slower and never attacks: its rolls are not in the script.
		var random = new ScriptedCombatRandom().Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 50, 30), Fighter(30, 5, 20) },
			new[] { Fighter(10, 7, 25) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });

		Assert.Equal(CombatStep.Finished, runner.Step);
		Assert.Equal(CombatResult.Won, runner.Result);
		Assert.Equal(1, runner.Round);
		Assert.Null(runner.PendingAttack);
		Assert.Empty(runner.MembersToOrder);
		Assert.Equal(
			new CombatEvent[]
			{
				new AttackResolved(P(0), E(0), AttackResult.Hit(50, isCritical: false), 50, 0),
				new CombatantDowned(E(0)),
				new FightEnded(CombatResult.Won)
			},
			runner.TakeEvents().Skip(1));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheFightIsLost_TheMomentTheWholePartyIsDown_AndNobodyElseActs()
	{
		// Two enemies take the two members down; the third enemy and the members never act.
		var random = new ScriptedCombatRandom().Target(0).Hit().Target(0).Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(10, 5, 3), Fighter(10, 5, 2) },
			new[] { Fighter(10, 20, 50), Fighter(10, 20, 40), Fighter(10, 20, 30) });

		runner.SubmitOrders(new[] { Attack(0, 0), Attack(1, 0) });
		Assert.Equal(P(0), runner.PendingAttack.Target);
		runner.AnswerEnemyAttack(20);
		Assert.Equal(CombatResult.Ongoing, runner.Result);
		Assert.Equal(P(1), runner.PendingAttack.Target);
		runner.AnswerEnemyAttack(20);

		Assert.Equal(CombatStep.Finished, runner.Step);
		Assert.Equal(CombatResult.Lost, runner.Result);
		Assert.Null(runner.PendingAttack);
		Assert.Equal(new[] { 0, 0 }, Health(runner.Party));
		Assert.Equal(new[] { 10, 10, 10 }, Health(runner.Enemies));
		Assert.Equal(
			new CombatEvent[]
			{
				new AttackResolved(E(0), P(0), AttackResult.Hit(20, isCritical: false), 20, 0),
				new CombatantDowned(P(0)),
				new AttackResolved(E(1), P(1), AttackResult.Hit(20, isCritical: false), 20, 0),
				new CombatantDowned(P(1)),
				new FightEnded(CombatResult.Lost)
			},
			runner.TakeEvents().Skip(1));
		random.RequireUsedUp();
	}

	[Fact]
	public void AFinishedFight_TakesNothingMore()
	{
		var random = new ScriptedCombatRandom().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 50, 30) }, new[] { Fighter(10, 7, 25) });
		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Throws<InvalidOperationException>(() => runner.SubmitOrders(new[] { Attack(0, 0) }));
		Assert.Throws<InvalidOperationException>(() => runner.AnswerEnemyAttack(0));
		Assert.Equal(CombatResult.Won, runner.Result);
	}

	[Fact]
	public void TakeEvents_HandsThemOverOnce()
	{
		var random = new ScriptedCombatRandom().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 50, 30) }, new[] { Fighter(10, 7, 25) });
		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Equal(4, runner.TakeEvents().Count);
		Assert.Empty(runner.TakeEvents());
	}

	// --- Defending ---

	[Fact]
	public void TheDefence_HoldsFromTheRoundsStart_BeforeAFasterEnemyActs()
	{
		var random = new ScriptedCombatRandom().Target(0, among: 1).Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 20, 20) });

		runner.SubmitOrders(new[] { Defend(0) });

		Assert.Equal(new PendingEnemyAttack(E(0), P(0), AttackResult.Hit(10, isCritical: false)), runner.PendingAttack);

		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.Equal(2, events.Count);
		Assert.IsType<RoundStarted>(events[0]);
		Assert.Equal(new Defended(P(0)), events[1]);

		runner.AnswerEnemyAttack(10);
		Assert.Equal(new[] { 20 }, Health(runner.Party));
		random.RequireUsedUp();
	}

	[Fact]
	public void ADefender_TakesNoTurn_AndRollsNothing()
	{
		// The member is the faster: had their turn done anything, it would come before the
		// enemy's attack, and the script holds only the enemy's rolls.
		var random = new ScriptedCombatRandom().Target(0, among: 1).Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 30) }, new[] { Fighter(25, 20, 20) });

		runner.SubmitOrders(new[] { Defend(0) });

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.Equal(2, runner.TakeEvents().Count);
		Assert.Equal(new[] { 25 }, Health(runner.Enemies));

		runner.AnswerEnemyAttack(10);
		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.DoesNotContain(runner.TakeEvents(), happened => happened is AttackResolved attack && attack.Attacker == P(0));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheDefences_ComeInThePartysOrder_RightAfterTheRoundStarts()
	{
		// The fastest member attacks first, yet both defences are already out.
		var random = new ScriptedCombatRandom().Hit().Target(0, among: 3).Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 10), Fighter(30, 10, 30), Fighter(30, 10, 20) },
			new[] { Fighter(50, 20, 5) });

		runner.SubmitOrders(new CombatOrder[] { Defend(2), Attack(1, 0), Defend(0) });

		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.IsType<RoundStarted>(events[0]);
		Assert.Equal(new Defended(P(0)), events[1]);
		Assert.Equal(new Defended(P(2)), events[2]);
		Assert.Equal(new AttackResolved(P(1), E(0), AttackResult.Hit(10, isCritical: false), 10, 40), events[3]);
		Assert.Equal(4, events.Count);

		Assert.Equal(AttackResult.Hit(10, isCritical: false), runner.PendingAttack.Result);
		runner.AnswerEnemyAttack(10);
		random.RequireUsedUp();
	}

	[Fact]
	public void OnlyTheMemberWhoDefends_TakesLess()
	{
		var random = new ScriptedCombatRandom().Target(1, among: 2).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10), Fighter(30, 10, 9) }, new[] { Fighter(100, 20, 20) });

		runner.SubmitOrders(new CombatOrder[] { Defend(0), Attack(1, 0) });

		Assert.Equal(new PendingEnemyAttack(E(0), P(1), AttackResult.Hit(20, isCritical: false)), runner.PendingAttack);
		runner.AnswerEnemyAttack(20);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheDefence_LastsOnlyItsRound()
	{
		var random = new ScriptedCombatRandom()
			.Target(0, among: 1).Hit()
			.Target(0, among: 1).Hit().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(60, 10, 10) }, new[] { Fighter(100, 20, 20) });

		runner.SubmitOrders(new[] { Defend(0) });
		Assert.Equal(10, runner.PendingAttack.Result.Damage);
		runner.AnswerEnemyAttack(10);

		runner.SubmitOrders(new[] { Attack(0, 0) });
		Assert.Equal(20, runner.PendingAttack.Result.Damage);
		runner.AnswerEnemyAttack(20);

		Assert.Equal(new[] { 30 }, Health(runner.Party));
		random.RequireUsedUp();
	}

	// --- Focusing ---

	[Fact]
	public void AFocus_RaisesTheAttack_ForTheRoundsAfterIt_AndThenEnds()
	{
		// The member acts first and the enemy always misses. Focusing rolls nothing: round 1
		// takes only the enemy's rolls.
		var random = new ScriptedCombatRandom().EnemyMisses();
		for (int round = 2; round <= 5; round++)
			random.Hit().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Fighter(100, 10, 30) }, new[] { Fighter(1000, 1, 1) });
		Assert.Equal(0, runner.FocusRoundsLeft(P(0)));

		IReadOnlyList<CombatEvent> focusing = PlayRound(runner, Focus(0));
		Assert.Contains(new Focused(P(0), 3), focusing);
		Assert.Empty(DamageBy(0, focusing));

		var damage = new List<int>();
		var left = new List<int>();

		for (int round = 2; round <= 5; round++)
		{
			left.Add(runner.FocusRoundsLeft(P(0)));
			damage.AddRange(DamageBy(0, PlayRound(runner, Attack(0, 0))));
		}

		Assert.Equal(new[] { 15, 15, 15, 10 }, damage);
		Assert.Equal(new[] { 3, 2, 1, 0 }, left);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheFocus_ComesOnTheMembersTurn()
	{
		var random = new ScriptedCombatRandom().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Fighter(100, 10, 10) }, new[] { Fighter(1000, 1, 20) });

		runner.SubmitOrders(new[] { Focus(0) });

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		Assert.DoesNotContain(runner.TakeEvents(), happened => happened is Focused);

		runner.AnswerEnemyAttack(0);
		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.IsType<AttackResolved>(events[0]);
		Assert.Equal(new Focused(P(0), 3), events[1]);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheRaisedAttack_IsRoundedDown()
	{
		// Half again of 5 is 7.5.
		var random = new ScriptedCombatRandom().EnemyMisses().Hit().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Fighter(100, 5, 30) }, new[] { Fighter(1000, 1, 1) });

		PlayRound(runner, Focus(0));

		Assert.Equal(new[] { 7 }, DamageBy(0, PlayRound(runner, Attack(0, 0))));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheRaisedAttack_GoesThroughTheFormulas_TheCriticalDamageIncluded()
	{
		// 100 attack raised to 150. The critical damage reads the attack too: 150 + 150 / 5 +
		// 100 / 5 = 200%, so 300. Read from the attack before the focus, it would be 190%, 285.
		var lucky = new CombatantSetup("speaker.someone", TestTuning.Stats(healthPoints: 100, attackDamage: 100, agility: 30, luck: 100, hitRate: 90), 100, 0);
		var random = new ScriptedCombatRandom()
			.EnemyMisses()
			.Percent(0).Percent(CombatRolls.Max).Percent(0).Percent(50).EnemyMisses();
		CombatRunner runner = Start(random, new[] { lucky }, new[] { Fighter(10_000, 1, 1) });

		PlayRound(runner, Focus(0));
		IReadOnlyList<CombatEvent> events = PlayRound(runner, Attack(0, 0));

		Assert.Equal(new[] { 300 }, DamageBy(0, events));
		Assert.True(events.OfType<AttackResolved>().Single(attack => attack.Attacker == P(0)).Result.IsCritical);
		random.RequireUsedUp();
	}

	[Fact]
	public void FocusingAgain_StartsTheCountAgain_WithoutAddingToIt()
	{
		var random = new ScriptedCombatRandom().EnemyMisses().EnemyMisses();
		for (int round = 3; round <= 6; round++)
			random.Hit().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Fighter(100, 10, 30) }, new[] { Fighter(1000, 1, 1) });

		PlayRound(runner, Focus(0));
		PlayRound(runner, Focus(0));
		Assert.Equal(3, runner.FocusRoundsLeft(P(0)));

		var damage = new List<int>();
		for (int round = 3; round <= 6; round++)
			damage.AddRange(DamageBy(0, PlayRound(runner, Attack(0, 0))));

		// Stacked, the attack would be 22 in round 3; added, it would last into round 7.
		Assert.Equal(new[] { 15, 15, 15, 10 }, damage);
		random.RequireUsedUp();
	}

	[Fact]
	public void GoingDown_LosesTheFocus()
	{
		var random = new ScriptedCombatRandom().Hit().Target(0, among: 2).Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(10, 10, 30), Fighter(100, 10, 20) },
			new[] { Fighter(1000, 20, 10) });

		PlayRound(runner, Focus(0), Attack(1, 0));

		Assert.True(runner.Party[0].IsDown);
		Assert.Equal(0, runner.FocusRoundsLeft(P(0)));
		Assert.Equal(0, runner.Party[0].FocusedThrough);
		random.RequireUsedUp();
	}

	[Fact]
	public void AMemberDownedBeforeTheirTurn_DoesNotFocus()
	{
		var random = new ScriptedCombatRandom().Target(0, among: 2).Hit().Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(10, 10, 5), Fighter(100, 10, 4) },
			new[] { Fighter(1000, 20, 20) });

		IReadOnlyList<CombatEvent> events = PlayRound(runner, Focus(0), Attack(1, 0));

		Assert.DoesNotContain(events, happened => happened is Focused);
		Assert.Equal(0, runner.FocusRoundsLeft(P(0)));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheFocus_IsTheTunings()
	{
		var random = new ScriptedCombatRandom().EnemyMisses().Hit().EnemyMisses().Hit().EnemyMisses();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(100, 10, 30) },
			new[] { Fighter(1000, 1, 1) },
			TestTuning.Combat(focusAttackPercent: 100, focusRounds: 1));

		Assert.Contains(new Focused(P(0), 1), PlayRound(runner, Focus(0)));
		Assert.Equal(new[] { 20 }, DamageBy(0, PlayRound(runner, Attack(0, 0))));
		Assert.Equal(new[] { 10 }, DamageBy(0, PlayRound(runner, Attack(0, 0))));
		random.RequireUsedUp();
	}

	[Fact]
	public void AHugeFocusedAttack_StopsAtTheLargestInt()
	{
		var random = new ScriptedCombatRandom().EnemyMisses().Hit();
		// Raised by half, the largest attack is past what an int holds: worked out in int it
		// would turn negative, and BaseStats would refuse it.
		CombatRunner runner = Start(random, new[] { Fighter(100, int.MaxValue, 30) }, new[] { Fighter(int.MaxValue, 1, 1) });

		PlayRound(runner, Focus(0));
		IReadOnlyList<CombatEvent> events = PlayRound(runner, Attack(0, 0));

		Assert.Equal(new[] { int.MaxValue }, DamageBy(0, events));
		Assert.Equal(CombatResult.Won, runner.Result);
		random.RequireUsedUp();
	}

	[Fact]
	public void FocusRoundsLeft_OfSomebodyNotInTheFight_IsRefused()
	{
		CombatRunner runner = Start(new ScriptedCombatRandom(), new[] { Fighter(100, 10, 30) }, new[] { Fighter(1000, 1, 1) });

		Assert.Throws<ArgumentException>(() => runner.FocusRoundsLeft(P(3)));
		Assert.Equal(0, runner.FocusRoundsLeft(E(0)));
	}

	// --- Meditating ---

	[Fact]
	public void AMeditation_GivesBackItsShareOfTheMostMana_OnTheMembersTurn()
	{
		// A quarter of 20 is 5. The enemy is the faster, so the meditation comes after its
		// attack; the script holds only the enemy's rolls, so meditating rolls nothing.
		var random = new ScriptedCombatRandom().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Mage(mostMana: 20, manaLeft: 2, agility: 10) }, new[] { Fighter(1000, 1, 20) });

		runner.SubmitOrders(new[] { Meditate(0) });
		Assert.DoesNotContain(runner.TakeEvents(), happened => happened is Meditated);
		Assert.Equal(2, runner.Party[0].ManaPoints);

		runner.AnswerEnemyAttack(0);
		IReadOnlyList<CombatEvent> events = runner.TakeEvents();

		Assert.Equal(new Meditated(P(0), 5, 7), events.Last());
		Assert.Equal(7, runner.Party[0].ManaPoints);
		random.RequireUsedUp();
	}

	[Theory]
	[InlineData(7, 1)] // A quarter of 7 is 1.75.
	[InlineData(3, 1)] // A quarter of 3 is 0.75, and the minimum is 1.
	[InlineData(1, 1)]
	[InlineData(40, 10)]
	public void TheShare_IsRoundedDown_AndNeverBelowTheMinimum(int mostMana, int recovered)
	{
		var random = new ScriptedCombatRandom().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Mage(mostMana, manaLeft: 0) }, new[] { Fighter(1000, 1, 1) });

		Assert.Contains(new Meditated(P(0), recovered, recovered), PlayRound(runner, Meditate(0)));
		random.RequireUsedUp();
	}

	[Fact]
	public void AMeditation_StopsAtTheMostMana()
	{
		var random = new ScriptedCombatRandom().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Mage(mostMana: 20, manaLeft: 18) }, new[] { Fighter(1000, 1, 1) });

		Assert.Contains(new Meditated(P(0), 2, 20), PlayRound(runner, Meditate(0)));
		Assert.False(runner.CanMeditate(P(0)));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheMeditation_IsTheTunings()
	{
		var random = new ScriptedCombatRandom().EnemyMisses().EnemyMisses();
		CombatRunner runner = Start(
			random,
			new[] { Mage(mostMana: 20, manaLeft: 0) },
			new[] { Fighter(1000, 1, 1) },
			TestTuning.Combat(meditatePercent: 50, meditateMinimum: 3));

		Assert.Contains(new Meditated(P(0), 10, 10), PlayRound(runner, Meditate(0)));

		CombatRunner small = Start(
			random,
			new[] { Mage(mostMana: 4, manaLeft: 0) },
			new[] { Fighter(1000, 1, 1) },
			TestTuning.Combat(meditatePercent: 50, meditateMinimum: 3));

		Assert.Contains(new Meditated(P(0), 3, 3), PlayRound(small, Meditate(0)));
		random.RequireUsedUp();
	}

	[Fact]
	public void TheLargestMostMana_DoesNotOverflow()
	{
		var random = new ScriptedCombatRandom().EnemyMisses();
		CombatRunner runner = Start(random, new[] { Mage(mostMana: int.MaxValue, manaLeft: 0) }, new[] { Fighter(1000, 1, 1) });

		Assert.Contains(new Meditated(P(0), int.MaxValue / 4, int.MaxValue / 4), PlayRound(runner, Meditate(0)));
		random.RequireUsedUp();
	}

	[Fact]
	public void AMemberDownedBeforeTheirTurn_DoesNotMeditate()
	{
		var random = new ScriptedCombatRandom().Target(0, among: 2).Hit().Hit();
		CombatRunner runner = Start(
			random,
			new[] { Mage(mostMana: 20, manaLeft: 0, agility: 5, healthPoints: 10), Fighter(100, 10, 4) },
			new[] { Fighter(1000, 20, 20) });

		IReadOnlyList<CombatEvent> events = PlayRound(runner, Meditate(0), Attack(1, 0));

		Assert.DoesNotContain(events, happened => happened is Meditated);
		Assert.Equal(0, runner.Party[0].ManaPoints);
		random.RequireUsedUp();
	}

	[Fact]
	public void CanMeditate_IsWhetherTheManaIsBelowItsMost()
	{
		CombatRunner runner = Start(
			new ScriptedCombatRandom(),
			new[] { Mage(mostMana: 20, manaLeft: 19), Mage(mostMana: 20, manaLeft: 20), Mage(mostMana: 0, manaLeft: 0), Mage(mostMana: 20, manaLeft: 0, healthPoints: 0) },
			new[] { Fighter(1000, 1, 1) });

		Assert.True(runner.CanMeditate(P(0)));
		Assert.False(runner.CanMeditate(P(1)));
		Assert.False(runner.CanMeditate(P(2)));
		Assert.False(runner.CanMeditate(P(3)));
		Assert.Throws<ArgumentException>(() => runner.CanMeditate(E(0)));
		Assert.Throws<ArgumentException>(() => runner.CanMeditate(P(4)));
	}

	[Theory]
	[InlineData(20, 20)]
	[InlineData(0, 0)]
	public void AMeditation_ForAMemberWhoCannot_IsRefused_BeforeAnythingChangesOrIsRolled(int mostMana, int manaLeft)
	{
		CombatRunner runner = Start(new ScriptedCombatRandom(), new[] { Mage(mostMana, manaLeft), Mage(20, 0) }, new[] { Fighter(1000, 1, 1) });

		Assert.Throws<ArgumentException>(() => runner.SubmitOrders(new[] { Meditate(1), Meditate(0) }));

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Empty(runner.TakeEvents());
		Assert.Equal(new[] { manaLeft, 0 }, runner.Party.Select(member => member.ManaPoints));
	}

	// --- Fleeing ---

	[Fact]
	public void AFleeThatComesUp_EndsTheFight_BeforeAnyoneActs()
	{
		// The escape rate is 50 + 10 / 20 - 20 / 20 = 49.5, and the enemy is the faster.
		var random = new ScriptedCombatRandom().Percent(49.4);
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Flee(0) });

		Assert.Equal(CombatStep.Finished, runner.Step);
		Assert.Equal(CombatResult.Fled, runner.Result);
		Assert.Null(runner.PendingAttack);
		Assert.Empty(runner.MembersToOrder);
		Assert.Equal(new[] { 30 }, Health(runner.Party));
		Assert.Equal(new[] { 25 }, Health(runner.Enemies));

		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.Equal(3, events.Count);
		Assert.IsType<RoundStarted>(events[0]);
		Assert.Equal(new EscapeAttempted(true), events[1]);
		Assert.Equal(new FightEnded(CombatResult.Fled), events[2]);
		random.RequireUsedUp();
	}

	[Fact]
	public void AFleeThatFails_CostsThePartyItsRound_AndTheEnemiesAct()
	{
		// The escape rate is 50 + 20 / 20 - 20 / 20 = 50, and a roll of 50 is not below it.
		// The second member asked, and the faster first member does nothing either.
		var random = new ScriptedCombatRandom().Percent(50).Target(1, among: 2).Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 30), Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Flee(1) });

		IReadOnlyList<CombatEvent> events = runner.TakeEvents();
		Assert.Equal(2, events.Count);
		Assert.IsType<RoundStarted>(events[0]);
		Assert.Equal(new EscapeAttempted(false), events[1]);
		Assert.Equal(new PendingEnemyAttack(E(0), P(1), AttackResult.Hit(7, isCritical: false)), runner.PendingAttack);

		runner.AnswerEnemyAttack(7);

		Assert.Equal(new CombatEvent[] { new AttackResolved(E(0), P(1), AttackResult.Hit(7, isCritical: false), 7, 23) }, runner.TakeEvents());
		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(CombatResult.Ongoing, runner.Result);
		Assert.Equal(2, runner.Round);
		Assert.Equal(new[] { 25 }, Health(runner.Enemies));
		random.RequireUsedUp();
	}

	[Theory]
	[InlineData(49.9, true)] // Counting the enemy who is down would make it 48.825.
	[InlineData(50.1, false)] // Counting the member who is down would make it 55.
	public void TheEscapeRate_CountsThoseStandingOnEachSide(double roll, bool escapes)
	{
		// Standing, it is 50 + 2 / 20 - 1 / 20 = 50.05. The member down has 200 agility,
		// and the enemy down 50.
		var random = new ScriptedCombatRandom().Percent(roll);
		if (!escapes)
			random.Index(0).Miss();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 2), Fighter(30, 10, 200, healthLeft: 0) },
			new[] { Fighter(25, 7, 1), Fighter(25, 7, 50, healthLeft: 0) });

		runner.SubmitOrders(new[] { Flee(0) });
		AnswerAsWorkedOut(runner);

		Assert.Equal(escapes ? CombatResult.Fled : CombatResult.Ongoing, runner.Result);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheEscapeRate_IsTheStatFormulas_LuckIncluded()
	{
		// 100 luck on the member adds 10: 60.
		var lucky = new CombatantSetup("speaker.someone", TestTuning.Stats(healthPoints: 30, attackDamage: 10, luck: 100, hitRate: 90), 30, 0);
		var random = new ScriptedCombatRandom().Percent(59.9);
		CombatRunner runner = Start(random, new[] { lucky }, new[] { Fighter(25, 7, 1) });

		runner.SubmitOrders(new[] { Flee(0) });

		Assert.Equal(CombatResult.Fled, runner.Result);
		random.RequireUsedUp();
	}

	[Fact]
	public void TheFleeRoll_ComesAfterTheTurnOrdersRolls()
	{
		// A tie in agility is rolled for first; a flee rolled before it would take the index
		// roll as its percentage, and the script would refuse it.
		var random = new ScriptedCombatRandom().Index(1).Percent(0);
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 20) }, new[] { Fighter(25, 7, 20) });

		runner.SubmitOrders(new[] { Flee(0) });

		Assert.Equal(CombatResult.Fled, runner.Result);
		random.RequireUsedUp();
	}

	[Fact]
	public void AFledFight_TakesNoMoreOrders()
	{
		CombatRunner runner = Start(new ScriptedCombatRandom().Percent(0), new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });
		runner.SubmitOrders(new[] { Flee(0) });

		Assert.Throws<InvalidOperationException>(() => runner.SubmitOrders(new[] { Attack(0, 0) }));
	}

	[Fact]
	public void CanFlee_IsWhatTheFightWasStartedWith()
	{
		Assert.True(Start(new ScriptedCombatRandom(), new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) }, canFlee: true).CanFlee);
		Assert.False(Start(new ScriptedCombatRandom(), new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) }, canFlee: false).CanFlee);
	}

	[Fact]
	public void AFlee_InAFightThatCannotBeFled_IsRefused_BeforeAnythingIsRolled()
	{
		CombatRunner runner = Start(new ScriptedCombatRandom(), new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) }, canFlee: false);

		Assert.Throws<ArgumentException>(() => runner.SubmitOrders(new[] { Flee(0) }));

		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Empty(runner.TakeEvents());
	}

	[Fact]
	public void TheOtherOrders_StillWork_InAFightThatCannotBeFled()
	{
		var random = new ScriptedCombatRandom().Target(0, among: 1).Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) }, canFlee: false);

		PlayRound(runner, Defend(0));

		Assert.Equal(2, runner.Round);
		random.RequireUsedUp();
	}

	// --- The rolls of the new orders ---

	[Fact]
	public void DefendingFocusingAndMeditating_RollNothing_SoARoundOfThemTakesOnlyTheEnemysRolls()
	{
		// The flee's is the only roll an order other than an attack adds; these three add none.
		var random = new ScriptedCombatRandom().Target(0, among: 3).Hit();
		CombatRunner runner = Start(
			random,
			new[] { Fighter(30, 10, 30), Fighter(30, 10, 20), Mage(mostMana: 20, manaLeft: 0, agility: 10) },
			new[] { Fighter(100, 20, 25) });

		IReadOnlyList<CombatEvent> events = PlayRound(runner, Defend(0), Focus(1), Meditate(2));

		Assert.Contains(new Defended(P(0)), events);
		Assert.Contains(new Focused(P(1), 3), events);
		Assert.Contains(new Meditated(P(2), 5, 5), events);
		Assert.Equal(new[] { 20, 30, 100 }, Health(runner.Party));
		random.RequireUsedUp();
	}

	// --- Orders that do not fit ---

	private sealed record WaveOrder(CombatantId Actor) : CombatOrder(Actor);

	/// <summary>Party 0 and 1 standing and Party 2 down, against Enemy 0 standing and Enemy 1 down.</summary>
	private static CombatRunner FightForOrders(ScriptedCombatRandom random) =>
		Start(
			random,
			new[] { Fighter(30, 10, 30), Fighter(30, 10, 20), Fighter(30, 10, 25, healthLeft: 0) },
			new[] { Fighter(25, 7, 10), Fighter(25, 7, 10, healthLeft: 0) });

	public static TheoryData<string, CombatOrder[]> OrdersThatDoNotFit() => new()
	{
		{ "none at all", Array.Empty<CombatOrder>() },
		{ "one missing", new CombatOrder[] { Attack(0, 0) } },
		{ "one for a member who is down", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Attack(2, 0) } },
		{ "two for the same member", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Attack(0, 0) } },
		{ "one for an enemy", new CombatOrder[] { Attack(0, 0), Attack(1, 0), new AttackOrder(E(0), E(0)) } },
		{ "one for a member who is not there", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Attack(7, 0) } },
		{ "a target on the party's side", new CombatOrder[] { Attack(0, 0), new AttackOrder(P(1), P(0)) } },
		{ "a target that is not there", new CombatOrder[] { Attack(0, 0), Attack(1, 9) } },
		{ "a target already down", new CombatOrder[] { Attack(0, 0), Attack(1, 1) } },
		{ "a missing order", new CombatOrder[] { Attack(0, 0), null } },
		{ "a kind the fight does not know", new CombatOrder[] { Attack(0, 0), new WaveOrder(P(1)) } },
		{ "a defence for a member who is down", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Defend(2) } },
		{ "a defence and an attack for the same member", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Defend(0) } },
		{ "a defence for an enemy", new CombatOrder[] { Attack(0, 0), Attack(1, 0), new DefendOrder(E(0)) } },
		{ "a focus for a member who is down", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Focus(2) } },
		{ "a focus and a defence for the same member", new CombatOrder[] { Focus(0), Attack(1, 0), Defend(0) } },
		{ "a meditation for a member who is down", new CombatOrder[] { Attack(0, 0), Attack(1, 0), Meditate(2) } },
		{ "a flee with another order", new CombatOrder[] { Flee(0), Attack(1, 0) } },
		{ "two flees", new CombatOrder[] { Flee(0), Flee(1) } },
		{ "a flee for a member who is down", new CombatOrder[] { Flee(2) } },
		{ "a flee for an enemy", new CombatOrder[] { new FleeOrder(E(0)) } }
	};

	[Theory]
	[MemberData(nameof(OrdersThatDoNotFit))]
	public void OrdersThatDoNotFit_AreRefused_BeforeAnythingChangesOrIsRolled(string what, CombatOrder[] orders)
	{
		// An empty script: a single roll would fail the test with another exception.
		CombatRunner runner = FightForOrders(new ScriptedCombatRandom());

		Assert.Throws<ArgumentException>(() => runner.SubmitOrders(orders));

		Assert.NotNull(what);
		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
		Assert.Equal(1, runner.Round);
		Assert.Empty(runner.TakeEvents());
		Assert.Equal(new[] { 25, 0 }, Health(runner.Enemies));
	}

	[Fact]
	public void MissingOrders_AreRefused()
	{
		CombatRunner runner = FightForOrders(new ScriptedCombatRandom());

		Assert.Throws<ArgumentNullException>(() => runner.SubmitOrders(null));
	}

	[Fact]
	public void AfterRefusedOrders_TheRightOnesAreStillTaken()
	{
		var random = new ScriptedCombatRandom().Hit().Hit().Target(0).Miss();
		CombatRunner runner = FightForOrders(random);
		Assert.Throws<ArgumentException>(() => runner.SubmitOrders(new[] { Attack(0, 0) }));

		runner.SubmitOrders(new[] { Attack(1, 0), Attack(0, 0) });
		runner.AnswerEnemyAttack(0);

		Assert.Equal(new[] { 5, 0 }, Health(runner.Enemies));
		Assert.Equal(2, runner.Round);
		random.RequireUsedUp();
	}

	[Fact]
	public void Orders_AreNotTaken_WhileAnEnemyAttackIsPending()
	{
		var random = new ScriptedCombatRandom().Target(0).Miss().Hit();
		CombatRunner runner = Start(random, new[] { Fighter(30, 10, 10) }, new[] { Fighter(25, 7, 20) });
		runner.SubmitOrders(new[] { Attack(0, 0) });

		Assert.Throws<InvalidOperationException>(() => runner.SubmitOrders(new[] { Attack(0, 0) }));

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		runner.AnswerEnemyAttack(0);
		random.RequireUsedUp();
	}

	// --- Whole fights ---

	[Fact]
	public void AWholeFight_OfFourAgainstThree_PlaysOutAsScripted()
	{
		// Agility sets one order with no ties: A, X, B, Y, C, Z, D.
		CombatantSetup[] party =
		{
			Fighter(30, 10, 40), // A
			Fighter(20, 8, 30),  // B
			Fighter(12, 6, 20),  // C
			Fighter(10, 4, 10)   // D
		};
		CombatantSetup[] enemies =
		{
			Fighter(18, 12, 35), // X
			Fighter(16, 6, 25),  // Y
			Fighter(14, 20, 15)  // Z
		};
		var random = new ScriptedCombatRandom();

		// Round 1. A hits X, 18 to 8.
		random.Hit();
		// X picks C, of A B C D, and takes them down.
		random.Target(2).Hit();
		// B takes X down.
		random.Hit();
		// Y picks D, of A B D, and misses.
		random.Target(2).Miss();
		// Z picks B, of A B D, and takes them down.
		random.Target(1).Hit();
		// D hits Y, 16 to 12.
		random.Hit();

		// Round 2: A, Y, Z, D. A hits Y, 12 to 2.
		random.Hit();
		// Y picks A, 30 to 24.
		random.Target(0).Hit();
		// Z picks D, of A D, and takes them down.
		random.Target(1).Hit();

		// Round 3: A, Y, Z. A takes Y down.
		random.Hit();
		// Z hits A, 24 to 4.
		random.Target(0).Hit();

		// Round 4: A, Z. A hits Z, 14 to 4, and Z misses.
		random.Hit();
		random.Target(0).Miss();

		// Round 5. A takes Z down.
		random.Hit();

		CombatRunner runner = Start(random, party, enemies);
		var events = new List<CombatEvent>();

		void Play(params CombatOrder[] orders)
		{
			runner.SubmitOrders(orders);
			AnswerAsWorkedOut(runner);
			events.AddRange(runner.TakeEvents());
		}

		Play(Attack(0, 0), Attack(1, 0), Attack(2, 1), Attack(3, 1));
		Assert.Equal(new[] { 30, 0, 0, 10 }, Health(runner.Party));
		Assert.Equal(new[] { 0, 12, 14 }, Health(runner.Enemies));
		Assert.Equal(new[] { P(0), P(3) }, runner.MembersToOrder);

		Play(Attack(0, 1), Attack(3, 1));
		Assert.Equal(new[] { 24, 0, 0, 0 }, Health(runner.Party));
		Assert.Equal(new[] { 0, 2, 14 }, Health(runner.Enemies));

		Play(Attack(0, 1));
		Assert.Equal(new[] { 4, 0, 0, 0 }, Health(runner.Party));

		Play(Attack(0, 2));
		Assert.Equal(CombatResult.Ongoing, runner.Result);
		Assert.Equal(5, runner.Round);

		Play(Attack(0, 2));

		Assert.Equal(CombatStep.Finished, runner.Step);
		Assert.Equal(CombatResult.Won, runner.Result);
		Assert.Equal(5, runner.Round);
		Assert.Equal(new[] { 4, 0, 0, 0 }, Health(runner.Party));
		Assert.Equal(new[] { 0, 0, 0 }, Health(runner.Enemies));
		Assert.Equal(
			new[] { P(2), E(0), P(1), P(3), E(1), E(2) },
			events.OfType<CombatantDowned>().Select(downed => downed.Combatant));
		Assert.Equal(
			new[] { P(0), E(0), P(1), E(1), P(2), E(2), P(3) },
			events.OfType<RoundStarted>().First().TurnOrder);
		Assert.Equal(new[] { P(0), E(1), E(2) }, events.OfType<RoundStarted>().ElementAt(2).TurnOrder);
		Assert.Equal(5, events.OfType<RoundStarted>().Count());
		Assert.Equal(14, events.OfType<AttackResolved>().Count());
		Assert.Equal(new FightEnded(CombatResult.Won), events.Last());
		random.RequireUsedUp();
	}

	[Fact]
	public void TheSameFight_WithAnEnemyStepThatDodgesHalf_EndsAnotherWay()
	{
		// One member against one enemy that hits for 20. Taking every hit, the member is
		// down in round 2. A step that answers half of it leaves them standing to win.
		static CombatRunner Fight(ScriptedCombatRandom random) =>
			Start(random, new[] { Fighter(35, 10, 10) }, new[] { Fighter(25, 20, 20) });

		var taken = new ScriptedCombatRandom().Target(0).Hit().Hit().Target(0).Hit();
		CombatRunner takesAll = Fight(taken);
		takesAll.SubmitOrders(new[] { Attack(0, 0) });
		takesAll.AnswerEnemyAttack(takesAll.PendingAttack.Result.Damage);
		takesAll.SubmitOrders(new[] { Attack(0, 0) });
		takesAll.AnswerEnemyAttack(takesAll.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Lost, takesAll.Result);
		taken.RequireUsedUp();

		var dodged = new ScriptedCombatRandom()
			.Target(0).Hit().Hit()
			.Target(0).Hit().Hit()
			.Target(0).Hit().Hit();
		CombatRunner dodgesHalf = Fight(dodged);

		while (dodgesHalf.Step != CombatStep.Finished)
		{
			dodgesHalf.SubmitOrders(new[] { Attack(0, 0) });

			if (dodgesHalf.Step == CombatStep.EnemyAttackPending)
				dodgesHalf.AnswerEnemyAttack(dodgesHalf.PendingAttack.Result.Damage / 2);
		}

		Assert.Equal(CombatResult.Won, dodgesHalf.Result);
		Assert.Equal(3, dodgesHalf.Round);
		Assert.Equal(5, dodgesHalf.Party[0].HealthPoints);
		dodged.RequireUsedUp();
	}
}
