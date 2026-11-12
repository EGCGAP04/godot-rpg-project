using System;

/// <summary>
/// Where a character stands in one world: a level, and the experience gathered towards
/// the next one. It is all a character keeps about growing; the stats are computed from
/// the level.
/// </summary>
/// <remarks>
/// The experience is what was gathered since the last level, not a total since level 1.
/// A total would tie the level to the curve, so retuning the curve would move the level
/// of every character already played. This way a level, once reached, stays.
/// </remarks>
public readonly record struct LevelProgress
{
	public LevelProgress(int level, long experience)
	{
		if (level < 1)
			throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");

		if (experience < 0)
			throw new ArgumentOutOfRangeException(nameof(experience), experience, "Experience cannot be negative.");

		Level = level;
		Experience = experience;
	}

	public int Level { get; }

	/// <summary>The experience gathered towards the next level. In 64 bits: see <see cref="ExperienceCurve"/>.</summary>
	public long Experience { get; }
}
