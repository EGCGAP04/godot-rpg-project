using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="FlagCatalogue"/>: reading the catalogue of flags, and the naming
/// convention every flag follows.
/// </summary>
public class FlagCatalogueTests
{
	/// <summary>Reads JSON written with single quotes, as in <see cref="ConversationReaderTests"/>.</summary>
	private static FlagCatalogue Read(string json) => FlagCatalogue.Read(json.Replace('\'', '"'));

	[Fact]
	public void Read_TheProjectCatalogue()
	{
		var catalogue = FlagCatalogue.Read(ProjectFiles.ReadText("Decisions", "flags.json"));

		Assert.Equal(
			new[] { "example.has_key", "example.key_returned", "example.met_before", "example.promised_help", "example.times_met" },
			catalogue.Flags.Keys.Order());
		Assert.Equal(FlagLifetime.Cycle, catalogue.Flags["example.has_key"].Lifetime);
		Assert.Equal(FlagLifetime.Persistent, catalogue.Flags["example.met_before"].Lifetime);
		Assert.Equal(FlagType.Counter, catalogue.Flags["example.times_met"].Type);
		Assert.Equal(FlagLifetime.Run, catalogue.Flags["example.times_met"].Lifetime);
	}

	[Fact]
	public void Read_ADescriptionIsOptional()
	{
		var catalogue = Read("{ 'format': 1, 'flags': { 'a.x': { 'type': 'bool', 'lifetime': 'run' } } }");

		Assert.Equal(new FlagDeclaration("a.x", FlagType.Bool, FlagLifetime.Run, ""), catalogue.Flags["a.x"]);
	}

	[Theory]
	[InlineData("{ 'format': 1 ", "not valid JSON")]
	[InlineData("{ 'format': 1 }", "missing 'flags'")]
	[InlineData("{ 'format': 2, 'flags': {} }", "format 2 is not supported")]
	[InlineData("{ 'format': 1, 'flags': { 'has_key': { 'type': 'bool', 'lifetime': 'run' } } }", "flag 'has_key'")]
	[InlineData("{ 'format': 1, 'flags': { 'example.and': { 'type': 'bool', 'lifetime': 'run' } } }", "flag 'example.and'")]
	[InlineData("{ 'format': 1, 'flags': { 'Example.key': { 'type': 'bool', 'lifetime': 'run' } } }", "flag 'Example.key'")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 'boolean', 'lifetime': 'run' } } }", "unknown type 'boolean'")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 1, 'lifetime': 'run' } } }", "'type' should be text")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 'bool', 'lifetime': 'forever' } } }", "unknown lifetime 'forever'")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 'bool' } } }", "flag 'a.x': missing 'lifetime'")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 'bool', 'lifetime': 'run', 'default': true } } }", "unknown property 'default'")]
	[InlineData("{ 'format': 1, 'flags': { 'a.x': { 'type': 'bool', 'lifetime': 'run' }, 'a.x': { 'type': 'counter', 'lifetime': 'run' } } }", "'a.x' appears twice")]
	public void Read_RejectsACatalogueThatIsNotValidAndSaysWhere(string json, string expected)
	{
		var error = Assert.Throws<DataFileException>(() => Read(json));

		Assert.Contains(expected, error.Message);
		Assert.Equal(FlagCatalogue.FileName, error.File);
	}

	[Theory]
	[InlineData("a.x", true)]
	[InlineData("example.has_key", true)]
	[InlineData("group.sub.fact", true)]
	[InlineData("a1.b_2", true)]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("has_key", false)]   // no group
	[InlineData("a.", false)]
	[InlineData(".a", false)]
	[InlineData("a..b", false)]
	[InlineData("A.b", false)]
	[InlineData("a.1b", false)]
	[InlineData("a._b", false)]
	[InlineData("a.and", false)]     // a reserved word of the condition language
	[InlineData("set.x", false)]
	[InlineData("a-b.c", false)]
	public void IsValidName_FollowsTheNamingConvention(string name, bool expected)
	{
		Assert.Equal(expected, FlagCatalogue.IsValidName(name));
	}
}
