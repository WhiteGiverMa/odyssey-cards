import { useEffect, useRef } from "react";
import type { CardRecord, Language, RulesResponse } from "../../shared/types.ts";
import {
  CLASS_COLORS,
  KEYWORD_NAMES,
  MECHANIC_NAMES,
  RARITY_COLORS,
  RARITY_NAMES,
  TYPE_NAMES,
  UNIT_NAMES,
} from "../../shared/constants.ts";
import { t } from "../i18n.ts";
import { CardView } from "./CardView.tsx";
import { UnitBadge } from "./UnitBadge.tsx";

interface InspectorProps {
  card: CardRecord;
  cards: CardRecord[];
  language: Language;
  palette: "classic" | "new";
  rules: RulesResponse;
  deckCount: number;
  onClose: () => void;
  onSelect: (id: string) => void;
  onAdd: (id: string) => void;
}

export function Inspector({
  card,
  cards,
  language,
  palette,
  rules,
  deckCount,
  onClose,
  onSelect,
  onAdd,
}: InspectorProps) {
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    document.body.classList.add("modal-open");
    closeRef.current?.focus();
    const keydown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", keydown);
    return () => {
      document.body.classList.remove("modal-open");
      window.removeEventListener("keydown", keydown);
      previous?.focus();
    };
  }, [onClose]);
  const related = card.relatedCardIds
    .map((id) => cards.find((entry) => entry.id === id))
    .filter((entry): entry is CardRecord => Boolean(entry));
  return (
    <div
      className="inspector-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section className="inspector" role="dialog" aria-modal="true" aria-labelledby="inspector-title">
        <header className="inspector-topbar">
          <div>
            <span className="live-dot" /> CARD INSPECTION <span className="muted">// {card.fileName}</span>
          </div>
          <button ref={closeRef} className="icon-button" onClick={onClose} aria-label={t(language, "close")}>
            ×
          </button>
        </header>
        <div className="inspector-layout">
          <div className="inspector-card-column">
            <CardView card={card} language={language} palette={palette} onInspect={onSelect} onAdd={onAdd} large />
            <button
              className="primary-button inspector-add"
              disabled={!card.buildable || deckCount >= card.maxCopies}
              onClick={() => onAdd(card.id)}
            >
              {card.buildable ? `${t(language, "add")} · ${deckCount}/${card.maxCopies}` : t(language, "unbuildable")}
            </button>
          </div>
          <div className="inspector-content">
            <div className="inspector-heading">
              <div className="rarity-large" style={{ color: RARITY_COLORS[palette][card.rarity] }}>
                {String(card.rarity).padStart(2, "0")} <span>{RARITY_NAMES[card.rarity]?.[language]}</span>
              </div>
              <h2 id="inspector-title">{card.name[language]}</h2>
              <p className="card-id-line">{card.id}</p>
              <div className="tag-row">
                <span className="data-tag accent">{TYPE_NAMES[card.typeKey][language]}</span>
                <span className="data-tag">
                  {t(language, "deployCost")} {card.cost}
                </span>
                {card.typeKey === "minion" && (
                  <span className="data-tag oil">
                    {t(language, "actionCost")} {card.actionCost}
                  </span>
                )}
                {card.unitClass && (
                  <span className="data-tag" style={{ borderColor: CLASS_COLORS[card.unitClass] }}>
                    <UnitBadge unitClass={card.unitClass} language={language} size={22} />
                    {UNIT_NAMES[card.unitClass][language]}
                  </span>
                )}
              </div>
            </div>
            <section className="inspector-section card-full-text">
              <h3>{t(language, "cardText")}</h3>
              <p>{card.description[language]}</p>
              {language === "en" && card.localizationFallback.en && (
                <small className="fallback-note">{t(language, "localizationFallback")}</small>
              )}
            </section>
            <div className="inspector-two-col">
              <section className="inspector-section">
                <h3>{t(language, "keywords")}</h3>
                <div className="keyword-glossary">
                  {card.keywordKeys
                    .filter((key) => key !== "none")
                    .map((key) => {
                      const rule = rules.keywords.find((entry) => entry.id === key);
                      return (
                        <div key={key}>
                          <strong>{KEYWORD_NAMES[key]?.[language] ?? key}</strong>
                          <p>{rule?.description[language] ?? t(language, "rawResource")}</p>
                        </div>
                      );
                    })}
                  {!card.keywordKeys.length && <span className="muted">—</span>}
                </div>
              </section>
              <section className="inspector-section">
                <h3>{t(language, "mechanics")}</h3>
                <div className="tag-row">
                  {card.mechanicTagKeys.map((key) => (
                    <span className="data-tag" key={key}>
                      {MECHANIC_NAMES[key]?.[language] ?? key}
                    </span>
                  ))}
                </div>
                {card.tactical && (
                  <div className="tactical-note">
                    <small>{t(language, "statusPlanned")}</small>
                    <strong>{card.tactical.role[language]}</strong>
                    <p>{card.tactical.note[language]}</p>
                    <dl>
                      <div>
                        <dt>{t(language, "recommendedLine")}</dt>
                        <dd>
                          {t(
                            language,
                            card.tactical.preferredLine === "frontline"
                              ? "frontline"
                              : card.tactical.preferredLine === "support"
                                ? "supportLine"
                                : "flexible",
                          )}
                        </dd>
                      </div>
                      {card.tactical.proposedActionCost !== undefined && (
                        <div>
                          <dt>{t(language, "proposedCost")}</dt>
                          <dd>
                            {card.tactical.proposedActionCost}{" "}
                            <span className="muted">
                              / {t(language, "rawCost")} {card.actionCost}
                            </span>
                          </dd>
                        </div>
                      )}
                    </dl>
                  </div>
                )}
              </section>
            </div>
            <section className="inspector-section">
              <h3>{t(language, "relatedCards")}</h3>
              {related.length ? (
                <div className="related-cards">
                  {related.map((entry) => (
                    <button key={entry.id} onClick={() => onSelect(entry.id)}>
                      <span style={{ color: RARITY_COLORS[palette][entry.rarity] }}>
                        {String(entry.rarity).padStart(2, "0")}
                      </span>
                      <strong>{entry.name[language]}</strong>
                      <small>{entry.id}</small>
                      <b>→</b>
                    </button>
                  ))}
                </div>
              ) : (
                <p className="muted">{t(language, "noRelated")}</p>
              )}
            </section>
            <details className="source-panel">
              <summary>
                {t(language, "viewSource")}
                <span>{card.sourcePath}</span>
              </summary>
              <div className="source-content">
                <table>
                  <tbody>
                    {Object.entries(card.raw)
                      .filter(([key]) => key !== "script")
                      .map(([key, value]) => (
                        <tr key={key}>
                          <th>{key}</th>
                          <td>{typeof value === "object" ? JSON.stringify(value) : String(value)}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
                {card.effects.length > 0 && (
                  <>
                    <h3>{t(language, "effects")}</h3>
                    <pre>{JSON.stringify(card.effects, null, 2)}</pre>
                  </>
                )}
              </div>
            </details>
          </div>
        </div>
      </section>
    </div>
  );
}
