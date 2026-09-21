using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CycleProgression"/>, the rule that decides which world is
/// played next. Nothing here touches Godot, so the full sequence, the cycle wrap and
/// every skip are verified without starting the engine.
/// </summary>
public class CycleProgressionTests
{
	[Fact]
	public void Next_FromRealGoesToFantasyInTheSameCycle()
	{
		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Real, 1), WorldUnlocks.All);

		Assert.Equal(new CyclePosition(World.Fantasy, 1), next);
	}

	[Fact]
	public void Next_FromFantasyGoesToNightmareInTheSameCycle()
	{
		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Fantasy, 1), WorldUnlocks.All);

		Assert.Equal(new CyclePosition(World.Nightmare, 1), next);
	}

	[Fact]
	public void Next_FromNightmareWrapsToRealOfTheNextCycle()
	{
		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Nightmare, 1), WorldUnlocks.All);

		Assert.Equal(new CyclePosition(World.Real, 2), next);
	}

	[Fact]
	public void Next_WalksTwoFullCyclesInOrder()
	{
		var position = new CyclePosition(World.Real, CycleProgression.FirstCycle);
		CyclePosition[] expected =
		[
			new(World.Fantasy, 1),
			new(World.Nightmare, 1),
			new(World.Real, 2),
			new(World.Fantasy, 2),
			new(World.Nightmare, 2),
			new(World.Real, 3)
		];

		foreach (CyclePosition step in expected)
		{
			position = CycleProgression.Next(position, WorldUnlocks.All);
			Assert.Equal(step, position);
		}
	}

	[Fact]
	public void Next_SkipsFantasyWhenItIsLockedAndGoesStraightToNightmare()
	{
		var unlocked = new WorldUnlocks(Fantasy: false, Nightmare: true);

		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Real, 4), unlocked);

		Assert.Equal(new CyclePosition(World.Nightmare, 4), next);
	}

	[Fact]
	public void Next_SkipsNightmareWhenItIsLockedAndEndsTheCycleAfterFantasy()
	{
		var unlocked = new WorldUnlocks(Fantasy: true, Nightmare: false);

		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Fantasy, 4), unlocked);

		Assert.Equal(new CyclePosition(World.Real, 5), next);
	}

	[Fact]
	public void Next_WithNothingUnlockedMakesACycleOfTheRealWorldAlone()
	{
		var unlocked = new WorldUnlocks(Fantasy: false, Nightmare: false);

		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Real, 7), unlocked);

		Assert.Equal(new CyclePosition(World.Real, 8), next);
	}

	[Fact]
	public void Next_OnlyLooksAtTheWorldsStillAhead()
	{
		// Standing in a world that is itself locked is reachable in practice: the
		// unlocks are read when the transition happens, and a later system may clear a
		// flag mid-cycle. Only the candidates ahead of the current world matter.
		var unlocked = new WorldUnlocks(Fantasy: false, Nightmare: true);

		CyclePosition next = CycleProgression.Next(new CyclePosition(World.Fantasy, 2), unlocked);

		Assert.Equal(new CyclePosition(World.Nightmare, 2), next);
	}

	[Theory]
	[InlineData(World.Real)]
	[InlineData(World.Fantasy)]
	[InlineData(World.Nightmare)]
	public void Next_FromAnyWorldWithNothingUnlockedStartsTheNextCycleInReal(World from)
	{
		var unlocked = new WorldUnlocks(Fantasy: false, Nightmare: false);

		CyclePosition next = CycleProgression.Next(new CyclePosition(from, 3), unlocked);

		Assert.Equal(new CyclePosition(World.Real, 4), next);
	}

	[Theory]
	[InlineData(World.Real)]
	[InlineData(World.Fantasy)]
	public void Next_LeavesTheCycleNumberAloneWhenItDoesNotWrap(World from)
	{
		CyclePosition next = CycleProgression.Next(new CyclePosition(from, 6), WorldUnlocks.All);

		Assert.Equal(6, next.Cycle);
	}

	[Fact]
	public void Next_RejectsAWorldThatIsNotDeclared()
	{
		var position = new CyclePosition((World)99, 1);

		Assert.Throws<ArgumentOutOfRangeException>(() => CycleProgression.Next(position, WorldUnlocks.All));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Next_RejectsACycleBelowTheFirstOne(int cycle)
	{
		var position = new CyclePosition(World.Real, cycle);

		Assert.Throws<ArgumentOutOfRangeException>(() => CycleProgression.Next(position, WorldUnlocks.All));
	}
}
