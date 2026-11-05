using System.Linq;
using Godot;

/// <summary>
/// The debug tooling (#75), in debug builds only. At startup it applies the preset asked
/// for on the command line, after <c>--</c>: where the run starts, and effects on the
/// decision registry's flags. The rules for reading one live in <see cref="DebugArguments"/>
/// and <see cref="DebugPreset"/>, which have no Godot dependency and are covered by unit
/// tests.
/// </summary>
/// <remarks>
/// <para>
/// Registered after <c>DecisionRegistry</c> under <c>[autoload]</c>: autoloads enter the
/// tree in that order, and this one needs the registry's state. All of it runs in
/// <c>_EnterTree</c>, before <c>Main</c> reads <c>GameState</c> from its own.
/// </para>
/// <para>
/// A preset that cannot be read or names a flag wrongly stops the game with exit code 1.
/// A run that went on in the default state instead would look exactly like one that
/// started where it was asked to.
/// </para>
/// </remarks>
public partial class DebugTools : Node
{
	public static DebugTools Instance { get; private set; }

	public override void _EnterTree()
	{
		// A release build has no debug tooling at all: the node leaves, and Instance
		// stays null, which is how other code can tell.
		if (!OS.IsDebugBuild())
		{
			QueueFree();
			return;
		}

		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		ApplyStartupPreset();
	}

	private void ApplyStartupPreset()
	{
		DecisionState state = DecisionRegistry.Instance.State;
		DebugPreset preset;

		try
		{
			DebugArguments arguments = DebugArguments.Parse(OS.GetCmdlineUserArgs());

			if (arguments.IsEmpty)
				return;

			DebugPreset file = arguments.PresetPath == null
				? DebugPreset.None
				: DebugPreset.Read(arguments.PresetPath.GetFile(), ReadFile(arguments.PresetPath));

			preset = file.Then(arguments.Overrides);
		}
		catch (DataFileException exception)
		{
			Stop(exception.Message);
			return;
		}

		string[] problems = preset.Validate(state.Catalogue).ToArray();

		if (problems.Length > 0)
		{
			Stop(string.Join("\n", problems));
			return;
		}

		GameState.Instance.StartAt(preset.Start);
		preset.Apply(state);

		GD.Print($"Debug: preset applied; starting in {preset.Start.World}, cycle {preset.Start.Cycle}.");

		foreach (FlagDeclaration flag in state.Catalogue.Flags.Values.OrderBy(flag => flag.Name, System.StringComparer.Ordinal))
		{
			string value = flag.Type == FlagType.Bool ? (state.GetFlag(flag.Name) ? "true" : "false") : state.GetCounter(flag.Name).ToString(System.Globalization.CultureInfo.InvariantCulture);
			GD.Print($"Debug:   {flag.Name} = {value}");
		}
	}

	private void Stop(string problem)
	{
		GD.PushError($"Debug: the preset was not applied, so the game stops.\n{problem}");
		GetTree().Quit(1);
	}

	/// <summary>The text of a preset file, from <c>res://</c> or an absolute path.</summary>
	/// <exception cref="DataFileException">The file cannot be read.</exception>
	private static string ReadFile(string path)
	{
		string text = FileAccess.GetFileAsString(path);
		Error error = FileAccess.GetOpenError();

		if (error != Error.Ok)
			throw new DataFileException(path.GetFile(), "", $"could not read {path}: {error}");

		return text;
	}
}
