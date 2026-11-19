import { useMemo, useState } from "react";
import { isValidPackId, packColor, summarizePacks } from "../../shared/packs.ts";
import type { CardRecord, Language } from "../../shared/types.ts";
import { api } from "../api.ts";
import { t } from "../i18n.ts";
import { displayPackName, savePackName } from "../lib/packNames.ts";
import { PackBadge } from "./PackBadge.tsx";

interface PackOrchestratorProps {
  cards: CardRecord[];
  language: Language;
  onInspect: (id: string) => void;
  onCardsAssigned: (cards: CardRecord[]) => void;
  onNotice: (message: string) => void;
}

/** 「卡包编排」——勾选卡牌后点目标卡包即可批量写回，也可现场新建自定义卡包。 */
export function PackOrchestrator({ cards, language, onInspect, onCardsAssigned, onNotice }: PackOrchestratorProps) {
  const [search, setSearch] = useState("");
  const [packFilter, setPackFilter] = useState("all");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [assigning, setAssigning] = useState<string | null>(null);
  const [newId, setNewId] = useState("");
  const [newName, setNewName] = useState("");

  const packs = useMemo(() => summarizePacks(cards.map((card) => card.packId)), [cards]);
  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return cards.filter((card) => {
      if (packFilter !== "all" && card.packId !== packFilter) return false;
      return !needle || `${card.id} ${card.name.zh} ${card.name.en}`.toLowerCase().includes(needle);
    });
  }, [cards, packFilter, search]);

  const toggle = (id: string) =>
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  const selectAll = () => setSelected(new Set(filtered.map((card) => card.id)));
  const clearSelection = () => setSelected(new Set());

  const assign = async (packId: string) => {
    if (!selected.size) {
      onNotice(t(language, "packNeedSelection"));
      return;
    }
    setAssigning(packId);
    try {
      const result = await api.assignPack([...selected], packId);
      onCardsAssigned(result.cards);
      onNotice(
        t(language, "packAssigned", {
          count: result.updated.length,
          pack: displayPackName(packId, language),
        }) + (result.missing.length ? ` · ${result.missing.join(", ")}` : ""),
      );
      setSelected(new Set());
    } catch (error) {
      onNotice(`${t(language, "packAssignFailed")}: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setAssigning(null);
    }
  };

  const createAndAssign = async () => {
    const id = newId.trim();
    if (!isValidPackId(id)) {
      onNotice(t(language, "packNeedId"));
      return;
    }
    savePackName(id, newName);
    setNewId("");
    setNewName("");
    await assign(id);
  };

  return (
    <div className="pack-orchestrator">
      <p className="editor-boundary-note">{t(language, "packManageHint")}</p>
      <div className="pack-toolbar">
        <label className="search-field workshop-search">
          <span>⌕</span>
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t(language, "searchPlaceholder")}
            aria-label={t(language, "search")}
          />
        </label>
        <div className="pack-toolbar-actions">
          <button className="pack-tool-button" onClick={selectAll} disabled={!filtered.length}>
            {t(language, "packSelectAll")}
          </button>
          <button className="pack-tool-button" onClick={clearSelection} disabled={!selected.size}>
            {t(language, "packClearSelection")}
          </button>
          <span className="pack-selected-count">{t(language, "packSelected", { count: selected.size })}</span>
        </div>
      </div>

      <section className="pack-targets">
        <header className="pack-targets-heading">
          <span className="section-index">TARGET / PACK</span>
          <h4>{t(language, "packAssignTo")}</h4>
        </header>
        <div className="pack-target-grid">
          {packs.map((pack) => (
            <button
              key={pack.id}
              type="button"
              className={`pack-target-btn ${assigning === pack.id ? "busy" : ""}`}
              style={{ "--pack-color": pack.color } as React.CSSProperties}
              onClick={() => void assign(pack.id)}
              disabled={assigning !== null}
            >
              <strong>{displayPackName(pack.id, language)}</strong>
              <small>
                {pack.custom ? t(language, "packCustom") : t(language, "packOfficial")} ·{" "}
                {t(language, "packCount", { count: pack.count })}
              </small>
            </button>
          ))}
        </div>
        <div className="pack-new-form">
          <span className="pack-new-label">{t(language, "packNew")}</span>
          <div className="pack-new-row">
            <input
              value={newId}
              onChange={(event) => setNewId(event.target.value)}
              placeholder="my_expansion"
              aria-label={t(language, "packNewId")}
              spellCheck={false}
            />
            <input
              value={newName}
              onChange={(event) => setNewName(event.target.value)}
              placeholder={t(language, "packNewName")}
              aria-label={t(language, "packNewName")}
            />
            <button className="primary-button" disabled={assigning !== null} onClick={() => void createAndAssign()}>
              {assigning ? t(language, "packAssigning") : t(language, "packCreateAndAssign")}
            </button>
          </div>
          <small className="pack-id-hint">{t(language, "packIdHint")}</small>
        </div>
      </section>

      <section className="pack-select-panel">
        <div className="pack-select-filters">
          <button className={packFilter === "all" ? "active" : ""} onClick={() => setPackFilter("all")}>
            {t(language, "packAll")} · {cards.length}
          </button>
          {packs.map((pack) => (
            <button
              key={pack.id}
              className={packFilter === pack.id ? "active" : ""}
              style={{ "--pack-color": pack.color } as React.CSSProperties}
              onClick={() => setPackFilter(pack.id)}
            >
              {displayPackName(pack.id, language)} · {pack.count}
            </button>
          ))}
        </div>
        <p className="filter-hint">{t(language, "packDefaultNote")}</p>
        <div className="pack-select-grid">
          {filtered.map((card) => (
            <div
              key={card.id}
              className={`pack-select-row ${selected.has(card.id) ? "selected" : ""}`}
              style={{ "--pack-color": packColor(card.packId) } as React.CSSProperties}
            >
              <input
                type="checkbox"
                checked={selected.has(card.id)}
                onChange={() => toggle(card.id)}
                aria-label={`${t(language, "pack")} · ${card.name[language]}`}
              />
              <button className="pack-select-name" onClick={() => onInspect(card.id)}>
                <strong>{card.name[language]}</strong>
                <small>{card.id}</small>
              </button>
              <PackBadge packId={card.packId} language={language} compact />
            </div>
          ))}
          {!filtered.length && <p className="muted">{t(language, "noCards")}</p>}
        </div>
      </section>
    </div>
  );
}
