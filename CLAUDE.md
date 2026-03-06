# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Lint, and Run Commands

### Build
```bash
# Build the C# solution
dotnet build

# Release build
dotnet build -c Release
```

Note: First-time setup requires opening the project in Godot Editor and running `Project -> Tools -> C# -> Create C# Solution`.

### Lint/Format
```bash
# Check code style (requires UTF-8 encoding on Windows for Chinese analyzer messages)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; dotnet format OdysseyCards.sln --verify-no-changes

# Auto-format code
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; dotnet format OdysseyCards.sln
```

### Run
- Main scene: `Scenes/Main.tscn`
- Combat scene: `Scenes/Combat.tscn`
- Run via Godot Editor's Play button or build standalone via export templates.

## Code Style and Conventions

- **Private fields**: `_camelCase` with underscore prefix
- **Public members**: `PascalCase`
- **Interfaces**: `IPascalCase` with I prefix
- **Namespaces**: `OdysseyCards.Module` (no `Scripts` prefix, e.g., `Scripts/Card/Unit.cs` → `OdysseyCards.Card`)
- **Comments**: Use Chinese comments for complex logic
- **Encoding**: UTF-8, CRLF line endings, 4-space indentation
- **Nullable reference types**: Enabled
- **Implicit usings**: Disabled (explicit using statements required)

## Architecture Overview

OdysseyCards is a tactical card game built with Godot 4.6 and C# (.NET 8.0). The architecture is inspired by [slay-the-model](https://github.com/wkzMagician/slay-the-model) and features a damage resolver pipeline, resource-based card data, and a turn-based combat system on a node-based battlefield map.

### Core Systems

#### Damage System (`Scripts/Core/DamageResolver.cs`)
The **single source of truth** for all damage calculations. Uses a 4-phase pipeline in a specific order:
1. **ADDITIVE**: Addition/subtraction (Strength +3, Defense -2)
2. **MULTIPLICATIVE**: Multiplication/division (Vulnerable 1.5x, Weak 0.75x)
3. **CAPPING**: Limits damage (Immune caps at 0, Intangible caps at 1)
4. **Clamp**: Ensures non-negative result

Key interfaces:
- `IDamageSource`: Combatants that deal damage
- `IDamageTarget`: Combatants that take damage
- `IDamageModifier`: Extensible damage modifiers with a `DamagePhase` enum

**Critical**: Phase order must be preserved - additive before multiplicative before capping.

#### Card System
- **CardData**: Resource files (`.tres`) defining card properties (cost, stats, tags, effects)
- **CardBase**: Abstract runtime class for all cards
- **Unit**: Deployable battlefield units with health, attack, range, action cost
- **Order**: Instant effect cards
- **Tag System**: Extensible card tags via `TagDefinition` and `TagImplementations`

**Key distinction**: `CardData` (Resource) is the static definition, while `Card` is the runtime instance created via `CardFactory`.

#### Map System
- **BattleMap**: Grid-based tactical battlefield
- **MapNode**: Individual grid cells with owner properties and distance from headquarters
- **Headquarters**: Player/Enemy base structures
- **MapEdge**: Connections between nodes

Units move along edges and attack based on range (shortest path calculation). Distance is calculated as number of nodes traversed.

#### Combat Flow
- **CombatManager**: State machine handling turn order
  - Turn states: `PlayerTurn` → `EnemyTurn` → `Victory`/`Defeat`
  - Selection modes: `DeployUnit`, `MoveUnit`, `AttackTarget`
- Energy-based card playing (max 3, increases with turns)
- Random first turn determination (first player: 4 cards/1 energy, second player: 5 cards/0 energy)
- Draw 1 card at start of each player turn

### Autoload Singletons
- **GameManager** (`Scripts/Core/GameManager.cs`): Global state, player deck, HQ health, run progression
- **UIScaler**: UI scaling management

### Localization System
Static manager (`Scripts/Localization/Localization.cs`) with YAML-based translations:
- Language files: `Resources/Localization/{lang}.yaml` (e.g., `en.yaml`, `zh.yaml`)
- Usage: `Localization.T("key")` or with parameters: `Localization.T("deal_damage", new { damage = 5 })`
- `LocalStr`: Wrapper class for localized string properties
- `ConcatLocalStr`: Operator overloading for string concatenation with localized values

Event `OnLanguageChanged` fires on language switches.

### Deck System
- **Finite deck thickness** (KARDS-style): Cards played are consumed (not cycled back)
- Exception: Cards with "rotation" tag return to draw pile at random position
- Units with "rotation" tag return to deck when defeated
- **No discard cycling** at turn end
- Hand retention between turns (no mandatory discard)
- Draw 5 cards at combat start, 1 per turn start
- Deck size limits require card removal when adding new cards (via `DeckAdjustment`)

## Data-Driven Design
Card data, enemy definitions, and other game content use Godot's Resource system (`.tres` files). This separates content from code for easy balance tuning and modding.

## External References
- **slay-the-model**: `G:\dev\slay-the-model` - Reference implementation for card game architecture
- **slay-the-model docs**: `G:\dev\slay-the-model\docs` - Architecture documentation

## Documentation
- `DevDocs/PROJECT_DESIGN.md` - Game design specifications
- `.trae/documents/tag_definition.md` - Card tag definitions
- `.trae/rules/project_rules.md` - Project-specific coding standards
- `DevDocs/` - Development notes and TODO lists
- `AGENTS.md` - Quick reference for AI agents

## Game Design Concepts
- **KARDS-style**: Limited finite decks, no discard cycling, hand retention, card costs increase each turn
- **Tactical positioning**: Units occupy map nodes and attack based on range/position
- **Headquarters**: Attackable base with persistent HP across combats (max 8 HP)
- **Chapter progression**: 4 acts, first 3 are random 6 rooms + boss, act 4 is fixed pattern (rest-shop-elite-boss)
