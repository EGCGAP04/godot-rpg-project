using System.Linq;
using Xunit;
using static FlagCondition;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="ConversationReader"/>: how a conversation file becomes nodes,
/// lines and options with their keys, conditions and effects, and where it reports a
/// file that is not well formed.
/// </summary>
public class ConversationReaderTests
{
	/// <summary>
	/// Reads JSON written with single quotes, so a test can keep it short without
	/// escaping every double quote. No test needs a real single quote in its JSON.
	/// </summary>
	private static Conversation Read(string json) => ConversationReader.Read("test", json.Replace('\'', '"'));

	[Fact]
	public void Read_BuildsNodesLinesAndOptionsWithTheirKeys()
	{
		var conversation = Read("""
			{
				'format': 1,
				'nodes': {
					'start': {
						'lines': [ { 'speaker': 'guide', 'line': 'ask' } ],
						'choices': [
							{ 'option': 'yes', 'if': 'a.x', 'do': [ 'set a.y', 'add a.z 2' ], 'next': 'done' },
							{ 'option': 'no', 'next': 'END' }
						]
					},
					'done': {
						'lines': [ { 'speaker': 'narrator', 'line': 'nods' } ],
						'next': 'END'
					}
				}
			}
			""");

		Assert.Equal("test", conversation.Id);
		Assert.Equal(new[] { "done", "start" }, conversation.Nodes.Keys.Order());

		DialogueNode start = conversation.Start;
		DialogueLine ask = Assert.Single(start.Lines);
		Assert.Equal("ask", ask.Name);
		Assert.Equal("test.start.ask", ask.Key);
		Assert.Equal("guide", ask.Speaker);
		Assert.False(ask.IsNarration);
		Assert.Null(ask.Condition);
		Assert.Empty(ask.Effects);
		Assert.Null(start.Next);

		Assert.Equal(new[] { "yes", "no" }, start.Choices.Select(option => option.Name));
		DialogueOption yes = start.Choices[0];
		Assert.Equal("test.start.yes", yes.Key);
		Assert.Equal(new IsSet("a.x"), yes.Condition);
		Assert.Equal(new FlagEffect[] { new FlagEffect.Set("a.y", true), new FlagEffect.Add("a.z", 2) }, yes.Effects);
		Assert.Equal("done", yes.Next);
		Assert.Null(start.Choices[1].Condition);
		Assert.Equal(DialogueNames.End, start.Choices[1].Next);

		DialogueNode done = conversation.Nodes["done"];
		DialogueLine nods = Assert.Single(done.Lines);
		Assert.True(nods.IsNarration);
		Assert.Equal("test.done.nods", nods.Key);
		Assert.Empty(done.Choices);
		Assert.Equal(DialogueNames.End, done.Next);
	}

	[Fact]
	public void Read_ANodeCanBeJustAChoice()
	{
		var conversation = Read("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'leave', 'next': 'END' } ] } } }");

		Assert.Empty(conversation.Start.Lines);
		Assert.Equal("test.start.leave", Assert.Single(conversation.Start.Choices).Key);
	}

	[Fact]
	public void Read_TheReferenceExample()
	{
		var first = ConversationReader.Read("example_first_meeting", ProjectFiles.ReadText("Dialogue", "Conversations", "example_first_meeting.json"));
		var second = ConversationReader.Read("example_second_meeting", ProjectFiles.ReadText("Dialogue", "Conversations", "example_second_meeting.json"));

		Assert.Equal(new[] { "farewell", "relief", "shrug", "start", "thanks" }, first.Nodes.Keys.Order());
		Assert.Equal(new[] { "greet", "greet_familiar", "greet_again", "lost_key", "ask" }, first.Start.Lines.Select(line => line.Name));
		Assert.Equal(new[] { "help", "refuse", "give_key" }, first.Start.Choices.Select(option => option.Name));
		Assert.Equal(new IsSet("example.has_key"), first.Start.Choices[2].Condition);
		Assert.True(first.Nodes["relief"].Lines[0].IsNarration);
		Assert.Equal(DialogueNames.End, first.Nodes["farewell"].Next);
		Assert.Equal("example_second_meeting.grateful.found_it", Assert.Single(second.Nodes["grateful"].Lines).Key);
	}

	[Theory]
	[InlineData("Example")]
	[InlineData("example-meeting")]
	[InlineData("")]
	public void Read_RejectsAConversationIdThatIsNotAWord(string id)
	{
		var error = Assert.Throws<DataFileException>(() => ConversationReader.Read(id, "{}"));

		Assert.Contains("conversation id", error.Message);
	}

	[Theory]
	// The file as a whole
	[InlineData("{ 'format': 1, ", "not valid JSON")]
	[InlineData("[]", "expected an object")]
	[InlineData("{ 'nodes': { 'start': { 'next': 'END' } } }", "missing 'format'")]
	[InlineData("{ 'format': '1', 'nodes': { 'start': { 'next': 'END' } } }", "'format' should be a whole number")]
	[InlineData("{ 'format': 2, 'nodes': { 'start': { 'next': 'END' } } }", "format 2 is not supported")]
	[InlineData("{ 'format': 1, 'version': 1, 'nodes': { 'start': { 'next': 'END' } } }", "unknown property 'version'")]
	[InlineData("{ 'format': 1, 'nodes': { 'begin': { 'next': 'END' } } }", "no 'start' node")]
	// Nodes
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'next': 'END' }, 'start': { 'next': 'END' } } }", "'start' appears twice")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'next': 'END', 'next': 'END' } } }", "'next' appears twice")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'next': 'END' }, 'Later': { 'next': 'END' } } }", "node id 'Later'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [] } } }", "exactly one of 'choices' or 'next'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'go', 'next': 'END' } ], 'next': 'END' } } }", "exactly one of 'choices' or 'next'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'choices': [] } } }", "'choices' has no options")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'next': 'farewel' } } }", "node 'start': jumps to unknown node 'farewel'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'next': 'End' } } }", "unknown node 'End'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': { 'speaker': 'guide', 'line': 'hi' }, 'next': 'END' } } }", "'lines' should be a list")]
	// Lines
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'line': 'hi' } ], 'next': 'END' } } }", "line 1: missing 'speaker'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speakr': 'guide', 'speaker': 'guide', 'line': 'hi' } ], 'next': 'END' } } }", "unknown property 'speakr'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'Guide', 'line': 'hi' } ], 'next': 'END' } } }", "speaker 'Guide'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'say-hi' } ], 'next': 'END' } } }", "line name 'say-hi'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi' }, { 'speaker': 'guide', 'line': 'hi' } ], 'next': 'END' } } }", "'hi' names more than one line or option")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 'a.x >=' } ], 'next': 'END' } } }", "node 'start', line 1 ('hi'), if: Column 7")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 1 } ], 'next': 'END' } } }", "'if' should be text")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'do': 'set a.x' } ], 'next': 'END' } } }", "'do' should be a list")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'do': [ 'set a.x', 'toggle a.x' ] } ], 'next': 'END' } } }", "line 1 ('hi'), do 2: Column 1")]
	// Options
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'go' } ] } } }", "option 1: missing 'next'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'choices': [ { 'option': 'go', 'next': 'nowhere' } ] } } }", "option 'go': jumps to unknown node 'nowhere'")]
	[InlineData("{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'ask' } ], 'choices': [ { 'option': 'ask', 'next': 'END' } ] } } }", "'ask' names more than one line or option")]
	public void Read_RejectsAFileThatIsNotWellFormedAndSaysWhere(string json, string expected)
	{
		var error = Assert.Throws<DataFileException>(() => Read(json));

		Assert.Contains(expected, error.Message);
		Assert.Equal("test", error.File);
	}
}
