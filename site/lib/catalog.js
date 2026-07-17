/**
 * The part catalog and body plans -- the shared contract between the
 * genome (this package), the Mutator service (docs/07), and the renderer
 * (docs/08). Versioned together with the genome schema.
 *
 * Each family declares:
 *  - homolog: the slot class it fits (the Hox grammar, docs/15)
 *  - origin:  organic (breeds) | tech (issued; never mutates or blends;
 *             Graft-only) | biotech (grown machines; breeds like flesh)
 *             -- docs/17
 *  - bounds:  per-axis CANALIZED phenotype bounds; genotype is always the
 *             full [0,1], expression maps into the authored-safe range
 *  - invariants: prose record of what may never vary (identity)
 */
import { PART_AXES } from "./genome.js";
const FULL = [0, 1];
function bounds(over = {}) {
    const b = {};
    for (const a of PART_AXES)
        b[a] = over[a] ?? FULL;
    return b;
}
export const FAMILIES = {
    // ---- hand homologs --------------------------------------------------------
    claw_hand: {
        homolog: "hand", origin: "organic",
        bounds: bounds({ curl: [0.1, 0.9] }),
        invariants: "a palm bearing 2-5 hard, curved, tapering talons",
    },
    pincer: {
        homolog: "hand", origin: "organic",
        bounds: bounds({ curl: [0.2, 1.0], count: [0.0, 0.6] }),
        invariants: "two opposing crescent jaws meeting at a gap",
    },
    tentacle: {
        homolog: "hand", origin: "organic",
        bounds: bounds({ girth: [0.0, 0.7], taper: [0.4, 1.0] }),
        invariants: "a single smooth limb tapering to a curling tip",
    },
    rifle_arm: {
        homolog: "hand", origin: "tech",
        bounds: bounds({ curl: [0.0, 0.2] }),
        invariants: "an arm shouldering a long gun: barrel, stock, trigger group",
    },
    plasma_lance: {
        homolog: "hand", origin: "biotech",
        bounds: bounds({ taper: [0.5, 1.0] }),
        invariants: "a fleshy arm ending in a glowing lance emitter with a charge bulb",
    },
    laser_array: {
        homolog: "hand", origin: "biotech",
        bounds: bounds({ count: [0.4, 1.0], curl: [0.0, 0.3] }),
        invariants: "a fleshy arm bearing a rigid cluster of narrow crystalline emitters firing needle-thin beams",
    },
    photon_blaster: {
        homolog: "hand", origin: "biotech",
        bounds: bounds({ girth: [0.4, 1.0], ornament: [0.3, 1.0] }),
        invariants: "a fleshy arm ending in a broad bioluminescent maw that pulses with stored photon charge",
    },
    chain_blade: {
        homolog: "hand", origin: "tech",
        bounds: bounds({ curl: [0.0, 0.25] }),
        invariants: "an arm ending in a motorized rotary chain-blade on a guide bar",
    },
    spore_launcher: {
        homolog: "hand", origin: "biotech",
        bounds: bounds({ girth: [0.35, 1.0] }),
        invariants: "a fleshy arm ending in a bulbous veined pod that vents spore motes",
    },
    // ---- hand homologs: harvest tools (docs/22 harvester morphology) ----------
    // Tools, not weapons: they gather blood/bone from targets living or dead
    // (harvest.ts computes the rates). They breed, mutate, and graft exactly
    // like any other hand family -- a harvester is BRED, not bolted on.
    lamprey_maw: {
        homolog: "hand", origin: "organic",
        bounds: bounds({ girth: [0.25, 0.85], taper: [0.4, 1.0], count: [0.2, 1.0] }),
        invariants: "a fleshy hose-arm ending in a round rasping sucker mouth ringed with tooth rows; drinks from the living and the dead alike",
    },
    bone_saw: {
        homolog: "hand", origin: "tech",
        bounds: bounds({ curl: [0.0, 0.25], girth: [0.3, 0.9] }),
        invariants: "an arm mounting a motorized circular surgical saw on an articulated boom; cuts skeleton out of carcasses",
    },
    ichor_siphon: {
        homolog: "hand", origin: "biotech",
        bounds: bounds({ girth: [0.2, 0.7], taper: [0.5, 1.0], count: [0.3, 1.0] }),
        invariants: "a fleshy arm splitting into translucent siphon tubes that drink from wounds, pulsing as they draw",
    },
    // ---- sensor homologs ------------------------------------------------------
    antenna: {
        homolog: "sensor", origin: "organic",
        bounds: bounds({ girth: [0.0, 0.45], length: [0.3, 1.0] }),
        invariants: "a thin paired stalk, segmented, ending in a tip bulb",
    },
    horn: {
        homolog: "sensor", origin: "organic",
        bounds: bounds({ girth: [0.3, 1.0], curl: [0.0, 0.7] }),
        invariants: "a rigid ridged cone, broad base to sharp point",
    },
    sensor_mast: {
        homolog: "sensor", origin: "tech",
        bounds: bounds({ girth: [0.0, 0.4] }),
        invariants: "a single rigid antenna mast with a dish or vane at the tip",
    },
    // ---- sensor homologs: storage vessels (docs/22 harvester morphology) ------
    // Dorsal-mounted like every sensor family (antenna/horn/mast all express
    // at the same mount), but these are TANKS, not senses: they extend the
    // creature's onboard resource capacity (harvest.ts). Storage rides the
    // sensor homolog so it breeds within the existing Hox grammar -- no new
    // slot, no schema change; the trade is real: a tank on your back is a
    // sensor you don't have.
    storage_bladder: {
        homolog: "sensor", origin: "organic",
        bounds: bounds({ girth: [0.4, 1.0], length: [0.2, 0.8] }),
        invariants: "a translucent distended dorsal sac, membrane-veined, that visibly sloshes as it fills",
    },
    steel_tank: {
        homolog: "sensor", origin: "tech",
        bounds: bounds({ girth: [0.35, 0.9], length: [0.3, 0.9], curl: [0.0, 0.2] }),
        invariants: "a riveted steel dorsal tank with a filler cap and a sight gauge; sloshes dully when struck",
    },
    amber_vesicle: {
        homolog: "sensor", origin: "biotech",
        bounds: bounds({ girth: [0.3, 0.9], count: [0.3, 1.0] }),
        invariants: "a clustered mass of amber vesicles fused along the spine, each glowing faintly with what it holds",
    },
    // ---- eye homologs ---------------------------------------------------------
    bug_eyes: {
        homolog: "eye", origin: "organic",
        bounds: bounds({ count: [0.2, 1.0] }),
        invariants: "a clustered constellation of 3+ round eyes with pupils",
    },
    cyclops_eye: {
        homolog: "eye", origin: "organic",
        bounds: bounds({ count: [0.0, 0.0], girth: [0.3, 1.0] }),
        invariants: "one single oversized lidded eye",
    },
    stalk_eyes: {
        homolog: "eye", origin: "organic",
        bounds: bounds({ length: [0.3, 1.0], girth: [0.0, 0.5] }),
        invariants: "eyeballs held aloft on flexible stalks",
    },
    optic_visor: {
        homolog: "eye", origin: "tech",
        bounds: bounds({ count: [0.0, 0.6] }),
        invariants: "a rectangular visor band with 1-3 round lenses",
    },
    // ---- leg homologs ---------------------------------------------------------
    hoofed_leg: {
        homolog: "leg", origin: "organic",
        bounds: bounds({ girth: [0.35, 1.0], curl: [0.0, 0.6] }),
        invariants: "a sturdy column ending in a hard cloven hoof",
    },
    talon_leg: {
        homolog: "leg", origin: "organic",
        bounds: bounds({ girth: [0.0, 0.4], count: [0.2, 1.0] }),
        invariants: "a thin bird leg, backward knee, ending in splayed clawed toes",
    },
    insect_leg: {
        homolog: "leg", origin: "organic",
        bounds: bounds({ girth: [0.0, 0.35], curl: [0.3, 1.0] }),
        invariants: "a segmented zigzag of chitinous struts with a tarsal hook",
    },
    piston_leg: {
        homolog: "leg", origin: "tech",
        bounds: bounds({ girth: [0.3, 0.9], curl: [0.0, 0.3] }),
        invariants: "a hydraulic strut: cylinder, piston rod, flat foot-plate",
    },
    jet_leg: {
        homolog: "leg", origin: "tech",
        bounds: bounds({ girth: [0.2, 0.7] }),
        invariants: "a strut ending in a gimbaled thruster nozzle; no foot ever touches down",
    },
    tendril_leg: {
        homolog: "leg", origin: "biotech",
        bounds: bounds({ girth: [0.0, 0.6], curl: [0.2, 1.0] }),
        invariants: "a boneless muscular pseudopod, tapering and rippling",
    },
    // ---- stumps: what a slot heals to after a part is harvested --------------
    hand_stump: {
        homolog: "hand", origin: "organic", vestigial: true,
        bounds: bounds({ length: [0, 0.1], girth: [0, 0.15] }),
        invariants: "a rounded, scarred-over stump where an arm was",
    },
    sensor_stub: {
        homolog: "sensor", origin: "organic", vestigial: true,
        bounds: bounds({ length: [0, 0.1], girth: [0, 0.1] }),
        invariants: "a healed nub where a sensor was",
    },
    eye_socket: {
        homolog: "eye", origin: "organic", vestigial: true,
        bounds: bounds({ length: [0, 0.05], girth: [0, 0.15] }),
        invariants: "an empty closed socket",
    },
    leg_stump: {
        homolog: "leg", origin: "organic", vestigial: true,
        bounds: bounds({ length: [0, 0.1], girth: [0, 0.2] }),
        invariants: "a scarred-over stump where a leg was",
    },
};
/** The stump family a slot heals to when its part is harvested. */
export const STUMP_OF = {
    hand: "hand_stump",
    sensor: "sensor_stub",
    eye: "eye_socket",
    leg: "leg_stump",
};
export const BODY_PLANS = {
    tetrapod: {
        invariants: "a torso on limbs with a head: posture spans biped to quadruped",
        ignoresSlots: [],
    },
    blob: {
        invariants: "a single amorphous mass; parts surface-mount on the membrane",
        ignoresSlots: ["leg"],
    },
    serpentine: {
        invariants: "one long tapering body coiling along the ground, head at the fore",
        ignoresSlots: ["leg"],
        amphibious: true, // the sea serpent: slides into rivers as easily as over land
    },
    winged: {
        invariants: "a small body slung between two membrane wings, standing on legs",
        ignoresSlots: [],
    },
    crab: {
        invariants: "a wide low shell body on a sideways stance, claws held forward",
        ignoresSlots: [],
        amphibious: true, // shoreline-native: water is its highway, not its wall
    },
    arachnid: {
        invariants: "a hunched two-part body low to the ground, crowded with legs",
        ignoresSlots: [],
    },
    avian: {
        invariants: "a forward-leaning two-legged runner: long neck, small head, tail counterbalance",
        ignoresSlots: [],
    },
    treant: {
        invariants: "a rooted trunk-like body with branch limbs; no true legs, it stands on roots",
        ignoresSlots: ["leg"],
    },
    floater: {
        invariants: "a sleek hovering drone-pod: a tapered spindle hull on a thruster ring, fin-stabilized; no legs",
        ignoresSlots: ["leg"],
    },
};
// ---- queries ---------------------------------------------------------------
export function isKnownFamily(family) {
    return Object.prototype.hasOwnProperty.call(FAMILIES, family);
}
export function homologOf(family) {
    const f = FAMILIES[family];
    if (!f)
        throw new Error(`unknown part family: ${family}`);
    return f.homolog;
}
export function originOf(family) {
    const f = FAMILIES[family];
    if (!f)
        throw new Error(`unknown part family: ${family}`);
    return f.origin;
}
export function isVestigial(family) {
    return FAMILIES[family]?.vestigial === true;
}
/** Whether a body plan crosses water hexes (docs/04 water rule; docs/18
 * terrain). Unknown plans are ground-bound rather than an error -- this
 * is a movement query the match sim calls in a hot path, not validation. */
export function isAmphibious(plan) {
    return BODY_PLANS[plan]?.amphibious === true;
}
/** Families fitting a slot, filtered by origin. Mutation family-jumps stay
 * within the allele's own origin; random generation defaults to organic --
 * tech enters a genome only by explicit issue (docs/17). Vestigial stumps
 * are never offered: you cut your way to a stump, you don't grow one. */
export function familiesInClass(homolog, origins = ["organic"]) {
    return Object.keys(FAMILIES)
        .filter((f) => {
        const spec = FAMILIES[f];
        return spec.homolog === homolog && origins.includes(spec.origin) && !spec.vestigial;
    })
        .sort();
}
/** Canalized expression: map a [0,1] gene into the family's phenotype
 * range for one axis (docs/15, Strategy 4). */
export function express(family, axis, gene) {
    const f = FAMILIES[family];
    if (!f)
        throw new Error(`unknown part family: ${family}`);
    const [lo, hi] = f.bounds[axis];
    return lo + gene * (hi - lo);
}
