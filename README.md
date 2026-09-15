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

The combat rules are covered by unit tests, which run without the engine. The solution includes the test project, so from the same `Game/` folder:

```bash
dotnet test "Godot RPG Project.sln" --configuration Debug
```

CI runs this on every push and pull request, along with `dotnet format --verify-no-changes`, so a formatting mismatch fails the build too.

## Project Structure

The project follows a **feature-based** folder structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type. The Godot project itself lives inside `Game/`, keeping the repository root limited to project metadata (README, LICENSE, CHANGELOG, docs, GitHub config).

```text
godot-rpg-project/
├── .github/                    # Issue/PR templates, CI, Dependabot
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.yml
│   │   ├── config.yml
│   │   └── feature.yml
│   ├── workflows/
│   │   └── build.yml           # restore → build → test → format
│   ├── dependabot.yml
│   └── pull_request_template.md
├── Game/                       # res:// — the Godot project
│   ├── Autoloads/
│   │   └── PlayerStats.cs (+.uid)      # global singleton: player HP and attack
│   ├── Combat/
│   │   ├── Combat.cs (+.uid)           # turn state, timers, input, UI
│   │   ├── CombatResolver.cs (+.uid)   # engine-free, covered by tests
│   │   └── combat.tscn                 # overlay scene, not a scene swap
│   ├── Enemy/
│   │   ├── Enemy.cs (+.uid)
│   │   ├── EnemyData.cs (+.uid)        # [GlobalClass] Resource holding stats
│   │   ├── enemy.tscn
│   │   ├── strong_enemy.tres           # 40 HP, 7 atk — the player loses
│   │   └── weak_enemy.tres             # 10 HP, 3 atk — the player wins
│   ├── Player/
│   │   ├── Player.cs (+.uid)
│   │   └── player.tscn
│   ├── Shared/                 # Fonts/ Scripts/ Theme/ — .gitkeep only so far
│   ├── UI/                     # empty so far
│   ├── World/
│   │   ├── WorldMap.cs (+.uid)
│   │   ├── floor_placeholder.png (+.import)
│   │   ├── wall_placeholder.png (+.import)
│   │   ├── tileset.tres
│   │   └── world_map.tscn              # test room, PlayerSpawn marker
│   ├── Godot RPG Project.csproj
│   ├── Godot RPG Project.sln   # also covers Game.Tests
│   ├── Main.cs (+.uid)         # coordinates the map and the combat overlay
│   ├── icon.svg (+.import)
│   ├── main.tscn               # entry point scene
│   └── project.godot           # autoloads, input map, rendering
├── docs/
│   └── CONVENTIONS.md
├── tests/                      # outside res:// on purpose — see CONVENTIONS.md
│   └── Game.Tests/
│       ├── CombatResolverTests.cs
│       └── Game.Tests.csproj
├── .editorconfig
├── .gitattributes
├── .gitignore
├── CHANGELOG.md
├── LICENSE
└── README.md
```

`.cs (+.uid)` means Godot's generated `.cs.uid` file sits next to the script and is committed with it.

For architectural conventions (naming, node communication patterns, scene structure), see [docs/CONVENTIONS.md](docs/CONVENTIONS.md).

## Screenshots

_(Will be added once there's playable content)_

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
