# MadDr.CityGen

The engine-agnostic logic core for the City Battlefields track
([docs/18](../../docs/18-city-battlefields.md)) — the same architectural
role `packages/genome-core` plays for the genome
([docs/06](../../docs/06-mutator-design.md)): pure data and pure
functions, no `UnityEngine` reference, no rendering, no I/O. The Unity
project ([`unity-client/`](../../unity-client/)) references it as a local
UPM package (`"com.maddr.citygen-core": "file:../../packages/citygen-core"`
in its `Packages/manifest.json`) and builds scenes, prefabs, and
MonoBehaviours on top — nothing here ever will.

**This is a growing slice, not the full track.** Implemented so far: the
hex grid index, the attack-arc model, the deterministic RNG, the
procedural city generator, a terrain layer (river, ponds, hills,
destructible bridges — [docs/18](../../docs/18-city-battlefields.md) §2 /
docs/04's water rule), destructible-building runtime state (§3), and the
engagement-zone LOD classifier (§5). Not yet started: the Mutator-service
HTTP client (§6), and — the real gap — none of this is wired into an
actual match sim yet, because no match sim exists in this repo (Phase 1's
own "ugly-box" combat sandbox doesn't exist either, [11](../../docs/11-roadmap.md)).
See [docs/18](../../docs/18-city-battlefields.md)'s Open questions for
sequencing (Q14: this whole track doesn't block Phases 1–3).

```
dotnet test Tests~/CityGenCore.Tests.csproj   # build + 130 tests
```

| Module | Contents |
| --- | --- |
| `src/HexCoord.cs` | Axial hex coordinates: neighbors, exact distance, `Ring`/`Range` (aura/radius queries), odd-r offset-rectangle conversion (`FromOffset`), and the world-space (meters) conversion at `HexCoord.HexMeters = 20` ([docs/18](../../docs/18-city-battlefields.md) §1) |
| `src/Facing.cs` | The attack-arc model — `Facing.ArcOf(attacker, defender, defenderFacing)` classifies Front/Flank/Rear ([docs/04](../../docs/04-combat-model.md) posMod) |
| `src/Rng.cs` | A **bit-exact port** of `packages/genome-core/src/rng.ts`'s sfc32 RNG — not just "also deterministic," but proven to produce the identical output sequence for the same seed as the TypeScript reference (golden values captured from a live node process, `Tests~/RngTests.cs`). What makes docs/18 §2's "city generation is a pure function of (seed, preset, size)" hold across languages. |
| `src/CityGenerator.cs` | The docs/18 §2 pipeline: seeded terrain (river/ponds/hills) → road network (Grid / MainStreet / Radial per preset) → block subdivision (connected components) → landmark allocation (emitter **xor** Community Hub per node, docs/02's 6–10 emitter cap, hubs ~1 per 2 km²) → building-footprint placement with tier downgrade. Integer/hex math throughout; every loop that matters walks an (R,Q)-sorted list, so output order is part of the determinism contract. The river is proven, not just plausible: `Tests~/TerrainTests.cs` flood-fills the map with all bridges standing (one connected walk) and again with every bridge destroyed (banks disconnect) — across 10 seeds, not one lucky one |
| `src/CityPreset.cs` | The three authored presets as **data** (docs/18 §1 table): `village` 1 km radial, `small_town` 2 km Main-Street grid, `big_city` 5 km dense grid — pitch, density, tier mix, landmark archetype rosters, river width, bridge/pond/hill counts |
| `src/CityModel.cs`, `src/BuildingTier.cs` | The output model (roads incl. bridge decks, water, ridges, buildings, landmarks with emitter-aura-3 / Collection-Station-5 radii, destructible `Bridge`s at the Large tier) and docs/18 §3's building tier table (300/2 · 600/4 · 1500/6 · 3000/8), verbatim |
| `src/Destruction.cs` | docs/18 §3's damage staging (`DamageStage`: Intact / Damaged at ≤50% HP / Destroyed at 0) and per-instance runtime HP (`BuildingRuntimeState`, `BridgeRuntimeState`) — immutable, `ApplyDamage` returns a new instance, same discipline as genome-core's surgery/mutation operators. No new combat math: whatever computes the damage amount is docs/04's existing formula, this only tracks the resulting HP |
| `src/BattlefieldState.cs` | Ties a `CityModel` to live building/bridge HP and derives what actually changes with damage: `BlockedToGround`/`BlockedToAmphibious` (a Destroyed building opens a flank route; a Destroyed bridge reverts to water, blocking ground but not amphibious plans) and `HighGround` (stage-independent — "a destroyed building's remaining structure grants the same +0.10 posMod," docs/04) |
| `src/EngagementZone.cs` | The docs/18 §5 LOD classifier: `EngagementZoneManager.ClassifyHex`/`ClassifyBuilding` sort any position into Engagement (~175 m, full sim) / LocalCity (~1 km, lightweight) / DistantSkyline (visual only) by distance to the nearest live engagement. Radii are `EngagementZoneConfig` data, not constants — "the radii are the first thing to retune" (docs/18), so retuning is a number, not a code change |
| `package.json`, `src/MadDr.CityGen.asmdef` | The UPM face of the package: Unity compiles `src/` from source as assembly `MadDr.CityGen` (`noEngineReferences: true`) |

Sample composition at seed 42 (the sanity anchor — regenerate any time,
same numbers forever): `village` 30% built, small-heavy (495/99/11),
2 emitters + 1 hub, 1-hex stream with 2 bridges; `small_town` 46% built,
6 emitters + 2 hubs, 2-hex river with 2 bridges; `big_city` 62% built,
large-heavy (5979/9084/3592), 10 emitters + 6 hubs, 3-hex river with 3
bridges.

## Dual-toolchain layout (why the tilde folders)

Unity imports **everything** in a `file:`-referenced package folder except
tilde-suffixed and dot-prefixed paths. This package is also a plain dotnet
project, so the two toolchains are kept out of each other's way:

- `Tests~/` — the xunit test project. Unity never sees it (it couldn't
  compile xunit anyway); dotnet runs it fine (a non-leading `~` is just a
  filename character).
- `bin~`/`obj~` — dotnet build outputs, redirected there by
  `Directory.Build.props`, so a locally-built `MadDr.CityGen.dll` can
  never be double-imported by Unity next to the compiled sources.
- Unity **generates `.meta` files in here** when the Editor opens the
  referencing project (local `file:` packages are mutable). That's
  expected — commit them.

## `src/` is C# 9, on purpose — read this before adding syntax

Unity's asmdef compiler caps at **C# 9** (confirmed against a real Editor
build: `error CS8773: Feature 'file-scoped namespace' is not available in
C# 9.0`) and does **not** do implicit usings, regardless of what this
package's own `.csproj` targets for the standalone `dotnet test` build —
Unity ignores that file entirely and compiles `src/*.cs` itself via
`MadDr.CityGen.asmdef`. `src/CityGenCore.csproj` pins `LangVersion` to
`9.0` and disables `ImplicitUsings` specifically so `dotnet test` fails on
the same things Unity would, instead of passing here and only breaking in
the Editor. Concretely, in `src/` (not `Tests~/` — Unity never compiles
that):

- Braced namespaces (`namespace X { ... }`), not file-scoped
  (`namespace X;` — C# 10)
- Plain `struct`/`class`, not `record`/`record struct` (C# 9/10) —
  hand-write `Equals`/`GetHashCode`/operators if you need value equality
- Explicit `using` directives at the top of every file — nothing is implicit

## Why a separate package, not inside genome-core

Different concerns, different lifecycles: `genome-core` is the
genotype/phenotype contract (schema-locked, normative,
[docs/06](../../docs/06-mutator-design.md)); this is spatial/battlefield
math that has nothing to do with a creature's genes. Keeping them
separate means a future Unity client references only what it actually
needs, and neither package's tests depend on the other compiling.

## What this package is not

- Not a Unity project: no scenes, prefabs, materials, or the uber-shader
  from [docs/08](../../docs/08-creature-visualization.md) — those are
  GUI/binary-authored assets that belong in an actual `.unity` project,
  not here.
- Not the match sim: this defines the *space* combat runs in (docs/18
  realizes docs/02–04's abstract hex grid in continuous 3D); resolving a
  fight is still [docs/04](../../docs/04-combat-model.md)'s job, unchanged.
- Not a building catalog: buildings are tier/footprint data, not the
  actual authored houses/hospitals/cathedrals a renderer would draw —
  that's the skin pass, deliberately out of scope here.
