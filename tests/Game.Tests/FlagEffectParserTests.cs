using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagEffectParser"/>: the three shapes an effect can take, and
/// where it reports anything else.
/// </summary>
public class FlagEffectParserTests
{
	[Fact]
	public void Parse_SetSetsAFlag()
	{
		Assert.Equal(new FlagEffect.Set("example.promised_help", true), FlagEffectParser.Parse("set example.promised_help"));
	}

	[Fact]
	public void Parse_ClearClearsAFlag()
	{
		Assert.Equal(new FlagEffect.Set("example.has_key", false), FlagEffectParser.Parse("clear example.has_key"));
	}

	[Theory]
	[InlineData("add example.times_met 1", 1)]
	[InlineData("add example.times_met -2", -2)]
	public void Parse_AddReadsTheAmount(string text, int amount)
	{
		Assert.Equal(new FlagEffect.Add("example.times_met", amount), FlagEffectParser.Parse(text));
	}

	[Theory]
	[InlineData("", 1)]                 // nothing at all
	[InlineData("toggle a.x", 1)]       // not one of the three verbs
	[InlineData("and a.x", 1)]          // a keyword, but not a verb
	[InlineData("set", 4)]              // a verb with no name
	[InlineData("set 3", 5)]            // a number where the name goes
	[InlineData("add a.x", 8)]          // add with no amount
	[InlineData("add a.x b.y", 9)]      // add with a name instead of an amount
	[InlineData("set a.x 1", 9)]        // set takes no amount
	[InlineData("clear a.x b.y", 11)]   // one name per effect
	[InlineData("add a.x 1 2", 11)]     // one amount per effect
	public void Parse_RejectsAMalformedEffectAndSaysWhere(string text, int column)
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagEffectParser.Parse(text));

		Assert.Equal(column, error.Column);
	}

	[Fact]
	public void Parse_RecruitNamesACharacter()
	{
		Assert.Equal(new FlagEffect.Recruit("demo_friend"), FlagEffectParser.Parse("recruit demo_friend"));
	}

	[Fact]
	public void Parse_DismissNamesACharacter()
	{
		Assert.Equal(new FlagEffect.Dismiss("demo_friend"), FlagEffectParser.Parse("dismiss demo_friend"));
	}

	[Theory]
	[InlineData("recruit", 8)]             // nobody named
	[InlineData("dismiss 3", 9)]           // a number where the character goes
	[InlineData("recruit abe bea", 13)]    // one character per effect
	[InlineData("dismiss abe 1", 13)]      // no amount
	[InlineData("recruit set", 9)]         // a keyword where the character goes
	[InlineData("in_party abe", 1)]        // a condition is not an effect
	[InlineData("party_full", 1)]
	public void Parse_RejectsAMalformedPartyEffectAndSaysWhere(string text, int column)
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagEffectParser.Parse(text));

		Assert.Equal(column, error.Column);
	}

	[Fact]
	public void Parse_SaysACharacterIsExpected_NotAFlag()
	{
		var error = Assert.Throws<FlagSyntaxException>(() => FlagEffectParser.Parse("recruit 3"));

		Assert.Contains("expected a character after 'recruit'", error.Message);
	}

	[Theory]
	[InlineData("set a.x")]
	[InlineData("clear a.x")]
	[InlineData("add a.n 3")]
	[InlineData("add a.n -2")]
	[InlineData("recruit abe")]
	[InlineData("dismiss abe")]
	public void Describe_WritesTheEffectBackAsItIsWritten(string text)
	{
		Assert.Equal(text, FlagEffectParser.Parse(text).Describe());
	}
}
