using Godot;

public partial class Combat : CanvasLayer
{
	[Signal]
	public delegate void CombatFinishedEventHandler(bool playerWon);

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

	[Export]
	public float EnemyTurnDelay = 0.6f;

	[Export]
	public float ResultDelay = 1.2f;

	private int _enemyHp;
	private TurnState _state = TurnState.PlayerTurn;

	private Label _enemyHpLabel;
	private Label _playerHpLabel;
	private Label _resultLabel;
	private Button _attackButton;

	public override void _Ready()
	{
		_enemyHp = EnemyMaxHp;

		_enemyHpLabel = GetNode<Label>("EnemyHpLabel");
		_playerHpLabel = GetNode<Label>("PlayerHpLabel");
		_resultLabel = GetNode<Label>("ResultLabel");
		_attackButton = GetNode<Button>("AttackButton");

		GD.Print($"Combat started. Player {PlayerStats.Instance.CurrentHp}/{PlayerStats.Instance.MaxHp} HP vs Enemy {_enemyHp}/{EnemyMaxHp} HP");
		UpdateHpLabels();
		StartPlayerTurn();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("confirm"))
		{
			TryPlayerAttack();
		}
	}

	private void OnAttackButtonPressed()
	{
		TryPlayerAttack();
	}

	private void TryPlayerAttack()
	{
		if (_state == TurnState.PlayerTurn)
		{
			PlayerAttack();
		}
	}

	private void StartPlayerTurn()
	{
		_state = TurnState.PlayerTurn;
		_attackButton.Disabled = false;
		GD.Print("Player turn. Attack with the button or the confirm input.");
	}

	private void PlayerAttack()
	{
		_enemyHp = Mathf.Max(_enemyHp - PlayerAttackPower, 0);
		GD.Print($"Player attacks for {PlayerAttackPower}. Enemy HP: {_enemyHp}/{EnemyMaxHp}");
		UpdateHpLabels();

		if (_enemyHp <= 0)
		{
			EndCombat("Player wins.", true);
			return;
		}

		StartEnemyTurn();
	}

	private void StartEnemyTurn()
	{
		_state = TurnState.EnemyTurn;
		_attackButton.Disabled = true;
		GetTree().CreateTimer(EnemyTurnDelay).Timeout += ResolveEnemyAttack;
	}

	private void ResolveEnemyAttack()
	{
		PlayerStats stats = PlayerStats.Instance;
		stats.TakeDamage(EnemyAttackPower);
		GD.Print($"Enemy attacks for {EnemyAttackPower}. Player HP: {stats.CurrentHp}/{stats.MaxHp}");
		UpdateHpLabels();

		if (stats.CurrentHp <= 0)
		{
			EndCombat("Player loses.", false);
			return;
		}

		StartPlayerTurn();
	}

	private void EndCombat(string result, bool playerWon)
	{
		_state = TurnState.Finished;
		_attackButton.Disabled = true;
		_resultLabel.Text = result;
		GD.Print($"Combat over. {result}");

		// Held on screen briefly rather than dismissed with confirm: the player is
		// already mashing confirm to attack and would skip the result instantly.
		GetTree().CreateTimer(ResultDelay).Timeout += () => EmitSignal(SignalName.CombatFinished, playerWon);
	}

	private void UpdateHpLabels()
	{
		PlayerStats stats = PlayerStats.Instance;
		_enemyHpLabel.Text = $"Enemy   {_enemyHp}/{EnemyMaxHp}";
		_playerHpLabel.Text = $"Player   {stats.CurrentHp}/{stats.MaxHp}";
	}
}
