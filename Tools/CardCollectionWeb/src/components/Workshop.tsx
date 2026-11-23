import { useMemo, useState } from "react";
import type { CardRecord, Language, ResourceValidationReport, ThemeRecord } from "../../shared/types.ts";
import { KEYWORD_NAMES, MECHANIC_NAMES, MECHANIC_TAGS, UNIT_CLASSES, UNIT_NAMES } from "../../shared/constants.ts";
import { api } from "../api.ts";
import { t } from "../i18n.ts";
import { PackOrchestrator } from "./PackOrchestrator.tsx";

interface CardDraft {
  mechanicTags: number;
  keywords: number[];
  unitClass: number;
}

interface WorkshopProps {
  cards: CardRecord[];
  themes: ThemeRecord[];
  language: Language;
  onInspect: (id: string) => void;
  onCardSaved: (card: CardRecord) => void;
  onCardsAssigned: (cards: CardRecord[]) => void;
  onThemeSaved: (theme: ThemeRecord) => void;
  onNotice: (message: string) => void;
}

const KEYWORD_BY_VALUE = [
  "none",
  "charge",
  "taunt",
  "battlecry",
  "deathrattle",
  "windfury",
  "ambush",
  "impact",
  "recycle",
  "unplayable",
  "ethereal",
  "qiqiao",
  "guard",
];

function cardDraft(card: CardRecord): CardDraft {
  return {
    mechanicTags: card.mechanicTags,
    keywords: card.resourceKeywords.filter((keyword) => keyword >= 1 && keyword <= 12),
    unitClass: card.unitClass ? UNIT_CLASSES.indexOf(card.unitClass) : 0,
  };
}

function isDirty(card: CardRecord, draft: CardDraft | undefined): boolean {
  if (!draft) return false;
  const original = cardDraft(card);
  return (
    original.mechanicTags !== draft.mechanicTags ||
    original.unitClass !== draft.unitClass ||
    original.keywords.length !== draft.keywords.length ||
    original.keywords.some((value, index) => value !== draft.keywords[index])
  );
}

function WeightGrid({
  kind,
  values,
  language,
  onChange,
}: {
  kind: "tag" | "keyword";
  values: Record<string, number>;
  language: Language;
  onChange: (values: Record<string, number>) => void;
}) {
  const entries =
    kind === "tag"
      ? MECHANIC_TAGS.map(([key, value]) => ({ key, value, name: MECHANIC_NAMES[key]?.[language] ?? key }))
      : Array.from({ length: 12 }, (_, index) => index + 1).map((value) => ({
          key: KEYWORD_BY_VALUE[value] ?? String(value),
          value,
          name: KEYWORD_NAMES[KEYWORD_BY_VALUE[value] ?? "none"]?.[language] ?? String(value),
        }));
  return (
    <div className="weight-grid">
      {entries.map(({ key, value, name }) => (
        <label key={key} title={kind === "tag" ? `0x${value.toString(16)}` : `Keyword ${value}`}>
          <span>{name}</span>
          <input
            aria-label={`${kind}:${key}`}
            type="number"
            min="-100"
            max="100"
            step="1"
            value={values[String(value)] ?? 0}
            onChange={(event) => {
              const weight = Math.max(-100, Math.min(100, Math.trunc(Number(event.target.value) || 0)));
              const next = { ...values };
              if (weight === 0) delete next[String(value)];
              else next[String(value)] = weight;
              onChange(next);
            }}
          />
        </label>
      ))}
    </div>
  );
}

function ThemeMixer({
  theme,
  cards,
  language,
  onSaved,
  onNotice,
}: {
  theme: ThemeRecord;
  cards: CardRecord[];
  language: Language;
  onSaved: (theme: ThemeRecord) => void;
  onNotice: (message: string) => void;
}) {
  const [tagWeights, setTagWeights] = useState(theme.tagWeights);
  const [keywordWeights, setKeywordWeights] = useState(theme.keywordWeights);
  const [coreCardIds, setCoreCardIds] = useState(theme.coreCardIds);
  const [saving, setSaving] = useState(false);
  const save = async () => {
    setSaving(true);
    try {
      const saved = await api.saveTheme(theme.id, { tagWeights, keywordWeights, coreCardIds });
      setTagWeights(saved.tagWeights);
      setKeywordWeights(saved.keywordWeights);
      setCoreCardIds(saved.coreCardIds);
      onSaved(saved);
      onNotice(t(language, "themeSaved", { name: saved.name[language] }));
    } catch (error) {
      onNotice(error instanceof Error ? error.message : t(language, "generalError"));
    } finally {
      setSaving(false);
    }
  };
  return (
    <article className="theme-mixer">
      <header>
        <div>
          <span className="section-index">THEME / {theme.id.toUpperCase()}</span>
          <h3>{theme.name[language]}</h3>
          <small>{theme.sourcePath}</small>
        </div>
        <button className="primary-button" disabled={saving} onClick={() => void save()}>
          {saving ? t(language, "saving") : t(language, "saveTheme")}
        </button>
      </header>
      <section>
        <h4>{t(language, "tagWeights")}</h4>
        <WeightGrid kind="tag" values={tagWeights} language={language} onChange={setTagWeights} />
      </section>
      <section>
        <h4>{t(language, "keywordWeights")}</h4>
        <WeightGrid kind="keyword" values={keywordWeights} language={language} onChange={setKeywordWeights} />
      </section>
      <section className="core-pool-editor">
        <h4>
          {t(language, "coreCardPool")} <span>{coreCardIds.length}</span>
        </h4>
        <div className="core-card-chips">
          {coreCardIds.map((id) => {
            const card = cards.find((entry) => entry.id === id);
            return (
              <button key={id} title={id} onClick={() => setCoreCardIds(coreCardIds.filter((entry) => entry !== id))}>
                {card?.name[language] ?? id} <b>×</b>
              </button>
            );
          })}
          {!coreCardIds.length && <span className="muted">—</span>}
        </div>
        <details>
          <summary>{t(language, "editCorePool")}</summary>
          <div className="core-card-checks">
            {cards
              .filter((card) => card.buildable)
              .map((card) => (
                <label key={card.id}>
                  <input
                    type="checkbox"
                    checked={coreCardIds.includes(card.id)}
                    onChange={(event) =>
                      setCoreCardIds(
                        event.target.checked ? [...coreCardIds, card.id] : coreCardIds.filter((id) => id !== card.id),
                      )
                    }
                  />
                  <span>
                    {card.name[language]}
                    <small>{card.id}</small>
                  </span>
                </label>
              ))}
          </div>
        </details>
      </section>
    </article>
  );
}

export function Workshop({
  cards,
  themes,
  language,
  onInspect,
  onCardSaved,
  onCardsAssigned,
  onThemeSaved,
  onNotice,
}: WorkshopProps) {
  const [tab, setTab] = useState<"matrix" | "themes" | "packs">("matrix");
  const [search, setSearch] = useState("");
  const [drafts, setDrafts] = useState<Record<string, CardDraft>>(() =>
    Object.fromEntries(cards.map((card) => [card.id, cardDraft(card)])),
  );
  const [savingIds, setSavingIds] = useState<Set<string>>(new Set());
  const [report, setReport] = useState<ResourceValidationReport | null>(null);
  const [validating, setValidating] = useState(false);
  const dirtyIds = cards.filter((card) => isDirty(card, drafts[card.id])).map((card) => card.id);
  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return cards.filter(
      (card) => !needle || `${card.id} ${card.name.zh} ${card.name.en}`.toLowerCase().includes(needle),
    );
  }, [cards, search]);
  const update = (id: string, recipe: (current: CardDraft) => CardDraft) =>
    setDrafts((current) => ({
      ...current,
      [id]: recipe(current[id] ?? cardDraft(cards.find((card) => card.id === id)!)),
    }));
  const save = async (id: string) => {
    const draft = drafts[id];
    const card = cards.find((entry) => entry.id === id);
    if (!draft || !card) return;
    setSavingIds((current) => new Set(current).add(id));
    try {
      const saved = await api.saveCardTags(id, {
        mechanicTags: draft.mechanicTags,
        keywords: draft.keywords,
        ...(card.typeKey === "minion" ? { unitClass: draft.unitClass } : {}),
      });
      setDrafts((current) => ({ ...current, [id]: cardDraft(saved) }));
      onCardSaved(saved);
    } catch (error) {
      onNotice(error instanceof Error ? error.message : t(language, "generalError"));
      throw error;
    } finally {
      setSavingIds((current) => {
        const next = new Set(current);
        next.delete(id);
        return next;
      });
    }
  };
  const saveAll = async () => {
    const failed: string[] = [];
    for (const id of dirtyIds) {
      try {
        await save(id);
      } catch {
        failed.push(id);
      }
    }
    onNotice(
      failed.length
        ? `${t(language, "saveFailed")}: ${failed.join(", ")}`
        : t(language, "allTagsSaved", { count: dirtyIds.length }),
    );
  };
  const validate = async () => {
    setValidating(true);
    try {
      setReport(await api.validate());
    } catch (error) {
      onNotice(error instanceof Error ? error.message : t(language, "generalError"));
    } finally {
      setValidating(false);
    }
  };
  return (
    <div className="workshop-page page-container">
      <header className="page-title-block">
        <div>
          <span className="section-index">02 / RESOURCE WORKSHOP</span>
          <h1>{t(language, "workshopTitle")}</h1>
          <p>{t(language, "workshopSubtitle")}</p>
        </div>
        <div className="title-technical">
          {cards.length} CARDS
          <br />
          {themes.length} THEME PROFILES
        </div>
      </header>
      <div className="workshop-commandbar">
        <div className="workshop-tabs">
          <button className={tab === "matrix" ? "active" : ""} onClick={() => setTab("matrix")}>
            {t(language, "batchMatrix")}
          </button>
          <button className={tab === "themes" ? "active" : ""} onClick={() => setTab("themes")}>
            {t(language, "themeMixer")}
          </button>
          <button className={tab === "packs" ? "active" : ""} onClick={() => setTab("packs")}>
            {t(language, "packManage")}
          </button>
        </div>
        <button className="validate-button" disabled={validating} onClick={() => void validate()}>
          {validating ? "VALIDATING…" : "✓ VALIDATE"}
        </button>
        {tab === "matrix" && (
          <button
            className="primary-button"
            disabled={!dirtyIds.length || savingIds.size > 0}
            onClick={() => void saveAll()}
          >
            {t(language, "saveAll")} {dirtyIds.length ? `(${dirtyIds.length})` : ""}
          </button>
        )}
      </div>
      <p className="editor-boundary-note workshop-boundary">{t(language, "tagBoundary")}</p>
      {report && (
        <div className={`validation-report ${report.ok ? "ok" : "failed"}`} role="status">
          <strong>
            {report.ok ? "✓" : "×"} {report.cards} CARDS / {report.themes} THEMES · {report.errors.length} ERRORS /{" "}
            {report.warnings.length} WARNINGS
          </strong>
          {[...report.errors, ...report.warnings].map((message) => (
            <p key={message}>{message}</p>
          ))}
        </div>
      )}
      {tab === "matrix" ? (
        <section className="matrix-section">
          <label className="search-field workshop-search">
            <span>⌕</span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t(language, "searchPlaceholder")}
            />
          </label>
          <div className="matrix-head">
            <span>CARD</span>
            <span>{t(language, "unitClass")}</span>
            <span>{t(language, "keywords")}</span>
            <span>{t(language, "mechanics")}</span>
            <span>WRITE</span>
          </div>
          <div className="card-matrix">
            {filtered.map((card) => {
              const draft = drafts[card.id] ?? cardDraft(card);
              return (
                <article
                  className={`matrix-row ${isDirty(card, draft) ? "dirty" : ""}`}
                  key={card.id}
                  data-matrix-card={card.id}
                >
                  <button className="matrix-card-name" onClick={() => onInspect(card.id)}>
                    <strong>{card.name[language]}</strong>
                    <small>{card.id}</small>
                  </button>
                  <div className="matrix-unit">
                    {card.typeKey === "minion" ? (
                      <select
                        aria-label={`${card.id} UnitClass`}
                        value={draft.unitClass}
                        onChange={(event) =>
                          update(card.id, (current) => ({ ...current, unitClass: Number(event.target.value) }))
                        }
                      >
                        {UNIT_CLASSES.map((entry, index) => (
                          <option key={entry} value={index}>
                            {UNIT_NAMES[entry][language]}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <span className="muted">N/A</span>
                    )}
                  </div>
                  <details className="matrix-popup">
                    <summary>
                      {t(language, "keywords")} <b>{draft.keywords.length}</b>
                    </summary>
                    <div>
                      {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => (
                        <label key={value}>
                          <input
                            type="checkbox"
                            checked={draft.keywords.includes(value)}
                            onChange={() =>
                              update(card.id, (current) => ({
                                ...current,
                                keywords: current.keywords.includes(value)
                                  ? current.keywords.filter((entry) => entry !== value)
                                  : [...current.keywords, value].sort((a, b) => a - b),
                              }))
                            }
                          />
                          {KEYWORD_NAMES[KEYWORD_BY_VALUE[value] ?? "none"]?.[language]}
                        </label>
                      ))}
                    </div>
                  </details>
                  <details className="matrix-popup">
                    <summary>
                      {t(language, "mechanics")}{" "}
                      <b>{MECHANIC_TAGS.filter(([, bit]) => (draft.mechanicTags & bit) === bit).length}</b>
                    </summary>
                    <div>
                      {MECHANIC_TAGS.map(([key, bit]) => (
                        <label key={key}>
                          <input
                            type="checkbox"
                            checked={(draft.mechanicTags & bit) === bit}
                            onChange={() =>
                              update(card.id, (current) => ({ ...current, mechanicTags: current.mechanicTags ^ bit }))
                            }
                          />
                          {MECHANIC_NAMES[key]?.[language] ?? key}
                        </label>
                      ))}
                    </div>
                  </details>
                  <button
                    className="matrix-save"
                    disabled={!isDirty(card, draft) || savingIds.has(card.id)}
                    onClick={() => void save(card.id).catch(() => undefined)}
                  >
                    {savingIds.has(card.id) ? "···" : isDirty(card, draft) ? t(language, "saveTags") : "✓"}
                  </button>
                </article>
              );
            })}
          </div>
        </section>
      ) : tab === "packs" ? (
        <PackOrchestrator
          cards={cards}
          language={language}
          onInspect={onInspect}
          onCardsAssigned={onCardsAssigned}
          onNotice={onNotice}
        />
      ) : (
        <div className="theme-mixer-grid">
          {themes.map((theme) => (
            <ThemeMixer
              key={`${theme.id}-${theme.coreCardIds.join(",")}`}
              theme={theme}
              cards={cards}
              language={language}
              onSaved={onThemeSaved}
              onNotice={onNotice}
            />
          ))}
        </div>
      )}
    </div>
  );
}
