/**
 * Deterministic seeded RNG -- the canonical randomness for all Mutator
 * operations (docs/07: server-seeded, logged, auditable; same seed =>
 * same monster, replayable forever).
 *
 * Implementation notes:
 *  - sfc32, seeded via splitmix32: small, fast, well-tested, and -- the
 *    property we actually need -- exactly reproducible from a 32-bit seed
 *    on every platform. Math.random is never used anywhere in this package.
 *  - This TypeScript implementation is the REFERENCE; the Python prototype
 *    used its own RNG and its outputs are not expected to match.
 */

export class Rng {
  private a: number;
  private b: number;
  private c: number;
  private d: number;
  private spareGauss: number | null = null;

  constructor(seed: number) {
    // splitmix32 stream to fill sfc32's state from one 32-bit seed
    let s = seed >>> 0;
    const next = () => {
      s = (s + 0x9e3779b9) >>> 0;
      let z = s;
      z = Math.imul(z ^ (z >>> 16), 0x21f0aaad);
      z = Math.imul(z ^ (z >>> 15), 0x735a2d97);
      return (z ^ (z >>> 15)) >>> 0;
    };
    this.a = next();
    this.b = next();
    this.c = next();
    this.d = next();
    for (let i = 0; i < 12; i++) this.next(); // warm up
  }

  /** Derive a seed from a string (e.g. an operation id) -- FNV-1a. */
  static seedFromString(s: string): number {
    let h = 0x811c9dc5;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 0x01000193);
    }
    return h >>> 0;
  }

  /** Uniform in [0, 1). */
  next(): number {
    const t = (((this.a + this.b) >>> 0) + this.d) >>> 0;
    this.d = (this.d + 1) >>> 0;
    this.a = this.b ^ (this.b >>> 9);
    this.b = (this.c + (this.c << 3)) >>> 0;
    this.c = ((this.c << 21) | (this.c >>> 11)) >>> 0;
    this.c = (this.c + t) >>> 0;
    return t / 4294967296;
  }

  uniform(lo: number, hi: number): number {
    return lo + this.next() * (hi - lo);
  }

  /** Standard normal via Box-Muller (cached spare for determinism). */
  gauss(mu = 0, sigma = 1): number {
    if (this.spareGauss !== null) {
      const z = this.spareGauss;
      this.spareGauss = null;
      return mu + sigma * z;
    }
    let u = 0;
    while (u === 0) u = this.next(); // avoid log(0)
    const v = this.next();
    const r = Math.sqrt(-2 * Math.log(u));
    this.spareGauss = r * Math.sin(2 * Math.PI * v);
    return mu + sigma * r * Math.cos(2 * Math.PI * v);
  }

  int(n: number): number {
    return Math.floor(this.next() * n);
  }

  choice<T>(arr: readonly T[]): T {
    if (arr.length === 0) throw new Error("choice from empty array");
    return arr[this.int(arr.length)]!;
  }

  /** Weighted choice -- one next() draw, same cost as choice(), so it
   * doesn't shift how many random numbers anything called afterward
   * consumes. Falls back to plain uniform choice if every weight is
   * non-positive (guards a caller passing an all-zero weight list). */
  weightedChoice<T>(arr: readonly T[], weights: readonly number[]): T {
    if (arr.length === 0) throw new Error("weightedChoice from empty array");
    let total = 0;
    for (const w of weights) total += w;
    if (total <= 0) return this.choice(arr);
    let r = this.next() * total;
    for (let i = 0; i < arr.length; i++) {
      r -= weights[i] ?? 0;
      if (r < 0) return arr[i]!;
    }
    return arr[arr.length - 1]!;
  }

  bool(p = 0.5): boolean {
    return this.next() < p;
  }
}
