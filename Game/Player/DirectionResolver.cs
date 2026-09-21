using System.Collections.Generic;

/// <summary>
/// Turns a set of simultaneously held directions into the single one a character is
/// actually moving in, and remembers which way it is facing after it stops.
/// </summary>
/// <remarks>
/// <para>
/// Two rules, in order. The <b>stronger</b> direction wins: a stick pushed at 30
/// degrees reports much more horizontal than vertical, and following the larger of
/// the two is what stops the character stuttering as the stick sweeps through a
/// diagonal. When strengths tie, the <b>most recently pressed</b> wins, and releasing
/// it falls back to the most recent one still held rather than stopping the
/// character — rolling a thumb across two keys should not freeze anything.
/// </para>
/// <para>
/// Digital input always reports full strength, so every direction from a keyboard or
/// a D-pad ties and the rule collapses to plain last-pressed-wins. The strength rule
/// only ever changes what an analog stick does.
/// </para>
/// <para>
/// Deliberately a plain C# class with no Godot dependency: which physical key or
/// stick produced a direction is the engine's problem, and this is the part worth
/// unit-testing.
/// </para>
/// </remarks>
public class DirectionResolver
{
	/// <summary>The strength reported by a fully engaged input, analog or not.</summary>
	public const float FullStrength = 1f;

	private readonly record struct HeldDirection(Direction Direction, float Strength);

	// Ordered oldest-first, so the last entry is the most recently pressed.
	private readonly List<HeldDirection> _held = new();

	private Direction _facing = Direction.Down;

	/// <summary>
	/// The direction being moved in, or <c>null</c> when nothing is held.
	/// </summary>
	public Direction? Active
	{
		get
		{
			if (_held.Count == 0)
				return null;

			// Starts from the most recent and only yields to something clearly stronger,
			// so a tie — every case that digital input can produce — keeps press order.
			HeldDirection winner = _held[^1];

			for (int i = _held.Count - 2; i >= 0; i--)
			{
				if (_held[i].Strength > winner.Strength)
					winner = _held[i];
			}

			return winner.Direction;
		}
	}

	/// <summary>
	/// The direction the character is facing. Follows <see cref="Active"/> while
	/// something is held and keeps its last value afterwards, so a character that
	/// stops goes on facing where it was going. Defaults to <see cref="Direction.Down"/>.
	/// </summary>
	public Direction Facing => _facing;

	/// <summary>Whether this direction is currently held.</summary>
	public bool IsHeld(Direction direction) => IndexOf(direction) >= 0;

	/// <summary>
	/// Records a direction as held. Pressing something already held moves it back to
	/// the front of the queue rather than duplicating it.
	/// </summary>
	/// <param name="strength">
	/// How hard the input is engaged, from 0 to 1. Digital input passes
	/// <see cref="FullStrength"/>, which is the default.
	/// </param>
	public void Press(Direction direction, float strength = FullStrength)
	{
		Remove(direction);
		_held.Add(new HeldDirection(direction, Clamp(strength)));
		RefreshFacing();
	}

	/// <summary>
	/// Updates how hard an already-held direction is engaged, which is what an analog
	/// stick changes every frame without ever pressing or releasing anything. Ignored
	/// for a direction that is not held.
	/// </summary>
	public void SetStrength(Direction direction, float strength)
	{
		int index = IndexOf(direction);

		if (index < 0)
			return;

		_held[index] = _held[index] with { Strength = Clamp(strength) };
		RefreshFacing();
	}

	/// <summary>
	/// Records a direction as no longer held. Releasing the winning one falls back to
	/// whatever wins among those still held, and facing follows it. Releasing a
	/// direction that was never held is a no-op, so callers do not have to track that
	/// themselves.
	/// </summary>
	public void Release(Direction direction)
	{
		Remove(direction);
		RefreshFacing();
	}

	private void RefreshFacing()
	{
		if (Active is Direction winner)
			_facing = winner;
	}

	private int IndexOf(Direction direction) => _held.FindIndex(held => held.Direction == direction);

	private void Remove(Direction direction)
	{
		int index = IndexOf(direction);

		if (index >= 0)
			_held.RemoveAt(index);
	}

	private static float Clamp(float strength) => strength < 0f ? 0f : strength > FullStrength ? FullStrength : strength;
}
