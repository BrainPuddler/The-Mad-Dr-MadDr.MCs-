import { test } from "node:test";
import assert from "node:assert/strict";

import {
  GENOME_VERSION,
  Rng,
  circulatoryLoad,
  harvestHeart,
  harvestPart,
  randomGenome,
  sewHeart,
  sewPart,
  validateGenome,
  viability,
  type Genome,
  type HeartTier,
  type Params6,
  type PartItem,
  type SlotName,
} from "../src/index.js";

// A heavy organic arm: full length/girth -> max mass -> max upkeep.
const HEAVY_CLAW: PartItem = {
  kind: "part",
  family: "claw_hand",
  params: [1, 1, 0.5, 0.5, 0.5, 0.5],
  hue: 0.5,
};

const STUMPS = {
  hand: { family: "hand_stump", params: [0, 0, 0, 0, 0, 0] as Params6 },
  sensor: { family: "sensor_stub", params: [0, 0, 0, 0, 0, 0] as Params6 },
  eye: { family: "eye_socket", params: [0, 0, 0, 0, 0, 0] as Params6 },
  leg: { family: "leg_stump", params: [0, 0, 0, 0, 0, 0] as Params6 },
};

function make(opts: {
  heart: HeartTier;
  vigor?: number;
  bulk?: number;
  slots?: Partial<Record<SlotName, { family: string; params: Params6 }>>;
}): Genome {
  return {
    genomeVersion: GENOME_VERSION,
    parentIds: [],
    body: { plan: "tetrapod", params: [0, opts.bulk ?? 0, 0, 0] },
    brain: { tier: "dim", params: [0, 0, 0, 0, 0] },
    heart: { tier: opts.heart, params: [opts.vigor ?? 0.5, 0, 0, 0, 0, 0] },
    slots: { ...STUMPS, ...(opts.slots ?? {}) },
  };
}

// ---- harvest -----------------------------------------------------------------

test("harvesting a part leaves a stump and yields a usable, identical part", () => {
  const donorStart = make({ heart: "titan", vigor: 1, slots: { hand: { family: "claw_hand", params: HEAVY_CLAW.params } } });
  const before = circulatoryLoad(donorStart);
  const { donor, part } = harvestPart(donorStart, "hand");

  assert.equal(donor.slots.hand.family, "hand_stump");
  assert.ok(validateGenome(donor).ok);
  assert.ok(circulatoryLoad(donor) < before, "a stumped creature is lighter");

  assert.equal(part.kind, "part");
  assert.equal(part.family, "claw_hand");
  assert.deepEqual(part.params, HEAVY_CLAW.params);

  // re-sew the harvested part into a big-hearted host: it expresses again
  const host = make({ heart: "titan", vigor: 1 });
  const r = sewPart(host, "hand", part);
  assert.equal(r.result, "survived");
  assert.equal(r.patient.slots.hand.family, "claw_hand");
});

// ---- sewing: the three outcomes ----------------------------------------------

test("a part the heart can afford is sewn on and the creature lives", () => {
  const host = make({ heart: "titan", vigor: 1 });
  const r = sewPart(host, "hand", HEAVY_CLAW);
  assert.equal(r.result, "survived");
  assert.equal(r.alive, true);
  assert.equal(r.patient.slots.hand.family, "claw_hand");
  assert.equal(r.returnedPart, undefined, "a consumed part is not handed back");
  assert.equal(viability(r.patient).state, "viable");
});

test("grafting onto a slot that already has a real part swaps it out instead of destroying it", () => {
  const host = make({
    heart: "titan", vigor: 1,
    slots: { hand: { family: "pincer", params: [0.2, 0.2, 0.2, 0.2, 0.2, 0.2] } },
  });
  const r = sewPart(host, "hand", HEAVY_CLAW);
  assert.equal(r.result, "survived");
  assert.equal(r.patient.slots.hand.family, "claw_hand", "the new part takes the slot");
  assert.ok(r.explantedPart, "the old part comes off instead of vanishing");
  assert.equal(r.explantedPart!.family, "pincer");
  assert.deepEqual(r.explantedPart!.params, [0.2, 0.2, 0.2, 0.2, 0.2, 0.2]);

  // and the swapped-out part is still perfectly usable elsewhere
  const host2 = make({ heart: "titan", vigor: 1 });
  assert.equal(sewPart(host2, "hand", r.explantedPart!).result, "survived");
});

test("grafting onto a bare stump has nothing to explant", () => {
  const host = make({ heart: "titan", vigor: 1 });   // hand is a stump by default
  const r = sewPart(host, "hand", HEAVY_CLAW);
  assert.equal(r.result, "survived");
  assert.equal(r.explantedPart, undefined, "a stump had nothing worth saving");
});

test("a part keeps its own hue through harvest and graft instead of taking the recipient's", () => {
  const donor = make({
    heart: "titan", vigor: 1,
    slots: { hand: { family: "claw_hand", params: HEAVY_CLAW.params } },
  });
  const donorHued: Genome = { ...donor, body: { ...donor.body, params: [0.85, 0, 0, 0] } };
  const { part } = harvestPart(donorHued, "hand");
  assert.equal(part.hue, 0.85, "harvest captures the donor's own hue");

  const recipient = make({ heart: "titan", vigor: 1 });
  const recipientHued: Genome = { ...recipient, body: { ...recipient.body, params: [0.1, 0, 0, 0] } };
  const r = sewPart(recipientHued, "hand", part);
  assert.equal(r.result, "survived");
  assert.equal(r.patient.slots.hand.hue, 0.85, "the graft keeps the donor's color, not the recipient's");
});

test("a part the heart can't quite afford is rejected; the creature lives, the part survives", () => {
  const host = make({ heart: "faint", vigor: 0 }); // smallest possible heart
  assert.equal(viability(host).state, "viable");
  const r = sewPart(host, "hand", HEAVY_CLAW);

  assert.equal(r.result, "limb_rejected");
  assert.equal(r.alive, true);
  assert.equal(viability(r.patient).state, "viable", "the limb came back off");
  assert.equal(r.patient.slots.hand.family, "hand_stump", "slot returns to its prior state");
  assert.ok(r.returnedPart, "the rejected limb is handed back");
  assert.deepEqual(r.returnedPart!.params, HEAVY_CLAW.params);

  // and that handed-back part is still perfectly usable elsewhere
  const host2 = make({ heart: "titan", vigor: 1 });
  assert.equal(sewPart(host2, "hand", r.returnedPart!).result, "survived");
});

test("sewing onto an already-strained body kills it on the table; the part survives", () => {
  // a creature already over capacity but alive (strained)
  const strained = make({ heart: "faint", vigor: 0, slots: { hand: { family: "claw_hand", params: HEAVY_CLAW.params } } });
  assert.equal(viability(strained).state, "strained");

  const r = sewPart(strained, "leg", { kind: "part", family: "hoofed_leg", params: [1, 1, 0.5, 0.5, 0.5, 0.5], hue: 0.5 });
  assert.equal(r.result, "patient_died");
  assert.equal(r.alive, false);
  assert.ok(r.returnedPart, "even from a corpse, the part is recovered");

  // recovered from the dead, the part still works
  const host = make({ heart: "titan", vigor: 1 });
  assert.equal(sewPart(host, "leg", r.returnedPart!).result, "survived");
});

test("sewing respects the homolog grammar: no arm in an eye socket", () => {
  const host = make({ heart: "titan", vigor: 1 });
  assert.throws(() => sewPart(host, "eye", HEAVY_CLAW), /homolog grammar/);
});

// ---- heart transplant: the counterplay ---------------------------------------

test("a bigger heart rescues a strained body and hands back the old heart", () => {
  const strained = make({ heart: "faint", vigor: 0, slots: { hand: { family: "claw_hand", params: HEAVY_CLAW.params } } });
  assert.equal(viability(strained).state, "strained");

  const { heart: bigHeart } = harvestHeart(make({ heart: "titan", vigor: 1 }));
  const r = sewHeart(strained, bigHeart);

  assert.equal(r.result, "survived");
  assert.equal(viability(r.patient).state, "viable", "now the body is well-supplied");
  assert.equal(r.patient.heart.tier, "titan");
  assert.ok(r.explantedHeart, "the old heart comes back out");
  assert.equal(r.explantedHeart!.tier, "faint");
});

test("a heart too small for the body stops on the table; the donor heart survives", () => {
  const heavy = make({
    heart: "titan",
    vigor: 1,
    bulk: 1,
    slots: {
      hand: { family: "claw_hand", params: HEAVY_CLAW.params },
      leg: { family: "hoofed_leg", params: [1, 1, 0.5, 0.5, 0.5, 0.5] },
    },
  });
  assert.equal(viability(heavy).state, "viable");

  const { heart: tinyHeart } = harvestHeart(make({ heart: "faint", vigor: 0 }));
  const r = sewHeart(heavy, tinyHeart);

  assert.equal(r.result, "patient_died");
  assert.equal(r.alive, false);
  assert.ok(r.returnedHeart, "the donor heart is recovered");
});

test("harvesting a heart leaves a non-viable donor (a corpse to strip)", () => {
  const live = make({
    heart: "strong",
    vigor: 1,
    bulk: 1,
    slots: {
      hand: { family: "claw_hand", params: HEAVY_CLAW.params },
      leg: { family: "hoofed_leg", params: [1, 1, 0.5, 0.5, 0.5, 0.5] },
    },
  });
  assert.notEqual(viability(live).state, "nonviable");
  const { donor, heart } = harvestHeart(live);
  assert.equal(heart.tier, "strong");
  assert.equal(viability(donor).state, "nonviable", "no heart, no life");
  assert.ok(validateGenome(donor).ok, "a heartless body is still a structurally valid corpse");
});

// ---- primordial creatures are born viable ------------------------------------

test("randomGenome fits a heart big enough to run the body", () => {
  const rng = new Rng(99);
  for (let i = 0; i < 60; i++) {
    assert.equal(viability(randomGenome(rng)).state, "viable");
  }
});
