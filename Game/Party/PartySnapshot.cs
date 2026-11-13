using System.Collections.Generic;

/// <summary>Where one character stands in one world, as a <see cref="PartySnapshot"/> holds it.</summary>
/// <param name="Id">The character's id.</param>
/// <param name="World">The world this progress belongs to.</param>
/// <param name="Progress">The level, the experience, and the health points and mana points left.</param>
public sealed record ProgressRecord(string Id, World World, CharacterProgress Progress);

/// <summary>
/// What one save slot keeps of the party: who is in it, in order, and where every
/// character stands in each world. It holds ids and numbers only, never a character's
/// stats, which are computed from the level.
/// </summary>
/// <param name="Members">The ids of the members, in the party's order, the protagonist first.</param>
/// <param name="Progress">One entry per character and world, for the whole roster, members or not.</param>
public sealed record PartySnapshot(
	IReadOnlyList<string> Members,
	IReadOnlyList<ProgressRecord> Progress);
