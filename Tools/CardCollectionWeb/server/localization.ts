import { readFile } from "node:fs/promises";
import { parse } from "yaml";
import type { Language, RawValue } from "../shared/types.ts";

export type TranslationTable = Record<string, string>;

function flatten(value: unknown, prefix = "", output: TranslationTable = {}): TranslationTable {
  if (!value || typeof value !== "object" || Array.isArray(value)) return output;
  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof child === "string" || typeof child === "number" || typeof child === "boolean")
      output[path] = String(child);
    else flatten(child, path, output);
  }
  return output;
}

export async function loadLocalization(path: string): Promise<TranslationTable> {
  const source = await readFile(path, "utf8");
  return flatten(parse(source) as RawValue);
}

export function localize(
  tables: Record<Language, TranslationTable>,
  key: string,
  fallback: string,
  parameters: Record<string, string | number> = {},
): { text: Record<Language, string>; fallback: Record<Language, boolean> } {
  const result = { zh: "", en: "" };
  const usedFallback = { zh: false, en: false };
  for (const language of ["zh", "en"] as Language[]) {
    const value = tables[language][key];
    usedFallback[language] = value === undefined;
    result[language] = interpolate(value ?? fallback, parameters);
  }
  return { text: result, fallback: usedFallback };
}

export function translate(
  tables: Record<Language, TranslationTable>,
  key: string,
  fallback: string,
): Record<Language, string> {
  return { zh: tables.zh[key] ?? fallback, en: tables.en[key] ?? tables.zh[key] ?? fallback };
}

export function interpolate(template: string, parameters: Record<string, string | number>): string {
  return template.replace(/\{\{?\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}?\}/g, (match, key: string) => {
    const value = parameters[key];
    return value === undefined ? match : String(value);
  });
}
