using System;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="DebugArguments"/>, which reads what the debug tooling was asked
/// for after <c>--</c> on the command line. Every argument it does not understand is an
/// error, never skipped.
/// </summary>
public class DebugArgumentsTests
{
	[Fact]
	public void Parse_WithNoArguments_AsksForNothing()
	{
		DebugArguments parsed = DebugArguments.Parse(Array.Empty<string>());

		Assert.True(parsed.IsEmpty);
		Assert.Null(parsed.PresetPath);
	}

	[Fact]
	public void Parse_ReadsEveryArgument_InAnyOrder()
	{
		DebugArguments parsed = DebugArguments.Parse(new[]
		{
			"--do", "set test.tonight", "--cycle", "2", "--preset", "res://Debug/Presets/a.json", "--world", "fantasy", "--do", "add test.visits 3"
		});

		Assert.False(parsed.IsEmpty);
		Assert.Equal("res://Debug/Presets/a.json", parsed.PresetPath);
		Assert.Equal(World.Fantasy, parsed.Overrides.World);
		Assert.Equal(2, parsed.Overrides.Cycle);
		Assert.Equal(
			new FlagEffect[] { new FlagEffect.Set("test.tonight", true), new FlagEffect.Add("test.visits", 3) },
			parsed.Overrides.Effects.Select(effect => effect.Effect));
		Assert.Equal(new[] { "command line: --do 1", "command line: --do 2" }, parsed.Overrides.Effects.Select(effect => effect.Source));
	}

	[Theory]
	[InlineData("--preset", "a.json")]
	[InlineData("--world", "real")]
	[InlineData("--cycle", "1")]
	[InlineData("--do", "set test.tonight")]
	public void Parse_WithAnyOneArgument_AsksForSomething(string argument, string value)
	{
		Assert.False(DebugArguments.Parse(new[] { argument, value }).IsEmpty);
	}

	[Theory]
	[InlineData(new[] { "--presets", "a.json" }, "unknown argument '--presets'; the ones the debug tooling reads are '--preset', '--world', '--cycle' and '--do'")]
	[InlineData(new[] { "a.json" }, "unknown argument 'a.json'; the ones the debug tooling reads are '--preset', '--world', '--cycle' and '--do'")]
	[InlineData(new[] { "--world" }, "'--world' needs a value after it")]
	[InlineData(new[] { "--preset", "" }, "'--preset' needs the path of a preset file")]
	[InlineData(new[] { "--preset", "a.json", "--preset", "b.json" }, "'--preset' appears twice; a run starts from one preset file")]
	[InlineData(new[] { "--world", "real", "--world", "fantasy" }, "'--world' appears twice")]
	[InlineData(new[] { "--cycle", "1", "--cycle", "2" }, "'--cycle' appears twice")]
	[InlineData(new[] { "--world", "dream" }, "'dream' is not a world; the worlds are 'real', 'fantasy', 'nightmare'")]
	[InlineData(new[] { "--cycle", "0" }, "'--cycle 0' is not a cycle; cycles are whole numbers from 1")]
	[InlineData(new[] { "--cycle", "-1" }, "'--cycle -1' is not a cycle; cycles are whole numbers from 1")]
	[InlineData(new[] { "--cycle", "two" }, "'--cycle two' is not a cycle; cycles are whole numbers from 1")]
	[InlineData(new[] { "--do", "set test.tonight", "--do", "add test.visits" }, "--do 2: Column 16: expected the amount to add to 'test.visits', but found the end of the text")]
	public void Parse_RejectsWhatItDoesNotUnderstand(string[] arguments, string expected)
	{
		DataFileException error = Assert.Throws<DataFileException>(() => DebugArguments.Parse(arguments));

		Assert.Equal($"command line: {expected}.", error.Message);
	}
}
