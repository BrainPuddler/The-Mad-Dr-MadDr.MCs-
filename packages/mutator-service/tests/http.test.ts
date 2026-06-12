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
