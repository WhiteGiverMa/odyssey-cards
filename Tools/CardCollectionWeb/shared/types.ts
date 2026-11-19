export type Language = "zh" | "en";
export type LocalText = Record<Language, string>;
export type CardTypeKey = "minion" | "spell" | "domain" | "status" | "countermeasure";
export type UnitClass = "vanguard" | "assault" | "support" | "skirmisher";
export type ArtworkStyle = "rune" | "mecha" | "abstract";
export type RawValue = string | number | boolean | null | RawValue[] | { [key: string]: RawValue };
export type RawProperties = Record<string, RawValue>;

export interface CardEffect {
  trigger: "battlecry" | "deathrattle" | "effect";
  type: number;
  value: number;
  secondaryValue: number;
  targetType?: string;
  customEffectName?: string;
  raw: RawProperties;
}

export interface ArtworkSpec {
  seed: number;
  symbolIndex: number;
  style: ArtworkStyle;
  baseColor: string;
  accentColor: string;
  glowColor: string;
  starCount: number;
  rotation: number;
}

export interface TacticalProfile {
  role: LocalText;
  preferredLine: "support" | "frontline" | "flex";
  range: "melee" | "global";
  note: LocalText;
  proposedActionCost?: number;
  proposedKeywords?: string[];
}

export interface CardRecord {
  id: string;
  fileName: string;
  sourcePath: string;
  type: number;
  typeKey: CardTypeKey;
  rarity: number;
  rarityKey: string;
  cost: number;
  actionCost: number;
  attack: number;
  health: number;
  defense: number;
  domainId: string;
  mechanicTags: number;
  mechanicTagKeys: string[];
  keywords: number[];
  keywordKeys: string[];
  resourceKeywords: number[];
  resourceKeywordKeys: string[];
  unitClass: UnitClass | null;
  unitClassSource: "resource" | "prd" | "not-applicable";
  tactical: TacticalProfile | null;
  themes: string[];
  /** 所属卡包（系列）。缺省为 "core"，DIY 卡包为玩家自定义 slug。 */
  packId: string;
  name: LocalText;
  description: LocalText;
  localizationFallback: Record<Language, boolean>;
  requiresTarget: boolean;
  targetFilter: number;
  excludeFilter: number;
  bonusDamageToDefendedTargets: number;
  effects: CardEffect[];
  relatedCardIds: string[];
  buildable: boolean;
  maxCopies: number;
  artwork: ArtworkSpec;
  raw: RawProperties;
}

export interface SkillRecord {
  name: LocalText;
  description: LocalText;
  cost: number;
  cooldown: number;
  maxCharges?: number;
}

export interface WeaponRecord {
  id: string;
  name: LocalText;
  attack: number;
  attackCost: number;
  range: "melee" | "ranged" | "friendlySupport";
  ignoreCounterAttack: boolean;
  owner: LocalText;
  passive: SkillRecord | null;
  active: SkillRecord | null;
  sourcePath: string;
}

export interface HeroRecord {
  id: string;
  name: LocalText;
  romanizedName: string;
  description: LocalText;
  maxHealth: number;
  startingDefense: number;
  weaponId: string;
  power: SkillRecord;
  artStyle: ArtworkStyle;
  sourcePath: string;
}

export interface RelicRecord {
  id: string;
  name: LocalText;
  description: LocalText;
  beneficial: boolean;
  subtle: boolean;
  sourcePath: string;
}

export interface ThemeRecord {
  id: string;
  name: LocalText;
  targetDeckSize: number;
  maxDuplicatesPerCard: number;
  maxDomainCards: number;
  tagWeights: Record<string, number>;
  keywordWeights: Record<string, number>;
  coreCardIds: string[];
  cardWeightOverrides: Record<string, number>;
  sourcePath: string;
}

export interface GenerationStats {
  totalCards: number;
  coreCardsIncluded: number;
  minionCount: number;
  spellCount: number;
  domainCount: number;
  manaCurve: number[];
  unitClasses: Record<UnitClass, number>;
  tagCounts: Record<string, number>;
}

export interface GeneratedDeck {
  themeId: string;
  seed: number;
  cardIds: string[];
  cards: CardRecord[];
  stats: GenerationStats;
}

export interface CardsResponse {
  cards: CardRecord[];
  total: number;
  generatedAt: string;
  repository: string;
}

export interface PackAssignResponse {
  cards: CardRecord[];
  total: number;
  generatedAt: string;
  updated: string[];
  missing: string[];
  packId: string;
}

export interface UnitClassRule {
  id: UnitClass;
  name: LocalText;
  actions: LocalText;
  range: LocalText;
  trait: LocalText;
}

export interface KeywordRule {
  id: string;
  name: LocalText;
  description: LocalText;
}

export interface ResourceValidationReport {
  ok: boolean;
  cards: number;
  themes: number;
  errors: string[];
  warnings: string[];
  checkedAt: string;
}

export interface RulesResponse {
  source: string;
  status: string;
  board: { rows: string[]; capacities: number[]; deployment: LocalText };
  unitClasses: UnitClassRule[];
  keywords: KeywordRule[];
  priority: LocalText[];
  intel: { reveal: LocalText; overflow: LocalText; conversion: LocalText };
}
