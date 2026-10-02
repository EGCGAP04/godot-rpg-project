/// <summary>
/// A dialogue effect: one entry of the <c>do</c> list of a line or an option, as built
/// by <see cref="FlagEffectParser"/>.
/// </summary>
public abstract record FlagEffect
{
	/// <summary>Applies the effect to the values in <paramref name="flags"/>.</summary>
	public abstract void Apply(IFlagStore flags);

	/// <summary>
	/// <c>set</c> a boolean flag, when <see cref="Value"/> is <c>true</c>, or <c>clear</c>
	/// it, when <c>false</c>.
	/// </summary>
	public sealed record Set(string Flag, bool Value) : FlagEffect
	{
		public override void Apply(IFlagStore flags) => flags.SetFlag(Flag, Value);
	}

	/// <summary><c>add</c> to a counter. A negative <see cref="Amount"/> subtracts.</summary>
	public sealed record Add(string Counter, int Amount) : FlagEffect
	{
		public override void Apply(IFlagStore flags) => flags.AddToCounter(Counter, Amount);
	}
}
