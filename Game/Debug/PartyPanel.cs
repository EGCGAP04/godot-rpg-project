using System;
using System.Globalization;
using System.Linq;
using Godot;

/// <summary>
/// The party's section of the debug overlay (#92): for a character and a world chosen
/// here, whether they are in the party, their level, and the derived stats the design
/// hides from the player. It also gives experience, as a fight won would.
/// </summary>
/// <remarks>
/// <para>
/// Built in code from the roster, so a new character shows without touching it. Every
/// change goes through the party's own rules: <c>TryRecruit</c> and <c>TryDismiss</c>,
/// whose refusals show here in their own words; <c>Award</c>, which gives every member
/// the whole amount; and <c>Restore</c> over a snapshot edited by
/// <see cref="PartyEdits"/> for a level, which the game itself never sets.
/// </para>
/// <para>
/// It listens to the party, so a change made anywhere shows, and whatever it changes
/// shows on the status screen behind the overlay. The derived stats come from
/// <see cref="DerivedStatsTable"/>, over the tuning in <c>stat_tuning.tres</c>.
/// </para>
/// </remarks>
public partial class PartyPanel : VBoxContainer
{
	private PartyState _party;
	private StatFormulas _formulas;
	private OptionButton _character;
	private OptionButton _world;
	private CheckBox _inParty;
	private SpinBox _level;
	private Label _levelRange;
	private SpinBox _experience;
	private Label _summary;
	private GridContainer _derived;
	private Label _message;

	private string Character => _party.Roster.Characters[_character.Selected].Id;

	private World World => (World)_world.GetSelectedId();

	public override void _Ready()
	{
		_party = Party.Instance.State;
		_formulas = new StatFormulas(GD.Load<StatTuning>(Party.TuningPath).ToValues());

		BuildSelection();
		BuildChanges();

		_summary = new Label();
		AddChild(_summary);

		_derived = new GridContainer { Columns = 3 };
		_derived.AddThemeConstantOverride("h_separation", 16);
		AddChild(_derived);

		_message = new Label();
		AddChild(_message);

		_party.MembersChanged += Refresh;
		_party.ProgressChanged += OnProgressChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		_party.MembersChanged -= Refresh;
		_party.ProgressChanged -= OnProgressChanged;
	}

	private void BuildSelection()
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = "Character" });

		_character = new OptionButton();
		foreach (CharacterValues character in _party.Roster.Characters)
			_character.AddItem(character.Id);
		_character.ItemSelected += _ => OnSelectionChanged();
		row.AddChild(_character);

		row.AddChild(new Label { Text = "world" });
		_world = new OptionButton();
		foreach (World world in Enum.GetValues<World>())
			_world.AddItem(world.ToString(), (int)world);
		_world.Selected = (int)GameState.Instance.CurrentWorld;
		_world.ItemSelected += _ => OnSelectionChanged();
		row.AddChild(_world);

		_inParty = new CheckBox { Text = "In party" };
		_inParty.Toggled += OnInPartyToggled;
		row.AddChild(_inParty);
		AddChild(row);
	}

	private void BuildChanges()
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = "Level" });
		_level = new SpinBox { Rounded = true };
		row.AddChild(_level);
		_levelRange = new Label();
		row.AddChild(_levelRange);

		var set = new Button { Text = "Set" };
		set.Pressed += OnSetLevelPressed;
		row.AddChild(set);

		row.AddChild(new Label { Text = "   Experience" });
		_experience = new SpinBox { MinValue = 1, MaxValue = 1_000_000, Rounded = true, Value = 10 };
		row.AddChild(_experience);

		var give = new Button { Text = "Give to party" };
		give.Pressed += OnGivePressed;
		row.AddChild(give);
		AddChild(row);
	}

	private void OnSelectionChanged()
	{
		_message.Text = "";
		Refresh();
	}

	private void OnProgressChanged(string id, World world) => Refresh();

	private void OnInPartyToggled(bool pressed)
	{
		string refusal;
		bool done = pressed ? _party.TryRecruit(Character, out refusal) : _party.TryDismiss(Character, out refusal);

		_message.Text = done ? "" : refusal;

		// A refusal changes nothing and raises nothing, so the box is put back by hand.
		Refresh();
	}

	private void OnSetLevelPressed()
	{
		try
		{
			_party.Restore(PartyEdits.WithLevel(_party.TakeSnapshot(), _party.Roster, Character, World, (int)_level.Value));
			_message.Text = "";
		}
		catch (ArgumentException refusal)
		{
			_message.Text = refusal.Message;
		}

		Refresh();
	}

	private void OnGivePressed()
	{
		ExperienceAward award = _party.Award(World, (long)_experience.Value);

		_message.Text = award.Experience == 0
			? $"The {World} world gives no experience."
			: string.Create(CultureInfo.InvariantCulture, $"Gave {award.Experience} to each of: {string.Join(", ", award.Gains.Select(gain => gain.Id))}.");

		Refresh();
	}

	/// <summary>Shows the chosen character in the chosen world as the party holds them now.</summary>
	private void Refresh()
	{
		CharacterValues character = _party.Roster.Get(Character);
		CharacterProgress progress = _party.ProgressOf(character.Id, World);
		BaseStats stats = _party.StatsOf(character.Id, World);

		_inParty.SetPressedNoSignal(_party.IsMember(character.Id));
		_inParty.Disabled = character.Id == _party.Roster.Protagonist.Id;

		int lowest = character.StartingLevel(World);
		int highest = _party.Curves.For(World)?.HighestLevel ?? lowest;

		_level.MinValue = lowest;
		_level.MaxValue = highest;
		_level.SetValueNoSignal(progress.Level.Level);
		_levelRange.Text = lowest == highest ? "(fixed)" : $"({lowest}-{highest})";

		_summary.Text = string.Create(CultureInfo.InvariantCulture,
			$"Level {progress.Level.Level}, experience {progress.Level.Experience}, HP {progress.HealthPoints}/{stats.HealthPoints}, MP {progress.ManaPoints}/{stats.ManaPoints}");

		foreach (Node cell in _derived.GetChildren())
		{
			_derived.RemoveChild(cell);
			cell.QueueFree();
		}

		AddCells("", "Attack", "Skill");

		foreach (DerivedStatRow row in DerivedStatsTable.For(_formulas, stats))
			AddCells(row.Name, DerivedStatsTable.Format(row.Attack), row.Skill is double skill ? DerivedStatsTable.Format(skill) : "");
	}

	private void AddCells(params string[] texts)
	{
		foreach (string text in texts)
			_derived.AddChild(new Label { Text = text });
	}
}
