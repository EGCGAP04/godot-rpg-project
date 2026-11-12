using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatGrowthValues"/>: each of the seven stats follows its own
/// line, and the result is the <see cref="BaseStats"/> the formulas read.
/// </summary>
public class StatGrowthValuesTests
{
	/// <summary>Seven different lines, so a stat read from another one's line shows.</summary>
	private static StatGrowthValues Growth() => new(
		HealthPoints: new StatLine(20, 5),
		AttackDamage: new StatLine(4, 1.5m),
		Armor: new StatLine(2, 0.5m),
		ManaPoints: new StatLine(10, 3),
		Agility: new StatLine(6, 0.25m),
		Luck: new StatLine(1, 0.1m),
		HitRate: new StatLine(90, 2));

	[Fact]
	public void At_LevelOne_GivesEveryBase()
	{
		Assert.Equal(new BaseStats(20, 4, 2, 10, 6, 1, 90), Growth().At(1));
	}

	[Fact]
	public void At_GrowsEachStatAlongItsOwnLine()
	{
		// Ten levels after the first: 20+50, 4+15, 2+5, 10+30, 6+2.5, 1+1, 90+20.
		Assert.Equal(new BaseStats(70, 19, 7, 40, 8, 2, 110), Growth().At(11));
	}

	[Fact]
	public void At_RoundsEachStatDownOnItsOwn()
	{
		// One level after the first: 5.5, 2.5, 6.25 and 1.1 round down; the rest are whole.
		Assert.Equal(new BaseStats(25, 5, 2, 13, 6, 1, 92), Growth().At(2));
	}

	[Fact]
	public void At_Level999_IsExact()
	{
		// 998 levels after the first: 20+4990, 4+1497, 2+499, 10+2994, 6+249.5, 1+99.8, 90+1996.
		Assert.Equal(new BaseStats(5010, 1501, 501, 3004, 255, 100, 2086), Growth().At(999));
	}

	[Fact]
	public void At_ALevelBelowOne_IsRefused()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Growth().At(0));
	}
}
