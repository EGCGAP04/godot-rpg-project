using System;

/// <summary>
/// One of the game's data files is malformed: a conversation or the flag catalogue. The
/// message says which file, where in it, and what is wrong, in that order.
/// </summary>
public sealed class DataFileException : Exception
{
	/// <param name="file">Which file: a conversation's id, or the catalogue's file name.</param>
	/// <param name="location">
	/// Where in it, such as "node 'start', line 2 ('greet')". Empty when the problem is the
	/// file as a whole.
	/// </param>
	/// <param name="problem">What is wrong, as the end of a sentence.</param>
	public DataFileException(string file, string location, string problem)
		: base(location.Length == 0 ? $"{file}: {problem}." : $"{file}: {location}: {problem}.")
	{
		File = file;
		Location = location;
	}

	/// <summary>Which file: a conversation's id, or the catalogue's file name.</summary>
	public string File { get; }

	/// <summary>Where in the file, or empty when the problem is the file as a whole.</summary>
	public string Location { get; }
}
