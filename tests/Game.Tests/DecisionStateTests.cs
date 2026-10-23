using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DecisionState"/>: reading and writing flags, recording fights,
/// and what each lifetime does at the start of a cycle and in a slot's snapshot.
/// </summary>
public class DecisionStateTests
{
	/// <summary>
	/// One flag of each type and lifetime that matters here, plus the two counters the
	/// registry needs.
	/// </summary>
	private static readonly FlagCatalogue Catalogue = FlagCatalogue.Read(@"{
		'format': 1,
		'flags': {
			'combat.wins': { 'type': 'counter', 'lifetime': 'run' },
			'combat.losses': { 'type': 'counter', 'lifetime': 'run' },
			'test.tonight': { 'type': 'bool', 'lifetime': 'cycle' },
			'test.steps': { 'type': 'counter', 'lifetime': 'cycle' },
			'test.promised': { 'type': 'bool', 'lifetime': 'run' },
			'test.visits': { 'type': 'counter', 'lifetime': 'run' },
			'test.ever': { 'type': 'bool', 'lifetime': 'persistent' },
			'test.runs': { 'type': 'counter', 'lifetime': 'persistent' }
		}
	}".Replace('\'', '"'));

	/// <summary>A state whose <see cref="DecisionState.FlagChanged"/> names go into <paramref name="changed"/>.</summary>
	private static DecisionState Listened(out List<string> changed)
	{
		var state = new DecisionState(Catalogue);
		var names = new List<string>();
		state.FlagChanged += names.Add;
		changed = names;
		return state;
	}

	/// <summary>Sets every flag of <see cref="Catalogue"/> away from its starting value.</summary>
	private static void ChangeEverything(DecisionState state)
	{
		foreach (FlagDeclaration flag in Catalogue.Flags.Values)
		{
			if (flag.Type == FlagType.Bool)
				state.SetFlag(flag.Name, true);
			else
				state.AddToCounter(flag.Name, 3);
		}
	}

	[Fact]
	public void New_EveryFlagStartsAtItsStartingValue_AndThereAreNoFights()
	{
		var state = new DecisionState(Catalogue);

		Assert.Same(Catalogue, state.Catalogue);
		Assert.All(new[] { "test.tonight", "test.promised", "test.ever" }, name => Assert.False(state.GetFlag(name)));
		Assert.All(new[] { "combat.wins", "combat.losses", "test.steps", "test.visits", "test.runs" }, name => Assert.Equal(0, state.GetCounter(name)));
		Assert.Empty(state.Combats);
	}

	[Theory]
	[InlineData("combat.wins")]
	[InlineData("combat.losses")]
	public void New_RejectsACatalogueWithoutTheFightCounters(string missing)
	{
		var catalogue = FlagCatalogue.Read($"{{ 'format': 1, 'flags': {{ '{(missing == "combat.wins" ? "combat.losses" : "combat.wins")}': {{ 'type': 'counter', 'lifetime': 'run' }} }} }}".Replace('\'', '"'));

		var error = Assert.Throws<ArgumentException>(() => new DecisionState(catalogue));

		Assert.Contains($"'{missing}' must be declared in flags.json as a counter", error.Message);
	}

	[Fact]
	public void New_RejectsAFightCounterDeclaredAsABooleanFlag()
	{
		var catalogue = FlagCatalogue.Read("{ 'format': 1, 'flags': { 'combat.wins': { 'type': 'bool', 'lifetime': 'run' }, 'combat.losses': { 'type': 'counter', 'lifetime': 'run' } } }".Replace('\'', '"'));

		var error = Assert.Throws<ArgumentException>(() => new DecisionState(catalogue));

		Assert.Contains("'combat.wins' must be declared in flags.json as a counter", error.Message);
	}

	[Fact]
	public void New_WorksWithTheProjectCatalogue()
	{
		var state = new DecisionState(FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json")));

		Assert.Equal(0, state.GetCounter(DecisionState.Wins));
		Assert.Equal(0, state.GetCounter(DecisionState.Losses));
	}

	[Fact]
	public void SetFlag_SetsAndClears()
	{
		var state = new DecisionState(Catalogue);

		state.SetFlag("test.promised", true);
		Assert.True(state.GetFlag("test.promised"));

		state.SetFlag("test.promised", false);
		Assert.False(state.GetFlag("test.promised"));
	}

	[Theory]
	[InlineData(new[] { 3 }, 3)]
	[InlineData(new[] { 3, 2 }, 5)]
	[InlineData(new[] { 3, -2 }, 1)]
	[InlineData(new[] { 3, -3 }, 0)]
	public void AddToCounter_ChangesTheCounterByTheAmount(int[] amounts, int expected)
	{
		var state = new DecisionState(Catalogue);

		foreach (int amount in amounts)
			state.AddToCounter("test.visits", amount);

		Assert.Equal(expected, state.GetCounter("test.visits"));
	}

	[Theory]
	[InlineData(new[] { -1 })]
	[InlineData(new[] { 2, -5 })]
	[InlineData(new[] { 2, int.MinValue })]
	public void AddToCounter_NeverGoesBelowZero(int[] amounts)
	{
		var state = new DecisionState(Catalogue);

		foreach (int amount in amounts)
			state.AddToCounter("test.visits", amount);

		Assert.Equal(0, state.GetCounter("test.visits"));
	}

	[Fact]
	public void AddToCounter_StopsAtTheLargestIntegerInsteadOfOverflowing()
	{
		var state = new DecisionState(Catalogue);

		state.AddToCounter("test.visits", int.MaxValue);
		state.AddToCounter("test.visits", 1);

		Assert.Equal(int.MaxValue, state.GetCounter("test.visits"));
	}

	[Fact]
	public void EveryMethod_RejectsAnUndeclaredName()
	{
		var state = new DecisionState(Catalogue);
		Action[] calls =
		{
			() => state.GetFlag("test.tonite"),
			() => state.GetCounter("test.tonite"),
			() => state.SetFlag("test.tonite", true),
			() => state.AddToCounter("test.tonite", 1)
		};

		Assert.All(calls, call => Assert.Contains("'test.tonite' is not declared in flags.json", Assert.Throws<ArgumentException>(call).Message));
	}

	[Fact]
	public void EveryMethod_RejectsTheWrongType()
	{
		var state = new DecisionState(Catalogue);

		Assert.Contains("'test.visits' is a counter, not a boolean flag", Assert.Throws<ArgumentException>(() => state.GetFlag("test.visits")).Message);
		Assert.Contains("'test.visits' is a counter, not a boolean flag", Assert.Throws<ArgumentException>(() => state.SetFlag("test.visits", true)).Message);
		Assert.Contains("'test.promised' is a boolean flag, not a counter", Assert.Throws<ArgumentException>(() => state.GetCounter("test.promised")).Message);
		Assert.Contains("'test.promised' is a boolean flag, not a counter", Assert.Throws<ArgumentException>(() => state.AddToCounter("test.promised", 1)).Message);
	}

	[Fact]
	public void ARejectedWrite_ChangesNothing()
	{
		var state = Listened(out List<string> changed);

		Assert.Throws<ArgumentException>(() => state.SetFlag("test.visits", true));
		Assert.Throws<ArgumentException>(() => state.AddToCounter("test.promised", 1));

		Assert.Empty(changed);
		Assert.Equal(0, state.GetCounter("test.visits"));
		Assert.False(state.GetFlag("test.promised"));
	}

	[Fact]
	public void FlagChanged_IsRaisedForEveryRealChange()
	{
		var state = Listened(out List<string> changed);

		state.SetFlag("test.promised", true);
		state.AddToCounter("test.visits", 2);
		state.AddToCounter("test.visits", -1);
		state.SetFlag("test.promised", false);

		Assert.Equal(new[] { "test.promised", "test.visits", "test.visits", "test.promised" }, changed);
	}

	[Fact]
	public void FlagChanged_IsNotRaisedWhenTheValueStaysTheSame()
	{
		var state = Listened(out List<string> changed);

		state.SetFlag("test.promised", false);
		state.AddToCounter("test.visits", 0);
		state.AddToCounter("test.visits", -1);

		Assert.Empty(changed);
	}

	[Fact]
	public void RecordCombat_AddsTheFightToTheHistoryAndToItsTotal()
	{
		var state = new DecisionState(Catalogue);

		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		state.RecordCombat(World.Fantasy, 1, "strong", playerWon: false);
		state.RecordCombat(World.Nightmare, 2, "weak", playerWon: true);

		Assert.Equal(
			new[]
			{
				new CombatRecord(World.Fantasy, 1, "weak", true),
				new CombatRecord(World.Fantasy, 1, "strong", false),
				new CombatRecord(World.Nightmare, 2, "weak", true)
			},
			state.Combats);
		Assert.Equal(2, state.GetCounter(DecisionState.Wins));
		Assert.Equal(1, state.GetCounter(DecisionState.Losses));
	}

	[Fact]
	public void RecordCombat_RaisesFlagChangedOnceTheFightIsInTheHistory()
	{
		var state = new DecisionState(Catalogue);
		int fightsSeen = -1;
		state.FlagChanged += name => fightsSeen = state.Combats.Count;

		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);

		Assert.Equal(1, fightsSeen);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void RecordCombat_RejectsAFightWithoutAnEnemy(string enemyId)
	{
		var state = new DecisionState(Catalogue);

		Assert.Throws<ArgumentException>(() => state.RecordCombat(World.Fantasy, 1, enemyId, playerWon: true));
		Assert.Empty(state.Combats);
		Assert.Equal(0, state.GetCounter(DecisionState.Wins));
	}

	[Fact]
	public void Combats_CannotBeChangedFromOutside()
	{
		var state = new DecisionState(Catalogue);

		Assert.Throws<NotSupportedException>(() => ((IList<CombatRecord>)state.Combats).Add(new CombatRecord(World.Real, 1, "weak", true)));
	}

	[Fact]
	public void StartCycle_ResetsOnlyTheCycleFlags_AndKeepsTheHistory()
	{
		var state = new DecisionState(Catalogue);
		ChangeEverything(state);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);

		state.StartCycle();

		Assert.False(state.GetFlag("test.tonight"));
		Assert.Equal(0, state.GetCounter("test.steps"));
		Assert.True(state.GetFlag("test.promised"));
		Assert.Equal(3, state.GetCounter("test.visits"));
		Assert.True(state.GetFlag("test.ever"));
		Assert.Equal(3, state.GetCounter("test.runs"));
		Assert.Equal(4, state.GetCounter(DecisionState.Wins));
		Assert.Single(state.Combats);
	}

	[Fact]
	public void StartCycle_RaisesFlagChangedOnlyForTheFlagsItResets()
	{
		var state = Listened(out List<string> changed);
		state.SetFlag("test.tonight", true);
		state.SetFlag("test.promised", true);
		changed.Clear();

		state.StartCycle();

		Assert.Equal(new[] { "test.tonight" }, changed);
	}

	[Fact]
	public void TakeSnapshot_HoldsTheCycleAndRunFlagsAndTheHistory_ButNoPersistentFlag()
	{
		var state = new DecisionState(Catalogue);
		ChangeEverything(state);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: false);

		SlotSnapshot snapshot = state.TakeSnapshot();

		Assert.Equal(
			new Dictionary<string, bool> { ["test.tonight"] = true, ["test.promised"] = true },
			snapshot.Flags);
		Assert.Equal(
			new Dictionary<string, int> { ["combat.wins"] = 3, ["combat.losses"] = 4, ["test.steps"] = 3, ["test.visits"] = 3 },
			snapshot.Counters);
		Assert.Equal(new[] { new CombatRecord(World.Fantasy, 1, "weak", false) }, snapshot.Combats);
	}

	[Fact]
	public void TakeSnapshot_IsACopy()
	{
		var state = new DecisionState(Catalogue);
		SlotSnapshot snapshot = state.TakeSnapshot();

		ChangeEverything(state);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);

		Assert.All(snapshot.Flags.Values, Assert.False);
		Assert.All(snapshot.Counters.Values, value => Assert.Equal(0, value));
		Assert.Empty(snapshot.Combats);
	}

	[Fact]
	public void Restore_BringsBackTheSnapshot()
	{
		var state = new DecisionState(Catalogue);
		state.SetFlag("test.promised", true);
		state.AddToCounter("test.visits", 2);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		SlotSnapshot saved = state.TakeSnapshot();

		ChangeEverything(state);
		state.SetFlag("test.promised", false);
		state.RecordCombat(World.Fantasy, 1, "strong", playerWon: false);
		state.Restore(saved);

		Assert.Equal(saved.Flags, state.TakeSnapshot().Flags);
		Assert.Equal(saved.Counters, state.TakeSnapshot().Counters);
		Assert.Equal(new[] { new CombatRecord(World.Fantasy, 1, "weak", true) }, state.Combats);
		Assert.True(state.GetFlag("test.promised"));
		Assert.Equal(2, state.GetCounter("test.visits"));
		Assert.Equal(1, state.GetCounter(DecisionState.Wins));
	}

	[Fact]
	public void Restore_LeavesThePersistentFlagsAlone()
	{
		var state = new DecisionState(Catalogue);
		SlotSnapshot saved = state.TakeSnapshot();
		state.SetFlag("test.ever", true);
		state.AddToCounter("test.runs", 1);

		state.Restore(saved);

		Assert.True(state.GetFlag("test.ever"));
		Assert.Equal(1, state.GetCounter("test.runs"));
	}

	[Fact]
	public void Restore_TakesAFlagTheSnapshotDoesNotMentionBackToItsStartingValue()
	{
		var state = new DecisionState(Catalogue);
		ChangeEverything(state);

		state.Restore(new SlotSnapshot(new Dictionary<string, bool>(), new Dictionary<string, int>(), Array.Empty<CombatRecord>()));

		Assert.False(state.GetFlag("test.promised"));
		Assert.Equal(0, state.GetCounter("test.visits"));
		Assert.Equal(0, state.GetCounter(DecisionState.Wins));
	}

	[Fact]
	public void Restore_RaisesFlagChangedForEveryFlagItChanges_AfterTheyAllHaveTheirValues()
	{
		var state = new DecisionState(Catalogue);
		state.SetFlag("test.promised", true);
		state.AddToCounter("test.visits", 2);
		SlotSnapshot saved = state.TakeSnapshot();
		state.SetFlag("test.promised", false);
		state.AddToCounter("test.visits", 1);
		state.SetFlag("test.ever", true);

		var changed = new List<string>();
		state.FlagChanged += name =>
		{
			Assert.True(state.GetFlag("test.promised"));
			Assert.Equal(2, state.GetCounter("test.visits"));
			changed.Add(name);
		};
		state.Restore(saved);

		Assert.Equal(new[] { "test.promised", "test.visits" }, changed.Order());
	}

	public static TheoryData<SlotSnapshot, string> SnapshotsThatDoNotMatch => new()
	{
		{ Snapshot(flags: new() { ["test.tonite"] = true }), "'test.tonite' is not declared in flags.json" },
		{ Snapshot(counters: new() { ["test.tonite"] = 1 }), "'test.tonite' is not declared in flags.json" },
		{ Snapshot(flags: new() { ["test.ever"] = true }), "'test.ever' is persistent" },
		{ Snapshot(counters: new() { ["test.runs"] = 1 }), "'test.runs' is persistent" },
		{ Snapshot(flags: new() { ["test.visits"] = true }), "'test.visits' is a counter, not a boolean flag" },
		{ Snapshot(counters: new() { ["test.promised"] = 1 }), "'test.promised' is a boolean flag, not a counter" },
		{ Snapshot(counters: new() { ["test.visits"] = -1 }), "'test.visits' is -1 in the snapshot, but a counter is never below 0" }
	};

	[Theory]
	[MemberData(nameof(SnapshotsThatDoNotMatch))]
	public void Restore_RejectsASnapshotThatDoesNotMatchTheCatalogue_AndChangesNothing(SlotSnapshot snapshot, string expected)
	{
		var state = Listened(out List<string> changed);
		state.SetFlag("test.promised", true);
		state.RecordCombat(World.Fantasy, 1, "weak", playerWon: true);
		changed.Clear();

		var error = Assert.Throws<ArgumentException>(() => state.Restore(snapshot));

		Assert.Contains(expected, error.Message);
		Assert.True(state.GetFlag("test.promised"));
		Assert.Single(state.Combats);
		Assert.Empty(changed);
	}

	private static SlotSnapshot Snapshot(Dictionary<string, bool> flags = null, Dictionary<string, int> counters = null) =>
		new(flags ?? new Dictionary<string, bool>(), counters ?? new Dictionary<string, int>(), Array.Empty<CombatRecord>());
}
