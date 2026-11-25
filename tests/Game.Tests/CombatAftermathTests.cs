using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GodotRPGProject.Tests.TestRoster;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatAftermath"/> over short fights with scripted rolls: what each
/// member leaves with, who is standing, the experience, and how it is written to the party.
/// </summary>
/// <remarks>
/// The characters are <see cref="TestRoster"/>'s: in the Fantasy world at level 1, 20
/// health points, 10 mana points, 4 attack and no agility. The foe has 10 agility, so it
/// acts first, and its attack is scripted to hit; a member's is too.
/// </remarks>
public class CombatAftermathTests
{
	private const long FoeExperience = 7;

	private static EncounterValues Foe(int healthPoints = 4, int attackDamage = 25) =>
		new("test_group", new[]
		{
			new EnemyValues("foe", "enemy.foe", TestTuning.Stats(healthPoints: healthPoints, attackDamage: attackDamage, agility: 10, hitRate: 100), FoeExperience)
		});

	private static PartyState HeroAndAbe()
	{
		PartyState party = Party("abe");
		party.Recruit("abe");
		return party;
	}

	private static CombatRunner Start(CombatLineup lineup, ScriptedCombatRandom random) =>
		CombatRunner.Start(lineup.Party, lineup.Enemies, canFlee: true, TestTuning.Formulas(), TestTuning.Combat(), random);

	/// <summary>
	/// A win where abe goes down: the foe hits abe for 25 first, then the hero hits the foe
	/// for its 4 health points. abe's turn never comes.
	/// </summary>
	private static (CombatLineup Lineup, CombatRunner Runner) AWinWhereAbeGoesDown(PartyState party)
	{
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Foe());
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 2); // hero before abe, who tie on agility
		random.Index(1, among: 2); // the foe picks abe
		random.Hit();
		random.Hit(); // the hero's attack

		CombatRunner runner = Start(lineup, random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)), new AttackOrder(CombatantId.Party(1), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Won, runner.Result);
		random.RequireUsedUp();
		return (lineup, runner);
	}

	[Fact]
	public void AFlight_GivesNoExperience_AndWhoWentDownBeforeIt_LeavesWithOneHealthAndOneMana()
	{
		PartyState party = HeroAndAbe();
		CombatAftermath aftermath = TestFights.AFlight(party, lastGoesDownFirst: true);

		Assert.Equal(CombatResult.Fled, aftermath.Result);
		Assert.Equal(FightOutcome.Fled, aftermath.Outcome);
		Assert.False(aftermath.Won);
		Assert.Equal(0, aftermath.Experience);
		Assert.Equal(new[] { Hero }, aftermath.Standing);
		Assert.Equal(
			new[] { new MemberAftermath(Hero, 20, 10, WentDown: false), new MemberAftermath("abe", 1, 1, WentDown: true) },
			aftermath.Members);

		ExperienceAward award = aftermath.ApplyTo(party);

		Assert.Equal(0, award.Experience);
		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 20, 10), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 1, 1), party.ProgressOf("abe", World.Fantasy));
	}

	[Fact]
	public void AFlight_LeavesTheEnemyOnTheMap_AndThePlayerWhereTheyAre()
	{
		CombatAftermath aftermath = TestFights.AFlight(HeroAndAbe(), lastGoesDownFirst: false);

		Assert.False(aftermath.DefeatsTheEnemy);
		Assert.False(aftermath.SendsThePlayerBack);
	}

	/// <summary>A loss: the hero, alone, takes <paramref name="damage"/> from the foe and goes down.</summary>
	private static (CombatLineup Lineup, CombatRunner Runner) ALoss(PartyState party, World world, int damage)
	{
		CombatLineup lineup = CombatLineup.For(party, world, Foe(attackDamage: damage));
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 1); // the foe picks the hero
		random.Hit();

		CombatRunner runner = Start(lineup, random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Lost, runner.Result);
		random.RequireUsedUp();
		return (lineup, runner);
	}

	/// <summary>A win where nobody goes down: the foe's hit for 5 is answered as taken, and the hero ends it.</summary>
	private static (CombatLineup Lineup, CombatRunner Runner) AWinWhereTheHeroTakesFive(PartyState party)
	{
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Foe(attackDamage: 5));
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 1);
		random.Hit();
		random.Hit();

		CombatRunner runner = Start(lineup, random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);

		Assert.Equal(CombatResult.Won, runner.Result);
		random.RequireUsedUp();
		return (lineup, runner);
	}

	// --- What each member leaves with ---

	[Fact]
	public void AMemberStanding_LeavesWithTheHealthAndManaTheyHadLeft()
	{
		(CombatLineup lineup, CombatRunner runner) = AWinWhereTheHeroTakesFive(Party());

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(new[] { new MemberAftermath(Hero, 15, 10, WentDown: false) }, aftermath.Members);
	}

	[Fact]
	public void AMemberDown_LeavesWithOneHealthPointAndOneManaPoint()
	{
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(HeroAndAbe());

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(
			new[] { new MemberAftermath(Hero, 20, 10, WentDown: false), new MemberAftermath("abe", 1, 1, WentDown: true) },
			aftermath.Members);
	}

	[Fact]
	public void AMemberDown_LeavesWithOneManaPoint_EvenWithNoneBefore()
	{
		PartyState party = HeroAndAbe();
		party.SetManaPoints("abe", World.Fantasy, 0);
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(party);

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(1, aftermath.Members[1].ManaPoints);
	}

	[Fact]
	public void AMemberDown_LeavesWithNoMana_WhereTheirMostIsNone()
	{
		// In the Real world the test characters have no mana, and 51 health points at 12.
		(CombatLineup lineup, CombatRunner runner) = ALoss(Party(), World.Real, damage: 60);

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(new[] { new MemberAftermath(Hero, 1, 0, WentDown: true) }, aftermath.Members);
	}

	// --- How it ended, who stands, and the experience ---

	[Fact]
	public void AWin_GivesTheGroupsExperience_ToThoseStanding()
	{
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(HeroAndAbe());

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(CombatResult.Won, aftermath.Result);
		Assert.True(aftermath.Won);
		Assert.Equal(FightOutcome.Won, aftermath.Outcome);
		Assert.Equal(World.Fantasy, aftermath.World);
		Assert.Same(lineup, aftermath.Lineup);
		Assert.Equal(new[] { Hero }, aftermath.Standing);
		Assert.Equal(FoeExperience, aftermath.Experience);
	}

	[Fact]
	public void ALoss_LeavesEveryoneDown_AndGivesNoExperience()
	{
		(CombatLineup lineup, CombatRunner runner) = ALoss(Party(), World.Fantasy, damage: 25);

		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);

		Assert.Equal(CombatResult.Lost, aftermath.Result);
		Assert.False(aftermath.Won);
		Assert.Equal(FightOutcome.Lost, aftermath.Outcome);
		Assert.Empty(aftermath.Standing);
		Assert.Equal(0, aftermath.Experience);
		Assert.Equal(new[] { new MemberAftermath(Hero, 1, 1, WentDown: true) }, aftermath.Members);
	}

	// --- Writing it to the party ---

	[Fact]
	public void ApplyTo_WritesEachMembersHealthAndMana_AndAwardsThoseStanding()
	{
		PartyState party = HeroAndAbe();
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(party);

		ExperienceAward award = CombatAftermath.Of(lineup, runner).ApplyTo(party);

		Assert.Equal(new CharacterProgress(new LevelProgress(1, 7), 20, 10), party.ProgressOf(Hero, World.Fantasy));
		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 1, 1), party.ProgressOf("abe", World.Fantasy));
		Assert.Equal(new[] { Hero }, award.Gains.Select(gain => gain.Id));
		Assert.Equal(FoeExperience, award.Experience);
	}

	[Fact]
	public void ApplyTo_WritesTheHealthLeft_OfAMemberStanding()
	{
		PartyState party = Party();
		(CombatLineup lineup, CombatRunner runner) = AWinWhereTheHeroTakesFive(party);

		CombatAftermath.Of(lineup, runner).ApplyTo(party);

		Assert.Equal(15, party.ProgressOf(Hero, World.Fantasy).HealthPoints);
	}

	[Fact]
	public void ApplyTo_AfterALoss_WritesOneAndOne_AndAwardsNobody()
	{
		PartyState party = Party();
		(CombatLineup lineup, CombatRunner runner) = ALoss(party, World.Fantasy, damage: 25);

		ExperienceAward award = CombatAftermath.Of(lineup, runner).ApplyTo(party);

		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 1, 1), party.ProgressOf(Hero, World.Fantasy));
		Assert.Empty(award.Gains);
	}

	[Fact]
	public void ApplyTo_WritesOnlyTheWorldOfTheFight()
	{
		PartyState party = HeroAndAbe();
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(party);

		CombatAftermath.Of(lineup, runner).ApplyTo(party);

		Assert.Equal(new CharacterProgress(new LevelProgress(1, 0), 30, 6), party.ProgressOf("abe", World.Nightmare));
	}

	[Fact]
	public void PreviewAward_SaysWhatApplyToWillAward_AndChangesNothing()
	{
		PartyState party = HeroAndAbe();
		party.Award(World.Fantasy, 5); // so the win's 7 reaches level 2
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(party);
		CombatAftermath aftermath = CombatAftermath.Of(lineup, runner);
		PartySnapshot before = party.TakeSnapshot();

		ExperienceAward preview = aftermath.PreviewAward(party);

		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
		Assert.Equal(new[] { new MemberGain(Hero, new LevelProgress(1, 5), new LevelProgress(2, 2)) }, preview.Gains);

		ExperienceAward award = aftermath.ApplyTo(party);

		Assert.Equal(preview.Experience, award.Experience);
		Assert.Equal(preview.Gains, award.Gains);
	}

	[Fact]
	public void ApplyTo_LeavesOutOfTheAward_AMemberWhoLeftThePartyDuringTheFight()
	{
		// The debug overlay can open over a fight and dismiss a member.
		PartyState party = HeroAndAbe();
		(CombatLineup lineup, CombatRunner runner) = AWinWhereTheHeroTakesFiveWithAbe(party);
		party.Dismiss("abe");

		ExperienceAward award = CombatAftermath.Of(lineup, runner).ApplyTo(party);

		Assert.Equal(new[] { Hero }, award.Gains.Select(gain => gain.Id));
		Assert.Equal(new LevelProgress(1, 0), party.ProgressOf("abe", World.Fantasy).Level);
		Assert.Equal(15, party.ProgressOf("abe", World.Fantasy).HealthPoints);
	}

	/// <summary>A win with both standing: the foe's hit on abe is answered as 5, and the hero ends it.</summary>
	private static (CombatLineup Lineup, CombatRunner Runner) AWinWhereTheHeroTakesFiveWithAbe(PartyState party)
	{
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Foe());
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 2);
		random.Index(1, among: 2);
		random.Hit();
		random.Hit();

		CombatRunner runner = Start(lineup, random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)), new AttackOrder(CombatantId.Party(1), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(5);

		Assert.Equal(CombatResult.Won, runner.Result);
		random.RequireUsedUp();
		return (lineup, runner);
	}

	// --- What it refuses ---

	[Fact]
	public void Of_RefusesAFightThatIsNotOver()
	{
		CombatLineup lineup = CombatLineup.For(Party(), World.Fantasy, Foe());
		CombatRunner runner = Start(lineup, new ScriptedCombatRandom());

		Assert.Throws<InvalidOperationException>(() => CombatAftermath.Of(lineup, runner));
	}

	[Fact]
	public void Of_RefusesARunnerFightingAnotherLineup()
	{
		(CombatLineup _, CombatRunner runner) = AWinWhereAbeGoesDown(HeroAndAbe());

		CombatLineup ofOne = CombatLineup.For(Party(), World.Fantasy, Foe());
		CombatLineup otherEnemies = CombatLineup.For(HeroAndAbe(), World.Fantasy, new EncounterValues("other", new[] { new EnemyValues("weak", "enemy.weak", TestTuning.Stats(healthPoints: 4), 1) }));

		Assert.Throws<ArgumentException>(() => CombatAftermath.Of(ofOne, runner));
		Assert.Throws<ArgumentException>(() => CombatAftermath.Of(otherEnemies, runner));
	}

	[Fact]
	public void Of_RefusesWhatIsMissing()
	{
		(CombatLineup lineup, CombatRunner runner) = AWinWhereAbeGoesDown(HeroAndAbe());

		Assert.Throws<ArgumentNullException>(() => CombatAftermath.Of(null, runner));
		Assert.Throws<ArgumentNullException>(() => CombatAftermath.Of(lineup, null));
		Assert.Throws<ArgumentNullException>(() => CombatAftermath.Of(lineup, runner).ApplyTo(null));
		Assert.Throws<ArgumentNullException>(() => CombatAftermath.Of(lineup, runner).PreviewAward(null));
	}
}
