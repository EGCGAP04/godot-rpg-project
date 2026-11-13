using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="PartyState"/>'s rules of membership: the protagonist never
/// leaves, the party holds four at most, and a refused change leaves it as it was.
/// </summary>
public class PartyStateTests
{
	/// <summary>A party over a roster of six, so a full party still leaves someone out.</summary>
	private static PartyState Party() => TestRoster.Party("abe", "bea", "cal", "dee", "eve");

	/// <summary>Counts how many times the party says its members changed.</summary>
	private static Func<int> CountChanges(PartyState party)
	{
		int changes = 0;
		party.MembersChanged += () => changes++;
		return () => changes;
	}

	[Fact]
	public void Constructor_StartsWithTheProtagonistAlone()
	{
		PartyState party = Party();

		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
		Assert.True(party.IsMember(TestRoster.Hero));
		Assert.False(party.IsFull);
	}

	[Fact]
	public void Constructor_WithoutARosterOrCurves_IsRefused()
	{
		Assert.Throws<ArgumentNullException>(() => new PartyState(null, TestRoster.Curves()));
		Assert.Throws<ArgumentNullException>(() => new PartyState(TestRoster.With(), null));
	}

	[Fact]
	public void MaximumSize_IsFour()
	{
		Assert.Equal(4, PartyState.MaximumSize);
	}

	[Fact]
	public void Recruit_AddsTheCharacterLast()
	{
		PartyState party = Party();

		party.Recruit("bea");
		party.Recruit("abe");

		Assert.Equal(new[] { TestRoster.Hero, "bea", "abe" }, party.Members);
		Assert.True(party.IsMember("bea"));
		Assert.False(party.IsMember("cal"));
	}

	[Fact]
	public void Recruit_UpToFour_FillsTheParty()
	{
		PartyState party = Party();

		party.Recruit("abe");
		party.Recruit("bea");
		Assert.False(party.IsFull);

		party.Recruit("cal");
		Assert.True(party.IsFull);
		Assert.Equal(4, party.Members.Count);
	}

	[Fact]
	public void Recruit_IntoAFullParty_IsRefused_AndChangesNothing()
	{
		PartyState party = Party();
		party.Recruit("abe");
		party.Recruit("bea");
		party.Recruit("cal");
		Func<int> changes = CountChanges(party);

		var exception = Assert.Throws<InvalidOperationException>(() => party.Recruit("dee"));

		Assert.Contains("'dee'", exception.Message);
		Assert.Equal(new[] { TestRoster.Hero, "abe", "bea", "cal" }, party.Members);
		Assert.Equal(0, changes());
	}

	[Fact]
	public void Recruit_SomeoneAlreadyInTheParty_IsRefused_AndChangesNothing()
	{
		PartyState party = Party();
		party.Recruit("abe");
		Func<int> changes = CountChanges(party);

		var exception = Assert.Throws<InvalidOperationException>(() => party.Recruit("abe"));

		Assert.Contains("already", exception.Message);
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.Equal(0, changes());
	}

	[Fact]
	public void Recruit_TheProtagonist_IsRefused()
	{
		PartyState party = Party();

		Assert.Throws<InvalidOperationException>(() => party.Recruit(TestRoster.Hero));
		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
	}

	[Theory]
	[InlineData("nobody")]
	[InlineData("ABE")]
	[InlineData(null)]
	public void Recruit_ACharacterThatDoesNotExist_IsRefused_AndChangesNothing(string id)
	{
		PartyState party = Party();
		Func<int> changes = CountChanges(party);

		Assert.Throws<ArgumentException>(() => party.Recruit(id));

		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
		Assert.Equal(0, changes());
	}

	[Fact]
	public void Dismiss_TakesTheCharacterOut_AndTheRestMoveUp()
	{
		PartyState party = Party();
		party.Recruit("abe");
		party.Recruit("bea");
		party.Recruit("cal");

		party.Dismiss("abe");

		Assert.Equal(new[] { TestRoster.Hero, "bea", "cal" }, party.Members);
		Assert.False(party.IsMember("abe"));
		Assert.False(party.IsFull);
	}

	[Fact]
	public void Dismiss_MakesRoomForAReplacement()
	{
		PartyState party = Party();
		party.Recruit("abe");
		party.Recruit("bea");
		party.Recruit("cal");

		party.Dismiss("bea");
		party.Recruit("dee");

		Assert.Equal(new[] { TestRoster.Hero, "abe", "cal", "dee" }, party.Members);
	}

	[Fact]
	public void Dismiss_ThenRecruitAgain_PutsThemLast()
	{
		PartyState party = Party();
		party.Recruit("abe");
		party.Recruit("bea");

		party.Dismiss("abe");
		party.Recruit("abe");

		Assert.Equal(new[] { TestRoster.Hero, "bea", "abe" }, party.Members);
	}

	[Fact]
	public void Dismiss_TheProtagonist_IsRefused_AndChangesNothing()
	{
		PartyState party = Party();
		party.Recruit("abe");
		Func<int> changes = CountChanges(party);

		var exception = Assert.Throws<InvalidOperationException>(() => party.Dismiss(TestRoster.Hero));

		Assert.Contains("protagonist", exception.Message);
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.Equal(0, changes());
	}

	[Fact]
	public void Dismiss_SomeoneNotInTheParty_IsRefused_AndChangesNothing()
	{
		PartyState party = Party();
		party.Recruit("abe");
		Func<int> changes = CountChanges(party);

		var exception = Assert.Throws<InvalidOperationException>(() => party.Dismiss("bea"));

		Assert.Contains("not in the party", exception.Message);
		Assert.Equal(new[] { TestRoster.Hero, "abe" }, party.Members);
		Assert.Equal(0, changes());
	}

	[Theory]
	[InlineData("nobody")]
	[InlineData(null)]
	public void Dismiss_ACharacterThatDoesNotExist_IsRefused(string id)
	{
		PartyState party = Party();

		Assert.Throws<ArgumentException>(() => party.Dismiss(id));
		Assert.Equal(new[] { TestRoster.Hero }, party.Members);
	}

	[Fact]
	public void IsMember_OfACharacterThatDoesNotExist_IsRefused()
	{
		Assert.Throws<ArgumentException>(() => Party().IsMember("nobody"));
	}

	[Fact]
	public void MembersChanged_IsRaisedOnceForEachChange_AfterIt()
	{
		PartyState party = Party();
		int seenWhenRaised = 0;
		Func<int> changes = CountChanges(party);
		party.MembersChanged += () => seenWhenRaised = party.Members.Count;

		party.Recruit("abe");
		Assert.Equal(1, changes());
		Assert.Equal(2, seenWhenRaised);

		party.Dismiss("abe");
		Assert.Equal(2, changes());
		Assert.Equal(1, seenWhenRaised);
	}

	[Fact]
	public void Members_CannotBeChangedFromOutside()
	{
		PartyState party = Party();

		Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)party.Members).Add("abe"));
	}
}
