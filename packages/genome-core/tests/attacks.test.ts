import { test } from "node:test";
import assert from "node:assert/strict";

import {
  secondaryAttackFor,
  secondaryAttackForGenome,
  randomGenome,
  Rng,
  type Genome,
  type Params6,
} from "../src/index.js";

const P6 = (x: number): Params6 => [x, x, x, x, x, x];

function withHand(g: Genome, family: string): Genome {
  return { ...g, slots: { ...g.slots, hand: { family, params: P6(0.5) } } };
}

test("alien-tech hand families get the Psionic Tractor Beam", () => {
  for (const fam of ["laser_array", "photon_blaster", "plasma_lance"]) {
    const info = secondaryAttackFor(fam);
    assert.equal(info.kind, "psionic_tractor_beam");
    assert.equal(info.effect, "pull_and_consume");
  }
});

test("every other hand family, including unarmed, gets Ground Stomp", () => {
  for (const fam of ["claw_hand", "pincer", "tentacle", "chain_blade", "hand_stump", "rifle_arm"]) {
    const info = secondaryAttackFor(fam);
    assert.equal(info.kind, "ground_stomp");
    assert.equal(info.effect, "stun");
    assert.equal(info.stunDurationSeconds, 2);
  }
});

test("electric_arc gets Area Shock -- a much longer stun than Ground Stomp", () => {
  const info = secondaryAttackFor("electric_arc");
  assert.equal(info.kind, "area_shock");
  assert.equal(info.effect, "stun");
  assert.equal(info.stunDurationSeconds, 10);
  const groundStompStun = secondaryAttackFor("claw_hand").stunDurationSeconds!;
  assert.ok(info.stunDurationSeconds! > groundStompStun, "Area Shock should stun longer than Ground Stomp");
});

test("Ground Stomp is self-centered (range 0) -- matches the Unity twin", () => {
  assert.equal(secondaryAttackFor("claw_hand").range, 0);
});

test("both kinds carry a nonzero blood+bones cast cost (docs/22 economy, docs/26 Phase 10)", () => {
  for (const fam of ["laser_array", "claw_hand"]) {
    const info = secondaryAttackFor(fam);
    assert.ok(info.bloodCost > 0, `${fam}: bloodCost > 0`);
    assert.ok(info.bonesCost > 0, `${fam}: bonesCost > 0`);
  }
});

test("secondaryAttackForGenome reads the genome's own hand slot", () => {
  const g = randomGenome(new Rng(1));
  const direct = secondaryAttackFor(g.slots.hand.family);
  assert.deepEqual(secondaryAttackForGenome(g), direct);

  const alienGenome = withHand(g, "photon_blaster");
  assert.equal(secondaryAttackForGenome(alienGenome).kind, "psionic_tractor_beam");
});
