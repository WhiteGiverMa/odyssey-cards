import { useRef } from "react";
import type { CardRecord, Language } from "../../shared/types.ts";
import { KEYWORD_NAMES, RARITY_COLORS, RARITY_NAMES, TYPE_NAMES } from "../../shared/constants.ts";
import { t } from "../i18n.ts";
import { ArtworkCanvas } from "./Artwork.tsx";
import { UnitBadge } from "./UnitBadge.tsx";
import { RichGameText } from "./RichGameText.tsx";
import { PackBadge } from "./PackBadge.tsx";

interface CardViewProps {
  card: CardRecord;
  cards: CardRecord[];
  language: Language;
  onWikiTerm: (id: string) => void;
  palette: "classic" | "new";
  deckCount?: number;
  onInspect: (id: string) => void;
  onAdd: (id: string) => void;
  large?: boolean;
}

export function CardView({
  card,
  cards,
  language,
  palette,
  deckCount = 0,
  onInspect,
  onAdd,
  onWikiTerm,
  large = false,
}: CardViewProps) {
  const ref = useRef<HTMLElement>(null);
  const rarityColor = RARITY_COLORS[palette][card.rarity] ?? "#c8c8c8";
  const addDisabled = !card.buildable || deckCount >= card.maxCopies;
  const tilt = (event: React.PointerEvent<HTMLElement>) => {
    if (large || event.pointerType === "touch" || !ref.current) return;
    const bounds = ref.current.getBoundingClientRect();
    const x = (event.clientX - bounds.left) / bounds.width;
    const y = (event.clientY - bounds.top) / bounds.height;
    ref.current.style.setProperty("--tilt-x", `${(0.5 - y) * 7}deg`);
    ref.current.style.setProperty("--tilt-y", `${(x - 0.5) * 9}deg`);
    ref.current.style.setProperty("--shine-x", `${x * 100}%`);
    ref.current.style.setProperty("--shine-y", `${y * 100}%`);
  };
  const resetTilt = () => {
    ref.current?.style.setProperty("--tilt-x", "0deg");
    ref.current?.style.setProperty("--tilt-y", "0deg");
  };
  return (
    <article
      ref={ref}
      className={`game-card ${large ? "game-card-large" : ""} ${deckCount ? "in-deck" : ""}`}
      style={{ "--rarity": rarityColor } as React.CSSProperties}
      onPointerMove={tilt}
      onPointerLeave={resetTilt}
      onClick={() => !large && onInspect(card.id)}
      onDoubleClick={() => !large && !addDisabled && onAdd(card.id)}
      onKeyDown={(event) => {
        if (large || event.target !== event.currentTarget) return;
        if (event.key === "Enter") onInspect(card.id);
        if (event.key === "+" && !addDisabled) onAdd(card.id);
      }}
      role={large ? undefined : "button"}
      tabIndex={large ? undefined : 0}
      aria-label={`${card.name[language]}, ${card.cost} ${t(language, "deployCost")}`}
    >
      <div className="card-metal-frame">
        <header className="card-title-row">
          <span className="card-type-index">
            {card.typeKey === "minion" ? "U" : card.typeKey === "spell" ? "O" : card.typeKey === "domain" ? "D" : "S"}
          </span>
          <h3>{card.name[language]}</h3>
          {deckCount > 0 && <span className="deck-count-tag">×{deckCount}</span>}
        </header>
        <div className="card-art-frame">
          <ArtworkCanvas spec={card.artwork} />
          <div className="card-scanlines" />
          <div className="mana-crystal deploy-crystal" title={t(language, "deployCost")}>
            <span>{card.cost}</span>
          </div>
          {card.typeKey === "minion" && (
            <div className="mana-crystal oil-crystal" title={t(language, "actionCost")}>
              <span>{card.actionCost}</span>
            </div>
          )}
          {card.unitClass && (
            <div className="card-unit-badge">
              <UnitBadge unitClass={card.unitClass} language={language} size={31} />
            </div>
          )}
          <div className="art-seed">#{card.artwork.seed.toString(16).toUpperCase().padStart(8, "0")}</div>
        </div>
        <div className="card-text-body">
          <div className="card-taxonomy">
            <PackBadge packId={card.packId} language={language} compact title={t(language, "packSeries")} />
            <span>{TYPE_NAMES[card.typeKey][language]}</span>
            {card.tactical && (
              <>
                <i /> <span>{card.tactical.role[language]}</span>
              </>
            )}
          </div>
          <p>
            <RichGameText
              text={card.description[language] || "—"}
              cards={cards}
              language={language}
              onWikiTerm={onWikiTerm}
              onSelectCard={onInspect}
            />
          </p>
        </div>
        {card.typeKey === "minion" ? (
          <div className="card-statline">
            <div className="armor-stat attack-stat">
              <small>ATK</small>
              <strong>{card.attack}</strong>
            </div>
            <div className="armor-stat defense-stat">
              <small>DEF</small>
              <strong>{card.defense}</strong>
            </div>
            <div className="armor-stat health-stat">
              <small>HP</small>
              <strong>{card.health}</strong>
            </div>
          </div>
        ) : (
          <div className="card-keyword-row">
            {card.keywordKeys
              .filter((key) => key !== "none")
              .slice(0, 3)
              .map((key) => (
                <span key={key}>{KEYWORD_NAMES[key]?.[language] ?? key}</span>
              ))}
            {!card.keywordKeys.length && <span>{TYPE_NAMES[card.typeKey][language]}</span>}
          </div>
        )}
        <footer className="card-rarity-row">
          <span className="rarity-code">{String(card.rarity).padStart(2, "0")}</span>
          <span>{RARITY_NAMES[card.rarity]?.[language] ?? card.rarityKey}</span>
          <span className="rarity-line" />
          {!large && (
            <button
              className="card-add"
              disabled={addDisabled}
              title={
                !card.buildable
                  ? t(language, "illegalCard")
                  : deckCount >= card.maxCopies
                    ? t(language, "copyLimit", { limit: card.maxCopies })
                    : t(language, "add")
              }
              onClick={(event) => {
                event.stopPropagation();
                if (!addDisabled) onAdd(card.id);
              }}
              aria-label={`${t(language, "add")} ${card.name[language]}`}
            >
              {card.buildable ? "+" : "—"}
            </button>
          )}
        </footer>
        <div className="card-metal-shine" />
      </div>
    </article>
  );
}
