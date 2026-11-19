using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Checks <c>Combat/combat_tuning.tres</c>, the file combat's tuning comes from, by reading
/// it as text: every value the <c>CombatTuning</c> resource exports is written in it, and
/// together they make a tuning the fight accepts.
/// </summary>
/// <remarks>
/// It reads the way <see cref="StatTuningTests"/> does and reuses its readers, whose own
/// test pins what they find. That <c>ToValues</c> hands each value to the right parameter
/// is not covered here and was checked with a headless run.
/// </remarks>
public class CombatTuningTests
{
	private static readonly string[] ExpectedProperties = { "MinimumDamage", "DamageVariationPercent" };

	private static string ResourceSource => ProjectFiles.ReadText("Combat", "CombatTuning.cs");

	private static string TuningFile => ProjectFiles.ReadText("Combat", "combat_tuning.tres");

	[Fact]
	public void TheResource_ExportsTheValuesThisTestKnows()
	{
		// When this fails, a property was added to or removed from CombatTuning: update the
		// list above, CombatTuningValues and the file together.
		Assert.Equal(ExpectedProperties, StatTuningTests.ExportedProperties(ResourceSource));
	}

	[Fact]
	public void TheFile_IsACombatTuning()
	{
		Assert.Contains("script_class=\"CombatTuning\"", TuningFile);
		Assert.Contains("path=\"res://Combat/CombatTuning.cs\"", TuningFile);
	}

	[Fact]
	public void TheFile_WritesEveryValue_AndNothingElse()
	{
		Dictionary<string, double> written = StatTuningTests.WrittenValues(TuningFile);

		string[] missing = ExpectedProperties.Where(name => !written.ContainsKey(name)).ToArray();
		string[] unknown = written.Keys.Where(name => !ExpectedProperties.Contains(name)).ToArray();

		Assert.True(missing.Length == 0, $"combat_tuning.tres does not write: {string.Join(", ", missing)}. A value equal to the resource's default, 0, is left out by the editor.");
		Assert.True(unknown.Length == 0, $"combat_tuning.tres writes values the resource does not have: {string.Join(", ", unknown)}.");
	}

	[Fact]
	public void TheFilesValues_MakeATuningTheFightAccepts()
	{
		Dictionary<string, double> v = StatTuningTests.WrittenValues(TuningFile);

		double minimum = v["MinimumDamage"];
		Assert.True(minimum == (int)minimum, $"combat_tuning.tres writes MinimumDamage as {minimum}, and it is a whole number.");

		var tuning = new CombatTuningValues((int)minimum, v["DamageVariationPercent"]);

		// The pipeline works with it: a hit against armor that stops everything still does
		// the minimum, so a tuning that builds but breaks the pipeline fails here.
		var pipeline = new DamagePipeline(TestTuning.Formulas(), tuning);
		var random = new ScriptedCombatRandom();
		random.Percent(0);
		random.Percent(99);
		random.Percent(99);
		random.Percent(50);

		AttackResult result = pipeline.Resolve(
			TestTuning.Stats(attackDamage: 1, hitRate: 100),
			TestTuning.Stats(armor: 1_000_000),
			ActionKind.Attack,
			random);

		Assert.Equal(AttackOutcome.Hit, result.Outcome);
		Assert.Equal(tuning.MinimumDamage, result.Damage);
		random.RequireUsedUp();
	}
}
