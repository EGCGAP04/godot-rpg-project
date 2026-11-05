using System.Linq;
using Godot;

/// <summary>
/// The debug tooling (#75), in debug builds only. At startup it applies the preset asked
/// for on the command line, after <c>--</c>: where the run starts, and effects on the
/// decision registry's flags. Afterwards, the <c>debug_overlay</c> action (F3) opens and
/// closes <see cref="DebugOverlay"/>. The rules for reading a preset live in
/// <see cref="DebugArguments"/> and <see cref="DebugPreset"/>, which have no Godot
/// dependency and are covered by unit tests.
/// </summary>
/// <remarks>
/// <para>
/// Registered after <c>DecisionRegistry</c> under <c>[autoload]</c>: autoloads enter the
/// tree in that order, and this one needs the registry's state. All of it runs in
/// <c>_EnterTree</c>, before <c>Main</c> reads <c>GameState</c> from its own.
/// </para>
/// <para>
/// A preset that cannot be read or names a flag wrongly stops the game with exit code 1.
/// A run that went on in the default state instead would look exactly like one that
/// started where it was asked to.
/// </para>
/// <para>
/// The overlay pauses the tree while it is open, unless something else already had, and
/// unpauses it when it closes only if it was the one that paused it. So a fight or a
/// forced conversation stays paused behind it. The one gap: if a forced conversation
/// starts while the overlay is open, which only a world's fade in can do, closing the
/// overlay unpauses it.
/// </para>
/// </remarks>
public partial class DebugTools : Node
{
	public static DebugTools Instance { get; private set; }

	private DebugOverlay _overlay;
	private bool _pausedTheTree;

	/// <summary>The snapshots taken from the overlay. Here and not in it, so they outlive closing it.</summary>
	public SnapshotShelf Shelf { get; } = new();

	/// <summary>Whether the overlay is open.</summary>
	public bool IsOpen => _overlay != null;

	public override void _EnterTree()
	{
		// A release build has no debug tooling at all: the node leaves, and Instance
		// stays null, which is how other code can tell.
		if (!OS.IsDebugBuild())
		{
			QueueFree();
			return;
		}

		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		ApplyStartupPreset();
	}

	// _Input, not _UnhandledInput: a focused control in the overlay, such as the snapshot's
	// name, would otherwise take the key before this sees it.
	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_overlay"))
			return;

		GetViewport().SetInputAsHandled();

		if (IsOpen)
			Close();
		else
			Open();
	}

	/// <summary>Opens the overlay over everything else, and pauses the tree if nothing had.</summary>
	public void Open()
	{
		if (IsOpen)
			return;

		_pausedTheTree = !GetTree().Paused;
		GetTree().Paused = true;

		// Under the root, after the main scene, so it is the last node in the tree. Godot
		// hands _Input to the last node first.
		_overlay = GD.Load<PackedScene>("res://Debug/debug_overlay.tscn").Instantiate<DebugOverlay>();
		GetTree().Root.AddChild(_overlay);
	}

	/// <summary>Closes the overlay, and unpauses the tree if opening it paused it.</summary>
	public void Close()
	{
		if (!IsOpen)
			return;

		_overlay.QueueFree();
		_overlay = null;

		if (_pausedTheTree)
			GetTree().Paused = false;
	}

	private void ApplyStartupPreset()
	{
		DecisionState state = DecisionRegistry.Instance.State;
		DebugPreset preset;

		try
		{
			DebugArguments arguments = DebugArguments.Parse(OS.GetCmdlineUserArgs());

			if (arguments.IsEmpty)
				return;

			DebugPreset file = arguments.PresetPath == null
				? DebugPreset.None
				: DebugPreset.Read(arguments.PresetPath.GetFile(), ReadFile(arguments.PresetPath));

			preset = file.Then(arguments.Overrides);
		}
		catch (DataFileException exception)
		{
			Stop(exception.Message);
			return;
		}

		string[] problems = preset.Validate(state.Catalogue).ToArray();

		if (problems.Length > 0)
		{
			Stop(string.Join("\n", problems));
			return;
		}

		GameState.Instance.StartAt(preset.Start);
		preset.Apply(state);

		GD.Print($"Debug: preset applied; starting in {preset.Start.World}, cycle {preset.Start.Cycle}.");

		foreach (FlagDeclaration flag in state.Catalogue.Flags.Values.OrderBy(flag => flag.Name, System.StringComparer.Ordinal))
		{
			string value = flag.Type == FlagType.Bool ? (state.GetFlag(flag.Name) ? "true" : "false") : state.GetCounter(flag.Name).ToString(System.Globalization.CultureInfo.InvariantCulture);
			GD.Print($"Debug:   {flag.Name} = {value}");
		}
	}

	private void Stop(string problem)
	{
		GD.PushError($"Debug: the preset was not applied, so the game stops.\n{problem}");
		GetTree().Quit(1);
	}

	/// <summary>The text of a preset file, from <c>res://</c> or an absolute path.</summary>
	/// <exception cref="DataFileException">The file cannot be read.</exception>
	private static string ReadFile(string path)
	{
		string text = FileAccess.GetFileAsString(path);
		Error error = FileAccess.GetOpenError();

		if (error != Error.Ok)
			throw new DataFileException(path.GetFile(), "", $"could not read {path}: {error}");

		return text;
	}
}
