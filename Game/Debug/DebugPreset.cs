using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>One effect of a preset, with where it was written, so a problem can say so.</summary>
/// <param name="Source">Where it was written, such as <c>no_fantasy.json: do 1</c>.</param>
/// <param name="Effect">The effect, as <see cref="FlagEffectParser"/> built it.</param>
public sealed record PresetEffect(string Source, FlagEffect Effect);

/// <summary>
/// A state for the game to start in (#75): where the run starts, and effects on the
/// decision registry's flags, written in the dialogue's effect language. Read from a
/// preset file or from the command line, through <see cref="DebugArguments"/>.
/// Deliberately a plain C# class with no Godot dependency, so it is unit-tested without
/// the engine; the <c>DebugTools</c> autoload reads the files and applies the result.
/// </summary>
/// <remarks>
/// <para>
/// Every flag starts at its starting value, so an <c>add</c> sets a counter to its
/// amount. A preset may change a persistent flag too: it describes the whole game's
/// state, not one save slot's.
/// </para>
/// <para>
/// A file is read property by property through <see cref="StrictJson"/>, like every other
/// data file, so a repeated or unknown property is an error.
/// </para>
/// </remarks>
public sealed class DebugPreset
{
	/// <summary>The version of the file format this game reads.</summary>
	public const int Format = 1;

	private static readonly string[] FileProperties = { "format", "description", "world", "cycle", "do" };
	private static readonly string[] RequiredProperties = { "format", "description" };

	/// <param name="world">Where the run starts, or null to leave it to a preset applied before.</param>
	/// <param name="cycle">The cycle the run starts in, or null to leave it to a preset applied before.</param>
	/// <param name="effects">The effects, in the order they apply.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="cycle"/> is before the first cycle, or <paramref name="world"/> is not declared.</exception>
	public DebugPreset(World? world, int? cycle, IReadOnlyList<PresetEffect> effects)
	{
		ArgumentNullException.ThrowIfNull(effects);

		if (world is World declared && !Enum.IsDefined(declared))
			throw new ArgumentOutOfRangeException(nameof(world), world, "Not a declared world.");

		if (cycle < CycleProgression.FirstCycle)
			throw new ArgumentOutOfRangeException(nameof(cycle), cycle, $"Cycles are numbered from {CycleProgression.FirstCycle}.");

		World = world;
		Cycle = cycle;
		Effects = effects.ToArray();
	}

	/// <summary>A preset that changes nothing.</summary>
	public static DebugPreset None { get; } = new(null, null, Array.Empty<PresetEffect>());

	/// <summary>Where the run starts, or null when this preset does not say.</summary>
	public World? World { get; }

	/// <summary>The cycle the run starts in, or null when this preset does not say.</summary>
	public int? Cycle { get; }

	/// <summary>The effects on the flags, in the order they apply.</summary>
	public IReadOnlyList<PresetEffect> Effects { get; }

	/// <summary>Where the run starts: what the preset says, and the first world and cycle for what it does not.</summary>
	public CyclePosition Start => new(World ?? global::World.Real, Cycle ?? CycleProgression.FirstCycle);

	/// <summary>Reads a preset file from its text.</summary>
	/// <param name="file">The name its errors give it, such as <c>no_fantasy.json</c>.</param>
	/// <exception cref="DataFileException">The text is not a valid preset, or one of its effects does not parse.</exception>
	public static DebugPreset Read(string file, string json)
	{
		using JsonDocument document = StrictJson.Parse(file, json);
		Dictionary<string, JsonElement> fields = StrictJson.Properties(document.RootElement, file, "", FileProperties, RequiredProperties);
		int format = StrictJson.Integer(fields["format"], file, "", "format");

		if (format != Format)
			throw new DataFileException(file, "", $"format {format} is not supported; this game reads format {Format}");

		if (string.IsNullOrWhiteSpace(StrictJson.Text(fields["description"], file, "", "description")))
			throw new DataFileException(file, "", "'description' is empty; say what state the preset starts in");

		World? world = null;

		if (fields.TryGetValue("world", out JsonElement worldField))
		{
			string text = StrictJson.Text(worldField, file, "", "world");
			world = WorldNamed(text) ?? throw new DataFileException(file, "", $"'{text}' is not a world; the worlds are {WorldList}");
		}

		int? cycle = null;

		if (fields.TryGetValue("cycle", out JsonElement cycleField))
		{
			cycle = StrictJson.Integer(cycleField, file, "", "cycle");

			if (cycle < CycleProgression.FirstCycle)
				throw new DataFileException(file, "", $"'cycle' is {cycle}, but cycles are numbered from {CycleProgression.FirstCycle}");
		}

		var effects = new List<PresetEffect>();

		if (fields.TryGetValue("do", out JsonElement doField))
		{
			List<JsonElement> entries = StrictJson.List(doField, file, "", "do");

			for (int i = 0; i < entries.Count; i++)
			{
				string where = $"do {i + 1}";
				effects.Add(ParseEffect(StrictJson.Text(entries[i], file, where, "do"), file, where));
			}
		}

		return new DebugPreset(world, cycle, effects);
	}

	/// <summary>
	/// The world called <paramref name="name"/> in a preset: its name in lower case, as in
	/// <c>real</c>, or null when no world is called that.
	/// </summary>
	public static World? WorldNamed(string name) =>
		Enum.GetValues<World>().Cast<World?>().FirstOrDefault(world => world.ToString().ToLowerInvariant() == name);

	/// <summary>Every world's name in a preset, for error messages.</summary>
	public static string WorldList => string.Join(", ", Enum.GetValues<World>().Select(world => $"'{world.ToString().ToLowerInvariant()}'"));

	/// <summary>Parses one effect's text, saying where it was written when it does not parse.</summary>
	/// <exception cref="DataFileException">The text is not a valid effect.</exception>
	public static PresetEffect ParseEffect(string text, string file, string where)
	{
		try
		{
			return new PresetEffect(where.Length == 0 ? file : $"{file}: {where}", FlagEffectParser.Parse(text));
		}
		catch (FlagSyntaxException exception)
		{
			throw new DataFileException(file, where, exception.Message.TrimEnd('.'));
		}
	}

	/// <summary>
	/// This preset with <paramref name="later"/> applied on top: its world and cycle, where
	/// it gives them, and its effects after these.
	/// </summary>
	public DebugPreset Then(DebugPreset later)
	{
		ArgumentNullException.ThrowIfNull(later);

		return new DebugPreset(later.World ?? World, later.Cycle ?? Cycle, Effects.Concat(later.Effects).ToArray());
	}

	/// <summary>
	/// Checks every flag the effects write against <paramref name="catalogue"/>: it has to
	/// be declared, and written with its type.
	/// </summary>
	/// <returns>
	/// Every problem found, each a sentence that starts with where the effect was written, in
	/// the same words <see cref="DialogueValidator"/> uses. Empty when everything is valid.
	/// </returns>
	public IReadOnlyList<string> Validate(FlagCatalogue catalogue)
	{
		ArgumentNullException.ThrowIfNull(catalogue);

		var problems = new List<string>();

		foreach (PresetEffect effect in Effects)
		{
			(string name, FlagType type) = FlagUses.InEffect(effect.Effect);
			string problem = FlagUses.Problem(catalogue, name, type, inEffect: true);

			if (problem != null)
				problems.Add($"{effect.Source}: {problem}.");
		}

		return problems;
	}

	/// <summary>Applies every effect to <paramref name="flags"/>, in order.</summary>
	/// <remarks>Validate first: an undeclared flag throws halfway, with the earlier effects already applied.</remarks>
	public void Apply(IFlagStore flags)
	{
		ArgumentNullException.ThrowIfNull(flags);

		foreach (PresetEffect effect in Effects)
			effect.Effect.Apply(flags);
	}
}
