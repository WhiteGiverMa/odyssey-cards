# 项目规范记录

本文档记录 OdysseyCards 项目的核心数据结构和实现规范，作为开发参考和知识库。

---

## 卡牌类型系统

### 卡牌类型 (CardType)

| 类型 | 说明 |
|-----|------|
| Unit | 单位卡牌，可部署到战场 |
| Order | 指令卡牌，打出后立即生效 |

### 目标类型 (CardTarget)

| 类型 | 说明 |
|-----|------|
| None | 无目标 |
| Self | 自身 |
| SingleEnemy | 单个敌人 |
| AllEnemies | 所有敌人 |
| Everyone | 所有角色 |
| Headquarters | 总部 |
| SingleUnit | 单个单位 |

---

## 词条系统

### 词条枚举 (CardTag)

| 枚举值 | 英文名 | 触发时机 |
|-----|--------|--------|
| None | - | - |
| Blitz | 闪击 | OnDeploy |
| Maneuver | 机动 | Passive |
| Rotation | 轮战 | OnDeath |
| Fury | 奋战 | Passive |
| Guard | 守护 | Passive |
| LastWords | 亡语 | OnDeath |
| Deploy | 部署 | OnDeploy |
| Defense | 防御 | Passive |
| Ambush | 伏击 | Passive |
| Impact | 冲击 | Passive |
| Immune | 免疫 | Passive |
| Pin | 压制 | Passive |
| Suppress | 抑制 | Passive |
| Massive | 断流 | Passive |
| Infiltrate | 渗透 | Passive |

### 词条效果

| 词条 | 英文 | 效果 |
|-----|------|------|
| 闪击 | Blitz | 部署后可立即行动 |
| 机动 | Maneuver | 每层额外行动1次 |
| 轮战 | Rotation | 返回抽牌堆随机位置 |
| 奋战 | Fury | 每回合可攻击两次 |
| 守护 | Guard | 保护距离内的友方单位 |
| 亡语 | LastWords | 阵亡时触发效果 |
| 部署 | Deploy | 部署时触发效果 |
| 防御 | Defense | 每层减免1点伤害 |
| 伏击 | Ambush | 首次被攻击时先造成反击伤害 |
| 冲击 | Impact | 首次攻击不受到反击伤害 |
| 免疫 | Immune | 不会受到伤害 |
| 压制 | Pin | 无法移动或攻击 |
| 抑制 | Suppress | 失去所有关键词和效果 |
| 断流 | Massive | 无法与其他单位处于同一节点 |
| 渗透 | Infiltrate | 可移动到敌方单位所在节点 |

---

### 单位数据 (UnitData)

| 属性 | 缩写 | 说明 |
|-----|------|------|
| DeployCost | C | 部署费用 |
| ActionCost | aC | 行动花费(油费) |
| Attack | A | 攻击力 |
| MaxHealth | HP | 生命值 |
| Range | R | 攻击距离 |
| Tags | - | 词条列表 |
| DeployEffects | - | 部署时触发效果 |
| LastWordsEffects | - | 阵亡时触发效果 |

**格式表示**: `C/aC/A/HP/R`

**示例**: 武侦小组 `1/0/3/1/1` = 部署1费、行动0费、攻击3、生命1、距离1

---

### 指令数据 (OrderData)

| 属性 | 说明 |
|-----|------|
| Cost | 费用 |
| Target | 目标类型 |
| Tags | 词条列表 |
| Effects | 效果列表 |

---

### 效果类型 (CardEffectType)

| 类型 | 说明 |
|-----|------|
| Damage | 造成伤害 |
| Heal | 恢复生命 |
| DrawCards | 抽牌 |
| GainEnergy | 获得费用 |
| GainMaxHealth | 总部获得生命值 |
| ApplyDebuff | 施加负面效果 |
| ApplyBuff | 施加增益 |
| Discard | 弃牌 |
| ReturnToDeck | 返回抽牌堆 |
| SummonUnit | 召唤单位 |
| Custom | 自定义效果 |

---

## 文件结构

详见 [file_structure.md](./file_structure.md) - 完整文件结构文档

---

## 初始卡牌数据

### 单位卡牌

| 名称 | C/aC/A/HP/R | 词条 | 效果 |
|-----|-------------|------|------|
| 武侦小组 | 1/0/3/1/1 | 闪击、机动 | - |
| 第18团 | 1/1/1/4/1 | 守护、轮战 | - |
| 联树机器犬 侦察型 | 0/0/1/1/1 | 闪击、轮战 | 亡语：抽1张牌 |

### 指令卡牌

| 名称 | 费用 | 词条 | 效果 |
|-----|------|------|------|
| 打击 | 1 | - | 造成3点伤害 |
| 出击 | 2 | 轮战 | 造成2点伤害，抽1张牌 |
| 警戒 | 2 | 轮战 | 抽1张牌，友方总部获得+2生命值 |

---

## 游戏机制

### 回合结构
- 随机先后手
- 先手：4张牌 + 1费上限
- 后手：5张牌 + 0费上限
- 每回合开始：抽1张牌，费用上限+1
- 自然费用上限最高12，硬上限24

### 单位行动
- 单位每回合可进行一次移动或攻击
- 被部署的单位同一回合无法行动（除非有闪击）
- 词条会影响行动规则

### 轮战机制
- 轮战指令：打出后洗回抽牌堆随机位置
- 轮战单位：阵亡后洗回抽牌堆随机位置
- 与杀戮尖塔不同：弃牌堆不会洗回抽牌堆

### 总部机制
- 初始8点生命值上限
- 战斗内损失继承到下一场战斗
- 战斗内可超过上限，但不继承

### 胜利条件
- 打空敌方总部血量 = 胜利
- 友方总部被打空 = 失败

---

## 开发阶段

| 阶段 | 内容 | 状态 |
|-----|------|------|
| 第一阶段 | 核心数据结构重构 | ✅ 已完成 (2026-03-01) |
| 第二阶段 | 地图系统实现 | ✅ 已完成 (2026-03-01) |
| 第二.五阶段 | 敌人系统重构 | ✅ 已完成 (2026-03-01) |
| 第二.六阶段 | 战斗系统重构 | ✅ 已完成 (2026-03-01) |
| 第三阶段 | 卡组机制调整 | ✅ 已完成 (2026-03-01) |
| 第四阶段 | UI重构 | ✅ 已完成 (2026-03-01) |
| 第五阶段 | 响应式布局 | ✅ 已完成 (2026-03-02) |
| 第六阶段 | 统一伤害计算管道 | ✅ 已完成 (2026-03-02) |
| 第七阶段 | 本地化系统 | ✅ 已完成 (2026-03-02) |

---

## 本地化系统

### 概述

参考 slay-the-model 架构实现的本地化系统，支持中英文多语言切换，文本与代码分离。

### 核心组件

| 组件 | 文件 | 说明 |
|-----|------|------|
| Localization | `Scripts/Localization/Localization.cs` | 核心本地化类，提供 `T()` 翻译方法 |
| LocalStr | `Scripts/Localization/LocalStr.cs` | 延迟解析的本地化字符串包装类 |
| ConcatLocalStr | `Scripts/Localization/ConcatLocalStr.cs` | 字符串拼接支持类 |
| ILocalizable | `Scripts/Localization/ILocalizable.cs` | 可本地化实体接口 |
| YamlParser | `Scripts/Localization/YamlParser.cs` | YAML 解析器，支持嵌套 key 扁平化 |

### 翻译文件

| 文件 | 路径 | 说明 |
|-----|------|------|
| en.yaml | `Resources/Localization/en.yaml` | 英文翻译 |
| zh.yaml | `Resources/Localization/zh.yaml` | 中文翻译 |

### 翻译查找方法

#### 基本用法

```csharp
// 在同一命名空间内 (如 LocalStr.cs)
string text = Localization.T("ui.combat.end_turn", "End Turn");

// 在其他命名空间 (使用类型别名)
using Loc = OdysseyCards.Localization.Localization;
string text = Loc.T("cards.strike.name", "Strike");
```

#### 带参数的翻译

```csharp
// Dictionary 参数
var parameters = new Dictionary<string, object> { { "damage", 6 } };
string text = Localization.T("cards.strike.description", "Deal {damage} damage.", parameters);

// 元组参数 (更简洁)
string text = Localization.T("cards.strike.description", "Deal {damage} damage.", ("damage", 6));
```

#### LocalStr 延迟解析

```csharp
// 创建延迟解析的本地化字符串
LocalStr localStr = new LocalStr("cards.strike.name");
string resolved = localStr.Resolve(); // 在需要时才解析

// 支持拼接
LocalStr combined = localStr + " - " + anotherLocalStr;
```

### Key 命名规范

| 类别 | Key 格式 | 示例 |
|-----|---------|------|
| UI 文本 | `ui.{section}.{item}` | `ui.combat.end_turn` |
| 卡牌名称 | `cards.{id}.name` | `cards.strike.name` |
| 卡牌描述 | `cards.{id}.description` | `cards.strike.description` |
| 敌人名称 | `enemies.{id}.name` | `enemies.cultist.name` |
| 战斗文本 | `combat.{event}` | `combat.victory` |

### YAML 文件结构

```yaml
ui:
  combat:
    end_turn: "结束回合"
    draw_pile: "抽牌堆"
cards:
  strike:
    name: "打击"
    description: "造成 {damage} 点伤害。"
enemies:
  cultist:
    name: "邪教徒"
```

### 语言管理

```csharp
// 初始化 (GameManager 中调用)
Localization.Initialize();

// 切换语言
Localization.SetLanguage("zh");
Localization.SetLanguage("en");

// 获取当前语言
string currentLang = Localization.CurrentLanguage;

// 语言切换事件
Localization.OnLanguageChanged += (lang) => { /* 刷新 UI */ };
```

### 语言持久化

GameManager 中实现了语言设置的持久化：

```csharp
private const string SettingsPath = "user://settings.cfg";

private void LoadSettings()
{
    using var config = new ConfigFile();
    if (config.Load(SettingsPath) == Error.Ok)
    {
        string lang = (string)config.GetValue("settings", "language", "zh");
        Localization.SetLanguage(lang);
    }
}

private void SaveSettings()
{
    using var config = new ConfigFile();
    config.SetValue("settings", "language", Localization.CurrentLanguage);
    config.Save(SettingsPath);
}
```

### CardData 集成

UnitData 和 OrderData 实现了 `ICardData` 接口和 `ILocalizable` 接口：

```csharp
public partial class UnitData : Resource, ICardData, ILocalizable
{
    public string LocalizationPrefix => "cards";
    public string LocalizationId => Id;

    public LocalStr Local(string field, Dictionary<string, object> parameters = null)
    {
        return new LocalStr($"cards.{Id}.{field}", parameters);
    }

    public bool HasLocal(string field)
    {
        return Loc.HasKey($"cards.{Id}.{field}");
    }

    public string GetLocalizedName()
    {
        return this.Local("name").Resolve();
    }

    public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
    {
        return this.Local("description", parameters).Resolve();
    }
}
```

**改进说明** (2026-03-02):
- 实现了 `ILocalizable` 接口，统一本地化调用方式
- 使用 `this.Local("field")` 方法构建本地化 key，代码更简洁
- 符合 slay-the-model 的设计哲学，减少硬编码 key 路径

### 注意事项

1. **命名空间冲突**: `using OdysseyCards.Localization;` 会导致 `Localization` 被解析为命名空间而非类，需使用类型别名 `using Loc = OdysseyCards.Localization.Localization;`

2. **FileAccess 歧义**: `FileAccess` 在 `Godot.FileAccess` 和 `System.IO.FileAccess` 之间存在歧义，需显式指定 `Godot.FileAccess`

3. **Fallback 机制**: 翻译 key 不存在时返回默认值或 key 本身，不会崩溃

4. **占位符格式**: 使用 `{placeholder}` 格式，支持运行时替换

---

## 显式 vs 隐式使用规范 (2026-03-02)

### 背景

在实现本地化系统时遇到了一个问题：`Order.Create()` 和 `Unit.Create()` 方法中显式设置了 `CardName = data.CardName`，导致卡牌名称显示为 localization key（如 `cards.order_Strike.name`）而不是翻译后的文本（如"打击"）。

**根本原因**：`CardBase.CardName` 属性的 getter 会调用 `_data?.GetLocalizedName()` 获取本地化值，但 setter 会设置 `_fallbackName`。当 getter 执行时，会优先返回 `_fallbackName` 而不是从 `_data` 获取本地化值。

### 规范原则

为了代码对 Agent 友好，遵循以下规范：

#### 1. ✅ 应该使用**显式**的场景

| 场景 | 说明 | 示例 |
|-----|------|------|
| **类型歧义** | 当类型可能在多个命名空间冲突时 | `using Godot.FileAccess` 而不是 `FileAccess` |
| **命名空间别名** | 当类名与命名空间名冲突时 | `using Loc = OdysseyCards.Localization.Localization;` |
| **初始化 fallback 值** | 当需要提供默认值且后续可能被覆盖时 | `_fallbackName = "Unknown";` |
| **破坏默认行为** | 当你确实想要覆盖属性的默认 getter 行为时 | `CardName = someCustomValue;` |
| **性能敏感场景** | 当需要避免重复计算时，缓存结果 | `var cachedResult = ExpensiveCalculation();` |
| **接口实现** | 显式实现接口成员以避免歧义 | `string ICardData.CardName => ...;` |

#### 2. ✅ 应该使用**隐式**的场景

| 场景 | 说明 | 示例 |
|-----|------|------|
| **属性 getter 已提供正确行为** | 当属性的 getter 已经实现了所需逻辑时 | 不要设置 `CardName`，让它从 `_data` 获取 |
| **依赖注入** | 当框架会自动注入依赖时 | Godot 的 `[Export]` 属性 |
| **默认构造函数** | 当默认构造函数已足够时 | `var list = new List<string>();` 而不是 `var list = new List<string>() { };` |
| **自动属性** | 当不需要自定义 backing field 时 | `public string Name { get; set; }` |
| **约定优于配置** | 当框架有明确约定时 | `Initialize()` 方法在 `_Ready()` 中调用 |

#### 3. ❌ 避免的模式

```csharp
// ❌ 错误：显式设置会破坏隐式行为
var order = new Order
{
    CardName = data.CardName,  // 这会覆盖 getter 的本地化逻辑
    Description = data.Description
};

// ✅ 正确：依赖属性的隐式 getter
var order = new Order
{
    _data = data,  // 设置 backing field
    // CardName 和 Description 会自动从 _data 获取本地化值
};
```

### 决策流程

```
需要设置属性/值吗？
    ↓
是 → 属性的 getter 是否已经提供了正确的默认行为？
    ↓
    是 → ✅ 使用隐式：不要显式设置，依赖 getter
    ↓
    否 → 是否需要破坏/覆盖默认行为？
        ↓
        是 → ✅ 使用显式：显式设置值
        ↓
        否 → ✅ 使用隐式：设置必要的 backing field
```

### 代码示例

#### 示例 1：卡牌创建（本次修复）

```csharp
// ❌ 错误：显式设置破坏了本地化
public static Order Create(OrderData data)
{
    return new Order
    {
        _data = data,
        CardName = data.CardName,      // 问题：调用了 setter，设置 fallbackName
        Description = data.Description // 问题：调用了 setter，设置 fallbackDescription
    };
}

// ✅ 正确：依赖隐式 getter
public static Order Create(OrderData data)
{
    return new Order
    {
        _data = data,  // 设置 backing field
        // CardName 会自动调用 getter: _data?.GetLocalizedName()
        // Description 会自动调用 getter: _data?.GetLocalizedDescription()
    };
}
```

#### 示例 2：命名空间歧义

```csharp
// ❌ 错误：歧义
using OdysseyCards.Localization;
var text = Localization.T("key");  // 编译错误：Localization 是命名空间还是类？

// ✅ 正确：显式别名
using Loc = OdysseyCards.Localization.Localization;
var text = Loc.T("key");
```

#### 示例 3：类型歧义

```csharp
// ❌ 错误：歧义
var file = FileAccess.Open(path, mode);  // Godot.FileAccess 还是 System.IO.FileAccess?

// ✅ 正确：显式指定
var file = Godot.FileAccess.Open(path, mode);
```

### Agent 友好性说明

这个规范的目标是让代码对 Agent 更友好：

1. **可预测性**：隐式行为遵循约定，Agent 可以预测代码行为
2. **可读性**：显式代码清楚地表明意图，减少猜测
3. **可维护性**：减少隐藏的副作用，便于理解和修改
4. **一致性**：统一的模式减少认知负担

**核心原则**：
- **隐式**用于"正常工作"的情况（遵循约定）
- **显式**用于"特殊情况"（破坏约定、处理歧义、明确意图）

---

## 相关文档

- [project_design.md](./project_design.md) - 完整项目设计文档
- [tag_definition.md](./tag_definition.md) - 词条详细定义
- [deck_placeholder.md](./deck_placeholder.md) - 初始卡组占位符
- [phase1_plan.md](./phase1_plan.md) - 第一阶段开发计划
- [refactor_plan_kards_style.md](./refactor_plan_kards_style.md) - KARDS风格重构计划

---

## 关键设计决策

1. **卡牌类型分离**: 将原来的 Attack/Skill/Power 改为 Unit/Order 两种类型
2. **词条系统**: 16种词条, 每种词条有特定的触发时机
3. **轮战机制**: KARDS风格, 打出的牌不洗回抽牌堆, 轮战牌返回抽牌堆
4. **单位属性**: C/aC/A/HP/R 五维属性系统
5. **效果系统**: 支持伤害、治疗、抽牌、增益等多种效果类型
6. **地图系统**: 点位网络地图, BFS最短路径, 部署点机制
7. **敌人AI系统**: 敌人作为打牌对手, 拥有卡组、手牌, AI决策部署和攻击
8. **疲劳机制**: 抽牌堆为空时抽牌造成递增伤害
9. **费用系统**: 自然上限12, 硬上限24, 每回合+1
10. **先后手机制**: 随机先后手, 先手4牌1费, 后手5牌0费
11. **总部继承**: 战斗间继承总部生命值损失
12. **卡组厚度上限**: 默认30张, 支持上限检查和超限调整
13. **战斗奖励系统**: 胜利后三选一卡牌奖励, 权重随机选择
14. **拖拽出牌系统**: KARDS风格拖拽出牌, 单位拖到节点部署, 指令拖到目标使用
15. **卡牌动画系统**: 出牌展示、抽牌、部署动画效果
16. **上下布局**: KARDS风格场景布局, 敌方上方、地图中间、友方下方
17. **UI初始化时序**: HandUI 事件驱动更新, SetPlayer() 只绑定事件不立即调用 UpdateHand(), 等待 DrawCards() 触发事件
18. **UI事件循环防护**: 移除 HandUI 对 _cardContainer.Resized 事件的订阅, 避免容器大小变化触发无限循环导致卡死
19. **胜利条件检查**: CheckCombatEnd() 应检查 HQCurrentHealth 而非 IsDead (IsDead 检查的是角色生命值而非总部生命值)
20. **敌人HQ血量初始化**: Enemy.Initialize() 必须设置 HQCurrentHealth 和 HQMaxHealth, 否则敌人HQ血量默认为8但未正确初始化
21. **统一伤害计算管道**: 参考 slay-the-model 设计, 创建 DamageResolver 作为伤害计算的"唯一真理之源", 阶段顺序: ADDITIVE(加算) → MULTIPLICATIVE(乘算) → CAPPING(限定) → Clamp(取整)
22. **本地化系统**: 参考 slay-the-model 架构, 实现 key-value 映射的多语言支持, YAML 翻译文件, 延迟解析 LocalStr, 运行时语言切换

---

## 后续开发重点

1. ~~地图系统: 点位网络地图, 节点和边~~ ✅ 已完成
2. ~~敌人系统: 敌人使用卡组对战~~ ✅ 已完成
3. ~~战斗系统: 疲劳、费用、先后手、总部继承~~ ✅ 已完成
4. ~~卡组机制: 厚度上限、战斗奖励~~ ✅ 已完成
5. ~~UI系统: 拖拽出牌、单位部署动画~~ ✅ 已完成
6. ~~Bug修复: 战斗界面初始化显示问题~~ ✅ 已完成 (2026-03-02)
7. ~~Bug修复: HandUI 无限循环卡死问题~~ ✅ 已完成 (2026-03-02)
8. ~~Bug修复: 部署单位后直接显示Defeat问题~~ ✅ 已完成 (2026-03-02)
9. ~~本地化系统: 多语言支持, YAML翻译文件~~ ✅ 已完成 (2026-03-02)
10. **游戏测试**: 完整战斗流程测试验证
11. **内容扩展**: 更多卡牌、敌人、词条实现
12. **翻译完善**: 补充更多卡牌和敌人的翻译条目

---

*最后更新: 2026-03-02*
*完成阶段: 第七阶段*
*当前状态: 本地化系统完成, 待测试验证*
