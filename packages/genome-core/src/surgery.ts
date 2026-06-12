/**
 * Surgery -- the physical grafting system (docs/06 grafting): cut a part
 * off one creature and sew it onto another, gated by whether the
 * recipient's HEART can drive the result.
 *
 * This is distinct from the lab-side `graft` operator in operators.ts:
 *   - operators.graft() composes a genome deterministically (the Mutator
 *     control valve / quartermaster issue) -- it does not check viability.
 *   - surgery here is the IN-WORLD operation: it runs the viability gate
 *     and can fail on the table.
 *
 * The three outcomes of sewing a part the heart may not afford:
 *   - SURVIVED       load within heart capacity; the graft takes.
 *   - LIMB_REJECTED  over capacity but within the shock factor; the new
 *                    limb necrotizes and is removed, the creature lives,
 *                    the slot returns to its prior state.
 *   - PATIENT_DIED   so far over capacity the heart stops; the creature
 *                    dies on the table.
 * In EVERY failure case the harvested part is handed back still usable --
 * a botched operation is never a wasted part. (Nothing is wasted: the
 * doctors' first principle, docs/01.)
 */

import {
  type Genome,
  type HeartGenes,
  type HeartParams,
  type Params6,
  type SlotName,
  clamp01,
} from "./genome.js";
import { STUMP_OF, homologOf, originOf, type Origin } from "./catalog.js";
import { viability, type Viability } from "./energy.js";

const ZERO6: Params6 = [0, 0, 0, 0, 0, 0];

// ---- harvested items: durable, reusable -------------------------------------

/** A part cut off a creature. Carries its genes intact, so it expresses
 * identically wherever it is sewn next. */
export interface PartItem {
  readonly kind: "part";
  readonly family: string;
  readonly params: Params6;
  /** Provenance: the creatureId it was cut from, if known. */
  readonly from?: string;
}

/** A heart explanted from a creature. */
export interface HeartItem {
  readonly kind: "heart";
  readonly tier: HeartGenes["tier"];
  readonly params: HeartParams;
  readonly from?: string;
}

export function partItemHomolog(item: PartItem): SlotName {
  return homologOf(item.family);
}
export function partItemOrigin(item: PartItem): Origin {
  return originOf(item.family);
}

// ---- harvest: cut a part off ------------------------------------------------

/** Cut the part in `slot` off the creature. The donor's slot heals to a
 * stump; the part comes away as a reusable item. (Harvesting from a live
 * creature is legal -- it simply leaves a stumped, lighter creature.) */
export function harvestPart(g: Genome, slot: SlotName): { donor: Genome; part: PartItem } {
  const taken = g.slots[slot];
  const part: PartItem = {
    kind: "part",
    family: taken.family,
    params: taken.params,
    ...(g.creatureId ? { from: g.creatureId } : {}),
  };
  const donor = stumpSlot(g, slot);
  return { donor, part };
}

/** Cut the heart out. This leaves the donor without a working pump -- you
 * harvest a heart from a corpse, or you kill the donor doing it. */
export function harvestHeart(g: Genome): { donor: Genome; heart: HeartItem } {
  const heart: HeartItem = {
    kind: "heart",
    tier: g.heart.tier,
    params: g.heart.params,
    ...(g.creatureId ? { from: g.creatureId } : {}),
  };
  // the cavity is left with a barely-beating vestige (vigor 0): a corpse
  // for any body of real size
  const donor: Genome = {
    ...g,
    parentIds: g.creatureId ? [g.creatureId] : g.parentIds,
    creatureId: undefined,
    heart: { tier: "faint", params: ZERO6 },
  };
  return { donor, heart };
}

// ---- sew: attach a part, gated by the heart ---------------------------------

export type SurgeryResult = "survived" | "limb_rejected" | "patient_died";

export interface PartSurgery {
  readonly result: SurgeryResult;
  /** The resulting creature: the new limb on success; unchanged on
   * rejection; the dead body (limb attached) on death. */
  readonly patient: Genome;
  readonly alive: boolean;
  /** The viability that decided the outcome (the candidate body's). */
  readonly viability: Viability;
  /** Handed back whenever the part was NOT consumed -- i.e. on every
   * failure. Always still usable. */
  readonly returnedPart?: PartItem;
}

/** Sew a harvested part into a slot, then see whether the heart can run
 * the result. Throws only on a homolog-grammar violation (you cannot sew
 * an arm into an eye socket); energy failure is a returned outcome, not an
 * exception. */
export function sewPart(patient: Genome, slot: SlotName, part: PartItem): PartSurgery {
  if (partItemHomolog(part) !== slot) {
    throw new Error(`${part.family} does not fit the ${slot} slot (homolog grammar)`);
  }
  const candidate: Genome = {
    ...patient,
    parentIds: patient.creatureId ? [patient.creatureId] : patient.parentIds,
    creatureId: undefined,
    slots: { ...patient.slots, [slot]: { family: part.family, params: clampAll(part.params) } },
  };
  const v = viability(candidate);

  if (v.state === "viable") {
    return { result: "survived", patient: candidate, alive: true, viability: v };
  }
  if (v.state === "strained") {
    // the limb necrotizes; the slot returns to what it was before surgery
    return {
      result: "limb_rejected",
      patient,
      alive: true,
      viability: v,
      returnedPart: part,
    };
  }
  // nonviable: the heart stops. The creature dies with the limb attached;
  // the part survives the corpse, still usable.
  return {
    result: "patient_died",
    patient: candidate,
    alive: false,
    viability: v,
    returnedPart: part,
  };
}

export interface HeartSurgery {
  readonly result: "survived" | "patient_died";
  readonly patient: Genome;
  readonly alive: boolean;
  readonly viability: Viability;
  /** The patient's OLD heart, explanted and handed back on a successful
   * swap -- reusable in the next patient. */
  readonly explantedHeart?: HeartItem;
  /** The donor heart, handed back if the transplant failed (the body
   * never restarted on it). Still usable. */
  readonly returnedHeart?: HeartItem;
}

/** Transplant a heart. This is the counterplay to an over-loaded body:
 * a bigger heart lets you carry heavier grafts. A heart that still cannot
 * drive the body kills the patient -- you don't get a second start. */
export function sewHeart(patient: Genome, heart: HeartItem): HeartSurgery {
  const old = patient.heart;
  const candidate: Genome = {
    ...patient,
    parentIds: patient.creatureId ? [patient.creatureId] : patient.parentIds,
    creatureId: undefined,
    heart: { tier: heart.tier, params: clampAll(heart.params) },
  };
  const v = viability(candidate);

  if (v.state === "nonviable") {
    // the new heart cannot run the body; it never beats. Patient dies; the
    // old heart is gone (already cut out), the donor heart is handed back.
    return {
      result: "patient_died",
      patient: candidate,
      alive: false,
      viability: v,
      returnedHeart: heart,
    };
  }
  // viable or merely strained: the transplant takes (a strained body lives
  // but runs over capacity -- it will starve in the field until relieved).
  const explanted: HeartItem = {
    kind: "heart",
    tier: old.tier,
    params: old.params,
    ...(patient.creatureId ? { from: patient.creatureId } : {}),
  };
  return {
    result: "survived",
    patient: candidate,
    alive: true,
    viability: v,
    explantedHeart: explanted,
  };
}

// ---- helpers ----------------------------------------------------------------

function stumpSlot(g: Genome, slot: SlotName): Genome {
  return {
    ...g,
    parentIds: g.creatureId ? [g.creatureId] : g.parentIds,
    creatureId: undefined,
    slots: { ...g.slots, [slot]: { family: STUMP_OF[slot], params: ZERO6 } },
  };
}

function clampAll(p: Params6): Params6 {
  return [
    clamp01(p[0]), clamp01(p[1]), clamp01(p[2]),
    clamp01(p[3]), clamp01(p[4]), clamp01(p[5]),
  ];
}
