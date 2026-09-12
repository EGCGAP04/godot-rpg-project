using Godot;

public partial class Enemy : Area2D
{
	[Signal]
	public delegate void CombatTriggeredEventHandler();

	[Export]
	public int Hp = 10;

	[Export]
	public int AttackPower = 3;

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
		{
			EmitSignal(SignalName.CombatTriggered);
		}
	}
}
