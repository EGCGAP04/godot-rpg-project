using Godot;

/// <summary>
/// One character, authored as a <c>.tres</c> resource: who they are, and how their stats
/// grow in each of the three worlds. A new character is a new file, not new code.
/// </summary>
/// <remarks>
/// <para>
/// The three <see cref="StatGrowth"/> resources live inside the character's own file,
/// as sub-resources: in the Inspector each one unfolds under its world. Levels and
/// stats are independent per world, which is why there are three and not one.
/// </para>
/// <para>
/// The game never reads the stats from here. <see cref="ToValues"/> builds the plain
/// <see cref="CharacterValues"/> the engine-free rules use, which is also where a
/// character is checked.
/// </para>
/// <para>
/// Every default is empty or zero, never a real character's value, as in
/// <c>EnemyData</c>. Resources are shared between everything that loads them, so this
/// holds what the character is and never what changes while playing: the current level,
/// experience and HP belong to the party.
/// </para>
/// </remarks>
[GlobalClass]
public partial class CharacterData : Resource
{
	/// <summary>
	/// What the party, the dialogue and a save call the character: a lowercase word such
	/// as <c>demo_hero</c>, unique among the characters. Unlike <see cref="NameKey"/>, it
	/// never changes with the text.
	/// </summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>
	/// The key of the character's name, never the name itself. It is <c>speaker.&lt;Id&gt;</c>:
	/// a party member is always someone who speaks, so the name over their lines and
	/// the one on the status screen are written once, in <c>Localization/speaker.*.po</c>.
	/// </summary>
	[Export]
	public string NameKey { get; set; } = "";

	/// <summary>The character's age. In the Real world it stands for the level, and no experience raises it.</summary>
	[Export]
	public int RealAge { get; set; }

	[Export]
	public StatGrowth Real { get; set; }

	[Export]
	public StatGrowth Fantasy { get; set; }

	[Export]
	public StatGrowth Nightmare { get; set; }

	/// <summary>
	/// The plain values the rules read. Throws when something is missing or wrong, naming
	/// the character, so a broken file stops whoever asks for it instead of reaching the party.
	/// </summary>
	public CharacterValues ToValues() => new(
		id: Id,
		nameKey: NameKey,
		realAge: RealAge,
		real: Real?.ToValues(),
		fantasy: Fantasy?.ToValues(),
		nightmare: Nightmare?.ToValues());
}
