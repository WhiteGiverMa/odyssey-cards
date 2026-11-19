import { KEYWORD_KEYS, MECHANIC_TAGS } from "../shared/constants.ts";
import { normalizePackId } from "../shared/packs.ts";
import { asNumber, asNumberArray, asNumberMap, asString, asStringArray, parseTres } from "./tres.ts";

export interface CardTagWrite {
  mechanicTags: number;
  keywords: number[];
  unitClass?: number | null;
  /** 可选：写入 PascalCase 的 PackId 字段。 */
  packId?: string;
}

export interface ThemeProfileWrite {
  tagWeights: Record<string, number>;
  keywordWeights?: Record<string, number>;
  coreCardIds: string[];
}

interface SourceLine {
  text: string;
  ending: string;
}

interface FieldRange {
  key: string;
  start: number;
  end: number;
  indent: string;
  value: string;
}

const VALID_KEYWORDS = new Set(KEYWORD_KEYS.map((_, index) => index).filter((index) => index >= 1 && index <= 12));
const VALID_MECHANIC_MASK = MECHANIC_TAGS.reduce((mask, [, bit]) => mask | bit, 0);
const ASSIGNMENT = /^(\s*)([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/;
const SECTION = /^\s*\[([^\]]+)\]/;

function linesOf(source: string): SourceLine[] {
  const lines: SourceLine[] = [];
  let start = 0;
  for (const match of source.matchAll(/\r\n|\n|\r/g)) {
    const index = match.index;
    lines.push({ text: source.slice(start, index), ending: match[0] });
    start = index + match[0].length;
  }
  if (start < source.length) lines.push({ text: source.slice(start), ending: "" });
  return lines;
}

function joinLines(lines: SourceLine[]): string {
  return lines.map((line) => line.text + line.ending).join("");
}

function fieldAt(lines: SourceLine[], index: number): FieldRange | null {
  const match = ASSIGNMENT.exec(lines[index]?.text ?? "");
  if (!match?.[2]) return null;
  let inResource = false;
  for (let cursor = index; cursor >= 0; cursor--) {
    const section = SECTION.exec(lines[cursor]?.text ?? "");
    if (!section) continue;
    inResource = section[1]?.split(/\s+/)[0] === "resource";
    break;
  }
  if (!inResource) return null;
  return {
    key: match[2],
    start: index,
    end: index,
    indent: match[1] ?? "",
    value: match[3]?.trim() ?? "",
  };
}

function fields(lines: SourceLine[]): FieldRange[] {
  const result: FieldRange[] = [];
  for (let index = 0; index < lines.length; index++) {
    const field = fieldAt(lines, index);
    if (!field) continue;
    if (field.value === "{") {
      for (let end = index + 1; end < lines.length; end++) {
        if (lines[end]?.text.trimStart().startsWith("}")) {
          field.end = end;
          index = end;
          break;
        }
      }
    }
    result.push(field);
  }
  return result;
}

function field(lines: SourceLine[], key: string): FieldRange | undefined {
  return fields(lines).find((entry) => entry.key.toLowerCase() === key.toLowerCase());
}

function dominantEnding(lines: SourceLine[]): string {
  return lines.find((line) => line.ending)?.ending ?? "\n";
}

function replaceSingle(lines: SourceLine[], target: FieldRange, text: string): void {
  lines[target.start] = { text: `${target.indent}${text}`, ending: lines[target.start]?.ending ?? "" };
}

function insertAfter(lines: SourceLine[], anchor: FieldRange | undefined, inserted: string[]): void {
  const newline = dominantEnding(lines);
  const index = anchor ? anchor.end + 1 : lines.length;
  const previous = anchor ? lines[anchor.end] : lines[index - 1];
  const finalEnding = previous && !previous.ending && index === lines.length ? "" : newline;
  if (previous && !previous.ending && index === lines.length) previous.ending = newline;
  lines.splice(
    index,
    0,
    ...inserted.map((text, item) => ({
      text,
      ending: item === inserted.length - 1 ? finalEnding : newline,
    })),
  );
}

function removeField(lines: SourceLine[], target: FieldRange): void {
  lines.splice(target.start, target.end - target.start + 1);
}

function sameNumbers(left: number[], right: number[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function sameStrings(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function normalizeNumberMap(value: Record<string, number>): Record<string, number> {
  const result: Record<string, number> = {};
  for (const [key, weight] of Object.entries(value)) {
    if (!/^\d+$/.test(key) || !Number.isSafeInteger(weight))
      throw new Error("Theme weights require integer keys and values.");
    result[key] = weight;
  }
  return result;
}

function sameMap(left: Record<string, number>, right: Record<string, number>): boolean {
  const a = Object.entries(left).sort(([keyA], [keyB]) => Number(keyA) - Number(keyB));
  const b = Object.entries(right).sort(([keyA], [keyB]) => Number(keyA) - Number(keyB));
  return a.length === b.length && a.every(([key, value], index) => key === b[index]?.[0] && value === b[index]?.[1]);
}

function validateCardWrite(document: ReturnType<typeof parseTres>, write: CardTagWrite): void {
  if (
    !Number.isSafeInteger(write.mechanicTags) ||
    write.mechanicTags < 0 ||
    (write.mechanicTags & ~VALID_MECHANIC_MASK) !== 0
  )
    throw new Error("MechanicTags contains an unknown bit.");
  if (!Array.isArray(write.keywords) || write.keywords.some((keyword) => !VALID_KEYWORDS.has(keyword)))
    throw new Error("Keywords contains an out-of-range value.");
  if (new Set(write.keywords).size !== write.keywords.length) throw new Error("Keywords must not contain duplicates.");
  if (write.unitClass !== undefined && write.unitClass !== null) {
    if (asNumber(document.properties.Type) !== 0)
      throw new Error("UnitClass can only be written to minions (Type = 0).");
    if (!Number.isInteger(write.unitClass) || write.unitClass < 0 || write.unitClass > 3)
      throw new Error("UnitClass must be an integer from 0 to 3.");
  }
  if (write.packId !== undefined && normalizePackId(write.packId) === null)
    throw new Error("PackId must be a slug of letters, digits, '-' or '_' (max 48 chars).");
}

/** 仅触碰 [resource] 段的 PackId 行，其他字节与行尾原样保留。 */
function setPackField(lines: SourceLine[], packId: string): void {
  const normalized = normalizePackId(packId);
  if (normalized === null) throw new Error("PackId must be a valid slug.");
  const text = `PackId = ${JSON.stringify(normalized)}`;
  const target = field(lines, "PackId");
  if (target) {
    if (
      lines
        .slice(target.start, target.end + 1)
        .map((line) => line.text.trim())
        .join(" ") === text
    )
      return;
    replaceSingle(lines, target, text);
    return;
  }
  insertAfter(lines, field(lines, "Id") ?? field(lines, "Type"), [text]);
}

/** 只写 PackId —— 批量归类走这条最窄的写路径，绝不动平衡数值。 */
export function rewriteCardPack(source: string, packId: string): string {
  const normalized = normalizePackId(packId);
  if (normalized === null) throw new Error("PackId must be a valid slug.");
  const document = parseTres(source);
  if (asString(document.properties.PackId) === normalized) return source;
  const lines = linesOf(source);
  setPackField(lines, normalized);
  return joinLines(lines);
}

/** Rewrites only the tag-owned CardData fields, preserving all untouched bytes and line endings. */
export function rewriteCardTags(source: string, write: CardTagWrite): string {
  const document = parseTres(source);
  validateCardWrite(document, write);
  const lines = linesOf(source);

  const mechanic = field(lines, "MechanicTags");
  if (mechanic) {
    if (asNumber(document.properties.MechanicTags) !== write.mechanicTags)
      replaceSingle(lines, mechanic, `MechanicTags = ${write.mechanicTags}`);
  } else if (write.mechanicTags !== 0) {
    insertAfter(lines, field(lines, "Type") ?? field(lines, "Id"), [`MechanicTags = ${write.mechanicTags}`]);
  }

  const keywords = field(lines, "Keywords");
  if (keywords && !sameNumbers(asNumberArray(document.properties.Keywords), write.keywords)) {
    if (write.keywords.length === 0) removeField(lines, keywords);
    else replaceSingle(lines, keywords, `Keywords = Array[int]([${write.keywords.join(", ")}])`);
  } else if (!keywords && write.keywords.length > 0) {
    insertAfter(lines, field(lines, "MechanicTags") ?? field(lines, "Type"), [
      `Keywords = Array[int]([${write.keywords.join(", ")}])`,
    ]);
  }

  if (write.unitClass !== undefined && write.unitClass !== null) {
    const unitClass = field(lines, "UnitClass");
    if (unitClass) {
      if (asNumber(document.properties.UnitClass) !== write.unitClass)
        replaceSingle(lines, unitClass, `UnitClass = ${write.unitClass}`);
    } else if (write.unitClass !== 0) {
      insertAfter(lines, field(lines, "Type"), [`UnitClass = ${write.unitClass}`]);
    }
  }

  if (write.packId !== undefined) setPackField(lines, write.packId);

  const speed = field(lines, "Speed");
  if (speed) removeField(lines, speed);
  return joinLines(lines);
}

function setDictionary(lines: SourceLine[], key: string, input: Record<string, number>, insertAfterKey?: string): void {
  const values = normalizeNumberMap(input);
  const target = field(lines, key);
  if (target) {
    const text = lines
      .slice(target.start, target.end + 1)
      .map((line) => line.text)
      .join("\n");
    const current = asNumberMap(parseTres(`[resource]\n${text}`).properties[key]);
    if (sameMap(current, values)) return;
    const entries = Object.entries(values).sort(([a], [b]) => Number(a) - Number(b));
    const replacement = entries.length
      ? [`${key} = {`, ...entries.map(([entry, weight]) => `\t${entry}: ${weight},`), "}"]
      : [`${key} = {}`];
    const newline = lines[target.start]?.ending || dominantEnding(lines);
    const lastEnding = lines[target.end]?.ending ?? "";
    lines.splice(
      target.start,
      target.end - target.start + 1,
      ...replacement.map((line, index) => ({
        text: `${target.indent}${line}`,
        ending: index === replacement.length - 1 ? lastEnding : newline,
      })),
    );
    return;
  }
  if (key === "KeywordWeights" && Object.keys(values).length === 0) return;
  const entries = Object.entries(values).sort(([a], [b]) => Number(a) - Number(b));
  const replacement = entries.length
    ? [`${key} = {`, ...entries.map(([entry, weight]) => `\t${entry}: ${weight},`), "}"]
    : [`${key} = {}`];
  insertAfter(lines, field(lines, insertAfterKey ?? "TagWeights") ?? field(lines, "MaxDomainCards"), replacement);
}

/** Rewrites only ThemeProfile tag/keyword weights and core card IDs. */
export function rewriteThemeProfile(source: string, write: ThemeProfileWrite): string {
  if (!write || typeof write !== "object") throw new Error("Theme payload is required.");
  if (!Array.isArray(write.coreCardIds) || write.coreCardIds.some((id) => typeof id !== "string" || !id.trim()))
    throw new Error("CoreCardIds must contain non-empty card IDs.");
  if (new Set(write.coreCardIds).size !== write.coreCardIds.length)
    throw new Error("CoreCardIds must not contain duplicates.");
  const document = parseTres(source);
  const lines = linesOf(source);
  setDictionary(lines, "TagWeights", write.tagWeights, "MaxDomainCards");

  if (write.keywordWeights !== undefined) setDictionary(lines, "KeywordWeights", write.keywordWeights, "TagWeights");

  const core = field(lines, "CoreCardIds");
  if (core && !sameStrings(asStringArray(document.properties.CoreCardIds), write.coreCardIds)) {
    replaceSingle(
      lines,
      core,
      `CoreCardIds = PackedStringArray(${write.coreCardIds.map((id) => JSON.stringify(id)).join(", ")})`,
    );
  } else if (!core && write.coreCardIds.length > 0) {
    insertAfter(lines, field(lines, "KeywordWeights") ?? field(lines, "TagWeights"), [
      `CoreCardIds = PackedStringArray(${write.coreCardIds.map((id) => JSON.stringify(id)).join(", ")})`,
    ]);
  }
  return joinLines(lines);
}
