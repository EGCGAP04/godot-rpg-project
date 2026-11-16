/// <summary>
/// When the player's menu can open. Deliberately a plain C# class with no Godot
/// dependency, like <see cref="ConversationRules"/>, whose <see cref="GameActivity"/> it
/// reads; <c>Main</c> applies it.
/// </summary>
public static class MenuRules
{
	/// <summary>
	/// Whether the menu can open now: only while exploring. Not during a fight, a world
	/// transition or a conversation of either kind, not while a fight waits to start,
	/// which would begin behind the menu, and not over a menu already open. The cooldown
	/// after a fight does not stop it.
	/// </summary>
	public static bool CanOpen(GameActivity activity) =>
		!activity.InCombat && !activity.Transitioning && activity.Conversation == null && !activity.FightQueued && !activity.MenuOpen;
}
