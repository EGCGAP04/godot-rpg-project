using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="SeededCombatRandom"/>: a seed repeats its rolls, and every roll
/// stays inside the range <see cref="ICombatRandom"/> promises. Nothing here is about how
/// the rolls are spread.
/// </summary>
public class SeededCombatRandomTests
{
	private static double[] Rolls(SeededCombatRandom random, int count) =>
		Enumerable.Range(0, count).Select(_ => random.Percent()).ToArray();

	[Fact]
	public void TheSameSeed_GivesTheSameRolls_InTheSameOrder()
	{
		var first = new SeededCombatRandom(1234);
		var second = new SeededCombatRandom(1234);

		for (int i = 0; i < 50; i++)
		{
			Assert.Equal(first.Percent(), second.Percent());
			Assert.Equal(first.Index(7), second.Index(7));
		}
	}

	[Fact]
	public void DifferentSeeds_GiveDifferentRolls()
	{
		Assert.NotEqual(Rolls(new SeededCombatRandom(1), 10), Rolls(new SeededCombatRandom(2), 10));
	}

	[Fact]
	public void Seed_IsTheOneItWasBuiltWith()
	{
		Assert.Equal(98765, new SeededCombatRandom(98765).Seed);
	}

	[Fact]
	public void WithoutASeed_ItChoosesOne_ThatRepeatsItsRolls()
	{
		var unseeded = new SeededCombatRandom();
		var repeated = new SeededCombatRandom(unseeded.Seed);

		Assert.Equal(Rolls(unseeded, 20), Rolls(repeated, 20));
	}

	[Fact]
	public void Percent_StaysFromZeroUpToButNotIncludingOneHundred()
	{
		var random = new SeededCombatRandom(42);

		Assert.All(Rolls(random, 1000), roll => Assert.InRange(roll, 0, Math.BitDecrement(100.0)));
	}

	[Fact]
	public void Percent_IsNotTheSameNumberEveryTime()
	{
		Assert.True(Rolls(new SeededCombatRandom(42), 10).Distinct().Count() > 1);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(9)]
	public void Index_StaysBelowTheCount(int count)
	{
		var random = new SeededCombatRandom(42);

		for (int i = 0; i < 200; i++)
			Assert.InRange(random.Index(count), 0, count - 1);
	}

	[Fact]
	public void Index_ReachesBothEndsOfTheCount()
	{
		var random = new SeededCombatRandom(42);
		var seen = Enumerable.Range(0, 200).Select(_ => random.Index(3)).Distinct().OrderBy(index => index);

		Assert.Equal(new[] { 0, 1, 2 }, seen);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Index_WithNothingToPickFrom_Throws(int count)
	{
		var random = new SeededCombatRandom(42);

		Assert.Throws<ArgumentOutOfRangeException>(() => random.Index(count));
	}
}
