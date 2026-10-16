using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace GodotRPGProject.Tests;

/// <summary>
/// Exercises <see cref="TextTemplate"/>: how a translated text's named placeholders are
/// filled, and what is left alone.
/// </summary>
public class TextTemplateTests
{
	[Fact]
	public void Fill_ReplacesEachPlaceholderWithItsValue()
	{
		Assert.Equal("Enemy   7/10", TextTemplate.Fill("{name}   {hp}/{max_hp}", ("name", "Enemy"), ("hp", 7), ("max_hp", 10)));
	}

	[Fact]
	public void Fill_TakesThePlaceholdersInWhateverOrderTheLanguageWritesThem()
	{
		Assert.Equal("Ciclo 2 de Fantasía", TextTemplate.Fill("Ciclo {cycle} de {world}", ("world", "Fantasía"), ("cycle", 2)));
	}

	[Fact]
	public void Fill_ReplacesARepeatedPlaceholderEveryTime()
	{
		Assert.Equal("7 and 7", TextTemplate.Fill("{hp} and {hp}", ("hp", 7)));
	}

	[Fact]
	public void Fill_LeavesAPlaceholderWithNoValueShowing()
	{
		Assert.Equal("Enemy   {hp}/10", TextTemplate.Fill("{name}   {hp}/{max_hp}", ("name", "Enemy"), ("max_hp", 10)));
	}

	[Fact]
	public void Fill_IgnoresAValueNoPlaceholderUses()
	{
		Assert.Equal("7", TextTemplate.Fill("{hp}", ("hp", 7), ("unused", 1)));
	}

	[Fact]
	public void Fill_LeavesAPseudolocalizedPlaceholderShowing()
	{
		// What Godot's pseudolocalization makes of "[{hp}]": the placeholder is no longer a
		// word, so it is text.
		Assert.Equal("[{h̀ṕ}]", TextTemplate.Fill("[{h̀ṕ}]", ("hp", 7)));
	}

	[Theory]
	[InlineData("{", "{")]
	[InlineData("}", "}")]
	[InlineData("{}", "{}")]
	[InlineData("{Hp}", "{Hp}")]
	[InlineData("{max hp}", "{max hp}")]
	[InlineData("{2hp}", "{2hp}")]
	[InlineData("{{hp}}", "{7}")]
	[InlineData("{a {hp}", "{a 7")]
	[InlineData("{hp", "{hp")]
	public void Fill_TreatsAnyOtherBraceAsText(string template, string expected)
	{
		Assert.Equal(expected, TextTemplate.Fill(template, ("hp", 7)));
	}

	[Fact]
	public void Fill_WritesNumbersTheSameWhateverTheSystemCulture()
	{
		CultureInfo before = CultureInfo.CurrentCulture;

		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("es-ES");
			Assert.Equal("1.5 / 1000", TextTemplate.Fill("{a} / {b}", ("a", 1.5), ("b", 1000)));
		}
		finally
		{
			CultureInfo.CurrentCulture = before;
		}
	}

	[Fact]
	public void Fill_WritesANullValueAsNothing()
	{
		Assert.Equal("[]", TextTemplate.Fill("[{name}]", ("name", null)));
	}

	[Fact]
	public void Fill_RefusesTheSameNameTwice()
	{
		var exception = Assert.Throws<ArgumentException>(() => TextTemplate.Fill("{hp}", ("hp", 7), ("hp", 8)));

		Assert.StartsWith("'hp' is given more than once.", exception.Message);
	}

	[Fact]
	public void Fill_WithNoPlaceholders_ReturnsTheTextUnchanged()
	{
		Assert.Equal("Atacar", TextTemplate.Fill("Atacar"));
	}

	[Theory]
	[InlineData("Atacar", new string[0])]
	[InlineData("{name}   {hp}/{max_hp}", new[] { "hp", "max_hp", "name" })]
	[InlineData("{hp} and {hp}", new[] { "hp" })]
	[InlineData("{{hp}} {Hp} {} {max hp}", new[] { "hp" })]
	public void Placeholders_NamesEachPlaceholderOnce(string template, string[] expected)
	{
		Assert.Equal(expected, TextTemplate.Placeholders(template).Order());
	}
}
