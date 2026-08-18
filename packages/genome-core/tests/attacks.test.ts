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

// 2026-08 (creator direction: "did that get rewritten in the lab as
// well?" -- the answer was no, this file included, until this pass):
// secondaryAttackFor/secondaryAttackForGenome now return the FULL POOL
// (an array), matching SecondaryAttackCatalog.ForMonster's real Unity
// return shape -- every test below was rewritten for that, not just
// patched at the call sites.

test("alien-tech hand families get the alien pool, led by Psionic Tractor Beam", () => {
  for (const fam of ["laser_array", "photon_blaster", "plasma_lance"]) {
    const pool = secondaryAttackFor(fam);
    assert.equal(pool.length, 5, `${fam}: alien pool has 5 abilities`);
    assert.equal(pool[0]!.kind, "psionic_tractor_beam");
    assert.equal(pool[0]!.effect, "pull_and_consume");
    assert.deepEqual(
      pool.map((a) => a.kind),
      ["psionic_tractor_beam", "psychic_pulse", "neural_disruption", "psychic_shield", "mind_control"],
    );
  }
});

test("every other hand family, including unarmed, gets the Mad Doctor default pool, led by Ground Stomp", () => {
  for (const fam of ["claw_hand", "pincer", "tentacle", "chain_blade", "hand_stump", "rifle_arm"]) {
    const pool = secondaryAttackFor(fam);
    assert.equal(pool.length, 6, `${fam}: Mad Doctor default pool has 6 abilities`);
    assert.equal(pool[0]!.kind, "ground_stomp");
    assert.equal(pool[0]!.effect, "stun");
    assert.equal(pool[0]!.stunDurationSeconds, 2);
    assert.deepEqual(
      pool.map((a) => a.kind),
      ["ground_stomp", "spore_cloud", "defensive_spore_burst", "toxic_sac", "panic_shriek", "mutagenic_pulse"],
    );
  }
});

test("electric_arc gets the electric pool, led by Area Shock -- a much longer stun than Ground Stomp", () => {
  const pool = secondaryAttackFor("electric_arc");
  assert.equal(pool.length, 5, "electric pool has 5 abilities");
  assert.equal(pool[0]!.kind, "area_shock");
  assert.equal(pool[0]!.effect, "stun");
  assert.equal(pool[0]!.stunDurationSeconds, 10);
  const groundStompStun = secondaryAttackFor("claw_hand")[0]!.stunDurationSeconds!;
  assert.ok(pool[0]!.stunDurationSeconds! > groundStompStun, "Area Shock should stun longer than Ground Stomp");
  assert.deepEqual(
    pool.map((a) => a.kind),
    ["area_shock", "emp_pulse", "discharge_burst", "arc_lance", "magnetic_tether"],
  );
});

test("each race's pool has exactly one defensive ability", () => {
  for (const fam of ["claw_hand", "laser_array", "electric_arc"]) {
    const pool = secondaryAttackFor(fam);
    const defensiveCount = pool.filter((a) => a.isDefensive === true).length;
    assert.equal(defensiveCount, 1, `${fam}: exactly one defensive ability`);
  }
});

test("Ground Stomp and every self-centered ability report range 0 -- matches the Unity twin", () => {
  const pool = secondaryAttackFor("claw_hand");
  assert.equal(pool[0]!.range, 0);   // Ground Stomp
  assert.equal(pool.find((a) => a.kind === "defensive_spore_burst")!.range, 0);
});

test("every ability in every pool carries a nonzero blood+bones cast cost (docs/22 economy, docs/26 Phase 10)", () => {
  for (const fam of ["laser_array", "claw_hand", "electric_arc"]) {
    for (const info of secondaryAttackFor(fam)) {
      assert.ok(info.bloodCost > 0, `${fam}/${info.kind}: bloodCost > 0`);
      assert.ok(info.bonesCost > 0, `${fam}/${info.kind}: bonesCost > 0`);
    }
  }
});

test("Arc Lance is the one Damage-type ability among the genome-side pools", () => {
  const pool = secondaryAttackFor("electric_arc");
  const arcLance = pool.find((a) => a.kind === "arc_lance")!;
  assert.equal(arcLance.effect, "damage");
  assert.ok(arcLance.damageAmount! > 0);
});

test("secondaryAttackForGenome reads the genome's own hand slot", () => {
  const g = randomGenome(new Rng(1));
  const direct = secondaryAttackFor(g.slots.hand.family);
  assert.deepEqual(secondaryAttackForGenome(g), direct);

  const alienGenome = withHand(g, "photon_blaster");
  assert.equal(secondaryAttackForGenome(alienGenome)[0]!.kind, "psionic_tractor_beam");
});
