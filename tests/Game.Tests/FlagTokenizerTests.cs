using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagTokenizer"/>, the first step of reading a dialogue
/// condition or effect: it splits the text into names, numbers, keywords, comparators
/// and parentheses, each with the column where it starts.
/// </summary>
public class FlagTokenizerTests
{
	[Fact]
	public void Tokenize_SplitsTheTextIntoTokensWithTheirColumns()
	{
		var tokens = FlagTokenizer.Tokenize("not (example.times_met >= 1)");

		Assert.Equal(
			new[]
			{
				new FlagToken(FlagTokenKind.Keyword, "not", 1),
				new FlagToken(FlagTokenKind.OpenParen, "(", 5),
				new FlagToken(FlagTokenKind.Name, "example.times_met", 6),
				new FlagToken(FlagTokenKind.Comparator, ">=", 24),
				new FlagToken(FlagTokenKind.Integer, "1", 27),
				new FlagToken(FlagTokenKind.CloseParen, ")", 28),
				new FlagToken(FlagTokenKind.End, "", 29)
			},
			tokens);
	}

	[Theory]
	[InlineData("==")]
	[InlineData("!=")]
	[InlineData("<")]
	[InlineData("<=")]
	[InlineData(">")]
	[InlineData(">=")]
	public void Tokenize_ReadsEachComparatorAsOneToken(string comparator)
	{
		var tokens = FlagTokenizer.Tokenize($"a.counter {comparator} 3");

		Assert.Equal(new FlagToken(FlagTokenKind.Comparator, comparator, 11), tokens[1]);
	}

	[Fact]
	public void Tokenize_DoesNotNeedSpacesAroundAComparator()
	{
		var tokens = FlagTokenizer.Tokenize("a.counter<=3");

		Assert.Equal(
			new[] { FlagTokenKind.Name, FlagTokenKind.Comparator, FlagTokenKind.Integer, FlagTokenKind.End },
			tokens.Select(token => token.Kind));
		Assert.Equal("<=", tokens[1].Text);
	}

	[Fact]
	public void Tokenize_ReadsANegativeNumber()
	{
		var tokens = FlagTokenizer.Tokenize("a.counter > -2");

		Assert.Equal(new FlagToken(FlagTokenKind.Integer, "-2", 13), tokens[2]);
	}

	[Theory]
	[InlineData("and")]
	[InlineData("or")]
	[InlineData("not")]
	[InlineData("set")]
	[InlineData("clear")]
	[InlineData("add")]
	public void Tokenize_ReadsReservedWordsAsKeywords(string word)
	{
		Assert.Equal(FlagTokenKind.Keyword, FlagTokenizer.Tokenize(word)[0].Kind);
	}

	[Fact]
	public void Tokenize_AWordThatOnlyContainsAReservedWordIsAName()
	{
		// "android" holds "and" and "notes" holds "not": only a whole word is reserved.
		var token = FlagTokenizer.Tokenize("android.notes")[0];

		Assert.Equal(new FlagToken(FlagTokenKind.Name, "android.notes", 1), token);
	}

	[Fact]
	public void Tokenize_TextWithNoTokensIsJustTheEnd()
	{
		var end = Assert.Single(FlagTokenizer.Tokenize("  "));

		Assert.Equal(new FlagToken(FlagTokenKind.End, "", 3), end);
	}

	[Theory]
	[InlineData("a.x & b.y", 5)]           // not a character of the language
	[InlineData("Example.flag", 1)]        // names are lowercase
	[InlineData("a.x = 1", 5)]             // '=' alone does not compare
	[InlineData("!a.x", 1)]                // negation is 'not'
	[InlineData("a.x > -", 7)]             // a minus sign with no number
	[InlineData("a..x", 1)]                // an empty part
	[InlineData("a.", 1)]                  // a trailing dot
	[InlineData("a.1x", 1)]                // a part starting with a digit
	[InlineData("example.and", 1)]         // a reserved word as a part
	[InlineData("a.x > 99999999999", 7)]   // a number too large for an int
	public void Tokenize_RejectsWhatIsNotATokenAndSaysWhere(string text, int column)
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagTokenizer.Tokenize(text));

		Assert.Equal(column, error.Column);
		Assert.StartsWith($"Column {column}: ", error.Message);
	}

	[Fact]
	public void Tokenize_PointsAnExclamationMarkTowardsNot()
	{
		// The likeliest slip for someone used to C#, so the message names the fix.
		var error = Assert.Throws<FlagSyntaxException>(() => FlagTokenizer.Tokenize("!example.has_key"));

		Assert.Contains("'not'", error.Message);
	}
}
