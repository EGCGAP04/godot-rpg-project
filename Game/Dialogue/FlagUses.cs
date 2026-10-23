using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Which flags a condition reads or an effect writes, with the type each use needs, and
/// what is wrong with a use according to the flag catalogue. Shared by
/// <see cref="DialogueValidator"/> and <see cref="WorldAccess"/>, so a flag misused in a
/// conversation and one misused in <c>world_access.json</c> are reported in the same words.
/// </summary>
public static class FlagUses
{
	/// <summary>Each flag <paramref name="condition"/> reads, with the type the reading needs. None when it is null.</summary>
	/// <remarks>The tree is plain data, so walking it is a switch over the kinds of node.</remarks>
	public static IEnumerable<(string Name, FlagType Type)> InCondition(FlagCondition condition) => condition switch
	{
		null => Enumerable.Empty<(string, FlagType)>(),
		FlagCondition.IsSet isSet => new[] { (isSet.Flag, FlagType.Bool) },
		FlagCondition.Comparison comparison => new[] { (comparison.Counter, FlagType.Counter) },
		FlagCondition.Not negation => InCondition(negation.Operand),
		FlagCondition.And both => InCondition(both.Left).Concat(InCondition(both.Right)),
		FlagCondition.Or either => InCondition(either.Left).Concat(InCondition(either.Right)),
		_ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Not a known kind of condition.")
	};

	/// <summary>The flag <paramref name="effect"/> writes, with the type the writing needs.</summary>
	public static (string Name, FlagType Type) InEffect(FlagEffect effect) => effect switch
	{
		FlagEffect.Set setting => (setting.Flag, FlagType.Bool),
		FlagEffect.Add addition => (addition.Counter, FlagType.Counter),
		_ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Not a known kind of effect.")
	};

	/// <summary>Checks one use of a flag against the catalogue.</summary>
	/// <param name="inEffect">Whether the use writes the flag, which changes how the right use is described.</param>
	/// <returns>What is wrong with this use of a flag, as the end of a sentence, or null when nothing is.</returns>
	public static string Problem(FlagCatalogue catalogue, string name, FlagType needed, bool inEffect)
	{
		if (!catalogue.Flags.TryGetValue(name, out FlagDeclaration declaration))
			return $"'{name}' is not declared in {FlagCatalogue.FileName}";

		if (declaration.Type == needed)
			return null;

		return (declaration.Type, inEffect) switch
		{
			(FlagType.Counter, false) => $"'{name}' is a counter, so it is compared with a number, as in '{name} >= 1'",
			(FlagType.Bool, false) => $"'{name}' is a boolean flag, so it is tested on its own, without a comparison",
			(FlagType.Counter, true) => $"'{name}' is a counter, so it changes with 'add', not with 'set' or 'clear'",
			_ => $"'{name}' is a boolean flag, so it changes with 'set' or 'clear', not with 'add'"
		};
	}
}
