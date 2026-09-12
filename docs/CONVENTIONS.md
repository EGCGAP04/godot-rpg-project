# Project Conventions

This document describes the architectural conventions followed throughout the project. It's meant to keep the codebase consistent as it grows, especially since this is a learning project built without prior Godot experience.

## Folder Structure

The project follows a **feature-based** structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type.

```text
Game/ (res://)
├── project.godot    # Project settings: autoloads, input map, rendering
├── main.tscn        # Entry point scene; Main.cs coordinates map and combat
├── Player/          # Player character: movement, sprite, input handling
├── Enemy/           # Enemy scenes and stats, contact detection on the map
├── Combat/          # Turn-based combat system: turn order, actions
├── World/           # Exploration maps: tilemaps, collisions, transitions
├── UI/               # Menus, HUD, dialogue boxes
├── Shared/
│   ├── Fonts/        # Shared fonts used across the project
│   ├── Scripts/      # Generic utilities, extensions, helper classes
│   └── Theme/        # Global UI theme resources
└── Autoloads/        # Global singletons (e.g. PlayerStats)
```

`Shared/` holds anything that doesn't belong to a single feature. `Autoloads/` is kept separate since it's infrastructure, not a feature.

## Naming Conventions

- **Folders:** `PascalCase`
- **Scene files (`.tscn`):** `snake_case`
- **C# scripts and classes:** `PascalCase`
- **Scene tree nodes:** `PascalCase`
- **C# fields/properties:** standard C# conventions (`PascalCase` for public properties, `_camelCase` for private fields)

## Node Communication Pattern

Godot offers several ways for nodes to communicate. This project follows a consistent default to avoid mixing styles as it grows:

- **Signals** are the default for communication **between different scenes** (e.g. a `Combat` scene notifying `UI` that HP changed). Signals decouple nodes from each other — the emitter doesn't need to know who's listening.
- **Direct references** are only used for **tight parent-child relationships within the same scene** (e.g. `Player.cs` accessing its own child `AnimationPlayer`), where the coupling doesn't matter because those nodes will always exist together.
- **Autoloads** hold global state that needs to persist across scene changes (e.g. `PlayerStats`). Godot autoloads aren't automatically globally accessible in C# like they are in GDScript, so every Autoload script exposes a static `Instance` property set in `_Ready()`:
  ```csharp
  public partial class PlayerStats : Node
  {
      public static PlayerStats Instance { get; private set; }
      public override void _Ready() { Instance = this; }
  }
  ```
  Access elsewhere as `PlayerStats.Instance.SomeField`.

## Feature Scene Skeleton

When creating the first scene of a feature (e.g. `Player/player.tscn`), the root node should be named after the scene/feature itself (e.g. root node `Player`, not a generic `Node2D`), with a matching C# script (`Player.cs`) attached to it. This keeps the scene tree self-explanatory when the scene is instanced elsewhere — instead of seeing a generic `Node2D` inside `world_map.tscn`, you see `Player`.

## Combat Overlay Architecture

Combat does **not** replace the exploration scene. `Combat/combat.tscn` is a `CanvasLayer` instanced on top of the paused world map. Keeping the map alive means the player's position survives the fight for free, with no state to save and restore.

The flow is:

1. `Enemy` (an `Area2D`) detects player contact and emits `CombatTriggered(enemy)`.
2. `WorldMap` re-emits it as `CombatRequested(enemy)`.
3. `Main` (on `main.tscn`) handles it: it instances `combat.tscn`, sets the enemy stats, adds it as a child and sets `GetTree().Paused = true`.
4. When the fight ends, `Combat` emits `CombatFinished(playerWon)`. `Main` applies the outcome, frees the overlay and unpauses.

`Main` is the coordinator because it is the only node that sees both the `Player` and the `WorldMap` — the `Player` is a **sibling** of `WorldMap`, not a child of it.

Three details are load-bearing:

- **The overlay must be added deferred.** The trigger fires inside a physics callback (`body_entered`), where adding or freeing nodes is not allowed, so `Main` uses `CallDeferred(MethodName.StartCombat, ...)`.
- **`Combat` sets `process_mode = 3` (Always).** Otherwise the paused tree would freeze the overlay too, and its turn timers would never fire.
- **Enemy stats are assigned before `AddChild`,** so they are already set when `Combat._Ready()` runs.

`Main` also guards against starting more than one combat: a trigger arriving while a fight is pending or running is ignored, which matters when two enemies overlap the player in the same frame.

## Stats as Resources

Per-entity stats live in `Resource` subclasses marked `[GlobalClass]`, authored as `.tres` files (e.g. `Enemy/EnemyData.cs` with `Enemy/weak_enemy.tres` and `Enemy/strong_enemy.tres`). A new enemy type is a new `.tres` file, not new code or per-instance values typed into a scene, and adding a stat means adding one property instead of changing every method signature it travels through.

Global player state is the exception: it lives in the `PlayerStats` Autoload, since it has to persist across scenes.
