# godot-rpg-project

![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)
![Godot](https://img.shields.io/badge/Godot-4.6.2-478cbf?logo=godotengine&logoColor=white)
[![Release](https://img.shields.io/github/v/release/EGCGAP04/godot-rpg-project?logo=github&color=success)](https://github.com/EGCGAP04/godot-rpg-project/releases/latest)

2D pixel art turn-based RPG, built in **Godot 4.6.2** with **C#**.

> **Tentative** project name — the final name of the game will be decided later. This repo is used in the meantime as the development project's identifier.

## Project status

🚧 In development — foundation phase.

The cycle is playable end to end: walk through the Real, Fantasy and Nightmare worlds in turn, move between them, and watch the cycle counter advance. Touching an enemy starts a turn-based fight that can be won or lost. In the Real world, a neighbor can be talked to by facing it and pressing `confirm`; one answer skips that night's dream, and the next morning remembers which answer was given. All art is placeholder (solid-colour rectangles and tinted tiles).

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

The rules that do not need the engine — combat resolution, the world/cycle progression, movement direction, the dialogue format, the translation tables and the decision registry — are covered by unit tests that run without it. Some of them check every conversation, every translation table and every data file in the project, so a broken conversation, a missing translation or a misspelt flag fails the build. The solution includes the test project, so from the same `Game/` folder:

```bash
dotnet test "Godot RPG Project.sln" --configuration Debug
```

CI runs this on every pull request into `main` and every push to it, along with `dotnet format --verify-no-changes`, so a formatting mismatch fails the build too. CodeQL also scans the C# code and the workflows on those same events and once a week, and reports what it finds as code scanning alerts rather than failing the build.

## Project Structure

The project follows a **feature-based** folder structure: each system has its own folder containing its scenes, scripts, and assets together, instead of splitting by file type. The Godot project itself lives inside `Game/`, keeping the repository root limited to project metadata (README, LICENSE, CHANGELOG, security policy, docs, GitHub config).

```text
godot-rpg-project/
│
├── .github/                    # Issue/PR templates, CI, Dependabot
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.yml
│   │   ├── chore.yml
│   │   ├── config.yml
│   │   ├── docs.yml
│   │   ├── feature.yml
│   │   └── refactor.yml
│   │
│   ├── workflows/
│   │   └── build.yml           # restore → build → test → format
│   │
│   ├── dependabot.yml
│   └── pull_request_template.md
│
├── Game/                       # res:// — the Godot project
│   ├── Autoloads/
│   │   ├── DecisionRegistry.cs (+.uid) # global singleton: loads the decision registry, starts each cycle
│   │   ├── GameState.cs (+.uid)        # global singleton: current world and cycle
│   │   └── PlayerStats.cs (+.uid)      # global singleton: player HP and attack
│   │
│   ├── Combat/
│   │   ├── Combat.cs (+.uid)           # turn state, timers, input, UI
│   │   ├── CombatResolver.cs (+.uid)   # engine-free, covered by tests
│   │   └── combat.tscn                 # overlay scene, not a scene swap
│   │
│   ├── Decisions/
│   │   ├── CombatRecord.cs (+.uid)        # engine-free: one fight in the history
│   │   ├── DecisionState.cs (+.uid)       # engine-free: every flag's value, the fights, the lifetimes
│   │   ├── FlagCatalogue.cs (+.uid)       # engine-free: reads the catalogue of flags
│   │   ├── SlotSnapshot.cs (+.uid)        # engine-free: what one save slot keeps
│   │   ├── WorldAccess.cs (+.uid)         # engine-free: reads world_access.json, computes the unlocks
│   │   ├── flags.json                     # every flag, with its type and lifetime
│   │   └── world_access.json              # the condition that opens each optional world
│   │
│   ├── Dialogue/
│   │   ├── Conversations/                 # one JSON file per conversation, its text beside it
│   │   │   ├── demo_evening.en.po
│   │   │   ├── demo_evening.es.po
│   │   │   ├── demo_evening.json          # M6's demo: talking to the neighbor, a voluntary conversation
│   │   │   ├── demo_morning.en.po
│   │   │   ├── demo_morning.es.po
│   │   │   ├── demo_morning.json          # the next morning, forced: it remembers the answer
│   │   │   ├── example_first_meeting.en.po # its text, one table per language
│   │   │   ├── example_first_meeting.es.po
│   │   │   ├── example_first_meeting.json # the format's reference example, in two parts
│   │   │   ├── example_second_meeting.en.po
│   │   │   ├── example_second_meeting.es.po
│   │   │   └── example_second_meeting.json
│   │   │
│   │   ├── Conversation.cs (+.uid)        # engine-free: a conversation's nodes, lines and options
│   │   ├── ConversationLoader.cs (+.uid)  # reads a conversation by its id, through FileAccess
│   │   ├── ConversationReader.cs (+.uid)  # engine-free: a conversation's JSON → Conversation
│   │   ├── ConversationRules.cs (+.uid)   # engine-free: what each kind of conversation allows
│   │   ├── DialogueNames.cs (+.uid)       # the format's naming rules and special names
│   │   ├── DialogueRunner.cs (+.uid)      # engine-free: walks a conversation, line by line and choice by choice
│   │   ├── DialogueValidator.cs (+.uid)   # engine-free: checks every conversation against the catalogue and its text
│   │   ├── FlagCondition.cs (+.uid)       # engine-free: a condition as a tree, and its evaluation
│   │   ├── FlagConditionParser.cs (+.uid) # engine-free: condition text → tree, by recursive descent
│   │   ├── FlagEffect.cs (+.uid)          # engine-free: set, clear or add a flag
│   │   ├── FlagEffectParser.cs (+.uid)    # engine-free: effect text → effect
│   │   ├── FlagSyntaxException.cs (+.uid) # a malformed condition or effect, and its column
│   │   ├── FlagTokenizer.cs (+.uid)       # engine-free: splits condition and effect text into tokens
│   │   ├── FlagUses.cs (+.uid)            # engine-free: the flags a condition reads or an effect writes
│   │   └── IFlagStore.cs (+.uid)          # where conditions read flags and effects write them
│   │
│   ├── Enemy/
│   │   ├── Enemy.cs (+.uid)
│   │   ├── EnemyData.cs (+.uid)        # [GlobalClass] Resource holding the id, stats and the name's key
│   │   ├── enemy.tscn
│   │   ├── strong_enemy.tres           # 40 HP, 7 atk — the player loses
│   │   └── weak_enemy.tres             # 10 HP, 3 atk — the player wins
│   │
│   ├── Localization/                      # the tables of no single conversation, one per kind of key
│   │   ├── LocaleSwitchKey.cs (+.uid)     # temporary: F2 switches the language, until #75
│   │   ├── LocalizationNames.cs (+.uid)   # the languages and how a table's file is named
│   │   ├── LocalizationValidator.cs (+.uid) # engine-free: checks every table and its registration
│   │   ├── TextTemplate.cs (+.uid)        # engine-free: fills a text's {named} placeholders
│   │   ├── TranslationTable.cs (+.uid)    # engine-free: one table's keys and texts
│   │   ├── TranslationTableReader.cs (+.uid) # engine-free: a .po file → TranslationTable
│   │   ├── enemy.en.po                    # enemies' names
│   │   ├── enemy.es.po
│   │   ├── speaker.en.po                  # the names of who speaks in conversations
│   │   ├── speaker.es.po
│   │   ├── ui.en.po                       # the interface's text
│   │   ├── ui.es.po
│   │   ├── world.en.po                    # the worlds' names
│   │   └── world.es.po
│   │
│   ├── Player/
│   │   ├── Direction.cs (+.uid)
│   │   ├── DirectionResolver.cs (+.uid) # engine-free: strongest direction wins, last pressed breaks ties
│   │   ├── Facing.cs (+.uid)            # engine-free: whether the player is facing something
│   │   ├── Player.cs (+.uid)
│   │   └── player.tscn
│   │
│   ├── Shared/
│   │   ├── Fonts/                         # .gitkeep only so far
│   │   ├── Scripts/
│   │   │   ├── DataFileException.cs (+.uid) # a malformed data file: which, where and what
│   │   │   └── StrictJson.cs (+.uid)        # strict JSON reading shared by the data files
│   │   │
│   │   └── Theme/                         # .gitkeep only so far
│   │
│   ├── UI/
│   │   ├── DialogueBox.cs (+.uid)      # shows a conversation and reads its input
│   │   ├── ScreenFade.cs (+.uid)       # shared fade, used by combat and transitions
│   │   ├── dialogue_box.tscn
│   │   └── screen_fade.tscn
│   ├── World/
│   │   ├── CyclePosition.cs (+.uid)    # engine-free: a world plus its cycle number
│   │   ├── CycleProgression.cs (+.uid) # engine-free: which world comes next
│   │   ├── Interactable.cs (+.uid)     # Area2D the player talks to by facing it
│   │   ├── World.cs (+.uid)            # the Real / Fantasy / Nightmare enum
│   │   ├── WorldExit.cs (+.uid)        # Area2D that ends a world on contact
│   │   ├── WorldScene.cs (+.uid)       # base class the three world scenes share
│   │   ├── WorldUnlocks.cs (+.uid)     # engine-free: which worlds are reachable
│   │   ├── floor_placeholder.png (+.import)
│   │   ├── wall_placeholder.png (+.import)
│   │   ├── tileset.tres
│   │   ├── fantasy_world.tscn          # the old test room, tinted; keeps both enemies
│   │   ├── interactable.tscn           # something to talk to, with a body of its own
│   │   ├── nightmare_world.tscn        # placeholder room, dark tint, no enemies yet
│   │   ├── real_world.tscn             # placeholder room, holds the bed and the neighbor
│   │   └── world_exit.tscn             # reusable bed / exit trigger
│   │
│   ├── Godot RPG Project.csproj
│   ├── Godot RPG Project.sln   # also covers Game.Tests
│   ├── Main.cs (+.uid)         # coordinates worlds, the player and combat
│   ├── icon.svg (+.import)
│   ├── main.tscn               # entry point scene
│   └── project.godot           # autoloads, input map, rendering, translation tables
│
├── docs/
│   └── CONVENTIONS.md
│
├── tests/                      # outside res:// on purpose — see CONVENTIONS.md
│   └── Game.Tests/
│       ├── CombatResolverTests.cs
│       ├── ConversationReaderTests.cs
│       ├── ConversationRulesTests.cs
│       ├── CycleProgressionTests.cs
│       ├── DecisionStateTests.cs
│       ├── DemoConversationsTests.cs # plays M6's demo against the real registry
│       ├── DialogueNamesTests.cs
│       ├── DialogueRunnerTests.cs
│       ├── DialogueValidatorTests.cs # includes the tests that check every conversation and its text
│       ├── DirectionResolverTests.cs
│       ├── EnemyDataTests.cs       # every enemy writes its name and HP
│       ├── FacingTests.cs
│       ├── FlagCatalogueTests.cs
│       ├── FlagConditionParserTests.cs
│       ├── FlagConditionTests.cs
│       ├── FlagEffectParserTests.cs
│       ├── FlagEffectTests.cs
│       ├── FlagTokenizerTests.cs
│       ├── FlagUsesTests.cs
│       ├── Game.Tests.csproj
│       ├── GameFlagsTests.cs       # every flag the code names is declared; every enemy has an id
│       ├── GameKeysTests.cs        # every key the game's code and scenes name has its text
│       ├── InMemoryFlagStore.cs    # stand-in for the decision registry
│       ├── LocalizationValidatorTests.cs # includes the test that checks every table
│       ├── PoText.cs               # writes small .po tables for tests
│       ├── ProjectFiles.cs         # finds Game/ and its data files from inside a test run
│       ├── TextTemplateTests.cs
│       ├── TranslationTableReaderTests.cs
│       ├── WorldAccessTests.cs
│       └── WorldUnlocksTests.cs
│
├── .editorconfig
├── .gitattributes
├── .gitignore
├── CHANGELOG.md
├── LICENSE
├── README.md
└── SECURITY.md
```

`.cs (+.uid)` means Godot's generated `.cs.uid` file sits next to the script and is committed with it.

For architectural conventions (naming, node communication patterns, scene structure), see [docs/CONVENTIONS.md](docs/CONVENTIONS.md). The [wiki](https://github.com/EGCGAP04/godot-rpg-project/wiki) gathers, on one page, links to every part of the documentation and to the milestones and releases.

## Screenshots

_(Will be added once there's playable content)_

## Security

Please don't report a vulnerability in a public Issue: [SECURITY.md](SECURITY.md) explains how to report it privately.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
