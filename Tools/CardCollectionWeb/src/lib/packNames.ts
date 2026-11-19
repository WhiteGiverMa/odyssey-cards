import { packLabel } from "../../shared/packs.ts";
import type { Language } from "../../shared/types.ts";

const STORAGE_KEY = "odyssey-web-pack-names";
type PackNameMap = Record<string, string>;

function readNames(): PackNameMap {
  try {
    const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "{}") as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) return parsed as PackNameMap;
  } catch {
    /* corrupt local state simply falls back to the ID-derived label */
  }
  return {};
}

/** 玩家为自定义卡包设置的显示名（浏览器本地保存）。 */
export function savePackName(id: string, name: string): void {
  const trimmed = name.trim();
  if (!trimmed) return;
  const names = readNames();
  names[id] = trimmed;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(names));
}

export function displayPackName(id: string, language: Language): string {
  return readNames()[id] || packLabel(id, language);
}
