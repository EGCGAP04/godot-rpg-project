/// <summary>
/// A dialogue effect: one entry of the <c>do</c> list of a line or an option, as built
/// by <see cref="FlagEffectParser"/>.
/// </summary>
/// <remarks>
/// An effect on a flag always applies. One on the party can be refused, when the party's
/// rules do not allow it: it then changes nothing and says why, and whoever applied it
/// decides what that means. A conversation reports it and goes on; a debug preset stops
/// the game.
/// </remarks>
public abstract record FlagEffect
{
	/// <summary>Applies the effect to what <paramref name="context"/> holds, unless it is refused.</summary>
	/// <returns>
	/// Whether it was applied. When not, nothing changed and <paramref name="refusal"/> is a
	/// sentence saying why; otherwise it is null.
	/// </returns>
	public abstract bool TryApply(DialogueContext context, out string refusal);

	/// <summary>The effect as it is written in the language, such as <c>set example.has_key</c>, for a message.</summary>
	public abstract string Describe();

	/// <summary>
	/// <c>set</c> a boolean flag, when <see cref="Value"/> is <c>true</c>, or <c>clear</c>
	/// it, when <c>false</c>.
	/// </summary>
	public sealed record Set(string Flag, bool Value) : FlagEffect
	{
		public override bool TryApply(DialogueContext context, out string refusal)
		{
			context.Flags.SetFlag(Flag, Value);
			refusal = null;
			return true;
		}

		public override string Describe() => $"{(Value ? "set" : "clear")} {Flag}";
	}

	/// <summary><c>add</c> to a counter. A negative <see cref="Amount"/> subtracts.</summary>
	public sealed record Add(string Counter, int Amount) : FlagEffect
	{
		public override bool TryApply(DialogueContext context, out string refusal)
		{
			context.Flags.AddToCounter(Counter, Amount);
			refusal = null;
			return true;
		}

		public override string Describe() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"add {Counter} {Amount}");
	}

	/// <summary><c>recruit</c> a character into the party. Refused when they are already in it, or it is full.</summary>
	public sealed record Recruit(string Character) : FlagEffect
	{
		public override bool TryApply(DialogueContext context, out string refusal) => context.Party.TryRecruit(Character, out refusal);

		public override string Describe() => $"recruit {Character}";
	}

	/// <summary><c>dismiss</c> a character from the party. Refused when they are the protagonist, or not in it.</summary>
	public sealed record Dismiss(string Character) : FlagEffect
	{
		public override bool TryApply(DialogueContext context, out string refusal) => context.Party.TryDismiss(Character, out refusal);

		public override string Describe() => $"dismiss {Character}";
	}
}
