using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="SnapshotShelf"/>, the debug tooling's named snapshots of the
/// decision registry: their names, their order, and what restoring one brings back.
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

	[Fact]
	public void Take_WithoutAName_UsesTheLowestFreeNumber()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();

		shelf.Take(state, Start);
		shelf.Take(state, Start);
		shelf.Take(state, Start);
		shelf.Remove("2");
		shelf.Take(state, Start, "  ");

		Assert.Equal(new[] { "1", "3", "2" }, shelf.Snapshots.Select(snapshot => snapshot.Name));
	}

	[Fact]
	public void Take_KeepsWhereItWasTaken_AndTrimsTheName()
	{
		var shelf = new SnapshotShelf();

		ShelvedSnapshot shelved = shelf.Take(new DecisionState(Catalogue), new CyclePosition(World.Fantasy, 2), "  before the bed ");

		Assert.Equal("before the bed", shelved.Name);
		Assert.Equal(new CyclePosition(World.Fantasy, 2), shelved.TakenAt);
		Assert.Same(shelved, shelf.Snapshots.Single());
	}

	[Fact]
	public void Take_WithANameAlreadyOnTheShelf_ReplacesThatOne_AndPutsItLast()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		shelf.Take(state, Start, "a");
		shelf.Take(state, Start, "b");
		state.AddToCounter("test.visits", 5);

		shelf.Take(state, new CyclePosition(World.Nightmare, 1), "a");

		Assert.Equal(new[] { "b", "a" }, shelf.Snapshots.Select(snapshot => snapshot.Name));
		Assert.Equal(5, shelf.Snapshots[1].Snapshot.Counters["test.visits"]);
		Assert.Equal(World.Nightmare, shelf.Snapshots[1].TakenAt.World);
	}

	[Fact]
	public void Take_RejectsAPositionTheGameCannotBeIn()
	{
		var shelf = new SnapshotShelf();

		Assert.Throws<ArgumentOutOfRangeException>(() => shelf.Take(new DecisionState(Catalogue), new CyclePosition(World.Real, 0)));
		Assert.Empty(shelf.Snapshots);
	}

	[Fact]
	public void Restore_BringsBackTheFlagsAndTheHistory_AndLeavesThePersistentFlagsAlone()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		state.SetFlag("test.tonight", true);
		state.AddToCounter("test.visits", 2);
		shelf.Take(state, Start, "saved");

		state.SetFlag("test.tonight", false);
		state.AddToCounter("test.visits", 4);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		state.SetFlag("test.ever", true);

		shelf.Restore(state, "saved");

		Assert.True(state.GetFlag("test.tonight"));
		Assert.Equal(2, state.GetCounter("test.visits"));
		Assert.Equal(0, state.GetCounter(DecisionState.Wins));
		Assert.Empty(state.Combats);
		Assert.True(state.GetFlag("test.ever"));
	}

	[Fact]
	public void Restore_KeepsTheSnapshotOnTheShelf_SoItCanBeRestoredAgain()
	{
		var state = new DecisionState(Catalogue);
		var shelf = new SnapshotShelf();
		shelf.Take(state, Start, "clean");

		state.SetFlag("test.tonight", true);
		shelf.Restore(state, "clean");
		state.SetFlag("test.tonight", true);
		shelf.Restore(state, "clean");

		Assert.False(state.GetFlag("test.tonight"));
		Assert.Single(shelf.Snapshots);
	}

	[Fact]
	public void Restore_RejectsANameThatIsNotOnTheShelf()
	{
		var shelf = new SnapshotShelf();
		shelf.Take(new DecisionState(Catalogue), Start, "a");

		ArgumentException error = Assert.Throws<ArgumentException>(() => shelf.Restore(new DecisionState(Catalogue), "b"));
		Assert.Contains("'b'", error.Message);
	}

	[Fact]
	public void Remove_SaysWhetherThereWasOne()
	{
		var shelf = new SnapshotShelf();
		shelf.Take(new DecisionState(Catalogue), Start, "a");

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
