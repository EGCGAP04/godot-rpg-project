using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks what the game's code and resources hand to the decision registry: every flag
/// the code names, and the id of every enemy, whose fights the registry records.
/// </summary>
/// <remarks>
/// The flag search is textual, like <see cref="GameKeysTests"/>, but it looks for calls
/// such as <c>SetFlag("world.fantasy_closed", true)</c> rather than for any string that
/// starts like a flag: <c>world.</c> is both a group of flags and the prefix of the
/// worlds' names in the translation tables. A name built at run time is invisible to it,
/// and so are <see cref="DecisionState.Wins"/> and <see cref="DecisionState.Losses"/>,
/// which <see cref="DecisionStateTests.New_WorksWithTheProjectCatalogue"/> covers.
/// </remarks>
public class GameFlagsTests
{
	/// <summary>
	/// A call that names a flag, with the method in group 1 and the name in group 2.
	/// <see cref="GameKeysTests"/> uses it too, to skip those names.
	/// </summary>
	public static readonly Regex FlagCall = new("\\b(GetFlag|GetCounter|SetFlag|AddToCounter)\\(\\s*\"([^\"]+)\"");

	private static IEnumerable<string> GameFiles(string extension) =>
		Directory.EnumerateFiles(ProjectFiles.GameDirectory, $"*{extension}", SearchOption.AllDirectories)
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal);

	[Fact]
	public void EveryFlagTheCodeNames_IsDeclaredWithItsType()
	{
		FlagCatalogue catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
		var problems = new List<string>();
		var searched = new List<string>();

		foreach (string path in GameFiles(".cs"))
		{
			searched.Add(Path.GetRelativePath(ProjectFiles.GameDirectory, path).Replace('\\', '/'));
			string[] lines = File.ReadAllLines(path);

			for (int i = 0; i < lines.Length; i++)
			{
				foreach (Match match in FlagCall.Matches(lines[i]))
				{
					string method = match.Groups[1].Value;
					string name = match.Groups[2].Value;
					FlagType needed = method is "GetFlag" or "SetFlag" ? FlagType.Bool : FlagType.Counter;
					string problem = FlagUses.Problem(catalogue, name, needed, inEffect: method is "SetFlag" or "AddToCounter");

					if (problem != null)
						problems.Add($"{Path.GetRelativePath(ProjectFiles.GameDirectory, path)}:{i + 1}: {method}: {problem}.");
				}
			}
		}

		// No code names a flag yet: the demo's flags are set by its conversations. So the
		// search cannot be required to find a call. Instead it must have read the code that
		// would hold one, and the pattern must match the shape of a call, or a broken
		// search would pass as a clean game.
		Assert.Contains("Main.cs", searched);
		Assert.Contains("Autoloads/DecisionRegistry.cs", searched);
		Assert.Equal("world.fantasy_closed", FlagCall.Match("state.SetFlag(\"world.fantasy_closed\", true);").Groups[2].Value);
		Assert.Empty(problems);
	}

	[Fact]
	public void EveryEnemy_HasAnIdOfItsOwn()
	{
		var idLine = new Regex("^Id = \"(.*)\"$", RegexOptions.Multiline);
		var ids = new Dictionary<string, string>();
		var problems = new List<string>();

		foreach (string path in GameFiles(".tres"))
		{
			string text = File.ReadAllText(path);
			string file = Path.GetRelativePath(ProjectFiles.GameDirectory, path);

			if (!text.Contains("script_class=\"EnemyData\""))
				continue;

			Match match = idLine.Match(text);

			if (!match.Success)
				problems.Add($"{file}: no Id.");
			else if (!DialogueNames.IsWord(match.Groups[1].Value))
				problems.Add($"{file}: the Id '{match.Groups[1].Value}' is not a lowercase word.");
			else if (!ids.TryAdd(match.Groups[1].Value, file))
				problems.Add($"{file}: the Id '{match.Groups[1].Value}' is already {ids[match.Groups[1].Value]}'s.");
		}

		// The two placeholder enemies, so a broken search cannot pass as a game with none.
		Assert.Contains("weak", ids.Keys);
		Assert.Contains("strong", ids.Keys);
		Assert.Empty(problems);
	}
}
