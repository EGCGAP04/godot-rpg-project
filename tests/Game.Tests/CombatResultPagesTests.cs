using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatResultPages"/>: which lines the combat screen shows when a
/// fight ends, and how they are cut into pages.
/// </summary>
/// <remarks>
/// The fights are <see cref="TestFights"/>' and the characters <see cref="TestRoster"/>'s:
/// a win gives 7 experience, and in the Fantasy world level 2 costs 10.
/// </remarks>
public class CombatResultPagesTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		[CombatResultPages.Won] = "WON",
		[CombatResultPages.Lost] = "LOST",
		[CombatResultPages.Fled] = "FLED",
		[CombatResultPages.ExperienceGained] = "+{experience}",
		[CombatResultPages.LevelReached] = "{name} -> {level}",
		[CombatResultPages.WasDown] = "{name} was down",
		["speaker.hero"] = "Hero",
		["speaker.abe"] = "Abe",
		["speaker.bea"] = "Bea",
		["speaker.cy"] = "Cy"
	};

	private static string Translate(string key) => Texts[key];

	private static PartyState PartyOf(params string[] others)
	{
		PartyState party = TestRoster.Party(others);

		foreach (string id in others)
			party.Recruit(id);

		return party;
	}

	private static IReadOnlyList<string> Lines(PartyState party, CombatAftermath aftermath) =>
		CombatResultPages.Lines(aftermath, aftermath.PreviewAward(party), party.Roster, Translate);

	private static IReadOnlyList<string> Pages(PartyState party, CombatAftermath aftermath) =>
		CombatResultPages.Pages(aftermath, aftermath.PreviewAward(party), party.Roster, Translate);

	[Fact]
	public void AWin_SaysSo_TheExperience_AndWhoWasDown()
	{
		PartyState party = PartyOf("abe");

		Assert.Equal(new[] { "WON", "+7", "Abe was down" }, Lines(party, TestFights.AWinWhereTheLastGoesDown(party)));
	}

	[Fact]
	public void AWin_SaysWhoReachesALevel_BeforeWhoWasDown()
	{
		PartyState party = PartyOf("abe", "bea");
		party.Award(World.Fantasy, 5);

		Assert.Equal(new[] { "WON", "+7", "Hero -> 2", "Abe -> 2", "Bea was down" }, Lines(party, TestFights.AWinWhereTheLastGoesDown(party)));
	}

	[Fact]
	public void AMemberWhoWasDown_ReachesNoLevel()
	{
		// Bea had 5 too, and would reach level 2 with the win's 7, but she went down.
		PartyState party = PartyOf("bea");
		party.Award(World.Fantasy, 5);

		Assert.DoesNotContain("Bea -> 2", Lines(party, TestFights.AWinWhereTheLastGoesDown(party)));
	}

	[Fact]
	public void ALoss_IsOneLine()
	{
		PartyState party = TestRoster.Party();

		Assert.Equal(new[] { "LOST" }, Lines(party, TestFights.ALoss(party)));
	}

	[Fact]
	public void AFlight_IsOneLine_EvenWithAMemberWhoWasDown()
	{
		PartyState party = PartyOf("abe");

		Assert.Equal(new[] { "FLED" }, Lines(party, TestFights.AFlight(party, lastGoesDownFirst: true)));
	}

	[Fact]
	public void AWinThatGivesNoExperience_SaysNothingAboutIt()
	{
		PartyState party = PartyOf("abe");
		CombatAftermath aftermath = TestFights.AWinWhereTheLastGoesDown(party);
		var nothing = new ExperienceAward(World.Fantasy, 0, Array.Empty<MemberGain>());

		Assert.Equal(new[] { "WON", "Abe was down" }, CombatResultPages.Lines(aftermath, nothing, party.Roster, Translate));
	}

	[Fact]
	public void Pages_HoldFourLinesEach()
	{
		PartyState party = PartyOf("abe", "bea", "cy");
		party.Award(World.Fantasy, 5);

		IReadOnlyList<string> pages = Pages(party, TestFights.AWinWhereTheLastGoesDown(party));

		Assert.Equal(new[] { "WON\n+7\nHero -> 2\nAbe -> 2", "Bea -> 2\nCy was down" }, pages);
	}

	[Fact]
	public void Pages_OfFewLines_AreOnePage()
	{
		PartyState party = TestRoster.Party();

		Assert.Equal(new[] { "LOST" }, Pages(party, TestFights.ALoss(party)));
	}

	[Fact]
	public void Lines_DoNotChangeTheParty()
	{
		PartyState party = PartyOf("abe");
		CombatAftermath aftermath = TestFights.AWinWhereTheLastGoesDown(party);
		PartySnapshot before = party.TakeSnapshot();

		Pages(party, aftermath);

		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
	}

	[Fact]
	public void WhatIsMissing_IsRefused()
	{
		PartyState party = PartyOf("abe");
		CombatAftermath aftermath = TestFights.AWinWhereTheLastGoesDown(party);
		ExperienceAward award = aftermath.PreviewAward(party);

		Assert.Throws<ArgumentNullException>(() => CombatResultPages.Lines(null, award, party.Roster, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatResultPages.Lines(aftermath, null, party.Roster, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatResultPages.Lines(aftermath, award, null, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatResultPages.Lines(aftermath, award, party.Roster, null));
	}

	[Theory]
	[InlineData("en", "Player wins.|Gained 7 experience.|Hero reached level 2.|Friend was down and gains nothing.")]
	[InlineData("es", "Gana el jugador.|Experiencia ganada: 7.|Héroe llegó al nivel 2.|Amigo cayó y no gana nada.")]
	public void WithTheGamesTables_EveryLineIsFilled(string locale, string expected)
	{
		Dictionary<string, string> texts = ProjectFiles.TranslationFiles()
			.Where(file => file.Folder == LocalizationNames.SharedFolder && file.Locale == locale)
			.Select(TranslationTableReader.Read)
			.SelectMany(table => table.Entries)
			.ToDictionary(entry => entry.Key, entry => entry.Text);
		var roster = new CharacterRosterValues(
			new CharacterValues("demo_hero", "speaker.demo_hero", 12, TestRoster.Growth(40, 1), TestRoster.Growth(20, 5), TestRoster.Growth(30, 10)),
			new[] { new CharacterValues("demo_friend", "speaker.demo_friend", 12, TestRoster.Growth(40, 1), TestRoster.Growth(20, 5), TestRoster.Growth(30, 10)) });
		var party = new PartyState(roster, TestRoster.Curves());
		party.Recruit("demo_friend");
		party.Award(World.Fantasy, 5);

		CombatAftermath aftermath = TestFights.AWinWhereTheLastGoesDown(party);
		IReadOnlyList<string> lines = CombatResultPages.Lines(aftermath, aftermath.PreviewAward(party), roster, key => texts[key]);

		Assert.Equal(expected, string.Join("|", lines));
	}
}
