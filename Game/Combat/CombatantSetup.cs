using System;

/// <summary>
/// What one combatant brings into a fight: the key of its name, its stats, and the health
/// and mana it has left, which need not be the most its stats allow.
/// </summary>
/// <remarks>
/// Checked here, once, when it is built: health and mana go from 0 to the stats' own
/// <see cref="BaseStats.HealthPoints"/> and <see cref="BaseStats.ManaPoints"/>. Someone
/// with 0 health comes into the fight already down.
/// </remarks>
public sealed record CombatantSetup
{
	public CombatantSetup(string nameKey, BaseStats stats, int healthPoints, int manaPoints)
	{
		if (string.IsNullOrWhiteSpace(nameKey))
			throw new ArgumentException("A combatant needs the key of its name.", nameof(nameKey));

		if (healthPoints < 0 || healthPoints > stats.HealthPoints)
			throw new ArgumentOutOfRangeException(nameof(healthPoints), healthPoints, $"Health goes from 0 to the stats' maximum, {stats.HealthPoints}.");

		if (manaPoints < 0 || manaPoints > stats.ManaPoints)
			throw new ArgumentOutOfRangeException(nameof(manaPoints), manaPoints, $"Mana goes from 0 to the stats' maximum, {stats.ManaPoints}.");

		NameKey = nameKey;
		Stats = stats;
		HealthPoints = healthPoints;
		ManaPoints = manaPoints;
	}

	public string NameKey { get; }

	public BaseStats Stats { get; }

	public int HealthPoints { get; }

	public int ManaPoints { get; }

	/// <summary>A combatant with all the health and mana its stats allow.</summary>
	public static CombatantSetup AtFull(string nameKey, BaseStats stats) =>
		new(nameKey, stats, stats.HealthPoints, stats.ManaPoints);
}
