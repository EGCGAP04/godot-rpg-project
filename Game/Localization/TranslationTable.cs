using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One translation table in one language, as <see cref="TranslationTableReader"/> builds it
/// from a <c>.po</c> file.
/// </summary>
/// <remarks>
/// Godot loads the same files by itself to show the text; this is what the tests and the
/// validator read, since they run without the engine.
/// </remarks>
/// <param name="Locale">The language the file's header declares, such as <c>es</c>.</param>
/// <param name="Entries">Every entry except the header, in the order of the file.</param>
public sealed record TranslationTable(string Locale, IReadOnlyList<TranslationEntry> Entries)
{
	private readonly Dictionary<string, TranslationEntry> _byKey = Entries.ToDictionary(entry => entry.Key);

	/// <summary>The entry for <paramref name="key"/>, or null when the table does not have it.</summary>
	public TranslationEntry Find(string key) => _byKey.GetValueOrDefault(key);
}

/// <summary>One key and its text.</summary>
/// <param name="Key">The <c>msgid</c>: a key such as <c>ui.combat.attack</c>, never English text.</param>
/// <param name="Text">The <c>msgstr</c>. Empty when nobody has written it yet.</param>
/// <param name="IsFuzzy">
/// Marked <c>#, fuzzy</c>, as Poedit does with a translation it guessed. Godot ignores
/// these entries and shows the raw key instead.
/// </param>
/// <param name="Line">Where the entry's <c>msgid</c> is in the file, for messages.</param>
public sealed record TranslationEntry(string Key, string Text, bool IsFuzzy, int Line);
