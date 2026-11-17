using System.Collections.Generic;

/// <summary>
/// Something that happened in a fight, for whoever shows it or logs it.
/// <see cref="CombatRunner.TakeEvents"/> hands them over in the order they happened.
/// </summary>
public abstract record CombatEvent;

/// <summary>A round started, with everyone standing in the order they will act.</summary>
public sealed record RoundStarted(int Round, IReadOnlyList<CombatantId> TurnOrder) : CombatEvent;

/// <summary>An attack was worked out and its damage taken.</summary>
/// <param name="Attacker">Who attacked.</param>
/// <param name="Target">
/// Who was attacked in the end, which is not the ordered target when that one had gone
/// down.
/// </param>
/// <param name="Result">What the damage pipeline worked out.</param>
/// <param name="DamageTaken">
/// The damage taken out of the target's health. For a member's attack it is the result's.
/// For an enemy's it is what was answered, which is another number once the player can
/// dodge.
/// </param>
public sealed record AttackResolved(CombatantId Attacker, CombatantId Target, AttackResult Result, int DamageTaken) : CombatEvent;

/// <summary>A combatant's health reached 0.</summary>
public sealed record CombatantDowned(CombatantId Combatant) : CombatEvent;

/// <summary>A whole side is down and the fight is over.</summary>
public sealed record FightEnded(CombatResult Result) : CombatEvent;
