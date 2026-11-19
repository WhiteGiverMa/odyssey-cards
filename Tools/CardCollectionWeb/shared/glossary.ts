import type { CardRecord, LocalText } from "./types.ts";

export type GlossaryCategory = "keyword" | "status" | "mechanic";
export interface GlossaryTerm {
  id: string;
  category: GlossaryCategory;
  name: LocalText;
  aliases: Record<"zh" | "en", string[]>;
  description: LocalText;
}

export const GLOSSARY: GlossaryTerm[] = [
  {
    id: "vulnerable",
    category: "status",
    name: { zh: "易伤", en: "Vulnerable" },
    aliases: { zh: ["易伤"], en: ["Vulnerable"] },
    description: {
      zh: "受到的伤害 ×1.5；总观效应下提升至 ×2.0。按状态自身设定的回合结束时机衰减。",
      en: "Damage taken is multiplied by 1.5, or 2.0 under Total Observation. Stacks decay at the status's configured side-turn end.",
    },
  },
  {
    id: "weak",
    category: "status",
    name: { zh: "虚弱", en: "Weak" },
    aliases: { zh: ["虚弱"], en: ["Weak"] },
    description: {
      zh: "造成的伤害 ×0.75；总观效应下进一步降至 ×0.5。",
      en: "Damage dealt is multiplied by 0.75, reduced to 0.5 under Total Observation.",
    },
  },
  {
    id: "fragile",
    category: "status",
    name: { zh: "脆弱", en: "Fragile" },
    aliases: { zh: ["脆弱"], en: ["Fragile", "Frail"] },
    description: {
      zh: "护甲/格挡获取量 ×0.75；总观效应下进一步降至 ×0.5。",
      en: "Armor and Block gained are multiplied by 0.75, reduced to 0.5 under Total Observation.",
    },
  },
  {
    id: "disabled",
    category: "status",
    name: { zh: "失能", en: "Incapacitated" },
    aliases: { zh: ["失能"], en: ["Incapacitated", "Disabled", "Disable"] },
    description: {
      zh: "失能随从不能移动、主动普通攻击、触发伏击或反击；失能英雄不能使用武器普通攻击，但仍可施放法术。按目标所属方回合结束衰减。",
      en: "An Incapacitated unit cannot move, make a normal attack, Ambush, or retaliate. Heroes cannot attack with weapons but may still cast spells. Decays at the end of the owner's turn.",
    },
  },
  {
    id: "damage-over-time",
    category: "status",
    name: { zh: "持续伤害", en: "Damage over Time" },
    aliases: { zh: ["持续伤害"], en: ["Damage over Time", "Damage Over Time", "DoT"] },
    description: {
      zh: "目标所属方回合开始时受到固定直接伤害（不经过伤害修改管线），随后减少 1 层。理惠的 SVDS-M338 被动可施加。",
      en: "At the start of the owner's turn, takes fixed direct damage outside the damage-modifier pipeline, then loses 1 stack. Applied by Rie's SVDS-M338 passive.",
    },
  },
  {
    id: "smokescreen",
    category: "status",
    name: { zh: "烟幕", en: "Smokescreen" },
    aliases: { zh: ["烟幕", "烟雾"], en: ["Smokescreen", "Smoke Screen"] },
    description: {
      zh: "无法成为任何敌方随从或武器普通攻击的目标，包括远程、支援与游击攻击；法术、AOE 和强制互殴仍可命中。与嘲讽、守护强制互斥。",
      en: "Cannot be targeted by any enemy unit or weapon normal attack, including ranged, Support, and Skirmisher attacks. Spells, AOE, and forced fights still hit. Mutually exclusive with Taunt and Guard.",
    },
  },
  {
    id: "total-observation",
    category: "status",
    name: { zh: "总观效应", en: "Total Observation" },
    aliases: { zh: ["总观效应"], en: ["Total Observation"] },
    description: {
      zh: "强化易伤、虚弱与脆弱的幅度：易伤 ×1.5 → ×2.0，虚弱/脆弱 ×0.75 → ×0.5。",
      en: "Amplifies Vulnerable, Weak, and Fragile: Vulnerable moves from ×1.5 to ×2.0; Weak and Fragile move from ×0.75 to ×0.5.",
    },
  },
  {
    id: "meltdown",
    category: "status",
    name: { zh: "熔毁", en: "Meltdown" },
    aliases: { zh: ["熔毁"], en: ["Meltdown"] },
    description: {
      zh: "目标防御力 -1，最多叠加 2 层。离子手枪命中后施加。",
      en: "The target loses 1 Defense, stacking up to 2 times. Applied when the Ion Pistol hits.",
    },
  },
  {
    id: "weapon-disabled",
    category: "status",
    name: { zh: "武器禁用", en: "Weapon Disabled" },
    aliases: { zh: ["武器禁用", "禁用武器"], en: ["Weapon Disabled", "Disable Weapon"] },
    description: {
      zh: "英雄武器在状态持续期间无法主动攻击或反击；不影响施放法术。",
      en: "The hero's weapon cannot attack or retaliate while this status lasts. Spellcasting is unaffected.",
    },
  },
  {
    id: "attack-zero",
    category: "status",
    name: { zh: "攻击归零", en: "Attack Zero" },
    aliases: { zh: ["攻击归零", "攻击力归零"], en: ["Attack Zero", "Zero Attack"] },
    description: {
      zh: "将随从攻击力设为 0，持续状态指定的回合数；可由离子脉冲施加。",
      en: "Sets a unit's Attack to 0 for the status duration. Ion Pulse can apply this effect.",
    },
  },
  {
    id: "rail-lock",
    category: "status",
    name: { zh: "磁轨锁定", en: "Rail Lock" },
    aliases: { zh: ["磁轨锁定"], en: ["Rail Lock"] },
    description: {
      zh: "磁轨手枪命中后施加，使目标在 1 回合内无法攻击；英雄表现为武器攻击被禁用。",
      en: "Applied by Rail Pistol hits. The target cannot attack for 1 turn; on heroes, weapon attacks are disabled.",
    },
  },
  {
    id: "inescapable-flaw",
    category: "status",
    name: { zh: "难逃之瑕", en: "Inescapable Flaw" },
    aliases: { zh: ["难逃之瑕"], en: ["Inescapable Flaw"] },
    description: {
      zh: "持有者失去一种负面效果时，立即随机获得 1 层另一种负面效果；失去本效果时也可能重新获得它。",
      en: "Whenever the owner loses a negative effect, it immediately gains 1 stack of another random negative effect. Losing this effect may reapply it.",
    },
  },
  {
    id: "solid-barrier",
    category: "status",
    name: { zh: "固璋", en: "Solid Barrier" },
    aliases: { zh: ["固璋"], en: ["Solid Barrier"] },
    description: {
      zh: "张郎被动：单次受到的生命伤害上限为 3，由伤害封顶阶段处理。",
      en: "Zhang Lang's passive: caps Health damage from each individual hit at 3 during the damage-capping stage.",
    },
  },
  {
    id: "unbreakable",
    category: "status",
    name: { zh: "不破", en: "Unbreakable" },
    aliases: { zh: ["不破"], en: ["Unbreakable"] },
    description: {
      zh: "珊胡被动：每回合只能受到 1 次大于 0 的生命伤害；0 点生命伤害不计入次数。",
      en: "Shan Hu's passive: can take positive Health damage only once per turn; zero-damage hits do not consume the count.",
    },
  },
  {
    id: "fatal-crack",
    category: "status",
    name: { zh: "致命裂痕", en: "Fatal Rift" },
    aliases: { zh: ["致命裂痕"], en: ["Fatal Rift", "Fatal Crack"] },
    description: {
      zh: "碎片子弹在低生命目标上施加的处决标记：生命低于 18% 时，受到友方伤害后消灭；Boss 免疫处决。",
      en: "Frag Bullet marks a low-Health target: below 18% Health, taking friendly damage destroys it. Bosses are immune to the execution.",
    },
  },
  {
    id: "charge",
    category: "keyword",
    name: { zh: "闪击", en: "Charge" },
    aliases: { zh: ["闪击"], en: ["Charge"] },
    description: {
      zh: "解除部署当回合的整备冷却，立即解锁所属兵种的行动配额；先锋仍只能移动或攻击，突击可移动并攻击。",
      en: "Removes deployment readiness cooldown and immediately unlocks the unit-class action quota. Vanguard still chooses move or attack; Assault may do both.",
    },
  },
  {
    id: "taunt",
    category: "keyword",
    name: { zh: "嘲讽", en: "Taunt" },
    aliases: { zh: ["嘲讽"], en: ["Taunt"] },
    description: {
      zh: "最高优先级防线：拦截对同阵线友方随从的普通攻击，任意可达嘲讽也能保护己方 HQ。与烟幕互斥。",
      en: "Top-priority defense: intercepts normal attacks against allies in its row; any reachable Taunt also protects its HQ. Mutually exclusive with Smokescreen.",
    },
  },
  {
    id: "guard",
    category: "keyword",
    name: { zh: "守护", en: "Guard" },
    aliases: { zh: ["守护"], en: ["Guard"] },
    description: {
      zh: "保护同线左右相邻的非嘲讽、非守护随从；不保护 HQ，也不与另一守护形成互锁。与烟幕互斥。",
      en: "Protects adjacent non-Taunt, non-Guard allies in the same row. Does not protect HQ or another Guard. Mutually exclusive with Smokescreen.",
    },
  },
  {
    id: "battlecry",
    category: "keyword",
    name: { zh: "战吼", en: "Battlecry" },
    aliases: { zh: ["战吼"], en: ["Battlecry", "Battle cry"] },
    description: {
      zh: "从手牌打出随从时触发；移动、召回或非部署方式进场不视为战吼。",
      en: "Triggers when a minion is played from hand. Movement, recall, and non-deployment entry do not count as Battlecry.",
    },
  },
  {
    id: "deathrattle",
    category: "keyword",
    name: { zh: "亡语", en: "Deathrattle" },
    aliases: { zh: ["亡语"], en: ["Deathrattle", "Death rattle"] },
    description: {
      zh: "随从死亡时触发；移动或召回离场不会触发。",
      en: "Triggers when the minion dies. Moving or being recalled from the board does not trigger it.",
    },
  },
  {
    id: "windfury",
    category: "keyword",
    name: { zh: "风怒", en: "Windfury" },
    aliases: { zh: ["风怒"], en: ["Windfury", "Wind Fury"] },
    description: {
      zh: "单回合主动攻击配额上限提升为 2 次；突击仍保留独立的 1 次移动配额。",
      en: "Raises the active attack quota to 2 per turn. Assault retains its separate single movement action.",
    },
  },
  {
    id: "ambush",
    category: "keyword",
    name: { zh: "伏击", en: "Ambush" },
    aliases: { zh: ["伏击"], en: ["Ambush"] },
    description: {
      zh: "每回合首次被普通攻击时，先于攻击者造成反击并替代普通反击；若击杀攻击者则取消原攻击。支援与免反击武器的主动攻击不触发。",
      en: "On the first normal attack received each turn, retaliates before the attacker and replaces normal retaliation; killing the attacker cancels its attack. Support and no-retaliation weapon attacks bypass it.",
    },
  },
  {
    id: "impact",
    category: "keyword",
    name: { zh: "冲击", en: "Impact" },
    aliases: { zh: ["冲击"], en: ["Impact"] },
    description: {
      zh: "主动普通攻击时抵消所有反击伤害，包括伏击先手伤害；结算后一次性消耗。",
      en: "Negates all retaliation during an active normal attack, including Ambush damage. Consumed after that attack resolves.",
    },
  },
  {
    id: "recycle",
    category: "keyword",
    name: { zh: "轮战", en: "Recycle" },
    aliases: { zh: ["轮战"], en: ["Recycle"] },
    description: {
      zh: "法术打出后，或随从被击败后，回到所属指挥官抽牌堆底部而不是弃牌堆。",
      en: "After a spell is played or a minion is defeated, returns it to the bottom of its commander's draw pile instead of the discard pile.",
    },
  },
  {
    id: "unplayable",
    category: "keyword",
    name: { zh: "不可打出", en: "Unplayable" },
    aliases: { zh: ["不可打出"], en: ["Unplayable"] },
    description: {
      zh: "无法从手牌主动打出，只能通过弃牌、消耗或其他机制移除。",
      en: "Cannot be actively played from hand; it must be discarded, exhausted, or removed by another mechanic.",
    },
  },
  {
    id: "ethereal",
    category: "keyword",
    name: { zh: "虚无", en: "Ethereal" },
    aliases: { zh: ["虚无"], en: ["Ethereal"] },
    description: {
      zh: "回合结束时若仍在手牌中，则自动消耗而不进入弃牌堆。",
      en: "If still in hand at the end of the turn, it is exhausted automatically instead of entering the discard pile.",
    },
  },
  {
    id: "qiqiao",
    category: "keyword",
    name: { zh: "奇巧", en: "Qiqiao" },
    aliases: { zh: ["奇巧"], en: ["Qiqiao"] },
    description: {
      zh: "被弃掉时自动打出；随从召唤至所属方支援线空槽，支援线满 4 格时溢出消失。",
      en: "Automatically plays when discarded. A minion is summoned to an empty friendly Support-row slot and overflows away if all 4 slots are full.",
    },
  },
  {
    id: "intel",
    category: "mechanic",
    name: { zh: "情报", en: "Intel" },
    aliases: { zh: ["情报"], en: ["Intel"] },
    description: {
      zh: "情报 N 从敌方手牌左侧开始揭露最多 N 张尚未公开的暗牌；同名不同实例独立记录，洗回或回手保留公开标记。",
      en: "Intel N reveals up to N unrevealed enemy cards from left to right. Each copy is tracked separately and stays revealed when returned or shuffled.",
    },
  },
  {
    id: "excess-intel",
    category: "mechanic",
    name: { zh: "溢出情报", en: "Excess Intel" },
    aliases: { zh: ["溢出情报"], en: ["Excess Intel", "Intel Overflow"] },
    description: {
      zh: "未实际用于新揭露的情报转化为溢出情报。每点回复己方英雄 1 点生命；若结算时已满血，改为获得 1 点格挡，随后触发溢出事件。",
      en: "Intel not spent on new reveals becomes Excess Intel. Each point heals your hero for 1, or grants 1 Block at full Health, then emits the overflow event.",
    },
  },
  {
    id: "block",
    category: "mechanic",
    name: { zh: "格挡 / 护甲", en: "Block / Armor" },
    aliases: { zh: ["格挡", "护甲"], en: ["Block", "Armor"] },
    description: {
      zh: "伤害管线结算后由护甲/格挡优先吸收，剩余部分才扣除生命；溢出情报在满血时也会转化为格挡。",
      en: "After the damage pipeline resolves, Armor/Block absorbs damage before Health is lost. Excess Intel also converts to Block at full Health.",
    },
  },
  {
    id: "defense",
    category: "mechanic",
    name: { zh: "防御力", en: "Defense" },
    aliases: { zh: ["防御力"], en: ["Defense"] },
    description: {
      zh: "随从的防御属性，与护甲吸收和具体卡牌效果联动；熔毁可降低防御力。",
      en: "A unit's Defense stat, used by armor interactions and card effects; Meltdown can reduce it.",
    },
  },
  {
    id: "heat",
    category: "mechanic",
    name: { zh: "热力", en: "Heat" },
    aliases: { zh: ["热力值", "热力"], en: ["Heat Value", "Heat"] },
    description: {
      zh: "每场战斗共享的全局节奏压力，通过热力伤害修改器接入伤害管线，而不是某张卡牌自身的状态。",
      en: "A battle-wide pressure meter connected to the damage pipeline through the Heat modifier, rather than a status owned by one card.",
    },
  },
  {
    id: "frontline",
    category: "mechanic",
    name: { zh: "前线", en: "Frontline" },
    aliases: { zh: ["中央前线", "前线"], en: ["Frontline", "Front line"] },
    description: {
      zh: "中央共享 5 格阵线，同一时刻只能由一方占领。随从须从支援线独立推进进入；最后一名驻军离场后立即释放控制权。",
      en: "The shared central 5-slot row, occupied by only one faction at a time. Units advance from Support as a separate move; losing the last occupant releases control immediately.",
    },
  },
  {
    id: "support-line",
    category: "mechanic",
    name: { zh: "支援线", en: "Support Row" },
    aliases: {
      zh: ["敌方支援线", "我方支援线", "支援线"],
      en: ["Enemy Support Row", "Friendly Support Row", "Support Row", "Support Line"],
    },
    description: {
      zh: "双方各自拥有 4 格后方阵线。所有部署与召唤只能进入所属方支援线；HQ 不占槽位，但在空间规则中视作自家支援线。",
      en: "Each faction has a 4-slot back row. Every deployment and summon enters its own Support row. HQ occupies no slot but counts as its own Support row for spatial rules.",
    },
  },
  {
    id: "vanguard",
    category: "mechanic",
    name: { zh: "先锋", en: "Vanguard" },
    aliases: { zh: ["先锋"], en: ["Vanguard"] },
    description: {
      zh: "近战兵种，每回合移动或攻击 1 次。支援线只能攻击敌占前线；推进后可攻击敌方支援线与 HQ。",
      en: "Melee class with one move or attack per turn. From Support it can hit the enemy-held Frontline; after advancing it can hit enemy Support and HQ.",
    },
  },
  {
    id: "assault",
    category: "mechanic",
    name: { zh: "突击", en: "Assault" },
    aliases: { zh: ["突击"], en: ["Assault"] },
    description: {
      zh: "近战兵种，每回合可移动 1 次并攻击 1 次（风怒攻击 2 次）。支持支付两段油费的一步突击。",
      en: "Melee class with one move and one attack per turn (two attacks with Windfury). Can perform a one-step advance-and-attack by paying both oil costs.",
    },
  },
  {
    id: "support",
    category: "mechanic",
    name: { zh: "支援", en: "Support" },
    aliases: { zh: ["支援"], en: ["Support"] },
    description: {
      zh: "全图射程兵种，每回合移动或攻击 1 次。主动普通攻击免反击，且不触发目标伏击。",
      en: "Global-range class with one move or attack per turn. Its active normal attacks ignore retaliation and do not trigger Ambush.",
    },
  },
  {
    id: "skirmisher",
    category: "mechanic",
    name: { zh: "游击", en: "Skirmisher" },
    aliases: { zh: ["游击"], en: ["Skirmisher"] },
    description: {
      zh: "全图射程但正常承受反击。位于支援线时免疫近战普攻；同线游击可为普通单位与 HQ 护航，挡刀守护可被优先拆除以避免死锁。",
      en: "Global range with normal retaliation. Immune to melee while in Support; allied Skirmishers screen normal units and HQ, while a Guard covering them can be attacked first to prevent deadlock.",
    },
  },
  {
    id: "film",
    category: "mechanic",
    name: { zh: "贴膜", en: "Film" },
    aliases: { zh: ["贴膜魔法", "贴膜"], en: ["Magic Film", "Film Healing", "Film"] },
    description: {
      zh: "绮梦魔法棒的友方支援攻击转化为治疗；随从治疗可突破原生命上限并同步抬高最大生命。",
      en: "Ayame's Magic Wand converts friendly-support attacks into healing. Minion healing may exceed the original maximum and raises Max Health accordingly.",
    },
  },
  {
    id: "domain",
    category: "mechanic",
    name: { zh: "领域", en: "Domain" },
    aliases: { zh: ["永久领域", "领域"], en: ["Permanent Domain", "Domain"] },
    description: {
      zh: "永久型持续效果，不按回合作自然衰减；Counter 只由指定战斗事件消耗，再次打出同领域会叠加 Counter。",
      en: "A permanent ongoing effect with no automatic turn decay. Counters are consumed only by specified combat events, and replaying the same Domain stacks them.",
    },
  },
];

export const GLOSSARY_BY_ID = new Map(GLOSSARY.map((term) => [term.id, term]));

export type RichTextSegment =
  | { kind: "text"; text: string }
  | { kind: "term"; text: string; term: GlossaryTerm }
  | { kind: "card"; text: string; card: CardRecord };

interface Candidate {
  phrase: string;
  segment: (text: string) => Exclude<RichTextSegment, { kind: "text" }>;
}

function isAsciiPhrase(phrase: string): boolean {
  return /^[\x00-\x7F“”‘’]+$/.test(phrase);
}

function safeAt(text: string, start: number, length: number, ascii: boolean): boolean {
  if (!ascii) return true;
  const before = text[start - 1] ?? "";
  const after = text[start + length] ?? "";
  return !/[A-Za-z0-9_]/.test(before) && !/[A-Za-z0-9_]/.test(after);
}

function candidatesFor(cards: CardRecord[]): Candidate[] {
  const candidates: Candidate[] = [];
  for (const term of GLOSSARY) {
    for (const phrase of new Set([...term.aliases.zh, ...term.aliases.en, term.name.zh, term.name.en])) {
      candidates.push({ phrase, segment: (text) => ({ kind: "term", text, term }) });
    }
  }
  for (const card of cards) {
    for (const name of new Set([card.name.zh, card.name.en])) {
      for (const phrase of [`「${name}」`, `“${name}”`, `"${name}"`, `'${name}'`]) {
        candidates.push({ phrase, segment: (text) => ({ kind: "card", text, card }) });
      }
    }
  }
  return candidates.sort((a, b) => b.phrase.length - a.phrase.length);
}

/** Longest-match tokenizer with ASCII word-boundary protection and quoted card-name links. */
export function tokenizeGameText(text: string, cards: CardRecord[]): RichTextSegment[] {
  if (!text) return [];
  const candidates = candidatesFor(cards);
  const output: RichTextSegment[] = [];
  let plainStart = 0;
  let index = 0;
  const pushPlain = (end: number) => {
    if (end > plainStart) output.push({ kind: "text", text: text.slice(plainStart, end) });
  };
  while (index < text.length) {
    let matched: Candidate | undefined;
    for (const candidate of candidates) {
      const phrase = candidate.phrase;
      const actual = text.slice(index, index + phrase.length);
      if (actual.length !== phrase.length) continue;
      if (isAsciiPhrase(phrase) ? actual.toLowerCase() !== phrase.toLowerCase() : actual !== phrase) continue;
      if (!safeAt(text, index, phrase.length, isAsciiPhrase(phrase))) continue;
      matched = candidate;
      break;
    }
    if (!matched) {
      index++;
      continue;
    }
    pushPlain(index);
    output.push(matched.segment(text.slice(index, index + matched.phrase.length)));
    index += matched.phrase.length;
    plainStart = index;
  }
  pushPlain(text.length);
  return output;
}
