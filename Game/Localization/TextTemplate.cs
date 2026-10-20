using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Fills the named placeholders of a translated text: <c>{hp}/{max_hp}</c> becomes
/// <c>7/10</c>.
/// </summary>
/// <remarks>
/// <para>
/// A placeholder is a word of the dialogue format between braces, so each language can
/// put them in whatever order reads naturally, and whoever translates sees what goes
/// where. Any other brace is plain text.
/// </para>
/// <para>
/// A placeholder with no value is left showing instead of throwing. Godot's
/// pseudolocalization rewrites placeholders along with the rest of the text (<c>{hp}</c>
/// comes back as <c>{h̀ṕ}</c>), and throwing there would break the very run meant to
/// find untranslated text. Whether every language uses the same placeholders is checked
/// by the tests instead, through <see cref="Placeholders"/>.
/// </para>
/// </remarks>
public static class TextTemplate
{
	/// <summary>Replaces each placeholder of <paramref name="template"/> with its value.</summary>
	/// <param name="template">A translated text, as <c>Tr()</c> returns it.</param>
	/// <param name="values">
	/// Each placeholder's name and value. Values are written with the invariant culture, so
	/// a number reads the same whatever the system's settings are; pass a string to format
	/// one differently. A value no placeholder uses is ignored.
	/// </param>
	/// <exception cref="ArgumentException">The same name is given twice.</exception>
	public static string Fill(string template, params (string Name, object Value)[] values)
	{
		ArgumentNullException.ThrowIfNull(template);
		ArgumentNullException.ThrowIfNull(values);

		var byName = new Dictionary<string, string>();

		foreach ((string name, object value) in values)
		{
			if (!byName.TryAdd(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""))
				throw new ArgumentException($"'{name}' is given more than once.", nameof(values));
		}

		var text = new StringBuilder();
		int copiedUpTo = 0;

		foreach ((int start, int end, string name) in Find(template))
		{
			if (!byName.TryGetValue(name, out string value))
				continue;

			text.Append(template, copiedUpTo, start - copiedUpTo).Append(value);
			copiedUpTo = end;
		}

		return text.Append(template, copiedUpTo, template.Length - copiedUpTo).ToString();
	}

	/// <summary>The names of the placeholders in <paramref name="template"/>, each once.</summary>
	public static IReadOnlySet<string> Placeholders(string template)
	{
		ArgumentNullException.ThrowIfNull(template);

		var names = new HashSet<string>();

		foreach ((_, _, string name) in Find(template))
			names.Add(name);

		return names;
	}

	/// <summary>
	/// Every placeholder in <paramref name="template"/>, in order: where it starts, where
	/// the text after it starts, and its name.
	/// </summary>
	private static IEnumerable<(int Start, int End, string Name)> Find(string template)
	{
		int position = 0;

		while (true)
		{
			int open = template.IndexOf('{', position);

			if (open < 0)
				yield break;

			int close = template.IndexOf('}', open + 1);

			if (close < 0)
				yield break;

			string name = template[(open + 1)..close];

			// Not a placeholder: the brace is text, and the next one may still open one,
			// as the second brace of "{{hp}" does.
			if (!DialogueNames.IsWord(name))
			{
				position = open + 1;
				continue;
			}

			yield return (open, close + 1, name);
			position = close + 1;
		}
	}
}
