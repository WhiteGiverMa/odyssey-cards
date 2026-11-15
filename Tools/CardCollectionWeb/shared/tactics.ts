import type { UnitClass } from "./types.ts";

export type BoardRow = "enemySupport" | "frontline" | "playerSupport";
export type TargetReason = "legal" | "smoke" | "meleeBlocked" | "skirmisherProtected" | "taunt" | "guard" | "escort";

export interface BoardUnit {
  id: string;
  side: "player" | "enemy";
  row: BoardRow;
  slot: number;
  unitClass: UnitClass;
  taunt?: boolean;
  guard?: boolean;
  smoke?: boolean;
  disabled?: boolean;
}

export interface TargetResult {
  id: string;
  legal: boolean;
  reason: TargetReason;
  interceptors: string[];
}

export const HQ_ID = "enemy-hq";

function isMelee(unitClass: UnitClass): boolean {
  return unitClass === "vanguard" || unitClass === "assault";
}

interface HqTarget {
  id: typeof HQ_ID;
  row: "enemySupport";
}
const HQ: HqTarget = { id: HQ_ID, row: "enemySupport" };

function isHq(target: BoardUnit | HqTarget): target is HqTarget {
  return target.id === HQ_ID;
}

function baseReachable(attacker: BoardUnit, target: BoardUnit | HqTarget): boolean {
  if (!isHq(target)) {
    if (target.smoke) return false;
    if (isMelee(attacker.unitClass) && target.unitClass === "skirmisher" && target.row === "enemySupport") return false;
  }
  if (!isMelee(attacker.unitClass)) return true;
  if (attacker.row === "frontline") return true;
  return target.row === "frontline";
}

/** PRD §4.1: base reachability → Taunt → adjacent Guard → Skirmisher escort. */
export function evaluateTargets(attacker: BoardUnit, allUnits: BoardUnit[]): TargetResult[] {
  const enemies = allUnits.filter((unit) => unit.side === "enemy" && unit.id !== attacker.id);
  const reachable = new Map<string, boolean>(enemies.map((unit) => [unit.id, baseReachable(attacker, unit)]));
  const tauntsAnywhere = enemies.filter((unit) => unit.taunt && reachable.get(unit.id));
  const output: TargetResult[] = [];

  for (const target of [...enemies, HQ] as Array<BoardUnit | HqTarget>) {
    const targetId = target.id;
    if (!isHq(target) && !reachable.get(targetId)) {
      const reason: TargetReason = target.smoke
        ? "smoke"
        : isMelee(attacker.unitClass) && target.unitClass === "skirmisher" && target.row === "enemySupport"
          ? "skirmisherProtected"
          : "meleeBlocked";
      output.push({ id: targetId, legal: false, reason, interceptors: [] });
      continue;
    }
    if (isHq(target) && !baseReachable(attacker, HQ)) {
      output.push({ id: targetId, legal: false, reason: "meleeBlocked", interceptors: [] });
      continue;
    }
    const rowTaunts = isHq(target)
      ? tauntsAnywhere
      : enemies.filter((unit) => unit.row === target.row && unit.taunt && reachable.get(unit.id));
    if (rowTaunts.length && (isHq(target) || !("taunt" in target) || !target.taunt)) {
      output.push({ id: targetId, legal: false, reason: "taunt", interceptors: rowTaunts.map((unit) => unit.id) });
      continue;
    }
    if (isHq(target)) {
      if (attacker.unitClass === "skirmisher") {
        const escorts = enemies.filter(
          (unit) =>
            unit.row === "enemySupport" &&
            unit.unitClass === "skirmisher" &&
            !unit.smoke &&
            !unit.disabled &&
            reachable.get(unit.id),
        );
        if (escorts.length)
          output.push({ id: targetId, legal: false, reason: "escort", interceptors: escorts.map((unit) => unit.id) });
        else output.push({ id: targetId, legal: true, reason: "legal", interceptors: [] });
      } else output.push({ id: targetId, legal: true, reason: "legal", interceptors: [] });
      continue;
    }

    const adjacentGuards =
      !target.taunt && !target.guard
        ? enemies.filter(
            (unit) =>
              unit.row === target.row &&
              unit.guard &&
              Math.abs(unit.slot - target.slot) === 1 &&
              reachable.get(unit.id),
          )
        : [];
    if (adjacentGuards.length) {
      output.push({ id: targetId, legal: false, reason: "guard", interceptors: adjacentGuards.map((unit) => unit.id) });
      continue;
    }
    if (attacker.unitClass === "skirmisher" && target.unitClass !== "skirmisher" && !target.taunt) {
      const escorts = enemies.filter(
        (unit) =>
          unit.row === target.row &&
          unit.unitClass === "skirmisher" &&
          !unit.smoke &&
          !unit.disabled &&
          reachable.get(unit.id),
      );
      const isProtectingGuard = target.guard && escorts.some((escort) => Math.abs(escort.slot - target.slot) === 1);
      if (escorts.length && !isProtectingGuard) {
        output.push({ id: targetId, legal: false, reason: "escort", interceptors: escorts.map((unit) => unit.id) });
        continue;
      }
    }
    output.push({ id: targetId, legal: true, reason: "legal", interceptors: [] });
  }
  return output;
}
