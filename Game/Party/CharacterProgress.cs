/// <summary>
/// What one character keeps in one world while playing: where they stand on the way to
/// the next level, and the health points and mana points they have left. Everything else
/// about them is computed from the level.
/// </summary>
/// <param name="Level">The level, and the experience gathered towards the next one.</param>
/// <param name="HealthPoints">The health points left, from 0 to what the level gives.</param>
/// <param name="ManaPoints">The mana points left, from 0 to what the level gives.</param>
public readonly record struct CharacterProgress(LevelProgress Level, int HealthPoints, int ManaPoints);
