# OdysseyCards - Godot 4.6 C# Card Game

## Directory Structure

详见 [.trae/documents/file_structure.md](./.trae/documents/file_structure.md) - 完整文件结构文档

### 概览

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

## Documentation

| 文档 | 说明 |
|-----|------|
| [project_design.md](./.trae/documents/project_design.md) | 项目设计方针文档 |
| [project_spec_record.md](./.trae/documents/project_spec_record.md) | 项目规范记录文档 |
| [tag_definition.md](./.trae/documents/tag_definition.md) | 词条定义字典 |
| [file_structure.md](./.trae/documents/file_structure.md) | 完整文件结构文档 |
| [slay_the_model_architecture.md](./DevDocs/slay_the_model_architecture.md) | slay-the-model架构学习笔记 |

## External Reference

| 资源 | 路径 | 说明 |
|-----|------|------|
| slay-the-model | `G:\dev\slay-the-model` | Python卡牌游戏参考实现 |
| slay-the-model docs | `G:\dev\slay-the-model\docs` | 架构文档目录 |

## Key Notes

- GameManager is Autoload singleton
- Main scene: `Scenes/Main.tscn`
- Combat flow: Player turn -> Enemy turn -> repeat
- CardData is Resource, Card is runtime instance

## Build

Godot Editor: `Project -> Tools -> C# -> Create C# Solution` (first time)
Then: `Build -> Build Solution` or `Ctrl+Shift+B`

## Lint

Run `dotnet format OdysseyCards.sln --verify-no-changes` after code changes to check code style.

## Git

完成代码修改后，及时进行git操作并提交到仓库。
