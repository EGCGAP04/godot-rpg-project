/// <summary>
/// Where dialogue conditions ask about the party and dialogue effects change it. The
/// party implements it for the game; tests implement it with a list.
/// </summary>
/// <remarks>
/// <para>
/// A change the party's rules do not allow is refused, not thrown: the method answers
/// <c>false</c>, says why in <c>refusal</c>, and changes nothing. A full party is ordinary
/// state, which a conversation can run into, and whoever asked for the change decides
/// what a refusal means there.
/// </para>
/// <para>
/// A character that does not exist is different. An implementation should throw for an
/// id it does not know, as <see cref="IFlagStore"/> does for an undeclared flag: a
/// misspelt character must not pass silently.
/// </para>
/// </remarks>
public interface IPartyStore
{
	/// <summary>Whether the party holds as many as it can.</summary>
	bool IsFull { get; }

	/// <summary>Whether <paramref name="character"/> is in the party now.</summary>
	bool IsMember(string character);

	/// <summary>
	/// Adds <paramref name="character"/> to the party. Refused when they are already in
	/// it, or when it is full.
	/// </summary>
	/// <returns>Whether they joined. When not, <paramref name="refusal"/> says why; otherwise it is null.</returns>
	bool TryRecruit(string character, out string refusal);

	/// <summary>
	/// Takes <paramref name="character"/> out of the party. Refused when they are the
	/// protagonist, or when they are not in it.
	/// </summary>
	/// <returns>Whether they left. When not, <paramref name="refusal"/> says why; otherwise it is null.</returns>
	bool TryDismiss(string character, out string refusal);
}
