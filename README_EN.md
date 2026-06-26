> [中文](README.md) | **English** | [日本語](README_JA.md) | [한국어](README_KO.md)

# Odyssey Cards<br><small>Shoujo Odyssey Cards</small>

A Hearthstone-like turn-based card battle Roguelite — Godot 4.7 + C#.

> **Branch:** `dev` | **Status:** playable MVP, expanding<br>
> 175 `Scripts/*.cs` files, ~37,000 lines of game code. Core combat loop runs with collection, map routes, saves, localization, dev console, and runtime QA. Term glossary (Domain = STS2 Power, Unit = Hero+Minion, etc.) lives in the root `AGENTS.md` Architecture Rules section.

## Core Systems

### Card Combat

Turn-based combat on a 2×5 minion board. Mana, armor, weapons, domains, relic hooks, and heat-based damage pressure are present.

- **Minion**: board unit with Attack/Health and keywords
- **Spell**: card type exists; runtime uses the shared `Card` base path
- **Domain**: permanent Power (analogous to STS2 Power) triggered by combat events; time-limited mount effects (e.g. Shiyoru Raidenkou, Sutaraito Spirit next-turn bonus) use the StatusEffect channel, not Domain
- **Weapon**: hero equipment and skills
- **Hero**: armor is wired; 4 hero power implementations exist, and combat UI still needs per-hero runtime verification
- **Relic**: lifecycle-hook system exists; resource data is still being built
- **Heat**: global battle pressure connected to the damage pipeline

### Keywords

| Keyword | Effect |
|---------|--------|
| Charge | Can attack the turn it is summoned |
| Taunt | Enemy minions must attack this minion first |
| Battlecry | Triggers an effect when played from hand |
| Deathrattle | Triggers an effect when the minion dies |
| Windfury | Can attack twice each turn |
| Ambush | Once per turn when attacked, strikes back before the attacker |
| Impact | Negates all counter-damage while attacking, one-time use |

### Damage Calculation

Four-stage pipeline: `ADDITIVE → MULTIPLICATIVE → HEAT → CAPPING` via DamageResolver. Armor absorbs after the pipeline.

### AI / Intents

Each enemy is an independent actor with its own HP, MoveState chain, and intent. New intent classes live in `Scripts/AI/Intents/` and support multi-intent moves, dynamic damage previews, icons, and tooltips.

### Roguelike

Map routing, reward/discover screens, and event/shop/rest UI exist. Event/shop/rest flow is still being integrated with MapUI and battle rewards.

### Collection / Localization / Dev Console

CollectionUI handles browsing and deck editing. Localization is YAML-based (`zh.yaml` / `en.yaml`) through `Localization.T()` and `GameManager.LanguageChanged`. ChatScreen is an autoload with command groups for resources, damage, spawning, relics, battle jumps, and QA.

## Tech Stack

- **Engine**: Godot 4.7
- **Language**: C# (.NET 8.0, Godot.NET.Sdk/4.7.0)
- **Tests**: xUnit (10 Unit + 1 Integration; Integration skipped because Godot Resource runtime is required)
- **Platforms**: Windows; Android export script exists

## Project Structure

```
Scripts/
├── Core/ (35)           # CardData, DamageResolver, GameManager, HeroProfile, RarityColorScheme…
├── UI/ (39)             # CombatUI partials, CardUI, BoardUI, CollectionUI, MapUI, Shop/Rest/Event UI…
├── Card/ (15)           # Card, Minion, Hero, Weapon, StatusEffect, ActiveDomain, HeroPowers/*
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState
├── Combat/ (14)         # CombatManager + AttackTracker/SelectionSystem/DeathHandler/WeaponAttackSystem/DomainTriggerManager…
├── AI/ (27)             # EnemyEncounter, EnemyRegistry, Brains, Intents/(19)
├── Heat/ (2)            # HeatSystem + HeatDamageModifier
├── Relic/ (7)           # AbstractRelic, RelicManager, concrete relics
├── Roguelike/ (5)       # EventSelector, RoomData, GameRunState, EventData, BlessingData
├── Localization/ (5)    # YAML localization
└── Infrastructure/ (20) # ChatScreen, InputManager, HotkeyManager, MobileInputRouter, Commands/8
Resources/Cards/         # 37 .tres resources
Resources/Localization/  # zh.yaml / en.yaml
Scenes/                  # Main, Combat, Collection, Map + Card/Board/CombatPreview
```

### Architecture Highlights

- Pure C# domain: Card/Minion/Hero/Board/GameState/EnemyUnit do not inherit Node
- Programmatic UI: Combat.tscn is mostly containers; `[Tool]` preview scenes cover Card/Board/Combat
- CombatUI split into core/Layout/Refresh/Selection partial files
- CombatManager delegates to pure C# helper systems with constructor injection + Action callbacks
- C# `event Action<>`, no Godot `[Signal]`
- InputManager → HotkeyManager → scene UI
- Export-safe resource fallbacks for DirAccess limitations

## First Run After Cloning

> **Prerequisites**: [Godot 4.7 Mono](https://godotengine.org/download/) and [.NET 8.0 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <repo-url>
cd OdysseyCards
dotnet build                          # Compile C# assemblies (required)
# Open the project once with Godot_v4.7-stable_mono_win64.exe → the editor auto-rebuilds .godot cache
# Then press F5 to run
```

> [!IMPORTANT]
> **Run `dotnet build` first, then open with the Mono edition of Godot.** Skipping either step causes "MainMenu.cs missing" or "No loader found for resource" errors — `.cs` scripts require compiled assemblies to load.

## Build / Test / Export

```bash
dotnet build
dotnet build -c Release
dotnet test
dotnet format OdysseyCards.sln --verify-no-changes

./build_export.ps1 [-Debug] [-SkipBuild]
./build_android.ps1 [-SkipBuild] [-ExportOnly]
./package_release.ps1 [version] [-OpenFolder]
```

No GitHub Actions, Dockerfile, or Makefile currently. GUT is installed but has no GDScript tests.

## Scenes

| Scene | Path | Description |
|-------|------|-------------|
| Main Menu | `Scenes/Main.tscn` | Entry scene |
| Combat | `Scenes/Combat.tscn` | Battle scene, programmatic UI |
| Collection | `Scenes/Collection.tscn` | Collection and deck editor |
| Map | `Scenes/Map.tscn` | Roguelike route map |
| Card Preview | `Scenes/CardPreview.tscn` | Editor preview |
| Board Preview | `Scenes/BoardPreview.tscn` | Editor preview |
| Combat Preview | `Scenes/CombatPreview.tscn` | Editor preview |

## Autoload Singletons

- **GameManager** (`Scripts/Core/GameManager.cs`) — global state, card registry, persistence, language switching
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI scaling, current base 1152×648
- **ChatScreen** (`Scripts/Infrastructure/ChatScreen.cs`) — developer console
- **MobileInputHelper** (`Scripts/Infrastructure/MobileInputHelper.cs`) — legacy touch helper still used outside combat UI
- **MobileInputRouter** (`Scripts/Infrastructure/MobileInputRouter.cs`) — mobile touch routing and modal stack
- **InputManager** (`Scripts/Infrastructure/InputManager.cs`) — physical keys to logical actions
- **HotkeyManager** (`Scripts/Infrastructure/HotkeyManager.cs`) — action callback stack

## Known Limitations

- `Spell.cs` is never instantiated; runtime uses the shared `Card` path
- `RailPistolPassive.cs` and `SafeAreaContainer.cs` are currently isolated
- Shop/Rest/Event UI exists but is not fully wired into MapUI flow
- IronWill / Starlight Supply / Suppressing Fire / Restructure hero powers exist; combat UI still needs per-hero runtime verification
- No hand limit and fatigue is incomplete (`Status_Fatigue.tres` exists)
- `InfoScreen.cs` still uses deprecated Godot API `SplitOffset`

## License

This project uses a mixed license:

- **Code** (`Scripts/` `.cs` source files and project config): [MIT](LICENSE_CODE)
- **Art/Audio assets** (`Assets/` media): [CC BY 4.0](LICENSE_ASSETS)

## Acknowledgments

This project's architecture references [slay-the-model](https://github.com/wkzMagician/slay-the-model), a well-structured Slay the Spire core framework that provided valuable learning resources for card game architecture design.
