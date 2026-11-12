using System;

/// <summary>
/// Every number the stat formulas can be tuned with, as plain values:
/// <see cref="StatFormulas"/> reads them from here and never from a Godot type. In the
/// game they come from the <c>StatTuning</c> resource; a test builds them directly.
/// </summary>
/// <remarks>
/// <para>
/// All of them are provisional, which is why none is a constant inside a formula. Every
/// percentage is written as the design writes it: 90 is 90%.
/// </para>
/// <para>
/// The values are checked here, once, when they are built. A <c>K</c> of 0, a cap of 0 or
/// a floor above its cap would not crash a formula: each would quietly return a
/// believable percentage. A resource whose values were never written is all zeros, and
/// this is also where that is caught.
/// </para>
/// </remarks>
public sealed record StatTuningValues
{
	public StatTuningValues(
		ByAction armorK,
		ByAction armorReductionCap,
		ByAction luckK,
		ByAction criticalChanceCap,
		double hitChanceFloor,
		double criticalDamageBase,
		ByAction criticalDamageCap,
		double evasionCap,
		double escapeBase,
		double escapeFloor,
		double escapeCap)
	{
		AboveZero(armorK, nameof(armorK));
		Percentage(armorReductionCap, nameof(armorReductionCap));
		AboveZero(luckK, nameof(luckK));
		Percentage(criticalChanceCap, nameof(criticalChanceCap));
		Percentage(hitChanceFloor, nameof(hitChanceFloor));
		AboveZero(criticalDamageBase, nameof(criticalDamageBase));
		AboveZero(criticalDamageCap, nameof(criticalDamageCap));
		Percentage(evasionCap, nameof(evasionCap));
		Percentage(escapeBase, nameof(escapeBase));
		Percentage(escapeFloor, nameof(escapeFloor));
		Percentage(escapeCap, nameof(escapeCap));

		if (criticalDamageCap.Attack < criticalDamageBase || criticalDamageCap.Skill < criticalDamageBase)
			throw new ArgumentOutOfRangeException(nameof(criticalDamageCap), criticalDamageCap, $"A critical damage cap cannot be below the base, {criticalDamageBase}.");

		if (escapeFloor > escapeCap)
			throw new ArgumentOutOfRangeException(nameof(escapeFloor), escapeFloor, $"The escape floor cannot be above the escape cap, {escapeCap}.");

		ArmorK = armorK;
		ArmorReductionCap = armorReductionCap;
		LuckK = luckK;
		CriticalChanceCap = criticalChanceCap;
		HitChanceFloor = hitChanceFloor;
		CriticalDamageBase = criticalDamageBase;
		CriticalDamageCap = criticalDamageCap;
		EvasionCap = evasionCap;
		EscapeBase = escapeBase;
		EscapeFloor = escapeFloor;
		EscapeCap = escapeCap;
	}

	/// <summary>The armor that gives half of <see cref="ArmorReductionCap"/>.</summary>
	public ByAction ArmorK { get; }

	/// <summary>The damage reduction armor approaches and never reaches, as a percentage.</summary>
	public ByAction ArmorReductionCap { get; }

	/// <summary>The luck that gives half of <see cref="CriticalChanceCap"/>.</summary>
	public ByAction LuckK { get; }

	/// <summary>The critical chance luck approaches and never reaches, as a percentage.</summary>
	public ByAction CriticalChanceCap { get; }

	/// <summary>The lowest chance to hit, whatever the hit rate, as a percentage.</summary>
	public double HitChanceFloor { get; }

	/// <summary>Critical damage before any stat adds to it, as a percentage of the normal damage.</summary>
	public double CriticalDamageBase { get; }

	/// <summary>The highest critical damage, as a percentage of the normal damage.</summary>
	public ByAction CriticalDamageCap { get; }

	/// <summary>The highest evasion rate against an attack, as a percentage. A skill is evaded half as often.</summary>
	public double EvasionCap { get; }

	/// <summary>The chance to escape when both sides are evenly matched, as a percentage.</summary>
	public double EscapeBase { get; }

	/// <summary>The lowest chance to escape, as a percentage.</summary>
	public double EscapeFloor { get; }

	/// <summary>The highest chance to escape, as a percentage.</summary>
	public double EscapeCap { get; }

	private static void AboveZero(ByAction value, string name)
	{
		AboveZero(value.Attack, name);
		AboveZero(value.Skill, name);
	}

	private static void AboveZero(double value, string name)
	{
		if (!double.IsFinite(value) || value <= 0)
			throw new ArgumentOutOfRangeException(name, value, "This tuning value must be a number above 0.");
	}

	private static void Percentage(ByAction value, string name)
	{
		Percentage(value.Attack, name);
		Percentage(value.Skill, name);
	}

	private static void Percentage(double value, string name)
	{
		if (!double.IsFinite(value) || value <= 0 || value > 100)
			throw new ArgumentOutOfRangeException(name, value, "This tuning value is a percentage: above 0, and 100 at most.");
	}
}
