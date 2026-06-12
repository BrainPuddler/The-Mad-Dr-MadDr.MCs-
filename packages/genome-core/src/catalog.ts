/**
 * The part catalog and body plans -- the shared contract between the
 * genome (this package), the Mutator service (docs/07), and the renderer
 * (docs/08). Versioned together with the genome schema.
 *
 * Each family declares:
 *  - homolog: the slot class it fits (the Hox grammar, docs/15)
 *  - origin:  organic (breeds) | tech (issued; never mutates or blends;
 *             Graft-only) | biotech (grown machines; breeds like flesh)
 *             -- docs/17
 *  - bounds:  per-axis CANALIZED phenotype bounds; genotype is always the
 *             full [0,1], expression maps into the authored-safe range
 *  - invariants: prose record of what may never vary (identity)
 */

import { PART_AXES, type PartAxis, type SlotName } from "./genome.js";

export type Origin = "organic" | "tech" | "biotech";
export type Bounds = Readonly<Record<PartAxis, readonly [number, number]>>;

export interface PartFamily {
  readonly homolog: SlotName;
  readonly origin: Origin;
  readonly bounds: Bounds;
  readonly invariants: string;
}

const FULL: readonly [number, number] = [0, 1];

function bounds(over: Partial<Record<PartAxis, readonly [number, number]>> = {}): Bounds {
  const b = {} as Record<PartAxis, readonly [number, number]>;
  for (const a of PART_AXES) b[a] = over[a] ?? FULL;
  return b;
}

export const FAMILIES: Readonly<Record<string, PartFamily>> = {
  // ---- hand homologs --------------------------------------------------------
  claw_hand: {
    homolog: "hand", origin: "organic",
    bounds: bounds({ curl: [0.1, 0.9] }),
    invariants: "a palm bearing 2-5 hard, curved, tapering talons",
  },
  pincer: {
    homolog: "hand", origin: "organic",
    bounds: bounds({ curl: [0.2, 1.0], count: [0.0, 0.6] }),
    invariants: "two opposing crescent jaws meeting at a gap",
  },
  tentacle: {
    homolog: "hand", origin: "organic",
    bounds: bounds({ girth: [0.0, 0.7], taper: [0.4, 1.0] }),
    invariants: "a single smooth limb tapering to a curling tip",
  },
  rifle_arm: {
    homolog: "hand", origin: "tech",
    bounds: bounds({ curl: [0.0, 0.2] }),
    invariants: "an arm shouldering a long gun: barrel, stock, trigger group",
  },
  plasma_lance: {
    homolog: "hand", origin: "biotech",
    bounds: bounds({ taper: [0.5, 1.0] }),
    invariants: "a fleshy arm ending in a glowing lance emitter with a charge bulb",
  },
  // ---- sensor homologs ------------------------------------------------------
  antenna: {
    homolog: "sensor", origin: "organic",
    bounds: bounds({ girth: [0.0, 0.45], length: [0.3, 1.0] }),
    invariants: "a thin paired stalk, segmented, ending in a tip bulb",
  },
  horn: {
    homolog: "sensor", origin: "organic",
    bounds: bounds({ girth: [0.3, 1.0], curl: [0.0, 0.7] }),
    invariants: "a rigid ridged cone, broad base to sharp point",
  },
  sensor_mast: {
    homolog: "sensor", origin: "tech",
    bounds: bounds({ girth: [0.0, 0.4] }),
    invariants: "a single rigid antenna mast with a dish or vane at the tip",
  },
  // ---- eye homologs ---------------------------------------------------------
  bug_eyes: {
    homolog: "eye", origin: "organic",
    bounds: bounds({ count: [0.2, 1.0] }),
    invariants: "a clustered constellation of 3+ round eyes with pupils",
  },
  cyclops_eye: {
    homolog: "eye", origin: "organic",
    bounds: bounds({ count: [0.0, 0.0], girth: [0.3, 1.0] }),
    invariants: "one single oversized lidded eye",
  },
  stalk_eyes: {
    homolog: "eye", origin: "organic",
    bounds: bounds({ length: [0.3, 1.0], girth: [0.0, 0.5] }),
    invariants: "eyeballs held aloft on flexible stalks",
  },
  optic_visor: {
    homolog: "eye", origin: "tech",
    bounds: bounds({ count: [0.0, 0.6] }),
    invariants: "a rectangular visor band with 1-3 round lenses",
  },
  // ---- leg homologs ---------------------------------------------------------
  hoofed_leg: {
    homolog: "leg", origin: "organic",
    bounds: bounds({ girth: [0.35, 1.0], curl: [0.0, 0.6] }),
    invariants: "a sturdy column ending in a hard cloven hoof",
  },
  talon_leg: {
    homolog: "leg", origin: "organic",
    bounds: bounds({ girth: [0.0, 0.4], count: [0.2, 1.0] }),
    invariants: "a thin bird leg, backward knee, ending in splayed clawed toes",
  },
  insect_leg: {
    homolog: "leg", origin: "organic",
    bounds: bounds({ girth: [0.0, 0.35], curl: [0.3, 1.0] }),
    invariants: "a segmented zigzag of chitinous struts with a tarsal hook",
  },
  piston_leg: {
    homolog: "leg", origin: "tech",
    bounds: bounds({ girth: [0.3, 0.9], curl: [0.0, 0.3] }),
    invariants: "a hydraulic strut: cylinder, piston rod, flat foot-plate",
  },
};

/** Body plans. "tetrapod" is the continuous plan family (posture spans
 * biped -> monkey-type -> quadruped, docs/15); the rest are discrete.
 * ignoresSlots: slots a plan does not express (genes ride along -- the
 * atavism). */
export interface BodyPlan {
  readonly invariants: string;
  readonly ignoresSlots: readonly SlotName[];
}

export const BODY_PLANS: Readonly<Record<string, BodyPlan>> = {
  tetrapod: {
    invariants: "a torso on limbs with a head: posture spans biped to quadruped",
    ignoresSlots: [],
  },
  blob: {
    invariants: "a single amorphous mass; parts surface-mount on the membrane",
    ignoresSlots: ["leg"],
  },
  serpentine: {
    invariants: "one long tapering body coiling along the ground, head at the fore",
    ignoresSlots: ["leg"],
  },
  winged: {
    invariants: "a small body slung between two membrane wings, standing on legs",
    ignoresSlots: [],
  },
};

// ---- queries ---------------------------------------------------------------

export function isKnownFamily(family: string): boolean {
  return Object.prototype.hasOwnProperty.call(FAMILIES, family);
}

export function homologOf(family: string): SlotName {
  const f = FAMILIES[family];
  if (!f) throw new Error(`unknown part family: ${family}`);
  return f.homolog;
}

export function originOf(family: string): Origin {
  const f = FAMILIES[family];
  if (!f) throw new Error(`unknown part family: ${family}`);
  return f.origin;
}

/** Families fitting a slot, filtered by origin. Mutation family-jumps stay
 * within the allele's own origin; random generation defaults to organic --
 * tech enters a genome only by explicit issue (docs/17). */
export function familiesInClass(
  homolog: SlotName,
  origins: readonly Origin[] = ["organic"],
): string[] {
  return Object.keys(FAMILIES)
    .filter((f) => {
      const spec = FAMILIES[f]!;
      return spec.homolog === homolog && origins.includes(spec.origin);
    })
    .sort();
}

/** Canalized expression: map a [0,1] gene into the family's phenotype
 * range for one axis (docs/15, Strategy 4). */
export function express(family: string, axis: PartAxis, gene: number): number {
  const f = FAMILIES[family];
  if (!f) throw new Error(`unknown part family: ${family}`);
  const [lo, hi] = f.bounds[axis];
  return lo + gene * (hi - lo);
}
