# 可达交互原型 — Bootstrap 修复与旧架构清理

## TL;DR

> **Quick Summary**: 修复 OdysseyCards 炉石重构分支的初始化链路、双 Core 手牌 Bug、场景布局冲突和空牌组问题，使 Combat 场景加载后能自动发牌、打牌、攻击、结束回合，到达可交互原型状态。
>
> **Deliverables**:
> - 12 张起始牌组（6种 .tres × 2）自动加载
> - Combat.tscn 中所有旧架构 UI 节点清理
> - 双 CommanderCore 手牌 Bug 桥接修复
> - 完整 bootstrap 链：场景加载 → 自动初始化 → StartCombat → UI 就绪
> - 可交互原型：发牌 → 选牌 → 放随从 → 攻击 → 结束回合循环
>
> **Estimated Effort**: Quick（~30分钟）
> **Parallel Execution**: YES — 3 Waves
> **Critical Path**: Task 1（牌组加载）→ Task 3（双 Core 修复）→ Task 4（bootstrap 串联）→ Task 6（验证）→ F1–F4

---

## Context

### Original Request
从 refactor/hearthstone-rework 分支出发，到达一个可交互原型（能发牌、能打牌），并彻底清理旧架构遗留。

### Interview Summary
**Key Discussions**:
- 初始化入口: CombatManager._Ready() 用 CallDeferred 延迟到下一帧自动启动
- 测试敌人: 硬编码 30HP Hero，无牌组无 AI（仅需要存在感）
- 起始牌组: Resources/Cards/ 下 6 张 .tres 各 2 份 = 12 张
- Combat.tscn: 彻底删除旧 UI 节点（TopBar/BoardArea/BottomBar/VBoxContainer），仅保留 CombatManager + CombatUI 两个脚本节点
- 双 Core 手牌 Bug: 最小桥接——Hero 新增 Hand 属性，HandUI 从 _combat.PlayerHero 读手牌
- 测试策略: 无自动化测试，QA 场景验证
- 旧架构清理: 空目录（Editor/Infrastructure/Autoload）、旧场景引用一并清理

**Research Findings**:
- 6 张 .tres 卡牌存在：Spell_Alert(2费), Spell_Assault(3费), Spell_Strike(4费), Minion_18thRegiment(1费1/1), Minion_DetectiveSquad(2费2/3), Minion_LianshuScout(3费2/2)
- Hero.cs 已有 `DeckState` 属性暴露 CombatDeckState，仅需追加 `Hand` 委托属性
- Scripts/Editor/、Scripts/Infrastructure/ 目录为空可删除
- Combat.tscn 节点树: Combat(root) → CanvasLayer → [VBoxContainer+CombatUI+GameMessageLabel]

### Metis Review
**Identified Gaps** (addressed):
- F1 双 CommanderCore 手牌 Bug: Hero 新增 `public IReadOnlyList<Card> Hand => _core.Hand;` — 遵循 Hero 现有委托模式
- F2 _Ready 执行顺序: CombatManager._Ready() 中使用 `CallDeferred` 延迟 BootstrapCombat() 到下一帧
- Scope 边界: 补充了完整 IN/OUT 清单（11 项排除）
- 验收条件: 细化为一句话可执行链

---

## Work Objectives

### Core Objective
修复 Combat 场景的完整启动链路，使游戏从主菜单进入战斗后能自动完成初始化、发牌、并进入可交互的手牌/棋盘/回合循环。

### Concrete Deliverables
- `GameManager.cs`: CreateStartingDeck() 加载 6 个 .tres × 2
- `Hero.cs`: 新增 `Hand` 委托属性
- `HandUI.cs`: 手牌数据源从 `_player.Hand` 改为 `_combat.PlayerHero.Hand`
- `CombatManager.cs`: _Ready() 中添加 CallDeferred(BootstrapCombat) + BootstrapCombat() 方法
- `Combat.tscn`: 删除 CanvasLayer/MainContainer 及其所有子节点
- 删除 Scripts/Editor/、Scripts/Infrastructure/ 空目录

### Definition of Done
- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] 场景加载后控制台输出 `[CombatManager] ========== 战斗开始 ==========`
- [ ] 控制台输出卡牌名称列表（12 张牌已加载）
- [ ] 控制台输出 `[CombatUI] 初始化完成`
- [ ] Godot 编辑器加载 Combat.tscn 无报错

### Must Have
- 完整 bootstrap 链路（MainMenu → Combat.tscn → 自动初始化 → StartCombat → UI 就绪）
- HandUI 能实际显示手牌（修复双 Core Bug）
- 12 张起始牌组加载成功
- Combat.tscn 干净无旧节点
- 构建 0 错误

### Must NOT Have (Guardrails)
- 不得修改 PlayMinion/PlaySpell/MinionAttack/EndPlayerTurn 的游戏逻辑
- 不得新增 CommanderCore 实例（维持当前双 Core 架构，只做桥接）
- 不得添加英雄技能实现
- 不得添加敌人 AI
- 不得添加更多卡牌（严格 12 张）
- 不得添加胜利/失败结算 UI
- 不得修改 CardData/Card/Minion/Spell 类
- 不得用 try-catch 掩盖 _Ready 顺序问题

---

## Verification Strategy (MANDATORY)

> **ZERO HUMAN INTERVENTION** - ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: NO（xunit 声明但 0 测试）
- **Automated tests**: None
- **Framework**: N/A

### QA Policy
Every task MUST include agent-executed QA scenarios.
Evidence saved to `.omo/evidence/task-{N}-{scenario-slug}.{ext}`.

- **API/Backend**: Use Bash (dotnet build, grep 日志) — 验证构建 + 关键控制台输出
- **Library/Module**: Use Bash (dotnet build) — 验证 C# 编译

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — 无依赖，可并行):
├── Task 1: 加载起始牌组（GameManager.cs）[quick]
├── Task 2: 清理 Combat.tscn 旧 UI 节点 [quick]
└── Task 3: 双 Core 手牌桥接（Hero.cs + HandUI.cs）[quick]

Wave 2 (After Wave 1 — bootstrap 串联):
└── Task 4: CombatManager 自动初始化 + BootstrapCombat [deep]

Wave 3 (After Wave 2 — 清理收尾):
├── Task 5: 清理空目录和旧文件引用 [quick]
└── Task 6: 构建验证 + QA 场景 [quick]

Wave FINAL (After ALL tasks — 4 parallel reviews):
├── Task F1: Plan Compliance Audit (oracle)
├── Task F2: Code Quality Review (unspecified-high)
├── Task F3: Real Manual QA (unspecified-high)
└── Task F4: Scope Fidelity Check (deep)
```

```
Critical Path: Task 3 → Task 4 → Task 6 → F1-F4
Parallel Speedup: ~60% faster than sequential（Wave 1 三任务并行）
Max Concurrent: 3 (Wave 1)
```

---

## TODOs

- [x] 1. 加载起始牌组 — `GameManager.CreateStartingDeck()` 加载 6 张 .tres × 2

  **What to do**:
  - 修改 `GameManager.cs` 的 `CreateStartingDeck()` 方法
  - 用 `ResourceLoader.Exists()` 和 `GD.Load<CardData>()` 加载以下 6 个 .tres：
    - `res://Resources/Cards/Spell_Alert.tres`
    - `res://Resources/Cards/Spell_Assault.tres`
    - `res://Resources/Cards/Spell_Strike.tres`
    - `res://Resources/Cards/Minion_18thRegiment.tres`
    - `res://Resources/Cards/Minion_DetectiveSquad.tres`
    - `res://Resources/Cards/Minion_LianshuScout.tres`
  - 每种卡牌添加 2 张到牌堆列表
  - 文件缺失时日志记录错误并跳过（不 NRE）
  - 打印加载结果：`[GameManager] 起始牌堆已创建，共 N 张牌`

  **Must NOT do**:
  - 不得修改 Deck 类
  - 不得添加超过 12 张卡牌
  - 不得修改 .tres 文件本身

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3)
  - **Blocks**: Task 4 (bootstrap needs deck)
  - **Blocked By**: None

  **References**:
  - `Scripts/Core/GameManager.cs:121-128` — 当前空的 CreateStartingDeck()，需要替换
  - `Scripts/Core/CardData.cs:14-114` — CardData 类型定义，确认 GD.Load<CardData> 可用
  - `Resources/Cards/Spell_Alert.tres` — 参考 .tres 格式，2费法术

  **Acceptance Criteria**:
  - [ ] `dotnet build` → 0 errors
  - [ ] 控制台输出：`[GameManager] 起始牌堆已创建，共 12 张牌`
  - [ ] 若 .tres 缺失，输出：`[GameManager] 警告：未找到 {路径}`

  **QA Scenarios**:

  ```
  Scenario: 正常加载 — 全部 6 个 .tres 文件存在
    Tool: Bash (dotnet build)
    Preconditions: Resources/Cards/ 下 6 个 .tres 文件完好
    Steps:
      1. 执行 `dotnet build`
      2. 检查构建输出：exit code 0
    Expected Result: 0 errors, 0 warnings
    Failure Indicators: 构建失败或 CardData 类型不匹配
    Evidence: .omo/evidence/task-1-build.txt

  Scenario: 缺失 .tres 文件的降级处理
    Tool: Bash (dotnet build)
    Preconditions: 临时重命名一个 .tres 文件使其不可访问（但不实际删除）
    Steps:
      1. 分析代码：检查 CreateStartingDeck() 是否使用 ResourceLoader.Exists 或 try-catch
      2. 确认缺失文件只会被跳过，不会导致 NRE
    Expected Result: 代码有防御性检查，构建仍通过
    Failure Indicators: GD.Load 返回 null 后直接传给 new Card(null) → 潜在 NRE
    Evidence: .omo/evidence/task-1-missing.txt
  ```

  **Commit**: YES
  - Message: `fix(game): load 12-card starting deck from .tres files`
  - Files: `Scripts/Core/GameManager.cs`

---

- [x] 2. 清理 Combat.tscn 旧架构 UI 节点

  **What to do**:
  - 编辑 `Scenes/Combat.tscn`，删除 CanvasLayer 下旧的 `MainContainer`（VBoxContainer 及其所有子节点）
  - 保留：CombatManager 根节点（`script = ExtResource("1_combat")`）
  - 保留：CanvasLayer + CombatUI 节点（`script = ExtResource("2_ui")`）
  - 保留：CanvasLayer + GameMessageLabel 节点（`script = ExtResource("7_message")`）
  - 删除：CanvasLayer/MainContainer（含 TopBar/BoardArea/BottomBar 及其下所有 HandUI、HealthBar、EndTurnButton 等）
  - 删除不再使用的 ext_resource 引用（HandUI脚本、HealthBar脚本）
  - 检查并删除 CanvasLayer/Background（ColorRect），因为 CombatUI.BuildLayout 也会创建背景

  **Must NOT do**:
  - 不得删除 CombatUI 或 GameMessageLabel 节点
  - 不得修改 CombatManager 脚本引用
  - 不得修改任何 C# 文件

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3)
  - **Blocks**: Task 4 (bootstrap 依赖干净的场景)
  - **Blocked By**: None

  **References**:
  - `Scenes/Combat.tscn` — 当前 204 行的场景文件，需精简到 ~20 行
  - 目标节点结构：
    ```
    Combat (root, script=CombatManager)
      └── CanvasLayer
            ├── CombatUI (script=CombatUI)
            └── GameMessageLabel (script=GameMessageLabel)
    ```

  **Acceptance Criteria**:
  - [ ] `Scenes/Combat.tscn` 不再包含 VBoxContainer/MainContainer/TopBar/BoardArea/BottomBar/HandUI/HealthBar 节点
  - [ ] ext_resource 引用仅剩：CombatManager、CombatUI、GameMessageLabel
  - [ ] Godot 编辑器可正常打开 Combat.tscn

  **QA Scenarios**:

  ```
  Scenario: 场景精简后编辑器加载无报错
    Tool: Bash (验证文件内容)
    Steps:
      1. Read Scenes/Combat.tscn 确认无旧节点残留
      2. 检查 ext_resource 列表：只应包含 CombatManager、CombatUI、GameMessageLabel
      3. 检查无 HandUI 或 HealthBar 的 ext_resource 引用
    Expected Result: 场景文件 < 50 行，无旧节点，ext_resource ≤ 3 条
    Failure Indicators: 残留 VBoxContainer/HandUI/HealthBar 节点或引用
    Evidence: .omo/evidence/task-2-scene.txt
  ```

  **Commit**: YES
  - Message: `refactor(scene): strip old UI nodes from Combat.tscn`
  - Files: `Scenes/Combat.tscn`

---

- [x] 3. 双 Core 手牌桥接 — Hero 新增 Hand 属性 + HandUI 改数据源

  **What to do**:
  - `Hero.cs`: 在 `DeckState` 属性（line 56）下方新增：
    ```csharp
    /// <summary>
    /// 手牌列表。
    /// </summary>
    public IReadOnlyList<OdysseyCards.Card.Card> Hand => _core.Hand;
    ```
    需要添加 `using System.Collections.Generic;`（已有）
  - `HandUI.cs`: 修改 `RefreshHand()` 方法（line 62-63）：
    ```csharp
    // 改前：
    foreach (var card in _player.Hand)
    // 改后：
    foreach (var card in _combat.PlayerHero.Hand)
    ```
  - 移除 `HandUI.cs` 中对 `_player` 手牌数据的依赖

  **Must NOT do**:
  - 不得创建第三个 CommanderCore
  - 不得删除 CombatManager 中的 `_playerCore`（保持当前双 Core 架构）
  - 不得修改 CommanderCore 类
  - 不得修改 Hearthstone 核心的战斗逻辑

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2)
  - **Blocks**: Task 4 (bootstrap 需要手牌桥接)
  - **Blocked By**: None

  **References**:
  - `Scripts/Card/Hero.cs:31-56` — 现有委托属性模式（`CurrentHealth => _core.CurrentHealth`），新 `Hand` 属性遵循相同模式
  - `Scripts/Card/Hero.cs:56` — `public CombatDeckState DeckState => _core.CombatDeckState;`，已暴露 DeckState
  - `Scripts/Character/CommanderCore.cs:37` — `public List<OdysseyCards.Card.Card> Hand => CombatDeckState.Hand;`
  - `Scripts/UI/HandUI.cs:61-68` — RefreshHand() 中需修改的代码
  - `Scripts/UI/HandUI.cs:23` — `_combat` 字段已存在

  **Acceptance Criteria**:
  - [ ] `dotnet build` → 0 errors, 0 warnings
  - [ ] `Hero.Hand` 暴露手牌列表（IReadOnlyList<Card>）
  - [ ] `HandUI.RefreshHand()` 不再依赖 `_player.Hand`

  **QA Scenarios**:

  ```
  Scenario: 编译验证 — Hero.Hand 属性添加
    Tool: Bash (dotnet build)
    Steps:
      1. 执行 `dotnet build`
      2. 检查 exit code = 0
    Expected Result: 0 errors, 0 warnings
    Failure Indicators: 编译错误（类型不匹配、缺少 using）
    Evidence: .omo/evidence/task-3-build.txt

  Scenario: 接口一致性检查
    Tool: Bash (grep 验证)
    Steps:
      1. 检查 Hero.cs 中 Hand 属性签名：`public IReadOnlyList<OdysseyCards.Card.Card> Hand => _core.Hand;`
      2. 检查 HandUI.cs 中数据源：`_combat.PlayerHero.Hand`
      3. 验证 CommanderCore.Hand 返回 List<Card> 而 IReadOnlyList<Card> 兼容
    Expected Result: 类型链 Card → Card 兼容，List<T> 可隐式转为 IReadOnlyList<T>
    Failure Indicators: 类型不匹配导致编译错误
    Evidence: .omo/evidence/task-3-chain.txt
  ```

  **Commit**: YES
  - Message: `fix(card): bridge dual-core hand via Hero.Hand delegate`
  - Files: `Scripts/Card/Hero.cs`, `Scripts/UI/HandUI.cs`

---

- [x] 4. CombatManager 自动初始化 — BootstrapCombat 串联

  **What to do**:
  - 修改 `CombatManager.cs`，在现有 `_Ready()` 方法末尾添加 `CallDeferred(nameof(BootstrapCombat));`
  - 新增 `private void BootstrapCombat()` 方法：
    1. 从 `GameManager.Instance.CurrentPlayer` 获取 Player 引用
    2. 检查 Player 是否为 null，为 null 则记录错误并返回
    3. 检查 Player.Deck 牌组数量，为 0 则记录错误并返回
    4. 创建敌方英雄：`new Hero(new CommanderCore())` → 30HP 默认
    5. 调用 `Initialize(player, enemyHero)`
    6. 获取 `GetNode<CombatUI>("CanvasLayer/CombatUI")` 并调用 `combatUI.Initialize(player, this)`
    7. 调用 `StartCombat()`
    8. 全程用 GD.Print 记录每一步状态
  - 确保 CombatUI 节点路径正确（CanvasLayer/CombatUI）
  - 确保 CallDeferred 在 Clean 后不会残留回调

  **Must NOT do**:
  - 不得在 BootstrapCombat 外调用 Initialize/StartCombat
  - 不得修改 PlayMinion 等游戏逻辑方法
  - 不得添加 try-catch 掩盖错误（让 NRE 暴露并修复）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []
  - **Reason**: 需要理解 Godot 生命周期（_Ready/CallDeferred）和跨组件依赖，一步错则全链断裂

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 2 (sequential, depends on Tasks 1, 2, 3)
  - **Blocks**: Task 6 (verification)
  - **Blocked By**: Tasks 1, 2, 3

  **References**:
  - `Scripts/Combat/CombatManager.cs:80-84` — 当前 _Ready()，需要在末尾添加 CallDeferred
  - `Scripts/Combat/CombatManager.cs:95-112` — Initialize() 方法签名和内容
  - `Scripts/Combat/CombatManager.cs:120-165` — StartCombat() 方法，确认 SetupDrawPile → StartGame → 发牌流程
  - `Scripts/Core/GameManager.cs:31-32` — `CurrentPlayer` 属性，bootstrap 通过它获取 Player
  - `Scripts/Core/GameManager.cs:48-54` — _Ready() 注册 Instance，确保 Autoload 可用
  - `Scripts/UI/CombatUI.cs:142-166` — Initialize() 方法签名 `Initialize(Player player, CombatManager combat)`
  - `Scripts/UI/CombatUI.cs:125-132` — _Ready() 中的 BuildLayout()
  - `Scripts/Card/Hero.cs:89-92` — Hero 构造函数（接收 CommanderCore）
  - `Scripts/Character/CommanderCore.cs:87-96` — CommanderCore 默认构造函数（30HP）
  - `Scenes/Combat.tscn` — 确认 CombatUI 节点路径为 `CanvasLayer/CombatUI`

  **Acceptance Criteria**:
  - [ ] `dotnet build` → 0 errors, 0 warnings
  - [ ] CombatManager._Ready() 末尾有 `CallDeferred(nameof(BootstrapCombat));`
  - [ ] BootstrapCombat() 存在且包含完整的 7 步初始化链
  - [ ] 控制台日志依次输出：
    - `[CombatManager] _Ready — 单例已注册`
    - `[CombatManager] 初始化完成 — 玩家 30/30，敌方 30/30`
    - `[CombatManager] ========== 战斗开始 ==========`
    - `[CombatManager] 抽牌堆已设置，共 12 张牌`
    - `[CombatUI] 初始化完成`
    - 法力值/回合信息

  **QA Scenarios**:

  ```
  Scenario: 正常启动 — 从 MainMenu 进入 Combat 场景
    Tool: Bash (dotnet build)
    Preconditions: Task 1-3 已完成
    Steps:
      1. 执行 `dotnet build`，确认 0 errors
      2. 在 Godot 编辑器中运行项目（或通过 godot-mcp_run_project）
      3. 点击 MainMenu StartButton
      4. 检查控制台输出关键日志
    Expected Result:
      - 无 NRE/异常
      - [CombatManager] 初始化完成
      - [CombatManager] 战斗开始
      - [CombatManager] 抽牌堆已设置，共 12 张牌
      - [CombatUI] 初始化完成
    Failure Indicators: NRE、空牌堆警告、CombatUI 节点未找到
    Evidence: .omo/evidence/task-4-bootstrap-log.txt

  Scenario: 防御性检查 — Player 为 null 时不崩溃
    Tool: Bash (code review)
    Steps:
      1. 检查 BootstrapCombat() 中是否有 `if (player == null)` 守卫
      2. 检查牌组数量检查
      3. 验证 null 守卫后执行 return（不继续调用 Initialize）
    Expected Result: 防御性检查存在，不会 NRE
    Failure Indicators: 直接使用 GameManager.Instance.CurrentPlayer 而无 null 检查
    Evidence: .omo/evidence/task-4-guard.txt
  ```

  **Commit**: YES
  - Message: `feat(combat): auto-bootstrap combat via CallDeferred on _Ready`
  - Files: `Scripts/Combat/CombatManager.cs`

---

- [x] 5. 清理空目录和旧文件引用

  **What to do**:
  - 删除空目录：`Scripts/Editor/`、`Scripts/Infrastructure/`
  - 删除 `Autoload/` 目录（如存在且为空 — AGENTS.md 标注此目录为空但 project.godot 中有 Autoload 注册）
  - 检查 project.godot 中是否有指向已删除目录的 autoload 引用
  - 检查是否有其他 .cs 文件引用这些目录下的类（using OdysseyCards.Editor 等）

  **Must NOT do**:
  - 不得删除 project.godot 中有效的 autoload 注册
  - 不得删除有内容的目录

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Task 6)
  - **Blocks**: None
  - **Blocked By**: Task 2 (需要先确认哪些节点/引用被清理)

  **References**:
  - `AGENTS.md` — 标注 Scripts/Editor/ 和 Scripts/Infrastructure/ 为空
  - `project.godot` — 检查 autoload 配置

  **Acceptance Criteria**:
  - [ ] `Scripts/Editor/` 目录不存在
  - [ ] `Scripts/Infrastructure/` 目录不存在
  - [ ] `dotnet build` → 0 errors（无引用断链）

  **QA Scenarios**:

  ```
  Scenario: 编译验证 — 删除目录后无引用断链
    Tool: Bash (dotnet build)
    Steps:
      1. 删除目录后执行 `dotnet build`
      2. 检查 exit code = 0
    Expected Result: 0 errors, 0 warnings
    Failure Indicators: 有 .cs 文件引用已删除目录下的类
    Evidence: .omo/evidence/task-5-build.txt
  ```

  **Commit**: YES
  - Message: `chore: remove empty Editor/Infrastructure directories`
  - Files: deleted directories

---

- [x] 6. 完整构建验证 + 全链路 QA

  **What to do**:
  - 执行 `dotnet build` 确认 0 errors
  - 检查所有关键文件的最终状态
  - 验证修改的文件数量 = 预期（5个文件：GameManager.cs、Hero.cs、HandUI.cs、CombatManager.cs、Combat.tscn）
  - 验证无意外文件变更

  **Must NOT do**:
  - 不得执行 `dotnet format`（自动格式化可能引入非预期变更）
  - 不得运行 Godot 编辑器（无 GUI 环境）

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Task 5)
  - **Blocks**: None (this is the last implementation task)
  - **Blocked By**: Task 4

  **References**:
  - 所有已完成文件的最终状态

  **Acceptance Criteria**:
  - [ ] `dotnet build` → 0 errors, 0 warnings
  - [ ] `git diff --stat` 显示仅预期文件变更
  - [ ] 代码评审通过（无 `as any`、空 catch、console.log 等 AI slop）

  **QA Scenarios**:

  ```
  Scenario: 全量构建
    Tool: Bash (dotnet build)
    Steps:
      1. `dotnet build` 在项目根目录执行
      2. 检查输出中的 error 和 warning 数量
    Expected Result: Build succeeded. 0 Error(s), 0 Warning(s)
    Failure Indicators: 任何编译错误或警告
    Evidence: .omo/evidence/task-6-build.txt

  Scenario: 变更文件审计
    Tool: Bash (git diff --stat)
    Steps:
      1. `git diff --stat HEAD` 列出所有变更文件
      2. 验证变更文件列表 = GameManager.cs, Hero.cs, HandUI.cs, CombatManager.cs, Combat.tscn
    Expected Result: 5 个文件变更，无额外文件
    Failure Indicators: 意外文件变更（如 .uid 文件、project.godot 等）
    Evidence: .omo/evidence/task-6-diff.txt
  ```

  **Commit**: NO（此任务仅验证，不提交）

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.
> **Do NOT auto-proceed after verification. Wait for user's explicit approval.**

- [ ] F1. **Plan Compliance Audit** — `oracle`
  阅读计划全文。逐项检查 "Must Have"：验证每个实现是否存在（读取文件、运行 curl、运行命令）。逐项检查 "Must NOT Have"：在代码库中搜索禁止模式——如果发现则拒绝并附上 file:line。检查 `.omo/evidence/` 中的证据文件。将交付物与计划对照。
  输出：`Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  运行 `dotnet build`。审查所有变更文件中的：`as any`/`@ts-ignore`、空 catch、生产代码中的 console.log(GD.Print 日志不算)、注释掉的代码、未使用的 using。检查 AI slop：过度注释、过度抽象、通用命名（data/result/item/temp）。
  输出：`Build [PASS/FAIL] | Lint [PASS/FAIL] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Real Manual QA** — `unspecified-high`
  从干净状态开始。执行每个任务中的每个 QA 场景——按照确切步骤执行，捕获证据。测试跨任务集成（功能协同工作，非孤立测试）。测试边缘情况：空牌堆、缺失 .tres、null Player 引用。保存到 `.omo/evidence/final-qa/`。
  输出：`Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  对每个任务：读取 "What to do"，读取实际 diff（git log/diff）。验证 1:1——spec 中的每项都已构建（无遗漏），spec 外的每项都未被构建（无 scope creep）。检查 "Must NOT do" 合规性。检测跨任务污染：Task N 触碰了 Task M 的文件。标记未记录的变更。
  输出：`Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **Task 1**: `fix(game): load 12-card starting deck from .tres files` — `Scripts/Core/GameManager.cs`
- **Task 2**: `refactor(scene): strip old UI nodes from Combat.tscn` — `Scenes/Combat.tscn`
- **Task 3**: `fix(card): bridge dual-core hand via Hero.Hand delegate` — `Scripts/Card/Hero.cs`, `Scripts/UI/HandUI.cs`
- **Task 4**: `feat(combat): auto-bootstrap combat via CallDeferred on _Ready` — `Scripts/Combat/CombatManager.cs`
- **Task 5**: `chore: remove empty Editor/Infrastructure directories` — deleted dirs
- **Task 6**: 不提交（验证用）

---

## Success Criteria

### Verification Commands
```bash
# 构建
dotnet build
# 期望：0 Error(s), 0 Warning(s)

# 变更审计
git diff --stat HEAD
# 期望：5 个文件变更
```

### Final Checklist
- [ ] All "Must Have" present（5/5）
- [ ] All "Must NOT Have" absent（8/8）
- [ ] 构建通过
- [ ] 控制台日志包含完整启动链
- [ ] Final Wave 4 个 review 全部 APPROVE
- [ ] 用户明确确认 "okay"