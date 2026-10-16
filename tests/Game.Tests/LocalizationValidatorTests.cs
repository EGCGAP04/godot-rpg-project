using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GodotRPGProject.Tests.PoText;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="LocalizationValidator"/>. The first test is the one that keeps the
/// project's own tables valid; the rest check each rule on a small table.
/// </summary>
public class LocalizationValidatorTests
{
	/// <summary>A valid table in both languages. Each test changes one thing in it.</summary>
	private static TranslationFile[] Valid() => new[]
	{
		File("Localization/ui.en.po", ("ui.attack", "Attack"), ("ui.hp", "{hp}/{max_hp}")),
		File("Localization/ui.es.po", ("ui.attack", "Atacar"), ("ui.hp", "{hp}/{max_hp}")),
	};

	/// <summary>Validates the files, with each of them registered once.</summary>
	private static IReadOnlyList<string> Validate(params TranslationFile[] files) =>
		LocalizationValidator.Validate(files, files.Select(file => file.Path));

	/// <summary><see cref="Valid"/>, with the Spanish table's text replaced.</summary>
	private static TranslationFile[] WithSpanish(string text) =>
		new[] { Valid()[0], new TranslationFile("Localization/ui.es.po", text) };

	[Fact]
	public void EveryTableInTheProject_IsValid()
	{
		// The test that makes a broken table fail CI. Like the conversations' one, it
		// refuses to pass by finding nothing.
		TranslationFile[] files = ProjectFiles.TranslationFiles();
		IReadOnlyList<string> registered = LocalizationValidator.RegisteredTables(ProjectFiles.ReadText("project.godot"));

		Assert.NotEmpty(files);
		Assert.Empty(LocalizationValidator.Validate(files, registered));
	}

	[Fact]
	public void ValidTables_HaveNoProblems()
	{
		Assert.Empty(Validate(Valid()));
	}

	[Fact]
	public void NoTablesAtAll_HaveNoProblems()
	{
		Assert.Empty(Validate());
	}

	// Each table

	[Theory]
	[InlineData("Localization/ui.fr.po")]
	[InlineData("Localization/UI.en.po")]
	[InlineData("Localization/ui.po")]
	[InlineData("Localization/ui.en.extra.po")]
	public void AFileNameThatIsNotATables_IsReported(string path)
	{
		var problems = Validate(File(path, ("ui.a", "A")));

		Assert.StartsWith($"{path}: a table's file is named <name>.<locale>.po", Assert.Single(problems));
	}

	[Fact]
	public void AFileTheReaderRefuses_IsReportedOnce_AndNotAlsoAsMissing()
	{
		var problems = Validate(WithSpanish("msgid \"\"\nmsgstr \"\"\n\nmsgid \"ui.attack\"\nmsgstr \"Atacar\"\n"));

		Assert.Equal(
			"Localization/ui.es.po: line 1: the header declares no Language, and Godot would load the table as English.",
			Assert.Single(problems));
	}

	[Fact]
	public void AHeaderThatDisagreesWithTheFileName_IsReported()
	{
		var problems = Validate(WithSpanish(Table("en", ("ui.attack", "Atacar"), ("ui.hp", "{hp}/{max_hp}"))));

		Assert.Equal("Localization/ui.es.po: line 1: the header says 'en', but the file name says 'es'.", Assert.Single(problems));
	}

	[Fact]
	public void AKeyThatDoesNotStartWithTheTablesName_IsReported()
	{
		var problems = Validate(
			File("Localization/enemy.en.po", ("enemy.weak", "Enemy"), ("ui.strong", "Strong Enemy")),
			File("Localization/enemy.es.po", ("enemy.weak", "Enemigo"), ("ui.strong", "Enemigo fuerte")));

		Assert.Equal(
			new[]
			{
				"Localization/enemy.en.po: line 7: 'ui.strong' does not start with 'enemy.', the table's name.",
				"Localization/enemy.es.po: line 7: 'ui.strong' does not start with 'enemy.', the table's name.",
			},
			problems);
	}

	[Fact]
	public void AFuzzyEntry_IsReported()
	{
		string text = Table("es", ("ui.attack", "Atacar"), ("ui.hp", "{hp}/{max_hp}")).Replace("msgid \"ui.attack\"", "#, fuzzy\nmsgid \"ui.attack\"");

		Assert.Equal(
			"Localization/ui.es.po: line 5: 'ui.attack' is marked fuzzy, and Godot ignores fuzzy entries; check the text and remove the flag.",
			Assert.Single(Validate(WithSpanish(text))));
	}

	[Fact]
	public void AnEmptyText_IsReported()
	{
		var problems = Validate(WithSpanish(Table("es", ("ui.attack", ""), ("ui.hp", "{hp}/{max_hp}"))));

		Assert.Equal("Localization/ui.es.po: line 4: 'ui.attack' has no text.", Assert.Single(problems));
	}

	// Between languages

	[Fact]
	public void ATableMissingInOneLanguage_IsReported()
	{
		var problems = Validate(Valid()[0]);

		Assert.Equal("Localization/ui.es.po: there is no such file, and every table needs one per language.", Assert.Single(problems));
	}

	[Fact]
	public void AKeyMissingInOneLanguage_IsReported()
	{
		var problems = Validate(WithSpanish(Table("es", ("ui.attack", "Atacar"))));

		Assert.Equal("Localization/ui.es.po: 'ui.hp' is missing; Localization/ui.en.po has it.", Assert.Single(problems));
	}

	[Theory]
	[InlineData("{hp}/{max}", "en has {hp, max_hp}; es has {hp, max}")]
	[InlineData("{hp}", "en has {hp, max_hp}; es has {hp}")]
	[InlineData("vida", "en has {hp, max_hp}; es has none")]
	public void DifferentPlaceholdersInEachLanguage_AreReported(string spanish, string detail)
	{
		var problems = Validate(WithSpanish(Table("es", ("ui.attack", "Atacar"), ("ui.hp", spanish))));

		Assert.Equal($"Localization/ui.*.po: 'ui.hp' needs the same placeholders in every language: {detail}.", Assert.Single(problems));
	}

	[Fact]
	public void PlaceholdersInADifferentOrder_AreFine()
	{
		Assert.Empty(Validate(WithSpanish(Table("es", ("ui.attack", "Atacar"), ("ui.hp", "{max_hp} de {hp}")))));
	}

	[Fact]
	public void TwoTablesWithTheSameName_AreReported()
	{
		var problems = Validate(Valid().Concat(new[]
		{
			File("Dialogue/Conversations/ui.en.po", ("ui.other", "Other")),
			File("Dialogue/Conversations/ui.es.po", ("ui.other", "Otro")),
		}).ToArray());

		Assert.Equal(
			"ui: there are tables with this name in 'Dialogue/Conversations', 'Localization', and their keys would mix, since Godot merges every table of a language into one.",
			Assert.Single(problems));
	}

	// Registration

	[Fact]
	public void ATableProjectGodotDoesNotRegister_IsReported()
	{
		var problems = LocalizationValidator.Validate(Valid(), new[] { "Localization/ui.en.po" });

		Assert.Equal("Localization/ui.es.po: project.godot does not register it, so Godot never shows its text.", Assert.Single(problems));
	}

	[Fact]
	public void ARegisteredTableThatDoesNotExist_IsReported()
	{
		var problems = LocalizationValidator.Validate(Valid(), new[] { "Localization/ui.en.po", "Localization/ui.es.po", "Localization/gone.es.po" });

		Assert.Equal("Localization/gone.es.po: project.godot registers it, but there is no such file.", Assert.Single(problems));
	}

	[Fact]
	public void ATableRegisteredTwice_IsReported()
	{
		var problems = LocalizationValidator.Validate(Valid(), new[] { "Localization/ui.en.po", "Localization/ui.es.po", "Localization/ui.es.po" });

		Assert.Equal("Localization/ui.es.po: project.godot registers it more than once.", Assert.Single(problems));
	}

	[Fact]
	public void RegisteredTables_ReadsTheListInsideGame()
	{
		string projectGodot = """
			[application]

			locale/translations=PackedStringArray("res://Wrong/section.en.po")

			[internationalization]

			locale/translations=PackedStringArray("res://Localization/ui.en.po", "res://Dialogue/Conversations/talk.es.po")

			[physics]
			""";

		Assert.Equal(new[] { "Localization/ui.en.po", "Dialogue/Conversations/talk.es.po" }, LocalizationValidator.RegisteredTables(projectGodot));
	}

	[Fact]
	public void RegisteredTables_IsEmptyWhenNothingIsRegistered()
	{
		Assert.Empty(LocalizationValidator.RegisteredTables("[application]\n\nconfig/name=\"Game\"\n"));
	}
}
