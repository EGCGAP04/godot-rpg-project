/// <summary>
/// What one party member is told to do in a round. Each kind of action is a record of its
/// own under this one, so a new action is a new type and changes no signature.
/// </summary>
/// <param name="Actor">The member the order is for.</param>
public abstract record CombatOrder(CombatantId Actor);

/// <summary>An attack on one enemy.</summary>
/// <param name="Actor">The member who attacks.</param>
/// <param name="Target">
/// The enemy attacked, standing when the order is given. If it goes down before the
/// member's turn, the attack moves to another standing enemy, picked by a roll.
/// </param>
public sealed record AttackOrder(CombatantId Actor, CombatantId Target) : CombatOrder(Actor);
