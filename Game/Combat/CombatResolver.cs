using System;

/// <summary>
/// The rules of a single fight: how much damage each side deals, what the current
/// HP is, and whether the fight is over. Deliberately a plain C# class with no
/// Godot dependency, so it can be unit-tested without running the engine —
/// <c>Combat.cs</c> keeps the nodes, timers, input and UI around it.
/// </summary>
public class CombatResolver
{
	public enum Outcome
	{
		Ongoing,
		PlayerWon,
		PlayerLost
	}

	public CombatResolver(int playerHp, int playerAttackPower, int enemyHp, int enemyAttackPower)
	{
		PlayerHp = Math.Max(playerHp, 0);
		PlayerAttackPower = Math.Max(playerAttackPower, 0);
		EnemyHp = Math.Max(enemyHp, 0);
		EnemyAttackPower = Math.Max(enemyAttackPower, 0);
	}

	public int PlayerHp { get; private set; }

	public int PlayerAttackPower { get; }

	public int EnemyHp { get; private set; }

	public int EnemyAttackPower { get; }

	/// <summary>
	/// How the fight stands after the last attack. A win takes precedence over a
	/// loss if both sides are somehow at 0, which the turn order already prevents:
	/// the player only takes damage on a turn the enemy survived to act on.
	/// </summary>
	public Outcome CurrentOutcome =>
		EnemyHp <= 0 ? Outcome.PlayerWon
		: PlayerHp <= 0 ? Outcome.PlayerLost
		: Outcome.Ongoing;

	/// <summary>Applies the player's damage to the enemy and reports the new outcome.</summary>
	public Outcome PlayerAttack()
	{
		EnemyHp = Math.Max(EnemyHp - PlayerAttackPower, 0);
		return CurrentOutcome;
	}

	/// <summary>Applies the enemy's damage to the player and reports the new outcome.</summary>
	public Outcome EnemyAttack()
	{
		PlayerHp = Math.Max(PlayerHp - EnemyAttackPower, 0);
		return CurrentOutcome;
	}
}
