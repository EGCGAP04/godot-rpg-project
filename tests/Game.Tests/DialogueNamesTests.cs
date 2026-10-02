using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DialogueNames.IsWord"/>, the rule every id and name in a
/// conversation file follows.
/// </summary>
public class DialogueNamesTests
{
	[Theory]
	[InlineData("start", true)]
	[InlineData("a", true)]
	[InlineData("greet_again", true)]
	[InlineData("x2", true)]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("Start", false)]   // capitals
	[InlineData("END", false)]     // the end of a conversation is not a node
	[InlineData("2x", false)]      // starts with a digit
	[InlineData("_x", false)]      // starts with an underscore
	[InlineData("a-b", false)]
	[InlineData("a.b", false)]     // dots join words; they are not part of one
	[InlineData("a b", false)]
	public void IsWord_IsLowercaseLettersDigitsAndUnderscoresStartingWithALetter(string text, bool expected)
	{
		Assert.Equal(expected, DialogueNames.IsWord(text));
	}
}
