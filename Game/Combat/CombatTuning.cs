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
/// leaves out of a <c>.tres</c> each property that equals its default. A minimum damage of
/// 0 is refused by <see cref="CombatTuningValues"/>, so a file that lost that line stops
/// whoever reads it. A variation of 0 is a real value, the one that turns it off, so it
/// could vanish from the file in silence; <c>CombatTuningTests</c> requires the file to
/// write both.
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

	/// <summary>
	/// The plain values the fight reads. Throws when a value is missing or wrong, naming
	/// it, so a broken tuning stops whoever asks for it instead of reaching a fight.
	/// </summary>
	public CombatTuningValues ToValues() => new(MinimumDamage, DamageVariationPercent);
}
