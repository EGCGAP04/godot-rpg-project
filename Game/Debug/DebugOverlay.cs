using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

/// <summary>
/// The debug tooling's overlay (#75): where the run is, a jump to any world and cycle, the
/// language, every flag of the catalogue with its value, the snapshots, a reset of the
/// persistent flags, and the history of fights. <see cref="DebugTools"/> opens and closes
/// it; the rows are built here, from the catalogue, when it opens.
/// </summary>
/// <remarks>
/// <para>
/// Every change goes through the registry's API, so its signals fire as if the game had
/// made it, and the overlay listens to <c>FlagChanged</c>, so a change the game makes
/// while it is open shows too.
/// </para>
/// <para>
/// Its text is not translated: it is a tool, not part of the game, so it is in English
/// and has no keys. The panel turns auto-translation off, and every control inherits it.
/// </para>
/// </remarks>
public partial class DebugOverlay : CanvasLayer
{
	private const int FontSize = 10;
	private const int MaxCycle = 99;
	private static readonly Color PanelColour = new(0.1f, 0.1f, 0.1f);

	private readonly Dictionary<string, Control> _flagControls = new();

	private DecisionState _state;
	private VBoxContainer _content;
	private Label _position;
	private OptionButton _jumpWorld;
	private SpinBox _jumpCycle;
	private Label _jumpWarning;
	private LineEdit _snapshotName;
	private VBoxContainer _snapshotList;
	private VBoxContainer _fightList;

	public override void _Ready()
	{
		_state = DecisionRegistry.Instance.State;
		_content = GetNode<VBoxContainer>("Panel/Scroll/Content");
		var panel = GetNode<Panel>("Panel");
		panel.Theme = new Theme { DefaultFontSize = FontSize };

		// Opaque: the default panel lets the world show through, and the check boxes were
		// lost against the tiles behind them.
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = PanelColour });

		BuildPosition();
		BuildLanguage();
		BuildFlags();
		BuildSnapshots();
		BuildFights();

		_state.FlagChanged += OnFlagChanged;
		GameState.Instance.WorldChanged += OnWorldChanged;
		_jumpWorld.CallDeferred(Control.MethodName.GrabFocus);
	}

	public override void _ExitTree()
	{
		_state.FlagChanged -= OnFlagChanged;
		GameState.Instance.WorldChanged -= OnWorldChanged;
	}

	// --- Where the run is, and the jump ---

	private void BuildPosition()
	{
		_content.AddChild(new Label { Text = "Debug (F3 closes)" });
		_position = new Label();
		_content.AddChild(_position);

		var row = new HBoxContainer();
		row.AddChild(new Label { Text = "Jump to" });

		_jumpWorld = new OptionButton();
		foreach (World world in Enum.GetValues<World>())
			_jumpWorld.AddItem(world.ToString(), (int)world);
		_jumpWorld.Selected = (int)GameState.Instance.CurrentWorld;
		_jumpWorld.ItemSelected += _ => UpdateJumpWarning();
		row.AddChild(_jumpWorld);

		row.AddChild(new Label { Text = "cycle" });
		_jumpCycle = new SpinBox { MinValue = CycleProgression.FirstCycle, MaxValue = MaxCycle, Rounded = true, Value = GameState.Instance.CurrentCycle };
		row.AddChild(_jumpCycle);

		var jump = new Button { Text = "Jump" };
		jump.Pressed += OnJumpPressed;
		row.AddChild(jump);

		_jumpWarning = new Label();
		row.AddChild(_jumpWarning);
		_content.AddChild(row);

		UpdatePosition();
	}

	private void OnJumpPressed()
	{
		var target = new CyclePosition((World)_jumpWorld.GetSelectedId(), (int)_jumpCycle.Value);

		// Closed first, so the tree is as it was and the fade can be seen.
		DebugTools.Instance.Close();
		GameState.Instance.RequestJump(target);
	}

	private void UpdatePosition()
	{
		_position.Text = $"Now: {GameState.Instance.CurrentWorld}, cycle {GameState.Instance.CurrentCycle}";
		UpdateJumpWarning();
	}

	// The jump can reach a world the flags keep closed tonight; this is the one place the
	// tool steps outside the game's rules, so it says so.
	private void UpdateJumpWarning()
	{
		var world = (World)_jumpWorld.GetSelectedId();
		bool open = world == World.Real || DecisionRegistry.Instance.Unlocks.IsUnlocked(world);
		_jumpWarning.Text = open ? "" : "(closed tonight)";
	}

	private void OnWorldChanged(World world) => UpdatePosition();

	// --- Language ---

	private void BuildLanguage()
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = "Language" });

		var locales = new OptionButton();
		string current = TranslationServer.GetLocale().Split('_')[0];

		for (int i = 0; i < LocalizationNames.Locales.Count; i++)
			locales.AddItem(LocalizationNames.Locales[i], i);

		locales.Selected = Math.Max(0, LocalizationNames.Locales.ToList().IndexOf(current));
		locales.ItemSelected += index => TranslationServer.SetLocale(LocalizationNames.Locales[(int)index]);
		row.AddChild(locales);
		_content.AddChild(row);
	}

	// --- Flags ---

	private void BuildFlags()
	{
		AddHeading("Flags");

		foreach (FlagDeclaration flag in _state.Catalogue.Flags.Values.OrderBy(flag => flag.Name, StringComparer.Ordinal))
		{
			var row = new HBoxContainer { TooltipText = flag.Description };
			row.AddChild(new Label { Text = flag.Name, CustomMinimumSize = new Vector2(200, 0), TooltipText = flag.Description, MouseFilter = Control.MouseFilterEnum.Pass });
			row.AddChild(new Label { Text = flag.Lifetime.ToString().ToLowerInvariant(), CustomMinimumSize = new Vector2(80, 0) });

			Control control = flag.Type == FlagType.Bool ? FlagCheckBox(flag.Name) : CounterSpinBox(flag.Name);
			row.AddChild(control);
			_flagControls[flag.Name] = control;
			_content.AddChild(row);
		}
	}

	private CheckBox FlagCheckBox(string name)
	{
		var box = new CheckBox { ButtonPressed = _state.GetFlag(name) };
		box.Toggled += pressed => _state.SetFlag(name, pressed);
		return box;
	}

	private SpinBox CounterSpinBox(string name)
	{
		var box = new SpinBox { MinValue = 0, MaxValue = int.MaxValue, Rounded = true, Value = _state.GetCounter(name) };

		// The registry only adds, so the new value is reached by the difference. Both are 0
		// or more, so the subtraction cannot overflow.
		box.ValueChanged += value => _state.AddToCounter(name, (int)value - _state.GetCounter(name));
		return box;
	}

	private void OnFlagChanged(string name)
	{
		if (_flagControls.TryGetValue(name, out Control control))
		{
			if (control is CheckBox box)
				box.SetPressedNoSignal(_state.GetFlag(name));
			else if (control is SpinBox spin)
				spin.SetValueNoSignal(_state.GetCounter(name));
		}

		// A fight raises FlagChanged on its total once it is in the history.
		if (name is DecisionState.Wins or DecisionState.Losses)
			RefreshFights();

		UpdateJumpWarning();
	}

	// --- Snapshots and the persistent flags ---

	private void BuildSnapshots()
	{
		AddHeading("Snapshots (cycle and run flags, and the fights)");

		var row = new HBoxContainer();
		_snapshotName = new LineEdit { PlaceholderText = "name, or empty for a number", CustomMinimumSize = new Vector2(200, 0) };
		row.AddChild(_snapshotName);

		var take = new Button { Text = "Take" };
		take.Pressed += OnTakePressed;
		row.AddChild(take);
		_content.AddChild(row);

		_snapshotList = new VBoxContainer();
		_content.AddChild(_snapshotList);
		RefreshSnapshots();

		var reset = new Button { Text = "Reset persistent flags", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		reset.Pressed += () => _state.ResetPersistent();
		_content.AddChild(reset);
	}

	private void OnTakePressed()
	{
		var position = new CyclePosition(GameState.Instance.CurrentWorld, GameState.Instance.CurrentCycle);
		DebugTools.Instance.Shelf.Take(_state, position, _snapshotName.Text);
		_snapshotName.Text = "";
		RefreshSnapshots();
	}

	private void RefreshSnapshots()
	{
		foreach (Node child in _snapshotList.GetChildren())
			child.QueueFree();

		foreach (ShelvedSnapshot snapshot in DebugTools.Instance.Shelf.Snapshots)
		{
			string name = snapshot.Name;
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = $"{name}  ({snapshot.TakenAt.World}, cycle {snapshot.TakenAt.Cycle})", CustomMinimumSize = new Vector2(280, 0) });

			var restore = new Button { Text = "Restore" };
			restore.Pressed += () => DebugTools.Instance.Shelf.Restore(_state, name);
			row.AddChild(restore);

			var delete = new Button { Text = "Delete" };
			delete.Pressed += () =>
			{
				DebugTools.Instance.Shelf.Remove(name);
				RefreshSnapshots();
			};
			row.AddChild(delete);
			_snapshotList.AddChild(row);
		}
	}

	// --- Fights ---

	private void BuildFights()
	{
		AddHeading("Fights");
		_fightList = new VBoxContainer();
		_content.AddChild(_fightList);
		RefreshFights();
	}

	private void RefreshFights()
	{
		foreach (Node child in _fightList.GetChildren())
			child.QueueFree();

		if (_state.Combats.Count == 0)
			_fightList.AddChild(new Label { Text = "none yet" });

		foreach (CombatRecord fight in _state.Combats)
		{
			string outcome = fight.PlayerWon ? "won" : "lost";
			_fightList.AddChild(new Label { Text = string.Create(CultureInfo.InvariantCulture, $"{fight.World}, cycle {fight.Cycle}: {fight.EnemyId}, {outcome}") });
		}
	}

	private void AddHeading(string text)
	{
		_content.AddChild(new HSeparator());
		_content.AddChild(new Label { Text = text });
	}
}
