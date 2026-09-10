# godot-rpg-project

![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)
![Godot](https://img.shields.io/badge/Godot-4.6.2-478cbf?logo=godotengine&logoColor=white)

2D pixel art turn-based RPG, built in **Godot 4.6.2** with **C#**.

> **Tentative** project name — the final name of the game will be decided later. This repo is used in the meantime as the development project's identifier.

## Project status

🚧 In development — foundation phase (architecture, minimal combat system, first map).

This is a personal learning project, with no deadline, developed following professional version control and code organization standards.

## Tech stack

- **Engine:** Godot 4.6.2
- **Language:** C# (.NET)
- **Genre:** Turn-based RPG, with possible real-time combat exceptions later on
- **Base resolution:** 640x360
- **Tile size:** 16x16 px

## Requirements to run the project

- [Godot 4.6.2 (.NET/Mono version)](https://godotengine.org/download)
- [.NET SDK](https://dotnet.microsoft.com/download) compatible with the Godot version used

## How to open the project

1. Clone the repository
2. Open Godot and select "Import" on the `project.godot` file from the cloned folder
3. Build the C# project from the editor (Project > Tools > C# > Create C# Solution, if it isn't generated automatically)

## Project Structure
 
The project follows a **feature-based** folder structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type.
 
```text
res://
├── Player/          # Player character: movement, sprite, input handling
├── Combat/          # Turn-based combat system: turn order, actions, enemies
├── World/           # Exploration maps: tilemaps, collisions, transitions
├── UI/               # Menus, HUD, dialogue boxes
├── Shared/
│   ├── Fonts/        # Shared fonts used across the project
│   ├── Scripts/      # Generic utilities, extensions, helper classes
│   └── Theme/        # Global UI theme resources
└── Autoloads/        # Global singletons (empty for now, e.g. future GameManager)
```
 
For architectural conventions (naming, node communication patterns, scene structure), see [docs/CONVENTIONS.md](docs/CONVENTIONS.md).

## Screenshots

_(Will be added once there's playable content)_

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
