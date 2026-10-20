using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Checks every translation table of the project together, and returns every problem it
/// finds instead of stopping at the first.
/// </summary>
/// <remarks>
/// <para>
/// The reader already rejects a file that is not well formed. The validator adds what
/// needs the file's name, or more than one file:
/// </para>
/// <list type="bullet">
/// <item>the file is named <c>&lt;name&gt;.&lt;locale&gt;.po</c>, and its header declares that locale;</item>
/// <item>every key starts with the table's name, and has a text that is not fuzzy;</item>
/// <item>every table exists in every language, with the same keys and the same placeholders;</item>
/// <item>no two tables share a name, since Godot merges every table of a language into one;</item>
/// <item><c>project.godot</c> registers exactly the tables there are, since Godot ignores the rest.</item>
/// </list>
/// <para>
/// Godot shows the raw key for any text it cannot find, so each of these would otherwise
/// turn up on screen in one language and not in the other. A test runs it over the whole
/// project. Whether each conversation has its text is <see cref="DialogueValidator"/>'s.
/// </para>
/// </remarks>
public static class LocalizationValidator
{
	private const string ResourcePrefix = "res://";

	/// <summary>Checks the tables, and their registration in <c>project.godot</c>.</summary>
	/// <param name="files">Every <c>.po</c> file of the project.</param>
	/// <param name="registered">
	/// The paths <c>project.godot</c> registers, as <see cref="RegisteredTables"/> reads them.
	/// </param>
	/// <returns>
	/// Every problem found, each a sentence that starts with the file it is in. Empty when
	/// everything is valid.
	/// </returns>
	public static IReadOnlyList<string> Validate(IEnumerable<TranslationFile> files, IEnumerable<string> registered)
	{
		ArgumentNullException.ThrowIfNull(files);
		ArgumentNullException.ThrowIfNull(registered);

		var problems = new List<string>();
		List<TranslationFile> fileList = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
		var tables = new List<(TranslationFile File, TranslationTable Table)>();

		// Files that could not be read still count as there, so the problem is said once:
		// a table with a broken header is not also reported missing.
		var present = new List<TranslationFile>();

		foreach (TranslationFile file in fileList)
		{
			if (!LocalizationNames.IsTableFileName(System.IO.Path.GetFileName(file.Path)))
			{
				problems.Add($"{file.Path}: a table's file is named <name>.<locale>.po, with a name made of lowercase letters, digits and underscores, and a locale among {string.Join(", ", LocalizationNames.Locales)}.");
				continue;
			}

			present.Add(file);

			try
			{
				TranslationTable table = TranslationTableReader.Read(file);
				tables.Add((file, table));
				CheckTable(file, table, problems);
			}
			catch (DataFileException exception)
			{
				problems.Add(exception.Message);
			}
		}

		foreach (IGrouping<string, TranslationFile> sameName in present.GroupBy(file => file.Name))
		{
			List<string> folders = sameName.Select(file => file.Folder).Distinct().ToList();

			if (folders.Count > 1)
			{
				string where = string.Join(", ", folders.Select(folder => $"'{folder}'"));
				problems.Add($"{sameName.Key}: there are tables with this name in {where}, and their keys would mix, since Godot merges every table of a language into one.");
			}
		}

		foreach (IGrouping<(string Folder, string Name), TranslationFile> group in present.GroupBy(file => (file.Folder, file.Name)))
			CheckLanguages(group.Key.Folder, group.Key.Name, group, tables, problems);

		CheckRegistration(fileList, registered, problems);
		return problems;
	}

	/// <summary>
	/// The paths inside <c>Game/</c> of the tables <c>project.godot</c> registers, read from
	/// its <c>locale/translations</c> setting. Empty when there is none.
	/// </summary>
	/// <param name="projectGodot">The text of <c>project.godot</c>.</param>
	public static IReadOnlyList<string> RegisteredTables(string projectGodot)
	{
		ArgumentNullException.ThrowIfNull(projectGodot);

		bool inSection = false;

		foreach (string rawLine in projectGodot.Split('\n'))
		{
			string line = rawLine.Trim();

			if (line.StartsWith('['))
				inSection = line == "[internationalization]";
			else if (inSection && line.StartsWith("locale/translations="))
				return Regex.Matches(line, "\"([^\"]*)\"")
					.Select(match => match.Groups[1].Value)
					.Select(path => path.StartsWith(ResourcePrefix) ? path[ResourcePrefix.Length..] : path)
					.ToList();
		}

		return Array.Empty<string>();
	}

	private static void CheckTable(TranslationFile file, TranslationTable table, List<string> problems)
	{
		if (table.Locale != file.Locale)
			problems.Add($"{file.Path}: line 1: the header says '{table.Locale}', but the file name says '{file.Locale}'.");

		foreach (TranslationEntry entry in table.Entries)
		{
			string where = $"{file.Path}: line {entry.Line}";

			if (!entry.Key.StartsWith(file.Name + "."))
				problems.Add($"{where}: '{entry.Key}' does not start with '{file.Name}.', the table's name.");

			if (entry.IsFuzzy)
				problems.Add($"{where}: '{entry.Key}' is marked fuzzy, and Godot ignores fuzzy entries; check the text and remove the flag.");
			else if (entry.Text.Length == 0)
				problems.Add($"{where}: '{entry.Key}' has no text.");
		}
	}

	/// <summary>Checks that a table exists in every language, with the same keys and placeholders.</summary>
	private static void CheckLanguages(string folder, string name, IEnumerable<TranslationFile> group,
		List<(TranslationFile File, TranslationTable Table)> tables, List<string> problems)
	{
		string prefix = folder.Length == 0 ? name : $"{folder}/{name}";
		HashSet<string> locales = group.Select(file => file.Locale).ToHashSet();

		foreach (string missing in LocalizationNames.Locales.Where(locale => !locales.Contains(locale)))
			problems.Add($"{prefix}.{missing}.po: there is no such file, and every table needs one per language.");

		List<(TranslationFile File, TranslationTable Table)> read = tables
			.Where(pair => pair.File.Folder == folder && pair.File.Name == name)
			.ToList();

		// In the order the keys first appear, so the problems read in the order of the files.
		IEnumerable<string> keys = read.SelectMany(pair => pair.Table.Entries.Select(entry => entry.Key)).Distinct();

		foreach (string key in keys)
		{
			var texts = new List<(string Locale, string Text)>();

			foreach ((TranslationFile file, TranslationTable table) in read)
			{
				TranslationEntry entry = table.Find(key);

				if (entry == null)
				{
					string other = read.First(pair => pair.Table.Find(key) != null).File.Path;
					problems.Add($"{file.Path}: '{key}' is missing; {other} has it.");
				}
				else
				{
					texts.Add((file.Locale, entry.Text));
				}
			}

			var shapes = texts
				.Select(text => (text.Locale, Names: string.Join(", ", TextTemplate.Placeholders(text.Text).Order(StringComparer.Ordinal))))
				.ToList();

			if (shapes.Select(shape => shape.Names).Distinct().Count() > 1)
			{
				string detail = string.Join("; ", shapes.Select(shape => $"{shape.Locale} has {(shape.Names.Length == 0 ? "none" : $"{{{shape.Names}}}")}"));
				problems.Add($"{prefix}.*.po: '{key}' needs the same placeholders in every language: {detail}.");
			}
		}
	}

	private static void CheckRegistration(List<TranslationFile> files, IEnumerable<string> registered, List<string> problems)
	{
		HashSet<string> onDisk = files.Select(file => file.Path.Replace('\\', '/')).ToHashSet();
		List<string> registeredList = registered.ToList();

		foreach (string path in onDisk.Where(path => !registeredList.Contains(path)))
			problems.Add($"{path}: project.godot does not register it, so Godot never shows its text.");

		foreach (string path in registeredList.Where(path => !onDisk.Contains(path)))
			problems.Add($"{path}: project.godot registers it, but there is no such file.");

		foreach (string path in registeredList.GroupBy(path => path).Where(group => group.Count() > 1).Select(group => group.Key))
			problems.Add($"{path}: project.godot registers it more than once.");
	}
}
