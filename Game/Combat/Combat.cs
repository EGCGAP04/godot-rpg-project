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

	private CombatResolver _resolver;
	private TurnState _state = TurnState.Transition;

	private Label _enemyHpLabel;
	private Label _playerHpLabel;
	private Label _resultLabel;
	private Button _attackButton;
	private ScreenFade _fade;

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
		_fade = GetNode<ScreenFade>("ScreenFade");

		GD.Print($"Combat started. Player {_resolver.PlayerHp}/{PlayerStats.Instance.MaxHp} HP vs {EnemyData.NameKey} {_resolver.EnemyHp}/{EnemyData.MaxHp} HP");
		UpdateHpLabels();

		// The overlay starts fully black so the world map is never cut away
		// abruptly; the first turn only begins once the fade in has finished.
		_attackButton.Disabled = true;
		Fade(ScreenFade.Clear, StartPlayerTurn);
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
		GD.Print($"Player attacks for {_resolver.PlayerAttackPower}. {EnemyData.NameKey} HP: {_resolver.EnemyHp}/{EnemyData.MaxHp}");
		UpdateHpLabels();

		if (outcome == CombatResolver.Outcome.PlayerWon)
		{
			EndCombat(true);
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
		GD.Print($"{EnemyData.NameKey} attacks for {_resolver.EnemyAttackPower}. Player HP: {_resolver.PlayerHp}/{PlayerStats.Instance.MaxHp}");
		UpdateHpLabels();

		if (outcome == CombatResolver.Outcome.PlayerLost)
		{
			EndCombat(false);
			return;
		}

		StartPlayerTurn();
	}

	private void EndCombat(bool playerWon)
	{
		_state = TurnState.Finished;
		_attackButton.Disabled = true;

		// A key, not text: the label translates it itself, and again if the language
		// changes while the result is on screen.
		_resultLabel.Text = playerWon ? "ui.combat.player_won" : "ui.combat.player_lost";
		GD.Print($"Combat over. {(playerWon ? "Player wins." : "Player loses.")}");

		// Held on screen briefly rather than dismissed with confirm: the player is
		// already mashing confirm to attack and would skip the result instantly.
		// Only after that does the screen fade back to black, so the result text
		// is not hidden by the transition.
		GetTree().CreateTimer(ResultDelay).Timeout += () =>
			Fade(ScreenFade.Opaque, () => EmitSignal(SignalName.CombatFinished, playerWon));
	}

	/// <summary>
	/// Suspends the turn while the shared <see cref="ScreenFade"/> plays. The fade owns
	/// the tween; all this adds is that no input is accepted until it finishes.
	/// </summary>
	private void Fade(float alpha, Action onFinished)
	{
		_state = TurnState.Transition;
		_fade.FadeTo(alpha, onFinished);
	}

	public override void _Notification(int what)
	{
		// The HP labels are built here rather than translated by Godot, so a change of
		// language has to rebuild them. It can arrive before _Ready has found them.
		if (what == NotificationTranslationChanged && _resolver != null)
			UpdateHpLabels();
	}

	// Built from a translated template, so these two labels have auto-translation off in
	// combat.tscn: Godot would otherwise look the finished text up as a key again.
	private void UpdateHpLabels()
	{
		_enemyHpLabel.Text = HpText(Tr(EnemyData.NameKey), _resolver.EnemyHp, EnemyData.MaxHp);
		_playerHpLabel.Text = HpText(Tr("ui.combat.player"), _resolver.PlayerHp, PlayerStats.Instance.MaxHp);
	}

	private string HpText(string name, int hp, int maxHp) =>
		TextTemplate.Fill(Tr("ui.combat.hp"), ("name", name), ("hp", hp), ("max_hp", maxHp));
}
