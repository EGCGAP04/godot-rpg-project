using System.Linq;
using Godot;

/// <summary>
/// A group of enemies fought together, authored as a .tres Resource: an id and the
/// enemies, in the order they stand in the fight. The enemy on the map carries one, so
/// the same group can stand in several places, and its id is what the history of fights
/// records.
/// </summary>
/// <remarks>
/// The defaults are empty, like every resource's, so a group nobody filled in is refused
/// by <see cref="ToValues"/> instead of passing for a fight against nobody. The rules
/// never read this resource: they read the <see cref="EncounterValues"/> it builds.
/// </remarks>
[GlobalClass]
public partial class EncounterData : Resource
{
	/// <summary>A lowercase word such as <c>demo_patrol</c>, unique among the encounters. The file is named after it.</summary>
	[Export]
	public string Id { get; set; } = "";

	/// <summary>From one enemy to four. The same enemy may be listed more than once.</summary>
	[Export]
	public Godot.Collections.Array<EnemyData> Enemies { get; set; } = new();

	/// <summary>This group as the plain values the rules of a fight read.</summary>
	public EncounterValues ToValues()
	{
		if (Enemies.Any(enemy => enemy == null))
			throw new System.ArgumentException($"The encounter '{Id}' has an empty entry among its enemies.");

		return new EncounterValues(Id, Enemies.Select(enemy => enemy.ToValues()));
	}
}
