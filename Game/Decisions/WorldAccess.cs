using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// What opens each of a cycle's optional worlds, read from
/// <c>Game/Decisions/world_access.json</c>: one condition per world, written in the
/// dialogue's condition language and evaluated against the decision registry's flags.
/// </summary>
/// <remarks>
/// <para>
/// The file holds exactly <c>fantasy</c> and <c>nightmare</c>. <see cref="World.Real"/>
/// is never in it, because every cycle starts there.
/// </para>
/// <para>
/// That the Nightmare world needs the Fantasy world is not in the file but in
/// <see cref="Unlocks"/>, so no data can break it: with the Fantasy world closed, the
/// Nightmare world is closed too, whatever its own condition says.
/// </para>
/// </remarks>
public sealed class WorldAccess
{
	/// <summary>The version of the file format this game reads.</summary>
	public const int Format = 1;

	/// <summary>The name the file's errors give it.</summary>
	public const string FileName = "world_access.json";

	private static readonly string[] FileProperties = { "format", "worlds" };
	private static readonly string[] Worlds = { "fantasy", "nightmare" };

	private WorldAccess(FlagCondition fantasy, FlagCondition nightmare)
	{
		Fantasy = fantasy;
		Nightmare = nightmare;
	}

	/// <summary>The condition that opens the Fantasy world.</summary>
	public FlagCondition Fantasy { get; }

	/// <summary>The condition that opens the Nightmare world, once the Fantasy world is open.</summary>
	public FlagCondition Nightmare { get; }

	/// <summary>Reads the file from its text.</summary>
	/// <exception cref="DataFileException">The text is not a valid file, or one of its conditions does not parse.</exception>
	public static WorldAccess Read(string json)
	{
		using JsonDocument document = StrictJson.Parse(FileName, json);
		Dictionary<string, JsonElement> fields = StrictJson.Properties(document.RootElement, FileName, "", FileProperties, FileProperties);
		int format = StrictJson.Integer(fields["format"], FileName, "", "format");

		if (format != Format)
			throw new DataFileException(FileName, "", $"format {format} is not supported; this game reads format {Format}");

		// Said apart from any other unknown world, because it is the one most likely to be added.
		if (fields["worlds"].ValueKind == JsonValueKind.Object && fields["worlds"].TryGetProperty("real", out _))
			throw new DataFileException(FileName, "worlds", "'real' has no condition, because every cycle starts there");

		Dictionary<string, JsonElement> worlds = StrictJson.Properties(fields["worlds"], FileName, "worlds", Worlds, Worlds);

		return new WorldAccess(ReadCondition(worlds, "fantasy"), ReadCondition(worlds, "nightmare"));
	}

	/// <summary>
	/// Checks every flag the conditions read against <paramref name="catalogue"/>: it has to
	/// be declared, and read with its type.
	/// </summary>
	/// <returns>
	/// Every problem found, each a sentence that starts with the file's name, in the same
	/// words <see cref="DialogueValidator"/> uses. Empty when everything is valid.
	/// </returns>
	public IReadOnlyList<string> Validate(FlagCatalogue catalogue)
	{
		ArgumentNullException.ThrowIfNull(catalogue);

		var problems = new List<string>();

		foreach ((string world, FlagCondition condition) in new[] { ("fantasy", Fantasy), ("nightmare", Nightmare) })
		{
			foreach ((string name, FlagType type) in FlagUses.InCondition(condition))
			{
				string problem = FlagUses.Problem(catalogue, name, type, inEffect: false);

				if (problem != null)
					problems.Add($"{FileName}: world '{world}': {problem}.");
			}
		}

		return problems;
	}

	/// <summary>
	/// Which optional worlds are open with the flags as they are now. The Nightmare world
	/// is only open when the Fantasy world is too.
	/// </summary>
	public WorldUnlocks Unlocks(IFlagStore flags)
	{
		ArgumentNullException.ThrowIfNull(flags);

		bool fantasy = Fantasy.Evaluate(flags);

		return new WorldUnlocks(Fantasy: fantasy, Nightmare: fantasy && Nightmare.Evaluate(flags));
	}

	private static FlagCondition ReadCondition(Dictionary<string, JsonElement> worlds, string world)
	{
		string where = $"world '{world}'";
		string text = StrictJson.Text(worlds[world], FileName, where, world);

		try
		{
			return FlagConditionParser.Parse(text);
		}
		catch (FlagSyntaxException exception)
		{
			throw new DataFileException(FileName, where, exception.Message.TrimEnd('.'));
		}
	}
}
