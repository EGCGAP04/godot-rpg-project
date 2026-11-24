using Godot;

/// <summary>
/// How a menu reads a press of one of its actions: a key or a button through Godot, a
/// stick through <see cref="StickPresses"/>, so that one push is one press (#117).
/// </summary>
/// <remarks>
/// A menu keeps its own <see cref="StickPresses"/> and asks about each action at most
/// once per event, every event, before deciding what to do with any: a stick's
/// release is only seen if the event that brings it reaches the state. The check on
/// <c>IsAction</c> keeps a motion of another axis, which gives the action a strength of
/// 0, from passing for its release.
/// </remarks>
public static class MenuInput
{
	/// <summary>Whether <paramref name="event"/> is a new press of <paramref name="action"/>.</summary>
	public static bool Pressed(this StickPresses stick, InputEvent @event, string action) =>
		@event is InputEventJoypadMotion
			? @event.IsAction(action) && stick.Press(action, @event.GetActionStrength(action))
			: @event.IsActionPressed(action);
}
