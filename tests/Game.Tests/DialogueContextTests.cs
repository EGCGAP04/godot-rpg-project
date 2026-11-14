using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DialogueContext"/>: it always carries both stores, and the one
/// built for text that may only speak of flags has a party that answers nothing.
/// </summary>
public class DialogueContextTests
{
	[Fact]
	public void Constructor_KeepsBothStores()
	{
		var flags = new InMemoryFlagStore();
		var party = new InMemoryPartyStore(TestRoster.Hero);

		var context = new DialogueContext(flags, party);

		Assert.Same(flags, context.Flags);
		Assert.Same(party, context.Party);
	}

	[Fact]
	public void Constructor_WithoutEitherStore_IsRefused()
	{
		Assert.Throws<ArgumentNullException>(() => new DialogueContext(null, new InMemoryPartyStore(TestRoster.Hero)));
		Assert.Throws<ArgumentNullException>(() => new DialogueContext(new InMemoryFlagStore(), null));
		Assert.Throws<ArgumentNullException>(() => DialogueContext.FlagsOnly(null));
	}

	[Fact]
	public void FlagsOnly_KeepsTheFlags()
	{
		var flags = new InMemoryFlagStore().WithFlag("a.x", true);

		DialogueContext context = DialogueContext.FlagsOnly(flags);

		Assert.Same(flags, context.Flags);
		Assert.True(new FlagCondition.IsSet("a.x").Evaluate(context));
	}

	[Fact]
	public void FlagsOnly_ItsPartyThrows_WhateverItIsAsked()
	{
		IPartyStore party = DialogueContext.FlagsOnly(new InMemoryFlagStore()).Party;

		Assert.Throws<InvalidOperationException>(() => party.IsFull);
		Assert.Throws<InvalidOperationException>(() => party.IsMember("abe"));
		Assert.Throws<InvalidOperationException>(() => party.TryRecruit("abe", out _));
		Assert.Throws<InvalidOperationException>(() => party.TryDismiss("abe", out _));
	}
}
