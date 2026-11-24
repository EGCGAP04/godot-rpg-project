using System;
using System.Collections.Generic;

/// <summary>
/// Turns the strength an analog stick gives an action into presses, one per push, for
/// the menus. A key or a D-pad button is already one press per press; a stick is not.
/// </summary>
/// <remarks>
/// <para>
/// Godot sends an <c>InputEventJoypadMotion</c> every time an axis moves, and every one
/// past the action's deadzone answers <c>true</c> to <c>IsActionPressed</c>, with no echo
/// to tell them apart. One push of the stick sends many of them on its way out and back,
/// so a menu reading it that way moved several steps per push (#117).
/// </para>
/// <para>
/// A press counts when the strength rises to <see cref="PressAt"/> or more, and the
/// stick counts as released only once it falls below <see cref="ReleaseBelow"/>. Two
/// thresholds, chosen by the state, are what make this hysteresis: with one, a stick
/// trembling around it would press again and again, and a fixed margin around one
/// threshold only moves it (the lesson of #64, in <see cref="DirectionResolver"/>).
/// The strength is Godot's, which already discounts the deadzone: 0 at the deadzone,
/// 1 at the end of the axis.
/// </para>
/// <para>
/// It keeps one state per action, so pushing left and then right is two presses even if
/// the stick never stops at the centre: the right push reports 0 for left, which releases
/// it. A new instance knows of no push, so a menu that opens with the stick already
/// pushed counts its first strength past <see cref="PressAt"/> as a press.
/// </para>
/// <para>
/// Deliberately a plain C# class with no Godot dependency: telling a stick from a key is
/// the menu's job, and this is the part worth unit-testing.
/// </para>
/// </remarks>
public sealed class StickPresses
{
	/// <summary>The strength at or above which a push counts as a press.</summary>
	public const float PressAt = 0.5f;

	/// <summary>The strength below which a pushed stick counts as released.</summary>
	public const float ReleaseBelow = 0.3f;

	private readonly HashSet<string> _pushed = new();

	/// <summary>
	/// Takes the strength the stick now gives <paramref name="action"/>, and answers
	/// whether that is a new press of it.
	/// </summary>
	/// <param name="action">The action, as the input map names it.</param>
	/// <param name="strength">From 0 to 1, as Godot reports it.</param>
	/// <exception cref="ArgumentException">The action is null or empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The strength is not a number from 0 to 1.</exception>
	public bool Press(string action, float strength)
	{
		ArgumentException.ThrowIfNullOrEmpty(action);

		if (float.IsNaN(strength) || strength < 0f || strength > 1f)
			throw new ArgumentOutOfRangeException(nameof(strength), strength, "A strength goes from 0 to 1.");

		if (_pushed.Contains(action))
		{
			if (strength < ReleaseBelow)
				_pushed.Remove(action);

			return false;
		}

		if (strength < PressAt)
			return false;

		_pushed.Add(action);
		return true;
	}
}
