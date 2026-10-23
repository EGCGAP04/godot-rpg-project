using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="WorldAccess"/>: reading <c>world_access.json</c>, checking its
/// flags against the catalogue, and which worlds its conditions open.
/// </summary>
public class WorldAccessTests
{
	private static readonly FlagCatalogue ProjectCatalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
	private static readonly WorldAccess ProjectAccess = WorldAccess.Read(ProjectFiles.ReadText("Decisions", "world_access.json"));

	/// <summary>Reads JSON written with single quotes, as in <see cref="ConversationReaderTests"/>.</summary>
	private static WorldAccess Read(string json) => WorldAccess.Read(json.Replace('\'', '"'));

	/// <summary>A file whose two conditions are the given texts.</summary>
	private static WorldAccess WithConditions(string fantasy, string nightmare) =>
		Read($"{{ 'format': 1, 'worlds': {{ 'fantasy': '{fantasy}', 'nightmare': '{nightmare}' }} }}");

	[Fact]
	public void TheProjectFile_IsValidAgainstTheProjectCatalogue()
	{
		Assert.Empty(ProjectAccess.Validate(ProjectCatalogue));
	}

	[Fact]
	public void TheProjectFile_OpensEveryWorld_WhenEveryFlagIsAtItsStartingValue()
	{
		// The cycle plays as it did before the registry existed.
		Assert.Equal(WorldUnlocks.All, ProjectAccess.Unlocks(new DecisionState(ProjectCatalogue)));
	}

	[Fact]
	public void TheProjectFile_ClosingTheFantasyWorld_ClosesBoth()
	{
		var state = new DecisionState(ProjectCatalogue);
		state.SetFlag("world.fantasy_closed", true);

		Assert.Equal(new WorldUnlocks(Fantasy: false, Nightmare: false), ProjectAccess.Unlocks(state));
	}

	[Fact]
	public void TheProjectFile_ClosingTheNightmareWorld_LeavesTheFantasyWorldOpen()
	{
		var state = new DecisionState(ProjectCatalogue);
		state.SetFlag("world.nightmare_closed", true);

		Assert.Equal(new WorldUnlocks(Fantasy: true, Nightmare: false), ProjectAccess.Unlocks(state));
	}

	[Theory]
	[InlineData(false, false, false, false)]
	[InlineData(true, false, true, false)]
	[InlineData(true, true, true, true)]
	[InlineData(false, true, false, false)]
	public void Unlocks_TheNightmareWorldNeedsTheFantasyWorld(bool fantasyCondition, bool nightmareCondition, bool fantasyOpen, bool nightmareOpen)
	{
		var flags = new InMemoryFlagStore().WithFlag("a.f", fantasyCondition).WithFlag("a.n", nightmareCondition);

		WorldUnlocks unlocks = WithConditions("a.f", "a.n").Unlocks(flags);

		Assert.Equal(new WorldUnlocks(Fantasy: fantasyOpen, Nightmare: nightmareOpen), unlocks);
	}

	[Fact]
	public void Unlocks_EvaluatesAFullCondition()
	{
		WorldAccess access = WithConditions("a.f and a.count >= 2", "not a.n or a.count > 5");
		var flags = new InMemoryFlagStore().WithFlag("a.f", true).WithFlag("a.n", true).WithCounter("a.count", 2);

		Assert.Equal(new WorldUnlocks(Fantasy: true, Nightmare: false), access.Unlocks(flags));

		flags.AddToCounter("a.count", 4);
		Assert.Equal(new WorldUnlocks(Fantasy: true, Nightmare: true), access.Unlocks(flags));
	}

	[Theory]
	[InlineData("{ 'format': 1 ", "line 1, column 15", "not valid JSON")]
	[InlineData("{ 'format': 1 }", "", "missing 'worlds'")]
	[InlineData("{ 'format': 2, 'worlds': { 'fantasy': 'a.f', 'nightmare': 'a.n' } }", "", "format 2 is not supported")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': 'a.f', 'nightmare': 'a.n' }, 'real': 'a.r' }", "", "unknown property 'real'")]
	[InlineData("{ 'format': 1, 'worlds': [] }", "worlds", "expected an object")]
	[InlineData("{ 'format': 1, 'worlds': { 'real': 'a.r', 'fantasy': 'a.f', 'nightmare': 'a.n' } }", "worlds", "'real' has no condition, because every cycle starts there")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': 'a.f', 'nightmare': 'a.n', 'dream': 'a.d' } }", "worlds", "unknown property 'dream'")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': 'a.f' } }", "worlds", "missing 'nightmare'")]
	[InlineData("{ 'format': 1, 'worlds': { 'nightmare': 'a.n' } }", "worlds", "missing 'fantasy'")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': 'a.f', 'fantasy': 'a.g', 'nightmare': 'a.n' } }", "worlds", "'fantasy' appears twice")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': true, 'nightmare': 'a.n' } }", "world 'fantasy'", "'fantasy' should be text")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': 'a.f', 'nightmare': 'a.n >=' } }", "world 'nightmare'", "expected a number after '>='")]
	[InlineData("{ 'format': 1, 'worlds': { 'fantasy': '', 'nightmare': 'a.n' } }", "world 'fantasy'", "Column 1: expected a condition, but the text is empty")]
	public void Read_RejectsAFileThatIsNotValidAndSaysWhere(string json, string location, string expected)
	{
		var error = Assert.Throws<DataFileException>(() => Read(json));

		Assert.Contains(expected, error.Message);
		Assert.Equal(WorldAccess.FileName, error.File);
		Assert.Equal(location, error.Location);
	}

	[Fact]
	public void Validate_FindsEveryProblem_InTheWordsOfTheDialogueValidator()
	{
		FlagCatalogue catalogue = FlagCatalogue.Read("{ 'format': 1, 'flags': { 'a.f': { 'type': 'bool', 'lifetime': 'cycle' }, 'a.count': { 'type': 'counter', 'lifetime': 'run' } } }".Replace('\'', '"'));
		WorldAccess access = WithConditions("a.count and a.missing", "a.f >= 1");

		Assert.Equal(
			new[]
			{
				"world_access.json: world 'fantasy': 'a.count' is a counter, so it is compared with a number, as in 'a.count >= 1'.",
				"world_access.json: world 'fantasy': 'a.missing' is not declared in flags.json.",
				"world_access.json: world 'nightmare': 'a.f' is a boolean flag, so it is tested on its own, without a comparison."
			},
			access.Validate(catalogue));
	}
}
