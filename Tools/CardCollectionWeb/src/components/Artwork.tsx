import { useEffect, useRef } from "react";
import type { ArtworkSpec, ArtworkStyle } from "../../shared/types.ts";
import { renderCardArtwork, renderEmblem } from "../art/engine.ts";

export function ArtworkCanvas({ spec, className = "" }: { spec: ArtworkSpec; className?: string }) {
  const ref = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const canvas = ref.current;
    if (!canvas) return;
    const draw = () => renderCardArtwork(canvas, spec);
    draw();
    const observer = new ResizeObserver(draw);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [spec]);
  return (
    <canvas
      ref={ref}
      className={`artwork-canvas ${className}`}
      role="img"
      aria-label={`${spec.style} procedural card artwork ${spec.seed}`}
    />
  );
}

export function EmblemCanvas({
  id,
  style,
  accent,
  className = "",
}: {
  id: string;
  style: ArtworkStyle;
  accent: string;
  className?: string;
}) {
  const ref = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const canvas = ref.current;
    if (!canvas) return;
    const draw = () => renderEmblem(canvas, id, style, accent);
    draw();
    const observer = new ResizeObserver(draw);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [id, style, accent]);
  return <canvas ref={ref} className={`emblem-canvas ${className}`} role="img" aria-label={`${id} emblem`} />;
}
