using System;
using System.Linq;
using Godot;

/// <summary>
/// Temporary: F2 switches the game to its next language, so both can be checked without
/// changing the system's. #75's debug tooling replaces it with a proper control; removing
/// it is deleting this file and its line under <c>[autoload]</c> in <c>project.godot</c>.
/// </summary>
/// <remarks>
/// An autoload, so it works on every screen, and it keeps working while a fight pauses
/// the tree. It has no <c>Instance</c>, unlike the other autoloads, because no other code
/// calls it.
/// </remarks>
public partial class LocaleSwitchKey : Node
{
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 })
			return;

		// The system's locale can carry a region, such as es_AR, while the tables are by
		// language alone.
		string current = TranslationServer.GetLocale().Split('_')[0];
		int index = LocalizationNames.Locales.ToList().IndexOf(current);
		string next = LocalizationNames.Locales[(Math.Max(index, 0) + 1) % LocalizationNames.Locales.Count];

		TranslationServer.SetLocale(next);
		GD.Print($"Locale switched to {next}.");
		GetViewport().SetInputAsHandled();
	}
}
