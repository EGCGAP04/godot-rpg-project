using Godot;

/// <summary>
/// The dialogue box: the speaker's name, the line, and the options of a choice. It drives
/// a <see cref="DialogueRunner"/> from the player's input and shows what the runner is at,
/// and knows nothing of what the conversation means.
/// </summary>
/// <remarks>
/// <para>
/// <c>confirm</c> moves past a line or picks the highlighted option, and <c>move_up</c> /
/// <c>move_down</c> move the highlight, so a keyboard and a gamepad behave alike. Input is
/// read in <see cref="_Input"/>, which runs before any node's <c>_UnhandledInput</c>, and
/// every press the box uses is marked as handled. That is what keeps the press that ends
/// a conversation from reaching the character the player was talking to, and starting the
/// conversation again.
/// </para>
/// <para>
/// Every text is a key, and the labels translate themselves, so a change of language
/// shows at once. A choice keeps the last line on screen, since that line is usually the
/// question. The box runs with <c>process_mode = Always</c>, because a forced conversation
/// pauses the tree around it.
/// </para>
/// </remarks>
public partial class DialogueBox : CanvasLayer
{
	/// <summary>Emitted once the conversation has ended. The box does not free itself.</summary>
	[Signal]
	public delegate void FinishedEventHandler();

	private static readonly Color Highlighted = new(1f, 1f, 1f);
	private static readonly Color Dimmed = new(0.55f, 0.55f, 0.55f);

	private DialogueRunner _runner;
	private Label _speakerLabel;
	private Label _textLabel;
	private VBoxContainer _choiceList;
	private int _selected;

	/// <summary>
	/// Hands over the conversation to show, already started. Called before the box enters
	/// the tree, as <c>Main</c> sets a combat's enemy before adding it.
	/// </summary>
	public void Begin(DialogueRunner runner)
	{
		_runner = runner;
	}

	public override void _Ready()
	{
		_speakerLabel = GetNode<Label>("Panel/SpeakerLabel");
		_textLabel = GetNode<Label>("Panel/TextLabel");
		_choiceList = GetNode<VBoxContainer>("Panel/ChoiceList");
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (_runner == null || _runner.Step == DialogueStep.End)
			return;

		if (@event.IsActionPressed("confirm"))
		{
			GetViewport().SetInputAsHandled();
			Continue();
		}
		else if (_runner.Step == DialogueStep.Choice && @event.IsActionPressed("move_up"))
		{
			GetViewport().SetInputAsHandled();
			Select(_selected - 1);
		}
		else if (_runner.Step == DialogueStep.Choice && @event.IsActionPressed("move_down"))
		{
			GetViewport().SetInputAsHandled();
			Select(_selected + 1);
		}
	}

	private void Continue()
	{
		if (_runner.Step == DialogueStep.Line)
			_runner.Advance();
		else
			_runner.Choose(_runner.Options[_selected].Name);

		if (_runner.Step == DialogueStep.End)
			EmitSignal(SignalName.Finished);
		else
			Refresh();
	}

	/// <summary>Shows what the runner is at: a line with its speaker, or the options of a choice under the last line.</summary>
	private void Refresh()
	{
		foreach (Node option in _choiceList.GetChildren())
		{
			_choiceList.RemoveChild(option);
			option.QueueFree();
		}

		if (_runner.Step == DialogueStep.Line)
		{
			DialogueLine line = _runner.CurrentLine;
			_speakerLabel.Visible = !line.IsNarration;
			_speakerLabel.Text = line.IsNarration ? "" : $"{LocalizationNames.SpeakerTable}.{line.Speaker}";
			_textLabel.Text = line.Key;
			return;
		}

		foreach (DialogueOption option in _runner.Options)
			_choiceList.AddChild(new Label { Text = option.Key });

		Select(0);
	}

	/// <summary>Highlights one option, going round from the last to the first and back.</summary>
	private void Select(int index)
	{
		int count = _choiceList.GetChildCount();
		_selected = (index % count + count) % count;

		for (int i = 0; i < count; i++)
			_choiceList.GetChild<Label>(i).Modulate = i == _selected ? Highlighted : Dimmed;
	}
}
