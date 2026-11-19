import { packColor } from "../../shared/packs.ts";
import type { Language } from "../../shared/types.ts";
import { displayPackName } from "../lib/packNames.ts";

interface PackBadgeProps {
  packId: string;
  language: Language;
  compact?: boolean;
  title?: string;
  onClick?: () => void;
}

/** 卡包（系列）徽章——收藏卡面、检视台、工坊共用。 */
export function PackBadge({ packId, language, compact = false, title, onClick }: PackBadgeProps) {
  const style = { "--pack-color": packColor(packId) } as React.CSSProperties;
  const className = `pack-badge ${compact ? "pack-badge-compact" : ""} ${onClick ? "pack-badge-clickable" : ""}`;
  const label = displayPackName(packId, language);
  if (!onClick)
    return (
      <span className={className} style={style} title={title ?? label}>
        {label}
      </span>
    );
  return (
    <button className={className} style={style} title={title ?? label} onClick={onClick} type="button">
      {label}
    </button>
  );
}
