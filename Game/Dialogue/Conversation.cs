using System.Collections.Generic;

/// <summary>
/// One conversation, as <see cref="ConversationReader"/> builds it from its JSON file.
/// </summary>
/// <remarks>
/// Only the reader builds these, and it guarantees that the start node and every node a
/// jump names exist, so whatever walks a conversation never has to check. The four types
/// in this file are records for brevity; their lists compare by reference, so compare
/// their members rather than whole records.
/// </remarks>
/// <param name="Id">The conversation's id: its file name without the extension.</param>
public sealed record Conversation(string Id, IReadOnlyDictionary<string, DialogueNode> Nodes)
{
	/// <summary>The node the conversation begins at.</summary>
	public DialogueNode Start => Nodes[DialogueNames.Start];
}

/// <summary>A block of lines read in order, ending in either a choice or a jump.</summary>
/// <param name="Choices">The options shown after the last line. Empty when the node jumps instead.</param>
/// <param name="Next">
/// Where the node jumps after its last line: a node id or <see cref="DialogueNames.End"/>.
/// Null when the node ends in a choice instead.
/// </param>
public sealed record DialogueNode(string Id, IReadOnlyList<DialogueLine> Lines, IReadOnlyList<DialogueOption> Choices, string Next);

/// <summary>
/// One line. Its text is not here: <see cref="Key"/> finds it in the localization tables.
/// </summary>
/// <param name="Name">The short name written in the file.</param>
/// <param name="Key">The conversation, the node and the name, joined by dots.</param>
/// <param name="Speaker">Who says it, or <see cref="DialogueNames.Narrator"/> for narration.</param>
/// <param name="Condition">The line shows only when this holds. Null when it always shows.</param>
/// <param name="Effects">Applied in order when the line shows.</param>
public sealed record DialogueLine(string Name, string Key, string Speaker, FlagCondition Condition, IReadOnlyList<FlagEffect> Effects)
{
	/// <summary>Whether nobody says the line, so the dialogue box shows no name.</summary>
	public bool IsNarration => Speaker == DialogueNames.Narrator;
}

/// <summary>One option of a choice. Its text is found by <see cref="Key"/>, like a line's.</summary>
/// <param name="Condition">The option is offered only when this holds. Null when it always is.</param>
/// <param name="Effects">Applied in order when the option is chosen.</param>
/// <param name="Next">The node the option leads to, or <see cref="DialogueNames.End"/>.</param>
public sealed record DialogueOption(string Name, string Key, FlagCondition Condition, IReadOnlyList<FlagEffect> Effects, string Next);
