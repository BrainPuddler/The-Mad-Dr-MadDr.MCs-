# 21 — World Architecture Report & Miniature-Set Upgrade

*Phase 1 deliverable of the "B-movie miniature world" upgrade (creator
brief, 2026-07). Read before touching the presentation layer.*

The goal restated: **"a 1950s monster movie set that happens to be an
RTS battlefield"** — not photorealism, and never at the cost of the
gameplay invariants below.

---

## 1. How the world actually gets built today

The pipeline is **two cleanly separated layers**, and every upgrade in
this doc preserves that separation:

```
packages/citygen-core  (engine-agnostic C#, deterministic, 140+ tests)
  CityPreset      Village / SmallTown / BigCity knobs -- including
                  HillCount/HillRadiusHexes, RiverWidthHexes, PondCount
  CityGenerator   seed -> CityModel: Roads, Water (river+ponds), Ridges
                  (the docs/04 HIGH-GROUND hills -- gameplay data!),
                  Buildings (footprint + tier + archetype), Bridges,
                  Landmarks
  BattlefieldState / Destruction   runtime HP, BlockedToGround/
                  Amphibious, HighGround -- pathing + combat truth
  HexPathfinder   deterministic A* over blocked sets

unity-client/Assets/Scripts  (presentation + agents)
  RuntimeCityBuilder  THE hub. Start(): Generate(seed) ->
      BuildGround()            one flat Unity Plane (collider = what
                               ground right-clicks hit)
      BuildTerrainAndRoads()   water = blue cubes (y -0.4, painted-on),
                               ridges = green 3m blocks, roads = dark
                               0.2m slabs, one cube per hex
      BuildBuildings()         ONE CUBE PER FOOTPRINT HEX, height by
                               tier (6/12/30/40m), tier-colored; cube
                               collider is the click/damage handle
                               (_buildingByCollider, _cubesByBuilding)
      BuildBridges()/SpawnCitizens()/SpawnTanks()
  MonsterAgent/MonsterBody     orders, gait (no-skate feet), flight
  Tank, Citizen, WeaponFx, HealthBars, camera/commander/HUD
```

**There are no prefabs, no ScriptableObjects, no scene content** — the
entire world is code-generated at Play time from primitives, styled by
`ShaderUtil.FindRenderableShader()` (URP/Lit) materials. That is this
project's actual "prefab workflow," and the upgrade follows it: new
dresser classes generate primitives deterministically rather than
introducing an asset pipeline nothing else uses.

## 2. Load-bearing invariants (break these and the game breaks)

1. **Determinism**: same seed + preset ⇒ identical city. All visual
   dressing must derive from `(seed, hex)` integer hashes — the codebase
   idiom is `(hex.Q * 31 + hex.R * 17) % n` — never `UnityEngine.Random`.
2. **The ground is y = 0** in *gameplay* terms. This session built an
   entire flight system on it: `MonsterBody` plants world-locked feet on
   a `_groundY` plane, perch/descent logic keys off
   `SurfaceHeightAt`/`HeightForTier` **absolute roof heights (6/12/30/40
   from y=0)**, the camera's middle-drag and ground clicks intersect the
   y=0 plane/collider. The four-iteration gait saga (docs/12) lives here.
3. **Building cubes are the interaction surface**: their colliders map
   clicks→Building and their transforms are mutated by damage (Damaged =
   tint, Destroyed = crush to 12% height rubble). Dressing must inherit
   that fate, not dodge it.
4. **Roads/water/ridges are hex sets consumed by pathfinding** — visuals
   may reinterpret them but never contradict passability.

## 3. Current limitations (what the brief correctly calls out)

- Board is a flat plane; the *generator's own hills* (`Ridges`) render
  as green blocks nobody reads as terrain.
- Water is a floating blue cube — painted-on, no bed, no banks.
- Buildings are single tier-colored boxes: no silhouettes, no rooftop
  clutter, no 1950s language, weak damage read.
- Roads are disconnected dark squares: no continuity, sidewalks,
  markings, or street furniture. No parked cars, poles, hydrants.
- Zero mid-ground detail at RTS camera height; the "miniature set"
  read is absent.

## 4. The plan (and the one big thing we deliberately do NOT do)

**Elevation is presentation-driven but unit-honest**: a deterministic
height field with units terrain-following it — while every *gameplay-
vertical* surface stays flat-locked at 0. The rule that makes this
tractable:

> **Buildings plots, roads, and bridges sit on ground flattened to
> exactly y=0. Only open terrain rolls. Water carves below 0 on its own
> (already-impassable) hexes.**

Consequences: roof heights, flight tiers, descent floors, perch logic,
bridge decks, rubble, and building colliders **all keep their existing
math unchanged**. Units (monsters/tanks/citizens) gain a one-line
terrain-follow; `MonsterBody` gains a ground *sampler* so feet plant on
the slope under each foot (a three-site touch to the gait, not a
rewrite). The click plane stays a flat invisible collider at y=0 —
hills are ≤ ~3 m, so worst-case click skew is a fraction of a hex; the
camera's analytic y=0 intersection stays valid.

What we deliberately do NOT do this pass: gameplay-affecting slope
costs / hill movement penalties (docs/04 already models high ground as
the `Ridges` posMod set — visual sculpting now matches that data), a
Unity `Terrain` object (wrong tool for a code-generated deterministic
board), or nav changes.

**Phase 2 — Terrain & water** (`TerrainField.cs` + `RuntimeCityBuilder`)
- Per-hex height targets: ridge hexes ≈ +3 m smooth mounds (matching
  the old block height, so the high-ground read is preserved), water
  hexes ≈ −1.4 m carved beds, flat-locked hexes 0, open ground gentle
  2-octave value-noise rolls (≤ ~1.5 m). Inverse-distance smoothing
  across a hex and its neighbors turns the per-hex targets into
  continuous banks, shorelines, and hill skirts.
- Chunked generated ground meshes (resolution auto-scaled by map size)
  replace the plane's *visual*; the plane collider survives, invisible.
- Water becomes thin translucent slabs sunk into the carved beds —
  physically *in* the world, banks visible above the surface.
- Miniature-set vegetation: puffball model trees on ridges and rare
  open ground, bushes on pond/river shores. Deterministic scatter.

**Phase 3 — 1950s buildings** (`BuildingDresser.cs`)
- Keeps the collider cube as the massing core; adds a dressing holder
  registered into `_cubesByBuilding` so crush/tint damage applies.
- Tier styles: Small → suburban gables / gas-station canopies / diner
  chrome+sign; Medium → brick apartments with window bands, cornices,
  fire escapes; Large → deco setback towers with pilasters; Landmark →
  archetype-aware (spired church, columned town hall, marquee'd movie
  house on the plaza, arched rail depot, red-cross hospital).
- Rooftop kit everywhere: water towers, antenna masts, vents,
  billboards. 1950s palette (brick, cream, seafoam, mustard, chrome)
  with a per-tier trim accent so RTS tier-reading survives. Shared
  cached materials (SRP-batcher friendly); ≤ ~15 primitives/building.

**Phase 4 — Roads** (`RoadDresser.cs`)
- Hub-and-spoke tiles: a center pad plus a connector strip toward each
  road/bridge neighbor — straights, corners, T's, X's, and dead ends
  *emerge from adjacency*, seamlessly, with no tile catalog to break.
- Sidewalk trim, yellow center dashes, crosswalk stripes at ≥3-way
  intersections; deterministic street furniture: streetlights,
  telephone poles, hydrants, trash cans, and pastel tail-finned 1950s
  parked cars. All colliderless (clicks fall through to the ground).

**Verification** (no Editor in this environment): the entire gameplay
layer stub-compiles against the real citygen-core/roster-client/
creature-mesh DLLs; terrain math is pure C# reviewed for determinism;
visual tuning constants are isolated for one-line Editor adjustment.

## 5. Known accepted tradeoffs

- Clicks on tall hills can skew a few meters toward the camera (flat
  click plane). Acceptable at ≤3 m amplitude; a MeshCollider swap is a
  one-liner later.
- Street furniture is cosmetic: units and monsters pass through it
  (genre-appropriate for a miniature set being stomped; a knock-down
  pass is future work).
- BigCity preset (250×250) gets coarser terrain resolution and heavier
  prop counts; Village/SmallTown are the tuned targets this pass.
- Bridge decks still float slightly above the carved river (pre-existing
  visual; a proper bridge dress-up is queued in §6).

## 6. Next 10 world upgrades (after this pass)

Batch 2 (2026-07, see docs/12 decision log) shipped items 1, 2, 3, 5, 7,
8, and 10. Deferred: 4 (needs Editor eyes for lighting), 6 (not scoped
this batch), 9 (a full standalone traffic feature).

1. ~~Bridge dressing: trusses, piers into the riverbed, road continuity.~~ **Done** (`BridgeDresser.cs`).
2. ~~Knock-downable street furniture (poles/cars react to monster steps).~~ **Done** (`KnockableProp.cs`).
3. ~~Damage smoke/fire plumes on Damaged buildings; dust puff on collapse.~~ **Done** (`DamageFx.cs`).
4. Neon night mode: emissive signage pass + dusk lighting preset.
5. ~~Rubble piles with silhouette (not just crushed cubes).~~ **Done** (`RubbleDresser.cs`).
6. Railyard/industrial district dressing keyed to `rail_depot`.
7. ~~Billboard art: period-poster quads ("ATOMIC COLA", movie one-sheets).~~ **Done** (`BuildingDresser.DressPoster` + `RoadDresser` roadside boards).
8. ~~Miniature-set border: table edge / painted-backdrop skybox ring.~~ **Done** (`RuntimeCityBuilder.BuildTableEdge`).
9. Citizen vehicles that drive road hexes and flee (docs/19 traffic).
10. ~~Per-district palettes (downtown vs suburbs) from road-graph radius.~~ **Done** (massing tint only; dressing-level palette bias still open).
