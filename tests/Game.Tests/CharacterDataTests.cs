using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks every character's <c>.tres</c> by reading it as text: an id of its own, the
/// key of its name, its age, and stats for the three worlds that make a character the
/// rules accept.
/// </summary>
/// <remarks>
/// <para>
/// <c>CharacterData</c> and <c>StatGrowth</c> are Godot types and cannot be loaded here,
/// so each file is rebuilt as the <see cref="CharacterValues"/> it stands for, through
/// the same checks the game applies. A stat missing from a file is 0, as in the game:
/// the editor leaves out every value equal to its default.
/// </para>
/// <para>
/// A name Godot does not know is the mistake a file can hide: it ignores the line, and
/// the stat is 0. So every name written under a world must be one <c>StatGrowth</c>
/// exports. That <c>ToValues</c> hands each number to the right stat is not covered
/// here and was checked with a headless run.
/// </para>
/// </remarks>
public class CharacterDataTests
{
	private static readonly string[] ExpectedGrowthProperties =
	{
		"HealthPointsBase", "HealthPointsGrowth",
		"AttackDamageBase", "AttackDamageGrowth",
		"ArmorBase", "ArmorGrowth",
		"ManaPointsBase", "ManaPointsGrowth",
		"AgilityBase", "AgilityGrowth",
		"LuckBase", "LuckGrowth",
		"HitRateBase", "HitRateGrowth"
	};

	private static readonly string[] Worlds = { "Real", "Fantasy", "Nightmare" };

	/// <summary>One <c>[heading]</c> of a <c>.tres</c> with the <c>name = value</c> lines under it.</summary>
	public sealed record Section(string Kind, IReadOnlyDictionary<string, string> Attributes, IReadOnlyDictionary<string, string> Values);

	/// <summary>The sections of a <c>.tres</c>, in the order the file writes them.</summary>
	public static List<Section> Sections(string text)
	{
		var sections = new List<Section>();
		Dictionary<string, string> values = null;

		foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
		{
			Match heading = Regex.Match(line, @"^\[(\w+)(.*)\]$");

			if (heading.Success)
			{
				values = new Dictionary<string, string>();
				Dictionary<string, string> attributes = Regex.Matches(heading.Groups[2].Value, "(\\w+)=\"([^\"]*)\"")
					.ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
				sections.Add(new Section(heading.Groups[1].Value, attributes, values));
				continue;
			}

			Match value = Regex.Match(line, @"^([\w/]+) = (.*)$");

			if (value.Success && values != null)
				values.Add(value.Groups[1].Value, value.Groups[2].Value);
		}

		return sections;
	}

	/// <summary>The character a file stands for, or the reasons it is not one.</summary>
	public static (CharacterValues Character, List<string> Problems) Read(string text)
	{
		var problems = new List<string>();
		List<Section> sections = Sections(text);
		Section resource = sections.SingleOrDefault(section => section.Kind == "resource");

		if (resource == null)
			return (null, new List<string> { "no [resource] section." });

		string growthScript = sections
			.Where(section => section.Kind == "ext_resource" && section.Attributes.GetValueOrDefault("path") == "res://Stats/StatGrowth.cs")
			.Select(section => section.Attributes.GetValueOrDefault("id"))
			.FirstOrDefault();

		var growths = new Dictionary<string, StatGrowthValues>();
		var used = new HashSet<string>();

		foreach (string world in Worlds)
		{
			Match reference = Regex.Match(resource.Values.GetValueOrDefault(world, ""), "^SubResource\\(\"(.+)\"\\)$");
			Section growth = reference.Success
				? sections.FirstOrDefault(section => section.Kind == "sub_resource" && section.Attributes.GetValueOrDefault("id") == reference.Groups[1].Value)
				: null;

			if (growth == null)
			{
				problems.Add($"no stats for the {world} world.");
				continue;
			}

			if (!used.Add(reference.Groups[1].Value))
				problems.Add($"the {world} world shares its stats with another world.");

			if (growthScript == null || growth.Values.GetValueOrDefault("script") != $"ExtResource(\"{growthScript}\")")
				problems.Add($"the {world} world's stats are not a StatGrowth.");

			var numbers = new Dictionary<string, double>();

			foreach ((string name, string written) in growth.Values.Where(pair => pair.Key != "script" && !pair.Key.StartsWith("metadata/", StringComparison.Ordinal)))
			{
				if (!ExpectedGrowthProperties.Contains(name))
					problems.Add($"the {world} world writes '{name}', which StatGrowth does not have.");
				else if (!double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
					problems.Add($"the {world} world's {name} is '{written}', not a number.");
				else
					numbers.Add(name, number);
			}

			try
			{
				StatLine Line(string stat) => StatLine.From(numbers.GetValueOrDefault(stat + "Base"), numbers.GetValueOrDefault(stat + "Growth"));

				growths.Add(world, new StatGrowthValues(Line("HealthPoints"), Line("AttackDamage"), Line("Armor"), Line("ManaPoints"), Line("Agility"), Line("Luck"), Line("HitRate")));
			}
			catch (ArgumentException exception)
			{
				problems.Add($"the {world} world: {exception.Message}");
			}
		}

		string Text(string name) => Regex.Match(resource.Values.GetValueOrDefault(name, ""), "^\"(.*)\"$").Groups[1].Value;

		string id = Text("Id");

		if (!int.TryParse(resource.Values.GetValueOrDefault("RealAge", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int realAge))
			problems.Add($"RealAge is '{resource.Values["RealAge"]}', not a whole number.");

		if (Text("NameKey") != $"speaker.{id}")
			problems.Add($"the NameKey is '{Text("NameKey")}', not 'speaker.{id}'.");

		if (problems.Count > 0)
			return (null, problems);

		try
		{
			return (new CharacterValues(id, Text("NameKey"), realAge, growths["Real"], growths["Fantasy"], growths["Nightmare"]), problems);
		}
		catch (ArgumentException exception)
		{
			problems.Add(exception.Message);
			return (null, problems);
		}
	}

	private static Dictionary<string, string> CharacterFiles() =>
		Directory.EnumerateFiles(ProjectFiles.GameDirectory, "*.tres", SearchOption.AllDirectories)
			.Where(path => !Path.GetRelativePath(ProjectFiles.GameDirectory, path).StartsWith(".godot"))
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => (File: Path.GetRelativePath(ProjectFiles.GameDirectory, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
			.Where(file => file.Text.Contains("script_class=\"CharacterData\""))
			.ToDictionary(file => file.File, file => file.Text);

	[Fact]
	public void TheGrowthResource_ExportsTheValuesThisTestKnows()
	{
		// When this fails, a property was added to or removed from StatGrowth: update the
		// list above and StatGrowth.ToValues together.
		Assert.Equal(ExpectedGrowthProperties, StatTuningTests.ExportedProperties(ProjectFiles.ReadText("Stats", "StatGrowth.cs")));
	}

	[Fact]
	public void EveryCharacter_IsOneTheRulesAccept_WithAnIdOfItsOwn()
	{
		Dictionary<string, string> files = CharacterFiles();
		var problems = new List<string>();
		var ids = new Dictionary<string, string>();

		foreach ((string file, string text) in files)
		{
			(CharacterValues character, List<string> found) = Read(text);
			problems.AddRange(found.Select(problem => $"{file}: {problem}"));

			if (character == null)
				continue;

			if (Path.GetFileName(file) != $"{character.Id}.tres")
				problems.Add($"{file}: the id is '{character.Id}', so the file should be {character.Id}.tres.");

			if (!ids.TryAdd(character.Id, file))
				problems.Add($"{file}: the id '{character.Id}' is already {ids[character.Id]}'s.");
		}

		// The three placeholders, so a broken search cannot pass as a game with no characters.
		Assert.Contains("Characters/demo_hero.tres", files.Keys);
		Assert.Contains("Characters/demo_friend.tres", files.Keys);
		Assert.Contains("Characters/demo_newcomer.tres", files.Keys);
		Assert.Empty(problems);
	}

	[Fact]
	public void ThePlaceholderProtagonist_StartsTheFantasyWorldWithTheStatsCombatUsesToday()
	{
		// PlayerStats has 20 HP and 4 attack, and the two enemies are tuned to them: one
		// win and one loss. The protagonist takes over with the same numbers.
		(CharacterValues hero, List<string> problems) = Read(CharacterFiles()["Characters/demo_hero.tres"]);

		Assert.Empty(problems);
		Assert.Equal(20, hero.StatsAt(World.Fantasy, 1).HealthPoints);
		Assert.Equal(4, hero.StatsAt(World.Fantasy, 1).AttackDamage);
	}

	private const string Sample = """
		[gd_resource type="Resource" script_class="CharacterData" format=3 uid="uid://sample"]

		[ext_resource type="Script" uid="uid://a" path="res://Characters/CharacterData.cs" id="1_c"]
		[ext_resource type="Script" uid="uid://b" path="res://Stats/StatGrowth.cs" id="2_g"]

		[sub_resource type="Resource" id="Resource_r"]
		script = ExtResource("2_g")
		HealthPointsBase = 7.0
		HealthPointsGrowth = 3.0
		metadata/_custom_type_script = "uid://b"

		[sub_resource type="Resource" id="Resource_f"]
		script = ExtResource("2_g")
		HealthPointsBase = 20.0
		AttackDamageBase = 4.0
		AttackDamageGrowth = 0.5
		LuckGrowth = 0.29

		[sub_resource type="Resource" id="Resource_n"]
		script = ExtResource("2_g")
		HealthPointsBase = 15.0
		HitRateBase = 85.0

		[resource]
		script = ExtResource("1_c")
		Id = "sample"
		NameKey = "speaker.sample"
		RealAge = 12
		Real = SubResource("Resource_r")
		Fantasy = SubResource("Resource_f")
		Nightmare = SubResource("Resource_n")
		metadata/_custom_type_script = "uid://a"

		""";

	[Fact]
	public void Read_FindsWhatASampleHolds()
	{
		// The checks above read by pattern, and a pattern that matches nothing passes for
		// a file with nothing wrong. This pins what the reader finds in a file as the
		// editor writes it.
		(CharacterValues character, List<string> problems) = Read(Sample);

		Assert.Empty(problems);
		Assert.Equal("sample", character.Id);
		Assert.Equal("speaker.sample", character.NameKey);
		Assert.Equal(12, character.RealAge);
		Assert.Equal(TestTuning.Stats(healthPoints: 40), character.StatsAt(World.Real, 12));
		Assert.Equal(TestTuning.Stats(healthPoints: 20, attackDamage: 4), character.StatsAt(World.Fantasy, 1));
		Assert.Equal(TestTuning.Stats(healthPoints: 20, attackDamage: 54, luck: 29), character.StatsAt(World.Fantasy, 101));
		Assert.Equal(TestTuning.Stats(healthPoints: 15, hitRate: 85), character.StatsAt(World.Nightmare, 1));
	}

	[Theory]
	[InlineData("Id = \"sample\"\nNameKey = \"speaker.sample\"", "Id = \"Sample\"\nNameKey = \"speaker.Sample\"", "lowercase")]
	[InlineData("NameKey = \"speaker.sample\"", "NameKey = \"enemy.sample\"", "NameKey")]
	[InlineData("RealAge = 12\n", "", "age")]
	[InlineData("RealAge = 12", "RealAge = 12.5", "RealAge")]
	[InlineData("Fantasy = SubResource(\"Resource_f\")\n", "", "Fantasy world")]
	[InlineData("Nightmare = SubResource(\"Resource_n\")", "Nightmare = SubResource(\"Resource_f\")", "shares")]
	[InlineData("Real = SubResource(\"Resource_r\")", "Real = SubResource(\"Resource_x\")", "Real world")]
	[InlineData("HealthPointsBase = 15.0\n", "", "health points in the Nightmare world")]
	[InlineData("HitRateBase = 85.0", "HitRatBase = 85.0", "'HitRatBase'")]
	[InlineData("LuckGrowth = 0.29", "LuckGrowth = -0.29", "negative")]
	[InlineData("AttackDamageBase = 4.0", "AttackDamageBase = four", "not a number")]
	[InlineData("[sub_resource type=\"Resource\" id=\"Resource_n\"]\nscript = ExtResource(\"2_g\")", "[sub_resource type=\"Resource\" id=\"Resource_n\"]\nscript = ExtResource(\"1_c\")", "not a StatGrowth")]
	public void Read_ReportsABrokenSample(string from, string to, string expectedProblem)
	{
		string sample = Sample.Replace("\r\n", "\n");
		Assert.Contains(from, sample);

		(CharacterValues character, List<string> problems) = Read(sample.Replace(from, to));

		Assert.Null(character);
		Assert.Contains(problems, problem => problem.Contains(expectedProblem, StringComparison.Ordinal));
	}
}
