import type { RulesResponse } from "./types.ts";

export const RULES: RulesResponse = {
  source: "res://docs/draft/PRD-kards-frontline-refactor.md",
  status: "Draft v1.0 · 三线重构规范",
  board: {
    rows: ["enemySupport", "frontline", "playerSupport"],
    capacities: [4, 5, 4],
    deployment: {
      zh: "所有随从只能部署到己方支援线；前线只能通过独立移动事务进入，且由单一阵营独占。",
      en: "All units deploy to their own support row. Frontline entry is a separate move, and only one faction may occupy it.",
    },
  },
  unitClasses: [
    {
      id: "vanguard",
      name: { zh: "先锋", en: "Vanguard" },
      actions: { zh: "移动或攻击 1 次", en: "Move or attack once" },
      range: {
        zh: "近战；支援线只能攻击敌占前线，推进后可打支援线与HQ",
        en: "Melee; from support, hit the enemy frontline. Advance to reach support and HQ.",
      },
      trait: { zh: "阵线中坚，适合守护、嘲讽与伏击。", en: "Line infantry suited to Guard, Taunt and Ambush." },
    },
    {
      id: "assault",
      name: { zh: "突击", en: "Assault" },
      actions: { zh: "移动 1 次 + 攻击 1 次", en: "Move once + attack once" },
      range: { zh: "近战；与先锋相同", en: "Melee; same reach as Vanguard" },
      trait: {
        zh: "每回合可推进并攻击；移动与攻击分别支付油费。",
        en: "Can advance and fire in one turn; each action costs oil.",
      },
    },
    {
      id: "support",
      name: { zh: "支援", en: "Support" },
      actions: { zh: "移动或攻击 1 次", en: "Move or attack once" },
      range: { zh: "跨线全图", en: "Global range" },
      trait: {
        zh: "主动普通攻击免反击，且不触发目标伏击。",
        en: "Attacks without retaliation and does not trigger Ambush.",
      },
    },
    {
      id: "skirmisher",
      name: { zh: "游击", en: "Skirmisher" },
      actions: { zh: "移动或攻击 1 次", en: "Move or attack once" },
      range: { zh: "跨线全图，正常承受反击", en: "Global range; receives retaliation" },
      trait: {
        zh: "支援线免近战攻击；同线游击可为非游击单位与HQ护航。",
        en: "Immune to melee in support; allied Skirmishers screen their row and HQ.",
      },
    },
  ],
  keywords: [
    {
      id: "charge",
      name: { zh: "闪击", en: "Charge" },
      description: {
        zh: "部署当回合立即解锁兵种行动配额；先锋仍只能移动或攻击。",
        en: "Ready on deployment. Vanguard must still choose between moving and attacking.",
      },
    },
    {
      id: "taunt",
      name: { zh: "嘲讽", en: "Taunt" },
      description: {
        zh: "最高优先级防线。拦截同阵线目标；任意可达嘲讽均能保护HQ。",
        en: "Top-priority defense. Screens its row; any reachable Taunt can protect HQ.",
      },
    },
    {
      id: "guard",
      name: { zh: "守护", en: "Guard" },
      description: {
        zh: "保护同线左右相邻的非守护、非嘲讽随从；位于支援阵线时也能掩护我方英雄（HQ），但不与另一守护互锁。",
        en: "Protects adjacent non-Guard, non-Taunt allies; a Guard in the Support row also shields its own hero (HQ), and two Guards never lock each other.",
      },
    },
    {
      id: "ambush",
      name: { zh: "伏击", en: "Ambush" },
      description: {
        zh: "每回合首次被攻击时先于攻击者反击；支援主动攻击不会触发。",
        en: "Retaliates before the first attacker each turn. Support attacks bypass it.",
      },
    },
    {
      id: "impact",
      name: { zh: "冲击", en: "Impact" },
      description: {
        zh: "攻击时抵消所有反击伤害，一次性消耗。",
        en: "Negates all retaliation when attacking; consumed on use.",
      },
    },
    {
      id: "recycle",
      name: { zh: "轮战", en: "Recycle" },
      description: {
        zh: "法术打出或随从被击败后回到抽牌堆底部，而非弃牌堆。",
        en: "Returns to the bottom of the draw pile instead of the discard pile.",
      },
    },
    {
      id: "qiqiao",
      name: { zh: "奇巧", en: "Qiqiao" },
      description: { zh: "被弃掉时自动打出。", en: "Automatically plays when discarded." },
    },
    {
      id: "smokescreen",
      name: { zh: "烟幕", en: "Smokescreen" },
      description: {
        zh: "无法成为普通攻击目标，但仍可被法术与效果命中；与嘲讽、守护互斥。",
        en: "Cannot be targeted by normal attacks, but effects still hit. Mutually exclusive with Taunt and Guard.",
      },
    },
    {
      id: "intel",
      name: { zh: "情报", en: "Intel" },
      description: {
        zh: "从左到右揭露敌方暗牌；未实际揭露的部分转化为溢出情报。",
        en: "Reveals enemy hidden cards left to right. Unspent reveals become Excess Intel.",
      },
    },
  ],
  priority: [
    {
      zh: "01 / 基础可达：存活、无烟幕，且处于兵种或武器射程内。",
      en: "01 / Reach: alive, visible, and within the attacker's range.",
    },
    {
      zh: "02 / 嘲讽：可达嘲讽优先，直接越过守护与护航，避免死锁。",
      en: "02 / Taunt: reachable Taunts take priority and bypass Guard and escort locks.",
    },
    {
      zh: "03 / 守护：相邻守护掩护普通随从；替游击挡刀的守护可被优先拆除。",
      en: "03 / Guard: adjacent Guards screen normal allies; Guards covering a Skirmisher can be removed first.",
    },
    {
      zh: "04 / 游击护航：游击不能越过同线护航攻击普通单位或HQ。",
      en: "04 / Escort: Skirmishers cannot bypass an allied Skirmisher to hit normal units or HQ.",
    },
  ],
  intel: {
    reveal: {
      zh: "情报 N 从左向右揭露最多 N 张尚未公开的敌方手牌；同名不同实例独立追踪。",
      en: "Intel N reveals up to N unrevealed cards from left to right. Copies are tracked separately.",
    },
    overflow: {
      zh: "实际新揭露 K 张，则溢出情报 = N − K。敌方空手、暗牌不足或无手牌架构都会提高溢出，但溢出不产生任何额外收益。",
      en: "If K new cards are revealed, Excess Intel = N − K. Empty hands and intent-driven enemies increase overflow, but overflow grants no extra effect.",
    },
    conversion: {
      zh: "多余点数直接溢出：不回复生命、不提供格挡，也不造成直伤；仅触发溢出情报事件供卡牌与藏品联动。",
      en: "Leftover points simply overflow: no healing, no Block and no direct damage. The overflow event still fires for synergies.",
    },
  },
};
