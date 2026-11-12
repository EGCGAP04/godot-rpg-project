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

	/// <summary>A character aged 12, with 20 health points in every world and 5 more per level.</summary>
	public static CharacterValues Character(string id) =>
		new(id, $"speaker.{id}", realAge: 12, real: Growth(20, 5, 10, 2), fantasy: Growth(20, 5, 10, 2), nightmare: Growth(20, 5, 10, 2));

	/// <summary>A roster with <see cref="Hero"/> as the protagonist, and the others in the order given.</summary>
	public static CharacterRosterValues With(params string[] others) =>
		new(Character(Hero), others.Select(Character));
}
