import { test } from "node:test";
import assert from "node:assert/strict";

import { Rng, bonesCost, randomGenome, type Genome, type Params6 } from "../src/index.js";

const P6 = (x: number): Params6 => [x, x, x, x, x, x];

function withBody(g: Genome, bulk: number): Genome {
  return { ...g, body: { ...g.body, params: [g.body.params[0], bulk, g.body.params[2], g.body.params[3]] } };
}

function withHeartTier(g: Genome, tier: Genome["heart"]["tier"]): Genome {
  return { ...g, heart: { ...g.heart, tier } };
}

function withUniformLimbs(g: Genome, lengthGirth: number): Genome {
  const slots = { ...g.slots };
  for (const slot of Object.keys(slots) as (keyof typeof slots)[]) {
    slots[slot] = { ...slots[slot], params: P6(lengthGirth) };
  }
  return { ...g, slots };
}

test("bonesCost grows with bulk, heart tier, and limb mass", () => {
  const base = randomGenome(new Rng(1));

  const small = withUniformLimbs(withHeartTier(withBody(base, 0), "faint"), 0);
  const big = withUniformLimbs(withHeartTier(withBody(base, 1), "titan"), 1);

  assert.ok(bonesCost(big) > bonesCost(small));
  assert.equal(bonesCost(small), 0); // fully degenerate: zero bulk, faint heart, zero limbs
});

test("bonesCost is deterministic and pure", () => {
  const g = randomGenome(new Rng(7));
  const a = bonesCost(g);
  const b = bonesCost(g);
  assert.equal(a, b);
});

test("bonesCost matches the hand-computed formula on a fixed genome", () => {
  const base = randomGenome(new Rng(3));
  const g = withUniformLimbs(withHeartTier(withBody(base, 0.5), "strong"), 0.5);
  // 20*0.5 (bulk) + 8*2 (strong = index 2) + 6*(4 slots * 0.5*0.5) = 10 + 16 + 6 = 32
  assert.equal(bonesCost(g), 32);
});
