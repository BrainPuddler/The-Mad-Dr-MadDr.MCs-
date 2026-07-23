/**
 * Harvest & carry -- the derived stats behind harvester creatures
 * (docs/22 harvester morphology). Pure functions of the genome, exactly
 * like energy.ts: nothing here is a new gene, everything is EXPRESSED
 * from parts the creature already carries, so harvesters are bred and
 * grafted in the Lab like any other design -- never a bolt-on unit type.
 *
 *   gather   -- how fast the creature strips blood/bone/brains from a
 *               target, set by its HAND family (a lamprey maw drinks, a
 *               bone saw cuts, ordinary claws paw uselessly slowly)
 *   capacity -- how much it can carry, set by its SENSOR-slot storage
 *               vessel (bladder / steel tank / amber vesicles) plus the
 *               body's own bulk; the blob plan gets a plan bonus -- an
 *               amorphous body IS a bag
 *   weight   -- carried load slows the carrier: mildly on the ground,
 *               twice as hard in the air (winged/floater plans), because
 *               flight pays for every kilogram (creator direction)
 *
 * All numbers v0.1 -- Phase-2 sandbox tuning (docs/22 SS10). Penalty
 * FLOORS follow docs/22 SS1's never-annoying contract: a fully-laden
 * flyer is slow, never grounded; a laden walker trudges, never stalls.
 */

import {
  SLOT_NAMES,
  bodyAxis,
  partAxis,
  type Genome,
} from "./genome.js";
import { BODY_PLANS, express, isVestigial } from "./catalog.js";

/** Gather-rate multipliers by hand family, per resource lane. Families
 * absent from this table use TOOLLESS -- anything with a working hand
 * can tear at a corpse, badly. `drainsLiving`: the tool can pull blood
 * from a target that is still alive (the lamprey/siphon fantasy);
 * everything else harvests corpses only. */
export interface GatherSpec {
  readonly blood: number;
  readonly bone: number;
  readonly brain: number;
  readonly drainsLiving: boolean;
}

const TOOLLESS: GatherSpec = { blood: 0.4, bone: 0.4, brain: 0.2, drainsLiving: false };

export const HARVEST_TOOLS: Readonly<Record<string, GatherSpec>> = {
  // dedicated harvest tools (docs/22)
  lamprey_maw: { blood: 3.0, bone: 0.3, brain: 0.4, drainsLiving: true },
  ichor_siphon: { blood: 2.4, bone: 0.3, brain: 0.8, drainsLiving: true },
  bone_saw: { blood: 0.5, bone: 3.0, brain: 0.6, drainsLiving: false },
  // general-purpose hands that happen to cut and grab decently
  claw_hand: { blood: 1.0, bone: 1.0, brain: 0.5, drainsLiving: false },
  pincer: { blood: 0.8, bone: 1.4, brain: 0.5, drainsLiving: false },
  chain_blade: { blood: 0.7, bone: 1.8, brain: 0.3, drainsLiving: false },
  tentacle: { blood: 1.2, bone: 0.4, brain: 0.6, drainsLiving: false },
};

/** Storage-vessel families (sensor homolog) and the capacity each grants
 * at full expression. Not a stat on other sensors: an antenna senses, a
 * bladder carries -- the slot is the trade. */
export const STORAGE_FAMILIES: Readonly<Record<string, number>> = {
  storage_bladder: 60,
  steel_tank: 70,
  tank_backpack: 85, // two tanks beat one, short of a flat doubling
  amber_vesicle: 55,
};

// v0.1 tuning knobs ----------------------------------------------------------
const BASE_CAPACITY = 10; // any creature can gulp a little
const BULK_CAPACITY = 15; // a bigger body holds more, vessel or not
const BLOB_BONUS = 1.5; // the amorphous plan is its own storage bag
const GROUND_PENALTY = 0.25; // fully laden ground speed loss
const FLIGHT_PENALTY = 0.5; // fully laden flight speed loss -- weight counts double aloft
const GROUND_FLOOR = 0.6; // never-annoying floors (docs/22 SS1)
const FLIGHT_FLOOR = 0.4;

/** Plans that fly (the weight rule's audience). A plan property, like
 * amphibious -- not a gene. */
const FLYING_PLANS: readonly string[] = ["winged", "floater"];

export interface HarvestProfile {
  /** Gather rates in resource-units/second against a valid target,
   * already scaled by the tool's expressed size. */
  readonly gather: { readonly blood: number; readonly bone: number; readonly brain: number };
  /** Whether the hand tool can drain a LIVING target. */
  readonly drainsLiving: boolean;
  /** Total onboard carry capacity (resource units, all lanes pooled). */
  readonly capacity: number;
  /** True when the sensor slot carries a dedicated storage vessel. */
  readonly hasVessel: boolean;
  /** True for winged/floater plans -- the ones the weight rule bites. */
  readonly flies: boolean;
}

export function harvestProfile(g: Genome): HarvestProfile {
  const ignored = BODY_PLANS[g.body.plan]?.ignoresSlots ?? [];

  // gather: from the hand tool, scaled by its expressed working size
  let gather = { blood: 0, bone: 0, brain: 0 };
  let drainsLiving = false;
  if (!ignored.includes("hand" as (typeof SLOT_NAMES)[number])) {
    const hand = g.slots.hand;
    if (!isVestigial(hand.family)) {
      const spec = HARVEST_TOOLS[hand.family] ?? TOOLLESS;
      const length = express(hand.family, "length", partAxis(hand, "length"));
      const girth = express(hand.family, "girth", partAxis(hand, "girth"));
      const size = 0.5 + (length + girth) / 2; // [0.5, 1.5]
      gather = {
        blood: spec.blood * size,
        bone: spec.bone * size,
        brain: spec.brain * size,
      };
      drainsLiving = spec.drainsLiving;
    }
  }

  // capacity: body bulk + the storage vessel, times the blob plan bonus
  let capacity = BASE_CAPACITY + BULK_CAPACITY * bodyAxis(g.body, "bulk");
  let hasVessel = false;
  if (!ignored.includes("sensor" as (typeof SLOT_NAMES)[number])) {
    const sensor = g.slots.sensor;
    const vesselMax = STORAGE_FAMILIES[sensor.family];
    if (vesselMax !== undefined) {
      hasVessel = true;
      const length = express(sensor.family, "length", partAxis(sensor, "length"));
      const girth = express(sensor.family, "girth", partAxis(sensor, "girth"));
      capacity += vesselMax * ((length + girth) / 2);
    }
  }
  if (g.body.plan === "blob") capacity *= BLOB_BONUS;

  return {
    gather,
    drainsLiving,
    capacity,
    hasVessel,
    flies: FLYING_PLANS.includes(g.body.plan),
  };
}

/** Speed multiplier for a carrier at `fill` (0 = empty, 1 = full),
 * on the ground. Linear, floored -- laden trudging, never stalling. */
export function groundSpeedFactor(fill: number): number {
  const f = fill < 0 ? 0 : fill > 1 ? 1 : fill;
  const factor = 1 - GROUND_PENALTY * f;
  return factor < GROUND_FLOOR ? GROUND_FLOOR : factor;
}

/** Speed multiplier for a FLYING carrier at `fill`. Twice the ground
 * sensitivity -- every kilogram aloft is paid for -- but floored: a full
 * tank makes a slow flyer, never a grounded one (docs/22 SS1). */
export function flightSpeedFactor(fill: number): number {
  const f = fill < 0 ? 0 : fill > 1 ? 1 : fill;
  const factor = 1 - FLIGHT_PENALTY * f;
  return factor < FLIGHT_FLOOR ? FLIGHT_FLOOR : factor;
}
