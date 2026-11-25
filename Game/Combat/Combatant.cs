using System;

/// <summary>
/// One side's member while a fight lasts: who it is, its stats, and the health and mana
/// it has left. Everyone may read it; only the fight changes it, which is why what
/// changes it is <c>internal</c>.
/// </summary>
public sealed class Combatant
{
	public Combatant(CombatantId id, CombatantSetup setup)
	{
		ArgumentNullException.ThrowIfNull(setup);

		Id = id;
		NameKey = setup.NameKey;
		Stats = setup.Stats;
		HealthPoints = setup.HealthPoints;
		ManaPoints = setup.ManaPoints;
	}

	public CombatantId Id { get; }

	public string NameKey { get; }

	public BaseStats Stats { get; }

	public int HealthPoints { get; private set; }

	public int ManaPoints { get; private set; }

	/// <summary>Down at 0 health: it takes no turn and nobody targets it.</summary>
	public bool IsDown => HealthPoints == 0;

	/// <summary>
	/// The last round in which a focus raises this combatant's attack, or 0 when it is not
	/// focused. Only a member focuses, and going down loses the focus.
	/// </summary>
	public int FocusedThrough { get; private set; }

	/// <summary>
	/// Takes the damage out of the health, which stops at 0. The damage is final: armor
	/// was applied by whoever worked it out, and is not applied again here.
	/// </summary>
	internal void TakeDamage(int damage)
	{
		if (damage < 0)
			throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage cannot be negative.");

		HealthPoints = Math.Max(0, HealthPoints - damage);

		if (IsDown)
			FocusedThrough = 0;
	}

	/// <summary>Adds the mana, which stops at the most the stats allow.</summary>
	internal void RecoverMana(int mana)
	{
		if (mana < 0)
			throw new ArgumentOutOfRangeException(nameof(mana), mana, "Mana recovered cannot be negative.");

		ManaPoints = (int)Math.Min((long)ManaPoints + mana, Stats.ManaPoints);
	}

	/// <summary>Raises the attack up to <paramref name="round"/>, the last round included, whatever an earlier focus said.</summary>
	internal void FocusThrough(int round) => FocusedThrough = round;
}
