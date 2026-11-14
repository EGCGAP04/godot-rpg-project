using System;
using System.Collections.Generic;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Holds <see cref="PartyState"/> and <see cref="InMemoryPartyStore"/> to the same
/// behaviour as an <see cref="IPartyStore"/>, with the same words in every refusal, so a
/// dialogue test over the stand-in says something about the game.
/// </summary>
public class PartyStoreContractTests
{
	/// <summary>Each implementation, as a party of the hero alone where five others exist.</summary>
	public static IEnumerable<object[]> Stores()
	{
		yield return new object[] { "PartyState", (Func<IPartyStore>)(() => TestRoster.Party("abe", "bea", "cal", "dee", "eve")) };
		yield return new object[] { "InMemoryPartyStore", (Func<IPartyStore>)(() => new InMemoryPartyStore(TestRoster.Hero, "abe", "bea", "cal", "dee", "eve")) };
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void ANewParty_HoldsTheProtagonistAlone(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.True(party.IsMember(TestRoster.Hero), name);
		Assert.False(party.IsMember("abe"), name);
		Assert.False(party.IsFull, name);
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryRecruit_SomeoneOutside_AddsThem_WithNoRefusal(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.True(party.TryRecruit("abe", out string refusal), name);

		Assert.Null(refusal);
		Assert.True(party.IsMember("abe"));
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryRecruit_SomeoneAlreadyIn_IsRefused_AndSaysSo(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();
		party.TryRecruit("abe", out _);

		Assert.False(party.TryRecruit("abe", out string refusal), name);

		Assert.Equal("'abe' is already in the party.", refusal);
		Assert.True(party.IsMember("abe"));
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryRecruit_TheProtagonist_IsRefused_AsAlreadyIn(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.False(party.TryRecruit(TestRoster.Hero, out string refusal), name);

		Assert.Equal("'hero' is already in the party.", refusal);
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryRecruit_IntoAFullParty_IsRefused_AndSaysSo(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();
		party.TryRecruit("abe", out _);
		party.TryRecruit("bea", out _);
		Assert.False(party.IsFull, name);
		party.TryRecruit("cal", out _);
		Assert.True(party.IsFull, name);

		Assert.False(party.TryRecruit("dee", out string refusal), name);

		Assert.Equal("'dee' cannot join: the party already holds 4.", refusal);
		Assert.False(party.IsMember("dee"));
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryDismiss_AMember_TakesThemOut_AndMakesRoom(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();
		party.TryRecruit("abe", out _);
		party.TryRecruit("bea", out _);
		party.TryRecruit("cal", out _);

		Assert.True(party.TryDismiss("bea", out string refusal), name);

		Assert.Null(refusal);
		Assert.False(party.IsMember("bea"));
		Assert.False(party.IsFull);
		Assert.True(party.TryRecruit("dee", out _));
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryDismiss_SomeoneOutside_IsRefused_AndSaysSo(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.False(party.TryDismiss("abe", out string refusal), name);

		Assert.Equal("'abe' is not in the party.", refusal);
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void TryDismiss_TheProtagonist_IsRefused_AndTheyStay(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.False(party.TryDismiss(TestRoster.Hero, out string refusal), name);

		Assert.Equal("'hero' is the protagonist, who never leaves the party.", refusal);
		Assert.True(party.IsMember(TestRoster.Hero));
	}

	[Theory]
	[MemberData(nameof(Stores))]
	public void ACharacterThatDoesNotExist_Throws_InsteadOfBeingRefused(string name, Func<IPartyStore> create)
	{
		IPartyStore party = create();

		Assert.Throws<ArgumentException>(() => party.IsMember("zed"));
		Assert.Throws<ArgumentException>(() => party.TryRecruit("zed", out _));
		Assert.Throws<ArgumentException>(() => party.TryDismiss("zed", out _));
		Assert.False(party.IsFull, name);
	}
}
