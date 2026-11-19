using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="EnemyValues"/>: the values are kept as given, and an enemy a
/// fight could not use is refused when it is built.
/// </summary>
public class EnemyValuesTests
{
	private static readonly BaseStats Stats = new(healthPoints: 12, attackDamage: 3, armor: 4, manaPoints: 5, agility: 6, luck: 7, hitRate: 80);

	[Fact]
	public void Constructor_KeepsEachValueInItsOwnProperty()
	{
		var enemy = new EnemyValues("demo_slime", "enemy.demo_slime", Stats, experience: 9);

		Assert.Equal("demo_slime", enemy.Id);
		Assert.Equal("enemy.demo_slime", enemy.NameKey);
		Assert.Equal(Stats, enemy.Stats);
		Assert.Equal(9, enemy.Experience);
	}

	[Fact]
	public void NameKeyOf_IsTheIdInTheEnemyTable()
	{
		Assert.Equal("enemy.weak", EnemyValues.NameKeyOf("weak"));
	}

	[Fact]
	public void AnEnemy_MayHaveOnlyHealthAndExperience()
	{
		var enemy = new EnemyValues("weak", "enemy.weak", TestTuning.Stats(healthPoints: 1), experience: 1);

		Assert.Equal(0, enemy.Stats.AttackDamage);
		Assert.Equal(0, enemy.Stats.HitRate);
	}

	[Fact]
	public void Experience_IsIn64Bits()
	{
		var enemy = new EnemyValues("weak", "enemy.weak", Stats, long.MaxValue);

		Assert.Equal(long.MaxValue, enemy.Experience);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Weak")]
	[InlineData("1weak")]
	[InlineData("weak enemy")]
	[InlineData("weak-enemy")]
	[InlineData("weak\n")]
	public void AnId_ThatIsNotALowercaseWord_IsRefused(string id)
	{
		var thrown = Assert.Throws<ArgumentException>(() => new EnemyValues(id, $"enemy.{id}", Stats, 9));

		Assert.Equal("id", thrown.ParamName);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("weak")]
	[InlineData("enemy.strong")]
	[InlineData("speaker.weak")]
	public void ANameKey_ThatIsNotTheIdInTheEnemyTable_IsRefused(string nameKey)
	{
		var thrown = Assert.Throws<ArgumentException>(() => new EnemyValues("weak", nameKey, Stats, 9));

		Assert.Equal("nameKey", thrown.ParamName);
	}

	[Fact]
	public void AnEnemyWithNoHealth_IsRefused()
	{
		var thrown = Assert.Throws<ArgumentException>(() => new EnemyValues("weak", "enemy.weak", TestTuning.Stats(attackDamage: 5), 9));

		Assert.Equal("stats", thrown.ParamName);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void AnEnemyThatGivesNoExperience_IsRefused(long experience)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyValues("weak", "enemy.weak", Stats, experience));

		Assert.Equal("experience", thrown.ParamName);
	}
}
