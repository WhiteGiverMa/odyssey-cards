import { useEffect, useMemo, useState } from "react";
import { KEYWORD_NAMES, MECHANIC_NAMES, MECHANIC_TAGS, UNIT_CLASSES, UNIT_NAMES } from "../../shared/constants.ts";
import { CARD_PACKS, normalizePackId, packColor } from "../../shared/packs.ts";
import type { CardRecord, Language, UnitClass } from "../../shared/types.ts";
import { api } from "../api.ts";
import { t } from "../i18n.ts";
import { displayPackName } from "../lib/packNames.ts";

interface CardTagEditorProps {
  card: CardRecord;
  language: Language;
  onSaved: (card: CardRecord) => void;
  onNotice: (message: string) => void;
}

export function CardTagEditor({ card, language, onSaved, onNotice }: CardTagEditorProps) {
  const [mechanicTags, setMechanicTags] = useState(card.mechanicTags);
  const [keywords, setKeywords] = useState(card.resourceKeywords.filter((keyword) => keyword >= 1 && keyword <= 12));
  const [unitClass, setUnitClass] = useState<UnitClass>(card.unitClass ?? "vanguard");
  const [packId, setPackId] = useState(card.packId);
  const [newPackId, setNewPackId] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    setMechanicTags(card.mechanicTags);
    setKeywords(card.resourceKeywords.filter((keyword) => keyword >= 1 && keyword <= 12));
    setUnitClass(card.unitClass ?? "vanguard");
    setPackId(card.packId);
  }, [card.id, card.mechanicTags, card.resourceKeywords, card.unitClass, card.packId]);
  const packOptions = useMemo(() => {
    const ids = CARD_PACKS.map((pack) => pack.id);
    if (!ids.includes(packId)) ids.push(packId);
    return ids;
  }, [packId]);
  const packChanged = packId !== card.packId;
  const dirty = useMemo(() => {
    const originalKeywords = card.resourceKeywords.filter((keyword) => keyword >= 1 && keyword <= 12);
    return (
      mechanicTags !== card.mechanicTags ||
      keywords.length !== originalKeywords.length ||
      keywords.some((keyword, index) => keyword !== originalKeywords[index]) ||
      (card.typeKey === "minion" && unitClass !== (card.unitClass ?? "vanguard")) ||
      packChanged
    );
  }, [card, keywords, mechanicTags, unitClass, packChanged]);
  const toggleKeyword = (value: number) =>
    setKeywords((current) =>
      current.includes(value)
        ? current.filter((keyword) => keyword !== value)
        : [...current, value].sort((a, b) => a - b),
    );
  const save = async () => {
    setSaving(true);
    try {
      const saved = await api.saveCardTags(card.id, {
        mechanicTags,
        keywords,
        ...(card.typeKey === "minion" ? { unitClass: UNIT_CLASSES.indexOf(unitClass) } : {}),
        packId,
      });
      onSaved(saved);
      onNotice(
        packChanged
          ? t(language, "packSaved", { name: saved.name[language], pack: displayPackName(saved.packId, language) })
          : t(language, "tagsSaved", { name: saved.name[language] }),
      );
    } catch (error) {
      onNotice(error instanceof Error ? error.message : t(language, "generalError"));
    } finally {
      setSaving(false);
    }
  };
  return (
    <section className="inspector-section inline-tag-editor" aria-label={t(language, "tagEditor")}>
      <header className="inline-editor-heading">
        <div>
          <span className="section-index">WRITE / ROUND-TRIP</span>
          <h3>{t(language, "tagEditor")}</h3>
        </div>
        <button className="primary-button" disabled={saving || !dirty} onClick={() => void save()}>
          {saving ? t(language, "saving") : t(language, "saveTags")}
        </button>
      </header>
      <p className="editor-boundary-note">{t(language, "tagBoundary")}</p>
      {card.typeKey === "minion" && (
        <div className="inline-field">
          <label>{t(language, "unitClass")}</label>
          <div className="compact-choice-row">
            {UNIT_CLASSES.map((entry) => (
              <button key={entry} className={unitClass === entry ? "active" : ""} onClick={() => setUnitClass(entry)}>
                {UNIT_NAMES[entry][language]}
              </button>
            ))}
          </div>
        </div>
      )}
      <div className="inline-field">
        <label>{t(language, "keywords")}</label>
        <div className="compact-choice-row">
          {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => {
            const key = [
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
            ][value];
            return (
              <button
                key={value}
                className={keywords.includes(value) ? "active" : ""}
                onClick={() => toggleKeyword(value)}
              >
                {KEYWORD_NAMES[key ?? "none"]?.[language] ?? value}
              </button>
            );
          })}
        </div>
      </div>
      <div className="inline-field">
        <label>{t(language, "mechanics")}</label>
        <div className="compact-choice-row mechanic-choice-row">
          {MECHANIC_TAGS.map(([key, bit]) => (
            <button
              key={key}
              className={(mechanicTags & bit) === bit ? "active" : ""}
              onClick={() => setMechanicTags((current) => current ^ bit)}
            >
              {MECHANIC_NAMES[key]?.[language] ?? key}
            </button>
          ))}
        </div>
      </div>
      <div className="inline-field">
        <label>{t(language, "packSeries")}</label>
        <div className="compact-choice-row pack-choice-row">
          {packOptions.map((id) => (
            <button
              key={id}
              className={packId === id ? "active" : ""}
              style={{ "--pack-color": packColor(id) } as React.CSSProperties}
              onClick={() => setPackId(id)}
            >
              {displayPackName(id, language)}
            </button>
          ))}
          <div className="pack-inline-new">
            <input
              value={newPackId}
              onChange={(event) => setNewPackId(event.target.value)}
              placeholder={t(language, "packNewId")}
              aria-label={t(language, "packNewId")}
              spellCheck={false}
            />
            <button
              title={t(language, "packCreateAndAssign")}
              onClick={() => {
                const id = normalizePackId(newPackId);
                if (!id) {
                  onNotice(t(language, "packNeedId"));
                  return;
                }
                setPackId(id);
                setNewPackId("");
              }}
            >
              +
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
