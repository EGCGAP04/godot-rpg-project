using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Who goes into a fight, and as what: the party's members with the health and mana they
/// have left, and a group's enemies at full. It also remembers which character stands in
/// each of the party's positions, so what the fight did can be written back to them.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free, so the combat screen only has to hand its setups to
/// <see cref="CombatRunner.Start"/>, and <see cref="CombatAftermath"/> reads it back when
/// the fight is over.
/// </para>
/// <para>
/// It is a picture of the party when the fight starts, and it changes nothing in it. The
/// party is written once, at the end, through <see cref="CombatAftermath.ApplyTo"/>.
/// </para>
/// </remarks>
public sealed class CombatLineup
{
	private CombatLineup(World world, EncounterValues encounter, IReadOnlyList<string> memberIds, IReadOnlyList<CombatantSetup> party, IReadOnlyList<CombatantSetup> enemies)
	{
		World = world;
		Encounter = encounter;
		MemberIds = memberIds;
		Party = party;
		Enemies = enemies;
	}

	/// <summary>The world the fight is in, whose stats and progress the members bring.</summary>
	public World World { get; }

	/// <summary>The group being fought.</summary>
	public EncounterValues Encounter { get; }

	/// <summary>The id of the character in each of the party's positions, in the party's order.</summary>
	public IReadOnlyList<string> MemberIds { get; }

	/// <summary>The members, in the party's order, with their stats in the world and the health and mana they have left.</summary>
	public IReadOnlyList<CombatantSetup> Party { get; }

	/// <summary>The group's enemies, in its order, at full.</summary>
	public IReadOnlyList<CombatantSetup> Enemies { get; }

	/// <summary>Lines up the party as it is now, in a world, against a group.</summary>
	/// <remarks>
	/// A member with no health left comes in already down. The runner refuses a fight
	/// where nobody in the party is standing; this does not check it twice.
	/// </remarks>
	public static CombatLineup For(PartyState party, World world, EncounterValues encounter)
	{
		ArgumentNullException.ThrowIfNull(party);
		ArgumentNullException.ThrowIfNull(encounter);

		if (!Enum.IsDefined(world))
			throw new ArgumentOutOfRangeException(nameof(world), world, "There is no such world.");

		string[] memberIds = party.Members.ToArray();

		CombatantSetup[] members = memberIds
			.Select(id =>
			{
				CharacterProgress progress = party.ProgressOf(id, world);
				return new CombatantSetup(party.Roster.Get(id).NameKey, party.StatsOf(id, world), progress.HealthPoints, progress.ManaPoints);
			})
			.ToArray();

		CombatantSetup[] enemies = encounter.Enemies
			.Select(enemy => CombatantSetup.AtFull(enemy.NameKey, enemy.Stats))
			.ToArray();

		return new CombatLineup(world, encounter, Array.AsReadOnly(memberIds), Array.AsReadOnly(members), Array.AsReadOnly(enemies));
	}

	/// <summary>The id of the character in a position of the party.</summary>
	/// <exception cref="ArgumentException">The combatant is an enemy, or its position is not in the lineup.</exception>
	public string MemberAt(CombatantId id)
	{
		if (id.Side != CombatSide.Party || id.Position >= MemberIds.Count)
			throw new ArgumentException($"{id} is not a member of this lineup, which has {MemberIds.Count}.", nameof(id));

		return MemberIds[id.Position];
	}
}
