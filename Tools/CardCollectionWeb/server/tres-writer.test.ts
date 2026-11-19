import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir, rm, writeFile, mkdir } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { rewriteCardPack, rewriteCardTags, rewriteThemeProfile } from "./tres-writer.ts";
import { ResourceTagService } from "./resource-service.ts";
import { parseTres, asNumber, asNumberArray, asNumberMap, asString, asStringArray } from "./tres.ts";
import { REPOSITORY_ROOT } from "./data.ts";
import { PACK_IDS, resolveDefaultPackId } from "../shared/packs.ts";

const CARD_FIXTURE = `[gd_resource type="Resource" load_steps=3 format=3]\r\n\r\n[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]\r\n[sub_resource type="Resource" id="fx"]\r\nValue = 6\r\nCustomProperty = "keep"\r\n\r\n[resource]\r\nscript = ExtResource("1_data")\r\nId = "test_minion"\r\nType = 0\r\nMechanicTags = 1\r\nAttack = 4\r\nHealth = 5\r\nSpeed = 3\r\nKeywords = [2, 8]\r\nEffects = [SubResource("fx")]\r\n`;

test("card writer preserves CRLF, unknown lines and subresources while editing only tags", () => {
  const output = rewriteCardTags(CARD_FIXTURE, { mechanicTags: 262145, keywords: [12], unitClass: 2 });
  assert.ok(output.includes("Type = 0\r\nUnitClass = 2\r\nMechanicTags = 262145\r\n"));
  assert.ok(output.includes("Keywords = Array[int]([12])\r\n"));
  assert.ok(!/(^|\r\n)Speed\s*=/.test(output));
  assert.ok(output.includes('[sub_resource type="Resource" id="fx"]\r\nValue = 6\r\nCustomProperty = "keep"'));
  assert.ok(output.includes("Attack = 4\r\nHealth = 5\r\n"));
  assert.equal(output.replace(/\r\n/g, "").includes("\n"), false);
});

test("card writer no-op preserves every byte and implicit Vanguard stays implicit", () => {
  const source = '[resource]\nId = "x"\nType = 0\nCost = 9\nCustomProperty = "untouched"';
  assert.equal(rewriteCardTags(source, { mechanicTags: 0, keywords: [], unitClass: 0 }), source);
  assert.equal(
    rewriteCardTags(CARD_FIXTURE.replace("Speed = 3\r\n", ""), {
      mechanicTags: 1,
      keywords: [2, 8],
    }),
    CARD_FIXTURE.replace("Speed = 3\r\n", ""),
  );
});

test("card writer removes a newly emptied keyword line and is idempotent", () => {
  const once = rewriteCardTags(CARD_FIXTURE, { mechanicTags: 0, keywords: [], unitClass: 0 });
  assert.ok(!/(^|\r\n)Keywords\s*=/.test(once));
  assert.ok(!/(^|\r\n)UnitClass\s*=/.test(once));
  const twice = rewriteCardTags(once, { mechanicTags: 0, keywords: [], unitClass: 0 });
  assert.equal(twice, once);
});

test("card writer rejects non-minion UnitClass and invalid fields", () => {
  const spell = '[resource]\nId = "spell"\nType = 1\nMechanicTags = 0\n';
  assert.equal(rewriteCardTags(spell, { mechanicTags: 0, keywords: [] }), spell);
  assert.throws(() => rewriteCardTags(spell, { mechanicTags: 0, keywords: [], unitClass: 2 }), /UnitClass/);
  assert.throws(() => rewriteCardTags(spell, { mechanicTags: 0, keywords: [99] }), /Keywords/);
});

test("card pack writer inserts PackId after Id preserving CRLF and untouched lines", () => {
  const source = `[gd_resource type="Resource" format=3]\r\n\r\n[resource]\r\nscript = ExtResource("1_data")\r\nId = "demo"\r\n\tType = 0\r\n\tMechanicTags = 1\r\nAttack = 4\r\n`;
  const output = rewriteCardPack(source, PACK_IDS.vanguard);
  assert.ok(output.includes('Id = "demo"\r\nPackId = "vanguard"\r\n\tType = 0\r\n'));
  assert.ok(output.includes("Attack = 4\r\n"));
  assert.equal((output.match(/\r\n/g) ?? []).length, (source.match(/\r\n/g) ?? []).length + 1);
  assert.equal(output.replace(/\r\n/g, "").includes("\n"), false);
  assert.equal(asString(parseTres(output).properties.PackId), "vanguard");
});

test("card pack writer updates an existing PackId in place, keeping its indentation and line ending", () => {
  const source = `[resource]\nId = "demo"\n\tPackId = "core"\nType = 1\nCustom = "keep"`;
  const output = rewriteCardPack(source, "my_diypack");
  assert.ok(output.includes('Id = "demo"\n\tPackId = "my_diypack"\nType = 1\nCustom = "keep"'));
  assert.equal(asString(parseTres(output).properties.PackId), "my_diypack");
});

test("card pack writer is byte-exact on a no-op and rejects malformed IDs", () => {
  const source = '[resource]\r\nId = "demo"\r\nPackId = "future_hunt"\r\n';
  assert.equal(rewriteCardPack(source, "future_hunt"), source);
  assert.throws(() => rewriteCardPack(source, ""), /PackId/);
  assert.throws(() => rewriteCardPack(source, 'bad"id'), /PackId/);
  assert.throws(() => rewriteCardPack(source, "has space"), /PackId/);
  assert.throws(() => rewriteCardPack(source, 42 as unknown as string), /PackId/);
});

test("card tag writer can write tags and PackId in the same lossless pass", () => {
  const output = rewriteCardTags(CARD_FIXTURE, {
    mechanicTags: 262145,
    keywords: [12],
    unitClass: 2,
    packId: "unmanned_victory",
  });
  assert.ok(output.includes('PackId = "unmanned_victory"\r\n'));
  assert.ok(output.includes("MechanicTags = 262145\r\n"));
  assert.ok(output.includes("Keywords = Array[int]([12])\r\n"));
  assert.equal(asString(parseTres(output).properties.PackId), "unmanned_victory");
  assert.equal(output.replace(/\r\n/g, "").includes("\n"), false);
});

test("default pack mapping follows the card pack draft, with explicit lists beating tag heuristics", () => {
  assert.equal(resolveDefaultPackId("minion_what_the_dog_doing"), PACK_IDS.unmannedVictory);
  assert.equal(resolveDefaultPackId("minion_mingshan_walking_support", ["mechanics"]), PACK_IDS.unmannedVictory);
  assert.equal(resolveDefaultPackId("minion_40a_main_battle_tank", ["mechanics"]), PACK_IDS.unmannedVictory);
  assert.equal(resolveDefaultPackId("minion_roach"), PACK_IDS.futureHunt);
  assert.equal(resolveDefaultPackId("minion_centurion"), PACK_IDS.futureHunt);
  assert.equal(resolveDefaultPackId("some_new_mech", ["mechanics"]), PACK_IDS.futureHunt);
  assert.equal(resolveDefaultPackId("domain_preemptive_strike"), PACK_IDS.superBodyProject);
  assert.equal(resolveDefaultPackId("spell_strike"), PACK_IDS.core);
});

test("theme writer inserts and updates dictionaries without changing other properties or LF", () => {
  const source = `[gd_resource type="Resource" format=3]\n[resource]\nHeroId = "test"\nTagWeights = {}\nCoreCardIds = PackedStringArray("a")\nCardWeightOverrides = {}\nCustomProperty = "keep"`;
  const output = rewriteThemeProfile(source, {
    tagWeights: { 8: 3, 1: -2 },
    keywordWeights: { 12: 4 },
    coreCardIds: ["a", "b"],
  });
  assert.ok(
    output.includes(
      'TagWeights = {\n\t1: -2,\n\t8: 3,\n}\nKeywordWeights = {\n\t12: 4,\n}\nCoreCardIds = PackedStringArray("a", "b")',
    ),
  );
  assert.ok(output.endsWith('CardWeightOverrides = {}\nCustomProperty = "keep"'));
  assert.ok(!output.includes("\r"));
  assert.equal(
    rewriteThemeProfile(output, {
      tagWeights: { 8: 3, 1: -2 },
      keywordWeights: { 12: 4 },
      coreCardIds: ["a", "b"],
    }),
    output,
  );
});

test("all real card and theme resources round-trip byte-for-byte without writes", async () => {
  for (const [directory, theme] of [
    ["Cards", false],
    ["Themes", true],
  ] as const) {
    const root = path.join(REPOSITORY_ROOT, "Resources", directory);
    for (const file of (await readdir(root)).filter((entry) => entry.endsWith(".tres"))) {
      const source = await readFile(path.join(root, file), "utf8");
      const { properties } = parseTres(source);
      const output = theme
        ? rewriteThemeProfile(source, {
            tagWeights: asNumberMap(properties.TagWeights),
            keywordWeights: asNumberMap(properties.KeywordWeights),
            coreCardIds: asStringArray(properties.CoreCardIds),
          })
        : rewriteCardTags(source, {
            mechanicTags: asNumber(properties.MechanicTags),
            keywords: asNumberArray(properties.Keywords),
            ...(asNumber(properties.Type) === 0 && properties.UnitClass !== undefined
              ? { unitClass: asNumber(properties.UnitClass) }
              : {}),
          });
      assert.equal(output, source, file);
    }
  }
});

test("temporary repository service saves twice identically and validates malformed resources", async () => {
  const root = await mkdtemp(path.join(tmpdir(), "odyssey-tags-"));
  try {
    await mkdir(path.join(root, "Resources", "Cards"), { recursive: true });
    await mkdir(path.join(root, "Resources", "Themes"), { recursive: true });
    await writeFile(path.join(root, "project.godot"), "; fixture\n");
    const cardPath = path.join(root, "Resources", "Cards", "Fixture.tres");
    await writeFile(
      path.join(root, "Resources", "Themes", "ThemeProfile_Test.tres"),
      '[resource]\nHeroId = "test"\nTagWeights = {}\nCoreCardIds = PackedStringArray("test_minion")\n',
    );
    // Write the fixture only inside the system temporary directory.
    await writeFile(cardPath, CARD_FIXTURE);
    const service = new ResourceTagService(root);
    await service.saveCard("test_minion", { mechanicTags: 262144, keywords: [12], unitClass: 3 });
    const first = await readFile(cardPath, "utf8");
    await service.saveCard("test_minion", { mechanicTags: 262144, keywords: [12], unitClass: 3 });
    assert.equal(await readFile(cardPath, "utf8"), first);
    assert.equal(service.validate().ok, true);
    await writeFile(cardPath, first.replace("MechanicTags = 262144", "MechanicTags = 999999"));
    const report = service.validate();
    assert.equal(report.ok, false);
    assert.ok(report.errors.some((error) => error.includes("MechanicTags")));
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("batch assignPack writes PackId to every selected card and leaves balance fields untouched", async () => {
  const root = await mkdtemp(path.join(tmpdir(), "odyssey-packs-"));
  try {
    await mkdir(path.join(root, "Resources", "Cards"), { recursive: true });
    await mkdir(path.join(root, "Resources", "Themes"), { recursive: true });
    await writeFile(path.join(root, "project.godot"), "; fixture\n");
    const cardDir = path.join(root, "Resources", "Cards");
    const alpha = path.join(cardDir, "Alpha.tres");
    const beta = path.join(cardDir, "Beta.tres");
    await writeFile(alpha, '[resource]\nId = "alpha"\nType = 0\nCost = 7\nAttack = 9\nMechanicTags = 4096\n');
    await writeFile(beta, '[resource]\r\nId = "beta"\r\nType = 1\r\nCost = 3\r\nMechanicTags = 2\r\n');
    const service = new ResourceTagService(root);
    const result = await service.assignPack(["alpha", "beta", "ghost"], "future_hunt");
    assert.deepEqual(result.updated, ["alpha", "beta"]);
    assert.deepEqual(result.missing, ["ghost"]);
    const alphaOut = await readFile(alpha, "utf8");
    const betaOut = await readFile(beta, "utf8");
    assert.ok(alphaOut.includes('Id = "alpha"\nPackId = "future_hunt"\nType = 0\nCost = 7\nAttack = 9\n'));
    assert.ok(betaOut.includes('Id = "beta"\r\nPackId = "future_hunt"\r\nType = 1\r\nCost = 3\r\n'));
    assert.equal(asNumber(parseTres(betaOut).properties.Cost), 3);
    assert.equal(service.validate().ok, true);
    // 重复指派同包是幂等的，字节不再变化。
    await service.assignPack(["alpha", "beta"], "future_hunt");
    assert.equal(await readFile(alpha, "utf8"), alphaOut);
    assert.equal(await readFile(beta, "utf8"), betaOut);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
