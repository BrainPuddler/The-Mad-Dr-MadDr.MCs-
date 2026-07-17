import { test } from "node:test";
import assert from "node:assert/strict";

import {
  HARVEST_TOOLS,
  Rng,
  STORAGE_FAMILIES,
  express,
  flightSpeedFactor,
  groundSpeedFactor,
  harvestProfile,
  isKnownFamily,
  homologOf,
  originOf,
  randomGenome,
  type Genome,
  type Params6,
} from "../src/index.js";

const P6 = (x: number): Params6 => [x, x, x, x, x, x];

function withSlots(
  g: Genome,
  slots: Partial<Record<"hand" | "sensor" | "eye" | "leg", { family: string; params: Params6 }>>,
): Genome {
  return { ...g, slots: { ...g.slots, ...slots } };
}

test("harvest tool and storage families are real catalog citizens", () => {
  // the whole point: harvesters live in the same catalog/Hox grammar as
  // everything else, so they breed, mutate, and graft like any part
  for (const fam of ["lamprey_maw", "bone_saw", "ichor_siphon"]) {
    assert.ok(isKnownFamily(fam), `${fam} in catalog`);
    assert.equal(homologOf(fam), "hand");
  }
  for (const fam of Object.keys(STORAGE_FAMILIES)) {
    assert.ok(isKnownFamily(fam), `${fam} in catalog`);
    assert.equal(homologOf(fam), "sensor");
  }
  // one per origin on each side -- so every faction path has its option
  assert.equal(originOf("lamprey_maw"), "organic");
  assert.equal(originOf("bone_saw"), "tech");
  assert.equal(originOf("ichor_siphon"), "biotech");
  assert.equal(originOf("storage_bladder"), "organic");
  assert.equal(originOf("steel_tank"), "tech");
  assert.equal(originOf("amber_vesicle"), "biotech");
});

test("a lamprey maw drinks blood fast and from the living; a bone saw cuts bone from corpses only", () => {
  const rng = new Rng(41);
  const base = randomGenome(rng, { plan: "tetrapod" });

  const lamprey = harvestProfile(withSlots(base, { hand: { family: "lamprey_maw", params: P6(0.5) } }));
  const saw = harvestProfile(withSlots(base, { hand: { family: "bone_saw", params: P6(0.5) } }));
  const claws = harvestProfile(withSlots(base, { hand: { family: "claw_hand", params: P6(0.5) } }));

  assert.ok(lamprey.drainsLiving, "suction pulls blood from units alive or dead");
  assert.ok(!saw.drainsLiving, "a saw is a corpse tool");
  assert.ok(lamprey.gather.blood > saw.gather.blood);
  assert.ok(saw.gather.bone > lamprey.gather.bone);
  assert.ok(lamprey.gather.blood > claws.gather.blood, "the dedicated tool beats generic claws");
});

test("speedy legs stay speedy: gather comes from the hand, not the legs", () => {
  const rng = new Rng(42);
  const base = randomGenome(rng, { plan: "tetrapod" });
  const a = harvestProfile(withSlots(base, {
    hand: { family: "lamprey_maw", params: P6(0.5) },
    leg: { family: "talon_leg", params: P6(0.9) },
  }));
  const b = harvestProfile(withSlots(base, {
    hand: { family: "lamprey_maw", params: P6(0.5) },
    leg: { family: "hoofed_leg", params: P6(0.2) },
  }));
  assert.deepEqual(a.gather, b.gather);
});

test("a bigger tool gathers faster (axes scale the rate)", () => {
  const rng = new Rng(43);
  const base = randomGenome(rng, { plan: "tetrapod" });
  const small = harvestProfile(withSlots(base, { hand: { family: "lamprey_maw", params: P6(0.1) } }));
  const large = harvestProfile(withSlots(base, { hand: { family: "lamprey_maw", params: P6(0.9) } }));
  assert.ok(large.gather.blood > small.gather.blood);
});

test("a storage vessel raises capacity; the blob plan is its own bag", () => {
  const rng = new Rng(44);
  const base = randomGenome(rng, { plan: "tetrapod" });

  const bare = harvestProfile(base);
  const tank = harvestProfile(withSlots(base, { sensor: { family: "steel_tank", params: P6(0.7) } }));
  assert.ok(!bare.hasVessel);
  assert.ok(tank.hasVessel);
  assert.ok(tank.capacity > bare.capacity, "a tank on the back carries more");

  const blobBase: Genome = { ...base, body: { ...base.body, plan: "blob" } };
  const blob = harvestProfile(blobBase);
  assert.ok(blob.capacity > bare.capacity, "blob body storage capacity");
});

test("every storage vessel origin option carries meaningfully", () => {
  const rng = new Rng(45);
  const base = randomGenome(rng, { plan: "tetrapod" });
  const bare = harvestProfile(base).capacity;
  for (const fam of Object.keys(STORAGE_FAMILIES)) {
    const p = harvestProfile(withSlots(base, { sensor: { family: fam, params: P6(0.6) } }));
    assert.ok(p.capacity > bare + 10, `${fam} adds real capacity`);
  }
});

test("flying plans are flagged, and flight pays double for weight -- floored, never grounded", () => {
  const rng = new Rng(46);
  const base = randomGenome(rng, { plan: "winged" });
  assert.ok(harvestProfile(base).flies);
  assert.ok(!harvestProfile({ ...base, body: { ...base.body, plan: "tetrapod" } }).flies);

  // empty carrier: no penalty either way
  assert.equal(groundSpeedFactor(0), 1);
  assert.equal(flightSpeedFactor(0), 1);
  // laden: flight hurts more than ground at the same fill
  assert.ok(flightSpeedFactor(0.5) < groundSpeedFactor(0.5));
  // floors (docs/22 never-annoying contract): slow, never stopped
  assert.ok(flightSpeedFactor(1) >= 0.4);
  assert.ok(groundSpeedFactor(1) >= 0.6);
  // out-of-range fills clamp instead of exploding
  assert.equal(flightSpeedFactor(2), flightSpeedFactor(1));
  assert.equal(groundSpeedFactor(-1), 1);
});

test("a stump hand gathers nothing; a toolless hand still paws slowly", () => {
  const rng = new Rng(47);
  const base = randomGenome(rng, { plan: "tetrapod" });
  const stump = harvestProfile(withSlots(base, { hand: { family: "hand_stump", params: P6(0.1) } }));
  assert.equal(stump.gather.blood, 0);
  assert.equal(stump.gather.bone, 0);
  const rifle = harvestProfile(withSlots(base, { hand: { family: "rifle_arm", params: P6(0.5) } }));
  assert.ok(rifle.gather.blood > 0, "any working hand can tear at a corpse, badly");
  assert.ok(rifle.gather.blood < 1, "...but a gun is not a harvest tool");
});

test("harvest tool gather rates cover every declared tool family", () => {
  for (const fam of Object.keys(HARVEST_TOOLS)) {
    assert.ok(isKnownFamily(fam), `${fam} is a real family`);
    assert.equal(homologOf(fam), "hand", `${fam} is a hand tool`);
  }
});

test("storage vessel bounds guarantee visible mass (canalization sanity)", () => {
  // a vessel whose girth could express to ~0 would be an invisible tank;
  // the catalog bounds keep every vessel's minimum girth substantial
  for (const fam of Object.keys(STORAGE_FAMILIES)) {
    assert.ok(express(fam, "girth", 0) >= 0.3, `${fam} min girth reads as a vessel`);
  }
});
