import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { parseTres } from "./tres.ts";
import { loadCatalog, REPOSITORY_ROOT } from "./data.ts";
import { generateThemeDeck } from "./generator.ts";
import { ResourceTagService } from "./resource-service.ts";
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
  assert.equal(catalog.cards.length, 81);
  assert.equal(new Set(catalog.cards.map((card) => card.id)).size, 81);
  assert.equal(catalog.cards.filter((card) => card.typeKey === "minion").length, 31);
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
  // 0.2.3 门禁：核心池与随机池同一标准——衍生 / 特殊 / 状态牌不得写入 ThemeProfile，也不能进入生成卡组。
  for (const theme of catalog.themes) {
    const deck = generateThemeDeck(theme, catalog.cards, 11);
    assert.equal(deck.cardIds.length, 20);
    assert.ok(
      theme.coreCardIds.every((id) => catalog.cards.find((card) => card.id === id)?.buildable),
      `${theme.id} 的核心池含不可构筑卡（衍生/特殊/状态牌）`,
    );
    assert.ok(deck.cardIds.every((id) => catalog.cards.find((card) => card.id === id)?.buildable));
    assert.ok(theme.coreCardIds.every((id) => deck.cardIds.includes(id)));
  }
});

test("0.2.3 data links intel removal and reworked spell references", async () => {
  const catalog = await loadCatalog();
  const byId = new Map(catalog.cards.map((card) => [card.id, card]));
  assert.equal(byId.get("spell_retrieve")?.keywordKeys.includes("intel"), false);
  assert.equal(byId.get("spell_discover")?.keywordKeys.includes("intel"), false);
  assert.equal(byId.get("spell_expose")?.keywordKeys.includes("intel"), true);
  assert.ok(byId.get("spell_longtermism")?.relatedCardIds.includes("spell_ignite"));
});

test("theme writes reject derivative, special and status core cards", async () => {
  const service = new ResourceTagService(REPOSITORY_ROOT);
  for (const cardId of ["spell_ukemi", "minion_roach", "domain_infinite_fire", "status_explain"]) {
    await assert.rejects(
      service.saveTheme("ayame", { tagWeights: {}, coreCardIds: [cardId] }),
      /buildable cards|unknown card/,
      `${cardId} 不应被写入 ThemeProfile 核心池`,
    );
  }
});

test("pack assignment stays orthogonal to mechanic tags", async () => {
  const catalog = await loadCatalog();
  const byId = new Map(catalog.cards.map((card) => [card.id, card]));
  // 骑士I型带 mechanics 标签，但属于核心包——卡包不受机制标签影响。
  assert.equal(byId.get("minion_knight_type1")?.mechanicTagKeys.includes("mechanics"), true);
  assert.equal(byId.get("minion_knight_type1")?.packId, "core");
  assert.equal(byId.get("minion_mingshan_walking_support")?.packId, "unmanned_victory");
  assert.equal(byId.get("minion_mech_lancer")?.packId, "future_hunt");
});

test("artwork FNV-1a hash is stable and card text is loaded from YAML", async () => {
  assert.equal(stableHash("spell_strike"), stableHash("spell_strike"));
  const source = await readFile(path.join(REPOSITORY_ROOT, "Resources", "Cards", "Spell_Strike.tres"), "utf8");
  assert.equal(parseTres(source).properties.Id, "spell_strike");
  const catalog = await loadCatalog();
  assert.equal(catalog.cards.find((card) => card.id === "spell_strike")?.name.en, "Strike");
});
