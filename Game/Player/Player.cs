using Godot;

public partial class Player : CharacterBody2D
{
	[Export]
	public float Speed = 100.0f;

	// The Input Map action behind each direction. Kept next to the movement code
	// because the action names are the engine's vocabulary, not the resolver's.
	private static readonly (Direction Direction, string Action)[] MovementActions =
	{
		(Direction.Up, "move_up"),
		(Direction.Down, "move_down"),
		(Direction.Left, "move_left"),
		(Direction.Right, "move_right")
	};

	private readonly DirectionResolver _directions = new();

	/// <summary>
	/// Which way the player is facing, which outlives the movement that set it.
	/// Nothing reads this yet — it is what the four-directional sprite animations will
	/// be driven from once there is art.
	/// </summary>
	public Direction Facing => _directions.Facing;

	public override void _PhysicsProcess(double delta)
	{
		SyncHeldDirections();

		Velocity = ToVector(_directions.Active) * Speed;
		MoveAndSlide();
	}

	// Reconciles the resolver against what is actually held right now, rather than
	// reacting to just-pressed/just-released edges. Edges are missed whenever this
	// node does not run — most obviously while a combat overlay has the tree paused —
	// which would leave a direction stuck as held and the player sliding after the
	// fight. Polling the current state cannot drift that way.
	private void SyncHeldDirections()
	{
		foreach ((Direction direction, string action) in MovementActions)
		{
			bool pressed = Input.IsActionPressed(action);

			// GetActionStrength is how hard the input is engaged: always 1 for a key or a
			// D-pad, and the deadzone-scaled axis magnitude for a stick. Feeding it in is
			// what lets the resolver follow the dominant axis of a diagonal push instead
			// of whichever of the two directions happened to cross the deadzone last.
			float strength = Input.GetActionStrength(action);

			if (pressed && !_directions.IsHeld(direction))
				_directions.Press(direction, strength);
			else if (!pressed && _directions.IsHeld(direction))
				_directions.Release(direction);
			else if (pressed)
				_directions.SetStrength(direction, strength);
		}
	}

	private static Vector2 ToVector(Direction? direction) => direction switch
	{
		Direction.Up => Vector2.Up,
		Direction.Down => Vector2.Down,
		Direction.Left => Vector2.Left,
		Direction.Right => Vector2.Right,
		_ => Vector2.Zero
	};
}
