/// <summary>
/// How the seven base stats of one character grow in one world, as plain values: a
/// <see cref="StatLine"/> per stat. A character has one of these for each world.
/// </summary>
/// <remarks>
/// Stats are computed from the level and never stored. What a character keeps is a
/// level, and <see cref="At"/> turns it into the <see cref="BaseStats"/> the formulas
/// read, so changing a character's numbers changes every level of that character at once.
/// </remarks>
public sealed record StatGrowthValues(
	StatLine HealthPoints,
	StatLine AttackDamage,
	StatLine Armor,
	StatLine ManaPoints,
	StatLine Agility,
	StatLine Luck,
	StatLine HitRate)
{
	/// <summary>The seven base stats at a level, each rounded down. Levels start at 1.</summary>
	public BaseStats At(int level) => new(
		healthPoints: HealthPoints.At(level),
		attackDamage: AttackDamage.At(level),
		armor: Armor.At(level),
		manaPoints: ManaPoints.At(level),
		agility: Agility.At(level),
		luck: Luck.At(level),
		hitRate: HitRate.At(level));
}
