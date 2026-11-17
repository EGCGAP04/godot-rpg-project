using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Who acts when in a round: everyone standing, by agility, highest first, with a roll
/// between those who tie.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rolls of a tie are part of the contract.</b> Those who tie are first put in a
/// known order, the party by position and then the enemies by position, whatever order
/// they were given in. Then one is picked among those left, again and again: a tie of
/// <c>k</c> takes <c>k - 1</c> <see cref="ICombatRandom.Index"/> rolls, over <c>k</c>,
/// <c>k - 1</c>… down to 2. The last one left is not rolled for, because there is nothing
/// to pick. Ties are settled from the highest agility down.
/// </para>
/// <para>
/// The order is worked out once, when the round starts, and nothing here follows what
/// happens after: whoever goes down before its turn is skipped by the fight itself.
/// </para>
/// </remarks>
public static class TurnOrder
{
	public static IReadOnlyList<Combatant> For(IEnumerable<Combatant> combatants, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(combatants);
		ArgumentNullException.ThrowIfNull(random);

		List<Combatant> everyone = combatants.ToList();

		if (everyone.Contains(null))
			throw new ArgumentException("A combatant is missing.", nameof(combatants));

		if (everyone.Select(combatant => combatant.Id).Distinct().Count() != everyone.Count)
			throw new ArgumentException("The same combatant is listed twice.", nameof(combatants));

		var order = new List<Combatant>();

		var ties = everyone
			.Where(combatant => !combatant.IsDown)
			.GroupBy(combatant => combatant.Stats.Agility)
			.OrderByDescending(tie => tie.Key);

		foreach (var tie in ties)
		{
			List<Combatant> left = tie
				.OrderBy(combatant => combatant.Id.Side)
				.ThenBy(combatant => combatant.Id.Position)
				.ToList();

			while (left.Count > 1)
			{
				int picked = random.Index(left.Count);
				order.Add(left[picked]);
				left.RemoveAt(picked);
			}

			order.Add(left[0]);
		}

		return order;
	}
}
