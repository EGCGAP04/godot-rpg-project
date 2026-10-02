using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Turns the text of one dialogue effect into a <see cref="FlagEffect"/>.
/// </summary>
/// <remarks>
/// Effects come in three fixed shapes rather than a recursive grammar, so the tokens are
/// checked against them directly instead of going through a parser like
/// <see cref="FlagConditionParser"/>:
/// <code>
/// set   name
/// clear name
/// add   name integer
/// </code>
/// </remarks>
public static class FlagEffectParser
{
	/// <exception cref="FlagSyntaxException">The text is not a valid effect.</exception>
	public static FlagEffect Parse(string text)
	{
		IReadOnlyList<FlagToken> tokens = FlagTokenizer.Tokenize(text);
		FlagToken verb = tokens[0];

		if (verb.Kind == FlagTokenKind.End)
			throw new FlagSyntaxException("expected an effect, but the text is empty", verb.Column);

		if (verb.Kind != FlagTokenKind.Keyword || verb.Text is not ("set" or "clear" or "add"))
			throw new FlagSyntaxException($"an effect starts with 'set', 'clear' or 'add', not {verb.Describe()}", verb.Column);

		bool isAdd = verb.Text == "add";
		FlagToken name = tokens[1];

		if (name.Kind != FlagTokenKind.Name)
			throw new FlagSyntaxException($"expected a {(isAdd ? "counter" : "flag")} name after '{verb.Text}', but found {name.Describe()}", name.Column);

		FlagEffect effect;
		int next;

		if (isAdd)
		{
			FlagToken amount = tokens[2];

			if (amount.Kind != FlagTokenKind.Integer)
				throw new FlagSyntaxException($"expected the amount to add to '{name.Text}', but found {amount.Describe()}", amount.Column);

			effect = new FlagEffect.Add(name.Text, int.Parse(amount.Text, CultureInfo.InvariantCulture));
			next = 3;
		}
		else
		{
			effect = new FlagEffect.Set(name.Text, verb.Text == "set");
			next = 2;
		}

		// The list always ends with End, and the checks above guarantee it reaches here.
		FlagToken extra = tokens[next];

		if (extra.Kind != FlagTokenKind.End)
			throw new FlagSyntaxException($"unexpected {extra.Describe()} after a complete effect", extra.Column);

		return effect;
	}
}
