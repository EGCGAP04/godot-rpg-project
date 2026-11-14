using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Which characters a condition asks about or an effect changes, and what is wrong with
/// a use according to the roster. The party's counterpart of <see cref="FlagUses"/>:
/// shared by <see cref="DialogueValidator"/> and the debug presets, so a character
/// misused in a conversation and one misused in a preset are reported in the same words.
/// </summary>
public static class PartyUses
{
	/// <summary>Each character <paramref name="condition"/> asks about with <c>in_party</c>. None when it is null.</summary>
	public static IEnumerable<string> InCondition(FlagCondition condition) => condition switch
	{
		null or FlagCondition.IsSet or FlagCondition.Comparison or FlagCondition.PartyFull => Enumerable.Empty<string>(),
		FlagCondition.InParty membership => new[] { membership.Character },
		FlagCondition.Not negation => InCondition(negation.Operand),
		FlagCondition.And both => InCondition(both.Left).Concat(InCondition(both.Right)),
		FlagCondition.Or either => InCondition(either.Left).Concat(InCondition(either.Right)),
		_ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Not a known kind of condition.")
	};

	/// <summary>
	/// Whether <paramref name="condition"/> asks anything about the party, with
	/// <c>in_party</c> or with <c>party_full</c>, which names nobody.
	/// </summary>
	public static bool AsksAboutTheParty(FlagCondition condition) => condition switch
	{
		null or FlagCondition.IsSet or FlagCondition.Comparison => false,
		FlagCondition.InParty or FlagCondition.PartyFull => true,
		FlagCondition.Not negation => AsksAboutTheParty(negation.Operand),
		FlagCondition.And both => AsksAboutTheParty(both.Left) || AsksAboutTheParty(both.Right),
		FlagCondition.Or either => AsksAboutTheParty(either.Left) || AsksAboutTheParty(either.Right),
		_ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Not a known kind of condition.")
	};

	/// <summary>The character <paramref name="effect"/> recruits or dismisses. Null when it writes a flag instead.</summary>
	public static string InEffect(FlagEffect effect) => effect switch
	{
		FlagEffect.Recruit recruit => recruit.Character,
		FlagEffect.Dismiss dismiss => dismiss.Character,
		FlagEffect.Set or FlagEffect.Add => null,
		_ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Not a known kind of effect.")
	};

	/// <summary>Checks one <c>in_party</c> against the roster.</summary>
	/// <returns>What is wrong with asking about this character, as the end of a sentence, or null when nothing is.</returns>
	public static string ProblemInCondition(CharacterRosterValues roster, string character) =>
		Unknown(roster, character);

	/// <summary>Checks one effect against the roster. An effect on a flag has nothing to check here.</summary>
	/// <returns>What is wrong with this effect, as the end of a sentence, or null when nothing is.</returns>
	/// <remarks>
	/// The protagonist is refused here because the party would refuse it every time: they
	/// are always in it, and never leave. What depends on the party as it is while playing,
	/// like recruiting into a full one, cannot be known here.
	/// </remarks>
	public static string ProblemInEffect(CharacterRosterValues roster, FlagEffect effect)
	{
		string character = InEffect(effect);

		if (character == null)
			return null;

		if (Unknown(roster, character) is string unknown)
			return unknown;

		if (character != roster.Protagonist.Id)
			return null;

		return effect is FlagEffect.Recruit
			? $"'{character}' is the protagonist, who is always in the party, so 'recruit' is always refused"
			: $"'{character}' is the protagonist, who never leaves the party, so 'dismiss' is always refused";
	}

	private static string Unknown(CharacterRosterValues roster, string character) =>
		roster.Contains(character)
			? null
			: $"'{character}' is not a character of the roster, which has: {string.Join(", ", roster.Characters.Select(each => each.Id))}";
}
