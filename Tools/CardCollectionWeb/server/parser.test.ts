import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { parseTres } from "./tres.ts";
import { loadCatalog, REPOSITORY_ROOT } from "./data.ts";
import { generateThemeDeck } from "./generator.ts";
import { stableHash } from "./artwork.ts";

test("Godot Variant parser reads arrays, dictionaries and subresources", () => {
  const document = parseTres(
    `[gd_resource type="Resource" load_steps=2 format=3]\n[sub_resource type="Resource" id="SubResource_1"]\nValue = 5\nTargetType = "res://Resources/Cards/Spell_Ukemi.tres"\n[resource]\nId = "domain_test"\nKeywords = Array[int]([2, 8])\nTagWeights = {\n1: -2,\n4: 3\n}\nEffects = [SubResource("SubResource_1")]`,
  );
  assert.equal(document.properties.Id, "domain_test");
  assert.deepEqual(document.properties.Keywords, [2, 8]);
  assert.deepEqual(document.properties.TagWeights, { "1": -2, "4": 3 });
  assert.equal(document.subResources.get("SubResource_1")?.Value, 5);
});

test("catalog loads every real card, all tactical classes and reference decks", async () => {
  const catalog = await loadCatalog();
  assert.equal(catalog.cards.length, 68);
  assert.equal(new Set(catalog.cards.map((card) => card.id)).size, 68);
  assert.equal(catalog.cards.filter((card) => card.typeKey === "minion").length, 19);
  assert.ok(catalog.cards.every((card) => card.tactical));
  assert.ok(catalog.cards.filter((card) => card.typeKey === "minion").every((card) => card.unitClass && card.tactical));
  assert.equal(catalog.themes.length, 3);
  assert.equal(catalog.heroes.length, 4);
  assert.equal(catalog.weapons.length, 7);
  assert.equal(catalog.relics.length, 8);
  const ayame = catalog.themes.find((theme) => theme.id === "ayame")!;
  const generated = generateThemeDeck(ayame, catalog.cards, 42);
  assert.equal(generated.cardIds.length, 20);
  const legalCore = ayame.coreCardIds.filter((id) => catalog.cards.find((card) => card.id === id)?.buildable);
  assert.ok(legalCore.every((id) => generated.cardIds.includes(id)));
});

test("artwork FNV-1a hash is stable and card text is loaded from YAML", async () => {
  assert.equal(stableHash("spell_strike"), stableHash("spell_strike"));
  const source = await readFile(path.join(REPOSITORY_ROOT, "Resources", "Cards", "Spell_Strike.tres"), "utf8");
  assert.equal(parseTres(source).properties.Id, "spell_strike");
  const catalog = await loadCatalog();
  assert.equal(catalog.cards.find((card) => card.id === "spell_strike")?.name.en, "Strike");
});
