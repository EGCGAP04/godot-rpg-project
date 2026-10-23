/// <summary>
/// One fight in the decision registry's history: where and when it happened, against
/// which kind of enemy, and who won.
/// </summary>
/// <param name="Cycle">The cycle the fight belongs to, so "in this cycle" is a filter.</param>
/// <param name="EnemyId">
/// The enemy's <c>Id</c>, which names the kind of enemy and stays the same when its
/// translated name changes.
/// </param>
public sealed record CombatRecord(World World, int Cycle, string EnemyId, bool PlayerWon);
