using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Reads one translation table from the text of its gettext <c>.po</c> file, or says where
/// the file is wrong.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free, like <see cref="ConversationReader"/>: it takes the file's text, not a path.
/// </para>
/// <para>
/// It reads the part of the format the game uses, and refuses the rest instead of skipping
/// it, as <see cref="StrictJson"/> does for JSON. A file is a header followed by entries,
/// separated by blank lines:
/// </para>
/// <code>
/// msgid ""
/// msgstr ""
/// "Language: es\n"
///
/// #. A note for whoever translates it.
/// msgid "ui.combat.attack"
/// msgstr "Atacar"
/// </code>
/// <list type="bullet">
/// <item>
/// The header is the first entry, with an empty <c>msgid</c>, and it has to declare
/// <c>Language</c>. Godot loads a table without one as English, and says nothing.
/// </item>
/// <item>
/// A line starting with <c>#</c> is a comment. <c>#, fuzzy</c> marks the entry after it,
/// which Godot ignores; the validator reports those.
/// </item>
/// <item>
/// A text can go on across several quoted lines, which are joined. Inside the quotes,
/// <c>\n</c>, <c>\t</c>, <c>\"</c> and <c>\\</c> are the only escapes.
/// </item>
/// <item>
/// Contexts (<c>msgctxt</c>) and plurals (<c>msgid_plural</c>, <c>msgstr[n]</c>) are
/// refused: nothing needs them yet, and a reader that ignored them would read a
/// different table from the one Godot shows.
/// </item>
/// </list>
/// </remarks>
public static class TranslationTableReader
{
	private static readonly char[] Whitespace = { ' ', '\t' };

	/// <param name="file">The file, as messages should name it.</param>
	/// <param name="text">The text of the file.</param>
	/// <exception cref="DataFileException">The file is not a table this game reads.</exception>
	public static TranslationTable Read(string file, string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		var entries = new List<RawEntry>();
		RawEntry current = null;
		bool nextIsFuzzy = false;

		// Godot accepts a byte order mark and Windows line endings, so the reader does too.
		string[] lines = text.TrimStart('\uFEFF').Split('\n');

		for (int i = 0; i < lines.Length; i++)
		{
			int number = i + 1;
			string line = lines[i].Trim();
			string where = $"line {number}";

			if (line.Length == 0)
			{
				if (current is { Text: null })
					throw new DataFileException(file, $"line {current.Line}", $"msgid \"{current.Key}\" has no msgstr");

				// The entry is over, so a quoted line after this one has nothing to continue.
				current = null;
				continue;
			}

			if (line.StartsWith('#'))
			{
				if (current is { Text: null })
					throw new DataFileException(file, where, "a comment cannot go between a msgid and its msgstr");

				if (line.StartsWith("#,") && HasFuzzyFlag(line))
					nextIsFuzzy = true;

				current = null;
				continue;
			}

			if (line.StartsWith('"'))
			{
				if (current == null)
					throw new DataFileException(file, where, "a quoted line continues a msgid or a msgstr, and there is none before it");

				string more = Unquote(line, file, where);

				if (current.Text == null)
					current.Key += more;
				else
					current.Text += more;

				continue;
			}

			int space = line.IndexOfAny(Whitespace);
			string keyword = space < 0 ? line : line[..space];
			string rest = line[keyword.Length..].TrimStart();

			switch (keyword)
			{
				case "msgid":
					if (current is { Text: null })
						throw new DataFileException(file, $"line {current.Line}", $"msgid \"{current.Key}\" has no msgstr");

					current = new RawEntry { Key = Unquote(rest, file, where), Line = number, IsFuzzy = nextIsFuzzy };
					entries.Add(current);
					nextIsFuzzy = false;
					break;

				case "msgstr":
					if (current is not { Text: null })
						throw new DataFileException(file, where, "a msgstr needs a msgid right before it");

					current.Text = Unquote(rest, file, where);
					break;

				case "msgctxt":
				case "msgid_plural":
					throw new DataFileException(file, where, $"{keyword} is not supported yet");

				default:
					if (keyword.StartsWith("msgstr["))
						throw new DataFileException(file, where, "plurals (msgstr[n]) are not supported yet");

					throw new DataFileException(file, where, $"'{keyword}' is not something a .po file has");
			}
		}

		if (current is { Text: null })
			throw new DataFileException(file, $"line {current.Line}", $"msgid \"{current.Key}\" has no msgstr");

		if (entries.Count == 0 || entries[0].Key.Length != 0)
			throw new DataFileException(file, "", "the first entry must be the header, with msgid \"\"");

		string locale = ReadLocale(file, entries[0].Text);
		var tableEntries = new List<TranslationEntry>();
		var seen = new HashSet<string>();

		foreach (RawEntry entry in entries.GetRange(1, entries.Count - 1))
		{
			string where = $"line {entry.Line}";

			if (entry.Key.Length == 0)
				throw new DataFileException(file, where, "only the header has an empty msgid");

			if (!IsKey(entry.Key))
				throw new DataFileException(file, where, $"'{entry.Key}' is not a key: keys are words joined by dots, such as ui.combat.attack");

			if (!seen.Add(entry.Key))
				throw new DataFileException(file, where, $"'{entry.Key}' is in the file twice");

			tableEntries.Add(new TranslationEntry(entry.Key, entry.Text, entry.IsFuzzy, entry.Line));
		}

		return new TranslationTable(locale, tableEntries);
	}

	/// <summary>
	/// Whether <paramref name="key"/> is words of the dialogue format joined by dots, at least
	/// two of them, since every key starts with the name of its table.
	/// </summary>
	public static bool IsKey(string key)
	{
		string[] parts = key.Split('.');
		return parts.Length >= 2 && Array.TrueForAll(parts, DialogueNames.IsWord);
	}

	private static bool HasFuzzyFlag(string line)
	{
		foreach (string flag in line[2..].Split(','))
		{
			if (flag.Trim() == "fuzzy")
				return true;
		}

		return false;
	}

	/// <summary>Reads <c>Language</c> out of the header, whose lines are <c>Name: value</c>.</summary>
	private static string ReadLocale(string file, string header)
	{
		foreach (string line in header.Split('\n'))
		{
			int colon = line.IndexOf(':');

			if (colon > 0 && line[..colon].Trim() == "Language")
			{
				string locale = line[(colon + 1)..].Trim();

				if (locale.Length > 0)
					return locale;
			}
		}

		throw new DataFileException(file, "line 1", "the header declares no Language, and Godot would load the table as English");
	}

	/// <summary>The text of a quoted string, with its escapes resolved.</summary>
	private static string Unquote(string quoted, string file, string where)
	{
		if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"')
			throw new DataFileException(file, where, "expected a text between double quotes");

		var text = new StringBuilder();

		for (int i = 1; i < quoted.Length - 1; i++)
		{
			char character = quoted[i];

			if (character == '"')
				throw new DataFileException(file, where, "a double quote inside a text is written \\\"");

			if (character != '\\')
			{
				text.Append(character);
				continue;
			}

			// The closing quote is never part of an escape, so a text ending in a lone
			// backslash runs out here.
			if (i + 1 == quoted.Length - 1)
				throw new DataFileException(file, where, "a backslash at the end of a text escapes nothing");

			char escaped = quoted[++i];

			text.Append(escaped switch
			{
				'n' => '\n',
				't' => '\t',
				'"' => '"',
				'\\' => '\\',
				_ => throw new DataFileException(file, where, $"'\\{escaped}' is not an escape; the escapes are \\n, \\t, \\\" and \\\\")
			});
		}

		return text.ToString();
	}

	/// <summary>An entry while it is being read, before the header and the keys are checked.</summary>
	private sealed class RawEntry
	{
		public string Key;
		public string Text;
		public bool IsFuzzy;
		public int Line;
	}
}
