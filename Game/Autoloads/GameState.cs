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
	/// Which of this cycle's optional worlds the player can reach. Always
	/// <see cref="WorldUnlocks.All"/> in M5 — the decision registry starts driving it
	/// in M6, which is why <see cref="CycleProgression"/> already takes it as an input.
	/// </summary>
	public WorldUnlocks Unlocks { get; set; } = WorldUnlocks.All;

	/// <summary>The world being played. No signal is emitted for the starting value;
	/// read this directly when setting up.</summary>
	public World CurrentWorld { get; private set; } = World.Real;

	/// <summary>The cycle being played, counting from
	/// <see cref="CycleProgression.FirstCycle"/>.</summary>
	public int CurrentCycle { get; private set; } = CycleProgression.FirstCycle;

	public override void _Ready()
	{
		Instance = this;
	}

	/// <summary>
	/// Moves the run to whatever comes next: the next unlocked world of this cycle, or
	/// the start of the following one.
	/// </summary>
	public void Advance()
	{
		CyclePosition next = CycleProgression.Next(new CyclePosition(CurrentWorld, CurrentCycle), Unlocks);

		bool cycleChanged = next.Cycle != CurrentCycle;

		CurrentWorld = next.World;
		CurrentCycle = next.Cycle;

		// Cycle first, on purpose: anything scoped to a cycle has to be reset before a
		// listener reacts to the new world by loading its scene.
		if (cycleChanged)
			EmitSignal(SignalName.CycleChanged, CurrentCycle);

		EmitSignal(SignalName.WorldChanged, Variant.From(CurrentWorld));
	}
}
