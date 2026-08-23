# 38 — Cityscape Rendering Index

A file/class index of everything that turns an **already-decided** city
layout into actual Unity visuals — meshes, materials, lighting, VFX. It
deliberately excludes **generation**: the code that decides *what* to
build and *where* (procedural road/plot layout, terrain-shape decisions,
building placement/footprint/tier). See the "Excluded" section below for
the explicit boundary, and [18-city-battlefields.md](18-city-battlefields.md)
for the generation side.

No prior doc grouped rendering separately from generation (checked
[00-index.md](00-index.md) and grepped every doc for
`rendering`/`dressing`/`cityscape`/the class names below) — this is a
new index, not a duplicate. Several existing docs cover one rendering
subsystem in depth; this doc is the cross-cutting map between them, not
a replacement:
[21-world-upgrade-report.md](21-world-upgrade-report.md),
[28-city-lighting-system.md](28-city-lighting-system.md),
[29-fire-propagation-system.md](29-fire-propagation-system.md),
[31-faction-building-architecture.md](31-faction-building-architecture.md),
[33-window-glow-gpu-system.md](33-window-glow-gpu-system.md).

All paths below are under `unity-client/Assets/Scripts/` unless noted.

## Building / road / bridge / rubble dressing

| File | Renders |
| --- | --- |
| `BaseDresser.cs` | Per-faction RTS building visuals (Factory/Control Centre/Hq/Big Brain) — primitive-kit dressing, faction windows/trim/materials, owner-color tinting. |
| `BuildingDresser.cs` | Procedurally-generated civilian city's building dressing — `DressSmall`/`DressIndustrial`/`DressApartment`/`DressOffice`, roofs, windows, doors, signage, facade-grammar dispatch. |
| `RoadDresser.cs` | Street-network visuals — road pads, sidewalks, curbs, lane paint, crosswalks, roundabouts, rail siding, street furniture. |
| `BridgeDresser.cs` | Bridge visuals — guardrails, truss arch, piers. |
| `RubbleDresser.cs` | Destroyed-building rubble silhouette. |
| `TramDresser.cs` | Embedded streetcar rail visuals (New York region preset). |
| `FacadeKit.cs` | Turns a solved `FacadeModule` (decided by citygen-core's `FacadeGrammar`) into actual mesh geometry on a building face. |

## Meshes / props

| File | Renders |
| --- | --- |
| `ProceduralMeshKit.cs` | Hand-authored placeholder geometry (tapered pole, lean-to awning). |
| `PropLibrary.cs` | Mesh-by-key lookup with primitive fallback, used by every dresser module. |
| `LowPolyFireMeshKit.cs` | Procedural flame-tongue mesh generation for the low-poly fire system. |
| `KnockableProp.cs` | Runtime tip/topple animation for already-placed street furniture. |

## Lighting / emissive / glow system (docs/28, docs/33)

| File | Renders |
| --- | --- |
| `LumenCycleController.cs` | Four-phase Dawn/Day/Dusk/Night visual lighting cycle, URP post stack — Unity-layer presentation only. |
| `DynamicLightBudget.cs` (also hosts `GlowPointRegistry`) | Spends a shared city-wide real-light budget on the nearest glow points; `GlowPointRegistry` is the shared registry every glowing prop registers into. |
| `CityLightingProfile.cs` | ScriptableObject of every tunable lighting number (streetlamps, windows, neon, marquee). |
| `EmissiveAnimator.cs` | Batched per-instance emissive animation (window/neon/marquee on-off-fade) via `MaterialPropertyBlock`. |
| `NeonRegistry.cs` | Shared registry of "neon" materials so day/night dimming can reach them without dressers knowing about lighting. |
| `BuildingWindowGrid.cs` + `Assets/Shaders/WindowGrid.shader` | GPU-batched window-grid rendering (docs/33) — one draw call per building, per-window lit/dark state. |
| `EerieChamberGlow.cs` | Big Brain jar's pulsing point light + emissive glow. |
| `BrainJarBubbles.cs` | Big Brain jar rising-bubble visual effect. |
| `TeslaArc.cs` | Mad Doctor Control Centre's Tesla-coil arc effect. |
| `BrainTextureKit.cs` | Procedural normal/height/AO/roughness detail for the Big Brain jar material. |

## Materials / textures

| File | Renders |
| --- | --- |
| `PbrTextureAtlas.cs` | Procedurally-generated PBR texture atlas (brick, limestone, wet asphalt, chrome, painted metal, glass) sampled by `BuildingDresser`/`RoadDresser` materials. |

## Damage/destruction visual feedback

| File | Renders |
| --- | --- |
| `DamageFx.cs` | Smoke plume on damaged buildings, dust burst on collapse — reacts to already-decided damage state. |
| `DamageFxProfile.cs` | ScriptableObject of fire/smoke size tuning knobs for `DamageFx`. |

## Terrain (presentation layer over generator data)

| File | Renders |
| --- | --- |
| `TerrainField.cs` | Deterministic presentation-side elevation that sculpts the terrain the generator already decided (`CityModel.Ridges`/`Water`) into a smooth height field — does not decide *where* ridges/water are. |

## Minimap / scene-view visualization

| File | Renders |
| --- | --- |
| `Minimap.cs` | Bakes the generated city (roads/water/ridges/buildings/landmarks) into a texture once; plots live unit blips, fog-of-war, camera-frustum indicator. |
| `CityGizmo.cs` | Editor-only scene-view gizmo visualization of a generated city — dev/debug visualization, no runtime cost. |

## Mixed file — `RuntimeCityBuilder.cs`

The largest source of ambiguity in the codebase; do not treat the whole
file as one category. Rendering-only methods: `BuildGround`,
`BuildTerrainMesh`, `BuildTableEdge`/`SpawnEdgeBar` (decorative table
rim), `CombineStaticRoadSurfaces`, `HexFanMesh`, `RiverFlow`,
`PondHexes`, `SpawnLilyPads`, `SpawnCattails`, `SpawnTree`, `SpawnRocks`,
`ApplyWorldScaledTiling`, `SpawnPrim`, `ApplyMatteFinish`, `SpawnMesh`,
`CombineDistantSkylineDressing`, `DeBatchBuildingDressingIfNeeded`,
`BuildLandmarkAuras`, `BuildBridges` (delegates to `BridgeDresser`),
`SpawnCube`, `NewMaterial`, `NewTexturedMaterial`, `SpawnScorchDecal`.

Mixed at the call-site or line level:
- `BuildTerrainAndRoads` — calls rendering (`BuildWater`,
  `ScatterVegetation`, `RoadDresser.Build`) over hex sets that were
  already decided by citygen-core before this method runs.
- `BuildBuildings` — the `foreach` over `_city.Buildings` reads an
  already-placed list (position/footprint/tier fixed by citygen-core),
  but everything inside the loop is rendering: tier/district material
  selection, `SpawnCube` massing, the `BuildingDresser.Dress(...)` call.
- `ApplyBuildingDamage` — the opening lines are gameplay simulation (HP
  decrement, battlefield-state update, pathing unblock); the remainder
  is pure rendering (rubble swap via `RubbleDresser.Shatter`, dressing
  transform squish, material swap, debris chunks). Split at that
  boundary, don't take the whole method as one category.

Everything else in the file — traffic AI, combat/steering, resource
wallet, worker/collector/army spawning, roof-slot bookkeeping — is
simulation/orchestration, not rendering, and is excluded.

## Excluded — generation, not rendering

Adjacent files someone might mistake for this list:

- `packages/citygen-core/src/CityGenerator.cs` — the actual procedural
  generator: seeded terrain, road-network growth, block subdivision,
  landmark allocation, building-footprint placement. No Unity/rendering
  dependency at all.
- `packages/citygen-core/src/CityModel.cs` — the generated data model
  every renderer above consumes.
- `packages/citygen-core/src/CityPreset.cs` — road-pattern/preset data
  driving generation, not visuals.
- `packages/citygen-core/src/FacadeGrammar.cs` — decides *which* facade
  module goes on a wall; its rendering counterpart is `FacadeKit.cs`
  above.
- `packages/citygen-core/src/EngagementZone.cs` — LOD/engagement-zone
  radius classification consumed by `RuntimeCityBuilder.BuildBuildings`
  to decide which dressing gets batched — a decision, not rendering.
- `RuntimeCityBuilder.cs`'s non-visual methods (see above).
- `BuildingIdentity.cs` — just an `EntityId` raycast-identity component;
  no visual output.
- `BuildingFactionSkin.cs` — HUD display names + a reused color
  reference; text/UI skinning, not 3D geometry/material output.
- `BuildingIconKit.cs` / `BuildingNavHud.cs` — bake/display 2D HUD icons
  per building kind; HUD icon rendering, not cityscape 3D dressing.
- `WindowLightsHud.cs` — a single OnGUI toggle button; a HUD control,
  not a renderer.
- `LumenHud.cs` — gameplay-stat HUD (phase clock, mana bar, capture
  progress), not city visual dressing.
- `TrafficCar.cs` — traffic movement/AI simulation (references
  `GlowPointRegistry` for taillights, but the file itself is behavior).
