/**
 * Genome serialization -- the wire/storage format (docs/07).
 *
 * Canonical JSON with stable key order, so that equal genomes serialize to
 * byte-identical strings (useful for hashing, signing, and golden tests).
 * Deserialization validates: a genome that didn't pass validateGenome
 * never enters the system.
 */
import { GENOME_VERSION, SLOT_NAMES, } from "./genome.js";
import { validateGenome } from "./validate.js";
export function toJson(g) {
    // explicit field order: stable across engines and refactors
    const slots = {};
    for (const s of SLOT_NAMES) {
        const a = g.slots[s];
        slots[s] = { family: a.family, params: a.params, ...(a.hue !== undefined ? { hue: a.hue } : {}) };
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
    errors;
    constructor(message, errors = []) {
        super(message);
        this.errors = errors;
        this.name = "GenomeParseError";
    }
}
export function fromJson(json) {
    let raw;
    try {
        raw = JSON.parse(json);
    }
    catch (e) {
        throw new GenomeParseError(`not valid JSON: ${e.message}`);
    }
    if (typeof raw !== "object" || raw === null) {
        throw new GenomeParseError("genome must be a JSON object");
    }
    const o = raw;
    if (o.genomeVersion !== GENOME_VERSION) {
        throw new GenomeParseError(`unsupported genomeVersion: ${String(o.genomeVersion)} (this build reads v${GENOME_VERSION})`);
    }
    // structural lift with no trust: validation decides what's acceptable
    const body = o.body;
    const brain = o.brain;
    const heart = o.heart;
    const slotsRaw = (o.slots ?? {});
    const g = {
        genomeVersion: GENOME_VERSION,
        ...(typeof o.creatureId === "string" ? { creatureId: o.creatureId } : {}),
        parentIds: Array.isArray(o.parentIds) ? o.parentIds : [],
        body: { plan: body?.plan, params: (body?.params ?? []) },
        brain: { tier: brain?.tier, params: (brain?.params ?? []) },
        heart: { tier: heart?.tier, params: (heart?.params ?? []) },
        slots: Object.fromEntries(Object.entries(slotsRaw).map(([k, v]) => [
            k,
            {
                family: v?.family,
                params: (v?.params ?? []),
                ...(typeof v?.hue === "number" ? { hue: v.hue } : {}),
            },
        ])),
    };
    const r = validateGenome(g);
    if (!r.ok)
        throw new GenomeParseError("invalid genome", r.errors);
    return g;
}
