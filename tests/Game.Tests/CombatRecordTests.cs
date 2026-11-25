using System;
using System.Collections.Generic;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatRecord"/>: a fight in the history is checked whole when it is
/// built, and two fights against the same enemies are equal, though a record would compare
/// their lists by reference.
/// </summary>
public class CombatRecordTests
{
	private static CombatRecord Fight(string encounterId = "demo_patrol", string[] enemies = null, FightOutcome outcome = FightOutcome.Won, World world = World.Fantasy, int cycle = 1) =>
		new(world, cycle, encounterId, enemies ?? new[] { "weak", "weak", "demo_clumsy" }, outcome);

	[Fact]
	public void Constructor_KeepsWhatItIsGiven()
	{
		CombatRecord fight = Fight(cycle: 4, world: World.Nightmare, outcome: FightOutcome.Lost);

		Assert.Equal(World.Nightmare, fight.World);
		Assert.Equal(4, fight.Cycle);
		Assert.Equal("demo_patrol", fight.EncounterId);
		Assert.Equal(new[] { "weak", "weak", "demo_clumsy" }, fight.EnemyIds);
		Assert.Equal(FightOutcome.Lost, fight.Outcome);
	}

	[Fact]
	public void TheEnemies_AreACopy_ThatTheGivenListDoesNotMove()
	{
		var enemies = new List<string> { "weak" };
		var fight = new CombatRecord(World.Fantasy, 1, "demo_patrol", enemies, FightOutcome.Won);

		enemies.Add("strong");

		Assert.Equal(new[] { "weak" }, fight.EnemyIds);
		Assert.Throws<NotSupportedException>(() => ((IList<string>)fight.EnemyIds).Add("strong"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Constructor_RefusesAFightWithoutAGroup(string encounterId)
	{
		Assert.Throws<ArgumentException>(() => Fight(encounterId: encounterId));
	}

	[Fact]
	public void Constructor_RefusesAFightWithoutEnemies()
	{
		Assert.Throws<ArgumentNullException>(() => new CombatRecord(World.Fantasy, 1, "demo_patrol", null, FightOutcome.Won));
		Assert.Throws<ArgumentException>(() => Fight(enemies: Array.Empty<string>()));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Constructor_RefusesAnEnemyWithoutAnId(string enemyId)
	{
		Assert.Throws<ArgumentException>(() => Fight(enemies: new[] { "weak", enemyId }));
	}

	[Fact]
	public void Constructor_RefusesAnOutcomeThatDoesNotExist()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Fight(outcome: (FightOutcome)99));
	}

	[Fact]
	public void TwoFightsAgainstTheSameEnemies_AreEqual_ThoughTheirListsAreNotTheSame()
	{
		CombatRecord one = Fight(enemies: new[] { "weak", "demo_clumsy" });
		CombatRecord other = Fight(enemies: new[] { "weak", "demo_clumsy" });

		Assert.Equal(one, other);
		Assert.True(one == other);
		Assert.Equal(one.GetHashCode(), other.GetHashCode());
	}

	[Fact]
	public void FightsThatDifferInAnything_AreNotEqual()
	{
		CombatRecord fight = Fight();

		Assert.NotEqual(fight, Fight(enemies: new[] { "weak", "demo_clumsy", "weak" }));
		Assert.NotEqual(fight, Fight(enemies: new[] { "weak", "weak" }));
		Assert.NotEqual(fight, Fight(encounterId: "demo_wall"));
		Assert.NotEqual(fight, Fight(outcome: FightOutcome.Lost));
		Assert.NotEqual(fight, Fight(world: World.Nightmare));
		Assert.NotEqual(fight, Fight(cycle: 2));
		Assert.False(fight.Equals(null));
	}

	[Fact]
	public void ToString_IsTheLineTheDebugOverlayShows()
	{
		Assert.Equal("Fantasy, cycle 1: demo_patrol (weak, weak, demo_clumsy), won", Fight().ToString());
		Assert.Equal("Nightmare, cycle 3: demo_wall (strong), lost", Fight("demo_wall", new[] { "strong" }, FightOutcome.Lost, World.Nightmare, 3).ToString());
	}
}
