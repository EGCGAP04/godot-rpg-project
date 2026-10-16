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
/// <item>conversation ids are unique;</item>
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
/// <para>
/// <see cref="ValidateText"/> checks the other half: that every line, option and speaker
/// has its text in the translation tables. The tables themselves are
/// <see cref="LocalizationValidator"/>'s.
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

	/// <summary>
	/// Checks that every conversation has its text: a table beside it, holding a text for
	/// each of its lines and options and nothing else, and a name for each of its speakers.
	/// </summary>
	/// <remarks>
	/// Keys are looked up in the <see cref="LocalizationNames.Fallback"/> language only.
	/// <see cref="LocalizationValidator"/> already checks that every language has the same
	/// keys, so checking each one here would report a missing text once per language. A
	/// conversation or a table that cannot be read is skipped: <see cref="Validate"/> and
	/// <see cref="LocalizationValidator"/> report those.
	/// </remarks>
	/// <param name="files">Every conversation file of the project.</param>
	/// <param name="tables">Every translation table file of the project.</param>
	/// <returns>Every problem found, each a sentence that starts with the file it is in.</returns>
	public static IReadOnlyList<string> ValidateText(IEnumerable<ConversationFile> files, IEnumerable<TranslationFile> tables)
	{
		ArgumentNullException.ThrowIfNull(files);
		ArgumentNullException.ThrowIfNull(tables);

		var problems = new List<string>();
		List<ConversationFile> fileList = files.ToList();
		List<TranslationFile> tableList = tables.OrderBy(table => table.Path, StringComparer.Ordinal).ToList();

		// The shared tables' names begin keys that are not a conversation's, so no
		// conversation may take one: its keys would mix with theirs.
		HashSet<string> sharedNames = tableList
			.Where(table => table.Folder == LocalizationNames.SharedFolder)
			.Select(table => table.Name)
			.ToHashSet();

		// With no speaker table at all, every speaker is missing its name. One that is there
		// but cannot be read is LocalizationValidator's to report, so it checks nothing.
		bool hasSpeakerTable = tableList.Any(table => table.Folder == LocalizationNames.SharedFolder && table.Name == LocalizationNames.SpeakerTable && table.Locale == LocalizationNames.Fallback);
		TranslationTable speakers = hasSpeakerTable
			? ReadFallback(tableList, LocalizationNames.SharedFolder, LocalizationNames.SpeakerTable)
			: new TranslationTable(LocalizationNames.Fallback, Array.Empty<TranslationEntry>());

		foreach (ConversationFile file in fileList)
		{
			string folder = (System.IO.Path.GetDirectoryName(file.Path) ?? "").Replace('\\', '/');

			if (sharedNames.Contains(file.Id))
			{
				problems.Add($"{file.Id}: this id is the name of the tables in {LocalizationNames.SharedFolder}/, which begin keys that are not a conversation's, so '{file.Path}' needs another name.");
				continue;
			}

			Conversation conversation;

			try
			{
				conversation = ConversationReader.Read(file);
			}
			catch (DataFileException)
			{
				continue;
			}

			if (!tableList.Any(table => table.Folder == folder && table.Name == file.Id))
			{
				problems.Add($"{file.Id}: there is no table beside '{file.Path}'; its text goes in {file.Id}.<locale>.po, in the same folder.");
				continue;
			}

			TranslationTable table = ReadFallback(tableList, folder, file.Id);

			if (table != null)
				CheckKeys(conversation, table, $"{folder}/{file.Id}.{LocalizationNames.Fallback}.po", problems);

			if (speakers != null)
				CheckSpeakers(conversation, speakers, problems);
		}

		HashSet<(string Folder, string Id)> conversations = fileList
			.Select(file => ((System.IO.Path.GetDirectoryName(file.Path) ?? "").Replace('\\', '/'), file.Id))
			.ToHashSet();

		foreach ((string folder, string name) in tableList
			.Where(table => table.Folder != LocalizationNames.SharedFolder)
			.Select(table => (table.Folder, table.Name))
			.Distinct())
		{
			if (!conversations.Contains((folder, name)))
				problems.Add($"{folder}/{name}: a table outside {LocalizationNames.SharedFolder}/ holds a conversation's text, and there is no conversation called '{name}' beside it.");
		}

		return problems;
	}

	/// <summary>The table in the fallback language, or null when it is missing or cannot be read.</summary>
	private static TranslationTable ReadFallback(List<TranslationFile> tables, string folder, string name)
	{
		foreach (TranslationFile file in tables)
		{
			if (file.Folder != folder || file.Name != name || file.Locale != LocalizationNames.Fallback)
				continue;

			try
			{
				return TranslationTableReader.Read(file);
			}
			catch (DataFileException)
			{
				return null;
			}
		}

		return null;
	}

	private static void CheckKeys(Conversation conversation, TranslationTable table, string tablePath, List<string> problems)
	{
		var used = new HashSet<string>();

		foreach (DialogueNode node in conversation.Nodes.Values)
		{
			foreach (string key in node.Lines.Select(line => line.Key).Concat(node.Choices.Select(option => option.Key)))
			{
				used.Add(key);

				if (table.Find(key) == null)
					problems.Add($"{conversation.Id}: '{key}' has no text in {tablePath}.");
			}
		}

		// Moving a line to another node changes its key, which leaves its old text here.
		foreach (TranslationEntry entry in table.Entries.Where(entry => !used.Contains(entry.Key)))
			problems.Add($"{tablePath}: line {entry.Line}: '{entry.Key}' is not the key of any line or option of {conversation.Id}.");
	}

	private static void CheckSpeakers(Conversation conversation, TranslationTable speakers, List<string> problems)
	{
		IEnumerable<string> named = conversation.Nodes.Values
			.SelectMany(node => node.Lines)
			.Where(line => !line.IsNarration)
			.Select(line => line.Speaker)
			.Distinct();

		foreach (string speaker in named)
		{
			string key = $"{LocalizationNames.SpeakerTable}.{speaker}";

			if (speakers.Find(key) == null)
				problems.Add($"{conversation.Id}: the speaker '{speaker}' has no name: '{key}' is not in {LocalizationNames.SharedFolder}/{LocalizationNames.SpeakerTable}.{LocalizationNames.Fallback}.po.");
		}
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
