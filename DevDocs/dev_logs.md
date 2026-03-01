# 开发日志

## 2026-02-27

### 警告修复 (Warning Cleanup)

**任务**: 减少构建输出噪音，修复 114 个代码分析警告

**修改文件**:
- `Directory.Build.props` - 添加 NoWarn 配置

**抑制的警告类型**:

| 警告代码 | 描述 | 原因 |
|---------|------|------|
| CA2213 | IDisposable 未释放 | Godot 节点继承 IDisposable 但由 Godot 引擎管理生命周期 |
| CA2000 | IDisposable 对象未释放 | 游戏资源由 Godot 自动管理 |
| CA1003 | Action 改为 EventHandler | Action 更适合 C# 事件模式 |
| CA1002 | List 改为 ReadOnlyCollection | 对于游戏开发 List 更实用 |
| CA1304/5 | 区域设置警告 | 游戏不需要全球化 |
| CA1822 | 静态方法建议 | 保持实例方法更灵活 |
| CS8618 | 可空性警告 | Godot 节点在构造函数中无法初始化 |
| CS8625 | null 字面量警告 | Godot 节点初始化模式 |
| CA1805 | 显式初始化默认值 | 字段初始化是良好实践 |
| CA1311 | 区域性依赖 | 游戏使用固定字符串 |
| CS0067 | 从不使用事件 | 事件可能未来使用 |

**结果**: `dotnet build` → 0 错误, 0 警告

**Git Commit**: `839b9d7` - chore: 抑制分析器警告，减少构建输出噪音

---

## 2026-03-01

### 第一阶段：核心数据结构重构 (Phase 1: Core Data Structure Refactor)

**任务**: 将卡牌系统从杀戮尖塔风格重构为KARDS风格，支持单位/指令两种类型和词条系统

**新建文件**:
- `Scripts/Core/CardType.cs` - 卡牌类型枚举（Unit, Order）
- `Scripts/Core/CardTag.cs` - 标签枚举（16种词条）
- `Scripts/Core/UnitData.cs` - 单位数据资源类（C/aC/A/HP/R/Tags/Effects）
- `Scripts/Core/OrderData.cs` - 指令数据资源类（C/Tags/Effects）
- `Scripts/Card/CardBase.cs` - 卡牌抽象基类
- `Scripts/Card/Unit.cs` - 单位运行时类
- `Scripts/Card/Order.cs` - 指令运行时类
- `Scripts/Card/Tags/TagDefinition.cs` - 词条定义基类
- `Scripts/Card/Tags/TagContext.cs` - 词条上下文
- `Scripts/Card/Tags/TagFactory.cs` - 词条工厂
- `Scripts/Card/Tags/TagImplementations.cs` - 16种词条实现
- `Resources/Cards/Unit_DetectiveSquad.tres` - 武侦小组（1/0/3/1/1，闪击+机动）
- `Resources/Cards/Unit_18thRegiment.tres` - 第18团（1/1/1/4/1，守护+轮战）
- `Resources/Cards/Unit_LianshuScout.tres` - 联树机器犬（0/0/1/1/1，闪击+轮战+亡语）
- `Resources/Cards/Order_Strike.tres` - 打击（费用1，造成3点伤害）
- `Resources/Cards/Order_Assault.tres` - 出击（费用2，轮战，造成2点伤害+抽1牌）
- `Resources/Cards/Order_Alert.tres` - 警戒（费用2，轮战，抽1牌+总部+2HP）

**修改文件**:
- `Scripts/Core/CardEffectData.cs` - 重构适配新系统
- `Scripts/Character/Player.cs` - 适配新卡牌类型
- `Scripts/Character/Enemy.cs` - 简化为占位符
- `Scripts/Character/EnemyFactory.cs` - 更新工厂方法
- `Scripts/Card/CardFactory.cs` - 更新卡牌创建逻辑
- `Scripts/Combat/CombatManager.cs` - 适配新卡牌系统
- `Scripts/UI/CardUI.cs` - 适配新卡牌显示
- `Scripts/UI/HandUI.cs` - 适配新卡牌交互
- `Scripts/UI/CombatUI.cs` - 适配新卡牌显示
- `Scripts/Core/GameManager.cs` - 使用新卡组

**删除文件**:
- `Scripts/Core/CardData.cs` - 被 UnitData/OrderData 替代
- `Scripts/Card/Card.cs` - 被 CardBase/Unit/Order 替代
- `Scripts/Card/Effects/CardEffect.cs` - 不再需要
- `Scripts/Card/Effects/CardEffectImplementations.cs` - 不再需要
- `Scripts/Character/EnemyAction.cs` - 不再需要
- `Scripts/Character/EnemyData.cs` - 不再需要
- `Resources/Cards/Strike.tres` - 旧卡牌数据
- `Resources/Cards/Defend.tres` - 旧卡牌数据
- `Resources/Enemies/*` - 旧敌人数据

**实现的词条**:
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

**结果**: `dotnet build` → 0 错误, 7 警告（均为代码风格建议，不影响功能）

---

### 词条英文命名调整 (Tag Renaming)

**任务**: 调整三个词条的英文命名，使其更符合KARDS原版风格

**修改内容**:
| 中文 | 原英文 | 新英文 |
|-----|--------|--------|
| 压制 | Suppressed | Pin |
| 抑制 | Inhibited | Suppress |
| 断流 | Isolation | Massive |

**修改文件**:
- `Scripts/Core/CardTag.cs` - 枚举值重命名
- `Scripts/Card/Tags/TagImplementations.cs` - 类名和属性引用更新
- `Scripts/Card/Tags/TagFactory.cs` - switch case 更新
- `Scripts/Card/Unit.cs` - 属性名称更新 (IsPinned, IsSuppressed, IsMassive)
- `.trae/documents/tag_definition.md` - 文档更新
- `.trae/documents/project_spec_record.md` - 规范记录更新

**命名说明**:
- **Pin**: 军事术语 "pin down"，表示压制敌人使其无法移动
- **Suppress**: 表示抑制能力/效果
- **Massive**: 表示"大型单位"，因规模大无法与其他单位共享节点（源自"投鞭断流"典故）

**结果**: `dotnet build` → 0 错误, 7 警告

---

### 词条示范卡牌创建 (Demo Cards)

**任务**: 创建一组示范卡牌，每张接近白板只拥有1-2个词条，用于规范实现方式

**新建目录**: `Resources/Cards/Demo/`

**单位卡牌 (13张)**:
| 名称 | C/aC/A/HP/R | 词条 | 说明 |
|-----|-------------|------|------|
| 突击步兵 | 2/0/2/2/1 | 闪击 | 部署后可立即行动 |
| 侦察车 | 1/0/1/1/2 | 机动x2 | 每层额外行动1次 |
| 老兵 | 2/1/2/3/1 | 奋战 | 每回合可攻击两次 |
| 重装兵 | 3/0/1/4/1 | 防御x2 | 每层减免1点伤害 |
| 护卫队 | 2/0/1/3/1 | 守护 | 保护距离内的友方单位 |
| 伏击手 | 1/0/2/1/1 | 伏击 | 首次被攻击时先反击 |
| 冲锋队 | 2/0/3/1/1 | 冲击 | 首次攻击不受反击伤害 |
| 不朽者 | 5/0/1/1/1 | 免疫 | 不会受到伤害 |
| 巨型坦克 | 4/2/4/6/2 | 断流 | 无法与其他单位共享节点 |
| 渗透者 | 2/0/2/2/1 | 渗透 | 可移动到敌方单位节点 |
| 轮战步兵 | 1/0/1/2/1 | 轮战 | 阵亡后返回抽牌堆 |
| 工兵 | 1/0/1/2/1 | 部署 | 部署：抽1张牌 |
| 烈士 | 0/0/1/1/1 | 亡语 | 亡语：造成2点伤害 |

**指令卡牌 (3张)**:
| 名称 | 费用 | 词条 | 效果 |
|-----|------|------|------|
| 轮战打击 | 2 | 轮战 | 造成2点伤害 |
| 治疗 | 1 | - | 恢复2点生命值 |
| 补给 | 2 | - | 抽2张牌 |

**修改文件**:
- `Scripts/Card/CardFactory.cs` - 添加 `GetDemoDeck()` 方法
- `Scripts/Combat/CombatManager.cs` - 修复命名空间引用
- `Scripts/Core/GameManager.cs` - 修复命名空间引用

**结果**: `dotnet build` → 0 错误, 7 警告
