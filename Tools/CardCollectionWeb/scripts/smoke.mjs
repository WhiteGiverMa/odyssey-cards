import assert from "node:assert/strict";

const base = process.env.API_BASE ?? "http://127.0.0.1:8787";
const request = async (path, init) => {
  const response = await fetch(`${base}${path}`, { ...init, headers: { "Content-Type": "application/json" } });
  assert.ok(response.ok, `${path} returned ${response.status}`);
  return response.json();
};

const health = await request("/api/health");
assert.equal(health.ok, true);
const cardResponse = await request("/api/cards");
assert.equal(cardResponse.total, 61);
assert.equal(cardResponse.cards.length, 61);
assert.equal(new Set(cardResponse.cards.map((card) => card.id)).size, 61);
const minions = cardResponse.cards.filter((card) => card.typeKey === "minion");
assert.equal(minions.length, 19);
assert.ok(minions.every((card) => card.unitClass && card.tactical));
assert.ok(cardResponse.cards.every((card) => card.tactical));
assert.ok(cardResponse.cards.every((card) => card.name.zh && card.description.zh && card.artwork.seed >= 0));
assert.equal(cardResponse.cards.find((card) => card.id === "spell_strike")?.name.en, "Strike");
assert.equal(
  cardResponse.cards.find((card) => card.id === "minion_18th_regiment")?.keywordKeys.includes("guard"),
  true,
);

const { heroes, weapons } = await request("/api/heroes");
assert.equal(heroes.length, 4);
assert.equal(weapons.length, 7);
assert.equal(weapons.find((weapon) => weapon.id === "svds_m338")?.attack, 2);
assert.equal(weapons.find((weapon) => weapon.id === "svds_m338")?.ignoreCounterAttack, true);
assert.equal(weapons.find((weapon) => weapon.id === "rail_pistol")?.attack, 1);
const { relics } = await request("/api/relics");
assert.equal(relics.length, 8);
const { themes } = await request("/api/themes");
assert.equal(themes.length, 3);
const rules = await request("/api/rules");
assert.deepEqual(rules.board.capacities, [4, 5, 4]);
assert.equal(rules.unitClasses.length, 4);
const generated = await request("/api/themes/ayame/generate", {
  method: "POST",
  body: JSON.stringify({ seed: 20261114 }),
});
assert.equal(generated.cardIds.length, 20);
assert.equal(generated.cards.length, 20);
assert.equal(generated.stats.totalCards, 20);
assert.ok(generated.stats.manaCurve.reduce((sum, value) => sum + value, 0) === 20);
const repeated = await request("/api/themes/ayame/generate", {
  method: "POST",
  body: JSON.stringify({ seed: 20261114 }),
});
assert.deepEqual(generated.cardIds, repeated.cardIds);

if (process.env.NODE_ENV === "production" || process.env.CHECK_CLIENT === "1") {
  const html = await fetch(base).then((response) => response.text());
  assert.match(html, /星图档案/);
  assert.match(html, /assets\/index-.*\.js/);
}

console.log(
  JSON.stringify(
    {
      ok: true,
      cards: cardResponse.total,
      minionsWithTacticalMetadata: minions.length,
      heroes: heroes.length,
      weapons: weapons.length,
      relics: relics.length,
      themes: themes.length,
      generatedDeck: generated.cardIds.length,
      seed: generated.seed,
      endpoints: [
        "/api/cards",
        "/api/heroes",
        "/api/relics",
        "/api/themes",
        "/api/themes/ayame/generate",
        "/api/rules",
      ],
    },
    null,
    2,
  ),
);
