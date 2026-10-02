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
}
