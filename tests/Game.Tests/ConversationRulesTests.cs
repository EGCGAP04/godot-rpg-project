using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="ConversationRules"/>: what each kind of conversation stops, what
/// becomes of a fight requested during one, and when a conversation or a world
/// transition can start.
/// </summary>
public class ConversationRulesTests
{
	private static readonly GameActivity Exploring = new(InCombat: false, Transitioning: false, OnCombatCooldown: false, Conversation: null, FightQueued: false);

	private static GameActivity Talking(ConversationKind kind, bool fightQueued = false) =>
		Exploring with { Conversation = kind, FightQueued = fightQueued };

	[Fact]
	public void AForcedConversation_PausesTheWorld_AndAVoluntaryOneDoesNot()
	{
		Assert.True(ConversationRules.PausesWorld(ConversationKind.Forced));
		Assert.False(ConversationRules.PausesWorld(ConversationKind.Voluntary));
	}

	[Fact]
	public void AFightRequestedWhileExploring_Starts()
	{
		Assert.Equal(CombatRequestOutcome.Start, ConversationRules.OnCombatRequest(Exploring));
	}

	[Theory]
	[InlineData(ConversationKind.Voluntary)]
	[InlineData(ConversationKind.Forced)]
	public void AFightRequestedDuringAConversation_IsQueued(ConversationKind kind)
	{
		Assert.Equal(CombatRequestOutcome.Queue, ConversationRules.OnCombatRequest(Talking(kind)));
	}

	[Fact]
	public void ASecondFightRequestedDuringAConversation_IsIgnored()
	{
		Assert.Equal(CombatRequestOutcome.Ignore, ConversationRules.OnCombatRequest(Talking(ConversationKind.Voluntary, fightQueued: true)));
	}

	[Theory]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, true)]
	public void TheGuardsFromBeforeConversations_StillRefuseAFight(bool inCombat, bool transitioning, bool onCooldown)
	{
		var activity = Exploring with { InCombat = inCombat, Transitioning = transitioning, OnCombatCooldown = onCooldown };

		Assert.Equal(CombatRequestOutcome.Ignore, ConversationRules.OnCombatRequest(activity));
		Assert.Equal(CombatRequestOutcome.Ignore, ConversationRules.OnCombatRequest(activity with { Conversation = ConversationKind.Voluntary }));
	}

	[Fact]
	public void AConversation_CanStartWhileExploring_EvenOnTheCooldownAfterAFight()
	{
		Assert.True(ConversationRules.CanStartConversation(Exploring));
		Assert.True(ConversationRules.CanStartConversation(Exploring with { OnCombatCooldown = true }));
	}

	[Theory]
	[InlineData(true, false, false, false)]
	[InlineData(false, true, false, false)]
	[InlineData(false, false, true, false)]
	[InlineData(false, false, false, true)]
	public void AConversation_CannotStartDuringAFightATransitionOrAnotherConversation(bool inCombat, bool transitioning, bool talking, bool fightQueued)
	{
		var activity = Exploring with
		{
			InCombat = inCombat,
			Transitioning = transitioning,
			Conversation = talking ? ConversationKind.Voluntary : null,
			FightQueued = fightQueued
		};

		Assert.False(ConversationRules.CanStartConversation(activity));
	}

	[Fact]
	public void TheWorldCanBeLeftWhileExploring()
	{
		Assert.True(ConversationRules.CanTransition(Exploring));
		Assert.True(ConversationRules.CanTransition(Exploring with { OnCombatCooldown = true }));
	}

	[Theory]
	[InlineData(ConversationKind.Voluntary)]
	[InlineData(ConversationKind.Forced)]
	public void EveryExitIsClosed_DuringAConversation(ConversationKind kind)
	{
		Assert.False(ConversationRules.CanTransition(Talking(kind)));
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void EveryExitIsClosed_DuringAFightOrATransition(bool inCombat, bool transitioning)
	{
		Assert.False(ConversationRules.CanTransition(Exploring with { InCombat = inCombat, Transitioning = transitioning }));
	}
}
