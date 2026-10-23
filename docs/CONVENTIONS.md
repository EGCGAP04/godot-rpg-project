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
├── Combat/          # Turn-based combat system: turn order, actions
├── World/           # Exploration maps: tilemaps, collisions, transitions
├── Dialogue/        # Branching dialogue: the conversation files, their reader, runner and validator
├── Decisions/       # The flag catalogue, the decision registry's state, and what opens each world
├── Localization/    # Translation tables of no single conversation, their reader and validator
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
- **`DialogueRunner`** walks one conversation a step at a time: a line, a choice, or the end. It is passive — whoever drives it asks what to show and calls `Advance` or `Choose` — and `DialogueRunner.Start` already applies the first line's effects. Conditions are evaluated when they are reached, so the effects of earlier lines count; a line's effects apply when it is shown, and an option's when it is chosen.
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
- **F2 switches to the next language,** on every screen and while a fight pauses the tree. This is `Localization/LocaleSwitchKey.cs`, a temporary autoload. #75's debug tooling replaces it, and removing it means deleting that file and its line under `[autoload]`.
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

Code names a flag as a string, as it does a translation key: `State.SetFlag("world.fantasy_closed", true)`. `GameFlagsTests` finds those calls by searching the source, and checks each name against the catalogue with its type. It searches for the calls, not for any string that starts like a flag, because `world.` is both a group of flags and the worlds' translation table; `GameKeysTests` skips the same calls for the same reason. A name built at run time is invisible to both.

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

## Engine-Free Game Logic

Rules that are pure decisions — how much damage an attack does, whether a fight is over, who won — live in plain C# classes with no Godot base type and no `using Godot;`, next to the feature they belong to — `Combat/CombatResolver.cs`, `World/CycleProgression.cs`, `Player/DirectionResolver.cs`, the whole dialogue format in `Dialogue/`, the reading and checking of the translation tables in `Localization/`, and the decision registry's state and world access in `Decisions/`. The `Node` keeps what actually needs the engine: child nodes, input, timers, tweens, signals and UI.

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

Global player state is the exception: it lives in the `PlayerStats` Autoload, since it has to persist across scenes.
