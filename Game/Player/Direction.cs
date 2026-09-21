/// <summary>
/// The four directions a character can move and face. The design calls for strictly
/// 4-directional movement with no diagonals, so this is the whole vocabulary — there
/// is deliberately no <c>None</c> member, because "not moving" is the absence of a
/// direction, not a direction of its own.
/// </summary>
/// <remarks>
/// <see cref="Down"/> is first so that the default value is the front-facing
/// direction, which is what a character faces before it has ever moved and what the
/// idle animation reuses.
/// </remarks>
public enum Direction
{
	Down,
	Up,
	Left,
	Right
}
