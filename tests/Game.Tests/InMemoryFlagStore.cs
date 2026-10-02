using System.Collections.Generic;

namespace GodotRPGProject.Tests;

/// <summary>
/// The decision registry's stand-in for tests: one dictionary per type, with every name
/// declared up front. Reading or writing a name that was never declared throws, as the
/// registry will, so a misspelt flag in a test fails instead of quietly reading false.
/// </summary>
public sealed class InMemoryFlagStore : IFlagStore
{
	private readonly Dictionary<string, bool> _flags = new();
	private readonly Dictionary<string, int> _counters = new();

	/// <summary>How many writes have gone through, so a test can prove something never writes.</summary>
	public int Writes { get; private set; }

	/// <summary>Declares a boolean flag with its starting value. Does not count as a write.</summary>
	public InMemoryFlagStore WithFlag(string name, bool value = false)
	{
		_flags[name] = value;
		return this;
	}

	/// <summary>Declares a counter with its starting value. Does not count as a write.</summary>
	public InMemoryFlagStore WithCounter(string name, int value = 0)
	{
		_counters[name] = value;
		return this;
	}

	/// <summary>
	/// Declares every flag of <paramref name="catalogue"/> at its starting value, as at the
	/// start of a playthrough: false or 0. Does not count as writes.
	/// </summary>
	public InMemoryFlagStore WithCatalogue(FlagCatalogue catalogue)
	{
		foreach (FlagDeclaration flag in catalogue.Flags.Values)
		{
			if (flag.Type == FlagType.Bool)
				WithFlag(flag.Name);
			else
				WithCounter(flag.Name);
		}

		return this;
	}

	public bool GetFlag(string name) => _flags[name];

	public int GetCounter(string name) => _counters[name];

	public void SetFlag(string name, bool value)
	{
		if (!_flags.ContainsKey(name))
			throw new KeyNotFoundException($"'{name}' was never declared as a flag.");

		_flags[name] = value;
		Writes++;
	}

	public void AddToCounter(string name, int amount)
	{
		if (!_counters.ContainsKey(name))
			throw new KeyNotFoundException($"'{name}' was never declared as a counter.");

		_counters[name] += amount;
		Writes++;
	}
}
