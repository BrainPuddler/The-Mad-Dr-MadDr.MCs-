# @maddr/mutator-service

The **Mutator API service** ([docs/07](../../docs/07-mutator-server-architecture.md)) —
the server-side authority over genomes. It runs all mutation math (via
[`@maddr/genome-core`](../genome-core/)), so clients submit *requests* and
never genomes (anti-cheat). Deliberately boring: a stateless HTTP service
over a pluggable store, **zero runtime dependencies** (Node's built-in
`http`).

```
npm install      # links @maddr/genome-core (build it first) + dev deps
npm test         # build + 28 tests (service logic + live HTTP)
npm start        # boot on :8787 with the in-memory store
```

## Guarantees (docs/07)

- **Immutable genomes.** Every operation inserts a *new* genome; lineage
  lives in `parentIds`. Rollback is a pointer, pedigrees are free.
- **Mandatory idempotency.** Every mutating call takes an `idempotencyKey`;
  a retry returns the *same* result and never double-spends (mobile
  networks retry — this is not optional).
- **Server-seeded, auditable RNG.** Each op's seed is generated and logged
  (`OperationRecord.serverSeed`); resubmitting can't reroll, and any result
  is replayable.
- **Signed results.** Genomes are HMAC-signed by the service key so match
  servers can trust a roster; clients never hold the key.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/spawn` | Mint a primordial creature (onboarding) |
| POST | `/mutate` | One parent, biased randomness |
| POST | `/splice` | Two parents, crossover |
| POST | `/graft` | Deterministic slot set (lab control valve) |
| POST | `/harvest/part` · `/harvest/heart` | Cut a part/heart off → tray item; donor stumped/corpse |
| POST | `/sew/part` · `/sew/heart` | Graft a tray item on, gated by the heart (survived / limb_rejected / patient_died) |
| POST | `/restore` | Re-insert a `{genome, signature}` pair the caller already holds a valid signature for — the client-side backup safety net, see below |
| GET | `/creatures` · `/creature/:id` · `/creature/:id/lineage` | Read genomes / ancestor tree |
| GET·PUT | `/menagerie` | The ≤12 loadout |
| GET | `/wallet` · `/tray` · `/catalog` | Components / harvested items / discovered families |
| GET | `/roster/:accountId` | **Internal** (match servers): signed Menagerie genomes |

Auth is stubbed for now: the `x-account-id` header stands in for the JWT
subject that OAuth/OIDC will issue ([docs/07](../../docs/07-mutator-server-architecture.md));
internal endpoints check a shared `x-internal-key`.

## Surgery economics (nothing is wasted)

`/harvest/*` drops the cut part into your **tray** (`/tray`) as a concrete
item with its genes intact. `/sew/part` consumes a tray item only if the
graft **survives**; on `limb_rejected` or `patient_died` the item stays in
the tray, still usable, and most of the operating fee is refunded. A
successful `/sew/heart` hands the patient's **old heart** back to the tray
— the natural way to recycle a heart up a lineage.

## Data loss on restart, and the client-side backup that papers over it

`InMemoryStore` is exactly what it sounds like: every genome lives only
in this process's RAM. **Any restart loses everything** — a redeploy, or
(more often, in practice) Render's free tier spinning the service down
after ~15 minutes idle and starting a brand-new process on the next
request. Re-polling the server can't fix this: the data isn't stale,
it's gone.

The Lab (`site/main.js`) mitigates this client-side: every creature it
saves to the Stable also gets its full `{genome, signature}` pair cached
in this browser's `localStorage`. When a sync finds the server missing
one the Stable list still names, it replays the cached pair through
`POST /restore`. The signature IS the security boundary here — `restore`
only accepts a `{genome, signature}` pair that verifies against
`SIGNING_KEY` (`verifyGenome`, `src/sign.ts`), and only the server ever
holds that key, so this can't be turned into "mint any hand-crafted
genome for free." It can only resurrect a row that legitimately existed
at some point, under its **original id** (not a fresh one — see
`MutatorService.restore`'s doc comment for why it deliberately bypasses
the shared `mint()` helper).

This is a genuine safety net, not a substitute for real persistence: it
only covers what a given browser actually had cached, and does nothing
for the operation log, wallet, or tray. **Postgres is still the real
fix** — see below.

## Swapping in Postgres

The service depends only on the `Store` interface (`src/store.ts`). The
included `InMemoryStore` is the dev/test backend; a `PostgresStore`
implementing the same methods drops in with no service changes — the data
model (genomes, op log, wallet, inventory, menagerie, catalog) maps
directly to the tables in [docs/07](../../docs/07-mutator-server-architecture.md).
