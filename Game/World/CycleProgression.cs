using System;

/// <summary>
/// The rule for what comes after the world being played now. Deliberately a plain
/// static class with no Godot dependency, so the whole progression — including the
/// cycle wrap and every skip — is unit-tested without starting the engine.
/// <c>GameState</c> holds the state and emits the signals around it.
/// </summary>
public static class CycleProgression
{
	/// <summary>The first cycle's number. Cycles count from 1, not 0, because the
	/// number is player-facing.</summary>
	public const int FirstCycle = 1;

	/// <summary>
	/// Returns the position that follows <paramref name="current"/>: the next unlocked
	/// world in the same cycle, or <see cref="World.Real"/> of the next cycle when none
	/// of the remaining worlds are unlocked.
	/// </summary>
	/// <remarks>
	/// Taking the unlocks as an input costs one parameter today and is what keeps M6
	/// from having to change this signature: in the design, reaching the Fantasy and
	/// Nightmare worlds depends on what the player did, and some endings close a cycle
	/// before its later worlds are ever played. In M5 every caller passes
	/// <see cref="WorldUnlocks.All"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="current"/> names a world that is not declared, or a cycle below
	/// <see cref="FirstCycle"/>.
	/// </exception>
	public static CyclePosition Next(CyclePosition current, WorldUnlocks unlocked)
	{
		if (!Enum.IsDefined(current.World))
			throw new ArgumentOutOfRangeException(nameof(current), current.World, "Not a declared world.");

		if (current.Cycle < FirstCycle)
			throw new ArgumentOutOfRangeException(nameof(current), current.Cycle, $"Cycles are numbered from {FirstCycle}.");

		// Walks forward through the enum rather than switching on each world, so that
		// skipping one, several or all of the remaining worlds is the same code path.
		for (World candidate = current.World + 1; candidate <= World.Nightmare; candidate++)
		{
			if (unlocked.IsUnlocked(candidate))
				return current with { World = candidate };
		}

		return new CyclePosition(World.Real, current.Cycle + 1);
	}
}
