import type {
  CardsResponse,
  GeneratedDeck,
  HeroRecord,
  RelicRecord,
  RulesResponse,
  ThemeRecord,
  WeaponRecord,
} from "../shared/types.ts";

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { ...init, headers: { "Content-Type": "application/json", ...init?.headers } });
  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { message?: string; error?: string } | null;
    throw new Error(body?.message ?? body?.error ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  cards: () => request<CardsResponse>("/api/cards"),
  heroes: () => request<{ heroes: HeroRecord[]; weapons: WeaponRecord[]; total: number }>("/api/heroes"),
  relics: () => request<{ relics: RelicRecord[]; total: number }>("/api/relics"),
  themes: () => request<{ themes: ThemeRecord[]; total: number }>("/api/themes"),
  rules: () => request<RulesResponse>("/api/rules"),
  generate: (themeId: string, seed?: number) =>
    request<GeneratedDeck>(`/api/themes/${encodeURIComponent(themeId)}/generate`, {
      method: "POST",
      body: JSON.stringify(seed === undefined ? {} : { seed }),
    }),
};
