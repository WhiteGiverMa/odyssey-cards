# Slay-the-Model 架构学习笔记

> 本文档记录对 [slay-the-model](https://github.com/wkzMagician/slay-the-model) 项目的架构分析，指导 OdysseyCards 未来重构方向。

## 项目概览

slay-the-model 是一个 Python 实现的《杀戮尖塔》核心框架，核心理念：

- **表现层与逻辑层完全分离**
- **Action 队列驱动的游戏流程**
- **精简基类设计**（卡牌基类约 500 行）

---

## 核心架构对比

### 1. Action 队列系统 vs 事件驱动

| 维度 | slay-the-model | OdysseyCards (现状) |
|------|----------------|---------------------|
| 核心模式 | Action 队列 | 事件/信号驱动 |
| 执行方式 | Action.execute() → Result → 队列后续 Action | 直接调用方法，立即执行 |
| 连锁效果 | 自然支持 | 需要手动处理 |

**slay-the-model 的 Action 系统：**

```python
# actions/base.py
class Action:
    def execute(self) -> BaseResult:
        # 返回结果类型：
        # - NoneResult: 完成，无后续
        # - SingleActionResult: 队列一个动作
        # - MultipleActionsResult: 队列多个动作
        # - GameStateResult: 状态转换（胜利/失败）
        raise NotImplementedError

class ActionQueue:
    def __init__(self):
        self.queue: List[Action] = []

    def add_action(self, action, to_front=False):
        # 可添加到队首或队尾

    def execute_next(self) -> BaseResult:
        # 执行下一个 Action，返回结果
```

**OdysseyCards (现状)：**

```csharp
// CombatManager.cs - 直接执行
public void PlayCard(Card card, Character target) {
    // 直接执行逻辑，无队列
    order.Play(Player, target);
    Player.DiscardCard(card);
}
```

**学习价值：**

- Action 队列让复杂的连锁效果变得清晰可控
- 一个动作触发另一个动作是卡牌游戏的核心模式
- 便于实现撤销、回放、AI 决策等功能

---

### 2. 统一的 Result 类型系统

```python
# utils/result_types.py
class ResultType(str, Enum):
    NONE = "none"
    SINGLE_ACTION = "single_action"
    MULTIPLE_ACTIONS = "multiple_actions"
    GAME_STATE = "game_state"

class BaseResult:
    result_type: ResultType

class NoneResult(BaseResult):            # 完成，无后续
class SingleActionResult(BaseResult):    # 队列一个动作
class MultipleActionsResult(BaseResult): # 队列多个动作
class GameStateResult(BaseResult):       # 状态转换 (WIN/LOSE/ESCAPE)
```

**学习价值：**

- 方法返回 Result 而非 void，流程控制更清晰
- 类型安全，便于追踪游戏状态变化
- 支持复杂的动作链：A → B → C → D

---

### 3. 统一的值解析管道

slay-the-model 有精心设计的伤害计算管道：

```python
# utils/dynamic_values.py - resolve_potential_damage()

def resolve_potential_damage(base_damage, attacker, target, card) -> int:
    """
    伤害计算的唯一真理之源 (Single Source of Truth)

    Phase order (CRITICAL - additive before multiplicative before capping):
    1. Normalize: 标准化 (callable/list -> int)
    2. ADDITIVE: 加算 (Strength +3, Dexterity for block)
    3. MULTIPLICATIVE: 乘算 (Vulnerable 1.5x, Weak 0.75x, PenNib 2x)
    4. CAPPING: 限定 (Intangible caps to 1)
    5. Clamp: 取整 (max 0)

    For each phase: Powers -> Relics
    """
    damage = base_damage

    # Phase 2: ADDITIVE
    for power in attacker.powers:
        if power.modify_phase == DamagePhase.ADDITIVE:
            damage = power.modify_damage_dealt(damage)
    for relic in attacker.relics:
        if relic.modify_phase == DamagePhase.ADDITIVE:
            damage = relic.modify_damage_dealt(damage, card, target)

    # Phase 3: MULTIPLICATIVE
    for power in attacker.powers:
        if power.modify_phase == DamagePhase.MULTIPLICATIVE:
            damage = power.modify_damage_dealt(damage)
    # ... target's Vulnerable, etc.

    # Phase 4: CAPPING
    for power in target.powers:
        if power.modify_phase == DamagePhase.CAPPING:
            damage = power.modify_damage_taken(damage)

    return max(0, int(damage))
```

**OdysseyCards (现状)：**

伤害计算逻辑可能分散在多处，没有统一的管道。

**学习价值：**

- 所有伤害计算走同一管道，确保修改器不会遗漏
- 明确的阶段顺序避免计算错误
- 便于添加新的修改器类型

---

### 4. 敌人意图系统

```python
# enemies/base.py
class Enemy(Creature):
    def __init__(self):
        self.intentions: Dict[str, Intention] = {}  # 所有可能的意图
        self.current_intention: Intention = None    # 当前意图
        self.history_intentions: List[str] = []     # 历史记录

    def on_player_turn_start(self):
        # 在玩家回合开始时决定下一个意图
        self.current_intention = self.determine_next_intention()

    def execute_intention(self) -> List[Action]:
        # 执行当前意图，返回 Action 列表
        return self.current_intention.execute()
```

**Intention 类设计：**

```python
# enemies/intention.py
class Intention:
    name: str                    # 意图名称
    damage: int = 0              # 伤害值 (用于显示)
    description: BaseLocalStr    # 描述文本

    def execute(self) -> List[Action]:
        # 执行意图，返回 Action 列表
```

**学习价值：**

- 玩家能预知敌人行动，增加策略深度
- 意图历史记录便于实现复杂的敌人 AI
- 意图与执行分离，便于测试

---

### 5. 卡牌设计对比

| 维度 | slay-the-model | OdysseyCards (现状) |
|------|----------------|---------------------|
| 定义方式 | 类驱动（每个卡牌一个类） | 数据驱动（Resource 文件） |
| 数值系统 | base_damage + upgrade_damage | CardData 中的字段 |
| 动态值 | `resolve_card_value(card, 'damage')` | 直接属性访问 |
| 升级系统 | `upgrade()` 方法 + upgrade_xxx 字段 | 需要查看实现 |

**slay-the-model 的卡牌类：**

```python
# cards/base.py
class Card:
    # 类属性定义基础值
    base_damage = 6
    base_block = 0
    base_cost = 1

    # 升级后的值
    upgrade_damage = 9
    upgrade_cost = 1

    def on_play(self, targets) -> List[Action]:
        """卡牌被打出时触发，返回 Action 列表"""
        actions = []
        if self.damage > 0:
            actions.append(DealDamageAction(damage=self.damage, target=targets[0]))
        if self.block > 0:
            actions.append(GainBlockAction(block=self.block, target=self.owner))
        return actions

    def on_draw(self) -> List[Action]:
        """抽到时触发"""
        return []

    def on_discard(self) -> List[Action]:
        """弃置时触发"""
        return []

    def on_exhaust(self) -> List[Action]:
        """消耗时触发"""
        return []
```

**学习价值：**

- `on_play() -> List[Action>` 模式清晰表达卡牌效果
- 生命周期钩子 (on_draw, on_discard, on_exhaust) 便于扩展
- 动态值解析支持 Strength/Dexterity 等修改器

---

### 6. 全局状态管理

```python
# engine/game_state.py
class GameState:
    def __init__(self):
        # 多章节支持
        self.current_act: int = 1
        self.floor_in_act: int = 0

        # 全局 Action 队列
        self.action_queue = ActionQueue()

        # 当前战斗
        self.current_combat: Optional[Combat] = None

        # 玩家
        self.player = create_player(config.character)

        # 遗物追踪
        self.obtained_relics: set = set()

        # 遭遇历史
        self.encounter_history: List[str] = []

    def execute_all_actions(self) -> BaseResult:
        """执行队列中所有 Action"""
        while not self.action_queue.is_empty():
            result = self.action_queue.execute_next()
            if isinstance(result, GameStateResult):
                return result
            # 处理 SingleActionResult, MultipleActionsResult
        return NoneResult()

# 全局单例
game_state = GameState()
```

**学习价值：**

- 全局 Action 队列统一管理所有游戏动作
- 单一入口 `execute_all_actions()` 执行所有逻辑
- 便于实现回放、AI 决策、网络同步

---

## 架构哲学对比

| 维度 | slay-the-model | OdysseyCards (现状) |
|------|----------------|---------------------|
| 图形依赖 | 无（纯逻辑/TUI） | Godot 引擎集成 |
| 定位 | 研究/AI 训练框架 | 完整游戏开发 |
| 数据定义 | 类驱动，代码即数据 | 数据驱动，Resource 文件 |
| 执行模式 | 单线程队列执行 | Godot 节点树 + 信号 |
| 可测试性 | 极高（无图形依赖） | 需要 Godot 环境 |

---

## 重构建议

### 短期（可立即借鉴）

1. **引入 Action 基类和队列**
   - 创建 `GameAction` 基类
   - 创建 `ActionQueue` 管理器
   - 让卡牌效果返回 `List<GameAction>`

2. **统一伤害计算**
   - 创建 `DamageResolver` 类
   - 定义修改器阶段：Additive → Multiplicative → Capping
   - 所有伤害计算走同一管道

3. **敌人意图系统**
   - 创建 `Intention` 类
   - 敌人在回合开始时决定意图
   - UI 显示意图图标

### 中期（需要规划）

4. **Result 类型系统**
   - 方法返回 Result 而非 void
   - 更清晰的游戏流程控制

5. **本地化系统**
   - 所有文本用 key 引用
   - 支持中英文切换

6. **生命周期钩子**
   - Card: on_draw, on_play, on_discard, on_exhaust
   - Enemy: on_combat_start, on_turn_start, on_death

### 长期（架构演进）

7. **逻辑层与表现层分离**
   - 核心逻辑不依赖 Godot 节点
   - 表现层通过信号/事件响应逻辑变化

8. **AI 接口设计**
   - 支持大语言模型决策
   - 支持强化学习训练

---

## 代码参考路径

| 模块 | slay-the-model 路径 |
|------|---------------------|
| Action 系统 | `actions/base.py`, `utils/result_types.py` |
| 战斗引擎 | `engine/combat.py`, `engine/game_state.py` |
| 卡牌系统 | `cards/base.py` |
| 敌人系统 | `enemies/base.py`, `enemies/intention.py` |
| 值解析 | `utils/dynamic_values.py`, `utils/damage_phase.py` |
| 本地化 | `localization/__init__.py`, `localization/zh.yaml` |

---

## 相关资源

- [slay-the-model GitHub](https://github.com/wkzMagician/slay-the-model)
- 本地克隆路径：`G:\dev\slay-the-model`

---

## 下一步学习计划

> 基于 slay-the-model docs 目录和 OdysseyCards 当前实现状态分析

### OdysseyCards 现状分析

| 模块 | 实现状态 | 对比 slay-the-model |
|------|---------|---------------------|
| 战斗系统 | ✅ 基础完成 | 事件驱动，无 Action 队列 |
| 敌人 AI | ✅ 简单实现 | 无意图系统，决策逻辑简单 |
| 卡牌系统 | ✅ Unit/Order | 数据驱动，无生命周期钩子 |
| 战斗地图 | ✅ BattleMap | 有战场地图，非游戏地图 |
| 游戏地图 | ❌ 未实现 | 缺少 Slay the Spire 风格的地图导航 |
| 遗物系统 | ❌ 未实现 | - |
| 药剂系统 | ❌ 未实现 | - |
| 房间系统 | ❌ 未实现 | - |

### slay-the-model docs 目录内容摘要

| 文档 | 核心内容 | 学习价值 |
|------|---------|---------|
| `combat.md` | 回合结构、意图系统、Boss/精英战 | 战斗流程优化 |
| `cards.md` | 5种卡牌类型、稀有度、升级机制 | 卡牌系统完善 |
| `rooms.md` | 7种房间类型、奖励机制 | 游戏地图系统设计 |
| `relics.md` | 遗物稀有度、功能分类 | 遗物系统设计 |
| `potions.md` | 药剂类型、获得方式 | 药剂系统设计 |
| `map_system_design.md` | 地图生成算法、防交叉连接 | 地图系统实现 |

---

### 学习优先级

#### 🔴 高优先级（核心体验提升）

**1. 敌人意图系统**

slay-the-model 的敌人意图系统是策略深度的核心：

```python
# enemies/intention.py
class Intention:
    name: str           # "Attack", "Defend", "Buff", "Debuff", "Special"
    damage: int = 0     # 用于 UI 显示伤害值
    description: str    # 描述文本

    def execute(self) -> List[Action]:
        """执行意图，返回 Action 列表"""
```

**OdysseyCards 当前问题：**
- `EnemyAI.DecideAction()` 是即时决策，玩家无法预知
- 缺少 `Intention` 类和意图显示

**学习目标：**
- 创建 `Intention` 类定义意图类型
- 敌人在玩家回合开始时决定下一回合意图
- UI 显示意图图标和数值

**参考代码：**
- `G:\dev\slay-the-model\enemies\base.py` - 敌人意图管理
- `G:\dev\slay-the-model\enemies\intention.py` - Intention 类设计
- `G:\dev\slay-the-model\docs\combat.md` - 意图系统说明

---

**2. 统一伤害计算管道**

slay-the-model 的伤害计算是"唯一真理之源"：

```python
# utils/dynamic_values.py
def resolve_potential_damage(base_damage, attacker, target, card) -> int:
    """
    Phase order (CRITICAL):
    1. Normalize: 标准化
    2. ADDITIVE: 加算 (Strength +3)
    3. MULTIPLICATIVE: 乘算 (Vulnerable 1.5x)
    4. CAPPING: 限定 (Intangible caps to 1)
    5. Clamp: 取整 (max 0)
    """
```

**OdysseyCards 当前问题：**
- 伤害计算分散在 `ExecuteAttack()` 等方法中
- 无修改器阶段概念
- 难以添加新的伤害修改效果

**学习目标：**
- 创建 `DamageResolver` 类
- 定义 `DamagePhase` 枚举
- 所有伤害计算走统一管道

**参考代码：**
- `G:\dev\slay-the-model\utils\dynamic_values.py`
- `G:\dev\slay-the-model\utils\damage_phase.py`

---

#### 🟡 中优先级（架构改进）

**3. Action 队列系统**

slay-the-model 的核心架构：

```python
class Action:
    def execute(self) -> BaseResult:
        # 返回 Result，可能队列更多 Action
        return NoneResult()

class ActionQueue:
    def execute_next(self) -> BaseResult:
        action = self.queue.pop(0)
        result = action.execute()
        # 根据 Result 类型处理后续
```

**学习目标：**
- 创建 `GameAction` 基类
- 创建 `ActionResult` 类型系统
- 重构卡牌效果返回 `List<GameAction>`

**参考代码：**
- `G:\dev\slay-the-model\actions\base.py`
- `G:\dev\slay-the-model\utils\result_types.py`

---

**4. 游戏地图系统**

slay-the-model 有完整的地图导航系统：

```
Floor 0: 3 个起始节点
Floor 8: 宝箱层
Floor 14: 休息层
Floor 15: Boss 层

房间类型: MONSTER(53%), ELITE(8%), REST(12%), MERCHANT(5%), UNKNOWN(22%)
```

**OdysseyCards 当前状态：**
- 有 `BattleMap`（战斗战场）
- 无游戏地图导航系统

**学习目标：**
- 创建 `GameMap` 和 `MapNode` 类
- 实现地图生成算法
- 实现房间类型和奖励

**参考代码：**
- `G:\dev\slay-the-model\map\map_manager.py`
- `G:\dev\slay-the-model\map\map_data.py`
- `G:\dev\slay-the-model\docs\map_system_design.md`
- `G:\dev\slay-the-model\docs\rooms.md`

---

**5. 卡牌生命周期钩子**

slay-the-model 的卡牌有完整生命周期：

```python
class Card:
    def on_play(self, targets) -> List[Action]:   # 打出时
    def on_draw(self) -> List[Action]:            # 抽到时
    def on_discard(self) -> List[Action]:         # 弃置时
    def on_exhaust(self) -> List[Action]:         # 消耗时
```

**学习目标：**
- 在 `CardBase` 中添加生命周期方法
- 在 `CombatManager` 中调用相应钩子

**参考代码：**
- `G:\dev\slay-the-model\cards\base.py`
- `G:\dev\slay-the-model\docs\cards.md`

---

#### 🟢 低优先级（功能扩展）

**6. 遗物系统**

- 参考：`G:\dev\slay-the-model\docs\relics.md`
- 稀有度：Common, Uncommon, Rare, Boss
- 功能分类：攻击增强、防御生存、资源管理、卡组优化

**7. 药剂系统**

- 参考：`G:\dev\slay-the-model\docs\potions.md`
- 战斗中使用的消耗品
- 携带数量限制（通常最多 3 瓶）

---

### 推荐学习顺序

```
Week 1: 敌人意图系统
├── 阅读 enemies/base.py, enemies/intention.py
├── 设计 OdysseyCards 的 Intention 类
└── 实现意图显示 UI

Week 2: 统一伤害计算
├── 阅读 utils/dynamic_values.py
├── 创建 DamageResolver 类
└── 重构现有伤害计算

Week 3: Action 队列系统
├── 阅读 actions/base.py, utils/result_types.py
├── 设计 GameAction 基类
└── 逐步迁移现有逻辑

Week 4: 游戏地图系统
├── 阅读 map/map_manager.py, docs/map_system_design.md
├── 设计 GameMap 类
└── 实现房间类型
```

---

## 相关资源

- [slay-the-model GitHub](https://github.com/wkzMagician/slay-the-model)
- 本地克隆路径：`G:\dev\slay-the-model`
- slay-the-model 文档路径：`G:\dev\slay-the-model\docs\`

---

*文档创建时间：2026-03-02*
*最后更新：2026-03-02*
