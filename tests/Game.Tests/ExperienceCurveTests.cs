using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="ExperienceCurve"/>: what each level costs, and what adding
/// experience does to a level.
/// </summary>
public class ExperienceCurveTests
{
	/// <summary>A curve whose costs are easy to work out: 10, 40, 90, 160… up to level 5.</summary>
	private static ExperienceCurve Square(int highestLevel = 5) => new(baseValue: 10, exponent: 2, highestLevel);

	[Theory]
	[InlineData(1, 10)]
	[InlineData(2, 40)]
	[InlineData(3, 90)]
	[InlineData(4, 160)]
	public void ToNextLevel_IsTheBaseTimesTheLevelToTheExponent(int level, long expected)
	{
		Assert.Equal(expected, Square().ToNextLevel(level));
	}

	[Fact]
	public void ToNextLevel_RoundsDown()
	{
		// 10 * 2^1.5 = 28.28…, and 10 * 3^1.5 = 51.96…
		var curve = new ExperienceCurve(10, 1.5, 99);

		Assert.Equal(28, curve.ToNextLevel(2));
		Assert.Equal(51, curve.ToNextLevel(3));
	}

	[Fact]
	public void ToNextLevel_IsNeverLessThanOne()
	{
		// 0.1 * 1 = 0.1, which rounds down to a free level.
		Assert.Equal(1, new ExperienceCurve(0.1, 1, 99).ToNextLevel(1));
	}

	[Fact]
	public void ToNextLevel_AtLevel998_IsExactAndBeyond32Bits()
	{
		// 10 * 998^3
		long cost = new ExperienceCurve(10, 3, 999).ToNextLevel(998);

		Assert.Equal(9_940_119_920L, cost);
		Assert.True(cost > int.MaxValue);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(5)]
	[InlineData(6)]
	public void ToNextLevel_WhereThereIsNoNextLevel_IsRefused(int level)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Square().ToNextLevel(level));

		Assert.Equal("level", exception.ParamName);
	}

	[Fact]
	public void Add_LessThanTheLevelCosts_KeepsTheLevelAndTheExperience()
	{
		Assert.Equal(new LevelProgress(1, 9), Square().Add(new LevelProgress(1, 0), 9));
		Assert.Equal(new LevelProgress(1, 9), Square().Add(new LevelProgress(1, 4), 5));
	}

	[Fact]
	public void Add_ExactlyWhatTheLevelCosts_RaisesItAndLeavesNothing()
	{
		Assert.Equal(new LevelProgress(2, 0), Square().Add(new LevelProgress(1, 0), 10));
		Assert.Equal(new LevelProgress(2, 0), Square().Add(new LevelProgress(1, 4), 6));
	}

	[Fact]
	public void Add_MoreThanTheLevelCosts_CarriesTheRestTowardsTheNext()
	{
		Assert.Equal(new LevelProgress(2, 7), Square().Add(new LevelProgress(1, 0), 17));
	}

	[Fact]
	public void Add_ALargeAward_CrossesSeveralLevels()
	{
		// 10 + 40 + 90 = 140 reaches level 4, and 5 are left over.
		Assert.Equal(new LevelProgress(4, 5), Square().Add(new LevelProgress(1, 0), 145));
	}

	[Fact]
	public void Add_ReachingTheHighestLevel_StopsThereAndKeepsNothing()
	{
		// 10 + 40 + 90 + 160 = 300 reaches level 5, the highest.
		Assert.Equal(new LevelProgress(5, 0), Square().Add(new LevelProgress(1, 0), 300));
		Assert.Equal(new LevelProgress(5, 0), Square().Add(new LevelProgress(1, 0), 301));
		Assert.Equal(new LevelProgress(5, 0), Square().Add(new LevelProgress(3, 20), long.MaxValue));
	}

	[Fact]
	public void Add_AtTheHighestLevel_ChangesNothing()
	{
		Assert.Equal(new LevelProgress(5, 0), Square().Add(new LevelProgress(5, 0), 1_000));
	}

	[Fact]
	public void Add_AnAwardOfZero_ChangesNothing()
	{
		Assert.Equal(new LevelProgress(2, 15), Square().Add(new LevelProgress(2, 15), 0));
	}

	[Fact]
	public void Add_ANegativeAward_IsRefused()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Square().Add(new LevelProgress(1, 5), -1));

		Assert.Equal("award", exception.ParamName);
	}

	[Fact]
	public void Add_AProgressHoldingMoreThanItsLevelCosts_RisesEvenWithNoAward()
	{
		// The curve was retuned to cost less: 55 at level 1 is 10 for level 2, 40 for level 3, and 5 over.
		Assert.Equal(new LevelProgress(3, 5), Square().Add(new LevelProgress(1, 55), 0));
		Assert.Equal(new LevelProgress(3, 8), Square().Add(new LevelProgress(1, 55), 3));
	}

	[Fact]
	public void Add_ALevelAboveTheHighest_IsNotTakenAway()
	{
		// The highest level was lowered after the character passed it.
		Assert.Equal(new LevelProgress(8, 0), Square().Add(new LevelProgress(8, 30), 100));
	}

	[Fact]
	public void Add_UpToLevel999_NeitherOverflowsNorLosesAPoint()
	{
		var curve = new ExperienceCurve(10, 3, 999);
		long total = 0;

		for (int level = 1; level < 999; level++)
			total += curve.ToNextLevel(level);

		// 10 * (998 * 999 / 2)^2, the sum of the cubes from 1 to 998.
		Assert.Equal(2_485_032_470_010L, total);
		Assert.Equal(new LevelProgress(999, 0), curve.Add(new LevelProgress(1, 0), total));

		// One point short stays one point short of the last level, to the unit.
		Assert.Equal(new LevelProgress(998, 9_940_119_919L), curve.Add(new LevelProgress(1, 0), total - 1));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-10)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void Constructor_ABaseThatIsNotAboveZero_IsRefused(double baseValue)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceCurve(baseValue, 2, 99));

		Assert.Equal("baseValue", exception.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void Constructor_AnExponentThatIsNotAboveZero_IsRefused(double exponent)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceCurve(10, exponent, 99));

		Assert.Equal("exponent", exception.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_AHighestLevelBelowOne_IsRefused(int highestLevel)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceCurve(10, 2, highestLevel));

		Assert.Equal("highestLevel", exception.ParamName);
	}

	[Fact]
	public void Constructor_ACurveWhoseLastLevelDoesNotFitIn64Bits_IsRefused()
	{
		// 10 * 998^7 is about 9.9e21, and 64 bits hold about 9.2e18.
		Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceCurve(10, 7, 999));

		// The same curve is fine while it stops early enough: 10 * 98^7 is about 8.7e14.
		Assert.Equal(99, new ExperienceCurve(10, 7, 99).HighestLevel);
	}

	[Fact]
	public void Constructor_AHighestLevelOfOne_IsACurveWithNoLevelToGain()
	{
		Assert.Equal(new LevelProgress(1, 0), new ExperienceCurve(10, 2, 1).Add(new LevelProgress(1, 0), 500));
	}
}
