using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks <c>Characters/roster.tres</c> by reading it as text: it names a protagonist,
/// lists every character the game has exactly once, and together with the tuning's
/// experience curves makes a party the rules accept.
/// </summary>
/// <remarks>
/// <c>CharacterRoster</c> is a Godot type and cannot be loaded here, so the file is
/// rebuilt as the <see cref="CharacterRosterValues"/> it stands for, from the characters'
/// own files as <see cref="CharacterDataTests"/> reads them. That <c>ToValues</c> and the
/// <c>Party</c> autoload load the same thing was checked with a headless run.
/// </remarks>
public class CharacterRosterTests
{
	private const string RosterFile = "Characters/roster.tres";

	/// <summary>What a roster file lists, as paths inside <c>res://</c>, or the reasons it is not a roster.</summary>
	public sealed record Listing(string Protagonist, IReadOnlyList<string> Others, IReadOnlyList<string> Problems);

	/// <summary>Reads a roster file: which character files it names, and in which role.</summary>
	public static Listing Read(string text)
	{
		var problems = new List<string>();
		List<CharacterDataTests.Section> sections = CharacterDataTests.Sections(text);
		CharacterDataTests.Section resource = sections.SingleOrDefault(section => section.Kind == "resource");

		if (resource == null)
			return new Listing(null, Array.Empty<string>(), new[] { "no [resource] section." });

		Dictionary<string, string> paths = sections
			.Where(section => section.Kind == "ext_resource")
			.ToDictionary(section => section.Attributes.GetValueOrDefault("id", ""), section => section.Attributes.GetValueOrDefault("path", ""));

		string script = Regex.Match(resource.Values.GetValueOrDefault("script", ""), "^ExtResource\\(\"(.+)\"\\)$").Groups[1].Value;

		if (paths.GetValueOrDefault(script) != "res://Characters/CharacterRoster.cs")
			problems.Add("the resource is not a CharacterRoster.");

		string PathOf(string id, string role)
		{
			if (paths.TryGetValue(id, out string path))
				return path.Replace("res://", "");

			problems.Add($"{role} points at '{id}', which the file does not declare.");
			return null;
		}

		Match protagonist = Regex.Match(resource.Values.GetValueOrDefault("Protagonist", ""), "^ExtResource\\(\"(.+)\"\\)$");
		string protagonistPath = null;

		if (protagonist.Success)
			protagonistPath = PathOf(protagonist.Groups[1].Value, "the protagonist");
		else
			problems.Add("no protagonist.");

		var others = new List<string>();

		// The editor writes the array's type before the list, and how it writes the type
		// has changed between versions; only the list matters here.
		Match list = Regex.Match(resource.Values.GetValueOrDefault("Others", ""), @"^Array\[.*?\]\(\[(.*)\]\)$");

		if (resource.Values.ContainsKey("Others") && !list.Success)
			problems.Add($"the others are '{resource.Values["Others"]}', not a list.");

		foreach (string entry in list.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			Match reference = Regex.Match(entry, "^ExtResource\\(\"(.+)\"\\)$");

			if (!reference.Success)
				problems.Add($"the others hold '{entry}', which is not a character's file.");
			else if (PathOf(reference.Groups[1].Value, "an entry of the others") is string path)
				others.Add(path);
		}

		return new Listing(protagonistPath, others, problems);
	}

	/// <summary>Every character file the game has, by its path inside <c>res://</c>.</summary>
	private static Dictionary<string, string> CharacterFiles() =>
		Directory.EnumerateFiles(ProjectFiles.GameDirectory, "*.tres", SearchOption.AllDirectories)
			.Select(path => (File: Path.GetRelativePath(ProjectFiles.GameDirectory, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
			.Where(file => !file.File.StartsWith(".godot", StringComparison.Ordinal) && file.Text.Contains("script_class=\"CharacterData\""))
			.ToDictionary(file => file.File, file => file.Text);

	private static Listing GameRoster() => Read(ProjectFiles.ReadText("Characters", "roster.tres"));

	/// <summary>What is wrong with a listing, given the character files there are.</summary>
	public static List<string> Problems(Listing listing, IReadOnlyCollection<string> characterFiles)
	{
		var problems = new List<string>(listing.Problems);
		List<string> listed = listing.Others.Prepend(listing.Protagonist).Where(path => path != null).ToList();

		foreach (string path in listed.Distinct().Where(path => !characterFiles.Contains(path)))
			problems.Add($"{path} is listed, and is not a character's file.");

		foreach (string path in listed.GroupBy(path => path).Where(group => group.Count() > 1).Select(group => group.Key))
			problems.Add($"{path} is listed more than once.");

		foreach (string path in characterFiles.Where(path => !listed.Contains(path)).OrderBy(path => path, StringComparer.Ordinal))
			problems.Add($"{path} is a character's file, and the roster does not list it.");

		return problems;
	}

	[Fact]
	public void TheRoster_ListsEveryCharacterOnce_AndNothingElse()
	{
		Dictionary<string, string> files = CharacterFiles();

		// So a broken search cannot pass as a roster with nothing to list.
		Assert.Contains("Characters/demo_hero.tres", files.Keys);
		Assert.Empty(Problems(GameRoster(), files.Keys));
	}

	[Fact]
	public void TheRoster_NamesThePlaceholderHeroAsTheProtagonist_AndTheOthersInOrder()
	{
		Listing roster = GameRoster();

		Assert.Equal("Characters/demo_hero.tres", roster.Protagonist);
		Assert.Equal(new[] { "Characters/demo_friend.tres", "Characters/demo_newcomer.tres" }, roster.Others);
	}

	[Fact]
	public void TheRoster_PointsAtEachCharacterByTheUidItsFileHas()
	{
		// A uid that is another file's would load that file instead, whatever the path says.
		Dictionary<string, string> files = CharacterFiles();
		var problems = new List<string>();

		foreach (CharacterDataTests.Section reference in CharacterDataTests.Sections(ProjectFiles.ReadText("Characters", "roster.tres")).Where(section => section.Kind == "ext_resource"))
		{
			string path = reference.Attributes.GetValueOrDefault("path", "").Replace("res://", "");

			if (!files.TryGetValue(path, out string text) || !reference.Attributes.TryGetValue("uid", out string uid))
				continue;

			string own = CharacterDataTests.Sections(text).First(section => section.Kind == "gd_resource").Attributes.GetValueOrDefault("uid");

			if (uid != own)
				problems.Add($"{path} is referenced as {uid}, and its file says {own}.");
		}

		Assert.Empty(problems);
	}

	[Fact]
	public void TheRosterAndTheTuning_MakeAParty_ThatStartsWithTheProtagonistAlone()
	{
		// PartyState's constructor is the check: every character's stats must be computable
		// up to each world's highest level, with the curves the game ships.
		Dictionary<string, string> files = CharacterFiles();
		Listing listing = GameRoster();

		CharacterValues Character(string path)
		{
			(CharacterValues character, List<string> problems) = CharacterDataTests.Read(files[path]);
			Assert.Empty(problems);
			return character;
		}

		var roster = new CharacterRosterValues(Character(listing.Protagonist), listing.Others.Select(Character));
		Dictionary<string, double> tuning = StatTuningTests.WrittenValues(ProjectFiles.ReadText("Stats", "stat_tuning.tres"));
		var curves = new ExperienceCurves(
			fantasy: new ExperienceCurve(tuning["FantasyExperienceBase"], tuning["FantasyExperienceExponent"], (int)tuning["FantasyHighestLevel"]),
			nightmare: new ExperienceCurve(tuning["NightmareExperienceBase"], tuning["NightmareExperienceExponent"], (int)tuning["NightmareHighestLevel"]));

		var party = new PartyState(roster, curves);

		Assert.Equal(new[] { "demo_hero" }, party.Members);
		Assert.Equal(3, roster.Characters.Count);
	}

	// --- The reader itself, on files written here ---

	private static string File_(string resource) => $@"[gd_resource type=""Resource"" script_class=""CharacterRoster"" format=3]

[ext_resource type=""Script"" path=""res://Characters/CharacterRoster.cs"" id=""1_roster""]
[ext_resource type=""Script"" path=""res://Characters/CharacterData.cs"" id=""9_data""]
[ext_resource type=""Resource"" path=""res://Characters/a.tres"" id=""2_a""]
[ext_resource type=""Resource"" path=""res://Characters/b.tres"" id=""3_b""]

[resource]
{resource}
";

	private static readonly string[] TwoFiles = { "Characters/a.tres", "Characters/b.tres" };

	[Theory]
	[InlineData("Array[Object]([ExtResource(\"3_b\")])")]
	[InlineData("Array[ExtResource(\"9_data\")]([ExtResource(\"3_b\")])")]
	public void Read_AGoodFile_HasNoProblems_HoweverTheArraysTypeIsWritten(string others)
	{
		Listing listing = Read(File_($"script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = {others}"));

		Assert.Equal("Characters/a.tres", listing.Protagonist);
		Assert.Equal(new[] { "Characters/b.tres" }, listing.Others);
		Assert.Empty(Problems(listing, TwoFiles));
	}

	[Theory]
	[InlineData("script = ExtResource(\"1_roster\")\nOthers = Array[Object]([ExtResource(\"2_a\"), ExtResource(\"3_b\")])", "no protagonist.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")", "Characters/b.tres is a character's file, and the roster does not list it.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"3_b\"), ExtResource(\"3_b\")])", "Characters/b.tres is listed more than once.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"2_a\"), ExtResource(\"3_b\")])", "Characters/a.tres is listed more than once.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"3_b\"), ExtResource(\"9_data\")])", "Characters/CharacterData.cs is listed, and is not a character's file.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"3_b\"), ExtResource(\"7_gone\")])", "an entry of the others points at '7_gone', which the file does not declare.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"3_b\"), null])", "the others hold 'null', which is not a character's file.")]
	[InlineData("script = ExtResource(\"1_roster\")\nProtagonist = ExtResource(\"2_a\")\nOthers = [ExtResource(\"3_b\")]", "the others are '[ExtResource(\"3_b\")]', not a list.")]
	[InlineData("script = ExtResource(\"9_data\")\nProtagonist = ExtResource(\"2_a\")\nOthers = Array[Object]([ExtResource(\"3_b\")])", "the resource is not a CharacterRoster.")]
	public void Read_AFileThatIsNotTheRoster_SaysWhy(string resource, string expected)
	{
		Assert.Contains(expected, Problems(Read(File_(resource)), TwoFiles));
	}
}
