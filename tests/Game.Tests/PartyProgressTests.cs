using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises what <see cref="PartyState"/> keeps for each character in each world: the
/// level, the experience, and the health points and mana points left.
/// </summary>
/// <remarks>
/// The numbers are <see cref="TestRoster"/>'s. In the Fantasy world a character has 20
/// health points and 10 mana points at level 1, and 5 and 2 more per level; a level costs
/// 10 times its number, up to level 5. In the Nightmare world: 30 and 6, 10 and 3 more,
/// 100 times, up to level 3. In the Real world: 51 health points at the age of 12.
/// </remarks>
public class PartyProgressTests
{
	private const string Hero = TestRoster.Hero;

	private static CharacterProgress Progress(int level, long experience, int healthPoints, int manaPoints) =>
		new(new LevelProgress(level, experience), healthPoints, manaPoints);

	/// <summary>Records every progress change the party reports, in order.</summary>
	private static List<(string Id, World World)> Changes(PartyState party)
	{
		var changes = new List<(string, World)>();
		party.ProgressChanged += (id, world) => changes.Add((id, world));
		return changes;
	}

	// --- Where everyone starts ---

	[Fact]
	public void Constructor_StartsEveryoneAtTheirStartingLevel_WithEverythingFull()
	{
		PartyState party = TestRoster.Party("abe");

		foreach (string id in new[] { Hero, "abe" })
		{
			Assert.Equal(Progress(12, 0, 51, 0), party.ProgressOf(id, World.Real));
			Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf(id, World.Fantasy));
			Assert.Equal(Progress(1, 0, 30, 6), party.ProgressOf(id, World.Nightmare));
		}
	}

	[Fact]
	public void Constructor_ACharacterWhoseStatsDoNotFitAtTheHighestLevel_IsRefused()
	{
		// Fine at level 1, and past what a base stat holds at level 5, the Fantasy curve's highest.
		var broken = new CharacterValues("abe", "speaker.abe", 12, TestRoster.Growth(40), TestRoster.Growth(20, 1_000_000_000), TestRoster.Growth(30));
		var roster = new CharacterRosterValues(TestRoster.Character(Hero), new[] { broken });

		var exception = Assert.Throws<ArgumentException>(() => new PartyState(roster, TestRoster.Curves()));

		Assert.Contains("'abe'", exception.Message);
		Assert.Contains("level 5", exception.Message);
		Assert.Contains("Fantasy", exception.Message);
	}

	[Fact]
	public void Constructor_ChecksTheRealWorldAtTheAge_NotAtACurvesLevel()
	{
		// 2 billion at the age of 12 fits; at 13 it would not. Nobody gets older here.
		var old = new CharacterValues("abe", "speaker.abe", 12, TestRoster.Growth(0, 180_000_000), TestRoster.Growth(20), TestRoster.Growth(30));
		var party = new PartyState(new CharacterRosterValues(TestRoster.Character(Hero), new[] { old }), TestRoster.Curves());

		Assert.Equal(1_980_000_000, party.StatsOf("abe", World.Real).HealthPoints);
	}

	[Theory]
	[InlineData("nobody")]
	[InlineData(null)]
	public void ProgressAndStats_OfACharacterThatDoesNotExist_AreRefused(string id)
	{
		PartyState party = TestRoster.Party();

		Assert.Throws<ArgumentException>(() => party.ProgressOf(id, World.Fantasy));
		Assert.Throws<ArgumentException>(() => party.StatsOf(id, World.Fantasy));
		Assert.Throws<ArgumentException>(() => party.SetHealthPoints(id, World.Fantasy, 1));
		Assert.Throws<ArgumentException>(() => party.SetManaPoints(id, World.Fantasy, 1));
		Assert.Throws<ArgumentException>(() => party.FullHeal(id, World.Fantasy));
	}

	[Fact]
	public void StatsOf_AreTheStatsAtTheLevelTheCharacterHasInThatWorld()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 30); // level 3

		Assert.Equal(new BaseStats(30, 6, 0, 14, 0, 0, 0), party.StatsOf(Hero, World.Fantasy));
		Assert.Equal(new BaseStats(30, 4, 0, 6, 0, 0, 0), party.StatsOf(Hero, World.Nightmare));
		Assert.Equal(new BaseStats(51, 15, 0, 0, 0, 0, 0), party.StatsOf(Hero, World.Real));
	}

	// --- Health points and mana points ---

	[Fact]
	public void SetHealthPoints_ChangesThatCharacterInThatWorldOnly()
	{
		PartyState party = TestRoster.Party("abe");

		party.SetHealthPoints(Hero, World.Fantasy, 7);

		Assert.Equal(Progress(1, 0, 7, 10), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(Progress(1, 0, 30, 6), party.ProgressOf(Hero, World.Nightmare));
		Assert.Equal(Progress(12, 0, 51, 0), party.ProgressOf(Hero, World.Real));
		Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf("abe", World.Fantasy));
	}

	[Theory]
	[InlineData(-5, 0)]
	[InlineData(0, 0)]
	[InlineData(20, 20)]
	[InlineData(21, 20)]
	[InlineData(int.MaxValue, 20)]
	[InlineData(int.MinValue, 0)]
	public void SetHealthPoints_StaysBetweenZeroAndWhatTheLevelGives(int value, int expected)
	{
		PartyState party = TestRoster.Party();
		party.SetHealthPoints(Hero, World.Fantasy, 10);

		party.SetHealthPoints(Hero, World.Fantasy, value);

		Assert.Equal(expected, party.ProgressOf(Hero, World.Fantasy).HealthPoints);
	}

	[Fact]
	public void SetManaPoints_ChangesThatCharacterInThatWorldOnly()
	{
		PartyState party = TestRoster.Party("abe");

		party.SetManaPoints("abe", World.Nightmare, 2);

		Assert.Equal(Progress(1, 0, 30, 2), party.ProgressOf("abe", World.Nightmare));
		Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(Progress(1, 0, 30, 6), party.ProgressOf(Hero, World.Nightmare));
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(10, 10)]
	[InlineData(11, 10)]
	public void SetManaPoints_StaysBetweenZeroAndWhatTheLevelGives(int value, int expected)
	{
		PartyState party = TestRoster.Party();
		party.SetManaPoints(Hero, World.Fantasy, 5);

		party.SetManaPoints(Hero, World.Fantasy, value);

		Assert.Equal(expected, party.ProgressOf(Hero, World.Fantasy).ManaPoints);
	}

	[Fact]
	public void FullHeal_GivesBothBack_InThatWorldOnly()
	{
		PartyState party = TestRoster.Party();
		party.SetHealthPoints(Hero, World.Fantasy, 3);
		party.SetManaPoints(Hero, World.Fantasy, 4);
		party.SetHealthPoints(Hero, World.Nightmare, 5);

		party.FullHeal(Hero, World.Fantasy);

		Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(Progress(1, 0, 5, 6), party.ProgressOf(Hero, World.Nightmare));
	}

	[Fact]
	public void ProgressChanged_IsRaisedOnARealChangeOnly()
	{
		PartyState party = TestRoster.Party("abe");
		List<(string, World)> changes = Changes(party);

		party.SetHealthPoints(Hero, World.Fantasy, 7);
		party.SetHealthPoints(Hero, World.Fantasy, 7);
		party.SetManaPoints("abe", World.Nightmare, 99); // already full: clamped to what it was
		party.FullHeal("abe", World.Real);
		party.SetManaPoints("abe", World.Nightmare, 1);
		party.FullHeal(Hero, World.Fantasy);

		Assert.Equal(new[] { (Hero, World.Fantasy), ("abe", World.Nightmare), (Hero, World.Fantasy) }, changes);
	}

	// --- Experience ---

	[Fact]
	public void Award_GivesEveryMemberTheWholeAward()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("abe");
		party.Recruit("bea");

		party.Award(World.Fantasy, 7);

		foreach (string id in new[] { Hero, "abe", "bea" })
			Assert.Equal(new LevelProgress(1, 7), party.ProgressOf(id, World.Fantasy).Level);
	}

	[Fact]
	public void Award_GivesNothingToWhoeverIsNotInTheParty()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("abe");

		party.Award(World.Fantasy, 7);

		Assert.Equal(new LevelProgress(1, 0), party.ProgressOf("bea", World.Fantasy).Level);
	}

	[Fact]
	public void Award_BelongsToOneWorld()
	{
		PartyState party = TestRoster.Party();

		party.Award(World.Nightmare, 150);

		Assert.Equal(new LevelProgress(2, 50), party.ProgressOf(Hero, World.Nightmare).Level);
		Assert.Equal(new LevelProgress(1, 0), party.ProgressOf(Hero, World.Fantasy).Level);
		Assert.Equal(new LevelProgress(12, 0), party.ProgressOf(Hero, World.Real).Level);
	}

	[Fact]
	public void Award_ReachingALevel_RaisesTheMaximumAndLeavesWhatIsLeftAlone()
	{
		PartyState party = TestRoster.Party();
		party.SetHealthPoints(Hero, World.Fantasy, 8);
		party.SetManaPoints(Hero, World.Fantasy, 3);

		party.Award(World.Fantasy, 10);

		Assert.Equal(Progress(2, 0, 8, 3), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(25, party.StatsOf(Hero, World.Fantasy).HealthPoints);

		// The room the level made can now be used.
		party.SetHealthPoints(Hero, World.Fantasy, 99);
		party.SetManaPoints(Hero, World.Fantasy, 99);
		Assert.Equal(Progress(2, 0, 25, 12), party.ProgressOf(Hero, World.Fantasy));
	}

	[Fact]
	public void Award_CanReachSeveralLevelsAtOnce_AndKeepsTheRest()
	{
		PartyState party = TestRoster.Party();

		// 10 + 20 + 30 pay for levels 2, 3 and 4; 5 are left towards the next.
		party.Award(World.Fantasy, 65);

		Assert.Equal(new LevelProgress(4, 5), party.ProgressOf(Hero, World.Fantasy).Level);
	}

	[Fact]
	public void Award_StopsAtTheWorldsHighestLevel()
	{
		PartyState party = TestRoster.Party();

		party.Award(World.Fantasy, 1_000_000);
		party.Award(World.Fantasy, 1_000_000);

		Assert.Equal(new LevelProgress(5, 0), party.ProgressOf(Hero, World.Fantasy).Level);
		Assert.Equal(40, party.StatsOf(Hero, World.Fantasy).HealthPoints);
	}

	[Fact]
	public void Award_InTheRealWorld_ChangesNobody_AndSaysItGaveNothing()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		List<(string, World)> changes = Changes(party);

		ExperienceAward award = party.Award(World.Real, 500);

		Assert.Equal(0, award.Experience);
		Assert.Equal(World.Real, award.World);
		Assert.All(award.Gains, gain => Assert.Equal(gain.Before, gain.After));
		Assert.Equal(Progress(12, 0, 51, 0), party.ProgressOf(Hero, World.Real));
		Assert.Empty(changes);
	}

	[Fact]
	public void Award_SaysWhatItDidToEachMember_InThePartysOrder()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("bea");
		party.Award(World.Fantasy, 25); // hero and bea: level 2, with 15
		party.Recruit("abe");

		ExperienceAward award = party.Award(World.Fantasy, 8);

		Assert.Equal(World.Fantasy, award.World);
		Assert.Equal(8, award.Experience);
		Assert.Equal(
			new[]
			{
				new MemberGain(Hero, new LevelProgress(2, 15), new LevelProgress(3, 3)),
				new MemberGain("bea", new LevelProgress(2, 15), new LevelProgress(3, 3)),
				new MemberGain("abe", new LevelProgress(1, 0), new LevelProgress(1, 8))
			},
			award.Gains);
		Assert.Equal(new[] { true, true, false }, award.Gains.Select(gain => gain.ReachesALevel));
	}

	[Fact]
	public void PreviewAward_SaysWhatAwardWouldDo_AndChangesNothing()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		party.Award(World.Fantasy, 4);
		List<(string, World)> changes = Changes(party);

		ExperienceAward preview = party.PreviewAward(World.Fantasy, 30);

		Assert.Equal(new LevelProgress(1, 4), party.ProgressOf(Hero, World.Fantasy).Level);
		Assert.Equal(new LevelProgress(1, 4), party.ProgressOf("abe", World.Fantasy).Level);
		Assert.Empty(changes);

		ExperienceAward award = party.Award(World.Fantasy, 30);

		Assert.Equal(preview.World, award.World);
		Assert.Equal(preview.Experience, award.Experience);
		Assert.Equal(preview.Gains, award.Gains);
		Assert.Equal(new LevelProgress(3, 4), award.Gains[0].After);
		Assert.Equal(new LevelProgress(3, 4), party.ProgressOf("abe", World.Fantasy).Level);
	}

	[Fact]
	public void Award_ANegativeAmount_IsRefused_AndChangesNothing()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 4);
		List<(string, World)> changes = Changes(party);

		Assert.Throws<ArgumentOutOfRangeException>(() => party.Award(World.Fantasy, -1));
		Assert.Throws<ArgumentOutOfRangeException>(() => party.PreviewAward(World.Fantasy, -1));
		Assert.Throws<ArgumentOutOfRangeException>(() => party.Award(World.Real, -1));

		Assert.Equal(new LevelProgress(1, 4), party.ProgressOf(Hero, World.Fantasy).Level);
		Assert.Empty(changes);
	}

	[Fact]
	public void Award_RaisesProgressChangedOncePerMemberItChanged_AfterAllOfThemHaveTheirValue()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("abe");
		var seen = new List<(string Id, long First, long Second)>();
		party.ProgressChanged += (id, _) => seen.Add((
			id,
			party.ProgressOf(Hero, World.Fantasy).Level.Experience,
			party.ProgressOf("abe", World.Fantasy).Level.Experience));

		party.Award(World.Fantasy, 6);

		Assert.Equal(new[] { (Hero, 6L, 6L), ("abe", 6L, 6L) }, seen);
	}

	[Fact]
	public void Award_OfNothing_OrAtTheHighestLevel_RaisesNothing()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Nightmare, 300); // level 3, the highest
		List<(string, World)> changes = Changes(party);

		party.Award(World.Fantasy, 0);
		party.Award(World.Nightmare, 50);

		Assert.Empty(changes);
		Assert.Equal(new LevelProgress(3, 0), party.ProgressOf(Hero, World.Nightmare).Level);
	}

	// --- Leaving and coming back ---

	[Fact]
	public void Dismiss_KeepsTheCharactersProgress_AndTheyHaveItBackOnRejoining()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		party.Award(World.Fantasy, 14);
		party.SetHealthPoints("abe", World.Fantasy, 9);
		party.SetManaPoints("abe", World.Nightmare, 1);

		party.Dismiss("abe");

		Assert.Equal(Progress(2, 4, 9, 10), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(Progress(1, 0, 30, 1), party.ProgressOf("abe", World.Nightmare));

		// Away, they miss what the party earns.
		party.Award(World.Fantasy, 100);
		party.Recruit("abe");

		Assert.Equal(Progress(2, 4, 9, 10), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(new LevelProgress(5, 0), party.ProgressOf(Hero, World.Fantasy).Level);
	}

	[Fact]
	public void RecruitAndDismiss_DoNotTouchAnyonesProgress()
	{
		PartyState party = TestRoster.Party("abe");
		List<(string, World)> changes = Changes(party);

		party.Recruit("abe");
		party.Dismiss("abe");

		Assert.Empty(changes);
	}
}
