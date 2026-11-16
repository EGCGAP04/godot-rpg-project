using System;

/// <summary>
/// The status screen's lines, built from a <see cref="StatusSheet"/>. Deliberately a
/// plain C# class with no Godot dependency, so which line shows in each world is
/// unit-tested without the engine; <c>StatusScreen.cs</c> hands it <c>Tr</c> and puts the
/// results on labels.
/// </summary>
/// <remarks>
/// The stats' names are keys of their own, <c>ui.stat.*</c>, apart from the screen's,
/// because the combat screen will name the same stats.
/// </remarks>
public static class StatusText
{
	public const string LevelKey = "ui.status.level";
	public const string AgeKey = "ui.status.age";
	public const string ExperienceKey = "ui.status.experience";
	public const string HighestLevelKey = "ui.status.highest_level";
	public const string PointsKey = "ui.status.points";
	public const string ValueKey = "ui.status.value";

	public const string HealthPointsKey = "ui.stat.health_points";
	public const string ManaPointsKey = "ui.stat.mana_points";
	public const string AttackDamageKey = "ui.stat.attack_damage";
	public const string ArmorKey = "ui.stat.armor";
	public const string AgilityKey = "ui.stat.agility";
	public const string LuckKey = "ui.stat.luck";
	public const string HitRateKey = "ui.stat.hit_rate";

	/// <summary>The character's name.</summary>
	public static string Name(StatusSheet sheet, Func<string, string> translate) => translate(sheet.NameKey);

	/// <summary>The level, or the age in the world where the level is one.</summary>
	public static string Level(StatusSheet sheet, Func<string, string> translate) => sheet.LevelIsAge
		? TextTemplate.Fill(translate(AgeKey), ("age", sheet.Level))
		: TextTemplate.Fill(translate(LevelKey), ("level", sheet.Level));

	/// <summary>
	/// The experience towards the next level, or that there is no next level. Empty in
	/// the world with no experience, which says nothing about it.
	/// </summary>
	public static string Experience(StatusSheet sheet, Func<string, string> translate)
	{
		if (sheet.LevelIsAge)
			return "";

		return sheet.ToNextLevel is long toNextLevel
			? TextTemplate.Fill(translate(ExperienceKey), ("experience", sheet.Experience), ("to_next", toNextLevel))
			: translate(HighestLevelKey);
	}

	/// <summary>The points left of the most there can be: health points first, then mana points.</summary>
	public static string[] Points(StatusSheet sheet, Func<string, string> translate) => new[]
	{
		PointsLine(HealthPointsKey, sheet.HealthPoints, sheet.Stats.HealthPoints, translate),
		PointsLine(ManaPointsKey, sheet.ManaPoints, sheet.Stats.ManaPoints, translate)
	};

	/// <summary>The other base stats, one line each, in the design's order.</summary>
	public static string[] Stats(StatusSheet sheet, Func<string, string> translate) => new[]
	{
		ValueLine(AttackDamageKey, sheet.Stats.AttackDamage, translate),
		ValueLine(ArmorKey, sheet.Stats.Armor, translate),
		ValueLine(AgilityKey, sheet.Stats.Agility, translate),
		ValueLine(LuckKey, sheet.Stats.Luck, translate),
		ValueLine(HitRateKey, sheet.Stats.HitRate, translate)
	};

	private static string PointsLine(string statKey, int current, int most, Func<string, string> translate) =>
		TextTemplate.Fill(translate(PointsKey), ("stat", translate(statKey)), ("current", current), ("max", most));

	private static string ValueLine(string statKey, int value, Func<string, string> translate) =>
		TextTemplate.Fill(translate(ValueKey), ("stat", translate(statKey)), ("value", value));
}
