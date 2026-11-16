using System.Collections.Generic;
using Godot;

/// <summary>
/// The status screen: one member of the party at a time, with their level and base
/// stats in the world the run is in. It shows what <see cref="StatusSheet"/> and
/// <see cref="StatusText"/> work out, and decides nothing about the party itself.
/// </summary>
/// <remarks>
/// <para>
/// Each member is a button in a row, and the sheet below follows whichever has the
/// keyboard focus. <c>move_left</c> and <c>move_right</c> move that focus to the
/// button's neighbour, so a keyboard and a gamepad behave alike; the row goes round
/// from the last member to the first and back. <c>open_menu</c> or <c>cancel</c> closes
/// the screen.
/// </para>
/// <para>
/// Input is read in <see cref="_Input"/>, which runs before the controls get the event,
/// and every press the screen uses is marked as handled. Two things depend on that. The
/// press that closes the screen would otherwise reach <c>Main</c>, just unpaused, and
/// open it again. And Godot's own focus keys never see the event, so an arrow does not
/// move the focus a second time and Tab, which is both <c>open_menu</c> and Godot's
/// "next control", only closes.
/// </para>
/// <para>
/// A name is a key, and its button or label translates itself: the members' buttons,
/// the name and the world. The other lines are built in code, so their labels do not
/// translate themselves (<c>auto_translate_mode</c> is Disabled on them in the scene)
/// and the screen rebuilds them when the language changes. It also listens to the party, so a change made behind it,
/// which today only the debug overlay can make, shows at once. It runs with
/// <c>process_mode = Always</c>, because <c>Main</c> pauses the tree around it.
/// </para>
/// </remarks>
public partial class StatusScreen : CanvasLayer
{
	/// <summary>Emitted when the player closes the screen. It does not free itself.</summary>
	[Signal]
	public delegate void ClosedEventHandler();

	private readonly List<string> _ids = new();

	private HBoxContainer _members;
	private Label _worldLabel;
	private Label _nameLabel;
	private Label _levelLabel;
	private Label _experienceLabel;
	private VBoxContainer _points;
	private VBoxContainer _stats;
	private string _shown;

	public override void _Ready()
	{
		_members = GetNode<HBoxContainer>("Panel/Members");
		_worldLabel = GetNode<Label>("Panel/WorldLabel");
		_nameLabel = GetNode<Label>("Panel/NameLabel");
		_levelLabel = GetNode<Label>("Panel/LevelLabel");
		_experienceLabel = GetNode<Label>("Panel/ExperienceLabel");
		_points = GetNode<VBoxContainer>("Panel/Points");
		_stats = GetNode<VBoxContainer>("Panel/Stats");

		Party.Instance.MembersChanged += OnMembersChanged;
		Party.Instance.ProgressChanged += OnProgressChanged;

		// In a release build there is no debug tooling, and nothing to listen to.
		if (DebugTools.Instance != null)
			DebugTools.Instance.OverlayClosed += OnOverlayClosed;

		BuildMembers(Party.Instance.State.Members[0]);
	}

	// The party outlives the screen, so its signals are let go of by hand.
	public override void _ExitTree()
	{
		Party.Instance.MembersChanged -= OnMembersChanged;
		Party.Instance.ProgressChanged -= OnProgressChanged;

		if (DebugTools.Instance != null)
			DebugTools.Instance.OverlayClosed -= OnOverlayClosed;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && _shown != null)
			ShowSheet();
	}

	public override void _Input(InputEvent @event)
	{
		// The debug overlay opens over this screen, and without this a key pressed in it
		// would also act here: Godot hands every _Input to the nodes before the controls.
		if (DebugTools.Instance?.IsOpen == true)
			return;

		if (@event.IsActionPressed("open_menu") || @event.IsActionPressed("cancel"))
		{
			GetViewport().SetInputAsHandled();
			EmitSignal(SignalName.Closed);
		}
		else if (@event.IsActionPressed("move_left"))
		{
			GetViewport().SetInputAsHandled();
			MoveFocus(Side.Left);
		}
		else if (@event.IsActionPressed("move_right"))
		{
			GetViewport().SetInputAsHandled();
			MoveFocus(Side.Right);
		}
		else if (@event.IsActionPressed("move_up") || @event.IsActionPressed("move_down") || @event.IsActionPressed("confirm"))
		{
			// Nothing to do with these yet, but they are the screen's while it is open.
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>Moves the focus to the neighbour of the member shown, on that side.</summary>
	private void MoveFocus(Side side)
	{
		Button current = ButtonOf(_shown);

		// Should the focus be anywhere else, or nowhere, the row takes it back first.
		if (GetViewport().GuiGetFocusOwner() != current)
			current.GrabFocus();

		current.FindValidFocusNeighbor(side)?.GrabFocus();
	}

	/// <summary>
	/// Builds the row, one button per member in the party's order, and gives the focus to
	/// <paramref name="focused"/>, or to the protagonist when that character has left.
	/// </summary>
	private void BuildMembers(string focused)
	{
		foreach (Node button in _members.GetChildren())
		{
			_members.RemoveChild(button);
			button.QueueFree();
		}

		PartyState party = Party.Instance.State;

		_ids.Clear();
		_ids.AddRange(party.Members);

		foreach (string id in _ids)
		{
			// The text is the name's key, and a button translates its own text.
			var button = new Button { Text = party.Roster.Get(id).NameKey, FocusMode = Control.FocusModeEnum.All };
			button.FocusEntered += () => OnMemberFocused(id);
			_members.AddChild(button);
		}

		// Each button names its neighbours, so the row goes round, and names itself for
		// every other direction, so the focus has nowhere else to go.
		for (int i = 0; i < _ids.Count; i++)
		{
			Button button = _members.GetChild<Button>(i);
			NodePath self = button.GetPath();

			button.FocusNeighborLeft = _members.GetChild((i - 1 + _ids.Count) % _ids.Count).GetPath();
			button.FocusNeighborRight = _members.GetChild((i + 1) % _ids.Count).GetPath();
			button.FocusNeighborTop = self;
			button.FocusNeighborBottom = self;
			button.FocusNext = self;
			button.FocusPrevious = self;
		}

		_shown = _ids.Contains(focused) ? focused : _ids[0];
		ShowSheet();

		// Not from under the debug overlay: the party changes while it is open, from its
		// own panel, and taking the focus then would take it from the control in use.
		// The screen takes it when the overlay closes.
		if (DebugTools.Instance?.IsOpen != true)
			ButtonOf(_shown).GrabFocus();
	}

	private Button ButtonOf(string id) => _members.GetChild<Button>(_ids.IndexOf(id));

	private void OnMemberFocused(string id)
	{
		// The member already shown: the focus came back to its button, as it does when
		// the row is built or the overlay closes, and there is nothing new to show.
		if (id == _shown)
			return;

		_shown = id;
		ShowSheet();
	}

	private void OnMembersChanged() => BuildMembers(_shown);

	// The overlay took the focus while it was open, and the control that had it is gone.
	private void OnOverlayClosed() => ButtonOf(_shown).GrabFocus();

	private void OnProgressChanged(string id, World world)
	{
		if (id == _shown && world == GameState.Instance.CurrentWorld)
			ShowSheet();
	}

	/// <summary>Fills the labels with the sheet of the member shown, in the current world.</summary>
	private void ShowSheet()
	{
		World world = GameState.Instance.CurrentWorld;
		StatusSheet sheet = StatusSheet.For(Party.Instance.State, _shown, world);

		_worldLabel.Text = $"world.{world.ToString().ToLowerInvariant()}";
		_nameLabel.Text = sheet.NameKey;
		_levelLabel.Text = StatusText.Level(sheet, Translate);
		_experienceLabel.Text = StatusText.Experience(sheet, Translate);
		Fill(_points, StatusText.Points(sheet, Translate));
		Fill(_stats, StatusText.Stats(sheet, Translate));
	}

	private string Translate(string key) => Tr(key);

	private static void Fill(VBoxContainer list, string[] lines)
	{
		foreach (Node line in list.GetChildren())
		{
			list.RemoveChild(line);
			line.QueueFree();
		}

		foreach (string line in lines)
			list.AddChild(new Label { Text = line });
	}
}
