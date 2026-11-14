using System;

/// <summary>
/// A dialogue condition: the <c>if</c> of a line or an option, as a tree built by
/// <see cref="FlagConditionParser"/>. Each kind of node is a nested record, so two trees
/// compare by value and print themselves readably, which is what the tests rely on.
/// </summary>
public abstract record FlagCondition
{
	/// <summary>
	/// Whether the condition holds for what <paramref name="context"/> holds. Only ever
	/// reads from it.
	/// </summary>
	public abstract bool Evaluate(DialogueContext context);

	/// <summary>A boolean flag is set: <c>example.has_key</c>.</summary>
	public sealed record IsSet(string Flag) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => context.Flags.GetFlag(Flag);
	}

	/// <summary>A counter compared with a number: <c>example.times_met &gt;= 1</c>.</summary>
	public sealed record Comparison(string Counter, Comparator Operator, int Value) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context)
		{
			int current = context.Flags.GetCounter(Counter);

			return Operator switch
			{
				Comparator.Equal => current == Value,
				Comparator.NotEqual => current != Value,
				Comparator.Less => current < Value,
				Comparator.LessOrEqual => current <= Value,
				Comparator.Greater => current > Value,
				Comparator.GreaterOrEqual => current >= Value,
				_ => throw new ArgumentOutOfRangeException(nameof(Operator), Operator, "Not a declared comparator.")
			};
		}
	}

	/// <summary>A character is in the party: <c>in_party demo_friend</c>.</summary>
	public sealed record InParty(string Character) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => context.Party.IsMember(Character);
	}

	/// <summary>The party holds as many as it can: <c>party_full</c>.</summary>
	public sealed record PartyFull : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => context.Party.IsFull;
	}

	/// <summary><c>not</c>: the operand does not hold.</summary>
	public sealed record Not(FlagCondition Operand) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => !Operand.Evaluate(context);
	}

	/// <summary><c>and</c>: both sides hold. The right side is skipped when the left one fails.</summary>
	public sealed record And(FlagCondition Left, FlagCondition Right) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => Left.Evaluate(context) && Right.Evaluate(context);
	}

	/// <summary><c>or</c>: at least one side holds. The right side is skipped when the left one holds.</summary>
	public sealed record Or(FlagCondition Left, FlagCondition Right) : FlagCondition
	{
		public override bool Evaluate(DialogueContext context) => Left.Evaluate(context) || Right.Evaluate(context);
	}

	/// <summary>The six ways of comparing a counter with a number.</summary>
	public enum Comparator
	{
		Equal,
		NotEqual,
		Less,
		LessOrEqual,
		Greater,
		GreaterOrEqual
	}
}
