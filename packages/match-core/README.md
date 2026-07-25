# match-core

Engine-agnostic **deterministic match simulation** for the RTS layer
(docs/23). The same architectural role `citygen-core` plays for the city and
`genome-core` plays for the genome: pure C#, zero `UnityEngine`, built and
tested standalone via `dotnet`, imported into Unity as an asmdef reference.

**This is the `(seed, command-stream) → state` pure function that docs/23 §11's
lockstep 4v4 is built on.** Everything here is integer/fixed-point and hashes
byte-for-byte identically across machines (docs/23 §0 float discipline).

## Phase 1 scope

The **skeleton**, per docs/23 Phase 1 (as amended by §13):

- `SimRng` — deterministic sfc32 exposing raw `uint` draws (integer math only;
  bit-identical to `citygen-core`'s proven stream, verified by test).
- `FnvHash` — streaming FNV-1a state digest; little-endian ints, bitwise
  floats, never `ToString`/JSON (docs/23 §13-J).
- `Origin` / `ResourceKind` / `Resources` — the three origins, six resources,
  and the energy-follows-origin rule (docs/17).
- `FactionDef` — the three factions with canon themed base names
  (The Sanatorium / Fort Vigilance / The Brood Nest).
- `PlayerState` — integer wallets (validation-not-clamping spend), supply
  used/cap (docs/23 §13-E), and the Chimera-Track origin mask (opens on **all
  three origins**, docs/23 §13-F).
- `Command` / `MatchState` — the fixed-tick (10 tps) advance function, a
  monotonic entity-ID allocator, and the canonical `Hash()`.

## Phase 1.5 scope (docs/23 §13 amendment A — "port the live sim")

The first slice of the sim-porting workstream: **deterministic unit
movement**.

- `SimUnit` — one entity's position (double X/Z, hashed bitwise) and order
  state (`Idle` / `MoveTo`), ticked by consuming a `Speed * dt` budget across
  as many path nodes as it covers per tick (never leaves fractional motion on
  the table).
- `MatchState.SpawnUnit` — setup-time spawn (direct call, like
  `AllocateEntityId`, not a replayable command — matches how the live game
  doesn't treat match-start placement as a player order either).
- `CommandKind.MoveTo` — the canonical, replayable way to ORDER a unit:
  targets an entity ID (never an object reference, §13-J), resolves a path
  via the SAME `HexPathfinder`/`BattlefieldState.BlockedToGround()`
  citygen-core already uses for the live game, so sim pathing and Unity
  pathing behave identically by construction, not by coincidence.
- Units are iterated for `Tick`/`Hash` in **entity-ID allocation order only**
  (a parallel dictionary gives O(1) command dispatch without touching
  iteration order) — the §0 "never object reference or hash-set order" rule,
  enforced structurally, not by convention.

**Deliberately NOT done in this pass:** the OTHER half of Phase 1.5's
acceptance bar — rewriting Unity's `MonsterAgent` to render this sim state
via interpolation with **zero** `Time.deltaTime`-driven gameplay decisions
left in it. `MonsterAgent` is a ~950-line file ten already-shipped phases of
combat/economy/special-attacks logic depend on, and this environment has no
Unity Editor to visually verify a rewrite of that size actually still works.
Doing it blind would violate this project's own "never claim visual
verification" rule. Flagged as the next real step (docs/12), not silently
skipped.

## Build & test

```
cd packages/match-core
dotnet test Tests~/MatchCore.Tests.csproj      # 19 tests
```

Acceptance harness: `Tools~/DetHarness` runs BOTH acceptance proofs and prints
each hash twice; every pair of lines must be identical.

- docs/23 Phase 1: a 10,000-tick 8-player empty match.
- docs/23 §13-A Phase 1.5: 100 units, scripted `MoveTo` orders over a real
  generated city, 3,000 ticks.

```
dotnet run --project Tools~/DetHarness
```

## Layout note

`bin~`/`obj~` and `Tests~`/`Tools~` are tilde-suffixed so Unity's package
importer ignores them (same trick as every other package here); `dotnet` builds
into them via `Directory.Build.props`.
