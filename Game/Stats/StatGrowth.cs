using Godot;

/// <summary>
/// How a character's seven base stats grow in one world, as a resource: for each stat,
/// its value at level 1 and what each level after it adds. A <c>CharacterData</c>
/// holds three, one per world, inside its own file.
/// </summary>
/// <remarks>
/// <para>
/// The rule itself is engine-free: <see cref="ToValues"/> builds the plain
/// <see cref="StatGrowthValues"/> that computes the stats, which is also where the
/// numbers are checked.
/// </para>
/// <para>
/// Every default is zero, and here zero is also a valid value: a stat that does not
/// grow, or that the character does not have. The editor leaves such a value out of
/// the file, so a line missing from a <c>.tres</c> is a 0 and not a mistake. What a
/// character cannot do without is checked by <c>CharacterValues</c>.
/// </para>
/// <para>
/// A property added here needs its place in <see cref="ToValues"/>; a test compares
/// this list with the one it knows.
/// </para>
/// </remarks>
[GlobalClass]
public partial class StatGrowth : Resource
{
	[ExportGroup("Health points")]

	[Export]
	public double HealthPointsBase { get; set; }

	[Export]
	public double HealthPointsGrowth { get; set; }

	[ExportGroup("Attack damage")]

	[Export]
	public double AttackDamageBase { get; set; }

	[Export]
	public double AttackDamageGrowth { get; set; }

	[ExportGroup("Armor")]

	[Export]
	public double ArmorBase { get; set; }

	[Export]
	public double ArmorGrowth { get; set; }

	[ExportGroup("Mana points")]

	[Export]
	public double ManaPointsBase { get; set; }

	[Export]
	public double ManaPointsGrowth { get; set; }

	[ExportGroup("Agility")]

	[Export]
	public double AgilityBase { get; set; }

	[Export]
	public double AgilityGrowth { get; set; }

	[ExportGroup("Luck")]

	[Export]
	public double LuckBase { get; set; }

	[Export]
	public double LuckGrowth { get; set; }

	[ExportGroup("Hit rate")]

	[Export]
	public double HitRateBase { get; set; }

	[Export]
	public double HitRateGrowth { get; set; }

	/// <summary>The plain values the growth rule reads. Throws when a number is wrong, naming it.</summary>
	public StatGrowthValues ToValues() => new(
		HealthPoints: StatLine.From(HealthPointsBase, HealthPointsGrowth),
		AttackDamage: StatLine.From(AttackDamageBase, AttackDamageGrowth),
		Armor: StatLine.From(ArmorBase, ArmorGrowth),
		ManaPoints: StatLine.From(ManaPointsBase, ManaPointsGrowth),
		Agility: StatLine.From(AgilityBase, AgilityGrowth),
		Luck: StatLine.From(LuckBase, LuckGrowth),
		HitRate: StatLine.From(HitRateBase, HitRateGrowth));
}
