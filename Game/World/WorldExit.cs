using Godot;

/// <summary>
/// The spot that ends a world when the player walks onto it: a bed in the Real
/// world, a way out in the other two. One scene reused three times, dressed
/// differently per instance, following the same shape as <c>Enemy</c> — the
/// <c>Area2D</c> reports the contact and the world re-emits it upward.
/// </summary>
public partial class WorldExit : Area2D
{
	[Signal]
	public delegate void ExitTriggeredEventHandler();

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
		{
			EmitSignal(SignalName.ExitTriggered);
		}
	}
}
