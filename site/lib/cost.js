/**
 * Bones cost: how much structural material a genome bills to build
 * (docs/06 "The bill: Bones, Parts, and a Brain", docs/20 Cannibalize).
 *
 * docs/06 originally phrased this as `4*sizeClass + 0.1*Vitality + 2*Armor`
 * -- a stat-block vocabulary (`Vitality`, `Armor`, `sizeClass`) that never
 * shipped in the genome v2 schema actually adopted (Q10): there is no
 * `statGenes` block on `Genome`. This is the real-schema equivalent,
 * computed from the fields that DO exist -- body bulk, heart tier, and
 * limb mass (mirroring how docs/17's Motive-class formula already prices
 * limbs as `length * girth`) -- so it means the same thing docs/06 always
 * intended ("bigger, tougher builds cost more Bones") without referencing
 * genes that aren't real. docs/06 needs a follow-up correction to match.
 *
 * v0.1 constants, chosen so the range (roughly 0-70) lands in the same
 * neighborhood as docs/06's original worked examples (9-76), not because
 * the two formulas are meant to agree exactly.
 */
import { bodyAxis, partAxis, HEART_TIERS } from "./genome.js";
import { SLOT_NAMES } from "./genome.js";
export function bonesCost(genome) {
    const bulk = bodyAxis(genome.body, "bulk"); // [0,1]
    const heartWeight = HEART_TIERS.indexOf(genome.heart.tier); // 0..3
    let limbMass = 0;
    for (const slot of SLOT_NAMES) {
        const allele = genome.slots[slot];
        limbMass += partAxis(allele, "length") * partAxis(allele, "girth");
    }
    // limbMass is 0..(number of slots), one [0,1]*[0,1] term per slot
    return 20 * bulk + 8 * heartWeight + 6 * limbMass;
}
