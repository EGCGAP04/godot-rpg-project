using Godot;

/// <summary>
/// Where the run currently is — which world, which cycle — and the only place that
/// moves it forward. The rule itself lives in <see cref="CycleProgression"/>, which
/// has no Godot dependency and is covered by unit tests; this node holds the state,
/// emits the signals, and does no arithmetic of its own.
/// </summary>
public partial class GameState : Node
{
	public static GameState Instance { get; private set; }

	/// <summary>
	/// Emitted whenever a world begins, <b>including</b> when a new cycle starts in the
	/// same world the run just left. Listeners load the world's scene on this signal,
	/// and a new cycle is a different day in the same place, so it has to fire on every
	/// transition rather than only when the value differs.
	/// </summary>
	/// <remarks>
	/// The parameter is the <see cref="World"/> enum rather than an <c>int</c>: Godot
	/// signals carry Variants, and an enum converts to one through
	/// <c>Variant.From</c>, arriving in the handler still typed. Verified at runtime,
	/// not just at compile time.
	/// </remarks>
	[Signal]
	public delegate void WorldChangedEventHandler(World world);

	/// <summary>Emitted only when the cycle number actually changes.</summary>
	[Signal]
	public delegate void CycleChangedEventHandler(int cycle);

	/// <summary>
	/// Emitted when the debug tooling asks to jump to a world and cycle (#75). <c>Main</c>
	/// decides whether it can happen now and, if so, fades out and calls
	/// <see cref="JumpTo"/>, as it does with an exit and <see cref="Advance"/>.
	/// </summary>
	[Signal]
	public delegate void JumpRequestedEventHandler(World world, int cycle);

	/// <summary>The world being played. No signal is emitted for the starting value;
	/// read this directly when setting up.</summary>
	public World CurrentWorld { get; private set; } = World.Real;

	/// <summary>The cycle being played, counting from
	/// <see cref="CycleProgression.FirstCycle"/>.</summary>
	public int CurrentCycle { get; private set; } = CycleProgression.FirstCycle;

	// _EnterTree, not _Ready: autoloads enter the tree before the main scene does,
	// but their _Ready has not run by the time the main scene's own _EnterTree runs.
	// Main needs this in _EnterTree, because that is where it has to place the player
	// before any Area2D registers against a stale transform (see #40).
	public override void _EnterTree()
	{
		Instance = this;
	}

	/// <summary>
	/// Moves the run to whatever comes next: the next unlocked world of this cycle, or
	/// the start of the following one.
	/// </summary>
	/// <remarks>
	/// Which worlds are unlocked is asked of <see cref="DecisionRegistry"/> now, with the
	/// flags as they are at this moment, and never stored here.
	/// </remarks>
	public void Advance() => MoveTo(CycleProgression.Next(new CyclePosition(CurrentWorld, CurrentCycle), DecisionRegistry.Instance.Unlocks));

	/// <summary>
	/// Sets where the run starts, without any signal: for the debug tooling's preset (#75),
	/// before the main scene reads the position. Since no signal is emitted, the cycle
	/// flags are not reset either, so a preset can set them.
	/// </summary>
	/// <exception cref="System.InvalidOperationException">The main scene is already in the tree.</exception>
	/// <exception cref="System.ArgumentOutOfRangeException">The position is not one the game can be in.</exception>
	public void StartAt(CyclePosition start)
	{
		// The main scene is already the current scene while the autoloads enter the tree
		// (seen headless), so what matters is whether it has entered it too.
		if (GetTree().CurrentScene?.IsInsideTree() == true)
			throw new System.InvalidOperationException("The run's start can only be set before the main scene enters the tree.");

		Valid(start);
		CurrentWorld = start.World;
		CurrentCycle = start.Cycle;
	}

	/// <summary>Asks for a jump to <paramref name="target"/>, which <c>Main</c> carries out if it can (#75).</summary>
	/// <exception cref="System.ArgumentOutOfRangeException">The position is not one the game can be in.</exception>
	public void RequestJump(CyclePosition target)
	{
		Valid(target);
		EmitSignal(SignalName.JumpRequested, Variant.From(target.World), target.Cycle);
	}

	/// <summary>
	/// Moves the run to <paramref name="target"/> with the same signals <see cref="Advance"/>
	/// emits, so a jump to another cycle resets its cycle flags as playing there would.
	/// Called by <c>Main</c>, once it has faded out; ask with <see cref="RequestJump"/>.
	/// </summary>
	/// <remarks>
	/// It does not ask whether the world is open: the debug tooling can reach a closed one,
	/// and says so on screen.
	/// </remarks>
	/// <exception cref="System.ArgumentOutOfRangeException">The position is not one the game can be in.</exception>
	public void JumpTo(CyclePosition target)
	{
		Valid(target);
		MoveTo(target);
	}

	private void MoveTo(CyclePosition next)
	{
		bool cycleChanged = next.Cycle != CurrentCycle;

		CurrentWorld = next.World;
		CurrentCycle = next.Cycle;

		// Cycle first, on purpose: anything scoped to a cycle, such as the registry's
		// cycle flags, has to be reset before a listener reacts to the new world by
		// loading its scene.
		if (cycleChanged)
			EmitSignal(SignalName.CycleChanged, CurrentCycle);

		EmitSignal(SignalName.WorldChanged, Variant.From(CurrentWorld));
	}

	private static void Valid(CyclePosition position)
	{
		if (!CycleProgression.IsValid(position))
			throw new System.ArgumentOutOfRangeException(nameof(position), position, "Not a position the game can be in.");
	}
}
