using Godot;

/// <summary>
/// Reads a conversation of the game by its id, from
/// <c>res://Dialogue/Conversations/&lt;id&gt;.json</c>, and hands its text to
/// <see cref="ConversationReader"/>.
/// </summary>
/// <remarks>
/// Through <see cref="FileAccess"/>, not <c>System.IO</c>, because in an exported game the
/// file lives inside the <c>.pck</c>. The tests already prove every conversation in that
/// folder is valid, so a failure here means a file was broken without running them, or a
/// scene names a conversation that does not exist.
/// </remarks>
public static class ConversationLoader
{
	/// <summary>The folder every conversation lives in.</summary>
	public const string Folder = "res://Dialogue/Conversations";

	/// <exception cref="System.IO.FileNotFoundException">There is no conversation with that id.</exception>
	/// <exception cref="DataFileException">The file is not a well-formed conversation.</exception>
	public static Conversation Load(string id)
	{
		string path = $"{Folder}/{id}.json";
		string text = FileAccess.GetFileAsString(path);
		Error error = FileAccess.GetOpenError();

		if (error != Error.Ok)
			throw new System.IO.FileNotFoundException($"Could not read the conversation '{id}' at {path}: {error}.", path);

		return ConversationReader.Read(new ConversationFile(path, text));
	}
}
