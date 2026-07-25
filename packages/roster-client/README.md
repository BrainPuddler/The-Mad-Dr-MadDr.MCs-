# MadDr.RosterClient

Engine-agnostic JSON parsing for `packages/mutator-service`'s Menagerie/
creature response shapes (docs/07) — the same architectural role
[`packages/citygen-core`](../citygen-core/) plays for the city: pure data
and pure functions, no `UnityEngine` reference, no networking, no file
I/O. The Unity project ([`unity-client/`](../../unity-client/)) does the
actual `UnityWebRequest` fetch and `Application.persistentDataPath`
local-disk caching, and hands the response bodies here to turn into
typed data.

## Why a hand-rolled JSON parser, not a library

Unity's built-in `JsonUtility` can't deserialize the shapes this needs
(no dictionary support, no optional-field handling for `PartAllele.hue`).
The obvious fix — add `com.unity.nuget.newtonsoft-json` via UPM — was
deliberately **not** taken: this session already hit one real Unity
Package Manager resolution failure (`unity-client/`'s `citygen-core`
reference briefly broke when Unity's project root got confused with the
repo's `packages/` folder). Adding a second external package dependency
right after fixing that is exactly the kind of avoidable risk "most
bulletproof possible" argues against. A ~250-line parser with nothing to
resolve, fully unit tested against real captured server responses, is
the safer bet.

```
dotnet test Tests~/RosterClient.Tests.csproj   # build + 18 tests
```

| Module | Contents |
| --- | --- |
| `src/Json.cs` | `JsonValue`: a minimal recursive-descent JSON parser/writer (RFC 8259 — objects, arrays, strings with escapes, numbers, `true`/`false`/`null`). `Field`/`FieldOrNull` for typed, throw-on-missing / null-safe access |
| `src/GenomeDto.cs` | Typed DTOs mirroring `packages/genome-core`'s `Genome` interface field-for-field: `GenomeDto`, `BodyGenesDto`, `BrainGenesDto`, `HeartGenesDto`, `SlotsDto`/`PartAlleleDto`, `StoredGenomeDto`, `MenagerieDto` — each with `FromJson`/`ToJson` |
| `src/RosterCache.cs` | `RosterCache`: bundles a fetched `MenagerieDto` + every referenced `StoredGenomeDto` + a fetch timestamp — the exact shape the Unity client writes to local disk on a successful fetch and reads back when offline |

**Tests~/GenomeDtoTests.cs's fixtures are verbatim captures from a real
running `mutator-service`** (`POST /spawn` → `PUT`/`GET /menagerie` →
`GET /creature/:id` against a live local instance), not hand-written
approximations — if the service's response shape ever changes, these
tests fail against the real thing.

## On signatures

`StoredGenomeDto.Signature` passes straight through, unexamined.
docs/07: genome signatures are "verified by match servers" — not by
clients. There's no verification key for this package (or the Unity
client that uses it) to hold, dev-mode or otherwise; the signature is
carried for whenever a real match server exists to check it.

## Golden fixtures now gate sim determinism, not just display (docs/23 §13-K-iv)

`Locomotion.cs`/`Weapon.cs`/`Harvest.cs`'s golden tests (`LocomotionTests.cs`,
`WeaponTests.cs`, `HarvestTests.cs`) were written to keep this package honest
as genome-core's **display-side twin** — proving the Lab and the Unity
battlefield agree on what a genome expresses. docs/23 (the RTS master build
plan) promotes this same code to be **`match-core`'s source of truth** for
genome-derived stats once units are ported into the deterministic tick sim
(§13-A, Phases 1.5+): a monster's locomotion/weapon/harvest profile, computed
here, becomes part of what the sim hashes and lockstep depends on being
bit-identical across every client. That means these golden fixtures no longer
guard a cosmetic mismatch if they regress — they guard **desync**. Treat any
change to `Locomotion.cs`/`Weapon.cs`/`Harvest.cs` (or their golden values)
with the same care as a change to `match-core` itself, and remember these
tests are load-bearing for §11's lockstep the moment Phase 1.5 lands, not
just for the Lab preview matching the battlefield.

## What this package is not

- Not the HTTP client: no `UnityWebRequest`, no retry/timeout logic, no
  local-cache read/write — that's Unity-side (`RosterClient.cs` in
  `unity-client/Assets/Scripts/`).
- Not auth: no OAuth, no JWT, no session handling. `x-account-id` is
  docs/07's own documented interim stand-in for what real auth will
  provide; this package doesn't know or care how an accountId was
  obtained.
- Not genome validation: doesn't check bounds, doesn't know which part
  families exist. It parses what the server sent; `packages/genome-core`
  is the only source of truth for whether a genome is *valid*.
