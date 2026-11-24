using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatTuningValues"/>: the values are kept as given, and a tuning
/// that would let a landed hit do nothing, or an action do nothing, is refused when it is
/// built.
/// </summary>
public class CombatTuningValuesTests
{
	[Fact]
	public void Constructor_KeepsEachValueInItsOwnProperty()
	{
		var tuning = new CombatTuningValues(
			minimumDamage: 3,
			damageVariationPercent: 12.5,
			defendReductionPercent: 40,
			focusAttackPercent: 60,
			focusRounds: 2,
			meditatePercent: 30,
			meditateMinimum: 4);

		Assert.Equal(3, tuning.MinimumDamage);
		Assert.Equal(12.5, tuning.DamageVariationPercent);
		Assert.Equal(40, tuning.DefendReductionPercent);
		Assert.Equal(60, tuning.FocusAttackPercent);
		Assert.Equal(2, tuning.FocusRounds);
		Assert.Equal(30, tuning.MeditatePercent);
		Assert.Equal(4, tuning.MeditateMinimum);
	}

	[Fact]
	public void TwoTuningsWithTheSameValues_AreEqual()
	{
		Assert.Equal(TestTuning.Combat(), TestTuning.Combat());
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(minimumDamage: 2));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(damageVariationPercent: 11));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(defendReductionPercent: 51));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(focusAttackPercent: 51));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(focusRounds: 4));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(meditatePercent: 26));
		Assert.NotEqual(TestTuning.Combat(), TestTuning.Combat(meditateMinimum: 2));
	}

	[Fact]
	public void AVariationOfZero_IsAccepted_BecauseItTurnsTheVariationOff()
	{
		Assert.Equal(0, TestTuning.Combat(damageVariationPercent: 0).DamageVariationPercent);
	}

	[Fact]
	public void AVariationJustBelowOneHundred_IsAccepted()
	{
		Assert.Equal(99.9, TestTuning.Combat(damageVariationPercent: 99.9).DamageVariationPercent);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void AMinimumDamageBelowOne_IsRefused(int minimumDamage)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(minimumDamage: minimumDamage));

		Assert.Equal("minimumDamage", thrown.ParamName);
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(100)]
	[InlineData(150)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void AVariationOutsideItsRange_IsRefused(double damageVariationPercent)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(damageVariationPercent: damageVariationPercent));

		Assert.Equal("damageVariationPercent", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	public void TheEndsOfTheDefendRange_AreAccepted(int defendReductionPercent)
	{
		Assert.Equal(defendReductionPercent, TestTuning.Combat(defendReductionPercent: defendReductionPercent).DefendReductionPercent);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	[InlineData(int.MinValue)]
	[InlineData(int.MaxValue)]
	public void ADefendReductionOutsideItsRange_IsRefused(int defendReductionPercent)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(defendReductionPercent: defendReductionPercent));

		Assert.Equal("defendReductionPercent", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(int.MaxValue)]
	public void AFocusOfZeroOrMore_IsAccepted(int focusAttackPercent)
	{
		Assert.Equal(focusAttackPercent, TestTuning.Combat(focusAttackPercent: focusAttackPercent).FocusAttackPercent);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void ANegativeFocus_IsRefused(int focusAttackPercent)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(focusAttackPercent: focusAttackPercent));

		Assert.Equal("focusAttackPercent", thrown.ParamName);
	}

	[Fact]
	public void AFocusOfOneRound_IsAccepted()
	{
		Assert.Equal(1, TestTuning.Combat(focusRounds: 1).FocusRounds);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void AFocusOfNoRounds_IsRefused(int focusRounds)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(focusRounds: focusRounds));

		Assert.Equal("focusRounds", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	public void TheEndsOfTheMeditateRange_AreAccepted(int meditatePercent)
	{
		Assert.Equal(meditatePercent, TestTuning.Combat(meditatePercent: meditatePercent).MeditatePercent);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	[InlineData(int.MinValue)]
	[InlineData(int.MaxValue)]
	public void AMeditateShareOutsideItsRange_IsRefused(int meditatePercent)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(meditatePercent: meditatePercent));

		Assert.Equal("meditatePercent", thrown.ParamName);
	}

	[Fact]
	public void AMeditateMinimumOfOne_IsAccepted()
	{
		Assert.Equal(1, TestTuning.Combat(meditateMinimum: 1).MeditateMinimum);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void AMeditateMinimumBelowOne_IsRefused(int meditateMinimum)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => TestTuning.Combat(meditateMinimum: meditateMinimum));

		Assert.Equal("meditateMinimum", thrown.ParamName);
	}

	[Fact]
	public void ATuningNobodyFilledIn_IsRefused()
	{
		// What a resource with every value left at its default would hand over.
		Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuningValues(default, default, default, default, default, default, default));
	}
}
