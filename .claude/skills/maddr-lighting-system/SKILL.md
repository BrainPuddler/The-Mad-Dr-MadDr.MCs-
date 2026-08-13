---
name: maddr-lighting-system
description: Technical knowledge for MadDr.MCs's city lighting/fog/post-processing pipeline (LumenCycleController, DynamicLightBudget, GlowPointRegistry, EmissiveAnimator, CityLightingProfile) — hard-won gotchas from a long real-Editor debugging saga (docs/28, docs/12), the project's confirmed URP-only rendering pipeline (NOT HDRP — do not port HDRP-specific techniques like Volumetric Fog / Local Volumetric Fog / HDAdditionalLightData.volumetricDimmer without translating them to a URP-native equivalent first), and the URP-native "light source fog" approximation this project actually uses. Consult this BEFORE touching any of those scripts, before proposing a lighting/fog/bloom/post-processing technique from an external reference (article, tutorial, Stack Overflow answer), or when a creator report mentions lights/fog/bloom/ambient/shadows not doing what's expected.
---

# MadDr.MCs city lighting system — technical reference

This is the technical companion to `maddr-aesthetic-preferences` (that one
covers *taste*; this one covers *how the rendering pipeline actually
works and where it's already burned someone*). Primary sources: `docs/28`
(the living architecture doc + a running bug-history table, read its
§0.5 first) and the `docs/12` decision log (search "docs/28" for the
full blow-by-blow of every entry below). This skill is the condensed,
pattern-level version so you don't have to re-read the whole saga every
session — but when a specific number or exact mechanism matters, go to
the source doc, not this summary.

## 0. The pipeline is URP. Full stop.

**This project is Unity 6000.3.13f1 on the Universal Render Pipeline
(URP), not HDRP.** This is stated in CLAUDE.md and confirmed by
`ShaderUtil.FindRenderableShader()` trying `"Universal Render Pipeline/
Lit"` first, `LumenCycleController`'s Volume stack using
`UnityEngine.Rendering.Universal.Bloom`/`ColorAdjustments`/etc., and
every material in the codebase.

**Never propose or half-implement an HDRP-only technique here.** HDRP
and URP are different render pipelines with almost entirely non-
overlapping APIs for anything volumetric/advanced. Concretely, these
HDRP-only things DO NOT EXIST in URP and cannot be dropped in:

- `LocalVolumetricFog` component (the "Local Volumetric Fog" box you add
  via Hierarchy > Rendering in HDRP)
- `HDAdditionalLightData` and its `volumetricDimmer`/"Volumetric" toggle
  on a light
- HDRP's `Fog` Volume override's "Enable Volumetric Fog" checkbox and
  the froxel-based volumetric lighting it drives
- Any `UnityEngine.Rendering.HighDefinition` namespace type

**2026-07 concrete instance**: the creator linked "Creating Light-Source
Fog In Unity HDRP" (a Medium article by Vincent Taylor) as a reference
for "I want the lights to truly pop, bright and diffuse through the
fog," then immediately clarified "stay in URP tho" once the mismatch was
flagged. The right move when a reference like this comes up: identify
what VISUAL RESULT it's going for (a light source reading as a bright,
soft, spread-out glow that gets hazier in thicker fog), then find or
build the closest URP-native equivalent — not attempt a literal port,
and not silently ignore the reference either. See §2 for what that
equivalent looks like here.

**If URP's newer volumetric/light-scattering features ever become
relevant** (Unity has been slowly adding some volumetric capability to
URP in Unity 6+), verify the SPECIFIC Unity/URP package version actually
ships whatever API you're about to use before writing code against it —
don't assume feature parity with HDRP or with what a given Unity version
"probably" has by the time you're reading this.

## 1. The two-tier light model (docs/28 §0, unchanged since Phase 1)

Every glowing thing in the city is **Tier 1: emissive material**
(`_EmissionColor`, effectively free, unlimited count) by default. Only
the `DynamicLightBudget.budget` nearest-to-camera glow points (across
EVERY kind combined — streetlamps, windows, neon, marquee, all one
shared pool) get promoted to **Tier 2: a real `Light` component** (a
real per-pixel cost, budgeted). This is the standard "windows glow, they
don't each cast light" technique for a dense city at scale. Don't add a
real `Light` per prop without going through this budget system — that's
exactly the performance cliff docs/28 exists to prevent.

`GlowPointRegistry.Register(transform, tintColor, lightType)` is the
one entry point every dresser (`RoadDresser`, `BuildingDresser`) uses to
compete for the shared budget. `lightType` defaults to `Point`
(omnidirectional); pass `LightType.Spot` for a fixture that's aimed at
something specific (currently: the overhanging streetlight, aimed
straight down at the road via `DynamicLightBudget.SpotDownRotation =
Quaternion.Euler(90, 0, 0)` — Unity's forward-is-local-+Z convention,
same rotation math `LumenCycleController`'s sun uses).

## 2. "Light source fog" in URP — the actual mechanism here (2026-07)

No volumetric rendering. The approximation is entirely in the existing
post-processing Bloom stack plus fog-aware scaling of the real lights'
own intensity, and it's made of pieces that were sitting unused for a
long time before anyone wired them up — worth knowing about even outside
the fog context, since they're just genuinely useful Bloom parameters:

- **`Bloom.intensity`** — how much bloom gets ADDED. `bloomScale`
  multiplies the whole authored day/night curve; `fogGlowBoost` adds an
  EXTRA fog-density-driven multiplier on top (`1 + fogDensity *
  fogGlowBoost`). This is the "brighter" half.
- **`Bloom.scatter`** — how FAR/SOFT the blur spreads. This is the
  actual "diffuse" knob and was never touched anywhere in this codebase
  before 2026-07 (silently riding whatever URP's Bloom component
  defaults to, ~0.7). `bloomScatter` is the base value; `fogDiffusionBoost`
  ADDS to it based on current fog density (clamped to 1, since it's a
  normalized URP parameter, not an open multiplier like intensity).
- **`Bloom.threshold`** — the HDR brightness cutoff above which a pixel
  blooms AT ALL. Also had `overrideState = true` set since Phase 1 but
  its VALUE was never assigned anywhere — meaning it silently rode
  URP's own default (~0.9) the entire time. A high threshold means only
  a light's very brightest core blooms; lowering it (`bloomThreshold`,
  now explicit) lets more of the scene's own brightness "pop." This is
  the exact same failure pattern as everything in §4 below: an
  `overrideState = true` alone does nothing without also setting
  `.value` — don't assume a flag being true means the parameter is
  actually being driven anywhere.
- **`DynamicLightBudget.pointIntensityMax/Min` and
  `spotIntensityMax/Min`** — the REAL light's own core intensity gets
  DIMMED (not extinguished — `Min` stays above 0) as fog thickens,
  blended by `fogDimReferenceDensity` (current `RenderSettings.
  fogDensity` normalized 0..1 against it). This is the closest URP
  analog to "fog absorbs a light source" without true volumetric
  scattering.

**Ceiling on this approach, honestly stated**: it's a 2D screen-space
trick. It cannot produce true light shafts, cannot occlude a light's
glow by actual 3D fog density between camera and source, and every
light dims by the same flat global fog factor regardless of how much
fog is actually between it and the camera. It reads as "hazier bloom,"
not "light visibly diffusing through a fog volume." If a report keeps
coming back to wanting the LATTER specifically, that's a sign this
ceiling has been hit — see §3.

## 3. Real volumetric fog for URP — evaluated, not integrated (2026-07)

**github.com/mseonKim/URP-VolumetricFog-ForwardPlus** — a genuine
froxel-based volumetric fog system for URP (ported from HDRP's
approach, unlike the HDRP-only article in §0 this is actually usable
here). Researched in depth when the §2 Bloom approximation's ceiling
was reached; NOT currently integrated — the creator chose to stick
with §2's free approach for performance reasons. Documented here so a
future session doesn't have to re-research it from scratch if that
decision changes.

**Confirmed compatible, concretely** (not just "should work"):
- `Assets/Settings/PC_Renderer.asset` has `m_RenderingMode: 2`
  (ForwardPlus) — the package's hard requirement, already satisfied on
  the PC target. `Mobile_Renderer.asset` is `m_RenderingMode: 0`
  (plain Forward) — this package would be **PC-only**; Mobile keeps
  whatever fog system is active there, consistent with this project's
  existing PC/Mobile tiering elsewhere (e.g. the Per-Vertex lighting
  fix applied to both, but plenty of other settings already differ per
  tier).
- Package needs `com.unity.render-pipelines.universal` >=14.0.8; this
  project is on `17.3.0`. It has a RenderGraph-specific code path
  "only implemented for Unity 6" — this project IS Unity 6000.3.13f1,
  so that's the path that actually runs.
- License: Unity Companion License — compatible with use in a Unity
  project, no conflict.
- Integration surface, if this is ever picked back up: add the package
  to `Packages/manifest.json` (safe, ordinary Package Manager
  resolution); the `FPVolumetricFog` `ScriptableRendererFeature` has
  exactly one serialized field (`renderPassEvent`) but STILL has to be
  added to `PC_Renderer.asset` via the Editor's "Add Renderer Feature"
  button, not hand-authored YAML — that asset backs the whole pipeline
  and a blind edit risks corrupting it with no Editor here to verify
  against; the `FPVolumetricFogVolume` Volume Component has a rich,
  well-designed parameter set including `enablePointAndSpotLight`/
  `localScatteringIntensity`, meaning it's built to have actual local
  lights (this project's `DynamicLightBudget`-promoted real lights)
  genuinely scatter into the fog volume — the real version of what §2
  approximates.
- Real GPU cost, not free like §2 — froxel volumetric fog (a MaxZ pass
  + a volumetric lighting pass + denoise, every frame) has a genuine
  per-frame budget line, tunable via `screenResolutionPercentage`
  (default 12.5%) and `volumeSliceCount` (default 128) down to a
  cheaper-but-still-genuinely-volumetric middle ground if performance
  allows some but not full-quality cost.

**If this gets picked back up**: don't ship it at default quality
settings without discussing the performance tradeoff again explicitly
— the creator's stated reason for not integrating it now was
performance, not quality or feasibility.

## 4. The recurring bug pattern: "a property was never explicitly set, so it silently used SOME default"

This is, by a wide margin, the single most common root cause across the
entire docs/28 saga. Every one of these looked like "the light/fog/
ambient system is broken" from the outside and was actually "nobody
ever told Unity what value to use, so it picked its own." Check for this
FIRST before assuming a numeric/logic bug:

- **`RenderSettings.ambientMode`** was never set anywhere → defaulted to
  `Skybox` → every `RenderSettings.ambientLight` assignment in
  `LumenCycleController` was silently discarded (Unity only reads
  `ambientLight` in `Flat` mode). Fixed by setting `ambientMode = Flat`
  once in `Start()`.
- **URP Pipeline Assets' Additional Lights Rendering Mode** was **Per
  Vertex**, not **Per Pixel** — on a city built entirely from low-poly
  primitives (a ground quad can have 4-8 vertices), a point light's
  contribution can evaluate to near-zero across most of a face even
  when perfectly configured, because per-vertex lighting only samples
  at vertices.
- **`Mesh.RecalculateNormals()` after double-winding every triangle**
  (a deliberate anti-mistake guard in `ProceduralMeshKit`) → +N and -N
  per face cancelled to EXACTLY zero at every shared vertex → `dot(N,L)
  == 0` for any light, ever, regardless of how bright/close/numerous.
  Two props (the ornate lamppost, the market stall canopy) rendered
  pure black no matter what lighting fix was tried, because the bug was
  never in the lighting at all.
- **`Bloom.threshold`'s `overrideState = true` with no `.value` ever
  assigned** (this session, see §2) — same shape as the two above:
  a flag/mode being set doesn't mean the underlying parameter is
  actually being driven.
- **`_Smoothness`/`_Metallic` never set on ANY material** in
  `RoadDresser`'s `M()`/`MTextured()` helpers → every prop, including
  the road, rendered at URP/Lit's own default smoothness (~0.5) →
  streetlights couldn't produce a tight specular glint on the pavement
  no matter how bright/correctly-positioned they were.

**The check to run**: when a report says "X isn't happening even though
I tuned the number that should control X," ask whether the underlying
Unity property that number feeds actually has a value flowing into it
at all — versus whether the code path even executes, versus whether
some OTHER unset property is silently gating the whole system. In this
project specifically, checking the actual serialized scene file
(`Assets/Scenes/SampleScene.unity`, grep for `m_FogMode`/`m_AmbientMode`/
etc.) for what a `RenderSettings` sub-property is ACTUALLY set to beats
guessing from Unity's documented "default" — a scene can carry its own
serialized override that differs from a fresh-scene default.

## 5. Verification discipline (no Editor exists in this environment)

Every fix in docs/28 was reasoned from creator-supplied screenshots/
console output/live Inspector values, not directly observed — and
guessing wrong here has a real, demonstrated cost: at least three
rounds in this saga were a fix that then caused (or failed to fix) the
next reported symptom, requiring a correction pass. The pattern that's
worked to catch this before shipping:

- **Numeric/logic claims**: build a small standalone flightcheck-style
  harness (see the scratchpad pattern used throughout docs/12 — a
  minimal `UnityStub.cs` compiled alongside the real source files,
  `dotnet build`/`dotnet run` outside Unity) and verify the ACTUAL
  compiled method's behavior, including private ones via reflection,
  rather than trusting hand-derived math. This caught real bugs
  (the ornate lamppost's normals genuinely were all zero) AND real
  false negatives from broken test-harness stubs (`Mathf.Floor`/
  `SmoothStep`/`Lerp`/`Clamp01` were themselves `return 0f;`/no-op
  stubs at various points — always suspect the harness before
  concluding shipped code is wrong when a check unexpectedly fails).
- **Pure visual/shader claims** (specular response, bloom "feel," fog
  thickness) have no meaningful pass/fail math — say so explicitly
  rather than inventing a fake numeric verification for them, and be
  clear the change is unconfirmed until seen in a real render.
- **When genuinely uncertain about a design tradeoff** (should fog dim
  lights, thicken overall, or both? how aggressive?), ask via a
  clarifying question instead of guessing a third numeric value in a
  row — this project's creator has been receptive to that and it's
  cheaper than another correction round.

## 6. Destruction and the light system don't talk to each other (2026-08)

Two separate gaps found while investigating "destroyed collapsed
building do not have lights," both in `RuntimeCityBuilder.
ApplyBuildingDamage`'s Destroyed branch — worth knowing before touching
either the destruction path or the light-registry self-prune logic
again:

- **Rubble itself had zero emissive surfaces.** Neither `rubbleMat` nor
  the scorch-decal material (`SpawnScorchDecal`) ever called
  `EnableKeyword("_EMISSION")`/`SetColor("_EmissionColor", ...)` — only
  `.color`. Combat fire on a Damaged building is destroyed outright with
  the massing cube the instant it collapses, never converted to
  lingering embers. Fixed: `DamageFx.CollapseEmbers` /
  `CollapseEmbersFx` now scatter a handful of small warm-orange emissive
  spheres across the collapse point (same `EnableKeyword`/`SetColor`
  technique as `BuildingDresser`'s window materials — §1's Tier 1
  emissive-material approach, not a real `Light`), pulsing out of phase,
  fading and self-destroying after 90s. Not routed through
  `EmissiveAnimator`'s batched system — a handful of embers per collapse
  is nowhere near the "hundreds of windows" scale that exists to batch
  for; see `RubblePileFx` right next to it in `DamageFx.cs` for the same
  "own Update(), own fade, self-destroy" shape.
- **Real latent bug, found but NOT fixed (flagged in docs/12 instead):**
  a destroyed building's dressing-holder cubes — the ones carrying
  `BuildingDresser`'s window strips, each individually
  `EmissiveAnimator.Register`'d/`GlowPointRegistry.Register`'d — are
  never `Object.Destroy`'d on collapse, only squished to 12% height with
  every child renderer's material force-overwritten. Both registries'
  self-prune logic (§ elsewhere: `if (e.Renderer == null)` in
  `EmissiveAnimator.cs`, similarly in `DynamicLightBudget.cs`) never
  fires, since the renderer survives — just recolored — so every former
  window on every destroyed building keeps ticking in
  `EmissiveAnimator.Tick()` and can occupy a `DynamicLightBudget`
  real-light slot, invisibly, for the rest of the match. Confirmed this
  produces no visible light (so it's not what any creator report would
  actually describe) — a pure CPU/light-budget hygiene issue. Next
  person touching destruction or the registries: either
  `Object.Destroy` those renderers on collapse (same as massing cubes
  already get) or add a proper `Unregister` to both registries.

## 7. Converting a real-depth Cube prop to a flat quad silently loses TWO things, not one (2026-08, docs/33)

`BuildingWindowGrid`'s conversion of per-window Cube GameObjects to
merged-mesh flat quads (docs/33) shipped with two silent regressions,
found in two separate rounds because the first fix wasn't the real
cause — worth knowing before converting any other Cube-based prop to a
flat quad the same way:

1. **The depth-derived outward clearance from the wall.** A Cube
   centered at `pos` with real depth naturally clears whatever's behind
   it by half that depth; a flat quad at the same `pos` doesn't, and can
   z-fight with it. Two of three `AddWindow` call sites lost this.
2. **Consistent triangle winding.** A Cube's culling doesn't care which
   way you built it — SOME face always points outward correctly. A flat
   quad's winding depends entirely on the caller's tangent handedness
   matching its own normal, and two of the three call sites hand it a
   tangent that's only correct for ONE of a pair of opposite-facing
   walls (same `Vector3.right` for both `Vector3.forward` and
   `Vector3.back`, etc.) — so on whichever side doesn't match, the quad
   is back-face CULLED: not dim, not glitchy, entirely absent. This was
   the actual cause of a creator-reported "dots, not rectangles" (what
   remained visible was the pre-existing, unrelated real-light halos).
   Fixed with `Cull Off` on the shader rather than a hand-derived
   winding correction — deliberately the lower-risk choice with no
   Editor here to render-check a CW/CCW guess against; a window pane
   has no legitimate back-viewing-angle anyway.

Full diagnosis of both rounds: docs/33 §7 (first fix, real but not the
dominant cause) and §8 (the actual cause). If you're converting another
Cube prop to a quad, check both of these up front instead of rediscovering
them one at a time.

## When you're not sure

Read `docs/28` §0.5 (the bug-history table) top to bottom before
touching `LumenCycleController`/`DynamicLightBudget`/`GlowPointRegistry`/
`EmissiveAnimator`/`CityLightingProfile` — nearly every non-obvious
number or design choice in those files has a "why" written down there in
the creator's own words, and the table's Status column tells you what's
actually confirmed working versus still-unverified.
