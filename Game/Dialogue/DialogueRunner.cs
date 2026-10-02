using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a running conversation is showing.</summary>
public enum DialogueStep
{
	/// <summary>A line, in <see cref="DialogueRunner.CurrentLine"/>. Continue with <see cref="DialogueRunner.Advance"/>.</summary>
	Line,

	/// <summary>A choice, in <see cref="DialogueRunner.Options"/>. Continue with <see cref="DialogueRunner.Choose"/>.</summary>
	Choice,

	/// <summary>Nothing: the conversation is over.</summary>
	End
}

/// <summary>
/// Walks one conversation a step at a time: it shows a line, offers a choice, or ends.
/// </summary>
/// <remarks>
/// <para>
/// Engine-free and passive. Whoever drives it, the dialogue box or a test, asks which
/// <see cref="Step"/> it is at, shows that, and calls <see cref="Advance"/> or
/// <see cref="Choose"/>. It never waits, draws or reads input itself.
/// </para>
/// <para>
/// Conditions are evaluated when they are reached, not when the conversation starts, so
/// the effects of earlier lines already count. A line's effects apply when it is shown and
/// an option's when it is chosen; a hidden line or option applies nothing.
/// </para>
/// </remarks>
public sealed class DialogueRunner
{
	private static readonly IReadOnlyList<DialogueOption> NoOptions = Array.Empty<DialogueOption>();

	private readonly Conversation _conversation;
	private readonly IFlagStore _flags;

	// Where the conversation is: a node, and the index of the line shown in it.
	private DialogueNode _node;
	private int _lineIndex;

	private DialogueRunner(Conversation conversation, IFlagStore flags)
	{
		_conversation = conversation;
		_flags = flags;
	}

	/// <summary>What the conversation is showing.</summary>
	public DialogueStep Step { get; private set; }

	/// <summary>The line being shown. Null unless <see cref="Step"/> is <see cref="DialogueStep.Line"/>.</summary>
	public DialogueLine CurrentLine { get; private set; }

	/// <summary>
	/// The options offered, in the file's order, leaving out those whose condition does not
	/// hold. Empty unless <see cref="Step"/> is <see cref="DialogueStep.Choice"/>.
	/// </summary>
	public IReadOnlyList<DialogueOption> Options { get; private set; } = NoOptions;

	/// <summary>
	/// Starts <paramref name="conversation"/> at its start node and runs to the first thing
	/// it shows, applying that line's effects if it is a line.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The conversation cannot show anything, which a conversation the validator accepted
	/// never does. See <see cref="Advance"/>.
	/// </exception>
	public static DialogueRunner Start(Conversation conversation, IFlagStore flags)
	{
		ArgumentNullException.ThrowIfNull(conversation);
		ArgumentNullException.ThrowIfNull(flags);

		var runner = new DialogueRunner(conversation, flags);
		runner.GoTo(DialogueNames.Start);
		return runner;
	}

	/// <summary>Moves past the line being shown, to whatever the conversation shows next.</summary>
	/// <exception cref="InvalidOperationException">
	/// The conversation is not showing a line. Also thrown if what follows cannot be shown:
	/// a choice with no option available, or jumps going round in a loop of nodes that show
	/// nothing. The validator rejects both, so only an unvalidated conversation gets here.
	/// </exception>
	public void Advance()
	{
		if (Step != DialogueStep.Line)
			throw new InvalidOperationException($"Advance moves past a line, but the conversation is at {Describe(Step)}.");

		ShowNext();
	}

	/// <summary>Picks one of the offered options, by name, applies its effects and follows it.</summary>
	/// <exception cref="InvalidOperationException">
	/// The conversation is not offering a choice, or what follows cannot be shown, as with
	/// <see cref="Advance"/>.
	/// </exception>
	/// <exception cref="ArgumentException"><paramref name="option"/> is not one of <see cref="Options"/>.</exception>
	public void Choose(string option)
	{
		if (Step != DialogueStep.Choice)
			throw new InvalidOperationException($"Choose picks an option, but the conversation is at {Describe(Step)}.");

		DialogueOption chosen = Options.FirstOrDefault(offered => offered.Name == option)
			?? throw new ArgumentException($"'{option}' is not one of the options offered: {string.Join(", ", Options.Select(offered => offered.Name))}.", nameof(option));

		Apply(chosen.Effects);
		GoTo(chosen.Next);
	}

	private void GoTo(string target)
	{
		if (target == DialogueNames.End)
		{
			Finish();
			return;
		}

		_node = _conversation.Nodes[target];
		_lineIndex = -1;
		ShowNext();
	}

	/// <summary>
	/// Shows the next line of the node whose condition holds. Past the last one, it does
	/// what ends the node: offers its choice, ends the conversation, or jumps and goes on
	/// looking in the next node.
	/// </summary>
	private void ShowNext()
	{
		// Each turn of this loop follows one jump. Jumping through more nodes than the
		// conversation has, without showing anything, means going round in a loop.
		for (int jumps = 0; ; jumps++)
		{
			if (jumps > _conversation.Nodes.Count)
				throw new InvalidOperationException($"Conversation '{_conversation.Id}' jumps round in a loop of nodes that show nothing, through node '{_node.Id}'.");

			while (++_lineIndex < _node.Lines.Count)
			{
				DialogueLine line = _node.Lines[_lineIndex];

				if (Holds(line.Condition))
				{
					Show(line);
					return;
				}
			}

			if (_node.Next == null)
			{
				Offer(_node.Choices.Where(option => Holds(option.Condition)).ToList());
				return;
			}

			if (_node.Next == DialogueNames.End)
			{
				Finish();
				return;
			}

			_node = _conversation.Nodes[_node.Next];
			_lineIndex = -1;
		}
	}

	private void Show(DialogueLine line)
	{
		Apply(line.Effects);
		Step = DialogueStep.Line;
		CurrentLine = line;
		Options = NoOptions;
	}

	private void Offer(IReadOnlyList<DialogueOption> options)
	{
		if (options.Count == 0)
			throw new InvalidOperationException($"Conversation '{_conversation.Id}' reaches the choice in node '{_node.Id}' with no option available.");

		Step = DialogueStep.Choice;
		CurrentLine = null;
		Options = options;
	}

	private void Finish()
	{
		Step = DialogueStep.End;
		CurrentLine = null;
		Options = NoOptions;
	}

	private bool Holds(FlagCondition condition) => condition == null || condition.Evaluate(_flags);

	private void Apply(IReadOnlyList<FlagEffect> effects)
	{
		foreach (FlagEffect effect in effects)
			effect.Apply(_flags);
	}

	private static string Describe(DialogueStep step) => step switch
	{
		DialogueStep.Line => "a line",
		DialogueStep.Choice => "a choice",
		_ => "its end"
	};
}
