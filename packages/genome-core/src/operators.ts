/**
 * The three Mutator operators (docs/06, 15): Mutate, Splice, Graft.
 *
 * All randomness comes from a caller-supplied Rng -- on the server this is
 * seeded per-operation and logged (docs/07 OperationLog.serverSeed).
 * Operators are pure: they never modify inputs and always return genomes
 * that pass validation (closure is enforced by tests).
 */

import {
  BODY_AXES,
  BRAIN_AXES,
  BRAIN_TIERS,
  GENOME_VERSION,
  PART_AXES,
  SLOT_NAMES,
  clamp01,
  type BodyGenes,
  type BrainGenes,
  type BrainTier,
  type Genome,
  type Params4,
  type Params5,
  type Params6,
  type PartAllele,
  type SlotName,
} from "./genome.js";
import {
  BODY_PLANS,
  familiesInClass,
  homologOf,
  originOf,
  type Origin,
} from "./catalog.js";
import type { Rng } from "./rng.js";

export interface MutateOptions {
  /** Per-gene mutation probability. */
  rate?: number;
  /** Gaussian drift sigma on the 0-1 scale. */
  sigma?: number;
  /** Probability of a part-family jump (within homolog AND origin). */
  familyJump?: number;
  /** Component feeding (docs/06): the fed slot is 3x as likely to change
   * and to jump family. */
  biasSlot?: SlotName;
  /** Probability the body plan jumps (cross-plan is the rare jackpot). */
  planJump?: number;
  /** Probability brain tier drifts one step. */
  tierShift?: number;
}

const DEFAULTS: Required<Omit<MutateOptions, "biasSlot">> = {
  rate: 0.45,
  sigma: 0.16,
  familyJump: 0.1,
  planJump: 0.03,
  tierShift: 0.06,
};

const FEED_BOOST = 3;

// ---- random generation -------------------------------------------------------

export function randomAllele(
  slot: SlotName,
  rng: Rng,
  origins: readonly Origin[] = ["organic"],
): PartAllele {
  const family = rng.choice(familiesInClass(slot, origins));
  return { family, params: sixOf(() => rng.next()) };
}

export function randomBody(rng: Rng, plan?: string): BodyGenes {
  const p = plan ?? rng.choice(Object.keys(BODY_PLANS).sort());
  return { plan: p, params: fourOf(() => rng.next()) };
}

export function randomBrain(rng: Rng, tier?: BrainTier): BrainGenes {
  const t = tier ?? rng.choice(BRAIN_TIERS);
  return { tier: t, params: fiveOf(() => rng.next()) };
}

export function randomGenome(
  rng: Rng,
  opts: { plan?: string; tier?: BrainTier; origins?: readonly Origin[] } = {},
): Genome {
  const slots = {} as Record<SlotName, PartAllele>;
  for (const s of SLOT_NAMES) slots[s] = randomAllele(s, rng, opts.origins);
  return {
    genomeVersion: GENOME_VERSION,
    parentIds: [],
    body: randomBody(rng, opts.plan),
    brain: randomBrain(rng, opts.tier),
    slots,
  };
}

// ---- Mutate -------------------------------------------------------------------

export function mutate(g: Genome, rng: Rng, opts: MutateOptions = {}): Genome {
  const o = { ...DEFAULTS, ...opts };
  const slots = {} as Record<SlotName, PartAllele>;

  for (const slot of SLOT_NAMES) {
    const allele = g.slots[slot];
    if (originOf(allele.family) === "tech") {
      slots[slot] = allele; // issued equipment: flesh mutates, steel doesn't
      continue;
    }
    const boost = slot === opts.biasSlot ? FEED_BOOST : 1;
    let family = allele.family;
    if (rng.next() < o.familyJump * boost) {
      // jumps stay within the allele's origin: organic stays flesh,
      // biotech stays grown-tech (docs/17)
      const choices = familiesInClass(homologOf(family), [originOf(family)]).filter(
        (f) => f !== family,
      );
      if (choices.length > 0) family = rng.choice(choices);
    }
    const params = mapParams6(allele.params, (p) =>
      rng.next() < o.rate * boost ? clamp01(p + rng.gauss(0, o.sigma)) : p,
    );
    slots[slot] = { family, params };
  }

  let plan = g.body.plan;
  if (rng.next() < o.planJump) {
    const others = Object.keys(BODY_PLANS).sort().filter((p) => p !== plan);
    plan = rng.choice(others);
  }
  const body: BodyGenes = {
    plan,
    params: mapParams4(g.body.params, (p) =>
      rng.next() < o.rate ? clamp01(p + rng.gauss(0, o.sigma)) : p,
    ),
  };

  let tier = g.brain.tier;
  if (rng.next() < o.tierShift) {
    const i = BRAIN_TIERS.indexOf(tier);
    const j = Math.max(0, Math.min(BRAIN_TIERS.length - 1, i + (rng.bool() ? 1 : -1)));
    tier = BRAIN_TIERS[j]!;
  }
  const brain: BrainGenes = {
    tier,
    params: mapParams5(g.brain.params, (p) =>
      rng.next() < o.rate ? clamp01(p + rng.gauss(0, o.sigma)) : p,
    ),
  };

  return {
    genomeVersion: GENOME_VERSION,
    parentIds: g.creatureId ? [g.creatureId] : [],
    body,
    brain,
    slots,
  };
}

// ---- Splice -------------------------------------------------------------------

export function splice(a: Genome, b: Genome, rng: Rng, noise = 0.05): Genome {
  const slots = {} as Record<SlotName, PartAllele>;
  for (const slot of SLOT_NAMES) {
    const alA = a.slots[slot];
    const alB = b.slots[slot];
    const src = rng.bool() ? alA : alB;
    if (originOf(src.family) === "tech") {
      // you don't gene-splice a rifle: the issued item passes whole
      slots[slot] = src;
      continue;
    }
    const params = zipParams6(alA.params, alB.params, (pa, pb) =>
      clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise)),
    );
    slots[slot] = { family: src.family, params };
  }

  const body: BodyGenes = {
    plan: rng.bool() ? a.body.plan : b.body.plan,
    params: zipParams4(a.body.params, b.body.params, (pa, pb) =>
      clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise)),
    ),
  };
  const brain: BrainGenes = {
    tier: rng.bool() ? a.brain.tier : b.brain.tier,
    params: zipParams5(a.brain.params, b.brain.params, (pa, pb) =>
      clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise)),
    ),
  };

  const parents: string[] = [];
  if (a.creatureId) parents.push(a.creatureId);
  if (b.creatureId) parents.push(b.creatureId);
  return { genomeVersion: GENOME_VERSION, parentIds: parents, body, brain, slots };
}

// ---- Graft --------------------------------------------------------------------

/** Deterministic slot replacement -- the player-control valve (docs/06);
 * for the human faction, fictionally the quartermaster (docs/17). Throws
 * on a homolog-grammar violation. */
export function graft(
  g: Genome,
  slot: SlotName,
  family: string,
  params: Params6,
): Genome {
  if (homologOf(family) !== slot) {
    throw new Error(`${family} does not fit the ${slot} slot (homolog grammar)`);
  }
  const allele: PartAllele = { family, params: mapParams6(params, clamp01) };
  return {
    ...g,
    parentIds: g.creatureId ? [g.creatureId] : [],
    creatureId: undefined,
    slots: { ...g.slots, [slot]: allele },
  };
}

// ---- tuple helpers (fixed-length, no surprises) --------------------------------

function sixOf(f: () => number): Params6 {
  return [f(), f(), f(), f(), f(), f()];
}
function fiveOf(f: () => number): Params5 {
  return [f(), f(), f(), f(), f()];
}
function fourOf(f: () => number): Params4 {
  return [f(), f(), f(), f()];
}
function mapParams6(p: Params6, f: (x: number) => number): Params6 {
  return [f(p[0]), f(p[1]), f(p[2]), f(p[3]), f(p[4]), f(p[5])];
}
function mapParams5(p: Params5, f: (x: number) => number): Params5 {
  return [f(p[0]), f(p[1]), f(p[2]), f(p[3]), f(p[4])];
}
function mapParams4(p: Params4, f: (x: number) => number): Params4 {
  return [f(p[0]), f(p[1]), f(p[2]), f(p[3])];
}
function zipParams6(
  a: Params6,
  b: Params6,
  f: (x: number, y: number) => number,
): Params6 {
  return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3]), f(a[4], b[4]), f(a[5], b[5])];
}
function zipParams5(
  a: Params5,
  b: Params5,
  f: (x: number, y: number) => number,
): Params5 {
  return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3]), f(a[4], b[4])];
}
function zipParams4(
  a: Params4,
  b: Params4,
  f: (x: number, y: number) => number,
): Params4 {
  return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3])];
}
