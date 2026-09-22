using Godot;

public partial class PlayerStats : Node
{
	public static PlayerStats Instance { get; private set; }

	[Export]
	public int MaxHp { get; set; } = 20;

	[Export]
	public int AttackPower { get; set; } = 4;

	private int _currentHp;

	/// <summary>
	/// The player's HP across scenes. Storage only: how much damage an attack does
	/// is decided by <c>CombatResolver</c>, and <c>Combat</c> writes the result here
	/// so it survives the fight. The setter clamps, so callers never have to.
	/// </summary>
	public int CurrentHp
	{
		get => _currentHp;
		set => _currentHp = Mathf.Clamp(value, 0, MaxHp);
	}

	// _EnterTree, not _Ready, matching every autoload: a node's own _EnterTree runs
	// before any autoload's _Ready, so _Ready is too late for anything that wires
	// itself up that early.
	public override void _EnterTree()
	{
		Instance = this;
		CurrentHp = MaxHp;
	}

	public void FullHeal()
	{
		CurrentHp = MaxHp;
	}
}
