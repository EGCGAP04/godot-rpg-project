using Godot;

/// <summary>
/// One kind of enemy, authored as a .tres Resource so a new enemy is a new file instead
/// of new code or properties typed into a scene: its id, the key of its name, its seven
/// base stats and the experience it gives.
/// </summary>
/// <remarks>
/// <para>
/// Every default is empty or zero, never a real enemy's value. The editor leaves out of a
/// <c>.tres</c> each property that equals its default, so when these were the weak
/// enemy's stats, saving <c>weak_enemy.tres</c> dropped them from the file. So a stat
/// missing from a file is a 0, as in a character's.
/// </para>
/// <para>
/// The stats are fixed: an enemy has no level. The rules of a fight never read this
/// resource: <see cref="ToValues"/> hands them an <see cref="EnemyValues"/>, which is
/// also where an enemy with no health or no experience is refused.
/// </para>
/// </remarks>
[GlobalClass]
public partial class EnemyData : Resource
{
	/// <summary>
	/// What kind of enemy this is, as the decision registry's history of fights records it:
	/// a lowercase word such as <c>weak</c>, unique among the enemies. Unlike
	/// <see cref="NameKey"/>, it never changes with the text, so the history keeps meaning
	/// the same. Empty by default, so a new enemy that is missing one fails a test.
	/// </summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>
	/// The key of the enemy's name in <c>Localization/enemy.*.po</c>, never the name itself.
	/// A new enemy type needs its two entries there, one per language, and no code.
	/// </summary>
	[Export]
	public string NameKey { get; set; } = "";

	[Export]
	public int HealthPoints { get; set; }

	[Export]
	public int AttackDamage { get; set; }

	[Export]
	public int Armor { get; set; }

	[Export]
	public int ManaPoints { get; set; }

	[Export]
	public int Agility { get; set; }

	[Export]
	public int Luck { get; set; }

	/// <summary>Accuracy, where 1 point is 1%. An enemy that leaves it at 0 hits at the floor.</summary>
	[Export]
	public int HitRate { get; set; }

	/// <summary>
	/// The experience a win over this enemy gives. Every enemy gives at least 1: the
	/// default is 0, which the editor leaves out of the file, and a test requires the
	/// file to write it, so an enemy with none is a mistake and not a choice. In 64
	/// bits, like the experience it is added to.
	/// </summary>
	[Export]
	public long Experience { get; set; }

	/// <summary>This enemy as the plain values the rules of a fight read.</summary>
	public EnemyValues ToValues() => new(
		id: Id,
		nameKey: NameKey,
		stats: new BaseStats(
			healthPoints: HealthPoints,
			attackDamage: AttackDamage,
			armor: Armor,
			manaPoints: ManaPoints,
			agility: Agility,
			luck: Luck,
			hitRate: HitRate),
		experience: Experience);
}
