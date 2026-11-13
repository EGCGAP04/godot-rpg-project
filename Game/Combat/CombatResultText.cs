using System;
using System.Collections.Generic;

/// <summary>
/// What the combat screen says when a fight ends: who won, the experience the party
/// gains, and who reaches a level with it. Deliberately a plain C# class with no Godot
/// dependency, so which lines show is unit-tested without the engine; <c>Combat.cs</c>
/// hands it <c>Tr</c> and puts the result on a label.
/// </summary>
/// <remarks>
/// It reads an <see cref="ExperienceAward"/>, which is what <see cref="PartyState.PreviewAward"/>
/// computes before the party receives anything: the screen announces the award while
/// the fight is still on it, and <c>Main</c> applies the same computation once it closes.
/// </remarks>
public static class CombatResultText
{
	public const string Won = "ui.combat.player_won";
	public const string Lost = "ui.combat.player_lost";
	public const string ExperienceGained = "ui.combat.experience_gained";
	public const string LevelReached = "ui.combat.level_reached";

	/// <summary>The result, one line per thing to say, in the language <paramref name="translate"/> speaks.</summary>
	/// <param name="playerWon">Whether the party won the fight.</param>
	/// <param name="award">What winning gives the party. It is not read after a loss, and may be <c>null</c> then.</param>
	/// <param name="roster">Where each character's name key is found.</param>
	/// <param name="translate">A key's text, as <c>Tr</c> gives it.</param>
	public static string Build(bool playerWon, ExperienceAward award, CharacterRosterValues roster, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(translate);

		if (!playerWon)
			return translate(Lost);

		ArgumentNullException.ThrowIfNull(award);
		ArgumentNullException.ThrowIfNull(roster);

		var lines = new List<string> { translate(Won) };

		// A world with no experience, or a fight that gives none, says nothing about it.
		if (award.Experience > 0)
			lines.Add(TextTemplate.Fill(translate(ExperienceGained), ("experience", award.Experience)));

		foreach (MemberGain gain in award.Gains)
		{
			if (gain.ReachesALevel)
				lines.Add(TextTemplate.Fill(translate(LevelReached), ("name", translate(roster.Get(gain.Id).NameKey)), ("level", gain.After.Level)));
		}

		return string.Join("\n", lines);
	}
}
