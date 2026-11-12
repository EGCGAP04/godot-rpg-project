using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The party: who is in it, in order, and where every character stands in each of the
/// three worlds. Deliberately a plain C# class with no Godot
/// dependency, so its rules are unit-tested without the engine; the <c>Party</c> autoload
/// holds one and adds the signals.
/// </summary>
/// <remarks>
/// <para>
/// The rules of membership: the protagonist is always in the party and cannot leave, the
/// party holds <see cref="MaximumSize"/> at most, and nobody is in it twice. Recruiting is
/// optional, so a party of one is a valid party.
/// </para>
/// <para>
/// A change that breaks a rule is refused with an exception and changes nothing. It is
/// never ignored in silence: a conversation that recruits into a full party is a mistake
/// in the game's content, and should be seen.
/// </para>
/// <para>
/// Progress is kept for every character of the roster, in the party or not, from the
/// start: a level, the experience towards the next one, and the health points and mana
/// points left, per world. So a character who leaves keeps their progress without any
/// code for it, since leaving only takes them off the list of members.
/// </para>
/// <para>
/// Experience goes to every member, whole: it is not split, because splitting would
/// punish recruiting. Awarding it cannot fail half way. The curves are checked when they
/// are built, and the constructor here checks that every character's stats can be
/// computed up to each world's highest level.
/// </para>
/// </remarks>
public sealed class PartyState
{
	/// <summary>How many characters the party holds at most, the protagonist included.</summary>
	public const int MaximumSize = 4;

	private readonly List<string> _members = new();
	private readonly Dictionary<(string Id, World World), CharacterProgress> _progress = new();

	/// <exception cref="ArgumentException">A character's stats cannot be computed at some world's highest level.</exception>
	public PartyState(CharacterRosterValues roster, ExperienceCurves curves)
	{
		Roster = roster ?? throw new ArgumentNullException(nameof(roster));
		Curves = curves ?? throw new ArgumentNullException(nameof(curves));

		foreach (CharacterValues character in roster.Characters)
		{
			foreach (World world in Enum.GetValues<World>())
			{
				CheckHighestLevel(character, world);

				int level = character.StartingLevel(world);
				BaseStats stats = character.StatsAt(world, level);
				_progress[(character.Id, world)] = new CharacterProgress(new LevelProgress(level, 0), stats.HealthPoints, stats.ManaPoints);
			}
		}

		_members.Add(roster.Protagonist.Id);
		Members = _members.AsReadOnly();
	}

	/// <summary>Raised after the members change: someone joined or someone left.</summary>
	public event Action MembersChanged;

	/// <summary>
	/// Raised after a character's progress in a world changes, with their id and the
	/// world. Only on a real change, and once everything the call changes has its value.
	/// </summary>
	public event Action<string, World> ProgressChanged;

	/// <summary>The experience curve of each world that has one.</summary>
	public ExperienceCurves Curves { get; }

	/// <summary>Every character there is, in the party or not.</summary>
	public CharacterRosterValues Roster { get; }

	/// <summary>
	/// The ids of the characters in the party, in the order they joined. The protagonist
	/// is always the first.
	/// </summary>
	public IReadOnlyList<string> Members { get; }

	/// <summary>Whether the party has no room for one more.</summary>
	public bool IsFull => _members.Count >= MaximumSize;

	/// <summary>Whether this character is in the party now.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public bool IsMember(string id) => _members.Contains(Roster.Get(id).Id);

	/// <summary>Adds a character to the party, last.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	/// <exception cref="InvalidOperationException">The character is already in the party, or the party is full.</exception>
	public void Recruit(string id)
	{
		if (IsMember(id))
			throw new InvalidOperationException($"'{id}' is already in the party.");

		if (IsFull)
			throw new InvalidOperationException($"'{id}' cannot join: the party already holds {MaximumSize}.");

		_members.Add(id);
		MembersChanged?.Invoke();
	}

	/// <summary>Takes a character out of the party. Those after them move up.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	/// <exception cref="InvalidOperationException">The character is the protagonist, or is not in the party.</exception>
	public void Dismiss(string id)
	{
		if (Roster.Get(id).Id == Roster.Protagonist.Id)
			throw new InvalidOperationException($"'{id}' is the protagonist, who never leaves the party.");

		if (!_members.Remove(id))
			throw new InvalidOperationException($"'{id}' is not in the party.");

		MembersChanged?.Invoke();
	}

	/// <summary>Where a character stands in a world, whether they are in the party or not.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public CharacterProgress ProgressOf(string id, World world) => _progress[(Roster.Get(id).Id, world)];

	/// <summary>A character's seven base stats in a world, at the level they have there now.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public BaseStats StatsOf(string id, World world) => Roster.Get(id).StatsAt(world, ProgressOf(id, world).Level.Level);

	/// <summary>
	/// Sets the health points a character has left in a world, kept between 0 and what
	/// their level gives, so callers never have to clamp.
	/// </summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public void SetHealthPoints(string id, World world, int value)
	{
		CharacterProgress progress = ProgressOf(id, world);
		Change(id, world, progress with { HealthPoints = Math.Clamp(value, 0, StatsOf(id, world).HealthPoints) });
	}

	/// <summary>
	/// Sets the mana points a character has left in a world, kept between 0 and what
	/// their level gives.
	/// </summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public void SetManaPoints(string id, World world, int value)
	{
		CharacterProgress progress = ProgressOf(id, world);
		Change(id, world, progress with { ManaPoints = Math.Clamp(value, 0, StatsOf(id, world).ManaPoints) });
	}

	/// <summary>Gives a character all their health points and mana points back in a world.</summary>
	/// <exception cref="ArgumentException">The roster has no character with this id.</exception>
	public void FullHeal(string id, World world)
	{
		BaseStats stats = StatsOf(id, world);
		Change(id, world, ProgressOf(id, world) with { HealthPoints = stats.HealthPoints, ManaPoints = stats.ManaPoints });
	}

	/// <summary>
	/// What awarding this experience in a world would do to each member, without doing it.
	/// In the Real world, which has no experience, nobody changes.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The experience is negative.</exception>
	public ExperienceAward PreviewAward(World world, long experience)
	{
		if (experience < 0)
			throw new ArgumentOutOfRangeException(nameof(experience), experience, "An award of experience cannot be negative.");

		var gains = new List<MemberGain>();

		foreach (string id in _members)
		{
			LevelProgress before = _progress[(id, world)].Level;
			gains.Add(new MemberGain(id, before, Curves.Add(world, before, experience)));
		}

		return new ExperienceAward(world, Curves.For(world) is null ? 0 : experience, gains.AsReadOnly());
	}

	/// <summary>
	/// Gives every member of the party this experience in a world, whole, and says what it
	/// did. A level reached raises what the health points and mana points can be and
	/// leaves the ones the character has as they are: levelling up does not heal.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The experience is negative.</exception>
	public ExperienceAward Award(World world, long experience)
	{
		ExperienceAward award = PreviewAward(world, experience);
		List<MemberGain> changed = award.Gains.Where(gain => gain.After != gain.Before).ToList();

		foreach (MemberGain gain in changed)
			_progress[(gain.Id, world)] = _progress[(gain.Id, world)] with { Level = gain.After };

		foreach (MemberGain gain in changed)
			ProgressChanged?.Invoke(gain.Id, world);

		return award;
	}

	private void Change(string id, World world, CharacterProgress next)
	{
		if (_progress[(id, world)] == next)
			return;

		_progress[(id, world)] = next;
		ProgressChanged?.Invoke(id, world);
	}

	/// <summary>The highest level a character can have in a world: the curve's, or their age in the Real one.</summary>
	private int HighestLevel(CharacterValues character, World world) =>
		Curves.For(world)?.HighestLevel ?? character.StartingLevel(world);

	// Stats only grow with the level, so if they can be computed at the highest one they
	// can be computed at every level below it.
	private void CheckHighestLevel(CharacterValues character, World world)
	{
		int highest = HighestLevel(character, world);

		try
		{
			character.StatsAt(world, highest);
		}
		catch (OverflowException exception)
		{
			throw new ArgumentException($"The character '{character.Id}' cannot reach level {highest} in the {world} world: {exception.Message}", nameof(Roster), exception);
		}
	}
}
