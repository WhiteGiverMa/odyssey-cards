import type { PackSummary } from "../shared/packs.ts";
import type {
  CardRecord,
  CardsResponse,
  GeneratedDeck,
  HeroRecord,
  PackAssignResponse,
  RelicRecord,
  RulesResponse,
  ResourceValidationReport,
  ThemeRecord,
  WeaponRecord,
} from "../shared/types.ts";

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const maxAttempts = 4;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(url, { ...init, headers: { "Content-Type": "application/json", ...init?.headers } });
      if ((response.status === 502 || response.status === 503 || response.status === 504) && attempt < maxAttempts) {
        await new Promise((resolve) => setTimeout(resolve, 250 * attempt));
        continue;
      }
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { message?: string; error?: string } | null;
        throw new Error(body?.message ?? body?.error ?? `${response.status} ${response.statusText}`);
      }
      return response.json() as Promise<T>;
    } catch (err) {
      if (attempt < maxAttempts && err instanceof TypeError) {
        await new Promise((resolve) => setTimeout(resolve, 250 * attempt));
        continue;
      }
      throw err;
    }
  }
  throw new Error(`Request failed: ${url}`);
}

export const api = {
  cards: () => request<CardsResponse>("/api/cards"),
  heroes: () => request<{ heroes: HeroRecord[]; weapons: WeaponRecord[]; total: number }>("/api/heroes"),
  relics: () => request<{ relics: RelicRecord[]; total: number }>("/api/relics"),
  themes: () => request<{ themes: ThemeRecord[]; total: number }>("/api/themes"),
  rules: () => request<RulesResponse>("/api/rules"),
  validate: () => request<ResourceValidationReport>("/api/validate"),
  packs: () => request<{ packs: PackSummary[]; total: number; generatedAt: string }>("/api/packs"),
  assignPack: (cardIds: string[], packId: string) =>
    request<PackAssignResponse>("/api/packs/assign", {
      method: "POST",
      body: JSON.stringify({ cardIds, packId }),
    }),
  saveCardTags: (
    cardId: string,
    body: { mechanicTags: number; keywords: number[]; unitClass?: number; packId?: string },
  ) =>
    request<CardRecord>(`/api/cards/${encodeURIComponent(cardId)}/tags`, { method: "PUT", body: JSON.stringify(body) }),
  saveTheme: (
    themeId: string,
    body: { tagWeights: Record<string, number>; keywordWeights: Record<string, number>; coreCardIds: string[] },
  ) =>
    request<ThemeRecord>(`/api/themes/${encodeURIComponent(themeId)}`, { method: "PUT", body: JSON.stringify(body) }),
  generate: (themeId: string, seed?: number) =>
    request<GeneratedDeck>(`/api/themes/${encodeURIComponent(themeId)}/generate`, {
      method: "POST",
      body: JSON.stringify(seed === undefined ? {} : { seed }),
    }),
};
