using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="ScriptedCombatRandom"/>, the dice every combat test rolls with:
/// it hands out what was written, in order, and a script that does not match what the
/// code asks for fails loudly.
/// </summary>
public class ScriptedCombatRandomTests
{
	[Fact]
	public void Rolls_ComeOutInTheOrderTheyWereWritten()
	{
		ICombatRandom random = new ScriptedCombatRandom().Percent(10).Index(2).Percent(99.5).Index(0);

		Assert.Equal(10, random.Percent());
		Assert.Equal(2, random.Index(3));
		Assert.Equal(99.5, random.Percent());
		Assert.Equal(0, random.Index(1));
	}

	[Fact]
	public void Remaining_CountsTheRollsNotTakenYet()
	{
		var scripted = new ScriptedCombatRandom().Percent(10).Percent(20);
		ICombatRandom random = scripted;

		Assert.Equal(2, scripted.Remaining);
		random.Percent();
		Assert.Equal(1, scripted.Remaining);
		random.Percent();
		Assert.Equal(0, scripted.Remaining);
	}

	[Fact]
	public void AskingForAPercent_WhenAnIndexComesNext_Throws_AndTakesNothing()
	{
		var scripted = new ScriptedCombatRandom().Index(1);
		ICombatRandom random = scripted;

		var thrown = Assert.Throws<InvalidOperationException>(() => random.Percent());

		Assert.Contains("Index(1)", thrown.Message);
		Assert.Equal(1, scripted.Remaining);
	}

	[Fact]
	public void AskingForAnIndex_WhenAPercentComesNext_Throws_AndTakesNothing()
	{
		var scripted = new ScriptedCombatRandom().Percent(50);
		ICombatRandom random = scripted;

		var thrown = Assert.Throws<InvalidOperationException>(() => random.Index(4));

		Assert.Contains("Percent(50)", thrown.Message);
		Assert.Equal(1, scripted.Remaining);
	}

	[Fact]
	public void AskingForMoreRollsThanWereWritten_Throws()
	{
		ICombatRandom random = new ScriptedCombatRandom().Percent(50);
		random.Percent();

		var thrown = Assert.Throws<InvalidOperationException>(() => random.Percent());

		Assert.Contains("Roll 2", thrown.Message);
		Assert.Throws<InvalidOperationException>(() => random.Index(2));
	}

	[Fact]
	public void AnEmptyScript_HasNoRollToGive()
	{
		ICombatRandom random = new ScriptedCombatRandom();

		Assert.Throws<InvalidOperationException>(() => random.Percent());
		Assert.Throws<InvalidOperationException>(() => random.Index(1));
	}

	[Fact]
	public void AScriptedIndex_ThatDoesNotFitTheCount_Throws_AndTakesNothing()
	{
		var scripted = new ScriptedCombatRandom().Index(2);
		ICombatRandom random = scripted;

		Assert.Throws<InvalidOperationException>(() => random.Index(2));

		Assert.Equal(1, scripted.Remaining);
		Assert.Equal(2, random.Index(3));
	}

	[Fact]
	public void AScriptedIndex_ThatNamesItsCount_IsGivenOnlyForThatCount()
	{
		var scripted = new ScriptedCombatRandom().Index(1, among: 3);
		ICombatRandom random = scripted;

		var tooMany = Assert.Throws<InvalidOperationException>(() => random.Index(4));
		Assert.Throws<InvalidOperationException>(() => random.Index(2));

		Assert.Contains("Index(1, among: 3)", tooMany.Message);
		Assert.Equal(1, scripted.Remaining);
		Assert.Equal(1, random.Index(3));
	}

	[Theory]
	[InlineData(2, 2)]
	[InlineData(2, 1)]
	[InlineData(0, 0)]
	public void AScriptedIndex_CannotNameACountItDoesNotFit(int roll, int among)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptedCombatRandom().Index(roll, among));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void AskingForAnIndex_WithNothingToPickFrom_Throws(int count)
	{
		ICombatRandom random = new ScriptedCombatRandom().Index(0);

		Assert.Throws<ArgumentOutOfRangeException>(() => random.Index(count));
	}

	[Fact]
	public void RequireUsedUp_Passes_OnceEveryRollWasTaken()
	{
		var scripted = new ScriptedCombatRandom().Percent(50).Index(0);
		ICombatRandom random = scripted;
		random.Percent();
		random.Index(1);

		scripted.RequireUsedUp();
	}

	[Fact]
	public void RequireUsedUp_Throws_WhileARollIsLeftOver_AndNamesIt()
	{
		var scripted = new ScriptedCombatRandom().Percent(50).Index(3);
		ICombatRandom random = scripted;
		random.Percent();

		var thrown = Assert.Throws<InvalidOperationException>(scripted.RequireUsedUp);

		Assert.Contains("Index(3)", thrown.Message);
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(100)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void APercentRoll_OutsideWhatTheGameCanRoll_CannotBeScripted(double roll)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptedCombatRandom().Percent(roll));
	}

	[Fact]
	public void TheEndsOfAPercentRoll_CanBeScripted()
	{
		ICombatRandom random = new ScriptedCombatRandom().Percent(0).Percent(Math.BitDecrement(100.0));

		Assert.Equal(0, random.Percent());
		Assert.True(random.Percent() < 100);
	}

	[Fact]
	public void ANegativeIndex_CannotBeScripted()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptedCombatRandom().Index(-1));
	}
}
