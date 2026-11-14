using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Plays M7's demo against the game's own roster and a real <see cref="PartyState"/>: a
/// friend who joins if asked and stays behind if asked, and a newcomer who takes the
/// friend's place. No effect is ever refused along the way, since
/// <see cref="DialogueRunnerTests.Play(Conversation, DialogueContext, string[])"/> would
/// throw.
/// </summary>
public class DemoRecruitmentTests
{
	private const string Hero = "demo_hero";
	private const string Friend = "demo_friend";
	private const string Newcomer = "demo_newcomer";

	private static readonly FlagCatalogue Catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
	private static readonly Conversation Recruit = Demo("demo_recruit");
	private static readonly Conversation Replace = Demo("demo_replace");

	private static Conversation Demo(string id) =>
		ConversationReader.Read(id, ProjectFiles.ReadText("Dialogue", "Conversations", $"{id}.json"));

	/// <summary>The game's party as a playthrough starts: its protagonist alone.</summary>
	private static PartyState NewParty() => new(CharacterRosterTests.GameRosterValues(), TestRoster.Curves());

	private static string[] Play(Conversation conversation, PartyState party, params string[] picks) =>
		DialogueRunnerTests.Play(conversation, new DialogueContext(new DecisionState(Catalogue), party), picks);

	[Fact]
	public void AcceptingTheFriend_PutsThemInTheParty()
	{
		PartyState party = NewParty();

		Assert.Equal(new[] { "greet", "[join | bye] -> join", "lead_on" }, Play(Recruit, party, "join"));
		Assert.Equal(new[] { Hero, Friend }, party.Members);
	}

	[Fact]
	public void DecliningTheFriend_LeavesThePartyAsItWas_AndTheOfferStands()
	{
		PartyState party = NewParty();

		Assert.Equal(new[] { "greet", "[join | bye] -> bye" }, Play(Recruit, party, "bye"));
		Assert.Equal(new[] { Hero }, party.Members);

		Assert.Equal(new[] { "greet", "[join | bye] -> join", "lead_on" }, Play(Recruit, party, "join"));
	}

	[Fact]
	public void OnceInTheParty_TheFriendCanBeAskedToStayBehind_AndToJoinAgain()
	{
		PartyState party = NewParty();
		Play(Recruit, party, "join");

		Assert.Equal(new[] { "greet_member", "[stay | bye] -> stay", "wait_here" }, Play(Recruit, party, "stay"));
		Assert.Equal(new[] { Hero }, party.Members);

		Assert.Equal(new[] { "greet", "[join | bye] -> join", "lead_on" }, Play(Recruit, party, "join"));
		Assert.Equal(new[] { Hero, Friend }, party.Members);
	}

	[Fact]
	public void WithAFullParty_TheFriendSaysThereIsNoRoom_AndIsNotOffered()
	{
		// The game's three characters cannot fill a party of four, so this one is filled
		// in the stand-in, by making it smaller.
		var party = new InMemoryPartyStore(1, Hero, Friend, Newcomer);
		var context = new DialogueContext(new DecisionState(Catalogue), party);

		Assert.Equal(new[] { "no_room", "[bye] -> bye" }, DialogueRunnerTests.Play(Recruit, context, "bye"));
		Assert.Equal(0, party.Changes);
	}

	[Fact]
	public void TheNewcomer_TakesTheFriendsPlace()
	{
		PartyState party = NewParty();
		Play(Recruit, party, "join");

		Assert.Equal(new[] { "offer", "[swap | bye] -> swap", "my_turn" }, Play(Replace, party, "swap"));
		Assert.Equal(new[] { Hero, Newcomer }, party.Members);

		Assert.Equal(new[] { "greet_member", "[bye] -> bye" }, Play(Replace, party, "bye"));
	}

	[Fact]
	public void DecliningTheNewcomer_KeepsTheFriend()
	{
		PartyState party = NewParty();
		Play(Recruit, party, "join");

		Assert.Equal(new[] { "offer", "[swap | bye] -> bye" }, Play(Replace, party, "bye"));
		Assert.Equal(new[] { Hero, Friend }, party.Members);
	}

	[Fact]
	public void WithoutTheFriend_TheNewcomerHasNobodyToReplace()
	{
		PartyState party = NewParty();

		Assert.Equal(new[] { "nobody_to_replace", "[bye] -> bye" }, Play(Replace, party, "bye"));
		Assert.Equal(new[] { Hero }, party.Members);
	}

	[Fact]
	public void AfterTheReplacement_TheFriendCanJoinAgain_BesideTheNewcomer()
	{
		PartyState party = NewParty();
		Play(Recruit, party, "join");
		Play(Replace, party, "swap");

		Assert.Equal(new[] { "greet", "[join | bye] -> join", "lead_on" }, Play(Recruit, party, "join"));
		Assert.Equal(new[] { Hero, Newcomer, Friend }, party.Members);
	}

	[Fact]
	public void WhoeverLeaves_KeepsTheirProgress()
	{
		PartyState party = NewParty();
		Play(Recruit, party, "join");
		party.Award(World.Fantasy, 25);
		CharacterProgress earned = party.ProgressOf(Friend, World.Fantasy);

		Play(Replace, party, "swap");

		Assert.Equal(earned, party.ProgressOf(Friend, World.Fantasy));
		Assert.NotEqual(earned.Level, party.ProgressOf(Newcomer, World.Fantasy).Level);
	}

	[Fact]
	public void ThePresetWithTheFriend_StartsInFrontOfTheNewcomersOffer()
	{
		PartyState party = NewParty();
		var state = new DecisionState(Catalogue);
		(string name, string json) = ProjectFiles.PresetFiles().Single(file => file.Name == "with_friend.json");
		DebugPreset preset = DebugPreset.Read(name, json);

		Assert.Empty(preset.Validate(Catalogue, party.Roster));
		Assert.Empty(preset.Apply(new DialogueContext(state, party)));

		Assert.Equal(new CyclePosition(World.Fantasy, 1), preset.Start);
		Assert.Equal(new[] { Hero, Friend }, party.Members);
		Assert.Equal(new[] { "offer", "[swap | bye] -> swap", "my_turn" }, Play(Replace, party, "swap"));
	}

	[Fact]
	public void TheReplacement_WorksInAFullParty_BecauseTheFriendLeavesFirst()
	{
		// A party of two is full with the friend in it: recruiting first would be refused.
		var party = new InMemoryPartyStore(2, Hero, Friend, Newcomer).With(Friend);
		var context = new DialogueContext(new DecisionState(Catalogue), party);

		Assert.Equal(new[] { "offer", "[swap | bye] -> swap", "my_turn" }, DialogueRunnerTests.Play(Replace, context, "swap"));
		Assert.Equal(new[] { Hero, Newcomer }, party.Members);
	}
}
