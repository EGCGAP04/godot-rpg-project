using Godot;

/// <summary>
/// The tuning of the stat formulas and of the experience curves as a resource, so every
/// provisional number can be changed in the Inspector, in <c>Stats/stat_tuning.tres</c>, without touching code.
/// </summary>
/// <remarks>
/// <para>
/// The formulas never see this class. A resource is a Godot type, and they are
/// engine-free, so <see cref="ToValues"/> builds the plain <see cref="StatTuningValues"/>
/// they read, which is also where the numbers are checked. <see cref="ToExperienceCurves"/>
/// does the same for the experience curves.
/// </para>
/// <para>
/// Every default is zero, never a real value. The editor leaves out of a <c>.tres</c>
/// each property that equals its default, so a default of 150 for the critical base
/// would disappear from the file the first time it was saved. No value here may be 0,
/// so a value missing from the file is refused by whichever method reads it, and
/// <c>StatTuningTests</c> requires the file to write every one.
/// </para>
/// <para>
/// A percentage is written as the design writes it: 90 is 90%. A property added here
/// needs its line in the file, and its parameter in <see cref="StatTuningValues"/> or
/// in an <see cref="ExperienceCurve"/>.
/// </para>
/// </remarks>
[GlobalClass]
public partial class StatTuning : Resource
{
	[ExportGroup("Armor into damage reduction")]

	/// <summary>The armor that gives half the cap against attacks.</summary>
	[Export]
	public double ArmorKAttack { get; set; }

	/// <summary>The armor that gives half the cap against skills.</summary>
	[Export]
	public double ArmorKSkill { get; set; }

	[Export]
	public double ArmorReductionCapAttack { get; set; }

	[Export]
	public double ArmorReductionCapSkill { get; set; }

	[ExportGroup("Luck into critical chance")]

	/// <summary>The luck that gives half the cap on attacks.</summary>
	[Export]
	public double LuckKAttack { get; set; }

	/// <summary>The luck that gives half the cap on skills.</summary>
	[Export]
	public double LuckKSkill { get; set; }

	[Export]
	public double CriticalChanceCapAttack { get; set; }

	[Export]
	public double CriticalChanceCapSkill { get; set; }

	[ExportGroup("Hit chance")]

	[Export]
	public double HitChanceFloor { get; set; }

	[ExportGroup("Critical damage")]

	[Export]
	public double CriticalDamageBase { get; set; }

	[Export]
	public double CriticalDamageCapAttack { get; set; }

	[Export]
	public double CriticalDamageCapSkill { get; set; }

	[ExportGroup("Evasion")]

	/// <summary>Against attacks. A skill is evaded half as often.</summary>
	[Export]
	public double EvasionCap { get; set; }

	[ExportGroup("Escape")]

	[Export]
	public double EscapeBase { get; set; }

	[Export]
	public double EscapeFloor { get; set; }

	[Export]
	public double EscapeCap { get; set; }

	[ExportGroup("Experience in the Fantasy world")]

	/// <summary>What going from level 1 to level 2 costs.</summary>
	[Export]
	public double FantasyExperienceBase { get; set; }

	/// <summary>How fast a level's cost grows: the cost is the base times the level to this power.</summary>
	[Export]
	public double FantasyExperienceExponent { get; set; }

	[Export]
	public int FantasyHighestLevel { get; set; }

	[ExportGroup("Experience in the Nightmare world")]

	/// <summary>What going from level 1 to level 2 costs.</summary>
	[Export]
	public double NightmareExperienceBase { get; set; }

	/// <summary>How fast a level's cost grows: the cost is the base times the level to this power.</summary>
	[Export]
	public double NightmareExperienceExponent { get; set; }

	[Export]
	public int NightmareHighestLevel { get; set; }

	/// <summary>
	/// The plain values the formulas read. Throws when a value is missing or wrong,
	/// naming it, so a broken tuning stops whoever asks for it instead of reaching a fight.
	/// </summary>
	public StatTuningValues ToValues() => new(
		armorK: new ByAction(ArmorKAttack, ArmorKSkill),
		armorReductionCap: new ByAction(ArmorReductionCapAttack, ArmorReductionCapSkill),
		luckK: new ByAction(LuckKAttack, LuckKSkill),
		criticalChanceCap: new ByAction(CriticalChanceCapAttack, CriticalChanceCapSkill),
		hitChanceFloor: HitChanceFloor,
		criticalDamageBase: CriticalDamageBase,
		criticalDamageCap: new ByAction(CriticalDamageCapAttack, CriticalDamageCapSkill),
		evasionCap: EvasionCap,
		escapeBase: EscapeBase,
		escapeFloor: EscapeFloor,
		escapeCap: EscapeCap);

	/// <summary>
	/// The experience curve of each world that has one. Throws when a value is missing
	/// or wrong, like <see cref="ToValues"/>.
	/// </summary>
	public ExperienceCurves ToExperienceCurves() => new(
		fantasy: new ExperienceCurve(FantasyExperienceBase, FantasyExperienceExponent, FantasyHighestLevel),
		nightmare: new ExperienceCurve(NightmareExperienceBase, NightmareExperienceExponent, NightmareHighestLevel));
}
