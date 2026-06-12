import { randomBytes, randomInt } from "node:crypto";

/** Short, sortable-ish opaque ids. Prefixes make logs readable. */
export function genId(prefix: string): string {
  return `${prefix}_${randomBytes(9).toString("hex")}`;
}

/** A server-seeded 32-bit RNG seed (docs/07): generated and logged per
 * operation so results are auditable and replayable, and a resubmitted
 * idempotency key cannot reroll. */
export function newServerSeed(): number {
  return randomInt(0, 0x100000000);
}
