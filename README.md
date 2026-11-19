# godot-rpg-project

[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue.svg)](LICENSE)
![Godot](https://img.shields.io/badge/Godot-4.6.2-478cbf?logo=godotengine&logoColor=white)
[![Release](https://img.shields.io/github/v/release/EGCGAP04/godot-rpg-project?logo=github&color=success)](https://github.com/EGCGAP04/godot-rpg-project/releases/latest)

2D pixel art turn-based RPG, built in **Godot 4.6.2** with **C#**.

> **Tentative** project name — the final name of the game will be decided later. This repo is used in the meantime as the development project's identifier.

## Project status

🚧 In development — core systems phase.

The cycle is playable end to end: walk through the Real, Fantasy and Nightmare worlds in turn, move between them, and watch the cycle counter advance. Touching an enemy starts a turn-based fight of the whole party against a group of enemies: each round every member is given an attack and a target, the round plays out in a log, and a fight won gives experience to the members still standing. In the Real world, a neighbor can be talked to by facing it and pressing `confirm`; one answer skips that night's dream, and the next morning remembers which answer was given. A friend there joins the party if asked, and a newcomer in the Fantasy world offers to take the friend's place. While exploring, `open_menu` (Tab) opens a status screen with each party member's level and base stats in the current world. Every text is in Spanish and English, and the game starts in the system's language. In a debug build, F3 opens an overlay to inspect and change the decision registry's flags, take and restore snapshots of them and of the party, change the party and see its derived stats, jump to any world and cycle, and switch the language. All art is placeholder (solid-colour rectangles and tinted tiles).

This is a personal learning project, with no deadline, developed following professional version control and code organization standards.

## Tech stack

- **Engine:** Godot 4.6.2
- **Language:** C# (.NET 8)
- **Genre:** Turn-based RPG, with possible real-time combat exceptions later on
- **Base resolution:** 640x360, in a window that opens at twice that size (1280x720); everything is laid out at 640x360 and drawn at the window's size, so text stays sharp
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

The rules that do not need the engine — combat, the world/cycle progression, movement direction, the dialogue format, the translation tables, the decision registry, the stat formulas, how characters grow and level up, the party, and what the status screen shows — are covered by unit tests that run without it. Some of them check every conversation, every translation table, every data file and every character in the project, so a broken conversation, a missing translation, a misspelt flag or a character with a misspelt stat fails the build. The solution includes the test project, so from the same `Game/` folder:

```bash
dotnet test "Godot RPG Project.sln" --configuration Debug
```

CI runs this on every pull request into `main` and every push to it, along with `dotnet format --verify-no-changes`, so a formatting mismatch fails the build too. CodeQL also scans the C# code and the workflows on those same events and once a week, and reports what it finds as code scanning alerts rather than failing the build.

In a debug build, F3 opens a debug overlay that shows every flag of the decision registry and lets them be changed, takes and restores snapshots, changes the party and shows its derived stats, jumps to any world and cycle, and switches the language. A run can also start in a chosen state from the command line, with a preset from `Game/Debug/Presets/`; the *Debug Tooling* section of [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) has the syntax:

```bash
godot --path . -- --preset res://Debug/Presets/no_fantasy.json
godot --path . -- --preset res://Debug/Presets/with_friend.json
```

## Project Structure

The project follows a **feature-based** folder structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type. The Godot project itself lives inside `Game/`, keeping the repository root limited to project metadata (README, LICENSE, CHANGELOG, security policy, docs, GitHub config).

```text
godot-rpg-project/
├── .github/            # Issue/PR templates, CI, Dependabot
├── Game/               # res:// — the Godot project
│   ├── Autoloads/      # global singletons: game state, decisions, party, debug tools
│   ├── Characters/     # the characters, as resources
│   ├── Combat/         # the fight overlay and its rules
│   ├── Debug/          # the debug overlay and presets; debug builds only
│   ├── Decisions/      # the decision registry and its flag catalogue
│   ├── Dialogue/       # the dialogue format, its runner and its validators
│   ├── Enemy/          # enemies, their groups and their data
│   ├── Localization/   # translation tables, in English and Spanish
│   ├── Party/          # the party's state
│   ├── Player/         # the player and 4-directional movement
│   ├── Shared/         # cross-feature scripts, fonts and theme
│   ├── Stats/          # stat formulas, growth and experience
│   ├── UI/             # dialogue box, status screen, screen fade
│   └── World/          # the three worlds and what stands in them
├── docs/               # conventions and the full file tree
└── tests/              # unit tests, outside res:// on purpose
    └── Game.Tests/
```

[docs/PROJECT_STRUCTURE.md](docs/PROJECT_STRUCTURE.md) lists every file. For architectural conventions (naming, node communication patterns, scene structure), see [docs/CONVENTIONS.md](docs/CONVENTIONS.md). The [wiki](https://github.com/EGCGAP04/godot-rpg-project/wiki) gathers, on one page, links to every part of the documentation and to the milestones and releases.

## Screenshots

_(Will be added once the game has real art: everything on screen is still a placeholder)_

## Security

Please don't report a vulnerability in a public Issue: [SECURITY.md](SECURITY.md) explains how to report it privately.

## License

This project is **source-available, not open source**. It is licensed under the [PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0) — see the [LICENSE](LICENSE) file for the full terms.

- **Noncommercial use is allowed:** you may read, run, modify and share it for any noncommercial purpose, such as personal use, study or research.
- **Attribution is required:** every copy you pass on, changed or not, must include the license's terms and the `Required Notice` line at the top of the [LICENSE](LICENSE) file.
- **Commercial use needs the maintainer's permission.** To ask for it, write to <ea.aguirrepolenta@gmail.com>.

Everything published before this change, the versions up to `v0.4.0` included, was released under the MIT License and stays available under it.

### Contributing

By opening a pull request, you agree that your contribution is licensed under the same terms, and you grant the maintainer the right to license it under other terms as well. That keeps the maintainer able to give permission for the whole project.
