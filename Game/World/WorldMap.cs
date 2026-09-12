using Godot;

public partial class WorldMap : Node2D
{
	private void OnCombatTriggered()
	{
		// Deferred: the signal fires inside a physics callback, where freeing the
		// current scene's collision nodes is not allowed.
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Combat/combat.tscn");
	}
}
