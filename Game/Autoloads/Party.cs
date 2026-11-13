using Godot;

/// <summary>
/// The party: who is in it, and where every character stands in each world. The logic
/// lives in <see cref="PartyState"/>, which has no Godot dependency and is covered by unit
/// tests; this node loads the roster and the experience curves, and turns the state's
/// events into Godot signals.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="State"/> from outside, rather than methods repeated here, as with
/// <c>DecisionRegistry</c>. It is in memory only; saving it is M9's job.
/// </para>
/// <para>
/// Registered before <c>DebugTools</c> under <c>[autoload]</c>, which reaches the party
/// for its snapshots.
/// </para>
/// </remarks>
public partial class Party : Node
{
	public const string RosterPath = "res://Characters/roster.tres";
	public const string TuningPath = "res://Stats/stat_tuning.tres";

	public static Party Instance { get; private set; }

	/// <summary>Emitted after someone joins or leaves the party.</summary>
	[Signal]
	public delegate void MembersChangedEventHandler();

	/// <summary>
	/// Emitted with a character's id and a world each time their progress there actually
	/// changes: the level, the experience, or the health points or mana points left.
	/// </summary>
	[Signal]
	public delegate void ProgressChangedEventHandler(string id, World world);

	/// <summary>The members, and everyone's progress in each world.</summary>
	public PartyState State { get; private set; }

	// In _EnterTree, not _Ready, for the reason GameState gives: Main reads the autoloads
	// from its own _EnterTree. The tests guarantee the roster's files and the tuning make
	// a party, so a failure here means one was broken without running them; it is logged,
	// and the party is left without state.
	public override void _EnterTree()
	{
		Instance = this;

		CharacterRosterValues roster = GD.Load<CharacterRoster>(RosterPath).ToValues();
		ExperienceCurves curves = GD.Load<StatTuning>(TuningPath).ToExperienceCurves();

		State = new PartyState(roster, curves);
		State.MembersChanged += () => EmitSignal(SignalName.MembersChanged);
		State.ProgressChanged += (id, world) => EmitSignal(SignalName.ProgressChanged, id, Variant.From(world));
	}
}
