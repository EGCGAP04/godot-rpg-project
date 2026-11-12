using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatFormulas"/> with <see cref="TestTuning"/>'s numbers, where
/// <c>K</c> is 100 against attacks and 50 against skills.
/// </summary>
public class StatFormulasTests
{
	private readonly StatFormulas _formulas = TestTuning.Formulas();

	[Fact]
	public void Constructor_RefusesAMissingTuning()
	{
		Assert.Throws<ArgumentNullException>(() => new StatFormulas(null!));
	}

	// --- Armor reduction ---

	[Theory]
	[InlineData(0, ActionKind.Attack, 0)]
	[InlineData(100, ActionKind.Attack, 45)]
	[InlineData(300, ActionKind.Attack, 67.5)]
	[InlineData(0, ActionKind.Skill, 0)]
	[InlineData(50, ActionKind.Skill, 40)]
	[InlineData(150, ActionKind.Skill, 60)]
	public void ArmorReduction_FollowsTheCurve_WithEachKindsCapAndK(int armor, ActionKind kind, double expected)
	{
		Assert.Equal(expected, _formulas.ArmorReduction(TestTuning.Stats(armor: armor), kind), precision: 10);
	}

	[Fact]
	public void ArmorReduction_StaysBelowEachKindsCap()
	{
		BaseStats wall = TestTuning.Stats(armor: int.MaxValue);

		Assert.InRange(_formulas.ArmorReduction(wall, ActionKind.Attack), 89.9, 89.9999999999);
		Assert.InRange(_formulas.ArmorReduction(wall, ActionKind.Skill), 79.9, 79.9999999999);
	}

	[Fact]
	public void ArmorReduction_ReadsArmorAndNothingElse()
	{
		BaseStats onlyArmor = TestTuning.Stats(armor: 100);
		BaseStats everything = new(999, 999, 100, 999, 999, 999, 999);

		Assert.Equal(_formulas.ArmorReduction(onlyArmor, ActionKind.Attack), _formulas.ArmorReduction(everything, ActionKind.Attack));
	}

	// --- Critical chance ---

	[Theory]
	[InlineData(0, ActionKind.Attack, 0)]
	[InlineData(100, ActionKind.Attack, 47.5)]
	[InlineData(300, ActionKind.Attack, 71.25)]
	[InlineData(0, ActionKind.Skill, 0)]
	[InlineData(50, ActionKind.Skill, 45)]
	[InlineData(150, ActionKind.Skill, 67.5)]
	public void CriticalChance_FollowsTheCurve_WithEachKindsCapAndK(int luck, ActionKind kind, double expected)
	{
		Assert.Equal(expected, _formulas.CriticalChance(TestTuning.Stats(luck: luck), kind), precision: 10);
	}

	[Fact]
	public void CriticalChance_StaysBelowEachKindsCap()
	{
		BaseStats charmed = TestTuning.Stats(luck: int.MaxValue);

		Assert.InRange(_formulas.CriticalChance(charmed, ActionKind.Attack), 94.9, 94.9999999999);
		Assert.InRange(_formulas.CriticalChance(charmed, ActionKind.Skill), 89.9, 89.9999999999);
	}

	[Fact]
	public void CriticalChance_ReadsLuckAndNothingElse()
	{
		BaseStats onlyLuck = TestTuning.Stats(luck: 100);
		BaseStats everything = new(999, 999, 999, 999, 999, 100, 999);

		Assert.Equal(_formulas.CriticalChance(onlyLuck, ActionKind.Attack), _formulas.CriticalChance(everything, ActionKind.Attack));
	}

	[Fact]
	public void AKindThatIsNeither_IsRefused()
	{
		BaseStats stats = TestTuning.Stats(armor: 10, luck: 10);

		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.ArmorReduction(stats, (ActionKind)7));
		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.CriticalChance(stats, (ActionKind)7));
	}

	// --- Hit chance ---

	[Theory]
	[InlineData(0, 25)]
	[InlineData(24, 25)]
	[InlineData(25, 25)]
	[InlineData(26, 26)]
	[InlineData(60, 60)]
	[InlineData(99, 99)]
	[InlineData(100, 100)]
	[InlineData(101, 100)]
	[InlineData(int.MaxValue, 100)]
	public void HitChance_IsTheHitRate_BetweenItsFloorAndOneHundred(int hitRate, double expected)
	{
		Assert.Equal(expected, _formulas.HitChance(TestTuning.Stats(hitRate: hitRate)));
	}

	[Fact]
	public void HitChance_UsesTheTuningsFloor()
	{
		var formulas = new StatFormulas(TestTuning.Values(hitChanceFloor: 40));

		Assert.Equal(40, formulas.HitChance(TestTuning.Stats(hitRate: 10)));
		Assert.Equal(41, formulas.HitChance(TestTuning.Stats(hitRate: 41)));
	}

	// --- Critical damage ---

	[Theory]
	[InlineData(0, 0, 0, 150)]
	[InlineData(100, 0, 0, 150)]
	[InlineData(130, 0, 0, 180)]
	[InlineData(0, 50, 0, 160)]
	[InlineData(0, 0, 50, 160)]
	[InlineData(130, 50, 25, 195)]
	[InlineData(0, 7, 0, 151.4)]
	[InlineData(0, 0, 3, 150.6)]
	public void CriticalDamage_AddsHitRateAboveOneHundred_AFifthOfAttackDamage_AndAFifthOfLuck(int hitRate, int attackDamage, int luck, double expected)
	{
		BaseStats attacker = TestTuning.Stats(hitRate: hitRate, attackDamage: attackDamage, luck: luck);

		Assert.Equal(expected, _formulas.CriticalDamage(attacker, ActionKind.Attack), precision: 10);
		Assert.Equal(expected, _formulas.CriticalDamage(attacker, ActionKind.Skill), precision: 10);
	}

	[Fact]
	public void CriticalDamage_GainsNothingFromHitRateBelowOneHundred()
	{
		Assert.Equal(150, _formulas.CriticalDamage(TestTuning.Stats(hitRate: 40), ActionKind.Attack));
		Assert.Equal(150, _formulas.CriticalDamage(TestTuning.Stats(hitRate: 99), ActionKind.Attack));
	}

	[Theory]
	[InlineData(249, ActionKind.Attack, 299)]
	[InlineData(249, ActionKind.Skill, 299)]
	[InlineData(250, ActionKind.Skill, 300)]
	[InlineData(251, ActionKind.Skill, 300)]
	[InlineData(251, ActionKind.Attack, 301)]
	[InlineData(450, ActionKind.Attack, 500)]
	[InlineData(451, ActionKind.Attack, 500)]
	[InlineData(int.MaxValue, ActionKind.Attack, 500)]
	[InlineData(int.MaxValue, ActionKind.Skill, 300)]
	public void CriticalDamage_StopsAtEachKindsCap(int hitRate, ActionKind kind, double expected)
	{
		Assert.Equal(expected, _formulas.CriticalDamage(TestTuning.Stats(hitRate: hitRate), kind), precision: 10);
	}

	[Fact]
	public void CriticalDamage_UsesTheTuningsBaseAndCaps()
	{
		var formulas = new StatFormulas(TestTuning.Values(criticalDamageBase: 120, criticalDamageCap: new ByAction(200, 130)));
		BaseStats sharp = TestTuning.Stats(hitRate: 150);

		Assert.Equal(120, formulas.CriticalDamage(TestTuning.Stats(), ActionKind.Attack));
		Assert.Equal(170, formulas.CriticalDamage(sharp, ActionKind.Attack));
		Assert.Equal(130, formulas.CriticalDamage(sharp, ActionKind.Skill));
	}

	[Fact]
	public void CriticalDamage_IgnoresArmorAgilityHealthAndMana()
	{
		BaseStats bare = TestTuning.Stats(hitRate: 130, attackDamage: 50, luck: 25);
		BaseStats padded = new(healthPoints: 999, attackDamage: 50, armor: 999, manaPoints: 999, agility: 999, luck: 25, hitRate: 130);

		Assert.Equal(_formulas.CriticalDamage(bare, ActionKind.Attack), _formulas.CriticalDamage(padded, ActionKind.Attack));
	}

	// --- Evasion rate ---

	[Theory]
	[InlineData(0, 0, 0, 0)]
	[InlineData(30, 0, 0, 1)]
	[InlineData(0, 20, 0, 1)]
	[InlineData(0, 0, 10, 1)]
	[InlineData(60, 40, 30, 7)]
	[InlineData(10, 0, 0, 0.3333333333333333)]
	[InlineData(0, 7, 0, 0.35)]
	public void EvasionRate_AddsAThirtiethOfArmor_ATwentiethOfAgility_AndATenthOfLuck(int armor, int agility, int luck, double expected)
	{
		BaseStats defender = TestTuning.Stats(armor: armor, agility: agility, luck: luck);

		Assert.Equal(expected, _formulas.EvasionRate(defender, ActionKind.Attack), precision: 10);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(100, 5)]
	[InlineData(70, 3.5)]
	public void EvasionRate_IsHalvedAgainstASkill(int luck, double expected)
	{
		BaseStats defender = TestTuning.Stats(luck: luck);

		Assert.Equal(expected, _formulas.EvasionRate(defender, ActionKind.Skill), precision: 10);
		Assert.Equal(_formulas.EvasionRate(defender, ActionKind.Attack) / 2, _formulas.EvasionRate(defender, ActionKind.Skill), precision: 10);
	}

	[Theory]
	[InlineData(499, 49.9, 24.95)]
	[InlineData(500, 50, 25)]
	[InlineData(501, 50, 25)]
	[InlineData(900, 50, 25)]
	[InlineData(int.MaxValue, 50, 25)]
	public void EvasionRate_StopsAtItsCap_AndASkillAtHalfOfIt(int luck, double againstAttack, double againstSkill)
	{
		BaseStats defender = TestTuning.Stats(luck: luck);

		Assert.Equal(againstAttack, _formulas.EvasionRate(defender, ActionKind.Attack), precision: 10);
		Assert.Equal(againstSkill, _formulas.EvasionRate(defender, ActionKind.Skill), precision: 10);
	}

	[Fact]
	public void EvasionRate_UsesTheTuningsCap()
	{
		var formulas = new StatFormulas(TestTuning.Values(evasionCap: 30));
		BaseStats slippery = TestTuning.Stats(luck: 400);

		Assert.Equal(30, formulas.EvasionRate(slippery, ActionKind.Attack));
		Assert.Equal(15, formulas.EvasionRate(slippery, ActionKind.Skill));
	}

	[Fact]
	public void EvasionRate_RefusesAKindThatIsNeither()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.EvasionRate(TestTuning.Stats(luck: 10), (ActionKind)7));
		Assert.Throws<ArgumentOutOfRangeException>(() => _formulas.CriticalDamage(TestTuning.Stats(luck: 10), (ActionKind)7));
	}

	// --- Escape rate ---

	private static BaseStats[] Side(params (int Agility, int Luck)[] members)
	{
		var side = new BaseStats[members.Length];

		for (int i = 0; i < members.Length; i++)
			side[i] = TestTuning.Stats(agility: members[i].Agility, luck: members[i].Luck);

		return side;
	}

	[Fact]
	public void EscapeRate_IsTheBase_WhenBothSidesMatch()
	{
		Assert.Equal(50, _formulas.EscapeRate(Side((0, 0)), Side((0, 0))));
		Assert.Equal(50, _formulas.EscapeRate(Side((40, 30)), Side((40, 30))));
	}

	[Theory]
	[InlineData(100, 0, 0, 0, 55)]
	[InlineData(0, 100, 0, 0, 60)]
	[InlineData(0, 0, 100, 0, 45)]
	[InlineData(0, 0, 0, 100, 40)]
	[InlineData(60, 50, 20, 10, 56)]
	[InlineData(7, 0, 0, 0, 50.35)]
	public void EscapeRate_RisesWithTheAllies_AndFallsWithTheEnemies(int allyAgility, int allyLuck, int enemyAgility, int enemyLuck, double expected)
	{
		double rate = _formulas.EscapeRate(Side((allyAgility, allyLuck)), Side((enemyAgility, enemyLuck)));

		Assert.Equal(expected, rate, precision: 10);
	}

	[Fact]
	public void EscapeRate_UsesEachSidesAverage_NotItsSum()
	{
		// Allies average 100 agility and 20 luck; enemies average 40 and 5.
		BaseStats[] allies = Side((200, 0), (100, 30), (0, 30), (100, 20));
		BaseStats[] enemies = Side((20, 10), (60, 0));

		Assert.Equal(50 + 5 + 2 - 2 - 0.5, _formulas.EscapeRate(allies, enemies), precision: 10);
	}

	[Fact]
	public void EscapeRate_DoesNotDependOnTheSizeOfASide()
	{
		double one = _formulas.EscapeRate(Side((60, 20)), Side((30, 10)));
		double four = _formulas.EscapeRate(Side((60, 20), (60, 20), (60, 20), (60, 20)), Side((30, 10), (30, 10), (30, 10)));

		Assert.Equal(one, four, precision: 10);
	}

	[Theory]
	[InlineData(0, 399, 10.1)]
	[InlineData(0, 400, 10)]
	[InlineData(0, 401, 10)]
	[InlineData(0, int.MaxValue, 10)]
	[InlineData(399, 0, 89.9)]
	[InlineData(400, 0, 90)]
	[InlineData(401, 0, 90)]
	[InlineData(int.MaxValue, 0, 90)]
	public void EscapeRate_StaysBetweenItsFloorAndItsCap(int allyLuck, int enemyLuck, double expected)
	{
		Assert.Equal(expected, _formulas.EscapeRate(Side((0, allyLuck)), Side((0, enemyLuck))), precision: 10);
	}

	[Fact]
	public void EscapeRate_UsesTheTuningsBaseFloorAndCap()
	{
		var formulas = new StatFormulas(TestTuning.Values(escapeBase: 40, escapeFloor: 20, escapeCap: 60));

		Assert.Equal(40, formulas.EscapeRate(Side((0, 0)), Side((0, 0))));
		Assert.Equal(20, formulas.EscapeRate(Side((0, 0)), Side((0, 900))));
		Assert.Equal(60, formulas.EscapeRate(Side((0, 900)), Side((0, 0))));
	}

	[Fact]
	public void EscapeRate_RefusesASideWithNobody_NamingIt()
	{
		BaseStats[] someone = Side((10, 10));
		BaseStats[] nobody = Array.Empty<BaseStats>();

		Assert.Equal("allies", Assert.Throws<ArgumentException>(() => _formulas.EscapeRate(nobody, someone)).ParamName);
		Assert.Equal("enemies", Assert.Throws<ArgumentException>(() => _formulas.EscapeRate(someone, nobody)).ParamName);
		Assert.Equal("allies", Assert.Throws<ArgumentNullException>(() => _formulas.EscapeRate(null!, someone)).ParamName);
		Assert.Equal("enemies", Assert.Throws<ArgumentNullException>(() => _formulas.EscapeRate(someone, null!)).ParamName);
	}

	[Fact]
	public void EscapeRate_IgnoresEveryStatButAgilityAndLuck()
	{
		BaseStats[] bare = Side((60, 20));
		BaseStats[] padded = { new(healthPoints: 999, attackDamage: 999, armor: 999, manaPoints: 999, agility: 60, luck: 20, hitRate: 999) };

		Assert.Equal(_formulas.EscapeRate(bare, Side((30, 10))), _formulas.EscapeRate(padded, Side((30, 10))));
		Assert.Equal(_formulas.EscapeRate(Side((30, 10)), bare), _formulas.EscapeRate(Side((30, 10)), padded));
	}

	[Fact]
	public void TheTuning_ReachesTheCurves()
	{
		var formulas = new StatFormulas(TestTuning.Values(
			armorK: new ByAction(300, 50),
			armorReductionCap: new ByAction(60, 80),
			luckK: new ByAction(100, 150),
			criticalChanceCap: new ByAction(95, 40)));

		Assert.Equal(15, formulas.ArmorReduction(TestTuning.Stats(armor: 100), ActionKind.Attack), precision: 10);
		Assert.Equal(10, formulas.CriticalChance(TestTuning.Stats(luck: 50), ActionKind.Skill), precision: 10);
	}
}
