import assert from "node:assert/strict";
import test from "node:test";
import type { CardRecord } from "../../shared/types.ts";
import { deckToText, parseDeckImport } from "./deck.ts";

const cards = [
  { id: "spell_strike", name: { zh: "打击", en: "Strike" }, cost: 1 },
  { id: "minion_40_main_battle_tank", name: { zh: "40主战坦克", en: "Type 40 Main Battle Tank" }, cost: 6 },
] as CardRecord[];

test("text deck import supports IDs, localized names and exported bracket IDs", () => {
  const exported = deckToText(
    { name: "Test", cardIds: ["spell_strike", "spell_strike", "minion_40_main_battle_tank"] },
    cards,
    "zh",
  );
  const imported = parseDeckImport(exported, cards);
  assert.deepEqual(imported.cardIds, ["spell_strike", "spell_strike", "minion_40_main_battle_tank"]);
});

test("numeric card names are not mistaken for quantities", () => {
  const imported = parseDeckImport("40主战坦克", cards);
  assert.deepEqual(imported.cardIds, ["minion_40_main_battle_tank"]);
});

test("JSON import retains oversized decks for explicit validity diagnostics", () => {
  const imported = parseDeckImport(
    JSON.stringify({ name: "Oversize", cardIds: Array(21).fill("spell_strike") }),
    cards,
  );
  assert.equal(imported.cardIds.length, 21);
});
