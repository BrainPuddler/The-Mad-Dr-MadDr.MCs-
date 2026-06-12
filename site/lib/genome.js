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
export const GENOME_VERSION = 2;
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
];
/** Body-plan axes. Shared across plans with plan-specific expression
 * (for a blob, posture expresses as membrane wobble; for the winged,
 * limb is wingspan) -- docs/15 "Body plans". */
export const BODY_AXES = ["posture", "bulk", "limb", "tail"];
/** Brain (behavioral) axes -- docs/16. */
export const BRAIN_AXES = [
    "command",
    "will",
    "temperament",
    "guile",
    "fury",
];
/** Brain quality tiers (docs/06 brain budget). Tier also sets brain SIZE,
 * the scalar in control capacity and cost (docs/16). */
export const BRAIN_TIERS = ["dim", "average", "gifted", "mastermind"];
export const BRAIN_SIZE = {
    dim: 1,
    average: 2,
    gifted: 3,
    mastermind: 4,
};
/** The standard slot set. Slots are homolog classes: crossover and family
 * jumps only ever swap within a slot's class (docs/15, Strategy 3 -- the
 * Hox rule). Plans may IGNORE slots at expression time (serpentine ignores
 * "leg"); the genes ride along silently -- the atavism. */
export const SLOT_NAMES = ["hand", "sensor", "eye", "leg"];
/** Heart (circulatory) tiers. The heart is the SUPPLY organ: it sets how
 * much upkeep the body can sustain (docs/06 viability). Tier sets the base
 * pumping output; the first param (vigor) tunes it within the tier. A
 * heart that cannot drive the body's parts kills the creature on the
 * operating table (docs/06 grafting; surgery.ts). */
export const HEART_TIERS = ["faint", "steady", "strong", "titan"];
/** Base circulatory output by tier, in energy-units/min (see energy.ts). */
export const HEART_OUTPUT = {
    faint: 14,
    steady: 26,
    strong: 42,
    titan: 64,
};
// ---- accessors --------------------------------------------------------------
export function partAxis(allele, axis) {
    return allele.params[PART_AXES.indexOf(axis)];
}
export function bodyAxis(body, axis) {
    return body.params[BODY_AXES.indexOf(axis)];
}
export function brainAxis(brain, axis) {
    return brain.params[BRAIN_AXES.indexOf(axis)];
}
export function brainSize(brain) {
    return BRAIN_SIZE[brain.tier];
}
/** Heart vigor: tunes pumping output within the tier (params[0]). */
export function heartVigor(heart) {
    return heart.params[0];
}
/** Structural replace of one slot; carries everything else through. */
export function withSlot(g, slot, allele) {
    return { ...g, slots: { ...g.slots, [slot]: allele } };
}
export function clamp01(x) {
    return x < 0 ? 0 : x > 1 ? 1 : x;
}
