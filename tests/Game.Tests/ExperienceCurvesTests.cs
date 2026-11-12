using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="ExperienceCurves"/> and <see cref="LevelProgress"/>: each world
/// uses its own curve, and the Real world has none.
/// </summary>
public class ExperienceCurvesTests
{
	/// <summary>Two different curves, so a world reading the other's shows: level 1 costs 10 and 100.</summary>
	private static ExperienceCurves Curves() => new(
		fantasy: new ExperienceCurve(10, 2, 99),
		nightmare: new ExperienceCurve(100, 2, 50));

	[Fact]
	public void For_GivesEachWorldItsOwnCurve()
	{
		ExperienceCurves curves = Curves();

		Assert.Same(curves.Fantasy, curves.For(World.Fantasy));
		Assert.Same(curves.Nightmare, curves.For(World.Nightmare));
		Assert.NotSame(curves.Fantasy, curves.Nightmare);
	}

	[Fact]
	public void For_TheRealWorld_HasNoCurve()
	{
		Assert.Null(Curves().For(World.Real));
	}

	[Fact]
	public void For_SomethingThatIsNotAWorld_IsRefused()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Curves().For((World)42));
	}

	[Fact]
	public void Add_UsesTheCurveOfTheWorld()
	{
		var start = new LevelProgress(1, 0);

		Assert.Equal(new LevelProgress(2, 5), Curves().Add(World.Fantasy, start, 15));
		Assert.Equal(new LevelProgress(1, 15), Curves().Add(World.Nightmare, start, 15));
	}

	[Fact]
	public void Add_InTheRealWorld_DoesNothing()
	{
		// There a level is an age: 12 stays 12 whatever is awarded.
		var age = new LevelProgress(12, 0);

		Assert.Equal(age, Curves().Add(World.Real, age, 1_000_000));
	}

	[Fact]
	public void Add_ANegativeAward_IsRefusedInEveryWorld()
	{
		foreach (World world in Enum.GetValues<World>())
			Assert.Throws<ArgumentOutOfRangeException>(() => Curves().Add(world, new LevelProgress(1, 0), -1));
	}

	[Fact]
	public void Constructor_AMissingCurve_IsRefused()
	{
		var curve = new ExperienceCurve(10, 2, 99);

		Assert.Throws<ArgumentNullException>(() => new ExperienceCurves(null, curve));
		Assert.Throws<ArgumentNullException>(() => new ExperienceCurves(curve, null));
	}

	[Fact]
	public void LevelProgress_KeepsItsLevelAndExperience()
	{
		var progress = new LevelProgress(7, 123_456_789_012L);

		Assert.Equal(7, progress.Level);
		Assert.Equal(123_456_789_012L, progress.Experience);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-3)]
	public void LevelProgress_ALevelBelowOne_IsRefused(int level)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new LevelProgress(level, 0));

		Assert.Equal("level", exception.ParamName);
	}

	[Fact]
	public void LevelProgress_NegativeExperience_IsRefused()
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new LevelProgress(1, -1));

		Assert.Equal("experience", exception.ParamName);
	}
}
