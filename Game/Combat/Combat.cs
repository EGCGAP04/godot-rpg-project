using Godot;

public partial class Combat : Node2D
{
	private enum TurnState
	{
		PlayerTurn,
		EnemyTurn,
		Finished
	}

	[Export]
	public int EnemyMaxHp = 10;

	[Export]
	public int EnemyAttackPower = 3;

	[Export]
	public int PlayerAttackPower = 4;

	private int _enemyHp;
	private TurnState _state = TurnState.PlayerTurn;

	public override void _Ready()
	{
		_enemyHp = EnemyMaxHp;
		GD.Print($"Combat started. Player {PlayerStats.Instance.CurrentHp}/{PlayerStats.Instance.MaxHp} HP vs Enemy {_enemyHp}/{EnemyMaxHp} HP");
		StartPlayerTurn();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_state == TurnState.PlayerTurn && @event.IsActionPressed("confirm"))
		{
			PlayerAttack();
		}
	}

	private void StartPlayerTurn()
	{
		_state = TurnState.PlayerTurn;
		GD.Print("Player turn. Press confirm to attack.");
	}

	private void PlayerAttack()
	{
		_enemyHp = Mathf.Max(_enemyHp - PlayerAttackPower, 0);
		GD.Print($"Player attacks for {PlayerAttackPower}. Enemy HP: {_enemyHp}/{EnemyMaxHp}");

		if (_enemyHp <= 0)
		{
			EndCombat("Player wins.");
			return;
		}

		EnemyTurn();
	}

	private void EnemyTurn()
	{
		_state = TurnState.EnemyTurn;

		PlayerStats stats = PlayerStats.Instance;
		stats.TakeDamage(EnemyAttackPower);
		GD.Print($"Enemy attacks for {EnemyAttackPower}. Player HP: {stats.CurrentHp}/{stats.MaxHp}");

		if (stats.CurrentHp <= 0)
		{
			EndCombat("Player loses.");
			return;
		}

		StartPlayerTurn();
	}

	private void EndCombat(string result)
	{
		_state = TurnState.Finished;
		GD.Print($"Combat over. {result}");
	}
}
