using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DialogueRunner"/>: first the format's reference example, through
/// the scenarios its design was checked against, then the rules behind them one at a time
/// on small conversations, and last what the runner refuses to do.
/// </summary>
public class DialogueRunnerTests
{
	private static readonly FlagCatalogue Catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
	private static readonly Conversation FirstMeeting = Example("example_first_meeting");
	private static readonly Conversation SecondMeeting = Example("example_second_meeting");

	private static Conversation Example(string id) =>
		ConversationReader.Read(id, ProjectFiles.ReadText("Dialogue", "Conversations", $"{id}.json"));

	/// <summary>Reads JSON written with single quotes, as in <see cref="ConversationReaderTests"/>.</summary>
	private static Conversation Read(string json) => ConversationReader.Read("test", json.Replace('\'', '"'));

	/// <summary>The flags of a playthrough that has just begun: every declared flag at its starting value.</summary>
	private static InMemoryFlagStore NewPlaythrough() => new InMemoryFlagStore().WithCatalogue(Catalogue);

	/// <summary>
	/// Plays a conversation to its end, picking the given options in order, and returns what
	/// was shown: each line's name, narration in parentheses, and each choice as the options
	/// offered and the one picked.
	/// </summary>
	private static string[] Play(Conversation conversation, IFlagStore flags, params string[] picks)
	{
		var shown = new List<string>();
		var remaining = new Queue<string>(picks);
		DialogueRunner runner = DialogueRunner.Start(conversation, flags);

		for (int steps = 0; runner.Step != DialogueStep.End; steps++)
		{
			Assert.True(steps < 100, "The conversation never ended.");

			if (runner.Step == DialogueStep.Line)
			{
				DialogueLine line = runner.CurrentLine;
				shown.Add(line.IsNarration ? $"({line.Name})" : line.Name);
				runner.Advance();
			}
			else
			{
				string pick = remaining.Dequeue();
				shown.Add($"[{string.Join(" | ", runner.Options.Select(option => option.Name))}] -> {pick}");
				runner.Choose(pick);
			}
		}

		Assert.Empty(remaining);
		return shown.ToArray();
	}

	// The reference example, played through the scenarios its design was checked against.

	[Fact]
	public void Example_AFirstMeetingEndsInAPromise()
	{
		var flags = NewPlaythrough();

		Assert.Equal(
			new[] { "greet", "lost_key", "ask", "[help | refuse] -> help", "will_wait", "bye" },
			Play(FirstMeeting, flags, "help"));
		Assert.True(flags.GetFlag("example.met_before"));
		Assert.Equal(1, flags.GetCounter("example.times_met"));
		Assert.True(flags.GetFlag("example.promised_help"));
	}

	[Fact]
	public void Example_TheSecondMeetingRemembersThePromise()
	{
		var flags = NewPlaythrough();
		Play(FirstMeeting, flags, "help");

		Assert.Equal(new[] { "remember_help", "still_looking", "[leave] -> leave" }, Play(SecondMeeting, flags, "leave"));
	}

	[Fact]
	public void Example_HandingOverTheKeyKeepsThePromise()
	{
		var flags = NewPlaythrough();
		Play(FirstMeeting, flags, "help");
		flags.SetFlag("example.has_key", true); // the key is picked up in the world, outside any conversation

		Assert.Equal(
			new[] { "remember_help", "[give_key | leave] -> give_key", "found_it" },
			Play(SecondMeeting, flags, "give_key"));
		Assert.False(flags.GetFlag("example.has_key"));
		Assert.True(flags.GetFlag("example.key_returned"));
		Assert.False(flags.GetFlag("example.promised_help"));
	}

	[Fact]
	public void Example_ARefusalIsRememberedNextTime()
	{
		var flags = NewPlaythrough();

		Assert.Equal(
			new[] { "greet", "lost_key", "ask", "[help | refuse] -> refuse", "never_mind", "bye" },
			Play(FirstMeeting, flags, "refuse"));
		Assert.Equal(new[] { "remember_refusal", "[leave] -> leave" }, Play(SecondMeeting, flags, "leave"));
	}

	[Fact]
	public void Example_WithTheKeyAlreadyFoundItCanBeHandedOverAtOnce()
	{
		var flags = NewPlaythrough();
		flags.SetFlag("example.has_key", true);

		Assert.Equal(
			new[] { "greet", "lost_key", "ask", "[help | refuse | give_key] -> give_key", "(hand_over)", "relieved", "bye" },
			Play(FirstMeeting, flags, "give_key"));
		Assert.Equal(new[] { "remember_key", "[leave] -> leave" }, Play(SecondMeeting, flags, "leave"));
	}

	[Fact]
	public void Example_ANewPlaythroughRemembersThePersistentFlag()
	{
		// met_before is persistent, so it survives into a new playthrough, while times_met,
		// a run flag, starts again at 0.
		var flags = NewPlaythrough();
		flags.SetFlag("example.met_before", true);

		Assert.Equal(
			new[] { "greet_familiar", "lost_key", "ask", "[help | refuse] -> help", "will_wait", "bye" },
			Play(FirstMeeting, flags, "help"));
	}

	[Fact]
	public void Example_InTheSamePlaythroughTheLatestAnswerCounts()
	{
		var flags = NewPlaythrough();
		Play(FirstMeeting, flags, "help");

		Assert.Equal(
			new[] { "greet_again", "lost_key", "ask", "[help | refuse] -> refuse", "never_mind", "bye" },
			Play(FirstMeeting, flags, "refuse"));
		Assert.Equal(new[] { "remember_refusal", "[leave] -> leave" }, Play(SecondMeeting, flags, "leave"));
		Assert.Equal(2, flags.GetCounter("example.times_met"));
		Assert.False(flags.GetFlag("example.promised_help"));
	}

	[Fact]
	public void Example_ASecondMeetingWithoutTheFirstIsAFirstEncounter()
	{
		Assert.Equal(new[] { "first_time", "[leave] -> leave" }, Play(SecondMeeting, NewPlaythrough(), "leave"));
	}

	// The rules, one at a time.

	[Fact]
	public void Start_ShowsTheFirstLineAndAppliesItsEffects()
	{
		var flags = new InMemoryFlagStore().WithCounter("a.n");
		var conversation = Read("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'do': [ 'add a.n 1' ] } ], 'next': 'END' } } }");

		var runner = DialogueRunner.Start(conversation, flags);

		Assert.Equal(DialogueStep.Line, runner.Step);
		Assert.Equal("test.start.hi", runner.CurrentLine.Key);
		Assert.Empty(runner.Options);
		Assert.Equal(1, flags.GetCounter("a.n"));
	}

	[Fact]
	public void Conditions_AreEvaluatedWhenReached_SoEarlierEffectsCount()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x");
		var conversation = Read("""
			{ 'format': 1, 'nodes': { 'start': {
				'lines': [ { 'speaker': 'guide', 'line': 'hi', 'do': [ 'set a.x' ] }, { 'speaker': 'guide', 'line': 'again', 'if': 'a.x' } ],
				'choices': [ { 'option': 'yes', 'if': 'a.x', 'next': 'END' }, { 'option': 'no', 'next': 'END' } ] } } }
			""");

		Assert.Equal(new[] { "hi", "again", "[yes | no] -> yes" }, Play(conversation, flags, "yes"));
	}

	[Fact]
	public void AHiddenLine_AppliesNoEffects()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x").WithCounter("a.n");
		var conversation = Read("""
			{ 'format': 1, 'nodes': { 'start': {
				'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 'a.x', 'do': [ 'add a.n 1' ] }, { 'speaker': 'guide', 'line': 'bye' } ],
				'next': 'END' } } }
			""");

		Assert.Equal(new[] { "bye" }, Play(conversation, flags));
		Assert.Equal(0, flags.GetCounter("a.n"));
	}

	[Fact]
	public void ANodeWithNoVisibleLine_GoesStraightToWhatEndsIt()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x");
		var conversation = Read("""
			{ 'format': 1, 'nodes': {
				'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 'a.x' } ], 'next': 'middle' },
				'middle': { 'choices': [ { 'option': 'ok', 'next': 'END' } ] } } }
			""");

		var runner = DialogueRunner.Start(conversation, flags);

		Assert.Equal(DialogueStep.Choice, runner.Step);
		Assert.Null(runner.CurrentLine);
		Assert.Equal("test.middle.ok", Assert.Single(runner.Options).Key);
	}

	[Fact]
	public void Choose_AppliesTheOptionsEffectsBeforeMovingOn()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x");
		var conversation = Read("""
			{ 'format': 1, 'nodes': {
				'start': { 'choices': [ { 'option': 'yes', 'do': [ 'set a.x' ], 'next': 'after' } ] },
				'after': { 'lines': [ { 'speaker': 'guide', 'line': 'seen', 'if': 'a.x' } ], 'next': 'END' } } }
			""");

		Assert.Equal(new[] { "[yes] -> yes", "seen" }, Play(conversation, flags, "yes"));
	}

	[Fact]
	public void TheEnd_ShowsNothing()
	{
		var runner = DialogueRunner.Start(Read("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'bye', 'next': 'END' } ] } } }"), new InMemoryFlagStore());

		runner.Choose("bye");

		Assert.Equal(DialogueStep.End, runner.Step);
		Assert.Null(runner.CurrentLine);
		Assert.Empty(runner.Options);
	}

	// What the runner refuses to do.

	[Fact]
	public void Advance_ThrowsAtAChoice()
	{
		var runner = DialogueRunner.Start(Read("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'bye', 'next': 'END' } ] } } }"), new InMemoryFlagStore());

		Assert.Throws<InvalidOperationException>(runner.Advance);
	}

	[Fact]
	public void Choose_ThrowsOnALine()
	{
		var runner = DialogueRunner.Start(FirstMeeting, NewPlaythrough());

		Assert.Throws<InvalidOperationException>(() => runner.Choose("help"));
	}

	[Theory]
	[InlineData("give_key")] // it exists, but is hidden: the key has not been found
	[InlineData("maybe")]    // it does not exist
	public void Choose_ThrowsOnAnOptionNotOffered(string option)
	{
		var runner = DialogueRunner.Start(FirstMeeting, NewPlaythrough());

		while (runner.Step == DialogueStep.Line)
			runner.Advance();

		var error = Assert.Throws<ArgumentException>(() => runner.Choose(option));
		Assert.Contains("help, refuse", error.Message);
	}

	[Fact]
	public void AdvanceAndChoose_ThrowAtTheEnd()
	{
		var runner = DialogueRunner.Start(Read("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'bye', 'next': 'END' } ] } } }"), new InMemoryFlagStore());
		runner.Choose("bye");

		Assert.Throws<InvalidOperationException>(runner.Advance);
		Assert.Throws<InvalidOperationException>(() => runner.Choose("bye"));
	}

	// The validator rejects the next two conversations, but the runner must still not hang
	// or stall on one: it throws instead.

	[Fact]
	public void ALoopOfNodesThatShowNothing_Throws()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x");
		var conversation = Read("""
			{ 'format': 1, 'nodes': {
				'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 'a.x' } ], 'next': 'other' },
				'other': { 'lines': [ { 'speaker': 'guide', 'line': 'bye', 'if': 'a.x' } ], 'next': 'start' } } }
			""");

		var error = Assert.Throws<InvalidOperationException>(() => DialogueRunner.Start(conversation, flags));
		Assert.Contains("loop", error.Message);
	}

	[Fact]
	public void AChoiceWithNoOptionAvailable_Throws()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x");
		var conversation = Read("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'yes', 'if': 'a.x', 'next': 'END' } ] } } }");

		var error = Assert.Throws<InvalidOperationException>(() => DialogueRunner.Start(conversation, flags));
		Assert.Contains("no option available", error.Message);
	}
}
