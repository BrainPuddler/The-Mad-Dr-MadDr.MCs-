# 09 — Real-Time Multiplayer Architecture

Status: Draft v0.1 · Pillars served: 3, 4 · Consumes determinism requirements from [04-combat-model.md](04-combat-model.md) and the genome contract from [06](06-mutator-design.md)/[07](07-mutator-server-architecture.md).

## Requirements recap

- 1v1 real-time RTS matches, 10–15 minutes, ≤ ~30 monsters + ~20 map entities ⇒ **≤ ~60 synced entities** ([02-gameplay-overview.md](02-gameplay-overview.md)).
- Primary platform: **mobile** — cellular networks with jitter, NAT, app backgrounding, and mid-match network handoffs are the *normal* case, not the edge case.
- Combat outcomes must be trustworthy (pillar 3) and luck rolls server-owned ([04](04-combat-model.md)).

## The netcode decision: server-authoritative state sync, not lockstep

The two classic RTS models, evaluated against *our* constraints:

| | Deterministic lockstep | **Server-authoritative state sync (chosen)** |
| --- | --- | --- |
| Bandwidth | Tiny (commands only) | Modest — fine at our entity count (math below) |
| Latency feel | Stalls on *any* late packet from either peer — fatal on cellular | Server ticks on; a lagging client interpolates and catches up |
| Determinism burden | Bit-perfect sim across iOS/Android/PC: float traps, RNG sync, every patch a desync risk | Only the server simulates; clients render |
| Reconnect / resume | Requires full input-history replay or save-sync; very hard | Trivial: send a state snapshot |
| Anti-cheat | Weak (maphack-prone; clients know everything) | Strong: clients are display + input only |
| Classic fit | 500-unit armies (StarCraft) | ≤ 60 entities — *us* |

Lockstep's one advantage (bandwidth) is worthless at 60 entities, and every one of its weaknesses lands squarely on mobile. **Decision: server-authoritative.** This also fulfills [04](04-combat-model.md)'s requirement directly: the per-match seeded combat RNG lives in the server sim and nowhere else.

## Simulation & sync model

- **Authoritative server sim at 10 Hz** (upgradeable to 15 Hz if feel demands; fixed-point math per [04](04-combat-model.md)).
- Clients send **commands** (move, attack, harvest, capture-channel, reanimate, ability, repair, cannibalize) — at most a few per second per player. `repair` and `cannibalize` are Vat-only real-time commands, not Mutator-service ops — full specs in [20-harvest-and-repair.md](20-harvest-and-repair.md).
- Server broadcasts **delta-compressed entity state** per tick; full keyframe every 2 s and on demand.
- Clients **render-interpolate ~150 ms behind** server time (smooths jitter; at RTS pace, 150 ms presentation delay is imperceptible). Client-side *command acknowledgment* effects (move marker, "she's moving" bark) fire instantly; positions never predicted speculatively in v1 — RTS units, unlike FPS avatars, don't need it.

### Bandwidth math (why this is comfortably mobile-safe)

~60 entities × ~20 B average delta × 10 Hz = **~12 kB/s worst case**, typically far less with delta compression and at-rest entities omitted. Cellular budgets this with an order of magnitude to spare; a 15-minute match costs ≈ 10 MB worst case.

On a city battlefield, this budget applies **per engagement zone**, not per map ([18-city-battlefields.md](18-city-battlefields.md) §5) — a 5 km map does not multiply the entity count, only the number of zones that could theoretically be active at once (v1: one, occasionally two adjacent). One additional line item: **building Structure-HP deltas**, sent only for buildings with damage > 0, delta-compressed the same way.

## Mobile resilience

| Scenario | Handling |
| --- | --- |
| Connection drop / network handoff | **30-second reconnect grace**: sim continues, monsters hold last orders (fight back if attacked); on reconnect, full keyframe snapshot resync. Beyond 30 s repeated, forfeit rules apply |
| App backgrounded (call, notification) | Same grace path; additionally each player has **one 20-second "the doctor steps out" pause** per match (1v1 courtesy pause; opponent sees a themed overlay) |
| Transport | UDP with a thin reliability/ordering layer for commands and keyframes; **WebSocket/TCP fallback** when UDP is blocked (hotel/corporate NATs) |
| Jitter | The 150 ms interpolation buffer absorbs it; buffer adapts ±50 ms |

## Match-start handshake (the genome boundary)

1. Matchmaker pairs players and assigns a match server.
2. Match server fetches both players' Menagerie genomes **directly from the Mutator service** ([07-mutator-server-architecture.md](07-mutator-server-architecture.md)), each genome **signed by the Mutator service**. Clients never upload genomes — a client cannot inject a hand-crafted monster (cross-ref [06](06-mutator-design.md) anti-cheat posture).
3. Both clients receive both rosters' genomes and **pre-assemble all monsters during the loading screen** ([08-creature-visualization.md](08-creature-visualization.md)) — no mid-match assembly hitches, and your opponent's horrors render identically on your device (deterministic assembly).

On a city battlefield ([18-city-battlefields.md](18-city-battlefields.md)), the same handshake payload also carries the **city seed, style preset, and size** — both clients generate the identical map from the seed alone during the same loading screen, so the city itself is never transmitted.

**Not to be confused with:** `unity-client/`'s `RosterFetcher` (a dev-time single-account roster fetch straight from the Mutator service, with a local-disk cache fallback, so a bred monster can actually be *seen* wandering a generated city — [18](18-city-battlefields.md)'s implementation note). It reuses the same `GET /menagerie` / `GET /creature/:id` account-facing endpoints this handshake's step 2 conceptually mirrors, but it is a single client talking directly to the Mutator service for local testing, not a match server fetching both players' signed rosters. No match server exists in this repo yet to do the real thing.

## Matchmaking

- **MMR (Elo-family) + power-band check**: the Menagerie's summed genome power budget ([06](06-mutator-design.md) brain budget) must fall within the opponent's band. Matchmaking and Mutator balance interlock through this single number — a deliberately simple first system.
- **Regional relays/servers** to keep RTT sane; match server placement by both players' region, worst-case midpoint.
- Queue widens MMR and power bands over wait time; AI fallback match offered at 90 s (mobile players don't wait).

## Anti-cheat boundary (one sentence, load-bearing)

**Clients are display and input only; everything that decides outcomes — simulation, combat RNG, capture timers, economy ticks, genome validity — executes on servers.** Remaining cheat surface (input automation, screen reading) is accepted for v1 and parked in [12-open-questions.md](12-open-questions.md).

## Server topology

- **Matchmaker / session service**: stateless API + queue.
- **Match servers**: one process hosts many concurrent 1v1 rooms (60 entities × 10 Hz is cheap; target ≥100 rooms per 2-vCPU instance, to be measured in Phase 3); autoscaled pool, regionally deployed.
- **Mutator service** ([07](07-mutator-server-architecture.md)): separate service, shared **account/auth** system (single sign-on across lab and matches — pillar 4).
- Implementation language/runtime for the match sim is an open question ([12-open-questions.md](12-open-questions.md)): headless engine build vs. custom sim — coupled to the engine decision ([10-engine-evaluation.md](10-engine-evaluation.md)).

## Test strategy

- Phase 3 ([11-roadmap.md](11-roadmap.md)) gates on: scripted-bot soak tests (1,000 simulated matches), packet-loss/jitter harness (5% loss, 200 ms jitter spikes must stay playable), reconnect torture test (drop every 60 s), and real-device cellular field tests.
- The sim core must run headless with recorded-input replay — the same harness validates combat tuning ([04](04-combat-model.md)) and netcode.
