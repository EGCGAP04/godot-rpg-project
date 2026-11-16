using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatusSheet"/>: what the status screen has to show of a character
/// in each world, and how the experience towards the next level is worked out.
/// </summary>
/// <remarks>
/// The numbers are <see cref="TestRoster"/>'s: in the Fantasy world a level costs 10 times
/// its number and the highest is 5; in the Nightmare one, 100 times and 3.
/// </remarks>
public class StatusSheetTests
{
	private const string Hero = TestRoster.Hero;

	[Fact]
	public void For_AtTheStart_ShowsTheFirstLevelAndWhatTheNextOneCosts()
	{
		StatusSheet sheet = StatusSheet.For(TestRoster.Party(), Hero, World.Fantasy);

		Assert.Equal("speaker.hero", sheet.NameKey);
		Assert.Equal(World.Fantasy, sheet.World);
		Assert.Equal(1, sheet.Level);
		Assert.False(sheet.LevelIsAge);
		Assert.Equal(0, sheet.Experience);
		Assert.Equal(10, sheet.ToNextLevel);
	}

	[Fact]
	public void For_AfterALevel_CountsTheExperienceFromThatLevel_NotATotal()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 14);

		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		// 10 paid for level 2, so 4 are left towards level 3, which costs 20.
		Assert.Equal(2, sheet.Level);
		Assert.Equal(4, sheet.Experience);
		Assert.Equal(20, sheet.ToNextLevel);
	}

	[Fact]
	public void For_AtTheHighestLevel_HasNoNextLevel()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 1000);

		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		Assert.Equal(5, sheet.Level);
		Assert.False(sheet.LevelIsAge);
		Assert.Null(sheet.ToNextLevel);
	}

	[Fact]
	public void For_OneLevelBelowTheHighest_StillHasANextLevel()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 10 + 20 + 30);

		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		Assert.Equal(4, sheet.Level);
		Assert.Equal(40, sheet.ToNextLevel);
	}

	[Fact]
	public void For_TheRealWorld_ShowsTheAge_AndNoExperience()
	{
		StatusSheet sheet = StatusSheet.For(TestRoster.Party(), Hero, World.Real);

		Assert.Equal(12, sheet.Level);
		Assert.True(sheet.LevelIsAge);
		Assert.Equal(0, sheet.Experience);
		Assert.Null(sheet.ToNextLevel);
	}

	[Fact]
	public void For_EachWorld_HasItsOwnLevelAndCost()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 14);

		StatusSheet sheet = StatusSheet.For(party, Hero, World.Nightmare);

		Assert.Equal(1, sheet.Level);
		Assert.Equal(0, sheet.Experience);
		Assert.Equal(100, sheet.ToNextLevel);
	}

	[Fact]
	public void For_ShowsThePointsLeft_AndTheStatsOfTheLevel()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 10);
		party.SetHealthPoints(Hero, World.Fantasy, 7);
		party.SetManaPoints(Hero, World.Fantasy, 3);

		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		// Level 2: 20 + 5 health points, 10 + 2 mana points, 4 + 1 attack.
		Assert.Equal(7, sheet.HealthPoints);
		Assert.Equal(3, sheet.ManaPoints);
		Assert.Equal(25, sheet.Stats.HealthPoints);
		Assert.Equal(12, sheet.Stats.ManaPoints);
		Assert.Equal(5, sheet.Stats.AttackDamage);
	}

	[Fact]
	public void For_SomeoneOutsideTheParty_StillHasASheet()
	{
		StatusSheet sheet = StatusSheet.For(TestRoster.Party("abe"), "abe", World.Fantasy);

		Assert.Equal("speaker.abe", sheet.NameKey);
		Assert.Equal(1, sheet.Level);
	}

	[Fact]
	public void For_ACharacterTheRosterDoesNotHave_Throws()
	{
		Assert.Throws<ArgumentException>(() => StatusSheet.For(TestRoster.Party(), "nobody", World.Fantasy));
	}
}
