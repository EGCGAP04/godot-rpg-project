using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises what a fight is made of: <see cref="CombatantId"/>,
/// <see cref="CombatantSetup"/> and <see cref="Combatant"/>.
/// </summary>
public class CombatantTests
{
	private static readonly BaseStats Stats = TestTuning.Stats(healthPoints: 30, manaPoints: 10, agility: 5);

	// --- CombatantId ---

	[Fact]
	public void AnId_IsItsSideAndItsPosition()
	{
		Assert.Equal(new CombatantId(CombatSide.Party, 2), CombatantId.Party(2));
		Assert.Equal(new CombatantId(CombatSide.Enemy, 0), CombatantId.Enemy(0));
		Assert.NotEqual(CombatantId.Party(1), CombatantId.Enemy(1));
		Assert.NotEqual(CombatantId.Party(1), CombatantId.Party(2));
		Assert.Equal(CombatSide.Enemy, CombatantId.Enemy(3).Side);
		Assert.Equal(3, CombatantId.Enemy(3).Position);
	}

	[Fact]
	public void AnId_ReadsAsItsSideAndPosition()
	{
		Assert.Equal("Enemy 1", CombatantId.Enemy(1).ToString());
	}

	[Fact]
	public void AnId_RefusesANegativePosition_AndASideThatIsNotOne()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => CombatantId.Party(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantId((CombatSide)2, 0));
	}

	// --- CombatantSetup ---

	[Fact]
	public void ASetup_KeepsEachValueInItsOwnProperty()
	{
		var setup = new CombatantSetup("speaker.someone", Stats, healthPoints: 12, manaPoints: 3);

		Assert.Equal("speaker.someone", setup.NameKey);
		Assert.Equal(Stats, setup.Stats);
		Assert.Equal(12, setup.HealthPoints);
		Assert.Equal(3, setup.ManaPoints);
	}

	[Fact]
	public void AtFull_TakesHealthAndManaFromTheStats()
	{
		Assert.Equal(new CombatantSetup("enemy.weak", Stats, 30, 10), CombatantSetup.AtFull("enemy.weak", Stats));
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(30, 10)]
	public void ASetup_AcceptsTheEndsOfHealthAndMana(int healthPoints, int manaPoints)
	{
		var setup = new CombatantSetup("enemy.weak", Stats, healthPoints, manaPoints);

		Assert.Equal(healthPoints, setup.HealthPoints);
		Assert.Equal(manaPoints, setup.ManaPoints);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(31)]
	public void ASetup_RefusesHealthOutsideWhatTheStatsAllow(int healthPoints)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSetup("enemy.weak", Stats, healthPoints, 0));

		Assert.Equal("healthPoints", thrown.ParamName);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(11)]
	public void ASetup_RefusesManaOutsideWhatTheStatsAllow(int manaPoints)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSetup("enemy.weak", Stats, 30, manaPoints));

		Assert.Equal("manaPoints", thrown.ParamName);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void ASetup_RefusesAMissingName(string nameKey)
	{
		Assert.Throws<ArgumentException>(() => new CombatantSetup(nameKey, Stats, 30, 10));
	}

	// --- Combatant ---

	[Fact]
	public void ACombatant_StartsFromItsSetup()
	{
		var combatant = new Combatant(CombatantId.Enemy(1), new CombatantSetup("enemy.weak", Stats, 12, 3));

		Assert.Equal(CombatantId.Enemy(1), combatant.Id);
		Assert.Equal("enemy.weak", combatant.NameKey);
		Assert.Equal(Stats, combatant.Stats);
		Assert.Equal(12, combatant.HealthPoints);
		Assert.Equal(3, combatant.ManaPoints);
		Assert.False(combatant.IsDown);
	}

	[Fact]
	public void ACombatant_RefusesAMissingSetup()
	{
		Assert.Throws<ArgumentNullException>(() => new Combatant(CombatantId.Party(0), null));
	}

	[Fact]
	public void TakeDamage_TakesItOutOfTheHealth_AndLeavesTheRestAlone()
	{
		var combatant = new Combatant(CombatantId.Party(0), CombatantSetup.AtFull("speaker.someone", Stats));

		combatant.TakeDamage(12);

		Assert.Equal(18, combatant.HealthPoints);
		Assert.Equal(10, combatant.ManaPoints);
		Assert.Equal(30, combatant.Stats.HealthPoints);
		Assert.False(combatant.IsDown);
	}

	[Theory]
	[InlineData(30)]
	[InlineData(31)]
	[InlineData(int.MaxValue)]
	public void TakeDamage_StopsAtZero_AndTheCombatantIsDown(int damage)
	{
		var combatant = new Combatant(CombatantId.Party(0), CombatantSetup.AtFull("speaker.someone", Stats));

		combatant.TakeDamage(damage);

		Assert.Equal(0, combatant.HealthPoints);
		Assert.True(combatant.IsDown);
	}

	[Fact]
	public void ACombatant_WithOneHealthLeft_IsStillStanding()
	{
		var combatant = new Combatant(CombatantId.Party(0), CombatantSetup.AtFull("speaker.someone", Stats));

		combatant.TakeDamage(29);

		Assert.Equal(1, combatant.HealthPoints);
		Assert.False(combatant.IsDown);
	}

	[Fact]
	public void TakeDamage_OfZero_ChangesNothing()
	{
		var combatant = new Combatant(CombatantId.Party(0), CombatantSetup.AtFull("speaker.someone", Stats));

		combatant.TakeDamage(0);

		Assert.Equal(30, combatant.HealthPoints);
	}

	[Fact]
	public void TakeDamage_RefusesANegativeNumber_AndChangesNothing()
	{
		var combatant = new Combatant(CombatantId.Party(0), new CombatantSetup("speaker.someone", Stats, 12, 3));

		Assert.Throws<ArgumentOutOfRangeException>(() => combatant.TakeDamage(-1));

		Assert.Equal(12, combatant.HealthPoints);
	}

	[Fact]
	public void ACombatant_ThatComesInWithNoHealth_IsDown()
	{
		var combatant = new Combatant(CombatantId.Party(0), new CombatantSetup("speaker.someone", Stats, 0, 0));

		Assert.True(combatant.IsDown);
	}
}
