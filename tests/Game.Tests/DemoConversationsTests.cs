using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Plays M6's demo against the real decision registry, catalogue and world access file:
/// an answer in the evening conversation keeps the player out of the Fantasy world
/// tonight, and the next cycle's forced conversation remembers which answer it was.
/// </summary>
public class DemoConversationsTests
{
	private static readonly FlagCatalogue Catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
	private static readonly WorldAccess Access = WorldAccess.Read(ProjectFiles.ReadText("Decisions", "world_access.json"));
	private static readonly Conversation Evening = Demo("demo_evening");
	private static readonly Conversation Morning = Demo("demo_morning");

	private static Conversation Demo(string id) =>
		ConversationReader.Read(id, ProjectFiles.ReadText("Dialogue", "Conversations", $"{id}.json"));

	private static string[] Play(Conversation conversation, DecisionState state, params string[] picks) =>
		DialogueRunnerTests.Play(conversation, state, picks);

	[Fact]
	public void TheFirstMorning_ShowsNothing()
	{
		Assert.Empty(Play(Morning, new DecisionState(Catalogue)));
	}

	[Fact]
	public void StayingUp_ClosesTheFantasyWorldTonight_AndTheNextMorningRemembersIt()
	{
		var state = new DecisionState(Catalogue);

		Assert.Equal(new[] { "greet", "[sleep | stay_up] -> stay_up", "no_dreams" }, Play(Evening, state, "stay_up"));
		Assert.Equal(new WorldUnlocks(Fantasy: false, Nightmare: false), Access.Unlocks(state));

		state.StartCycle();

		Assert.Equal(WorldUnlocks.All, Access.Unlocks(state));
		Assert.Equal(new[] { "stayed_up" }, Play(Morning, state));
	}

	[Fact]
	public void Sleeping_LeavesEveryWorldOpen_AndTheNextMorningRemembersIt()
	{
		var state = new DecisionState(Catalogue);

		Assert.Equal(new[] { "greet", "[sleep | stay_up] -> sleep", "good_night" }, Play(Evening, state, "sleep"));
		Assert.Equal(WorldUnlocks.All, Access.Unlocks(state));

		state.StartCycle();

		Assert.Equal(new[] { "slept" }, Play(Morning, state));
	}

	[Fact]
	public void ALaterAnswer_ReplacesTheFirst()
	{
		var state = new DecisionState(Catalogue);
		Play(Evening, state, "stay_up");

		Assert.Equal(new[] { "greet_again", "[sleep | stay_up] -> sleep", "good_night" }, Play(Evening, state, "sleep"));
		Assert.Equal(WorldUnlocks.All, Access.Unlocks(state));

		state.StartCycle();

		Assert.Equal(new[] { "slept" }, Play(Morning, state));
	}
}
