using System;

/// <summary>
/// The experience curve of each world that has one. The Real world has none: there a
/// level is the character's age, and no experience is earned.
/// </summary>
public sealed record ExperienceCurves
{
	public ExperienceCurves(ExperienceCurve fantasy, ExperienceCurve nightmare)
	{
		Fantasy = fantasy ?? throw new ArgumentNullException(nameof(fantasy));
		Nightmare = nightmare ?? throw new ArgumentNullException(nameof(nightmare));
	}

	public ExperienceCurve Fantasy { get; }

	public ExperienceCurve Nightmare { get; }

	/// <summary>The curve of a world, or <c>null</c> for the Real world, which has none.</summary>
	public ExperienceCurve For(World world) => world switch
	{
		World.Real => null,
		World.Fantasy => Fantasy,
		World.Nightmare => Nightmare,
		_ => throw new ArgumentOutOfRangeException(nameof(world), world, "Not a world.")
	};

	/// <summary>
	/// Adds experience to a character's progress in a world. In the Real world it does
	/// nothing and returns the progress as it was.
	/// </summary>
	public LevelProgress Add(World world, LevelProgress progress, long award)
	{
		if (award < 0)
			throw new ArgumentOutOfRangeException(nameof(award), award, "An award of experience cannot be negative.");

		ExperienceCurve curve = For(world);

		return curve is null ? progress : curve.Add(progress, award);
	}
}
