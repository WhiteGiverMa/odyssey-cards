import type { CardTypeKey, Language, LocalText, UnitClass } from "./types.ts";

export const TYPE_KEYS: CardTypeKey[] = ["minion", "spell", "domain", "status"];
export const RARITY_KEYS = ["derivative", "master", "excellent", "good", "common", "special", "statusToken"];
export const KEYWORD_KEYS = [
  "none",
  "charge",
  "taunt",
  "battlecry",
  "deathrattle",
  "windfury",
  "ambush",
  "impact",
  "recycle",
  "unplayable",
  "ethereal",
  "qiqiao",
  "guard",
  "smokescreen",
  "intel",
];
export const MECHANIC_TAGS: Array<[string, number]> = [
  ["directDamage", 1],
  ["damageOverTime", 2],
  ["heal", 4],
  ["armor", 8],
  ["draw", 16],
  ["discover", 32],
  ["summon", 64],
  ["buff", 128],
  ["silence", 256],
  ["discard", 512],
  ["domain", 1024],
  ["weaponSynergy", 2048],
  ["manaRamp", 4096],
  ["statusApply", 8192],
  ["shuffle", 16384],
  ["token", 32768],
  ["mechanics", 65536],
  ["superBody", 131072],
  ["intel", 262144],
];
export const UNIT_CLASSES: UnitClass[] = ["vanguard", "assault", "support", "skirmisher"];
export const RARITY_COLORS = {
  classic: ["#8c8c94", "#ffd700", "#bfc7d1", "#cc8033", "#85858c", "#ff8c00", "#8c8c94"],
  new: ["#ffffff", "#b44cff", "#4d9eff", "#4dd964", "#c8c8c8", "#ff8c00", "#808080"],
};
export const CLASS_COLORS: Record<UnitClass, string> = {
  vanguard: "#ffd98e",
  assault: "#ff9e7d",
  support: "#7fd8ff",
  skirmisher: "#ff9ed2",
};

export const TYPE_NAMES: Record<CardTypeKey, LocalText> = {
  minion: { zh: "随从", en: "Minion" },
  spell: { zh: "法术", en: "Spell" },
  domain: { zh: "领域", en: "Domain" },
  status: { zh: "状态", en: "Status" },
};
export const UNIT_NAMES: Record<UnitClass, LocalText> = {
  vanguard: { zh: "先锋", en: "Vanguard" },
  assault: { zh: "突击", en: "Assault" },
  support: { zh: "支援", en: "Support" },
  skirmisher: { zh: "游击", en: "Skirmisher" },
};
export const RARITY_NAMES: LocalText[] = [
  { zh: "衍生", en: "Derivative" },
  { zh: "大师", en: "Master" },
  { zh: "极佳", en: "Excellent" },
  { zh: "良好", en: "Good" },
  { zh: "普通", en: "Common" },
  { zh: "专属", en: "Special" },
  { zh: "状态", en: "Status token" },
];
export const KEYWORD_NAMES: Record<string, LocalText> = {
  none: { zh: "无", en: "None" },
  charge: { zh: "闪击", en: "Charge" },
  taunt: { zh: "嘲讽", en: "Taunt" },
  battlecry: { zh: "战吼", en: "Battlecry" },
  deathrattle: { zh: "亡语", en: "Deathrattle" },
  windfury: { zh: "风怒", en: "Windfury" },
  ambush: { zh: "伏击", en: "Ambush" },
  impact: { zh: "冲击", en: "Impact" },
  recycle: { zh: "轮战", en: "Recycle" },
  unplayable: { zh: "不可打出", en: "Unplayable" },
  ethereal: { zh: "虚无", en: "Ethereal" },
  qiqiao: { zh: "奇巧", en: "Qiqiao" },
  guard: { zh: "守护", en: "Guard" },
  smokescreen: { zh: "烟幕", en: "Smokescreen" },
  intel: { zh: "情报", en: "Intel" },
};
export const MECHANIC_NAMES: Record<string, LocalText> = {
  directDamage: { zh: "直伤", en: "Direct damage" },
  damageOverTime: { zh: "持续伤害", en: "Damage over time" },
  heal: { zh: "治疗", en: "Heal" },
  armor: { zh: "护甲", en: "Armor" },
  draw: { zh: "抽牌", en: "Draw" },
  discover: { zh: "发现", en: "Discover" },
  summon: { zh: "召唤", en: "Summon" },
  buff: { zh: "增益", en: "Buff" },
  silence: { zh: "沉默", en: "Silence" },
  discard: { zh: "弃牌", en: "Discard" },
  domain: { zh: "领域", en: "Domain" },
  weaponSynergy: { zh: "武器协同", en: "Weapon synergy" },
  manaRamp: { zh: "法力增益", en: "Mana ramp" },
  statusApply: { zh: "状态施加", en: "Status apply" },
  shuffle: { zh: "洗牌", en: "Shuffle" },
  token: { zh: "衍生", en: "Token" },
  mechanics: { zh: "机械族", en: "Mechanics" },
  superBody: { zh: "超体", en: "Super body" },
  intel: { zh: "情报", en: "Intel" },
};
export function local(text: LocalText, language: Language): string {
  return text[language] || text.zh;
}
