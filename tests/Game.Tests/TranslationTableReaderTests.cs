using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="TranslationTableReader"/>: how a <c>.po</c> file becomes a locale
/// and its entries, and where it reports a file the game cannot read.
/// </summary>
public class TranslationTableReaderTests
{
	/// <summary>The smallest valid header, which every other test's file starts with.</summary>
	private const string Header = """
		msgid ""
		msgstr "Language: es\n"


		""";

	private static TranslationTable Read(string entries) => TranslationTableReader.Read("test.es.po", Header + entries);

	private static string ReadError(string text) =>
		Assert.Throws<DataFileException>(() => TranslationTableReader.Read("test.es.po", text)).Message;

	[Fact]
	public void Read_BuildsTheLocaleAndTheEntriesInOrder()
	{
		var table = TranslationTableReader.Read("ui.es.po", """
			# A comment about the file.
			msgid ""
			msgstr ""
			"Language: es\n"
			"Content-Type: text/plain; charset=UTF-8\n"

			#. The combat button.
			#: Combat/combat.tscn
			msgid "ui.combat.attack"
			msgstr "Atacar"

			msgid "ui.combat.player"
			msgstr "Jugador"
			""");

		Assert.Equal("es", table.Locale);
		Assert.Equal(new[] { "ui.combat.attack", "ui.combat.player" }, table.Entries.Select(entry => entry.Key));
		Assert.Equal(new TranslationEntry("ui.combat.attack", "Atacar", false, 9), table.Entries[0]);
		Assert.Equal(new TranslationEntry("ui.combat.player", "Jugador", false, 12), table.Entries[1]);
	}

	[Fact]
	public void Find_ReturnsTheEntry_OrNullWhenTheKeyIsNotThere()
	{
		var table = Read("""
			msgid "ui.a"
			msgstr "A"
			""");

		Assert.Equal("A", table.Find("ui.a").Text);
		Assert.Null(table.Find("ui.b"));
	}

	[Fact]
	public void Read_JoinsATextWrittenAcrossSeveralLines()
	{
		var table = Read("""
			msgid ""
			"ui."
			"long"
			msgstr ""
			"First part, "
			"second part."
			""");

		TranslationEntry entry = Assert.Single(table.Entries);
		Assert.Equal("ui.long", entry.Key);
		Assert.Equal("First part, second part.", entry.Text);
	}

	[Theory]
	[InlineData(@"one\ntwo", "one\ntwo")]
	[InlineData(@"a\tb", "a\tb")]
	[InlineData(@"say \""hi\""", "say \"hi\"")]
	[InlineData(@"back\\slash", @"back\slash")]
	[InlineData("¡Ñandú! «sí» {hp}", "¡Ñandú! «sí» {hp}")]
	public void Read_ResolvesTheEscapes(string written, string expected)
	{
		var table = Read($"msgid \"ui.a\"\nmsgstr \"{written}\"\n");

		Assert.Equal(expected, Assert.Single(table.Entries).Text);
	}

	[Fact]
	public void Read_MarksOnlyTheEntryAfterAFuzzyFlag()
	{
		var table = Read("""
			#, c-format, fuzzy
			msgid "ui.a"
			msgstr "A"

			#, c-format
			msgid "ui.b"
			msgstr "B"
			""");

		Assert.True(table.Entries[0].IsFuzzy);
		Assert.False(table.Entries[1].IsFuzzy);
	}

	[Fact]
	public void Read_AcceptsAFuzzyHeader_AsGodotDoes()
	{
		var table = TranslationTableReader.Read("test.es.po", "#, fuzzy\n" + Header + "msgid \"ui.a\"\nmsgstr \"A\"\n");

		Assert.Equal("es", table.Locale);
		Assert.False(Assert.Single(table.Entries).IsFuzzy);
	}

	[Fact]
	public void Read_AcceptsAByteOrderMarkAndWindowsLineEndings_AsGodotDoes()
	{
		var table = TranslationTableReader.Read("test.es.po", "\uFEFFmsgid \"\"\r\nmsgstr \"Language: es\\n\"\r\n\r\nmsgid \"ui.a\"\r\nmsgstr \"A\"\r\n");

		Assert.Equal("es", table.Locale);
		Assert.Equal("A", Assert.Single(table.Entries).Text);
	}

	[Fact]
	public void Read_KeepsAnEmptyText_ForTheValidatorToReport()
	{
		var table = Read("""
			msgid "ui.a"
			msgstr ""
			""");

		Assert.Equal("", Assert.Single(table.Entries).Text);
	}

	[Fact]
	public void Read_AcceptsATableWithNoEntries()
	{
		Assert.Empty(Read("").Entries);
	}

	// The header

	[Theory]
	[InlineData("", "test.es.po: the first entry must be the header, with msgid \"\".")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\n", "test.es.po: the first entry must be the header, with msgid \"\".")]
	[InlineData("msgid \"\"\nmsgstr \"Content-Type: text/plain; charset=UTF-8\\n\"\n", "test.es.po: line 1: the header declares no Language, and Godot would load the table as English.")]
	[InlineData("msgid \"\"\nmsgstr \"Language: \\n\"\n", "test.es.po: line 1: the header declares no Language, and Godot would load the table as English.")]
	public void Read_RequiresAHeaderThatDeclaresTheLanguage(string text, string expected)
	{
		Assert.Equal(expected, ReadError(text));
	}

	// What it refuses

	[Theory]
	[InlineData("msgctxt \"menu\"\nmsgid \"ui.a\"\nmsgstr \"A\"\n", "line 4: msgctxt is not supported yet")]
	[InlineData("msgid \"ui.a\"\nmsgid_plural \"ui.as\"\nmsgstr[0] \"A\"\n", "line 5: msgid_plural is not supported yet")]
	[InlineData("msgid \"ui.a\"\nmsgstr[0] \"A\"\n", "line 5: plurals (msgstr[n]) are not supported yet")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\\q\"\n", @"line 5: '\q' is not an escape; the escapes are \n, \t, \"" and \\")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\\\"\n", "line 5: a backslash at the end of a text escapes nothing")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"say \"hi\"\"\n", "line 5: a double quote inside a text is written \\\"")]
	[InlineData("msgid \"ui.a\"\nmsgstr A\n", "line 5: expected a text between double quotes")]
	[InlineData("msgid \"ui.a\"\nmsgstr\n", "line 5: expected a text between double quotes")]
	[InlineData("msgid \"ui.a\"\nmsgtsr \"A\"\n", "line 5: 'msgtsr' is not something a .po file has")]
	public void Read_RefusesWhatItDoesNotRead(string entries, string expected)
	{
		Assert.Equal($"test.es.po: {expected}.", ReadError(Header + entries));
	}

	[Theory]
	[InlineData("msgid \"ui.a\"\n", "line 4: msgid \"ui.a\" has no msgstr")]
	[InlineData("msgid \"ui.a\"\n\nmsgstr \"A\"\n", "line 4: msgid \"ui.a\" has no msgstr")]
	[InlineData("msgid \"ui.a\"\nmsgid \"ui.b\"\nmsgstr \"B\"\n", "line 4: msgid \"ui.a\" has no msgstr")]
	[InlineData("msgid \"ui.a\"\n# a note\nmsgstr \"A\"\n", "line 5: a comment cannot go between a msgid and its msgstr")]
	[InlineData("msgstr \"A\"\n", "line 4: a msgstr needs a msgid right before it")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\nmsgstr \"B\"\n", "line 6: a msgstr needs a msgid right before it")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\n\n\"more\"\n", "line 7: a quoted line continues a msgid or a msgstr, and there is none before it")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\n# a note\n\"more\"\n", "line 7: a quoted line continues a msgid or a msgstr, and there is none before it")]
	public void Read_RequiresEveryMsgidToHaveItsMsgstr(string entries, string expected)
	{
		Assert.Equal($"test.es.po: {expected}.", ReadError(Header + entries));
	}

	// The keys

	[Theory]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\n\nmsgid \"ui.a\"\nmsgstr \"B\"\n", "line 7: 'ui.a' is in the file twice")]
	[InlineData("msgid \"ui.a\"\nmsgstr \"A\"\n\nmsgid \"\"\nmsgstr \"B\"\n", "line 7: only the header has an empty msgid")]
	[InlineData("msgid \"Attack\"\nmsgstr \"Atacar\"\n", "line 4: 'Attack' is not a key: keys are words joined by dots, such as ui.combat.attack")]
	public void Read_RequiresUniqueKeys(string entries, string expected)
	{
		Assert.Equal($"test.es.po: {expected}.", ReadError(Header + entries));
	}

	[Theory]
	[InlineData("ui.combat.attack", true)]
	[InlineData("example_first_meeting.start.greet", true)]
	[InlineData("enemy.weak2", true)]
	[InlineData("ui", false)]
	[InlineData("ui.", false)]
	[InlineData(".ui.a", false)]
	[InlineData("ui..a", false)]
	[InlineData("ui.Combat", false)]
	[InlineData("ui.combat attack", false)]
	[InlineData("ui.2a", false)]
	public void IsKey_AcceptsOnlyWordsJoinedByDots(string key, bool expected)
	{
		Assert.Equal(expected, TranslationTableReader.IsKey(key));
	}
}
