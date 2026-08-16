/**
 * Secondary attacks -- the derived-stat twin of the Unity battlefield's
 * `SecondaryAttackCatalog.cs` (docs/26 Phase 9/10: "Roll secondary
 * attacks for all races into the lab and all monsters"). Pure function
 * of the genome, exactly like harvest.ts: nothing here is a new gene --
 * a monster's secondary attack is read off its existing HAND family, the
 * same signal `Combat.WeaponFor` (roster-client) already keys its
 * primary weapon on, so a monster's weapon and its secondary attack
 * always read as thematically consistent for free.
 *
 * KEEP THIS IN SYNC WITH THE C# TWIN. There is no automated golden test
 * for this one yet (unlike Locomotion/Weapon/Harvest, which are numeric
 * enough to golden-verify against real Node output) -- the two
 * classification tables and cost numbers below must be hand-kept
 * identical to `unity-client/Assets/Scripts/SecondaryAttackCatalog.cs`.
 * Flagged, not hidden (docs/12, 2026-07).
 *
 * Humans (Tanks) are NOT genome creatures -- their Flamethrower
 * secondary attack is fixed archetype data in Tank.cs, not bred, so it
 * has no Lab representation and isn't modeled here. This module only
 * covers the two monster-side outcomes.
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
 * -- must match `SecondaryAttackCatalog.ForMonster`'s new case. */
const ELECTRIC_ARC_HAND_FAMILY = "electric_arc";
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
/** Which secondary attack a MONSTER is equipped with, derived purely
 * from its hand family -- alien-tech hands (laser/photon/plasma) get the
 * Psionic Tractor Beam (literally the same pull-and-consume mechanic Web
 * Attack uses, just a shorter-range definition); the electric_arc hand
 * gets Area Shock (its own much-longer stun); everything else --
 * including a vestigial or cut-off hand (chop-shop-safe: a stump reads
 * the same as an unarmed creature, since it's simply not in either set
 * above) -- gets the Mad Doctor's default, Ground Stomp. Mirrors
 * `SecondaryAttackCatalog.ForMonster`'s switch exactly, same cases, same
 * default. */
export function secondaryAttackFor(handFamily) {
    if (ALIEN_HAND_FAMILIES.has(handFamily))
        return PSIONIC_TRACTOR_BEAM;
    if (handFamily === ELECTRIC_ARC_HAND_FAMILY)
        return AREA_SHOCK;
    return GROUND_STOMP;
}
/** Convenience overload reading straight off a genome's hand slot. */
export function secondaryAttackForGenome(g) {
    return secondaryAttackFor(g.slots.hand.family);
}
