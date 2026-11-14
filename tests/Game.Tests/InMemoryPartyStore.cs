using System;
using System.Collections.Generic;
using System.Linq;

namespace GodotRPGProject.Tests;

/// <summary>
/// The party's stand-in for dialogue tests: a list of members over the ids it was told
/// exist. It refuses what <see cref="PartyState"/> refuses and throws for an id it does
/// not know, so a misspelt character in a test fails instead of quietly not being there.
/// <c>PartyStoreContractTests</c> holds both to the same behaviour.
/// </summary>
public sealed class InMemoryPartyStore : IPartyStore
{
	private readonly string _protagonist;
	private readonly HashSet<string> _known;
	private readonly List<string> _members;
	private readonly int _maximumSize;

	/// <summary>A party of the protagonist alone, where <paramref name="others"/> exist too.</summary>
	public InMemoryPartyStore(string protagonist, params string[] others)
		: this(PartyState.MaximumSize, protagonist, others)
	{
	}

	/// <summary>The same, holding <paramref name="maximumSize"/> at most, to fill it with fewer characters.</summary>
	public InMemoryPartyStore(int maximumSize, string protagonist, params string[] others)
	{
		_maximumSize = maximumSize;
		_protagonist = protagonist;
		_known = new HashSet<string>(others.Prepend(protagonist));
		_members = new List<string> { protagonist };
	}

	/// <summary>Who is in the party, in order.</summary>
	public IReadOnlyList<string> Members => _members;

	/// <summary>How many changes went through, so a test can prove something never changes the party.</summary>
	public int Changes { get; private set; }

	public bool IsFull => _members.Count >= _maximumSize;

	/// <summary>Puts <paramref name="character"/> in the party from the start. Does not count as a change.</summary>
	public InMemoryPartyStore With(string character)
	{
		_members.Add(Known(character));
		return this;
	}

	public bool IsMember(string character) => _members.Contains(Known(character));

	public bool TryRecruit(string character, out string refusal)
	{
		if (IsMember(character))
		{
			refusal = $"'{character}' is already in the party.";
			return false;
		}

		if (IsFull)
		{
			refusal = $"'{character}' cannot join: the party already holds {_maximumSize}.";
			return false;
		}

		refusal = null;
		_members.Add(character);
		Changes++;
		return true;
	}

	public bool TryDismiss(string character, out string refusal)
	{
		if (Known(character) == _protagonist)
		{
			refusal = $"'{character}' is the protagonist, who never leaves the party.";
			return false;
		}

		if (!_members.Remove(character))
		{
			refusal = $"'{character}' is not in the party.";
			return false;
		}

		refusal = null;
		Changes++;
		return true;
	}

	private string Known(string character) => _known.Contains(character)
		? character
		: throw new ArgumentException($"'{character}' was never declared as a character.", nameof(character));
}
