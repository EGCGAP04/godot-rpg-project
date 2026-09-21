/// <summary>
/// The three worlds a single cycle runs through, declared in the order they are
/// played. The design documents call these <c>Real_World</c>, <c>Fantasy_World</c>
/// and <c>Nightmare_World</c>; those are design labels, and this is their code form.
/// </summary>
/// <remarks>
/// The declaration order is meaningful: <see cref="CycleProgression"/> walks forward
/// through these values to find the next world to play, so reordering them reorders
/// the game.
/// </remarks>
public enum World
{
	/// <summary>Ordinary life. Every cycle starts here and it can never be skipped.</summary>
	Real,

	/// <summary>Reached by falling asleep, once the Real world's conditions are met.</summary>
	Fantasy,

	/// <summary>Reached only through specific actions in both previous worlds.</summary>
	Nightmare
}
