/**
 * Server-side genome validation (docs/06 "Viability & balance constraints",
 * docs/15 Strategy 6). The service validates every operator result and
 * every client-referenced genome; an invalid result is surfaced to the
 * player as a comic "failed experiment" -- never silently clamped.
 */

import {
  BODY_AXES,
  BRAIN_AXES,
  BRAIN_TIERS,
  GENOME_VERSION,
  HEART_TIERS,
  PART_AXES,
  SLOT_NAMES,
  type Genome,
} from "./genome.js";
import { BODY_PLANS, homologOf, isKnownFamily } from "./catalog.js";

export interface ValidationResult {
  readonly ok: boolean;
  readonly errors: readonly string[];
}

function inUnit(xs: readonly number[]): boolean {
  return xs.every((x) => typeof x === "number" && Number.isFinite(x) && x >= 0 && x <= 1);
}

export function validateGenome(g: Genome): ValidationResult {
  const errors: string[] = [];

  if (g.genomeVersion !== GENOME_VERSION) {
    errors.push(`unsupported genomeVersion: ${String(g.genomeVersion)}`);
  }

  // body
  if (!Object.prototype.hasOwnProperty.call(BODY_PLANS, g.body?.plan)) {
    errors.push(`unknown body plan: ${String(g.body?.plan)}`);
  }
  if (!Array.isArray(g.body?.params) || g.body.params.length !== BODY_AXES.length) {
    errors.push(`body params must have ${BODY_AXES.length} entries`);
  } else if (!inUnit(g.body.params)) {
    errors.push("body params out of [0,1]");
  }

  // brain
  if (!BRAIN_TIERS.includes(g.brain?.tier)) {
    errors.push(`unknown brain tier: ${String(g.brain?.tier)}`);
  }
  if (!Array.isArray(g.brain?.params) || g.brain.params.length !== BRAIN_AXES.length) {
    errors.push(`brain params must have ${BRAIN_AXES.length} entries`);
  } else if (!inUnit(g.brain.params)) {
    errors.push("brain params out of [0,1]");
  }

  // heart
  if (!HEART_TIERS.includes(g.heart?.tier)) {
    errors.push(`unknown heart tier: ${String(g.heart?.tier)}`);
  }
  if (!Array.isArray(g.heart?.params) || g.heart.params.length !== PART_AXES.length) {
    errors.push(`heart params must have ${PART_AXES.length} entries`);
  } else if (!inUnit(g.heart.params)) {
    errors.push("heart params out of [0,1]");
  }

  // slots: exactly the standard set, each allele fitting its slot's homolog
  const present = Object.keys(g.slots ?? {});
  for (const s of SLOT_NAMES) {
    if (!present.includes(s)) errors.push(`missing slot: ${s}`);
  }
  for (const s of present) {
    if (!(SLOT_NAMES as readonly string[]).includes(s)) {
      errors.push(`unknown slot: ${s}`);
      continue;
    }
    const allele = g.slots[s as (typeof SLOT_NAMES)[number]];
    if (!allele) continue;
    if (!isKnownFamily(allele.family)) {
      errors.push(`unknown part family: ${allele.family}`);
      continue;
    }
    if (homologOf(allele.family) !== s) {
      errors.push(`${allele.family} does not fit the ${s} slot (homolog grammar)`);
    }
    if (!Array.isArray(allele.params) || allele.params.length !== PART_AXES.length) {
      errors.push(`${s} params must have ${PART_AXES.length} entries`);
    } else if (!inUnit(allele.params)) {
      errors.push(`${s} params out of [0,1]`);
    }
  }

  // lineage sanity
  if (!Array.isArray(g.parentIds) || g.parentIds.length > 2) {
    errors.push("parentIds must be an array of 0-2 ids");
  }

  return { ok: errors.length === 0, errors };
}

/** Convenience guard for service code paths. */
export function assertValid(g: Genome): void {
  const r = validateGenome(g);
  if (!r.ok) {
    throw new Error(`invalid genome (failed experiment): ${r.errors.join("; ")}`);
  }
}
