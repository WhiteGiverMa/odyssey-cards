import { useMemo, useRef, useState } from "react";
import type { CardRecord, GeneratedDeck, Language, ThemeRecord } from "../../shared/types.ts";
import { CLASS_COLORS, RARITY_COLORS, TYPE_NAMES, UNIT_CLASSES, UNIT_NAMES } from "../../shared/constants.ts";
import { api } from "../api.ts";
import { t } from "../i18n.ts";
import { cardCounts, deckProblems, deckToText, parseDeckImport, type DeckState } from "../lib/deck.ts";

interface DeckPanelProps {
  deck: DeckState;
  cards: CardRecord[];
  themes: ThemeRecord[];
  language: Language;
  palette: "classic" | "new";
  onChange: (deck: DeckState) => void;
  onInspect: (id: string) => void;
  onNotice: (message: string) => void;
}

export function DeckPanel({ deck, cards, themes, language, palette, onChange, onInspect, onNotice }: DeckPanelProps) {
  const [generating, setGenerating] = useState<string | null>(null);
  const [generation, setGeneration] = useState<GeneratedDeck | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importText, setImportText] = useState("");
  const [importError, setImportError] = useState("");
  const [confirmClear, setConfirmClear] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const counts = useMemo(() => cardCounts(deck.cardIds), [deck.cardIds]);
  const rows = useMemo(
    () =>
      [...counts]
        .map(([id, count]) => ({ card: cards.find((entry) => entry.id === id), count }))
        .filter((entry): entry is { card: CardRecord; count: number } => Boolean(entry.card))
        .sort((a, b) => a.card.cost - b.card.cost || a.card.name[language].localeCompare(b.card.name[language])),
    [counts, cards, language],
  );
  const curve = useMemo(
    () =>
      Array.from(
        { length: 8 },
        (_, cost) =>
          deck.cardIds.filter((id) => {
            const card = cards.find((entry) => entry.id === id);
            return card && (cost === 7 ? card.cost >= 7 : card.cost === cost);
          }).length,
      ),
    [deck.cardIds, cards],
  );
  const unitDistribution = UNIT_CLASSES.map((unitClass) => ({
    unitClass,
    count: deck.cardIds.filter((id) => cards.find((card) => card.id === id)?.unitClass === unitClass).length,
  }));
  const typeDistribution = (["minion", "spell", "domain", "status"] as const).map((type) => ({
    type,
    count: deck.cardIds.filter((id) => cards.find((card) => card.id === id)?.typeKey === type).length,
  }));
  const problems = deckProblems(deck, cards);
  const valid = deck.cardIds.length >= 10 && deck.cardIds.length <= 20 && problems.length === 0;
  const maxCurve = Math.max(1, ...curve);
  const add = (id: string) => {
    if (deck.cardIds.length >= 20) return onNotice(t(language, "maxDeck"));
    onChange({ ...deck, cardIds: [...deck.cardIds, id] });
  };
  const remove = (id: string) => {
    const index = deck.cardIds.lastIndexOf(id);
    if (index < 0) return;
    onChange({ ...deck, cardIds: deck.cardIds.filter((_, cardIndex) => cardIndex !== index) });
  };
  const generate = async (themeId: string) => {
    setGenerating(themeId);
    try {
      const generated = await api.generate(themeId);
      setGeneration(generated);
      onChange({
        name: generated.cards.length
          ? `${themes.find((theme) => theme.id === themeId)?.name[language] ?? themeId}`
          : deck.name,
        cardIds: generated.cardIds,
      });
      onNotice(
        t(language, "generated", {
          theme: themes.find((theme) => theme.id === themeId)?.name[language] ?? themeId,
          seed: generated.seed,
        }),
      );
    } catch (error) {
      onNotice(error instanceof Error ? error.message : t(language, "generalError"));
    } finally {
      setGenerating(null);
    }
  };
  const download = (kind: "json" | "text") => {
    const content =
      kind === "json"
        ? JSON.stringify({ version: 1, name: deck.name, cardIds: deck.cardIds }, null, 2)
        : deckToText(deck, cards, language);
    const blob = new Blob([content], { type: kind === "json" ? "application/json" : "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${deck.name.replace(/[\\/:*?"<>|]/g, "-") || "odyssey-deck"}.${kind === "json" ? "json" : "txt"}`;
    link.click();
    URL.revokeObjectURL(url);
    onNotice(t(language, "exportDone"));
  };
  const doImport = () => {
    try {
      const imported = parseDeckImport(importText, cards);
      onChange(imported);
      setImportOpen(false);
      setImportText("");
      setImportError("");
    } catch (error) {
      setImportError(error instanceof Error ? error.message : t(language, "importError"));
    }
  };
  return (
    <aside className="deck-panel" aria-label={t(language, "deck")}>
      <header className="deck-heading">
        <div>
          <span className="section-index">D/20</span>
          <h2>{t(language, "deck")}</h2>
        </div>
        <span className={`deck-status ${valid ? "valid" : ""}`}>
          {valid ? t(language, "deckValid") : t(language, "deckBuilding")}
        </span>
      </header>
      <div className="deck-name-field">
        <label htmlFor="deck-name">{t(language, "deckName")}</label>
        <input
          id="deck-name"
          value={deck.name}
          maxLength={48}
          onChange={(event) => onChange({ ...deck, name: event.target.value })}
        />
      </div>
      <div className="deck-total">
        <strong>{String(deck.cardIds.length).padStart(2, "0")}</strong>
        <span>/ 20 CARDS</span>
        <small>{t(language, "deckSaved")}</small>
      </div>
      {deck.cardIds.length < 10 && (
        <div className="validity-hint">{t(language, "deckTooFew", { count: 10 - deck.cardIds.length })}</div>
      )}
      {problems.length > 0 && <div className="validity-hint error">{problems.slice(0, 2).join(" · ")}</div>}
      <section className="deck-section curve-section">
        <h3>
          {t(language, "curve")}
          <span>0—7+</span>
        </h3>
        <div className="curve-chart">
          {curve.map((count, index) => (
            <div className="curve-column" key={index}>
              <span className="curve-value">{count || ""}</span>
              <div className="curve-track">
                <i style={{ height: `${(count / maxCurve) * 100}%` }} />
              </div>
              <small>{index === 7 ? "7+" : index}</small>
            </div>
          ))}
        </div>
      </section>
      <section className="deck-section distribution-section">
        <h3>{t(language, "distribution")}</h3>
        <div className="distribution-bars">
          {unitDistribution.map(({ unitClass, count }) => (
            <div key={unitClass}>
              <span style={{ color: CLASS_COLORS[unitClass] }}>{UNIT_NAMES[unitClass][language]}</span>
              <div>
                <i
                  style={{
                    width: `${Math.min(100, (count / Math.max(1, deck.cardIds.length)) * 100)}%`,
                    background: CLASS_COLORS[unitClass],
                  }}
                />
              </div>
              <b>{count}</b>
            </div>
          ))}
        </div>
        <div className="type-mini-stats">
          {typeDistribution.map(({ type, count }) => (
            <span key={type}>
              {TYPE_NAMES[type][language]} <b>{count}</b>
            </span>
          ))}
        </div>
      </section>
      <section className="deck-section generator-section">
        <h3>{t(language, "generate")}</h3>
        <p>{t(language, "generateHint")}</p>
        <div className="theme-generators">
          {themes.map((theme) => (
            <button
              key={theme.id}
              disabled={Boolean(generating)}
              onClick={() => generate(theme.id)}
              className={generating === theme.id ? "generating" : ""}
            >
              <span>{theme.name[language]}</span>
              <b>{generating === theme.id ? "···" : "20"}</b>
            </button>
          ))}
        </div>
        {generation && (
          <div className="generation-readout">
            SEED {generation.seed} · CORE {generation.stats.coreCardsIncluded} · CURVE{" "}
            {generation.stats.manaCurve.join("/")}
          </div>
        )}
      </section>
      <section className="deck-section deck-list-section">
        <h3>
          {t(language, "deckList")}
          <span>{rows.length} UNIQUE</span>
        </h3>
        {rows.length ? (
          <div className="deck-list">
            {rows.map(({ card, count }) => (
              <div className="deck-row" key={card.id}>
                <button className="deck-row-name" onClick={() => onInspect(card.id)}>
                  <span className="deck-row-cost">{card.cost}</span>
                  <span>
                    <strong>{card.name[language]}</strong>
                    <small>
                      {card.typeKey}
                      {card.unitClass ? ` · ${UNIT_NAMES[card.unitClass][language]}` : ""}
                    </small>
                  </span>
                </button>
                <span className="deck-row-count" style={{ color: RARITY_COLORS[palette][card.rarity] }}>
                  ×{count}
                </span>
                <div className="deck-row-actions">
                  <button
                    onClick={() => remove(card.id)}
                    aria-label={`${t(language, "removeOne")} ${card.name[language]}`}
                  >
                    −
                  </button>
                  <button onClick={() => add(card.id)} aria-label={`${t(language, "addOne")} ${card.name[language]}`}>
                    +
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="deck-empty">
            <span className="empty-crosshair">+</span>
            <strong>{t(language, "deckEmpty")}</strong>
            <p>{t(language, "deckEmptyHint")}</p>
          </div>
        )}
      </section>
      <footer className="deck-footer-actions">
        <div>
          <button onClick={() => setImportOpen(true)}>{t(language, "import")}</button>
          <button onClick={() => download("json")}>{t(language, "exportJson")}</button>
          <button onClick={() => download("text")}>{t(language, "exportText")}</button>
        </div>
        {confirmClear ? (
          <div className="clear-confirm">
            <span>{t(language, "clear")}?</span>
            <button
              onClick={() => {
                onChange({ ...deck, cardIds: [] });
                setConfirmClear(false);
              }}
            >
              ✓
            </button>
            <button onClick={() => setConfirmClear(false)}>×</button>
          </div>
        ) : (
          <button className="clear-deck" disabled={!deck.cardIds.length} onClick={() => setConfirmClear(true)}>
            {t(language, "clear")}
          </button>
        )}
      </footer>
      {importOpen && (
        <div
          className="import-overlay"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setImportOpen(false);
          }}
        >
          <section className="import-dialog" role="dialog" aria-modal="true" aria-labelledby="import-title">
            <header>
              <h3 id="import-title">{t(language, "importTitle")}</h3>
              <button className="icon-button" onClick={() => setImportOpen(false)}>
                ×
              </button>
            </header>
            <p>{t(language, "importHint")}</p>
            <textarea
              autoFocus
              value={importText}
              onChange={(event) => setImportText(event.target.value)}
              placeholder={t(language, "importPlaceholder")}
              rows={10}
            />
            {importError && <div className="validity-hint error">{importError}</div>}
            <input
              ref={fileRef}
              type="file"
              accept=".json,.txt"
              hidden
              onChange={async (event) => {
                const file = event.target.files?.[0];
                if (file) setImportText(await file.text());
                event.target.value = "";
              }}
            />
            <footer>
              <button onClick={() => fileRef.current?.click()}>JSON / TXT</button>
              <div>
                <button onClick={() => setImportOpen(false)}>{t(language, "cancel")}</button>
                <button className="primary-button" onClick={doImport}>
                  {t(language, "confirmImport")}
                </button>
              </div>
            </footer>
          </section>
        </div>
      )}
    </aside>
  );
}
