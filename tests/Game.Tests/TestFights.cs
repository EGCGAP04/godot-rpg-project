namespace GodotRPGProject.Tests;

/// <summary>Groups of enemies for the tests that record fights, with numbers of their own.</summary>
internal static class TestFights
{
	/// <summary>A group of one enemy, with the enemy's own id as the group's.</summary>
	public static EncounterValues Group(string enemyId) =>
		new(enemyId, new[] { new EnemyValues(enemyId, EnemyValues.NameKeyOf(enemyId), TestTuning.Stats(healthPoints: 10), experience: 5) });

	/// <summary>What <see cref="Group"/> leaves in the history.</summary>
	public static CombatRecord Record(World world, int cycle, string enemyId, FightOutcome outcome) =>
		new(world, cycle, enemyId, new[] { enemyId }, outcome);
}
