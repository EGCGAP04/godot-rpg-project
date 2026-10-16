using System.Linq;

namespace GodotRPGProject.Tests;

/// <summary>Writes small translation tables for tests, so each one only says what it is about.</summary>
public static class PoText
{
	/// <summary>A table in <paramref name="locale"/>: a header, then each key and its text.</summary>
	public static string Table(string locale, params (string Key, string Text)[] entries) =>
		$"msgid \"\"\nmsgstr \"Language: {locale}\\n\"\n"
		+ string.Concat(entries.Select(entry => $"\nmsgid \"{entry.Key}\"\nmsgstr \"{entry.Text}\"\n"));

	/// <summary>A table file whose header declares the language its name says.</summary>
	public static TranslationFile File(string path, params (string Key, string Text)[] entries) =>
		new(path, Table(new TranslationFile(path, "").Locale, entries));
}
