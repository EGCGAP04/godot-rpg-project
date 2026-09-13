using System;
using Godot;

public partial class Combat : CanvasLayer
{
	[Signal]
	public delegate void CombatFinishedEventHandler(bool playerWon);

	private enum TurnState
	{
		/// <summary>Fading in or out: no turn is active and input is ignored.</summary>
		Transition,
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

	[Export]
	public float FadeDuration = 0.25f;

	private CombatResolver _resolver;
	private TurnState _state = TurnState.Transition;

	private Label _enemyHpLabel;
	private Label _playerHpLabel;
	private Label _resultLabel;
	private Button _attackButton;
	private ColorRect _fadeOverlay;

	public override void _Ready()
	{
		// Falls back to the resource defaults so combat.tscn can still be run on
		// its own for testing, without Main setting the stats first.
		EnemyData ??= new EnemyData();
		_resolver = new CombatResolver(
			PlayerStats.Instance.CurrentHp,
			PlayerStats.Instance.AttackPower,
			EnemyData.MaxHp,
			EnemyData.AttackPower);

		_enemyHpLabel = GetNode<Label>("EnemyHpLabel");
		_playerHpLabel = GetNode<Label>("PlayerHpLabel");
		_resultLabel = GetNode<Label>("ResultLabel");
		_attackButton = GetNode<Button>("AttackButton");
		_fadeOverlay = GetNode<ColorRect>("FadeOverlay");

		GD.Print($"Combat started. Player {_resolver.PlayerHp}/{PlayerStats.Instance.MaxHp} HP vs {EnemyData.DisplayName} {_resolver.EnemyHp}/{EnemyData.MaxHp} HP");
		UpdateHpLabels();

		// The overlay starts fully black so the world map is never cut away
		// abruptly; the first turn only begins once the fade in has finished.
		_attackButton.Disabled = true;
		FadeTo(0f, StartPlayerTurn);
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
		CombatResolver.Outcome outcome = _resolver.PlayerAttack();
		GD.Print($"Player attacks for {_resolver.PlayerAttackPower}. {EnemyData.DisplayName} HP: {_resolver.EnemyHp}/{EnemyData.MaxHp}");
		UpdateHpLabels();

		if (outcome == CombatResolver.Outcome.PlayerWon)
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
		CombatResolver.Outcome outcome = _resolver.EnemyAttack();

		// The resolver owns the fight's numbers; the Autoload is where they have to
		// end up, since it is what carries the player's HP back out to the map.
		PlayerStats.Instance.CurrentHp = _resolver.PlayerHp;
		GD.Print($"{EnemyData.DisplayName} attacks for {_resolver.EnemyAttackPower}. Player HP: {_resolver.PlayerHp}/{PlayerStats.Instance.MaxHp}");
		UpdateHpLabels();

		if (outcome == CombatResolver.Outcome.PlayerLost)
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
		// Only after that does the screen fade back to black, so the result text
		// is not hidden by the transition.
		GetTree().CreateTimer(ResultDelay).Timeout += () =>
			FadeTo(1f, () => EmitSignal(SignalName.CombatFinished, playerWon));
	}

	/// <summary>
	/// Tweens the black overlay to the given alpha and runs <paramref name="onFinished"/>
	/// afterwards. The tween is bound to this node, which runs with
	/// <c>process_mode = Always</c>, so it keeps playing while the world map is paused.
	/// </summary>
	private void FadeTo(float alpha, Action onFinished)
	{
		_state = TurnState.Transition;

		Tween tween = CreateTween();
		tween.TweenProperty(_fadeOverlay, "modulate:a", alpha, FadeDuration);
		tween.Finished += onFinished;
	}

	private void UpdateHpLabels()
	{
		_enemyHpLabel.Text = $"{EnemyData.DisplayName}   {_resolver.EnemyHp}/{EnemyData.MaxHp}";
		_playerHpLabel.Text = $"Player   {_resolver.PlayerHp}/{PlayerStats.Instance.MaxHp}";
	}
}
