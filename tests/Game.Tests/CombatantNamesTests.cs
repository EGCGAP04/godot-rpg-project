using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatantNames"/>: a name alone when its kind is alone on its side,
/// and a letter by position when the kind repeats.
/// </summary>
public class CombatantNamesTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		[CombatantNames.NameWithLetter] = "{name}-{letter}",
		["enemy.weak"] = "Weak",
		["enemy.strong"] = "Strong",
		["enemy.demo_clumsy"] = "Clumsy",
		["speaker.hero"] = "Hero",
		["speaker.abe"] = "Abe"
	};

	private static string Translate(string key) => Texts[key];

	private static IReadOnlyList<string> Side(params string[] keys) => CombatantNames.ForSide(keys, Translate);

	[Fact]
	public void AKindAloneOnItsSide_IsShownByItsName()
	{
		Assert.Equal(new[] { "Weak", "Strong", "Clumsy" }, Side("enemy.weak", "enemy.strong", "enemy.demo_clumsy"));
	}

	[Fact]
	public void AKindThatRepeats_IsToldApartByALetter_InTheOrderOfTheSide()
	{
		Assert.Equal(new[] { "Weak-A", "Clumsy", "Weak-B" }, Side("enemy.weak", "enemy.demo_clumsy", "enemy.weak"));
	}

	[Fact]
	public void EachKindThatRepeats_HasLettersOfItsOwn()
	{
		Assert.Equal(
			new[] { "Strong-A", "Weak-A", "Strong-B", "Weak-B" },
			Side("enemy.strong", "enemy.weak", "enemy.strong", "enemy.weak"));
	}

	[Fact]
	public void FourOfAKind_GoFromAToD()
	{
		Assert.Equal(new[] { "Weak-A", "Weak-B", "Weak-C", "Weak-D" }, Side("enemy.weak", "enemy.weak", "enemy.weak", "enemy.weak"));
	}

	[Fact]
	public void MoreOfAKindThanThereAreLetters_IsRefused()
	{
		Assert.Throws<ArgumentException>(() => Side(Enumerable.Repeat("enemy.weak", 5).ToArray()));
	}

	[Fact]
	public void AMissingKey_IsRefused()
	{
		Assert.Throws<ArgumentException>(() => Side("enemy.weak", null));
		Assert.Throws<ArgumentException>(() => Side("enemy.weak", ""));
	}

	[Fact]
	public void Of_GivesEachCombatantItsName_OnItsOwnSide()
	{
		// The same kind on the two sides does not take a letter: the letters are per side.
		var names = new CombatantNames(new[] { "speaker.hero", "speaker.abe" }, new[] { "enemy.weak", "speaker.hero", "enemy.weak" }, Translate);

		Assert.Equal("Hero", names.Of(CombatantId.Party(0)));
		Assert.Equal("Abe", names.Of(CombatantId.Party(1)));
		Assert.Equal("Weak-A", names.Of(CombatantId.Enemy(0)));
		Assert.Equal("Hero", names.Of(CombatantId.Enemy(1)));
		Assert.Equal("Weak-B", names.Of(CombatantId.Enemy(2)));
	}

	[Fact]
	public void Of_RefusesAPositionTheSideDoesNotHave()
	{
		var names = new CombatantNames(new[] { "speaker.hero" }, new[] { "enemy.weak" }, Translate);

		Assert.Throws<ArgumentException>(() => names.Of(CombatantId.Party(1)));
		Assert.Throws<ArgumentException>(() => names.Of(CombatantId.Enemy(1)));
	}

	[Fact]
	public void Of_ALineup_NamesItsMembersAndItsEnemies()
	{
		var texts = new Dictionary<string, string>(Texts) { ["enemy.foe"] = "Foe" };
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, new EncounterValues("pair", TestFights.Foe().Enemies.Concat(TestFights.Foe().Enemies)));

		CombatantNames names = CombatantNames.Of(lineup, key => texts[key]);

		Assert.Equal("Abe", names.Of(CombatantId.Party(1)));
		Assert.Equal("Foe-B", names.Of(CombatantId.Enemy(1)));
	}

	[Theory]
	[InlineData("en", "Strong Enemy A")]
	[InlineData("es", "Enemigo fuerte A")]
	public void WithTheGamesTables_TheLetterFollowsTheName(string locale, string expected)
	{
		Dictionary<string, string> texts = ProjectFiles.TranslationFiles()
			.Where(file => file.Folder == LocalizationNames.SharedFolder && file.Locale == locale)
			.Select(TranslationTableReader.Read)
			.SelectMany(table => table.Entries)
			.ToDictionary(entry => entry.Key, entry => entry.Text);

		Assert.Equal(expected, CombatantNames.ForSide(new[] { "enemy.strong", "enemy.strong" }, key => texts[key])[0]);
	}
}
