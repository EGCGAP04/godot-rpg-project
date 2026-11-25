using System;

/// <summary>
/// The line the combat screen's log shows for each thing that happens in a fight, from
/// the runner's events.
/// </summary>
/// <remarks>
/// Engine-free: it takes the translation as a function, as <c>Tr</c> gives it. The damage
/// a line gives is the damage taken, which for an enemy's attack is what the screen
/// answered. <see cref="FightEnded"/> has no line: the result pages tell it. Neither has an
/// <see cref="EscapeAttempted"/> that got away, for the same reason; one that failed does.
/// </remarks>
public static class CombatLog
{
	public const string Round = "ui.combat.log.round";
	public const string Hit = "ui.combat.log.hit";
	public const string Critical = "ui.combat.log.critical";
	public const string Missed = "ui.combat.log.missed";
	public const string Evaded = "ui.combat.log.evaded";
	public const string Downed = "ui.combat.log.downed";
	public const string Defends = "ui.combat.log.defends";
	public const string Focuses = "ui.combat.log.focuses";
	public const string Meditates = "ui.combat.log.meditates";
	public const string EscapeFailed = "ui.combat.log.escape_failed";

	/// <summary>The event's line, or <c>null</c> for one the log does not show.</summary>
	/// <exception cref="ArgumentException">The event is of a kind the log does not know.</exception>
	public static string LineFor(CombatEvent happened, CombatantNames names, Func<string, string> translate)
	{
		ArgumentNullException.ThrowIfNull(happened);
		ArgumentNullException.ThrowIfNull(names);
		ArgumentNullException.ThrowIfNull(translate);

		return happened switch
		{
			RoundStarted round => TextTemplate.Fill(translate(Round), ("round", round.Round)),
			AttackResolved attack => AttackLine(attack, names, translate),
			CombatantDowned downed => TextTemplate.Fill(translate(Downed), ("name", names.Of(downed.Combatant))),
			Defended defended => TextTemplate.Fill(translate(Defends), ("name", names.Of(defended.Member))),
			Focused focused => TextTemplate.Fill(translate(Focuses), ("name", names.Of(focused.Member))),
			Meditated meditated => TextTemplate.Fill(translate(Meditates), ("name", names.Of(meditated.Member)), ("mana", meditated.Recovered)),
			EscapeAttempted { Escaped: true } => null,
			EscapeAttempted => translate(EscapeFailed),
			FightEnded => null,
			_ => throw new ArgumentException($"The log has no line for {happened.GetType().Name}.", nameof(happened))
		};
	}

	private static string AttackLine(AttackResolved attack, CombatantNames names, Func<string, string> translate)
	{
		string key = attack.Result.Outcome switch
		{
			AttackOutcome.Missed => Missed,
			AttackOutcome.Evaded => Evaded,
			_ => attack.Result.IsCritical ? Critical : Hit
		};

		return TextTemplate.Fill(
			translate(key),
			("attacker", names.Of(attack.Attacker)),
			("target", names.Of(attack.Target)),
			("damage", attack.DamageTaken));
	}
}
