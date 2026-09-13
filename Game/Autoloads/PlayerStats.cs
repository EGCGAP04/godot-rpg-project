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

	public override void _Ready()
	{
		Instance = this;
		CurrentHp = MaxHp;
	}

	public void FullHeal()
	{
		CurrentHp = MaxHp;
	}
}
