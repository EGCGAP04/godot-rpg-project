using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatLine"/>: a stat is its base at level 1, grows by the same
/// amount each level, and is rounded down once.
/// </summary>
public class StatLineTests
{
	[Fact]
	public void At_LevelOne_IsTheBase()
	{
		Assert.Equal(20, new StatLine(20, 3).At(1));
	}

	[Theory]
	[InlineData(2, 23)]
	[InlineData(3, 26)]
	[InlineData(10, 47)]
	public void At_AddsTheGrowthForEachLevelAfterTheFirst(int level, int expected)
	{
		Assert.Equal(expected, new StatLine(20, 3).At(level));
	}

	[Theory]
	[InlineData(1, 4)]   // 4
	[InlineData(2, 4)]   // 4.5
	[InlineData(3, 5)]   // 5
	[InlineData(4, 5)]   // 5.5
	public void At_RoundsAFractionalGrowthDown(int level, int expected)
	{
		Assert.Equal(expected, new StatLine(4, 0.5m).At(level));
	}

	[Fact]
	public void At_RoundsDownAndNeverToTheNearest()
	{
		// 4 + 0.9 = 4.9, which is 4 rounded down and 5 rounded to the nearest.
		Assert.Equal(4, new StatLine(4, 0.9m).At(2));
	}

	[Fact]
	public void At_RoundsAFractionalBaseDownToo()
	{
		Assert.Equal(7, new StatLine(7.99m, 0).At(1));
	}

	[Fact]
	public void At_AGrowthWithNoExactBinaryForm_StillLandsOnTheWholeNumber()
	{
		// 0.29 * 100 is 29. In double it is 28.999999999999996, which rounds down to 28.
		Assert.Equal(29, new StatLine(0, 0.29m).At(101));
	}

	[Fact]
	public void At_Level999_IsExact()
	{
		// 20 + 3.7 * 998 = 3712.6
		Assert.Equal(3712, new StatLine(20, 3.7m).At(999));
		// 0.1 * 998 = 99.8, and 0.1 * 990 = 99 exactly.
		Assert.Equal(99, new StatLine(0, 0.1m).At(999));
		Assert.Equal(99, new StatLine(0, 0.1m).At(991));
	}

	[Fact]
	public void At_AZeroLine_IsZeroAtAnyLevel()
	{
		Assert.Equal(0, default(StatLine).At(1));
		Assert.Equal(0, new StatLine(0, 0).At(999));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void At_ALevelBelowOne_IsRefused(int level)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StatLine(20, 3).At(level));

		Assert.Equal("level", exception.ParamName);
	}

	[Fact]
	public void At_AStatTooLargeForABaseStat_IsRefused()
	{
		var line = new StatLine(0, 3_000_000);

		Assert.Equal(2_145_000_000, line.At(716));
		Assert.Throws<OverflowException>(() => line.At(999));
	}

	[Fact]
	public void From_TurnsTheNumbersOfAResourceIntoExactOnes()
	{
		StatLine line = StatLine.From(20.0, 0.29);

		Assert.Equal(20m, line.Base);
		Assert.Equal(0.29m, line.Growth);
		Assert.Equal(new StatLine(20, 0.29m), line);

		// The case a double gets wrong: 0.29 * 100 lands on 29, not on 28.
		Assert.Equal(29, StatLine.From(0, 0.29).At(101));
	}

	[Fact]
	public void From_KeepsEachNumberInItsOwnPlace()
	{
		StatLine line = StatLine.From(7, 0.5);

		Assert.Equal(7m, line.Base);
		Assert.Equal(0.5m, line.Growth);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(1e30)]
	[InlineData(-1)]
	public void From_ABaseThatIsNotAStat_IsRefused(double baseValue)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => StatLine.From(baseValue, 1));

		Assert.Equal("baseValue", exception.ParamName);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.NegativeInfinity)]
	[InlineData(1e30)]
	[InlineData(-0.5)]
	public void From_AGrowthThatIsNotAStat_IsRefused(double growth)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => StatLine.From(10, growth));

		Assert.Equal("growth", exception.ParamName);
	}

	[Fact]
	public void Constructor_ANegativeBase_IsRefused()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StatLine(-0.01m, 1));

		Assert.Equal("baseValue", exception.ParamName);
	}

	[Fact]
	public void Constructor_ANegativeGrowth_IsRefused()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new StatLine(10, -0.01m));

		Assert.Equal("growth", exception.ParamName);
	}
}
