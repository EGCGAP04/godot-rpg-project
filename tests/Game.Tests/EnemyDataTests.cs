using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks every enemy's <c>.tres</c> by reading it as text: an id of its own, the key of
/// its name, and stats and experience that make an enemy the rules accept.
/// </summary>
/// <remarks>
/// <para>
/// <c>EnemyData</c> is a Godot type and cannot be loaded here, so each file is rebuilt as
/// the <see cref="EnemyValues"/> it stands for, through the same checks the game
/// applies. A stat missing from a file is 0, as in the game: the editor leaves out every
/// value equal to its default. Health and experience cannot be 0, so those two a file
/// must write.
/// </para>
/// <para>
/// A name Godot does not know is the mistake a file can hide: it ignores the line, and
/// the stat is 0. So every name a file writes must be one <c>EnemyData</c> exports. That
/// <c>ToValues</c> hands each number to the right stat is not covered here and was
/// checked with a headless run.
/// </para>
/// </remarks>
public class EnemyDataTests
{
	private static readonly string[] Stats = { "HealthPoints", "AttackDamage", "Armor", "ManaPoints", "Agility", "Luck", "HitRate" };

	private static readonly string[] ExpectedProperties = new[] { "Id", "NameKey" }.Concat(Stats).Append("Experience").ToArray();

	/// <summary>The exported properties of a resource, of any type, read from its source.</summary>
	public static string[] ExportedProperties(string source) =>
		Regex.Matches(source, @"\[Export\]\s+public\s+[\w.<>]+\s+(\w+)\s*\{")
			.Select(match => match.Groups[1].Value)
			.ToArray();

	/// <summary>The enemy a file stands for, or the reasons it is not one.</summary>
	public static (EnemyValues Enemy, List<string> Problems) Read(string text)
	{
		var problems = new List<string>();
		CharacterDataTests.Section resource = CharacterDataTests.Sections(text).SingleOrDefault(section => section.Kind == "resource");

		if (resource == null)
			return (null, new List<string> { "no [resource] section." });

		var numbers = new Dictionary<string, long>();

		foreach ((string name, string written) in resource.Values.Where(pair => pair.Key != "script" && !pair.Key.StartsWith("metadata/", StringComparison.Ordinal)))
		{
			if (!ExpectedProperties.Contains(name))
				problems.Add($"it writes '{name}', which EnemyData does not have.");
			else if (name is "Id" or "NameKey")
				continue;
			else if (!long.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
				problems.Add($"{name} is '{written}', not a whole number.");
			else if (name != "Experience" && (number < int.MinValue || number > int.MaxValue))
				problems.Add($"{name} is {written}, which does not fit a stat.");
			else
				numbers.Add(name, number);
		}

		string Text(string name) => Regex.Match(resource.Values.GetValueOrDefault(name, ""), "^\"(.*)\"$").Groups[1].Value;

		if (problems.Count > 0)
			return (null, problems);

		try
		{
			int Stat(string name) => (int)numbers.GetValueOrDefault(name);

			var stats = new BaseStats(Stat("HealthPoints"), Stat("AttackDamage"), Stat("Armor"), Stat("ManaPoints"), Stat("Agility"), Stat("Luck"), Stat("HitRate"));
			return (new EnemyValues(Text("Id"), Text("NameKey"), stats, numbers.GetValueOrDefault("Experience")), problems);
		}
		catch (ArgumentException exception)
		{
			problems.Add(exception.Message);
			return (null, problems);
		}
	}

	/// <summary>Every enemy file of the game, by its path under <c>Game/</c>.</summary>
	public static Dictionary<string, string> EnemyFiles() =>
		Directory.EnumerateFiles(ProjectFiles.GameDirectory, "*.tres", SearchOption.AllDirectories)
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => (File: Path.GetRelativePath(ProjectFiles.GameDirectory, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
			.Where(file => file.Text.Contains("script_class=\"EnemyData\""))
			.ToDictionary(file => file.File, file => file.Text);

	private static string EnemyFile(params string[] lines) =>
		"[gd_resource type=\"Resource\" script_class=\"EnemyData\" format=3 uid=\"uid://abc\"]\n\n"
		+ "[ext_resource type=\"Script\" uid=\"uid://def\" path=\"res://Enemy/EnemyData.cs\" id=\"1_x\"]\n\n"
		+ "[resource]\nscript = ExtResource(\"1_x\")\n"
		+ string.Join("\n", lines)
		+ "\nmetadata/_custom_type_script = \"uid://def\"\n";

	[Fact]
	public void TheResource_ExportsTheValuesThisTestKnows()
	{
		// When this fails, a property was added to or removed from EnemyData: update the
		// list above and EnemyData.ToValues together.
		Assert.Equal(ExpectedProperties, ExportedProperties(ProjectFiles.ReadText("Enemy", "EnemyData.cs")));
	}

	[Fact]
	public void EveryEnemy_IsOneTheRulesAccept_WithAnIdOfItsOwn()
	{
		Dictionary<string, string> files = EnemyFiles();
		var problems = new List<string>();
		var ids = new Dictionary<string, string>();

		foreach ((string file, string text) in files)
		{
			(EnemyValues enemy, List<string> found) = Read(text);
			problems.AddRange(found.Select(problem => $"{file}: {problem}"));

			if (enemy != null && !ids.TryAdd(enemy.Id, file))
				problems.Add($"{file}: the id '{enemy.Id}' is already {ids[enemy.Id]}'s.");
		}

		// The two first placeholders, so a broken search cannot pass as a game with no enemies.
		Assert.Contains("Enemy/weak_enemy.tres", files.Keys);
		Assert.Contains("Enemy/strong_enemy.tres", files.Keys);
		Assert.Empty(problems);
	}

	[Fact]
	public void Read_BuildsTheEnemyAFileStandsFor()
	{
		(EnemyValues enemy, List<string> problems) = Read(EnemyFile(
			"Id = \"demo_slime\"",
			"NameKey = \"enemy.demo_slime\"",
			"HealthPoints = 12",
			"AttackDamage = 3",
			"Armor = 4",
			"ManaPoints = 5",
			"Agility = 6",
			"Luck = 7",
			"HitRate = 80",
			"Experience = 9"));

		Assert.Empty(problems);
		Assert.Equal(new EnemyValues("demo_slime", "enemy.demo_slime", new BaseStats(12, 3, 4, 5, 6, 7, 80), 9), enemy);
	}

	[Fact]
	public void Read_TakesAStatTheFileLeavesOut_AsZero()
	{
		(EnemyValues enemy, List<string> problems) = Read(EnemyFile("Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 10", "Experience = 5"));

		Assert.Empty(problems);
		Assert.Equal(new BaseStats(10, 0, 0, 0, 0, 0, 0), enemy.Stats);
	}

	public static TheoryData<string, string[]> FilesThatAreNotAnEnemy() => new()
	{
		{ "a misspelt stat", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 10", "Agilty = 5", "Experience = 5" } },
		{ "a stat of the old enemy", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "MaxHp = 10", "Experience = 5" } },
		{ "no health", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "AttackDamage = 3", "Experience = 5" } },
		{ "no experience", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 10" } },
		{ "no id", new[] { "NameKey = \"enemy.weak\"", "HealthPoints = 10", "Experience = 5" } },
		{ "no name key", new[] { "Id = \"weak\"", "HealthPoints = 10", "Experience = 5" } },
		{ "another enemy's name key", new[] { "Id = \"weak\"", "NameKey = \"enemy.strong\"", "HealthPoints = 10", "Experience = 5" } },
		{ "a negative stat", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 10", "Armor = -1", "Experience = 5" } },
		{ "a stat with a fraction", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 10.5", "Experience = 5" } },
		{ "a stat too large", new[] { "Id = \"weak\"", "NameKey = \"enemy.weak\"", "HealthPoints = 4294967306", "Experience = 5" } }
	};

	[Theory]
	[MemberData(nameof(FilesThatAreNotAnEnemy))]
	public void Read_RefusesAFileThatIsNotAnEnemy(string what, string[] lines)
	{
		(EnemyValues enemy, List<string> problems) = Read(EnemyFile(lines));

		Assert.NotNull(what);
		Assert.Null(enemy);
		Assert.Single(problems);
	}

	[Fact]
	public void Read_RefusesAFileWithNoResource()
	{
		(EnemyValues enemy, List<string> problems) = Read("[gd_resource type=\"Resource\"]\n");

		Assert.Null(enemy);
		Assert.Single(problems);
	}

	[Fact]
	public void ExportedProperties_FindsEveryType()
	{
		// The search is by pattern, and a pattern that matches nothing passes for a
		// resource with nothing exported. This sample pins what it finds.
		string source = "\t[Export]\n\tpublic string First { get; set; } = \"\";\n\n\t/// <summary>x</summary>\n\t[Export]\n\tpublic long Second { get; set; }\n\tpublic int NotExported { get; set; }\n";

		Assert.Equal(new[] { "First", "Second" }, ExportedProperties(source));
	}
}
