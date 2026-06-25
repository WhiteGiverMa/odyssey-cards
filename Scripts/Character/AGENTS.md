# Scripts/Character — 玩家、指挥官核心与牌堆

## Scope

玩家实体与牌堆管理。`Player`（Godot Node）持有 `CommanderCore`（纯 C# 数据）作为战斗与 UI 之间的桥梁。`CombatManager` 也持有自己的 CommanderCore 引用——双层共享。

## Map

| 文件 | 职责 | 注意 |
|------|------|------|
| `Player.cs` | Godot Node——桥接 UI/场景树 | 持有 `_core: CommanderCore` **不是** `CombatManager._playerCore` |
| `CommanderCore.cs` | 纯 C# 指挥官数据——HP、法力、牌堆 | CombatManager 和 Player 各持一份引用 |
| `Deck.cs` | 构筑卡组——卡牌集合 | `internal setter` 机制供 CombatManager 共享牌堆 |
| `CombatDeckState.cs` | 战斗期牌堆状态——抽牌堆/手牌/弃牌堆 | 构筑上限 10~20，战斗牌堆可突破 |

## 双层 CommanderCore 模式

```
Player._core   ←→   CombatManager._playerCore
     ↑                    ↑
  场景树               战斗逻辑层
（同一实例引用）
```

- `Player` 是 UI/场景树的入口——配合 UI 拿手牌列表、展示 HP。
- `CombatManager` 是战斗逻辑的入口——战斗流程、回合、法力推进。
- **禁止混淆** `Player._core` 和 `CombatManager._playerCore`——它们是指向同一个 CommanderCore 的引用，但使用侧不同。
- `internal Deck setter` 允许 CombatManager 在战斗初始化时替换牌堆。

## Anti-Patterns

- 禁止在 `Player` 里写战斗规则逻辑——只做桥接。
- 禁止混淆 `Player._core` 与 `CombatManager._playerCore`——指向同一实例但层次不同。
- 禁止用旧档超限牌组静默截断——走 `DeckValidityService` 返回 invalid。
