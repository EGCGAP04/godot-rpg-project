using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Checks every conversation of the project against the flag catalogue and against each
/// other, and returns every problem it finds instead of stopping at the first.
/// </summary>
/// <remarks>
/// <para>
/// The reader already rejects a file that is not well formed. The validator adds what
/// needs more than one file, or more than reading:
/// </para>
/// <list type="bullet">
/// <item>every flag a condition or an effect names is declared in the catalogue, and used with its type;</item>
/// <item>conversation ids are unique, and none is reserved;</item>
/// <item>every node can be reached from the start;</item>
/// <item>
/// the player can never be left stuck: every choice offers an option with no condition,
/// and from every node there is a way to the end that depends on none.
/// </item>
/// </list>
/// <para>
/// With around a hundred endings planned, this is what makes a misspelt flag or a dead
/// end fail the build instead of turning up in play. A test runs it over the whole project.
/// </para>
/// </remarks>
public static class DialogueValidator
{
	/// <summary>Checks the catalogue and the conversations together.</summary>
	/// <param name="catalogueJson">The text of the flag catalogue.</param>
	/// <param name="files">Every conversation file of the project.</param>
	/// <returns>
	/// Every problem found, each a sentence that starts with the file it is in. Empty when
	/// everything is valid.
	/// </returns>
	public static IReadOnlyList<string> Validate(string catalogueJson, IEnumerable<ConversationFile> files)
	{
		ArgumentNullException.ThrowIfNull(files);

		var problems = new List<string>();
		FlagCatalogue catalogue = null;

		try
		{
			catalogue = FlagCatalogue.Read(catalogueJson);
		}
		catch (DataFileException exception)
		{
			// Without a catalogue, every flag would read as undeclared: say it once, here.
			problems.Add(exception.Message);
		}

		List<ConversationFile> fileList = files.ToList();

		foreach (IGrouping<string, ConversationFile> sameId in fileList.GroupBy(file => file.Id).Where(group => group.Count() > 1))
		{
			string paths = string.Join(", ", sameId.Select(file => $"'{file.Path}'"));
			problems.Add($"{sameId.Key}: {sameId.Count()} files have this id, which must be unique: {paths}.");
		}

		var conversations = new List<Conversation>();

		foreach (ConversationFile file in fileList)
		{
			if (DialogueNames.ReservedConversationIds.Contains(file.Id))
			{
				problems.Add($"{file.Id}: this id is reserved for localization keys that are not a conversation's, so '{file.Path}' needs another name.");
				continue;
			}

			try
			{
				conversations.Add(ConversationReader.Read(file));
			}
			catch (DataFileException exception)
			{
				problems.Add(exception.Message);
			}
		}

		foreach (Conversation conversation in conversations)
		{
			if (catalogue != null)
				CheckFlags(conversation, catalogue, problems);

			CheckPaths(conversation, problems);
		}

		return problems;
	}

	private static void CheckFlags(Conversation conversation, FlagCatalogue catalogue, List<string> problems)
	{
		void Check(string where, IEnumerable<(string Name, FlagType Type)> uses, bool inEffect)
		{
			foreach ((string name, FlagType type) in uses)
			{
				string problem = CheckUse(catalogue, name, type, inEffect);

				if (problem != null)
					problems.Add($"{conversation.Id}: {where}: {problem}.");
			}
		}

		foreach (DialogueNode node in conversation.Nodes.Values)
		{
			for (int i = 0; i < node.Lines.Count; i++)
			{
				DialogueLine line = node.Lines[i];
				string where = $"node '{node.Id}', line {i + 1} ('{line.Name}')";
				Check($"{where}, if", Uses(line.Condition), false);

				for (int k = 0; k < line.Effects.Count; k++)
					Check($"{where}, do {k + 1}", new[] { Use(line.Effects[k]) }, true);
			}

			for (int i = 0; i < node.Choices.Count; i++)
			{
				DialogueOption option = node.Choices[i];
				string where = $"node '{node.Id}', option {i + 1} ('{option.Name}')";
				Check($"{where}, if", Uses(option.Condition), false);

				for (int k = 0; k < option.Effects.Count; k++)
					Check($"{where}, do {k + 1}", new[] { Use(option.Effects[k]) }, true);
			}
		}
	}

	// Each flag a condition reads, with the type the reading needs. The tree is plain data,
	// so walking it is a switch over the kinds of node.
	private static IEnumerable<(string Name, FlagType Type)> Uses(FlagCondition condition) => condition switch
	{
		null => Enumerable.Empty<(string, FlagType)>(),
		FlagCondition.IsSet isSet => new[] { (isSet.Flag, FlagType.Bool) },
		FlagCondition.Comparison comparison => new[] { (comparison.Counter, FlagType.Counter) },
		FlagCondition.Not negation => Uses(negation.Operand),
		FlagCondition.And both => Uses(both.Left).Concat(Uses(both.Right)),
		FlagCondition.Or either => Uses(either.Left).Concat(Uses(either.Right)),
		_ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Not a known kind of condition.")
	};

	// The flag an effect writes, with the type the writing needs.
	private static (string Name, FlagType Type) Use(FlagEffect effect) => effect switch
	{
		FlagEffect.Set setting => (setting.Flag, FlagType.Bool),
		FlagEffect.Add addition => (addition.Counter, FlagType.Counter),
		_ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Not a known kind of effect.")
	};

	/// <returns>What is wrong with this use of a flag, or null when nothing is.</returns>
	private static string CheckUse(FlagCatalogue catalogue, string name, FlagType needed, bool inEffect)
	{
		if (!catalogue.Flags.TryGetValue(name, out FlagDeclaration declaration))
			return $"'{name}' is not declared in {FlagCatalogue.FileName}";

		if (declaration.Type == needed)
			return null;

		return (declaration.Type, inEffect) switch
		{
			(FlagType.Counter, false) => $"'{name}' is a counter, so it is compared with a number, as in '{name} >= 1'",
			(FlagType.Bool, false) => $"'{name}' is a boolean flag, so it is tested on its own, without a comparison",
			(FlagType.Counter, true) => $"'{name}' is a counter, so it changes with 'add', not with 'set' or 'clear'",
			_ => $"'{name}' is a boolean flag, so it changes with 'set' or 'clear', not with 'add'"
		};
	}

	private static void CheckPaths(Conversation conversation, List<string> problems)
	{
		// Where a node can go next, and whether going there depends on a condition.
		static IEnumerable<(string Target, bool Conditional)> Exits(DialogueNode node) =>
			node.Next != null
				? new[] { (Target: node.Next, Conditional: false) }
				: node.Choices.Select(option => (Target: option.Next, Conditional: option.Condition != null));

		// The nodes reachable from the start, whatever the conditions.
		var reached = new HashSet<string>();
		var pending = new Stack<string>();
		pending.Push(DialogueNames.Start);

		while (pending.Count > 0)
		{
			string id = pending.Pop();

			if (id == DialogueNames.End || !reached.Add(id))
				continue;

			foreach ((string target, _) in Exits(conversation.Nodes[id]))
				pending.Push(target);
		}

		foreach (string id in conversation.Nodes.Keys.Where(id => !reached.Contains(id)))
			problems.Add($"{conversation.Id}: node '{id}': no jump or option leads here from '{DialogueNames.Start}'.");

		// Every choice must offer an option with no condition.
		HashSet<string> allConditional = conversation.Nodes.Values
			.Where(node => node.Next == null && node.Choices.All(option => option.Condition != null))
			.Select(node => node.Id)
			.ToHashSet();

		foreach (string id in allConditional)
			problems.Add($"{conversation.Id}: node '{id}': every option has a condition, but one must have none, so there is always a way on.");

		// The nodes with a way to the end that depends on no condition: a node with an
		// unconditional exit to the end, or to a node already found. Grown until it stops.
		var canFinish = new HashSet<string>();
		bool grew = true;

		while (grew)
		{
			grew = false;

			foreach (DialogueNode node in conversation.Nodes.Values)
			{
				if (canFinish.Contains(node.Id))
					continue;

				if (Exits(node).Any(exit => !exit.Conditional && (exit.Target == DialogueNames.End || canFinish.Contains(exit.Target))))
				{
					canFinish.Add(node.Id);
					grew = true;
				}
			}
		}

		// A node already reported above is left out, so one mistake is not reported twice.
		List<string> stuck = reached
			.Where(id => !canFinish.Contains(id) && !allConditional.Contains(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();

		if (stuck.Count > 0)
		{
			string nodes = $"{(stuck.Count == 1 ? "node" : "nodes")} {string.Join(", ", stuck.Select(id => $"'{id}'"))}";
			problems.Add($"{conversation.Id}: from {nodes} there is no way to {DialogueNames.End} that does not depend on a condition, so the player could be left with no way out.");
		}
	}
}
