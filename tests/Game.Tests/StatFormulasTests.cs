using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatFormulas"/> with <see cref="TestTuning"/>'s numbers, where
/// <c>K</c> is 100 against attacks and 50 against skills.
/// </summary>
public class StatFormulasTests
{
	private readonly StatFormulas _formulas = TestTuning.Formulas();

	[Fact]
	public void Constructor_RefusesAMissingTuning()
	{
		Assert.Throws<ArgumentNullException>(() => new StatFormulas(null!));
	}

	// --- Armor reduction ---

	[Theory]
	[InlineData(0, ActionKind.Attack, 0)]
	[InlineData(100, ActionKind.Attack, 45)]
	[InlineData(300, ActionKind.Attack, 67.5)]
	[InlineData(0, ActionKind.Skill, 0)]
	[InlineData(50, ActionKind.Skill, 40)]
	[InlineData(150, ActionKind.Skill, 60)]
	public void ArmorReduction_FollowsTheCurve_WithEachKindsCapAndK(int armor, ActionKind kind, double expected)
	{
		Assert.Equal(expected, _formulas.ArmorReduction(TestTuning.Stats(armor: armor), kind), precision: 10);
	}

	[Fact]
	public void ArmorReduction_StaysBelowEachKindsCap()
	{
		BaseStats wall = TestTuning.Stats(armor: int.MaxValue);

		Assert.InRange(_formulas.ArmorReduction(wall, ActionKind.Attack), 89.9, 89.9999999999);
		Assert.InRange(_formulas.ArmorReduction(wall, ActionKind.Skill), 79.9, 79.9999999999);
	}

	[Fact]
	public void ArmorReduction_ReadsArmorAndNothingElse()
	{
		BaseStats onlyArmor = TestTuning.Stats(armor: 100);
		BaseStats everything = new(999, 999, 100, 999, 999, 999, 999);

		Assert.Equal(_formulas.ArmorReduction(onlyArmor, ActionKind.Attack), _formulas.ArmorReduction(everything, ActionKind.Attack));
	}

	// --- Critical chance ---

	[Theory]
	[InlineData(0, ActionKind.Attack, 0)]
	[InlineData(100, ActionKind.Attack, 47.5)]
	[InlineData(300, ActionKind.Attack, 71.25)]
	[InlineData(0, ActionKind.Skill, 0)]
	[InlineData(50, ActionKind.Skill, 45)]
	[InlineData(150, ActionKind.Skill, 67.5)]
	public void CriticalChance_FollowsTheCurve_WithEachKindsCapAndK(int luck, ActionKind kind, double expected)
	{
		Assert.Equal(expected, _formulas.CriticalChance(TestTuning.Stats(luck: luck), kind), precision: 10);
	}

	[Fact]
	public void CriticalChance_StaysBelowEachKindsCap()
	{
		BaseStats charmed = TestTuning.Stats(luck: int.MaxValue);

		Assert.InRange(_formulas.CriticalChance(charmed, ActionKind.Attack), 94.9, 94.9999999999);
		Assert.InRange(_formulas.CriticalChance(charmed, ActionKind.Skill), 89.9, 89.9999999999);
	}

	[Fact]
	public void CriticalChance_ReadsLuckAndNothingElse()
	{
		BaseStats onlyLuck = TestTuning.Stats(luck: 100);
		BaseStats everything = new(999, 999, 999, 999, 999, 100, 999);

		Assert.Equal(_formulas.CriticalChance(onlyLuck, ActionKind.Attack), _formulas.CriticalChance(everything, ActionKind.Attack));
	}

	[Fact]
	public void AKindThatIsNeither_IsRefused()
	{
		BaseStats stats = TestTuning.Stats(armor: 10, luck: 10);

		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.ArmorReduction(stats, (ActionKind)7));
		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.CriticalChance(stats, (ActionKind)7));
	}

	// --- Hit chance ---

	[Theory]
	[InlineData(0, 25)]
	[InlineData(24, 25)]
	[InlineData(25, 25)]
	[InlineData(26, 26)]
	[InlineData(60, 60)]
	[InlineData(99, 99)]
	[InlineData(100, 100)]
	[InlineData(101, 100)]
	[InlineData(int.MaxValue, 100)]
	public void HitChance_IsTheHitRate_BetweenItsFloorAndOneHundred(int hitRate, double expected)
	{
		Assert.Equal(expected, _formulas.HitChance(TestTuning.Stats(hitRate: hitRate)));
	}

	[Fact]
	public void HitChance_UsesTheTuningsFloor()
	{
		var formulas = new StatFormulas(TestTuning.Values(hitChanceFloor: 40));

		Assert.Equal(40, formulas.HitChance(TestTuning.Stats(hitRate: 10)));
		Assert.Equal(41, formulas.HitChance(TestTuning.Stats(hitRate: 41)));
	}

	[Fact]
	public void TheTuning_ReachesTheCurves()
	{
		var formulas = new StatFormulas(TestTuning.Values(
			armorK: new ByAction(300, 50),
			armorReductionCap: new ByAction(60, 80),
			luckK: new ByAction(100, 150),
			criticalChanceCap: new ByAction(95, 40)));

		Assert.Equal(15, formulas.ArmorReduction(TestTuning.Stats(armor: 100), ActionKind.Attack), precision: 10);
		Assert.Equal(10, formulas.CriticalChance(TestTuning.Stats(luck: 50), ActionKind.Skill), precision: 10);
	}
}
