# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- `PlayerStats.TakeDamage(int)` to apply combat damage to the player's current HP
- The touched enemy's `Hp`/`AttackPower` are now carried into the combat scene, so different enemies produce different fights
- Second, stronger enemy instance on the test map, making the defeat outcome reachable in normal play
- Minimal combat UI: player and enemy HP labels, an Attack button (disabled during the enemy's turn), and a result message on win/loss
- Short delay before the enemy's attack resolves, so the turn handover is visible instead of instant
- Combat now runs as an overlay on a paused world map instead of replacing the scene, keeping the map and player state alive
- Combat outcome resolution: both outcomes fully heal the player; winning keeps the player's position and removes the defeated enemy, losing returns them to a `PlayerSpawn` marker
- `PlayerSpawn` marker in the test map, now the authoritative player start position
- `EnemyData` Resource (`[GlobalClass]`, with `DisplayName`/`MaxHp`/`AttackPower`) plus `weak_enemy.tres` and `strong_enemy.tres`, so new enemy types are created as resource files instead of per-instance values typed into the scene
- Combat overlay architecture and the stats-as-Resources convention documented in `docs/CONVENTIONS.md`

### Changed

- `Enemy` now holds a single `EnemyData` resource instead of loose `Hp`/`AttackPower` fields, and that resource is passed straight to `Combat` instead of two separate ints
- The player's attack power moved from an export on `Combat` into the `PlayerStats` Autoload, next to the rest of the player's stats
- The combat UI and console output now use the enemy's `DisplayName` instead of a hardcoded "Enemy"

### Fixed

- Combat could be triggered more than once (two overlapping enemies, or a re-trigger before the deferred start ran), leaking an orphaned overlay on a permanently paused tree and crashing when the first fight resolved
- `.editorconfig` declared spaces for C# files while every file in the project is tab-indented, which would have introduced mixed indentation
