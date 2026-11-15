import type { ArtworkSpec, ArtworkStyle } from "../shared/types.ts";

export function stableHash(value: string): number {
  let hash = 2166136261 >>> 0;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619) >>> 0;
  }
  return hash & 0x7fffffff;
}

/** .NET's legacy seeded System.Random, used by the Godot artwork generator. */
class DotNetRandom {
  private readonly seedArray = new Int32Array(56);
  private inext = 0;
  private inextp = 21;

  public constructor(seed: number) {
    const subtraction = seed === -2147483648 ? 2147483647 : Math.abs(seed);
    let mj = 161803398 - subtraction;
    this.seedArray[55] = mj;
    let mk = 1;
    for (let i = 1; i < 55; i++) {
      const ii = (21 * i) % 55;
      this.seedArray[ii] = mk;
      mk = mj - mk;
      if (mk < 0) mk += 2147483647;
      mj = this.seedArray[ii] ?? 0;
    }
    for (let k = 1; k < 5; k++) {
      for (let i = 1; i < 56; i++) {
        this.seedArray[i] = (this.seedArray[i] ?? 0) - (this.seedArray[1 + ((i + 30) % 55)] ?? 0);
        if ((this.seedArray[i] ?? 0) < 0) this.seedArray[i] = (this.seedArray[i] ?? 0) + 2147483647;
      }
    }
  }

  private sample(): number {
    let inext = this.inext + 1;
    if (inext >= 56) inext = 1;
    let inextp = this.inextp + 1;
    if (inextp >= 56) inextp = 1;
    let value = (this.seedArray[inext] ?? 0) - (this.seedArray[inextp] ?? 0);
    if (value === 2147483647) value--;
    if (value < 0) value += 2147483647;
    this.seedArray[inext] = value;
    this.inext = inext;
    this.inextp = inextp;
    return value;
  }

  public next(max: number): number {
    return Math.floor(this.sample() * (1 / 2147483647) * max);
  }

  public nextDouble(): number {
    return this.sample() * (1 / 2147483647);
  }
}

export function resolveArtwork(
  cardId: string,
  type: number,
  rarity: number,
  mechanicTags: number,
  domainId: string,
  heroTheme: string | null,
): ArtworkSpec {
  const seed = stableHash(cardId);
  const random = new DotNetRandom(seed);
  const isDomain = Boolean(domainId) || (mechanicTags & 1024) !== 0;
  const colors = isDomain ? ["#2a1a3d", "#c9a0ff"] : type === 0 ? ["#3d2f1a", "#ffd98e"] : ["#16283d", "#7fd8ff"];
  const style: ArtworkStyle =
    heroTheme === "ayame" || heroTheme === "redpantsu"
      ? "rune"
      : heroTheme === "rie" || heroTheme === "sokou"
        ? "mecha"
        : "abstract";
  const glow =
    rarity === 1
      ? "#ffd600"
      : rarity === 2
        ? "#bfc7d9"
        : rarity === 3
          ? "#cc8033"
          : rarity === 5
            ? "#ff8c00"
            : "transparent";
  return {
    seed,
    symbolIndex: random.next(12),
    starCount: 18 + random.next(15),
    rotation: (random.nextDouble() - 0.5) * 0.35,
    style,
    baseColor: colors[0] ?? "#16283d",
    accentColor: colors[1] ?? "#7fd8ff",
    glowColor: glow,
  };
}

export function seededRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state += 0x6d2b79f5;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}
