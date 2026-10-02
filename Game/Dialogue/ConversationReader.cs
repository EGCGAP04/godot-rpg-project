using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Reads one conversation from the text of its JSON file, or says where the file is
/// wrong.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free: it takes the file's text, not a path. The game reads the file through
/// Godot's <c>FileAccess</c>, which can see inside the packed <c>res://</c> of an
/// exported game, and the tests read it straight from disk.
/// </para>
/// <para>
/// It checks that a conversation is well formed: the right properties with the right
/// types, valid names, conditions and effects that parse, a start node, and jumps to
/// nodes that exist. Checks that need more than this one file, such as whether a flag is
/// declared in the catalogue, belong to the validator.
/// </para>
/// </remarks>
public static class ConversationReader
{
	/// <summary>The version of the conversation format this game reads.</summary>
	public const int Format = 1;

	private static readonly string[] ConversationProperties = { "format", "nodes" };
	private static readonly string[] NodeProperties = { "lines", "choices", "next" };
	private static readonly string[] LineProperties = { "speaker", "line", "if", "do" };
	private static readonly string[] RequiredLineProperties = { "speaker", "line" };
	private static readonly string[] OptionProperties = { "option", "next", "if", "do" };
	private static readonly string[] RequiredOptionProperties = { "option", "next" };

	/// <param name="id">The conversation's id: its file name without the extension.</param>
	/// <param name="json">The text of the file.</param>
	/// <exception cref="DataFileException">The file is not a well-formed conversation.</exception>
	public static Conversation Read(string id, string json)
	{
		if (!DialogueNames.IsWord(id))
			throw new DataFileException(id, "", "a conversation id, which is its file name, must be lowercase letters, digits and underscores, starting with a letter");

		using JsonDocument document = StrictJson.Parse(id, json);
		Dictionary<string, JsonElement> fields = StrictJson.Properties(document.RootElement, id, "", ConversationProperties, ConversationProperties);
		int format = StrictJson.Integer(fields["format"], id, "", "format");

		if (format != Format)
			throw new DataFileException(id, "", $"format {format} is not supported; this game reads format {Format}");

		var nodes = new Dictionary<string, DialogueNode>();

		foreach ((string nodeId, JsonElement element) in StrictJson.Properties(fields["nodes"], id, "nodes", null, Array.Empty<string>()))
			nodes.Add(nodeId, ReadNode(id, nodeId, element));

		if (!nodes.ContainsKey(DialogueNames.Start))
			throw new DataFileException(id, "", $"there is no '{DialogueNames.Start}' node, where every conversation begins");

		// Only now are all the nodes known, so jumps can be checked against them.
		foreach (DialogueNode node in nodes.Values)
		{
			if (node.Next != null)
				CheckJump(id, nodes, $"node '{node.Id}'", node.Next);

			foreach (DialogueOption option in node.Choices)
				CheckJump(id, nodes, $"node '{node.Id}', option '{option.Name}'", option.Next);
		}

		return new Conversation(id, nodes);
	}

	private static DialogueNode ReadNode(string id, string nodeId, JsonElement element)
	{
		string where = $"node '{nodeId}'";
		Word(nodeId, id, where, "node id");
		Dictionary<string, JsonElement> fields = StrictJson.Properties(element, id, where, NodeProperties, Array.Empty<string>());
		bool endsInChoice = fields.ContainsKey("choices");

		if (endsInChoice == fields.ContainsKey("next"))
			throw new DataFileException(id, where, "a node ends in exactly one of 'choices' or 'next'");

		var lines = new List<DialogueLine>();

		if (fields.TryGetValue("lines", out JsonElement lineList))
		{
			List<JsonElement> lineElements = StrictJson.List(lineList, id, where, "lines");

			for (int i = 0; i < lineElements.Count; i++)
				lines.Add(ReadLine(id, nodeId, i + 1, lineElements[i]));
		}

		var choices = new List<DialogueOption>();
		string next = null;

		if (endsInChoice)
		{
			List<JsonElement> optionElements = StrictJson.List(fields["choices"], id, where, "choices");

			if (optionElements.Count == 0)
				throw new DataFileException(id, where, "'choices' has no options");

			for (int i = 0; i < optionElements.Count; i++)
				choices.Add(ReadOption(id, nodeId, i + 1, optionElements[i]));
		}
		else
		{
			next = StrictJson.Text(fields["next"], id, where, "next");
		}

		// Lines and options of a node share the space their keys are made in.
		string repeated = lines.Select(line => line.Name)
			.Concat(choices.Select(option => option.Name))
			.GroupBy(name => name)
			.FirstOrDefault(group => group.Count() > 1)?.Key;

		if (repeated != null)
			throw new DataFileException(id, where, $"'{repeated}' names more than one line or option, so their keys would collide");

		return new DialogueNode(nodeId, lines, choices, next);
	}

	private static DialogueLine ReadLine(string id, string nodeId, int number, JsonElement element)
	{
		string where = $"node '{nodeId}', line {number}";
		Dictionary<string, JsonElement> fields = StrictJson.Properties(element, id, where, LineProperties, RequiredLineProperties);
		string name = Word(StrictJson.Text(fields["line"], id, where, "line"), id, where, "line name");
		where += $" ('{name}')";
		string speaker = Word(StrictJson.Text(fields["speaker"], id, where, "speaker"), id, where, "speaker");

		return new DialogueLine(name, $"{id}.{nodeId}.{name}", speaker, ReadCondition(id, where, fields), ReadEffects(id, where, fields));
	}

	private static DialogueOption ReadOption(string id, string nodeId, int number, JsonElement element)
	{
		string where = $"node '{nodeId}', option {number}";
		Dictionary<string, JsonElement> fields = StrictJson.Properties(element, id, where, OptionProperties, RequiredOptionProperties);
		string name = Word(StrictJson.Text(fields["option"], id, where, "option"), id, where, "option name");
		where += $" ('{name}')";
		string next = StrictJson.Text(fields["next"], id, where, "next");

		return new DialogueOption(name, $"{id}.{nodeId}.{name}", ReadCondition(id, where, fields), ReadEffects(id, where, fields), next);
	}

	private static FlagCondition ReadCondition(string id, string where, Dictionary<string, JsonElement> fields)
	{
		if (!fields.TryGetValue("if", out JsonElement element))
			return null;

		string text = StrictJson.Text(element, id, where, "if");

		try
		{
			return FlagConditionParser.Parse(text);
		}
		catch (FlagSyntaxException exception)
		{
			throw new DataFileException(id, $"{where}, if", exception.Message.TrimEnd('.'));
		}
	}

	private static IReadOnlyList<FlagEffect> ReadEffects(string id, string where, Dictionary<string, JsonElement> fields)
	{
		var effects = new List<FlagEffect>();

		if (!fields.TryGetValue("do", out JsonElement element))
			return effects;

		List<JsonElement> entries = StrictJson.List(element, id, where, "do");

		for (int i = 0; i < entries.Count; i++)
		{
			string effectWhere = $"{where}, do {i + 1}";
			string text = StrictJson.Text(entries[i], id, effectWhere, "do");

			try
			{
				effects.Add(FlagEffectParser.Parse(text));
			}
			catch (FlagSyntaxException exception)
			{
				throw new DataFileException(id, effectWhere, exception.Message.TrimEnd('.'));
			}
		}

		return effects;
	}

	private static void CheckJump(string id, Dictionary<string, DialogueNode> nodes, string where, string target)
	{
		if (target != DialogueNames.End && !nodes.ContainsKey(target))
			throw new DataFileException(id, where, $"jumps to unknown node '{target}'");
	}

	private static string Word(string text, string id, string where, string what)
	{
		if (!DialogueNames.IsWord(text))
			throw new DataFileException(id, where, $"the {what} '{text}' must be lowercase letters, digits and underscores, starting with a letter");

		return text;
	}
}
