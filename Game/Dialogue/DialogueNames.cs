/// <summary>
/// The naming rules of the dialogue format in one place: what a valid id or name looks
/// like, and the few names with a meaning of their own.
/// </summary>
public static class DialogueNames
{
	/// <summary>The node every conversation begins at.</summary>
	public const string Start = "start";

	/// <summary>Not a node: jumping here ends the conversation. The only name in capitals.</summary>
	public const string End = "END";

	/// <summary>The speaker of narration, which the dialogue box shows without a name.</summary>
	public const string Narrator = "narrator";

	/// <summary>
	/// Whether <paramref name="text"/> is a word of the format: lowercase letters, digits
	/// and underscores, starting with a letter. Conversation ids, node ids, line and
	/// option names and speakers are all words.
	/// </summary>
	public static bool IsWord(string text)
	{
		if (string.IsNullOrEmpty(text) || !char.IsAsciiLetterLower(text[0]))
			return false;

		foreach (char character in text)
		{
			if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '_')
				return false;
		}

		return true;
	}
}
