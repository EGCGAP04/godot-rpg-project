using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="TurnOrder"/>: agility decides, a tie is rolled for, and the
/// number of rolls is checked along with the order.
/// </summary>
public class TurnOrderTests
{
	private static Combatant Member(int position, int agility, int healthPoints = 10) =>
		Build(CombatantId.Party(position), agility, healthPoints);

	private static Combatant Enemy(int position, int agility, int healthPoints = 10) =>
		Build(CombatantId.Enemy(position), agility, healthPoints);

	private static Combatant Build(CombatantId id, int agility, int healthPoints) =>
		new(id, new CombatantSetup("speaker.someone", TestTuning.Stats(healthPoints: 10, agility: agility), healthPoints, 0));

	private static CombatantId[] Order(ScriptedCombatRandom random, params Combatant[] combatants)
	{
		CombatantId[] order = TurnOrder.For(combatants, random).Select(combatant => combatant.Id).ToArray();
		random.RequireUsedUp();
		return order;
	}

	[Fact]
	public void TheHighestAgility_GoesFirst_WhateverTheSide_WithNoRoll()
	{
		CombatantId[] order = Order(
			new ScriptedCombatRandom(),
			Member(0, agility: 5), Member(1, agility: 30), Enemy(0, agility: 20), Enemy(1, agility: 1));

		Assert.Equal(new[] { CombatantId.Party(1), CombatantId.Enemy(0), CombatantId.Party(0), CombatantId.Enemy(1) }, order);
	}

	[Fact]
	public void WhoeverIsDown_HasNoTurn()
	{
		CombatantId[] order = Order(
			new ScriptedCombatRandom(),
			Member(0, agility: 5), Member(1, agility: 30, healthPoints: 0), Enemy(0, agility: 20));

		Assert.Equal(new[] { CombatantId.Enemy(0), CombatantId.Party(0) }, order);
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, true)]
	public void ATieOfTwo_TakesOneRoll_OverThePartyThenTheEnemies(int roll, bool enemyFirst)
	{
		CombatantId[] order = Order(
			new ScriptedCombatRandom().Index(roll),
			Enemy(0, agility: 10), Member(0, agility: 10));

		CombatantId[] expected = enemyFirst
			? new[] { CombatantId.Enemy(0), CombatantId.Party(0) }
			: new[] { CombatantId.Party(0), CombatantId.Enemy(0) };
		Assert.Equal(expected, order);
	}

	[Fact]
	public void ATieOfFour_TakesThreeRolls_EachOverThoseLeft()
	{
		// Listed as Party 0, Party 1, Enemy 0, Enemy 1. Index 3 of four is Enemy 1; index 0
		// of the three left is Party 0; index 1 of the two left is Enemy 0; Party 1 is last.
		CombatantId[] order = Order(
			new ScriptedCombatRandom().Index(3).Index(0).Index(1),
			Enemy(1, agility: 7), Member(1, agility: 7), Enemy(0, agility: 7), Member(0, agility: 7));

		Assert.Equal(new[] { CombatantId.Enemy(1), CombatantId.Party(0), CombatantId.Enemy(0), CombatantId.Party(1) }, order);
	}

	[Fact]
	public void TheSameRolls_GiveTheSameOrder_WhateverOrderTheCombatantsCameIn()
	{
		Combatant[] oneWay = { Member(0, 7), Member(1, 7), Enemy(0, 7) };
		Combatant[] another = { Enemy(0, 7), Member(1, 7), Member(0, 7) };

		CombatantId[] first = Order(new ScriptedCombatRandom().Index(2).Index(1), oneWay);
		CombatantId[] second = Order(new ScriptedCombatRandom().Index(2).Index(1), another);

		Assert.Equal(first, second);
		Assert.Equal(new[] { CombatantId.Enemy(0), CombatantId.Party(1), CombatantId.Party(0) }, first);
	}

	[Fact]
	public void SeveralTies_AreSettledFromTheHighestAgilityDown()
	{
		// The fast pair takes the first roll and the slow pair the second.
		CombatantId[] order = Order(
			new ScriptedCombatRandom().Index(1).Index(0),
			Member(0, agility: 3), Member(1, agility: 9), Enemy(0, agility: 3), Enemy(1, agility: 9), Enemy(2, agility: 5));

		Assert.Equal(
			new[] { CombatantId.Enemy(1), CombatantId.Party(1), CombatantId.Enemy(2), CombatantId.Party(0), CombatantId.Enemy(0) },
			order);
	}

	[Fact]
	public void SomeoneDown_IsNotPartOfATie()
	{
		CombatantId[] order = Order(
			new ScriptedCombatRandom(),
			Member(0, agility: 10), Enemy(0, agility: 10, healthPoints: 0));

		Assert.Equal(new[] { CombatantId.Party(0) }, order);
	}

	[Fact]
	public void WithNobodyStanding_TheOrderIsEmpty()
	{
		Assert.Empty(Order(new ScriptedCombatRandom(), Member(0, agility: 10, healthPoints: 0)));
		Assert.Empty(Order(new ScriptedCombatRandom()));
	}

	[Fact]
	public void For_RefusesWhatIsMissing_AndACombatantListedTwice()
	{
		var random = new ScriptedCombatRandom();
		Combatant member = Member(0, agility: 10);

		Assert.Throws<ArgumentNullException>(() => TurnOrder.For(null, random));
		Assert.Throws<ArgumentNullException>(() => TurnOrder.For(new[] { member }, null));
		Assert.Throws<ArgumentException>(() => TurnOrder.For(new[] { member, null }, random));
		Assert.Throws<ArgumentException>(() => TurnOrder.For(new[] { member, Member(0, agility: 4) }, random));
	}
}
