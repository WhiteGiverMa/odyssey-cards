import { useState } from "react";
import { TYPE_NAMES } from "../../shared/constants.ts";
import { packColor, summarizePacks } from "../../shared/packs.ts";
import type { CardRecord, HeroRecord, Language, RelicRecord, WeaponRecord } from "../../shared/types.ts";
import { t } from "../i18n.ts";
import { displayPackName } from "../lib/packNames.ts";
import { EmblemCanvas } from "./Artwork.tsx";
import { RichGameText } from "./RichGameText.tsx";

export function Codex({
  heroes,
  weapons,
  relics,
  cards,
  language,
  onWikiTerm,
  onSelectCard,
}: {
  heroes: HeroRecord[];
  weapons: WeaponRecord[];
  relics: RelicRecord[];
  cards: CardRecord[];
  language: Language;
  onWikiTerm: (id: string) => void;
  onSelectCard: (id: string) => void;
}) {
  const [selectedHero, setSelectedHero] = useState(heroes[0]?.id ?? "");
  const packs = summarizePacks(cards.map((card) => card.packId));
  const [activePack, setActivePack] = useState(packs[0]?.id ?? "all");
  const previewCards = cards.filter((card) => activePack === "all" || card.packId === activePack);
  const rich = (text: string) => (
    <RichGameText text={text} cards={cards} language={language} onWikiTerm={onWikiTerm} onSelectCard={onSelectCard} />
  );
  return (
    <div className="codex-page page-container">
      <header className="page-title-block">
        <div>
          <span className="section-index">03 / COMMAND</span>
          <h1>{t(language, "heroesTitle")}</h1>
          <p>{t(language, "heroesSubtitle")}</p>
        </div>
        <div className="title-technical">
          04 HEROES
          <br />
          07 WEAPONS · 08 RELICS
        </div>
      </header>
      <section className="pack-board-section">
        <header className="section-heading">
          <div>
            <span className="section-index">PACK / SERIES</span>
            <h2>{t(language, "packBoard")}</h2>
          </div>
          <p>{t(language, "packDefaultNote")}</p>
        </header>
        <div className="pack-board">
          <button className={`pack-tile ${activePack === "all" ? "active" : ""}`} onClick={() => setActivePack("all")}>
            <span className="pack-tile-code">ALL</span>
            <strong>{t(language, "packAll")}</strong>
            <span className="pack-tile-count">{t(language, "packCount", { count: cards.length })}</span>
            <small>ARCHIVE</small>
          </button>
          {packs.map((pack) => (
            <button
              key={pack.id}
              className={`pack-tile ${activePack === pack.id ? "active" : ""}`}
              style={{ "--pack-color": pack.color } as React.CSSProperties}
              onClick={() => setActivePack(pack.id)}
            >
              <span className="pack-tile-code">{pack.code}</span>
              <strong>{displayPackName(pack.id, language)}</strong>
              <span className="pack-tile-count">{t(language, "packCount", { count: pack.count })}</span>
              <small>{pack.custom ? t(language, "packCustom") : t(language, "packOfficial")}</small>
            </button>
          ))}
        </div>
        <div className="pack-preview">
          <div className="pack-preview-head">
            <strong>{activePack === "all" ? t(language, "packAll") : displayPackName(activePack, language)}</strong>
            <span>{t(language, "packCount", { count: previewCards.length })}</span>
          </div>
          {previewCards.length ? (
            <div className="pack-preview-grid">
              {previewCards.map((card) => (
                <button
                  className="pack-preview-card"
                  key={card.id}
                  style={{ "--pack-color": packColor(card.packId) } as React.CSSProperties}
                  onClick={() => onSelectCard(card.id)}
                >
                  <span>{card.id}</span>
                  <strong>{card.name[language]}</strong>
                  <small>{TYPE_NAMES[card.typeKey][language]}</small>
                </button>
              ))}
            </div>
          ) : (
            <p className="muted">{t(language, "packEmpty")}</p>
          )}
        </div>
      </section>
      <section className="hero-grid">
        {heroes.map((hero, index) => {
          const weapon = weapons.find((entry) => entry.id === hero.weaponId);
          const accent = hero.artStyle === "rune" ? "#ff9ed2" : hero.id === "rie" ? "#ff9e7d" : "#7fd8ff";
          return (
            <article
              className={`hero-record ${selectedHero === hero.id ? "selected" : ""}`}
              key={hero.id}
              onClick={() => setSelectedHero(hero.id)}
            >
              <div className="hero-emblem-wrap">
                <EmblemCanvas id={hero.id} style={hero.artStyle} accent={accent} />
                <span className="hero-index">0{index + 1}</span>
              </div>
              <div className="hero-info">
                <div className="hero-name-row">
                  <div>
                    <h2>{hero.name[language]}</h2>
                    <span>{hero.romanizedName.toUpperCase()}</span>
                  </div>
                  <div className="hero-hp">
                    <small>HP</small>
                    <strong>{hero.maxHealth}</strong>
                  </div>
                </div>
                <p>{rich(hero.description[language])}</p>
                <div className="hero-loadout">
                  <div>
                    <span className="loadout-icon">P</span>
                    <section>
                      <small>
                        {t(language, "heroPower")} · {hero.power.cost} MP
                        {hero.power.cooldown ? ` · CD ${hero.power.cooldown}` : ""}
                      </small>
                      <strong>{hero.power.name[language]}</strong>
                      <p>{rich(hero.power.description[language])}</p>
                    </section>
                  </div>
                  <div>
                    <span className="loadout-icon">W</span>
                    <section>
                      <small>
                        {t(language, "weapon")} · {weapon?.attack} ATK / {weapon?.attackCost} MP
                      </small>
                      <strong>{weapon?.name[language]}</strong>
                      <p>{rich(weapon?.passive?.description[language] ?? "")}</p>
                    </section>
                  </div>
                </div>
              </div>
            </article>
          );
        })}
      </section>
      <section className="arsenal-section">
        <header className="section-heading">
          <div>
            <span className="section-index">ARM / RANGE</span>
            <h2>{t(language, "arsenal")}</h2>
          </div>
          <p>MELEE / RANGED / FRIENDLY SUPPORT</p>
        </header>
        <div className="weapon-table">
          <div className="weapon-table-head">
            <span>ID</span>
            <span>{t(language, "weapon")}</span>
            <span>{t(language, "owner")}</span>
            <span>ATK / MP</span>
            <span>RANGE</span>
            <span>
              {t(language, "passive")} / {t(language, "active")}
            </span>
          </div>
          {weapons.map((weapon, index) => (
            <article className="weapon-row" key={weapon.id}>
              <span className="weapon-index">{String(index + 1).padStart(2, "0")}</span>
              <div className="weapon-name">
                <strong>{weapon.name[language]}</strong>
                <small>{weapon.id}</small>
              </div>
              <span className="weapon-owner">{weapon.owner[language]}</span>
              <span className="weapon-stats">
                <b>{weapon.attack}</b>
                <i>/</i>
                {weapon.attackCost}
              </span>
              <div className="range-tags">
                <span className={`range-tag ${weapon.range}`}>{t(language, weapon.range)}</span>
                <small>{weapon.ignoreCounterAttack ? t(language, "noCounter") : t(language, "hasCounter")}</small>
              </div>
              <div className="weapon-skills">
                <p>
                  <b>{weapon.passive?.name[language] ?? "—"}</b> {rich(weapon.passive?.description[language] ?? "")}
                </p>
                {weapon.active && (
                  <p>
                    <b>
                      {weapon.active.name[language]} · {weapon.active.cost}MP
                    </b>{" "}
                    {rich(weapon.active.description[language])}
                  </p>
                )}
              </div>
            </article>
          ))}
        </div>
      </section>
      <section className="relic-section">
        <header className="section-heading">
          <div>
            <span className="section-index">REL / PASSIVE</span>
            <h2>{t(language, "relics")}</h2>
          </div>
          <p>COMBAT LIFECYCLE HOOKS</p>
        </header>
        <div className="relic-grid">
          {relics.map((relic, index) => {
            const accent = !relic.beneficial
              ? "#ff6b7a"
              : index % 3 === 0
                ? "#ffd98e"
                : index % 3 === 1
                  ? "#7fd8ff"
                  : "#ff9ed2";
            return (
              <article className="relic-record" key={relic.id}>
                <div className="relic-emblem">
                  <EmblemCanvas id={relic.id} style={index % 2 ? "abstract" : "rune"} accent={accent} />
                </div>
                <div>
                  <span className={`relic-disposition ${!relic.beneficial ? "negative" : ""}`}>
                    {t(language, relic.beneficial ? "beneficial" : relic.subtle ? "subtle" : "negative")}
                  </span>
                  <h3>{relic.name[language]}</h3>
                  <small>{relic.id}</small>
                  <p>{rich(relic.description[language])}</p>
                </div>
              </article>
            );
          })}
        </div>
      </section>
    </div>
  );
}
