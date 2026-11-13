using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="SnapshotShelf"/>, the debug tooling's named snapshots of the
/// decision registry and the party: their names, their order, what restoring one brings
/// back, and that a snapshot one of the two refuses changes neither.
/// </summary>
public class SnapshotShelfTests
{
	private static readonly FlagCatalogue Catalogue = FlagCatalogue.Read(@"{
		'format': 1,
		'flags': {
			'combat.wins': { 'type': 'counter', 'lifetime': 'run' },
			'combat.losses': { 'type': 'counter', 'lifetime': 'run' },
			'test.tonight': { 'type': 'bool', 'lifetime': 'cycle' },
			'test.visits': { 'type': 'counter', 'lifetime': 'run' },
			'test.ever': { 'type': 'bool', 'lifetime': 'persistent' }
		}
	}".Replace('\'', '"'));

	private static readonly CyclePosition Start = new(World.Real, 1);

	/// <summary>A new party of the hero alone, over a roster with abe and bea.</summary>
	private static PartyState Party() => TestRoster.Party("abe", "bea");

	[Fact]
	public void Take_WithoutAName_UsesTheLowestFreeNumber()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();

		shelf.Take(state, Party(), Start);
		shelf.Take(state, Party(), Start);
		shelf.Take(state, Party(), Start);
		shelf.Remove("2");
		shelf.Take(state, Party(), Start, "  ");

		Assert.Equal(new[] { "1", "3", "2" }, shelf.Snapshots.Select(snapshot => snapshot.Name));
	}

	[Fact]
	public void Take_KeepsWhereItWasTaken_AndTrimsTheName()
	{
		var shelf = new SnapshotShelf();

		ShelvedSnapshot shelved = shelf.Take(new DecisionState(Catalogue), Party(), new CyclePosition(World.Fantasy, 2), "  before the bed ");

		Assert.Equal("before the bed", shelved.Name);
		Assert.Equal(new CyclePosition(World.Fantasy, 2), shelved.TakenAt);
		Assert.Same(shelved, shelf.Snapshots.Single());
	}

	[Fact]
	public void Take_WithANameAlreadyOnTheShelf_ReplacesThatOne_AndPutsItLast()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		shelf.Take(state, Party(), Start, "a");
		shelf.Take(state, Party(), Start, "b");
		state.AddToCounter("test.visits", 5);

		shelf.Take(state, Party(), new CyclePosition(World.Nightmare, 1), "a");

		Assert.Equal(new[] { "b", "a" }, shelf.Snapshots.Select(snapshot => snapshot.Name));
		Assert.Equal(5, shelf.Snapshots[1].Decisions.Counters["test.visits"]);
		Assert.Equal(World.Nightmare, shelf.Snapshots[1].TakenAt.World);
	}

	[Fact]
	public void Take_RejectsAPositionTheGameCannotBeIn()
	{
		var shelf = new SnapshotShelf();

		Assert.Throws<ArgumentOutOfRangeException>(() => shelf.Take(new DecisionState(Catalogue), Party(), new CyclePosition(World.Real, 0)));
		Assert.Empty(shelf.Snapshots);
	}

	[Fact]
	public void Restore_BringsBackTheFlagsAndTheHistory_AndLeavesThePersistentFlagsAlone()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		state.SetFlag("test.tonight", true);
		state.AddToCounter("test.visits", 2);
		shelf.Take(state, Party(), Start, "saved");

		state.SetFlag("test.tonight", false);
		state.AddToCounter("test.visits", 4);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		state.SetFlag("test.ever", true);

		shelf.Restore(state, Party(), "saved");

		Assert.True(state.GetFlag("test.tonight"));
		Assert.Equal(2, state.GetCounter("test.visits"));
		Assert.Equal(0, state.GetCounter(DecisionState.Wins));
		Assert.Empty(state.Combats);
		Assert.True(state.GetFlag("test.ever"));
	}

	[Fact]
	public void Take_HoldsThePartyToo()
	{
		PartyState party = Party();
		party.Recruit("bea");
		party.Award(World.Fantasy, 15);
		var shelf = new SnapshotShelf();

		ShelvedSnapshot shelved = shelf.Take(new DecisionState(Catalogue), party, Start);
		party.Dismiss("bea");

		Assert.Equal(new[] { TestRoster.Hero, "bea" }, shelved.Party.Members);
		Assert.Contains(new ProgressRecord("bea", World.Fantasy, new CharacterProgress(new LevelProgress(2, 5), 20, 10)), shelved.Party.Progress);
	}

	[Fact]
	public void Restore_BringsBackTheParty_WithTheFlags()
	{
		var state = new DecisionState(Catalogue);
		PartyState party = Party();
		party.Recruit("abe");
		party.SetHealthPoints(TestRoster.Hero, World.Fantasy, 3);
		state.AddToCounter("test.visits", 2);
		var shelf = new SnapshotShelf();
		shelf.Take(state, party, Start, "saved");

		party.Dismiss("abe");
		party.Recruit("bea");
		party.Award(World.Fantasy, 100);
		party.FullHeal(TestRoster.Hero, World.Fantasy);
		state.AddToCounter("test.visits", 4);
		shelf.Restore(state, party, "saved");

		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 3, 10), party.ProgressOf(TestRoster.Hero, World.Fantasy));
		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 20, 10), party.ProgressOf("bea", World.Fantasy));
		Assert.Equal(2, state.GetCounter("test.visits"));
	}

	[Fact]
	public void Restore_ASnapshotThePartyRefuses_ChangesNeitherThePartyNorTheFlags()
	{
		// Taken with abe in the party, and restored over a roster that has no abe.
		var state = new DecisionState(Catalogue);
		PartyState withAbe = Party();
		withAbe.Recruit("abe");
		var shelf = new SnapshotShelf();
		shelf.Take(state, withAbe, Start, "saved");

		state.SetFlag("test.tonight", true);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		PartyState withoutAbe = TestRoster.Party("bea");
		withoutAbe.Recruit("bea");

		ArgumentException error = Assert.Throws<ArgumentException>(() => shelf.Restore(state, withoutAbe, "saved"));

		Assert.Contains("'abe'", error.Message);
		Assert.True(state.GetFlag("test.tonight"));
		Assert.Single(state.Combats);
		Assert.Equal(new[] { TestRoster.Hero, "bea" }, withoutAbe.Members);
	}

	[Fact]
	public void Restore_ASnapshotTheRegistryRefuses_ChangesNeitherTheFlagsNorTheParty()
	{
		// Taken with a flag that the catalogue it is restored over does not declare.
		PartyState party = Party();
		var shelf = new SnapshotShelf();
		shelf.Take(new DecisionState(Catalogue), party, Start, "saved");

		var smaller = new DecisionState(FlagCatalogue.Read(@"{
			'format': 1,
			'flags': {
				'combat.wins': { 'type': 'counter', 'lifetime': 'run' },
				'combat.losses': { 'type': 'counter', 'lifetime': 'run' },
				'test.visits': { 'type': 'counter', 'lifetime': 'run' }
			}
		}".Replace('\'', '"')));
		smaller.AddToCounter("test.visits", 3);
		party.Recruit("abe");
		party.SetHealthPoints(TestRoster.Hero, World.Fantasy, 3);

		ArgumentException error = Assert.Throws<ArgumentException>(() => shelf.Restore(smaller, party, "saved"));

		Assert.Contains("'test.tonight'", error.Message);
		Assert.Equal(3, smaller.GetCounter("test.visits"));
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.Equal(3, party.ProgressOf(TestRoster.Hero, World.Fantasy).HealthPoints);
	}

	[Fact]
	public void Restore_KeepsTheSnapshotOnTheShelf_SoItCanBeRestoredAgain()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		shelf.Take(state, Party(), Start, "clean");

		state.SetFlag("test.tonight", true);
		shelf.Restore(state, Party(), "clean");
		state.SetFlag("test.tonight", true);
		shelf.Restore(state, Party(), "clean");

		Assert.False(state.GetFlag("test.tonight"));
		Assert.Single(shelf.Snapshots);
	}

	[Fact]
	public void Restore_RejectsANameThatIsNotOnTheShelf()
	{
		var shelf = new SnapshotShelf();
		shelf.Take(new DecisionState(Catalogue), Party(), Start, "a");

		ArgumentException error = Assert.Throws<ArgumentException>(() => shelf.Restore(new DecisionState(Catalogue), Party(), "b"));
		Assert.Contains("'b'", error.Message);
	}

	[Fact]
	public void Remove_SaysWhetherThereWasOne()
	{
		var shelf = new SnapshotShelf();
		shelf.Take(new DecisionState(Catalogue), Party(), Start, "a");

		Assert.False(shelf.Remove("b"));
		Assert.True(shelf.Remove("a"));
		Assert.Empty(shelf.Snapshots);
	}

	[Fact]
	public void Snapshots_CannotBeChangedFromOutside()
	{
		var shelf = new SnapshotShelf();

		Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ShelvedSnapshot>)shelf.Snapshots).Clear());
	}
}
