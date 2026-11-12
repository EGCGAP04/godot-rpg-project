using System;
using System.Collections.Generic;
using System.Linq;

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

	// How many points of a stat make one percentage point. They are part of each formula's
	// shape and not of the tuning: the design tunes the caps, the floors and the bases.
	private const double AttackDamagePerCriticalDamage = 5;
	private const double LuckPerCriticalDamage = 5;
	private const double ArmorPerEvasion = 30;
	private const double AgilityPerEvasion = 20;
	private const double LuckPerEvasion = 10;
	private const double AgilityPerEscape = 20;
	private const double LuckPerEscape = 10;

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

	/// <summary>
	/// What a critical hit deals, as a percentage of the normal damage: the base, plus
	/// the hit rate above 100, plus a fifth of attack damage and a fifth of luck, up to
	/// the cap of the kind of action.
	/// </summary>
	public double CriticalDamage(BaseStats attacker, ActionKind kind)
	{
		double damage = _tuning.CriticalDamageBase
			+ Math.Max(0, attacker.HitRate - HitChanceCeiling)
			+ attacker.AttackDamage / AttackDamagePerCriticalDamage
			+ attacker.Luck / LuckPerCriticalDamage;

		return Math.Min(damage, _tuning.CriticalDamageCap.For(kind));
	}

	/// <summary>
	/// The chance that the defender evades an action that has already landed, from
	/// armor, agility and luck, up to the cap. A skill is evaded half as often as an
	/// attack: the cap applies first and the half after, so a skill's ceiling is half
	/// the cap.
	/// </summary>
	public double EvasionRate(BaseStats defender, ActionKind kind)
	{
		double rate = defender.Armor / ArmorPerEvasion
			+ defender.Agility / AgilityPerEvasion
			+ defender.Luck / LuckPerEvasion;

		double capped = Math.Min(rate, _tuning.EvasionCap);

		return kind switch
		{
			ActionKind.Attack => capped,
			ActionKind.Skill => capped / 2,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of action.")
		};
	}

	/// <summary>
	/// The chance that the allies escape the enemies: the base, raised by the allies'
	/// average agility and luck and lowered by the enemies', between its floor and its cap.
	/// </summary>
	/// <remarks>
	/// Each side needs at least one member. An average over nobody has no value, and
	/// treating it as 0 would answer with the base, as if the two sides were evenly
	/// matched. Who counts on each side (whether a downed ally does) is the caller's
	/// to decide.
	/// </remarks>
	public double EscapeRate(IReadOnlyCollection<BaseStats> allies, IReadOnlyCollection<BaseStats> enemies)
	{
		RequireMembers(allies, nameof(allies));
		RequireMembers(enemies, nameof(enemies));

		double rate = _tuning.EscapeBase
			+ allies.Average(ally => (double)ally.Agility) / AgilityPerEscape
			+ allies.Average(ally => (double)ally.Luck) / LuckPerEscape
			- enemies.Average(enemy => (double)enemy.Agility) / AgilityPerEscape
			- enemies.Average(enemy => (double)enemy.Luck) / LuckPerEscape;

		return Math.Clamp(rate, _tuning.EscapeFloor, _tuning.EscapeCap);
	}

	private static void RequireMembers(IReadOnlyCollection<BaseStats> side, string name)
	{
		if (side == null)
			throw new ArgumentNullException(name);

		if (side.Count == 0)
			throw new ArgumentException("An escape needs at least one member on each side.", name);
	}
}
