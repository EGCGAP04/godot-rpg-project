using System;

/// <summary>
/// The formulas that turn base stats into what a fight uses. Each one answers with a
/// percentage, written as the design writes it (90 is 90%), in <c>double</c> and
/// unrounded: rounding belongs to whoever applies the result.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here rolls. A formula returns a chance, and deciding whether that chance
/// comes up needs a source of randomness, which is not this class's.
/// </para>
/// <para>
/// The tuning comes in through the constructor, already checked by
/// <see cref="StatTuningValues"/>, and the stats come already checked by
/// <see cref="BaseStats"/>, so no formula validates its numbers again.
/// </para>
/// </remarks>
public sealed class StatFormulas
{
	/// <summary>A chance to hit never goes above this, whatever the hit rate.</summary>
	public const double HitChanceCeiling = 100;

	private readonly StatTuningValues _tuning;

	public StatFormulas(StatTuningValues tuning)
	{
		_tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
	}

	/// <summary>
	/// The percentage of incoming damage the defender's armor removes, with diminishing
	/// returns. Armor stops less of a skill than of an attack.
	/// </summary>
	public double ArmorReduction(BaseStats defender, ActionKind kind) =>
		DiminishingReturns.Percentage(_tuning.ArmorReductionCap.For(kind), defender.Armor, _tuning.ArmorK.For(kind));

	/// <summary>The chance that the attacker's action is a critical hit, from luck, with diminishing returns.</summary>
	public double CriticalChance(BaseStats attacker, ActionKind kind) =>
		DiminishingReturns.Percentage(_tuning.CriticalChanceCap.For(kind), attacker.Luck, _tuning.LuckK.For(kind));

	/// <summary>
	/// The chance that the attacker's action lands: one point of hit rate is 1%, never
	/// below the floor and never above 100%. Hit rate above 100 is not lost: it feeds
	/// critical damage.
	/// </summary>
	public double HitChance(BaseStats attacker) =>
		Math.Clamp(attacker.HitRate, _tuning.HitChanceFloor, HitChanceCeiling);
}
