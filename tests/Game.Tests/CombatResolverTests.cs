using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatResolver"/>, the engine-free half of the combat
/// system. Nothing here touches Godot, so the whole fight's math is verified
/// without starting the engine.
/// </summary>
public class CombatResolverTests
{
	[Fact]
	public void PlayerAttack_SubtractsAttackPowerFromEnemyHp()
	{
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 5, enemyHp: 10, enemyAttackPower: 3);

		resolver.PlayerAttack();

		Assert.Equal(5, resolver.EnemyHp);
		Assert.Equal(20, resolver.PlayerHp);
	}

	[Fact]
	public void EnemyAttack_SubtractsAttackPowerFromPlayerHp()
	{
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 5, enemyHp: 10, enemyAttackPower: 3);

		resolver.EnemyAttack();

		Assert.Equal(17, resolver.PlayerHp);
		Assert.Equal(10, resolver.EnemyHp);
	}

	[Fact]
	public void PlayerAttack_ClampsEnemyHpAtZeroInsteadOfGoingNegative()
	{
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 50, enemyHp: 10, enemyAttackPower: 3);

		resolver.PlayerAttack();

		Assert.Equal(0, resolver.EnemyHp);
	}

	[Fact]
	public void EnemyAttack_ClampsPlayerHpAtZeroInsteadOfGoingNegative()
	{
		var resolver = new CombatResolver(playerHp: 5, playerAttackPower: 1, enemyHp: 100, enemyAttackPower: 50);

		resolver.EnemyAttack();

		Assert.Equal(0, resolver.PlayerHp);
	}

	[Fact]
	public void CurrentOutcome_IsOngoingWhileBothSidesAreAlive()
	{
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 5, enemyHp: 10, enemyAttackPower: 3);

		Assert.Equal(CombatResolver.Outcome.Ongoing, resolver.CurrentOutcome);
		Assert.Equal(CombatResolver.Outcome.Ongoing, resolver.PlayerAttack());
		Assert.Equal(CombatResolver.Outcome.Ongoing, resolver.EnemyAttack());
	}

	[Fact]
	public void PlayerAttack_ReportsPlayerWonWhenItDropsEnemyHpToZero()
	{
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 10, enemyHp: 10, enemyAttackPower: 3);

		Assert.Equal(CombatResolver.Outcome.PlayerWon, resolver.PlayerAttack());
		Assert.Equal(CombatResolver.Outcome.PlayerWon, resolver.CurrentOutcome);
	}

	[Fact]
	public void EnemyAttack_ReportsPlayerLostWhenItDropsPlayerHpToZero()
	{
		var resolver = new CombatResolver(playerHp: 7, playerAttackPower: 1, enemyHp: 100, enemyAttackPower: 7);

		Assert.Equal(CombatResolver.Outcome.PlayerLost, resolver.EnemyAttack());
		Assert.Equal(CombatResolver.Outcome.PlayerLost, resolver.CurrentOutcome);
	}

	[Fact]
	public void CurrentOutcome_PrefersPlayerWonWhenBothSidesAreAtZero()
	{
		// The turn order stops this from happening in a real fight — the player only
		// takes damage on a turn the enemy survived to act on — but the tie-break is
		// part of the class's contract, so it is pinned down here.
		var resolver = new CombatResolver(playerHp: 3, playerAttackPower: 10, enemyHp: 10, enemyAttackPower: 3);

		resolver.EnemyAttack();
		resolver.PlayerAttack();

		Assert.Equal(0, resolver.PlayerHp);
		Assert.Equal(0, resolver.EnemyHp);
		Assert.Equal(CombatResolver.Outcome.PlayerWon, resolver.CurrentOutcome);
	}

	[Theory]
	[InlineData(-5, 0)]
	[InlineData(0, 0)]
	[InlineData(12, 12)]
	public void Constructor_ClampsNegativeStatsToZero(int given, int expected)
	{
		var resolver = new CombatResolver(playerHp: given, playerAttackPower: given, enemyHp: given, enemyAttackPower: given);

		Assert.Equal(expected, resolver.PlayerHp);
		Assert.Equal(expected, resolver.PlayerAttackPower);
		Assert.Equal(expected, resolver.EnemyHp);
		Assert.Equal(expected, resolver.EnemyAttackPower);
	}

	[Fact]
	public void AFullFight_ResolvesAfterTheExpectedNumberOfRounds()
	{
		// A whole fight in miniature, checking that the outcome only flips on the blow
		// that actually drops the enemy. The numbers are illustrative and deliberately
		// not the shipped ones: the player's attack power and the enemies' stats are
		// [Export]/.tres values meant to be retuned, and a unit test that pinned them
		// would fail on every balance pass while proving nothing about the rules.
		var resolver = new CombatResolver(playerHp: 20, playerAttackPower: 5, enemyHp: 10, enemyAttackPower: 3);

		Assert.Equal(CombatResolver.Outcome.Ongoing, resolver.PlayerAttack());
		Assert.Equal(CombatResolver.Outcome.Ongoing, resolver.EnemyAttack());
		Assert.Equal(CombatResolver.Outcome.PlayerWon, resolver.PlayerAttack());

		Assert.Equal(17, resolver.PlayerHp);
		Assert.Equal(0, resolver.EnemyHp);
	}
}
