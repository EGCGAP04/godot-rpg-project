using Godot;

/// <summary>
/// Stats for a single enemy type, authored as a .tres Resource so new enemies
/// are created by duplicating a file instead of editing code or scene properties.
/// </summary>
/// <remarks>
/// Every default is empty or zero, never a real enemy's value. The editor leaves out of a
/// <c>.tres</c> each property that equals its default, so when these were the weak
/// enemy's stats, saving <c>weak_enemy.tres</c> dropped them from the file. Empty
/// defaults also make a new enemy that is missing a value fail a test.
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
	public int MaxHp { get; set; }

	[Export]
	public int AttackPower { get; set; }

	/// <summary>
	/// The experience a win over this enemy gives. Every enemy gives at least 1: the
	/// default is 0, which the editor leaves out of the file, and a test requires the
	/// file to write it, so an enemy with none is a mistake and not a choice. In 64
	/// bits, like the experience it is added to.
	/// </summary>
	[Export]
	public long Experience { get; set; }
}
