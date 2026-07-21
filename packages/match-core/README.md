# match-core

Engine-agnostic **deterministic match simulation** for the RTS layer
(docs/23). The same architectural role `citygen-core` plays for the city and
`genome-core` plays for the genome: pure C#, zero `UnityEngine`, built and
tested standalone via `dotnet`, imported into Unity as an asmdef reference.

**This is the `(seed, command-stream) → state` pure function that docs/23 §11's
lockstep 4v4 is built on.** Everything here is integer/fixed-point and hashes
byte-for-byte identically across machines (docs/23 §0 float discipline).

## Phase 1 scope (this commit)

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

**Not yet here** (arrive with their phases, docs/23 §13-A porting workstream):
units, buildings, economy income/upkeep, combat, emitters/mana. Phase 1 proves
the *shape* — one seeded stream, integer state, canonical hash, pure tick.

## Build & test

```
cd packages/match-core
dotnet test Tests~/MatchCore.Tests.csproj      # 13 tests
```

Acceptance harness (docs/23 Phase 1): `Tools~/DetHarness` prints the hash of a
10,000-tick 8-player empty match twice; the two lines must be identical.

```
dotnet run --project Tools~/DetHarness
```

## Layout note

`bin~`/`obj~` and `Tests~`/`Tools~` are tilde-suffixed so Unity's package
importer ignores them (same trick as every other package here); `dotnet` builds
into them via `Directory.Build.props`.
