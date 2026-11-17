/// <summary>How far an attack got through the damage pipeline.</summary>
public enum AttackOutcome
{
	/// <summary>The attacker's accuracy failed. Nothing else was rolled.</summary>
	Missed,

	/// <summary>It landed, and the defender evaded it. Nothing else was rolled.</summary>
	Evaded,

	/// <summary>It landed and was not evaded, so it did damage.</summary>
	Hit
}

/// <summary>
/// What <see cref="DamagePipeline"/> worked out for one attack. <see cref="Damage"/> is
/// final: armor is already taken out of it. An attack that missed or was evaded is never
/// critical and does 0.
/// </summary>
public readonly record struct AttackResult(AttackOutcome Outcome, bool IsCritical, int Damage)
{
	public static AttackResult Missed { get; } = new(AttackOutcome.Missed, false, 0);

	public static AttackResult Evaded { get; } = new(AttackOutcome.Evaded, false, 0);

	public static AttackResult Hit(int damage, bool isCritical) => new(AttackOutcome.Hit, isCritical, damage);
}
