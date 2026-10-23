/// <summary>The two kinds of conversation, which differ in what they stop.</summary>
public enum ConversationKind
{
	/// <summary>Started by the game. It stops the world: the tree is paused, so no fight starts and no timer runs.</summary>
	Forced,

	/// <summary>
	/// Started by the player while exploring. The world keeps going, so enemies keep
	/// moving and timers keep running: talking is never a pause.
	/// </summary>
	Voluntary
}

/// <summary>What becomes of a fight requested by an enemy that touched the player.</summary>
public enum CombatRequestOutcome
{
	/// <summary>The fight starts now.</summary>
	Start,

	/// <summary>
	/// The fight waits for the conversation to end, and starts then. Dropping it instead
	/// would leave the enemy standing on the player with no fight, since an area does not
	/// report a body that is already inside it a second time.
	/// </summary>
	Queue,

	/// <summary>The request is refused, as it was before conversations existed.</summary>
	Ignore
}

/// <summary>
/// What is going on in the run at one moment, as far as starting a fight, a conversation
/// or a world transition is concerned. <c>Main</c> builds one from its own state each
/// time it has to decide.
/// </summary>
/// <param name="InCombat">A fight is running, or about to start.</param>
/// <param name="Transitioning">The run is moving to another world, from the fade out until the fade in ends.</param>
/// <param name="OnCombatCooldown">A fight or a world change has just ended, and new fights are refused for a moment.</param>
/// <param name="Conversation">The kind of the conversation running, or null when there is none.</param>
/// <param name="FightQueued">A fight is waiting for the conversation to end.</param>
public readonly record struct GameActivity(
	bool InCombat,
	bool Transitioning,
	bool OnCombatCooldown,
	ConversationKind? Conversation,
	bool FightQueued);

/// <summary>
/// The rules for what a conversation allows while it runs, and when one can start.
/// Deliberately a plain C# class with no Godot dependency, so the rules are unit-tested
/// without the engine; <c>Main</c> applies them.
/// </summary>
/// <remarks>
/// Both kinds keep the player still and every exit closed. They differ in the world:
/// a forced conversation pauses it, and a voluntary one lets it run, so a fight can be
/// requested in the middle of one. That fight is queued, never dropped.
/// </remarks>
public static class ConversationRules
{
	/// <summary>Whether a conversation of this kind pauses the world, and with it every fight and timer.</summary>
	public static bool PausesWorld(ConversationKind kind) => kind == ConversationKind.Forced;

	/// <summary>
	/// What becomes of a fight requested now. The guards that existed before conversations
	/// come first and still refuse it: a fight already running, a world transition, and
	/// the cooldown after either. Otherwise a running conversation queues it, once.
	/// </summary>
	public static CombatRequestOutcome OnCombatRequest(GameActivity activity)
	{
		if (activity.InCombat || activity.Transitioning || activity.OnCombatCooldown)
			return CombatRequestOutcome.Ignore;

		if (activity.Conversation == null)
			return CombatRequestOutcome.Start;

		return activity.FightQueued ? CombatRequestOutcome.Ignore : CombatRequestOutcome.Queue;
	}

	/// <summary>
	/// Whether a conversation can start now: not during a fight, a world transition or
	/// another conversation, and not while a fight waits to start. The cooldown after a
	/// fight does not stop one.
	/// </summary>
	public static bool CanStartConversation(GameActivity activity) =>
		!activity.InCombat && !activity.Transitioning && activity.Conversation == null && !activity.FightQueued;

	/// <summary>Whether the player can leave the world now. A conversation of either kind keeps every exit closed.</summary>
	public static bool CanTransition(GameActivity activity) =>
		!activity.InCombat && !activity.Transitioning && activity.Conversation == null;
}
