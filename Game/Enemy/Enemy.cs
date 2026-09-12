using Godot;

public partial class Enemy : Area2D
{
	[Signal]
	public delegate void CombatTriggeredEventHandler(Enemy enemy);

	[Export]
	public EnemyData Data { get; set; }

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
		{
			EmitSignal(SignalName.CombatTriggered, this);
		}
	}
}
