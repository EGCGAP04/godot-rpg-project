using System;

/// <summary>
/// A tuning number that has one value against attacks and another against skills, such
/// as armor's cap: 90% and 80%.
/// </summary>
public readonly record struct ByAction(double Attack, double Skill)
{
	/// <summary>The value for one kind of action. A kind that is neither is refused, not guessed.</summary>
	public double For(ActionKind kind) => kind switch
	{
		ActionKind.Attack => Attack,
		ActionKind.Skill => Skill,
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of action.")
	};
}
