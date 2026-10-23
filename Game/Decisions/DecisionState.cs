using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>
/// Everything the decision registry remembers: the value of every flag in the catalogue
/// and the history of fights. Deliberately a plain C# class with no Godot dependency, so
/// it can be unit-tested without running the engine; the <c>DecisionRegistry</c>
/// autoload wraps it.
/// </summary>
/// <remarks>
/// <para>
/// Only the flags of the catalogue exist, each starting at <c>false</c> or 0. A name
/// that was never declared, or one used with the wrong type, throws
/// <see cref="ArgumentException"/> instead of answering with a default, so a misspelt
/// flag cannot pass silently.
/// </para>
/// <para>
/// Each flag's lifetime decides what happens to it: <see cref="StartCycle"/> resets the
/// <see cref="FlagLifetime.Cycle"/> ones, and a <see cref="SlotSnapshot"/> carries the
/// cycle and <see cref="FlagLifetime.Run"/> ones but never the
/// <see cref="FlagLifetime.Persistent"/> ones. In memory, before there are save slots, a
/// run flag and a persistent flag behave the same; the difference only shows in the
/// snapshot.
/// </para>
/// </remarks>
public sealed class DecisionState : IFlagStore
{
	/// <summary>The counter of fights the player won, which <see cref="RecordCombat"/> adds to.</summary>
	public const string Wins = "combat.wins";

	/// <summary>The counter of fights the player lost, which <see cref="RecordCombat"/> adds to.</summary>
	public const string Losses = "combat.losses";

	private readonly Dictionary<string, bool> _flags = new();
	private readonly Dictionary<string, int> _counters = new();
	private readonly List<CombatRecord> _combats = new();

	/// <summary>Starts with every flag of <paramref name="catalogue"/> at its starting value and no fights.</summary>
	/// <exception cref="ArgumentException">
	/// The catalogue does not declare <see cref="Wins"/> and <see cref="Losses"/> as counters.
	/// </exception>
	public DecisionState(FlagCatalogue catalogue)
	{
		ArgumentNullException.ThrowIfNull(catalogue);

		foreach (string name in new[] { Wins, Losses })
		{
			if (!catalogue.Flags.TryGetValue(name, out FlagDeclaration flag) || flag.Type != FlagType.Counter)
				throw new ArgumentException($"'{name}' must be declared in {FlagCatalogue.FileName} as a counter: the registry counts fights with it", nameof(catalogue));
		}

		Catalogue = catalogue;
		Combats = _combats.AsReadOnly();

		foreach (FlagDeclaration flag in catalogue.Flags.Values)
		{
			if (flag.Type == FlagType.Bool)
				_flags.Add(flag.Name, false);
			else
				_counters.Add(flag.Name, 0);
		}
	}

	/// <summary>The catalogue every flag comes from, with each one's type, lifetime and description.</summary>
	public FlagCatalogue Catalogue { get; }

	/// <summary>
	/// Raised with a flag's name each time its value actually changes. Setting a flag that
	/// is already set, or adding 0, raises nothing, so a listener does not have to filter.
	/// When one call changes several flags, it raises this once per flag, after all of
	/// them have their new values.
	/// </summary>
	public event Action<string> FlagChanged;

	/// <summary>Every fight recorded so far, oldest first.</summary>
	public IReadOnlyList<CombatRecord> Combats { get; }

	public bool GetFlag(string name)
	{
		Declared(name, FlagType.Bool);
		return _flags[name];
	}

	public int GetCounter(string name)
	{
		Declared(name, FlagType.Counter);
		return _counters[name];
	}

	public void SetFlag(string name, bool value)
	{
		Declared(name, FlagType.Bool);

		if (Assign(name, value))
			FlagChanged?.Invoke(name);
	}

	/// <inheritdoc/>
	/// <remarks>Adding past <see cref="int.MaxValue"/> stops there too, so an overflow cannot turn a counter negative.</remarks>
	public void AddToCounter(string name, int amount)
	{
		Declared(name, FlagType.Counter);

		if (Assign(name, (int)Math.Clamp((long)_counters[name] + amount, 0, int.MaxValue)))
			FlagChanged?.Invoke(name);
	}

	/// <summary>
	/// Records the end of a fight: adds it to <see cref="Combats"/> and 1 to
	/// <see cref="Wins"/> or <see cref="Losses"/>. The two always go together, so the
	/// history and the totals never disagree.
	/// </summary>
	/// <param name="enemyId">The enemy's <c>Id</c>. It cannot be empty.</param>
	public void RecordCombat(World world, int cycle, string enemyId, bool playerWon)
	{
		if (string.IsNullOrEmpty(enemyId))
			throw new ArgumentException("a fight needs the id of the enemy it was against", nameof(enemyId));

		_combats.Add(new CombatRecord(world, cycle, enemyId, playerWon));
		AddToCounter(playerWon ? Wins : Losses, 1);
	}

	/// <summary>
	/// Starts a new cycle: every flag whose lifetime is <see cref="FlagLifetime.Cycle"/>
	/// goes back to its starting value. The rest, and the history, stay as they are.
	/// </summary>
	public void StartCycle()
	{
		var changed = new List<string>();

		foreach (FlagDeclaration flag in Catalogue.Flags.Values.Where(flag => flag.Lifetime == FlagLifetime.Cycle))
		{
			bool wasChanged = flag.Type == FlagType.Bool ? Assign(flag.Name, false) : Assign(flag.Name, 0);

			if (wasChanged)
				changed.Add(flag.Name);
		}

		Announce(changed);
	}

	/// <summary>
	/// A copy of what belongs to the save slot: the cycle and run flags, and the history.
	/// Changing the state afterwards does not change the snapshot.
	/// </summary>
	public SlotSnapshot TakeSnapshot()
	{
		IEnumerable<FlagDeclaration> slotFlags = Catalogue.Flags.Values.Where(flag => flag.Lifetime != FlagLifetime.Persistent);

		return new SlotSnapshot(
			new ReadOnlyDictionary<string, bool>(slotFlags.Where(flag => flag.Type == FlagType.Bool).ToDictionary(flag => flag.Name, flag => _flags[flag.Name])),
			new ReadOnlyDictionary<string, int>(slotFlags.Where(flag => flag.Type == FlagType.Counter).ToDictionary(flag => flag.Name, flag => _counters[flag.Name])),
			_combats.ToArray());
	}

	/// <summary>
	/// Replaces everything that belongs to the save slot with what
	/// <paramref name="snapshot"/> holds. Persistent flags stay as they are. A cycle or run
	/// flag the snapshot does not mention goes back to its starting value.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// The snapshot names a flag the catalogue does not declare, declares as persistent or
	/// declares with the other type, or holds a negative counter. Nothing changes then.
	/// </exception>
	public void Restore(SlotSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		foreach (string name in snapshot.Flags.Keys)
			Restorable(name, FlagType.Bool);

		foreach ((string name, int value) in snapshot.Counters)
		{
			Restorable(name, FlagType.Counter);

			if (value < 0)
				throw new ArgumentException($"'{name}' is {value} in the snapshot, but a counter is never below 0", nameof(snapshot));
		}

		var changed = new List<string>();

		foreach (FlagDeclaration flag in Catalogue.Flags.Values.Where(flag => flag.Lifetime != FlagLifetime.Persistent))
		{
			bool wasChanged = flag.Type == FlagType.Bool
				? Assign(flag.Name, snapshot.Flags.GetValueOrDefault(flag.Name))
				: Assign(flag.Name, snapshot.Counters.GetValueOrDefault(flag.Name));

			if (wasChanged)
				changed.Add(flag.Name);
		}

		_combats.Clear();
		_combats.AddRange(snapshot.Combats);
		Announce(changed);
	}

	/// <summary>Throws unless the catalogue declares <paramref name="name"/> with <paramref name="type"/>.</summary>
	private FlagDeclaration Declared(string name, FlagType type)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (!Catalogue.Flags.TryGetValue(name, out FlagDeclaration flag))
			throw new ArgumentException($"'{name}' is not declared in {FlagCatalogue.FileName}", nameof(name));

		if (flag.Type != type)
		{
			throw new ArgumentException(
				flag.Type == FlagType.Counter ? $"'{name}' is a counter, not a boolean flag" : $"'{name}' is a boolean flag, not a counter",
				nameof(name));
		}

		return flag;
	}

	/// <summary>Throws unless <paramref name="name"/> can be in a snapshot as <paramref name="type"/>.</summary>
	private void Restorable(string name, FlagType type)
	{
		if (Declared(name, type).Lifetime == FlagLifetime.Persistent)
			throw new ArgumentException($"'{name}' is persistent, so it belongs to the game and never to a save slot", nameof(name));
	}

	/// <summary>Gives a boolean flag its value, and says whether that changed it.</summary>
	private bool Assign(string name, bool value)
	{
		if (_flags[name] == value)
			return false;

		_flags[name] = value;
		return true;
	}

	/// <summary>Gives a counter its value, and says whether that changed it.</summary>
	private bool Assign(string name, int value)
	{
		if (_counters[name] == value)
			return false;

		_counters[name] = value;
		return true;
	}

	private void Announce(IEnumerable<string> changed)
	{
		foreach (string name in changed)
			FlagChanged?.Invoke(name);
	}
}
