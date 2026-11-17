using System;
using System.Collections.Generic;

namespace GodotRPGProject.Tests;

/// <summary>
/// The tests' dice: rolls written by hand, handed out in the order they were written.
/// </summary>
/// <remarks>
/// <para>
/// It is strict on purpose, so a test that scripts its rolls wrongly fails for that
/// reason and not with a puzzling result. Asking for a roll of the other kind than the
/// next one, asking for one when none is left, and a scripted index that does not fit the
/// count it is asked with all throw. <see cref="RequireUsedUp"/> lets a test also require
/// that nothing was left over, which is how the number of rolls becomes part of what it
/// checks.
/// </para>
/// <para>
/// The interface's two methods are implemented explicitly, so on the class itself
/// <see cref="Percent"/> and <see cref="Index"/> script a roll, and only through
/// <see cref="ICombatRandom"/> do they take one.
/// </para>
/// </remarks>
public sealed class ScriptedCombatRandom : ICombatRandom
{
	private readonly Queue<(bool IsPercent, double Value)> _rolls = new();
	private int _taken;

	/// <summary>How many scripted rolls have not been taken yet.</summary>
	public int Remaining => _rolls.Count;

	/// <summary>Scripts the next <see cref="ICombatRandom.Percent"/> roll.</summary>
	public ScriptedCombatRandom Percent(double roll)
	{
		if (!double.IsFinite(roll) || roll < 0 || roll >= 100)
			throw new ArgumentOutOfRangeException(nameof(roll), roll, "A percent roll goes from 0 up to, but not including, 100.");

		_rolls.Enqueue((true, roll));
		return this;
	}

	/// <summary>Scripts the next <see cref="ICombatRandom.Index"/> roll.</summary>
	public ScriptedCombatRandom Index(int roll)
	{
		if (roll < 0)
			throw new ArgumentOutOfRangeException(nameof(roll), roll, "An index roll cannot be negative.");

		_rolls.Enqueue((false, roll));
		return this;
	}

	/// <summary>Throws if any scripted roll was never taken.</summary>
	public void RequireUsedUp()
	{
		if (_rolls.Count > 0)
			throw new InvalidOperationException($"{_rolls.Count} scripted roll(s) were never taken; the next one is {Describe(_rolls.Peek())}.");
	}

	double ICombatRandom.Percent() => Take(isPercent: true);

	int ICombatRandom.Index(int count)
	{
		if (count <= 0)
			throw new ArgumentOutOfRangeException(nameof(count), count, "There is nothing to pick from: the count must be 1 or more.");

		if (_rolls.TryPeek(out var next) && !next.IsPercent && next.Value >= count)
			throw new InvalidOperationException($"Roll {_taken + 1} is scripted as {Describe(next)}, but it was asked to pick among {count}.");

		return (int)Take(isPercent: false);
	}

	private double Take(bool isPercent)
	{
		string asked = isPercent ? "a percent roll" : "an index roll";

		if (!_rolls.TryPeek(out var next))
			throw new InvalidOperationException($"Roll {_taken + 1} was asked for, {asked}, but only {_taken} were scripted.");

		if (next.IsPercent != isPercent)
			throw new InvalidOperationException($"Roll {_taken + 1} is scripted as {Describe(next)}, but {asked} was asked for.");

		_rolls.Dequeue();
		_taken++;
		return next.Value;
	}

	private static string Describe((bool IsPercent, double Value) roll) =>
		roll.IsPercent ? $"Percent({roll.Value})" : $"Index({roll.Value})";
}
