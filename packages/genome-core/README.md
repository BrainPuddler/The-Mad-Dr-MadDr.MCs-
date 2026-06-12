# @maddr/genome-core

The production genome library for MadDr.MCs — **the genotype side of the
genotype/phenotype split**. Pure data and pure functions: no graphics, no
engine, no I/O. The renderer ([docs/08](../../docs/08-creature-visualization.md))
and the Mutator service ([docs/07](../../docs/07-mutator-server-architecture.md))
both consume this package through the schema contract.

Implements **genome v2** (adopted per Q10, [docs/12](../../docs/12-open-questions.md))
in TypeScript (Q3): the schema validated by the
[Python prototype](../../prototype/mutator/), productionized.

```
npm install     # dev deps (typescript only)
npm test        # build + 16 property/golden tests
```

| Module | Contents |
| --- | --- |
| `src/genome.ts` | The v2 schema: part alleles (6 shared axes), body genes (plan + 4 axes), brain genes (tier + 5 behavioral axes), slots, lineage |
| `src/catalog.ts` | 16 part families × 4 homolog classes × 3 origins (organic / tech / biotech), 4 body plans, canalized expression — the contract shared with the renderer |
| `src/rng.ts` | Deterministic seeded RNG (sfc32) — the canonical randomness; `Math.random` is never used |
| `src/operators.ts` | Mutate / Splice / Graft with the full rule set: homolog grammar, origin rules (tech inert, biotech breeds), component-feeding bias, plan/tier jumps, lineage recording |
| `src/validate.ts` | Server-side viability validation ("failed experiment", never silent clamping) |
| `src/behavior.ts` | Brain → behavior expression: control capacity/cost/radius, berserk threshold/buffs, power-budget contribution |
| `src/energy.ts` | Energy demand: upkeep per minute typed by part origin (organic→blood, tech→fuel, biotech→ichor), reanimation surge |
| `src/serialize.ts` | Canonical JSON (byte-stable for hashing/signing) with validating deserialization |

## Determinism contract

Same seed ⇒ same monster, on every platform, forever. `tests/golden.txt`
pins the RNG stream and a lineage digest; if the golden test breaks, the
change is **breaking for replayability/auditability** (docs/07) and must be
a deliberate, versioned decision (`npm run test:update-golden`).

## What this package is not

- Not the renderer: genomes become 3D monsters elsewhere (engine-side),
  as a pure function of this data.
- Not the service: HTTP, Postgres, idempotency, and auth live in the
  Mutator service (next step), which imports this library.
- Not the match sim: loyalty/rage dynamics over time are match-server
  logic; this package only defines what a brain *is*
  ([docs/16](../../docs/16-brains-behavior-command.md)).
