/**
 * Energy requirements -- the upkeep demand side (docs/05 generalized per
 * faction in docs/17). Pure functions of the genome; v0.1 numbers, to be
 * tuned in the Phase-1 sandbox.
 *
 * The energy TYPE follows the part's origin:
 *   organic -> BLOOD  (the doctors' commodity: flesh must be fed)
 *   tech    -> FUEL   (the human army: machines burn fuel)
 *   biotech -> ICHOR  (the hive: grown machines drink alien fuel)
 *
 * A creature's upkeep is therefore mixed when its body is mixed: an alien
 * drone (chitin legs + plasma lance) drinks blood AND ichor; a monster
 * with one grafted tech part starts needing fuel. Running any pool dry
 * starves exactly the parts that pool feeds (decay damage / inert
 * equipment -- match-sim behavior, defined in docs/05 and docs/17).
 *
 * Supply is the match economy's job (territory trickle, supply convoys,
 * digestion pits -- docs/17); this module computes only DEMAND.
 */

import {
  HEART_OUTPUT,
  SLOT_NAMES,
  bodyAxis,
  brainSize,
  heartVigor,
  partAxis,
  type Genome,
  type HeartGenes,
  type PartAllele,
} from "./genome.js";
import { BODY_PLANS, express, isVestigial, originOf, type Origin } from "./catalog.js";

export const ENERGY_TYPES = ["blood", "fuel", "ichor"] as const;
export type EnergyType = (typeof ENERGY_TYPES)[number];

export const ORIGIN_ENERGY: Record<Origin, EnergyType> = {
  organic: "blood",
  tech: "fuel",
  biotech: "ichor",
};

/** Upkeep per minute, by energy type. */
export type Upkeep = Record<EnergyType, number>;

// v0.1 tuning knobs --------------------------------------------------------
const BODY_BASE = 4.0; // blood/min for the smallest living frame
const BODY_BULK = 8.0; // additional blood/min at full bulk
const BRAIN_PER_SIZE = 1.5; // blood/min per brain size step (minds are hungry)
const HEART_PER_OUTPUT = 0.05; // blood/min the pump itself draws, per output unit
const PART_BASE = 2.5; // energy/min for a part at minimal mass
const PART_MASS = 2.5; // additional energy/min at full expressed mass
const STUMP_UPKEEP = 0.3; // a healed stump barely costs anything

/** A part's energy demand scales with its expressed mass (length x girth
 * through the family's canalized bounds: a vestigial arm sips, a titan
 * arm gulps). */
export function partUpkeep(allele: PartAllele): { type: EnergyType; perMin: number } {
  const type = ORIGIN_ENERGY[originOf(allele.family)];
  if (isVestigial(allele.family)) {
    return { type, perMin: STUMP_UPKEEP };
  }
  const length = express(allele.family, "length", partAxis(allele, "length"));
  const girth = express(allele.family, "girth", partAxis(allele, "girth"));
  const mass = (length + girth) / 2;
  return { type, perMin: PART_BASE + PART_MASS * mass };
}

/** Total upkeep demand per minute. The living frame (body + brain) always
 * drinks blood -- even a rifleman must eat; parts pay by their origin.
 * Slots the body plan ignores (docs/15 atavism) cost nothing: silent
 * genes are free to carry. */
export function upkeep(g: Genome): Upkeep {
  const out: Upkeep = { blood: 0, fuel: 0, ichor: 0 };

  out.blood += BODY_BASE + BODY_BULK * bodyAxis(g.body, "bulk");
  out.blood += BRAIN_PER_SIZE * brainSize(g.brain);
  out.blood += HEART_PER_OUTPUT * heartCapacity(g.heart); // the pump feeds itself

  const ignored = BODY_PLANS[g.body.plan]?.ignoresSlots ?? [];
  for (const slot of SLOT_NAMES) {
    if (ignored.includes(slot)) continue;
    const p = partUpkeep(g.slots[slot]);
    out[p.type] += p.perMin;
  }
  return out;
}

// ---- viability: supply vs. demand --------------------------------------------

/** How severely an over-capacity graft can exceed the heart before the
 * patient dies on the table rather than merely rejecting the limb. Below
 * this, the limb necrotizes (rejected) and the creature survives the
 * shock; above it, the heart stops. (docs/06 grafting.) */
export const SHOCK_FACTOR = 1.5;

/** Circulatory output the heart can sustain, in energy-units/min. Tier
 * sets the base; vigor (params[0]) tunes it +/-30%. This is the SUPPLY
 * ceiling every transplant is measured against. */
export function heartCapacity(heart: HeartGenes): number {
  return HEART_OUTPUT[heart.tier] * (0.7 + 0.6 * heartVigor(heart));
}

/** Total circulatory load: the whole upkeep bill the heart must drive,
 * across every energy type (the pump moves the vehicle; fuel and ichor
 * lines tap the same circulation). */
export function circulatoryLoad(g: Genome): number {
  const u = upkeep(g);
  return u.blood + u.fuel + u.ichor;
}

export type ViabilityState = "viable" | "strained" | "nonviable";

export interface Viability {
  readonly state: ViabilityState;
  readonly capacity: number;
  readonly load: number;
  /** capacity - load: positive is headroom, negative is overload. */
  readonly margin: number;
}

/** Is this body's heart big enough to run it? "strained" means the body
 * is over capacity but within the shock factor -- it lives, but a graft
 * here will be rejected; "nonviable" means the heart cannot sustain the
 * body at all (a corpse on the table). */
export function viability(g: Genome): Viability {
  const capacity = heartCapacity(g.heart);
  const load = circulatoryLoad(g);
  const margin = capacity - load;
  const state: ViabilityState =
    load <= capacity ? "viable" : load <= capacity * SHOCK_FACTOR ? "strained" : "nonviable";
  return { state, capacity, load, margin };
}

/** The mana surge paid at reanimation (docs/03 dual-currency split: mana
 * is energy, components are material). Scales with total upkeep demand --
 * hungrier designs take a bigger jolt to start -- plus a brain premium. */
export function reanimationSurge(g: Genome): number {
  const u = upkeep(g);
  const total = u.blood + u.fuel + u.ichor;
  return Math.round(5 + 1.2 * total + 3 * brainSize(g.brain));
}
