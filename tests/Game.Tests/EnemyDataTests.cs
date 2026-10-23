using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks that every enemy's <c>.tres</c> writes the values a fight needs: the key of its
/// name and its HP. <c>EnemyData</c>'s defaults are empty and zero, so an enemy missing
/// either would show no name, or lose the fight before it starts.
/// </summary>
/// <remarks>
/// <c>AttackPower</c> is not required: the editor leaves a property out of the file when
/// it equals its default, so a missing <c>AttackPower</c> means 0, which an enemy may have.
/// The enemy's <c>Id</c> is checked by <see cref="GameFlagsTests.EveryEnemy_HasAnIdOfItsOwn"/>.
/// </remarks>
public class EnemyDataTests
{
	[Fact]
	public void EveryEnemy_WritesItsNameAndHp()
	{
		var nameKeyLine = new Regex("^NameKey = \"(.*)\"$", RegexOptions.Multiline);
		var maxHpLine = new Regex("^MaxHp = (-?\\d+)$", RegexOptions.Multiline);
		var enemies = new List<string>();
		var problems = new List<string>();

		IEnumerable<string> paths = Directory.EnumerateFiles(ProjectFiles.GameDirectory, "*.tres", SearchOption.AllDirectories)
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal);

		foreach (string path in paths)
		{
			string text = File.ReadAllText(path);
			string file = Path.GetRelativePath(ProjectFiles.GameDirectory, path);

			if (!text.Contains("script_class=\"EnemyData\""))
				continue;

			enemies.Add(file);
			Match nameKey = nameKeyLine.Match(text);
			Match maxHp = maxHpLine.Match(text);

			if (!nameKey.Success)
				problems.Add($"{file}: no NameKey.");
			else if (!nameKey.Groups[1].Value.StartsWith("enemy.", StringComparison.Ordinal))
				problems.Add($"{file}: the NameKey '{nameKey.Groups[1].Value}' is not in the enemy table.");

			if (!maxHp.Success)
				problems.Add($"{file}: no MaxHp.");
			else if (int.Parse(maxHp.Groups[1].Value) <= 0)
				problems.Add($"{file}: MaxHp is {maxHp.Groups[1].Value}, not above 0.");
		}

		// The two placeholder enemies, so a broken search cannot pass as a game with none.
		Assert.Contains(Path.Combine("Enemy", "weak_enemy.tres"), enemies);
		Assert.Contains(Path.Combine("Enemy", "strong_enemy.tres"), enemies);
		Assert.Empty(problems);
	}
}
