using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GodotRPGProject.Tests.TestRoster;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="CombatLineup"/>: the party goes into a fight as it stands in the
/// world, the group at full, and each position remembers its character.
/// </summary>
public class CombatLineupTests
{
	private static EnemyValues Enemy(string id, int healthPoints) =>
		new(id, EnemyValues.NameKeyOf(id), TestTuning.Stats(healthPoints: healthPoints, attackDamage: 3, agility: 2), experience: 5);

	private static EncounterValues Group(params EnemyValues[] enemies) => new("test_group", enemies);

	private static PartyState PartyOfThree()
	{
		PartyState party = Party("abe", "bea");
		party.Recruit("bea");
		party.Recruit("abe");
		return party;
	}

	[Fact]
	public void TheMembers_GoInInThePartysOrder_WithTheirNamesAndTheirStatsInTheWorld()
	{
		PartyState party = PartyOfThree();
		party.Award(World.Fantasy, 10, new[] { "bea" }); // level 2: 25 health points at most

		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Equal(new[] { Hero, "bea", "abe" }, lineup.MemberIds);
		Assert.Equal(new[] { 20, 25, 20 }, lineup.Party.Select(member => member.Stats.HealthPoints));
		Assert.Equal(new[] { "speaker.hero", "speaker.bea", "speaker.abe" }, lineup.Party.Select(member => member.NameKey));
		Assert.Equal(lineup.MemberIds.Select(id => party.StatsOf(id, World.Fantasy)), lineup.Party.Select(member => member.Stats));
	}

	[Fact]
	public void TheMembers_BringTheStatsOfTheWorldTheFightIsIn()
	{
		PartyState party = PartyOfThree();

		CombatLineup fantasy = CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));
		CombatLineup nightmare = CombatLineup.For(party, World.Nightmare, Group(Enemy("weak", 10)));

		Assert.Equal(World.Fantasy, fantasy.World);
		Assert.Equal(20, fantasy.Party[0].Stats.HealthPoints);
		Assert.Equal(World.Nightmare, nightmare.World);
		Assert.Equal(30, nightmare.Party[0].Stats.HealthPoints);
	}

	[Fact]
	public void TheMembers_BringTheHealthAndManaTheyHaveLeft()
	{
		PartyState party = PartyOfThree();
		party.SetHealthPoints("bea", World.Fantasy, 7);
		party.SetManaPoints("abe", World.Fantasy, 3);

		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Equal(new[] { 20, 7, 20 }, lineup.Party.Select(member => member.HealthPoints));
		Assert.Equal(new[] { 10, 10, 3 }, lineup.Party.Select(member => member.ManaPoints));
	}

	[Fact]
	public void AMemberWithNoHealthLeft_ComesInDown()
	{
		PartyState party = PartyOfThree();
		party.SetHealthPoints("abe", World.Fantasy, 0);

		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Equal(0, lineup.Party[2].HealthPoints);
	}

	[Fact]
	public void TheEnemies_GoInAtFull_InTheGroupsOrder_RepeatsIncluded()
	{
		EnemyValues weak = Enemy("weak", 10);

		CombatLineup lineup = CombatLineup.For(PartyOfThree(), World.Fantasy, Group(Enemy("strong", 40), weak, weak));

		Assert.Equal(new[] { "enemy.strong", "enemy.weak", "enemy.weak" }, lineup.Enemies.Select(enemy => enemy.NameKey));
		Assert.Equal(new[] { 40, 10, 10 }, lineup.Enemies.Select(enemy => enemy.HealthPoints));
		Assert.All(lineup.Enemies, enemy => Assert.Equal(enemy.Stats.HealthPoints, enemy.HealthPoints));
		Assert.Equal(weak.Stats, lineup.Enemies[1].Stats);
	}

	[Fact]
	public void TheLineup_KeepsTheGroupItWasMadeFor()
	{
		EncounterValues group = Group(Enemy("weak", 10));

		Assert.Same(group, CombatLineup.For(PartyOfThree(), World.Fantasy, group).Encounter);
	}

	[Fact]
	public void MakingALineup_ChangesNothingInTheParty()
	{
		PartyState party = PartyOfThree();
		party.SetHealthPoints("bea", World.Fantasy, 7);
		PartySnapshot before = party.TakeSnapshot();
		var changes = new List<string>();
		party.ProgressChanged += (id, _) => changes.Add(id);
		party.MembersChanged += () => changes.Add("members");

		CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Empty(changes);
		Assert.Equal(before.Members, party.TakeSnapshot().Members);
		Assert.Equal(before.Progress, party.TakeSnapshot().Progress);
	}

	[Fact]
	public void TheLineup_IsAPicture_ThatALaterChangeToThePartyDoesNotMove()
	{
		PartyState party = PartyOfThree();
		CombatLineup lineup = CombatLineup.For(party, World.Fantasy, Group(Enemy("weak", 10)));

		party.SetHealthPoints(Hero, World.Fantasy, 1);
		party.Dismiss("abe");

		Assert.Equal(20, lineup.Party[0].HealthPoints);
		Assert.Equal(new[] { Hero, "bea", "abe" }, lineup.MemberIds);
	}

	[Fact]
	public void MemberAt_GivesTheCharacterInAPosition()
	{
		CombatLineup lineup = CombatLineup.For(PartyOfThree(), World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Equal(Hero, lineup.MemberAt(CombatantId.Party(0)));
		Assert.Equal("abe", lineup.MemberAt(CombatantId.Party(2)));
	}

	[Fact]
	public void MemberAt_RefusesAnEnemy_AndAPositionTheLineupDoesNotHave()
	{
		CombatLineup lineup = CombatLineup.For(PartyOfThree(), World.Fantasy, Group(Enemy("weak", 10)));

		Assert.Throws<ArgumentException>(() => lineup.MemberAt(CombatantId.Enemy(0)));
		Assert.Throws<ArgumentException>(() => lineup.MemberAt(CombatantId.Party(3)));
	}

	[Fact]
	public void For_RefusesWhatIsMissing_AndAWorldThatDoesNotExist()
	{
		EncounterValues group = Group(Enemy("weak", 10));

		Assert.Throws<ArgumentNullException>(() => CombatLineup.For(null, World.Fantasy, group));
		Assert.Throws<ArgumentNullException>(() => CombatLineup.For(PartyOfThree(), World.Fantasy, null));
		Assert.Throws<ArgumentOutOfRangeException>(() => CombatLineup.For(PartyOfThree(), (World)7, group));
	}

	[Fact]
	public void TheLineup_StartsAFight()
	{
		CombatLineup lineup = CombatLineup.For(PartyOfThree(), World.Fantasy, Group(Enemy("weak", 10), Enemy("weak", 10)));

		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, canFlee: true, TestTuning.Formulas(), TestTuning.Combat(), new ScriptedCombatRandom());

		Assert.Equal(3, runner.Party.Count);
		Assert.Equal(2, runner.Enemies.Count);
		Assert.Equal(CombatStep.AwaitingOrders, runner.Step);
	}
}
