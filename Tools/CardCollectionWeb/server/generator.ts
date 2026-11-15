import { KEYWORD_KEYS, MECHANIC_TAGS, UNIT_CLASSES } from "../shared/constants.ts";
import type { CardRecord, GeneratedDeck, GenerationStats, ThemeRecord, UnitClass } from "../shared/types.ts";
import { seededRandom, stableHash } from "./artwork.ts";

interface WeightedCard {
  card: CardRecord;
  weight: number;
}

const CURVE_TARGETS = [
  { min: 0, max: 3, ratio: 0.45 },
  { min: 4, max: 7, ratio: 0.35 },
  { min: 8, max: 12, ratio: 0.2 },
  { min: 13, max: 30, ratio: 0 },
];

function computeWeight(profile: ThemeRecord, card: CardRecord): number {
  let weight = 0;
  for (const [tagBit, tagWeight] of Object.entries(profile.tagWeights)) {
    if ((card.mechanicTags & Number(tagBit)) !== 0) weight += tagWeight;
  }
  for (const [keyword, keywordWeight] of Object.entries(profile.keywordWeights)) {
    if (card.resourceKeywords.includes(Number(keyword))) weight += keywordWeight;
  }
  weight += profile.cardWeightOverrides[card.id] ?? 0;
  return Math.max(1, weight);
}

function curveSegment(cost: number): number {
  const index = CURVE_TARGETS.findIndex((segment) => cost >= segment.min && cost <= segment.max);
  return index < 0 ? 3 : index;
}

export function generateThemeDeck(profile: ThemeRecord, pool: CardRecord[], seed: number): GeneratedDeck {
  const random = seededRandom(seed);
  const target = profile.targetDeckSize;
  const maxDuplicates = Math.max(1, profile.maxDuplicatesPerCard);
  const maxDomains = Math.max(0, profile.maxDomainCards);
  const legalPool = pool.filter((card) => card.typeKey !== "status" && ![0, 5, 6].includes(card.rarity));
  const result: CardRecord[] = [];
  const duplicates = new Map<string, number>();
  let domainCount = 0;
  const add = (card: CardRecord): void => {
    result.push(card);
    duplicates.set(card.id, (duplicates.get(card.id) ?? 0) + 1);
    if (card.typeKey === "domain") domainCount++;
  };
  const available = (card: CardRecord): boolean =>
    (duplicates.get(card.id) ?? 0) < maxDuplicates && (card.typeKey !== "domain" || domainCount < maxDomains);
  for (const coreId of profile.coreCardIds) {
    if (result.length >= target) break;
    const card = legalPool.find((candidate) => candidate.id === coreId);
    if (card && available(card)) add(card);
  }
  let weighted: WeightedCard[] = legalPool
    .filter(available)
    .map((card) => ({ card, weight: computeWeight(profile, card) }));
  while (result.length < target && weighted.length > 0) {
    weighted = weighted.filter(({ card }) => available(card));
    if (!weighted.length) break;
    const segmentCounts = [0, 0, 0, 0];
    for (const card of result)
      segmentCounts[curveSegment(card.cost)] = (segmentCounts[curveSegment(card.cost)] ?? 0) + 1;
    if (result.length > 0) {
      for (const weightedCard of weighted) {
        const segment = curveSegment(weightedCard.card.cost);
        const expected = Math.ceil(target * (CURVE_TARGETS[segment]?.ratio ?? 0));
        if ((segmentCounts[segment] ?? 0) >= expected)
          weightedCard.weight = Math.max(1, Math.floor(weightedCard.weight / 2));
      }
    }
    const nonDomain = result.filter((card) => card.typeKey !== "domain");
    if (nonDomain.length) {
      const minionRatio = nonDomain.filter((card) => card.typeKey === "minion").length / nonDomain.length;
      for (const weightedCard of weighted) {
        if (
          (weightedCard.card.typeKey === "minion" && minionRatio > 0.7) ||
          (weightedCard.card.typeKey === "spell" && minionRatio < 0.4)
        ) {
          weightedCard.weight = Math.max(1, Math.floor(weightedCard.weight / 2));
        }
      }
    }
    const totalWeight = weighted.reduce((total, entry) => total + entry.weight, 0);
    let picked = weighted[weighted.length - 1];
    if (totalWeight <= 0) picked = weighted[Math.floor(random() * weighted.length)];
    else {
      let roll = random() * totalWeight;
      picked =
        weighted.find((entry) => {
          roll -= entry.weight;
          return roll < 0;
        }) ?? picked;
    }
    if (picked) add(picked.card);
  }
  for (const card of legalPool) {
    if (result.length >= target) break;
    if (available(card)) add(card);
  }
  const stats = buildStats(profile, result);
  return { themeId: profile.id, seed, cardIds: result.map((card) => card.id), cards: result, stats };
}

function buildStats(profile: ThemeRecord, cards: CardRecord[]): GenerationStats {
  const manaCurve = [0, 0, 0, 0];
  const unitClasses: Record<UnitClass, number> = { vanguard: 0, assault: 0, support: 0, skirmisher: 0 };
  const tagCounts: Record<string, number> = {};
  for (const card of cards) {
    const segment = curveSegment(card.cost);
    manaCurve[segment] = (manaCurve[segment] ?? 0) + 1;
    if (card.unitClass) unitClasses[card.unitClass]++;
    for (const [, bit] of MECHANIC_TAGS) {
      if ((card.mechanicTags & bit) === 0) continue;
      const key = MECHANIC_TAGS.find(([, candidate]) => candidate === bit)?.[0] ?? String(bit);
      tagCounts[key] = (tagCounts[key] ?? 0) + 1;
    }
    for (const keyword of card.resourceKeywords) {
      const key = `keyword:${KEYWORD_KEYS[keyword] ?? keyword}`;
      tagCounts[key] = (tagCounts[key] ?? 0) + 1;
    }
  }
  return {
    totalCards: cards.length,
    coreCardsIncluded: cards.filter((card) => profile.coreCardIds.includes(card.id)).length,
    minionCount: cards.filter((card) => card.typeKey === "minion").length,
    spellCount: cards.filter((card) => card.typeKey === "spell").length,
    domainCount: cards.filter((card) => card.typeKey === "domain").length,
    manaCurve,
    unitClasses,
    tagCounts,
  };
}

export function seedFrom(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) return Math.abs(Math.floor(value)) % 2147483647;
  if (typeof value === "string" && value) return stableHash(value);
  return Math.floor(Math.random() * 2147483647);
}

export function emptyUnitClassCounts(): Record<UnitClass, number> {
  return Object.fromEntries(UNIT_CLASSES.map((unitClass) => [unitClass, 0])) as Record<UnitClass, number>;
}
