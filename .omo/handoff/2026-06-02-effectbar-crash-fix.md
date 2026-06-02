HANDOFF CONTEXT
===============

USER REQUESTS (AS-IS)
---------------------
- 阅读上个会话的交接文档。"G:\dev\odyssey-cards\.omo\handoff\2026-06-02-animosity-centipede-handoff.txt"。阅读并审计可能的修复方案。"G:\dev\odyssey-cards\docs\draft\issue_1_root_cause_fix.md"。这可能是一个较大的架构问题。期望干净重构修复，可接受一定程度过度设计。更多澄清请提问。
- 现在开始修复，P0完成后验证，再让你决定是否要继续P1
- 保存当前会话信息到文档，原子化提交改动

GOAL
----
- 继续从已完成的 EffectBar 崩溃修复出发，只在出现新证据时再评估是否需要做 P1 的 CombatUI 刷新合并。

WORK COMPLETED
--------------
- 我先通读了上个会话交接文档 `.omo/handoff/2026-06-02-animosity-centipede-handoff.txt` 和修复草案 `docs/draft/issue_1_root_cause_fix.md`，并追了 `Scripts/UI/EffectBar.cs`、`Scripts/UI/BoardUI.cs`、`Scripts/UI/CombatUI.cs`、`Scripts/Card/Minion.cs`、`Scripts/Combat/CombatManager.cs` 的实际链路。
- 我并行调了 explore 子代理和 Oracle，确认根因更像 `EffectBar` 的 UI 生命周期竞态，而不是 `Animosity` 业务逻辑本身。
- 我确认了 `BoardUI` 中的 `CanvasLayer` 不能轻易移除，因为 `docs/notepads/effect-display-implementation.md` 记录过它是为修复 EffectBar 被 HandArea 遮挡、hover 失效而引入的。
- 我参考 STS2 的安全树操作模式，补了一份笔记到 `docs/notepads/sts2-godot-tree-safe-ops-2026-06-02.md`。
- 我完成了 P0：只修改 `Scripts/UI/EffectBar.cs`，将 `Populate()` 改为签名比对 + deferred rebuild，清旧 icon 时先 `Disable()`，再从树移除并 `QueueFree()`，同时删掉重复 hover 通道，只保留 `_Notification(NotificationMouseEnter/Exit)`。
- 我给 tooltip 显示/隐藏补了树状态与删除态保护，让 `HideTooltip()`、`ShowTooltip()`、icon hover 在 queued-for-deletion 场景下更稳。
- 我实际跑了 Godot，用 DevConsole 在战斗场景中直接召唤 `骑士I型`、加入 `敌意`、把 `敌意` 施放到 `骑士I型`，并 hover 两个效果图标验证 tooltip。
- 我决定先不做 P1，因为 P0 已经过真实场景验证并命中根因，当前扩大改动面收益不够确定。

CURRENT STATE
-------------
- `Scripts/UI/EffectBar.cs` 的 P0 修复已经完成且通过 `lsp_diagnostics`。
- `dotnet build` 通过，0 error；仍有项目既有 warning，与本次修复无关。
- 运行期验证通过：`骑士I型` 被施放 `敌意` 后，`PlayerEffectBar_2` 正常显示两个图标（🛡 granted taunt、💢 animosity），hover 两个图标都会出现 tooltip，未闪退。
- 我已明确决定当前不继续做 P1（`CombatUI` 的 deferred refresh 合并）。
- 当前工作区除了本次修复外，还有未提交的其他变更：`.gitignore`、`project.godot`、`build_export.ps1`、`export/`、`export_presets.cfg`。这些不应混入本次原子提交。

PENDING TASKS
-------------
- 是否需要做 P1：仅当后续再观察到同帧手牌/棋盘刷新导致的新不稳定、重复刷新性能问题，或其他 UI 刷新链崩溃时再评估。
- 可以考虑补一个更自动化的 QA 入口，覆盖“敌意贴骑士I型后双图标 hover 不崩”的场景。
- 当前 todo 已全部完成：P0 修改、编译校验、Godot 运行验证都已完成。

KEY FILES
---------
- Scripts/UI/EffectBar.cs - 本次 P0 修复的唯一代码改动，负责效果 icon 的安全重建与 hover/tooltip 生命周期。
- Scripts/UI/BoardUI.cs - 通过 `CanvasLayer` 承载每个槽位的 `EffectBar`，是本次明确保留、不回退的历史修复约束。
- Scripts/UI/CombatUI.cs - 当前未改，但它的 `OnHandChanged` 即时刷新与 `RefreshAll()` 叠加是潜在 P1 入口。
- Scripts/Card/Minion.cs - `GetDisplayableEffects()` 与 granted taunt 逻辑决定了 `骑士I型` 会比 `第18团` 多一个图标。
- Scripts/Combat/CombatManager.cs - `Animosity` 的业务逻辑入口；本次确认它不是根因。
- docs/draft/issue_1_root_cause_fix.md - 本次审计的修复草案来源。
- docs/notepads/effect-display-implementation.md - 说明了为什么 `EffectBar` 当年必须迁到 `CanvasLayer`。
- docs/notepads/sts2-godot-tree-safe-ops-2026-06-02.md - 我本会话新增的 STS2 参考笔记。
- .omo/handoff/2026-06-02-animosity-centipede-handoff.txt - 上个会话关于 Animosity / 机械蜈蚣 / 崩溃 issue 的原始交接。
- .omo/handoff/2026-06-02-effectbar-crash-fix.md - 本次会话交接文档。

IMPORTANT DECISIONS
-------------------
- 我决定把修复边界收在 `EffectBar`，不把 `EffectBar` 从 `CanvasLayer` 挪回普通布局，因为那会回退之前已修过的 HandArea 遮挡 / hover 失效问题。
- 我决定不做 `CombatUI` 的 `RequestRefreshAll()` 风格合并，理由是 P0 已足以通过真实复现验证，P1 会扩大改动面但当前收益不确定。
- 我把问题定性为“同帧树改动 + hover/tooltip 生命周期竞态”，而不是 `Animosity` 效果或 granted taunt 业务逻辑错误。
- 我保留了嵌套类结构，没有进一步拆成顶层 `EffectIcon.cs` / `EffectTooltip.cs`，因为 P0 已通过验证，先不再扩改。

EXPLICIT CONSTRAINTS
--------------------
- 期望干净重构修复，可接受一定程度过度设计。更多澄清请提问。
- 现在开始修复，P0完成后验证，再让你决定是否要继续P1
- 保存当前会话信息到文档，原子化提交改动

CONTEXT FOR CONTINUATION
------------------------
- 如果下个会话继续工作，优先先看这次 handoff，再看 `docs/notepads/effect-display-implementation.md`，避免误把 `CanvasLayer` 当成冗余设计删掉。
- 运行验证时我用的是 MCP + DevConsole 的这条路径：进入 `Scenes/Combat.tscn`，`/summon_player minion_knight_type1 2`，`/token spell_Animosity`，`/mana 10`，再真实施法到槽位 2 的 `骑士I型`。
- 运行后不要把 `project.godot`、`.gitignore`、`build_export.ps1`、`export/`、`export_presets.cfg` 这些当前工作区中的无关变更顺手提交；本次应保持只提交 `Scripts/UI/EffectBar.cs` 和 handoff 文档。
- 如果未来又出现 hover 相关崩溃，先检查 `EffectBar` 是否重新引入了同步 `QueueFree + AddChild`、双 hover 通道或缺少删除态保护，再考虑进入 P1。
