using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GodotRPGProject.Tests;

/// <summary>
/// Finds the Godot project's own files from inside a test run, which happens deep under
/// <c>tests/Game.Tests/bin/</c>, by walking up until a folder holds
/// <c>Game/project.godot</c>.
/// </summary>
public static class ProjectFiles
{
	/// <summary>The project's <c>Game/</c> folder: what the game calls <c>res://</c>.</summary>
	public static string GameDirectory { get; } = FindGameDirectory();

	/// <summary>Reads a file by its path inside <c>Game/</c>, one folder per argument.</summary>
	public static string ReadText(params string[] pathInsideGame) =>
		File.ReadAllText(Path.Combine(GameDirectory, Path.Combine(pathInsideGame)));

	/// <summary>
	/// Every conversation file under <c>Dialogue/Conversations/</c>, in a fixed order. Fails
	/// when the folder is missing, so a wrong path cannot pass as a project with nothing in it.
	/// </summary>
	public static ConversationFile[] ConversationFiles()
	{
		string folder = Path.Combine(GameDirectory, "Dialogue", "Conversations");

		if (!Directory.Exists(folder))
			throw new DirectoryNotFoundException($"There is no conversations folder at {folder}.");

		return Find(folder, "*.json").Select(path => new ConversationFile(InsideGame(path), File.ReadAllText(path))).ToArray();
	}

	/// <summary>Every translation table file in <c>Game/</c>, in a fixed order.</summary>
	public static TranslationFile[] TranslationFiles() =>
		Find(GameDirectory, "*.po").Select(path => new TranslationFile(InsideGame(path), File.ReadAllText(path))).ToArray();

	private static IEnumerable<string> Find(string folder, string pattern) =>
		Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
			.Where(path => !InsideGame(path).StartsWith(".godot/"))
			.OrderBy(path => path, StringComparer.Ordinal);

	/// <summary>A path as the game names it: inside <c>Game/</c>, with forward slashes.</summary>
	private static string InsideGame(string path) => Path.GetRelativePath(GameDirectory, path).Replace('\\', '/');

	private static string FindGameDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
		{
			string game = Path.Combine(directory.FullName, "Game");

			if (File.Exists(Path.Combine(game, "project.godot")))
				return game;
		}

		throw new DirectoryNotFoundException($"No Game/project.godot in any folder above {AppContext.BaseDirectory}.");
	}
}
