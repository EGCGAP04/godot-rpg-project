/// <summary>
/// A dialogue effect: one entry of the <c>do</c> list of a line or an option, as built
/// by <see cref="FlagEffectParser"/>.
/// </summary>
public abstract record FlagEffect
{
	/// <summary>Applies the effect to what <paramref name="context"/> holds.</summary>
	public abstract void Apply(DialogueContext context);

	/// <summary>
	/// <c>set</c> a boolean flag, when <see cref="Value"/> is <c>true</c>, or <c>clear</c>
	/// it, when <c>false</c>.
	/// </summary>
	public sealed record Set(string Flag, bool Value) : FlagEffect
	{
		public override void Apply(DialogueContext context) => context.Flags.SetFlag(Flag, Value);
	}

	/// <summary><c>add</c> to a counter. A negative <see cref="Amount"/> subtracts.</summary>
	public sealed record Add(string Counter, int Amount) : FlagEffect
	{
		public override void Apply(DialogueContext context) => context.Flags.AddToCounter(Counter, Amount);
	}
}
