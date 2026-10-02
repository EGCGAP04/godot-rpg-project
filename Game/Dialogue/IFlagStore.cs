/// <summary>
/// Where dialogue conditions read flags and dialogue effects write them. The decision
/// registry implements it for the game; tests implement it with a dictionary.
/// </summary>
/// <remarks>
/// Flags come in the two types the flag catalogue declares: booleans, which start
/// <c>false</c>, and integer counters, which start at 0. An implementation should
/// reject a name that was never declared, or one used with the wrong type, instead of
/// answering with a default: a misspelt flag must not pass silently.
/// </remarks>
public interface IFlagStore
{
	/// <summary>Whether the boolean flag <paramref name="name"/> is set.</summary>
	bool GetFlag(string name);

	/// <summary>The current value of the counter <paramref name="name"/>.</summary>
	int GetCounter(string name);

	/// <summary>Sets or clears the boolean flag <paramref name="name"/>.</summary>
	void SetFlag(string name, bool value);

	/// <summary>
	/// Adds <paramref name="amount"/> to the counter <paramref name="name"/>. A negative
	/// amount subtracts.
	/// </summary>
	void AddToCounter(string name, int amount);
}
