# Project Conventions

This document describes the architectural conventions followed throughout the project. It's meant to keep the codebase consistent as it grows, especially since this is a learning project built without prior Godot experience.

## Folder Structure

The project follows a **feature-based** structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type.

```text
Game/ (res://)
├── project.godot    # Project settings: autoloads, input map, rendering
├── main.tscn        # Entry point; Main.cs coordinates worlds, player and combat
├── Player/          # Player character: movement, sprite, input handling
├── Enemy/           # Enemy scenes and stats, contact detection on the map
├── Combat/          # Turn-based combat system: turn order, actions
├── World/           # Exploration maps: tilemaps, collisions, transitions
├── UI/              # Menus, HUD, dialogue boxes, the shared screen fade
├── Shared/
│   ├── Fonts/        # Shared fonts used across the project
│   ├── Scripts/      # Generic utilities, extensions, helper classes
│   └── Theme/        # Global UI theme resources
└── Autoloads/       # Global singletons (GameState, PlayerStats)
```

`Shared/` holds anything that doesn't belong to a single feature. `Autoloads/` is kept separate since it's infrastructure, not a feature.

Unit tests are the one piece of C# that lives **outside** `Game/`, in `tests/Game.Tests/` at the repository root. See [Engine-Free Game Logic](#engine-free-game-logic) for why.

## Code Formatting

`.editorconfig` at the repository root is the single source of truth for whitespace: UTF-8, LF line endings, a final newline, no trailing whitespace, and **tabs** for `.cs` files, matching what Godot's own C# script templates emit.

These are enforced, not merely documented: CI runs `dotnet format --verify-no-changes` over the solution and fails the `build` check on any file that does not match. The check covers both projects, `Game/` and `tests/Game.Tests/`. Run it locally before pushing:

```bash
cd Game && dotnet format "Godot RPG Project.sln" --verify-no-changes
```

Drop `--verify-no-changes` to have it apply the fixes instead of just reporting them.

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
- **Autoloads** hold global state that needs to persist across scene changes (e.g. `PlayerStats`). Godot autoloads aren't automatically globally accessible in C# like they are in GDScript, so every Autoload script exposes a static `Instance` property set in **`_EnterTree()`**:
  ```csharp
  public partial class PlayerStats : Node
  {
      public static PlayerStats Instance { get; private set; }
      public override void _EnterTree() { Instance = this; }
  }
  ```
  Access elsewhere as `PlayerStats.Instance.SomeField`.

  **`_EnterTree`, not `_Ready`, and this is load-bearing.** Autoloads enter the tree before the main scene, but their `_Ready` has not run by the time the main scene's own `_EnterTree` does — measured, after a `NullReferenceException` from `Main._EnterTree`. `Main` has to read `GameState` that early, because `_EnterTree` is where it must place the player before any `Area2D` registers against a stale transform. Setting `Instance` in `_EnterTree` makes an Autoload usable from anywhere, including that window.

## Feature Scene Skeleton

When creating the first scene of a feature (e.g. `Player/player.tscn`), the root node should be named after the scene/feature itself (e.g. root node `Player`, not a generic `Node2D`), with a matching C# script (`Player.cs`) attached to it. This keeps the scene tree self-explanatory when the scene is instanced elsewhere — instead of seeing a generic `Node2D` under `main.tscn`, you see `Player` and the world scene it sits beside.

## Combat Overlay Architecture

Combat does **not** replace the exploration scene. `Combat/combat.tscn` is a `CanvasLayer` instanced on top of the paused world map. Keeping the map alive means the player's position survives the fight for free, with no state to save and restore.

The flow is:

1. `Enemy` (an `Area2D`) detects player contact and emits `CombatTriggered(enemy)`.
2. The `WorldScene` re-emits it as `CombatRequested(enemy)`.
3. `Main` (on `main.tscn`) handles it: it instances `combat.tscn`, sets the enemy stats, adds it as a child and sets `GetTree().Paused = true`.
4. When the fight ends, `Combat` emits `CombatFinished(playerWon)`. `Main` applies the outcome, frees the overlay and unpauses.

`Main` is the coordinator because it is the only node that sees both the `Player` and the world — the `Player` is a **sibling** of the world scene, not a child of it. It holds a `WorldScene`, never a concrete world, so the same flow works in all three.

Five details are load-bearing:

- **The overlay must be added deferred.** The trigger fires inside a physics callback (`body_entered`), where adding or freeing nodes is not allowed, so `Main` uses `CallDeferred(MethodName.StartCombat, ...)`.
- **`Combat` sets `process_mode = 3` (Always).** Otherwise the paused tree would freeze the overlay too, and its turn timers would never fire.
- **Enemy stats are assigned before `AddChild`,** so they are already set when `Combat._Ready()` runs.
- **The player is spawned from `Main._EnterTree()`, not `_Ready()`.** A `CharacterBody2D` registers its transform with the physics server when it enters the tree, and a parent's `_EnterTree` runs before its children enter, so spawning there is what keeps the server from ever seeing the position authored in `main.tscn`. Moving the player afterwards leaves enemy areas paired against that stale transform for one frame, which reports a contact — a `body_entered` followed immediately by `body_exited` — that never happened. `WorldScene.PlayerSpawnPosition` is a computed property rather than a value cached in `_Ready` for the same reason: it has to be readable before the world's `_Ready` has run.
- **The fade tween is created on the `ScreenFade` node itself** (`CreateTween()`), not on the tree. A tween is bound to the node that creates it and follows that node's process mode, and `UI/screen_fade.tscn` sets `process_mode = Always`, which is what keeps the fade playing while the world is paused. The same scene is reused for world transitions, so the two never drift apart in colour or duration.

`Main` also guards against starting more than one combat: a trigger arriving while a fight is pending or running is ignored, which matters when two enemies overlap the player in the same frame. The same guard also holds for a short cooldown after a fight ends, so a player who loses and respawns inside another enemy's area is not pulled straight into the next fight.

## World and Cycle Coordination

The game is three worlds traversed in a fixed order, and each pass through them is one cycle. `GameState` (an Autoload) owns where the run is; `CycleProgression` — engine-free and unit-tested — owns the rule for what comes next; `Main` owns the scenes.

The flow mirrors the combat one:

1. A `WorldExit` (an `Area2D`, instanced as the bed in the Real world and as the way out of the other two) detects the player and emits `ExitTriggered`.
2. The `WorldScene` re-emits it as `TransitionRequested`.
3. `Main` fades to black, then calls `GameState.Advance()`.
4. `GameState` asks `CycleProgression` for the next position and emits `WorldChanged`.
5. `Main` swaps the world scene, places the player at its `PlayerSpawn`, and fades back in.

`main.tscn` authors **no** world at all. `Main` builds the first one from `GameState.CurrentWorld` in `_EnterTree`, exactly as it builds every later one, so there is one source of truth for which world the run is in rather than a scene and an Autoload that have to agree.

### The phantom contact across a swap, and what actually fixes it

Swapping worlds recreates the bug from the player-spawn fix, but with the player **already in the tree**. The incoming world's enemy `Area2D`s pair against the transform the physics server holds for the player, and a body that is already registered and merely moved leaves that transform stale for at least a frame. An enemy standing where the player stood in the world it just left therefore reports a contact that never happened.

Three fixes that look obviously right were tried and **measured to fail**, each with the player 80px away from the enemy that triggered the fight:

- moving the player to the new spawn *before* the new world is added;
- waiting a physics frame between moving the player and adding the world;
- removing the player from the tree, adding the world, then reattaching the player — an exact replica of the start-up ordering that does work.

What works is refusing the trigger: `Main` ignores combat requests while a transition is in flight and for `CombatCooldown` after one completes. Both were confirmed active at the instant the phantom arrived. This is the same mechanism already used after a lost fight, and the same trade-off comes with it — a player who genuinely lands on an enemy at the new world's spawn is not pulled into a fight either, because `body_entered` does not fire again once the cooldown lapses. That is a level-design constraint, not a bug to fix in code.

**Do not "simplify" this by removing the guards and relying on ordering.** It has been tried.

## Engine-Free Game Logic

Rules that are pure decisions — how much damage an attack does, whether a fight is over, who won — live in plain C# classes with no Godot base type and no `using Godot;`, next to the feature they belong to — `Combat/CombatResolver.cs`, `World/CycleProgression.cs`, `Player/DirectionResolver.cs`. The `Node` keeps what actually needs the engine: child nodes, input, timers, tweens, signals and UI.

The point is testability. A `Node` can only run inside a scene tree, so anything mixed into it can only be checked by playing the game; a plain class can be exercised directly by a unit test, and by a headless run, without an engine around it.

When a plain class and an Autoload hold the same number, the plain class decides it and the `Node` writes the result into the Autoload — never both applying the same arithmetic. `CombatResolver` owns the fight's HP while it lasts; `Combat` copies it into `PlayerStats`, which is what carries it back out to the map.

### Where the tests live

The xUnit project sits at `tests/Game.Tests/`, outside `Game/`. Godot's C# SDK compiles every `.cs` file under the project folder and the editor scans all of `res://`, so a test project inside `Game/` would be built into the game's own assembly and shipped in the export unless an explicit `<Compile Remove>` and a `.gdignore` were maintained by hand forever. Keeping it out of `res://` costs nothing and removes both.

It does **not** use a `ProjectReference` to the Godot project. Instead it compiles the engine-free sources directly:

```xml
<Compile Include="..\..\Game\Combat\CombatResolver.cs" Link="GameLogic\CombatResolver.cs" />
```

Referencing the Godot project would pull its source generators into the test assembly, where they fail with `CS8785` because `GodotProjectDir` is not set outside a Godot build. Compiling the source directly also turns this whole convention into a compiler check: a `using Godot;` added to one of these files breaks the test project immediately, instead of waiting for someone to notice it in review.

**Every new engine-free class that gets tests needs its own `<Compile Include>` line** in `tests/Game.Tests/Game.Tests.csproj`. That is the deliberate trade: one line per class, in exchange for a test suite that never starts the engine.

Even though it sits outside `Game/`, the test project **is** listed in `Game/Godot RPG Project.sln`. Without it, VS Code's C# Dev Kit treats it as a project no solution covers and generates its own solution under `workspaceStorage`, which on Linux writes a broken path for `Godot RPG Project.csproj` and fails every restore in the editor. One solution covering both projects avoids that, and lets CI restore, build and test from a single `.sln` path. Godot does not rewrite the file when it already exists, so the entry survives opening the editor (verified).

The test project is deliberately mapped to `ActiveCfg` **without** a matching `Build.0` under `ExportDebug` and `ExportRelease`, so exporting the game does not compile the tests — a failing test should never be able to block an export.

The file is stored in the format VS Code's C# Dev Kit writes (a newline after the BOM, indented configuration entries, a `SolutionProperties` section). The Dev Kit rewrites the solution into that shape whenever it loads it, so storing anything else means the editor produces a diff nobody asked for. Godot does not rewrite the file at all, so this format is stable for both tools.

## Stats as Resources

Per-entity stats live in `Resource` subclasses marked `[GlobalClass]`, authored as `.tres` files (e.g. `Enemy/EnemyData.cs` with `Enemy/weak_enemy.tres` and `Enemy/strong_enemy.tres`). A new enemy type is a new `.tres` file, not new code or per-instance values typed into a scene, and adding a stat means adding one property instead of changing every method signature it travels through.

Global player state is the exception: it lives in the `PlayerStats` Autoload, since it has to persist across scenes.
