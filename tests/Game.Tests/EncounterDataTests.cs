using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks every encounter's <c>.tres</c> by reading it as text: an id of its own, which
/// names the file, and from one to four enemies that are enemy files of the game. It
/// also checks that every enemy standing on a map carries an encounter.
/// </summary>
/// <remarks>
/// <c>EncounterData</c> is a Godot type and cannot be loaded here, so each file is
/// rebuilt as the <see cref="EncounterValues"/> it stands for, from the enemies
/// <see cref="EnemyDataTests"/> reads.
/// </remarks>
public class EncounterDataTests
{
	private const string Folder = "Enemy/Encounters/";

	/// <summary>The encounter a file stands for, or the reasons it is not one.</summary>
	/// <param name="enemies">Every enemy file of the game, by its path under <c>Game/</c>.</param>
	public static (EncounterValues Encounter, List<string> Problems) Read(string text, IReadOnlyDictionary<string, string> enemies)
	{
		var problems = new List<string>();
		List<CharacterDataTests.Section> sections = CharacterDataTests.Sections(text);
		CharacterDataTests.Section resource = sections.SingleOrDefault(section => section.Kind == "resource");

		if (resource == null)
			return (null, new List<string> { "no [resource] section." });

		foreach (string name in resource.Values.Keys.Where(key => key != "script" && !key.StartsWith("metadata/", StringComparison.Ordinal)))
		{
			if (name is not ("Id" or "Enemies" or "CannotFlee"))
				problems.Add($"it writes '{name}', which EncounterData does not have.");
		}

		Dictionary<string, string> paths = sections
			.Where(section => section.Kind == "ext_resource" && section.Attributes.ContainsKey("id") && section.Attributes.ContainsKey("path"))
			.ToDictionary(section => section.Attributes["id"], section => section.Attributes["path"]);

		Match list = Regex.Match(resource.Values.GetValueOrDefault("Enemies", ""), @"^Array\[ExtResource\(""[^""]+""\)\]\(\[(.*)\]\)$");
		var group = new List<EnemyValues>();

		if (!list.Success)
			problems.Add("it has no list of enemies.");

		foreach (string entry in list.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			Match reference = Regex.Match(entry, @"^ExtResource\(""([^""]+)""\)$");
			string path = reference.Success ? paths.GetValueOrDefault(reference.Groups[1].Value, "") : "";
			string file = path.StartsWith("res://", StringComparison.Ordinal) ? path["res://".Length..] : "";

			if (!enemies.TryGetValue(file, out string enemyText))
			{
				problems.Add($"its entry '{entry}' is not an enemy file of the game.");
				continue;
			}

			(EnemyValues enemy, List<string> found) = EnemyDataTests.Read(enemyText);

			if (enemy == null)
				problems.Add($"its enemy {file} is not one: {string.Join(" ", found)}");
			else
				group.Add(enemy);
		}

		// Godot writes a bool as true or false, and leaves it out at its default, false.
		bool cannotFlee = false;

		if (resource.Values.TryGetValue("CannotFlee", out string flee))
		{
			if (flee is "true" or "false")
				cannotFlee = flee == "true";
			else
				problems.Add($"it writes CannotFlee as {flee}, which is neither true nor false.");
		}

		if (problems.Count > 0)
			return (null, problems);

		try
		{
			string id = Regex.Match(resource.Values.GetValueOrDefault("Id", ""), "^\"(.*)\"$").Groups[1].Value;
			return (new EncounterValues(id, group, cannotFlee), problems);
		}
		catch (ArgumentException exception)
		{
			problems.Add(exception.Message);
			return (null, problems);
		}
	}

	private static Dictionary<string, string> GameFiles(string pattern, Func<string, bool> keep) =>
		Directory.EnumerateFiles(ProjectFiles.GameDirectory, pattern, SearchOption.AllDirectories)
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => (File: Path.GetRelativePath(ProjectFiles.GameDirectory, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
			.Where(file => keep(file.Text))
			.ToDictionary(file => file.File, file => file.Text);

	/// <summary>Every encounter file of the game, by its path under <c>Game/</c>.</summary>
	public static Dictionary<string, string> EncounterFiles() =>
		GameFiles("*.tres", text => text.Contains("script_class=\"EncounterData\""));

	/// <summary>
	/// The nodes of a scene that are instances of the enemy scene, each with whether it
	/// sets an encounter that is a file of <paramref name="encounters"/>.
	/// </summary>
	public static List<(string Node, bool HasEncounter)> EnemiesOnAMap(string scene, IReadOnlyCollection<string> encounters)
	{
		List<CharacterDataTests.Section> sections = CharacterDataTests.Sections(scene);

		Dictionary<string, string> paths = sections
			.Where(section => section.Kind == "ext_resource" && section.Attributes.ContainsKey("id") && section.Attributes.ContainsKey("path"))
			.ToDictionary(section => section.Attributes["id"], section => section.Attributes["path"]);

		var found = new List<(string, bool)>();

		foreach (Match node in Regex.Matches(scene, @"^\[node name=""([^""]+)""[^\]]*instance=ExtResource\(""([^""]+)""\)\]\n((?:(?!\[).*\n?)*)", RegexOptions.Multiline))
		{
			if (paths.GetValueOrDefault(node.Groups[2].Value) != "res://Enemy/enemy.tscn")
				continue;

			Match encounter = Regex.Match(node.Groups[3].Value, @"^Encounter = ExtResource\(""([^""]+)""\)$", RegexOptions.Multiline);
			string path = encounter.Success ? paths.GetValueOrDefault(encounter.Groups[1].Value, "") : "";
			found.Add((node.Groups[1].Value, path.StartsWith("res://", StringComparison.Ordinal) && encounters.Contains(path["res://".Length..])));
		}

		return found;
	}

	/// <summary>
	/// Whether a scene connects a node's <c>CombatTriggered</c> to the scene's root, the
	/// world, which is what turns a touch into a fight. An enemy added in the editor has no
	/// connection until one is made by hand, and without it touching the enemy does nothing.
	/// </summary>
	public static bool StartsAFight(string scene, string node) =>
		Regex.IsMatch(scene, $@"^\[connection signal=""CombatTriggered"" from=""{Regex.Escape(node)}"" to=""\."" method=""OnCombatTriggered""\]$", RegexOptions.Multiline);

	[Fact]
	public void TheResource_ExportsTheValuesThisTestKnows()
	{
		Assert.Equal(new[] { "Id", "Enemies", "CannotFlee" }, EnemyDataTests.ExportedProperties(ProjectFiles.ReadText("Enemy", "EncounterData.cs")));
	}

	[Fact]
	public void EveryEncounter_IsOneTheRulesAccept_NamedAfterItsId()
	{
		Dictionary<string, string> files = EncounterFiles();
		Dictionary<string, string> enemies = EnemyDataTests.EnemyFiles();
		var problems = new List<string>();

		foreach ((string file, string text) in files)
		{
			(EncounterValues encounter, List<string> found) = Read(text, enemies);
			problems.AddRange(found.Select(problem => $"{file}: {problem}"));

			// The folder holds every encounter and a file is named after its id, so no two share one.
			if (encounter != null && file != $"{Folder}{encounter.Id}.tres")
				problems.Add($"{file}: the id is '{encounter.Id}', so the file should be {Folder}{encounter.Id}.tres.");
		}

		// The demo's groups, so a broken search cannot pass as a game with no encounters.
		Assert.Contains(Folder + "demo_patrol.tres", files.Keys);
		Assert.Contains(Folder + "demo_showcase.tres", files.Keys);
		Assert.Contains(Folder + "demo_wall.tres", files.Keys);
		Assert.Empty(problems);
	}

	[Fact]
	public void TheDemosGroups_HoldTheEnemiesTheyAreMeantToShow()
	{
		Dictionary<string, string> files = EncounterFiles();
		Dictionary<string, string> enemies = EnemyDataTests.EnemyFiles();

		string[] Ids(string id) => Read(files[$"{Folder}{id}.tres"], enemies).Encounter.Enemies.Select(enemy => enemy.Id).ToArray();

		Assert.Equal(new[] { "weak", "demo_clumsy" }, Ids("demo_patrol"));
		Assert.Equal(new[] { "demo_armored", "demo_nimble", "demo_lucky" }, Ids("demo_showcase"));
		Assert.Equal(new[] { "strong", "strong" }, Ids("demo_wall"));
	}

	[Fact]
	public void OnlyTheDemosWall_CannotBeFled()
	{
		Dictionary<string, string> enemies = EnemyDataTests.EnemyFiles();

		Dictionary<string, bool> cannotFlee = EncounterFiles().ToDictionary(file => file.Key, file => Read(file.Value, enemies).Encounter.CannotFlee);

		Assert.Equal(
			new Dictionary<string, bool>
			{
				[$"{Folder}demo_patrol.tres"] = false,
				[$"{Folder}demo_showcase.tres"] = false,
				[$"{Folder}demo_wall.tres"] = true
			},
			cannotFlee);
	}

	[Fact]
	public void EveryEnemyOnAMap_CarriesAnEncounter_AndStartsAFight()
	{
		Dictionary<string, string> encounters = EncounterFiles();
		var onMaps = new List<string>();
		var problems = new List<string>();

		foreach ((string file, string scene) in GameFiles("*.tscn", _ => true))
		{
			foreach ((string node, bool hasEncounter) in EnemiesOnAMap(scene, encounters.Keys))
			{
				onMaps.Add($"{file}:{node}");

				if (!hasEncounter)
					problems.Add($"{file}: the enemy '{node}' carries no encounter of the game.");

				if (!StartsAFight(scene, node))
					problems.Add($"{file}: the enemy '{node}' has its CombatTriggered connected to nothing, so touching it starts no fight. Connect it to the world's OnCombatTriggered.");
			}
		}

		// The Fantasy placeholder's three, so a broken search cannot pass as maps with no enemies.
		Assert.Contains("World/fantasy_world.tscn:Enemy", onMaps);
		Assert.Contains("World/fantasy_world.tscn:StrongEnemy", onMaps);
		Assert.Contains("World/fantasy_world.tscn:ShowcaseEnemy", onMaps);
		Assert.Empty(problems);
	}

	private const string Weak = "[gd_resource type=\"Resource\" script_class=\"EnemyData\" format=3]\n\n[resource]\nId = \"weak\"\nNameKey = \"enemy.weak\"\nHealthPoints = 10\nExperience = 5\n";

	private static readonly Dictionary<string, string> OneEnemy = new() { ["Enemy/weak_enemy.tres"] = Weak };

	private static string EncounterFile(string id, string enemies, string extra = "") =>
		"[gd_resource type=\"Resource\" script_class=\"EncounterData\" format=3]\n\n"
		+ "[ext_resource type=\"Script\" path=\"res://Enemy/EncounterData.cs\" id=\"1_e\"]\n"
		+ "[ext_resource type=\"Script\" path=\"res://Enemy/EnemyData.cs\" id=\"2_d\"]\n"
		+ "[ext_resource type=\"Resource\" path=\"res://Enemy/weak_enemy.tres\" id=\"3_weak\"]\n"
		+ "[ext_resource type=\"Resource\" path=\"res://Enemy/missing_enemy.tres\" id=\"4_missing\"]\n\n"
		+ $"[resource]\nscript = ExtResource(\"1_e\")\nId = \"{id}\"\n{enemies}\n{extra}";

	[Fact]
	public void Read_BuildsTheEncounterAFileStandsFor()
	{
		string file = EncounterFile("demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\"), ExtResource(\"3_weak\")])");

		(EncounterValues encounter, List<string> problems) = Read(file, OneEnemy);

		Assert.Empty(problems);
		Assert.Equal("demo_pair", encounter.Id);
		Assert.Equal(new[] { "weak", "weak" }, encounter.Enemies.Select(enemy => enemy.Id));
		Assert.Equal(10, encounter.Experience);
		Assert.False(encounter.CannotFlee);
	}

	[Theory]
	[InlineData("CannotFlee = true\n", true)]
	[InlineData("CannotFlee = false\n", false)]
	public void Read_TakesWhetherTheGroupCanBeFled(string extra, bool cannotFlee)
	{
		string file = EncounterFile("demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\")])", extra);

		(EncounterValues encounter, List<string> problems) = Read(file, OneEnemy);

		Assert.Empty(problems);
		Assert.Equal(cannotFlee, encounter.CannotFlee);
	}

	public static TheoryData<string, string, string, string> FilesThatAreNotAnEncounter() => new()
	{
		{ "no enemies written", "demo_pair", "", "" },
		{ "an empty list", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([])", "" },
		{ "an enemy that is not a file", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"4_missing\")])", "" },
		{ "an empty entry", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\"), null])", "" },
		{ "five enemies", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\"), ExtResource(\"3_weak\"), ExtResource(\"3_weak\"), ExtResource(\"3_weak\"), ExtResource(\"3_weak\")])", "" },
		{ "an id that is not a word", "Demo Pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\")])", "" },
		{ "a property the resource does not have", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\")])", "CanFlee = true\n" },
		{ "a flight that is not true or false", "demo_pair", "Enemies = Array[ExtResource(\"2_d\")]([ExtResource(\"3_weak\")])", "CannotFlee = 1\n" }
	};

	[Theory]
	[MemberData(nameof(FilesThatAreNotAnEncounter))]
	public void Read_RefusesAFileThatIsNotAnEncounter(string what, string id, string enemies, string extra)
	{
		(EncounterValues encounter, List<string> problems) = Read(EncounterFile(id, enemies, extra), OneEnemy);

		Assert.NotNull(what);
		Assert.Null(encounter);
		Assert.Single(problems);
	}

	[Fact]
	public void EnemiesOnAMap_FindsEachEnemyNode_AndWhetherItCarriesAnEncounter()
	{
		// The search is by pattern, and a pattern that matches nothing passes for a map
		// with nothing wrong. This sample pins what it finds.
		string scene = "[gd_scene format=3]\n\n"
			+ "[ext_resource type=\"PackedScene\" path=\"res://Enemy/enemy.tscn\" id=\"1_enemy\"]\n"
			+ "[ext_resource type=\"PackedScene\" path=\"res://World/world_exit.tscn\" id=\"2_exit\"]\n"
			+ "[ext_resource type=\"Resource\" path=\"res://Enemy/Encounters/demo_pair.tres\" id=\"3_pair\"]\n"
			+ "[ext_resource type=\"Resource\" path=\"res://Enemy/weak_enemy.tres\" id=\"4_weak\"]\n\n"
			+ "[node name=\"World\" type=\"Node2D\"]\n\n"
			+ "[node name=\"With\" parent=\".\" instance=ExtResource(\"1_enemy\")]\nposition = Vector2(1, 2)\nEncounter = ExtResource(\"3_pair\")\n\n"
			+ "[node name=\"Without\" parent=\".\" instance=ExtResource(\"1_enemy\")]\nposition = Vector2(3, 4)\n\n"
			+ "[node name=\"WithAnEnemy\" parent=\".\" instance=ExtResource(\"1_enemy\")]\nEncounter = ExtResource(\"4_weak\")\n\n"
			+ "[node name=\"Exit\" parent=\".\" instance=ExtResource(\"2_exit\")]\nEncounter = ExtResource(\"3_pair\")\n";

		List<(string Node, bool HasEncounter)> found = EnemiesOnAMap(scene, new[] { "Enemy/Encounters/demo_pair.tres" });

		Assert.Equal(new[] { ("With", true), ("Without", false), ("WithAnEnemy", false) }, found);
	}

	[Fact]
	public void StartsAFight_FindsTheConnectionOfThatNodeOnly()
	{
		string scene = "[node name=\"World\" type=\"Node2D\"]\n\n"
			+ "[connection signal=\"CombatTriggered\" from=\"Enemy\" to=\".\" method=\"OnCombatTriggered\"]\n"
			+ "[connection signal=\"ExitTriggered\" from=\"Exit\" to=\".\" method=\"OnExitTriggered\"]\n"
			+ "[connection signal=\"CombatTriggered\" from=\"Elsewhere\" to=\"Other\" method=\"OnCombatTriggered\"]\n";

		Assert.True(StartsAFight(scene, "Enemy"));
		Assert.False(StartsAFight(scene, "Exit"));
		Assert.False(StartsAFight(scene, "Elsewhere"));
		Assert.False(StartsAFight(scene, "Enem"));
		Assert.False(StartsAFight(scene, "Missing"));
	}
}
