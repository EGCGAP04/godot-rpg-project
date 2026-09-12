using Godot;

public partial class WorldMap : Node2D
{
	private void OnCombatTriggered(Enemy enemy)
	{
		// Deferred: the signal fires inside a physics callback, where freeing the
		// current scene's collision nodes is not allowed. The stats travel as plain
		// ints because the enemy node is freed along with this scene.
		CallDeferred(MethodName.StartCombat, enemy.Hp, enemy.AttackPower);
	}

	private void StartCombat(int enemyHp, int enemyAttackPower)
	{
		Combat combat = GD.Load<PackedScene>("res://Combat/combat.tscn").Instantiate<Combat>();
		combat.EnemyMaxHp = enemyHp;
		combat.EnemyAttackPower = enemyAttackPower;

		SceneTree tree = GetTree();
		tree.CurrentScene.QueueFree();
		tree.Root.AddChild(combat);
		tree.CurrentScene = combat;
	}
}
