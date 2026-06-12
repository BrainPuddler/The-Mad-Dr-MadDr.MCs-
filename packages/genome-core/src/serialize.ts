/**
 * Genome serialization -- the wire/storage format (docs/07).
 *
 * Canonical JSON with stable key order, so that equal genomes serialize to
 * byte-identical strings (useful for hashing, signing, and golden tests).
 * Deserialization validates: a genome that didn't pass validateGenome
 * never enters the system.
 */

import {
  GENOME_VERSION,
  SLOT_NAMES,
  type Genome,
  type Params4,
  type Params5,
  type Params6,
} from "./genome.js";
import type { BrainTier, HeartTier } from "./genome.js";
import { validateGenome } from "./validate.js";

export function toJson(g: Genome): string {
  // explicit field order: stable across engines and refactors
  const slots: Record<string, unknown> = {};
  for (const s of SLOT_NAMES) {
    const a = g.slots[s];
    slots[s] = { family: a.family, params: a.params };
  }
  return JSON.stringify({
    genomeVersion: g.genomeVersion,
    ...(g.creatureId !== undefined ? { creatureId: g.creatureId } : {}),
    parentIds: g.parentIds,
    body: { plan: g.body.plan, params: g.body.params },
    brain: { tier: g.brain.tier, params: g.brain.params },
    heart: { tier: g.heart.tier, params: g.heart.params },
    slots,
  });
}

export class GenomeParseError extends Error {
  constructor(
    message: string,
    readonly errors: readonly string[] = [],
  ) {
    super(message);
    this.name = "GenomeParseError";
  }
}

export function fromJson(json: string): Genome {
  let raw: unknown;
  try {
    raw = JSON.parse(json);
  } catch (e) {
    throw new GenomeParseError(`not valid JSON: ${(e as Error).message}`);
  }
  if (typeof raw !== "object" || raw === null) {
    throw new GenomeParseError("genome must be a JSON object");
  }
  const o = raw as Record<string, unknown>;
  if (o.genomeVersion !== GENOME_VERSION) {
    throw new GenomeParseError(
      `unsupported genomeVersion: ${String(o.genomeVersion)} (this build reads v${GENOME_VERSION})`,
    );
  }

  // structural lift with no trust: validation decides what's acceptable
  const body = o.body as { plan: string; params: number[] };
  const brain = o.brain as { tier: BrainTier; params: number[] };
  const heart = o.heart as { tier: HeartTier; params: number[] };
  const slotsRaw = (o.slots ?? {}) as Record<string, { family: string; params: number[] }>;
  const g: Genome = {
    genomeVersion: GENOME_VERSION,
    ...(typeof o.creatureId === "string" ? { creatureId: o.creatureId } : {}),
    parentIds: Array.isArray(o.parentIds) ? (o.parentIds as string[]) : [],
    body: { plan: body?.plan, params: (body?.params ?? []) as unknown as Params4 },
    brain: { tier: brain?.tier, params: (brain?.params ?? []) as unknown as Params5 },
    heart: { tier: heart?.tier, params: (heart?.params ?? []) as unknown as Params6 },
    slots: Object.fromEntries(
      Object.entries(slotsRaw).map(([k, v]) => [
        k,
        { family: v?.family, params: (v?.params ?? []) as unknown as Params6 },
      ]),
    ) as Genome["slots"],
  };

  const r = validateGenome(g);
  if (!r.ok) throw new GenomeParseError("invalid genome", r.errors);
  return g;
}
