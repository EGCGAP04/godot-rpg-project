using Godot;

public partial class PlayerStats : Node
{
	public static PlayerStats Instance { get; private set; }

	[Export]
	public int MaxHp { get; set; } = 20;

	public int CurrentHp { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		CurrentHp = MaxHp;
	}

	public void TakeDamage(int amount)
	{
		CurrentHp = Mathf.Max(CurrentHp - amount, 0);
	}

	public void FullHeal()
	{
		CurrentHp = MaxHp;
	}
}
