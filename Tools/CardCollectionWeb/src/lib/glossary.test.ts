import assert from "node:assert/strict";
import test from "node:test";
import { GLOSSARY, tokenizeGameText } from "../../shared/glossary.ts";
import type { CardRecord } from "../../shared/types.ts";

const card = {
  id: "spell_heavy_strike",
  name: { zh: "沉重打击", en: "Heavy Strike" },
} as CardRecord;

test("glossary recognizes every required status and all twelve combat keywords", () => {
  for (const id of [
    "vulnerable",
    "weak",
    "fragile",
    "disabled",
    "damage-over-time",
    "smokescreen",
    "total-observation",
    "meltdown",
    "weapon-disabled",
    "attack-zero",
    "rail-lock",
    "inescapable-flaw",
    "solid-barrier",
    "unbreakable",
    "fatal-crack",
  ])
    assert.equal(GLOSSARY.find((term) => term.id === id)?.category, "status", id);
  assert.equal(GLOSSARY.filter((term) => term.category === "keyword").length, 12);
  assert.ok(GLOSSARY.every((term) => term.name.zh && term.name.en && term.description.zh && term.description.en));
});

test("tokenizer links Heavy Strike statuses and preserves surrounding text", () => {
  const segments = tokenizeGameText("造成6点伤害，使其获得3层易伤，失能3回合。", []);
  assert.deepEqual(
    segments.filter((entry) => entry.kind === "term").map((entry) => (entry.kind === "term" ? entry.term.id : "")),
    ["vulnerable", "disabled"],
  );
  assert.equal(segments.map((entry) => entry.text).join(""), "造成6点伤害，使其获得3层易伤，失能3回合。");
});

test("longest match wins and English aliases never match inside a word", () => {
  const segments = tokenizeGameText("支援线与 Unsupported Support support-line", []);
  const terms = segments.filter((entry) => entry.kind === "term");
  assert.equal(terms[0]?.kind === "term" ? terms[0].term.id : "", "support-line");
  assert.equal(
    terms.some((entry) => entry.kind === "term" && entry.text === "Support"),
    true,
  );
  assert.equal(segments.map((entry) => entry.text).join(""), "支援线与 Unsupported Support support-line");
  assert.equal(
    tokenizeGameText("Disablement", []).some((entry) => entry.kind === "term"),
    false,
  );
});

test("quoted localized card names become card links and outrank inner terms", () => {
  for (const text of ["获得「沉重打击」", "Gain “Heavy Strike”", 'Gain "Heavy Strike"']) {
    const segments = tokenizeGameText(text, [card]);
    const link = segments.find((entry) => entry.kind === "card");
    assert.equal(link?.kind === "card" ? link.card.id : "", "spell_heavy_strike");
    assert.equal(segments.map((entry) => entry.text).join(""), text);
  }
});
