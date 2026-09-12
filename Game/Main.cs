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
		if (_combat != null || _pendingEnemy != null)
			return;

		_pendingEnemy = enemy;
		CallDeferred(MethodName.StartCombat, enemy.Data);
	}

	private void StartCombat(EnemyData enemyData)
	{
		_combat = GD.Load<PackedScene>("res://Combat/combat.tscn").Instantiate<Combat>();
		_combat.EnemyData = enemyData;
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
