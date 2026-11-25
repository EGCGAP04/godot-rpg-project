using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="OrderEntry"/>: a command and a target for each member in turn, and
/// the way back with <c>cancel</c>.
/// </summary>
public class OrderEntryTests
{
	private static readonly CombatantId P0 = CombatantId.Party(0), P1 = CombatantId.Party(1), P3 = CombatantId.Party(3);
	private static readonly CombatantId E0 = CombatantId.Enemy(0), E2 = CombatantId.Enemy(2);

	/// <summary>Members 0, 1 and 3 need orders (2 is down); enemies 0 and 2 can be attacked (1 is down).</summary>
	private static OrderEntry Entry() => new(new[] { P0, P1, P3 }, new[] { E0, E2 });

	[Fact]
	public void ItStarts_WithTheFirstMembersCommand()
	{
		OrderEntry entry = Entry();

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
		Assert.Equal(new[] { E0, E2 }, entry.Targets);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void ACommandThenATarget_GiveTheMemberAnOrder_AndPassToTheNext()
	{
		OrderEntry entry = Entry();

		entry.ChooseAttack();
		Assert.Equal(OrderStep.Target, entry.Step);
		Assert.Equal(P0, entry.Member);

		entry.ChooseTarget(E2);
		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P1, entry.Member);
		Assert.Equal(new CombatOrder[] { new AttackOrder(P0, E2) }, entry.Orders);
	}

	[Fact]
	public void EveryMembersOrder_CompletesIt_InTheMembersOrder()
	{
		OrderEntry entry = Entry();

		foreach (CombatantId target in new[] { E2, E0, E0 })
		{
			entry.ChooseAttack();
			entry.ChooseTarget(target);
		}

		Assert.Equal(OrderStep.Complete, entry.Step);
		Assert.Equal(new CombatOrder[] { new AttackOrder(P0, E2), new AttackOrder(P1, E0), new AttackOrder(P3, E0) }, entry.Orders);
		Assert.Throws<InvalidOperationException>(() => entry.Member);
	}

	[Fact]
	public void TheOrders_AreTheOnesTheRunnerTakes()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, TestFights.Foe());
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 2); // the tie between the members
		random.Index(1, among: 2); // the foe, first, picks abe
		random.Hit();
		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, canFlee: true, TestTuning.Formulas(), TestTuning.Combat(), random);
		var entry = new OrderEntry(runner.MembersToOrder, runner.Enemies.Where(enemy => !enemy.IsDown).Select(enemy => enemy.Id));

		entry.ChooseAttack();
		entry.ChooseTarget(CombatantId.Enemy(0));
		entry.ChooseAttack();
		entry.ChooseTarget(CombatantId.Enemy(0));
		runner.SubmitOrders(entry.Orders);

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		random.RequireUsedUp();
	}

	// --- Going back ---

	[Fact]
	public void Back_FromATarget_GoesToTheSameMembersCommand()
	{
		OrderEntry entry = Entry();
		entry.ChooseAttack();

		Assert.True(entry.Back());

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
	}

	[Fact]
	public void Back_FromACommand_GoesToTheTargetOfTheMemberBefore_AndForgetsTheirOrder()
	{
		OrderEntry entry = Entry();
		entry.ChooseAttack();
		entry.ChooseTarget(E2);
		entry.ChooseAttack();
		entry.ChooseTarget(E0);

		Assert.True(entry.Back());

		Assert.Equal(OrderStep.Target, entry.Step);
		Assert.Equal(P1, entry.Member);
		Assert.Equal(new CombatOrder[] { new AttackOrder(P0, E2) }, entry.Orders);

		entry.ChooseTarget(E2);
		Assert.Equal(new CombatOrder[] { new AttackOrder(P0, E2), new AttackOrder(P1, E2) }, entry.Orders);
	}

	[Fact]
	public void Back_OnTheFirstMembersCommand_DoesNothing()
	{
		OrderEntry entry = Entry();

		Assert.False(entry.CanGoBack);
		Assert.False(entry.Back());

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
	}

	[Fact]
	public void CanGoBack_SaysWhetherBackWouldGoAnywhere()
	{
		OrderEntry entry = Entry();
		entry.ChooseAttack();
		Assert.True(entry.CanGoBack);

		entry.ChooseTarget(E0);
		Assert.True(entry.CanGoBack);
	}

	[Fact]
	public void Back_AllTheWay_ReturnsToTheStart()
	{
		OrderEntry entry = Entry();
		entry.ChooseAttack();
		entry.ChooseTarget(E0);
		entry.ChooseAttack();

		while (entry.Back())
		{
		}

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Back_OnceComplete_IsRefused()
	{
		var entry = new OrderEntry(new[] { P0 }, new[] { E0 });
		entry.ChooseAttack();
		entry.ChooseTarget(E0);

		Assert.Throws<InvalidOperationException>(() => entry.Back());
		Assert.False(entry.CanGoBack);
	}

	// --- What it refuses ---

	[Fact]
	public void AStepOutOfTurn_IsRefused()
	{
		OrderEntry entry = Entry();

		Assert.Throws<InvalidOperationException>(() => entry.ChooseTarget(E0));

		entry.ChooseAttack();
		Assert.Throws<InvalidOperationException>(() => entry.ChooseAttack());
	}

	[Fact]
	public void ATargetThatCannotBeAttacked_IsRefused_AndChangesNothing()
	{
		OrderEntry entry = Entry();
		entry.ChooseAttack();

		Assert.Throws<ArgumentException>(() => entry.ChooseTarget(CombatantId.Enemy(1)));
		Assert.Throws<ArgumentException>(() => entry.ChooseTarget(P1));

		Assert.Equal(OrderStep.Target, entry.Step);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Constructor_RefusesNobodyToOrder_AndNobodyToAttack()
	{
		Assert.Throws<ArgumentException>(() => new OrderEntry(Array.Empty<CombatantId>(), new[] { E0 }));
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { P0 }, Array.Empty<CombatantId>()));
		Assert.Throws<ArgumentNullException>(() => new OrderEntry(null, new[] { E0 }));
		Assert.Throws<ArgumentNullException>(() => new OrderEntry(new[] { P0 }, null));
	}

	[Fact]
	public void Constructor_RefusesTheWrongSide_AndRepeats()
	{
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { E0 }, new[] { E2 }));
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { P0, P0 }, new[] { E0 }));
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { P0 }, new[] { P1 }));
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { P0 }, new[] { E0, E0 }));
	}
}
