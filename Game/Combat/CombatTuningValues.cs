using System;

/// <summary>
/// The numbers combat itself can be tuned with, as plain values: the fight's rules read
/// them from here and never from a Godot type. They are apart from
/// <see cref="StatTuningValues"/> because those tune how a stat becomes a percentage, and
/// these tune what a fight does with the result.
/// </summary>
/// <remarks>
/// Both are provisional. They are checked here, once, when they are built: a minimum of 0
/// would let a hit that landed do nothing, and a variation of 100% would let it roll down
/// to nothing. A resource whose values were never written is all zeros, and the minimum
/// damage is what refuses it.
/// </remarks>
public sealed record CombatTuningValues
{
	public CombatTuningValues(int minimumDamage, double damageVariationPercent)
	{
		if (minimumDamage < 1)
			throw new ArgumentOutOfRangeException(nameof(minimumDamage), minimumDamage, "A hit that lands does some damage: the minimum must be 1 or more.");

		if (!double.IsFinite(damageVariationPercent) || damageVariationPercent < 0 || damageVariationPercent >= 100)
			throw new ArgumentOutOfRangeException(nameof(damageVariationPercent), damageVariationPercent, "The damage variation is a percentage: 0 or more, and below 100.");

		MinimumDamage = minimumDamage;
		DamageVariationPercent = damageVariationPercent;
	}

	/// <summary>The least damage a hit that lands can do, whatever the armor.</summary>
	public int MinimumDamage { get; }

	/// <summary>
	/// How far the base damage of a hit can move either way, as a percentage: 10 lets it
	/// land anywhere from 90% to 110%. 0 turns the variation off.
	/// </summary>
	public double DamageVariationPercent { get; }
}
