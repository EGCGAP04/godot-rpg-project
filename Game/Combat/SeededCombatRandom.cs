using System;

/// <summary>
/// The game's dice: an <see cref="ICombatRandom"/> over a <see cref="Random"/> built from
/// a seed. The same seed gives the same rolls in the same order, so a fight can be
/// repeated by starting it again from the seed it reports.
/// </summary>
/// <remarks>
/// .NET keeps the sequence of a seeded <see cref="Random"/> the same between versions; an
/// unseeded one gives no such promise, which is why a seed is always chosen here, even
/// when nobody asked for a particular one.
/// </remarks>
public sealed class SeededCombatRandom : ICombatRandom
{
	private readonly Random _random;

	/// <summary>Dice with a seed nobody chose. <see cref="Seed"/> says which one it was.</summary>
	public SeededCombatRandom()
		: this(Random.Shared.Next())
	{
	}

	public SeededCombatRandom(int seed)
	{
		Seed = seed;
		_random = new Random(seed);
	}

	/// <summary>The seed these dice started from.</summary>
	public int Seed { get; }

	public double Percent() => _random.NextDouble() * 100;

	public int Index(int count)
	{
		if (count <= 0)
			throw new ArgumentOutOfRangeException(nameof(count), count, "There is nothing to pick from: the count must be 1 or more.");

		return _random.Next(count);
	}
}
