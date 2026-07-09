/**
 * The genome v2 schema -- the normative creature representation.
 *
 * Adopted per Q10 (docs/12 decision log) from the validated prototype:
 * docs/15-part-genetics.md (parts, shared axes, origins),
 * docs/16-brains-behavior-command.md (brain genes),
 * docs/17-factions.md (origins and faction canalization).
 *
 * Everything here is pure data. Rendering (the phenotype) consumes this
 * through the catalog contract and lives elsewhere -- by design, this
 * package never touches graphics.
 */

export const GENOME_VERSION = 2 as const;

/** The six shared semantic part axes, identically interpreted by every
 * part family. Shared semantics are what make cross-family breeding
 * meaningful (docs/15, Strategy 2). All genotype values live in [0, 1]. */
export const PART_AXES = [
  "length",
  "girth",
  "taper",
  "curl",
  "count",
  "ornament",
] as const;
export type PartAxis = (typeof PART_AXES)[number];
export type Params6 = readonly [number, number, number, number, number, number];

/** Body-plan axes. Shared across plans with plan-specific expression
 * (for a blob, posture expresses as membrane wobble; for the winged,
 * limb is wingspan) -- docs/15 "Body plans". */
export const BODY_AXES = ["posture", "bulk", "limb", "tail"] as const;
export type BodyAxis = (typeof BODY_AXES)[number];
export type Params4 = readonly [number, number, number, number];

/** Brain (behavioral) axes -- docs/16. */
export const BRAIN_AXES = [
  "command",
  "will",
  "temperament",
  "guile",
  "fury",
] as const;
export type BrainAxis = (typeof BRAIN_AXES)[number];
export type Params5 = readonly [number, number, number, number, number];

/** Brain quality tiers (docs/06 brain budget). Tier also sets brain SIZE,
 * the scalar in control capacity and cost (docs/16). */
export const BRAIN_TIERS = ["dim", "average", "gifted", "mastermind"] as const;
export type BrainTier = (typeof BRAIN_TIERS)[number];
export const BRAIN_SIZE: Record<BrainTier, number> = {
  dim: 1,
  average: 2,
  gifted: 3,
  mastermind: 4,
};

/** The standard slot set. Slots are homolog classes: crossover and family
 * jumps only ever swap within a slot's class (docs/15, Strategy 3 -- the
 * Hox rule). Plans may IGNORE slots at expression time (serpentine ignores
 * "leg"); the genes ride along silently -- the atavism. */
export const SLOT_NAMES = ["hand", "sensor", "eye", "leg"] as const;
export type SlotName = (typeof SLOT_NAMES)[number];

/** Heart (circulatory) tiers. The heart is the SUPPLY organ: it sets how
 * much upkeep the body can sustain (docs/06 viability). Tier sets the base
 * pumping output; the first param (vigor) tunes it within the tier. A
 * heart that cannot drive the body's parts kills the creature on the
 * operating table (docs/06 grafting; surgery.ts). */
export const HEART_TIERS = ["faint", "steady", "strong", "titan"] as const;
export type HeartTier = (typeof HEART_TIERS)[number];
/** Base circulatory output by tier, in energy-units/min (see energy.ts). */
export const HEART_OUTPUT: Record<HeartTier, number> = {
  faint: 14,
  steady: 26,
  strong: 42,
  titan: 64,
};
/** Heart genes use the part axes for shape (so a heart can be harvested and
 * expressed like any organ); only the first three are read today:
 * [vigor, girth (mass/cost), reserve]. */
export type HeartParams = Params6;

export interface PartAllele {
  readonly family: string;
  readonly params: Params6;
  /** The skin hue [0,1] this specific part expresses, independent of the
   * body's own hue gene. Absent = express the body's own hue, which is
   * what every native (never-grafted) part does. Set by surgery.ts when a
   * harvested part is sewn on, so a transplant keeps the donor's color
   * instead of blending into the recipient (docs/06 grafting). */
  readonly hue?: number;
}

export interface BodyGenes {
  readonly plan: string;
  readonly params: Params4;
}

export interface BrainGenes {
  readonly tier: BrainTier;
  readonly params: Params5;
}

export interface HeartGenes {
  readonly tier: HeartTier;
  readonly params: HeartParams;
}

export interface Genome {
  readonly genomeVersion: typeof GENOME_VERSION;
  /** Assigned by the Mutator service on creation; absent on unsaved
   * results. Genomes are immutable rows (docs/07): operators return NEW
   * genomes and record lineage in parentIds. */
  readonly creatureId?: string;
  /** 0 entries (primordial), 1 (mutation/graft), or 2 (splice). */
  readonly parentIds: readonly string[];
  readonly body: BodyGenes;
  readonly brain: BrainGenes;
  /** The circulatory organ: the supply side of viability. Transplantable
   * (surgery.ts) -- a bigger heart is how you support heavier grafts. */
  readonly heart: HeartGenes;
  readonly slots: Readonly<Record<SlotName, PartAllele>>;
}

// ---- accessors --------------------------------------------------------------

export function partAxis(allele: PartAllele, axis: PartAxis): number {
  return allele.params[PART_AXES.indexOf(axis) as 0 | 1 | 2 | 3 | 4 | 5];
}

export function bodyAxis(body: BodyGenes, axis: BodyAxis): number {
  return body.params[BODY_AXES.indexOf(axis) as 0 | 1 | 2 | 3];
}

export function brainAxis(brain: BrainGenes, axis: BrainAxis): number {
  return brain.params[BRAIN_AXES.indexOf(axis) as 0 | 1 | 2 | 3 | 4];
}

export function brainSize(brain: BrainGenes): number {
  return BRAIN_SIZE[brain.tier];
}

/** Heart vigor: tunes pumping output within the tier (params[0]). */
export function heartVigor(heart: HeartGenes): number {
  return heart.params[0];
}

/** Structural replace of one slot; carries everything else through. */
export function withSlot(g: Genome, slot: SlotName, allele: PartAllele): Genome {
  return { ...g, slots: { ...g.slots, [slot]: allele } };
}

export function clamp01(x: number): number {
  return x < 0 ? 0 : x > 1 ? 1 : x;
}
