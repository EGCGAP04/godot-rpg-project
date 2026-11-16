using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>Exercises <see cref="MenuRules"/>: the player's menu opens only while exploring.</summary>
public class MenuRulesTests
{
	private static readonly GameActivity Exploring = new(InCombat: false, Transitioning: false, OnCombatCooldown: false, Conversation: null, FightQueued: false, MenuOpen: false);

	[Fact]
	public void TheMenuOpensWhileExploring_EvenOnTheCooldownAfterAFight()
	{
		Assert.True(MenuRules.CanOpen(Exploring));
		Assert.True(MenuRules.CanOpen(Exploring with { OnCombatCooldown = true }));
	}

	[Fact]
	public void TheMenuDoesNotOpen_DuringAFight()
	{
		Assert.False(MenuRules.CanOpen(Exploring with { InCombat = true }));
	}

	[Fact]
	public void TheMenuDoesNotOpen_DuringATransition()
	{
		Assert.False(MenuRules.CanOpen(Exploring with { Transitioning = true }));
	}

	[Theory]
	[InlineData(ConversationKind.Voluntary)]
	[InlineData(ConversationKind.Forced)]
	public void TheMenuDoesNotOpen_DuringAConversation(ConversationKind kind)
	{
		Assert.False(MenuRules.CanOpen(Exploring with { Conversation = kind }));
	}

	[Fact]
	public void TheMenuDoesNotOpen_WhileAFightWaitsToStart()
	{
		Assert.False(MenuRules.CanOpen(Exploring with { FightQueued = true }));
	}

	[Fact]
	public void TheMenuDoesNotOpen_OverAMenuAlreadyOpen()
	{
		Assert.False(MenuRules.CanOpen(Exploring with { MenuOpen = true }));
	}
}
