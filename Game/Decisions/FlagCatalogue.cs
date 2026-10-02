using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>The two types of flag.</summary>
public enum FlagType
{
	/// <summary>Set or not. Starts <c>false</c>.</summary>
	Bool,

	/// <summary>A whole number. Starts at 0.</summary>
	Counter
}

/// <summary>How long a flag keeps its value, which decides where a save keeps it.</summary>
public enum FlagLifetime
{
	/// <summary>Reset when a new cycle starts.</summary>
	Cycle,

	/// <summary>
	/// Belongs to one playthrough and its save slot, and goes back with it when an
	/// earlier save of that slot is loaded.
	/// </summary>
	Run,

	/// <summary>Belongs to the game: never goes back, and is shared by every save slot.</summary>
	Persistent
}

/// <summary>One flag of the catalogue.</summary>
/// <param name="Description">What the flag means, for the debug tooling. Empty when the file gives none.</param>
public sealed record FlagDeclaration(string Name, FlagType Type, FlagLifetime Lifetime, string Description);

/// <summary>
/// Every flag in the game, each declared once in <c>Game/Decisions/flags.json</c> with its
/// type and lifetime. A flag that is not here does not exist: the dialogue validator
/// checks conditions and effects against it, the decision registry takes each flag's
/// lifetime from it, and the debug tooling lists it.
/// </summary>
public sealed class FlagCatalogue
{
	/// <summary>The version of the catalogue format this game reads.</summary>
	public const int Format = 1;

	/// <summary>The name the catalogue's errors give it.</summary>
	public const string FileName = "flags.json";

	private static readonly string[] CatalogueProperties = { "format", "flags" };
	private static readonly string[] FlagProperties = { "type", "lifetime", "description" };
	private static readonly string[] RequiredFlagProperties = { "type", "lifetime" };

	private FlagCatalogue(IReadOnlyDictionary<string, FlagDeclaration> flags)
	{
		Flags = flags;
	}

	/// <summary>The declared flags, by name.</summary>
	public IReadOnlyDictionary<string, FlagDeclaration> Flags { get; }

	/// <summary>
	/// Whether <paramref name="name"/> follows the naming convention: a group and a fact,
	/// as words joined by dots (<c>example.has_key</c>), none of them reserved by the
	/// condition language.
	/// </summary>
	public static bool IsValidName(string name)
	{
		if (string.IsNullOrEmpty(name))
			return false;

		string[] parts = name.Split('.');

		if (parts.Length < 2)
			return false;

		foreach (string part in parts)
		{
			if (!DialogueNames.IsWord(part) || FlagTokenizer.Keywords.Contains(part))
				return false;
		}

		return true;
	}

	/// <summary>Reads the catalogue from the text of its file.</summary>
	/// <exception cref="DataFileException">The text is not a valid catalogue.</exception>
	public static FlagCatalogue Read(string json)
	{
		using JsonDocument document = StrictJson.Parse(FileName, json);
		Dictionary<string, JsonElement> fields = StrictJson.Properties(document.RootElement, FileName, "", CatalogueProperties, CatalogueProperties);
		int format = StrictJson.Integer(fields["format"], FileName, "", "format");

		if (format != Format)
			throw new DataFileException(FileName, "", $"format {format} is not supported; this game reads format {Format}");

		var flags = new Dictionary<string, FlagDeclaration>();

		foreach ((string name, JsonElement element) in StrictJson.Properties(fields["flags"], FileName, "flags", null, Array.Empty<string>()))
		{
			string where = $"flag '{name}'";

			if (!IsValidName(name))
				throw new DataFileException(FileName, where, "a flag is named by a group and a fact, as lowercase words joined by dots such as 'example.has_key', none of them a reserved word");

			Dictionary<string, JsonElement> entry = StrictJson.Properties(element, FileName, where, FlagProperties, RequiredFlagProperties);

			FlagType type = StrictJson.Text(entry["type"], FileName, where, "type") switch
			{
				"bool" => FlagType.Bool,
				"counter" => FlagType.Counter,
				var other => throw new DataFileException(FileName, where, $"unknown type '{other}'; a flag is a 'bool' or a 'counter'")
			};

			FlagLifetime lifetime = StrictJson.Text(entry["lifetime"], FileName, where, "lifetime") switch
			{
				"cycle" => FlagLifetime.Cycle,
				"run" => FlagLifetime.Run,
				"persistent" => FlagLifetime.Persistent,
				var other => throw new DataFileException(FileName, where, $"unknown lifetime '{other}'; it is 'cycle', 'run' or 'persistent'")
			};

			string description = entry.TryGetValue("description", out JsonElement text)
				? StrictJson.Text(text, FileName, where, "description")
				: "";

			flags.Add(name, new FlagDeclaration(name, type, lifetime, description));
		}

		return new FlagCatalogue(flags);
	}
}
