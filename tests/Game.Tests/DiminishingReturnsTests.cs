using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DiminishingReturns"/>: the shape of the curve, and that a wrong
/// input is refused instead of answered.
/// </summary>
public class DiminishingReturnsTests
{
	[Fact]
	public void AStatOfZero_GivesZero()
	{
		Assert.Equal(0, DiminishingReturns.Percentage(cap: 90, stat: 0, k: 100));
	}

	[Fact]
	public void AStatEqualToK_GivesHalfTheCap()
	{
		Assert.Equal(45, DiminishingReturns.Percentage(cap: 90, stat: 100, k: 100), precision: 10);
	}

	[Theory]
	[InlineData(90, 50, 100, 30)]
	[InlineData(90, 300, 100, 67.5)]
	[InlineData(80, 25, 75, 20)]
	[InlineData(95, 10, 190, 4.75)]
	public void KnownPoints_OfTheCurve(double cap, double stat, double k, double expected)
	{
		Assert.Equal(expected, DiminishingReturns.Percentage(cap, stat, k), precision: 10);
	}

	[Fact]
	public void TheResult_IsNotRounded()
	{
		// 90 / 7: rounding to any number of decimals a game would show loses it.
		Assert.Equal(12.857142857142858, DiminishingReturns.Percentage(cap: 90, stat: 1, k: 6), precision: 12);
	}

	[Fact]
	public void AHigherStat_AlwaysGivesMore_ButEachPointGivesLess()
	{
		double previous = DiminishingReturns.Percentage(90, 0, 100);
		double previousGain = double.MaxValue;

		for (int stat = 1; stat <= 2000; stat++)
		{
			double current = DiminishingReturns.Percentage(90, stat, 100);
			double gain = current - previous;

			Assert.True(gain > 0, $"A stat of {stat} gave no more than {stat - 1}.");
			Assert.True(gain < previousGain, $"The point that reaches {stat} gave as much as the one before it.");

			previous = current;
			previousGain = gain;
		}
	}

	[Theory]
	[InlineData(1_000)]
	[InlineData(1_000_000)]
	[InlineData(int.MaxValue)]
	public void TheCap_IsApproachedAndNeverReached(double stat)
	{
		double result = DiminishingReturns.Percentage(cap: 90, stat: stat, k: 100);

		Assert.True(result < 90, $"A stat of {stat} reached the cap: {result}.");
		Assert.True(result > 81, $"A stat of {stat} stayed far from the cap: {result}.");
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-0.001)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void AStatThatIsNotZeroOrMore_IsRefused(double stat)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => DiminishingReturns.Percentage(90, stat, 100));

		Assert.Equal("stat", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-100)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void AKThatIsNotAboveZero_IsRefused(double k)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => DiminishingReturns.Percentage(90, 50, k));

		Assert.Equal("k", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-90)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void ACapThatIsNotAboveZero_IsRefused(double cap)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => DiminishingReturns.Percentage(cap, 50, 100));

		Assert.Equal("cap", thrown.ParamName);
	}
}
