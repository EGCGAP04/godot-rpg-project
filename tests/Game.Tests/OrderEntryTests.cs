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

	/// <summary>
	/// Members 0, 1 and 3 need orders (2 is down); enemies 0 and 2 can be attacked (1 is
	/// down). Members 1 and 3 can meditate, and the fight can be fled unless it says not.
	/// </summary>
	private static OrderEntry Entry(bool canFlee = true) => new(new[] { P0, P1, P3 }, new[] { E0, E2 }, new[] { P1, P3 }, canFlee);

	/// <summary>An entry for the given members and targets, where nobody meditates and the fight can be fled.</summary>
	private static OrderEntry Entry(CombatantId[] members, CombatantId[] targets) => new(members, targets, Array.Empty<CombatantId>(), canFlee: true);

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

		entry.Choose(CombatCommand.Attack);
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
			entry.Choose(CombatCommand.Attack);
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
		var entry = new OrderEntry(runner.MembersToOrder, runner.Enemies.Where(enemy => !enemy.IsDown).Select(enemy => enemy.Id), runner.MembersToOrder.Where(runner.CanMeditate), runner.CanFlee);

		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(CombatantId.Enemy(0));
		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(CombatantId.Enemy(0));
		runner.SubmitOrders(entry.Orders);

		Assert.Equal(CombatStep.EnemyAttackPending, runner.Step);
		random.RequireUsedUp();
	}

	// --- The other commands ---

	[Fact]
	public void DefendingFocusingAndMeditating_AreTheMembersOrder_AndTheNextChooses()
	{
		OrderEntry entry = Entry();

		entry.Choose(CombatCommand.Defend);
		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P1, entry.Member);

		entry.Choose(CombatCommand.Focus);
		entry.Choose(CombatCommand.Meditate);

		Assert.Equal(OrderStep.Complete, entry.Step);
		Assert.Equal(new CombatOrder[] { new DefendOrder(P0), new FocusOrder(P1), new MeditateOrder(P3) }, entry.Orders);
	}

	[Fact]
	public void CanChoose_OffersMeditating_OnlyToWhoeverCan_AndFleeing_OnlyWhereTheFightAllows()
	{
		OrderEntry entry = Entry(canFlee: false);

		Assert.True(entry.CanChoose(CombatCommand.Attack));
		Assert.True(entry.CanChoose(CombatCommand.Defend));
		Assert.True(entry.CanChoose(CombatCommand.Focus));
		Assert.False(entry.CanChoose(CombatCommand.Meditate));
		Assert.False(entry.CanChoose(CombatCommand.Flee));

		entry.Choose(CombatCommand.Defend);

		Assert.True(entry.CanChoose(CombatCommand.Meditate));
		Assert.True(Entry(canFlee: true).CanChoose(CombatCommand.Flee));
	}

	[Theory]
	[InlineData(CombatCommand.Meditate, true)]
	[InlineData(CombatCommand.Flee, false)]
	public void ACommandThatCannotBeChosen_IsRefused_AndChangesNothing(CombatCommand command, bool canFlee)
	{
		OrderEntry entry = Entry(canFlee);

		Assert.Throws<InvalidOperationException>(() => entry.Choose(command));

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void ACommandTheMenuDoesNotHave_IsRefused()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Entry().CanChoose((CombatCommand)99));
		Assert.Throws<ArgumentOutOfRangeException>(() => Entry().Choose((CombatCommand)99));
	}

	[Fact]
	public void CanChoose_OutsideACommand_IsRefused()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);

		Assert.Throws<InvalidOperationException>(() => entry.CanChoose(CombatCommand.Defend));
		Assert.Throws<InvalidOperationException>(() => entry.Choose(CombatCommand.Defend));
	}

	[Fact]
	public void Fleeing_FromAnyMembersMenu_ForgetsTheOrdersGiven_AndCompletesAlone()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(E2);

		entry.Choose(CombatCommand.Flee);

		Assert.Equal(OrderStep.Complete, entry.Step);
		Assert.Equal(new CombatOrder[] { new FleeOrder(P1) }, entry.Orders);
		Assert.False(entry.CanGoBack);
	}

	[Fact]
	public void Fleeing_FromTheFirstMembersMenu_IsTheirs()
	{
		OrderEntry entry = Entry();

		entry.Choose(CombatCommand.Flee);

		Assert.Equal(new CombatOrder[] { new FleeOrder(P0) }, entry.Orders);
	}

	[Fact]
	public void EveryCommand_GivesOrdersTheRunnerTakes()
	{
		PartyState party = TestRoster.Party("abe", "bea");
		party.Recruit("abe");
		party.Recruit("bea");
		party.SetManaPoints("bea", World.Fantasy, 0);
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, TestFights.Pursuer());
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 3).Index(0, among: 2); // the ties between the members
		random.Index(0, among: 3); // the pursuer, first, picks the hero
		random.Hit();
		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, canFlee: true, TestTuning.Formulas(), TestTuning.Combat(), random);
		var entry = new OrderEntry(runner.MembersToOrder, runner.Enemies.Where(enemy => !enemy.IsDown).Select(enemy => enemy.Id), runner.MembersToOrder.Where(runner.CanMeditate), runner.CanFlee);

		entry.Choose(CombatCommand.Defend);
		entry.Choose(CombatCommand.Focus);
		entry.Choose(CombatCommand.Meditate);
		runner.SubmitOrders(entry.Orders);
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Contains(new Meditated(CombatantId.Party(2), 2, 2), runner.TakeEvents());
		random.RequireUsedUp();
	}

	// --- Going back ---

	[Fact]
	public void Back_FromATarget_GoesToTheSameMembersCommand()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);

		Assert.True(entry.Back());

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
	}

	[Fact]
	public void Back_FromACommand_GoesToTheTargetOfTheMemberBefore_AndForgetsTheirOrder()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(E2);
		entry.Choose(CombatCommand.Attack);
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
		entry.Choose(CombatCommand.Attack);
		Assert.True(entry.CanGoBack);

		entry.ChooseTarget(E0);
		Assert.True(entry.CanGoBack);
	}

	[Fact]
	public void Back_AllTheWay_ReturnsToTheStart()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(E0);
		entry.Choose(CombatCommand.Attack);

		while (entry.Back())
		{
		}

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Back_ToAMemberWhoseOrderHadNoTarget_GoesToTheirCommand()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Defend);

		Assert.True(entry.Back());

		Assert.Equal(OrderStep.Command, entry.Step);
		Assert.Equal(P0, entry.Member);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Back_AcrossOrdersWithAndWithoutATarget_GoesToEachOnesStep()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);
		entry.ChooseTarget(E2);
		entry.Choose(CombatCommand.Focus);

		Assert.True(entry.Back());
		Assert.Equal((OrderStep.Command, P1), (entry.Step, entry.Member));
		Assert.Equal(new CombatOrder[] { new AttackOrder(P0, E2) }, entry.Orders);

		Assert.True(entry.Back());
		Assert.Equal((OrderStep.Target, P0), (entry.Step, entry.Member));
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Back_OnceComplete_IsRefused()
	{
		OrderEntry entry = Entry(new[] { P0 }, new[] { E0 });
		entry.Choose(CombatCommand.Attack);
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

		entry.Choose(CombatCommand.Attack);
		Assert.Throws<InvalidOperationException>(() => entry.Choose(CombatCommand.Attack));
	}

	[Fact]
	public void ATargetThatCannotBeAttacked_IsRefused_AndChangesNothing()
	{
		OrderEntry entry = Entry();
		entry.Choose(CombatCommand.Attack);

		Assert.Throws<ArgumentException>(() => entry.ChooseTarget(CombatantId.Enemy(1)));
		Assert.Throws<ArgumentException>(() => entry.ChooseTarget(P1));

		Assert.Equal(OrderStep.Target, entry.Step);
		Assert.Empty(entry.Orders);
	}

	[Fact]
	public void Constructor_RefusesNobodyToOrder_AndNobodyToAttack()
	{
		Assert.Throws<ArgumentException>(() => Entry(Array.Empty<CombatantId>(), new[] { E0 }));
		Assert.Throws<ArgumentException>(() => Entry(new[] { P0 }, Array.Empty<CombatantId>()));
		Assert.Throws<ArgumentNullException>(() => Entry(null, new[] { E0 }));
		Assert.Throws<ArgumentNullException>(() => Entry(new[] { P0 }, null));
		Assert.Throws<ArgumentNullException>(() => new OrderEntry(new[] { P0 }, new[] { E0 }, null, canFlee: true));
	}

	[Fact]
	public void Constructor_RefusesTheWrongSide_AndRepeats()
	{
		Assert.Throws<ArgumentException>(() => Entry(new[] { E0 }, new[] { E2 }));
		Assert.Throws<ArgumentException>(() => Entry(new[] { P0, P0 }, new[] { E0 }));
		Assert.Throws<ArgumentException>(() => Entry(new[] { P0 }, new[] { P1 }));
		Assert.Throws<ArgumentException>(() => Entry(new[] { P0 }, new[] { E0, E0 }));
		Assert.Throws<ArgumentException>(() => new OrderEntry(new[] { P0 }, new[] { E0 }, new[] { P1 }, canFlee: true));
	}
}
