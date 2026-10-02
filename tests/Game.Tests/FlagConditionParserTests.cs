using Xunit;
using static FlagCondition;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagConditionParser"/>: which tree each condition becomes, with
/// <c>not</c> applying before <c>and</c>, <c>and</c> before <c>or</c>, and parentheses
/// before everything, and where it reports a malformed one.
/// </summary>
public class FlagConditionParserTests
{
	[Fact]
	public void Parse_ANameOnItsOwnIsAFlagThatIsSet()
	{
		Assert.Equal(new IsSet("example.has_key"), FlagConditionParser.Parse("example.has_key"));
	}

	[Theory]
	[InlineData("==", Comparator.Equal)]
	[InlineData("!=", Comparator.NotEqual)]
	[InlineData("<", Comparator.Less)]
	[InlineData("<=", Comparator.LessOrEqual)]
	[InlineData(">", Comparator.Greater)]
	[InlineData(">=", Comparator.GreaterOrEqual)]
	public void Parse_AComparedNameIsACounterComparison(string text, Comparator comparator)
	{
		Assert.Equal(
			new Comparison("example.times_met", comparator, -1),
			FlagConditionParser.Parse($"example.times_met {text} -1"));
	}

	[Fact]
	public void Parse_NotAppliesBeforeAnd()
	{
		Assert.Equal(
			new And(new Not(new IsSet("a.x")), new IsSet("b.y")),
			FlagConditionParser.Parse("not a.x and b.y"));
	}

	[Fact]
	public void Parse_AndGroupsBeforeOr()
	{
		Assert.Equal(
			new Or(new IsSet("a.x"), new And(new IsSet("b.y"), new IsSet("c.z"))),
			FlagConditionParser.Parse("a.x or b.y and c.z"));
	}

	[Fact]
	public void Parse_ParenthesesGroupFirst()
	{
		Assert.Equal(
			new And(new Or(new IsSet("a.x"), new IsSet("b.y")), new IsSet("c.z")),
			FlagConditionParser.Parse("(a.x or b.y) and c.z"));
	}

	[Fact]
	public void Parse_RepeatedOperatorsGroupFromTheLeft()
	{
		Assert.Equal(
			new Or(new Or(new IsSet("a.x"), new IsSet("b.y")), new IsSet("c.z")),
			FlagConditionParser.Parse("a.x or b.y or c.z"));
	}

	[Fact]
	public void Parse_NotCanBeRepeated()
	{
		Assert.Equal(new Not(new Not(new IsSet("a.x"))), FlagConditionParser.Parse("not not a.x"));
	}

	[Fact]
	public void Parse_ReadsTheReferenceExamplesLongestCondition()
	{
		// The line in the format's reference example that remembers a refusal.
		Assert.Equal(
			new And(
				new Comparison("example.times_met", Comparator.GreaterOrEqual, 1),
				new Not(new Or(new IsSet("example.promised_help"), new IsSet("example.key_returned")))),
			FlagConditionParser.Parse("example.times_met >= 1 and not (example.promised_help or example.key_returned)"));
	}

	[Theory]
	[InlineData("", 1)]             // nothing at all
	[InlineData("   ", 4)]          // only spaces
	[InlineData("a.x and", 8)]      // an operator with nothing after it
	[InlineData("a.x or", 7)]
	[InlineData("not", 4)]
	[InlineData("and a.x", 1)]      // an operator with nothing before it
	[InlineData("(a.x", 5)]         // a parenthesis left open
	[InlineData("a.x)", 4)]         // a parenthesis closed that never opened
	[InlineData("()", 2)]           // parentheses around nothing
	[InlineData("a.x >=", 7)]       // a comparator with no number
	[InlineData("a.x >= b.y", 8)]   // a comparator with a name instead of a number
	[InlineData("a.x b.y", 5)]      // two conditions with no operator between them
	[InlineData("3", 1)]            // a number where a condition should start
	public void Parse_RejectsAMalformedConditionAndSaysWhere(string text, int column)
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagConditionParser.Parse(text));

		Assert.Equal(column, error.Column);
	}

	[Fact]
	public void Parse_SaysWhichParenthesisWasLeftOpen()
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagConditionParser.Parse("(a.x and (b.y)"));

		Assert.Equal(15, error.Column);
		Assert.Contains("'(' at column 1", error.Message);
	}
}
