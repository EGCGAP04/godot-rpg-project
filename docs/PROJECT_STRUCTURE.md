# Project Structure

Every file in the repository. The [README](../README.md#project-structure) has the short version: the folders, with a line on each.

**A pull request that adds, removes or renames a file updates this tree.** The README's short tree changes only when a folder appears or goes.

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
│   │   └── Party.cs (+.uid)            # global singleton: loads the roster, holds the party's state
│   │
│   ├── Characters/                    # the characters, as resources
│   │   ├── CharacterData.cs (+.uid)       # [GlobalClass] Resource: id, name key, age and a growth per world
│   │   ├── CharacterRoster.cs (+.uid)     # [GlobalClass] Resource: the protagonist and everyone else
│   │   ├── CharacterRosterValues.cs (+.uid) # engine-free: the roster as plain, checked values
│   │   ├── CharacterValues.cs (+.uid)     # engine-free: a character as plain, checked values
│   │   ├── demo_friend.tres               # placeholder: the first recruit
│   │   ├── demo_hero.tres                 # placeholder: the protagonist, 20 HP and 4 atk in the Fantasy world
│   │   ├── demo_newcomer.tres             # placeholder: the one who takes a place in the party
│   │   └── roster.tres                    # every character the game has, the protagonist named
│   │
│   ├── Combat/                         # combat v1, and the engine-free core of v2 beside it
│   │   ├── AttackResult.cs (+.uid)     # engine-free: one attack worked out
│   │   ├── Combat.cs (+.uid)           # v1: turn state, timers, input, UI
│   │   ├── CombatEvent.cs (+.uid)      # engine-free: what happened in a fight
│   │   ├── CombatOrder.cs (+.uid)      # engine-free: what a member is told to do
│   │   ├── CombatResolver.cs (+.uid)   # v1: engine-free, covered by tests
│   │   ├── CombatResultText.cs (+.uid) # engine-free: what the screen says when a fight ends
│   │   ├── CombatRunner.cs (+.uid)     # engine-free: a fight, a round at a time
│   │   ├── CombatTuningValues.cs (+.uid) # engine-free: combat's tunable numbers
│   │   ├── Combatant.cs (+.uid)        # engine-free: one side's member while a fight lasts
│   │   ├── CombatantId.cs (+.uid)      # engine-free: a side and a position
│   │   ├── CombatantSetup.cs (+.uid)   # engine-free: what a combatant brings into a fight
│   │   ├── DamagePipeline.cs (+.uid)   # engine-free: accuracy, evasion, critical, armor
│   │   ├── ICombatRandom.cs (+.uid)    # engine-free: every roll a fight makes
│   │   ├── SeededCombatRandom.cs (+.uid) # engine-free: the game's dice, from a seed
│   │   ├── TurnOrder.cs (+.uid)        # engine-free: who acts when in a round
│   │   └── combat.tscn                 # v1: overlay scene, not a scene swap
│   │
│   ├── Debug/                         # debug builds only (#75)
│   │   ├── Presets/
│   │   │   ├── no_fantasy.json            # starts with the Fantasy world closed tonight
│   │   │   └── with_friend.json           # starts in the Fantasy world with the friend recruited
│   │   │
│   │   ├── DebugArguments.cs (+.uid)      # engine-free: reads the arguments after -- on the command line
│   │   ├── DebugOverlay.cs (+.uid)        # the overlay: flags, snapshots, jump and language
│   │   ├── DebugPreset.cs (+.uid)         # engine-free: reads a preset, checks it, applies it
│   │   ├── DebugTools.cs (+.uid)          # global singleton: the startup preset, and F3 opens the overlay
│   │   ├── DerivedStatsTable.cs (+.uid)   # engine-free: the derived stats the overlay shows
│   │   ├── PartyEdits.cs (+.uid)          # engine-free: a party snapshot with a level forced
│   │   ├── PartyPanel.cs (+.uid)          # the overlay's party section
│   │   ├── SnapshotShelf.cs (+.uid)       # engine-free: named snapshots of the registry and the party
│   │   └── debug_overlay.tscn             # opened and closed with F3
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
│   │   │   ├── demo_recruit.en.po
│   │   │   ├── demo_recruit.es.po
│   │   │   ├── demo_recruit.json          # M7's demo: a friend who joins the party if asked
│   │   │   ├── demo_replace.en.po
│   │   │   ├── demo_replace.es.po
│   │   │   ├── demo_replace.json          # a newcomer who takes the friend's place
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
│   │   ├── DialogueContext.cs (+.uid)     # engine-free: the flags and the party, as conditions and effects see them
│   │   ├── DialogueNames.cs (+.uid)       # the format's naming rules and special names
│   │   ├── DialogueRunner.cs (+.uid)      # engine-free: walks a conversation, line by line and choice by choice
│   │   ├── DialogueValidator.cs (+.uid)   # engine-free: checks every conversation against the catalogue, the roster and its text
│   │   ├── FlagCondition.cs (+.uid)       # engine-free: a condition as a tree, and its evaluation
│   │   ├── FlagConditionParser.cs (+.uid) # engine-free: condition text → tree, by recursive descent
│   │   ├── FlagEffect.cs (+.uid)          # engine-free: set, clear or add a flag; recruit or dismiss a character
│   │   ├── FlagEffectParser.cs (+.uid)    # engine-free: effect text → effect
│   │   ├── FlagSyntaxException.cs (+.uid) # a malformed condition or effect, and its column
│   │   ├── FlagTokenizer.cs (+.uid)       # engine-free: splits condition and effect text into tokens
│   │   ├── FlagUses.cs (+.uid)            # engine-free: the flags a condition reads or an effect writes
│   │   ├── IFlagStore.cs (+.uid)          # where conditions read flags and effects write them
│   │   ├── IPartyStore.cs (+.uid)         # where conditions ask about the party and effects change it
│   │   └── PartyUses.cs (+.uid)           # engine-free: the characters a condition or an effect names
│   │
│   ├── Enemy/
│   │   ├── Enemy.cs (+.uid)
│   │   ├── EnemyData.cs (+.uid)        # [GlobalClass] Resource holding the id, stats, experience and the name's key
│   │   ├── enemy.tscn
│   │   ├── strong_enemy.tres           # 40 HP, 7 atk — the player loses
│   │   └── weak_enemy.tres             # 10 HP, 3 atk — the player wins
│   │
│   ├── Localization/                      # the tables of no single conversation, one per kind of key
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
│   ├── Party/                             # the party's state; engine-free
│   │   ├── CharacterProgress.cs (+.uid)   # what one character keeps in one world
│   │   ├── ExperienceAward.cs (+.uid)     # what an award of experience does to each member
│   │   ├── PartySnapshot.cs (+.uid)       # what one save slot keeps of the party
│   │   ├── PartyState.cs (+.uid)          # members, progress per world, experience, snapshots
│   │   └── StatusSheet.cs (+.uid)         # what the status screen shows of one character in one world
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
│   ├── Stats/                             # stat formulas, growth and experience; engine-free but for the resources
│   │   ├── ActionKind.cs (+.uid)          # attack or skill
│   │   ├── BaseStats.cs (+.uid)           # the seven base stats of one combatant
│   │   ├── ByAction.cs (+.uid)            # a tuning number with a value per kind of action
│   │   ├── DiminishingReturns.cs (+.uid)  # cap * stat / (stat + K)
│   │   ├── ExperienceCurve.cs (+.uid)     # what each level costs in one world, and adding experience
│   │   ├── ExperienceCurves.cs (+.uid)    # the curve of each world; the Real world has none
│   │   ├── LevelProgress.cs (+.uid)       # a level and the experience towards the next
│   │   ├── StatFormulas.cs (+.uid)        # reduction, critical, hit, evasion and escape
│   │   ├── StatGrowth.cs (+.uid)          # Resource: base and growth of the seven stats in one world
│   │   ├── StatGrowthValues.cs (+.uid)    # the seven stats at a level, from their lines
│   │   ├── StatLine.cs (+.uid)            # one stat: base + growth * (level - 1), rounded down
│   │   ├── StatTuning.cs (+.uid)          # Resource: the tuning, editable in the Inspector
│   │   ├── StatTuningValues.cs (+.uid)    # the tuning as plain, checked values
│   │   └── stat_tuning.tres               # the game's tuning; every number is provisional
│   │
│   ├── UI/
│   │   ├── DialogueBox.cs (+.uid)      # shows a conversation and reads its input
│   │   ├── MenuRules.cs (+.uid)        # engine-free: when the player's menu can open
│   │   ├── ScreenFade.cs (+.uid)       # shared fade, used by combat and transitions
│   │   ├── StatusScreen.cs (+.uid)     # the status screen: one party member at a time
│   │   ├── StatusText.cs (+.uid)       # engine-free: the status screen's lines
│   │   ├── dialogue_box.tscn
│   │   ├── screen_fade.tscn
│   │   └── status_screen.tscn          # opened with open_menu, closed with it or cancel
│   │
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
│   ├── CONVENTIONS.md
│   └── PROJECT_STRUCTURE.md    # this file
│
├── tests/                      # outside res:// on purpose — see CONVENTIONS.md
│   └── Game.Tests/
│       ├── BaseStatsTests.cs
│       ├── CharacterDataTests.cs   # every character's file makes a character the rules accept
│       ├── CharacterRosterTests.cs # the roster lists every character once, and makes a party
│       ├── CharacterRosterValuesTests.cs
│       ├── CharacterValuesTests.cs
│       ├── CombatResolverTests.cs
│       ├── CombatResultTextTests.cs
│       ├── CombatRunnerTests.cs    # includes a whole fight of four against three
│       ├── CombatTuningValuesTests.cs
│       ├── CombatantTests.cs
│       ├── ConversationReaderTests.cs
│       ├── ConversationRulesTests.cs
│       ├── CycleProgressionTests.cs
│       ├── DamagePipelineTests.cs
│       ├── DebugArgumentsTests.cs
│       ├── DebugPresetTests.cs     # includes the test that checks every preset
│       ├── DecisionStateTests.cs
│       ├── DemoConversationsTests.cs # plays M6's demo against the real registry
│       ├── DemoRecruitmentTests.cs # plays M7's demo against the game's roster and a real party
│       ├── DerivedStatsTableTests.cs
│       ├── DialogueContextTests.cs
│       ├── DialogueNamesTests.cs
│       ├── DialogueRunnerTests.cs
│       ├── DialogueValidatorTests.cs # includes the tests that check every conversation and its text
│       ├── DiminishingReturnsTests.cs
│       ├── DirectionResolverTests.cs
│       ├── EnemyDataTests.cs       # every enemy writes its name, HP and experience
│       ├── ExperienceCurveTests.cs
│       ├── ExperienceCurvesTests.cs
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
│       ├── InMemoryPartyStore.cs   # stand-in for the party, in dialogue tests
│       ├── LocalizationValidatorTests.cs # includes the test that checks every table
│       ├── MenuRulesTests.cs
│       ├── PartyEditsTests.cs
│       ├── PartyProgressTests.cs
│       ├── PartySnapshotTests.cs
│       ├── PartyStateTests.cs
│       ├── PartyStoreContractTests.cs # the party and its stand-in behave alike
│       ├── PartyUsesTests.cs
│       ├── PoText.cs               # writes small .po tables for tests
│       ├── ProjectFiles.cs         # finds Game/ and its data files from inside a test run
│       ├── ScriptedCombatRandom.cs # the dice of the combat tests: rolls written by hand
│       ├── ScriptedCombatRandomTests.cs
│       ├── SeededCombatRandomTests.cs
│       ├── SnapshotShelfTests.cs
│       ├── StatFormulasTests.cs
│       ├── StatGrowthValuesTests.cs
│       ├── StatLineTests.cs
│       ├── StatTuningTests.cs      # the tuning file writes every value
│       ├── StatTuningValuesTests.cs
│       ├── StatusSheetTests.cs
│       ├── StatusTextTests.cs
│       ├── TestRoster.cs           # the characters, curves and party the party's tests use
│       ├── TestTuning.cs           # the tuning and the stats the formula and combat tests use
│       ├── TextTemplateTests.cs
│       ├── TranslationTableReaderTests.cs
│       ├── TurnOrderTests.cs
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

For architectural conventions (naming, node communication patterns, scene structure), see [CONVENTIONS.md](CONVENTIONS.md).
