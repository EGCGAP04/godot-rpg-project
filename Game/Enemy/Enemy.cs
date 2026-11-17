using Godot;

/// <summary>
/// An enemy on the map. It is one node whatever the size of its group: the fight it
/// starts is against its whole <see cref="Encounter"/>.
/// </summary>
public partial class Enemy : Area2D
{
	[Signal]
	public delegate void CombatTriggeredEventHandler(Enemy enemy);

	/// <summary>The group this enemy stands for on the map: touching it starts a fight against all of them.</summary>
	[Export]
	public EncounterData Encounter { get; set; }

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
		{
			EmitSignal(SignalName.CombatTriggered, this);
		}
	}
}
