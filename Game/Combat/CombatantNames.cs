using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The name each combatant of a fight is shown by. Two of one kind on a side are told
/// apart by a letter, A, B, C or D, given by position; one whose kind does not repeat is
/// shown by its name alone.
/// </summary>
/// <remarks>
/// Engine-free: it takes the translation as a function, as <c>Tr</c> gives it. The letters
/// are given once, by the order the side was given in, so they do not move when one of
/// those it tells apart goes down. Changing the language means building it again.
/// </remarks>
public sealed class CombatantNames
{
	/// <summary>A name and its letter: <c>{name} {letter}</c>.</summary>
	public const string NameWithLetter = "ui.combat.name_with_letter";

	private const string Letters = "ABCD";

	private readonly IReadOnlyList<string> _party;
	private readonly IReadOnlyList<string> _enemies;

	/// <param name="partyKeys">The key of each member's name, in the party's order.</param>
	/// <param name="enemyKeys">The key of each enemy's name, in the group's order.</param>
	/// <param name="translate">A key's text, as <c>Tr</c> gives it.</param>
	public CombatantNames(IReadOnlyList<string> partyKeys, IReadOnlyList<string> enemyKeys, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(partyKeys);
		ArgumentNullException.ThrowIfNull(enemyKeys);
		ArgumentNullException.ThrowIfNull(translate);

		_party = ForSide(partyKeys, translate);
		_enemies = ForSide(enemyKeys, translate);
	}

	/// <summary>The names of a lineup's combatants.</summary>
	public static CombatantNames Of(CombatLineup lineup, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(lineup);

		return new CombatantNames(
			lineup.Party.Select(member => member.NameKey).ToArray(),
			lineup.Enemies.Select(enemy => enemy.NameKey).ToArray(),
			translate);
	}

	/// <summary>The name a combatant is shown by.</summary>
	/// <exception cref="ArgumentException">The side has nobody at that position.</exception>
	public string Of(CombatantId id)
	{
		IReadOnlyList<string> side = id.Side == CombatSide.Party ? _party : _enemies;

		if (id.Position >= side.Count)
			throw new ArgumentException($"{id} is not in this fight, whose side has {side.Count}.", nameof(id));

		return side[id.Position];
	}

	/// <summary>The name each combatant of one side is shown by, in the side's order.</summary>
	/// <exception cref="ArgumentException">
	/// One kind repeats more than there are letters, or a key is missing.
	/// </exception>
	public static IReadOnlyList<string> ForSide(IReadOnlyList<string> keys, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(keys);
		ArgumentNullException.ThrowIfNull(translate);

		if (keys.Any(string.IsNullOrEmpty))
			throw new ArgumentException("A combatant's name key is missing.", nameof(keys));

		Dictionary<string, int> counts = keys.GroupBy(key => key).ToDictionary(kind => kind.Key, kind => kind.Count());
		var seen = new Dictionary<string, int>();
		var names = new List<string>();

		foreach (string key in keys)
		{
			string name = translate(key);

			if (counts[key] == 1)
			{
				names.Add(name);
				continue;
			}

			int index = seen.GetValueOrDefault(key);
			seen[key] = index + 1;

			if (index >= Letters.Length)
				throw new ArgumentException($"'{key}' is on one side {counts[key]} times, and there are letters for {Letters.Length}.", nameof(keys));

			names.Add(TextTemplate.Fill(translate(NameWithLetter), ("name", name), ("letter", Letters[index].ToString())));
		}

		return names.AsReadOnly();
	}
}
