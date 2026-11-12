using System;
using System.Text.RegularExpressions;

/// <summary>
/// One character as plain values: who they are, and how their stats grow in each of the
/// three worlds. In the game they come from a <c>CharacterData</c> resource; a test
/// builds them directly.
/// </summary>
/// <remarks>
/// <para>
/// Levels and stats are independent per world, so a character carries three growths.
/// What a character keeps while playing (a level and its experience per world) is not
/// here: this is only what the character is, and it never changes during a game.
/// </para>
/// <para>
/// The values are checked here, once, when they are built. A character with no health
/// points in some world would not crash anything: they would enter a fight already
/// down. So each world's health points must be 1 or more at the level the character
/// starts with.
/// </para>
/// </remarks>
public sealed record CharacterValues
{
	private static readonly Regex IdShape = new(@"^[a-z][a-z0-9_]*\z");

	public CharacterValues(string id, string nameKey, int realAge, StatGrowthValues real, StatGrowthValues fantasy, StatGrowthValues nightmare)
	{
		if (id is null || !IdShape.IsMatch(id))
			throw new ArgumentException($"A character's id is lowercase letters, digits and underscores, starting with a letter; '{id}' is not.", nameof(id));

		if (string.IsNullOrEmpty(nameKey))
			throw new ArgumentException($"The character '{id}' has no name key.", nameof(nameKey));

		if (realAge < 1)
			throw new ArgumentOutOfRangeException(nameof(realAge), realAge, $"The character '{id}' needs an age of 1 or more: it is their level in the Real world.");

		Id = id;
		NameKey = nameKey;
		RealAge = realAge;
		Real = real ?? throw new ArgumentNullException(nameof(real), $"The character '{id}' has no stats for the Real world.");
		Fantasy = fantasy ?? throw new ArgumentNullException(nameof(fantasy), $"The character '{id}' has no stats for the Fantasy world.");
		Nightmare = nightmare ?? throw new ArgumentNullException(nameof(nightmare), $"The character '{id}' has no stats for the Nightmare world.");

		foreach (World world in Enum.GetValues<World>())
		{
			if (GrowthIn(world).HealthPoints.At(StartingLevel(world)) < 1)
				throw new ArgumentException($"The character '{id}' has no health points in the {world} world at level {StartingLevel(world)}.", world.ToString().ToLowerInvariant());
		}
	}

	/// <summary>
	/// What the party, the dialogue and a save call the character: a lowercase word such
	/// as <c>demo_hero</c>, unique among the characters, that never changes with the text.
	/// </summary>
	public string Id { get; }

	/// <summary>The key of the character's name in the translation tables, never the name itself.</summary>
	public string NameKey { get; }

	/// <summary>The character's age, which is their level in the Real world.</summary>
	public int RealAge { get; }

	public StatGrowthValues Real { get; }

	public StatGrowthValues Fantasy { get; }

	public StatGrowthValues Nightmare { get; }

	/// <summary>How the character's stats grow in a world.</summary>
	public StatGrowthValues GrowthIn(World world) => world switch
	{
		World.Real => Real,
		World.Fantasy => Fantasy,
		World.Nightmare => Nightmare,
		_ => throw new ArgumentOutOfRangeException(nameof(world), world, "Not a world.")
	};

	/// <summary>
	/// The level the character has in a world before anything raises it: their age in
	/// the Real world, and 1 in the other two.
	/// </summary>
	public int StartingLevel(World world) => world switch
	{
		World.Real => RealAge,
		World.Fantasy or World.Nightmare => 1,
		_ => throw new ArgumentOutOfRangeException(nameof(world), world, "Not a world.")
	};

	/// <summary>
	/// The character's seven base stats in a world at a level. The rule is the same in
	/// the three worlds; in the Real one the level is an age.
	/// </summary>
	public BaseStats StatsAt(World world, int level) => GrowthIn(world).At(level);
}
