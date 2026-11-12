using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="BaseStats"/>: it carries the seven numbers as given and refuses
/// a negative one where it is built.
/// </summary>
public class BaseStatsTests
{
	[Fact]
	public void Constructor_KeepsEachStatInItsOwnProperty()
	{
		var stats = new BaseStats(healthPoints: 1, attackDamage: 2, armor: 3, manaPoints: 4, agility: 5, luck: 6, hitRate: 7);

		Assert.Equal(1, stats.HealthPoints);
		Assert.Equal(2, stats.AttackDamage);
		Assert.Equal(3, stats.Armor);
		Assert.Equal(4, stats.ManaPoints);
		Assert.Equal(5, stats.Agility);
		Assert.Equal(6, stats.Luck);
		Assert.Equal(7, stats.HitRate);
	}

	[Fact]
	public void Constructor_AcceptsEveryStatAtZero()
	{
		var stats = new BaseStats(0, 0, 0, 0, 0, 0, 0);

		Assert.Equal(default, stats);
	}

	[Theory]
	[InlineData(0, "healthPoints")]
	[InlineData(1, "attackDamage")]
	[InlineData(2, "armor")]
	[InlineData(3, "manaPoints")]
	[InlineData(4, "agility")]
	[InlineData(5, "luck")]
	[InlineData(6, "hitRate")]
	public void Constructor_RefusesANegativeStat_NamingIt(int position, string name)
	{
		int[] values = { 10, 10, 10, 10, 10, 10, 10 };
		values[position] = -1;

		var thrown = Assert.Throws<ArgumentOutOfRangeException>(
			() => new BaseStats(values[0], values[1], values[2], values[3], values[4], values[5], values[6]));

		Assert.Equal(name, thrown.ParamName);
	}

	[Fact]
	public void TwoSetsOfStats_AreEqualWhenEveryNumberMatches()
	{
		var one = new BaseStats(1, 2, 3, 4, 5, 6, 7);
		var same = new BaseStats(1, 2, 3, 4, 5, 6, 7);
		var other = new BaseStats(1, 2, 3, 4, 5, 6, 8);

		Assert.Equal(one, same);
		Assert.NotEqual(one, other);
	}
}
