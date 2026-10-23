using System.Collections.Generic;

/// <summary>
/// What one save slot keeps of the decision registry: the value of every flag whose
/// lifetime is <see cref="FlagLifetime.Cycle"/> or <see cref="FlagLifetime.Run"/>, and
/// the history of fights. Persistent flags belong to the game, not to a slot, so they
/// are never in it.
/// </summary>
/// <param name="Flags">The boolean flags, by name.</param>
/// <param name="Counters">The counters, by name.</param>
/// <param name="Combats">The fights, oldest first.</param>
public sealed record SlotSnapshot(
	IReadOnlyDictionary<string, bool> Flags,
	IReadOnlyDictionary<string, int> Counters,
	IReadOnlyList<CombatRecord> Combats);
