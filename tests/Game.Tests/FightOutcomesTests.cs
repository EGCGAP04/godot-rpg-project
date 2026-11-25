using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Puts every way a fight can end through every place that tells them apart, so a new one
/// cannot be treated as an old one in silence.
/// </summary>
/// <remarks>
/// <para>
/// Each of those places is a <c>switch</c> expression ending in <c>_ => throw</c>, and the
/// compiler does not guard them: a <c>switch</c> statement over an enum warns about nothing,
/// and the warnings of one that is an expression do not stop the build. What guards them
/// is this test, which takes the values from the enums themselves: a value added to
/// <see cref="FightOutcome"/> reaches every place on its own, and one added to
/// <see cref="CombatResult"/> fails <see cref="OneOfEach"/> until a test fight ends that way.
/// </para>
/// <para>
/// <c>Main</c> and the debug overlay are not tested, since they need the engine; they only
/// read answers given here, <see cref="CombatAftermath.DefeatsTheEnemy"/>,
/// <see cref="CombatAftermath.SendsThePlayerBack"/> and <see cref="DecisionState.FightCounters"/>.
/// </para>
/// </remarks>
public class FightOutcomesTests
{
	private static readonly FightOutcome[] Outcomes = Enum.GetValues<FightOutcome>();

	private static readonly CombatResult[] Finished = Enum.GetValues<CombatResult>().Where(result => result != CombatResult.Ongoing).ToArray();

	private static PartyState HeroAndAbe()
	{
		PartyState party = TestRoster.Party("abe");
		party.Recruit("abe");
		return party;
	}

	/// <summary>A fight that ended in each way a fight can end.</summary>
	private static Dictionary<CombatResult, CombatAftermath> OneOfEach()
	{
		var fights = new Dictionary<CombatResult, CombatAftermath>
		{
			[CombatResult.Won] = TestFights.AWinWhereTheLastGoesDown(HeroAndAbe()),
			[CombatResult.Lost] = TestFights.ALoss(TestRoster.Party()),
			[CombatResult.Fled] = TestFights.AFlight(HeroAndAbe(), lastGoesDownFirst: false)
		};

		// When this fails, a fight can end in a way no test fight here ends in: add one, and
		// every test below then asks what that ending means.
		Assert.Equal(Finished, fights.Keys.Order());
		return fights;
	}

	// --- The history ---

	[Fact]
	public void EveryEnding_IsAnOutcomeOfItsOwn_AndEveryOutcomeIsOne()
	{
		Dictionary<CombatResult, FightOutcome> outcomes = OneOfEach().ToDictionary(fight => fight.Key, fight => fight.Value.Outcome);

		Assert.Equal(
			new Dictionary<CombatResult, FightOutcome>
			{
				[CombatResult.Won] = FightOutcome.Won,
				[CombatResult.Lost] = FightOutcome.Lost,
				[CombatResult.Fled] = FightOutcome.Fled
			},
			outcomes);
		Assert.Equal(Outcomes, outcomes.Values.Order());
	}

	[Fact]
	public void EveryOutcome_HasACounterOfItsOwn_DeclaredInTheGamesCatalogue()
	{
		FlagCatalogue catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));
		string[] counters = Outcomes.Select(DecisionState.CounterFor).ToArray();

		Assert.Equal(counters.Length, counters.Distinct().Count());
		Assert.Equal(counters, DecisionState.FightCounters);
		Assert.All(counters, name => Assert.Equal(FlagType.Counter, catalogue.Flags[name].Type));
		Assert.Equal(new[] { DecisionState.Wins, DecisionState.Losses, DecisionState.Escapes }, counters);
	}

	[Fact]
	public void RecordingAFight_AddsOneToItsOutcomesCounter_AndToNoOther()
	{
		FlagCatalogue catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));

		foreach (FightOutcome outcome in Outcomes)
		{
			var state = new DecisionState(catalogue);
			state.RecordCombat(World.Fantasy, 1, TestFights.Group("weak"), outcome);

			Assert.All(DecisionState.FightCounters, name => Assert.Equal(name == DecisionState.CounterFor(outcome) ? 1 : 0, state.GetCounter(name)));
			Assert.Equal(outcome, state.Combats.Single().Outcome);
		}
	}

	[Fact]
	public void EveryOutcome_ReadsAsAWordOfItsOwn()
	{
		string[] words = Outcomes.Select(outcome => TestFights.Record(World.Fantasy, 1, "weak", outcome).ToString().Split(", ").Last()).ToArray();

		Assert.Equal(new[] { "won", "lost", "fled" }, words);
	}

	[Fact]
	public void AnOutcomeThatDoesNotExist_HasNoCounter()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => DecisionState.CounterFor((FightOutcome)99));
	}

	// --- The map and the screen ---

	[Fact]
	public void EveryEnding_SaysWhatBecomesOfTheEnemyAndThePlayer()
	{
		Dictionary<CombatResult, (bool, bool)> map = OneOfEach().ToDictionary(fight => fight.Key, fight => (fight.Value.DefeatsTheEnemy, fight.Value.SendsThePlayerBack));

		Assert.Equal(
			new Dictionary<CombatResult, (bool, bool)>
			{
				[CombatResult.Won] = (true, false),
				[CombatResult.Lost] = (false, true),
				[CombatResult.Fled] = (false, false)
			},
			map);
	}

	[Fact]
	public void EveryEnding_HasAResultLineOfItsOwn()
	{
		static string Translate(string key) => key switch
		{
			CombatResultPages.ExperienceGained => "{experience}",
			CombatResultPages.LevelReached => "{name} {level}",
			CombatResultPages.WasDown => "{name}",
			_ => key
		};

		Dictionary<CombatResult, string> first = OneOfEach().ToDictionary(
			fight => fight.Key,
			fight => CombatResultPages.Lines(fight.Value, fight.Value.PreviewAward(HeroAndAbe()), HeroAndAbe().Roster, Translate)[0]);

		Assert.Equal(
			new Dictionary<CombatResult, string>
			{
				[CombatResult.Won] = CombatResultPages.Won,
				[CombatResult.Lost] = CombatResultPages.Lost,
				[CombatResult.Fled] = CombatResultPages.Fled
			},
			first);
	}
}
