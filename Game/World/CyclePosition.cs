/// <summary>
/// Where the run currently stands: the world being played and the cycle it belongs
/// to. A cycle is one narratively important day, numbered from 1.
/// </summary>
/// <remarks>
/// A <c>readonly record struct</c> so that comparing two positions in a test is a
/// plain <c>Assert.Equal</c>, and so a position can never be mutated in place by a
/// caller that only meant to read it.
/// </remarks>
public readonly record struct CyclePosition(World World, int Cycle);
