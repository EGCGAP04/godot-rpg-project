using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks <c>Stats/stat_tuning.tres</c>, the file the game's tuning comes from, by
/// reading it as text: every value the <c>StatTuning</c> resource exports is written
/// in it, and together they make a tuning the formulas accept and experience curves
/// that can be built.
/// </summary>
/// <remarks>
/// <para>
/// <c>StatTuning</c>'s defaults are zero and the editor leaves out of a <c>.tres</c>
/// every property equal to its default, so a missing line means a value of 0, which no
/// tuning value may be. This is the test that notices a line gone after an editor save.
/// </para>
/// <para>
/// The resource itself is a Godot type and cannot be loaded here. Its list of
/// properties is read from its source, so a property added to it without a line in the
/// file fails; that <c>ToValues</c> and <c>ToExperienceCurves</c> hand each one to the
/// right parameter is not covered here and was checked with a headless run.
/// </para>
/// </remarks>
public class StatTuningTests
{
	private static readonly string[] ExpectedProperties =
	{
		"ArmorKAttack", "ArmorKSkill", "ArmorReductionCapAttack", "ArmorReductionCapSkill",
		"LuckKAttack", "LuckKSkill", "CriticalChanceCapAttack", "CriticalChanceCapSkill",
		"HitChanceFloor",
		"CriticalDamageBase", "CriticalDamageCapAttack", "CriticalDamageCapSkill",
		"EvasionCap",
		"EscapeBase", "EscapeFloor", "EscapeCap",
		"FantasyExperienceBase", "FantasyExperienceExponent", "FantasyHighestLevel",
		"NightmareExperienceBase", "NightmareExperienceExponent", "NightmareHighestLevel"
	};

	private static string ResourceSource => ProjectFiles.ReadText("Stats", "StatTuning.cs");

	private static string TuningFile => ProjectFiles.ReadText("Stats", "stat_tuning.tres");

	/// <summary>The exported properties of a resource, read from its source.</summary>
	public static string[] ExportedProperties(string source) =>
		Regex.Matches(source, @"\[Export\]\s+public\s+(?:double|int)\s+(\w+)\s*\{")
			.Select(match => match.Groups[1].Value)
			.ToArray();

	/// <summary>The <c>Name = number</c> lines of a <c>.tres</c>, after its <c>[resource]</c> heading.</summary>
	public static Dictionary<string, double> WrittenValues(string file)
	{
		int start = file.IndexOf("[resource]", StringComparison.Ordinal);
		Assert.True(start >= 0, "The file has no [resource] section.");

		var values = new Dictionary<string, double>();

		foreach (Match match in Regex.Matches(file[start..], @"^(\w+) = (-?\d+(?:\.\d+)?(?:e[+-]?\d+)?)$", RegexOptions.Multiline))
			values.Add(match.Groups[1].Value, double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));

		return values;
	}

	[Fact]
	public void TheResource_ExportsTheValuesThisTestKnows()
	{
		// When this fails, a property was added to or removed from StatTuning: update the
		// list above, StatTuningValues and the file together.
		Assert.Equal(ExpectedProperties, ExportedProperties(ResourceSource));
	}

	[Fact]
	public void TheFile_IsAStatTuning()
	{
		Assert.Contains("script_class=\"StatTuning\"", TuningFile);
		Assert.Contains("path=\"res://Stats/StatTuning.cs\"", TuningFile);
	}

	[Fact]
	public void TheFile_WritesEveryValue_AndNothingElse()
	{
		Dictionary<string, double> written = WrittenValues(TuningFile);

		string[] missing = ExpectedProperties.Where(name => !written.ContainsKey(name)).ToArray();
		string[] unknown = written.Keys.Where(name => !ExpectedProperties.Contains(name)).ToArray();

		Assert.True(missing.Length == 0, $"stat_tuning.tres does not write: {string.Join(", ", missing)}. A value equal to the resource's default, 0, is left out by the editor.");
		Assert.True(unknown.Length == 0, $"stat_tuning.tres writes values the resource does not have: {string.Join(", ", unknown)}.");
	}

	[Fact]
	public void TheFilesValues_MakeATuningTheFormulasAccept()
	{
		Dictionary<string, double> v = WrittenValues(TuningFile);

		var tuning = new StatTuningValues(
			armorK: new ByAction(v["ArmorKAttack"], v["ArmorKSkill"]),
			armorReductionCap: new ByAction(v["ArmorReductionCapAttack"], v["ArmorReductionCapSkill"]),
			luckK: new ByAction(v["LuckKAttack"], v["LuckKSkill"]),
			criticalChanceCap: new ByAction(v["CriticalChanceCapAttack"], v["CriticalChanceCapSkill"]),
			hitChanceFloor: v["HitChanceFloor"],
			criticalDamageBase: v["CriticalDamageBase"],
			criticalDamageCap: new ByAction(v["CriticalDamageCapAttack"], v["CriticalDamageCapSkill"]),
			evasionCap: v["EvasionCap"],
			escapeBase: v["EscapeBase"],
			escapeFloor: v["EscapeFloor"],
			escapeCap: v["EscapeCap"]);

		// The formulas work with it: one result, so a tuning that builds but breaks them fails here.
		double reduction = new StatFormulas(tuning).ArmorReduction(TestTuning.Stats(armor: 50), ActionKind.Attack);
		Assert.InRange(reduction, 0.0001, tuning.ArmorReductionCap.Attack);
	}

	[Fact]
	public void TheFilesValues_MakeExperienceCurvesThatCanBeBuilt()
	{
		Dictionary<string, double> v = WrittenValues(TuningFile);

		var curves = new ExperienceCurves(
			fantasy: new ExperienceCurve(v["FantasyExperienceBase"], v["FantasyExperienceExponent"], WholeNumber(v, "FantasyHighestLevel")),
			nightmare: new ExperienceCurve(v["NightmareExperienceBase"], v["NightmareExperienceExponent"], WholeNumber(v, "NightmareHighestLevel")));

		// The curves work: an award large enough for the first level reaches the second.
		foreach (ExperienceCurve curve in new[] { curves.Fantasy, curves.Nightmare })
			Assert.Equal(new LevelProgress(2, 0), curve.Add(new LevelProgress(1, 0), curve.ToNextLevel(1)));
	}

	private static int WholeNumber(Dictionary<string, double> values, string name)
	{
		double value = values[name];
		Assert.True(value == Math.Floor(value), $"stat_tuning.tres writes {name} as {value}, and it is a whole number.");

		return (int)value;
	}

	[Fact]
	public void TheReaders_FindWhatASampleHolds()
	{
		// The searches above are by pattern, and a pattern that matches nothing passes
		// for a file with nothing wrong. These samples pin what each one finds.
		string source = "\t[Export]\n\tpublic double First { get; set; }\n\n\t/// <summary>x</summary>\n\t[Export]\n\tpublic double Second { get; set; }\n\tpublic double NotExported { get; set; }\n\n\t[Export]\n\tpublic int Whole { get; set; }\n";
		string file = "[gd_resource]\nNotAValue = 1.0\n\n[resource]\nscript = ExtResource(\"1\")\nFirst = 100.0\nSecond = 2.5\nThird = 7\n";

		Assert.Equal(new[] { "First", "Second", "Whole" }, ExportedProperties(source));

		Dictionary<string, double> written = WrittenValues(file);
		Assert.Equal(new[] { "First", "Second", "Third" }, written.Keys.ToArray());
		Assert.Equal(100, written["First"]);
		Assert.Equal(2.5, written["Second"]);
		Assert.Equal(7, written["Third"]);
	}
}
