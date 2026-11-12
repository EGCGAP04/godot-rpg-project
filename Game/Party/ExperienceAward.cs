using System.Collections.Generic;

/// <summary>What an award of experience does to one member of the party.</summary>
/// <param name="Id">The character's id.</param>
/// <param name="Before">Where they stood before the award.</param>
/// <param name="After">Where they stand with it.</param>
public sealed record MemberGain(string Id, LevelProgress Before, LevelProgress After)
{
	/// <summary>Whether the award takes the character to a higher level.</summary>
	public bool ReachesALevel => After.Level > Before.Level;
}

/// <summary>
/// What an award of experience does to the party: computed by
/// <see cref="PartyState.PreviewAward"/> without changing anything, and returned by
/// <see cref="PartyState.Award"/> once applied. The two are the same computation, so what
/// a screen announces and what the party receives cannot differ.
/// </summary>
/// <param name="World">The world the experience belongs to.</param>
/// <param name="Experience">What each member receives: the whole award, or 0 in a world with no experience.</param>
/// <param name="Gains">One entry per member of the party, in the party's order.</param>
public sealed record ExperienceAward(World World, long Experience, IReadOnlyList<MemberGain> Gains);
