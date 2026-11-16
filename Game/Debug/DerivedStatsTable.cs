using System.Collections.Generic;
using System.Globalization;

/// <summary>One derived stat, against an attack and against a skill.</summary>
/// <param name="Name">The stat's name, in English like the rest of the debug overlay.</param>
/// <param name="Attack">The percentage for an attack.</param>
/// <param name="Skill">The percentage for a skill, or <c>null</c> when the kind of action makes no difference.</param>
public sealed record DerivedStatRow(string Name, double Attack, double? Skill);

/// <summary>
/// The derived stats the debug overlay shows for one set of base stats: what
/// <see cref="StatFormulas"/> gives for each, by kind of action. The design hides these
/// from the player, so they show nowhere else. Deliberately a plain C# class with no
/// Godot dependency, covered by unit tests.
/// </summary>
/// <remarks>
/// The escape rate is not here: it compares two sides of a fight, and a character alone
/// has none.
/// </remarks>
public static class DerivedStatsTable
{
	public const string ArmorReduction = "Armor reduction";
	public const string CriticalChance = "Critical chance";
	public const string HitChance = "Hit chance";
	public const string CriticalDamage = "Critical damage";
	public const string EvasionRate = "Evasion rate";

	/// <summary>The five rows, in a fixed order.</summary>
	public static IReadOnlyList<DerivedStatRow> For(StatFormulas formulas, BaseStats stats) => new[]
	{
		new DerivedStatRow(ArmorReduction, formulas.ArmorReduction(stats, ActionKind.Attack), formulas.ArmorReduction(stats, ActionKind.Skill)),
		new DerivedStatRow(CriticalChance, formulas.CriticalChance(stats, ActionKind.Attack), formulas.CriticalChance(stats, ActionKind.Skill)),
		new DerivedStatRow(HitChance, formulas.HitChance(stats), null),
		new DerivedStatRow(CriticalDamage, formulas.CriticalDamage(stats, ActionKind.Attack), formulas.CriticalDamage(stats, ActionKind.Skill)),
		new DerivedStatRow(EvasionRate, formulas.EvasionRate(stats, ActionKind.Attack), formulas.EvasionRate(stats, ActionKind.Skill))
	};

	/// <summary>A percentage with one decimal, written the same whatever the system's settings are: <c>12.5%</c>.</summary>
	public static string Format(double percentage) => percentage.ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
