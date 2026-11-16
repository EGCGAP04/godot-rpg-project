using System;
using System.Linq;

/// <summary>
/// Changes the debug overlay makes to the party that the game itself never makes, such
/// as putting a character at a level. Deliberately a plain C# class with no Godot
/// dependency, covered by unit tests.
/// </summary>
/// <remarks>
/// Each one edits a <see cref="PartySnapshot"/> and leaves applying it to
/// <see cref="PartyState.Restore"/>. So the party gains no method that only the debug
/// tooling would call, and what a forced state may hold is checked by the same rules as
/// a loaded save: a level the character cannot have is refused there, in its words, and
/// nothing changes.
/// </remarks>
public static class PartyEdits
{
	/// <summary>
	/// A copy of <paramref name="snapshot"/> with one character at <paramref name="level"/>
	/// in one world, with no experience towards the next. Health points and mana points
	/// are cut down to what that level gives, and never raised: a forced level does not
	/// heal, as a level reached in the game does not.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// The snapshot has no progress for that character in that world, or the roster has
	/// no such character.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">The level is below 1, which no level is.</exception>
	public static PartySnapshot WithLevel(PartySnapshot snapshot, CharacterRosterValues roster, string id, World world, int level)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(roster);

		if (!snapshot.Progress.Any(record => record.Id == id && record.World == world))
			throw new ArgumentException($"The snapshot holds no progress for '{id}' in the {world} world.", nameof(id));

		var progress = new LevelProgress(level, 0);
		BaseStats stats = roster.Get(id).StatsAt(world, level);

		ProgressRecord[] records = snapshot.Progress
			.Select(record => record.Id == id && record.World == world
				? record with
				{
					Progress = new CharacterProgress(
						progress,
						Math.Min(record.Progress.HealthPoints, stats.HealthPoints),
						Math.Min(record.Progress.ManaPoints, stats.ManaPoints))
				}
				: record)
			.ToArray();

		return snapshot with { Progress = records };
	}
}
