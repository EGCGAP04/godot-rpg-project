namespace GodotRPGProject.Tests;

/// <summary>
/// The tuning the formula tests run with: the design's numbers, with a round <c>K</c> of
/// 100 for attacks and 50 for skills so expected values can be worked out by hand. Any
/// one value can be replaced, which is how a test builds a wrong tuning.
/// </summary>
/// <remarks>
/// It is deliberately not read from <c>stat_tuning.tres</c>: the formulas' tests should
/// not change when the game is tuned.
/// </remarks>
public static class TestTuning
{
	public static StatTuningValues Values(
		ByAction? armorK = null,
		ByAction? armorReductionCap = null,
		ByAction? luckK = null,
		ByAction? criticalChanceCap = null,
		double hitChanceFloor = 25,
		double criticalDamageBase = 150,
		ByAction? criticalDamageCap = null,
		double evasionCap = 50,
		double escapeBase = 50,
		double escapeFloor = 10,
		double escapeCap = 90) =>
		new(
			armorK ?? new ByAction(100, 50),
			armorReductionCap ?? new ByAction(90, 80),
			luckK ?? new ByAction(100, 50),
			criticalChanceCap ?? new ByAction(95, 90),
			hitChanceFloor,
			criticalDamageBase,
			criticalDamageCap ?? new ByAction(500, 300),
			evasionCap,
			escapeBase,
			escapeFloor,
			escapeCap);

	public static StatFormulas Formulas() => new(Values());

	/// <summary>Stats with only the named ones set, so a test reads as what it is about.</summary>
	public static BaseStats Stats(int healthPoints = 0, int attackDamage = 0, int armor = 0, int manaPoints = 0, int agility = 0, int luck = 0, int hitRate = 0) =>
		new(healthPoints, attackDamage, armor, manaPoints, agility, luck, hitRate);
}
