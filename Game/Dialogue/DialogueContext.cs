using System;

/// <summary>
/// Everything the dialogue's small language can speak of: the flags and the party. A
/// condition is evaluated against one, and an effect is applied to one.
/// </summary>
/// <remarks>
/// It exists so that the language can learn to speak of something else without every
/// signature that carries it changing again: that would be one more property here.
/// </remarks>
public sealed record DialogueContext
{
	/// <exception cref="ArgumentNullException">Either store is missing.</exception>
	public DialogueContext(IFlagStore flags, IPartyStore party)
	{
		Flags = flags ?? throw new ArgumentNullException(nameof(flags));
		Party = party ?? throw new ArgumentNullException(nameof(party));
	}

	/// <summary>Where conditions read flags and effects write them.</summary>
	public IFlagStore Flags { get; }

	/// <summary>Where conditions ask about the party and effects change it.</summary>
	public IPartyStore Party { get; }

	/// <summary>
	/// A context for text that may only speak of flags, like <c>world_access.json</c>. Its
	/// party throws <see cref="InvalidOperationException"/> whatever it is asked, so text
	/// that was not checked first fails loudly instead of reading an empty party.
	/// </summary>
	public static DialogueContext FlagsOnly(IFlagStore flags) => new(flags, NoParty.Instance);

	private sealed class NoParty : IPartyStore
	{
		public static readonly NoParty Instance = new();

		public bool IsFull => throw Refuse();

		public bool IsMember(string character) => throw Refuse();

		public bool TryRecruit(string character, out string refusal) => throw Refuse();

		public bool TryDismiss(string character, out string refusal) => throw Refuse();

		private static InvalidOperationException Refuse() => new("This text may only speak of flags, and it asked about the party.");
	}
}
