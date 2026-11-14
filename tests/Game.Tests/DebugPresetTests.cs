using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DebugPreset"/>: reading a preset file, checking its effects
/// against the catalogue, putting one preset on top of another, and applying it. Also
/// holds the test that checks every preset in the project.
/// </summary>
public class DebugPresetTests
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

	/// <summary>The characters there are: a protagonist and two others.</summary>
	private static readonly CharacterRosterValues Roster = TestRoster.With("abe", "bea");

	private static DebugPreset Read(string json) => DebugPreset.Read("test.json", json.Replace('\'', '"'));

	[Fact]
	public void EveryPresetInTheProject_IsValid()
	{
		// Refuses to pass by finding nothing, so a wrong folder cannot look like valid presets.
		(string Name, string Json)[] files = ProjectFiles.PresetFiles();
		FlagCatalogue catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));

		Assert.NotEmpty(files);
		Assert.Empty(files.SelectMany(file => DebugPreset.Read(file.Name, file.Json).Validate(catalogue, CharacterRosterTests.GameRosterValues())));
	}

	[Fact]
	public void Read_TakesTheWorldTheCycleAndTheEffects_InOrder()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'world': 'nightmare', 'cycle': 3, 'do': ['set test.tonight', 'add test.visits 2', 'clear test.ever'] }");

		Assert.Equal(World.Nightmare, preset.World);
		Assert.Equal(3, preset.Cycle);
		Assert.Equal(new CyclePosition(World.Nightmare, 3), preset.Start);
		Assert.Equal(
			new FlagEffect[] { new FlagEffect.Set("test.tonight", true), new FlagEffect.Add("test.visits", 2), new FlagEffect.Set("test.ever", false) },
			preset.Effects.Select(effect => effect.Effect));
		Assert.Equal(new[] { "test.json: do 1", "test.json: do 2", "test.json: do 3" }, preset.Effects.Select(effect => effect.Source));
	}

	[Fact]
	public void Read_WithoutWorldCycleOrEffects_StartsAtTheFirstWorldAndCycle()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd' }");

		Assert.Null(preset.World);
		Assert.Null(preset.Cycle);
		Assert.Empty(preset.Effects);
		Assert.Equal(new CyclePosition(World.Real, CycleProgression.FirstCycle), preset.Start);
	}

	[Theory]
	[InlineData("{ 'description': 'd' }", "test.json: missing 'format'.")]
	[InlineData("{ 'format': 1 }", "test.json: missing 'description'.")]
	[InlineData("{ 'format': 2, 'description': 'd' }", "test.json: format 2 is not supported; this game reads format 1.")]
	[InlineData("{ 'format': 1, 'description': ' ' }", "test.json: 'description' is empty; say what state the preset starts in.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'flags': [] }", "test.json: unknown property 'flags'; the ones allowed here are 'format', 'description', 'world', 'cycle', 'do'.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'cycle': 1, 'cycle': 2 }", "test.json: 'cycle' appears twice.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'world': 'Real' }", "test.json: 'Real' is not a world; the worlds are 'real', 'fantasy', 'nightmare'.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'world': '1' }", "test.json: '1' is not a world; the worlds are 'real', 'fantasy', 'nightmare'.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'cycle': 0 }", "test.json: 'cycle' is 0, but cycles are numbered from 1.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'cycle': '2' }", "test.json: 'cycle' should be a whole number, but it is text.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'do': 'set test.tonight' }", "test.json: 'do' should be a list, but it is text.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'do': ['set test.tonight', 3] }", "test.json: do 2: 'do' should be text, but it is a number.")]
	[InlineData("{ 'format': 1, 'description': 'd', 'do': ['set test.tonight', 'toggle test.tonight'] }", "test.json: do 2: Column 1: an effect starts with 'set', 'clear', 'add', 'recruit' or 'dismiss', not 'toggle'.")]
	public void Read_RejectsAMalformedFile_SayingWhere(string json, string expected)
	{
		DataFileException error = Assert.Throws<DataFileException>(() => Read(json));

		Assert.Equal(expected, error.Message);
	}

	[Fact]
	public void Validate_AcceptsEffectsOnEveryLifetime()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['set test.tonight', 'add test.visits 1', 'set test.ever'] }");

		Assert.Empty(preset.Validate(Catalogue, Roster));
	}

	[Fact]
	public void Validate_ReportsEveryMisusedFlag_SayingWhereItWasWritten()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['set test.missing', 'set test.tonight', 'add test.ever 1', 'set test.visits'] }");

		Assert.Equal(
			new[]
			{
				"test.json: do 1: 'test.missing' is not declared in flags.json.",
				"test.json: do 3: 'test.ever' is a boolean flag, so it changes with 'set' or 'clear', not with 'add'.",
				"test.json: do 4: 'test.visits' is a counter, so it changes with 'add', not with 'set' or 'clear'."
			},
			preset.Validate(Catalogue, Roster));
	}

	[Fact]
	public void Then_TakesTheLaterWorldAndCycle_WhereItGivesThem_AndAppliesItsEffectsAfter()
	{
		DebugPreset file = Read("{ 'format': 1, 'description': 'd', 'world': 'fantasy', 'cycle': 2, 'do': ['add test.visits 1'] }");
		var later = new DebugPreset(null, 4, new[] { DebugPreset.ParseEffect("add test.visits 2", "command line", "--do 1") });

		DebugPreset both = file.Then(later);

		Assert.Equal(new CyclePosition(World.Fantasy, 4), both.Start);
		Assert.Equal(new[] { "test.json: do 1", "command line: --do 1" }, both.Effects.Select(effect => effect.Source));
	}

	[Fact]
	public void Apply_AppliesTheEffectsInOrder_SoAnAddSetsACounterToItsAmount()
	{
		var state = new DecisionState(Catalogue);
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['set test.tonight', 'add test.visits 3', 'clear test.tonight', 'set test.ever'] }");

		preset.Apply(DialogueContext.FlagsOnly(state));

		Assert.False(state.GetFlag("test.tonight"));
		Assert.Equal(3, state.GetCounter("test.visits"));
		Assert.True(state.GetFlag("test.ever"));
	}

	[Fact]
	public void New_RejectsACycleBeforeTheFirst_AndAnUndeclaredWorld()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new DebugPreset(null, 0, Array.Empty<PresetEffect>()));
		Assert.Throws<ArgumentOutOfRangeException>(() => new DebugPreset((World)7, null, Array.Empty<PresetEffect>()));
	}

	[Fact]
	public void Apply_WithNothingRefused_ReturnsNoRefusals()
	{
		var state = new DecisionState(Catalogue);
		var party = new InMemoryPartyStore(TestRoster.Hero, "abe");
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['recruit abe', 'set test.ever'] }");

		Assert.Empty(preset.Apply(new DialogueContext(state, party)));

		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.True(state.GetFlag("test.ever"));
	}

	[Fact]
	public void Apply_ReturnsEveryRefusal_WithWhereItWasWritten_AndAppliesTheRest()
	{
		var state = new DecisionState(Catalogue);
		var party = new InMemoryPartyStore(2, TestRoster.Hero, "abe", "bea");
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['dismiss abe', 'recruit abe', 'recruit bea', 'set test.ever'] }");

		var refusals = preset.Apply(new DialogueContext(state, party));

		Assert.Equal(
			new[]
			{
				"test.json: do 1: 'dismiss abe' was refused: 'abe' is not in the party.",
				"test.json: do 3: 'recruit bea' was refused: 'bea' cannot join: the party already holds 2."
			},
			refusals);
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.True(state.GetFlag("test.ever"));
	}

	[Fact]
	public void Validate_AcceptsEffectsOnThePartysCharacters()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['recruit abe', 'recruit bea', 'dismiss abe'] }");

		Assert.Empty(preset.Validate(Catalogue, Roster));
	}

	[Fact]
	public void Validate_ReportsEveryMisusedCharacter_InTheWordsOfTheDialogueValidator()
	{
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['recruit zed', 'recruit abe', 'dismiss hero', 'set test.missing', 'recruit hero'] }");

		Assert.Equal(
			new[]
			{
				"test.json: do 1: 'zed' is not a character of the roster, which has: hero, abe, bea.",
				"test.json: do 3: 'hero' is the protagonist, who never leaves the party, so 'dismiss' is always refused.",
				"test.json: do 4: 'test.missing' is not declared in flags.json.",
				"test.json: do 5: 'hero' is the protagonist, who is always in the party, so 'recruit' is always refused."
			},
			preset.Validate(Catalogue, Roster));
	}

	[Fact]
	public void Validate_ChecksAnEffectFromTheCommandLineToo()
	{
		var preset = new DebugPreset(null, null, new[] { DebugPreset.ParseEffect("recruit zed", "command line", "--do 1") });

		Assert.Equal(
			new[] { "command line: --do 1: 'zed' is not a character of the roster, which has: hero, abe, bea." },
			preset.Validate(Catalogue, Roster));
	}

	[Fact]
	public void Validate_WithoutACatalogueOrARoster_IsRefused()
	{
		Assert.Throws<System.ArgumentNullException>(() => DebugPreset.None.Validate(null, Roster));
		Assert.Throws<System.ArgumentNullException>(() => DebugPreset.None.Validate(Catalogue, null));
	}

	[Fact]
	public void AValidPresetOverTheRealParty_AppliesWithNoRefusal()
	{
		// The whole path a preset takes in the game, with PartyState instead of the stand-in.
		PartyState party = TestRoster.Party("abe", "bea");
		DebugPreset preset = Read("{ 'format': 1, 'description': 'd', 'do': ['recruit abe', 'recruit bea', 'dismiss abe'] }");

		Assert.Empty(preset.Validate(Catalogue, party.Roster));
		Assert.Empty(preset.Apply(new DialogueContext(new DecisionState(Catalogue), party)));

		Assert.Equal(new[] { TestRoster.Hero, "bea" }, party.Members);
	}
}
