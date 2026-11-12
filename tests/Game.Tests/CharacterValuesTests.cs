using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CharacterValues"/>: a character has a growth of its own in each
/// world, starts at their age in the Real one, and is checked where it is built.
/// </summary>
public class CharacterValuesTests
{
	/// <summary>A growth with only health points and attack, different in each world.</summary>
	private static StatGrowthValues Growth(decimal healthPoints, decimal healthGrowth = 0, decimal attackDamage = 0) => new(
		HealthPoints: new StatLine(healthPoints, healthGrowth),
		AttackDamage: new StatLine(attackDamage, 1),
		Armor: default,
		ManaPoints: default,
		Agility: default,
		Luck: default,
		HitRate: default);

	private static CharacterValues Character(
		string id = "test_hero",
		string nameKey = "speaker.test_hero",
		int realAge = 12,
		StatGrowthValues real = null,
		StatGrowthValues fantasy = null,
		StatGrowthValues nightmare = null) =>
		new(id, nameKey, realAge, real ?? Growth(7, 3, 1), fantasy ?? Growth(20, 4, 4), nightmare ?? Growth(15, 3, 3));

	[Fact]
	public void Constructor_KeepsWhoTheCharacterIs()
	{
		CharacterValues character = Character();

		Assert.Equal("test_hero", character.Id);
		Assert.Equal("speaker.test_hero", character.NameKey);
		Assert.Equal(12, character.RealAge);
	}

	[Fact]
	public void GrowthIn_GivesEachWorldItsOwnGrowth()
	{
		StatGrowthValues real = Growth(7);
		StatGrowthValues fantasy = Growth(20);
		StatGrowthValues nightmare = Growth(15);
		CharacterValues character = Character(real: real, fantasy: fantasy, nightmare: nightmare);

		Assert.Same(real, character.GrowthIn(World.Real));
		Assert.Same(fantasy, character.GrowthIn(World.Fantasy));
		Assert.Same(nightmare, character.GrowthIn(World.Nightmare));
	}

	[Fact]
	public void StartingLevel_IsTheAgeInTheRealWorld_AndOneInTheOthers()
	{
		CharacterValues character = Character(realAge: 12);

		Assert.Equal(12, character.StartingLevel(World.Real));
		Assert.Equal(1, character.StartingLevel(World.Fantasy));
		Assert.Equal(1, character.StartingLevel(World.Nightmare));
	}

	[Fact]
	public void StatsAt_UsesTheWorldsGrowthAndTheLevel()
	{
		CharacterValues character = Character();

		// Fantasy: 20 + 4 per level, and 4 + 1 per level.
		Assert.Equal(TestTuning.Stats(healthPoints: 20, attackDamage: 4), character.StatsAt(World.Fantasy, 1));
		Assert.Equal(TestTuning.Stats(healthPoints: 28, attackDamage: 6), character.StatsAt(World.Fantasy, 3));
		// Nightmare: 15 + 3 per level, and 3 + 1 per level.
		Assert.Equal(TestTuning.Stats(healthPoints: 21, attackDamage: 5), character.StatsAt(World.Nightmare, 3));
	}

	[Fact]
	public void StatsAt_InTheRealWorld_FollowsTheSameRuleWithTheAgeAsLevel()
	{
		CharacterValues character = Character(realAge: 12);

		// 7 + 3 * 11 and 1 + 1 * 11.
		Assert.Equal(TestTuning.Stats(healthPoints: 40, attackDamage: 12), character.StatsAt(World.Real, character.StartingLevel(World.Real)));
	}

	[Fact]
	public void GrowthInAndStartingLevel_SomethingThatIsNotAWorld_IsRefused()
	{
		CharacterValues character = Character();

		Assert.Throws<ArgumentOutOfRangeException>(() => character.GrowthIn((World)42));
		Assert.Throws<ArgumentOutOfRangeException>(() => character.StartingLevel((World)42));
	}

	[Theory]
	[InlineData("a")]
	[InlineData("demo_hero")]
	[InlineData("friend2")]
	public void Constructor_ALowercaseId_IsAccepted(string id)
	{
		Assert.Equal(id, Character(id: id).Id);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Hero")]
	[InlineData("demo hero")]
	[InlineData("demo-hero")]
	[InlineData("2nd")]
	[InlineData("_hero")]
	[InlineData("héroe")]
	[InlineData("hero\n")]
	public void Constructor_AnIdThatIsNotALowercaseWord_IsRefused(string id)
	{
		var exception = Assert.Throws<ArgumentException>(() => Character(id: id));

		Assert.Equal("id", exception.ParamName);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Constructor_AMissingNameKey_IsRefused(string nameKey)
	{
		var exception = Assert.Throws<ArgumentException>(() => Character(nameKey: nameKey));

		Assert.Equal("nameKey", exception.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_AnAgeBelowOne_IsRefused(int realAge)
	{
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Character(realAge: realAge));

		Assert.Equal("realAge", exception.ParamName);
	}

	[Fact]
	public void Constructor_AMissingWorld_IsRefusedByName()
	{
		StatGrowthValues growth = Growth(10);

		Assert.Equal("real", Assert.Throws<ArgumentNullException>(() => new CharacterValues("a", "speaker.a", 12, null, growth, growth)).ParamName);
		Assert.Equal("fantasy", Assert.Throws<ArgumentNullException>(() => new CharacterValues("a", "speaker.a", 12, growth, null, growth)).ParamName);
		Assert.Equal("nightmare", Assert.Throws<ArgumentNullException>(() => new CharacterValues("a", "speaker.a", 12, growth, growth, null)).ParamName);
	}

	[Fact]
	public void Constructor_NoHealthPointsInAWorld_IsRefusedNamingTheWorld()
	{
		Assert.Equal("real", Assert.Throws<ArgumentException>(() => Character(real: Growth(0))).ParamName);
		Assert.Equal("fantasy", Assert.Throws<ArgumentException>(() => Character(fantasy: Growth(0.5m, 9))).ParamName);
		Assert.Equal("nightmare", Assert.Throws<ArgumentException>(() => Character(nightmare: Growth(0, 9))).ParamName);
	}

	[Fact]
	public void Constructor_HealthPointsInTheRealWorld_AreAskedAtTheAge()
	{
		// Nothing at level 1, but 0 + 0.1 * 11 = 1.1 at the age of 12, which is where the character starts.
		Assert.Equal(1, Character(realAge: 12, real: Growth(0, 0.1m)).StatsAt(World.Real, 12).HealthPoints);

		// At the age of 5 it is 0.4: no health points.
		Assert.Throws<ArgumentException>(() => Character(realAge: 5, real: Growth(0, 0.1m)));
	}
}
