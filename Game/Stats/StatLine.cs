using System;

/// <summary>
/// How one stat grows with the level: a straight line, worth <see cref="Base"/> at
/// level 1 and <see cref="Growth"/> more for each level after it.
/// </summary>
/// <remarks>
/// <para>
/// The two numbers are <c>decimal</c>, not <c>double</c>, because the result is rounded
/// down. A growth of 0.29 does not exist in binary: over 100 levels a <c>double</c> adds
/// up to 28.999999999999996, and rounding that down gives 28 where the design says 29.
/// In <c>decimal</c>, 0.29 is exactly 0.29.
/// </para>
/// <para>
/// Neither number may be negative: a stat never starts below 0 and never falls with the
/// level. A line of 0 and 0 is valid, and is a stat the character does not have.
/// </para>
/// </remarks>
public readonly record struct StatLine
{
	public StatLine(decimal baseValue, decimal growth)
	{
		if (baseValue < 0)
			throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "A stat's base cannot be negative.");

		if (growth < 0)
			throw new ArgumentOutOfRangeException(nameof(growth), growth, "A stat's growth cannot be negative.");

		Base = baseValue;
		Growth = growth;
	}

	/// <summary>The stat at level 1.</summary>
	public decimal Base { get; }

	/// <summary>What each level after the first adds. It may be a fraction.</summary>
	public decimal Growth { get; }

	/// <summary>
	/// The stat at a level, rounded down to a whole number. The rounding happens here,
	/// once, so everything that shows or uses the stat reads the same value.
	/// </summary>
	public int At(int level)
	{
		if (level < 1)
			throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");

		decimal value = decimal.Floor(Base + (Growth * (level - 1)));

		if (value > int.MaxValue)
			throw new OverflowException($"A stat of {value} at level {level} does not fit in a base stat.");

		return (int)value;
	}
}
