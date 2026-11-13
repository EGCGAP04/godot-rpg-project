using System.Linq;

namespace GodotRPGProject.Tests;

/// <summary>
/// Characters and rosters for the tests, with round numbers of their own, so changing
/// the game's characters does not break a test of the rules.
/// </summary>
internal static class TestRoster
{
	public const string Hero = "hero";

	/// <summary>A growth with health points and mana points only.</summary>
	public static StatGrowthValues Growth(decimal healthPoints, decimal healthGrowth = 0, decimal manaPoints = 0, decimal manaGrowth = 0) => new(
		HealthPoints: new StatLine(healthPoints, healthGrowth),
		AttackDamage: new StatLine(4, 1),
		Armor: default,
		ManaPoints: new StatLine(manaPoints, manaGrowth),
		Agility: default,
		Luck: default,
		HitRate: default);

	/// <summary>
	/// A character aged 12, with numbers of their own in each world: 40 health points and 1
	/// more per year in the Real one (51 at 12), 20 and 5 more per level in the Fantasy one,
	/// 30 and 10 more in the Nightmare one. Mana points: none, 10 and 2 more, 6 and 3 more.
	/// </summary>
	public static CharacterValues Character(string id) =>
		new(id, $"speaker.{id}", realAge: 12, real: Growth(40, 1), fantasy: Growth(20, 5, 10, 2), nightmare: Growth(30, 10, 6, 3));

	/// <summary>A roster with <see cref="Hero"/> as the protagonist, and the others in the order given.</summary>
	public static CharacterRosterValues With(params string[] others) =>
		new(Character(Hero), others.Select(Character));

	/// <summary>
	/// Curves with costs to work out by hand. Fantasy: 10 times the level (10, 20, 30, 40),
	/// up to level 5. Nightmare: 100 times the level (100, 200), up to level 3.
	/// </summary>
	public static ExperienceCurves Curves() =>
		new(fantasy: new ExperienceCurve(10, 1, 5), nightmare: new ExperienceCurve(100, 1, 3));

	/// <summary>A party over <see cref="With"/> and <see cref="Curves"/>.</summary>
	public static PartyState Party(params string[] others) => new(With(others), Curves());
}
