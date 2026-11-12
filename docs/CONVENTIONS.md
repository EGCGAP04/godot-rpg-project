# Project Conventions

This document describes the architectural conventions followed throughout the project. It's meant to keep the codebase consistent as it grows, especially since this is a learning project built without prior Godot experience.

## Folder Structure

The project follows a **feature-based** structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type.

```text
Game/ (res://)
├── project.godot    # Project settings: autoloads, input map, rendering, translation tables
├── main.tscn        # Entry point; Main.cs coordinates worlds, player and combat
├── Player/          # Player character: movement, sprite, input handling
├── Enemy/           # Enemy scenes and stats, contact detection on the map
├── Characters/      # The characters as resources, and the plain values the rules read
├── Stats/           # Stat formulas, growth with the level, experience curves and their tuning
├── Combat/          # Turn-based combat system: turn order, actions
├── World/           # Exploration maps: tilemaps, collisions, transitions, things to talk to
├── Dialogue/        # Branching dialogue: the conversation files, their reader, runner and validator
├── Decisions/       # The flag catalogue, the decision registry's state, and what opens each world
├── Localization/    # Translation tables of no single conversation, their reader and validator
├── Debug/           # The debug tooling: presets, the overlay and its autoload (debug builds only)
├── UI/              # Menus, HUD, dialogue boxes, the shared screen fade
├── Shared/
│   ├── Fonts/        # Shared fonts used across the project
│   ├── Scripts/      # Generic utilities, extensions, helper classes
│   └── Theme/        # Global UI theme resources
└── Autoloads/       # Global singletons (GameState, DecisionRegistry, PlayerStats)
```

`Shared/` holds anything that doesn't belong to a single feature. `Autoloads/` is kept separate since it's infrastructure, not a feature.

Unit tests are the one piece of C# that lives **outside** `Game/`, in `tests/Game.Tests/` at the repository root. See [Engine-Free Game Logic](#engine-free-game-logic) for why.

## Code Formatting

`.editorconfig` at the repository root is the single source of truth for whitespace: UTF-8, LF line endings, a final newline, no trailing whitespace, and **tabs** for `.cs` files, matching what Godot's own C# script templates emit. The JSON data files use tabs too, though for them the rule only guides the editor: `dotnet format` checks C# alone.

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
- **Data files (`.json`):** `snake_case`, like scenes. A conversation's file name is its id (see [Dialogue Format](#dialogue-format))

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
4. When the fight ends, `Combat` emits `CombatFinished(playerWon)`. `Main` applies the outcome, frees the overlay, unpauses, and only then records the fight in the decision registry (see [Fights](#fights) for why last).

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
4. `GameState` asks `CycleProgression` for the next position, passing the worlds the decision registry has open at that moment, and emits `CycleChanged` when the cycle changes, then `WorldChanged`.
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

## Dialogue Flow

A conversation is the third thing `Main` coordinates, next to combat and world transitions, and it follows their shape:

1. An `Interactable` (an `Area2D`, `World/interactable.tscn`) holds the id of a conversation. When the player stands inside it, faces it and presses `confirm`, it emits `InteractionTriggered(conversation)` and marks the press as handled.
2. The `WorldScene` re-emits it as `ConversationRequested(conversation)`.
3. `Main` checks it can start, loads the file through `ConversationLoader`, starts a `DialogueRunner` on `DecisionRegistry.Instance.State`, and adds `UI/dialogue_box.tscn` with the runner in it.
4. When the runner reaches the end, the `DialogueBox` emits `Finished`, and `Main` frees it and gives the world back.

An interactable's area is wider than the thing it holds, which has a `StaticBody2D` of its own, so the player stops next to it rather than inside it. Facing it is `Facing.IsToward`: the target lies further along the player's `Facing` than along the other axis, with the exact diagonal counting for both.

### Two kinds of conversation

- **A voluntary conversation**, started by the player, leaves the world running: enemies keep moving and timers keep counting, so talking is never a pause.
- **A forced conversation**, started by the game, pauses the tree, which stops every fight and every timer on the tree's clock. The only way to start one today is a world's `OpeningConversation`, which `Main` starts each time the player arrives there, from the fade in's callback, so never before the world is visible.

Both keep the player still, through `Player.CanMove`, and every exit closed. Held directions are not even read while it is off, so moving through a choice's options does not turn the player; since they are polled, they are right again on the first frame back.

**Whether a fight, a conversation or a transition can start is decided by `ConversationRules`,** an engine-free class, from a `GameActivity` that `Main` builds from its own state at that moment. The guards that existed before conversations (a fight running or pending, a transition, the cooldown) come first and still refuse a fight. Otherwise, **a fight requested during a conversation is queued, never dropped,** and starts as soon as the conversation ends. An area does not report a body already inside it a second time, so a dropped request would leave the enemy standing on the player with no fight.

### The box and its input

- **The box reads input in `_Input`, and marks every press it uses as handled.** `_Input` runs before any node's `_UnhandledInput`, which is where the interactables listen. Without it, the `confirm` that ends a conversation reaches the character the player was talking to and starts the conversation again, on the same press (verified headless by removing it).
- `confirm` moves past a line or picks the highlighted option, and `move_up` / `move_down` move the highlight, going round at either end. A choice keeps the last line on screen, since that line is usually the question.
- **Every text is a key, and the labels translate themselves,** so a change of language shows at once. A narration line shows no name.
- The box is a `CanvasLayer` with `process_mode = Always`, because a forced conversation pauses the tree around it.
- **The box ignores input while the debug overlay is open,** and so does `Combat`. Godot runs every node's `_Input` before any control sees the event, so without that a key pressed in the overlay also moved the conversation behind it; `Combat`, which listens in `_UnhandledInput`, still received `confirm` with an overlay control holding the focus (both measured headless).

**A conversation that shows nothing ends at once, without a box.** When no line's condition holds and the start node goes straight to `END`, `DialogueRunner.Start` is already at the end. That is what lets a world have an opening conversation that only sometimes has something to say, as the demo's morning does. A conversation that fails to load is read before anything changes, so the game carries on as it was, with the error in the log.

## Dialogue Format

Branching dialogue is written in a JSON format of the project's own. Godot's localization system handles text, but has no notion of which line follows which or of what unlocks a branch, so that structure needs a format. Ink and Yarn Spinner were the alternatives, and both would have brought validation and editor tooling for free; this is a learning project, and designing the format teaches more here than integrating one. The cost is that the tooling has to be built. The validator below is where most of it is paid: with around a hundred endings planned, a dangling jump or a misspelt flag has to fail CI, not turn up in play. Both tools remain references — Ink for flow control, Yarn Spinner for line ids and localization.

**The JSON holds structure and keys, never text.** The text of every line lives in the localization tables, found by its key.

### A conversation

One file per conversation, anywhere under `Dialogue/Conversations/`. The file name is the conversation's id, which must be unique across the project. Its text sits beside it, in one table per language: `example_first_meeting.en.po` and `example_first_meeting.es.po` (see [Localization](#localization)). `example_first_meeting.json` and `example_second_meeting.json` are the format's reference example: between them they use every feature it has.

```json
{
	"format": 1,
	"nodes": {
		"start": {
			"lines": [
				{ "speaker": "example_stranger", "line": "ask" }
			],
			"choices": [
				{ "option": "help", "do": ["set example.promised_help"], "next": "thanks" },
				{ "option": "give_key", "if": "example.has_key", "next": "thanks" },
				{ "option": "refuse", "next": "END" }
			]
		},
		"thanks": {
			"lines": [
				{ "speaker": "example_stranger", "line": "thanks" }
			],
			"next": "END"
		}
	}
}
```

- A conversation is a set of **nodes** and begins at `start`. A node is a block of **lines** read in order, ending in exactly one of a **choice** (`choices`) or a **jump** (`next`, to another node or to `END`).
- Every line names its `speaker`: a character's id, or `narrator` for narration, which the dialogue box shows with no name.
- Lines and options can carry a condition (`if`) and effects (`do`). A line whose condition does not hold is skipped; an option whose condition does not hold is not offered.
- Ids and names are lowercase words: letters, digits and underscores, starting with a letter. `END` is the only name in capitals, because it is not a node.
- `format` is the version of the format, so a later change can be migrated by a script that knows what it starts from.

### Keys

The key of a line or an option is its conversation, its node and its own name: `example_first_meeting.start.ask`. A speaker's name is `speaker.<id>`, in the shared `speaker` table. No conversation may take the name of a shared table — `speaker`, `ui`, `enemy`, `world` and any added later — since its keys would mix with that table's. Sorted, a conversation's table groups by node, which keeps it navigable at hundreds of lines. Renaming a node, or moving a line to another one, changes the key on purpose: the old text is left behind where it can be seen, instead of silently showing on the wrong line, as it would with keys made from line positions.

### Conditions and effects

Conditions and effects are written in a small language over flags, not as general expressions:

```text
example.has_key                       a boolean flag is set
not example.met_before
example.times_met >= 1                a counter compared with a number: == != < <= > >=
example.promised_help and not (example.key_returned or example.has_key)

set example.promised_help             effects set or clear a boolean flag,
clear example.has_key                 or add to a counter (a negative amount subtracts, down to 0)
add example.times_met 1
```

`not` applies before `and` and `and` before `or`, as in C#, and parentheses group first. `and`, `or`, `not`, `set`, `clear` and `add` are reserved, so no name can use them.

`FlagTokenizer` splits the text into tokens. `FlagConditionParser` builds a `FlagCondition` tree from them by recursive descent: one method per rule of the grammar, each asking the rule below it for its operands, which is where the precedence comes from. `FlagEffectParser` checks the three fixed shapes of an effect. Every error names the column where it starts.

### Flags

Every flag is declared once, in `Decisions/flags.json`, with its type — `bool`, which starts `false`, or `counter`, which starts at 0 — its lifetime — `cycle`, `run` or `persistent` — and optionally a description for the debug tooling. A flag that is not declared does not exist.

A flag's name is a **group and a fact**, joined by a dot. The group says what the flag is about: usually the character it matters to, under the same id the character speaks with, and otherwise a place or a game system, such as `world.` or `combat.`. The fact says what is true once the flag is set, in the positive — `promised_help`, not `not_promised` — and a counter is named for what it counts. Lifetime and type stay out of the name, since the catalogue holds them, and so do cycle and world: a flag is written in one place and read in many.

**A choice is remembered only through the flags its option sets.** Nothing records choices on its own, so everything the game remembers is a declared flag, with a lifetime someone chose. The reference example shows the pattern, including a later answer replacing an earlier one with `clear`.

### Reading, running and validating

- **`ConversationReader`** reads one file and checks that it is well formed: the right properties with the right types, valid names, conditions and effects that parse, a `start` node, and jumps to nodes that exist. Every JSON object is read property by property, through `StrictJson`, instead of being deserialized. JSON allows a repeated property and `JsonDocument` accepts one without complaint (verified), while a deserializer would quietly keep one of the two. The reader takes the file's text, not a path: the game reads files through `FileAccess`, which sees inside the packed `res://` of an export, and the tests read them from disk.
- **`DialogueRunner`** walks one conversation a step at a time: a line, a choice, or the end. It is passive — whoever drives it asks what to show and calls `Advance` or `Choose`; in the game that is the dialogue box (see [Dialogue Flow](#dialogue-flow)) — and `DialogueRunner.Start` already applies the first line's effects. Conditions are evaluated when they are reached, so the effects of earlier lines count; a line's effects apply when it is shown, and an option's when it is chosen.
- **`DialogueValidator`** checks every conversation against the catalogue and against each other, and reports every problem instead of the first: flags that are not declared or are used with the wrong type, ids that repeat, nodes nothing leads to, and anywhere the player could be left stuck. Every choice must offer an option with no condition, and from every node there must be a way to `END` that depends on none. That is strict on purpose: two options with opposite conditions would also always leave a way out, and are still rejected. `EveryConversationInTheProject_IsValid` runs it over `Dialogue/Conversations/`, so a broken conversation fails the `build` check. `DialogueValidator.ValidateText` checks the other half: that each conversation has its table beside it, with a text for every line and option and nothing else, and that every speaker but `narrator` has a name. `EveryConversationInTheProject_HasItsText` runs it.

Conditions read flags and effects write them through `IFlagStore`, which the decision registry's `DecisionState` implements for the game (see [Decision Registry](#decision-registry)). The tests use an in-memory stand-in that rejects any name it was not given, as the registry does.

## Localization

Every text the player sees comes from a translation table, in Spanish and English, so nothing on screen is written in code or in a scene. Console output (`GD.Print`) is for development and stays in English.

### The tables

The tables are gettext `.po` files. Godot loads them as they are, with nothing generated beside them and nothing to keep out of git, and each language is its own file, so correcting the Spanish touches the Spanish file alone. The format also has room for contexts, plurals and notes for whoever translates. CSV was the alternative, and it was rejected for three reasons: Godot imports every `.csv` under `res://` as a translation and writes binary files next to it, a compressed CSV table cannot list its keys at run time, and every row holds every language, so one correction rewrites the whole row.

```po
msgid ""
msgstr ""
"Language: es\n"
"Content-Type: text/plain; charset=UTF-8\n"

#. {name} is a combatant's name; {hp} and {max_hp}, numbers.
msgid "ui.combat.hp"
msgstr "{name}   {hp}/{max_hp}"
```

- **A table is a file `<name>.<locale>.po`, and every key in it starts with `<name>.`** That way two tables can never hold the same key. This matters because Godot merges every table of a language into one, and when a key appears twice it keeps one of them without saying.
- **The header must declare `Language`, matching the file name.** Godot loads a table without one as English, silently, so a Spanish table with a broken header would replace the English text.
- **The `msgid` is the key, never English text.** English is one more translation, in `*.en.po`, so correcting it does not change the key or break the other languages.
- **Notes for whoever translates (`#.`) are repeated in every language,** since each translator opens only their own file.
- **Entries go in a readable order:** a conversation's in the order of its JSON, the rest grouped by area.

### Where they live, and the keys

A conversation's table sits beside its JSON and is named after it. Every other table lives in `Localization/`, one per kind of key:

| Key | Table | Example |
|---|---|---|
| A line or an option | the conversation's | `example_first_meeting.start.help` |
| A speaker's name | `speaker` | `speaker.example_stranger` |
| The interface's text: `ui.<area>.<name>`, where the area is the screen or system it appears on | `ui` | `ui.combat.attack`, `ui.hud.world_and_cycle` |
| A world's name, from the `World` enum in lowercase | `world` | `world.fantasy` |
| An enemy's name, held by its `EnemyData` | `enemy` | `enemy.strong` |

A new kind of content, such as items or skills, gets a pair of tables of its own in `Localization/` (`item.en.po` and `item.es.po`). Those tables are what reserve the prefix: no conversation may take the name of a shared table, and there is no other list to keep up to date.

**Godot only uses the tables `project.godot` lists** under `internationalization/locale/translations`. Every new conversation adds two paths to that list, which is easy to forget, so the validator compares the list with the `.po` files on disk, both ways.

### Placeholders

A text with something in it from the code names its placeholders: `{hp}/{max_hp}`, never `{0}/{1}`. Each language puts them in whatever order reads naturally, and whoever translates sees what goes where.

```csharp
_enemyHpLabel.Text = TextTemplate.Fill(Tr("ui.combat.hp"), ("name", Tr(EnemyData.NameKey)), ("hp", hp), ("max_hp", maxHp));
```

- **A placeholder is a word between braces.** Any other brace is plain text.
- **Values are written with the invariant culture,** so a number reads the same whatever the system's settings are. To format a number differently, pass it as a string.
- **A placeholder with no value is left showing,** instead of throwing. Pseudolocalization rewrites placeholders along with the rest of the text (`{hp}` comes back as `{h̀ṕ}`), and throwing there would break the very run meant to find untranslated text.
- **The validator checks that every language uses the same placeholders,** so a translation that loses one, or misspells it, fails CI.

### In the game

- **The starting language needs no code.** Godot starts in the system's language when a table exists for it (`es_AR` finds `es`) and falls back to English otherwise, and English is already Godot's default fallback, so `project.godot` does not repeat it. Remembering the player's choice waits for a place to store settings, in M9 at the latest.
- **A fixed text is a key, and Godot translates it by itself.** A `Control` whose `text` is a key shows its translation, and updates when the language changes: the Attack button holds `ui.combat.attack` in `combat.tscn`, and `Combat` assigns the result's key to its label.
- **A text built in code goes through `Tr()` and `TextTemplate`,** as the HP labels and the world and cycle label do. Those labels have `auto_translate_mode` set to *Disabled*; otherwise Godot would look the finished text up as a key again, and pseudolocalization would rewrite it twice. They are rebuilt on `NotificationTranslationChanged`, which Godot sends every node when the language changes.
- **The debug overlay switches the language** (see [Debug Tooling](#debug-tooling)), on every screen and while a fight pauses the tree. It replaced F2, a temporary key from #72.
- **The debug overlay's own text is the one exception to "every text is a key".** It is a tool, not part of the game, so it is in English, its panel has auto-translation off, and `GameKeysTests` skips `Debug/`.
- **Pseudolocalization checks that no text bypasses the tables.** It rewrites every translated text with accents and brackets (`[Åŧáćáŕ]`), so any text on screen without brackets did not come from a table. Turn it on for one run with an `override.cfg` beside `project.godot`, which `.gitignore` already lists:

  ```ini
  [internationalization]

  pseudolocalization/use_pseudolocalization=true
  ```

### Reading and validating

- **`TranslationTableReader`** reads a `.po` file and stops at its first problem, giving the line, just like `ConversationReader`. It reads comments, `msgid` and `msgstr`, texts split across quoted lines, and the escapes `\n`, `\t`, `\"` and `\\`. It refuses contexts and plurals rather than skipping them, since nothing needs them yet, and it refuses any line that is not part of the format. Entries marked `#, fuzzy` are kept but flagged: Godot ignores them, and a translation Poedit guessed would otherwise show as a raw key.
- **`LocalizationValidator`** checks every table together and reports every problem:
  - a file name or header that disagrees with its language;
  - a key outside its table's prefix;
  - a fuzzy or empty text;
  - a table or a key that one language lacks;
  - different placeholders in different languages;
  - two tables sharing a name;
  - any table `project.godot` does not register, or registers when the file does not exist.

  `EveryTableInTheProject_IsValid` runs it over the project.
- **`GameKeysTests`** checks every key the code, scenes and resources name against the shared tables, by searching for quoted strings that start with a table's name. Keys built at run time are invisible to that search, so each kind needs a test of its own; for the worlds' names, that test is `EveryWorld_HasAName`. It skips the flag names in calls such as `SetFlag("world.fantasy_closed", true)`, which `GameFlagsTests` checks instead.

## Decision Registry

The decision registry is what the game remembers of the player: every flag of the catalogue, the fights, and which optional worlds those open. It is in memory for now; saving it is M9's job, and the snapshot below is what M9 will save.

It follows the split of `CombatResolver` and `Combat`. The logic is engine-free in `Decisions/`, and the `DecisionRegistry` autoload is a thin node around it:

- **`DecisionState`** holds every flag's value and the history of fights, and implements `IFlagStore`, so the dialogue runner reads and writes it directly.
- **`WorldAccess`** reads `Decisions/world_access.json` and computes which optional worlds are open.
- **`DecisionRegistry`** loads both files in `_EnterTree`, through `FileAccess`, for the same reason `GameState` sets its `Instance` there: `Main` reads the autoloads from its own `_EnterTree`. It re-emits `DecisionState.FlagChanged` as a Godot signal, and starts each new cycle on `GameState.CycleChanged`. Outside code uses its `State`, rather than methods repeated on the node.

### Flags at run time

- **Only declared flags exist.** A name the catalogue does not declare, or one used with the wrong type, throws `ArgumentException`, whose message says which. It never answers with a default, so a misspelt flag cannot read as `false`.
- **A counter never goes below 0.** Taking 5 from 2 leaves 0, as with HP, and it also stops at `int.MaxValue` instead of overflowing. Throwing instead would cut a conversation short over an authoring mistake, and the debug tooling is where a counter that hit the floor shows. The floor is part of `IFlagStore`'s contract, so the tests' `InMemoryFlagStore` keeps it too.
- **`FlagChanged` fires only when a value actually changes,** once per flag. When one call changes several, it fires after all of them have their new values, so a listener never sees half a change.

Code names a flag as a string, as it does a translation key: `State.SetFlag("world.fantasy_closed", true)`. `GameFlagsTests` finds those calls by searching the source, and checks each name against the catalogue with its type. It searches for the calls, not for any string that starts like a flag, because `world.` is both a group of flags and the worlds' translation table; `GameKeysTests` skips the same calls for the same reason. A name built at run time is invisible to both. No code names a flag yet, since the demo's flags are set by its conversations, so `GameFlagsTests` cannot require finding a call; it requires having read `Main.cs` and `DecisionRegistry.cs` instead, and its pattern to match a sample call, so a broken search cannot pass as a clean game.

### Lifetimes and the slot's snapshot

- **`StartCycle`** resets every `cycle` flag, and nothing else. The autoload calls it on `GameState.CycleChanged`, which `GameState` emits before `WorldChanged` for exactly this: the new world's scene loads with the cycle's flags already cleared.
- **`TakeSnapshot`** copies what belongs to a save slot into a `SlotSnapshot`: the `cycle` and `run` flags and the history of fights. Persistent flags are never in it, since they belong to the game rather than to a slot. In memory a `run` flag and a `persistent` one behave the same; the snapshot is where they differ.
- **`Restore`** puts a snapshot back and leaves the persistent flags alone. A flag the snapshot does not mention goes back to its starting value. It refuses, and changes nothing, a snapshot that names an undeclared or persistent flag, uses a flag with the wrong type, or holds a negative counter. That is strict enough while every snapshot comes from the same catalogue; loading a save from an older version of the game is M9's to decide.

### Fights

`Main` calls `RecordCombat` when a fight ends, with the world, the cycle, the enemy's `Id` and the result. It adds a `CombatRecord` to the history and 1 to `combat.wins` or `combat.losses`, always together, so the totals and the history never disagree. The two counters are declared in the catalogue, and `DecisionState` refuses a catalogue without them. Dialogue can read the totals like any counter. The history is not a flag: only code reads it, it belongs to the slot like the `run` flags, and each entry keeps its cycle, so "in this cycle" is a filter.

**It is the last step of `Main.OnCombatFinished`, after unpausing,** on purpose. An exception thrown in a signal handler is logged with its stack, and the game goes on, but the rest of that handler does not run (verified headless). Anywhere earlier, a failure would leave the game paused under the combat overlay. There, the worst it can do is leave one fight out of the history.

An enemy's `Id` is a lowercase word in its `EnemyData`, separate from its `NameKey`, so the history keeps meaning the same when the name's text changes. It is empty by default, and `GameFlagsTests` requires every `EnemyData` resource to have one of its own.

### World access

`Decisions/world_access.json` holds one condition per optional world, in the dialogue's condition language:

```json
{
	"format": 1,
	"worlds": {
		"fantasy": "not world.fantasy_closed",
		"nightmare": "not world.nightmare_closed"
	}
}
```

It holds exactly `fantasy` and `nightmare`; the Real world has no condition, since every cycle starts there. `WorldAccess.Read` reads it through `StrictJson` like the catalogue, and `Validate` checks its flags against the catalogue, in the same words `DialogueValidator` uses; both share `FlagUses`. A test runs it over the project's file.

**That the Nightmare world needs the Fantasy world is code, not data:** `Unlocks` closes the Nightmare world whenever the Fantasy world is closed, whatever its own condition says, so no file can break the order. `GameState.Advance` asks the registry for the unlocks at the moment of moving on, with the flags as they are then, and stores nothing. The two `world.` flags are `cycle` flags that start `false`, so a cycle in which nothing sets them plays all three worlds.

## Debug Tooling

With around a hundred endings planned, checking them by playing each through by hand is not viable, so the decision registry has tooling of its own (#75). It is small on purpose and grows with the game. It exists in debug builds only: the `DebugTools` autoload frees itself when `OS.IsDebugBuild()` is false, and `DebugTools.Instance` stays null. Confirming nothing of it reaches a release export waits for the export configuration.

The logic is engine-free, as in the rest of the game. `DebugPreset` reads and checks a preset, `DebugArguments` reads the command line and `SnapshotShelf` keeps the snapshots, all three covered by tests. `DebugTools` and `DebugOverlay` are the nodes around them.

### The overlay

**F3 (the `debug_overlay` action) opens and closes `Debug/debug_overlay.tscn`.** `DebugTools` reads the action in `_Input`, so a focused text field in the overlay cannot swallow it, and adds the overlay under the root, after the main scene. It shows:

- where the run is, and a jump to any world and cycle, with a warning when the flags keep that world closed tonight;
- the language;
- every flag of the catalogue with its lifetime and a check box or a number, and its description as a tooltip;
- the snapshots, with a name of their own or a number, each with Restore and Delete;
- a button that resets the persistent flags;
- the history of fights.

**Every change goes through the registry's API,** so its signals fire as if the game had made it: a number is set by adding the difference with `AddToCounter`. The overlay follows `FlagChanged`, so a change the game makes while it is open shows too. Its rows are built from the catalogue each time it opens.

**Opening pauses the tree unless something already had, and closing unpauses it only if opening paused it,** so a fight or a forced conversation stays paused behind it. The one gap: a forced conversation that starts while the overlay is open, which only a world's fade in can do, is unpaused when it closes. While the box and `Combat` ignore input, nothing behind the overlay reacts to a key pressed in it (see [The box and its input](#the-box-and-its-input)).

### Snapshots and the persistent flags

A snapshot is `DecisionState.TakeSnapshot`'s: the cycle and run flags and the fights. The shelf keeps several in memory, each with a name or the lowest free number, and the world and cycle it was taken in; a name already on the shelf replaces that snapshot. **Restoring brings back the flags and the fights, not the world and cycle.** Jumping is a separate step, because a jump to another cycle resets the cycle flags, and a restore that also jumped would undo part of itself depending on the order of the two.

Persistent flags are shared by every save slot, so nothing in a slot can bring them back to a clean slate. `DecisionState.ResetPersistent` does, leaving the slot's flags and the fights alone.

### The jump

**A jump takes the same road as an exit:** `GameState.RequestJump` emits `JumpRequested`, and `Main` answers it as it answers a `WorldExit`, with the same guards from `ConversationRules`, the same fade, and then `GameState.JumpTo`, which moves the run with the signals `Advance` emits. A jump to another cycle therefore resets its cycle flags, and the swap keeps the guards against the phantom contact (see [World and Cycle Coordination](#world-and-cycle-coordination)); a jump that skipped them would produce fights the game never could. It can reach a world the flags keep closed, which is the one place the tool steps outside the game's rules, and the overlay says so. A jump `Main` refuses, during a fight, a transition or a conversation, is only printed, since a signal has no answer.

### Presets

A preset is a state for the game to start in. The project's presets live in `Debug/Presets/`:

```json
{
	"format": 1,
	"description": "Starts on the first evening with the Fantasy world closed, so the bed leads straight to the next morning.",
	"world": "real",
	"cycle": 1,
	"do": [
		"set world.fantasy_closed"
	]
}
```

- `format` and `description` are required; `world` (`real`, `fantasy` or `nightmare`), `cycle` (from 1) and `do` are not. The run starts in the Real world, cycle 1, unless the preset says otherwise.
- `do` holds effects in the dialogue's effect language (see [Conditions and effects](#conditions-and-effects)). Every flag starts at its starting value, so `add example.times_met 3` sets the counter to 3. A preset may change a persistent flag, since it describes the whole game, not one slot.
- It is read through `StrictJson` and `FlagEffectParser`, and `Validate` checks its flags against the catalogue in `FlagUses`' words. `EveryPresetInTheProject_IsValid` runs it over every preset in the project, and fails when it finds none.

**A preset is applied from the command line,** with the arguments after `--`, which Godot hands the game through `OS.GetCmdlineUserArgs()`:

```bash
godot --headless --path . --quit-after 800 -- --preset res://Debug/Presets/no_fantasy.json
godot --headless --path . --quit-after 800 -- --world fantasy --cycle 2 --do "set demo.stayed_up"
```

`--preset` takes a `res://` or an absolute path, once. `--world` and `--cycle` appear once at most, and `--do` as often as wanted; all three apply on top of the file, the effects after its own. **Anything else is an error, never skipped,** since a run that quietly ignored a misspelt argument would start in another state than the one asked for and look as if it had worked.

**`DebugTools` applies it in `_EnterTree`,** registered after `DecisionRegistry` under `[autoload]`, so the registry exists and `Main` has not yet read `GameState`. `GameState.StartAt` sets the start without any signal, so the preset's cycle flags are not reset; it refuses once the main scene is in the tree (the main scene is already `CurrentScene` while the autoloads enter, so it checks `IsInsideTree`). Then it prints the world, the cycle and every flag, so a headless run leaves its state in the log.

**A preset that cannot be applied stops the game with exit code 1,** and says why: a file that cannot be read or is malformed, an unknown argument, or a flag misused. A run that went on in the default state would look like one that worked, the same false result the terminal marker of a headless run exists to catch.

## Engine-Free Game Logic

Rules that are pure decisions — how much damage an attack does, whether a fight is over, who won — live in plain C# classes with no Godot base type and no `using Godot;`, next to the feature they belong to — `Combat/CombatResolver.cs`, `World/CycleProgression.cs`, `Player/DirectionResolver.cs`, the whole dialogue format in `Dialogue/`, the reading and checking of the translation tables in `Localization/`, the decision registry's state and world access in `Decisions/`, the debug tooling's presets, command line and snapshots in `Debug/`, the stat formulas, the growth of stats and the experience curves in `Stats/`, a character's values in `Characters/`, and the rules for conversations (`Dialogue/ConversationRules.cs`) and for facing something (`Player/Facing.cs`). The `Node` keeps what actually needs the engine: child nodes, input, timers, tweens, signals and UI.

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

Per-entity stats live in `Resource` subclasses marked `[GlobalClass]`, authored as `.tres` files (e.g. `Enemy/EnemyData.cs` with `Enemy/weak_enemy.tres` and `Enemy/strong_enemy.tres`). A new enemy type is a new `.tres` file, with an `Id` of its own for the decision registry's history of fights, plus its name in the two `Localization/enemy.*.po` tables, not new code or per-instance values typed into a scene, and adding a stat means adding one property instead of changing every method signature it travels through.

A resource's defaults are empty or zero, never one real entity's values. The editor leaves out of a `.tres` every property that equals its default, so when `EnemyData`'s defaults were the weak enemy's stats, saving `weak_enemy.tres` emptied it of them. `EnemyDataTests` requires every enemy to write its name, its HP and the experience it gives, so every enemy gives at least 1.

Global player state is the exception: it lives in the `PlayerStats` Autoload, since it has to persist across scenes.

A resource is a Godot type, so an engine-free class cannot take one. When plain logic needs a resource's numbers, the resource builds a plain value from them and the logic only ever sees that value: `Stats/StatTuning.cs` has a `ToValues()` that returns a `StatTuningValues`, which is what `StatFormulas` reads. See *Stat Formulas*. A character does the same: `Characters/CharacterData.cs` builds a `CharacterValues`. See *Characters and Levels*.

## Stat Formulas

`Game/Stats/` holds the formulas that turn a combatant's base stats into what a fight uses. Nothing in the game calls them yet: M7's debug panel shows their results, and M8's damage pipeline (accuracy, then evasion, then critical, then armor) is built on them. Expect M8 to change them.

| File | What it is |
|---|---|
| `BaseStats` | The seven base stats of one combatant as integers: health points, attack damage, armor, mana points, agility, luck and hit rate. It does not know whose they are, nor the level or the world that produced them. |
| `ActionKind` | `Attack` or `Skill`. Several formulas answer differently for the two. |
| `DiminishingReturns` | The one curve, `cap * stat / (stat + K)`. |
| `StatTuningValues` | Every tunable number, as a plain record. `ByAction` is a number with one value against attacks and another against skills. |
| `StatFormulas` | The formulas, built from a `StatTuningValues`. |
| `StatTuning`, `stat_tuning.tres` | The tuning as a resource and the game's one file of it. The only part that needs the engine. |

The same folder holds how stats grow with the level and what a level costs. See *Characters and Levels*.

### What they compute

Every result is a percentage written as the design writes it (**90 is 90%**, not 0.9), in `double`, and **never rounded**: rounding belongs to the end of whatever applies the result. Nothing here draws a random number. A formula returns a chance, and deciding whether it comes up needs a source of randomness the tests can control, which M8 adds.

| Method | Formula | Limits |
|---|---|---|
| `ArmorReduction(defender, kind)` | `cap * armor / (armor + K)` | approaches 90% against attacks, 80% against skills |
| `CriticalChance(attacker, kind)` | `cap * luck / (luck + K)` | approaches 95% on attacks, 90% on skills |
| `HitChance(attacker)` | the hit rate, 1 point is 1% | never below 25%, never above 100% |
| `CriticalDamage(attacker, kind)` | `150 + max(0, hit rate - 100) + attack damage / 5 + luck / 5` | at most 500% for attacks, 300% for skills |
| `EvasionRate(defender, kind)` | `armor / 30 + agility / 20 + luck / 10` | at most 50%; halved against skills |
| `EscapeRate(allies, enemies)` | `50 + allies' average agility / 20 + allies' average luck / 10 - enemies' average agility / 20 - enemies' average luck / 10` | never below 10%, never above 90% |

- **The curve needs no clamp.** It is 0 for a stat of 0, half the cap when the stat equals `K`, and approaches the cap without reaching it. `K` is the one knob of each use: the stat value that gives half the cap.
- **Hit rate above 100 is not lost.** It no longer improves the chance to hit, and feeds critical damage instead.
- **Evasion applies its cap first and the half after,** so a skill is evaded 25% of the time at most.
- **Evasion is the chance to evade an action that already landed,** which is why accuracy comes before it in M8's pipeline.
- **Escape uses each side's average,** so it does not depend on how many are on a side. Who counts on a side, such as whether a downed ally does, is the caller's to decide.

### A wrong input fails loudly

None of these mistakes would crash a formula. Each would return a believable percentage, so each is refused where the value is built, with an exception that names it:

- **A negative stat** is refused by `BaseStats`. A stat of 0 is valid, and gives 0% wherever it is the only input.
- **A tuning value of 0 or less, or one that is not a number,** is refused by `StatTuningValues`. So are a percentage above 100, a critical damage cap below its base, and an escape floor above its cap. A `K` of 0 would return the full cap for any stat.
- **An escape with nobody on a side** is refused by `EscapeRate`. An average over nobody, taken as 0, would answer with the base, as if both sides were evenly matched.
- **A kind of action that is neither** `Attack` nor `Skill` is refused, not treated as one of them.

Because the two types check themselves, no formula validates its numbers again.

### Tuning

Every number in the *Limits* column, the two bases (150 and 50) and the four `K` are provisional, and live in `Stats/stat_tuning.tres`, a `StatTuning` resource that can be edited in the Inspector. The divisors (`/ 5`, `/ 20`, `/ 30`, `/ 10`) are constants in `StatFormulas`: they are part of each formula's shape. The four `K` are 100 only so that the file has a value; choosing them is an open design decision.

- **`StatTuning`'s defaults are zero,** like every resource's (see *Stats as Resources*). No tuning value may be 0, so a value missing from the file is refused by `ToValues()` and never reaches a formula.
- **`StatTuningTests` reads the file as text** and requires it to write every value the resource exports, nothing else, and values `StatTuningValues` accepts. It reads the list of exports from `StatTuning.cs`, so a new one without its line in the file fails.
- **A new tuning number** needs four things: its export in `StatTuning`, its parameter in `StatTuningValues` with its check, its line in the file, and its name in `StatTuningTests`.
- **The formulas' own tests do not read the file.** They use `TestTuning`, with the design's numbers and round `K`s, so that tuning the game does not break them.
- **`ToValues()` cannot be unit-tested,** since `StatTuning` is a Godot type. That it hands each value to its own parameter was checked with a headless run; check it again when a value is added.

## Characters and Levels

A character is data: a `CharacterData` resource in `Game/Characters/`, authored as a `.tres` like an enemy. **Levels and stats are independent per character and per world,** so one file carries three sets of numbers. Nothing in the game uses the characters yet: the party that holds them, and what each one keeps while playing, come next in M7.

| File | What it is |
|---|---|
| `Characters/CharacterData` | The resource: an `Id`, a `NameKey`, a `RealAge`, and a `StatGrowth` for each world, held inside the same file as sub-resources. |
| `Stats/StatGrowth` | The resource for one world: for each of the seven stats, its value at level 1 and what each level adds. |
| `Characters/CharacterValues` | The same character as plain, checked values. What the engine-free rules read. |
| `Stats/StatLine`, `Stats/StatGrowthValues` | One stat's line, and the seven lines of one character in one world. |
| `Stats/ExperienceCurve`, `Stats/ExperienceCurves` | What each level costs in one world, and the curve of each world that has one. |
| `Stats/LevelProgress` | A level and the experience gathered towards the next. |

### Stats come from the level

**A stat is never stored.** It is a straight line, computed when it is needed and rounded down once:

```
stat = floor(base + growth * (level - 1))
```

So what a character keeps is a level, and changing a character's numbers changes every level of that character at once, saved games included. `CharacterValues.StatsAt(world, level)` returns the `BaseStats` the formulas read.

- **Growth may be a fraction,** and the two numbers are `decimal`, not `double`, because the result is rounded down. A growth of 0.29 over 100 levels is 28.999999999999996 in `double`, which rounds to 28 and not 29. Godot exports `double`; `StatLine.From` is the one place where they become `decimal`.
- **The rule is the same in the three worlds.** In the Real world the level is the character's age, `RealAge`, so `base` is the stat at the age of 1. `StartingLevel(world)` is the age there and 1 in the other two.
- **Neither number may be negative,** and a level below 1 is refused. A line of 0 and 0 is a stat the character does not have.
- **A character needs health points where they start:** 1 or more in each world at its starting level, or `CharacterValues` refuses them.

### Experience

Going from a level to the next costs:

```
cost = max(1, floor(base * level ^ exponent))
```

`ExperienceCurve` holds one world's `base`, `exponent` and highest level, and `Add(progress, award)` returns the new `LevelProgress`. The Fantasy and the Nightmare world each have a curve. **The Real world has none:** `ExperienceCurves.For(World.Real)` is `null`, and adding experience there returns the progress untouched.

- **One award may raise several levels.** What is left over stays as experience towards the next.
- **At the highest level nothing is kept:** the experience is 0 and the rest of the award is lost.
- **A `LevelProgress` holds the experience gathered since the last level, not a total.** A total would tie the level to the curve, and retuning the curve would move the level of every character already played.
- **A retuned curve never takes a level away.** If a level now costs less than a character already holds, they rise on the next award, even one of 0. If the highest level was lowered below theirs, they stay where they are.
- **Experience is a 64-bit number, and the highest level is data,** because the design keeps open a mode that goes far past the standard highest level. A curve whose last level would not fit in 64 bits is refused when it is built, so `ToNextLevel` cannot fail in the middle of handing out a fight's experience.

The six numbers (base, exponent and highest level, for each of the two worlds) are in `Stats/stat_tuning.tres`, and `StatTuning.ToExperienceCurves()` builds the curves. They are provisional, like the rest of the tuning, and follow its rules: none may be 0, and `StatTuningTests` requires the file to write each one.

**Every enemy says how much experience it gives,** in `EnemyData.Experience`.

### A character's file

- **`Id`** is a lowercase word (letters, digits and underscores, starting with a letter), unique among the characters, and the file is named after it: `demo_hero.tres`. It is what the party, the dialogue and a save will call the character, so it never changes with the text.
- **`NameKey`** is `speaker.<Id>`. A party member is always someone who speaks, so the name is written once, in `Localization/speaker.*.po`, in both languages.
- **A stat of 0 is not written.** `StatGrowth`'s defaults are zero and the editor leaves out every value equal to its default, so, unlike the tuning, a line missing under a world is a 0 and not a mistake.
- **A misspelt stat is a mistake Godot hides:** it ignores a name the resource does not have, and the stat is 0. `CharacterDataTests` refuses any name under a world that `StatGrowth` does not export.
- **`CharacterDataTests` reads every character's file as text** and rebuilds it as a `CharacterValues`, through the same checks the game applies. It also requires the id to be unique and to match the file's name.
- **`ToValues()` cannot be unit-tested,** on either resource. That each hands every number to its own stat was checked with a headless run; check it again when a property is added.
- **A character written by hand has no `uid://`** until the editor saves the file, like a scene. The editor may also reorder the worlds inside the file and rename their ids, which changes nothing.

The three characters in the folder (`demo_hero`, `demo_friend` and `demo_newcomer`) are **placeholders**, like M6's demo conversation: enough to build and test the party with. The placeholder protagonist starts the Fantasy world with the 20 HP and 4 attack that `PlayerStats` has today, so the two enemies keep giving one win and one loss.
