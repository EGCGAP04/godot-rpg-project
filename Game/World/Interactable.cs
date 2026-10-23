using Godot;

/// <summary>
/// Something in a world the player can talk to: standing close, facing it and pressing
/// <c>confirm</c> asks for its conversation. The same shape as <c>Enemy</c> and
/// <c>WorldExit</c>: the <c>Area2D</c> reports the request, and the world re-emits it for
/// <c>Main</c> to decide.
/// </summary>
/// <remarks>
/// The area is wider than the thing itself, so the player only has to stand next to it.
/// Whether the player is facing it is <see cref="Facing.IsToward"/>, from the player's
/// <see cref="Player.Facing"/> and where each of them stands. The press is marked as
/// handled once used, so it starts one conversation and nothing else.
/// </remarks>
public partial class Interactable : Area2D
{
	/// <summary>Emitted with the id of the conversation to start.</summary>
	[Signal]
	public delegate void InteractionTriggeredEventHandler(string conversation);

	/// <summary>The id of the conversation this starts: its file name in <c>Dialogue/Conversations/</c>.</summary>
	[Export]
	public string Conversation { get; set; } = "";

	private Player _player;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_player == null || !@event.IsActionPressed("confirm"))
			return;

		Vector2 offset = GlobalPosition - _player.GlobalPosition;

		if (!Facing.IsToward(_player.Facing, offset.X, offset.Y))
			return;

		GetViewport().SetInputAsHandled();
		EmitSignal(SignalName.InteractionTriggered, Conversation);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player player)
			_player = player;
	}

	private void OnBodyExited(Node2D body)
	{
		if (body == _player)
			_player = null;
	}
}
