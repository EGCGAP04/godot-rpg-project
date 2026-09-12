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

	/// <summary>
	/// Stats of the enemy being fought. Set by <c>Main</c> before the overlay is
	/// added to the tree, so it is already available in <c>_Ready</c>.
	/// </summary>
	[Export]
	public EnemyData EnemyData { get; set; }

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
		// Falls back to the resource defaults so combat.tscn can still be run on
		// its own for testing, without Main setting the stats first.
		EnemyData ??= new EnemyData();
		_enemyHp = EnemyData.MaxHp;

		_enemyHpLabel = GetNode<Label>("EnemyHpLabel");
		_playerHpLabel = GetNode<Label>("PlayerHpLabel");
		_resultLabel = GetNode<Label>("ResultLabel");
		_attackButton = GetNode<Button>("AttackButton");

		GD.Print($"Combat started. Player {PlayerStats.Instance.CurrentHp}/{PlayerStats.Instance.MaxHp} HP vs {EnemyData.DisplayName} {_enemyHp}/{EnemyData.MaxHp} HP");
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
		_enemyHp = Mathf.Max(_enemyHp - PlayerStats.Instance.AttackPower, 0);
		GD.Print($"Player attacks for {PlayerStats.Instance.AttackPower}. {EnemyData.DisplayName} HP: {_enemyHp}/{EnemyData.MaxHp}");
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
		stats.TakeDamage(EnemyData.AttackPower);
		GD.Print($"{EnemyData.DisplayName} attacks for {EnemyData.AttackPower}. Player HP: {stats.CurrentHp}/{stats.MaxHp}");
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
		_enemyHpLabel.Text = $"{EnemyData.DisplayName}   {_enemyHp}/{EnemyData.MaxHp}";
		_playerHpLabel.Text = $"Player   {stats.CurrentHp}/{stats.MaxHp}";
	}
}
