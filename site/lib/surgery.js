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
import { clamp01, } from "./genome.js";
import { STUMP_OF, homologOf, isVestigial, originOf } from "./catalog.js";
import { viability } from "./energy.js";
const ZERO6 = [0, 0, 0, 0, 0, 0];
export function partItemHomolog(item) {
    return homologOf(item.family);
}
export function partItemOrigin(item) {
    return originOf(item.family);
}
// ---- harvest: cut a part off ------------------------------------------------
/** Cut the part in `slot` off the creature. The donor's slot heals to a
 * stump; the part comes away as a reusable item. (Harvesting from a live
 * creature is legal -- it simply leaves a stumped, lighter creature.) */
export function harvestPart(g, slot) {
    const taken = g.slots[slot];
    const part = {
        kind: "part",
        family: taken.family,
        params: taken.params,
        hue: taken.hue ?? g.body.params[0],
        ...(g.creatureId ? { from: g.creatureId } : {}),
    };
    const donor = stumpSlot(g, slot);
    return { donor, part };
}
/** Cut the heart out. This leaves the donor without a working pump -- you
 * harvest a heart from a corpse, or you kill the donor doing it. */
export function harvestHeart(g) {
    const heart = {
        kind: "heart",
        tier: g.heart.tier,
        params: g.heart.params,
        ...(g.creatureId ? { from: g.creatureId } : {}),
    };
    // the cavity is left with a barely-beating vestige (vigor 0): a corpse
    // for any body of real size
    const donor = {
        ...g,
        parentIds: g.creatureId ? [g.creatureId] : g.parentIds,
        creatureId: undefined,
        heart: { tier: "faint", params: ZERO6 },
    };
    return { donor, heart };
}
/** Sew a harvested part into a slot, then see whether the heart can run
 * the result. A slot that already holds a real part is a SWAP: the prior
 * occupant comes off and is handed back (explantedPart), not discarded --
 * grafting over an existing head shouldn't just erase it. Throws only on
 * a homolog-grammar violation (you cannot sew an arm into an eye socket);
 * energy failure is a returned outcome, not an exception. */
export function sewPart(patient, slot, part) {
    if (partItemHomolog(part) !== slot) {
        throw new Error(`${part.family} does not fit the ${slot} slot (homolog grammar)`);
    }
    const prior = patient.slots[slot];
    const candidate = {
        ...patient,
        parentIds: patient.creatureId ? [patient.creatureId] : patient.parentIds,
        creatureId: undefined,
        slots: { ...patient.slots, [slot]: { family: part.family, params: clampAll(part.params), hue: part.hue } },
    };
    const v = viability(candidate);
    if (v.state === "viable") {
        const explantedPart = isVestigial(prior.family)
            ? undefined
            : {
                kind: "part",
                family: prior.family,
                params: prior.params,
                hue: prior.hue ?? patient.body.params[0],
                ...(patient.creatureId ? { from: patient.creatureId } : {}),
            };
        return { result: "survived", patient: candidate, alive: true, viability: v, explantedPart };
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
/** Transplant a heart. This is the counterplay to an over-loaded body:
 * a bigger heart lets you carry heavier grafts. A heart that still cannot
 * drive the body kills the patient -- you don't get a second start. */
export function sewHeart(patient, heart) {
    const old = patient.heart;
    const candidate = {
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
    const explanted = {
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
function stumpSlot(g, slot) {
    return {
        ...g,
        parentIds: g.creatureId ? [g.creatureId] : g.parentIds,
        creatureId: undefined,
        slots: { ...g.slots, [slot]: { family: STUMP_OF[slot], params: ZERO6 } },
    };
}
function clampAll(p) {
    return [
        clamp01(p[0]), clamp01(p[1]), clamp01(p[2]),
        clamp01(p[3]), clamp01(p[4]), clamp01(p[5]),
    ];
}
