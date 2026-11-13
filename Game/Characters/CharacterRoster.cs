using System.Linq;
using Godot;

/// <summary>
/// Every character the game has, authored as one <c>.tres</c> resource: the protagonist,
/// and everyone who can join the party. It is how the party finds the characters.
/// </summary>
/// <remarks>
/// <para>
/// A list in a file, and not a search of the folder, so that the game says who exists
/// and in which order, and so that the protagonist is named instead of guessed. The cost
/// is a second place to touch when a character is added: a test requires every
/// character's file to be listed here, so one left out is caught before it is missed.
/// </para>
/// <para>
/// Being in the roster is not being in the party. The party starts with the protagonist
/// alone, and the rest join through dialogue.
/// </para>
/// <para>
/// The game never reads the characters from here. <see cref="ToValues"/> builds the plain
/// <see cref="CharacterRosterValues"/> the engine-free party uses, which is also where the
/// roster is checked. The defaults are empty, as in every resource.
/// </para>
/// </remarks>
[GlobalClass]
public partial class CharacterRoster : Resource
{
	/// <summary>The character who is always in the party and never leaves it.</summary>
	[Export]
	public CharacterData Protagonist { get; set; }

	/// <summary>Everyone else, in the order the debug tooling and the tests list them.</summary>
	[Export]
	public Godot.Collections.Array<CharacterData> Others { get; set; } = new();

	/// <summary>
	/// The plain values the party reads. Throws when the protagonist or an entry is
	/// missing, when a character is wrong, or when two share an id.
	/// </summary>
	public CharacterRosterValues ToValues()
	{
		if (Protagonist == null)
			throw new System.ArgumentException("The roster has no protagonist.");

		if (Others.Any(character => character == null))
			throw new System.ArgumentException("The roster has an empty entry among the others.");

		return new CharacterRosterValues(Protagonist.ToValues(), Others.Select(character => character.ToValues()));
	}
}
