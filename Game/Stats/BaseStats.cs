using System;

/// <summary>
/// The seven base stats of one combatant at one moment, as plain numbers. It is what the
/// formulas in <see cref="StatFormulas"/> read, and it does not know whose stats they are,
/// which level produced them or which world they belong to.
/// </summary>
/// <remarks>
/// A stat of 0 is valid. A negative one is not, and is refused here, once, so that no
/// formula has to decide what a negative armor means: a wrong number fails where it is
/// built instead of coming out the other end as a plausible percentage.
/// </remarks>
public readonly record struct BaseStats
{
	public BaseStats(int healthPoints, int attackDamage, int armor, int manaPoints, int agility, int luck, int hitRate)
	{
		HealthPoints = NotNegative(healthPoints, nameof(healthPoints));
		AttackDamage = NotNegative(attackDamage, nameof(attackDamage));
		Armor = NotNegative(armor, nameof(armor));
		ManaPoints = NotNegative(manaPoints, nameof(manaPoints));
		Agility = NotNegative(agility, nameof(agility));
		Luck = NotNegative(luck, nameof(luck));
		HitRate = NotNegative(hitRate, nameof(hitRate));
	}

	public int HealthPoints { get; }

	public int AttackDamage { get; }

	public int Armor { get; }

	public int ManaPoints { get; }

	public int Agility { get; }

	public int Luck { get; }

	/// <summary>Accuracy, where 1 point is 1%. What it has above 100 feeds critical damage.</summary>
	public int HitRate { get; }

	private static int NotNegative(int value, string name)
	{
		if (value < 0)
			throw new ArgumentOutOfRangeException(name, value, "A base stat cannot be negative.");

		return value;
	}
}
