using System;
using System.Text.RegularExpressions;

/// <summary>
/// One kind of enemy as plain values: its id, the key of its name, its seven base stats
/// and the experience a win over it gives. It is what the rules of a fight read, never
/// the <c>EnemyData</c> resource it is built from.
/// </summary>
/// <remarks>
/// <para>
/// An enemy's stats are fixed: it has no level and does not grow, unlike a character.
/// </para>
/// <para>
/// Checked here, once, when it is built. An enemy with no health would be down before
/// its fight starts, and one worth no experience would be a win that gives nothing, which
/// in a file means a value nobody wrote and not a choice. The name key is always
/// <c>enemy.&lt;id&gt;</c>, so an id is enough to find the name.
/// </para>
/// </remarks>
public sealed record EnemyValues
{
	private static readonly Regex IdShape = new(@"^[a-z][a-z0-9_]*\z");

	public EnemyValues(string id, string nameKey, BaseStats stats, long experience)
	{
		if (id is null || !IdShape.IsMatch(id))
			throw new ArgumentException($"An enemy's id is lowercase letters, digits and underscores, starting with a letter; '{id}' is not.", nameof(id));

		if (nameKey != NameKeyOf(id))
			throw new ArgumentException($"The enemy '{id}' has the name key '{nameKey}', not '{NameKeyOf(id)}'.", nameof(nameKey));

		if (stats.HealthPoints < 1)
			throw new ArgumentException($"The enemy '{id}' has no health points.", nameof(stats));

		if (experience < 1)
			throw new ArgumentOutOfRangeException(nameof(experience), experience, $"The enemy '{id}' gives no experience: every enemy gives 1 or more.");

		Id = id;
		NameKey = nameKey;
		Stats = stats;
		Experience = experience;
	}

	/// <summary>The kind of enemy, as the history of fights records it. It never changes with the text.</summary>
	public string Id { get; }

	/// <summary>The key of its name in <c>Localization/enemy.*.po</c>.</summary>
	public string NameKey { get; }

	public BaseStats Stats { get; }

	/// <summary>The experience a win over it gives, in 64 bits like the experience it is added to.</summary>
	public long Experience { get; }

	/// <summary>The name key an enemy with this id has.</summary>
	public static string NameKeyOf(string id) => $"enemy.{id}";
}
