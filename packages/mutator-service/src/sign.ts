import { createHmac, timingSafeEqual } from "node:crypto";
import { toJson, type Genome } from "@maddr/genome-core";

/** Genome signatures (docs/07): an HMAC by the Mutator service key over the
 * canonical genome JSON. Match servers verify these to trust a roster;
 * clients never hold the key. Canonical serialization (genome-core) makes
 * the signature stable. */
export function signGenome(g: Genome, key: string): string {
  return createHmac("sha256", key).update(toJson(g)).digest("hex");
}

export function verifyGenome(g: Genome, signature: string, key: string): boolean {
  const expected = signGenome(g, key);
  const a = Buffer.from(expected, "hex");
  const b = Buffer.from(signature, "hex");
  return a.length === b.length && timingSafeEqual(a, b);
}
