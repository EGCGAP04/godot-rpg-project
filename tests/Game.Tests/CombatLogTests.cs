using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>Exercises <see cref="CombatLog"/>: the line each event of a fight shows.</summary>
public class CombatLogTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		[CombatLog.Round] = "R{round}",
		[CombatLog.Hit] = "{attacker} hit {target} {damage}",
		[CombatLog.Critical] = "{attacker} crit {target} {damage}",
		[CombatLog.Missed] = "{attacker} missed {target}",
		[CombatLog.Evaded] = "{target} evaded {attacker}",
		[CombatLog.Downed] = "{name} down",
		[CombatantNames.NameWithLetter] = "{name}{letter}",
		["speaker.hero"] = "Hero",
		["enemy.weak"] = "Weak"
	};

	private static string Translate(string key) => Texts[key];

	private static readonly CombatantNames Names = new(new[] { "speaker.hero" }, new[] { "enemy.weak", "enemy.weak" }, Translate);

	private static string Line(CombatEvent happened) => CombatLog.LineFor(happened, Names, Translate);

	private static AttackResolved Attack(CombatantId attacker, CombatantId target, AttackResult result, int damageTaken) =>
		new(attacker, target, result, damageTaken);

	[Fact]
	public void ARound_SaysItsNumber()
	{
		Assert.Equal("R3", Line(new RoundStarted(3, new[] { CombatantId.Party(0) })));
	}

	[Fact]
	public void AHit_SaysWhoHitWhomAndTheDamageTaken()
	{
		Assert.Equal("Hero hit WeakB 6", Line(Attack(CombatantId.Party(0), CombatantId.Enemy(1), AttackResult.Hit(6, isCritical: false), 6)));
	}

	[Fact]
	public void AnEnemysHit_SaysTheDamageAnswered_NotTheDamageWorkedOut()
	{
		// Once the player can dodge, what was taken is less than what was worked out.
		Assert.Equal("WeakA hit Hero 2", Line(Attack(CombatantId.Enemy(0), CombatantId.Party(0), AttackResult.Hit(9, isCritical: false), 2)));
	}

	[Fact]
	public void ACriticalHit_HasALineOfItsOwn()
	{
		Assert.Equal("Hero crit WeakA 12", Line(Attack(CombatantId.Party(0), CombatantId.Enemy(0), AttackResult.Hit(12, isCritical: true), 12)));
	}

	[Fact]
	public void AMiss_AndAnEvasion_SayWhoAttackedWhom()
	{
		Assert.Equal("WeakB missed Hero", Line(Attack(CombatantId.Enemy(1), CombatantId.Party(0), AttackResult.Missed, 0)));
		Assert.Equal("WeakA evaded Hero", Line(Attack(CombatantId.Party(0), CombatantId.Enemy(0), AttackResult.Evaded, 0)));
	}

	[Fact]
	public void SomeoneDown_SaysWho()
	{
		Assert.Equal("WeakB down", Line(new CombatantDowned(CombatantId.Enemy(1))));
	}

	[Fact]
	public void TheEndOfTheFight_HasNoLine()
	{
		Assert.Null(Line(new FightEnded(CombatResult.Won)));
	}

	private sealed record Unknown : CombatEvent;

	[Fact]
	public void AnEventTheLogDoesNotKnow_IsRefused()
	{
		Assert.Throws<ArgumentException>(() => Line(new Unknown()));
	}

	[Fact]
	public void WhatIsMissing_IsRefused()
	{
		Assert.Throws<ArgumentNullException>(() => CombatLog.LineFor(null, Names, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatLog.LineFor(new FightEnded(CombatResult.Won), null, Translate));
		Assert.Throws<ArgumentNullException>(() => CombatLog.LineFor(new FightEnded(CombatResult.Won), Names, null));
	}

	[Fact]
	public void AWholeFightsEvents_ReadAsALog()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		CombatAftermath aftermath = TestFights.AWinWhereTheLastGoesDown(party);
		var texts = new Dictionary<string, string>(Texts) { ["speaker.abe"] = "Abe", ["enemy.foe"] = "Foe" };
		CombatantNames names = CombatantNames.Of(aftermath.Lineup, key => texts[key]);

		// The fight is played again here to read its events: the aftermath does not keep them.
		CombatRunner runner = Replay(aftermath.Lineup);
		string[] lines = runner.TakeEvents().Select(happened => CombatLog.LineFor(happened, names, key => texts[key])).Where(line => line != null).ToArray();

		Assert.Equal(new[] { "R1", "Foe hit Abe 25", "Abe down", "Hero hit Foe 4", "Foe down" }, lines);
	}

	private static CombatRunner Replay(CombatLineup lineup)
	{
		var random = new ScriptedCombatRandom();
		random.Index(0, among: 2);
		random.Index(1, among: 2);
		random.Hit();
		random.Hit();

		CombatRunner runner = CombatRunner.Start(lineup.Party, lineup.Enemies, TestTuning.Formulas(), TestTuning.Combat(), random);
		runner.SubmitOrders(new CombatOrder[] { new AttackOrder(CombatantId.Party(0), CombatantId.Enemy(0)), new AttackOrder(CombatantId.Party(1), CombatantId.Enemy(0)) });
		runner.AnswerEnemyAttack(runner.PendingAttack.Result.Damage);
		return runner;
	}

	[Theory]
	[InlineData("en", "Round 2|Enemy A hits Strong Enemy for 6.|Critical hit! Enemy B hits Strong Enemy for 12.|Enemy A misses Strong Enemy.|Strong Enemy evades Enemy B's attack.|Enemy B goes down.")]
	[InlineData("es", "Ronda 2|Enemigo A golpea a Enemigo fuerte: 6 de daño.|¡Golpe crítico! Enemigo B golpea a Enemigo fuerte: 12 de daño.|Enemigo A no le acierta a Enemigo fuerte.|Enemigo fuerte esquiva el ataque de Enemigo B.|Enemigo B cae.")]
	public void WithTheGamesTables_EveryLineIsFilled(string locale, string expected)
	{
		Dictionary<string, string> texts = ProjectFiles.TranslationFiles()
			.Where(file => file.Folder == LocalizationNames.SharedFolder && file.Locale == locale)
			.Select(TranslationTableReader.Read)
			.SelectMany(table => table.Entries)
			.ToDictionary(entry => entry.Key, entry => entry.Text);
		var names = new CombatantNames(new[] { "enemy.weak", "enemy.weak" }, new[] { "enemy.strong" }, key => texts[key]);
		CombatantId a = CombatantId.Party(0), b = CombatantId.Party(1), strong = CombatantId.Enemy(0);

		CombatEvent[] events =
		{
			new RoundStarted(2, new[] { a }),
			Attack(a, strong, AttackResult.Hit(6, isCritical: false), 6),
			Attack(b, strong, AttackResult.Hit(12, isCritical: true), 12),
			Attack(a, strong, AttackResult.Missed, 0),
			Attack(b, strong, AttackResult.Evaded, 0),
			new CombatantDowned(b)
		};

		Assert.Equal(expected, string.Join("|", events.Select(happened => CombatLog.LineFor(happened, names, key => texts[key]))));
	}
}
