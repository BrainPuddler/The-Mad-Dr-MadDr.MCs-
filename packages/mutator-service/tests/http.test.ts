import { test } from "node:test";
import assert from "node:assert/strict";
import type { AddressInfo } from "node:net";

import { createApp, InMemoryStore, MutatorService } from "../src/index.js";

const CFG = { signingKey: "http-sign", internalKey: "http-internal" };

function startServer() {
  const store = new InMemoryStore();
  const svc = new MutatorService(store, CFG);
  const app = createApp(svc);
  return new Promise<{ base: string; close: () => Promise<void> }>((resolve) => {
    app.listen(0, () => {
      const { port } = app.address() as AddressInfo;
      resolve({
        base: `http://127.0.0.1:${port}`,
        close: () => new Promise<void>((r) => app.close(() => r())),
      });
    });
  });
}

const ACC = { "content-type": "application/json", "x-account-id": "http-acct" };

test("a full lab flow over HTTP: spawn, mutate, read, wallet", async () => {
  const { base, close } = await startServer();
  try {
    const spawn = await (await fetch(`${base}/spawn`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "k1" }),
    })).json();
    assert.equal(spawn.status, "completed");
    const parentId = spawn.genomeId;
    assert.ok(parentId);

    const mut = await (await fetch(`${base}/mutate`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "k2", parentId }),
    })).json();
    assert.deepEqual(mut.genome.parentIds, [parentId]);

    const read = await fetch(`${base}/creature/${mut.genomeId}`, { headers: ACC });
    assert.equal(read.status, 200);
    assert.equal((await read.json()).id, mut.genomeId);

    const wallet = await (await fetch(`${base}/wallet`, { headers: ACC })).json();
    assert.equal(wallet.blood, 490); // 500 - mutate fee 10 (spawn is free)
  } finally {
    await close();
  }
});

test("idempotent POST over HTTP does not mint a sibling", async () => {
  const { base, close } = await startServer();
  try {
    const spawn = await (await fetch(`${base}/spawn`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "s1" }),
    })).json();
    const body = JSON.stringify({ idempotencyKey: "dup", parentId: spawn.genomeId });
    const a = await (await fetch(`${base}/mutate`, { method: "POST", headers: ACC, body })).json();
    const b = await (await fetch(`${base}/mutate`, { method: "POST", headers: ACC, body })).json();
    assert.equal(a.genomeId, b.genomeId);
  } finally {
    await close();
  }
});

test("missing auth is 401; internal roster needs the internal key", async () => {
  const { base, close } = await startServer();
  try {
    const noAuth = await fetch(`${base}/wallet`);
    assert.equal(noAuth.status, 401);

    // seed a menagerie for the account
    const spawn = await (await fetch(`${base}/spawn`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "r1" }),
    })).json();
    await fetch(`${base}/menagerie`, {
      method: "PUT", headers: ACC, body: JSON.stringify({ creatureIds: [spawn.genomeId] }),
    });

    const denied = await fetch(`${base}/roster/http-acct`);
    assert.equal(denied.status, 401);

    const ok = await fetch(`${base}/roster/http-acct`, { headers: { "x-internal-key": CFG.internalKey } });
    assert.equal(ok.status, 200);
    assert.equal((await ok.json()).roster.length, 1);
  } finally {
    await close();
  }
});

test("POST /cannibalize retires a genome, credits Bones, and rejects loading it back into the menagerie", async () => {
  const { base, close } = await startServer();
  try {
    const spawn = await (await fetch(`${base}/spawn`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "cb-spawn" }),
    })).json();
    const genomeId = spawn.genomeId;

    const before = await (await fetch(`${base}/wallet`, { headers: ACC })).json();

    const cann = await (await fetch(`${base}/cannibalize`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "cb-1", genomeId }),
    })).json();
    assert.equal(cann.status, "completed");
    assert.ok(cann.bonesRecovered > 0);

    const after = await (await fetch(`${base}/wallet`, { headers: ACC })).json();
    assert.equal(after.bones, before.bones + cann.bonesRecovered);

    const tray = await (await fetch(`${base}/tray`, { headers: ACC })).json();
    assert.equal(tray.items.length, 5); // 4 parts + 1 heart

    const menagerie = await fetch(`${base}/menagerie`, {
      method: "PUT", headers: ACC, body: JSON.stringify({ creatureIds: [genomeId] }),
    });
    assert.equal(menagerie.status, 400);
  } finally {
    await close();
  }
});

test("POST /restore brings a creature back after a simulated server wipe", async () => {
  const { base, close } = await startServer();
  try {
    const spawn = await (await fetch(`${base}/spawn`, {
      method: "POST", headers: ACC, body: JSON.stringify({ idempotencyKey: "rs-spawn" }),
    })).json();
    const before = await (await fetch(`${base}/creature/${spawn.genomeId}`, { headers: ACC })).json();

    // a rejected restore first: tampering with the genome breaks the signature
    const tampered = { ...before.genome, body: { ...before.genome.body, params: [1, 1, 1, 1] } };
    const rejected = await (await fetch(`${base}/restore`, {
      method: "POST", headers: ACC,
      body: JSON.stringify({ idempotencyKey: "rs-bad", genome: tampered, signature: before.signature }),
    })).json();
    assert.equal(rejected.status, "failed_experiment");

    // the real thing: restoring the untouched {genome, signature} pair --
    // this is exactly what the Lab's localStorage backup replays
    const restored = await (await fetch(`${base}/restore`, {
      method: "POST", headers: ACC,
      body: JSON.stringify({ idempotencyKey: "rs-good", genome: before.genome, signature: before.signature }),
    })).json();
    assert.equal(restored.status, "completed");
    assert.equal(restored.genomeId, spawn.genomeId, "restored under its original id");

    const read = await fetch(`${base}/creature/${spawn.genomeId}`, { headers: ACC });
    assert.equal(read.status, 200);
  } finally {
    await close();
  }
});

test("/health and /version are public -- no x-account-id needed", async () => {
  const { base, close } = await startServer();
  try {
    const health = await fetch(`${base}/health`);
    assert.equal(health.status, 200);
    assert.deepEqual(await health.json(), { ok: true });

    const version = await fetch(`${base}/version`);
    assert.equal(version.status, 200);
    const v = await version.json();
    // no RENDER_GIT_COMMIT/BUILD_COMMIT in the test environment -- this
    // is exactly the "deploy forgot to bake a commit in" case, so the
    // field must still be present and honest about not knowing
    assert.equal(v.commit, "unknown");
    assert.ok(typeof v.startedAt === "string" && !Number.isNaN(Date.parse(v.startedAt)));
  } finally {
    await close();
  }
});
