using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="PartyEdits"/>: the snapshot a forced level produces, and that
/// <see cref="PartyState.Restore"/> is what accepts or refuses it.
/// </summary>
/// <remarks>
/// The numbers are <see cref="TestRoster"/>'s: in the Fantasy world, 20 health points and
/// 5 more per level, 10 mana points and 2 more, and levels up to 5.
/// </remarks>
public class PartyEditsTests
{
	private const string Hero = TestRoster.Hero;

	private static void SetLevel(PartyState party, string id, World world, int level) =>
		party.Restore(PartyEdits.WithLevel(party.TakeSnapshot(), party.Roster, id, world, level));

	[Fact]
	public void WithLevel_PutsTheCharacterAtThatLevel_WithNoExperience()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 14);

		SetLevel(party, Hero, World.Fantasy, 4);

		Assert.Equal(new LevelProgress(4, 0), party.ProgressOf(Hero, World.Fantasy).Level);
	}

	[Fact]
	public void WithLevel_ARaisedLevel_DoesNotHeal()
	{
		PartyState party = TestRoster.Party();

		SetLevel(party, Hero, World.Fantasy, 3);

		// Level 3 gives 30 and 14; the character keeps the 20 and 10 they had.
		Assert.Equal(20, party.ProgressOf(Hero, World.Fantasy).HealthPoints);
		Assert.Equal(10, party.ProgressOf(Hero, World.Fantasy).ManaPoints);
		Assert.Equal(30, party.StatsOf(Hero, World.Fantasy).HealthPoints);
	}

	[Fact]
	public void WithLevel_ALoweredLevel_CutsThePointsDownToWhatItGives()
	{
		PartyState party = TestRoster.Party();
		SetLevel(party, Hero, World.Fantasy, 5);
		party.FullHeal(Hero, World.Fantasy);

		SetLevel(party, Hero, World.Fantasy, 2);

		Assert.Equal(25, party.ProgressOf(Hero, World.Fantasy).HealthPoints);
		Assert.Equal(12, party.ProgressOf(Hero, World.Fantasy).ManaPoints);
	}

	[Fact]
	public void WithLevel_ChangesNothingElse()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		PartySnapshot before = party.TakeSnapshot();

		PartySnapshot after = PartyEdits.WithLevel(before, party.Roster, "abe", World.Nightmare, 2);

		Assert.Equal(before.Members, after.Members);
		Assert.Equal(
			before.Progress.Where(record => !(record.Id == "abe" && record.World == World.Nightmare)),
			after.Progress.Where(record => !(record.Id == "abe" && record.World == World.Nightmare)));
		Assert.Equal(1, before.Progress.Single(record => record.Id == "abe" && record.World == World.Nightmare).Progress.Level.Level);
		Assert.Equal(2, after.Progress.Single(record => record.Id == "abe" && record.World == World.Nightmare).Progress.Level.Level);
	}

	[Fact]
	public void ALevelAboveTheHighest_IsRefusedByRestore_AndNothingChanges()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 14);
		PartySnapshot before = party.TakeSnapshot();

		ArgumentException refusal = Assert.Throws<ArgumentException>(() => SetLevel(party, Hero, World.Fantasy, 6));

		Assert.Contains("outside 1 to 5", refusal.Message);
		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
	}

	[Fact]
	public void InTheRealWorld_OnlyTheAgeIsALevel()
	{
		PartyState party = TestRoster.Party();

		SetLevel(party, Hero, World.Real, 12);
		Assert.Throws<ArgumentException>(() => SetLevel(party, Hero, World.Real, 13));

		Assert.Equal(12, party.ProgressOf(Hero, World.Real).Level.Level);
	}

	[Fact]
	public void WithLevel_ALevelBelowOne_Throws()
	{
		PartyState party = TestRoster.Party();

		Assert.Throws<ArgumentOutOfRangeException>(() => PartyEdits.WithLevel(party.TakeSnapshot(), party.Roster, Hero, World.Fantasy, 0));
	}

	[Fact]
	public void WithLevel_ACharacterTheSnapshotDoesNotHold_Throws()
	{
		PartyState party = TestRoster.Party();

		Assert.Throws<ArgumentException>(() => PartyEdits.WithLevel(party.TakeSnapshot(), party.Roster, "nobody", World.Fantasy, 2));
	}
}
