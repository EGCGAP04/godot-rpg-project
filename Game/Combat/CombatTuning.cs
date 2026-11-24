using Godot;

/// <summary>
/// The tuning of combat itself as a resource, so its provisional numbers can be changed in
/// the Inspector, in <c>Combat/combat_tuning.tres</c>, without touching code.
/// </summary>
/// <remarks>
/// <para>
/// The fight's rules never see this class. A resource is a Godot type, and they are
/// engine-free, so <see cref="ToValues"/> builds the plain <see cref="CombatTuningValues"/>
/// they read, which is also where the numbers are checked.
/// </para>
/// <para>
/// Every default is zero, never a real value, as in <see cref="StatTuning"/>: the editor
/// leaves out of a <c>.tres</c> each property that equals its default. A minimum damage, a
/// focus length or a meditation minimum of 0 is refused by <see cref="CombatTuningValues"/>,
/// so a file that lost one of those lines stops whoever reads it. The others can be a real
/// 0 (a variation of 0 turns it off), so they could vanish from the file in silence;
/// <c>CombatTuningTests</c> requires the file to write every value.
/// </para>
/// <para>
/// A property added here needs its line in the file and its parameter in
/// <see cref="CombatTuningValues"/>.
/// </para>
/// </remarks>
[GlobalClass]
public partial class CombatTuning : Resource
{
	[ExportGroup("Damage")]

	/// <summary>The least damage a hit that lands can do, whatever the armor.</summary>
	[Export]
	public int MinimumDamage { get; set; }

	/// <summary>How far a hit's base damage can move either way, as a percentage: 10 is from 90% to 110%.</summary>
	[Export]
	public double DamageVariationPercent { get; set; }

	[ExportGroup("Defend")]

	/// <summary>How much less damage a member who defends takes, as a percentage, after the armor.</summary>
	[Export]
	public int DefendReductionPercent { get; set; }

	[ExportGroup("Focus")]

	/// <summary>How much a focus raises the attack, as a percentage.</summary>
	[Export]
	public int FocusAttackPercent { get; set; }

	/// <summary>How many rounds a focus lasts, after the round the member focused in.</summary>
	[Export]
	public int FocusRounds { get; set; }

	[ExportGroup("Meditate")]

	/// <summary>The share of the most mana a meditation gives back, as a percentage.</summary>
	[Export]
	public int MeditatePercent { get; set; }

	/// <summary>The least mana a meditation gives back.</summary>
	[Export]
	public int MeditateMinimum { get; set; }

	/// <summary>
	/// The plain values the fight reads. Throws when a value is missing or wrong, naming
	/// it, so a broken tuning stops whoever asks for it instead of reaching a fight.
	/// </summary>
	public CombatTuningValues ToValues() =>
		new(MinimumDamage, DamageVariationPercent, DefendReductionPercent, FocusAttackPercent, FocusRounds, MeditatePercent, MeditateMinimum);
}
