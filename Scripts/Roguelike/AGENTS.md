# Scripts/Roguelike — 地图、事件、奖励与主题卡组

## Scope

Roguelite 流程数据与生成器。定义房间、事件、祝福、运行状态、主题卡组生成。不接 UI（UI 由 `Scripts/UI/MapUI.cs`、`ShopUI.cs` 等负责）。

## Map

| 文件 | 职责 | 注意 |
|------|------|------|
| `GameRunState.cs` | 运行状态机——血量、金币、卡组快照 | 跨战斗持久化 |
| `RoomData.cs` | 地图节点类型配置 | 战斗/商店/休息/事件 |
| `EventSelector.cs` | 战后奖励选择逻辑 | `ApplyReward` 已标记 `[Obsolete]` |
| `EventData.cs` | 事件内容数据 | |
| `BlessingData.cs` | 祝福/增益数据 | |
| `ThemedDeckGenerator.cs` | 主题卡组生成器——0 牌开局选 20 张主题卡组 | 与 `ThemeProfile` (Core/) 配合 |

## 当前接入状态

| 系统 | 状态 | 备注 |
|------|:---:|------|
| MapUI 路线选择 | ✅ 已接入 | |
| 主题卡组选择 | ✅ 已接入 | `ThemedDeckSelectUI` + 3 个 ThemeProfile |
| 战后奖励流 | ⚠️ 未统一 | `EventSelector.ApplyReward` 已 Obsolete，新奖励通道待整合 |
| Shop/Rest/Event UI | ⚠️ 存在但未完整接入 MapUI 流程 | UI 文件在 Scripts/UI/，事件内容数据在此 |

## Anti-Patterns

- 禁止在 Roguelike 数据类中引用 Godot API——保持纯 C#。
- 禁止 `EventSelector` 绕过 `ApplyReward` 后不更新文档。
- 禁止主题卡组生成器硬编码卡牌 ID——走 `ThemeProfile` 资源 + `CardMechanicTag`/`KeywordWeights`。
