using System;
using Godot;

/// <summary>
/// The combat overlay: turn state, timers, input and labels around a
/// <see cref="CombatResolver"/>, which owns the fight's numbers.
/// </summary>
/// <remarks>
/// Combat v1 is one against one: the party fights with its protagonist's stats in the
/// world the fight happens in, read from the <c>Party</c> autoload, and only the
/// protagonist's health points change. M8 brings the whole party into the fight.
/// </remarks>
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

	// Who fights, and where: read once, when the fight starts.
	private PartyState _party;
	private CharacterValues _fighter;
	private World _world;
	private int _maxHp;

	// Set when the fight ends, and kept so a change of language can rebuild the result.
	private bool? _playerWon;
	private ExperienceAward _award;

	private Label _enemyHpLabel;
	private Label _playerHpLabel;
	private Label _resultLabel;
	private Button _attackButton;
	private ScreenFade _fade;

	public override void _Ready()
	{
		// Falls back to the weak enemy so combat.tscn can still be run on its own
		// for testing, without Main setting the stats first.
		EnemyData ??= GD.Load<EnemyData>("res://Enemy/weak_enemy.tres");

		_party = Party.Instance.State;
		_fighter = _party.Roster.Protagonist;
		_world = GameState.Instance.CurrentWorld;

		BaseStats stats = _party.StatsOf(_fighter.Id, _world);
		_maxHp = stats.HealthPoints;
		_resolver = new CombatResolver(
			_party.ProgressOf(_fighter.Id, _world).HealthPoints,
			stats.AttackDamage,
			EnemyData.MaxHp,
			EnemyData.AttackPower);

		_enemyHpLabel = GetNode<Label>("EnemyHpLabel");
		_playerHpLabel = GetNode<Label>("PlayerHpLabel");
		_resultLabel = GetNode<Label>("ResultLabel");
		_attackButton = GetNode<Button>("AttackButton");
		_fade = GetNode<ScreenFade>("ScreenFade");

		GD.Print($"Combat started. {_fighter.Id} {_resolver.PlayerHp}/{_maxHp} HP in the {_world} world vs {EnemyData.NameKey} {_resolver.EnemyHp}/{EnemyData.MaxHp} HP");
		UpdateHpLabels();

		// The overlay starts fully black so the world map is never cut away
		// abruptly; the first turn only begins once the fade in has finished.
		_attackButton.Disabled = true;
		Fade(ScreenFade.Clear, StartPlayerTurn);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// A confirm pressed in the debug overlay still reached this, even with a control
		// of the overlay holding the focus (measured headless, #75).
		if (DebugTools.Instance?.IsOpen == true)
			return;

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

		// The resolver owns the fight's numbers; the party is where they have to end
		// up, since it is what carries the fighter's HP back out to the map.
		_party.SetHealthPoints(_fighter.Id, _world, _resolver.PlayerHp);
		GD.Print($"{EnemyData.NameKey} attacks for {_resolver.EnemyAttackPower}. Player HP: {_resolver.PlayerHp}/{_maxHp}");
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

		// A preview: the screen announces what the party gains, and Main hands it out
		// once the overlay is gone, with the same computation. A loss gives nothing.
		_playerWon = playerWon;
		_award = playerWon ? _party.PreviewAward(_world, EnemyData.Experience) : null;
		UpdateResultLabel();
		GD.Print($"Combat over. {(playerWon ? "Player wins." : "Player loses.")}");

		if (_award != null)
		{
			GD.Print($"Experience: {_award.Experience}.");

			foreach (MemberGain gain in _award.Gains)
				GD.Print($"  {gain.Id}: level {gain.Before.Level} ({gain.Before.Experience}) -> level {gain.After.Level} ({gain.After.Experience})");
		}

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
		// The labels are built here rather than translated by Godot, so a change of
		// language has to rebuild them. It can arrive before _Ready has found them.
		if (what == NotificationTranslationChanged && _resolver != null)
		{
			UpdateHpLabels();
			UpdateResultLabel();
		}
	}

	// Built from a translated template, so the three labels have auto-translation off in
	// combat.tscn: Godot would otherwise look the finished text up as a key again.
	private void UpdateHpLabels()
	{
		_enemyHpLabel.Text = HpText(Tr(EnemyData.NameKey), _resolver.EnemyHp, EnemyData.MaxHp);
		_playerHpLabel.Text = HpText(Tr(_fighter.NameKey), _resolver.PlayerHp, _maxHp);
	}

	/// <summary>Nothing until the fight ends; then who won and what the party gains.</summary>
	private void UpdateResultLabel()
	{
		if (_playerWon is bool playerWon)
			_resultLabel.Text = CombatResultText.Build(playerWon, _award, _party.Roster, key => Tr(key));
	}

	private string HpText(string name, int hp, int maxHp) =>
		TextTemplate.Fill(Tr("ui.combat.hp"), ("name", name), ("hp", hp), ("max_hp", maxHp));
}
