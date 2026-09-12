using Godot;

/// <summary>
/// Stats for a single enemy type, authored as a .tres Resource so new enemies
/// are created by duplicating a file instead of editing code or scene properties.
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
	[Export]
	public string DisplayName { get; set; } = "Enemy";

	[Export]
	public int MaxHp { get; set; } = 10;

	[Export]
	public int AttackPower { get; set; } = 3;
}
