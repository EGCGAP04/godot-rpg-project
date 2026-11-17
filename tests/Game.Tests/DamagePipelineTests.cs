using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DamagePipeline"/> with <see cref="TestTuning"/>'s numbers and
/// scripted rolls. Every test says which rolls it expects and requires that none was left
/// over, so the number of rolls and their order are checked along with the result.
/// </summary>
/// <remarks>
/// The numbers worth having at hand, all against attacks: 100 armor removes 45% and 50
/// removes 30%; 100 luck gives a 47.5% critical chance and adds 20 to critical damage;
/// 100 luck on the defender is a 10% evasion. A variation roll of 50 changes nothing.
/// </remarks>
public class DamagePipelineTests
{
	private const double Max = 99.999999;

	private readonly DamagePipeline _pipeline = new(TestTuning.Formulas(), TestTuning.Combat());

	private static BaseStats Attacker(int attackDamage = 20, int hitRate = 100, int luck = 0) =>
		TestTuning.Stats(attackDamage: attackDamage, hitRate: hitRate, luck: luck);

	private static BaseStats Defender(int armor = 0, int agility = 0, int luck = 0) =>
		TestTuning.Stats(armor: armor, agility: agility, luck: luck);

	/// <summary>The rolls of a plain hit: it lands, is not evaded, is not critical, and does not vary.</summary>
	private static ScriptedCombatRandom PlainHit() =>
		new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(Max).Percent(50);

	private AttackResult Resolve(BaseStats attacker, BaseStats defender, ScriptedCombatRandom random, ActionKind kind = ActionKind.Attack, DamagePipeline pipeline = null)
	{
		AttackResult result = (pipeline ?? _pipeline).Resolve(attacker, defender, kind, random);
		random.RequireUsedUp();
		return result;
	}

	[Fact]
	public void Constructor_RefusesAMissingPart()
	{
		Assert.Throws<ArgumentNullException>(() => new DamagePipeline(null!, TestTuning.Combat()));
		Assert.Throws<ArgumentNullException>(() => new DamagePipeline(TestTuning.Formulas(), null!));
	}

	[Fact]
	public void Resolve_RefusesMissingDice()
	{
		Assert.Throws<ArgumentNullException>(() => _pipeline.Resolve(Attacker(), Defender(), ActionKind.Attack, null!));
	}

	// --- Accuracy ---

	[Fact]
	public void APlainHit_DoesTheAttackDamage_AndTakesFourRolls()
	{
		AttackResult result = Resolve(Attacker(attackDamage: 20), Defender(), PlainHit());

		Assert.Equal(AttackResult.Hit(20, isCritical: false), result);
	}

	[Theory]
	[InlineData(95, 94.9, true)]
	[InlineData(95, 95, false)]
	[InlineData(95, 99, false)]
	[InlineData(0, 24.9, true)] // The floor: a hit rate of 0 still lands 25% of the time.
	[InlineData(0, 25, false)]
	[InlineData(100, Max, true)]
	[InlineData(150, Max, true)]
	public void TheAttackLands_WhenTheFirstRollIsBelowTheHitChance(int hitRate, double roll, bool lands)
	{
		var random = new ScriptedCombatRandom().Percent(roll);
		if (lands)
			random.Percent(Max).Percent(Max).Percent(50);

		AttackResult result = Resolve(Attacker(hitRate: hitRate), Defender(), random);

		Assert.Equal(lands ? AttackOutcome.Hit : AttackOutcome.Missed, result.Outcome);
	}

	[Fact]
	public void AMiss_TakesOneRoll_AndDoesNoDamage_NotEvenTheMinimum()
	{
		var random = new ScriptedCombatRandom().Percent(60);

		AttackResult result = Resolve(Attacker(hitRate: 50), Defender(), random);

		Assert.Equal(AttackResult.Missed, result);
		Assert.Equal(0, result.Damage);
		Assert.False(result.IsCritical);
	}

	// --- Evasion ---

	[Theory]
	[InlineData(9.9, true)]
	[InlineData(10, false)]
	[InlineData(50, false)]
	public void TheAttackIsEvaded_WhenTheSecondRollIsBelowTheEvasionRate(double roll, bool evaded)
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(roll);
		if (!evaded)
			random.Percent(Max).Percent(50);

		AttackResult result = Resolve(Attacker(), Defender(luck: 100), random);

		Assert.Equal(evaded ? AttackOutcome.Evaded : AttackOutcome.Hit, result.Outcome);
	}

	[Fact]
	public void AnEvadedAttack_TakesTwoRolls_AndDoesNoDamage()
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(0);

		AttackResult result = Resolve(Attacker(luck: 100), Defender(luck: 100), random);

		Assert.Equal(AttackResult.Evaded, result);
	}

	[Fact]
	public void AnEvasionOfZero_StillRolls_AndNeverComesUp()
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(0).Percent(Max).Percent(50);

		AttackResult result = Resolve(Attacker(), Defender(), random);

		Assert.Equal(AttackOutcome.Hit, result.Outcome);
	}

	[Fact]
	public void ASkill_IsEvadedHalfAsOften()
	{
		// 100 luck is 10% against an attack and 5% against a skill.
		var random = new ScriptedCombatRandom().Percent(0).Percent(7).Percent(Max).Percent(50);

		AttackResult result = Resolve(Attacker(), Defender(luck: 100), random, ActionKind.Skill);

		Assert.Equal(AttackOutcome.Hit, result.Outcome);
	}

	// --- The critical hit ---

	[Theory]
	[InlineData(47.4, true)]
	[InlineData(47.5, false)]
	[InlineData(90, false)]
	public void TheHitIsCritical_WhenTheThirdRollIsBelowTheCriticalChance(double roll, bool critical)
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(roll).Percent(50);

		AttackResult result = Resolve(Attacker(luck: 100), Defender(), random);

		Assert.Equal(critical, result.IsCritical);
	}

	[Fact]
	public void ACriticalHit_DoesTheCriticalDamage_RoundedDown()
	{
		// 150 + 20 / 5 + 100 / 5 = 174%, and 20 * 1.74 = 34.8.
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(0).Percent(50);

		AttackResult result = Resolve(Attacker(attackDamage: 20, luck: 100), Defender(), random);

		Assert.Equal(AttackResult.Hit(34, isCritical: true), result);
	}

	[Fact]
	public void ACriticalChanceOfZero_StillRolls_AndNeverComesUp()
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(0).Percent(50);

		AttackResult result = Resolve(Attacker(luck: 0), Defender(), random);

		Assert.Equal(AttackResult.Hit(20, isCritical: false), result);
	}

	[Fact]
	public void TheRolls_AreTakenInOrder_AccuracyEvasionCritical()
	{
		// Both have 100 luck: 10% evasion, 47.5% critical. Read in any other order, these
		// three rolls give another result: 5 would evade, and 30 or 5 would be critical.
		var random = new ScriptedCombatRandom().Percent(5).Percent(30).Percent(80).Percent(50);

		AttackResult result = Resolve(Attacker(attackDamage: 20, hitRate: 20, luck: 100), Defender(luck: 100), random);

		Assert.Equal(AttackResult.Hit(20, isCritical: false), result);
	}

	// --- Armor ---

	[Theory]
	[InlineData(20, 0, 20)]
	[InlineData(20, 100, 11)] // 45% off.
	[InlineData(20, 300, 6)]  // 67.5% off: 6.5.
	[InlineData(100, 50, 70)] // 30% off.
	public void Armor_TakesItsReductionOutOfTheDamage(int attackDamage, int armor, int expected)
	{
		AttackResult result = Resolve(Attacker(attackDamage: attackDamage), Defender(armor: armor), PlainHit());

		Assert.Equal(expected, result.Damage);
	}

	[Fact]
	public void ASkill_GoesThroughArmor_WithTheSkillsNumbers()
	{
		// 50 armor removes 30% of an attack and 40% of a skill. Evasion is 50 / 30 here.
		AttackResult attack = Resolve(Attacker(attackDamage: 20), Defender(armor: 50), PlainHit(), ActionKind.Attack);
		AttackResult skill = Resolve(Attacker(attackDamage: 20), Defender(armor: 50), PlainHit(), ActionKind.Skill);

		Assert.Equal(14, attack.Damage);
		Assert.Equal(12, skill.Damage);
	}

	[Fact]
	public void ACriticalHit_GoesThroughArmorToo()
	{
		// 20 * 1.74 * 0.55 = 19.14.
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(0).Percent(50);

		AttackResult result = Resolve(Attacker(attackDamage: 20, luck: 100), Defender(armor: 100), random);

		Assert.Equal(AttackResult.Hit(19, isCritical: true), result);
	}

	// --- The variation ---

	[Theory]
	[InlineData(0, 90)]
	[InlineData(25, 95)]
	[InlineData(50, 100)]
	[InlineData(75, 105)]
	[InlineData(Max, 109)] // It never reaches 110.
	public void TheFourthRoll_MovesTheBaseDamage_TenPercentEitherWay(double roll, int expected)
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(Max).Percent(roll);

		AttackResult result = Resolve(Attacker(attackDamage: 100), Defender(), random);

		Assert.Equal(expected, result.Damage);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(50)]
	[InlineData(Max)]
	public void AVariationOfZero_StillRolls_AndChangesNothing(double roll)
	{
		var pipeline = new DamagePipeline(TestTuning.Formulas(), TestTuning.Combat(damageVariationPercent: 0));
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(Max).Percent(roll);

		AttackResult result = Resolve(Attacker(attackDamage: 100), Defender(), random, pipeline: pipeline);

		Assert.Equal(100, result.Damage);
	}

	[Theory]
	[InlineData(7)]
	[InlineData(33.3)]
	[InlineData(99)]
	public void ARollOfFifty_ChangesNothing_WhateverTheVariation(double variation)
	{
		var pipeline = new DamagePipeline(TestTuning.Formulas(), TestTuning.Combat(damageVariationPercent: variation));

		AttackResult result = Resolve(Attacker(attackDamage: 1000), Defender(), PlainHit(), pipeline: pipeline);

		Assert.Equal(1000, result.Damage);
	}

	// --- Rounding and the minimum ---

	[Fact]
	public void TheDamage_IsRoundedDownOnce_AtTheEnd()
	{
		// 11 * 0.9 = 9.9, and 9.9 * 0.55 = 5.445. Rounding the 9.9 down first would give 4.
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(Max).Percent(0);

		AttackResult result = Resolve(Attacker(attackDamage: 11), Defender(armor: 100), random);

		Assert.Equal(5, result.Damage);
	}

	[Fact]
	public void AWholeResult_IsNotLostToTheNoiseOfDouble()
	{
		// 90 * (1 - 0.3) is 62.99999999999999 in double, and it is 63.
		AttackResult result = Resolve(Attacker(attackDamage: 90), Defender(armor: 50), PlainHit());

		Assert.Equal(63, result.Damage);
	}

	[Fact]
	public void AWholeResult_IsNotLostToAReductionWithNoExactDecimal()
	{
		// 8 armor removes 6.666…%, a fifteenth, so 15 damage is exactly 14. Converting the
		// reduction to decimal cuts it short and gives 13.99….
		AttackResult result = Resolve(Attacker(attackDamage: 15), Defender(armor: 8), PlainHit());

		Assert.Equal(14, result.Damage);
	}

	[Theory]
	[InlineData(0, 0)]   // No attack at all.
	[InlineData(1, 100)] // 0.55.
	[InlineData(1, 5000)]
	public void AHitThatLands_DoesAtLeastOne(int attackDamage, int armor)
	{
		AttackResult result = Resolve(Attacker(attackDamage: attackDamage), Defender(armor: armor), PlainHit());

		Assert.Equal(AttackResult.Hit(1, isCritical: false), result);
	}

	[Fact]
	public void TheMinimum_IsTheTunings()
	{
		var pipeline = new DamagePipeline(TestTuning.Formulas(), TestTuning.Combat(minimumDamage: 3));

		Assert.Equal(3, Resolve(Attacker(attackDamage: 1), Defender(), PlainHit(), pipeline: pipeline).Damage);
		Assert.Equal(20, Resolve(Attacker(attackDamage: 20), Defender(), PlainHit(), pipeline: pipeline).Damage);
	}

	[Fact]
	public void DamageTooLargeForAnInt_StopsAtTheLargestInt()
	{
		var random = new ScriptedCombatRandom().Percent(0).Percent(Max).Percent(0).Percent(Max);

		AttackResult result = Resolve(Attacker(attackDamage: int.MaxValue, luck: 100), Defender(), random);

		Assert.Equal(AttackResult.Hit(int.MaxValue, isCritical: true), result);
	}

	[Fact]
	public void Resolve_ChangesNothing_SoTheSameRollsGiveTheSameResult()
	{
		BaseStats attacker = Attacker(attackDamage: 37, luck: 60);
		BaseStats defender = Defender(armor: 80, agility: 40, luck: 10);

		AttackResult first = Resolve(attacker, defender, new ScriptedCombatRandom().Percent(12).Percent(70).Percent(3).Percent(81));
		AttackResult second = Resolve(attacker, defender, new ScriptedCombatRandom().Percent(12).Percent(70).Percent(3).Percent(81));

		Assert.Equal(first, second);
		Assert.Equal(AttackOutcome.Hit, first.Outcome);
		Assert.True(first.IsCritical);
	}
}
