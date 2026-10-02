using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>The kinds of token in the language dialogue conditions and effects are written in.</summary>
public enum FlagTokenKind
{
	/// <summary>A flag or counter name: lowercase words joined by dots, such as <c>example.has_key</c>.</summary>
	Name,

	/// <summary>A whole number, optionally negative.</summary>
	Integer,

	/// <summary>A reserved word: <c>and</c>, <c>or</c>, <c>not</c>, <c>set</c>, <c>clear</c> or <c>add</c>.</summary>
	Keyword,

	/// <summary>One of <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c>.</summary>
	Comparator,

	OpenParen,

	CloseParen,

	/// <summary>
	/// Always the last token, one column past the text, so a parser can look at the next
	/// token without first checking whether there is one.
	/// </summary>
	End
}

/// <summary>One token, with the 1-based column where it starts so an error can point at it.</summary>
public readonly record struct FlagToken(FlagTokenKind Kind, string Text, int Column)
{
	/// <summary>How an error message names this token: quoted, or "the end of the text".</summary>
	public string Describe() => Kind == FlagTokenKind.End ? "the end of the text" : $"'{Text}'";
}

/// <summary>
/// Splits the text of a dialogue condition or effect into tokens: the first step of
/// reading it. <see cref="FlagConditionParser"/> and <see cref="FlagEffectParser"/>
/// only ever look at tokens, never at the raw characters, so everything about spaces,
/// digits and letters is decided here, once.
/// </summary>
public static class FlagTokenizer
{
	/// <summary>Words with a meaning of their own, which can never be a name or part of one.</summary>
	public static readonly IReadOnlySet<string> Keywords = new HashSet<string> { "and", "or", "not", "set", "clear", "add" };

	/// <summary>
	/// Returns the tokens of <paramref name="text"/>, always ending with an
	/// <see cref="FlagTokenKind.End"/> token placed one column past the last character.
	/// </summary>
	/// <exception cref="FlagSyntaxException">Something in the text is not a token.</exception>
	public static IReadOnlyList<FlagToken> Tokenize(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		var tokens = new List<FlagToken>();
		int position = 0;

		while (position < text.Length)
		{
			char current = text[position];
			int column = position + 1;

			if (current is ' ' or '\t')
			{
				position++;
			}
			else if (current is '(' or ')')
			{
				FlagTokenKind kind = current == '(' ? FlagTokenKind.OpenParen : FlagTokenKind.CloseParen;
				tokens.Add(new FlagToken(kind, current.ToString(), column));
				position++;
			}
			else if (current is '=' or '!' or '<' or '>')
			{
				// '<' and '>' work alone or with '='; '=' and '!' only as "==" and "!=".
				bool withEquals = position + 1 < text.Length && text[position + 1] == '=';

				if (!withEquals && current == '=')
					throw new FlagSyntaxException("'=' alone is not an operator; compare with '=='", column);

				if (!withEquals && current == '!')
					throw new FlagSyntaxException("'!' alone is not an operator; negate with 'not'", column);

				int length = withEquals ? 2 : 1;
				tokens.Add(new FlagToken(FlagTokenKind.Comparator, text.Substring(position, length), column));
				position += length;
			}
			else if (char.IsAsciiDigit(current) || (current == '-' && position + 1 < text.Length && char.IsAsciiDigit(text[position + 1])))
			{
				int end = position + 1;

				while (end < text.Length && char.IsAsciiDigit(text[end]))
					end++;

				string number = text[position..end];

				if (!int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
					throw new FlagSyntaxException($"the number {number} is out of range", column);

				tokens.Add(new FlagToken(FlagTokenKind.Integer, number, column));
				position = end;
			}
			else if (char.IsAsciiLetterLower(current))
			{
				int end = position + 1;

				while (end < text.Length && IsWordCharacter(text[end]))
					end++;

				string word = text[position..end];

				if (Keywords.Contains(word))
				{
					tokens.Add(new FlagToken(FlagTokenKind.Keyword, word, column));
				}
				else
				{
					CheckName(word, column);
					tokens.Add(new FlagToken(FlagTokenKind.Name, word, column));
				}

				position = end;
			}
			else if (char.IsAsciiLetterUpper(current))
			{
				throw new FlagSyntaxException($"names are lowercase, but found '{current}'", column);
			}
			else
			{
				throw new FlagSyntaxException($"unexpected character '{current}'", column);
			}
		}

		tokens.Add(new FlagToken(FlagTokenKind.End, "", text.Length + 1));
		return tokens;
	}

	private static bool IsWordCharacter(char character) =>
		char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '_' or '.';

	/// <summary>
	/// A name is lowercase words joined by single dots, each word starting with a letter
	/// and none of them reserved: <c>example.has_key</c>, but not <c>example..key</c>,
	/// <c>example.1st</c> or <c>example.and</c>.
	/// </summary>
	private static void CheckName(string name, int column)
	{
		foreach (string part in name.Split('.'))
		{
			if (part.Length == 0)
				throw new FlagSyntaxException($"'{name}' has an empty part; a name is words joined by single dots", column);

			if (!char.IsAsciiLetterLower(part[0]))
				throw new FlagSyntaxException($"'{name}' has a part that does not start with a letter", column);

			if (Keywords.Contains(part))
				throw new FlagSyntaxException($"'{name}' uses the reserved word '{part}' as part of a name", column);
		}
	}
}
