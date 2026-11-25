using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// What the combat screen says when a fight ends, in pages of <see cref="LinesPerPage"/>
/// lines: how it ended, the experience, who reaches a level, and who was down and gains
/// nothing.
/// </summary>
/// <remarks>
/// Engine-free: it takes the translation as a function, as <c>Tr</c> gives it. It reads a
/// <see cref="CombatAftermath"/> and the award its <see cref="CombatAftermath.PreviewAward"/>
/// computed, before the party receives anything: the screen announces it, and <c>Main</c>
/// applies the same computation once the screen closes.
/// </remarks>
public static class CombatResultPages
{
	public const int LinesPerPage = 4;

	public const string Won = "ui.combat.player_won";
	public const string Lost = "ui.combat.player_lost";
	public const string Fled = "ui.combat.player_fled";
	public const string ExperienceGained = "ui.combat.experience_gained";
	public const string LevelReached = "ui.combat.level_reached";
	public const string WasDown = "ui.combat.was_down";

	/// <summary>Every line, in order, before it is cut into pages.</summary>
	/// <remarks>
	/// After a loss it is the one line: everyone was down, and nobody gains anything, so a
	/// line per member would say nothing. After a flight too: nobody gains anything either.
	/// After a win, one line per member who was down, after the levels, so the rule that
	/// they gain nothing shows even though everyone is healed once the fight is over.
	/// </remarks>
	public static IReadOnlyList<string> Lines(CombatAftermath aftermath, ExperienceAward award, CharacterRosterValues roster, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(aftermath);
		ArgumentNullException.ThrowIfNull(award);
		ArgumentNullException.ThrowIfNull(roster);
		ArgumentNullException.ThrowIfNull(translate);

		switch (aftermath.Result)
		{
			case CombatResult.Lost:
				return new[] { translate(Lost) };
			case CombatResult.Fled:
				return new[] { translate(Fled) };
			case CombatResult.Won:
				break;
			default:
				throw new UnreachableException($"A fight ended as {aftermath.Result}, which the result pages do not know.");
		}

		var lines = new List<string> { translate(Won) };

		// A world with no experience, or a fight that gives none, says nothing about it.
		if (award.Experience > 0)
			lines.Add(TextTemplate.Fill(translate(ExperienceGained), ("experience", award.Experience)));

		foreach (MemberGain gain in award.Gains.Where(gain => gain.ReachesALevel))
			lines.Add(TextTemplate.Fill(translate(LevelReached), ("name", translate(roster.Get(gain.Id).NameKey)), ("level", gain.After.Level)));

		foreach (MemberAftermath member in aftermath.Members.Where(member => member.WentDown))
			lines.Add(TextTemplate.Fill(translate(WasDown), ("name", translate(roster.Get(member.Id).NameKey))));

		return lines.AsReadOnly();
	}

	/// <summary>The lines cut into pages of <see cref="LinesPerPage"/>, each page one text of lines joined by a line break.</summary>
	public static IReadOnlyList<string> Pages(CombatAftermath aftermath, ExperienceAward award, CharacterRosterValues roster, Func<string, string> translate) =>
		Lines(aftermath, award, roster, translate)
			.Chunk(LinesPerPage)
			.Select(page => string.Join("\n", page))
			.ToArray();
}
