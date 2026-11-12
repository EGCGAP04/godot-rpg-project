using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Every character the game has, as plain values: the protagonist, and everyone who can
/// join the party. In the game it comes from a <c>CharacterRoster</c> resource; a test
/// builds it directly.
/// </summary>
/// <remarks>
/// The protagonist is a parameter of its own instead of an entry marked in a list, so a
/// roster without one, or with two, cannot be written. Ids are unique across the roster:
/// the party, the dialogue and a save all name a character by id.
/// </remarks>
public sealed class CharacterRosterValues
{
	private readonly Dictionary<string, CharacterValues> _byId = new();

	public CharacterRosterValues(CharacterValues protagonist, IEnumerable<CharacterValues> others)
	{
		ArgumentNullException.ThrowIfNull(protagonist);
		ArgumentNullException.ThrowIfNull(others);

		var characters = new List<CharacterValues> { protagonist };
		characters.AddRange(others);

		foreach (CharacterValues character in characters)
		{
			if (character is null)
				throw new ArgumentException("A roster cannot hold an empty entry.", nameof(others));

			if (!_byId.TryAdd(character.Id, character))
				throw new ArgumentException($"The roster holds the character '{character.Id}' twice.", nameof(others));
		}

		Protagonist = protagonist;
		Characters = characters.AsReadOnly();
	}

	/// <summary>The character the player is. Always in the party.</summary>
	public CharacterValues Protagonist { get; }

	/// <summary>Every character, the protagonist first and the rest in the order given.</summary>
	public IReadOnlyList<CharacterValues> Characters { get; }

	/// <summary>Whether the roster has a character with this id.</summary>
	public bool Contains(string id) => id is not null && _byId.ContainsKey(id);

	/// <summary>The character with this id.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public CharacterValues Get(string id)
	{
		if (id is null || !_byId.TryGetValue(id, out CharacterValues character))
			throw new ArgumentException($"There is no character called '{id}'. The roster has: {string.Join(", ", Characters.Select(each => each.Id))}.", nameof(id));

		return character;
	}
}
