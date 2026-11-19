using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>Groups of enemies and whole fights for the tests, with numbers of their own.</summary>
internal static class TestFights
{
	/// <summary>A group of one enemy, with the enemy's own id as the group's.</summary>
	public static EncounterValues Group(string enemyId) =>
		new(enemyId, new[] { new EnemyValues(enemyId, EnemyValues.NameKeyOf(enemyId), TestTuning.Stats(healthPoints: 10), experience: 5) });

	/// <summary>What <see cref="Group"/> leaves in the history.</summary>
	public static CombatRecord Record(World world, int cycle, string enemyId, FightOutcome outcome) =>
		new(world, cycle, enemyId, new[] { enemyId }, outcome);

	/// <summary>
	/// A foe with 4 health points, 25 attack and 10 agility, worth 7 experience: it acts
	/// before any of <see cref="TestRoster"/>'s characters, downs one at level 1 in the
	/// Fantasy world with a hit, and goes down to one of theirs.
	/// </summary>
	public static EncounterValues Foe() =>
		new("test_foe", new[] { new EnemyValues("foe", "enemy.foe", TestTuning.Stats(healthPoints: 4, attackDamage: 25, agility: 10, hitRate: 100), experience: 7) });

	/// <summary>
	/// A win in the Fantasy world, against <see cref="Foe"/>, for a party of two or more:
	/// the foe downs the last member, and the protagonist ends it before anyone else acts.
	/// </summary>
	public static CombatAftermath AWinWhereTheLastGoesDown(PartyState party)
	{
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Foe());
		int members = lineup.Party.Count;
		Assert.True(members >= 2, "The last member going down is a win only with someone else standing.");

		var random = new ScriptedCombatRandom();

		// The members tie on agility, and stay in the party's order.
		for (int left = members; left > 1; left--)
			random.Index(0, among: left);

		random.Index(members - 1, among: members);
		random.Hit();
		random.Hit();

		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, TestTuning.Formulas(), TestTuning.Combat(), random);
		runner.SubmitOrders(Enumerable.Range(0, members).Select(member => (CombatOrder)new AttackOrder(CombatantId.Party(member), CombatantId.Enemy(0))).ToArray());
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Won, runner.Result);
		random.RequireUsedUp();
		return CombatAftermath.Of(lineup, runner);
	}

	/// <summary>A loss in the Fantasy world, against <see cref="Foe"/>, for the protagonist alone.</summary>
	public static CombatAftermath ALoss(PartyState party)
	{
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Foe());
		Assert.Single(lineup.Party);

		var random = new ScriptedCombatRandom();
		random.Index(0, among: 1);
		random.Hit();

		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, TestTuning.Formulas(), TestTuning.Combat(), random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Lost, runner.Result);
		random.RequireUsedUp();
		return CombatAftermath.Of(lineup, runner);
	}
}
