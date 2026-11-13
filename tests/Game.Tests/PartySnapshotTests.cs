using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="PartyState.TakeSnapshot"/>, <see cref="PartyState.CheckSnapshot"/>
/// and <see cref="PartyState.Restore"/>: what a save slot keeps of the party, and that a
/// snapshot the party could never hold is refused whole.
/// </summary>
/// <remarks>
/// The numbers are <see cref="TestRoster"/>'s. In the Fantasy world a character has 20
/// health points and 10 mana points at level 1, and 5 and 2 more per level; a level costs
/// 10 times its number, up to level 5. In the Real world: level 12, with 51 health points.
/// </remarks>
public class PartySnapshotTests
{
	private const string Hero = TestRoster.Hero;

	private static CharacterProgress Progress(int level, long experience, int healthPoints, int manaPoints) =>
		new(new LevelProgress(level, experience), healthPoints, manaPoints);

	/// <summary>The snapshot with one character's progress in one world replaced.</summary>
	private static PartySnapshot With(PartySnapshot snapshot, string id, World world, CharacterProgress progress) =>
		snapshot with
		{
			Progress = snapshot.Progress
				.Select(record => record.Id == id && record.World == world ? record with { Progress = progress } : record)
				.ToArray()
		};

	private static PartySnapshot WithMembers(PartySnapshot snapshot, params string[] members) =>
		snapshot with { Members = members };

	/// <summary>A party of the hero and abe, over a roster of five, with something changed in it.</summary>
	private static PartyState Played()
	{
		PartyState party = TestRoster.Party("abe", "bea", "cal", "dee");
		party.Recruit("abe");
		party.Award(World.Fantasy, 15);
		party.SetHealthPoints(Hero, World.Fantasy, 7);
		return party;
	}

	// --- Taking one ---

	[Fact]
	public void TakeSnapshot_HoldsTheMembersInOrder_AndEveryonesProgressInEveryWorld()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("bea");
		party.Award(World.Fantasy, 15);
		party.SetHealthPoints(Hero, World.Fantasy, 7);
		party.SetManaPoints("abe", World.Nightmare, 2);

		PartySnapshot snapshot = party.TakeSnapshot();

		Assert.Equal(new[] { Hero, "bea" }, snapshot.Members);
		Assert.Equal(
			new[]
			{
				new ProgressRecord(Hero, World.Real, Progress(12, 0, 51, 0)),
				new ProgressRecord(Hero, World.Fantasy, Progress(2, 5, 7, 10)),
				new ProgressRecord(Hero, World.Nightmare, Progress(1, 0, 30, 6)),
				new ProgressRecord("abe", World.Real, Progress(12, 0, 51, 0)),
				new ProgressRecord("abe", World.Fantasy, Progress(1, 0, 20, 10)),
				new ProgressRecord("abe", World.Nightmare, Progress(1, 0, 30, 2)),
				new ProgressRecord("bea", World.Real, Progress(12, 0, 51, 0)),
				new ProgressRecord("bea", World.Fantasy, Progress(2, 5, 20, 10)),
				new ProgressRecord("bea", World.Nightmare, Progress(1, 0, 30, 6))
			},
			snapshot.Progress);
	}

	[Fact]
	public void TakeSnapshot_IsACopy()
	{
		PartyState party = TestRoster.Party("abe");
		PartySnapshot snapshot = party.TakeSnapshot();

		party.Recruit("abe");
		party.Award(World.Fantasy, 15);
		party.SetHealthPoints(Hero, World.Nightmare, 1);

		Assert.Equal(new[] { Hero }, snapshot.Members);
		Assert.Equal(TestRoster.Party("abe").TakeSnapshot().Progress, snapshot.Progress);
	}

	// --- Restoring one ---

	[Fact]
	public void Restore_BringsBackTheMembersAndTheProgress()
	{
		PartyState party = Played();
		PartySnapshot saved = party.TakeSnapshot();

		party.Dismiss("abe");
		party.Recruit("cal");
		party.Recruit("bea");
		party.Award(World.Fantasy, 100);
		party.Award(World.Nightmare, 100);
		party.FullHeal(Hero, World.Fantasy);
		party.Restore(saved);

		Assert.Equal(new[] { Hero, "abe" }, party.Members);
		Assert.Equal(saved.Progress, party.TakeSnapshot().Progress);
		Assert.Equal(Progress(2, 5, 7, 10), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(Progress(2, 5, 20, 10), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf("cal", World.Fantasy));
		Assert.Equal(Progress(1, 0, 30, 6), party.ProgressOf(Hero, World.Nightmare));
	}

	[Fact]
	public void Restore_IntoAnotherPartyOverTheSameRoster_MakesItTheSame()
	{
		PartySnapshot saved = Played().TakeSnapshot();
		PartyState other = TestRoster.Party("abe", "bea", "cal", "dee");

		other.Restore(saved);

		Assert.Equal(saved.Members, other.TakeSnapshot().Members);
		Assert.Equal(saved.Progress, other.TakeSnapshot().Progress);
	}

	[Fact]
	public void Restore_ACharacterAndWorldTheSnapshotDoesNotMention_GoBackToWhereTheyStart()
	{
		PartyState party = Played();
		var onlyTheHeroInFantasy = new PartySnapshot(
			new[] { Hero },
			new[] { new ProgressRecord(Hero, World.Fantasy, Progress(3, 29, 0, 0)) });

		party.Restore(onlyTheHeroInFantasy);

		Assert.Equal(new[] { Hero }, party.Members);
		Assert.Equal(Progress(3, 29, 0, 0), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(Progress(1, 0, 20, 10), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(Progress(12, 0, 51, 0), party.ProgressOf(Hero, World.Real));
	}

	[Fact]
	public void Restore_RaisesTheEventsOnlyForWhatChanged_OnceEverythingHasItsValue()
	{
		PartyState party = Played();
		PartySnapshot saved = party.TakeSnapshot();
		party.Recruit("bea");
		party.SetHealthPoints(Hero, World.Fantasy, 20);
		party.SetManaPoints("cal", World.Nightmare, 0);

		int membersChanged = 0;
		var changes = new List<(string, World)>();

		void EverythingIsBack()
		{
			Assert.Equal(new[] { Hero, "abe" }, party.Members);
			Assert.Equal(7, party.ProgressOf(Hero, World.Fantasy).HealthPoints);
			Assert.Equal(6, party.ProgressOf("cal", World.Nightmare).ManaPoints);
		}

		party.MembersChanged += () =>
		{
			EverythingIsBack();
			membersChanged++;
		};
		party.ProgressChanged += (id, world) =>
		{
			EverythingIsBack();
			changes.Add((id, world));
		};
		party.Restore(saved);

		Assert.Equal(1, membersChanged);
		Assert.Equal(new[] { (Hero, World.Fantasy), ("cal", World.Nightmare) }, changes);
	}

	[Fact]
	public void Restore_WhatThePartyAlreadyHolds_RaisesNothing()
	{
		PartyState party = Played();
		int events = 0;
		party.MembersChanged += () => events++;
		party.ProgressChanged += (_, _) => events++;

		party.Restore(party.TakeSnapshot());

		Assert.Equal(0, events);
	}

	[Fact]
	public void Restore_TheSameMembersInAnotherOrder_IsAChangeOfMembers()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("abe");
		party.Recruit("bea");
		int membersChanged = 0;
		party.MembersChanged += () => membersChanged++;

		party.Restore(WithMembers(party.TakeSnapshot(), Hero, "bea", "abe"));

		Assert.Equal(new[] { Hero, "bea", "abe" }, party.Members);
		Assert.Equal(1, membersChanged);
	}

	// --- What a snapshot may hold ---

	public static TheoryData<string, World, int, long, int, int> ProgressAtTheEdges => new()
	{
		// The last experience before level 3, which costs 20.
		{ Hero, World.Fantasy, 2, 19, 25, 12 },
		// The highest level, where no experience is kept; 40 and 18 are all it gives.
		{ Hero, World.Fantasy, 5, 0, 40, 18 },
		// Down, and out of mana points.
		{ "abe", World.Nightmare, 3, 0, 0, 0 },
		{ "abe", World.Real, 12, 0, 1, 0 }
	};

	[Theory]
	[MemberData(nameof(ProgressAtTheEdges))]
	public void Restore_ProgressAtTheEdgeOfWhatTheRulesAllow_IsAccepted(string id, World world, int level, long experience, int healthPoints, int manaPoints)
	{
		PartyState party = Played();
		CharacterProgress progress = Progress(level, experience, healthPoints, manaPoints);

		party.Restore(With(party.TakeSnapshot(), id, world, progress));

		Assert.Equal(progress, party.ProgressOf(id, world));
	}

	[Fact]
	public void Restore_APartyOfFour_IsAccepted()
	{
		PartyState party = Played();

		party.Restore(WithMembers(party.TakeSnapshot(), Hero, "dee", "cal", "bea"));

		Assert.Equal(new[] { Hero, "dee", "cal", "bea" }, party.Members);
		Assert.True(party.IsFull);
	}

	public static TheoryData<string[], string> MembersThePartyCouldNotHold => new()
	{
		{ Array.Empty<string>(), "does not start with the protagonist, 'hero'" },
		{ new[] { "abe", Hero }, "does not start with the protagonist, 'hero'" },
		{ new[] { Hero, "abe", "bea", "cal", "dee" }, "holds 5, and a party holds 4 at most" },
		{ new[] { Hero, "zed" }, "holds 'zed', who is not in the roster" },
		{ new[] { Hero, null }, "holds '', who is not in the roster" },
		{ new[] { Hero, "abe", "abe" }, "holds 'abe' twice" },
		{ new[] { Hero, Hero }, "holds 'hero' twice" }
	};

	[Theory]
	[MemberData(nameof(MembersThePartyCouldNotHold))]
	public void Restore_MembersThatBreakARule_AreRefused_AndNothingChanges(string[] members, string expected)
	{
		PartyState party = Played();

		AssertRefused(party, WithMembers(party.TakeSnapshot(), members), expected);
	}

	public static TheoryData<string, World, int, long, int, int, string> ProgressThePartyCouldNotHold => new()
	{
		{ Hero, World.Fantasy, 6, 0, 20, 10, "'hero' in the Fantasy world at level 6, outside 1 to 5" },
		{ Hero, World.Nightmare, 4, 0, 30, 6, "'hero' in the Nightmare world at level 4, outside 1 to 3" },
		{ "abe", World.Real, 13, 0, 51, 0, "'abe' in the Real world at level 13, outside 12 to 12" },
		{ "abe", World.Real, 11, 0, 50, 0, "'abe' in the Real world at level 11, outside 12 to 12" },
		{ Hero, World.Fantasy, 2, 20, 25, 12, "20 experience at level 2, where it stays below 20" },
		{ Hero, World.Fantasy, 5, 1, 40, 18, "1 experience at level 5, where it stays below 1" },
		{ Hero, World.Real, 12, 1, 51, 0, "1 experience at level 12, where it stays below 1" },
		{ Hero, World.Fantasy, 2, 0, 26, 12, "26 health points, outside 0 to 25" },
		{ Hero, World.Fantasy, 2, 0, -1, 12, "-1 health points, outside 0 to 25" },
		{ Hero, World.Fantasy, 2, 0, 25, 13, "13 mana points, outside 0 to 12" },
		{ Hero, World.Fantasy, 2, 0, 25, -1, "-1 mana points, outside 0 to 12" },
		{ Hero, World.Real, 12, 0, 51, 1, "1 mana points, outside 0 to 0" }
	};

	[Theory]
	[MemberData(nameof(ProgressThePartyCouldNotHold))]
	public void Restore_ProgressTheRulesDoNotAllow_IsRefused_AndNothingChanges(string id, World world, int level, long experience, int healthPoints, int manaPoints, string expected)
	{
		PartyState party = Played();

		AssertRefused(party, With(party.TakeSnapshot(), id, world, Progress(level, experience, healthPoints, manaPoints)), expected);
	}

	[Fact]
	public void Restore_ALevelNeverGiven_IsRefused()
	{
		// default skips LevelProgress's constructor, and leaves a level of 0.
		PartyState party = Played();

		AssertRefused(party, With(party.TakeSnapshot(), Hero, World.Fantasy, default), "at level 0, outside 1 to 5");
	}

	[Fact]
	public void Restore_ProgressForSomeoneOutsideTheRoster_IsRefused()
	{
		PartyState party = Played();
		PartySnapshot snapshot = party.TakeSnapshot();
		snapshot = snapshot with { Progress = snapshot.Progress.Append(new ProgressRecord("zed", World.Fantasy, Progress(1, 0, 20, 10))).ToArray() };

		AssertRefused(party, snapshot, "progress for 'zed', who is not in the roster");
	}

	[Fact]
	public void Restore_ProgressInAWorldThatDoesNotExist_IsRefused()
	{
		PartyState party = Played();
		PartySnapshot snapshot = party.TakeSnapshot();
		snapshot = snapshot with { Progress = snapshot.Progress.Append(new ProgressRecord(Hero, (World)7, Progress(1, 0, 20, 10))).ToArray() };

		AssertRefused(party, snapshot, "progress for 'hero' in a world that does not exist");
	}

	[Fact]
	public void Restore_TheSameCharacterAndWorldTwice_IsRefused()
	{
		PartyState party = Played();
		PartySnapshot snapshot = party.TakeSnapshot();
		snapshot = snapshot with { Progress = snapshot.Progress.Append(new ProgressRecord("abe", World.Nightmare, Progress(1, 0, 30, 6))).ToArray() };

		AssertRefused(party, snapshot, "the progress of 'abe' in the Nightmare world twice");
	}

	[Fact]
	public void Restore_AnEmptyEntryOrAMissingList_IsRefused()
	{
		PartyState party = Played();
		PartySnapshot snapshot = party.TakeSnapshot();

		AssertRefused(party, snapshot with { Progress = snapshot.Progress.Append(null).ToArray() }, "an empty progress entry");
		AssertRefused(party, snapshot with { Members = null }, "no members or no progress");
		AssertRefused(party, snapshot with { Progress = null }, "no members or no progress");
		Assert.Throws<ArgumentNullException>(() => party.Restore(null));
	}

	[Fact]
	public void Restore_ABadEntryAfterGoodOnes_StillChangesNothing()
	{
		// The last entry of all is the bad one, so everything before it has been read.
		PartyState party = Played();
		PartySnapshot fresh = TestRoster.Party("abe", "bea", "cal", "dee").TakeSnapshot();

		AssertRefused(party, With(fresh, "dee", World.Nightmare, Progress(9, 0, 30, 6)), "'dee' in the Nightmare world at level 9");
	}

	// --- Checking without restoring ---

	[Fact]
	public void CheckSnapshot_AGoodSnapshot_IsAccepted_AndChangesNothing()
	{
		PartyState party = Played();
		PartySnapshot before = party.TakeSnapshot();
		int events = 0;
		party.MembersChanged += () => events++;
		party.ProgressChanged += (_, _) => events++;

		party.CheckSnapshot(TestRoster.Party("abe", "bea", "cal", "dee").TakeSnapshot());

		Assert.Equal(before.Members, party.Members);
		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
		Assert.Equal(0, events);
	}

	[Fact]
	public void CheckSnapshot_RefusesWhatRestoreRefuses()
	{
		PartyState party = Played();
		PartySnapshot snapshot = party.TakeSnapshot();

		Assert.Throws<ArgumentException>(() => party.CheckSnapshot(WithMembers(snapshot, "abe")));
		Assert.Throws<ArgumentException>(() => party.CheckSnapshot(With(snapshot, Hero, World.Fantasy, Progress(6, 0, 20, 10))));
		Assert.Throws<ArgumentNullException>(() => party.CheckSnapshot(null));
	}

	/// <summary>Restoring and checking both refuse the snapshot, in these words, and the party is as it was.</summary>
	private static void AssertRefused(PartyState party, PartySnapshot snapshot, string expected)
	{
		PartySnapshot before = party.TakeSnapshot();
		int events = 0;
		void Count() => events++;
		void CountProgress(string id, World world) => events++;
		party.MembersChanged += Count;
		party.ProgressChanged += CountProgress;

		var checking = Assert.Throws<ArgumentException>(() => party.CheckSnapshot(snapshot));
		var restoring = Assert.Throws<ArgumentException>(() => party.Restore(snapshot));

		party.MembersChanged -= Count;
		party.ProgressChanged -= CountProgress;

		Assert.Contains(expected, restoring.Message);
		Assert.Equal(restoring.Message, checking.Message);
		Assert.Equal(before.Members, party.Members);
		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
		Assert.Equal(0, events);
	}
}
