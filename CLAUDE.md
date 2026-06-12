# MadDr.MCs — project memory

Mad-doctor monster construction set: breed/mutate/stitch comic-horror
monsters (the El-Fish promise), field them in async tactical matches.
Design docs in `docs/` (start: 00-index, 12-open-questions has the
decision log). Vision pillars: ownership of creations, no gacha.

## Repo layout

- `docs/` — numbered design docs. **Normative-schema rule:** the genome
  schema is defined once; docs 06 (mutator), 07 (service), 08 (rendering)
  must change together. Decisions go in the docs/12 decision log.
- `prototype/mutator/` — exploratory Python prototype (SVG galleries).
  Superseded for correctness by genome-core; keep for visual experiments.
- `packages/genome-core` — **the source of truth for all genetics.**
  TypeScript, zero runtime deps, no graphics/engine/I-O.
- `packages/mutator-service` — HTTP API over genome-core. Zero runtime
  deps (Node built-in http). Store is an interface; only InMemoryStore
  exists so far (data lost on restart — Postgres impl is the known next
  brick).
- `site/` — "The Lab" browser test bench, deployed by GitHub Pages
  (`.github/workflows/pages.yml`) from `main`:
  https://brainpuddler.github.io/The-Mad-Dr-MadDr.MCs-/
  `site/lib/` is a **vendored copy** of compiled genome-core; after
  changing genome-core run:
  `cd packages/genome-core && npm run build && cp dist/src/*.js ../../site/lib/`

## Build & test

Each package: `npm install && npm test` (tsc + node:test; tests live in
`tests/`, compiled to `dist/tests/`). Build genome-core before
mutator-service (file: dependency).

## Invariants (do not break casually)

- **Genome v2** (adopted; Q10): 4 homolog slots × 6 shared part axes,
  body (plan+4 axes), brain (tier+5 axes), heart (tier+6 params).
  All genes in [0,1]; phenotype ranges come from canalized bounds in
  `catalog.ts` (the contract renderer + service share).
- **Determinism contract:** sfc32 RNG in `rng.ts` is canonical; never
  Math.random. `tests/golden.txt` pins the stream + a lineage digest —
  if the golden test breaks, that's a versioned breaking change
  (`npm run test:update-golden` only deliberately).
- **Origins:** organic breeds; tech never mutates/blends (Graft-only);
  biotech breeds but stays biotech. Energy follows origin:
  organic→blood, tech→fuel, biotech→ichor. Living frame always drinks
  blood. Silent (plan-ignored) slots cost nothing.
- **Heart/surgery:** heart capacity vs upkeep load gates viability
  (viable / strained / nonviable, SHOCK_FACTOR 1.5). Sew outcomes:
  survived / limb_rejected / patient_died — **failed surgery always
  returns the part still usable** ("nothing is wasted").
- **Service:** genomes are immutable rows (operators mint new ones,
  lineage in parentIds); idempotency key mandatory on every mutating op
  (retry returns same result, charges once); per-op server seed logged;
  genomes HMAC-signed. Validation = "failed experiment", never silent
  clamping.

## Workflow

- `main` is home and always deployable; session branches merge into main
  promptly (fast-forward when possible). Merging to main = publishing
  (Pages auto-redeploys on pushes touching `site/`).
- v0.1 economy/upkeep numbers everywhere are placeholders; real balance
  is a Phase-2 sandbox pass (docs/11).
