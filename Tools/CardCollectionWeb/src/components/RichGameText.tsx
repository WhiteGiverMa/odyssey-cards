import { useEffect, useId, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { tokenizeGameText, type RichTextSegment } from "../../shared/glossary.ts";
import { TYPE_NAMES, UNIT_NAMES } from "../../shared/constants.ts";
import type { CardRecord, Language } from "../../shared/types.ts";

interface RichGameTextProps {
  text: string;
  cards: CardRecord[];
  language: Language;
  onWikiTerm: (id: string) => void;
  onSelectCard: (id: string) => void;
  className?: string;
}

interface Position {
  left: number;
  top: number;
  width: number;
}

const CATEGORY_NAMES = {
  keyword: { zh: "战斗关键词", en: "Combat keyword" },
  status: { zh: "状态效果", en: "Status effect" },
  mechanic: { zh: "空间与机制", en: "Rules & mechanics" },
} as const;

function clampPosition(anchor: DOMRect, element: HTMLElement | null): Position {
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const width = Math.min(370, viewportWidth - 24);
  const height = element?.offsetHeight ?? 190;
  const left = Math.max(12, Math.min(viewportWidth - width - 12, anchor.left + anchor.width / 2 - width / 2));
  const above = anchor.top - height - 12;
  const top = above >= 12 ? above : Math.min(viewportHeight - height - 12, anchor.bottom + 12);
  return { left, top: Math.max(12, top), width };
}

function RichLink({
  segment,
  language,
  onWikiTerm,
  onSelectCard,
}: {
  segment: Exclude<RichTextSegment, { kind: "text" }>;
  language: Language;
  onWikiTerm: (id: string) => void;
  onSelectCard: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<Position>({ left: 12, top: 12, width: 340 });
  const anchorRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const tooltipId = useId();
  const show = () => {
    if (anchorRef.current) setPosition(clampPosition(anchorRef.current.getBoundingClientRect(), popoverRef.current));
    setOpen(true);
  };
  useEffect(() => {
    if (!open || !anchorRef.current) return;
    const update = () => setPosition(clampPosition(anchorRef.current!.getBoundingClientRect(), popoverRef.current));
    const frame = requestAnimationFrame(update);
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [open]);
  const card = segment.kind === "card" ? segment.card : null;
  const term = segment.kind === "term" ? segment.term : null;
  return (
    <>
      <button
        ref={anchorRef}
        type="button"
        className={`game-text-link ${segment.kind}`}
        data-term={term?.id}
        data-card-link={card?.id}
        aria-describedby={open ? tooltipId : undefined}
        onMouseEnter={show}
        onMouseLeave={() => setOpen(false)}
        onFocus={show}
        onBlur={() => setOpen(false)}
        onDoubleClick={(event) => {
          event.preventDefault();
          event.stopPropagation();
        }}
        onClick={(event) => {
          event.preventDefault();
          event.stopPropagation();
          setOpen(false);
          if (term) onWikiTerm(term.id);
          if (card) onSelectCard(card.id);
        }}
      >
        {segment.text}
      </button>
      {open &&
        createPortal(
          <div
            ref={popoverRef}
            id={tooltipId}
            role="tooltip"
            className={`game-popover ${segment.kind}`}
            style={{ left: position.left, top: position.top, width: position.width }}
          >
            <span className="popover-kicker">
              {card
                ? language === "zh"
                  ? "关联卡牌 / CARD LINK"
                  : "CARD LINK"
                : CATEGORY_NAMES[term!.category][language]}
            </span>
            {term ? (
              <>
                <strong>{term.name[language]}</strong>
                <small>{language === "zh" ? term.name.en : term.name.zh}</small>
                <p>{term.description[language]}</p>
                <footer>{language === "zh" ? "点击前往战术百科 ↗" : "Open in tactical glossary ↗"}</footer>
              </>
            ) : card ? (
              <>
                <strong>{card.name[language]}</strong>
                <small>{card.id}</small>
                <div className="popover-card-stats">
                  <span>{card.cost} MP</span>
                  <span>{TYPE_NAMES[card.typeKey][language]}</span>
                  {card.unitClass && <span>{UNIT_NAMES[card.unitClass][language]}</span>}
                  {card.typeKey === "minion" && (
                    <span>
                      {card.attack}/{card.defense}/{card.health}
                    </span>
                  )}
                </div>
                <p>{card.description[language]}</p>
                <footer>{language === "zh" ? "点击切换卡牌检视台 ↗" : "Inspect this card ↗"}</footer>
              </>
            ) : null}
          </div>,
          document.body,
        )}
    </>
  );
}

export function RichGameText({ text, cards, language, onWikiTerm, onSelectCard, className = "" }: RichGameTextProps) {
  const segments = useMemo(() => tokenizeGameText(text, cards), [text, cards]);
  return (
    <span className={`rich-game-text ${className}`}>
      {segments.map((segment, index) =>
        segment.kind === "text" ? (
          <span key={`${index}-${segment.text.slice(0, 8)}`}>{segment.text}</span>
        ) : (
          <RichLink
            key={`${segment.kind}-${index}`}
            segment={segment}
            language={language}
            onWikiTerm={onWikiTerm}
            onSelectCard={onSelectCard}
          />
        ),
      )}
    </span>
  );
}
