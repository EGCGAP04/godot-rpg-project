using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks the keys the game's code, scenes and resources name against the shared tables in
/// <c>Localization/</c>. A misspelt key shows raw on screen, and nothing else catches it:
/// pseudolocalization brackets a raw key just as it brackets a real text.
/// </summary>
/// <remarks>
/// The search is textual: a quoted string that starts with the name of a shared table and
/// a dot, such as <c>"ui.combat.attack"</c>. A key built at run time is invisible to it, so
/// each of those needs a test of its own, like <see cref="EveryWorld_HasAName"/>.
/// </remarks>
public class GameKeysTests
{
	private static readonly string[] Extensions = { ".cs", ".tscn", ".tres" };

	/// <summary>Every key of the shared tables, in the fallback language.</summary>
	private static (IReadOnlySet<string> Names, IReadOnlySet<string> Keys) SharedTables()
	{
		TranslationFile[] tables = ProjectFiles.TranslationFiles()
			.Where(file => file.Folder == LocalizationNames.SharedFolder && file.Locale == LocalizationNames.Fallback)
			.ToArray();

		Assert.NotEmpty(tables);

		return (
			tables.Select(file => file.Name).ToHashSet(),
			tables.SelectMany(file => TranslationTableReader.Read(file).Entries).Select(entry => entry.Key).ToHashSet());
	}

	[Fact]
	public void EveryKeyTheGameNames_HasText()
	{
		(IReadOnlySet<string> names, IReadOnlySet<string> keys) = SharedTables();
		var pattern = new Regex($"\"((?:{string.Join("|", names)})(?:\\.[a-z0-9_]+)+)\"");
		var found = new List<string>();
		var missing = new List<string>();

		IEnumerable<string> files = Directory.EnumerateFiles(ProjectFiles.GameDirectory, "*", SearchOption.AllDirectories)
			.Where(path => Extensions.Contains(Path.GetExtension(path)))
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal);

		foreach (string path in files)
		{
			string[] lines = File.ReadAllLines(path);

			for (int i = 0; i < lines.Length; i++)
			{
				foreach (Match match in pattern.Matches(lines[i]))
				{
					string key = match.Groups[1].Value;
					found.Add(key);

					if (!keys.Contains(key))
						missing.Add($"{Path.GetRelativePath(ProjectFiles.GameDirectory, path)}:{i + 1}: '{key}' is not in any table of {LocalizationNames.SharedFolder}/.");
				}
			}
		}

		// The game names keys in combat and in its resources, so finding none means the
		// search is broken, not that the game is clean.
		Assert.Contains("ui.combat.attack", found);
		Assert.Contains("enemy.strong", found);
		Assert.Empty(missing);
	}

	[Fact]
	public void EveryWorld_HasAName()
	{
		// Main builds these keys from the enum, so the search above cannot see them.
		(_, IReadOnlySet<string> keys) = SharedTables();

		foreach (World world in Enum.GetValues<World>())
			Assert.Contains($"world.{world.ToString().ToLowerInvariant()}", keys);
	}
}
