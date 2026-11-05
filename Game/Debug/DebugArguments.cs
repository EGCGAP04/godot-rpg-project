using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// What the debug tooling was asked for on the command line (#75): the arguments after
/// <c>--</c>, which Godot hands the game through <c>OS.GetCmdlineUserArgs()</c>.
/// Deliberately a plain C# class with no Godot dependency, so it is unit-tested without
/// the engine.
/// </summary>
/// <remarks>
/// <code>
/// --preset &lt;path&gt;     a preset file, res:// or absolute; once at most
/// --world &lt;world&gt;     real, fantasy or nightmare; once at most
/// --cycle &lt;number&gt;    from 1; once at most
/// --do "&lt;effect&gt;"     an effect in the dialogue's language; as many as wanted
/// </code>
/// <c>--world</c>, <c>--cycle</c> and <c>--do</c> apply on top of the preset file. Anything
/// else is an error, never ignored: a run that quietly skipped a misspelt argument would
/// start in another state than the one asked for, and look as if it had worked.
/// </remarks>
public sealed class DebugArguments
{
	/// <summary>The name problems on the command line give it.</summary>
	public const string Source = "command line";

	private DebugArguments(string presetPath, DebugPreset overrides)
	{
		PresetPath = presetPath;
		Overrides = overrides;
	}

	/// <summary>The preset file to read, or null when none was given.</summary>
	public string PresetPath { get; }

	/// <summary>What <c>--world</c>, <c>--cycle</c> and <c>--do</c> asked for, to apply after the file.</summary>
	public DebugPreset Overrides { get; }

	/// <summary>Whether anything at all was asked for.</summary>
	public bool IsEmpty => PresetPath == null && Overrides.World == null && Overrides.Cycle == null && Overrides.Effects.Count == 0;

	/// <exception cref="DataFileException">
	/// An argument is unknown, repeated when it can only appear once, or missing its value,
	/// or a value is not valid.
	/// </exception>
	public static DebugArguments Parse(IReadOnlyList<string> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);

		string presetPath = null;
		World? world = null;
		int? cycle = null;
		var effects = new List<PresetEffect>();

		for (int i = 0; i < arguments.Count; i++)
		{
			string argument = arguments[i];

			if (argument is not ("--preset" or "--world" or "--cycle" or "--do"))
				throw new DataFileException(Source, "", $"unknown argument '{argument}'; the ones the debug tooling reads are '--preset', '--world', '--cycle' and '--do'");

			if (i + 1 >= arguments.Count)
				throw new DataFileException(Source, "", $"'{argument}' needs a value after it");

			string value = arguments[++i];

			switch (argument)
			{
				case "--preset":
					if (presetPath != null)
						throw new DataFileException(Source, "", "'--preset' appears twice; a run starts from one preset file");

					if (value.Length == 0)
						throw new DataFileException(Source, "", "'--preset' needs the path of a preset file");

					presetPath = value;
					break;

				case "--world":
					if (world != null)
						throw new DataFileException(Source, "", "'--world' appears twice");

					world = DebugPreset.WorldNamed(value)
						?? throw new DataFileException(Source, "", $"'{value}' is not a world; the worlds are {DebugPreset.WorldList}");
					break;

				case "--cycle":
					if (cycle != null)
						throw new DataFileException(Source, "", "'--cycle' appears twice");

					if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < CycleProgression.FirstCycle)
						throw new DataFileException(Source, "", $"'--cycle {value}' is not a cycle; cycles are whole numbers from {CycleProgression.FirstCycle}");

					cycle = number;
					break;

				default:
					effects.Add(DebugPreset.ParseEffect(value, Source, $"--do {effects.Count + 1}"));
					break;
			}
		}

		return new DebugArguments(presetPath, new DebugPreset(world, cycle, effects));
	}
}
