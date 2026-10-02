using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Turns the text of a dialogue condition into a <see cref="FlagCondition"/> tree.
/// </summary>
/// <remarks>
/// <para>
/// A recursive descent parser: one method per rule of the grammar below, each calling
/// the rule under it for its operands. Precedence falls out of that order. <c>or</c>
/// asks <c>and</c> for its operands, <c>and</c> asks <c>not</c>, and <c>not</c> asks
/// for a single condition, so <c>a or b and c</c> reads as <c>a or (b and c)</c>, the
/// same as in C#.
/// </para>
/// <code>
/// condition  = and_expr { "or" and_expr }
/// and_expr   = unary { "and" unary }
/// unary      = "not" unary | primary
/// primary    = "(" condition ")" | name [ comparator integer ]
/// </code>
/// <para>
/// Whether a name is a boolean or a counter is not decided here: a name on its own is
/// read as a boolean and a compared one as a counter, and checking that against the
/// flag catalogue is the validator's job.
/// </para>
/// </remarks>
public static class FlagConditionParser
{
	/// <exception cref="FlagSyntaxException">The text is not a valid condition.</exception>
	public static FlagCondition Parse(string text)
	{
		var reader = new TokenReader(FlagTokenizer.Tokenize(text));

		if (reader.Peek.Kind == FlagTokenKind.End)
			throw new FlagSyntaxException("expected a condition, but the text is empty", reader.Peek.Column);

		FlagCondition condition = ParseOr(reader);

		if (reader.Peek.Kind != FlagTokenKind.End)
			throw new FlagSyntaxException($"unexpected {reader.Peek.Describe()} after a complete condition", reader.Peek.Column);

		return condition;
	}

	// condition = and_expr { "or" and_expr }
	private static FlagCondition ParseOr(TokenReader reader)
	{
		FlagCondition condition = ParseAnd(reader);

		while (reader.TakeKeyword("or"))
			condition = new FlagCondition.Or(condition, ParseAnd(reader));

		return condition;
	}

	// and_expr = unary { "and" unary }
	private static FlagCondition ParseAnd(TokenReader reader)
	{
		FlagCondition condition = ParseNot(reader);

		while (reader.TakeKeyword("and"))
			condition = new FlagCondition.And(condition, ParseNot(reader));

		return condition;
	}

	// unary = "not" unary | primary
	private static FlagCondition ParseNot(TokenReader reader) =>
		reader.TakeKeyword("not") ? new FlagCondition.Not(ParseNot(reader)) : ParsePrimary(reader);

	// primary = "(" condition ")" | name [ comparator integer ]
	private static FlagCondition ParsePrimary(TokenReader reader)
	{
		FlagToken token = reader.Take();

		if (token.Kind == FlagTokenKind.OpenParen)
		{
			FlagCondition inner = ParseOr(reader);

			if (reader.Peek.Kind != FlagTokenKind.CloseParen)
				throw new FlagSyntaxException($"missing ')' for the '(' at column {token.Column}", reader.Peek.Column);

			reader.Take();
			return inner;
		}

		if (token.Kind != FlagTokenKind.Name)
			throw new FlagSyntaxException($"expected a flag name, 'not' or '(', but found {token.Describe()}", token.Column);

		if (reader.Peek.Kind != FlagTokenKind.Comparator)
			return new FlagCondition.IsSet(token.Text);

		FlagToken comparator = reader.Take();
		FlagToken number = reader.Take();

		if (number.Kind != FlagTokenKind.Integer)
			throw new FlagSyntaxException($"expected a number after '{comparator.Text}', but found {number.Describe()}", number.Column);

		return new FlagCondition.Comparison(token.Text, ToComparator(comparator.Text), int.Parse(number.Text, CultureInfo.InvariantCulture));
	}

	private static FlagCondition.Comparator ToComparator(string text) => text switch
	{
		"==" => FlagCondition.Comparator.Equal,
		"!=" => FlagCondition.Comparator.NotEqual,
		"<" => FlagCondition.Comparator.Less,
		"<=" => FlagCondition.Comparator.LessOrEqual,
		">" => FlagCondition.Comparator.Greater,
		">=" => FlagCondition.Comparator.GreaterOrEqual,
		_ => throw new ArgumentOutOfRangeException(nameof(text), text, "Not a comparator.")
	};

	/// <summary>
	/// Walks the token list for the parser. The list always ends with an
	/// <see cref="FlagTokenKind.End"/> token, and <see cref="Take"/> never moves past it,
	/// so <see cref="Peek"/> is always safe.
	/// </summary>
	private sealed class TokenReader
	{
		private readonly IReadOnlyList<FlagToken> _tokens;
		private int _next;

		public TokenReader(IReadOnlyList<FlagToken> tokens)
		{
			_tokens = tokens;
		}

		public FlagToken Peek => _tokens[_next];

		public FlagToken Take()
		{
			FlagToken token = _tokens[_next];

			if (token.Kind != FlagTokenKind.End)
				_next++;

			return token;
		}

		/// <summary>Takes the next token if it is <paramref name="keyword"/>, and says whether it did.</summary>
		public bool TakeKeyword(string keyword)
		{
			if (Peek.Kind != FlagTokenKind.Keyword || Peek.Text != keyword)
				return false;

			_next++;
			return true;
		}
	}
}
