using Godot;

public partial class Main : Node2D
{
	private WorldMap _worldMap;
	private Player _player;
	private Combat _combat;
	private Enemy _pendingEnemy;

	public override void _Ready()
	{
		_worldMap = GetNode<WorldMap>("WorldMap");
		_player = GetNode<Player>("Player");
		_player.GlobalPosition = _worldMap.PlayerSpawnPosition;
	}

	private void OnCombatRequested(Enemy enemy)
	{
		_pendingEnemy = enemy;
		// Deferred: the request originates in a physics callback, where adding
		// children to the tree is not allowed.
		CallDeferred(MethodName.StartCombat, enemy.Hp, enemy.AttackPower);
	}

	private void StartCombat(int enemyHp, int enemyAttackPower)
	{
		_combat = GD.Load<PackedScene>("res://Combat/combat.tscn").Instantiate<Combat>();
		_combat.EnemyMaxHp = enemyHp;
		_combat.EnemyAttackPower = enemyAttackPower;
		_combat.CombatFinished += OnCombatFinished;

		AddChild(_combat);
		GetTree().Paused = true;
	}

	private void OnCombatFinished(bool playerWon)
	{
		PlayerStats.Instance.FullHeal();

		if (playerWon)
		{
			_pendingEnemy.QueueFree();
		}
		else
		{
			_player.GlobalPosition = _worldMap.PlayerSpawnPosition;
		}

		_pendingEnemy = null;
		_combat.QueueFree();
		_combat = null;
		GetTree().Paused = false;
	}
}
