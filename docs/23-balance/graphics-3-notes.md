# docs/23 Phase 10.3 (Materials) — shipped vs deferred

## Shipped

`PbrTextureAtlas.cs` procedurally generates six small (64×64) placeholder
`Texture2D`s — brick, limestone, asphalt-wet, chrome, painted metal,
glass — entirely in code, no imported asset. Each is a coarse stand-in
for the real material's defining visual trait, not a finished look:

- **Brick**: horizontal coursing with a mortar grid, staggered joints on
  alternating rows, and per-brick color jitter for weathering.
- **Limestone**: two-octave mottling (coarse + fine) for a soft
  weathered-stone look instead of pure per-pixel noise.
- **Asphalt-wet**: fine grain plus a handful of brighter horizontal
  "streak" rows standing in for a real wet-street reflection shader.
- **Chrome**: vertical brushed-metal bands — a cheap substitute for real
  environment-reflection streaks, since there's no reflection probe
  setup to lean on here.
- **Painted metal**: sparse scratch pixels plus a faint rivet-dot grid.
- **Glass**: a diagonal sheen band standing in for a specular highlight;
  no real transparency (stays an Opaque material — this project has no
  transparent-material convention yet, and adding one is a bigger,
  separate decision than this sub-phase's scope).

Wired into the **existing** dresser material functions (per the plan's
own framing — "dressers keep their geometry logic, gain material
richness"), zero geometry/call-site changes: `BuildingDresser.Brick()`/
`Concrete()` (limestone)/`Chrome()`/`WindowBand()` (glass), and
`RoadDresser.Asphalt()`/`ChromeTrim()`/`PoleMetal()` (painted metal).
Every other existing flat-color material (`Cream`, `Seafoam`, `Mustard`,
`LanePaint`, `CrossPaint`, etc.) is untouched.

## Deliberate simplification

**No per-object UV tiling scaled to world size.** Unity's built-in
primitive UVs aren't world-scale-aware — the SAME 0..1 UV rect stretches
across a 1m curb prop or a 30m building wall equally. Getting brick
coursing to read at a consistent real-world scale across wildly
different prop/building sizes needs per-instance tiling (a
`MaterialPropertyBlock` computed from each object's own `localScale`),
which would touch every `SpawnPrim` call site across both dresser files
— out of scope for a placeholder pass. Instead every textured material
gets one fixed tiling scale (3×3) regardless of the face it lands on.
Flagged here rather than silently implying it's scale-correct.

## Deferred

- A transparent/glass render path (real alpha blending) — this project
  has no transparent-material convention yet; adding one is a bigger,
  separate decision than a placeholder texture swap.
- Per-instance world-scale-correct tiling (see above).
- Real authored PBR maps (normal/metallic/roughness) — everything here
  is a base-color (`_BaseMap`) texture only; no normal maps, no
  metallic/smoothness maps. `ColorAdjustments`/lighting from Phase 10.1–2
  are doing the "richness" work maps would otherwise carry.

## Verification

`flightcheck` compiles clean (added `Texture.wrapMode`/
`TextureWrapMode`, `Material.SetTexture`/`SetTextureScale` to the
harness stub — the exact surface this code calls). **Not visually
verified** — no Unity Editor exists in this environment.
