using System;

/// <summary>The two sides of a fight.</summary>
public enum CombatSide
{
	Party,
	Enemy
}

/// <summary>
/// Who a combatant is inside one fight: its side and its position on that side, counted
/// from 0 in the order the fight was given them. It says nothing about which character or
/// which kind of enemy it is, so two enemies of one kind are still two combatants.
/// </summary>
public readonly record struct CombatantId
{
	public CombatantId(CombatSide side, int position)
	{
		if (!Enum.IsDefined(side))
			throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side of a fight.");

		if (position < 0)
			throw new ArgumentOutOfRangeException(nameof(position), position, "A position cannot be negative.");

		Side = side;
		Position = position;
	}

	public CombatSide Side { get; }

	public int Position { get; }

	public static CombatantId Party(int position) => new(CombatSide.Party, position);

	public static CombatantId Enemy(int position) => new(CombatSide.Enemy, position);

	public override string ToString() => $"{Side} {Position}";
}
