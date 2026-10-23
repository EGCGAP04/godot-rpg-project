using Godot;

/// <summary>
/// The game's decision registry: every flag of <c>Decisions/flags.json</c>, the history of
/// fights, and which optional worlds <c>Decisions/world_access.json</c> opens. The logic
/// lives in <see cref="DecisionState"/> and <see cref="WorldAccess"/>, which have no Godot
/// dependency and are covered by unit tests; this node loads the two files, turns
/// <see cref="DecisionState.FlagChanged"/> into a Godot signal, and starts each new cycle.
/// </summary>
/// <remarks>
/// Use <see cref="State"/> from outside, rather than methods repeated here: the dialogue
/// runner takes it as its <see cref="IFlagStore"/>, and <c>Main</c> records fights on it.
/// It is in memory only; saving it is M9's job.
/// </remarks>
public partial class DecisionRegistry : Node
{
	public static DecisionRegistry Instance { get; private set; }

	/// <summary>Emitted with a flag's name each time its value actually changes.</summary>
	[Signal]
	public delegate void FlagChangedEventHandler(string name);

	private WorldAccess _access;

	/// <summary>Every flag's value and the history of fights.</summary>
	public DecisionState State { get; private set; }

	/// <summary>Which optional worlds are open with the flags as they are now.</summary>
	public WorldUnlocks Unlocks => _access.Unlocks(State);

	// Everything here, not in _Ready, for the reason GameState gives: Main reads the
	// autoloads from its own _EnterTree, which runs before any autoload's _Ready. The
	// tests guarantee both files are valid, so a failure here means one was broken
	// without running them; it is logged, and the registry is left without state.
	public override void _EnterTree()
	{
		Instance = this;

		State = new DecisionState(FlagCatalogue.Read(ReadFile("res://Decisions/flags.json")));
		_access = WorldAccess.Read(ReadFile("res://Decisions/world_access.json"));
		State.FlagChanged += name => EmitSignal(SignalName.FlagChanged, name);
	}

	public override void _Ready()
	{
		// Every autoload runs _EnterTree before any of them runs _Ready, so
		// GameState.Instance is set by now.
		GameState.Instance.CycleChanged += OnCycleChanged;
	}

	private void OnCycleChanged(int cycle) => State.StartCycle();

	/// <summary>
	/// The text of a file inside <c>res://</c>. Through <see cref="FileAccess"/>, not
	/// <c>System.IO</c>, because in an exported game the file lives inside the
	/// <c>.pck</c>.
	/// </summary>
	private static string ReadFile(string path)
	{
		string text = FileAccess.GetFileAsString(path);
		Error error = FileAccess.GetOpenError();

		if (error != Error.Ok)
			throw new System.IO.FileNotFoundException($"Could not read {path}: {error}.", path);

		return text;
	}
}
