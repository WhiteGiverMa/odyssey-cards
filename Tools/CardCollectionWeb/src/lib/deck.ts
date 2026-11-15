import type { CardRecord } from "../../shared/types.ts";

export interface DeckState {
  name: string;
  cardIds: string[];
}

export function deckCards(deck: DeckState, cards: CardRecord[]): CardRecord[] {
  return deck.cardIds
    .map((id) => cards.find((card) => card.id === id))
    .filter((card): card is CardRecord => Boolean(card));
}

export function cardCounts(cardIds: string[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const id of cardIds) counts.set(id, (counts.get(id) ?? 0) + 1);
  return counts;
}

export function deckProblems(deck: DeckState, cards: CardRecord[]): string[] {
  const problems: string[] = [];
  if (deck.cardIds.length > 20) problems.push(`Deck exceeds 20 cards (${deck.cardIds.length})`);
  for (const [id, count] of cardCounts(deck.cardIds)) {
    const card = cards.find((entry) => entry.id === id);
    if (!card) problems.push(`Unknown card: ${id}`);
    else if (!card.buildable) problems.push(`Unbuildable: ${card.id}`);
    else if (count > card.maxCopies) problems.push(`${card.id}: ${count}/${card.maxCopies}`);
  }
  return problems;
}

export function parseDeckImport(source: string, cards: CardRecord[]): DeckState {
  const trimmed = source.trim();
  if (!trimmed) throw new Error("Empty deck");
  if (trimmed.startsWith("{") || trimmed.startsWith("[")) {
    const parsed = JSON.parse(trimmed) as { name?: string; cardIds?: unknown; cards?: unknown } | unknown[];
    const container = Array.isArray(parsed) ? { cards: parsed } : parsed;
    const input = container.cardIds ?? container.cards;
    if (!Array.isArray(input)) throw new Error("Missing cardIds");
    const cardIds = input
      .map((entry) =>
        typeof entry === "string"
          ? entry
          : typeof entry === "object" && entry && "id" in entry
            ? String((entry as { id: unknown }).id)
            : "",
      )
      .filter(Boolean);
    resolveIds(cardIds, cards);
    return {
      name: "name" in container && typeof container.name === "string" ? container.name : "Imported deck",
      cardIds,
    };
  }
  const cardIds: string[] = [];
  for (const rawLine of trimmed.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#") || line.startsWith("//")) continue;
    const match = /^(?:(\d+)(?:\s*[x×*]\s*|\s+))?(.+?)(?:\s*[x×]\s*(\d+))?$/i.exec(line);
    if (!match) continue;
    const count = Number(match[1] ?? match[3] ?? 1);
    const rawNeedle = (match[2] ?? "").trim();
    const bracketId = /\[([a-z0-9_]+)\]\s*$/i.exec(rawNeedle)?.[1];
    const needle = rawNeedle.replace(/\s*\[[a-z0-9_]+\]\s*$/i, "").trim();
    const card = cards.find(
      (entry) =>
        entry.id.toLowerCase() === (bracketId ?? needle).toLowerCase() ||
        entry.id.toLowerCase() === needle.toLowerCase() ||
        entry.name.zh.toLowerCase() === needle.toLowerCase() ||
        entry.name.en.toLowerCase() === needle.toLowerCase(),
    );
    if (!card) throw new Error(`Unknown card: ${needle}`);
    for (let index = 0; index < Math.min(999, Math.max(1, count)); index++) cardIds.push(card.id);
  }
  resolveIds(cardIds, cards);
  return { name: "Imported deck", cardIds };
}

function resolveIds(ids: string[], cards: CardRecord[]): void {
  if (!ids.length) throw new Error("No cards");
  for (const id of ids) if (!cards.some((card) => card.id === id)) throw new Error(`Unknown card: ${id}`);
}

export function deckToText(deck: DeckState, cards: CardRecord[], language: "zh" | "en"): string {
  const counts = cardCounts(deck.cardIds);
  const rows = [...counts]
    .map(([id, count]) => ({ card: cards.find((entry) => entry.id === id), count }))
    .filter((entry) => entry.card)
    .sort((a, b) => (a.card?.cost ?? 0) - (b.card?.cost ?? 0));
  return `# ${deck.name}\n${rows.map(({ card, count }) => `${count}× ${card?.name[language]} [${card?.id}]`).join("\n")}`;
}
