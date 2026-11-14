using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="PartyUses"/>: which characters a condition or an effect names,
/// and what the roster has to say about each.
/// </summary>
public class PartyUsesTests
{
	private static readonly CharacterRosterValues Roster = TestRoster.With("abe", "bea");

	[Theory]
	[InlineData("a.x")]
	[InlineData("a.n >= 2")]
	[InlineData("party_full")]
	[InlineData("not (a.x or party_full)")]
	public void InCondition_OfAConditionThatNamesNobody_IsEmpty(string condition)
	{
		Assert.Empty(PartyUses.InCondition(FlagConditionParser.Parse(condition)));
	}

	[Fact]
	public void InCondition_OfNoCondition_IsEmpty()
	{
		Assert.Empty(PartyUses.InCondition(null));
	}

	[Fact]
	public void InCondition_IsEveryCharacterAskedAbout_InOrder_WhereverTheyAre()
	{
		FlagCondition condition = FlagConditionParser.Parse("in_party abe and not (a.x or in_party bea) or in_party abe");

		Assert.Equal(new[] { "abe", "bea", "abe" }, PartyUses.InCondition(condition));
	}

	[Theory]
	[InlineData("a.x", false)]
	[InlineData("a.n >= 2 and not a.x", false)]
	[InlineData("party_full", true)]
	[InlineData("in_party abe", true)]
	[InlineData("a.x or not (a.y and party_full)", true)]
	[InlineData("not in_party abe and a.x", true)]
	[InlineData("a.x or in_party abe", true)]
	public void AsksAboutTheParty_IsWhetherEitherPartyWordIsAnywhereInIt(string condition, bool expected)
	{
		Assert.Equal(expected, PartyUses.AsksAboutTheParty(FlagConditionParser.Parse(condition)));
	}

	[Fact]
	public void AsksAboutTheParty_OfNoCondition_IsFalse()
	{
		Assert.False(PartyUses.AsksAboutTheParty(null));
	}

	[Theory]
	[InlineData("recruit abe", "abe")]
	[InlineData("dismiss bea", "bea")]
	[InlineData("set a.x", null)]
	[InlineData("clear a.x", null)]
	[InlineData("add a.n 1", null)]
	public void InEffect_IsTheCharacterItChanges_OrNobody(string effect, string expected)
	{
		Assert.Equal(expected, PartyUses.InEffect(FlagEffectParser.Parse(effect)));
	}

	[Theory]
	[InlineData("abe")]
	[InlineData(TestRoster.Hero)]    // always true, which is useless but not wrong
	public void ProblemInCondition_OfACharacterOfTheRoster_IsNone(string character)
	{
		Assert.Null(PartyUses.ProblemInCondition(Roster, character));
	}

	[Theory]
	[InlineData("abbe")]
	[InlineData("demo.friend")]    // a flag's shape parses, and names nobody
	public void ProblemInCondition_OfAnUnknownCharacter_NamesTheOnesThereAre(string character)
	{
		Assert.Equal($"'{character}' is not a character of the roster, which has: hero, abe, bea", PartyUses.ProblemInCondition(Roster, character));
	}

	[Theory]
	[InlineData("recruit abe")]
	[InlineData("dismiss bea")]
	[InlineData("set a.x")]
	[InlineData("add a.n 1")]
	public void ProblemInEffect_OfAnEffectThatCanApply_IsNone(string effect)
	{
		Assert.Null(PartyUses.ProblemInEffect(Roster, FlagEffectParser.Parse(effect)));
	}

	[Theory]
	[InlineData("recruit zed")]
	[InlineData("dismiss zed")]
	public void ProblemInEffect_OfAnUnknownCharacter_IsTheSameAsInACondition(string effect)
	{
		Assert.Equal(PartyUses.ProblemInCondition(Roster, "zed"), PartyUses.ProblemInEffect(Roster, FlagEffectParser.Parse(effect)));
	}

	[Theory]
	[InlineData("recruit hero", "'hero' is the protagonist, who is always in the party, so 'recruit' is always refused")]
	[InlineData("dismiss hero", "'hero' is the protagonist, who never leaves the party, so 'dismiss' is always refused")]
	public void ProblemInEffect_OnTheProtagonist_SaysItIsAlwaysRefused(string effect, string expected)
	{
		Assert.Equal(expected, PartyUses.ProblemInEffect(Roster, FlagEffectParser.Parse(effect)));
	}
}
