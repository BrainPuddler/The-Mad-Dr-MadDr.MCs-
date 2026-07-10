/**
 * @maddr/genome-core -- the genotype side of MadDr.MCs.
 *
 * Schema (docs/06, 15, 16, 17 -- genome v2, adopted per Q10),
 * operators (Mutate/Splice/Graft), validation, behavior expression,
 * canonical serialization, and the deterministic RNG. No graphics,
 * no engine, no I/O: pure data and pure functions.
 */

export * from "./genome.js";
export * from "./catalog.js";
export * from "./rng.js";
export * from "./operators.js";
export * from "./validate.js";
export * from "./behavior.js";
export * from "./energy.js";
export * from "./surgery.js";
export * from "./serialize.js";
export * from "./cost.js";
