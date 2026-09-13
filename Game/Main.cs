using Godot;

public partial class Main : Node2D
{
	/// <summary>
	/// How long new combat triggers are ignored after one ends. Without it, losing
	/// next to an enemy that sits on <c>PlayerSpawn</c> would start the next fight
	/// on the very frame the player is moved back there.
	/// </summary>
	[Export]
	public float CombatCooldown = 1.0f;

	private WorldMap _worldMap;
	private Player _player;
	private Combat _combat;
	private Enemy _pendingEnemy;
	private bool _combatOnCooldown;

	// Runs in _EnterTree, not _Ready, on purpose: a CharacterBody2D registers its
	// transform with the physics server when it enters the tree, and a parent's
	// _EnterTree runs before its children enter. Spawning the player here means the
	// server never sees the position authored in main.tscn. Moving it later (in
	// _Ready) leaves enemy areas paired against that stale transform for one frame,
	// which fires a phantom body_entered — immediately followed by body_exited —
	// from wherever the player node happened to be placed in the scene.
	public override void _EnterTree()
	{
		_worldMap = GetNode<WorldMap>("WorldMap");
		_player = GetNode<Player>("Player");
		_player.GlobalPosition = _worldMap.PlayerSpawnPosition;
	}

	private void OnCombatRequested(Enemy enemy)
	{
		if (_combat != null || _pendingEnemy != null || _combatOnCooldown)
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

		StartCombatCooldown();
		GetTree().Paused = false;
	}

	private void StartCombatCooldown()
	{
		_combatOnCooldown = true;

		// SceneTreeTimer ignores the pause state by default, so the cooldown is
		// started before unpausing on purpose: it must already be active on the
		// first physics frame the world map runs again.
		GetTree().CreateTimer(CombatCooldown).Timeout += () => _combatOnCooldown = false;
	}
}
