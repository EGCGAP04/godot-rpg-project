using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CharacterRosterValues"/>: one protagonist, everyone else after
/// them, and no id twice.
/// </summary>
public class CharacterRosterValuesTests
{
	[Fact]
	public void Constructor_PutsTheProtagonistFirst_AndKeepsTheOrderOfTheRest()
	{
		CharacterRosterValues roster = TestRoster.With("zoe", "abe");

		Assert.Equal(TestRoster.Hero, roster.Protagonist.Id);
		Assert.Equal(new[] { TestRoster.Hero, "zoe", "abe" }, roster.Characters.Select(character => character.Id));
	}

	[Fact]
	public void Constructor_WithNobodyElse_IsARosterOfOne()
	{
		CharacterRosterValues roster = TestRoster.With();

		Assert.Same(roster.Protagonist, Assert.Single(roster.Characters));
	}

	[Fact]
	public void Constructor_TheSameIdTwice_IsRefused()
	{
		var exception = Assert.Throws<ArgumentException>(() => TestRoster.With("abe", "zoe", "abe"));

		Assert.Contains("'abe' twice", exception.Message);
	}

	[Fact]
	public void Constructor_TheProtagonistAgainAmongTheOthers_IsRefused()
	{
		var exception = Assert.Throws<ArgumentException>(() => TestRoster.With("abe", TestRoster.Hero));

		Assert.Contains($"'{TestRoster.Hero}' twice", exception.Message);
	}

	[Fact]
	public void Constructor_AnEmptyEntry_IsRefused()
	{
		var others = new CharacterValues[] { TestRoster.Character("abe"), null };

		Assert.Throws<ArgumentException>(() => new CharacterRosterValues(TestRoster.Character(TestRoster.Hero), others));
	}

	[Fact]
	public void Constructor_NoProtagonistOrNoList_IsRefused()
	{
		Assert.Throws<ArgumentNullException>(() => new CharacterRosterValues(null, Array.Empty<CharacterValues>()));
		Assert.Throws<ArgumentNullException>(() => new CharacterRosterValues(TestRoster.Character(TestRoster.Hero), null));
	}

	[Fact]
	public void Get_GivesTheCharacterWithThatId()
	{
		CharacterRosterValues roster = TestRoster.With("abe", "zoe");

		Assert.Same(roster.Characters[2], roster.Get("zoe"));
		Assert.Same(roster.Protagonist, roster.Get(TestRoster.Hero));
	}

	[Theory]
	[InlineData("nobody")]
	[InlineData("Abe")]
	[InlineData("")]
	[InlineData(null)]
	public void Get_AnIdTheRosterDoesNotHave_IsRefused_NamingTheOnesItHas(string id)
	{
		var exception = Assert.Throws<ArgumentException>(() => TestRoster.With("abe").Get(id));

		Assert.Contains($"'{id}'", exception.Message);
		Assert.Contains($"{TestRoster.Hero}, abe", exception.Message);
	}

	[Fact]
	public void Contains_SaysWhetherTheRosterHasTheId()
	{
		CharacterRosterValues roster = TestRoster.With("abe");

		Assert.True(roster.Contains("abe"));
		Assert.True(roster.Contains(TestRoster.Hero));
		Assert.False(roster.Contains("zoe"));
		Assert.False(roster.Contains(null));
	}
}
