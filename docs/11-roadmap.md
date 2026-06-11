# 11 — Development Roadmap

Status: Draft v0.1 · Sequencing philosophy: **risk-first** — kill the scariest unknowns at their cheapest point. The two existential risks are *"is the combat fun?"* and *"can we render mutated 3D creatures on phones?"* — both are retired in Phase 1, deliberately **before any netcode exists**.

## Phase table

| Phase | Name | Duration | Risk retired |
| --- | --- | --- | --- |
| 0 | Paper & Pixels | 4–6 wks | "Do the numbers hold up at all?" |
| 1 | Twin Prototypes | 8–10 wks | Combat fun; creature rendering; engine choice |
| 2 | The Stitching | 10–12 wks | "Is the *loop* fun?" (fight → mutate → refight) |
| 3 | First Blood Online | 12 wks | Real-time mobile multiplayer viability |
| 4 | Cross-Device Lab | 8 wks | The pillar-4 promise end-to-end |
| 5 | Closed Beta | 12 wks | Content breadth, balance at population scale |
| 6 | Launch & Live | ongoing | — |

## Phase 0 — Paper & Pixels (now)

- **Deliverables**: this documentation suite written and reviewed; **spreadsheet/paper playtest** of the combat formula and Lumen Cycle — hex paper, dice, the [04](04-combat-model.md) worked examples re-derived by hand, the [03](03-mana-system.md) phase timings walked through as turns.
- **Exit criteria**: docs 01–10 at "Reviewed" status in [00-index.md](00-index.md); paper playtest reproduces the [04](04-combat-model.md) sanity anchors (~70% / ~85% / near-even) within tolerance, or the numbers are retuned until it does.

## Phase 1 — Twin Prototypes (parallel tracks)

Two independent tracks; neither blocks the other, and **no netcode in either**.

- **Track A — Combat sandbox**: single-device, local-only, ugly-box units on a hex map. Implements [03](03-mana-system.md) + [04](04-combat-model.md) + the in-match half of [05](05-component-economy.md) exactly as written (the sim core is built headless-first with recorded-input replay — the same core later goes inside the match server, [09](09-multiplayer-architecture.md)).
- **Track B — Mutator vertical slice**: genome schema ([06](06-mutator-design.md)) implemented; server `POST /mutate` + Postgres end-to-end ([07](07-mutator-server-architecture.md)); and the **engine validation spike** from [10](10-engine-evaluation.md) — runtime-assemble genomes into 3D monsters, 30 on screen at 60 fps on the mid-range Android reference device ([08](08-creature-visualization.md)).
- **Exit criteria**: (a) three devs play the sandbox and *choose* to play again — fights demonstrably swing on position and Lumen timing; (b) **the sandbox test is run on a phone, not a dev PC** — ordering a flank with a thumb under time pressure must feel good, since touchscreen positional input is the design's biggest fun risk ([13-lens-review.md](13-lens-review.md)); (c) a mutated genome round-trips server → phone → recognizable 3D monster; (d) the spike passes and the engine decision locks ([10](10-engine-evaluation.md)).

## Phase 2 — The Stitching

- Join the tracks: real creatures (Track B) fighting in the real sandbox (Track A). Full single-player skirmish vs. scripted AI on the [02](02-gameplay-overview.md) match anatomy — all three victory conditions, FTUE rough cut, the meta loop live (win components → mutate → refight).
- **Exit criterion (the famous one)**: *a stranger plays three matches and mutates a monster unprompted* — the loop sells itself without a designer in the room.

## Phase 3 — First Blood Online

- Server-authoritative netcode per [09](09-multiplayer-architecture.md): match server hosting the Phase-1 sim core, delta sync, reconnect grace, the pause rule, WebSocket fallback. Closed friends-and-family test on real devices over real cellular networks.
- **Exit criteria**: the [09](09-multiplayer-architecture.md) test gates — 1,000-match bot soak, playable at 5% loss / 200 ms jitter spikes, reconnect torture test, field test sessions completing at >95%.

## Phase 4 — Cross-Device Lab

- PC lab bench client; shared accounts/auth ([07](07-mutator-server-architecture.md)); queued commute-mode mutations + push notifications; matchmaking v1 (MMR + power band, [09](09-multiplayer-architecture.md)).
- **Exit criterion**: the commute story demoable end-to-end — queue a splice on a phone in airplane mode, see the result on the PC bench.

## Phase 5 — Closed Beta

- Content to launch budget: part library to the ~150–250 mesh target, 6 archetypes, quirk set ([08](08-creature-visualization.md), [06](06-mutator-design.md)); balance telemetry (win rates by affinity/archetype/power band); store compliance; soft launch in one region.
- **Exit criteria**: D1/D7 retention targets set and met in soft launch (numbers to be set when Phase 5 begins — [12](12-open-questions.md)); no balance outlier archetype >55% win rate at equal power band.

## Phase 6 — Launch & Live

- Launch checklist gated on Phase 5 telemetry. First two live-ops beats, pre-designed: (1) a **Lunar Eclipse event** — limited-time Lumen variant (Night-heavy cycles); (2) the first **new part-family drop**, exercising the content lever ([08](08-creature-visualization.md)) and the discovery system ([06](06-mutator-design.md)).

## De-scope ladder (cut in this order if behind)

1. PC lab bench client (mobile lab keeps the Mutator alive)
2. Quirk genes
3. 6th archetype (`amorphous` — the rigging risk-pile)
4. Territory-score victory condition (keep Vat + Dominion)

**Never cut**: the server-authoritative core ([09](09-multiplayer-architecture.md)) and the Mutator itself ([06](06-mutator-design.md)/[07](07-mutator-server-architecture.md)) — they are pillars 3, 1, and 4; without them this is a different game.

## Team-shape notes

- Phases 0–2 are a 3–5 person shape: gameplay engineer, server/tools engineer, technical artist (the part pipeline is the art bottleneck — start the DCC kit in Phase 1), designer-producer.
- Phase 3 adds netcode/infra; Phase 5 adds art throughput for the part library. The part-authoring workflow ([08](08-creature-visualization.md)) is what makes art scale linear — invest in the import-validation tooling early.
