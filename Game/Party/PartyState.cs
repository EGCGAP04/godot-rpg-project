using System;
using System.Collections.Generic;

/// <summary>
/// The party: who is in it, in order. Deliberately a plain C# class with no Godot
/// dependency, so its rules are unit-tested without the engine; the <c>Party</c> autoload
/// holds one and adds the signals.
/// </summary>
/// <remarks>
/// <para>
/// The rules of membership: the protagonist is always in the party and cannot leave, the
/// party holds <see cref="MaximumSize"/> at most, and nobody is in it twice. Recruiting is
/// optional, so a party of one is a valid party.
/// </para>
/// <para>
/// A change that breaks a rule is refused with an exception and changes nothing. It is
/// never ignored in silence: a conversation that recruits into a full party is a mistake
/// in the game's content, and should be seen.
/// </para>
/// </remarks>
public sealed class PartyState
{
	/// <summary>How many characters the party holds at most, the protagonist included.</summary>
	public const int MaximumSize = 4;

	private readonly List<string> _members = new();

	public PartyState(CharacterRosterValues roster)
	{
		Roster = roster ?? throw new ArgumentNullException(nameof(roster));
		_members.Add(roster.Protagonist.Id);
		Members = _members.AsReadOnly();
	}

	/// <summary>Raised after the members change: someone joined or someone left.</summary>
	public event Action MembersChanged;

	/// <summary>Every character there is, in the party or not.</summary>
	public CharacterRosterValues Roster { get; }

	/// <summary>
	/// The ids of the characters in the party, in the order they joined. The protagonist
	/// is always the first.
	/// </summary>
	public IReadOnlyList<string> Members { get; }

	/// <summary>Whether the party has no room for one more.</summary>
	public bool IsFull => _members.Count >= MaximumSize;

	/// <summary>Whether this character is in the party now.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public bool IsMember(string id) => _members.Contains(Roster.Get(id).Id);

	/// <summary>Adds a character to the party, last.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	/// <exception cref="InvalidOperationException">The character is already in the party, or the party is full.</exception>
	public void Recruit(string id)
	{
		if (IsMember(id))
			throw new InvalidOperationException($"'{id}' is already in the party.");

		if (IsFull)
			throw new InvalidOperationException($"'{id}' cannot join: the party already holds {MaximumSize}.");

		_members.Add(id);
		MembersChanged?.Invoke();
	}

	/// <summary>Takes a character out of the party. Those after them move up.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	/// <exception cref="InvalidOperationException">The character is the protagonist, or is not in the party.</exception>
	public void Dismiss(string id)
	{
		if (Roster.Get(id).Id == Roster.Protagonist.Id)
			throw new InvalidOperationException($"'{id}' is the protagonist, who never leaves the party.");

		if (!_members.Remove(id))
			throw new InvalidOperationException($"'{id}' is not in the party.");

		MembersChanged?.Invoke();
	}
}
