/**
 * Secondary attacks -- the derived-stat twin of the Unity battlefield's
 * `SecondaryAttackCatalog.cs` (docs/26 Phase 9/10: "Roll secondary
 * attacks for all races into the lab and all monsters"; 2026-08 follow-
 * up, "Expand Secondary Attack Variety Across Races"). Pure function of
 * the genome, exactly like harvest.ts: nothing here is a new gene -- a
 * monster's secondary attacks are read off its existing HAND family, the
 * same signal `Combat.WeaponFor` (roster-client) already keys its
 * primary weapon on, so a monster's weapon and its secondary attacks
 * always read as thematically consistent for free.
 *
 * KEEP THIS IN SYNC WITH THE C# TWIN. There is no automated golden test
 * for this one yet (unlike Locomotion/Weapon/Harvest, which are numeric
 * enough to golden-verify against real Node output) -- the classification
 * table and every ability's numbers below must be hand-kept identical to
 * `unity-client/Assets/Scripts/SecondaryAttackCatalog.cs`. Flagged, not
 * hidden (docs/12, 2026-07).
 *
 * 2026-08 (creator direction: "did that get rewritten in the lab as
 * well?"): the first pass of the variety-expansion work only touched the
 * Unity side -- this file was missed, which meant the Lab kept showing
 * exactly one ability per hand family (the pre-expansion state) while
 * the real battlefield had already moved to pools of 5-6. Every
 * `secondaryAttackFor*` function now returns the FULL POOL (a readonly
 * array), matching `SecondaryAttackCatalog.ForMonster`'s real return
 * shape, not a single ability.
 *
 * Humans (Tanks) are NOT genome creatures -- their secondary-attack pool
 * is fixed archetype data equipped in Tank.cs, not bred, so it has no
 * Lab representation and isn't modeled here (unchanged from before this
 * pass). This module only covers the three genome-side hand-family
 * groups: Mad Doctor default, Alien/Psionic, Electric/Tech.
 */
/** The exact three hand families the creator called out as alien tech
 * ("aliens laser and photonic blasters") -- must match
 * `SecondaryAttackCatalog.ForMonster`'s switch cases 1:1. */
const ALIEN_HAND_FAMILIES = new Set([
    "laser_array",
    "photon_blaster",
    "plasma_lance",
]);
/** 2026-08 (creator direction: "add [an electric attack]... Area shock
 * stuns enemy units for 10 seconds"): the fourth alien-tech hand family
 * -- must match `SecondaryAttackCatalog.ForMonster`'s electric_arc case. */
const ELECTRIC_ARC_HAND_FAMILY = "electric_arc";
// ---- Mad Doctor default pool ("the Lab Gnome") -----------------------
const GROUND_STOMP = {
    kind: "ground_stomp",
    name: "Ground Stomp",
    description: "A seismic slam that freezes anything standing too close.",
    effect: "stun",
    range: 0,
    areaOfEffect: 6,
    bloodCost: 2,
    bonesCost: 4,
    stunDurationSeconds: 2,
};
const SPORE_CLOUD = {
    kind: "spore_cloud",
    name: "Spore Cloud",
    description: "A drifting cloud of mind-altering spores -- rarely, something caught inside loses itself for a moment.",
    effect: "possess",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 2,
    bonesCost: 2,
    possessChancePercent: 3,
    possessDurationSeconds: 3,
};
const DEFENSIVE_SPORE_BURST = {
    kind: "defensive_spore_burst",
    name: "Defensive Spore Burst",
    description: "A choking burst of spores that drives back anything pressing the attack.",
    effect: "fear",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 1,
    bonesCost: 2,
    fearDurationSeconds: 3,
    isDefensive: true,
};
const TOXIC_SAC = {
    kind: "toxic_sac",
    name: "Toxic Sac",
    description: "A thrown biological sac that bursts into a lingering toxic patch.",
    effect: "hazard",
    range: 12,
    areaOfEffect: 4,
    bloodCost: 2,
    bonesCost: 3,
    hazardDurationSeconds: 6,
    hazardTickIntervalSeconds: 0.6,
    tempoMultiplier: 1.6,
    tempoDurationSeconds: 1.2,
};
const PANIC_SHRIEK = {
    kind: "panic_shriek",
    name: "Panic Shriek",
    description: "A sudden bone-deep shriek that scatters attention in every direction.",
    effect: "fear",
    range: 0,
    areaOfEffect: 7,
    bloodCost: 2,
    bonesCost: 2,
    fearDurationSeconds: 2.5,
};
const MUTAGENIC_PULSE = {
    kind: "mutagenic_pulse",
    name: "Mutagenic Pulse",
    description: "A pulsing wave of unstable biology that fouls muscle and nerve alike.",
    effect: "weaken",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 2,
    bonesCost: 2,
    tempoMultiplier: 1.5,
    tempoDurationSeconds: 4,
};
const MAD_DOCTOR_DEFAULT_POOL = [
    GROUND_STOMP,
    SPORE_CLOUD,
    DEFENSIVE_SPORE_BURST,
    TOXIC_SAC,
    PANIC_SHRIEK,
    MUTAGENIC_PULSE,
];
// ---- Alien/Psionic pool -------------------------------------------------
const PSIONIC_TRACTOR_BEAM = {
    kind: "psionic_tractor_beam",
    name: "Psionic Tractor Beam",
    description: "A short-range beam of alien mental force that hauls light targets in and pins down anything too heavy to lift.",
    effect: "pull_and_consume",
    range: 10,
    areaOfEffect: 2,
    bloodCost: 3,
    bonesCost: 1,
};
const PSYCHIC_PULSE = {
    kind: "psychic_pulse",
    name: "Psychic Pulse",
    description: "A radiating wave of alien mental force that scrambles nearby minds.",
    effect: "weaken",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 3,
    bonesCost: 1,
    tempoMultiplier: 1.6,
    tempoDurationSeconds: 4,
};
const NEURAL_DISRUPTION = {
    kind: "neural_disruption",
    name: "Neural Disruption",
    description: "A focused psychic strike that overloads a target's own nervous system.",
    effect: "fear",
    range: 9,
    areaOfEffect: 3,
    bloodCost: 3,
    bonesCost: 1,
    fearDurationSeconds: 3,
};
const PSYCHIC_SHIELD = {
    kind: "psychic_shield",
    name: "Psychic Shield",
    description: "A warping mental deterrent that unnerves anything closing in.",
    effect: "fear",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 2,
    bonesCost: 1,
    fearDurationSeconds: 2.5,
    isDefensive: true,
};
const MIND_CONTROL = {
    kind: "mind_control",
    name: "Mind Control",
    description: "A focused psychic intrusion that can seize a weak mind for a few seconds.",
    effect: "possess",
    range: 8,
    areaOfEffect: 2,
    bloodCost: 4,
    bonesCost: 2,
    possessChancePercent: 5,
    possessDurationSeconds: 4,
};
const ALIEN_POOL = [
    PSIONIC_TRACTOR_BEAM,
    PSYCHIC_PULSE,
    NEURAL_DISRUPTION,
    PSYCHIC_SHIELD,
    MIND_CONTROL,
];
// ---- Electric/Tech pool -------------------------------------------------
const AREA_SHOCK = {
    kind: "area_shock",
    name: "Area Shock",
    description: "A crackling discharge of electrical current that locks up anything caught within range.",
    effect: "stun",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 4,
    bonesCost: 2,
    stunDurationSeconds: 10,
};
const EMP_PULSE = {
    kind: "emp_pulse",
    name: "EMP Pulse",
    description: "A short-range electromagnetic surge that overloads nearby systems.",
    effect: "weaken",
    range: 0,
    areaOfEffect: 5,
    bloodCost: 3,
    bonesCost: 2,
    tempoMultiplier: 1.7,
    tempoDurationSeconds: 4,
};
const DISCHARGE_BURST = {
    kind: "discharge_burst",
    name: "Discharge Burst",
    description: "A defensive arc of current that startles anything closing in.",
    effect: "fear",
    range: 0,
    areaOfEffect: 4,
    bloodCost: 2,
    bonesCost: 2,
    fearDurationSeconds: 2.5,
    isDefensive: true,
};
const ARC_LANCE = {
    kind: "arc_lance",
    name: "Arc Lance",
    description: "A focused lance of electrical current punched straight through a target.",
    effect: "damage",
    range: 12,
    areaOfEffect: 2,
    bloodCost: 3,
    bonesCost: 2,
    damageAmount: 25,
};
const MAGNETIC_TETHER = {
    kind: "magnetic_tether",
    name: "Magnetic Tether",
    description: "A short-range magnetic pulse that hauls light targets in and pins down anything too heavy to lift.",
    effect: "pull_and_consume",
    range: 9,
    areaOfEffect: 2,
    bloodCost: 3,
    bonesCost: 1,
};
const ELECTRIC_POOL = [
    AREA_SHOCK,
    EMP_PULSE,
    DISCHARGE_BURST,
    ARC_LANCE,
    MAGNETIC_TETHER,
];
/** Which secondary-attack POOL a MONSTER is equipped with, derived
 * purely from its hand family -- alien-tech hands (laser/photon/plasma)
 * get the alien pool (literally the same pull-and-consume mechanic Web
 * Attack uses for its own first entry, just a shorter-range definition);
 * the electric_arc hand gets the electric pool; everything else --
 * including a vestigial or cut-off hand (chop-shop-safe: a stump reads
 * the same as an unarmed creature, since it's simply not in either set
 * above) -- gets the Mad Doctor's default pool. Mirrors
 * `SecondaryAttackCatalog.ForMonster`'s switch exactly, same cases, same
 * default. */
export function secondaryAttackFor(handFamily) {
    if (ALIEN_HAND_FAMILIES.has(handFamily))
        return ALIEN_POOL;
    if (handFamily === ELECTRIC_ARC_HAND_FAMILY)
        return ELECTRIC_POOL;
    return MAD_DOCTOR_DEFAULT_POOL;
}
/** Convenience overload reading straight off a genome's hand slot. */
export function secondaryAttackForGenome(g) {
    return secondaryAttackFor(g.slots.hand.family);
}
