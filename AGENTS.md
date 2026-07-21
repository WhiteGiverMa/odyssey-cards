# OdysseyCards（少女星途卡牌） — Godot 4.7 C# · 类炉石 Roguelite 卡牌

**Updated:** 2026-06-25
**Scale:** 192 `Scripts/*.cs`（含 `Scripts/AssemblyInfo.cs`）· ~37,000 行 · 52 `.tres` 卡牌/状态资源 · 7 `.tscn` 场景（4 正式 + 3 预览）

## 项目文化标签

`#少女 #可爱 #微涩情 #轻小说 #轻松愉快 #中式&日式二次元 #轻科幻`

## Start

- 本文件是公共版。本地规则见 `AGENTS_LOCAL.md`（gitignored）。
- 工作语言中文。代码注释中文。子代理调度中文。
- 新增功能后 → 更新本文件。局部规则见各子目录 `AGENTS.md`。
- `wiki/` 是概念库，不是实现事实；进入 `wiki/` 先读 `wiki/AGENTS.md`。
- 本文保持“广而浅”：写入口、边界、雷区，不写详设。

## Map

```
Scripts/
├── Core/ (42)      # CardData, GameManager(Autoload), DamageResolver, HeroProfile, RarityColorScheme, SaveDataManager, VersionInfo…
├── UI/ (41)        # CombatUI partials, BoardUI, HandUI, CardUI, CollectionUI, MapUI, InfoScreen, Shop/Rest/Event UI…
├── Card/ (15)      # Card, Minion, Hero, Weapon, StatusEffect, ActiveDomain, HeroPowers/*（纯 C#）
├── Character/ (5)  # Player(Node), CommanderCore, Deck, CombatDeckState
├── Combat/ (14)    # CombatManager + AttackTracker/SelectionSystem/DeathHandler/WeaponAttackSystem/DomainTriggerManager 等拆分模块
├── AI/ (30)        # EnemyEncounter, EnemyRegistry, 多敌人 Brain + Intents/(20) MoveState/Intent 类型体系
├── Heat/ (2)       # HeatSystem + HeatDamageModifier，全局热力伤害倍率
├── Relic/ (7)      # AbstractRelic, RelicManager, 5 个具体藏品
├── Roguelike/ (5)  # EventSelector, RoomData, GameRunState, EventData, BlessingData
├── Localization/ (5)# YAML 多语言 (LocalStr, ConcatLocalStr, ILocalizable, YamlParser)
└── Infrastructure/ (23, Commands/9) # ChatScreen, InputManager, HotkeyManager, MobileInputRouter, SceneLifecycleGuard…
Resources/Cards/    # 52 .tres：领域6 + 随从18 + 法术27 + 状态1
Resources/Localization/ # zh.yaml / en.yaml
Resources/Enemies/ Resources/Relics/ # 结构桩，当前为空
Scenes/             # Main, Combat, Collection, Map + Card/Board/CombatPreview
Tests/              # tests/csharp：xUnit 12 Unit + 1 Integration(跳过)
```

## Autoload

| 名称 | 路径 | 用途 |
|------|------|------|
| `GameManager` | `Scripts/Core/GameManager.cs` | 全局状态、卡牌注册表、语言切换、跨战斗持久化 |
| `UIScaler` | `Scripts/UI/UIScaler.cs` | UI 缩放，当前基准 1152×648（TODO 统一 1600×900） |
| `ChatScreen` | `Scripts/Infrastructure/ChatScreen.cs` | 开发者控制台，`` ` `` 呼出 |
| `MobileInputHelper` | `Scripts/Infrastructure/MobileInputHelper.cs` | 旧触控辅助；非战斗 UI 仍有使用，战斗表面新代码用 Router |
| `MobileInputRouter` | `Scripts/Infrastructure/MobileInputRouter.cs` | 移动端触控路由（手势所有权 + 模态栈），桌面端透传 |
| `InputManager` | `Scripts/Infrastructure/InputManager.cs` | 键盘键位映射（物理键→逻辑动作），多套配置持久化 |
| `HotkeyManager` | `Scripts/Infrastructure/HotkeyManager.cs` | 键盘动作→回调分发（栈式绑定），AddBlockingScreen 输入拦截 |
| `EvalGateway` | `Scripts/Infrastructure/EvalGateway.cs` | C# 反射求值网关（DEBUG only），为 godot-mcp 提供 C# 路径求值 + 快照 |

## WHERE TO LOOK

| 做什么 | 位置 | 注意 |
|-------|------|------|
| 入口/主菜单 | `Core/MainMenu.cs`, `Scenes/Main.tscn` | `project.godot` main_scene；开始游戏前有英雄选择覆盖层 |
| 卡牌数据 | `Core/CardData.cs`, `Core/CardEffectData.cs` | Godot Resource, `[Export]`，效果枚举→分发器 |
| 卡牌运行时 | `Card/Card.cs` → `Minion.cs` | **纯 C#，不继承 Node**；`Spell.cs` 是死代码 |
| 英雄/护甲 | `Card/Hero.cs` | 包装 CommanderCore；护甲吸收在 DamageResolver 后 |
| 英雄技能 | `Card/IHeroPower.cs`, `Card/HeroPowers/` | 当前有 IronWill、绮梦「星光补给」、理惠「火力筛选」、溯光「重整」；带冷却/存储层数的技能看 `IChargeCooldownSkill` |
| 武器 | `Card/Weapon.cs`, `WeaponSkill.cs` | 当前有离子手枪、SVDS-M338、棍木；`RailPistolPassive.cs` 仍孤立 |
| 战斗主循环 | `Combat/CombatManager.cs` | 仍是中介；详见 `Scripts/Combat/AGENTS.md` |
| 战斗拆分模块 | `Combat/AttackTracker.cs`, `SelectionSystem.cs`, `DeathHandler.cs`, `WeaponAttackSystem.cs`… | 纯 C# + 构造注入 + Action 回调 |
| 棋盘 | `Combat/Board.cs` | 2×5 槽位，嘲讽检测，C# event |
| 回合/法力 | `Combat/GameState.cs` | 纯 C#，法力自然上限/硬上限见测试 |
| 敌方单位 | `Combat/EnemyUnit.cs` | Hero 身体 + EnemyEncounter 大脑 |
| 伤害计算 | `Core/DamageResolver.cs`, `Core/*DamageModifier.cs` | 四阶段管线；Heat 独立在 `Scripts/Heat/` |
| 热力系统 | `Heat/HeatSystem.cs` | 每战斗全局倍率，通过 `HeatDamageModifier` 接入 |
| 藏品系统 | `Relic/AbstractRelic.cs`, `RelicManager.cs` | 生命周期钩子，资源目录尚为空 |
| 敌人 AI | `AI/IntentAI.cs`, `AI/EnemyRegistry.cs` | 新旧意图桥接仍在；避免新增兼容层 |
| 意图数据模型 | `AI/Intents/AbstractIntent.cs` | 19 文件，MoveState 多意图；详见 `Scripts/AI/Intents/AGENTS.md` |
| 意图图标/提示 | `UI/IntentIcon.cs`, `IntentTooltip.cs`, `EnemyIdentityCard.cs` | 悬停/长按，敌方英雄攻击绿框在 EnemyIdentityCard |
| UI 总控 | `UI/CombatUI*.cs` | 4 个 partial：核心/Layout/Refresh/Selection；详见 `Scripts/UI/AGENTS.md` |
| 手牌 | `UI/HandUI.cs` | 风扇交叠，Control 手动布局（非 Container） |
| 卡牌交互 | `UI/CardUI.cs` | `[Tool]` 预览 + 点击/拖拽状态机；卡图区由 CardArtworkView 程序化生成 |
| 星途视觉系统 | `Core/CardArtworkGenerator.cs`, `UI/CardArtworkView.cs`, `UI/StarfieldBackground.cs`, `UI/UIThemeFactory.cs`, `UI/ManaCrystalBar.cs` | 0 美术资产程序化视觉：卡图生成（ID 哈希种子）、星云星空背景、全局主题色板、六边形法力水晶 |
| 交互状态机 | `UI/InteractionFSM.cs` | 纯 C#；统一 Idle/CardPickedUp/Targeting/BoardDrag 阶段转换 |
| 棋盘 UI | `UI/BoardUI.cs` | `[Tool]` 预览，2×5 BoardSlot |
| 收藏 | `UI/CollectionUI.cs`, `CardGrid.cs` | 浏览/编辑/分页/过滤 |
| 地图/事件 | `UI/MapUI.cs`, `EventUI.cs`, `ShopUI.cs`, `RestSiteUI.cs` | 后三者存在但未完整接入流程 |
| 发现/奖励 | `UI/DiscoverUI.cs`, `RewardUI.cs` | TaskCompletionSource 异步，一次性屏幕 |
| 弹窗/移动端 | `UI/MobileDialogHost.cs`, `Infrastructure/MobileInputRouter.cs` | Shop/Rest/Event 已采用，旧弹窗未全迁移 |
| 效果图标 | `UI/EffectBar.cs`, `Core/EffectIconTable.cs` | Emoji+层数，CanvasLayer 独立渲染 |
| 箭头 | `UI/ArrowRenderer.cs` | `_Draw()` 攻击/意图箭头 |
| 暂停/设置 | `UI/PauseMenu.cs`, `UI/SettingsPage.cs`, `UI/SubmenuStack.cs` | ESC 覆盖；设置页含键位标签 |
| 综合信息 | `UI/InfoScreen.cs` | CapsLock 覆盖；`SplitOffset` Godot API 已过时 |
| 存档 | `Core/SaveDataManager.cs`, `RunSaveData.cs` | user://save.json 持久化；包含 selected hero / active run hero id |
| 版本号 | `Core/VersionInfo.cs`, 仓库根 `VERSION` 文件 | 唯一真源是 VERSION；构建期烧入 AssemblyInformationalVersion，运行时反射读取 |
| 本地化 | `Localization/Localization.cs` | YAML 加载，DirAccess→硬编码回退 |
| 控制台 | `Infrastructure/ChatScreen.cs` → `ChatScreenEngine.cs` → `Commands/` | 8 个命令组，`/help` 自动生成 |
| C# 求值网关 | `Infrastructure/EvalGateway.cs` | DEBUG only Autoload，godot-mcp `game_eval_csharp`/`game_eval_csharp_snapshot` 后端 |
| 生命周期防护 | `Infrastructure/SceneLifecycleGuard.cs` | `CallDeferredSafe` 防 QueueFree/场景切换竞态 |
| 编辑器预览 | `Scenes/CardPreview.tscn`, `BoardPreview.tscn`, `CombatPreview.tscn` | `[Tool]` + `#if TOOLS`，发布版零开销 |
| 测试 | `tests/csharp/` | xUnit；集成测试因 Godot Resource 跳过 |

## Architecture Rules

### 术语对照（强约束）

项目的设计语言混合了炉石/STS2/自创新词，多份历史文档与 wiki 都用过其中之一。本节是术语唯一真源，新增/改写时统一对齐。

| 项目术语 | 等价映射 | 实现位置 | 范围与约束 |
|---|---|---|---|
| **领域** | STS2 的 Power（永久型）/ STS 1 的「能力」/ 塔圈社区常用「领域」 | ActiveDomain : IPermanentEffect，挂在 Hero.ActiveDomains | **永久**——Counter 仅由战斗事件消耗（被攻击/回合结束事件/随从进场），**禁止自动回合衰减**。再次打出同领域叠加 Counter。 |
| **限时挂载效果** | STS2 的限时 Power（如 RegenPower / DuplicationPower）| StatusEffect : ITemporaryEffect + OnTick 钩子 | **限时**——TickOn 驱动衰减，归零自动移除；触发逻辑由 CardEffectDispatcher.HandleMountHeroEffect 唯一注入 OnTick lambda。**禁止用 ActiveDomain 模拟回合计时。** |
| **状态效果** | STS2 的 VulnerablePower/WeakPower 等 debuff | StatusEffect 通道，与限时 mount 同储但语义上分开 | 自带衰减、可被净化（Polarity=Negative）。限时 mount 使用 Polarity=NonNegative 不可被净化。 |
| **目标/单位** | IDamageTarget 接口实现 | Hero : IDamageTarget, IDamageSource、Minion : Card, IDamageTarget, IDamageSource | 玩家英雄、敌方英雄、友方随从、敌方随从、衍生衍生牌生成的临时单位都是「单位」。卡牌效果的目标都是 unit。 |
| **直伤卡** | CardMechanicTag.DirectDamage flag 标记的卡 | CardData.MechanicTags | 由人工权威标注，**不从 CardEffectType 自动推导**。火力筛选（理惠）与 ThemeProfile 直伤维度共用此真源。 |
| **触发时机（tick timing）** | STS2 的 AfterSideTurnStart/End | TickTiming enum + Hero/Minion.TickStatusEffects(timing) | 玩家回合开始/结束、敌方回合开始/结束四档。StatusEffect.OnTick 在 Tick() 衰减**之前**触发（对齐 STS2「先 heal 再 Decrement」），避免最后一帧丢触发。 |
| **涩情文案（ecchi）** | 设置页「涩情文案♥️」开关 | `UIScaler.EcchiTextEnabled` + `GameManager.EcchiTextEnabled`，持久化 key `visual/ecchi_text` | 事件文案按此开关切换过审版 / 涩情版前缀。YAML key 加 `_ecchi` 后缀区分（如 `ayame_mirror_ecchi`）。外部命名统一用 `ecchi`（日语「エッチ」），不用 `lewd`。 |

「领域」这一玩家可见术语只指 **永久 Power**——限时挂载效果不是「领域」。

### 纯 C# 核心
- `Card`, `Minion`, `Spell`, `Hero`, `Weapon`, `Board`, `GameState`, `EnemyUnit` — **禁止**调用 Godot API。
- 数据与渲染分离：`Card` = 纯数据，`CardUI` = Godot Control 包装。
- `CombatManager` 是唯一跨两层的中介；拆分模块仍通过它接入 UI/场景树。
- 交互状态机：`UI/InteractionFSM.cs` 统一管理卡牌拖拽/点击/攻击拖拽的 `Idle→CardPickedUp→Targeting→BoardDrag` 四阶段转换；`CardUI._Process` 和 `CombatUI` 攻击拖拽均委托给 FSM。

### 伤害管线
- 四阶段：`ADDITIVE` → `MULTIPLICATIVE` → `HEAT` → `CAPPING` → Clamp。
- 每阶段挂载 0..N 个 `IDamageModifier`；意图预览走 `ResolvePreviewDamage()`。
- 护甲吸收在 DamageResolver **之后**。Hero 和 Minion 统一此顺序。
- Heat 阶段由 `Scripts/Heat/HeatDamageModifier.cs` 接入。
- 注意：`FragileArmorModifier` 是护甲增益修改器，不是标准 `IDamageModifier` 模式。

### 意图系统
- 新系统在 `AI/Intents/`：`AbstractIntent` + `MoveState` + `IntentType`。
- 旧 `EnemyIntent` 仍作为 UI 桥：`DamageCalc` lambda 延迟计算 + `TargetSelector` 动态目标。
- 意图不存静态数值；战场变化时重算。
- 敌方回合动画期间 `CombatManager.IsEnemyTurnAnimating` 冻结 UI。
- 新增意图类型：改 `IntentType.cs`、具体 Intent 类、图标/tooltip 映射，不加 legacy shim。

### 多敌人架构
- 每个敌人是独立 actor：自己的 HP、MoveState 链、Intent。
- `EnemyUnit` = `Hero`(身体) + `EnemyEncounter`(大脑)，不是共享血条包装器。
- 协同通过被动/事件监听，不通过上帝对象指挥。
- 敌方随从意图≥`DefaultAttackMinionBrain`，不降级为“无意图自动攻击”。

### 领域/藏品/热力
- **领域（永久 Power）**：打出时挂 ActiveDomain 到 Hero.ActiveDomains，在战斗事件点长期触发。**Counter 只由事件消费**——被攻击/敌方英雄攻击格挡/随从进场/回合结束事件触发；**禁止自动回合衰减**——自动 StackCount-- 意味着这是限时效果，应改走 StatusEffect。
- **限时挂载效果（限时 Power）**：模拟 STS2 RegenPower/DuplicationPower 的回合计时效果——StatusEffect + OnTick 通道：TickOn 衰减归零自动移除，触发逻辑通过 OnTick lambda 注入。**唯一注入点**：CardEffectDispatcher.HandleMountHeroEffect；其他调用点禁止设置此字段。当前示例：四夜雷电光、星途精神下回合收益。
- **Counter 叠加**：多次打出同一领域 = 多层 counter，每触发一次消耗一层。限时挂载同样支持叠层（AddStatusEffect 的同名 ID 叠层分支）。
- 藏品：AbstractRelic 生命周期钩子；不要把藏品逻辑塞进 CombatManager。
- 热力：每战斗全局节奏压力，不是卡牌状态。

### UI 交互
- 双交互模式：点击选中→第二击目标 / 按住拖拽→松手打出。`DragThreshold=10f`。
- 攻击：拖拽不 reparent 随从到 DragLayer，视觉靠 `ArrowRenderer`。
- 出牌区域：Y 轴阈值（屏幕高度 75%），自适应拖拽起始位置。
- 取消：右键 / 拖回底部 / 松手在无效区域。
- 敌方英雄攻击区域：`EnemyIdentityCard.SetAttackTargetHighlight()` 绿色矩形覆盖层。
- 交互状态机 | `UI/InteractionFSM.cs` | 纯 C# 状态机；统一管理卡牌拖拽/点击/攻击拖拽的 Idle/CardPickedUp/Targeting/BoardDrag 四阶段转换；右键取消路径通过 FSM.Cancel() 统一

### 移动端输入
- 移动端同一控件同一动作只能有一条主触控路径；避免 `Button.Pressed`、`MobileInputRouter.RegisterTapZone`、局部 `_Input` 手动 hit-test 同时处理同一次触摸。
- 迁移期若无法移除双路径，入口必须防重入/幂等（例如已有模态页时重复打开直接 return），防止真机一次 tap 触发两次。
- 手动 hit-test 必须用 `IsVisibleInTree()`，不能只看控件自身 `Visible`；隐藏父页下的控件不得响应触摸。
- Tab/模态页切换必须同步更新 `Visible` 与 `MouseFilter`：当前页 `Stop`，隐藏页 `Ignore`。
- `MobileInputRouter` 模态栈只负责 Router zone 过滤；Godot 原生 Control 信号仍可能触发，不能把 PushModalLayer 当作全局输入屏蔽。

### 异步 UI
- `TaskCompletionSource<T>`：创建 UI → 等待选择 → `SetResult` → `QueueFree`。
- 屏幕即一次性：选择完成即销毁。
- 入场 350ms 防误触保护。
- 新 Roguelike 弹窗优先考虑 `MobileDialogHost` + `MobileInputRouter` 模态栈。

### Godot UI 陷阱
- `MouseFilter` 默认 `Stop`；覆盖父控件的子控件要显式 `Ignore` 或接管点击。
- `MouseFilter=Ignore` 完全阻断 `_Input()`；拖拽追踪必须 `_Process` 轮询全局 Input。
- `HBoxContainer` 等容器覆盖手动 `Position`；脱离容器的内容用 `CanvasLayer` 或 Root 子节点。
- 非 Container 父控件禁用 `Offset*`；位置全由 `Position` 控制。
- 嵌套 partial class 的 `signal +=` 在 Godot Mono 中不可靠；用 `Connect` 或 `_Notification`。
- `[Tool]` 扫描会实例化嵌套 Control；嵌套类必须有无参构造函数，值类型字段用 `default`。

### 树操作安全
- 避免同一调用栈内 `QueueFree` 旧节点 + `AddChild` 新节点。
- 批量重建：缓存目标状态 → deferred 边界统一重建。
- 可用 `SceneLifecycleGuard.CallDeferredSafe()` 守卫 `IsInsideTree()` / `IsQueuedForDeletion()`。

## Conventions

### 命名/格式
- 接口：`I` 前缀。private 字段：`_camelCase`。public：`PascalCase`。局部/参数：`camelCase`。
- 命名空间：`file_scoped` (`namespace X;`)，新文件必须；`.editorconfig` 的 block_scoped 配置已过时。
- `using` 在 namespace 外。
- **缩进统一 Tab（4空格宽度）**；文件末尾换行，去尾随空格。
- **YAML 例外**：`Resources/Localization/*.yaml` 与 `*.json` 等数据格式按其语法标准缩进——YAML 用 **2 空格递增**（root 0 → level1 2 → level2 4 → level3 6），**禁止 tab**。原因是项目自定义 `YamlParser.GetIndentLevel` 把 tab 算作 2 空格，与父级 2 空格歧义会导致子节点错位到 root，整段翻译失效。
- XML doc 中文。日志：`GD.Print("[ClassName] 消息")`。

### 事件/信号
- **不用** `[Signal]`。全部 C# `event Action<...>`。
- 触发：`?.Invoke()`；订阅：`+=`；取消：`-=`。

### UI 刷新
- Pull 模式：`CombatUI.RefreshAll()` → `BoardUI.RefreshBoard` / `HandUI.RefreshHand`。
- HandUI 刷新手牌 = QueueFree 全部 CardUI 后重建。
- BoardUI = 属性更新（`BoardSlot.UpdateDisplay` 原地刷新）。

### 资源加载
- `GD.Load<T>(path)` / `ResourceLoader.Exists(path)`。
- `[Export] PackedScene` → `Instantiate<T>()`。
- `GetNode<Type>(path)` / `GetNodeOrNull<Type>(path)`。

### 本地化
- 入口：`GameManager.SetLanguage()` → `Localization.SetLanguage()`。
- 事件：`GameManager.LanguageChanged`，所有场景订阅刷新。
- 查找：`Localization.T("key", "默认值")`。支持 `{key}` 占位符。
- 卡牌翻译：`cards.{id}.name/description` → `Card.GetLocalizedName()` / `GetLocalizedDescription()`。
- 新增 UI 文本 checklist：`Localization.T()`；同步 `zh.yaml` + `en.yaml`；订阅语言事件；回调加 `IsInsideTree()`；`_ExitTree` 取消订阅。

### 输入/热键
- 三层：`InputManager`（物理键→动作）→ `HotkeyManager`（动作→回调栈）→ 场景 UI。
- 新功能不用原始 `Key.` / 硬编码 action 字符串；统一 `OdysseyInput` 常量。
- `_EnterTree`/`_Ready` 注册的 HotkeyManager 绑定，必须在 `_ExitTree` 注销。
- 模态屏用 `AddBlockingScreen` 拦截输入。

### 格式化
- **C#**：`.editorconfig` 是唯一真源——tab 缩进（4 宽度），详细规则见文件内 `[*.cs]` 段。CI 验证：`dotnet format OdysseyCards.sln --verify-no-changes`。
- **YAML**（`Resources/Localization/*.yaml`）：无自动 formatter。缩进规则：**2 空格递增**（root 0 → level1 2 → level2 4 → level3 6），**禁止 tab**。原因：`YamlParser.GetIndentLevel` 将 tab 计为 4 空格，与标准 2 空格递增产生歧义。YAML 文件**禁止在 Godot 内置编辑器中编辑**——Godot 的 `text_editor/behavior/indent/type=0`（Tab 缩进）会破坏 YAML 缩进（已关闭 `convert_indent_on_save` 作为防护，但仍不推荐）。
- **TS CN/TRES**：无自动 formatter。缩进规则同 C#（tab，4 宽度），由 `.editorconfig` 的 `[*.{tscn,tres}]` 段约束。

## Export Build

- Godot 4 `.pck` 中 `DirAccess.Open("res://...")` 枚举目录失败；所有 DirAccess 使用点必须有硬编码回退。
- `GameManager.cs`：`CardResourcePaths[]` 硬编码卡牌路径；**新增卡牌必须同步更新**。
- `Localization.cs`：`TryLoadTranslationsViaDirAccess()` + 已知文件回退；新增语言同步回退列表。
- `export_presets.cfg` 当前被 `.gitignore` 命中但项目依赖其 include_filter；改动前确认跟踪状态。
- 版本号唯一真源是仓库根 `VERSION` 文件（单行，无 v 前缀，如 `0.2.0-alpha`）。改版本号只改这一处：`dotnet build` 自动读入 `AssemblyInformationalVersion`；`build_export.ps1`/`build_android.ps1` 导出前临时注入 `project.godot`/`export_presets.cfg`，导出后恢复。**禁止多处硬编码版本号。**
- Android 签名密钥：`android/debug.keystore`（开发期）+ `android/release.keystore`（正式发布，25 年有效期）；配置在 `android/keystore.properties`（gitignored），`build_android.ps1` 通过 Godot 环境变量注入，密码不落盘到 cfg。`-Release` 开关切换 release 签名。**release.keystore 丢失 = 应用无法更新 = 所有用户存档丢失，必须多备份。** 模板见 `android/keystore.properties.example`。
- 导出前删除 `user://save.json`（`%APPDATA%/OdysseyCards/save.json`）确保干净初始化。

## Commands

```bash
dotnet build
dotnet build -c Release
dotnet test
dotnet format OdysseyCards.sln --verify-no-changes
./build_export.ps1 [-Debug] [-SkipBuild]
./build_android.ps1 [-SkipBuild] [-ExportOnly] [-Release]
./package_release.ps1 [version] [-OpenFolder]
```

- 当前无 `.github/workflows`、Dockerfile、Makefile。
- GUT 插件已安装但 `res://test/` 无 GDScript 测试，暂为 dormant config。

## ChatScreen

AI 调用：`game_call_method(nodePath="/root/ChatScreen", method="DevCommand", args=["/damage 10"])`

架构：`ChatScreen`(薄 UI 壳) → `ChatScreenEngine`(纯 C# 引擎) → `Commands/*`(9 个命令组，继承 `ChatScreenCommand`)。
新增命令：写 `ChatScreenCommand` 子类并在 `RegisterAllCommands()` 注册。`/help` 自动从命令元数据生成。

| 命令 | 效果 |
|------|------|
| `/damage N` / `/damage_enemy N` | 对敌方英雄造成 N 点伤害 |
| `/damage_self N` | 对自己造成 N 点伤害 |
| `/damage_eslot X N` / `/damage_pslot X N` | 对指定槽位随从造成 N 点伤害 |
| `/damage_all N` | 对所有敌方随从造成 N 点伤害 |
| `/damage -c N` | 点击模式：隐藏控制台→点击目标造成伤害→右键取消 |
| `/draw N` `/mana N` `/heal N` `/armor N` | 资源/生命调试 |
| `/end` `/refresh` `/clear` | 回合/UI/控制台 |
| `/token <id> [n]` `/play <id> [target]` `/summon_player <id> <slot>` | 卡牌/召唤 QA；`/play` 目标可用 `enemy`/`player`/`eslotN`/`pslotN` |
| `/fight <enemy>` | 直接与指定敌人战斗，跳过地图 |
| `/addrelic <id>` `/unlock_all` | 藏品/解锁 |
| `/intent_debug` `/tags` | 意图/标签调试 |
| `/qa_tombstone` `/qa_bait_tactics` `/qa_new_cards` | 运行时 QA |
| `/emote <id>` | 表情 QA |
| `/version` | 显示当前游戏版本号 |
| `/help` | 帮助 |

## Verification / MCP Testing

- 自动测试：`dotnet test`，纯 C# 单测可跑；Integration 因 Godot Resource 依赖跳过。
- 运行时 QA：启动 Godot → ChatScreen 执行 `/qa_*` → `game_get_logs` 读结果。
- godot-mcp 可验证：`/damage`、`/draw`、`/mana`、`/end`、`/armor` + logs/UI。
- godot-mcp C# 求值：`game_eval_csharp(path="CombatManager.PlayerHero.CurrentHealth")`、`game_eval_csharp_snapshot(kind="combat")`。通过 `/root/EvalGateway` Autoload 反射 C# 成员，解决 `game_eval`（GDScript）无法访问 C# 静态成员/纯 C# 类/List<T>/enum 的痛点。
- godot-mcp **无法可靠验证拖拽**：`game_click` 合成事件 vs OS 真实鼠标不一致。
- 需人类肉眼：UI 位置/大小、拖拽交互、动画、字体。
- 路径硬编码：`G:\dev\godot-mcp\build\scripts\`（分家后真源在 `WhiteGiverMa/godot-mcp`）。
- mcp无法验证移动端特有行为，需请求真机测试

## Anti-Patterns

- **禁止** 对 `Card`/`Minion`/`Hero`/`Board`/`GameState`/`EnemyUnit` 调 Godot API。
- **禁止** 直接操作 `Board.PlayerSlots[index]` → 用 `PlaceMinion`/`RemoveMinion`。
- **禁止** `async void`。当前已知例外/债务：`UI/CardAnimation.cs`。
- **禁止** 混淆 `Player._core` 和 `CombatManager._playerCore`。
- **禁止** `DamageResolver` 传 null source 不检查。
- **禁止** Hero 有护甲时假设 DamageResolver 吸收护甲；护甲在管线之后。
- **禁止** UI 硬编码中文字符串 → 必须 `Localization.T()` + YAML key。
- **禁止** `MouseFilter=Ignore` 控件用 `_Input()` → 轮询 `_Process`。
- **禁止** 非 Container 父控件用 `Offset*` → 只 `Position`。
- **禁止** DragLayer 同时持有多张卡 → 先归还旧卡再取新卡。
- **禁止** 用 `GetGlobalMousePosition()` 替代 `InputEventMouseButton.GlobalPosition` 做点击初始坐标。
- **禁止** 用 `_Input` 的原始 `Key.` 枚举处理新功能 → 必须走 `OdysseyInput` + `HotkeyManager`。
- **禁止** 同一栈帧内 `QueueFree` + `AddChild` 混用 → deferred 批处理。
- **禁止** 为当前周期草稿写兼容层；项目膨胀期直接删除旧形状。

## Unique Styles

- 程序化 UI：CombatUI 子组件纯代码，Combat.tscn 仅容器；预览靠 `[Tool]` 场景。
- CombatUI partial：核心 / Layout / Refresh / Selection 四分。
- CombatManager 拆分：纯 C# 小系统 + 构造注入 + Action 回调，不走 Godot 信号。
- 双层 CommanderCore：Player 和 CombatManager 各持一份，`internal Deck setter` 共享牌堆。
- 手动法力同步：GameState 和 CommanderCore 各维护法力，CombatManager 手动 SetMana。
- 意图动态计算：lambda 延迟求值，不缓存静态数值。
- 风扇手牌：Control 手动 LayoutChildren，`OVERLAP_FACTOR=0.85`，`BASE_SCALE=0.85`。
- 攻击双交互：箭头追随+松手攻击/右键取消，敌方英雄整卡绿色攻击高亮。
- 导出回退：DirAccess 优先 + 硬编码回退。
- 关键词检测：比较运行时 vs `CardData` 基线区分“自带”vs“授予”。
- 程序化视觉（星途视觉系统）：0 美术资产原则——卡图 = ID 哈希种子派生规格（纯函数可测）+ `_Draw` 矢量绘制（渐变底/星点/几何符号/稀有度光晕）；背景 = FastNoiseLite 星云纹理（根除 8bit banding）+ 视差星点；主题 = UIThemeFactory 单例 Theme + 星途色板（深空#12101F/星粉#FF9ED2/青金#7FD8FF/暖金#FFD98E/绯红#FF6B7A）；真图通道保留（CardData.Artwork 非 null 时优先）。

## State

- ✅ 全面键盘支持：InputManager→HotkeyManager→各场景，对齐 STS2 键位。
- ✅ 主菜单英雄选择：当前可选红裤衩 / 绮梦 / 理惠 / 溯光，旧 qimeng 存档映射到新绮梦，选择结果持久化到存档。
- ✅ 新英雄理惠：25HP，SVDS-M338，持续伤害状态，英雄技能「火力筛选」。
- ✅ 新英雄溯光：25HP，射线手枪，被动「铭记」，武器主动「致盲」，英雄技能「重整」。
- ✅ 新绮梦：30HP，魔法棒，被动「贴膜魔法」，武器主动「星辉净化」，英雄技能「星光补给」；原 demo 配置改名红裤衩。
- ✅ 本地化全场景接线；YAML include_filter 已处理导出。
- ✅ 卡牌选中/拖拽、攻击双交互、右键取消、敌方英雄攻击绿框已修复。
- ✅ 意图图标 hover 稳定；设置页支持开关图标/伤害数字浮动视觉风格。
- ✅ 多敌人 AI：IntentAI + 多 Brain + EnemyRegistry。
- ✅ 新意图系统：`AI/Intents/` 20 文件，MoveState 多意图，Icon/Tooltip 路径。
- ✅ 新敌人劫蛋者：开场「难逃之瑕」+ B/C/D 乱序循环；「聚焦」「蓄谋」「智能臭鸡蛋」已接入。智能臭鸡蛋已解耦——亡语机制驱动伤害，Brain 只驱动自爆意图。
- ✅ CombatUI 大文件已 partial 拆分，支持 Card/Board/Combat 编辑器预览。
- ✅ CombatManager 已拆出 AttackTracker/SelectionSystem/DeathHandler/VictoryDefeatResolver/WeaponAttackSystem/EmoteSystem。
- ✅ ChatScreen v2：Engine + Command 抽象 + 8 命令组 + 历史持久化。
- ✅ 版本号体系：`VERSION` 文件（仓库根，单行无 v 前缀）是唯一真源；csproj 构建期读入 `AssemblyInformationalVersion`，`VersionInfo.cs` 运行时反射读取；`build_export.ps1`/`build_android.ps1` 导出前临时注入 `project.godot`/`export_presets.cfg`，导出后恢复；`package_release.ps1` 默认读 VERSION；主菜单右下角 + ChatScreen `/version` 显示。
- ✅ Android 签名密钥：项目专属 `debug.keystore` + `release.keystore`（25 年），配置 `android/keystore.properties`（gitignored），`build_android.ps1` 用 Godot 环境变量注入，`-Release` 切换正式签名。
- ✅ 新增三张法术卡：四夜雷电光（轮战，英雄挂载回合触发）、十万条吸血狗（选择手牌并复制填满）、星途精神（跳费/AOE/抽牌/下回合挂载收益）。
- ✅ 新增六张通用法术卡：引擎、检索、响应、肾上腺素、沉重打击、震慑；新增两张随从：40主战坦克、百机长。
- ✅ 新增六张卡牌：曼巴导弹、肘击、40A主战坦克、40B主战坦克、「岷山」步行支援机、联树机器犬运输型；岷山响应直伤法术，运输型拥有单卡独立的两次回抽牌堆计数。
- ✅ 新机制失能：负面 StatusEffect，按目标所属方回合结束衰减；失能单位不能攻击或反击，英雄不能用武器攻击但仍可施放法术。
- ✅ 获得格挡动画：Hero/Minion 护甲获得事件驱动 `🛡+N` 浮字，复用战斗跳字层。
- ✅ 主题卡组支持 `ThemeProfile.KeywordWeights`，可按 `Keyword`（如轮战、亡语）给角色主题加权，不污染 `CardMechanicTag`。
- ✅ 热力系统与藏品系统已存在；资源化仍未完成。
- ✅ ActiveDomain/StatusEffect 语义边界已明确（永久 Power vs 限时 Power）；四夜雷电光、星途精神 mount 改为限时 Power 走 StatusEffect + OnTick 通道。
- ✅ 星途视觉系统 v1（PRD: issue #2）：程序化卡图生成器（61 张卡全有独特卡面，ID 哈希种子可复现，主题风格分派 ayame=Rune/rie/sokou=Mecha）、全局主题工厂 + 星途色板、星云星空背景（主菜单/战斗/地图三档密度）、六边形法力水晶、棋盘阵营色边框（玩家青金/敌方绯红 + 空槽同步呼吸）；STS 遗物 PlaceholderAssetGenerator 已删，Assets 死图 212→24（仅保留 _backup 引用的 Demo jpg）。
- ✅ 术语对照表已建立（Architecture Rules → 术语对照段），涵盖领域/限时挂载/状态/目标单位/直伤卡/触发时机。
- ⚠️ `Spell.cs` 从未实例化（死代码）。
- ⚠️ `RailPistolPassive.cs`、`SafeAreaContainer.cs` 当前孤立。
- ⚠️ `EventSelector` 奖励逻辑完整但战斗奖励流仍未统一接线；`ApplyReward` 已 Obsolete。
- ⚠️ `ShopUI` / `RestSiteUI` / `EventUI` / `MobileDialogHost` 存在但未完整接入 MapUI 流程。
- ⚠️ 英雄技能四个实现均存在；战斗 UI/输入仍需按英雄逐项运行时验证。
- ⚠️ 手牌无上限 + 无疲劳；已有 `Status_Fatigue.tres` 但系统未完整化。
- ⚠️ `CardEffectData.GetDescription()` debug 字符串未本地化（低优）。
- ⚠️ `InfoScreen.cs` 仍用 Godot 过时 API `SplitOffset`。
- `.godot/` 删除后编辑器重生成 UID，异常先查 .tscn 引用。
- `.cs` ↔ `.uid` 配对，重命名/移动同步处理。
- 端口冲突→旧 Godot 进程残留→手动 Stop-Process。

## 子目录规则

- `Scripts/Combat/AGENTS.md`：战斗拆分边界。
- `Scripts/Core/AGENTS.md`：数据资源、伤害管线、保存与全局状态。
- `Scripts/Card/AGENTS.md`：运行时卡牌/英雄/随从/武器模型。
- `Scripts/UI/AGENTS.md`：程序化 UI、CombatUI partial、预览与弹窗。
- `Scripts/AI/AGENTS.md`：敌人 AI 父级——EnemyRegistry、Brain、旧意图桥接、新增敌人流程。
- `Scripts/AI/Intents/AGENTS.md`：新意图类型与 MoveState。
- `Scripts/Heat/AGENTS.md`：热力阶段。
- `Scripts/Relic/AGENTS.md`：藏品生命周期。
- `Scripts/Localization/AGENTS.md`：YAML 多语言 + tab 缩进陷阱。
- `Scripts/Infrastructure/AGENTS.md`：控制台、输入栈、移动端、生命周期防护、Commands/。
- `Scripts/Roguelike/AGENTS.md`：地图、事件、奖励与主题卡组生成。
- `Scripts/Character/AGENTS.md`：Player 双层 CommanderCore 模式、牌堆管理。
- `tests/AGENTS.md`：xUnit 测试约定——RED phase、Skip 规范、覆盖盲区。
- `wiki/AGENTS.md`：wiki 概念库写作纪律；只约束 agents，不约束人类。

## 杂项（待归类注意事项/notes）

- **必须** 在 `_ExitTree` 中注销 `_EnterTree`/`_Ready` 注册的 HotkeyManager 绑定 → `PushPressedBinding`/`RemovePressedBinding` 配对。
- 新增卡牌**必须**同步 `CardResourcePaths[]`。
- 新增语言**必须**同步 `TryLoadTranslationsViaDirAccess()` 回退。
- 改版本号**只改**仓库根 `VERSION` 文件（唯一真源），**禁止**多处硬编码版本号。
- **必须**在产出新的特性、内容时提问用户「是否需要更新wiki」。**禁止**不经询问删除wiki中与代码不符的信息。
- `project.godot`的空行噪音是常见现象；只提交有意义的变动，空行变动可忽略或回退。
