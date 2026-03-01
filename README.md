# OdysseyCards

一个使用 Godot 4.6 和 C# 开发的卡牌游戏。

> **项目状态**: 概念开发阶段
> - 程序开发采用 Vibe Coding 方式
> - 美术资源暂时使用占位符

## 项目简介

OdysseyCards 是一款受《KARDS》启发的卡牌构建类 Roguelike 游戏。玩家通过收集卡牌、构建牌组来与敌人战斗。

## 技术栈

- **游戏引擎**: Godot 4.6
- **编程语言**: C# (.NET 8.0)
- **开发平台**: Windows

## 项目结构

```
OdysseyCards/
├── Assets/          # 美术、音频资源
├── Resources/       # 卡牌、敌人、遗物数据文件 (.tres)
├── Scenes/          # Godot 场景文件 (.tscn)
└── Scripts/         # C# 脚本
    ├── Card/        # 卡牌系统
    ├── Character/   # 玩家、敌人、角色基类
    ├── Combat/      # 战斗管理器
    ├── Core/        # 卡牌数据、游戏管理器
    └── UI/          # 血条、手牌UI、战斗UI
```

## 核心功能

- **卡牌系统**: 攻击、防御等多种卡牌效果
- **战斗系统**: 回合制战斗（玩家回合 -> 敌人回合）
- **角色系统**: 玩家和敌人角色，包含生命值、能量等属性
- **资源管理**: 使用 Godot Resource 系统管理卡牌数据

## 构建项目

### 在 Godot 编辑器中

1. 首次打开项目：`项目 -> 工具 -> C# -> 创建 C# 解决方案`
2. 构建：`构建 -> 构建解决方案` 或 `Ctrl+Shift+B`

### 使用命令行

```bash
dotnet build
```

发布版本：
```bash
dotnet build -c Release
```

## 代码规范

检查代码风格：
```bash
dotnet format OdysseyCards.sln --verify-no-changes
```

自动格式化：
```bash
dotnet format OdysseyCards.sln
```

## 主要场景

- **主菜单**: `Scenes/Main.tscn`
- **战斗场景**: `Scenes/Combat.tscn`

## 自动加载单例

- **GameManager**: 游戏全局管理器 (`Scripts/Core/GameManager.cs`)

## 开发文档

详见 `DevDocs/` 目录：
- `dev_designdoc.md` - 设计文档
- `dev_note.md` - 开发笔记
- `dev_todolist.md` - 待办事项
