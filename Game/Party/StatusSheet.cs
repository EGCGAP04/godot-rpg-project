using System;

/// <summary>
/// What the status screen shows of one character in one world: who they are, their
/// level, how far they are towards the next one, and their base stats. Deliberately a
/// plain C# class with no Godot dependency, so what shows in each world is unit-tested
/// without the engine; <see cref="StatusText"/> turns it into text.
/// </summary>
/// <remarks>
/// The derived stats are not here on purpose: the design hides them from the player, and
/// they show only in the debug overlay.
/// </remarks>
/// <param name="NameKey">The key of the character's name.</param>
/// <param name="World">The world these numbers belong to.</param>
/// <param name="Level">The level, which in the Real world is the character's age.</param>
/// <param name="LevelIsAge">Whether <paramref name="Level"/> is an age: true in the world with no experience.</param>
/// <param name="Experience">The experience gained since the last level, not a total. It is 0 where there is none.</param>
/// <param name="ToNextLevel">
/// What the next level costs, counted from the last one, so the character is
/// <paramref name="Experience"/> into it. It is <c>null</c> when there is no next level:
/// at the highest one, and in the world with no experience.
/// </param>
/// <param name="HealthPoints">The health points left; the most they can be is in <paramref name="Stats"/>.</param>
/// <param name="ManaPoints">The mana points left; the most they can be is in <paramref name="Stats"/>.</param>
/// <param name="Stats">The base stats at this level.</param>
public sealed record StatusSheet(
	string NameKey,
	World World,
	int Level,
	bool LevelIsAge,
	long Experience,
	long? ToNextLevel,
	int HealthPoints,
	int ManaPoints,
	BaseStats Stats)
{
	/// <summary>The sheet of a character of the roster, a member of the party or not, in a world.</summary>
	/// <exception cref="ArgumentException">The roster has no such character.</exception>
	public static StatusSheet For(PartyState party, string id, World world)
	{
		ArgumentNullException.ThrowIfNull(party);

		CharacterProgress progress = party.ProgressOf(id, world);
		ExperienceCurve curve = party.Curves.For(world);

		// ToNextLevel throws at the highest level, where there is nothing to pay for, so
		// that case is settled before asking.
		long? toNextLevel = curve != null && progress.Level.Level < curve.HighestLevel
			? curve.ToNextLevel(progress.Level.Level)
			: null;

		return new StatusSheet(
			NameKey: party.Roster.Get(id).NameKey,
			World: world,
			Level: progress.Level.Level,
			LevelIsAge: curve == null,
			Experience: progress.Level.Experience,
			ToNextLevel: toNextLevel,
			HealthPoints: progress.HealthPoints,
			ManaPoints: progress.ManaPoints,
			Stats: party.StatsOf(id, world));
	}
}
