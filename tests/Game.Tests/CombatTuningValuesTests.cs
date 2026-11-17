using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatTuningValues"/>: the values are kept as given, and a tuning
/// that would let a landed hit do nothing is refused when it is built.
/// </summary>
public class CombatTuningValuesTests
{
	[Fact]
	public void Constructor_KeepsEachValueInItsOwnProperty()
	{
		var tuning = new CombatTuningValues(minimumDamage: 3, damageVariationPercent: 12.5);

		Assert.Equal(3, tuning.MinimumDamage);
		Assert.Equal(12.5, tuning.DamageVariationPercent);
	}

	[Fact]
	public void TwoTuningsWithTheSameValues_AreEqual()
	{
		Assert.Equal(new CombatTuningValues(1, 10), new CombatTuningValues(1, 10));
		Assert.NotEqual(new CombatTuningValues(1, 10), new CombatTuningValues(2, 10));
		Assert.NotEqual(new CombatTuningValues(1, 10), new CombatTuningValues(1, 11));
	}

	[Fact]
	public void AVariationOfZero_IsAccepted_BecauseItTurnsTheVariationOff()
	{
		Assert.Equal(0, new CombatTuningValues(1, 0).DamageVariationPercent);
	}

	[Fact]
	public void AVariationJustBelowOneHundred_IsAccepted()
	{
		Assert.Equal(99.9, new CombatTuningValues(1, 99.9).DamageVariationPercent);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void AMinimumDamageBelowOne_IsRefused(int minimumDamage)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuningValues(minimumDamage, 10));

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
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuningValues(1, damageVariationPercent));

		Assert.Equal("damageVariationPercent", thrown.ParamName);
	}

	[Fact]
	public void ATuningNobodyFilledIn_IsRefused()
	{
		// What a resource with every value left at its default would hand over.
		Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuningValues(default, default));
	}
}
