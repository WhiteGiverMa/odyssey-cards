import { readdir, readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { KEYWORD_KEYS, MECHANIC_TAGS, RARITY_KEYS, TYPE_KEYS } from "../shared/constants.ts";
import { resolveDefaultPackId } from "../shared/packs.ts";
import type {
  CardEffect,
  CardRecord,
  HeroRecord,
  Language,
  LocalText,
  RelicRecord,
  SkillRecord,
  ThemeRecord,
  UnitClass,
  WeaponRecord,
} from "../shared/types.ts";
import { resolveArtwork } from "./artwork.ts";
import { loadLocalization, localize, translate, type TranslationTable } from "./localization.ts";
import { nonUnitProfile, PRD_UNIT_MAPPINGS, profileFor } from "./metadata.ts";
import { asNumber, asNumberArray, asNumberMap, asString, asStringArray, parseTres, type TresDocument } from "./tres.ts";

const HERE = path.dirname(fileURLToPath(import.meta.url));
function findRepositoryRoot(): string {
  const explicitRoot = process.env.ODYSSEY_REPOSITORY_ROOT;
  if (explicitRoot) {
    const resolved = path.resolve(explicitRoot);
    if (existsSync(path.join(resolved, "Resources", "Cards")) && existsSync(path.join(resolved, "OdysseyCards.csproj")))
      return resolved;
    throw new Error(`Invalid ODYSSEY_REPOSITORY_ROOT: ${resolved}`);
  }
  let candidate = HERE;
  for (let depth = 0; depth < 8; depth++) {
    if (
      existsSync(path.join(candidate, "Resources", "Cards")) &&
      existsSync(path.join(candidate, "OdysseyCards.csproj"))
    )
      return candidate;
    const parent = path.dirname(candidate);
    if (parent === candidate) break;
    candidate = parent;
  }
  throw new Error(`Unable to locate OdysseyCards repository from ${HERE}`);
}
export const REPOSITORY_ROOT = findRepositoryRoot();
function findApplicationRoot(): string {
  let candidate = HERE;
  for (let depth = 0; depth < 8; depth++) {
    if (existsSync(path.join(candidate, "package.json")) && existsSync(path.join(candidate, "index.html")))
      return candidate;
    const parent = path.dirname(candidate);
    if (parent === candidate) break;
    candidate = parent;
  }
  throw new Error(`Unable to locate CardCollectionWeb application from ${HERE}`);
}
export const APPLICATION_ROOT = findApplicationRoot();
const CARD_DIRECTORY = path.join(REPOSITORY_ROOT, "Resources", "Cards");
const THEME_DIRECTORY = path.join(REPOSITORY_ROOT, "Resources", "Themes");
const VIRTUAL_INTEL = new Set(["minion_detective_squad", "minion_lianshu_scout", "spell_expose"]);
const VIRTUAL_SMOKE = new Set(["minion_rayan_ge", "spell_smoke_dodge", "spell_smoke_restore"]);
/** 卡牌关联补充：资源 TargetType 未覆盖、或需要在规则层额外标注的引用（0.2.3 术语与效果调整后同步）。 */
const EFFECT_REFERENCES: Record<string, string[]> = {
  domain_infinite_fire: ["spell_strike"],
  spell_longtermism: ["spell_ignite"],
  spell_blade_crisis: ["minion_what_the_dog_doing"],
  spell_perfect_block: ["spell_ukemi"],
  minion_hover_gunship_drone: ["spell_strike"],
  minion_t90m_late: ["domain_explosive_reactive_armor"],
  domain_flying_away: ["spell_ukemi"],
};

interface LoadedCardResources {
  cards: CardRecord[];
  documents: Map<string, TresDocument>;
}

export interface GameCatalog {
  cards: CardRecord[];
  themes: ThemeRecord[];
  heroes: HeroRecord[];
  weapons: WeaponRecord[];
  relics: RelicRecord[];
  generatedAt: string;
}

function numberMapKeys(value: number, table: Array<[string, number]>): string[] {
  return table.filter(([, bit]) => (value & bit) === bit).map(([key]) => key);
}

function resourceUnitClass(value: unknown): UnitClass | null {
  if (typeof value === "number")
    return (["vanguard", "assault", "support", "skirmisher"] as UnitClass[])[value] ?? null;
  if (typeof value === "string") {
    const normalized = value.toLowerCase();
    return (
      (["vanguard", "assault", "support", "skirmisher"] as UnitClass[]).find((entry) => entry === normalized) ?? null
    );
  }
  return null;
}

function collectEffects(document: TresDocument): CardEffect[] {
  const output: CardEffect[] = [];
  const groups: Array<["battlecry" | "deathrattle" | "effect", string]> = [
    ["battlecry", "BattlecryEffects"],
    ["deathrattle", "DeathrattleEffects"],
    ["effect", "Effects"],
  ];
  for (const [trigger, key] of groups) {
    const references = document.properties[key];
    if (!Array.isArray(references)) continue;
    for (const reference of references) {
      if (typeof reference !== "string") continue;
      const raw = document.subResources.get(reference);
      if (!raw) continue;
      output.push({
        trigger,
        type: asNumber(raw.EffectType),
        value: asNumber(raw.Value),
        secondaryValue: asNumber(raw.SecondaryValue),
        targetType: asString(raw.TargetType) || undefined,
        customEffectName: asString(raw.CustomEffectName) || undefined,
        raw,
      });
    }
  }
  return output;
}

async function loadCards(tables: Record<Language, TranslationTable>): Promise<LoadedCardResources> {
  const files = (await readdir(CARD_DIRECTORY))
    .filter((file) => file.endsWith(".tres"))
    .sort((a, b) => a.localeCompare(b));
  const loaded = await Promise.all(
    files.map(async (fileName) => ({
      fileName,
      document: parseTres(await readFile(path.join(CARD_DIRECTORY, fileName), "utf8")),
    })),
  );
  const documents = new Map(loaded.map((entry) => [entry.fileName, entry.document]));
  const pathToId = new Map<string, string>();
  for (const { fileName, document } of loaded) {
    pathToId.set(`res://Resources/Cards/${fileName}`, asString(document.properties.Id));
  }
  const themeByCard = new Map<string, string[]>();
  const themeFiles = (await readdir(THEME_DIRECTORY)).filter((file) => file.endsWith(".tres"));
  for (const themeFile of themeFiles) {
    const theme = parseTres(await readFile(path.join(THEME_DIRECTORY, themeFile), "utf8"));
    const heroId = asString(theme.properties.HeroId);
    for (const cardId of asStringArray(theme.properties.CoreCardIds)) {
      themeByCard.set(cardId, [...(themeByCard.get(cardId) ?? []), heroId]);
    }
  }

  const cards = loaded.map(({ fileName, document }): CardRecord => {
    const raw = document.properties;
    const id = asString(raw.Id);
    if (!id) throw new Error(`Card resource ${fileName} has no Id`);
    const type = asNumber(raw.Type);
    const typeKey = TYPE_KEYS[type] ?? "minion";
    const rarity = asNumber(raw.Rarity, 4);
    const mechanicTags = asNumber(raw.MechanicTags);
    const resourceKeywords = asNumberArray(raw.Keywords);
    const keywords = new Set(resourceKeywords);
    const mapping = PRD_UNIT_MAPPINGS[id];
    if (mapping?.guard) {
      keywords.delete(2);
      keywords.add(12);
    }
    if (VIRTUAL_INTEL.has(id)) keywords.add(14);
    if (VIRTUAL_SMOKE.has(id)) keywords.add(13);
    const mechanicTagKeys = numberMapKeys(mechanicTags, MECHANIC_TAGS);
    if (VIRTUAL_INTEL.has(id) && !mechanicTagKeys.includes("intel")) mechanicTagKeys.push("intel");
    const cost = asNumber(raw.Cost, 1);
    const actionCost = asNumber(raw.ActionCost);
    const attack = asNumber(raw.Attack, 1);
    const health = asNumber(raw.Health, 1);
    const defense = asNumber(raw.Defense);
    const domainId = asString(raw.DomainId);
    const themes = themeByCard.get(id) ?? [];
    const parameters = { cost, actionCost, attack, health, defense, value: 1 };
    const name = localize(tables, `cards.${id}.name`, asString(raw.CardName, id), parameters);
    const description = localize(tables, `cards.${id}.description`, asString(raw.Description), parameters);
    const resourceUnit = resourceUnitClass(raw.UnitClass);
    const unitClass = typeKey === "minion" ? (resourceUnit ?? mapping?.unitClass ?? null) : null;
    const effects = collectEffects(document);
    const related = new Set(EFFECT_REFERENCES[id] ?? []);
    for (const effect of effects) {
      if (!effect.targetType) continue;
      const targetId = pathToId.get(effect.targetType) ?? effect.targetType;
      if (documentsHasId(loaded, targetId)) related.add(targetId);
    }
    return {
      id,
      fileName,
      sourcePath: `res://Resources/Cards/${fileName}`,
      type,
      typeKey,
      rarity,
      rarityKey: RARITY_KEYS[rarity] ?? "common",
      cost,
      actionCost,
      attack,
      health,
      defense,
      domainId,
      mechanicTags,
      mechanicTagKeys,
      keywords: [...keywords].sort((a, b) => a - b),
      keywordKeys: [...keywords].sort((a, b) => a - b).map((keyword) => KEYWORD_KEYS[keyword] ?? `unknown${keyword}`),
      resourceKeywords,
      resourceKeywordKeys: resourceKeywords.map((keyword) => KEYWORD_KEYS[keyword] ?? `unknown${keyword}`),
      unitClass,
      unitClassSource: unitClass ? (resourceUnit ? "resource" : "prd") : "not-applicable",
      tactical: profileFor(id) ?? nonUnitProfile(id, typeKey, mechanicTags, domainId),
      themes,
      // 资源尚未写入 PackId 时，按显式卡单推断（卡包与机制标签正交）。
      packId: asString(raw.PackId) || resolveDefaultPackId(id),
      name: name.text,
      description: description.text,
      localizationFallback: name.fallback,
      requiresTarget: raw.RequiresTarget === true,
      targetFilter: asNumber(raw.TargetFilter),
      excludeFilter: asNumber(raw.ExcludeFilter),
      bonusDamageToDefendedTargets: asNumber(raw.BonusDamageToDefendedTargets),
      effects,
      relatedCardIds: [...related],
      buildable: typeKey !== "status" && ![0, 5, 6].includes(rarity),
      maxCopies: rarity >= 1 && rarity <= 4 ? rarity : 0,
      artwork: resolveArtwork(id, type, rarity, mechanicTags, domainId, themes[0] ?? null),
      raw,
    };
  });

  const cardIds = new Set(cards.map((card) => card.id));
  for (const card of cards) {
    const related = new Set(card.relatedCardIds);
    for (const other of cards) {
      if (other.relatedCardIds.includes(card.id)) related.add(other.id);
    }
    card.relatedCardIds = [...related].filter((id) => cardIds.has(id));
  }
  return { cards, documents };
}

function documentsHasId(loaded: Array<{ document: TresDocument }>, id: string): boolean {
  return loaded.some(({ document }) => document.properties.Id === id);
}

async function loadThemes(tables: Record<Language, TranslationTable>): Promise<ThemeRecord[]> {
  const files = (await readdir(THEME_DIRECTORY)).filter((file) => file.endsWith(".tres")).sort();
  const englishNames: Record<string, string> = {
    ayame: "Ayame · Guardian Sustain",
    rie: "Rie · Direct Fire",
    sokou: "Sokou · Deck Control",
  };
  return Promise.all(
    files.map(async (fileName): Promise<ThemeRecord> => {
      const sourcePath = `res://Resources/Themes/${fileName}`;
      const { properties } = parseTres(await readFile(path.join(THEME_DIRECTORY, fileName), "utf8"));
      const id = asString(properties.HeroId);
      const fallbackName = asString(properties.ThemeName, id);
      return {
        id,
        name: { zh: fallbackName, en: englishNames[id] ?? fallbackName },
        targetDeckSize: asNumber(properties.TargetDeckSize, 20),
        maxDuplicatesPerCard: asNumber(properties.MaxDuplicatesPerCard, 2),
        maxDomainCards: asNumber(properties.MaxDomainCards, 3),
        tagWeights: asNumberMap(properties.TagWeights),
        keywordWeights: asNumberMap(properties.KeywordWeights),
        coreCardIds: asStringArray(properties.CoreCardIds),
        cardWeightOverrides: asNumberMap(properties.CardWeightOverrides),
        sourcePath,
      };
    }),
  );
}

function classBody(source: string, className: string): string {
  const match = new RegExp(`public class ${className}\\s*:`).exec(source);
  if (!match || match.index === undefined) return "";
  const start = match.index;
  const next = source.indexOf("\npublic class ", start + match[0].length);
  return source.slice(start, next < 0 ? undefined : next);
}

function propertyString(source: string, property: string, fallback = ""): string {
  return new RegExp(`public (?:override )?string ${property}\\s*=>\\s*"([^"]*)"`).exec(source)?.[1] ?? fallback;
}

function propertyNumber(source: string, property: string, fallback = 0): number {
  const value = new RegExp(`public (?:override )?int ${property}\\s*=>\\s*(-?\\d+)`).exec(source)?.[1];
  return value === undefined ? fallback : Number(value);
}

function skillFromSource(
  source: string,
  className: string,
  tables: Record<Language, TranslationTable>,
  prefix: "weapon.skill" | "weapon.passive" | "hero_power",
  fallbackKey: string,
): SkillRecord | null {
  const body = classBody(source, className);
  if (!body) return null;
  const nameFallback = propertyString(body, "Name", fallbackKey);
  const descriptionFallback = propertyString(body, "Description");
  const nameKey = propertyString(body, "NameKey", `${prefix}.${fallbackKey}.name`);
  const descKey = propertyString(body, "DescKey", `${prefix}.${fallbackKey}.desc`);
  return {
    name: translate(tables, nameKey, nameFallback),
    description: translate(tables, descKey, descriptionFallback),
    cost: propertyNumber(body, "Cost"),
    cooldown: propertyNumber(body, "Cooldown"),
    maxCharges: propertyNumber(body, "MaxCharges", 0) || undefined,
  };
}

function weaponsFromSource(source: string, tables: Record<Language, TranslationTable>): WeaponRecord[] {
  const definitions = [
    {
      id: "ion_pistol",
      className: "IonPistol",
      passive: "MeltdownPassive",
      active: "IonPulse",
      range: "melee" as const,
      ignoreCounterAttack: false,
      owner: { zh: "红裤衩", en: "RedPantsu" },
    },
    {
      id: "rolling_log",
      className: "RollingLog",
      passive: "",
      active: "",
      range: "melee" as const,
      ignoreCounterAttack: false,
      owner: { zh: "敌方通用", en: "Enemy default" },
    },
    {
      id: "light_crucible_clamp",
      className: "LightCrucibleClamp",
      passive: "",
      active: "",
      range: "melee" as const,
      ignoreCounterAttack: false,
      owner: { zh: "劫蛋者", en: "Egg Raider" },
    },
    {
      id: "svds_m338",
      className: "SvdsM338",
      passive: "SvdsM338Passive",
      active: "SuppressiveBarrage",
      range: "ranged" as const,
      ignoreCounterAttack: true,
      owner: { zh: "理惠", en: "Rie" },
    },
    {
      id: "ray_pistol",
      className: "RayPistol",
      passive: "RememberPassive",
      active: "BlindShot",
      range: "ranged" as const,
      ignoreCounterAttack: false,
      owner: { zh: "溯光", en: "Sokou" },
    },
    {
      id: "magic_wand",
      className: "MagicWand",
      passive: "MagicFilmPassive",
      active: "StarlightCleanse",
      range: "friendlySupport" as const,
      ignoreCounterAttack: true,
      owner: { zh: "绮梦", en: "Ayame" },
    },
    {
      id: "rail_pistol",
      className: "RailPistol",
      passive: "RailPistolPassive",
      active: "",
      range: "ranged" as const,
      ignoreCounterAttack: false,
      owner: { zh: "宇宙员", en: "Cosmonaut" },
    },
  ];
  return definitions.map((definition) => {
    const body = classBody(source, definition.className);
    const base = /base\s*\(\s*name:\s*"([^"]+)"[\s\S]*?attack:\s*(\d+)[\s\S]*?attackCost:\s*(\d+)/.exec(body);
    return {
      id: definition.id,
      name: translate(tables, `weapon.${definition.id}.name`, base?.[1] ?? definition.id),
      attack: Number(base?.[2] ?? 0),
      attackCost: Number(base?.[3] ?? 0),
      range: definition.range,
      ignoreCounterAttack: definition.ignoreCounterAttack,
      owner: definition.owner,
      passive: definition.passive
        ? skillFromSource(
            source,
            definition.passive,
            tables,
            "weapon.passive",
            definition.id === "rail_pistol" ? "rail_lock" : definition.id,
          )
        : null,
      active: definition.active
        ? skillFromSource(source, definition.active, tables, "weapon.skill", definition.id)
        : null,
      sourcePath:
        definition.id === "rail_pistol"
          ? "res://Scripts/Card/RailPistolPassive.cs"
          : "res://Scripts/Card/WeaponSkill.cs",
    };
  });
}

function heroesFromSource(
  source: string,
  tables: Record<Language, TranslationTable>,
  weaponSource: string,
): HeroRecord[] {
  const definitions = [
    {
      id: "redpantsu",
      romanizedName: "RedPantsu",
      weaponId: "ion_pistol",
      powerClass: "IronWillHeroPower",
      powerKey: "iron_will",
      maxHealth: 30,
    },
    {
      id: "ayame",
      romanizedName: "Ayame",
      weaponId: "magic_wand",
      powerClass: "AyameStarlightSupplyHeroPower",
      powerKey: "ayame_starlight_supply",
      maxHealth: 30,
    },
    {
      id: "rie",
      romanizedName: "Rie",
      weaponId: "svds_m338",
      powerClass: "RieSuppressingFireHeroPower",
      powerKey: "rie_suppressing_fire",
      maxHealth: 25,
    },
    {
      id: "sokou",
      romanizedName: "Sokou",
      weaponId: "ray_pistol",
      powerClass: "SokouRestructureHeroPower",
      powerKey: "sokou_restructure",
      maxHealth: 25,
    },
  ];
  return definitions.map((definition) => {
    const profile =
      definition.id === "redpantsu"
        ? source.slice(0, source.indexOf("public static readonly"))
        : source.slice(source.indexOf(`Id = "${definition.id}"`) - 100);
    const descriptionFallback = /DefaultDescription\s*=\s*"([^"]*)"/.exec(profile)?.[1] ?? "";
    const power = skillFromSource(weaponSource, definition.powerClass, tables, "hero_power", definition.powerKey);
    if (!power) throw new Error(`Hero power source not found: ${definition.powerClass}`);
    return {
      id: definition.id,
      name: translate(tables, `hero.${definition.id}.name`, definition.romanizedName),
      romanizedName: definition.romanizedName,
      description: translate(tables, `hero.${definition.id}.desc`, descriptionFallback),
      maxHealth: definition.maxHealth,
      startingDefense: 0,
      weaponId: definition.weaponId,
      power,
      artStyle: definition.id === "ayame" || definition.id === "redpantsu" ? "rune" : "mecha",
      sourcePath: "res://Scripts/Core/HeroProfile.cs",
    };
  });
}

async function loadRelics(tables: Record<Language, TranslationTable>): Promise<RelicRecord[]> {
  const directory = path.join(REPOSITORY_ROOT, "Scripts", "Relic");
  const files = (await readdir(directory))
    .filter((file) => /Relic\.cs$/.test(file) && !["AbstractRelic.cs", "RelicManager.cs"].includes(file))
    .sort();
  return Promise.all(
    files.map(async (fileName) => {
      const source = await readFile(path.join(directory, fileName), "utf8");
      const id = propertyString(source, "Id");
      const fallbackName = propertyString(source, "Name", id);
      const fallbackDescription = propertyString(source, "Description");
      return {
        id,
        name: translate(tables, `relic.${id}.name`, fallbackName),
        description: translate(tables, `relic.${id}.desc`, fallbackDescription),
        beneficial: !/public override bool IsBeneficial => false/.test(source),
        subtle: /public override bool IsSubtle => true/.test(source),
        sourcePath: `res://Scripts/Relic/${fileName}`,
      };
    }),
  );
}

export async function loadCatalog(): Promise<GameCatalog> {
  const [zh, en] = await Promise.all([
    loadLocalization(path.join(REPOSITORY_ROOT, "Resources", "Localization", "zh.yaml")),
    loadLocalization(path.join(REPOSITORY_ROOT, "Resources", "Localization", "en.yaml")),
  ]);
  const tables = { zh, en };
  const [{ cards }, themes, weaponSource, powerSources, heroSource] = await Promise.all([
    loadCards(tables),
    loadThemes(tables),
    readFile(path.join(REPOSITORY_ROOT, "Scripts", "Card", "WeaponSkill.cs"), "utf8"),
    Promise.all(
      [
        "IronWillHeroPower",
        "AyameStarlightSupplyHeroPower",
        "RieSuppressingFireHeroPower",
        "SokouRestructureHeroPower",
      ].map((name) => readFile(path.join(REPOSITORY_ROOT, "Scripts", "Card", "HeroPowers", `${name}.cs`), "utf8")),
    ),
    readFile(path.join(REPOSITORY_ROOT, "Scripts", "Core", "HeroProfile.cs"), "utf8"),
  ]);
  const railSource = await readFile(path.join(REPOSITORY_ROOT, "Scripts", "Card", "RailPistolPassive.cs"), "utf8");
  const combinedWeaponSource = `${weaponSource}\n${railSource}`;
  const [relics] = await Promise.all([loadRelics(tables)]);
  return {
    cards,
    themes,
    heroes: heroesFromSource(heroSource, tables, powerSources.join("\n")),
    weapons: weaponsFromSource(combinedWeaponSource, tables),
    relics,
    generatedAt: new Date().toISOString(),
  };
}
