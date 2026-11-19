import { existsSync, readFileSync, readdirSync } from "node:fs";
import { readFile, rename, rm, writeFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import path from "node:path";
import { KEYWORD_KEYS, MECHANIC_TAGS } from "../shared/constants.ts";
import { normalizePackId, resolveDefaultPackId } from "../shared/packs.ts";
import type { RawValue, ResourceValidationReport } from "../shared/types.ts";
import {
  rewriteCardPack,
  rewriteCardTags,
  rewriteThemeProfile,
  type CardTagWrite,
  type ThemeProfileWrite,
} from "./tres-writer.ts";
import { asNumber, asNumberArray, asNumberMap, asString, asStringArray, parseTres } from "./tres.ts";

export type ValidationReport = ResourceValidationReport;

export class ResourceInputError extends Error {
  public readonly status = 400;
}

const MECHANIC_BITS = new Set(MECHANIC_TAGS.map(([, bit]) => bit));
const VALID_MECHANIC_MASK = MECHANIC_TAGS.reduce((mask, [, bit]) => mask | bit, 0);
const VALID_KEYWORDS = new Set(Array.from({ length: 12 }, (_, index) => index + 1));

function object(value: unknown, name: string): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new ResourceInputError(`${name} must be an object.`);
  return value as Record<string, unknown>;
}

function assertKeys(input: Record<string, unknown>, allowed: string[]): void {
  const permitted = new Set(allowed);
  const rejected = Object.keys(input).filter((key) => !permitted.has(key));
  if (rejected.length) throw new ResourceInputError(`Unsupported or read-only fields: ${rejected.join(", ")}`);
}

function numberMap(value: unknown, key: "tagWeights" | "keywordWeights"): Record<string, number> {
  const input = object(value, key);
  const validKeys = key === "tagWeights" ? MECHANIC_BITS : VALID_KEYWORDS;
  const output: Record<string, number> = {};
  for (const [entry, weight] of Object.entries(input)) {
    const numericKey = Number(entry);
    if (!validKeys.has(numericKey)) throw new ResourceInputError(`${key} contains unknown key ${entry}.`);
    if (typeof weight !== "number" || !Number.isSafeInteger(weight) || Math.abs(weight) > 100)
      throw new ResourceInputError(`${key}.${entry} must be an integer between -100 and 100.`);
    output[String(numericKey)] = weight;
  }
  return output;
}

export function normalizeCardWrite(value: unknown): CardTagWrite {
  const input = object(value, "Card tag payload");
  assertKeys(input, ["mechanicTags", "keywords", "unitClass", "packId"]);
  if (typeof input.mechanicTags !== "number" || !Number.isSafeInteger(input.mechanicTags) || input.mechanicTags < 0)
    throw new ResourceInputError("mechanicTags must be a non-negative integer.");
  if ((input.mechanicTags & ~VALID_MECHANIC_MASK) !== 0)
    throw new ResourceInputError("mechanicTags contains unknown bits.");
  if (
    !Array.isArray(input.keywords) ||
    input.keywords.some((keyword) => typeof keyword !== "number" || !VALID_KEYWORDS.has(keyword))
  )
    throw new ResourceInputError("keywords must only contain Keyword values 1 through 12.");
  if (new Set(input.keywords).size !== input.keywords.length)
    throw new ResourceInputError("keywords must not contain duplicates.");
  if (
    input.unitClass !== undefined &&
    input.unitClass !== null &&
    (typeof input.unitClass !== "number" ||
      !Number.isInteger(input.unitClass) ||
      input.unitClass < 0 ||
      input.unitClass > 3)
  )
    throw new ResourceInputError("unitClass must be null or an integer from 0 to 3.");
  const packId = input.packId === undefined ? undefined : normalizePackId(input.packId);
  if (input.packId !== undefined && packId === null)
    throw new ResourceInputError("packId must be a slug of letters, digits, '-' or '_' (max 48 chars).");
  return {
    mechanicTags: input.mechanicTags,
    keywords: [...input.keywords] as number[],
    ...(input.unitClass === undefined ? {} : { unitClass: input.unitClass as number | null }),
    ...(packId !== null && packId !== undefined ? { packId } : {}),
  };
}

export function normalizeThemeWrite(value: unknown): ThemeProfileWrite {
  const input = object(value, "Theme payload");
  assertKeys(input, ["tagWeights", "keywordWeights", "coreCardIds"]);
  if (
    !Array.isArray(input.coreCardIds) ||
    input.coreCardIds.some((id) => typeof id !== "string" || !/^[a-z0-9_]+$/i.test(id))
  )
    throw new ResourceInputError("coreCardIds must contain valid card IDs.");
  if (new Set(input.coreCardIds).size !== input.coreCardIds.length)
    throw new ResourceInputError("coreCardIds must not contain duplicates.");
  return {
    tagWeights: numberMap(input.tagWeights, "tagWeights"),
    ...(input.keywordWeights === undefined
      ? {}
      : { keywordWeights: numberMap(input.keywordWeights, "keywordWeights") }),
    coreCardIds: [...input.coreCardIds] as string[],
  };
}

function rawNumberMap(value: RawValue | undefined): Record<string, number> | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  if (Object.values(value).some((entry) => typeof entry !== "number")) return null;
  return value as Record<string, number>;
}

/** Shared use-case layer for the web API and read-only CLI. */
export class ResourceTagService {
  private readonly cardsDirectory: string;
  private readonly themesDirectory: string;

  public constructor(public readonly repositoryRoot: string) {
    if (!existsSync(path.join(repositoryRoot, "project.godot")))
      throw new Error(`Not an OdysseyCards repository: ${repositoryRoot}`);
    this.cardsDirectory = path.join(repositoryRoot, "Resources", "Cards");
    this.themesDirectory = path.join(repositoryRoot, "Resources", "Themes");
  }

  private files(directory: string): string[] {
    if (!existsSync(directory)) return [];
    // Synchronous directory entries keep validation and CLI listing simple; saving remains async and atomic.
    return requireReaddir(directory)
      .filter((file) => file.endsWith(".tres"))
      .sort();
  }

  private cardFiles(): string[] {
    return this.files(this.cardsDirectory);
  }

  private themeFiles(): string[] {
    return this.files(this.themesDirectory);
  }

  /** 按 Id（大小写不敏感）定位卡资源文件名。 */
  private cardFileForId(id: string): string | undefined {
    return this.cardFiles().find((file) => {
      const document = parseTres(readText(path.join(this.cardsDirectory, file)));
      return asString(document.properties.Id).toLowerCase() === id.toLowerCase();
    });
  }

  public listCards(): Array<Record<string, unknown>> {
    return this.cardFiles().map((fileName) => {
      const document = parseTres(readText(path.join(this.cardsDirectory, fileName)));
      const type = asNumber(document.properties.Type);
      const mechanicTags = asNumber(document.properties.MechanicTags);
      const mechanicTagKeys = MECHANIC_TAGS.filter(([, bit]) => (mechanicTags & bit) === bit).map(([key]) => key);
      const id = asString(document.properties.Id);
      return {
        id,
        name: asString(document.properties.CardName),
        fileName,
        type,
        mechanicTags,
        keywords: asNumberArray(document.properties.Keywords),
        unitClass: type === 0 ? asNumber(document.properties.UnitClass) : null,
        packId: asString(document.properties.PackId) || resolveDefaultPackId(id, mechanicTagKeys),
      };
    });
  }

  public listThemes(): Array<Record<string, unknown>> {
    return this.themeFiles().map((fileName) => {
      const { properties } = parseTres(readText(path.join(this.themesDirectory, fileName)));
      return {
        id: asString(properties.HeroId),
        name: asString(properties.ThemeName),
        fileName,
        tagWeights: asNumberMap(properties.TagWeights),
        keywordWeights: asNumberMap(properties.KeywordWeights),
        coreCardIds: asStringArray(properties.CoreCardIds),
      };
    });
  }

  public dump(id: string): Record<string, unknown> | null {
    if (id.startsWith("theme:")) {
      const heroId = id.slice(6);
      const fileName = this.themeFiles().find((file) => {
        const document = parseTres(readText(path.join(this.themesDirectory, file)));
        return asString(document.properties.HeroId).toLowerCase() === heroId.toLowerCase();
      });
      if (!fileName) return null;
      const document = parseTres(readText(path.join(this.themesDirectory, fileName)));
      return { fileName, ...document.properties };
    }
    const fileName = this.cardFileForId(id);
    if (!fileName) return null;
    const document = parseTres(readText(path.join(this.cardsDirectory, fileName)));
    return { fileName, ...document.properties };
  }

  public async saveCard(id: string, value: unknown): Promise<void> {
    const write = normalizeCardWrite(value);
    const fileName = this.cardFileForId(id);
    if (!fileName) throw new ResourceNotFoundError(`Card '${id}' does not exist.`);
    const filePath = path.join(this.cardsDirectory, fileName);
    const source = await readFile(filePath, "utf8");
    const document = parseTres(source);
    if (asNumber(document.properties.Type) !== 0 && write.unitClass !== undefined && write.unitClass !== null)
      throw new ResourceInputError("UnitClass is only editable on minions.");
    const output = rewriteCardTags(source, write);
    await atomicWrite(filePath, output);
  }

  /**
   * 批量归类：只把选中卡牌的 PackId 写回 .tres，其他字段零改动。
   * 返回值区分成功写回与找不到的卡牌，便于 API 给出明确提示。
   */
  public async assignPack(
    cardIds: unknown,
    packId: unknown,
  ): Promise<{ updated: string[]; missing: string[]; packId: string }> {
    const normalized = normalizePackId(packId);
    if (normalized === null)
      throw new ResourceInputError("packId must be a slug of letters, digits, '-' or '_' (max 48 chars).");
    if (!Array.isArray(cardIds) || cardIds.some((id) => typeof id !== "string" || !id.trim()))
      throw new ResourceInputError("cardIds must be an array of non-empty card IDs.");
    const uniqueIds = [...new Set((cardIds as string[]).map((id) => id.trim()))];
    if (!uniqueIds.length) throw new ResourceInputError("cardIds must contain at least one card.");

    const updated: string[] = [];
    const missing: string[] = [];
    for (const id of uniqueIds) {
      const fileName = this.cardFileForId(id);
      if (!fileName) {
        missing.push(id);
        continue;
      }
      const filePath = path.join(this.cardsDirectory, fileName);
      const source = await readFile(filePath, "utf8");
      await atomicWrite(filePath, rewriteCardPack(source, normalized));
      updated.push(id);
    }
    return { updated, missing, packId: normalized };
  }

  public async saveTheme(id: string, value: unknown): Promise<void> {
    const write = normalizeThemeWrite(value);
    const fileName = this.themeFiles().find((file) => {
      const document = parseTres(readText(path.join(this.themesDirectory, file)));
      return asString(document.properties.HeroId).toLowerCase() === id.toLowerCase();
    });
    if (!fileName) throw new ResourceNotFoundError(`Theme '${id}' does not exist.`);
    const filePath = path.join(this.themesDirectory, fileName);
    const source = await readFile(filePath, "utf8");
    await atomicWrite(filePath, rewriteThemeProfile(source, write));
  }

  public validate(): ValidationReport {
    const errors: string[] = [];
    const warnings: string[] = [];
    const cardIds = new Map<string, string>();
    let cards = 0;
    let themes = 0;
    for (const fileName of this.cardFiles()) {
      cards++;
      try {
        const { properties } = parseTres(readText(path.join(this.cardsDirectory, fileName)));
        const id = asString(properties.Id);
        if (!id) errors.push(`${fileName}: Id is empty.`);
        else if (cardIds.has(id)) errors.push(`${fileName}: duplicate Id '${id}' also used by ${cardIds.get(id)}.`);
        else cardIds.set(id, fileName);
        const mechanicTags = asNumber(properties.MechanicTags);
        const unknown = mechanicTags & ~VALID_MECHANIC_MASK;
        if (unknown)
          errors.push(`${fileName}: MechanicTags contains unknown bits 0x${unknown.toString(16).toUpperCase()}.`);
        for (const keyword of asNumberArray(properties.Keywords))
          if (!VALID_KEYWORDS.has(keyword))
            errors.push(`${fileName}: Keywords contains out-of-range value ${keyword}.`);
        const type = asNumber(properties.Type);
        if (type !== 0 && properties.UnitClass !== undefined)
          errors.push(`${fileName}: non-minion resources must not contain UnitClass.`);
        if (type === 0 && properties.UnitClass !== undefined) {
          const unitClass = asNumber(properties.UnitClass, -1);
          if (!Number.isInteger(unitClass) || unitClass < 0 || unitClass > 3)
            errors.push(`${fileName}: UnitClass contains out-of-range value ${unitClass}.`);
        }
        if (properties.Speed !== undefined)
          warnings.push(`${fileName}: legacy Speed will be removed on the next tag save.`);
      } catch (error) {
        errors.push(`${fileName}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    for (const fileName of this.themeFiles()) {
      themes++;
      try {
        const { properties } = parseTres(readText(path.join(this.themesDirectory, fileName)));
        for (const [field, valid] of [
          ["TagWeights", MECHANIC_BITS],
          ["KeywordWeights", VALID_KEYWORDS],
        ] as const) {
          const map = rawNumberMap(properties[field]);
          if (properties[field] !== undefined && !map)
            errors.push(`${fileName}: ${field} must be an integer dictionary.`);
          for (const key of Object.keys(map ?? {}))
            if (!valid.has(Number(key))) errors.push(`${fileName}: ${field} contains unknown key ${key}.`);
        }
        for (const cardId of asStringArray(properties.CoreCardIds))
          if (!cardIds.has(cardId)) warnings.push(`${fileName}: CoreCardIds references missing card '${cardId}'.`);
      } catch (error) {
        errors.push(`${fileName}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    return { ok: errors.length === 0, cards, themes, errors, warnings, checkedAt: new Date().toISOString() };
  }
}

export class ResourceNotFoundError extends Error {
  public readonly status = 404;
}

async function atomicWrite(filePath: string, content: string): Promise<void> {
  const temporary = `${filePath}.${randomUUID()}.tmp`;
  try {
    await writeFile(temporary, content, "utf8");
    await rename(temporary, filePath);
  } finally {
    await rm(temporary, { force: true });
  }
}

// Isolated wrappers make the synchronous read path explicit and easy to audit.
function readText(filePath: string): string {
  return readFileSync(filePath, "utf8");
}
function requireReaddir(directory: string): string[] {
  return readdirSync(directory);
}
