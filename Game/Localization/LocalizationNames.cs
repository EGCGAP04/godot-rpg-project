using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The naming rules of the translation tables in one place: the languages, where the
/// shared tables live, and what a table's file is called.
/// </summary>
/// <remarks>
/// Every table is a file <c>&lt;name&gt;.&lt;locale&gt;.po</c>, and every key in it starts
/// with <c>&lt;name&gt;.</c>. A conversation's table sits beside its JSON and is named after
/// it; the rest live in <see cref="SharedFolder"/>, one per kind of key: <c>ui</c> for the
/// interface, and one per kind of content, such as <c>enemy</c> or <c>speaker</c>.
/// </remarks>
public static class LocalizationNames
{
	/// <summary>Every language the game ships in. Each table exists once per language.</summary>
	public static readonly IReadOnlyList<string> Locales = new[] { "en", "es" };

	/// <summary>
	/// The language Godot falls back to when the system's has no table, and when a key is
	/// missing from the current one. It is Godot's default, so <c>project.godot</c> does not
	/// repeat it.
	/// </summary>
	public const string Fallback = "en";

	/// <summary>
	/// The folder, inside <c>Game/</c>, of the tables that belong to no conversation. Their
	/// names are the key prefixes no conversation may take.
	/// </summary>
	public const string SharedFolder = "Localization";

	/// <summary>The shared table that holds the names of the people who speak in conversations.</summary>
	public const string SpeakerTable = "speaker";

	/// <summary>Whether <paramref name="fileName"/> is a table's: a word, a known language and <c>.po</c>.</summary>
	public static bool IsTableFileName(string fileName)
	{
		string[] parts = fileName.Split('.');

		return parts.Length == 3
			&& DialogueNames.IsWord(parts[0])
			&& Locales.Contains(parts[1])
			&& parts[2] == "po";
	}
}
