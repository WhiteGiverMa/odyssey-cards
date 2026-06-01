> [中文](README.md) | **English** | [日本語](README_JA.md)

# Odyssey Cards<br><small>Shoujo Odyssey Cards</small>

A Hearthstone-like turn-based card battle Roguelite — Godot 4.6 + C#

> **Branch:** `main` | **Status:** Playable MVP
> 59 `.cs` files, ~17,700 lines of code. Full turn-based combat loop operational, with card collection, map routes, and save system.

## Core Systems

### Card Combat

Turn-based combat on a 2×5 minion board (5 slots per side). Mana crystal system (start at 1, +1 per turn, max 10).

- **Minion**: Placeable on board, has Attack/Health, supports 7 keywords
- **Spell**: Cast from hand, resolves immediately
- **Domain**: Persistent field effect that affects global rules
- **Weapon**: Hero equipment providing attack power and weapon skills
- **Hero**: Each has a hero power (TBD) and armor mechanic

### Keywords

| Keyword | Effect |
|---------|--------|
| Charge | Can attack the turn it is summoned |
| Taunt | Enemy minions must attack this minion first |
| Battlecry | Triggers an effect when played from hand |
| Deathrattle | Triggers an effect when the minion dies |
| Windfury | Can attack twice each turn |
| Ambush | Once per turn when attacked, strikes back before the attacker |
| Impact | When attacking, negates all counter-damage (one-time use) |

### Damage Calculation

Three-stage pipeline: `ADDITIVE → MULTIPLICATIVE → CAPPING` (DamageResolver). Minimum damage clamped to 1.

### AI System

Spire-style intent rotation with 3 enemy types:

- **Cultist**: HP 20, pattern Attack(6)→Attack(6)→Defend(5)
- **SlimeBoss**: HP 40, pattern Attack(8)→Summon(1)→Defend(4), summons 1/1 slimes
- **WolfRider**: HP 12, pattern Attack(5), consistent damage output

### Roguelike

Post-battle 3-choice loot (EventSelector + RewardUI), Fisher-Yates shuffle. Map route selection (MapUI).

> ⚠️ EventSelector post-battle reward logic is complete but not yet wired into the combat loop.

### Card Collection

CollectionUI provides card browsing and deck editing. Features rarity-based color coding, adaptive description display, and delete confirmation. Deck has a soft cap.

### Localization

YAML-based localization system (`Scripts/Localization/`), Chinese/English bilingual support. All UI text refreshes dynamically via `GameManager.LanguageChanged` event.

### Dev Console

`DevConsole` (Autoload singleton) — press `` ` `` to toggle. Supports 11+ commands: `/damage`, `/draw`, `/mana`, `/heal`, `/armor`, `/end`, etc., for rapid testing and debugging.

### Pause Menu

ESC or button-triggered fullscreen overlay. Includes resume, settings (language switch), save, and quick save/load.

### Save System

SaveDataManager + GameSaveData provides game progress persistence.

## Tech Stack

- **Engine**: Godot 4.6
- **Language**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **Testing**: xUnit (4 test files, 303 lines)
- **Platform**: Windows

## Project Structure

```
Scripts/
├── Core/ (16)           # CardData, DamageResolver, GameManager (Autoload), Keyword, CardType, SaveDataManager…
├── UI/ (15)             # CombatUI, BoardUI, HandUI, CardUI, CollectionUI, MapUI, PauseMenu, DiscoverUI, RewardUI…
├── Card/ (9)            # Card, Minion, Spell, Hero, Weapon, WeaponSkill, ActiveDomain, StatusEffect (pure C#)
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (3)          # CombatManager (1740 lines), Board, GameState (pure C#)
├── AI/ (1)              # IntentAI (Cultist/SlimeBoss/WolfRider)
├── Roguelike/ (3)       # EventSelector, RoomData, GameRunState
├── Localization/ (5)    # YAML-based localization system
└── Infrastructure/ (1)  # DevConsole (Autoload) — developer console
Resources/Cards/         # 16 card data .tres (7 spells + 6 minions + 3 domains)
Resources/Localization/  # zh.yaml / en.yaml translation files
Scenes/                  # Main.tscn, Combat.tscn, Collection.tscn, Map.tscn (4 scenes)
```

### Architecture Highlights

- **Programmatic UI**: CombatUI and child components are created entirely in code, no .tscn dependency (Combat.tscn only provides layout containers)
- **Pure C# Core**: Card/Minion/Hero/Board/GameState do not inherit Godot Node — zero coupling with the scene tree
- **Dual CommanderCore**: Player and CombatManager each maintain a CommanderCore, sharing the deck via `internal Deck setter`
- **C# Events**: No Godot `[Signal]` — all events use `event Action<...>`
- **Pull-Mode UI Refresh**: Driven by `CombatUI.RefreshAll()`, no `_Process` polling
- **Auto-Init**: `CallDeferred` auto-starts combat on scene load, 12-card starting deck

## Build

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Format check (CI)
dotnet format OdysseyCards.sln --verify-no-changes

# Auto-format
dotnet format OdysseyCards.sln

# Run tests
dotnet test
```

## Scenes

| Scene | Path | Description |
|-------|------|-------------|
| Main Menu | `Scenes/Main.tscn` | Entry scene |
| Combat | `Scenes/Combat.tscn` | Combat scene, programmatic UI layout |
| Collection | `Scenes/Collection.tscn` | Card collection and deck editing |
| Map | `Scenes/Map.tscn` | Roguelike route selection |

## Autoload Singletons

- **GameManager** (`Scripts/Core/GameManager.cs`) — global state, cross-combat persistence, language switching
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI scaling, base resolution 1152×648
- **DevConsole** (`Scripts/Infrastructure/DevConsole.cs`) — developer console, toggle with `` ` `` key

## Known Limitations

- ⚠️ **Spell.cs never instantiated** — CombatManager uses Card base class for all cards (dead code)
- ⚠️ **EventSelector not wired** — post-battle reward logic is complete but has no call site
- ⚠️ **Hero powers not implemented** — IHeroPower interface is empty
- ⚠️ **No hand limit / no fatigue** — drawing from empty deck is unhandled

## License

This project uses a mixed license:

- **Code** (`.cs` source files under `Scripts/` and project config files): [MIT](LICENSE_CODE)
- **Art/Audio assets** (images, audio, and other media under `Assets/`): [CC BY 4.0](LICENSE_ASSETS)

## Acknowledgments

This project's architecture references [slay-the-model](https://github.com/wkzMagician/slay-the-model), a well-structured Slay the Spire core framework that provided valuable learning resources for card game architecture design.
