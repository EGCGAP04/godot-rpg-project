using Godot;

/// <summary>
/// The base every world scene is built on, so <c>Main</c> can drive the Real,
/// Fantasy and Nightmare worlds through one type instead of knowing each concrete
/// scene. A world owns where the player starts, forwards its enemies' combat
/// triggers, and asks to be left when the player reaches its exit.
/// </summary>
/// <remarks>
/// The three placeholder scenes attach this script directly rather than each
/// declaring an empty subclass: they differ only in their tiles, their tint and
/// what they contain, which is scene data, not behaviour. A world that grows real
/// behaviour of its own can subclass this then.
/// </remarks>
public partial class WorldScene : Node2D
{
	/// <summary>Raised when one of this world's enemies has been touched.</summary>
	[Signal]
	public delegate void CombatRequestedEventHandler(Enemy enemy);

	/// <summary>
	/// Raised when the player reaches this world's exit. The world does not decide what
	/// comes next — <c>GameState</c> owns that — it only reports that it is finished.
	/// </summary>
	[Signal]
	public delegate void TransitionRequestedEventHandler();

	/// <summary>
	/// Where the player starts, and where they are sent back to after losing a fight.
	/// Resolved on access rather than cached in <c>_Ready</c>, because <c>Main</c>
	/// spawns the player from its own <c>_EnterTree</c>, which runs earlier.
	/// </summary>
	public Vector2 PlayerSpawnPosition => GetNode<Marker2D>("PlayerSpawn").GlobalPosition;

	private void OnCombatTriggered(Enemy enemy)
	{
		EmitSignal(SignalName.CombatRequested, enemy);
	}

	private void OnExitTriggered()
	{
		EmitSignal(SignalName.TransitionRequested);
	}
}
