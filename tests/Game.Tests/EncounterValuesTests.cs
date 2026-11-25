using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="EncounterValues"/>: a group keeps its enemies in order, adds up
/// their experience, and has from one enemy to four.
/// </summary>
public class EncounterValuesTests
{
	private static EnemyValues Enemy(string id, long experience = 5) =>
		new(id, EnemyValues.NameKeyOf(id), TestTuning.Stats(healthPoints: 10), experience);

	[Fact]
	public void Constructor_KeepsTheIdAndTheEnemiesInTheirOrder()
	{
		var encounter = new EncounterValues("demo_patrol", new[] { Enemy("zed"), Enemy("abe"), Enemy("moe") });

		Assert.Equal("demo_patrol", encounter.Id);
		Assert.Equal(new[] { "zed", "abe", "moe" }, encounter.Enemies.Select(enemy => enemy.Id));
	}

	[Fact]
	public void AGroup_CanBeFled_UnlessItSaysOtherwise()
	{
		Assert.False(new EncounterValues("demo_pair", new[] { Enemy("weak") }).CannotFlee);
		Assert.True(new EncounterValues("demo_pair", new[] { Enemy("weak") }, cannotFlee: true).CannotFlee);
	}

	[Fact]
	public void TheSameEnemy_MayBeInAGroupMoreThanOnce()
	{
		EnemyValues weak = Enemy("weak");

		var encounter = new EncounterValues("demo_pair", new[] { weak, weak });

		Assert.Equal(new[] { "weak", "weak" }, encounter.Enemies.Select(enemy => enemy.Id));
	}

	[Fact]
	public void Experience_IsTheSumOfTheEnemies()
	{
		var encounter = new EncounterValues("demo_patrol", new[] { Enemy("a", 5), Enemy("b", 20), Enemy("b", 20) });

		Assert.Equal(45, encounter.Experience);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public void AGroup_HasFromOneEnemyToFour(int size)
	{
		var encounter = new EncounterValues("demo_group", Enumerable.Repeat(Enemy("weak"), size));

		Assert.Equal(size, encounter.Enemies.Count);
		Assert.Equal(4, EncounterValues.MaximumSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	public void AGroup_OfNoEnemiesOrOfTooMany_IsRefused(int size)
	{
		var thrown = Assert.Throws<ArgumentException>(() => new EncounterValues("demo_group", Enumerable.Repeat(Enemy("weak"), size)));

		Assert.Equal("enemies", thrown.ParamName);
	}

	[Fact]
	public void AnEmptyEntry_IsRefused()
	{
		Assert.Throws<ArgumentException>(() => new EncounterValues("demo_group", new[] { Enemy("weak"), null }));
	}

	[Fact]
	public void MissingEnemies_AreRefused()
	{
		Assert.Throws<ArgumentNullException>(() => new EncounterValues("demo_group", null));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Demo")]
	[InlineData("demo patrol")]
	[InlineData("demo\n")]
	public void AnId_ThatIsNotALowercaseWord_IsRefused(string id)
	{
		var thrown = Assert.Throws<ArgumentException>(() => new EncounterValues(id, new[] { Enemy("weak") }));

		Assert.Equal("id", thrown.ParamName);
	}

	[Fact]
	public void ExperienceThatDoesNotFit_IsRefused_InsteadOfWrappingAround()
	{
		Assert.Throws<ArgumentException>(() => new EncounterValues("demo_group", new[] { Enemy("a", long.MaxValue), Enemy("b", 1) }));
	}

	[Fact]
	public void TheEnemies_CannotBeChangedFromOutside()
	{
		EnemyValues[] given = { Enemy("weak"), Enemy("strong") };
		var encounter = new EncounterValues("demo_group", given);

		given[0] = Enemy("other");

		Assert.Equal("weak", encounter.Enemies[0].Id);
	}
}
