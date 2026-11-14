using System;
using Xunit;
using static FlagCondition;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagCondition.Evaluate"/>: what each kind of condition answers
/// for a given set of flags, and that evaluating one never changes them.
/// </summary>
public class FlagConditionTests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void IsSet_HoldsExactlyWhenTheFlagIsSet(bool value)
	{
		var flags = new InMemoryFlagStore().WithFlag("example.has_key", value);

		Assert.Equal(value, new IsSet("example.has_key").Evaluate(DialogueContext.FlagsOnly(flags)));
	}

	// Always compared with 3, so each pair of rows sits on either side of the boundary.
	[Theory]
	[InlineData(Comparator.Equal, 3, true)]
	[InlineData(Comparator.Equal, 2, false)]
	[InlineData(Comparator.NotEqual, 2, true)]
	[InlineData(Comparator.NotEqual, 3, false)]
	[InlineData(Comparator.Less, 2, true)]
	[InlineData(Comparator.Less, 3, false)]
	[InlineData(Comparator.LessOrEqual, 3, true)]
	[InlineData(Comparator.LessOrEqual, 4, false)]
	[InlineData(Comparator.Greater, 4, true)]
	[InlineData(Comparator.Greater, 3, false)]
	[InlineData(Comparator.GreaterOrEqual, 3, true)]
	[InlineData(Comparator.GreaterOrEqual, 2, false)]
	public void Comparison_ComparesTheCounterWithTheValue(Comparator comparator, int counter, bool expected)
	{
		var flags = new InMemoryFlagStore().WithCounter("example.times_met", counter);

		Assert.Equal(expected, new Comparison("example.times_met", comparator, 3).Evaluate(DialogueContext.FlagsOnly(flags)));
	}

	[Fact]
	public void Evaluate_FollowsThePrecedenceOfTheText()
	{
		// Read as "(a or b) and c" this would be false, so true proves "and" was grouped first.
		var flags = new InMemoryFlagStore().WithFlag("a.x", true).WithFlag("b.y").WithFlag("c.z");

		Assert.True(FlagConditionParser.Parse("a.x or b.y and c.z").Evaluate(DialogueContext.FlagsOnly(flags)));
	}

	// The reference example's line that remembers a refusal: shown once the stranger has
	// been met, while no promise is pending and the key has not come back.
	[Theory]
	[InlineData(1, false, false, true)]
	[InlineData(0, false, false, false)]
	[InlineData(1, true, false, false)]
	[InlineData(1, false, true, false)]
	public void Evaluate_TheReferenceExamplesRefusalLine(int timesMet, bool promisedHelp, bool keyReturned, bool expected)
	{
		var condition = FlagConditionParser.Parse("example.times_met >= 1 and not (example.promised_help or example.key_returned)");
		var flags = new InMemoryFlagStore()
			.WithCounter("example.times_met", timesMet)
			.WithFlag("example.promised_help", promisedHelp)
			.WithFlag("example.key_returned", keyReturned);

		Assert.Equal(expected, condition.Evaluate(DialogueContext.FlagsOnly(flags)));
	}

	[Fact]
	public void Evaluate_NeverWritesAFlag()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x", true).WithFlag("b.y").WithCounter("c.z", 2);

		_ = FlagConditionParser.Parse("not (a.x and b.y) or c.z >= 1").Evaluate(DialogueContext.FlagsOnly(flags));

		Assert.Equal(0, flags.Writes);
	}

	[Fact]
	public void InParty_IsWhetherTheCharacterIsAMember()
	{
		var context = new DialogueContext(new InMemoryFlagStore(), new InMemoryPartyStore(TestRoster.Hero, "abe", "bea").With("abe"));

		Assert.True(new InParty("abe").Evaluate(context));
		Assert.False(new InParty("bea").Evaluate(context));
		Assert.True(new InParty(TestRoster.Hero).Evaluate(context));
	}

	[Fact]
	public void InParty_OfACharacterThatDoesNotExist_Throws()
	{
		var context = new DialogueContext(new InMemoryFlagStore(), new InMemoryPartyStore(TestRoster.Hero));

		Assert.Throws<ArgumentException>(() => new InParty("zed").Evaluate(context));
	}

	[Theory]
	[InlineData(2, true)]
	[InlineData(3, false)]
	public void PartyFull_IsWhetherThePartyHoldsAsManyAsItCan(int maximumSize, bool expected)
	{
		var context = new DialogueContext(new InMemoryFlagStore(), new InMemoryPartyStore(maximumSize, TestRoster.Hero, "abe").With("abe"));

		Assert.Equal(expected, new PartyFull().Evaluate(context));
	}

	[Theory]
	[InlineData(false, false, true)]    // outside, with room: the offer shows
	[InlineData(true, false, false)]    // already in
	[InlineData(false, true, false)]    // no room
	public void ThePartyWordsCombineWithTheRest(bool member, bool full, bool expected)
	{
		var party = new InMemoryPartyStore(full ? (member ? 2 : 1) : 4, TestRoster.Hero, "abe");

		if (member)
			party.With("abe");

		var context = new DialogueContext(new InMemoryFlagStore(), party);

		Assert.Equal(expected, FlagConditionParser.Parse("not in_party abe and not party_full").Evaluate(context));
	}

	[Fact]
	public void Evaluate_NeverChangesTheParty()
	{
		var party = new InMemoryPartyStore(TestRoster.Hero, "abe");

		_ = FlagConditionParser.Parse("in_party abe or party_full").Evaluate(new DialogueContext(new InMemoryFlagStore(), party));

		Assert.Equal(0, party.Changes);
		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
	}

	[Fact]
	public void AConditionOnTheParty_OverFlagsOnly_Throws()
	{
		DialogueContext context = DialogueContext.FlagsOnly(new InMemoryFlagStore());

		Assert.Throws<InvalidOperationException>(() => new InParty("abe").Evaluate(context));
		Assert.Throws<InvalidOperationException>(() => new PartyFull().Evaluate(context));
	}
}
