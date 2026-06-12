import { test } from "node:test";
import assert from "node:assert/strict";

import {
  BRAIN_TIERS,
  PART_AXES,
  SLOT_NAMES,
  Rng,
  berserkArmorBonus,
  berserkPowerMult,
  berserkThreshold,
  brainPowerBudget,
  capacity,
  controlCost,
  controlRadius,
  express,
  FAMILIES,
  familiesInClass,
  fromJson,
  graft,
  GenomeParseError,
  homologOf,
  mutate,
  originOf,
  randomGenome,
  splice,
  toJson,
  validateGenome,
  type Genome,
  type BrainGenes,
  type Params6,
} from "../src/index.js";

const P6 = (x: number): Params6 => [x, x, x, x, x, x];

// ---- closure & bounds --------------------------------------------------------

test("operators are closed: 400 bred genomes all validate, in range", () => {
  const rng = new Rng(7);
  const pop: Genome[] = [];
  for (let i = 0; i < 40; i++) pop.push(randomGenome(rng));
  for (let i = 0; i < 360; i++) {
    const g = rng.bool()
      ? splice(rng.choice(pop), rng.choice(pop), rng)
      : mutate(rng.choice(pop), rng, { biasSlot: rng.bool() ? "hand" : undefined });
    const r = validateGenome(g);
    assert.ok(r.ok, r.errors.join("; "));
    pop.push(g);
  }
});

// ---- homolog grammar -----------------------------------------------------------

test("family jumps never leave the homolog class, even at jump rate 0.9", () => {
  const rng = new Rng(11);
  let g = randomGenome(rng);
  for (let i = 0; i < 500; i++) {
    g = mutate(g, rng, { familyJump: 0.9 });
    for (const slot of SLOT_NAMES) {
      assert.equal(homologOf(g.slots[slot].family), slot);
    }
  }
});

test("graft enforces the grammar and rejects a hand part in the eye slot", () => {
  const rng = new Rng(11);
  const g = randomGenome(rng);
  assert.throws(() => graft(g, "eye", "claw_hand", P6(0.5)), /homolog grammar/);
});

// ---- determinism ---------------------------------------------------------------

function lineage(seed: number): string {
  const rng = new Rng(seed);
  let g = randomGenome(rng);
  for (let i = 0; i < 20; i++) g = mutate(g, rng);
  return toJson(g);
}

test("same seed, same monster; different seed, different monster", () => {
  assert.equal(lineage(42), lineage(42));
  assert.notEqual(lineage(42), lineage(43));
});

test("golden snapshot: the canonical RNG never silently changes", () => {
  // If this test breaks, the RNG or operator order changed: that is a
  // BREAKING change for replayability/auditability (docs/07) and must be
  // a deliberate, versioned decision.
  const rng = new Rng(2026);
  const r = [rng.next(), rng.next(), rng.int(1000), rng.gauss(0, 1)];
  const got = r.map((x) => x.toFixed(12)).join(",");
  const g = lineage(2026);
  assert.equal(got.length > 0, true);
  // pin a stable digest of the lineage rather than the full JSON
  let h = 0x811c9dc5;
  for (let i = 0; i < g.length; i++) {
    h ^= g.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  const digest = (h >>> 0).toString(16);
  const expected = process.env.UPDATE_GOLDEN ? digest : readGolden();
  if (process.env.UPDATE_GOLDEN) {
    writeGolden(`${got}\n${digest}\n`);
  } else {
    const [goldRng, goldDigest] = expected.split("\n");
    assert.equal(got, goldRng, "RNG stream changed");
    assert.equal(digest, goldDigest, "operator/lineage output changed");
  }
});

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const goldenPath = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "tests",
  "golden.txt",
);
function readGolden(): string {
  return readFileSync(goldenPath, "utf8").trim();
}
function writeGolden(s: string): void {
  writeFileSync(goldenPath, s);
}

// ---- canalization ---------------------------------------------------------------

test("canalized expression maps the full genotype onto authored bounds", () => {
  for (const [fam, spec] of Object.entries(FAMILIES)) {
    for (const axis of PART_AXES) {
      const [lo, hi] = spec.bounds[axis];
      assert.ok(Math.abs(express(fam, axis, 0) - lo) < 1e-9);
      assert.ok(Math.abs(express(fam, axis, 1) - hi) < 1e-9);
    }
  }
});

// ---- shared-axis inheritance -----------------------------------------------------

test("splice children land between parents on every part axis", () => {
  const rng = new Rng(3);
  const base = randomGenome(rng);
  const a: Genome = { ...base, slots: { ...base.slots, hand: { family: "tentacle", params: [0.95, 0.05, 0.9, 0.9, 0.5, 0.2] } } };
  const b: Genome = { ...base, slots: { ...base.slots, hand: { family: "claw_hand", params: [0.1, 0.9, 0.2, 0.2, 0.8, 0.7] } } };
  let clawChildren = 0;
  for (let i = 0; i < 200; i++) {
    const child = splice(a, b, rng).slots.hand;
    for (let k = 0; k < 6; k++) {
      const lo = Math.min(a.slots.hand.params[k]!, b.slots.hand.params[k]!) - 0.16;
      const hi = Math.max(a.slots.hand.params[k]!, b.slots.hand.params[k]!) + 0.16;
      assert.ok(child.params[k]! >= lo && child.params[k]! <= hi);
    }
    if (child.family === "claw_hand") clawChildren++;
  }
  assert.ok(clawChildren > 60 && clawChildren < 140, `family split ${clawChildren}/200`);
});

// ---- origins: tech inert, biotech breeds ------------------------------------------

test("issued tech never mutates, never jumps, never blends", () => {
  const rng = new Rng(13);
  const base = randomGenome(rng);
  const rifle = { family: "rifle_arm", params: P6(0.5) };
  let g: Genome = { ...base, slots: { ...base.slots, hand: rifle } };
  for (let i = 0; i < 50; i++) {
    g = mutate(g, rng, { rate: 0.9, familyJump: 0.9, biasSlot: "hand" });
  }
  assert.deepEqual(g.slots.hand, rifle);

  const other: Genome = { ...base, slots: { ...base.slots, hand: rifle } };
  for (let i = 0; i < 20; i++) {
    const child = splice(g, other, rng);
    assert.deepEqual(child.slots.hand, rifle);
  }
});

test("biotech breeds like flesh but stays biotech on jumps", () => {
  const rng = new Rng(14);
  const base = randomGenome(rng);
  const lance = { family: "plasma_lance", params: P6(0.5) };
  let g: Genome = { ...base, slots: { ...base.slots, hand: lance } };
  for (let i = 0; i < 50; i++) g = mutate(g, rng, { rate: 0.9, biasSlot: "hand" });
  assert.notDeepEqual(g.slots.hand.params, lance.params, "biotech failed to drift");
  assert.equal(originOf(g.slots.hand.family), "biotech");
});

test("random generation defaults to organic families only", () => {
  const rng = new Rng(15);
  for (let i = 0; i < 50; i++) {
    const g = randomGenome(rng);
    for (const slot of SLOT_NAMES) {
      assert.equal(originOf(g.slots[slot].family), "organic");
    }
  }
  assert.ok(familiesInClass("hand", ["tech"]).includes("rifle_arm"));
});

// ---- behavior expression -----------------------------------------------------------

const brain = (
  tier: (typeof BRAIN_TIERS)[number],
  command: number,
  will: number,
  temperament: number,
  guile: number,
  fury: number,
): BrainGenes => ({ tier, params: [command, will, temperament, guile, fury] });

test("behavior expression is monotonic in the driving genes", () => {
  assert.ok(capacity(brain("mastermind", 0.9, 0, 0, 0, 0)) > capacity(brain("dim", 0.9, 0, 0, 0, 0)));
  assert.ok(capacity(brain("dim", 0.9, 0, 0, 0, 0)) > capacity(brain("dim", 0.1, 0, 0, 0, 0)));
  assert.ok(controlCost(brain("dim", 0, 0.9, 0, 0, 0)) > controlCost(brain("dim", 0, 0.1, 0, 0, 0)));
  assert.ok(controlRadius(brain("dim", 0.9, 0, 0, 0, 0)) > controlRadius(brain("dim", 0.1, 0, 0, 0, 0)));
  assert.ok(berserkPowerMult(brain("dim", 0, 0, 0, 0, 0.9)) > berserkPowerMult(brain("dim", 0, 0, 0, 0, 0.1)));
  assert.ok(berserkArmorBonus(brain("dim", 0, 0, 0, 0, 0.9)) > berserkArmorBonus(brain("dim", 0, 0, 0, 0, 0.1)));
  assert.ok(berserkThreshold(brain("dim", 0, 0, 0, 0, 0.9)) < berserkThreshold(brain("dim", 0, 0, 0, 0, 0.1)));
});

test("low fury can never berserk: threshold above the rage cap", () => {
  assert.ok(berserkThreshold(brain("dim", 0, 0, 0, 0, 0.0)) > 1.0);
  assert.ok(berserkThreshold(brain("dim", 0, 0, 0, 0, 0.05)) > 1.0);
});

test("fury discounts the power budget (cheap power with a blast radius)", () => {
  const calm = brainPowerBudget(brain("average", 0.5, 0.5, 0.5, 0.5, 0.0));
  const furious = brainPowerBudget(brain("average", 0.5, 0.5, 0.5, 0.5, 1.0));
  assert.ok(furious < calm);
});

// ---- serialization ------------------------------------------------------------------

test("toJson/fromJson round-trips and is canonical", () => {
  const rng = new Rng(21);
  for (let i = 0; i < 25; i++) {
    const g = randomGenome(rng);
    const json = toJson(g);
    const back = fromJson(json);
    assert.equal(toJson(back), json);
  }
});

test("fromJson rejects garbage, wrong versions, and grammar violations", () => {
  assert.throws(() => fromJson("not json"), GenomeParseError);
  assert.throws(() => fromJson(JSON.stringify({ genomeVersion: 1 })), /genomeVersion/);
  const rng = new Rng(22);
  const g = randomGenome(rng);
  const evil = JSON.parse(toJson(g));
  evil.slots.eye = { family: "claw_hand", params: [0.5, 0.5, 0.5, 0.5, 0.5, 0.5] };
  assert.throws(() => fromJson(JSON.stringify(evil)), GenomeParseError);
  const outOfRange = JSON.parse(toJson(g));
  outOfRange.slots.hand.params[0] = 1.5;
  assert.throws(() => fromJson(JSON.stringify(outOfRange)), GenomeParseError);
});

// ---- lineage -------------------------------------------------------------------------

test("operators record parent lineage from creatureIds", () => {
  const rng = new Rng(23);
  const a: Genome = { ...randomGenome(rng), creatureId: "id-a" };
  const b: Genome = { ...randomGenome(rng), creatureId: "id-b" };
  assert.deepEqual(mutate(a, rng).parentIds, ["id-a"]);
  assert.deepEqual([...splice(a, b, rng).parentIds].sort(), ["id-a", "id-b"]);
  assert.deepEqual(graft(a, "hand", "pincer", P6(0.4)).parentIds, ["id-a"]);
  assert.deepEqual(mutate(randomGenome(rng), rng).parentIds, []);
});
