using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>A snapshot kept by the debug tooling, with its name and where it was taken.</summary>
/// <param name="Name">What the shelf calls it, unique on the shelf.</param>
/// <param name="TakenAt">The world and cycle the run was in when it was taken.</param>
/// <param name="Decisions">The cycle and run flags and the history, as <see cref="DecisionState.TakeSnapshot"/> gave them.</param>
/// <param name="Party">The members and everyone's progress, as <see cref="PartyState.TakeSnapshot"/> gave them.</param>
public sealed record ShelvedSnapshot(string Name, CyclePosition TakenAt, SlotSnapshot Decisions, PartySnapshot Party);

/// <summary>
/// The debug tooling's snapshots of the decision registry and the party (#75, #90):
/// several, each with a name, kept in memory and lost when the game closes. Each holds
/// what a save slot will. Deliberately a plain C# class with
/// no Godot dependency, so it is unit-tested without the engine.
/// </summary>
/// <remarks>
/// <para>
/// Restoring brings back the flags, the history and the party, never the world and the
/// cycle: jumping there is a separate step. A jump to another cycle resets the cycle
/// flags, so a restore that also jumped would undo part of itself depending on the order
/// of the two.
/// </para>
/// <para>
/// A restore is one step over two states. Both halves are checked before either is
/// applied, so a snapshot one of them refuses leaves both as they were.
/// </para>
/// </remarks>
public sealed class SnapshotShelf
{
	private readonly List<ShelvedSnapshot> _snapshots = new();

	public SnapshotShelf()
	{
		Snapshots = _snapshots.AsReadOnly();
	}

	/// <summary>Every snapshot on the shelf, oldest first.</summary>
	public IReadOnlyList<ShelvedSnapshot> Snapshots { get; }

	/// <summary>
	/// Takes a snapshot of the registry and the party and puts it on the shelf, last. Without
	/// a name it gets the lowest number no snapshot is using; with the name of one already
	/// on the shelf, it replaces that one.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="takenAt"/> is not a position the game can be in.</exception>
	public ShelvedSnapshot Take(DecisionState state, PartyState party, CyclePosition takenAt, string name = null)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(party);

		if (!CycleProgression.IsValid(takenAt))
			throw new ArgumentOutOfRangeException(nameof(takenAt), takenAt, "Not a position the game can be in.");

		name = string.IsNullOrWhiteSpace(name) ? FreeNumber() : name.Trim();

		var shelved = new ShelvedSnapshot(name, takenAt, state.TakeSnapshot(), party.TakeSnapshot());
		_snapshots.RemoveAll(snapshot => snapshot.Name == name);
		_snapshots.Add(shelved);
		return shelved;
	}

	/// <summary>
	/// Restores the snapshot called <paramref name="name"/> into the registry and the
	/// party. It stays on the shelf.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// No snapshot on the shelf has that name, or the registry or the party refuses its
	/// half of it. Nothing changes then, in either.
	/// </exception>
	public void Restore(DecisionState state, PartyState party, string name)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(party);

		ShelvedSnapshot shelved = _snapshots.FirstOrDefault(snapshot => snapshot.Name == name)
			?? throw new ArgumentException($"There is no snapshot called '{name}'", nameof(name));

		// Both checks before either restore: a restore cannot be undone.
		state.CheckSnapshot(shelved.Decisions);
		party.CheckSnapshot(shelved.Party);

		state.Restore(shelved.Decisions);
		party.Restore(shelved.Party);
	}

	/// <summary>Takes the snapshot called <paramref name="name"/> off the shelf, and says whether there was one.</summary>
	public bool Remove(string name) => _snapshots.RemoveAll(snapshot => snapshot.Name == name) > 0;

	private string FreeNumber()
	{
		int number = 1;

		while (_snapshots.Any(snapshot => snapshot.Name == number.ToString(CultureInfo.InvariantCulture)))
			number++;

		return number.ToString(CultureInfo.InvariantCulture);
	}
}
