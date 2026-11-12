using System;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="StatTuningValues"/> and <see cref="ByAction"/>: the values are
/// kept as given, and a tuning that would make a formula answer nonsense is refused when
/// it is built.
/// </summary>
public class StatTuningValuesTests
{
	[Fact]
	public void Constructor_KeepsEachValueInItsOwnProperty()
	{
		var tuning = new StatTuningValues(
			armorK: new ByAction(1, 2),
			armorReductionCap: new ByAction(3, 4),
			luckK: new ByAction(5, 6),
			criticalChanceCap: new ByAction(7, 8),
			hitChanceFloor: 9,
			criticalDamageBase: 10,
			criticalDamageCap: new ByAction(11, 12),
			evasionCap: 13,
			escapeBase: 14,
			escapeFloor: 15,
			escapeCap: 16);

		Assert.Equal(new ByAction(1, 2), tuning.ArmorK);
		Assert.Equal(new ByAction(3, 4), tuning.ArmorReductionCap);
		Assert.Equal(new ByAction(5, 6), tuning.LuckK);
		Assert.Equal(new ByAction(7, 8), tuning.CriticalChanceCap);
		Assert.Equal(9, tuning.HitChanceFloor);
		Assert.Equal(10, tuning.CriticalDamageBase);
		Assert.Equal(new ByAction(11, 12), tuning.CriticalDamageCap);
		Assert.Equal(13, tuning.EvasionCap);
		Assert.Equal(14, tuning.EscapeBase);
		Assert.Equal(15, tuning.EscapeFloor);
		Assert.Equal(16, tuning.EscapeCap);
	}

	[Fact]
	public void TheDesignsNumbers_AreAccepted()
	{
		Assert.NotNull(TestTuning.Values());
	}

	[Fact]
	public void ByAction_AnswersForEachKind_AndRefusesAnyOther()
	{
		var value = new ByAction(Attack: 90, Skill: 80);

		Assert.Equal(90, value.For(ActionKind.Attack));
		Assert.Equal(80, value.For(ActionKind.Skill));
		Assert.Throws<ArgumentOutOfRangeException>(() => value.For((ActionKind)2));
	}

	public static TheoryData<string, Func<StatTuningValues>> WrongTunings()
	{
		var data = new TheoryData<string, Func<StatTuningValues>>();

		foreach (double wrong in new[] { 0, -1, double.NaN, double.PositiveInfinity })
		{
			data.Add("armorK", () => TestTuning.Values(armorK: new ByAction(wrong, 50)));
			data.Add("armorK", () => TestTuning.Values(armorK: new ByAction(100, wrong)));
			data.Add("armorReductionCap", () => TestTuning.Values(armorReductionCap: new ByAction(wrong, 80)));
			data.Add("armorReductionCap", () => TestTuning.Values(armorReductionCap: new ByAction(90, wrong)));
			data.Add("luckK", () => TestTuning.Values(luckK: new ByAction(wrong, 50)));
			data.Add("luckK", () => TestTuning.Values(luckK: new ByAction(100, wrong)));
			data.Add("criticalChanceCap", () => TestTuning.Values(criticalChanceCap: new ByAction(wrong, 90)));
			data.Add("criticalChanceCap", () => TestTuning.Values(criticalChanceCap: new ByAction(95, wrong)));
			data.Add("hitChanceFloor", () => TestTuning.Values(hitChanceFloor: wrong));
			data.Add("criticalDamageBase", () => TestTuning.Values(criticalDamageBase: wrong));
			data.Add("criticalDamageCap", () => TestTuning.Values(criticalDamageCap: new ByAction(wrong, 300)));
			data.Add("criticalDamageCap", () => TestTuning.Values(criticalDamageCap: new ByAction(500, wrong)));
			data.Add("evasionCap", () => TestTuning.Values(evasionCap: wrong));
			data.Add("escapeBase", () => TestTuning.Values(escapeBase: wrong));
			data.Add("escapeFloor", () => TestTuning.Values(escapeFloor: wrong));
			data.Add("escapeCap", () => TestTuning.Values(escapeCap: wrong));
		}

		// A percentage above 100.
		data.Add("armorReductionCap", () => TestTuning.Values(armorReductionCap: new ByAction(100.5, 80)));
		data.Add("criticalChanceCap", () => TestTuning.Values(criticalChanceCap: new ByAction(95, 101)));
		data.Add("hitChanceFloor", () => TestTuning.Values(hitChanceFloor: 101));
		data.Add("evasionCap", () => TestTuning.Values(evasionCap: 101));
		data.Add("escapeBase", () => TestTuning.Values(escapeBase: 101));
		data.Add("escapeCap", () => TestTuning.Values(escapeCap: 101));

		// Values that are each valid alone and wrong together.
		data.Add("criticalDamageCap", () => TestTuning.Values(criticalDamageBase: 150, criticalDamageCap: new ByAction(149, 300)));
		data.Add("criticalDamageCap", () => TestTuning.Values(criticalDamageBase: 150, criticalDamageCap: new ByAction(500, 149)));
		data.Add("escapeFloor", () => TestTuning.Values(escapeFloor: 91, escapeCap: 90));

		return data;
	}

	[Theory]
	[MemberData(nameof(WrongTunings))]
	public void AWrongValue_IsRefused_NamingIt(string name, Func<StatTuningValues> build)
	{
		var thrown = Assert.Throws<ArgumentOutOfRangeException>(build);

		Assert.Equal(name, thrown.ParamName);
	}

	[Fact]
	public void LimitsThatMeet_AreAccepted()
	{
		// A cap equal to the base, a floor equal to its cap, and a percentage of exactly 100.
		Assert.NotNull(TestTuning.Values(criticalDamageBase: 150, criticalDamageCap: new ByAction(150, 150)));
		Assert.NotNull(TestTuning.Values(escapeFloor: 90, escapeCap: 90));
		Assert.NotNull(TestTuning.Values(hitChanceFloor: 100, evasionCap: 100, escapeCap: 100));
	}
}
