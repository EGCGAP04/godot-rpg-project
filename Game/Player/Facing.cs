using System;

/// <summary>
/// Whether a character facing one way is looking at something, which is what talking to
/// it takes: the player has to face it, not just stand next to it.
/// </summary>
/// <remarks>
/// With movement strictly 4-directional, "facing" means the target lies mostly in that
/// direction: further along that axis than along the other one. Exactly on the diagonal
/// counts for both axes, so the player never has to line up to the pixel.
/// </remarks>
public static class Facing
{
	/// <summary>Whether a character facing <paramref name="facing"/> is looking at a target.</summary>
	/// <param name="dx">How far the target is to the right of the character; negative is to the left.</param>
	/// <param name="dy">
	/// How far the target is below the character; negative is above. Godot's 2D y axis
	/// grows downwards, so this is the difference of the two positions as they are.
	/// </param>
	/// <returns>True as well when the target is exactly where the character is.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The value is not a declared <see cref="Direction"/>.</exception>
	public static bool IsToward(Direction facing, float dx, float dy)
	{
		if (dx == 0 && dy == 0)
			return true;

		float horizontal = Math.Abs(dx);
		float vertical = Math.Abs(dy);

		return facing switch
		{
			Direction.Right => dx > 0 && horizontal >= vertical,
			Direction.Left => dx < 0 && horizontal >= vertical,
			Direction.Down => dy > 0 && vertical >= horizontal,
			Direction.Up => dy < 0 && vertical >= horizontal,
			_ => throw new ArgumentOutOfRangeException(nameof(facing), facing, "Not a declared direction.")
		};
	}
}
