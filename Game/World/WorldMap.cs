using Godot;

public partial class WorldMap : Node2D
{
	[Signal]
	public delegate void CombatRequestedEventHandler(Enemy enemy);

	public Vector2 PlayerSpawnPosition { get; private set; }

	public override void _Ready()
	{
		PlayerSpawnPosition = GetNode<Marker2D>("PlayerSpawn").GlobalPosition;
	}

	private void OnCombatTriggered(Enemy enemy)
	{
		EmitSignal(SignalName.CombatRequested, enemy);
	}
}
