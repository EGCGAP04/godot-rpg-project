using Godot;

/// <summary>
/// Stats for a single enemy type, authored as a .tres Resource so new enemies
/// are created by duplicating a file instead of editing code or scene properties.
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
	/// <summary>
	/// The key of the enemy's name in <c>Localization/enemy.*.po</c>, never the name itself.
	/// A new enemy type needs its two entries there, one per language, and no code.
	/// </summary>
	[Export]
	public string NameKey { get; set; } = "enemy.weak";

	[Export]
	public int MaxHp { get; set; } = 10;

	[Export]
	public int AttackPower { get; set; } = 3;
}
