import { useCallback, useEffect, useMemo, useState } from "react";
import type {
  CardRecord,
  GeneratedDeck,
  HeroRecord,
  Language,
  RelicRecord,
  RulesResponse,
  ThemeRecord,
  UnitClass,
  WeaponRecord,
} from "../shared/types.ts";
import {
  KEYWORD_NAMES,
  MECHANIC_NAMES,
  MECHANIC_TAGS,
  RARITY_COLORS,
  RARITY_NAMES,
  TYPE_NAMES,
  UNIT_CLASSES,
  UNIT_NAMES,
} from "../shared/constants.ts";
import { api } from "./api.ts";
import { summarizePacks } from "../shared/packs.ts";
import { displayPackName } from "./lib/packNames.ts";
import { t } from "./i18n.ts";
import { cardCounts, type DeckState } from "./lib/deck.ts";
import { CardView } from "./components/CardView.tsx";
import { Inspector } from "./components/Inspector.tsx";
import { DeckPanel } from "./components/DeckPanel.tsx";
import { Codex } from "./components/Codex.tsx";
import { Wiki } from "./components/Wiki.tsx";
import { Workshop } from "./components/Workshop.tsx";

interface CatalogData {
  cards: CardRecord[];
  heroes: HeroRecord[];
  weapons: WeaponRecord[];
  relics: RelicRecord[];
  themes: ThemeRecord[];
  rules: RulesResponse;
}

type Page = "collection" | "workshop" | "codex" | "wiki";
const PAGE_IDS: Page[] = ["collection", "workshop", "codex", "wiki"];
function pageFromHash(): Page {
  const id = window.location.hash.replace(/^#\/?/, "").split("/")[0];
  if (id === "builder") return "collection";
  return PAGE_IDS.includes(id as Page) ? (id as Page) : "collection";
}
function wikiTermFromHash(): string {
  const match = /^#\/?wiki\/term\/([a-z0-9-]+)/i.exec(window.location.hash);
  return match?.[1] ?? "";
}
interface Filters {
  search: string;
  theme: string;
  cost: string;
  actionCost: string;
  type: string;
  unitClass: string;
  rarity: string;
  keyword: string;
  mechanic: string;
  pack: string;
  sort: string;
}
const DEFAULT_FILTERS: Filters = {
  search: "",
  theme: "all",
  cost: "all",
  actionCost: "all",
  type: "all",
  unitClass: "all",
  rarity: "all",
  keyword: "all",
  mechanic: "all",
  pack: "all",
  sort: "cost",
};

function readStoredDeck(): DeckState {
  try {
    const value = JSON.parse(localStorage.getItem("odyssey-web-deck") ?? "null") as Partial<DeckState> | null;
    if (
      value &&
      typeof value.name === "string" &&
      Array.isArray(value.cardIds) &&
      value.cardIds.every((id) => typeof id === "string")
    )
      return { name: value.name, cardIds: value.cardIds };
  } catch {
    /* A malformed local deck falls back to a clean workspace. */
  }
  return { name: "星途远征", cardIds: [] };
}

function BrandMark() {
  return (
    <svg viewBox="0 0 44 44" fill="none" aria-hidden="true">
      <path
        d="m22 3 4.8 13.2L41 22l-14.2 5.3L22 41l-4.7-13.7L3 22l14.3-5.8L22 3Z"
        stroke="currentColor"
        strokeWidth="1.4"
      />
      <path d="m22 11 2.6 8.4L34 22l-9.4 2.7L22 34l-2.5-9.3L10 22l9.5-2.6L22 11Z" fill="currentColor" opacity=".22" />
      <circle cx="22" cy="22" r="4" stroke="currentColor" />
      <path d="M22 0v8M22 36v8M0 22h8M36 22h8" stroke="currentColor" opacity=".55" />
    </svg>
  );
}

function FilterGroup({
  title,
  children,
  className = "",
}: {
  title: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <details className={`filter-group ${className}`} open>
      <summary>
        {title}
        <span>−</span>
      </summary>
      <div className="filter-group-content">{children}</div>
    </details>
  );
}

function ChoiceButton({
  active,
  onClick,
  children,
  title,
  color,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  title?: string;
  color?: string;
}) {
  return (
    <button
      className={`filter-choice ${active ? "active" : ""}`}
      onClick={onClick}
      title={title}
      style={color ? ({ "--choice-color": color } as React.CSSProperties) : undefined}
    >
      {children}
    </button>
  );
}

export default function App() {
  const [language, setLanguage] = useState<Language>(() =>
    localStorage.getItem("odyssey-web-language") === "en" ? "en" : "zh",
  );
  const [palette, setPalette] = useState<"classic" | "new">(() =>
    localStorage.getItem("odyssey-web-palette") === "classic" ? "classic" : "new",
  );
  const [page, setPage] = useState<Page>(pageFromHash);
  const [data, setData] = useState<CatalogData | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS);
  const [deck, setDeck] = useState<DeckState>(readStoredDeck);
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null);
  const [wikiTermId, setWikiTermId] = useState(wikiTermFromHash);
  const [deckOpen, setDeckOpen] = useState(
    () => window.location.hash.replace(/^#\/?/, "") === "builder" || window.innerWidth > 1200,
  );
  const [filterOpen, setFilterOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [cardResponse, heroResponse, relicResponse, themeResponse, rules] = await Promise.all([
        api.cards(),
        api.heroes(),
        api.relics(),
        api.themes(),
        api.rules(),
      ]);
      setData({
        cards: cardResponse.cards,
        heroes: heroResponse.heroes,
        weapons: heroResponse.weapons,
        relics: relicResponse.relics,
        themes: themeResponse.themes,
        rules,
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    const sync = () => {
      if (window.location.hash.replace(/^#\/?/, "") === "builder") setDeckOpen(true);
      setPage(pageFromHash());
      setWikiTermId(wikiTermFromHash());
      if (window.location.hash.replace(/^#\/?/, "") === "builder") window.history.replaceState(null, "", "#collection");
    };
    window.addEventListener("hashchange", sync);
    return () => window.removeEventListener("hashchange", sync);
  }, []);
  useEffect(() => {
    if (page === "wiki" && window.location.hash.startsWith("#wiki")) return;
    if (window.location.hash !== `#${page}`) window.history.replaceState(null, "", `#${page}`);
  }, [page]);
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [page]);
  useEffect(() => {
    localStorage.setItem("odyssey-web-language", language);
    document.documentElement.lang = language === "zh" ? "zh-CN" : "en";
  }, [language]);
  useEffect(() => {
    localStorage.setItem("odyssey-web-palette", palette);
  }, [palette]);
  useEffect(() => {
    if (data) localStorage.setItem("odyssey-web-deck", JSON.stringify(deck));
  }, [deck, data]);
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(""), 3400);
    return () => window.clearTimeout(timer);
  }, [notice]);
  const counts = useMemo(() => cardCounts(deck.cardIds), [deck.cardIds]);
  const packSummaries = useMemo(() => (data ? summarizePacks(data.cards.map((card) => card.packId)) : []), [data]);
  const setFilter = (key: keyof Filters, value: string) => setFilters((current) => ({ ...current, [key]: value }));
  const addCard = (id: string) => {
    if (!data) return;
    const card = data.cards.find((entry) => entry.id === id);
    if (!card?.buildable) return setNotice(t(language, "illegalCard"));
    if (deck.cardIds.length >= 20) return setNotice(t(language, "maxDeck"));
    if ((counts.get(id) ?? 0) >= card.maxCopies)
      return setNotice(`${card.name[language]} · ${t(language, "copyLimit", { limit: card.maxCopies })}`);
    setDeck((current) => ({ ...current, cardIds: [...current.cardIds, id] }));
  };
  const filteredCards = useMemo(() => {
    if (!data) return [];
    const needle = filters.search.trim().toLowerCase();
    const result = data.cards.filter((card) => {
      if (
        filters.theme !== "all" &&
        (filters.theme === "neutral" ? card.themes.length > 0 : !card.themes.includes(filters.theme))
      )
        return false;
      if (filters.cost !== "all" && (filters.cost === "7" ? card.cost < 7 : card.cost !== Number(filters.cost)))
        return false;
      if (
        filters.actionCost !== "all" &&
        (filters.actionCost === "3" ? card.actionCost < 3 : card.actionCost !== Number(filters.actionCost))
      )
        return false;
      if (filters.type !== "all" && card.typeKey !== filters.type) return false;
      if (filters.unitClass !== "all" && card.unitClass !== filters.unitClass) return false;
      if (filters.rarity !== "all" && card.rarity !== Number(filters.rarity)) return false;
      if (filters.keyword !== "all" && !card.keywordKeys.includes(filters.keyword)) return false;
      if (filters.mechanic !== "all" && !card.mechanicTagKeys.includes(filters.mechanic)) return false;
      if (filters.pack !== "all" && card.packId !== filters.pack) return false;
      if (needle) {
        const haystack = [
          card.id,
          card.name.zh,
          card.name.en,
          card.description.zh,
          card.description.en,
          ...card.keywordKeys.map((key) => KEYWORD_NAMES[key]?.zh ?? key),
          ...card.keywordKeys.map((key) => KEYWORD_NAMES[key]?.en ?? key),
          ...card.mechanicTagKeys,
        ]
          .join(" ")
          .toLowerCase();
        if (!haystack.includes(needle)) return false;
      }
      return true;
    });
    return result.sort((a, b) =>
      filters.sort === "name"
        ? a.name[language].localeCompare(b.name[language])
        : filters.sort === "rarity"
          ? a.rarity - b.rarity || a.cost - b.cost
          : filters.sort === "type"
            ? a.type - b.type || a.cost - b.cost
            : a.cost - b.cost || a.type - b.type || a.name[language].localeCompare(b.name[language]),
    );
  }, [data, filters, language]);
  const selectedCard = data?.cards.find((card) => card.id === selectedCardId) ?? null;
  const closeInspector = useCallback(() => setSelectedCardId(null), []);
  const openWikiTerm = (id: string) => {
    setSelectedCardId(null);
    setWikiTermId(id);
    setPage("wiki");
    window.location.hash = `wiki/term/${id}`;
  };
  const cardSaved = (saved: CardRecord) => {
    setData((current) =>
      current ? { ...current, cards: current.cards.map((card) => (card.id === saved.id ? saved : card)) } : current,
    );
  };
  const cardsAssigned = (cards: CardRecord[]) => {
    setData((current) => (current ? { ...current, cards } : current));
  };
  const themeSaved = (saved: ThemeRecord) => {
    setData((current) =>
      current
        ? { ...current, themes: current.themes.map((theme) => (theme.id === saved.id ? saved : theme)) }
        : current,
    );
  };
  const activeFilterCount =
    Object.entries(filters).filter(([key, value]) => key !== "sort" && key !== "search" && value !== "all").length +
    (filters.search ? 1 : 0);

  if (loading && !data)
    return (
      <div className="app-loading">
        <div className="loading-sigil">
          <BrandMark />
        </div>
        <span>OC // ARCHIVE LINK</span>
        <h1>{t(language, "loading")}</h1>
        <p>{t(language, "loadingSub")}</p>
        <div className="loading-track">
          <i />
        </div>
      </div>
    );
  if (error && !data)
    return (
      <div className="app-loading error-state">
        <div className="loading-sigil">
          <BrandMark />
        </div>
        <h1>{t(language, "loadError")}</h1>
        <p>{error}</p>
        <button className="primary-button" onClick={() => void load()}>
          {t(language, "retry")}
        </button>
      </div>
    );
  if (!data) return null;

  const collectionMode = page === "collection";
  return (
    <div className="app-shell">
      <header className="app-header">
        <button className="brand" onClick={() => setPage("collection")} aria-label={t(language, "archive")}>
          <span className="brand-mark">
            <BrandMark />
          </span>
          <span>
            <strong>{t(language, "archive")}</strong>
            <small>{t(language, "archiveSub")}</small>
          </span>
        </button>
        <nav className="main-nav" aria-label="Main navigation">
          {(["collection", "workshop", "codex", "wiki"] as Page[]).map((entry, index) => (
            <button
              key={entry}
              className={page === entry ? "active" : ""}
              onClick={() => {
                setWikiTermId("");
                setPage(entry);
              }}
            >
              <span>0{index + 1}</span>
              {t(language, entry)}
            </button>
          ))}
        </nav>
        <div className="header-actions">
          <span className="resource-status">
            <i />
            {t(language, "online")}
          </span>
          <button
            className="header-tool palette-toggle"
            onClick={() => setPalette(palette === "new" ? "classic" : "new")}
            title={t(language, palette === "new" ? "paletteClassic" : "palette")}
          >
            <span className="palette-dots">
              <i />
              <i />
              <i />
            </span>
            {palette === "new" ? "N" : "C"}
          </button>
          <button className="header-tool language-toggle" onClick={() => setLanguage(language === "zh" ? "en" : "zh")}>
            {t(language, "language")}
          </button>
          {collectionMode && (
            <button className="header-tool deck-toggle" onClick={() => setDeckOpen(!deckOpen)}>
              {deckOpen ? "×" : "▤"}
              <span>{t(language, deckOpen ? "closeDeck" : "openDeck")}</span>
              <b>{deck.cardIds.length}</b>
            </button>
          )}
        </div>
      </header>
      <div className="system-strip">
        <span>ODYSSEY / DATABASE</span>
        <div>
          <i />
          {t(language, "sourceNote")}
        </div>
        <span>BUILD 01.04</span>
      </div>
      <main className="app-main">
        {collectionMode && (
          <>
            <header className="collection-title page-container">
              <div>
                <span className="section-index">01 / COLLECTION & DECK</span>
                <h1>{t(language, "collectionTitle")}</h1>
                <p>{t(language, "collectionSubtitle")}</p>
              </div>
              <div className="registry-summary">
                <strong>{String(data.cards.length).padStart(3, "0")}</strong>
                <span>
                  RESOURCE
                  <br />
                  RECORDS
                </span>
                <div>
                  {data.cards.filter((card) => card.typeKey === "minion").length} UNITS
                  <br />
                  {data.cards.filter((card) => card.typeKey !== "minion").length} ORDERS
                </div>
              </div>
            </header>
            <div className={`collection-workspace ${deckOpen ? "with-deck" : ""} ${filterOpen ? "filters-open" : ""}`}>
              <aside className="filter-panel">
                <header>
                  <div>
                    <span className="section-index">F / TACTICAL</span>
                    <h2>{t(language, "filters")}</h2>
                  </div>
                  <button
                    onClick={() => {
                      setFilters(DEFAULT_FILTERS);
                    }}
                  >
                    {t(language, "reset")}
                    {activeFilterCount ? ` ${activeFilterCount}` : ""}
                  </button>
                </header>
                <div className="mobile-search">
                  <label>{t(language, "search")}</label>
                  <input
                    value={filters.search}
                    onChange={(event) => setFilter("search", event.target.value)}
                    placeholder={t(language, "searchPlaceholder")}
                  />
                </div>
                <FilterGroup title={t(language, "theme")}>
                  <div className="filter-button-grid theme-filter">
                    {["all", "ayame", "rie", "sokou", "neutral"].map((theme) => (
                      <ChoiceButton
                        key={theme}
                        active={filters.theme === theme}
                        onClick={() => setFilter("theme", theme)}
                      >
                        {theme === "all"
                          ? t(language, "all")
                          : theme === "neutral"
                            ? t(language, "neutral")
                            : data.heroes.find((hero) => hero.id === theme)?.name[language]}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "deployCost")}>
                  <div className="filter-button-grid cost-filter">
                    {["all", "0", "1", "2", "3", "4", "5", "6", "7"].map((cost) => (
                      <ChoiceButton key={cost} active={filters.cost === cost} onClick={() => setFilter("cost", cost)}>
                        {cost === "all" ? t(language, "all") : cost === "7" ? "7+" : cost}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "actionCost")}>
                  <div className="filter-button-grid cost-filter">
                    {["all", "0", "1", "2", "3"].map((cost) => (
                      <ChoiceButton
                        key={cost}
                        active={filters.actionCost === cost}
                        onClick={() => setFilter("actionCost", cost)}
                      >
                        {cost === "all" ? t(language, "all") : cost === "3" ? "3+" : cost}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "cardType")}>
                  <div className="filter-button-grid type-filter">
                    {(["all", "minion", "spell", "domain", "status", "countermeasure"] as const).map((type) => (
                      <ChoiceButton key={type} active={filters.type === type} onClick={() => setFilter("type", type)}>
                        {type === "all" ? t(language, "all") : TYPE_NAMES[type][language]}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "unitClass")}>
                  <div className="filter-button-grid unit-filter">
                    <ChoiceButton active={filters.unitClass === "all"} onClick={() => setFilter("unitClass", "all")}>
                      {t(language, "all")}
                    </ChoiceButton>
                    {UNIT_CLASSES.map((unitClass: UnitClass) => (
                      <ChoiceButton
                        key={unitClass}
                        active={filters.unitClass === unitClass}
                        onClick={() => setFilter("unitClass", unitClass)}
                        color={
                          unitClass === "vanguard"
                            ? "#ffd98e"
                            : unitClass === "assault"
                              ? "#ff9e7d"
                              : unitClass === "support"
                                ? "#7fd8ff"
                                : "#ff9ed2"
                        }
                      >
                        {UNIT_NAMES[unitClass][language]}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "rarity")}>
                  <div className="filter-button-grid rarity-filter">
                    <ChoiceButton active={filters.rarity === "all"} onClick={() => setFilter("rarity", "all")}>
                      {t(language, "all")}
                    </ChoiceButton>
                    {RARITY_NAMES.map((name, rarity) => (
                      <ChoiceButton
                        key={rarity}
                        active={filters.rarity === String(rarity)}
                        onClick={() => setFilter("rarity", String(rarity))}
                        color={RARITY_COLORS[palette][rarity]}
                        title={name[language]}
                      >
                        {rarity}
                      </ChoiceButton>
                    ))}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "keywords")}>
                  <div className="filter-chip-list">
                    {["charge", "taunt", "guard", "ambush", "impact", "recycle", "qiqiao", "smokescreen", "intel"].map(
                      (keyword) => (
                        <ChoiceButton
                          key={keyword}
                          active={filters.keyword === keyword}
                          onClick={() => setFilter("keyword", filters.keyword === keyword ? "all" : keyword)}
                        >
                          {KEYWORD_NAMES[keyword]?.[language]}
                        </ChoiceButton>
                      ),
                    )}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "mechanics")}>
                  <div className="filter-chip-list mechanics-filter">
                    <ChoiceButton active={filters.mechanic === "all"} onClick={() => setFilter("mechanic", "all")}>
                      {t(language, "all")}
                    </ChoiceButton>
                    {MECHANIC_TAGS.filter(([key]) => data.cards.some((card) => card.mechanicTagKeys.includes(key))).map(
                      ([key]) => (
                        <ChoiceButton
                          key={key}
                          active={filters.mechanic === key}
                          onClick={() => setFilter("mechanic", filters.mechanic === key ? "all" : key)}
                        >
                          {MECHANIC_NAMES[key]?.[language] ?? key}
                        </ChoiceButton>
                      ),
                    )}
                  </div>
                </FilterGroup>
                <FilterGroup title={t(language, "packFilter")}>
                  <div className="filter-button-grid pack-filter">
                    <ChoiceButton active={filters.pack === "all"} onClick={() => setFilter("pack", "all")}>
                      {t(language, "packAll")} · {data.cards.length}
                    </ChoiceButton>
                    {packSummaries.map((pack) => (
                      <ChoiceButton
                        key={pack.id}
                        active={filters.pack === pack.id}
                        onClick={() => setFilter("pack", pack.id)}
                        color={pack.color}
                        title={pack.description[language]}
                      >
                        {displayPackName(pack.id, language)} · {pack.count}
                      </ChoiceButton>
                    ))}
                  </div>
                  <p className="filter-hint">{t(language, "packFilterHint")}</p>
                </FilterGroup>
              </aside>
              <section className="collection-results">
                <div className="result-toolbar">
                  <button className="mobile-filter-toggle" onClick={() => setFilterOpen(!filterOpen)}>
                    {t(language, "filters")} {activeFilterCount ? `(${activeFilterCount})` : ""}
                  </button>
                  <label className="search-field">
                    <span>⌕</span>
                    <input
                      aria-label={t(language, "search")}
                      value={filters.search}
                      onChange={(event) => setFilter("search", event.target.value)}
                      placeholder={t(language, "searchPlaceholder")}
                    />
                    {filters.search && (
                      <button onClick={() => setFilter("search", "")} aria-label={t(language, "reset")}>
                        ×
                      </button>
                    )}
                  </label>
                  <label className="sort-field">
                    <span>{t(language, "sort")}</span>
                    <select value={filters.sort} onChange={(event) => setFilter("sort", event.target.value)}>
                      {["cost", "name", "rarity", "type"].map((sort) => (
                        <option key={sort} value={sort}>
                          {t(language, `sort${sort[0]?.toUpperCase()}${sort.slice(1)}`)}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
                <div className="result-count">
                  <span>
                    <i />
                    {t(language, "showing", { shown: filteredCards.length, total: data.cards.length })}
                  </span>
                  <small>CLICK TO INSPECT · + TO DEPLOY</small>
                </div>
                {filteredCards.length ? (
                  <div className="card-grid">
                    {filteredCards.map((card) => (
                      <CardView
                        key={card.id}
                        card={card}
                        cards={data.cards}
                        language={language}
                        palette={palette}
                        onWikiTerm={openWikiTerm}
                        deckCount={counts.get(card.id) ?? 0}
                        onInspect={setSelectedCardId}
                        onAdd={addCard}
                      />
                    ))}
                  </div>
                ) : (
                  <div className="empty-results">
                    <span>Ø</span>
                    <h2>{t(language, "noCards")}</h2>
                    <p>{t(language, "noCardsHint")}</p>
                    <button className="primary-button" onClick={() => setFilters(DEFAULT_FILTERS)}>
                      {t(language, "clearFilters")}
                    </button>
                  </div>
                )}
              </section>
              {deckOpen && (
                <DeckPanel
                  deck={deck}
                  cards={data.cards}
                  themes={data.themes}
                  language={language}
                  palette={palette}
                  onChange={setDeck}
                  onInspect={setSelectedCardId}
                  onNotice={setNotice}
                />
              )}
            </div>
          </>
        )}
        {page === "workshop" && (
          <Workshop
            cards={data.cards}
            themes={data.themes}
            language={language}
            onInspect={setSelectedCardId}
            onCardSaved={cardSaved}
            onCardsAssigned={cardsAssigned}
            onThemeSaved={themeSaved}
            onNotice={setNotice}
          />
        )}
        {page === "codex" && (
          <Codex
            heroes={data.heroes}
            weapons={data.weapons}
            relics={data.relics}
            cards={data.cards}
            language={language}
            onWikiTerm={openWikiTerm}
            onSelectCard={setSelectedCardId}
          />
        )}
        {page === "wiki" && (
          <Wiki
            language={language}
            rules={data.rules}
            cards={data.cards}
            focusedTermId={wikiTermId}
            onWikiTerm={openWikiTerm}
            onSelectCard={setSelectedCardId}
          />
        )}
      </main>
      <footer className="app-footer">
        <span>{t(language, "footer")}</span>
        <div>
          <i /> API / LOCAL REPOSITORY <b>·</b> {data.cards.length} CARDS
        </div>
      </footer>
      {selectedCard && (
        <Inspector
          card={selectedCard}
          cards={data.cards}
          language={language}
          palette={palette}
          rules={data.rules}
          deckCount={counts.get(selectedCard.id) ?? 0}
          onClose={closeInspector}
          onSelect={setSelectedCardId}
          onAdd={addCard}
          onWikiTerm={openWikiTerm}
          onSaved={cardSaved}
          onNotice={setNotice}
        />
      )}
      {notice && (
        <div className="toast" role="status">
          <span>●</span>
          {notice}
          <button onClick={() => setNotice("")}>×</button>
        </div>
      )}
      {filterOpen && (
        <button className="filter-scrim" aria-label={t(language, "close")} onClick={() => setFilterOpen(false)} />
      )}
    </div>
  );
}
