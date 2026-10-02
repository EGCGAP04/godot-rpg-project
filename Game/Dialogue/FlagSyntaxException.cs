using System;

/// <summary>
/// The text of a dialogue condition or effect is not valid. <see cref="Column"/>
/// points at the problem, so an error can say exactly where it is.
/// </summary>
public sealed class FlagSyntaxException : Exception
{
	/// <param name="problem">What is wrong, as the end of a sentence: "expected a number after '&gt;='".</param>
	/// <param name="column">The 1-based column where the problem starts.</param>
	public FlagSyntaxException(string problem, int column)
		: base($"Column {column}: {problem}.")
	{
		Column = column;
	}

	/// <summary>The 1-based column where the problem starts.</summary>
	public int Column { get; }
}
