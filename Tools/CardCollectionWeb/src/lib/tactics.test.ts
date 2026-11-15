import assert from "node:assert/strict";
import test from "node:test";
import { evaluateTargets, HQ_ID, type BoardUnit } from "../../shared/tactics.ts";

const attacker: BoardUnit = { id: "attacker", side: "player", row: "frontline", slot: 2, unitClass: "skirmisher" };
const guard: BoardUnit = {
  id: "guard",
  side: "enemy",
  row: "enemySupport",
  slot: 1,
  unitClass: "vanguard",
  guard: true,
};
const escort: BoardUnit = { id: "escort", side: "enemy", row: "enemySupport", slot: 2, unitClass: "skirmisher" };
const backline: BoardUnit = { id: "backline", side: "enemy", row: "enemySupport", slot: 3, unitClass: "support" };

test("Guard exposes itself so Skirmisher escort cannot deadlock", () => {
  const targets = evaluateTargets(attacker, [attacker, guard, escort, backline]);
  assert.equal(targets.find((target) => target.id === "guard")?.legal, true);
  assert.equal(targets.find((target) => target.id === "escort")?.reason, "guard");
  assert.equal(targets.find((target) => target.id === "backline")?.reason, "escort");
  assert.equal(targets.find((target) => target.id === HQ_ID)?.reason, "escort");
});

test("Taunt overrides Guard and escort priority", () => {
  const taunt: BoardUnit = { ...backline, id: "taunt", taunt: true };
  const targets = evaluateTargets(attacker, [attacker, guard, escort, taunt]);
  assert.equal(targets.find((target) => target.id === "guard")?.reason, "taunt");
  assert.equal(targets.find((target) => target.id === "taunt")?.legal, true);
});

test("Support-row Skirmisher is immune to melee but not global fire", () => {
  const melee: BoardUnit = { ...attacker, unitClass: "vanguard" };
  assert.equal(
    evaluateTargets(melee, [melee, escort]).find((target) => target.id === "escort")?.reason,
    "skirmisherProtected",
  );
  assert.equal(evaluateTargets(attacker, [attacker, escort]).find((target) => target.id === "escort")?.legal, true);
});

test("Smoke prevents every normal attack", () => {
  const smoked: BoardUnit = { ...backline, smoke: true };
  assert.equal(
    evaluateTargets(attacker, [attacker, smoked]).find((target) => target.id === "backline")?.reason,
    "smoke",
  );
});
