import type { ArtworkSpec, ArtworkStyle } from "../../shared/types.ts";

const WIDTH = 600;
const HEIGHT = 500;
const TAU = Math.PI * 2;
const cache = new Map<string, HTMLCanvasElement>();

function hash(value: string): number {
  let result = 2166136261 >>> 0;
  for (let index = 0; index < value.length; index++) {
    result ^= value.charCodeAt(index);
    result = Math.imul(result, 16777619) >>> 0;
  }
  return result & 0x7fffffff;
}

function randomFactory(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state += 0x6d2b79f5;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

function rgba(hex: string, alpha: number): string {
  if (!hex.startsWith("#")) return `rgba(200,160,255,${alpha})`;
  const value = Number.parseInt(hex.slice(1), 16);
  return `rgba(${(value >> 16) & 255},${(value >> 8) & 255},${value & 255},${alpha})`;
}

function polygon(context: CanvasRenderingContext2D, points: Array<[number, number]>, close = true): void {
  context.beginPath();
  points.forEach(([x, y], index) => (index ? context.lineTo(x, y) : context.moveTo(x, y)));
  if (close) context.closePath();
}

function regular(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
  sides: number,
  rotation = -Math.PI / 2,
): void {
  polygon(
    context,
    Array.from({ length: sides }, (_, index) => {
      const angle = rotation + (index * TAU) / sides;
      return [x + Math.cos(angle) * radius, y + Math.sin(angle) * radius];
    }),
  );
}

function star(context: CanvasRenderingContext2D, x: number, y: number, radius: number, points = 5, inner = 0.42): void {
  polygon(
    context,
    Array.from({ length: points * 2 }, (_, index) => {
      const angle = -Math.PI / 2 + (index * Math.PI) / points;
      const r = index % 2 ? radius * inner : radius;
      return [x + Math.cos(angle) * r, y + Math.sin(angle) * r];
    }),
  );
}

function line(
  context: CanvasRenderingContext2D,
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  color: string,
  width = 1,
): void {
  context.beginPath();
  context.moveTo(x1, y1);
  context.lineTo(x2, y2);
  context.strokeStyle = color;
  context.lineWidth = width;
  context.stroke();
}

function ring(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
  color: string,
  width = 1,
  start = 0,
  end = TAU,
): void {
  context.beginPath();
  context.arc(x, y, radius, start, end);
  context.strokeStyle = color;
  context.lineWidth = width;
  context.stroke();
}

function drawBackground(context: CanvasRenderingContext2D, spec: ArtworkSpec, random: () => number): void {
  const base = context.createLinearGradient(0, 0, WIDTH, HEIGHT);
  base.addColorStop(0, spec.baseColor);
  base.addColorStop(0.48, "#111323");
  base.addColorStop(1, "#090b14");
  context.fillStyle = base;
  context.fillRect(0, 0, WIDTH, HEIGHT);
  const secondary = spec.style === "rune" ? "#ff9ed2" : spec.style === "mecha" ? "#ff9e7d" : "#c9a0ff";
  for (let index = 0; index < 4; index++) {
    const x = WIDTH * (0.15 + random() * 0.7);
    const y = HEIGHT * (0.1 + random() * 0.75);
    const radius = 170 + random() * 210;
    const gradient = context.createRadialGradient(x, y, 0, x, y, radius);
    gradient.addColorStop(0, rgba(index % 2 ? secondary : spec.accentColor, 0.13 + random() * 0.08));
    gradient.addColorStop(0.45, rgba(spec.accentColor, 0.045));
    gradient.addColorStop(1, rgba(spec.baseColor, 0));
    context.fillStyle = gradient;
    context.fillRect(0, 0, WIDTH, HEIGHT);
  }
  context.save();
  context.strokeStyle = rgba(spec.accentColor, 0.045);
  context.lineWidth = 1;
  for (let x = 0; x < WIDTH; x += 24) line(context, x, 0, x, HEIGHT, rgba(spec.accentColor, 0.045));
  for (let y = 0; y < HEIGHT; y += 24) line(context, 0, y, WIDTH, y, rgba(spec.accentColor, 0.045));
  for (let y = 1; y < HEIGHT; y += 4) line(context, 0, y, WIDTH, y, "rgba(255,255,255,0.012)");
  context.restore();
  for (let index = 0; index < spec.starCount * 3; index++) {
    const x = random() * WIDTH;
    const y = random() * HEIGHT;
    const radius = random() < 0.92 ? 0.45 + random() * 1.2 : 1.8 + random() * 1.8;
    context.fillStyle =
      random() < 0.72 ? `rgba(238,242,255,${0.12 + random() * 0.65})` : rgba(spec.accentColor, 0.25 + random() * 0.6);
    context.beginPath();
    context.arc(x, y, radius, 0, TAU);
    context.fill();
    if (radius > 2) {
      line(context, x - radius * 2.4, y, x + radius * 2.4, y, rgba(spec.accentColor, 0.35));
      line(context, x, y - radius * 2.4, x, y + radius * 2.4, rgba(spec.accentColor, 0.35));
    }
  }
  const constellation = Array.from({ length: 8 }, () => ({ x: 45 + random() * 510, y: 45 + random() * 390 }));
  context.setLineDash([2, 7]);
  context.beginPath();
  constellation.forEach((point, index) =>
    index ? context.lineTo(point.x, point.y) : context.moveTo(point.x, point.y),
  );
  context.strokeStyle = rgba(spec.accentColor, 0.12);
  context.lineWidth = 0.8;
  context.stroke();
  context.setLineDash([]);
  for (const point of constellation) {
    context.fillStyle = rgba(spec.accentColor, 0.6);
    context.fillRect(point.x - 1, point.y - 1, 2, 2);
  }
}

function symbolPath(context: CanvasRenderingContext2D, index: number, radius: number): void {
  switch (index) {
    case 0:
      star(context, 0, 0, radius, 5);
      break;
    case 1:
      polygon(context, [
        [0, -radius],
        [radius * 0.19, radius * 0.22],
        [0, radius * 0.1],
        [-radius * 0.19, radius * 0.22],
      ]);
      context.moveTo(-radius * 0.55, radius * 0.32);
      context.lineTo(radius * 0.55, radius * 0.32);
      context.moveTo(0, radius * 0.22);
      context.lineTo(0, radius * 0.92);
      break;
    case 2:
      polygon(context, [
        [0, -radius],
        [radius * 0.8, -radius * 0.55],
        [radius * 0.64, radius * 0.45],
        [0, radius],
        [-radius * 0.64, radius * 0.45],
        [-radius * 0.8, -radius * 0.55],
      ]);
      break;
    case 3:
      context.arc(0, 0, radius, 0, TAU);
      context.moveTo(radius * 0.56, 0);
      context.arc(0, 0, radius * 0.56, 0, TAU);
      break;
    case 4:
      polygon(context, [
        [0, -radius],
        [radius * 0.72, 0],
        [0, radius],
        [-radius * 0.72, 0],
      ]);
      break;
    case 5:
      polygon(context, [
        [radius * 0.28, -radius],
        [-radius * 0.42, radius * 0.08],
        [radius * 0.02, radius * 0.08],
        [-radius * 0.28, radius],
        [radius * 0.5, -radius * 0.18],
        [radius * 0.08, -radius * 0.18],
      ]);
      break;
    case 6:
      context.arc(0, 0, radius * 0.72, 0, TAU);
      context.moveTo(-radius, 0);
      context.lineTo(radius, 0);
      context.moveTo(0, -radius);
      context.lineTo(0, radius);
      break;
    case 7:
      for (let tooth = 0; tooth < 16; tooth++) {
        const angle = (tooth * TAU) / 16;
        const r = tooth % 2 ? radius * 0.76 : radius;
        const x = Math.cos(angle) * r;
        const y = Math.sin(angle) * r;
        tooth ? context.lineTo(x, y) : context.moveTo(x, y);
      }
      context.closePath();
      context.moveTo(radius * 0.34, 0);
      context.arc(0, 0, radius * 0.34, 0, TAU);
      break;
    case 8:
      context.moveTo(-radius, 0);
      context.quadraticCurveTo(0, -radius * 0.95, radius, 0);
      context.quadraticCurveTo(0, radius * 0.95, -radius, 0);
      context.moveTo(radius * 0.25, 0);
      context.arc(0, 0, radius * 0.25, 0, TAU);
      break;
    case 9:
      context.moveTo(0, radius * 0.85);
      context.bezierCurveTo(-radius * 1.25, radius * 0.15, -radius * 0.72, -radius * 0.9, 0, -radius * 0.28);
      context.bezierCurveTo(radius * 0.72, -radius * 0.9, radius * 1.25, radius * 0.15, 0, radius * 0.85);
      break;
    case 10:
      regular(context, 0, radius * 0.08, radius, 3);
      break;
    default:
      context.moveTo(-radius, -radius * 0.3);
      context.quadraticCurveTo(-radius * 0.55, radius * 0.5, 0, -radius * 0.08);
      context.quadraticCurveTo(radius * 0.55, radius * 0.5, radius, -radius * 0.3);
      context.moveTo(-radius * 0.78, radius * 0.16);
      context.quadraticCurveTo(-radius * 0.42, radius * 0.72, 0, radius * 0.2);
      context.quadraticCurveTo(radius * 0.42, radius * 0.72, radius * 0.78, radius * 0.16);
  }
}

function drawMainSymbol(context: CanvasRenderingContext2D, spec: ArtworkSpec): void {
  context.save();
  context.translate(300, 235);
  context.rotate(spec.rotation);
  for (const pass of [0, 1]) {
    context.beginPath();
    symbolPath(context, spec.symbolIndex, pass ? 91 : 103);
    context.strokeStyle = pass ? rgba(spec.accentColor, 0.9) : "rgba(4,6,14,0.85)";
    context.lineWidth = pass ? 2.1 : 10;
    context.lineJoin = "round";
    context.stroke();
    if (spec.symbolIndex === 5 || spec.symbolIndex === 9) {
      context.fillStyle = rgba(spec.accentColor, pass ? 0.09 : 0.3);
      context.fill();
    }
  }
  context.restore();
}

function drawRune(context: CanvasRenderingContext2D, spec: ArtworkSpec): void {
  const color = rgba(spec.accentColor, 0.52);
  for (const [radius, ticks] of [
    [160, 96],
    [137, 72],
    [119, 36],
  ] as const) {
    ring(context, 300, 235, radius, rgba(spec.accentColor, radius === 137 ? 0.22 : 0.43), radius === 160 ? 1.4 : 0.8);
    for (let index = 0; index < ticks; index++) {
      const angle = (index * TAU) / ticks;
      const length = index % 8 === 0 ? 9 : index % 4 === 0 ? 5 : 2.5;
      line(
        context,
        300 + Math.cos(angle) * (radius - length),
        235 + Math.sin(angle) * (radius - length),
        300 + Math.cos(angle) * radius,
        235 + Math.sin(angle) * radius,
        color,
        index % 8 === 0 ? 1.4 : 0.65,
      );
    }
  }
  context.save();
  context.translate(300, 235);
  context.rotate(-spec.rotation * 2);
  regular(context, 0, 0, 130, 6, Math.PI / 6);
  context.strokeStyle = rgba("#ff9ed2", 0.2);
  context.lineWidth = 1;
  context.stroke();
  regular(context, 0, 0, 130, 3);
  context.strokeStyle = rgba(spec.accentColor, 0.22);
  context.stroke();
  context.restore();
  for (let index = 0; index < 8; index++) {
    const angle = (index * TAU) / 8 + spec.rotation;
    const x = 300 + Math.cos(angle) * 181;
    const y = 235 + Math.sin(angle) * 181;
    context.save();
    context.translate(x, y);
    context.rotate(angle);
    star(context, 0, 0, index % 2 ? 5 : 8, 4, 0.18);
    context.strokeStyle = rgba(index % 2 ? "#ff9ed2" : spec.accentColor, 0.75);
    context.lineWidth = 1;
    context.stroke();
    context.restore();
  }
  context.lineWidth = 5;
  ring(context, 300, 235, 174, rgba("#ff9ed2", 0.16), 5, Math.PI * 0.15, Math.PI * 0.8);
  ring(context, 300, 235, 174, rgba(spec.accentColor, 0.3), 1, Math.PI * 1.1, Math.PI * 1.85);
}

function drawMecha(context: CanvasRenderingContext2D, spec: ArtworkSpec, random: () => number): void {
  const color = rgba(spec.accentColor, 0.48);
  for (const side of [-1, 1]) {
    context.save();
    if (side === 1) {
      context.translate(600, 0);
      context.scale(-1, 1);
    }
    polygon(context, [
      [54, 116],
      [146, 76],
      [196, 113],
      [157, 156],
      [179, 199],
      [92, 220],
      [46, 178],
    ]);
    context.fillStyle = rgba(spec.accentColor, 0.035);
    context.fill();
    context.strokeStyle = color;
    context.lineWidth = 1.4;
    context.stroke();
    polygon(context, [
      [62, 283],
      [150, 268],
      [181, 310],
      [151, 351],
      [169, 393],
      [93, 419],
      [43, 353],
    ]);
    context.fillStyle = rgba(spec.accentColor, 0.025);
    context.fill();
    context.stroke();
    for (let index = 0; index < 5; index++)
      line(context, 70, 139 + index * 10, 132 + index * 3, 116 + index * 10, rgba(spec.accentColor, 0.17));
    context.restore();
  }
  for (const radius of [142, 107, 60])
    ring(
      context,
      300,
      235,
      radius,
      rgba(spec.accentColor, radius === 107 ? 0.22 : 0.42),
      radius === 60 ? 1.4 : 0.8,
      0.18,
      TAU - 0.18,
    );
  for (let index = 0; index < 64; index++) {
    const angle = (index * TAU) / 64;
    const length = index % 8 === 0 ? 13 : 5;
    line(
      context,
      300 + Math.cos(angle) * 149,
      235 + Math.sin(angle) * 149,
      300 + Math.cos(angle) * (149 - length),
      235 + Math.sin(angle) * (149 - length),
      color,
      index % 8 === 0 ? 1.5 : 0.7,
    );
  }
  line(context, 91, 235, 509, 235, rgba(spec.accentColor, 0.22));
  line(context, 300, 26, 300, 444, rgba(spec.accentColor, 0.22));
  context.setLineDash([9, 7]);
  context.beginPath();
  context.moveTo(65, 404);
  context.bezierCurveTo(176, 315, 391, 380, 540, 99);
  context.strokeStyle = rgba("#ff9e7d", 0.4);
  context.lineWidth = 1.2;
  context.stroke();
  context.setLineDash([]);
  context.fillStyle = rgba(spec.accentColor, 0.65);
  context.font = "10px Consolas, monospace";
  context.fillText(`TRK ${String(spec.seed % 997).padStart(3, "0")}`, 458, 59);
  context.fillText(`AZ ${(spec.rotation * 57.2958).toFixed(2)}`, 464, 74);
  context.fillText("LOCK / PASSIVE", 48, 450);
  for (let index = 0; index < 10; index++) {
    context.fillStyle = rgba(spec.accentColor, 0.14 + random() * 0.4);
    context.fillRect(477 + index * 6, 410 - random() * 24, 3, 12 + random() * 24);
  }
  const core = context.createRadialGradient(300, 235, 0, 300, 235, 50);
  core.addColorStop(0, rgba(spec.accentColor, 0.24));
  core.addColorStop(1, rgba(spec.accentColor, 0));
  context.fillStyle = core;
  context.fillRect(245, 180, 110, 110);
}

function drawAbstract(context: CanvasRenderingContext2D, spec: ArtworkSpec): void {
  context.save();
  context.translate(300, 235);
  context.rotate(spec.rotation);
  for (let layer = 0; layer < 3; layer++) {
    context.save();
    context.rotate((layer * Math.PI) / 6);
    regular(context, 0, 0, 118 - layer * 17, 4, Math.PI / 4);
    context.strokeStyle = rgba(spec.accentColor, 0.42 - layer * 0.08);
    context.lineWidth = 1.2;
    context.stroke();
    context.restore();
  }
  for (const angle of [0, TAU / 3, (TAU * 2) / 3]) {
    line(context, 0, 0, Math.cos(angle) * 155, Math.sin(angle) * 155, rgba(spec.accentColor, 0.25));
    line(context, 0, 0, -Math.cos(angle) * 155, -Math.sin(angle) * 155, rgba(spec.accentColor, 0.25));
  }
  context.restore();
  context.save();
  context.translate(300, 235);
  context.scale(1, 0.34);
  context.rotate(-0.28);
  ring(context, 0, 0, 196, rgba(spec.accentColor, 0.42), 4, 0.2, Math.PI * 1.55);
  ring(context, 0, 0, 173, rgba("#ff9ed2", 0.2), 1.2);
  context.restore();
  context.beginPath();
  for (let x = 45; x <= 555; x += 4) {
    const y = 407 + Math.sin(x * 0.045 + spec.rotation * 10) * 13 + Math.sin(x * 0.013) * 8;
    x === 45 ? context.moveTo(x, y) : context.lineTo(x, y);
  }
  context.strokeStyle = rgba(spec.accentColor, 0.38);
  context.lineWidth = 1.3;
  context.stroke();
  for (let x = 45; x < 555; x += 24) line(context, x, 430, x, 435 + ((x / 24) % 3) * 4, rgba(spec.accentColor, 0.25));
}

function paint(spec: ArtworkSpec): HTMLCanvasElement {
  const key = `${spec.seed}-${spec.symbolIndex}-${spec.style}-${spec.glowColor}`;
  const existing = cache.get(key);
  if (existing) return existing;
  const canvas = document.createElement("canvas");
  canvas.width = WIDTH * 2;
  canvas.height = HEIGHT * 2;
  const context = canvas.getContext("2d");
  if (!context) throw new Error("Canvas 2D is unavailable");
  context.scale(2, 2);
  const random = randomFactory(spec.seed);
  drawBackground(context, spec, random);
  if (spec.style === "rune") drawRune(context, spec);
  else if (spec.style === "mecha") drawMecha(context, spec, random);
  else drawAbstract(context, spec);
  drawMainSymbol(context, spec);
  if (spec.glowColor !== "transparent") {
    ring(context, 300, 235, 190, rgba(spec.glowColor, 0.34), 1.5);
    ring(context, 300, 235, 195, rgba(spec.glowColor, 0.1), 5);
  }
  const vignette = context.createRadialGradient(300, 225, 140, 300, 235, 385);
  vignette.addColorStop(0, "rgba(4,5,12,0)");
  vignette.addColorStop(1, "rgba(4,5,12,0.8)");
  context.fillStyle = vignette;
  context.fillRect(0, 0, WIDTH, HEIGHT);
  context.strokeStyle = rgba(spec.accentColor, 0.24);
  context.lineWidth = 1;
  context.strokeRect(10.5, 10.5, 579, 479);
  context.fillStyle = rgba(spec.accentColor, 0.54);
  context.font = "9px Consolas, monospace";
  context.fillText(`ODYSSEY / ${spec.seed.toString(16).toUpperCase().padStart(8, "0")}`, 24, 30);
  context.textAlign = "right";
  context.fillText(`${spec.style.toUpperCase()} · ${String(spec.symbolIndex + 1).padStart(2, "0")}`, 576, 478);
  cache.set(key, canvas);
  return canvas;
}

export function renderCardArtwork(canvas: HTMLCanvasElement, spec: ArtworkSpec): void {
  const bounds = canvas.getBoundingClientRect();
  if (bounds.width < 1 || bounds.height < 1) return;
  const dpr = Math.min(3, Math.max(1, window.devicePixelRatio || 1));
  const width = Math.round(bounds.width * dpr);
  const height = Math.round(bounds.height * dpr);
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  const context = canvas.getContext("2d");
  if (!context) return;
  context.clearRect(0, 0, width, height);
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = "high";
  context.drawImage(paint(spec), 0, 0, width, height);
}

export function renderEmblem(canvas: HTMLCanvasElement, id: string, style: ArtworkStyle, accent: string): void {
  const bounds = canvas.getBoundingClientRect();
  if (bounds.width < 1) return;
  const size = Math.round(bounds.width * Math.min(2, window.devicePixelRatio || 1));
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext("2d");
  if (!context) return;
  context.scale(size / 400, size / 400);
  const seed = hash(id);
  const random = randomFactory(seed);
  const background = context.createRadialGradient(200, 180, 10, 200, 200, 200);
  background.addColorStop(0, rgba(accent, 0.18));
  background.addColorStop(1, "#0b0c17");
  context.fillStyle = background;
  context.fillRect(0, 0, 400, 400);
  for (let index = 0; index < 48; index++) {
    context.fillStyle = rgba(accent, 0.15 + random() * 0.45);
    context.fillRect(random() * 400, random() * 400, 1.5, 1.5);
  }
  for (const radius of [162, 143, 116])
    ring(context, 200, 200, radius, rgba(accent, radius === 143 ? 0.18 : 0.5), radius === 162 ? 1.2 : 0.7);
  for (let index = 0; index < 48; index++) {
    const angle = (index * TAU) / 48;
    line(
      context,
      200 + Math.cos(angle) * 151,
      200 + Math.sin(angle) * 151,
      200 + Math.cos(angle) * (index % 4 ? 155 : 162),
      200 + Math.sin(angle) * (index % 4 ? 155 : 162),
      rgba(accent, 0.48),
    );
  }
  context.save();
  context.translate(200, 200);
  context.rotate((seed % 12) * 0.018);
  context.beginPath();
  symbolPath(context, seed % 12, 91);
  context.strokeStyle = rgba(accent, 0.88);
  context.lineWidth = 2.2;
  context.stroke();
  context.restore();
  context.fillStyle = rgba(accent, 0.8);
  context.textAlign = "center";
  context.font = "11px Consolas, monospace";
  context.fillText(
    style === "rune" ? "ASTRAL SIGNATURE" : style === "mecha" ? "TACTICAL FRAME" : "ODYSSEY RELIC",
    200,
    373,
  );
}
