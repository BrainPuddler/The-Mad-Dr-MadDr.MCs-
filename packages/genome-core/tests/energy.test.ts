import { test } from "node:test";
import assert from "node:assert/strict";

import {
  Rng,
  randomGenome,
  reanimationSurge,
  upkeep,
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

test("an all-organic monster drinks blood only", () => {
  const rng = new Rng(31);
  for (let i = 0; i < 30; i++) {
    const u = upkeep(randomGenome(rng)); // random generation is organic-only
    assert.ok(u.blood > 0);
    assert.equal(u.fuel, 0);
    assert.equal(u.ichor, 0);
  }
});

test("a human trooper burns fuel for its equipment and blood for its body", () => {
  const rng = new Rng(32);
  const trooper = withSlots(randomGenome(rng, { plan: "tetrapod" }), {
    hand: { family: "rifle_arm", params: P6(0.5) },
    sensor: { family: "sensor_mast", params: P6(0.5) },
    eye: { family: "optic_visor", params: P6(0.5) },
    leg: { family: "piston_leg", params: P6(0.5) },
  });
  const u = upkeep(trooper);
  assert.ok(u.blood > 0, "even a rifleman must eat");
  assert.ok(u.fuel > 0, "machines burn fuel");
  assert.equal(u.ichor, 0);
});

test("an alien drone drinks blood AND ichor (flesh plus grown machines)", () => {
  const rng = new Rng(33);
  const drone = withSlots(randomGenome(rng, { plan: "tetrapod" }), {
    hand: { family: "plasma_lance", params: P6(0.5) },
    leg: { family: "insect_leg", params: P6(0.5) },
  });
  const u = upkeep(drone);
  assert.ok(u.blood > 0);
  assert.ok(u.ichor > 0, "biotech drinks alien fuel");
  assert.equal(u.fuel, 0);
});

test("a single tech graft adds a fuel line to a monster's bill", () => {
  const rng = new Rng(34);
  // Pin a leg-expressing plan: this test is about the graft's fuel line,
  // not about random plan selection, and must stay correct regardless of
  // which (leg-ignoring or not) plans exist in the catalog.
  const monster = randomGenome(rng, { plan: "tetrapod" });
  assert.equal(upkeep(monster).fuel, 0);
  const grafted = withSlots(monster, { leg: { family: "piston_leg", params: P6(0.5) } });
  assert.ok(upkeep(grafted).fuel > 0);
});

test("upkeep is monotonic in mass: bigger bodies and parts cost more", () => {
  const rng = new Rng(35);
  const g = randomGenome(rng);
  const small: Genome = {
    ...withSlots(g, { hand: { family: "claw_hand", params: P6(0.1) } }),
    body: { ...g.body, params: [g.body.params[0], 0.1, g.body.params[2], g.body.params[3]] },
    brain: { ...g.brain, tier: "dim" },
  };
  const large: Genome = {
    ...withSlots(g, { hand: { family: "claw_hand", params: P6(0.9) } }),
    body: { ...g.body, params: [g.body.params[0], 0.9, g.body.params[2], g.body.params[3]] },
    brain: { ...g.brain, tier: "mastermind" },
  };
  assert.ok(upkeep(large).blood > upkeep(small).blood);
  assert.ok(reanimationSurge(large) > reanimationSurge(small));
});

test("ignored slots cost nothing: silent genes are free to carry", () => {
  const rng = new Rng(36);
  const g = randomGenome(rng, { plan: "tetrapod" });
  const legged = upkeep(g);
  const serpent: Genome = { ...g, body: { ...g.body, plan: "serpentine" } };
  const slithering = upkeep(serpent);
  // same genome, but serpentine ignores the leg slot: strictly cheaper
  assert.ok(
    slithering.blood + slithering.fuel + slithering.ichor <
      legged.blood + legged.fuel + legged.ichor,
  );
});
