/**
 * The three Mutator operators (docs/06, 15): Mutate, Splice, Graft.
 *
 * All randomness comes from a caller-supplied Rng -- on the server this is
 * seeded per-operation and logged (docs/07 OperationLog.serverSeed).
 * Operators are pure: they never modify inputs and always return genomes
 * that pass validation (closure is enforced by tests).
 */
import { BRAIN_TIERS, GENOME_VERSION, HEART_TIERS, SLOT_NAMES, clamp01, heartVigor, } from "./genome.js";
import { BODY_PLANS, familiesInClass, homologOf, originOf, } from "./catalog.js";
import { viability } from "./energy.js";
const DEFAULTS = {
    rate: 0.45,
    sigma: 0.16,
    familyJump: 0.1,
    planJump: 0.03,
    tierShift: 0.06,
    heartShift: 0.06,
};
const FEED_BOOST = 3;
// ---- random generation -------------------------------------------------------
export function randomAllele(slot, rng, origins = ["organic"]) {
    const family = rng.choice(familiesInClass(slot, origins));
    return { family, params: sixOf(() => rng.next()) };
}
export function randomBody(rng, plan) {
    const p = plan ?? rng.choice(Object.keys(BODY_PLANS).sort());
    return { plan: p, params: fourOf(() => rng.next()) };
}
export function randomBrain(rng, tier) {
    const t = tier ?? rng.choice(BRAIN_TIERS);
    return { tier: t, params: fiveOf(() => rng.next()) };
}
export function randomHeart(rng, tier) {
    const t = tier ?? rng.choice(HEART_TIERS);
    return { tier: t, params: sixOf(() => rng.next()) };
}
/** Return g with its heart grown just big enough to be viable with
 * headroom -- a primordial creature is born able to run its own body.
 * Raises vigor first, then steps up the tier, until viable (or maxed). */
function fitHeart(g, headroom = 1.15) {
    let out = g;
    for (let guard = 0; guard < 16; guard++) {
        const v = viability(out);
        if (v.capacity >= v.load * headroom)
            return out;
        const i = HEART_TIERS.indexOf(out.heart.tier);
        if (heartVigor(out.heart) < 0.99) {
            // crank vigor to the top of the current tier
            out = { ...out, heart: { ...out.heart, params: setVigor(out.heart.params, 1) } };
        }
        else if (i < HEART_TIERS.length - 1) {
            // step to the next tier, reset vigor mid-range to leave room to drift
            out = {
                ...out,
                heart: { tier: HEART_TIERS[i + 1], params: setVigor(out.heart.params, 0.5) },
            };
        }
        else {
            return out; // already a titan at full vigor: nothing bigger exists
        }
    }
    return out;
}
export function randomGenome(rng, opts = {}) {
    const slots = {};
    for (const s of SLOT_NAMES)
        slots[s] = randomAllele(s, rng, opts.origins);
    const g = {
        genomeVersion: GENOME_VERSION,
        parentIds: [],
        body: randomBody(rng, opts.plan),
        brain: randomBrain(rng, opts.tier),
        heart: randomHeart(rng, opts.heartTier),
        slots,
    };
    // a primordial monster is born viable; a forced heartTier is left as-is
    return opts.heartTier ? g : fitHeart(g);
}
// ---- Mutate -------------------------------------------------------------------
export function mutate(g, rng, opts = {}) {
    const o = { ...DEFAULTS, ...opts };
    const slots = {};
    for (const slot of SLOT_NAMES) {
        const allele = g.slots[slot];
        if (originOf(allele.family) === "tech") {
            slots[slot] = allele; // issued equipment: flesh mutates, steel doesn't
            continue;
        }
        const boost = slot === opts.biasSlot ? FEED_BOOST : 1;
        let family = allele.family;
        if (rng.next() < o.familyJump * boost) {
            // jumps stay within the allele's origin: organic stays flesh,
            // biotech stays grown-tech (docs/17)
            const choices = familiesInClass(homologOf(family), [originOf(family)]).filter((f) => f !== family);
            if (choices.length > 0)
                family = rng.choice(choices);
        }
        const params = mapParams6(allele.params, (p) => rng.next() < o.rate * boost ? clamp01(p + rng.gauss(0, o.sigma)) : p);
        slots[slot] = { family, params };
    }
    let plan = g.body.plan;
    if (rng.next() < o.planJump) {
        const others = Object.keys(BODY_PLANS).sort().filter((p) => p !== plan);
        plan = rng.choice(others);
    }
    const body = {
        plan,
        params: mapParams4(g.body.params, (p) => rng.next() < o.rate ? clamp01(p + rng.gauss(0, o.sigma)) : p),
    };
    let tier = g.brain.tier;
    if (rng.next() < o.tierShift) {
        const i = BRAIN_TIERS.indexOf(tier);
        const j = Math.max(0, Math.min(BRAIN_TIERS.length - 1, i + (rng.bool() ? 1 : -1)));
        tier = BRAIN_TIERS[j];
    }
    const brain = {
        tier,
        params: mapParams5(g.brain.params, (p) => rng.next() < o.rate ? clamp01(p + rng.gauss(0, o.sigma)) : p),
    };
    let heartTier = g.heart.tier;
    if (rng.next() < o.heartShift) {
        const i = HEART_TIERS.indexOf(heartTier);
        const j = Math.max(0, Math.min(HEART_TIERS.length - 1, i + (rng.bool() ? 1 : -1)));
        heartTier = HEART_TIERS[j];
    }
    const heart = {
        tier: heartTier,
        params: mapParams6(g.heart.params, (p) => rng.next() < o.rate ? clamp01(p + rng.gauss(0, o.sigma)) : p),
    };
    return {
        genomeVersion: GENOME_VERSION,
        parentIds: g.creatureId ? [g.creatureId] : [],
        body,
        brain,
        heart,
        slots,
    };
}
// ---- Splice -------------------------------------------------------------------
export function splice(a, b, rng, noise = 0.05) {
    const slots = {};
    for (const slot of SLOT_NAMES) {
        const alA = a.slots[slot];
        const alB = b.slots[slot];
        const src = rng.bool() ? alA : alB;
        if (originOf(src.family) === "tech") {
            // you don't gene-splice a rifle: the issued item passes whole
            slots[slot] = src;
            continue;
        }
        const params = zipParams6(alA.params, alB.params, (pa, pb) => clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise)));
        slots[slot] = { family: src.family, params };
    }
    const body = {
        plan: rng.bool() ? a.body.plan : b.body.plan,
        params: zipParams4(a.body.params, b.body.params, (pa, pb) => clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise))),
    };
    const brain = {
        tier: rng.bool() ? a.brain.tier : b.brain.tier,
        params: zipParams5(a.brain.params, b.brain.params, (pa, pb) => clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise))),
    };
    const heart = {
        tier: rng.bool() ? a.heart.tier : b.heart.tier,
        params: zipParams6(a.heart.params, b.heart.params, (pa, pb) => clamp01(pa + rng.next() * (pb - pa) + rng.gauss(0, noise))),
    };
    const parents = [];
    if (a.creatureId)
        parents.push(a.creatureId);
    if (b.creatureId)
        parents.push(b.creatureId);
    return { genomeVersion: GENOME_VERSION, parentIds: parents, body, brain, heart, slots };
}
// ---- Graft --------------------------------------------------------------------
/** Deterministic slot replacement -- the player-control valve (docs/06);
 * for the human faction, fictionally the quartermaster (docs/17). Throws
 * on a homolog-grammar violation. */
export function graft(g, slot, family, params) {
    if (homologOf(family) !== slot) {
        throw new Error(`${family} does not fit the ${slot} slot (homolog grammar)`);
    }
    const allele = { family, params: mapParams6(params, clamp01) };
    return {
        ...g,
        parentIds: g.creatureId ? [g.creatureId] : [],
        creatureId: undefined,
        slots: { ...g.slots, [slot]: allele },
    };
}
// ---- tuple helpers (fixed-length, no surprises) --------------------------------
function sixOf(f) {
    return [f(), f(), f(), f(), f(), f()];
}
function setVigor(p, vigor) {
    return [vigor, p[1], p[2], p[3], p[4], p[5]];
}
function fiveOf(f) {
    return [f(), f(), f(), f(), f()];
}
function fourOf(f) {
    return [f(), f(), f(), f()];
}
function mapParams6(p, f) {
    return [f(p[0]), f(p[1]), f(p[2]), f(p[3]), f(p[4]), f(p[5])];
}
function mapParams5(p, f) {
    return [f(p[0]), f(p[1]), f(p[2]), f(p[3]), f(p[4])];
}
function mapParams4(p, f) {
    return [f(p[0]), f(p[1]), f(p[2]), f(p[3])];
}
function zipParams6(a, b, f) {
    return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3]), f(a[4], b[4]), f(a[5], b[5])];
}
function zipParams5(a, b, f) {
    return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3]), f(a[4], b[4])];
}
function zipParams4(a, b, f) {
    return [f(a[0], b[0]), f(a[1], b[1]), f(a[2], b[2]), f(a[3], b[3])];
}
