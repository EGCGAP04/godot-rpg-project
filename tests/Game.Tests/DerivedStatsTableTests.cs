using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DerivedStatsTable"/>: that each row carries the formula its name
/// says, for the right kind of action. The formulas themselves are
/// <see cref="StatFormulas"/>'s tests'.
/// </summary>
public class DerivedStatsTableTests
{
	/// <summary>
	/// Stats that give every formula a different, non-zero answer for each kind of action.
	/// The hit rate is far above 100 so that critical damage passes a skill's cap of 300:
	/// below it, an attack and a skill have the same.
	/// </summary>
	private static readonly BaseStats Stats = new(healthPoints: 100, attackDamage: 40, armor: 60, manaPoints: 30, agility: 50, luck: 25, hitRate: 260);

	private static readonly StatFormulas Formulas = TestTuning.Formulas();

	private static DerivedStatRow Row(string name) => DerivedStatsTable.For(Formulas, Stats).Single(row => row.Name == name);

	[Fact]
	public void For_HasTheFiveRows_InOrder()
	{
		Assert.Equal(
			new[] { "Armor reduction", "Critical chance", "Hit chance", "Critical damage", "Evasion rate" },
			DerivedStatsTable.For(Formulas, Stats).Select(row => row.Name));
	}

	[Fact]
	public void For_EachRow_IsItsFormula_AgainstAnAttackAndAgainstASkill()
	{
		Assert.Equal(new DerivedStatRow("Armor reduction", Formulas.ArmorReduction(Stats, ActionKind.Attack), Formulas.ArmorReduction(Stats, ActionKind.Skill)), Row("Armor reduction"));
		Assert.Equal(new DerivedStatRow("Critical chance", Formulas.CriticalChance(Stats, ActionKind.Attack), Formulas.CriticalChance(Stats, ActionKind.Skill)), Row("Critical chance"));
		Assert.Equal(new DerivedStatRow("Critical damage", Formulas.CriticalDamage(Stats, ActionKind.Attack), Formulas.CriticalDamage(Stats, ActionKind.Skill)), Row("Critical damage"));
		Assert.Equal(new DerivedStatRow("Evasion rate", Formulas.EvasionRate(Stats, ActionKind.Attack), Formulas.EvasionRate(Stats, ActionKind.Skill)), Row("Evasion rate"));
	}

	[Fact]
	public void TheStatsOfTheTest_TellEveryFormulaAndKindApart()
	{
		// Otherwise a row wired to the wrong formula, or to the wrong kind, would pass.
		IEnumerable<double> values = DerivedStatsTable.For(Formulas, Stats).SelectMany(row => row.Skill is double skill ? new[] { row.Attack, skill } : new[] { row.Attack });

		Assert.Equal(9, values.Distinct().Count());
		Assert.All(values, value => Assert.True(value > 0));
	}

	[Fact]
	public void HitChance_IsOneNumber_WhateverTheAction()
	{
		Assert.Equal(new DerivedStatRow("Hit chance", Formulas.HitChance(Stats), null), Row("Hit chance"));
	}

	[Theory]
	[InlineData(0, "0.0%")]
	[InlineData(12.34, "12.3%")]
	[InlineData(12.35, "12.4%")]
	[InlineData(150, "150.0%")]
	public void Format_WritesOneDecimal_WithAPoint(double percentage, string expected)
	{
		Assert.Equal(expected, DerivedStatsTable.Format(percentage));
	}
}
