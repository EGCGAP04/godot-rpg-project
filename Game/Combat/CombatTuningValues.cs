using System;

/// <summary>
/// The numbers combat itself can be tuned with, as plain values: the fight's rules read
/// them from here and never from a Godot type. They are apart from
/// <see cref="StatTuningValues"/> because those tune how a stat becomes a percentage, and
/// these tune what a fight does with the result.
/// </summary>
/// <remarks>
/// <para>
/// All of them are provisional. They are checked here, once, when they are built: a
/// minimum of 0 would let a hit that landed do nothing, a variation of 100% would let it
/// roll down to nothing, a focus of no rounds would be no focus, and a meditation with no
/// minimum could give nothing back. A resource whose values were never written is all
/// zeros, and the minimum damage is what refuses it.
/// </para>
/// <para>
/// The numbers of the actions (#105) are whole numbers, so the attack a focus raises and
/// the mana a meditation gives back come out exact, with no <c>double</c> noise to round
/// away.
/// </para>
/// </remarks>
public sealed record CombatTuningValues
{
	public CombatTuningValues(
		int minimumDamage,
		double damageVariationPercent,
		int defendReductionPercent,
		int focusAttackPercent,
		int focusRounds,
		int meditatePercent,
		int meditateMinimum)
	{
		if (minimumDamage < 1)
			throw new ArgumentOutOfRangeException(nameof(minimumDamage), minimumDamage, "A hit that lands does some damage: the minimum must be 1 or more.");

		if (!double.IsFinite(damageVariationPercent) || damageVariationPercent < 0 || damageVariationPercent >= 100)
			throw new ArgumentOutOfRangeException(nameof(damageVariationPercent), damageVariationPercent, "The damage variation is a percentage: 0 or more, and below 100.");

		if (defendReductionPercent is < 0 or > 100)
			throw new ArgumentOutOfRangeException(nameof(defendReductionPercent), defendReductionPercent, "Defending takes off a percentage of the damage: from 0 to 100.");

		if (focusAttackPercent < 0)
			throw new ArgumentOutOfRangeException(nameof(focusAttackPercent), focusAttackPercent, "Focusing raises the attack: by 0% or more.");

		if (focusRounds < 1)
			throw new ArgumentOutOfRangeException(nameof(focusRounds), focusRounds, "A focus lasts for some rounds: 1 or more.");

		if (meditatePercent is < 0 or > 100)
			throw new ArgumentOutOfRangeException(nameof(meditatePercent), meditatePercent, "Meditating gives back a share of the most mana: from 0 to 100.");

		if (meditateMinimum < 1)
			throw new ArgumentOutOfRangeException(nameof(meditateMinimum), meditateMinimum, "Meditating gives something back: the minimum must be 1 or more.");

		MinimumDamage = minimumDamage;
		DamageVariationPercent = damageVariationPercent;
		DefendReductionPercent = defendReductionPercent;
		FocusAttackPercent = focusAttackPercent;
		FocusRounds = focusRounds;
		MeditatePercent = meditatePercent;
		MeditateMinimum = meditateMinimum;
	}

	/// <summary>The least damage a hit that lands can do, whatever the armor.</summary>
	public int MinimumDamage { get; }

	/// <summary>
	/// How far the base damage of a hit can move either way, as a percentage: 10 lets it
	/// land anywhere from 90% to 110%. 0 turns the variation off.
	/// </summary>
	public double DamageVariationPercent { get; }

	/// <summary>
	/// How much less damage a member who defends takes, as a percentage, taken out after
	/// the armor and outside its cap. A hit still does <see cref="MinimumDamage"/>.
	/// </summary>
	public int DefendReductionPercent { get; }

	/// <summary>How much a focus raises the member's attack, as a percentage: 50 makes 4 into 6.</summary>
	public int FocusAttackPercent { get; }

	/// <summary>How many rounds a focus lasts, counted from the round after the one the member focused in.</summary>
	public int FocusRounds { get; }

	/// <summary>The share of the most mana a meditation gives back, as a percentage, rounded down.</summary>
	public int MeditatePercent { get; }

	/// <summary>The least mana a meditation gives back, whatever the share comes to.</summary>
	public int MeditateMinimum { get; }
}
