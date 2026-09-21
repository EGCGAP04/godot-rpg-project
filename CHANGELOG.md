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
- `WorldScene`, the base class every world scene is built on, exposing the player spawn position, the `CombatRequested` signal and a `TransitionRequested` signal a world raises when the player reaches its exit
- `WorldExit` (`World/world_exit.tscn`), a reusable trigger that ends the current world on player contact, instanced as the bed in the Real world and as the way out of the other two
- Placeholder rooms for the Real and Nightmare worlds (`real_world.tscn`, `nightmare_world.tscn`), each with its own `PlayerSpawn` marker and exit
- The three world scenes are told apart by a `Modulate` tint on their `TileMapLayer` nodes, so no new art is needed to see which world is being played

- `Direction` enum and `DirectionResolver`, an engine-free class that turns several simultaneously held directions into the one being moved in, and remembers the facing after movement stops
- Gamepad bindings across the whole Input Map: D-pad and left stick for the four movement actions, and face buttons for `confirm`, `cancel` and `open_menu`
- Unit tests covering single and overlapping presses, the fallback on release, re-pressing a held direction, and facing persistence

- `ScreenFade` (`UI/screen_fade.tscn`), the fade to and from black extracted from the combat overlay so world transitions reuse it instead of growing a second copy
- `Main` now coordinates the three worlds and the cycle: it holds whichever world is active, swaps it when `GameState` reports a change, keeps the `Player` alive across the swap and places it at the new world's spawn, and fades through the transition
- Temporary on-screen label showing the current world and cycle

### Changed

- `main.tscn` no longer authors a world; `Main` builds the first one from `GameState.CurrentWorld`, so which world the run is in has a single source of truth
- Autoloads now assign their static `Instance` in `_EnterTree` instead of `_Ready`: a node's own `_EnterTree` runs before any autoload's `_Ready`, and `Main` needs `GameState` that early to place the player before any `Area2D` registers against a stale transform
- Player movement is now strictly 4-directional, replacing `Input.GetVector(...)`, which allowed diagonals: the strongest held direction wins, ties go to the most recently pressed one, and releasing the winner falls back to whatever is still held instead of stopping the character. Following the stronger direction is what keeps an analog stick from stuttering as it sweeps through a diagonal; digital input always reports full strength, so on a keyboard or D-pad the rule is exactly last-pressed-wins
- `WorldMap` renamed to `WorldScene` and `world_map.tscn` to `fantasy_world.tscn`: the test room is now the Fantasy world placeholder, keeping both enemies, and `Main` talks to the base class instead of one concrete scene
- Test dependency bumped by Dependabot: `Microsoft.NET.Test.Sdk` 18.10.0 -> 18.10.1

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
