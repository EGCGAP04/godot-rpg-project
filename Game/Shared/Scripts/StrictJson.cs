using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Strict reading of the game's JSON data files, shared by <see cref="ConversationReader"/>
/// and <see cref="FlagCatalogue"/>.
/// </summary>
/// <remarks>
/// Every object is read property by property instead of deserialized into a class. JSON
/// allows an object to repeat a property, and a deserializer quietly keeps one of the
/// two and ignores any property it does not know. Read by hand, a repeated, unknown or
/// missing property is an error that says where it is.
/// </remarks>
internal static class StrictJson
{
	/// <summary>Parses standard JSON: no comments and no trailing commas.</summary>
	/// <exception cref="DataFileException">The text is not valid JSON.</exception>
	public static JsonDocument Parse(string file, string json)
	{
		try
		{
			return JsonDocument.Parse(json);
		}
		catch (JsonException exception)
		{
			// The parser's message ends with its own position; the location says it instead.
			string reason = exception.Message;
			int cut = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);

			if (cut >= 0)
				reason = reason[..cut];

			string location = exception.LineNumber is long line && exception.BytePositionInLine is long column
				? $"line {line + 1}, column {column + 1}"
				: "";

			throw new DataFileException(file, location, $"not valid JSON: {reason.TrimEnd('.', ' ')}");
		}
	}

	/// <summary>The properties of the object <paramref name="element"/>, by name.</summary>
	/// <param name="allowed">The only names allowed, or null to allow any name, as node ids are.</param>
	/// <param name="required">The names that must be present.</param>
	/// <exception cref="DataFileException">
	/// The element is not an object, or one of its properties is repeated, unknown or missing.
	/// </exception>
	public static Dictionary<string, JsonElement> Properties(
		JsonElement element, string file, string location, string[] allowed, string[] required)
	{
		if (element.ValueKind != JsonValueKind.Object)
			throw new DataFileException(file, location, $"expected an object, but found {Describe(element)}");

		var properties = new Dictionary<string, JsonElement>();

		foreach (JsonProperty property in element.EnumerateObject())
		{
			if (allowed != null && !allowed.Contains(property.Name))
			{
				string expected = string.Join(", ", allowed.Select(name => $"'{name}'"));
				throw new DataFileException(file, location, $"unknown property '{property.Name}'; the ones allowed here are {expected}");
			}

			if (!properties.TryAdd(property.Name, property.Value))
				throw new DataFileException(file, location, $"'{property.Name}' appears twice");
		}

		foreach (string name in required)
		{
			if (!properties.ContainsKey(name))
				throw new DataFileException(file, location, $"missing '{name}'");
		}

		return properties;
	}

	/// <exception cref="DataFileException">The element is not text.</exception>
	public static string Text(JsonElement element, string file, string location, string property)
	{
		if (element.ValueKind != JsonValueKind.String)
			throw new DataFileException(file, location, $"'{property}' should be text, but it is {Describe(element)}");

		return element.GetString();
	}

	/// <exception cref="DataFileException">The element is not a whole number that fits an int.</exception>
	public static int Integer(JsonElement element, string file, string location, string property)
	{
		if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
		{
			string found = element.ValueKind == JsonValueKind.Number ? element.GetRawText() : Describe(element);
			throw new DataFileException(file, location, $"'{property}' should be a whole number, but it is {found}");
		}

		return value;
	}

	/// <exception cref="DataFileException">The element is not a list.</exception>
	public static List<JsonElement> List(JsonElement element, string file, string location, string property)
	{
		if (element.ValueKind != JsonValueKind.Array)
			throw new DataFileException(file, location, $"'{property}' should be a list, but it is {Describe(element)}");

		return element.EnumerateArray().ToList();
	}

	private static string Describe(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.Object => "an object",
		JsonValueKind.Array => "a list",
		JsonValueKind.String => "text",
		JsonValueKind.Number => "a number",
		JsonValueKind.True or JsonValueKind.False => "true or false",
		JsonValueKind.Null => "null",
		_ => "nothing"
	};
}
