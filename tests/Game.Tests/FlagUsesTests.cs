using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagUses"/>: which flags a condition reads and an effect writes.
/// What it says about a wrong use is covered through <see cref="DialogueValidatorTests"/>
/// and <see cref="WorldAccessTests"/>, which report it.
/// </summary>
public class FlagUsesTests
{
	[Theory]
	[InlineData("a.x", "a.x:Bool")]
	[InlineData("a.n >= 2", "a.n:Counter")]
	[InlineData("not a.x", "a.x:Bool")]
	[InlineData("a.x and a.n > 1", "a.x:Bool a.n:Counter")]
	[InlineData("a.x or not a.y", "a.x:Bool a.y:Bool")]
	[InlineData("(a.x or a.y) and not (a.n == 0 or a.z)", "a.x:Bool a.y:Bool a.n:Counter a.z:Bool")]
	public void InCondition_FindsEveryFlagInTheTree_WithTheTypeItsReadingNeeds(string condition, string expected)
	{
		var uses = FlagUses.InCondition(FlagConditionParser.Parse(condition)).Select(use => $"{use.Name}:{use.Type}");

		Assert.Equal(expected, string.Join(" ", uses));
	}

	[Fact]
	public void InCondition_OfNoCondition_IsEmpty()
	{
		Assert.Empty(FlagUses.InCondition(null));
	}

	[Theory]
	[InlineData("set a.x", "a.x", FlagType.Bool)]
	[InlineData("clear a.x", "a.x", FlagType.Bool)]
	[InlineData("add a.n -1", "a.n", FlagType.Counter)]
	public void InEffect_IsTheFlagItWrites_WithTheTypeTheWritingNeeds(string effect, string name, FlagType type)
	{
		Assert.Equal((name, type), FlagUses.InEffect(FlagEffectParser.Parse(effect)));
	}
}
