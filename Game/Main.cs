using Godot;

/// <summary>
/// Coordinates the run: which world scene is loaded, the player that outlives every
/// one of them, the combat overlay and the dialogue box. It is the only node that sees
/// all of them, and it talks to <see cref="WorldScene"/> rather than to any concrete
/// world, so adding or changing a world is scene work rather than code here. Whether a
/// fight, a conversation or a transition can start is decided by
/// <see cref="ConversationRules"/>, from what is going on at that moment.
/// </summary>
public partial class Main : Node2D
{
	/// <summary>
	/// How long new combat triggers are ignored after a fight ends or a world is
	/// swapped. After a fight it stops losing next to an enemy sitting on
	/// <c>PlayerSpawn</c> from starting the next fight on the frame the player is moved
	/// back there. After a swap it covers the equivalent case across worlds — see the
	/// note on <see cref="SwapWorld"/>.
	/// </summary>
	[Export]
	public float CombatCooldown = 1.0f;

	[Export]
	public PackedScene RealWorldScene { get; set; }

	[Export]
	public PackedScene FantasyWorldScene { get; set; }

	[Export]
	public PackedScene NightmareWorldScene { get; set; }

	private WorldScene _world;
	private Player _player;
	private ScreenFade _fade;
	private Label _worldLabel;
	private Combat _combat;
	private Enemy _pendingEnemy;
	private bool _combatOnCooldown;
	private bool _transitioning;
	private DialogueBox _dialogue;
	private ConversationKind? _conversationKind;
	private Enemy _queuedEnemy;

	/// <summary>What is going on right now, for <see cref="ConversationRules"/> to decide on.</summary>
	private GameActivity Activity => new(
		InCombat: _combat != null || _pendingEnemy != null,
		Transitioning: _transitioning,
		OnCombatCooldown: _combatOnCooldown,
		Conversation: _conversationKind,
		FightQueued: _queuedEnemy != null);

	// Runs in _EnterTree, not _Ready, on purpose: a CharacterBody2D registers its
	// transform with the physics server when it enters the tree, and a parent's
	// _EnterTree runs before its children enter. Building the first world and placing
	// the player here means the server never sees the position authored in main.tscn.
	// Moving it later (in _Ready) leaves enemy areas paired against that stale
	// transform for one frame, which fires a phantom body_entered — immediately
	// followed by body_exited — from wherever the player node happened to be placed.
	public override void _EnterTree()
	{
		_player = GetNode<Player>("Player");
		_fade = GetNode<ScreenFade>("Hud/ScreenFade");
		_worldLabel = GetNode<Label>("Hud/WorldLabel");

		// No world is authored in main.tscn: GameState is the single source of truth
		// for which one the run is in, so the first one is built from it like every
		// other one after it.
		EnterWorld(GameState.Instance.CurrentWorld);
	}

	public override void _Ready()
	{
		GameState.Instance.WorldChanged += OnWorldChanged;
		_fade.FadeTo(ScreenFade.Clear, StartOpeningConversation);
	}

	// --- World transitions ---

	private void OnTransitionRequested()
	{
		// A fight in progress owns the enemy nodes the outgoing world is about to free,
		// and a transition already under way must not be restarted by a second contact
		// with the exit before the world is gone. A conversation keeps every exit closed.
		if (!ConversationRules.CanTransition(Activity))
			return;

		_transitioning = true;
		_fade.FadeTo(ScreenFade.Opaque, () => GameState.Instance.Advance());
	}

	private void OnWorldChanged(World world)
	{
		// Deferred for the same reason combat is: the chain that gets here starts in an
		// Area2D's body_entered, and adding or freeing nodes inside a physics callback
		// is not allowed.
		CallDeferred(MethodName.SwapWorld, (int)world);
	}

	/// <summary>
	/// Replaces the loaded world, keeping the player alive across the swap.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is where #40 comes back, and the fix that worked there does <b>not</b> work
	/// here. The incoming world's enemy <c>Area2D</c>s pair against the transform the
	/// physics server holds for the player, and a player already in the tree leaves that
	/// transform stale for at least a frame — so an enemy sitting where the player stood
	/// in the world it just left reports a contact that never happened.
	/// </para>
	/// <para>
	/// Measured, not assumed: moving the player before the world is added, waiting a
	/// physics frame before adding it, and detaching the player from the tree and
	/// reattaching it after — all three still produced the phantom fight, with the player
	/// 80px from the enemy that triggered it. What does work is refusing triggers while
	/// <c>_transitioning</c> is set and for <see cref="CombatCooldown"/> afterwards, both
	/// of which were confirmed active at the moment the phantom arrived. Hence the
	/// cooldown started here, and the <c>_transitioning</c> check in
	/// <see cref="OnCombatRequested"/>.
	/// </para>
	/// <para>
	/// The trade-off: a player who genuinely lands on an enemy at the new world's spawn
	/// is not pulled into a fight either, because <c>body_entered</c> does not fire again
	/// once the cooldown lapses. That is the same trade-off already accepted for
	/// respawning after a loss, and it is a level-design problem rather than a code one.
	/// </para>
	/// </remarks>
	private void SwapWorld(int world)
	{
		// Both, and in this order: QueueFree alone would leave the outgoing world in the
		// tree until the end of the frame, with its enemies' areas still registered while
		// the incoming world is added. RemoveChild detaches it immediately.
		_world.QueueFree();
		RemoveChild(_world);

		EnterWorld((World)world);
		StartCombatCooldown();

		_fade.FadeTo(ScreenFade.Clear, () =>
		{
			_transitioning = false;
			StartOpeningConversation();
		});
	}

	/// <summary>
	/// Builds a world, puts the player at its spawn, and makes it the current one.
	/// </summary>
	private void EnterWorld(World world)
	{
		WorldScene next = SceneFor(world).Instantiate<WorldScene>();
		next.CombatRequested += OnCombatRequested;
		next.TransitionRequested += OnTransitionRequested;
		next.ConversationRequested += OnConversationRequested;

		AddChild(next);
		MoveChild(next, 0);
		_world = next;

		_player.GlobalPosition = next.PlayerSpawnPosition;
		UpdateWorldLabel();
	}

	private PackedScene SceneFor(World world) => world switch
	{
		World.Real => RealWorldScene,
		World.Fantasy => FantasyWorldScene,
		World.Nightmare => NightmareWorldScene,
		_ => RealWorldScene
	};

	public override void _Notification(int what)
	{
		// The world label is built here rather than translated by Godot, so a change of
		// language has to rebuild it.
		if (what == NotificationTranslationChanged && _worldLabel != null)
			UpdateWorldLabel();
	}

	// Temporary, until there is a real HUD: without it there is nothing on screen
	// that says which world or cycle the run is in. Built from a translated template,
	// so the label has auto-translation off in main.tscn.
	private void UpdateWorldLabel()
	{
		World world = GameState.Instance.CurrentWorld;
		string worldName = Tr($"world.{world.ToString().ToLowerInvariant()}");
		_worldLabel.Text = TextTemplate.Fill(Tr("ui.hud.world_and_cycle"), ("world", worldName), ("cycle", GameState.Instance.CurrentCycle));
	}

	// --- Combat ---

	private void OnCombatRequested(Enemy enemy)
	{
		switch (ConversationRules.OnCombatRequest(Activity))
		{
			case CombatRequestOutcome.Start:
				_pendingEnemy = enemy;
				CallDeferred(MethodName.StartCombat, enemy.Data);
				break;

			// The enemy is still touching the player, and its area will not report that
			// again, so the fight is kept for when the conversation ends.
			case CombatRequestOutcome.Queue:
				_queuedEnemy = enemy;
				break;
		}
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
		// Read now: the enemy is forgotten below, and may be freed.
		string enemyId = _pendingEnemy.Data.Id;

		PlayerStats.Instance.FullHeal();

		if (playerWon)
		{
			_pendingEnemy.QueueFree();
		}
		else
		{
			_player.GlobalPosition = _world.PlayerSpawnPosition;
		}

		_pendingEnemy = null;
		_combat.QueueFree();
		_combat = null;

		StartCombatCooldown();
		GetTree().Paused = false;

		// Last, on purpose. An exception thrown in a signal handler is logged and the game
		// goes on, but the rest of the handler does not run: anywhere earlier, a failure
		// here would leave the game paused under the overlay. Here, the worst it can do
		// is leave this one fight out of the history.
		DecisionRegistry.Instance.State.RecordCombat(GameState.Instance.CurrentWorld, GameState.Instance.CurrentCycle, enemyId, playerWon);
	}

	private void StartCombatCooldown()
	{
		_combatOnCooldown = true;

		// SceneTreeTimer ignores the pause state by default, so the cooldown is
		// started before unpausing on purpose: it must already be active on the
		// first physics frame the world map runs again.
		GetTree().CreateTimer(CombatCooldown).Timeout += () => _combatOnCooldown = false;
	}

	// --- Conversations ---

	private void OnConversationRequested(string conversation)
	{
		StartConversation(conversation, ConversationKind.Voluntary);
	}

	// Runs from a fade's callback, like clearing _transitioning, so the conversation never
	// starts before the world is visible. A killed tween skips its callback; nothing else
	// fades this instance while a transition is under way, but a third caller of the fade
	// would have to account for it.
	private void StartOpeningConversation()
	{
		if (_world.OpeningConversation.Length > 0)
			StartConversation(_world.OpeningConversation, ConversationKind.Forced);
	}

	/// <summary>
	/// Starts a conversation, if one can start now. The player stops answering to movement
	/// either way; a forced conversation also pauses the world.
	/// </summary>
	/// <remarks>
	/// The conversation is read and started before anything changes here, so a file that
	/// fails to load leaves the game as it was, with the error in the log. One that shows
	/// nothing at all, because no line's condition holds, ends there without a box.
	/// </remarks>
	private void StartConversation(string id, ConversationKind kind)
	{
		if (!ConversationRules.CanStartConversation(Activity))
			return;

		DialogueRunner runner = DialogueRunner.Start(ConversationLoader.Load(id), DecisionRegistry.Instance.State);

		if (runner.Step == DialogueStep.End)
			return;

		_dialogue = GD.Load<PackedScene>("res://UI/dialogue_box.tscn").Instantiate<DialogueBox>();
		_dialogue.Begin(runner);
		_dialogue.Finished += OnConversationFinished;
		_conversationKind = kind;
		_player.CanMove = false;

		AddChild(_dialogue);

		if (ConversationRules.PausesWorld(kind))
			GetTree().Paused = true;
	}

	private void OnConversationFinished()
	{
		bool paused = ConversationRules.PausesWorld(_conversationKind.Value);

		_dialogue.QueueFree();
		_dialogue = null;
		_conversationKind = null;
		_player.CanMove = true;

		if (paused)
			GetTree().Paused = false;

		// A fight requested during the conversation starts now, bypassing the guards that
		// would have refused a new request: it was accepted when it arrived.
		Enemy queued = _queuedEnemy;
		_queuedEnemy = null;

		if (queued != null && IsInstanceValid(queued))
		{
			_pendingEnemy = queued;
			CallDeferred(MethodName.StartCombat, queued.Data);
		}
	}
}
