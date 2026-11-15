import type { UnitClass } from "../../shared/types.ts";
import { CLASS_COLORS, UNIT_NAMES } from "../../shared/constants.ts";

export function UnitBadge({
  unitClass,
  showLabel = false,
  language = "zh",
  size = 28,
}: {
  unitClass: UnitClass;
  showLabel?: boolean;
  language?: "zh" | "en";
  size?: number;
}) {
  return (
    <span
      className="unit-badge"
      style={
        {
          "--unit-color": CLASS_COLORS[unitClass],
          width: showLabel ? "auto" : size,
          height: size,
        } as React.CSSProperties
      }
      title={UNIT_NAMES[unitClass][language]}
    >
      <svg width={size * 0.72} height={size * 0.72} viewBox="0 0 32 32" fill="none" aria-hidden="true">
        {unitClass === "vanguard" && (
          <>
            <path
              d="M16 3 26 7v8c0 7-4.2 11.2-10 14-5.8-2.8-10-7-10-14V7l10-4Z"
              stroke="currentColor"
              strokeWidth="1.6"
            />
            <path d="m16 7 5.5 2.4v5.4c0 4-2.2 6.8-5.5 8.8V7Z" fill="currentColor" opacity=".28" />
            <path d="M16 7v16.5M9.5 13.5h13" stroke="currentColor" strokeWidth="1.2" />
          </>
        )}
        {unitClass === "assault" && (
          <>
            <path d="M5 20h22l-2 6H7l-2-6Zm3-7h15l4 7H4l4-7Z" stroke="currentColor" strokeWidth="1.6" />
            <path d="M12 13V8h7l4 5M18 8V5h10M9 23h14" stroke="currentColor" strokeWidth="1.6" />
            <circle cx="10" cy="23" r="2" fill="currentColor" />
            <circle cx="22" cy="23" r="2" fill="currentColor" />
          </>
        )}
        {unitClass === "support" && (
          <>
            <path d="M7 25h18M10 25l3-9h7l3 9M15 16V9" stroke="currentColor" strokeWidth="1.6" />
            <path d="m12 13 13-8 2 3-13 8" stroke="currentColor" strokeWidth="2" />
            <circle cx="16.5" cy="20" r="3" stroke="currentColor" strokeWidth="1.4" />
            <path d="M5 28h22" stroke="currentColor" strokeWidth="1.4" />
          </>
        )}
        {unitClass === "skirmisher" && (
          <>
            <path
              d="M15 13C10 6 5 5 3 6c3 2 4 6 4 10l8 4 1-7Zm2 0c5-7 10-8 12-7-3 2-4 6-4 10l-8 4-1-7Z"
              stroke="currentColor"
              strokeWidth="1.5"
            />
            <path d="m16 9 3 11-3 8-3-8 3-11Z" stroke="currentColor" strokeWidth="1.4" />
            <path d="m7 11 6 3M25 11l-6 3" stroke="currentColor" opacity=".55" />
          </>
        )}
      </svg>
      {showLabel && <span>{UNIT_NAMES[unitClass][language]}</span>}
    </span>
  );
}
