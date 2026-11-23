import type { Language, LocalText } from "./types.ts";

/** 官方标准卡包 ID（与 C# `[Export] public string PackId` 的缺省值对齐）。 */
export const PACK_IDS = {
  core: "core",
  unmannedVictory: "unmanned_victory",
  futureHunt: "future_hunt",
  superBodyProject: "super_body_project",
  reserve: "reserve",
  vanguard: "vanguard",
  cutoffCurrent: "cutoff_current",
} as const;

export interface CardPack {
  id: string;
  /** 短代号，用于卡片徽章与紧凑列表。 */
  code: string;
  name: LocalText;
  description: LocalText;
  color: string;
  order: number;
  official: boolean;
}

/**
 * 六个标准卡包。`reserve` 收纳待重做卡牌，`vanguard` 默认空置，作为新卡与玩家 DIY 的入口。
 */
export const CARD_PACKS: CardPack[] = [
  {
    id: PACK_IDS.core,
    code: "CORE",
    name: { zh: "核心", en: "Core" },
    description: {
      zh: "未归入其他系列的全部基础卡牌。",
      en: "Every foundational card not filed under another series.",
    },
    color: "#ffd98e",
    order: 0,
    official: true,
  },
  {
    id: PACK_IDS.unmannedVictory,
    code: "U/V",
    name: { zh: "无人智胜", en: "Unmanned Victory" },
    description: {
      zh: "联树机器犬、智能臭鸡蛋、刀盾危机、曼巴导弹与「岷山」步行支援机。",
      en: "Lianshu machines, smart eggs, blade tactics, Mamba missiles and the Mingshan walker.",
    },
    color: "#7fd8ff",
    order: 1,
    official: true,
  },
  {
    id: PACK_IDS.futureHunt,
    code: "F/H",
    name: { zh: "未来追杀", en: "Future Hunt" },
    description: {
      zh: "机械族单位与白色军团——机械小蠊、机械蜈蚣、百机长、墓碑等。",
      en: "Mechanics units and the White Legion — roaches, centipedes, the Centurion, tombstones and more.",
    },
    color: "#ff9e7d",
    order: 2,
    official: true,
  },
  {
    id: PACK_IDS.superBodyProject,
    code: "S/B",
    name: { zh: "超体计划", en: "Super Body Project" },
    description: {
      zh: "碎片子弹、烟雾恢复、先发制人等超体对抗指令。",
      en: "Frag bullets, smoke recovery, preemptive strike and other super-body orders.",
    },
    color: "#b44cff",
    order: 3,
    official: true,
  },
  {
    id: PACK_IDS.reserve,
    code: "RES",
    name: { zh: "预备", en: "Reserve" },
    description: {
      zh: "需要重做或存在待修问题的卡牌暂存区：无限潜能、无限火力与计划。",
      en: "Holding area for cards awaiting redesign or fixes: Unlimited Potential, Infinite Fire and Plan.",
    },
    color: "#8c8c94",
    order: 4,
    official: true,
  },
  {
    id: PACK_IDS.vanguard,
    code: "VGD",
    name: { zh: "先锋", en: "Vanguard" },
    description: {
      zh: "新卡默认加入的卡包，也是玩家 DIY 扩展的起跑线。",
      en: "Default home for newly added cards and the starting line for player-made expansions.",
    },
    color: "#8fd9b6",
    order: 5,
    official: true,
  },
  {
    id: PACK_IDS.cutoffCurrent,
    code: "C/C",
    name: { zh: "断流", en: "Cutoff Current" },
    description: {
      zh: "潜艇战主题：弃牌、回卡组顶与反制指令。",
      en: "Submarine warfare: discard, deck-top return and counter-order tactics.",
    },
    color: "#4d9eff",
    order: 6,
    official: true,
  },
];

export const STANDARD_PACK_IDS: ReadonlySet<string> = new Set(CARD_PACKS.map((pack) => pack.id));

/** 依据 docs/draft/卡包.md 逐项列出的「无人智胜」成员（40 系主战坦克已于 0.2.3 移入核心）。 */
const UNMANNED_VICTORY_CARDS: ReadonlySet<string> = new Set([
  "minion_lianshu_scout", // 联树侦察犬（侦察型）
  "minion_lianshu_transport", // 联树机器犬 运输型
  "minion_smart_stinky_egg", // 智能臭鸡蛋
  "spell_blade_crisis", // 刀盾危机
  "minion_what_the_dog_doing", // 我的刀盾
  "spell_mamba_missile", // 曼巴导弹
  "minion_mingshan_walking_support", // 「岷山」步行支援机（机械族但明确排除出未来追杀）
]);

/**
 * 「未来追杀」成员：与 `Resources/Cards/*.tres` 中 `PackId = "future_hunt"` 的卡牌一一对应。
 * 卡包与机制标签完全正交——不再因为带 Mechanics 标签就自动归入本包。
 */
const FUTURE_HUNT_CARDS: ReadonlySet<string> = new Set([
  "minion_roach", // 机械小蠊
  "minion_centipede_aa", // 机械蜈蚣-防空型
  "minion_centipede_siege", // 机械蜈蚣-攻城型
  "minion_centurion", // 百机长
  "minion_mech_lancer", // 机械枪骑兵
  "minion_tombstone", // 墓碑
  "spell_white_legion", // 白色军团（0.2.3 从核心移入未来追杀）
]);

/** 0.2.3「预备」暂存区：待重做或存在待修问题的卡牌。 */
const RESERVE_CARDS: ReadonlySet<string> = new Set([
  "domain_unlimited_potential", // 无限潜能
  "domain_infinite_fire", // 无限火力
  "spell_plan", // 计划（与「燃起」重复，待重做）
]);

/** 草案「超体计划」逐项列出的指令。 */
const SUPER_BODY_PROJECT_CARDS: ReadonlySet<string> = new Set([
  "spell_frag_bullet", // 碎片子弹
  "spell_smoke_restore", // 烟雾恢复
  "domain_preemptive_strike", // 先发制人
]);

/** 0.2.2「断流」潜艇战主题卡（能量币属于核心包，不在此列）。 */
const CUTOFF_CURRENT_CARDS: ReadonlySet<string> = new Set([
  "spell_093b_ssn", // 093B SSN
  "spell_uss_indiana", // 印第安纳号
  "spell_beneath_black_water", // 黑水之下
  "spell_ultimatum", // 最后通牒
  "counter_ohio_ssbn", // 俄亥俄级战略核潜艇
  "counter_borei", // 北风之神
]);

/**
 * 当 `.tres` 尚未写入 `PackId` 时，依据显式卡单推断卡包归属。
 * 卡包与机制标签正交：Mechanics 等标签不参与判断，未列入任何卡单的卡默认归入核心。
 * `_mechanicTagKeys` 仅保留旧调用签名兼容，函数不再读取。
 */
export function resolveDefaultPackId(cardId: string, _mechanicTagKeys?: readonly string[]): string {
  if (UNMANNED_VICTORY_CARDS.has(cardId)) return PACK_IDS.unmannedVictory;
  if (FUTURE_HUNT_CARDS.has(cardId)) return PACK_IDS.futureHunt;
  if (SUPER_BODY_PROJECT_CARDS.has(cardId)) return PACK_IDS.superBodyProject;
  if (CUTOFF_CURRENT_CARDS.has(cardId)) return PACK_IDS.cutoffCurrent;
  if (RESERVE_CARDS.has(cardId)) return PACK_IDS.reserve;
  return PACK_IDS.core;
}

/** 校验可写回 .tres 的卡包 ID：不含引号/空白/控制字符的稳定 slug。 */
export function isValidPackId(value: unknown): value is string {
  return typeof value === "string" && /^[A-Za-z][A-Za-z0-9_-]{0,47}$/.test(value.trim());
}

export function normalizePackId(value: unknown): string | null {
  if (!isValidPackId(value)) return null;
  return value.trim();
}

export function getPackDefinition(id: string): CardPack | undefined {
  return CARD_PACKS.find((pack) => pack.id === id);
}

/** 将 snake_case / kebab-case 的自定义 ID 转成分展示用的标题。 */
export function prettifyPackId(id: string): string {
  if (!id) return id;
  return id
    .split(/[_-]+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export function packLabel(id: string, language: Language): string {
  return getPackDefinition(id)?.name[language] ?? prettifyPackId(id);
}

export function packColor(id: string): string {
  return getPackDefinition(id)?.color ?? "#9a8fae";
}

/** 供 API / 预览共用的卡包摘要。 */
export interface PackSummary extends CardPack {
  count: number;
  custom: boolean;
}

export function summarizePacks(cardPackIds: readonly string[]): PackSummary[] {
  const counts = new Map<string, number>();
  for (const id of cardPackIds) counts.set(id, (counts.get(id) ?? 0) + 1);
  const summaries: PackSummary[] = CARD_PACKS.map((pack) => ({
    ...pack,
    count: counts.get(pack.id) ?? 0,
    custom: false,
  }));
  for (const id of [...counts.keys()].sort()) {
    if (STANDARD_PACK_IDS.has(id)) continue;
    const pretty = prettifyPackId(id);
    summaries.push({
      id,
      code: id.slice(0, 3).toUpperCase(),
      name: { zh: pretty, en: pretty },
      description: { zh: "玩家自定义卡包", en: "Player-made card pack" },
      color: "#9a8fae",
      order: 100,
      official: false,
      count: counts.get(id) ?? 0,
      custom: true,
    });
  }
  return summaries;
}
