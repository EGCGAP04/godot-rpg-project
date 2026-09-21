# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- GitHub release badge in the README, linking to the latest release
- `World` enum (Real, Fantasy, Nightmare) naming the three worlds a cycle runs through, in the order they are played
- `CycleProgression`, an engine-free class deciding which world follows the one being played and when the cycle number advances, taking the unlocked worlds as an input so a world can be skipped
- `WorldUnlocks` and `CyclePosition`, the engine-free value types `CycleProgression` reads and returns
- `GameState` Autoload singleton exposing the current world and cycle, advancing them through `CycleProgression`, and emitting `WorldChanged` and `CycleChanged`
- Unit tests covering the full three-world sequence, the cycle wrap, and skipping either or both optional worlds

## [0.1.0] - 2026-09-14

### Added

- Initial repository setup: README, LICENSE (MIT), .gitignore for Godot 4 + C#/.NET
- Branch protection, PR/issue templates, labels, milestones and GitHub Project board
- Godot project initialized (Compatibility renderer, 640x360 base resolution, 16x16 tile size, Nearest texture filter)
- Input Map configured (move_up, move_down, move_left, move_right, confirm, cancel, open_menu)
- CHANGELOG.md and static README badges (license, Godot version)
- English translations for the README and the GitHub issue/PR templates
- Feature-based project folder structure (Player/, Combat/, World/, UI/, Shared/, Autoloads/)
- Entry point scene (main.tscn) and project conventions documentation (docs/CONVENTIONS.md)
- Godot project moved into a dedicated Game/ subfolder, separating it from repository metadata
- CI workflow to validate the build on every push and pull request
- CHANGELOG checklist item added to the PR template
- Basic top-down player movement (CharacterBody2D, 4-direction input via Input Map, normalized diagonal movement)
- Tilemap-based test room with floor and wall collisions (TileSet, FloorLayer, WallsLayer)
- Camera2D with dead-zone drag follow behavior as a child of Player
- `PlayerStats` Autoload singleton (max/current HP) for shared player state across scenes
- Enemy placeholder (`Area2D` with red `ColorRect`, `Hp`/`AttackPower` stats) that detects player contact and emits a `CombatTriggered` signal
- Placeholder combat scene (`Combat/combat.tscn`) and scene switch from the world map on enemy contact
- Turn-based combat loop (`Combat/Combat.cs`): alternating player/enemy turns with fixed damage, win/lose detection, and console output
- `Combat/CombatResolver.cs`: a plain C# class with no Godot dependency holding the fight's HP, attack power and win/lose determination, so the combat math can be exercised without running the engine
- The touched enemy's `Hp`/`AttackPower` are now carried into the combat scene, so different enemies produce different fights
- Second, stronger enemy instance on the test map, making the defeat outcome reachable in normal play
- Minimal combat UI: player and enemy HP labels, an Attack button (disabled during the enemy's turn), and a result message on win/loss
- Short delay before the enemy's attack resolves, so the turn handover is visible instead of instant
- Combat now runs as an overlay on a paused world map instead of replacing the scene, keeping the map and player state alive
- Combat outcome resolution: both outcomes fully heal the player; winning keeps the player's position and removes the defeated enemy, losing returns them to a `PlayerSpawn` marker
- `PlayerSpawn` marker in the test map, now the authoritative player start position
- `EnemyData` Resource (`[GlobalClass]`, with `DisplayName`/`MaxHp`/`AttackPower`) plus `weak_enemy.tres` and `strong_enemy.tres`, so new enemy types are created as resource files instead of per-instance values typed into the scene
- Combat overlay architecture and the stats-as-Resources convention documented in `docs/CONVENTIONS.md`
- Fade to and from black when a combat starts and ends, so the overlay no longer cuts in and out abruptly
- xUnit test project (`tests/Game.Tests/`) covering `CombatResolver`'s damage, HP clamping and win/loss detection, run in CI on every push and pull request, and registered in `Game/Godot RPG Project.sln` so editors and CI see a single solution covering both projects
- CI now fails on any file that does not match `.editorconfig`, via `dotnet format --verify-no-changes` over the whole solution
- Dependabot configuration (`.github/dependabot.yml`) watching the NuGet and GitHub Actions ecosystems weekly

### Changed

- The CI workflow's token is now restricted to `contents: read`, since it only checks out and builds the code
- CI actions bumped to versions running on Node 24 (`actions/checkout` v4 -> v7, `actions/setup-dotnet` v4 -> v6); the v4 majors run on the deprecated Node 20
- `Game/Godot RPG Project.sln` normalized to the format VS Code's C# Dev Kit writes, so the editor stops producing spurious diffs on it
- `Enemy` now holds a single `EnemyData` resource instead of loose `Hp`/`AttackPower` fields, and that resource is passed straight to `Combat` instead of two separate ints
- The player's attack power moved from an export on `Combat` into the `PlayerStats` Autoload, next to the rest of the player's stats
- The combat UI and console output now use the enemy's `DisplayName` instead of a hardcoded "Enemy"
- Wall tiles now live in `WallsLayer` instead of `FloorLayer`, matching the layer names
- `Combat.cs` delegates every damage and outcome decision to `CombatResolver`, keeping only the Godot-specific concerns (nodes, turn state, timers, tween, input and UI)
- `PlayerStats.CurrentHp` became a clamping property that combat writes the fight's result into, replacing `PlayerStats.TakeDamage(int)`, so the damage arithmetic exists in exactly one place
- Test dependencies bumped by Dependabot: `Microsoft.NET.Test.Sdk` 17.11.1 -> 18.10.0, `xunit` 2.9.2 -> 2.9.3, `xunit.runner.visualstudio` 2.8.2 -> 4.0.0
- README's Project Structure section now lists every tracked file in the repository, not just `Game/`'s top-level folders

### Removed

- Obsolete `.gitkeep` files in `Game/Player/` and `Game/World/`; both folders hold real content now, so Git no longer needs a placeholder to track them

### Fixed

- Losing a fight next to an enemy placed on `PlayerSpawn` immediately started another combat on respawn; a short cooldown now ignores triggers right after a fight ends
- The player was spawned in `Main._Ready()`, after its body had already registered with the physics server, so an enemy overlapping the position authored in `main.tscn` reported a phantom contact on the second physics frame and started a combat the player was never in; the player is now spawned in `_EnterTree()`, before that registration
- Combat could be triggered more than once (two overlapping enemies, or a re-trigger before the deferred start ran), leaking an orphaned overlay on a permanently paused tree and crashing when the first fight resolved
- `.editorconfig` declared spaces for C# files while every file in the project is tab-indented, which would have introduced mixed indentation

[Unreleased]: https://github.com/EGCGAP04/godot-rpg-project/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/EGCGAP04/godot-rpg-project/releases/tag/v0.1.0
