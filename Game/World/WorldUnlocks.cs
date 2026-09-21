using System;

/// <summary>
/// Which of a cycle's optional worlds the player has earned access to this cycle.
/// </summary>
/// <remarks>
/// <see cref="World.Real"/> is deliberately absent: every cycle starts there, so it
/// is never optional and <see cref="IsUnlocked"/> always reports it as available.
/// Passing this as one value rather than two loose booleans keeps call sites from
/// reading as <c>Next(position, true, false)</c>.
/// </remarks>
public readonly record struct WorldUnlocks(bool Fantasy, bool Nightmare)
{
	/// <summary>Everything reachable. This is what M5 always uses — the conditions that
	/// actually gate the two optional worlds arrive with the decision registry in M6.</summary>
	public static WorldUnlocks All => new(Fantasy: true, Nightmare: true);

	/// <summary>Whether <paramref name="world"/> can be entered this cycle.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is not a declared <see cref="World"/>.</exception>
	public bool IsUnlocked(World world) => world switch
	{
		World.Real => true,
		World.Fantasy => Fantasy,
		World.Nightmare => Nightmare,
		_ => throw new ArgumentOutOfRangeException(nameof(world), world, "Not a declared world.")
	};
}
