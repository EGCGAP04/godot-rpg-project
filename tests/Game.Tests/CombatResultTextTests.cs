using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatResultText"/>: which lines the combat screen shows when a
/// fight ends, and that the game's tables have the text for them.
/// </summary>
/// <remarks>
/// The numbers are <see cref="TestRoster"/>'s: in the Fantasy world a level costs 10 times
/// its number, so 10 experience takes a character from level 1 to 2, and 30 to level 3.
/// </remarks>
public class CombatResultTextTests
{
	private const string Hero = TestRoster.Hero;

	/// <summary>A translation that shows its key, so a test reads which key was asked for.</summary>
	private static readonly Dictionary<string, string> Texts = new()
	{
		[CombatResultText.Won] = "WON",
		[CombatResultText.Lost] = "LOST",
		[CombatResultText.ExperienceGained] = "+{experience}",
		[CombatResultText.LevelReached] = "{name} -> {level}",
		["speaker.hero"] = "Hero",
		["speaker.abe"] = "Abe",
		["speaker.bea"] = "Bea"
	};

	private static string Translate(string key) => Texts[key];

	private static string[] Lines(PartyState party, bool playerWon, World world, long experience) =>
		CombatResultText.Build(playerWon, party.PreviewAward(world, experience), party.Roster, Translate).Split('\n');

	[Fact]
	public void Build_AWinThatReachesNoLevel_SaysWhoWonAndTheExperience()
	{
		Assert.Equal(new[] { "WON", "+5" }, Lines(TestRoster.Party(), playerWon: true, World.Fantasy, 5));
	}

	[Fact]
	public void Build_AWinThatReachesALevel_NamesWhoReachedIt_AndTheLevelTheyEndAt()
	{
		// 30 pays for levels 2 and 3 at once: one line, with the last level.
		Assert.Equal(new[] { "WON", "+30", "Hero -> 3" }, Lines(TestRoster.Party(), playerWon: true, World.Fantasy, 30));
	}

	[Fact]
	public void Build_SaysEveryMemberWhoReachesALevel_InThePartysOrder_AndNobodyElse()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("bea");
		party.Recruit("abe");
		party.Award(World.Fantasy, 9);
		party.Dismiss("abe");
		party.Award(World.Fantasy, 12);
		party.Recruit("abe");

		// The hero and bea are at level 2 with 11 of 20; abe, who was out, at level 1 with 9 of 10.
		Assert.Equal(new[] { "WON", "+1", "Abe -> 2" }, Lines(party, playerWon: true, World.Fantasy, 1));
		Assert.Equal(new[] { "WON", "+9", "Hero -> 3", "Bea -> 3", "Abe -> 2" }, Lines(party, playerWon: true, World.Fantasy, 9));
	}

	[Fact]
	public void Build_AWinInTheRealWorld_SaysNothingAboutExperience()
	{
		Assert.Equal(new[] { "WON" }, Lines(TestRoster.Party(), playerWon: true, World.Real, 50));
	}

	[Fact]
	public void Build_AWinThatGivesNoExperience_SaysNothingAboutIt()
	{
		Assert.Equal(new[] { "WON" }, Lines(TestRoster.Party(), playerWon: true, World.Fantasy, 0));
	}

	[Fact]
	public void Build_AtTheHighestLevel_StillSaysTheExperience_AndNoLevel()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 1000);

		Assert.Equal(new[] { "WON", "+7" }, Lines(party, playerWon: true, World.Fantasy, 7));
	}

	[Fact]
	public void Build_ALoss_SaysOnlyThat_AndNeedsNoAward()
	{
		Assert.Equal("LOST", CombatResultText.Build(false, null, null, Translate));
		Assert.Equal(new[] { "LOST" }, Lines(TestRoster.Party(), playerWon: false, World.Fantasy, 30));
	}

	[Fact]
	public void Build_AWinWithoutAnAward_IsRefused()
	{
		Assert.Throws<ArgumentNullException>(() => CombatResultText.Build(true, null, TestRoster.With(), Translate));
		Assert.Throws<ArgumentNullException>(() => CombatResultText.Build(true, TestRoster.Party().PreviewAward(World.Fantasy, 1), null, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatResultText.Build(false, null, null, null));
	}

	[Fact]
	public void Build_DoesNotChangeTheParty()
	{
		PartyState party = TestRoster.Party();

		Lines(party, playerWon: true, World.Fantasy, 30);

		Assert.Equal(new LevelProgress(1, 0), party.ProgressOf(Hero, World.Fantasy).Level);
	}

	// --- The game's own text ---

	private static IEnumerable<TranslationTable> SharedTables(string locale) =>
		ProjectFiles.TranslationFiles()
			.Where(file => file.Folder == LocalizationNames.SharedFolder && file.Locale == locale)
			.Select(TranslationTableReader.Read);

	[Theory]
	[InlineData("en", "Player wins.\nGained 25 experience.\nHero reached level 2.")]
	[InlineData("es", "Gana el jugador.\nExperiencia ganada: 25.\nHéroe llegó al nivel 2.")]
	public void Build_WithTheGamesTables_FillsEveryPlaceholder(string locale, string expected)
	{
		// Through a constant, a key is invisible to GameKeysTests' search; this is its check.
		Dictionary<string, string> texts = SharedTables(locale).SelectMany(table => table.Entries).ToDictionary(entry => entry.Key, entry => entry.Text);
		var roster = new CharacterRosterValues(
			new CharacterValues("demo_hero", "speaker.demo_hero", 12, TestRoster.Growth(40, 1), TestRoster.Growth(20, 5), TestRoster.Growth(30, 10)),
			Array.Empty<CharacterValues>());
		var party = new PartyState(roster, TestRoster.Curves());

		string text = CombatResultText.Build(true, party.PreviewAward(World.Fantasy, 25), roster, key => texts[key]);

		Assert.Equal(expected, text);
		Assert.Equal(locale == "en" ? "Player loses." : "Pierde el jugador.", CombatResultText.Build(false, null, null, key => texts[key]));
	}
}
