using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DialogueValidator"/>. The first test is the one that keeps the
/// project's own conversations valid; the rest check each rule on a small conversation.
/// </summary>
public class DialogueValidatorTests
{
	private const string Catalogue =
		"{ 'format': 1, 'flags': { 'a.flag': { 'type': 'bool', 'lifetime': 'run' }, 'a.count': { 'type': 'counter', 'lifetime': 'run' } } }";

	/// <summary>A valid conversation: one line, then the end. Each test changes one thing in it.</summary>
	private const string Valid = "{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi' } ], 'next': 'END' } } }";

	/// <summary>A conversation file written with single quotes, as in <see cref="ConversationReaderTests"/>.</summary>
	private static ConversationFile Source(string id, string json) =>
		new($"Dialogue/Conversations/{id}.json", json.Replace('\'', '"'));

	private static IReadOnlyList<string> Validate(params ConversationFile[] files) =>
		DialogueValidator.Validate(Catalogue.Replace('\'', '"'), files);

	/// <summary>The valid conversation, with extra properties added to its only line.</summary>
	private static ConversationFile WithLine(string extraProperties) =>
		Source("test", Valid.Replace("'line': 'hi'", $"'line': 'hi', {extraProperties}"));

	[Fact]
	public void EveryConversationInTheProject_IsValid()
	{
		// The test that makes a broken conversation fail CI. It refuses to pass by finding
		// nothing, so a wrong folder cannot look like a valid project.
		string folder = Path.Combine(ProjectFiles.GameDirectory, "Dialogue", "Conversations");
		Assert.True(Directory.Exists(folder), $"There is no conversations folder at {folder}.");

		ConversationFile[] files = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => new ConversationFile(Path.GetRelativePath(ProjectFiles.GameDirectory, path), File.ReadAllText(path)))
			.ToArray();

		Assert.NotEmpty(files);
		Assert.Empty(DialogueValidator.Validate(ProjectFiles.ReadText("Decisions", "flags.json"), files));
	}

	[Fact]
	public void AValidConversation_HasNoProblems()
	{
		Assert.Empty(Validate(Source("test", Valid)));
	}

	// Flags

	[Theory]
	[InlineData("'if': 'a.flagg'", "test: node 'start', line 1 ('hi'), if: 'a.flagg' is not declared in flags.json.")]
	[InlineData("'if': 'a.count'", "'a.count' is a counter, so it is compared with a number")]
	[InlineData("'if': 'a.flag >= 1'", "'a.flag' is a boolean flag, so it is tested on its own")]
	[InlineData("'if': 'not (a.flag or a.count)'", "'a.count' is a counter")]
	[InlineData("'do': [ 'set a.count' ]", "line 1 ('hi'), do 1: 'a.count' is a counter, so it changes with 'add'")]
	[InlineData("'do': [ 'add a.flag 1' ]", "'a.flag' is a boolean flag, so it changes with 'set' or 'clear'")]
	[InlineData("'do': [ 'set a.flag', 'add a.counter 1' ]", "do 2: 'a.counter' is not declared")]
	public void AFlag_MustBeDeclaredAndUsedWithItsType(string extraProperties, string expected)
	{
		Assert.Contains(expected, Assert.Single(Validate(WithLine(extraProperties))));
	}

	[Fact]
	public void AnOptionsFlags_AreCheckedToo()
	{
		var problems = Validate(Source("test", """
			{ 'format': 1, 'nodes': { 'start': { 'choices': [
				{ 'option': 'go', 'if': 'a.nope', 'do': [ 'clear a.gone' ], 'next': 'END' },
				{ 'option': 'stay', 'next': 'END' } ] } } }
			"""));

		Assert.Equal(
			new[]
			{
				"test: node 'start', option 1 ('go'), if: 'a.nope' is not declared in flags.json.",
				"test: node 'start', option 1 ('go'), do 1: 'a.gone' is not declared in flags.json."
			},
			problems);
	}

	// Paths

	[Fact]
	public void ANodeNothingLeadsTo_IsReported()
	{
		var problems = Validate(Source("test", "{ 'format': 1, 'nodes': { 'start': { 'next': 'END' }, 'orphan': { 'next': 'END' } } }"));

		Assert.Equal("test: node 'orphan': no jump or option leads here from 'start'.", Assert.Single(problems));
	}

	[Fact]
	public void AChoiceWhoseOptionsAllHaveConditions_IsReported()
	{
		// Even when the conditions cover every case between them: the rule is kept strict.
		var problems = Validate(Source("test", """
			{ 'format': 1, 'nodes': { 'start': { 'choices': [
				{ 'option': 'yes', 'if': 'a.flag', 'next': 'END' },
				{ 'option': 'no', 'if': 'not a.flag', 'next': 'END' } ] } } }
			"""));

		Assert.Contains("node 'start': every option has a condition", Assert.Single(problems));
	}

	[Fact]
	public void ALoopWithNoWayOut_IsReported()
	{
		var problems = Validate(Source("test", """
			{ 'format': 1, 'nodes': {
				'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi' } ], 'next': 'again' },
				'again': { 'lines': [ { 'speaker': 'guide', 'line': 'hi' } ], 'next': 'start' } } }
			"""));

		Assert.Contains("from nodes 'again', 'start' there is no way to END", Assert.Single(problems));
	}

	[Fact]
	public void AWayOutThatOnlyAConditionOpens_IsReported()
	{
		var problems = Validate(Source("test", """
			{ 'format': 1, 'nodes': { 'start': { 'choices': [
				{ 'option': 'leave', 'if': 'a.flag', 'next': 'END' },
				{ 'option': 'wait', 'next': 'start' } ] } } }
			"""));

		Assert.Contains("from node 'start' there is no way to END that does not depend on a condition", Assert.Single(problems));
	}

	[Fact]
	public void AConditionalBranchBesideAWayOut_IsFine()
	{
		var problems = Validate(Source("test", """
			{ 'format': 1, 'nodes': {
				'start': { 'choices': [
					{ 'option': 'secret', 'if': 'a.flag', 'next': 'hidden' },
					{ 'option': 'leave', 'next': 'END' } ] },
				'hidden': { 'lines': [ { 'speaker': 'guide', 'line': 'psst' } ], 'next': 'start' } } }
			"""));

		Assert.Empty(problems);
	}

	// Ids

	[Fact]
	public void TwoFilesWithTheSameId_AreReported()
	{
		string json = Valid.Replace('\'', '"');

		var problems = Validate(
			new ConversationFile("Dialogue/Conversations/Real/test.json", json),
			new ConversationFile("Dialogue/Conversations/Fantasy/test.json", json));

		Assert.Equal(
			"test: 2 files have this id, which must be unique: 'Dialogue/Conversations/Real/test.json', 'Dialogue/Conversations/Fantasy/test.json'.",
			Assert.Single(problems));
	}

	[Theory]
	[InlineData("speaker")]
	[InlineData("ui")]
	public void AReservedId_IsReported(string id)
	{
		Assert.Contains("this id is reserved", Assert.Single(Validate(Source(id, Valid))));
	}

	// Reading

	[Fact]
	public void AFileThatCannotBeRead_IsReported_AndTheOthersAreStillChecked()
	{
		var problems = Validate(Source("broken", "{ 'format': 1 "), WithLine("'if': 'a.nope'"));

		Assert.Equal(2, problems.Count);
		Assert.StartsWith("broken: line 1", problems[0]);
		Assert.Contains("'a.nope' is not declared", problems[1]);
	}

	[Fact]
	public void AnInvalidCatalogue_IsReportedOnce_InsteadOfBlamingEveryFlag()
	{
		var problems = DialogueValidator.Validate("{ \"format\": 2, \"flags\": {} }", new[] { WithLine("'if': 'a.flag'") });

		Assert.Equal("flags.json: format 2 is not supported; this game reads format 1.", Assert.Single(problems));
	}

	[Fact]
	public void EveryProblem_IsReported_NotJustTheFirst()
	{
		var problems = Validate(
			Source("test", "{ 'format': 1, 'nodes': { 'start': { 'lines': [ { 'speaker': 'guide', 'line': 'hi', 'if': 'a.nope' } ], 'next': 'END' }, 'orphan': { 'next': 'END' } } }"),
			Source("ui", Valid));

		Assert.Equal(3, problems.Count);
	}
}
