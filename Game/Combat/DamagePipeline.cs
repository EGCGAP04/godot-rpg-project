using System;

/// <summary>
/// Works out one attack, from the attacker's accuracy to the damage the defender takes:
/// accuracy, then evasion, then the critical hit, then armor. It changes nobody's health;
/// it answers with an <see cref="AttackResult"/> and whoever asked applies it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rolls, and their order, are part of the contract.</b> Tests script them, and a
/// seed only repeats a fight while they stay the same:
/// </para>
/// <list type="number">
/// <item>Accuracy. If it fails the attack missed, and nothing else is rolled.</item>
/// <item>Evasion. If it comes up the attack was evaded, and nothing else is rolled.</item>
/// <item>The critical hit.</item>
/// <item>The variation of the base damage.</item>
/// </list>
/// <para>
/// A stage that is reached always rolls, even when its chance is 0% or 100%, so how many
/// rolls an attack takes depends on how far it got and never on the stats: one for a
/// miss, two for an evaded attack, four for a hit.
/// </para>
/// <para>
/// <b>The damage is rounded down once, at the end,</b> and a hit that got this far does
/// at least <see cref="CombatTuningValues.MinimumDamage"/>. The arithmetic is in
/// <c>double</c>, where a result that should be a whole number can land a hair under it:
/// 90 damage against a 30% reduction comes out as 62.99999999999999, which would round
/// down to 62. So the result is first rounded to <see cref="NoiseDigits"/> decimals,
/// which only removes that noise. <c>decimal</c> does not solve it here as it does for
/// <c>StatLine</c>: a reduction is a ratio, and one like 6.666…% has no exact decimal
/// either, so 15 damage against it comes out as 13.99… instead of 14.
/// </para>
/// </remarks>
public sealed class DamagePipeline
{
	/// <summary>
	/// The decimals kept before rounding down. Far more than any damage needs, and far
	/// fewer than the sixteen or so digits a <c>double</c> carries, so only the arithmetic's
	/// own noise falls outside.
	/// </summary>
	private const int NoiseDigits = 9;

	private readonly StatFormulas _formulas;
	private readonly CombatTuningValues _tuning;

	public DamagePipeline(StatFormulas formulas, CombatTuningValues tuning)
	{
		_formulas = formulas ?? throw new ArgumentNullException(nameof(formulas));
		_tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
	}

	public AttackResult Resolve(BaseStats attacker, BaseStats defender, ActionKind kind, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random);

		if (!ComesUp(_formulas.HitChance(attacker), random))
			return AttackResult.Missed;

		if (ComesUp(_formulas.EvasionRate(defender, kind), random))
			return AttackResult.Evaded;

		bool isCritical = ComesUp(_formulas.CriticalChance(attacker, kind), random);

		double damage = attacker.AttackDamage * VariationFactor(random.Percent());

		if (isCritical)
			damage *= _formulas.CriticalDamage(attacker, kind) / 100;

		damage *= 1 - _formulas.ArmorReduction(defender, kind) / 100;

		return AttackResult.Hit(RoundDown(damage), isCritical);
	}

	private static bool ComesUp(double chance, ICombatRandom random) => random.Percent() < chance;

	/// <summary>
	/// What the base damage is multiplied by: from <c>1 - x</c> at a roll of 0 up to, but
	/// not reaching, <c>1 + x</c>, where <c>x</c> is the tuning's variation. A roll of 50
	/// is exactly 1, whatever the variation.
	/// </summary>
	private double VariationFactor(double roll) =>
		1 + _tuning.DamageVariationPercent / 100 * ((roll - 50) / 50);

	private int RoundDown(double damage)
	{
		double whole = Math.Floor(Math.Round(damage, NoiseDigits));

		if (whole >= int.MaxValue)
			return int.MaxValue;

		return Math.Max(_tuning.MinimumDamage, (int)whole);
	}
}
