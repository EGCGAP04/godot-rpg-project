# godot-rpg-project

![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)
![Godot](https://img.shields.io/badge/Godot-4.6.2-478cbf?logo=godotengine&logoColor=white)

2D pixel art turn-based RPG, built in **Godot 4.6.2** with **C#**.

> **Tentative** project name — the final name of the game will be decided later. This repo is used in the meantime as the development project's identifier.

## Project status

🚧 In development — foundation phase.

The core loop is playable end to end: walk around a test map, touch an enemy to start a turn-based fight, and win or lose with consequences. All art is placeholder (solid-colour rectangles and tiles).

This is a personal learning project, with no deadline, developed following professional version control and code organization standards.

## Tech stack

- **Engine:** Godot 4.6.2
- **Language:** C# (.NET 8)
- **Genre:** Turn-based RPG, with possible real-time combat exceptions later on
- **Base resolution:** 640x360
- **Tile size:** 16x16 px

## Requirements to run the project

- [Godot 4.6.2 (.NET/Mono version)](https://godotengine.org/download)
- [.NET SDK 8.0](https://dotnet.microsoft.com/download) or newer

## How to open the project

1. Clone the repository
2. Open Godot and select "Import" on the `Game/project.godot` file from the cloned repository
3. Build the C# solution (the `.sln` and `.csproj` are committed, so there's no need to generate them)

The project can also be built from the command line, from the `Game/` folder:

```bash
dotnet build --configuration Debug
```

Use `Debug`: Godot-generated solutions don't define a `Release` configuration, only `Debug`, `ExportDebug` and `ExportRelease`.

## Project Structure

The project follows a **feature-based** folder structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type. The Godot project itself lives inside `Game/`, keeping the repository root limited to project metadata (README, LICENSE, CHANGELOG, docs, GitHub config).

```text
Game/ (res://)
├── project.godot     # Project settings: autoloads, input map, rendering
├── main.tscn         # Entry point scene; Main.cs coordinates map and combat
├── Player/           # Player character: movement, sprite, input handling
├── Enemy/            # Enemy scenes and stats, contact detection on the map
├── Combat/           # Turn-based combat system: turn order, actions
├── World/            # Exploration maps: tilemaps, collisions, transitions
├── UI/               # Menus, HUD, dialogue boxes
├── Shared/
│   ├── Fonts/        # Shared fonts used across the project
│   ├── Scripts/      # Generic utilities, extensions, helper classes
│   └── Theme/        # Global UI theme resources
└── Autoloads/        # Global singletons (e.g. PlayerStats)
```

For architectural conventions (naming, node communication patterns, scene structure), see [docs/CONVENTIONS.md](docs/CONVENTIONS.md).

## Screenshots

_(Will be added once there's playable content)_

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
