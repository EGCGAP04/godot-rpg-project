using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagEffect.TryApply"/>: what each effect writes, and what the
/// ones on the party answer when they are refused.
/// </summary>
public class FlagEffectTests
{
	[Fact]
	public void Set_SetsAndClearsTheFlag()
	{
		var flags = new InMemoryFlagStore().WithFlag("example.has_key");

		new FlagEffect.Set("example.has_key", true).TryApply(DialogueContext.FlagsOnly(flags), out _);
		Assert.True(flags.GetFlag("example.has_key"));

		new FlagEffect.Set("example.has_key", false).TryApply(DialogueContext.FlagsOnly(flags), out _);
		Assert.False(flags.GetFlag("example.has_key"));
	}

	[Theory]
	[InlineData(1, 3)]
	[InlineData(-2, 0)]
	[InlineData(-5, 0)]
	public void Add_ChangesTheCounterByTheAmount(int amount, int expected)
	{
		var flags = new InMemoryFlagStore().WithCounter("example.times_met", 2);

		new FlagEffect.Add("example.times_met", amount).TryApply(DialogueContext.FlagsOnly(flags), out _);

		Assert.Equal(expected, flags.GetCounter("example.times_met"));
	}

	[Fact]
	public void Apply_TheReferenceExamplesHandingOverOfTheKey()
	{
		// The do list of the option that gives the key back: it changes hands and the
		// promise to look for it is kept, so it is no longer pending.
		var flags = new InMemoryFlagStore()
			.WithFlag("example.has_key", true)
			.WithFlag("example.key_returned")
			.WithFlag("example.promised_help", true);

		foreach (string effect in new[] { "clear example.has_key", "set example.key_returned", "clear example.promised_help" })
			FlagEffectParser.Parse(effect).TryApply(DialogueContext.FlagsOnly(flags), out _);

		Assert.False(flags.GetFlag("example.has_key"));
		Assert.True(flags.GetFlag("example.key_returned"));
		Assert.False(flags.GetFlag("example.promised_help"));
	}

	private static DialogueContext Context(InMemoryPartyStore party) => new(new InMemoryFlagStore().WithFlag("a.x"), party);

	[Fact]
	public void SetAndAdd_AlwaysApply_WithNoRefusal()
	{
		var context = new DialogueContext(new InMemoryFlagStore().WithFlag("a.x").WithCounter("a.n"), new InMemoryPartyStore(TestRoster.Hero));

		Assert.True(new FlagEffect.Set("a.x", true).TryApply(context, out string first));
		Assert.True(new FlagEffect.Add("a.n", -5).TryApply(context, out string second));

		Assert.Null(first);
		Assert.Null(second);
	}

	[Fact]
	public void Recruit_PutsTheCharacterInTheParty()
	{
		var party = new InMemoryPartyStore(TestRoster.Hero, "abe");

		Assert.True(new FlagEffect.Recruit("abe").TryApply(Context(party), out string refusal));

		Assert.Null(refusal);
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
	}

	[Fact]
	public void Dismiss_TakesTheCharacterOut()
	{
		var party = new InMemoryPartyStore(TestRoster.Hero, "abe").With("abe");

		Assert.True(new FlagEffect.Dismiss("abe").TryApply(Context(party), out string refusal));

		Assert.Null(refusal);
		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
	}

	[Fact]
	public void Recruit_ThatThePartyRefuses_AnswersWithItsReason_AndChangesNothing()
	{
		var party = new InMemoryPartyStore(2, TestRoster.Hero, "abe", "bea").With("abe");

		Assert.False(new FlagEffect.Recruit("bea").TryApply(Context(party), out string full));
		Assert.False(new FlagEffect.Recruit("abe").TryApply(Context(party), out string already));

		Assert.Equal("'bea' cannot join: the party already holds 2.", full);
		Assert.Equal("'abe' is already in the party.", already);
		Assert.Equal(0, party.Changes);
	}

	[Fact]
	public void Dismiss_ThatThePartyRefuses_AnswersWithItsReason_AndChangesNothing()
	{
		var party = new InMemoryPartyStore(TestRoster.Hero, "abe");

		Assert.False(new FlagEffect.Dismiss("abe").TryApply(Context(party), out string outside));
		Assert.False(new FlagEffect.Dismiss(TestRoster.Hero).TryApply(Context(party), out string protagonist));

		Assert.Equal("'abe' is not in the party.", outside);
		Assert.Equal("'hero' is the protagonist, who never leaves the party.", protagonist);
		Assert.Equal(0, party.Changes);
	}

	[Fact]
	public void AnEffectOnACharacterThatDoesNotExist_Throws_InsteadOfBeingRefused()
	{
		var party = new InMemoryPartyStore(TestRoster.Hero);

		Assert.Throws<System.ArgumentException>(() => new FlagEffect.Recruit("zed").TryApply(Context(party), out _));
		Assert.Throws<System.ArgumentException>(() => new FlagEffect.Dismiss("zed").TryApply(Context(party), out _));
	}
}
