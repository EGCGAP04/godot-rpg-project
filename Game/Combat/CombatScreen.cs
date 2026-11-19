using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The combat screen: the party against a group, a round at a time. It shows what a
/// <see cref="CombatRunner"/> works out and decides nothing about the fight itself.
/// </summary>
/// <remarks>
/// <para>
/// Everything with a rule is engine-free and tested on its own: <see cref="CombatLineup"/>
/// says who goes in, <see cref="OrderEntry"/> walks the menu, <see cref="CombatLog"/> and
/// <see cref="CombatantNames"/> give the text, and <see cref="CombatAftermath"/> and
/// <see cref="CombatResultPages"/> say what the fight left. What is here is the timing,
/// the input and the controls.
/// </para>
/// <para>
/// <b>The party is not written while the fight is on.</b> The screen keeps its own copy
/// of the health shown, moved by each line of the log as it appears, so the numbers fall
/// with the text and not before it. <c>Main</c> writes the party once, from
/// <see cref="Aftermath"/>, after <see cref="CombatFinished"/>.
/// </para>
/// <para>
/// <b>The log runs on a clock.</b> Each line waits <see cref="LineDelay"/> before the next
/// one, and <c>confirm</c> ends the wait. An enemy's attack is answered when its turn in
/// the log comes, with the damage the pipeline worked out, so its line appears the moment
/// it is answered. The result pages ignore <c>confirm</c> for <see cref="ResultInputDelay"/>,
/// so a player hurrying the log does not skip the first one.
/// </para>
/// <para>
/// Input is read in <see cref="_Input"/>, before the controls, and every press the screen
/// uses is marked as handled, like the status screen. With the debug overlay open it reads
/// nothing and its clock stops. It runs with <c>process_mode = Always</c>, because
/// <c>Main</c> pauses the tree around it.
/// </para>
/// </remarks>
public partial class CombatScreen : CanvasLayer
{
	/// <summary>Emitted when the fight is over and the screen has faded out. It does not free itself.</summary>
	[Signal]
	public delegate void CombatFinishedEventHandler();

	public const string TuningPath = "res://Combat/combat_tuning.tres";

	/// <summary>The group fought when the scene runs on its own, without <c>Main</c>.</summary>
	public const string StandaloneEncounterPath = "res://Enemy/Encounters/demo_patrol.tres";

	private const int LogLines = 4;

	private enum Phase
	{
		/// <summary>Fading in or out: input is ignored.</summary>
		Transition,
		Ordering,
		Playing,
		Result
	}

	/// <summary>The group fought. Set by <c>Main</c> before the screen is added to the tree.</summary>
	[Export]
	public EncounterData Encounter { get; set; }

	/// <summary>How long each line of the log stays before the next, in seconds.</summary>
	[Export]
	public double LineDelay { get; set; } = 0.6;

	/// <summary>How long the result ignores <c>confirm</c> when it appears, in seconds.</summary>
	[Export]
	public double ResultInputDelay { get; set; } = 0.4;

	/// <summary>What the fight left, once it is over. Null until then.</summary>
	public CombatAftermath Aftermath { get; private set; }

	private Phase _phase = Phase.Transition;
	private PartyState _party;
	private CombatLineup _lineup;
	private CombatRunner _runner;
	private CombatantNames _names;
	private OrderEntry _entry;

	// The log: what is still to be shown, and the last lines that were.
	private readonly Queue<CombatEvent> _toShow = new();
	private readonly List<CombatEvent> _shown = new();
	private double _wait;

	// The health each combatant shows, which follows the log rather than the runner.
	private int[] _partyHealth;
	private int[] _enemyHealth;

	private ExperienceAward _award;
	private IReadOnlyList<string> _pages;
	private int _page;

	private HBoxContainer _enemies;
	private HBoxContainer _members;
	private Label _logLabel;
	private Label _resultLabel;
	private Control _commands;
	private Button _attackButton;
	private ScreenFade _fade;

	public override void _Ready()
	{
		Encounter ??= GD.Load<EncounterData>(StandaloneEncounterPath);

		_enemies = GetNode<HBoxContainer>("Enemies");
		_members = GetNode<HBoxContainer>("Members");
		_logLabel = GetNode<Label>("LogPanel/LogLabel");
		_resultLabel = GetNode<Label>("LogPanel/ResultLabel");
		_commands = GetNode<Control>("Commands");
		_attackButton = GetNode<Button>("Commands/AttackButton");
		_fade = GetNode<ScreenFade>("ScreenFade");

		_party = Party.Instance.State;
		World world = GameState.Instance.CurrentWorld;
		_lineup = CombatLineup.For(_party, world, Encounter.ToValues());

		var random = new SeededCombatRandom();
		_runner = CombatRunner.Start(
			_lineup.Party,
			_lineup.Enemies,
			new StatFormulas(GD.Load<StatTuning>(Party.TuningPath).ToValues()),
			GD.Load<CombatTuning>(TuningPath).ToValues(),
			random);

		_partyHealth = _lineup.Party.Select(member => member.HealthPoints).ToArray();
		_enemyHealth = _lineup.Enemies.Select(enemy => enemy.HealthPoints).ToArray();
		_names = CombatantNames.Of(_lineup, key => Tr(key));

		BuildEnemies();
		BuildMembers();
		_attackButton.Pressed += OnAttackPressed;
		_commands.Visible = false;
		_resultLabel.Visible = false;

		if (DebugTools.Instance != null)
			DebugTools.Instance.OverlayClosed += OnOverlayClosed;

		GD.Print($"Combat started, seed {random.Seed}. {string.Join(", ", _lineup.MemberIds)} in the {world} world vs {_lineup.Encounter.Id} ({string.Join(", ", _lineup.Encounter.Enemies.Select(enemy => enemy.Id))})");

		// The screen starts black, so the map is never cut away; the first round is
		// ordered once the fade in has finished.
		_phase = Phase.Transition;
		_fade.FadeTo(ScreenFade.Clear, BeginOrders);
	}

	public override void _ExitTree()
	{
		if (DebugTools.Instance != null)
			DebugTools.Instance.OverlayClosed -= OnOverlayClosed;
	}

	public override void _Notification(int what)
	{
		// The names, the numbers, the log and the result are built in code, so a change of
		// language rebuilds them. It can arrive before _Ready.
		if (what != NotificationTranslationChanged || _lineup == null)
			return;

		_names = CombatantNames.Of(_lineup, key => Tr(key));
		RefreshEnemies();
		RefreshMembers();
		RefreshLog();

		if (_phase == Phase.Result)
			ShowPage(keepPage: true);
	}

	public override void _Process(double delta)
	{
		if (DebugTools.Instance?.IsOpen == true)
			return;

		if (_phase == Phase.Playing || _phase == Phase.Result)
			_wait -= delta;

		if (_phase == Phase.Playing && _wait <= 0)
			Advance();
	}

	public override void _Input(InputEvent @event)
	{
		// Godot hands every _Input to the nodes before the controls, so a key pressed in
		// the debug overlay would also act here.
		if (DebugTools.Instance?.IsOpen == true)
			return;

		bool confirm = @event.IsActionPressed("confirm");
		bool cancel = @event.IsActionPressed("cancel");
		bool left = @event.IsActionPressed("move_left");
		bool right = @event.IsActionPressed("move_right");

		if (!(confirm || cancel || left || right || @event.IsActionPressed("move_up") || @event.IsActionPressed("move_down")))
			return;

		// Every one of these is the screen's while it is open.
		GetViewport().SetInputAsHandled();

		switch (_phase)
		{
			case Phase.Ordering when confirm:
				Confirm();
				break;
			case Phase.Ordering when cancel:
				if (_entry.Back())
					ShowOrderStep();
				break;
			case Phase.Ordering when (left || right) && _entry.Step == OrderStep.Target:
				MoveTargetFocus(left ? Side.Left : Side.Right);
				break;
			case Phase.Playing when confirm:
				_wait = 0;
				break;
			case Phase.Result when confirm && _wait <= 0:
				NextPage();
				break;
		}
	}

	// --- Orders ---

	private void BeginOrders()
	{
		_entry = new OrderEntry(_runner.MembersToOrder, StandingEnemies());
		_phase = Phase.Ordering;
		ShowOrderStep();
	}

	private IEnumerable<CombatantId> StandingEnemies() =>
		_runner.Enemies.Where(enemy => !enemy.IsDown).Select(enemy => enemy.Id);

	private void Confirm()
	{
		if (_entry.Step == OrderStep.Command)
		{
			_entry.ChooseAttack();
			ShowOrderStep();
		}
		else if (_entry.Step == OrderStep.Target && FocusedEnemy() is CombatantId target)
		{
			ChooseTarget(target);
		}
	}

	private void OnAttackPressed()
	{
		if (_phase == Phase.Ordering && _entry.Step == OrderStep.Command)
			Confirm();
	}

	private void ChooseTarget(CombatantId target)
	{
		_entry.ChooseTarget(target);

		if (_entry.Step == OrderStep.Complete)
			Play();
		else
			ShowOrderStep();
	}

	/// <summary>Shows the step the orders are at: the menu for a command, the enemies for a target.</summary>
	private void ShowOrderStep()
	{
		_commands.Visible = _entry.Step == OrderStep.Command;
		RefreshMembers();
		LinkTargets();

		if (DebugTools.Instance?.IsOpen == true)
			return;

		if (_entry.Step == OrderStep.Command)
			_attackButton.GrabFocus();
		else if (_entry.Step == OrderStep.Target)
			EnemyButton(_entry.Targets[0]).GrabFocus();
	}

	private void MoveTargetFocus(Side side)
	{
		Button current = FocusedEnemy() is CombatantId focused ? EnemyButton(focused) : EnemyButton(_entry.Targets[0]);

		if (GetViewport().GuiGetFocusOwner() != current)
			current.GrabFocus();

		current.FindValidFocusNeighbor(side)?.GrabFocus();
	}

	private CombatantId? FocusedEnemy()
	{
		Control owner = GetViewport().GuiGetFocusOwner();

		for (int position = 0; position < _enemies.GetChildCount(); position++)
		{
			if (_enemies.GetChild(position) == owner && _entry.Targets.Contains(CombatantId.Enemy(position)))
				return CombatantId.Enemy(position);
		}

		return null;
	}

	/// <summary>
	/// Makes the standing enemies a row the focus goes round, and the rest unreachable.
	/// </summary>
	private void LinkTargets()
	{
		IReadOnlyList<CombatantId> targets = _entry?.Targets ?? Array.Empty<CombatantId>();
		bool choosing = _phase == Phase.Ordering && _entry.Step == OrderStep.Target;

		for (int position = 0; position < _enemies.GetChildCount(); position++)
		{
			Button button = _enemies.GetChild<Button>(position);
			int index = targets.ToList().IndexOf(CombatantId.Enemy(position));
			button.FocusMode = choosing && index >= 0 ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;

			if (index < 0)
				continue;

			NodePath self = button.GetPath();
			button.FocusNeighborLeft = EnemyButton(targets[(index - 1 + targets.Count) % targets.Count]).GetPath();
			button.FocusNeighborRight = EnemyButton(targets[(index + 1) % targets.Count]).GetPath();
			button.FocusNeighborTop = self;
			button.FocusNeighborBottom = self;
			button.FocusNext = self;
			button.FocusPrevious = self;
		}
	}

	// --- The log ---

	private void Play()
	{
		_commands.Visible = false;
		_phase = Phase.Playing;
		GetViewport().GuiReleaseFocus();
		LinkTargets();
		RefreshMembers();
		_runner.SubmitOrders(_entry.Orders);
		TakeEvents();
		Advance();
	}

	private void TakeEvents()
	{
		foreach (CombatEvent happened in _runner.TakeEvents())
			_toShow.Enqueue(happened);
	}

	/// <summary>
	/// The log's next step: the next line, or the enemy's attack whose turn it is, or the
	/// next round's orders, or the result.
	/// </summary>
	private void Advance()
	{
		while (_toShow.Count > 0)
		{
			CombatEvent happened = _toShow.Dequeue();

			if (Show(happened))
			{
				_wait = LineDelay;
				return;
			}
		}

		switch (_runner.Step)
		{
			case CombatStep.EnemyAttackPending:
				_runner.AnswerEnemyAttack(_runner.PendingAttack.Result.Damage);
				TakeEvents();
				Advance();
				break;
			case CombatStep.AwaitingOrders:
				BeginOrders();
				break;
			case CombatStep.Finished:
				ShowResult();
				break;
		}
	}

	/// <summary>Shows one event, if it has a line, and moves the health shown with it.</summary>
	/// <returns>Whether it had a line.</returns>
	private bool Show(CombatEvent happened)
	{
		if (happened is AttackResolved attack)
		{
			int[] side = attack.Target.Side == CombatSide.Party ? _partyHealth : _enemyHealth;
			side[attack.Target.Position] = Math.Max(0, side[attack.Target.Position] - attack.DamageTaken);
		}

		string line = CombatLog.LineFor(happened, _names, key => Tr(key));

		if (line == null)
			return false;

		GD.Print($"  {line}");
		_shown.Add(happened);

		if (_shown.Count > LogLines)
			_shown.RemoveAt(0);

		RefreshLog();
		RefreshEnemies();
		RefreshMembers();
		return true;
	}

	// --- The result ---

	private void ShowResult()
	{
		Aftermath = CombatAftermath.Of(_lineup, _runner);
		_award = Aftermath.PreviewAward(_party);
		_phase = Phase.Result;
		_page = 0;
		_logLabel.Visible = false;
		_resultLabel.Visible = true;
		ShowPage(keepPage: false);

		GD.Print($"Combat over: {Aftermath.Result}. Standing: {string.Join(", ", Aftermath.Standing)}. Experience {_award.Experience}.");

		foreach (MemberGain gain in _award.Gains)
			GD.Print($"  {gain.Id}: level {gain.Before.Level} ({gain.Before.Experience}) -> level {gain.After.Level} ({gain.After.Experience})");
	}

	private void ShowPage(bool keepPage)
	{
		_pages = CombatResultPages.Pages(Aftermath, _award, _party.Roster, key => Tr(key));
		_page = Math.Min(_page, _pages.Count - 1);
		_resultLabel.Text = _pages[_page];

		if (!keepPage)
			_wait = ResultInputDelay;
	}

	private void NextPage()
	{
		if (_page + 1 < _pages.Count)
		{
			_page++;
			ShowPage(keepPage: false);
			return;
		}

		_phase = Phase.Transition;
		_fade.FadeTo(ScreenFade.Opaque, () => EmitSignal(SignalName.CombatFinished));
	}

	// --- The controls ---

	private void BuildEnemies()
	{
		for (int position = 0; position < _lineup.Enemies.Count; position++)
		{
			var id = CombatantId.Enemy(position);

			// Built in code, so it does not translate itself: Godot would look the finished
			// text up as a key again.
			var button = new Button
			{
				AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
				FocusMode = Control.FocusModeEnum.None,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			button.Pressed += () => OnEnemyPressed(id);
			_enemies.AddChild(button);
		}

		RefreshEnemies();
	}

	private void OnEnemyPressed(CombatantId id)
	{
		if (_phase == Phase.Ordering && _entry.Step == OrderStep.Target && _entry.Targets.Contains(id))
			ChooseTarget(id);
	}

	private void BuildMembers()
	{
		foreach (CombatantSetup _ in _lineup.Party)
		{
			_members.AddChild(new Label
			{
				AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			});
		}

		RefreshMembers();
	}

	private Button EnemyButton(CombatantId id) => _enemies.GetChild<Button>(id.Position);

	private void RefreshEnemies()
	{
		for (int position = 0; position < _lineup.Enemies.Count; position++)
		{
			Button button = _enemies.GetChild<Button>(position);
			int health = _enemyHealth[position];

			button.Text = $"{_names.Of(CombatantId.Enemy(position))}\n{Points("ui.stat.health_points", health, _lineup.Enemies[position].Stats.HealthPoints)}";
			button.Disabled = health == 0;
		}
	}

	private void RefreshMembers()
	{
		CombatantId? choosing = _phase == Phase.Ordering && _entry.Step != OrderStep.Complete ? _entry.Member : (CombatantId?)null;

		for (int position = 0; position < _lineup.Party.Count; position++)
		{
			var id = CombatantId.Party(position);
			Label label = _members.GetChild<Label>(position);
			CombatantSetup member = _lineup.Party[position];
			string marker = choosing == id ? "> " : "";

			label.Text = string.Join(
				"\n",
				marker + _names.Of(id),
				Points("ui.stat.health_points", _partyHealth[position], member.Stats.HealthPoints),
				Points("ui.stat.mana_points", _runner.Party[position].ManaPoints, member.Stats.ManaPoints));

			// A member down is dimmed.
			label.Modulate = _partyHealth[position] == 0 ? new Color(1, 1, 1, 0.4f) : Colors.White;
		}
	}

	private void RefreshLog() =>
		_logLabel.Text = string.Join("\n", _shown.Select(happened => CombatLog.LineFor(happened, _names, key => Tr(key))));

	private string Points(string statKey, int current, int most) =>
		TextTemplate.Fill(Tr("ui.status.points"), ("stat", Tr(statKey)), ("current", current), ("max", most));

	// The overlay took the focus while it was open, and the control that had it is gone.
	private void OnOverlayClosed()
	{
		if (_phase == Phase.Ordering)
			ShowOrderStep();
	}
}
