import { useMemo, useState } from "react";
import type { Language, RulesResponse, UnitClass } from "../../shared/types.ts";
import { UNIT_CLASSES } from "../../shared/constants.ts";
import { evaluateTargets, HQ_ID, type BoardRow, type BoardUnit, type TargetReason } from "../../shared/tactics.ts";
import { t } from "../i18n.ts";
import { UnitBadge } from "./UnitBadge.tsx";

type EditorMode = "select" | "player" | "enemy" | "erase";

const SCENARIOS: Record<string, BoardUnit[]> = {
  escort: [
    { id: "P-01", side: "player", row: "frontline", slot: 2, unitClass: "skirmisher" },
    { id: "E-G", side: "enemy", row: "enemySupport", slot: 1, unitClass: "vanguard", guard: true },
    { id: "E-A", side: "enemy", row: "enemySupport", slot: 2, unitClass: "skirmisher" },
    { id: "E-B", side: "enemy", row: "enemySupport", slot: 3, unitClass: "support" },
  ],
  taunt: [
    { id: "P-01", side: "player", row: "frontline", slot: 2, unitClass: "skirmisher" },
    { id: "E-T", side: "enemy", row: "enemySupport", slot: 0, unitClass: "assault", taunt: true },
    { id: "E-G", side: "enemy", row: "enemySupport", slot: 1, unitClass: "vanguard", guard: true },
    { id: "E-A", side: "enemy", row: "enemySupport", slot: 2, unitClass: "skirmisher" },
    { id: "E-B", side: "enemy", row: "enemySupport", slot: 3, unitClass: "support" },
  ],
  melee: [
    { id: "P-V", side: "player", row: "playerSupport", slot: 1, unitClass: "vanguard" },
    { id: "E-T", side: "enemy", row: "frontline", slot: 2, unitClass: "assault", taunt: true },
    { id: "E-B", side: "enemy", row: "enemySupport", slot: 1, unitClass: "support" },
    { id: "E-A", side: "enemy", row: "enemySupport", slot: 3, unitClass: "skirmisher" },
  ],
  smoke: [
    { id: "P-S", side: "player", row: "frontline", slot: 2, unitClass: "support" },
    { id: "E-T", side: "enemy", row: "enemySupport", slot: 0, unitClass: "assault", taunt: true },
    { id: "E-S", side: "enemy", row: "enemySupport", slot: 2, unitClass: "support", smoke: true },
    { id: "E-A", side: "enemy", row: "enemySupport", slot: 3, unitClass: "skirmisher" },
  ],
};

function reasonKey(reason: TargetReason): string {
  return reason === "legal"
    ? "reasonLegal"
    : reason === "smoke"
      ? "reasonSmoke"
      : reason === "meleeBlocked"
        ? "reasonMeleeBlocked"
        : reason === "skirmisherProtected"
          ? "reasonSkirmisherProtected"
          : reason === "taunt"
            ? "reasonTaunt"
            : reason === "guard"
              ? "reasonGuard"
              : "reasonEscort";
}

export function Wiki({ language, rules }: { language: Language; rules: RulesResponse }) {
  const [units, setUnits] = useState<BoardUnit[]>(SCENARIOS.escort ?? []);
  const [scenario, setScenario] = useState("escort");
  const [attackerId, setAttackerId] = useState("P-01");
  const [focusTarget, setFocusTarget] = useState("E-G");
  const [mode, setMode] = useState<EditorMode>("select");
  const [placementClass, setPlacementClass] = useState<UnitClass>("vanguard");
  const [flags, setFlags] = useState({ taunt: false, guard: false, smoke: false, disabled: false });
  const [boardMessage, setBoardMessage] = useState("");
  const [intel, setIntel] = useState(3);
  const [hiddenCards, setHiddenCards] = useState(1);
  const attacker = units.find((unit) => unit.id === attackerId && unit.side === "player");
  const targets = useMemo(() => (attacker ? evaluateTargets(attacker, units) : []), [attacker, units]);
  const selectedTarget = targets.find((target) => target.id === focusTarget);
  const frontlineOwner = units.some((unit) => unit.row === "frontline" && unit.side === "player")
    ? "player"
    : units.some((unit) => unit.row === "frontline" && unit.side === "enemy")
      ? "enemy"
      : "none";
  const revealed = Math.min(intel, hiddenCards);
  const overflow = intel - revealed;

  const resetScenario = (id: string) => {
    setScenario(id);
    setUnits((SCENARIOS[id] ?? []).map((unit) => ({ ...unit })));
    setAttackerId(SCENARIOS[id]?.find((unit) => unit.side === "player")?.id ?? "");
    setFocusTarget(id === "melee" ? "E-B" : id === "smoke" ? "E-S" : id === "taunt" ? "E-G" : "E-G");
    setMode("select");
    setBoardMessage("");
  };
  const removeAt = (row: BoardRow, slot: number) => {
    const removed = units.find((unit) => unit.row === row && unit.slot === slot);
    setUnits(units.filter((unit) => !(unit.row === row && unit.slot === slot)));
    if (removed?.id === attackerId) setAttackerId("");
    if (removed?.id === focusTarget) setFocusTarget(HQ_ID);
  };
  const clickSlot = (row: BoardRow, slot: number) => {
    const existing = units.find((unit) => unit.row === row && unit.slot === slot);
    setBoardMessage("");
    if (mode === "erase") {
      removeAt(row, slot);
      return;
    }
    if (mode === "select") {
      if (existing?.side === "player") setAttackerId(existing.id);
      else if (existing?.side === "enemy") setFocusTarget(existing.id);
      return;
    }
    const side = mode;
    if ((row === "playerSupport" && side !== "player") || (row === "enemySupport" && side !== "enemy")) {
      setBoardMessage(language === "zh" ? "该阵线只能部署对应阵营的单位。" : "This row only accepts its own faction.");
      return;
    }
    if (row === "frontline" && units.some((unit) => unit.row === "frontline" && unit.side !== side)) {
      setBoardMessage(
        language === "zh"
          ? "前线正由敌方占领，请先拆除对方单位。"
          : "The enemy holds frontline. Remove opposing units first.",
      );
      return;
    }
    const serial = units.filter((unit) => unit.side === side).length + 1;
    const unit: BoardUnit = {
      id: existing?.id ?? `${side === "player" ? "P" : "E"}-${serial}-${Date.now() % 97}`,
      side,
      row,
      slot,
      unitClass: placementClass,
      ...flags,
    };
    if (unit.smoke) {
      unit.taunt = false;
      unit.guard = false;
    }
    setUnits([...units.filter((entry) => !(entry.row === row && entry.slot === slot)), unit]);
    if (side === "player") setAttackerId(unit.id);
    else setFocusTarget(unit.id);
  };
  const changeAttackerClass = (unitClass: UnitClass) => {
    if (!attacker) return;
    setUnits(units.map((unit) => (unit.id === attacker.id ? { ...unit, unitClass } : unit)));
  };
  const renderRow = (row: BoardRow, capacity: number) => (
    <div className={`tactical-row ${row}`} style={{ "--capacity": capacity } as React.CSSProperties}>
      {Array.from({ length: capacity }, (_, slot) => {
        const unit = units.find((entry) => entry.row === row && entry.slot === slot);
        const result = unit?.side === "enemy" ? targets.find((target) => target.id === unit.id) : undefined;
        return (
          <button
            key={slot}
            className={`board-slot ${unit ? "occupied" : "empty"} ${unit?.side ?? ""} ${result?.legal ? "legal" : result ? "blocked" : ""} ${unit?.id === attackerId ? "attacker" : ""} ${unit?.id === focusTarget ? "focused" : ""}`}
            onClick={() => clickSlot(row, slot)}
            onContextMenu={(event) => {
              event.preventDefault();
              removeAt(row, slot);
            }}
            aria-label={`${row} ${slot + 1}${unit ? ` ${unit.unitClass}` : " empty"}`}
          >
            <span className="slot-coordinate">
              {row === "frontline" ? "F" : row === "enemySupport" ? "E" : "P"}
              {slot + 1}
            </span>
            {unit ? (
              <>
                <UnitBadge unitClass={unit.unitClass} language={language} size={43} />
                <strong>{unit.id}</strong>
                <div className="unit-status-flags">
                  {unit.taunt && <span>T</span>}
                  {unit.guard && <span>G</span>}
                  {unit.smoke && <span>S</span>}
                  {unit.disabled && <span>D</span>}
                </div>
                {result && <i className="target-state">{result.legal ? "✓" : "×"}</i>}
              </>
            ) : (
              <span className="empty-slot-cross">+</span>
            )}
          </button>
        );
      })}
    </div>
  );

  return (
    <div className="wiki-page page-container">
      <header className="page-title-block">
        <div>
          <span className="section-index">04 / FIELD MANUAL</span>
          <h1>{t(language, "wikiTitle")}</h1>
          <p>{t(language, "wikiSubtitle")}</p>
        </div>
        <div className="title-technical">
          ENEMY 04
          <br />
          FRONTLINE 05 · FRIENDLY 04
        </div>
      </header>
      <div className="draft-notice">
        <span>DRAFT</span>
        {t(language, "draftNotice")}
      </div>
      <section className="sandbox-section">
        <header className="section-heading">
          <div>
            <span className="section-index">SIM / TARGETING</span>
            <h2>{t(language, "sandbox")}</h2>
          </div>
          <div className="target-legend">
            <span className="legal">◆ {t(language, "legalTarget")}</span>
            <span className="blocked">◇ {t(language, "blockedTarget")}</span>
          </div>
        </header>
        <div className="scenario-bar">
          <span>{t(language, "scenario")}</span>
          {["escort", "taunt", "melee", "smoke"].map((id) => (
            <button className={scenario === id ? "active" : ""} onClick={() => resetScenario(id)} key={id}>
              {t(language, `${id}Scenario`)}
            </button>
          ))}
        </div>
        <div className="sandbox-toolbar">
          <div className="editor-modes">
            {(["select", "player", "enemy", "erase"] as EditorMode[]).map((entry) => (
              <button key={entry} className={mode === entry ? "active" : ""} onClick={() => setMode(entry)}>
                {t(
                  language,
                  entry === "select"
                    ? "selectAttacker"
                    : entry === "player"
                      ? "placePlayer"
                      : entry === "enemy"
                        ? "placeEnemy"
                        : "erase",
                )}
              </button>
            ))}
          </div>
          {mode === "select" && attacker ? (
            <label>
              {t(language, "attackerClass")}
              <select
                value={attacker.unitClass}
                onChange={(event) => changeAttackerClass(event.target.value as UnitClass)}
              >
                {UNIT_CLASSES.map((unitClass) => (
                  <option key={unitClass} value={unitClass}>
                    {rules.unitClasses.find((rule) => rule.id === unitClass)?.name[language]}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            (mode === "player" || mode === "enemy") && (
              <label>
                {t(language, "selectedClass")}
                <select value={placementClass} onChange={(event) => setPlacementClass(event.target.value as UnitClass)}>
                  {UNIT_CLASSES.map((unitClass) => (
                    <option key={unitClass} value={unitClass}>
                      {rules.unitClasses.find((rule) => rule.id === unitClass)?.name[language]}
                    </option>
                  ))}
                </select>
              </label>
            )
          )}
        </div>
        {(mode === "player" || mode === "enemy") && (
          <div className="placement-flags">
            {(["taunt", "guard", "smoke", "disabled"] as const).map((flag) => (
              <label key={flag}>
                <input
                  type="checkbox"
                  checked={flags[flag]}
                  onChange={(event) =>
                    setFlags({
                      ...flags,
                      [flag]: event.target.checked,
                      ...(flag === "smoke" && event.target.checked
                        ? { taunt: false, guard: false }
                        : (flag === "taunt" || flag === "guard") && event.target.checked
                          ? { smoke: false }
                          : {}),
                    })
                  }
                />
                {flag.toUpperCase()}
              </label>
            ))}
          </div>
        )}
        <p className="battlefield-hint">{t(language, "battlefieldHint")}</p>
        {boardMessage && <div className="validity-hint error">{boardMessage}</div>}
        <div className="sandbox-layout">
          <div className="tactical-board-wrap">
            <button
              className={`enemy-hq ${targets.find((target) => target.id === HQ_ID)?.legal ? "legal" : "blocked"} ${focusTarget === HQ_ID ? "focused" : ""}`}
              onClick={() => setFocusTarget(HQ_ID)}
            >
              <span>HQ</span>
              <strong>{t(language, "enemyHq")}</strong>
              <small>{targets.find((target) => target.id === HQ_ID)?.legal ? "✓ TARGET" : "× INTERCEPTED"}</small>
            </button>
            <div className="board-row-label">
              <span>{t(language, "enemySupport")}</span>
              <small>ENEMY / 04</small>
            </div>
            {renderRow("enemySupport", 4)}
            <div className="frontline-divider">
              <span />
              <div>
                {t(language, "sharedFrontline")} <b className={frontlineOwner}>{t(language, frontlineOwner)}</b>
              </div>
              <span />
            </div>
            {renderRow("frontline", 5)}
            <div className="board-row-label friendly">
              <span>{t(language, "playerSupport")}</span>
              <small>FRIENDLY / 04</small>
            </div>
            {renderRow("playerSupport", 4)}
            <div className="player-hq">
              <span>HQ</span>
              <strong>{language === "zh" ? "我方总部" : "Friendly HQ"}</strong>
              <small>INDEPENDENT SLOT</small>
            </div>
          </div>
          <aside className="target-analysis">
            <header>
              <span className="section-index">LIVE / EVAL</span>
              <h3>{t(language, "reason")}</h3>
            </header>
            {attacker ? (
              <div className="attacker-readout">
                <UnitBadge unitClass={attacker.unitClass} language={language} size={36} />
                <div>
                  <small>{t(language, "selected")}</small>
                  <strong>
                    {attacker.id} · {rules.unitClasses.find((rule) => rule.id === attacker.unitClass)?.name[language]}
                  </strong>
                  <span>
                    {attacker.row} / {attacker.slot + 1}
                  </span>
                </div>
              </div>
            ) : (
              <p>{t(language, "noAttacker")}</p>
            )}
            {selectedTarget && (
              <div className={`reason-readout ${selectedTarget.legal ? "legal" : "blocked"}`}>
                <span>{selectedTarget.legal ? "✓" : "×"}</span>
                <div>
                  <strong>
                    {selectedTarget.id} · {t(language, selectedTarget.legal ? "legal" : "blockedTarget")}
                  </strong>
                  <p>{t(language, reasonKey(selectedTarget.reason))}</p>
                  {selectedTarget.interceptors.length > 0 && (
                    <small>INTERCEPTORS: {selectedTarget.interceptors.join(", ")}</small>
                  )}
                </div>
              </div>
            )}
            <div className="target-list">
              {targets.map((target) => (
                <button
                  key={target.id}
                  className={`${target.legal ? "legal" : "blocked"} ${focusTarget === target.id ? "active" : ""}`}
                  onClick={() => setFocusTarget(target.id)}
                >
                  <span>{target.id}</span>
                  <small>{target.legal ? "✓" : target.reason.toUpperCase()}</small>
                </button>
              ))}
            </div>
          </aside>
        </div>
      </section>
      <section className="priority-section">
        <header className="section-heading">
          <div>
            <span className="section-index">RULE / PRIORITY</span>
            <h2>{t(language, "priority")}</h2>
          </div>
          <p>BASE → TAUNT → GUARD → ESCORT</p>
        </header>
        <div className="priority-grid">
          {rules.priority.map((rule, index) => (
            <article key={index}>
              <span>0{index + 1}</span>
              <p>{rule[language]}</p>
            </article>
          ))}
        </div>
      </section>
      <section className="doctrine-section">
        <header className="section-heading">
          <div>
            <span className="section-index">UNIT / DOCTRINE</span>
            <h2>{t(language, "unitDoctrine")}</h2>
          </div>
          <p>MOVE / STRIKE / SCREEN</p>
        </header>
        <div className="doctrine-grid">
          {rules.unitClasses.map((rule, index) => (
            <article key={rule.id}>
              <header>
                <UnitBadge unitClass={rule.id} language={language} size={44} />
                <div>
                  <span>0{index + 1}</span>
                  <h3>{rule.name[language]}</h3>
                </div>
              </header>
              <dl>
                <div>
                  <dt>ACTION</dt>
                  <dd>{rule.actions[language]}</dd>
                </div>
                <div>
                  <dt>RANGE</dt>
                  <dd>{rule.range[language]}</dd>
                </div>
              </dl>
              <p>{rule.trait[language]}</p>
            </article>
          ))}
        </div>
      </section>
      <section className="intel-section">
        <header className="section-heading">
          <div>
            <span className="section-index">INT / OVERFLOW</span>
            <h2>{t(language, "intelLab")}</h2>
          </div>
          <p>REVEAL K → OVERFLOW N−K</p>
        </header>
        <div className="intel-layout">
          <div className="intel-controls">
            <label>
              <span>{t(language, "intelAmount")}</span>
              <output>{intel}</output>
              <input
                type="range"
                min="0"
                max="6"
                value={intel}
                onChange={(event) => setIntel(Number(event.target.value))}
              />
            </label>
            <label>
              <span>{t(language, "hiddenCards")}</span>
              <output>{hiddenCards}</output>
              <input
                type="range"
                min="0"
                max="6"
                value={hiddenCards}
                onChange={(event) => setHiddenCards(Number(event.target.value))}
              />
            </label>
            <p>{rules.intel.reveal[language]}</p>
          </div>
          <div className="intel-equation">
            <div>
              <small>INTEL</small>
              <strong>{intel}</strong>
            </div>
            <span>−</span>
            <div>
              <small>{t(language, "revealed")}</small>
              <strong>{revealed}</strong>
            </div>
            <span>=</span>
            <div className="overflow-result">
              <small>{t(language, "overflow")}</small>
              <strong>{overflow}</strong>
            </div>
            <span>→</span>
            <div className="heal-result">
              <small>{t(language, "healing")}</small>
              <strong>+{overflow}</strong>
            </div>
          </div>
          <div className="intel-rules">
            <p>{rules.intel.overflow[language]}</p>
            <p>{rules.intel.conversion[language]}</p>
            <small>{t(language, "intelNote")}</small>
          </div>
        </div>
      </section>
    </div>
  );
}
