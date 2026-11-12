using System;

/// <summary>
/// How much experience each level costs in one world, and where levels stop: going from
/// a level to the next takes <c>base * level ^ exponent</c>, rounded down.
/// </summary>
/// <remarks>
/// <para>
/// It is a formula and not a table, and the highest level is a value and not a constant,
/// because the design keeps open a mode that goes far past the standard highest level.
/// For the same reason experience is a 64-bit number: with a base of 10 and an exponent
/// of 3, level 999 alone costs about ten thousand million.
/// </para>
/// <para>
/// The values are checked when the curve is built, including that its most expensive
/// level fits in 64 bits, so <see cref="ToNextLevel"/> cannot fail later, in the middle
/// of handing out a fight's experience.
/// </para>
/// </remarks>
public sealed record ExperienceCurve
{
	/// <summary>The first number a <c>double</c> cannot turn into a <c>long</c>: 2^63.</summary>
	private const double BeyondLong = 9223372036854775808.0;

	public ExperienceCurve(double baseValue, double exponent, int highestLevel)
	{
		if (!double.IsFinite(baseValue) || baseValue <= 0)
			throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "The experience base must be a number above 0.");

		if (!double.IsFinite(exponent) || exponent <= 0)
			throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "The experience exponent must be a number above 0.");

		if (highestLevel < 1)
			throw new ArgumentOutOfRangeException(nameof(highestLevel), highestLevel, "The highest level must be 1 or more.");

		// The cost only grows with the level, so the last level to pay for is the dearest.
		double dearest = baseValue * Math.Pow(Math.Max(highestLevel - 1, 1), exponent);

		if (!double.IsFinite(dearest) || dearest >= BeyondLong)
			throw new ArgumentOutOfRangeException(nameof(exponent), exponent, $"With a base of {baseValue}, reaching level {highestLevel} costs more experience than 64 bits hold.");

		Base = baseValue;
		Exponent = exponent;
		HighestLevel = highestLevel;
	}

	/// <summary>What going from level 1 to level 2 costs.</summary>
	public double Base { get; }

	/// <summary>How fast the cost grows with the level: 1 is a straight line, 2 a square.</summary>
	public double Exponent { get; }

	/// <summary>The level at which experience stops counting.</summary>
	public int HighestLevel { get; }

	/// <summary>
	/// The experience it takes to go from a level to the next: rounded down, and never
	/// less than 1, so a level is never free.
	/// </summary>
	public long ToNextLevel(int level)
	{
		if (level < 1 || level >= HighestLevel)
			throw new ArgumentOutOfRangeException(nameof(level), level, $"There is a next level only from 1 to {HighestLevel - 1}.");

		return Math.Max(1, (long)Math.Floor(Base * Math.Pow(level, Exponent)));
	}

	/// <summary>
	/// Adds experience to a character's progress. One award may raise several levels, and
	/// what is left over stays as experience towards the next. At the highest level
	/// nothing is kept: the experience is 0 and the rest of the award is lost.
	/// </summary>
	/// <remarks>
	/// A progress that already holds a level's cost or more (the curve was retuned to
	/// cost less) rises too, even with an award of 0. A level above the highest (the
	/// highest was lowered) is left where it is: a level is never taken away.
	/// </remarks>
	public LevelProgress Add(LevelProgress progress, long award)
	{
		if (award < 0)
			throw new ArgumentOutOfRangeException(nameof(award), award, "An award of experience cannot be negative.");

		int level = Math.Max(progress.Level, 1);
		long experience = progress.Experience;

		while (level < HighestLevel)
		{
			long missing = ToNextLevel(level) - experience;

			if (missing > award)
				return new LevelProgress(level, experience + award);

			if (missing > 0)
			{
				award -= missing;
				experience = 0;
			}
			else
			{
				experience = -missing;
			}

			level++;
		}

		return new LevelProgress(level, 0);
	}
}
