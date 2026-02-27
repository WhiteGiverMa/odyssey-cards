# OdysseyCards - Godot 4.6 C# Card Game

## Directory Structure

```
OdysseyCards/
├── Assets/          # Art, Audio resources
├── Resources/       # Card, Enemy, Relic data files (.tres)
├── Scenes/          # Godot scenes (.tscn)
└── Scripts/         # C# scripts
    ├── Card/        # Card system
    ├── Character/   # Player, Enemy, Character base
    ├── Combat/      # CombatManager
    ├── Core/        # CardData, GameManager
    └── UI/          # HealthBar, HandUI, CombatUI
```

## Key Notes

- GameManager is Autoload singleton
- Main scene: `Scenes/Main.tscn`
- Combat flow: Player turn -> Enemy turn -> repeat
- CardData is Resource, Card is runtime instance

## Build

Godot Editor: `Project -> Tools -> C# -> Create C# Solution` (first time)
Then: `Build -> Build Solution` or `Ctrl+Shift+B`
