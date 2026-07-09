import { test } from "node:test";
import assert from "node:assert/strict";

import {
  GENOME_VERSION,
  validateGenome,
  type Genome,
  type Params6,
  type PartItem,
} from "@maddr/genome-core";

import {
  InMemoryStore,
  MutatorService,
  verifyGenome,
  type StoredGenome,
} from "../src/index.js";
import { COSTS } from "../src/economy.js";

const CFG = { signingKey: "test-sign", internalKey: "test-internal" };
const ACC = "acct-1";

function fresh() {
  const store = new InMemoryStore();
  return { store, svc: new MutatorService(store, CFG) };
}

let n = 0;
const key = () => `idem-${n++}`;

/** spawn a creature and return its id. */
function spawn(svc: MutatorService, acc = ACC): string {
  const r: any = svc.spawn(acc, key(), {});
  return r.result.genomeId as string;
}

// ---- spawn / mint / catalog --------------------------------------------------

test("spawn mints a valid, signed creature and discovers its families", () => {
  const { svc } = fresh();
  const r: any = svc.spawn(ACC, key(), {});
  assert.equal(r.status, "completed");
  const stored = svc.getCreature(ACC, r.result.genomeId);
  assert.ok(validateGenome(stored.genome).ok);
  assert.equal(stored.genome.creatureId, stored.id);
  assert.ok(verifyGenome(stored.genome, stored.signature, CFG.signingKey));
  assert.equal(svc.getCatalog(ACC).length, 4, "four slot families discovered");
});

// ---- the operators + lineage -------------------------------------------------

test("mutate produces a child whose parentIds point at the parent", () => {
  const { svc } = fresh();
  const parent = spawn(svc);
  const r: any = svc.mutate(ACC, key(), { parentId: parent });
  const child = svc.getCreature(ACC, r.result.genomeId);
  assert.deepEqual(child.genome.parentIds, [parent]);
});

test("splice records both parents; lineage walks the whole tree", () => {
  const { svc } = fresh();
  const a = spawn(svc);
  const b = spawn(svc);
  const r: any = svc.splice(ACC, key(), { parentAId: a, parentBId: b });
  const childId = r.result.genomeId;
  const child = svc.getCreature(ACC, childId);
  assert.deepEqual([...child.genome.parentIds].sort(), [a, b].sort());

  const lineageIds = svc.lineage(ACC, childId).map((g) => g.id).sort();
  assert.deepEqual(lineageIds, [childId, a, b].sort());
});

test("graft sets a slot deterministically and rejects a homolog violation", () => {
  const { svc } = fresh();
  const parent = spawn(svc);
  const params: Params6 = [0.4, 0.4, 0.4, 0.4, 0.4, 0.4];
  const r: any = svc.graft(ACC, key(), { parentId: parent, slot: "hand", family: "pincer", params });
  assert.equal(svc.getCreature(ACC, r.result.genomeId).genome.slots.hand.family, "pincer");
  assert.throws(
    () => svc.graft(ACC, key(), { parentId: parent, slot: "eye", family: "pincer", params }),
    /does not fit/,
  );
});

// ---- idempotency + economy ---------------------------------------------------

test("a replayed idempotency key returns the same result and charges once", () => {
  const { svc } = fresh();
  const parent = spawn(svc);
  const before = svc.getWallet(ACC).blood;
  const k = key();
  const r1: any = svc.mutate(ACC, k, { parentId: parent });
  const r2: any = svc.mutate(ACC, k, { parentId: parent });
  assert.equal(r1.result.genomeId, r2.result.genomeId, "same genome, not a sibling");
  assert.equal(svc.getWallet(ACC).blood, before - COSTS.mutate.blood, "charged exactly once");
});

test("running out of components is rejected with a 402", () => {
  const { store, svc } = fresh();
  const parent = spawn(svc);
  store.saveWallet({ accountId: ACC, blood: 0, bones: 0 });
  assert.throws(() => svc.mutate(ACC, key(), { parentId: parent }), (e: any) => e.status === 402);
});

// ---- ownership ---------------------------------------------------------------

test("you cannot touch another account's creature", () => {
  const { svc } = fresh();
  const mine = spawn(svc, ACC);
  assert.throws(() => svc.getCreature("acct-2", mine), (e: any) => e.status === 403);
  assert.throws(
    () => svc.mutate("acct-2", key(), { parentId: mine }),
    (e: any) => e.status === 403,
  );
});

// ---- surgery plumbing --------------------------------------------------------

test("harvest stumps the donor and drops a usable part in the tray", () => {
  const { svc } = fresh();
  const donor = spawn(svc);
  const slotFamilyBefore = svc.getCreature(ACC, donor).genome.slots.hand.family;
  const r: any = svc.harvestPart(ACC, key(), { creatureId: donor, slot: "hand" });

  const stumped = svc.getCreature(ACC, r.result.genomeId);
  assert.equal(stumped.genome.slots.hand.family, "hand_stump");
  assert.notEqual(slotFamilyBefore, "hand_stump");
  const tray = svc.listTray(ACC);
  assert.equal(tray.length, 1);
  assert.equal((tray[0]!.item as PartItem).family, slotFamilyBefore);
});

test("sewing a part the heart affords consumes the tray item and mints a creature", () => {
  const { store, svc } = fresh();
  // a big-hearted host so any part is afforded
  const host = seedCreature(store, bigHeart());
  const itemId = seedPart(store, HEAVY_CLAW);

  const r: any = svc.sewPart(ACC, key(), { creatureId: host, slot: "hand", itemId });
  assert.equal(r.result.result, "survived");
  assert.equal(svc.getCreature(ACC, r.result.genomeId).genome.slots.hand.family, "claw_hand");
  assert.equal(svc.listTray(ACC).length, 0, "consumed");
});

test("grafting onto a slot that already has a part swaps it into the tray instead of losing it", () => {
  const { store, svc } = fresh();
  const host = seedCreature(store, bigHeart(), { hand: { family: "pincer", params: [0.2, 0.2, 0.2, 0.2, 0.2, 0.2] } });
  const itemId = seedPart(store, HEAVY_CLAW);

  const r: any = svc.sewPart(ACC, key(), { creatureId: host, slot: "hand", itemId });
  assert.equal(r.result.result, "survived");
  assert.equal(svc.getCreature(ACC, r.result.genomeId).genome.slots.hand.family, "claw_hand");

  const tray = svc.listTray(ACC);
  assert.equal(tray.length, 1, "the old pincer lands back in the tray, not lost");
  assert.equal((tray[0]!.item as PartItem).family, "pincer");
  assert.equal(r.result.explantedPartItemId, tray[0]!.itemId);
});

test("a rejected graft keeps the part in the tray and refunds most of the fee", () => {
  const { store, svc } = fresh();
  const host = seedCreature(store, faintHeart()); // smallest heart
  const itemId = seedPart(store, HEAVY_CLAW);
  const before = svc.getWallet(ACC).blood;

  const r: any = svc.sewPart(ACC, key(), { creatureId: host, slot: "hand", itemId });
  assert.equal(r.result.result, "limb_rejected");
  assert.equal(r.result.genomeId, undefined, "no new creature minted");
  assert.equal(svc.listTray(ACC).length, 1, "part survives, still in the tray");
  const spent = before - svc.getWallet(ACC).blood;
  assert.ok(spent < COSTS.surgery.blood, "fee mostly refunded");
});

test("heart transplant: a bigger heart is consumed and the old one returns to the tray", () => {
  const { store, svc } = fresh();
  // a strained patient (heavy arm on the smallest heart)
  const patient = seedCreature(store, faintHeart(), {
    hand: { family: "claw_hand", params: HEAVY_CLAW.params },
  });
  const bigHeartItem = seedHeart(store);

  const r: any = svc.sewHeart(ACC, key(), { creatureId: patient, itemId: bigHeartItem });
  assert.equal(r.result.result, "survived");
  assert.equal(svc.getCreature(ACC, r.result.genomeId).genome.heart.tier, "titan");
  const tray = svc.listTray(ACC);
  assert.equal(tray.length, 1, "the old heart popped out");
  assert.equal((tray[0]!.item as any).tier, "faint");
});

// ---- menagerie + roster ------------------------------------------------------

test("menagerie enforces ownership, the 12 cap, and no duplicates", () => {
  const { svc } = fresh();
  const a = spawn(svc);
  const b = spawn(svc);
  const m = svc.setMenagerie(ACC, [a, b]);
  assert.deepEqual(m.creatureIds, [a, b]);
  assert.throws(() => svc.setMenagerie(ACC, [a, a]), /duplicate/);
  assert.throws(() => svc.setMenagerie(ACC, new Array(13).fill(a)), /at most/);
});

test("roster is internal-key gated and returns verifiable signatures", () => {
  const { svc } = fresh();
  const a = spawn(svc);
  svc.setMenagerie(ACC, [a]);
  assert.throws(() => svc.roster("wrong-key", ACC), (e: any) => e.status === 403);
  const roster = svc.roster(CFG.internalKey, ACC);
  assert.equal(roster.length, 1);
  assert.ok(verifyGenome(roster[0]!.genome, roster[0]!.signature, CFG.signingKey));
});

// ---- helpers: seed the store with controlled creatures -----------------------

const HEAVY_CLAW: PartItem = { kind: "part", family: "claw_hand", params: [1, 1, 0.5, 0.5, 0.5, 0.5], hue: 0.5 };

const STUMPS = {
  hand: { family: "hand_stump", params: z() },
  sensor: { family: "sensor_stub", params: z() },
  eye: { family: "eye_socket", params: z() },
  leg: { family: "leg_stump", params: z() },
};
function z(): Params6 {
  return [0, 0, 0, 0, 0, 0];
}
function faintHeart(): Genome["heart"] {
  return { tier: "faint", params: [0, 0, 0, 0, 0, 0] };
}
function bigHeart(): Genome["heart"] {
  return { tier: "titan", params: [1, 0, 0, 0, 0, 0] };
}

function makeGenome(heart: Genome["heart"], slots: Partial<typeof STUMPS> = {}): Genome {
  return {
    genomeVersion: GENOME_VERSION,
    parentIds: [],
    body: { plan: "tetrapod", params: [0, 0, 0, 0] },
    brain: { tier: "dim", params: [0, 0, 0, 0, 0] },
    heart,
    slots: { ...STUMPS, ...slots },
  };
}

function seedCreature(store: InMemoryStore, heart: Genome["heart"], slots: Partial<typeof STUMPS> = {}): string {
  const id = `cr-seed-${n++}`;
  const genome = { ...makeGenome(heart, slots), creatureId: id };
  const stored: StoredGenome = { id, accountId: ACC, genome, signature: "seed", createdAt: new Date().toISOString() };
  store.putGenome(stored);
  return id;
}
function seedPart(store: InMemoryStore, part: PartItem): string {
  const itemId = `item-seed-${n++}`;
  store.addItem({ itemId, accountId: ACC, item: part });
  return itemId;
}
function seedHeart(store: InMemoryStore): string {
  const itemId = `heart-seed-${n++}`;
  store.addItem({ itemId, accountId: ACC, item: { kind: "heart", tier: "titan", params: [1, 0, 0, 0, 0, 0] } });
  return itemId;
}
