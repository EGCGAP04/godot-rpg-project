using Godot;

public partial class WorldMap : Node2D
{
	[Signal]
	public delegate void CombatRequestedEventHandler(Enemy enemy);

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
}
