using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// A group of enemies fought together, as plain values: an id of its own and its
/// enemies, in the order they stand in the fight. It is what the rules read, never the
/// <c>EncounterData</c> resource it is built from.
/// </summary>
/// <remarks>
/// The same kind of enemy may be in a group more than once. A group has from one enemy
/// to <see cref="MaximumSize"/>, which is how many the combat screen has room for. Most
/// groups can be fled from; one that cannot says so with <see cref="CannotFlee"/>.
/// </remarks>
public sealed class EncounterValues
{
	/// <summary>The most enemies a group can have.</summary>
	public const int MaximumSize = 4;

	private static readonly Regex IdShape = new(@"^[a-z][a-z0-9_]*\z");

	public EncounterValues(string id, IEnumerable<EnemyValues> enemies, bool cannotFlee = false)
	{
		if (id is null || !IdShape.IsMatch(id))
			throw new ArgumentException($"An encounter's id is lowercase letters, digits and underscores, starting with a letter; '{id}' is not.", nameof(id));

		ArgumentNullException.ThrowIfNull(enemies);
		List<EnemyValues> group = enemies.ToList();

		if (group.Count < 1 || group.Count > MaximumSize)
			throw new ArgumentException($"The encounter '{id}' has {group.Count} enemies: a group has from 1 to {MaximumSize}.", nameof(enemies));

		if (group.Contains(null))
			throw new ArgumentException($"The encounter '{id}' has an empty entry among its enemies.", nameof(enemies));

		try
		{
			Experience = group.Aggregate(0L, (sum, enemy) => checked(sum + enemy.Experience));
		}
		catch (OverflowException)
		{
			throw new ArgumentException($"The encounter '{id}' gives more experience than fits in 64 bits.", nameof(enemies));
		}

		Id = id;
		Enemies = group.AsReadOnly();
		CannotFlee = cannotFlee;
	}

	/// <summary>The group, as the history of fights records it. It never changes with the text.</summary>
	public string Id { get; }

	public IReadOnlyList<EnemyValues> Enemies { get; }

	/// <summary>What a win over the group gives: the sum of its enemies' experience.</summary>
	public long Experience { get; }

	/// <summary>Whether the party is refused when it tries to flee from this group.</summary>
	public bool CannotFlee { get; }
}
