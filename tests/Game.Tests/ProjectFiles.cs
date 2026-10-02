using System;
using System.IO;

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
