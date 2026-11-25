using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>How a fight in the history ended.</summary>
/// <remarks>
/// Apart from the combat core's <c>CombatResult</c> on purpose: that one also says a
/// fight is still going on, which a fight in the history never is.
/// </remarks>
public enum FightOutcome
{
	Won,
	Lost,

	/// <summary>The party fled: the group is still on the map, and nobody gained anything.</summary>
	Fled
}

/// <summary>
/// One fight in the decision registry's history: where and when it happened, against
/// which group and which enemies, and how it ended.
/// </summary>
/// <remarks>
/// <para>
/// Checked here, once, when it is built, so a fight in the history is always whole: it
/// names a group, at least one enemy, none of them empty, and an outcome that exists.
/// </para>
/// <para>
/// <b>Its equality is written by hand.</b> A record compares a list by reference, so two
/// fights against the same enemies would be different; here they are equal when the
/// enemies are the same ids in the same order, as the snapshots' tests need.
/// </para>
/// </remarks>
public sealed record CombatRecord
{
	/// <param name="cycle">The cycle the fight belongs to, so "in this cycle" is a filter.</param>
	/// <param name="encounterId">The group's id, which stays the same when its enemies' names change.</param>
	/// <param name="enemyIds">The <c>Id</c> of each enemy in the group, in its order, repeats included.</param>
	public CombatRecord(World world, int cycle, string encounterId, IEnumerable<string> enemyIds, FightOutcome outcome)
	{
		if (string.IsNullOrEmpty(encounterId))
			throw new ArgumentException("A fight needs the id of the group it was against.", nameof(encounterId));

		ArgumentNullException.ThrowIfNull(enemyIds);
		string[] enemies = enemyIds.ToArray();

		if (enemies.Length == 0)
			throw new ArgumentException($"The fight against '{encounterId}' names no enemy.", nameof(enemyIds));

		if (enemies.Any(string.IsNullOrEmpty))
			throw new ArgumentException($"The fight against '{encounterId}' names an enemy with no id.", nameof(enemyIds));

		if (!Enum.IsDefined(outcome))
			throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A fight is won, lost or fled.");

		World = world;
		Cycle = cycle;
		EncounterId = encounterId;
		EnemyIds = Array.AsReadOnly(enemies);
		Outcome = outcome;
	}

	public World World { get; }

	public int Cycle { get; }

	public string EncounterId { get; }

	public IReadOnlyList<string> EnemyIds { get; }

	public FightOutcome Outcome { get; }

	public bool Equals(CombatRecord other) =>
		other is not null
		&& World == other.World
		&& Cycle == other.Cycle
		&& EncounterId == other.EncounterId
		&& EnemyIds.SequenceEqual(other.EnemyIds)
		&& Outcome == other.Outcome;

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(World);
		hash.Add(Cycle);
		hash.Add(EncounterId);

		foreach (string id in EnemyIds)
			hash.Add(id);

		hash.Add(Outcome);
		return hash.ToHashCode();
	}

	/// <summary>The fight as one line, as the debug overlay lists it: <c>Fantasy, cycle 1: demo_patrol (weak, weak), won</c>.</summary>
	public override string ToString() =>
		FormattableString.Invariant($"{World}, cycle {Cycle}: {EncounterId} ({string.Join(", ", EnemyIds)}), {Word(Outcome)}");

	// The constructor lets no other value through.
	private static string Word(FightOutcome outcome) => outcome switch
	{
		FightOutcome.Won => "won",
		FightOutcome.Lost => "lost",
		FightOutcome.Fled => "fled",
		_ => throw new UnreachableException($"A fight in the history ended as {outcome}, which has no word.")
	};
}
