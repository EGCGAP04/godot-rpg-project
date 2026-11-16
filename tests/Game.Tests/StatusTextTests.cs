using System.Collections.Generic;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatusText"/>: which lines the status screen shows in each world.
/// That the game's tables have the text for its keys is <see cref="GameKeysTests"/>'s.
/// </summary>
public class StatusTextTests
{
	private const string Hero = TestRoster.Hero;

	/// <summary>A translation short enough to read which key and which values were used.</summary>
	private static readonly Dictionary<string, string> Texts = new()
	{
		[StatusText.LevelKey] = "Lv {level}",
		[StatusText.AgeKey] = "Age {age}",
		[StatusText.ExperienceKey] = "{experience} of {to_next}",
		[StatusText.HighestLevelKey] = "TOP",
		[StatusText.PointsKey] = "{stat}={current}/{max}",
		[StatusText.ValueKey] = "{stat}={value}",
		[StatusText.HealthPointsKey] = "HP",
		[StatusText.ManaPointsKey] = "MP",
		[StatusText.AttackDamageKey] = "ATK",
		[StatusText.ArmorKey] = "ARM",
		[StatusText.AgilityKey] = "AGI",
		[StatusText.LuckKey] = "LCK",
		[StatusText.HitRateKey] = "HIT",
		["speaker.hero"] = "Hero"
	};

	private static string Translate(string key) => Texts[key];

	[Fact]
	public void Name_IsTheCharactersName()
	{
		Assert.Equal("Hero", StatusText.Name(StatusSheet.For(TestRoster.Party(), Hero, World.Fantasy), Translate));
	}

	[Fact]
	public void Level_SaysTheLevel_AndTheExperienceTowardsTheNext()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 14);
		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		Assert.Equal("Lv 2", StatusText.Level(sheet, Translate));
		Assert.Equal("4 of 20", StatusText.Experience(sheet, Translate));
	}

	[Fact]
	public void Experience_AtTheHighestLevel_SaysSo()
	{
		PartyState party = TestRoster.Party();
		party.Award(World.Fantasy, 1000);
		StatusSheet sheet = StatusSheet.For(party, Hero, World.Fantasy);

		Assert.Equal("Lv 5", StatusText.Level(sheet, Translate));
		Assert.Equal("TOP", StatusText.Experience(sheet, Translate));
	}

	[Fact]
	public void InTheRealWorld_TheLevelIsAnAge_AndNothingIsSaidOfExperience()
	{
		StatusSheet sheet = StatusSheet.For(TestRoster.Party(), Hero, World.Real);

		Assert.Equal("Age 12", StatusText.Level(sheet, Translate));
		Assert.Equal("", StatusText.Experience(sheet, Translate));
	}

	[Fact]
	public void Points_AreHealthThenMana_EachWithWhatIsLeftAndTheMost()
	{
		PartyState party = TestRoster.Party();
		party.SetHealthPoints(Hero, World.Fantasy, 7);
		party.SetManaPoints(Hero, World.Fantasy, 3);

		Assert.Equal(new[] { "HP=7/20", "MP=3/10" }, StatusText.Points(StatusSheet.For(party, Hero, World.Fantasy), Translate));
	}

	[Fact]
	public void Stats_AreTheOtherFive_EachUnderItsOwnName()
	{
		var sheet = new StatusSheet("speaker.hero", World.Fantasy, Level: 1, LevelIsAge: false, Experience: 0, ToNextLevel: 10, HealthPoints: 1, ManaPoints: 2,
			Stats: new BaseStats(healthPoints: 10, attackDamage: 11, armor: 12, manaPoints: 13, agility: 14, luck: 15, hitRate: 16));

		Assert.Equal(new[] { "ATK=11", "ARM=12", "AGI=14", "LCK=15", "HIT=16" }, StatusText.Stats(sheet, Translate));
	}
}
