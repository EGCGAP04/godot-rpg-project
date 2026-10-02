using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagEffect.Apply"/>: what each effect writes.
/// </summary>
public class FlagEffectTests
{
	[Fact]
	public void Set_SetsAndClearsTheFlag()
	{
		var flags = new InMemoryFlagStore().WithFlag("example.has_key");

		new FlagEffect.Set("example.has_key", true).Apply(flags);
		Assert.True(flags.GetFlag("example.has_key"));

		new FlagEffect.Set("example.has_key", false).Apply(flags);
		Assert.False(flags.GetFlag("example.has_key"));
	}

	[Theory]
	[InlineData(1, 3)]
	[InlineData(-2, 0)]
	public void Add_ChangesTheCounterByTheAmount(int amount, int expected)
	{
		var flags = new InMemoryFlagStore().WithCounter("example.times_met", 2);

		new FlagEffect.Add("example.times_met", amount).Apply(flags);

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
			FlagEffectParser.Parse(effect).Apply(flags);

		Assert.False(flags.GetFlag("example.has_key"));
		Assert.True(flags.GetFlag("example.key_returned"));
		Assert.False(flags.GetFlag("example.promised_help"));
	}
}
