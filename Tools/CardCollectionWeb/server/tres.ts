import type { RawProperties, RawValue } from "../shared/types.ts";

export interface TresDocument {
  header: RawProperties;
  externalResources: Array<RawProperties & { path?: RawValue }>;
  subResources: Map<string, RawProperties>;
  properties: RawProperties;
}

/** A small, strict Godot Variant reader for the resource syntax used by this project. */
class VariantReader {
  private index = 0;
  public constructor(private readonly source: string) {}

  public parse(): RawValue {
    this.whitespace();
    const value = this.value();
    this.whitespace();
    if (this.index !== this.source.length) {
      throw new Error(`Unexpected Godot value at ${this.index}: ${this.source.slice(this.index, this.index + 32)}`);
    }
    return value;
  }

  private value(): RawValue {
    this.whitespace();
    const char = this.source[this.index];
    if (char === '"') return this.string();
    if (char === "[") return this.array();
    if (char === "{") return this.dictionary();
    if (char === "-" || char === "+" || (char && /\d/.test(char))) return this.number();
    let identifier: string;
    if (this.source.slice(this.index).startsWith("Array[")) {
      const genericEnd = this.source.indexOf("]", this.index);
      if (genericEnd < 0) throw new Error(`Unclosed typed Array at ${this.index}`);
      identifier = "Array";
      this.index = genericEnd + 1;
    } else identifier = this.identifier();
    if (identifier === "true") return true;
    if (identifier === "false") return false;
    if (identifier === "null") return null;
    this.whitespace();
    if (this.source[this.index] === "(") {
      this.index++;
      const args: RawValue[] = [];
      this.whitespace();
      if (this.source[this.index] !== ")") {
        while (this.index < this.source.length) {
          args.push(this.value());
          this.whitespace();
          if (this.source[this.index] !== ",") break;
          this.index++;
        }
      }
      this.expect(")");
      if (identifier.startsWith("Packed")) return args;
      if (identifier.startsWith("Array")) {
        return (args[0] as RawValue[] | undefined) ?? [];
      }
      if (identifier === "ExtResource" || identifier === "SubResource" || identifier === "Resource") {
        return args[0] ?? null;
      }
      return { [`$${identifier}`]: args };
    }
    return identifier;
  }

  private string(): string {
    const start = this.index;
    this.index++;
    let escaped = false;
    while (this.index < this.source.length) {
      const char = this.source[this.index++];
      if (escaped) escaped = false;
      else if (char === "\\") escaped = true;
      else if (char === '"') return JSON.parse(this.source.slice(start, this.index)) as string;
    }
    throw new Error("Unterminated string in Godot resource");
  }

  private number(): number {
    const match = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?/i.exec(this.source.slice(this.index));
    if (!match) throw new Error(`Invalid number at ${this.index}`);
    this.index += match[0].length;
    return Number(match[0]);
  }

  private identifier(): string {
    const match = /^[A-Za-z_][A-Za-z0-9_\[\].]*/.exec(this.source.slice(this.index));
    if (!match) throw new Error(`Expected value at ${this.index}: ${this.source.slice(this.index, this.index + 32)}`);
    this.index += match[0].length;
    return match[0];
  }

  private array(): RawValue[] {
    const values: RawValue[] = [];
    this.expect("[");
    this.whitespace();
    while (this.index < this.source.length && this.source[this.index] !== "]") {
      values.push(this.value());
      this.whitespace();
      if (this.source[this.index] === ",") this.index++;
      else break;
      this.whitespace();
    }
    this.expect("]");
    return values;
  }

  private dictionary(): RawProperties {
    const result: RawProperties = {};
    this.expect("{");
    this.whitespace();
    while (this.index < this.source.length && this.source[this.index] !== "}") {
      const keyValue = this.value();
      this.whitespace();
      this.expect(":");
      const key = typeof keyValue === "object" ? JSON.stringify(keyValue) : String(keyValue);
      result[key] = this.value();
      this.whitespace();
      if (this.source[this.index] === ",") this.index++;
      else break;
      this.whitespace();
    }
    this.expect("}");
    return result;
  }

  private whitespace(): void {
    while (this.index < this.source.length && /\s/.test(this.source[this.index] ?? "")) this.index++;
  }

  private expect(expected: string): void {
    if (this.source[this.index] !== expected) {
      throw new Error(`Expected '${expected}' at ${this.index}: ${this.source.slice(this.index, this.index + 32)}`);
    }
    this.index++;
  }
}

function sectionAttributes(header: string): RawProperties {
  const result: RawProperties = {};
  for (const match of header.matchAll(/([A-Za-z_][A-Za-z0-9_]*)=("[^"]*"|\S+)/g)) {
    if (!match[1] || !match[2]) continue;
    result[match[1]] = match[2].startsWith('"') ? JSON.parse(match[2]) : match[2];
  }
  return result;
}

export function parseTres(source: string): TresDocument {
  const normalized = source.replace(/^\uFEFF/, "").replace(/\r\n?/g, "\n");
  const lines = normalized.split("\n");
  const document: TresDocument = {
    header: {},
    externalResources: [],
    subResources: new Map(),
    properties: {},
  };
  let section = "header";
  let current: RawProperties = document.header;
  let property: { target: RawProperties; key: string; buffer: string } | null = null;

  const flush = (): void => {
    if (!property) return;
    property.target[property.key] = new VariantReader(property.buffer.trim()).parse();
    property = null;
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith(";") || trimmed.startsWith("#")) continue;
    const sectionMatch = /^\[([^\]]+)\]$/.exec(trimmed);
    if (sectionMatch?.[1]) {
      flush();
      const [kind, ...rest] = sectionMatch[1].split(/\s+/);
      const attributes = sectionAttributes(rest.join(" "));
      section = kind ?? "header";
      if (section === "resource") current = document.properties;
      else if (section === "sub_resource") {
        current = attributes;
        document.subResources.set(String(attributes.id ?? ""), current);
      } else if (section === "ext_resource") {
        current = attributes;
        document.externalResources.push(current);
      } else current = document.header;
      continue;
    }
    if (section === "header" || section === "gd_resource") continue;
    const assignment = /^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/.exec(trimmed);
    if (assignment?.[1] && assignment[2] !== undefined) {
      flush();
      property = { target: current, key: assignment[1], buffer: assignment[2] };
      if (isCompleteValue(property.buffer)) flush();
    } else if (property) {
      property.buffer += `\n${trimmed}`;
      if (isCompleteValue(property.buffer)) flush();
    }
  }
  flush();
  return document;
}

function isCompleteValue(source: string): boolean {
  let square = 0;
  let curly = 0;
  let round = 0;
  let quoted = false;
  let escaped = false;
  for (const char of source) {
    if (escaped) {
      escaped = false;
      continue;
    }
    if (char === "\\" && quoted) {
      escaped = true;
      continue;
    }
    if (char === '"') {
      quoted = !quoted;
      continue;
    }
    if (quoted) continue;
    if (char === "[") square++;
    else if (char === "]") square--;
    else if (char === "{") curly++;
    else if (char === "}") curly--;
    else if (char === "(") round++;
    else if (char === ")") round--;
  }
  return !quoted && square <= 0 && curly <= 0 && round <= 0;
}

export function asNumber(value: RawValue | undefined, fallback = 0): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function asString(value: RawValue | undefined, fallback = ""): string {
  return typeof value === "string" ? value : fallback;
}

export function asNumberArray(value: RawValue | undefined): number[] {
  return Array.isArray(value) ? value.filter((entry): entry is number => typeof entry === "number") : [];
}

export function asStringArray(value: RawValue | undefined): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === "string") : [];
}

export function asNumberMap(value: RawValue | undefined): Record<string, number> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};
  return Object.fromEntries(
    Object.entries(value).filter((entry): entry is [string, number] => typeof entry[1] === "number"),
  );
}
