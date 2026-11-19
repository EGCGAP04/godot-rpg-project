using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What one member takes out of a fight: the health and mana they leave with, and whether
/// they went down.
/// </summary>
/// <param name="Id">The character's id.</param>
/// <param name="HealthPoints">What they leave with: what they had left, or 1 if they went down.</param>
/// <param name="ManaPoints">What they leave with: what they had left, or 1 if they went down, 0 where their most is 0.</param>
/// <param name="WentDown">Whether they were down when the fight ended. Such a member gets no experience from it.</param>
public sealed record MemberAftermath(string Id, int HealthPoints, int ManaPoints, bool WentDown);

/// <summary>
/// What a finished fight leaves behind, for the party: each member's health and mana, who
/// is still standing, how it ended, and the experience it gives.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free, built from a <see cref="CombatLineup"/> and the <see cref="CombatRunner"/>
/// that fought it out, so the rules of what a fight leaves are tested without the screen.
/// </para>
/// <para>
/// <b>A member who went down leaves with 1 health point and 1 mana point,</b> or 0 mana
/// where their most is 0, whatever they had before: going down drains the mana too. They
/// get none of the experience. In a loss the whole party is down, so everyone leaves that
/// way, and there is no experience at all.
/// </para>
/// <para>
/// The party is written in one place, <see cref="ApplyTo"/>, once the fight is over; what
/// the screen shows of it in the meantime comes from the runner.
/// </para>
/// </remarks>
public sealed class CombatAftermath
{
	private CombatAftermath(CombatLineup lineup, CombatResult result, IReadOnlyList<MemberAftermath> members)
	{
		Lineup = lineup;
		Result = result;
		Members = members;
		Standing = Array.AsReadOnly(members.Where(member => !member.WentDown).Select(member => member.Id).ToArray());
	}

	/// <summary>Who went into the fight, and against which group.</summary>
	public CombatLineup Lineup { get; }

	/// <summary>The world the fight was in.</summary>
	public World World => Lineup.World;

	/// <summary>How the fight ended: <see cref="CombatResult.Won"/> or <see cref="CombatResult.Lost"/>, never <see cref="CombatResult.Ongoing"/>.</summary>
	public CombatResult Result { get; }

	public bool Won => Result == CombatResult.Won;

	/// <summary>One entry per member, in the party's order.</summary>
	public IReadOnlyList<MemberAftermath> Members { get; }

	/// <summary>The ids of the members who did not go down, in the party's order. Empty after a loss.</summary>
	public IReadOnlyList<string> Standing { get; }

	/// <summary>The experience the fight gives each member standing: the group's, after a win, and 0 after a loss.</summary>
	public long Experience => Won ? Lineup.Encounter.Experience : 0;

	/// <summary>Reads what a finished fight left.</summary>
	/// <exception cref="InvalidOperationException">The fight is not over.</exception>
	/// <exception cref="ArgumentException">The runner is not fighting this lineup: its sides are not the lineup's.</exception>
	public static CombatAftermath Of(CombatLineup lineup, CombatRunner runner)
	{
		ArgumentNullException.ThrowIfNull(lineup);
		ArgumentNullException.ThrowIfNull(runner);

		if (runner.Step != CombatStep.Finished)
			throw new InvalidOperationException($"A fight leaves something behind once it is over, and this one is at {runner.Step}.");

		CheckSide(runner.Party, lineup.Party, "party");
		CheckSide(runner.Enemies, lineup.Enemies, "enemies");

		var members = new List<MemberAftermath>();

		for (int position = 0; position < runner.Party.Count; position++)
		{
			Combatant member = runner.Party[position];
			string id = lineup.MemberIds[position];

			members.Add(member.IsDown
				? new MemberAftermath(id, 1, Math.Min(1, member.Stats.ManaPoints), WentDown: true)
				: new MemberAftermath(id, member.HealthPoints, member.ManaPoints, WentDown: false));
		}

		return new CombatAftermath(lineup, runner.Result, members.AsReadOnly());
	}

	/// <summary>
	/// What <see cref="ApplyTo"/> will award, without changing anything: for the screen to
	/// announce before the party receives it.
	/// </summary>
	public ExperienceAward PreviewAward(PartyState party)
	{
		ArgumentNullException.ThrowIfNull(party);
		return party.PreviewAward(World, Experience, Recipients(party));
	}

	/// <summary>
	/// Writes the fight into the party: each member's health and mana first, then the
	/// experience for those standing, and says what the award did.
	/// </summary>
	/// <remarks>
	/// The health and mana go first because a level reached does not heal. The party is
	/// not supposed to change while a fight is on, but the debug overlay can open over one
	/// and dismiss a member: such a member still has their health written, and is left out
	/// of the award instead of making it fail.
	/// </remarks>
	public ExperienceAward ApplyTo(PartyState party)
	{
		ArgumentNullException.ThrowIfNull(party);

		foreach (MemberAftermath member in Members)
		{
			party.SetHealthPoints(member.Id, World, member.HealthPoints);
			party.SetManaPoints(member.Id, World, member.ManaPoints);
		}

		return party.Award(World, Experience, Recipients(party));
	}

	private IEnumerable<string> Recipients(PartyState party) => Standing.Where(party.IsMember).ToArray();

	private static void CheckSide(IReadOnlyList<Combatant> fought, IReadOnlyList<CombatantSetup> lined, string side)
	{
		if (fought.Count != lined.Count || fought.Where((combatant, position) => combatant.NameKey != lined[position].NameKey).Any())
			throw new ArgumentException($"The runner's {side} are not the lineup's.", "runner");
	}
}
