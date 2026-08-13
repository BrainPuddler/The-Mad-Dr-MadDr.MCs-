# 33. Window glow GPU system

**Status: implemented, unverified in a real Editor** (this environment has
none -- see §5). Replaces docs/28's per-window Cube-GameObject window
rendering (`BuildingDresser.SpawnWindowStrip`, `FacadeKit.WindowBay`/
`OrielBay`) with a GPU-driven system: one merged mesh + one shared material
per building, individual windows still independently controllable, no
per-window GameObject/Renderer/Light.

## 0. Why (creator ask, condensed)

Replace the window-glow *rendering* implementation for performance at
BigCity scale (thousands of buildings, tens of thousands of windows) while
preserving individual per-window ON/OFF control -- explicitly NOT "make
every window permanently emissive" or "remove per-window control." The
task framed this as an HDRP request; this project is confirmed **URP**
(CLAUDE.md, `.claude/skills/maddr-lighting-system` §0), so this doc and
implementation are the URP-native equivalent of what was asked, not a
literal port.

## 1. What the old system actually cost (audit)

Every "lit" or "dark" window strip/bay was `RuntimeCityBuilder.SpawnPrim`'s
own `GameObject.CreatePrimitive(Cube)` -- a real GameObject with
MeshFilter + MeshRenderer (the Collider is stripped, but the rest isn't).
At BigCity scale this is confirmed "tens of thousands" of these
(`FacadeKit.cs`'s own pre-existing comment). Two costs stacked on top of
that base GameObject count:

- **Draw calls**: even with a shared, SRP-batcher-friendly Material (this
  codebase already did that part right -- see docs/28 §3), each window was
  still a separate Renderer, so a building with dozens of windows was
  dozens of draw calls, not one.
- **CPU tick cost**: every *lit* window got an `EmissiveAnimator.Register`
  call (`LightBehaviorKind.Window`) -- a per-instance `MaterialPropertyBlock`
  walked every frame by `EmissiveAnimator.Tick()` (distance-culled, but
  still a real per-registered-instance cost for anything in range) to
  compute its own randomized day/night occupancy schedule + flicker wobble
  in C#.

Genuinely **not** a bug this doc is fixing: the old system already used
the correct two-tier model (docs/28 §0) -- emissive material for
everything, a real `Light` for only `DynamicLightBudget.budget` (~64)
nearest-camera points citywide, across every glow kind combined. No
"HDRP Light per window" mistake existed here to begin with; that part of
the task's framing didn't apply to this codebase.

## 2. Architecture

```
BuildingDresser / FacadeKit (unchanged call sites, new payload)
        |  AddWindow(pos, tangent, normal, w, h, seed, canGlow, color)
        v
BuildingWindowGrid (one per building dressing-holder GameObject)
        |  Build(): merges every queued window into ONE Mesh + a small
        |  per-building "override state" Texture2D (default-value 0.5
        |  == "no override, use ambient schedule")
        v
ONE MeshRenderer, ONE shared Material ("MadDr/WindowGrid")
        |
        v
WindowGrid.shader (URP, hand-authored ForwardLit pass; ShadowCaster/
DepthOnly/DepthNormals passes reused verbatim from stock URP Lit via
UsePass)
```

Per-window data splits into what's genuinely static vs. genuinely dynamic:

- **Static, baked into vertex data once at `Build()` time** (TEXCOORD1/2):
  seed, warm/cool tint, brightness variance, this window's own randomized
  arrival/bedtime cycle fractions (same formulas as the old
  `EmissiveAnimator.LightBehaviorKind.Window` case), whether it can ever
  glow at all, whether it's one of the small flicker-eligible fraction.
  Costs nothing at runtime beyond ordinary vertex-attribute bandwidth --
  never touched again after the building is built.
- **Dynamic, in a small per-building single-channel override texture**
  (TEXCOORD3 indexes into it): the mandatory gameplay
  `SetWindowOn`/`SetWindowOff`/`ClearWindowOverride` API
  (`BuildingWindowGrid.cs`). A texture sized to the building's own window
  count (a handful to a few hundred texels) -- a single `SetPixel`+`Apply`
  is already trivially cheap at that size, so this deliberately does NOT
  reach for a `GraphicsBuffer`/`ComputeBuffer`; see "Investigate the best
  state representation" reasoning below.
- **Live global state** (day/night cycle position, light activity, neon
  boost, the `WindowScheduleEnabled` toggle, flicker tuning): pushed as
  shader globals once per frame by `BuildingWindowGridDriver` -- NOT
  per-building, not per-window. Every building's shared material instance
  reads the same values.

The ambient occupancy schedule + flicker (arrival/bedtime gate, activity
gate, sine-wave wobble) that used to run in C# on `EmissiveAnimator.Tick()`
now runs **entirely in the fragment shader**, per pixel, from the static
vertex data + the live globals above. Zero CPU cost regardless of window
count -- this is the actual performance win, more than the draw-call
reduction.

## 3. State representation: why a texture, not a bitmask/ComputeBuffer

Options considered (per the task's own list): bitmask, texture-based mask,
GraphicsBuffer/ComputeBuffer, MaterialPropertyBlock array, per-instance
shader data.

**Chosen: one small `Texture2D` per building, `MaterialPropertyBlock`-bound
per renderer.** Reasoning:

- Building window counts here are small (a handful to low hundreds), not
  the "one giant buffer for the whole city" scale that would justify a
  `GraphicsBuffer`/`ComputeBuffer`'s extra API complexity and platform
  compatibility surface (compute buffers need a compute-capable target;
  this project's `Mobile_Renderer.asset` is a plain Forward URP renderer,
  a strictly more conservative baseline `MaterialPropertyBlock` textures
  don't need to worry about).
- A per-building texture keeps the mandatory `SetWindowOn(id)` API
  genuinely simple: one texel write + one `Apply`, no index math into a
  city-wide buffer, no cross-building synchronization.
- `MaterialPropertyBlock`-per-renderer is exactly this codebase's own
  established idiom for "many instances share one Material, each needs its
  own per-instance override" (see `EmissiveAnimator.cs`'s own doc comment)
  -- SRP-batcher-friendly, no Material fork.
- A pure vertex-color/UV bitmask (no texture at all) was ruled out because
  it can't be MUTATED after the mesh is built without touching mesh data
  (`Mesh.SetUVs`/`SetColors` again) -- a real per-window state CHANGE
  needs something writable at runtime without rebuilding geometry, which a
  texture (or a buffer) gives and baked vertex data doesn't.

## 4. Scope: city buildings only, not faction HQ buildings

`BaseDresser.cs` (docs/31, faction building architecture -- Mad Doctor
arrow-slit windows, Alien riveted-brass windows) also calls
`EmissiveAnimator.Register(..., LightBehaviorKind.Window, ...)` for its
own pedestal/arrow-slit windows. **Deliberately left untouched.** That
system dresses a handful of faction HQ/Factory/Control-Centre buildings
per match, not "thousands of buildings, tens of thousands of windows" --
the scale that motivated this replacement doesn't apply there, and
`EmissiveAnimator.cs`'s `LightBehaviorKind.Window` case is still fully
intact and still the right tool for that system. `WindowScheduleEnabled`
(the one public toggle `WindowLightsHud` flips) now gates BOTH systems
correctly without any change to `EmissiveAnimator.cs` itself --
`BuildingWindowGridDriver` reads the same existing public static bool.

## 5. What's NOT verified (no Editor in this environment)

Every fix in docs/28's own bug-history table was reasoned from
screenshots/console output against a real Editor session this environment
doesn't have -- same ceiling applies here, more so, since this is new
shader code rather than tuning an existing one. Specifically unverified:

- **Shader compiles.** Hand-authored HLSL against URP 17.3's
  `Core.hlsl`/`Lighting.hlsl`/`Shadows.hlsl` includes, following the
  standard "custom URP lit shader" recipe (`GetVertexPositionInputs`,
  `GetMainLight`, `SampleSH`, `MixFog`) and reusing stock URP Lit's
  ShadowCaster/DepthOnly/DepthNormals passes verbatim via `UsePass` to
  minimize hand-rolled surface area -- but never run through Unity's
  shader compiler.
- **Visual result.** Warm/cool tint blend, flicker read, dark-vs-lit
  contrast -- no render to look at.
- **Mesh/UV plumbing end-to-end.** `BuildingWindowGrid.Build()`'s
  world-to-local vertex conversion, the override-texture texel-UV mapping,
  and the vertex-data packing were checked by hand (traced the exact
  formulas against `EmissiveAnimator`'s original C# math and confirmed the
  world-space -> `transform.InverseTransformPoint` conversion is
  necessary given the dressing holder's non-zero position), not run.

**Deliberate, disclosed behavior changes from the old system** (not
silently carried over):

- **Flicker is now rare** (~12% of glow-eligible windows,
  `BuildingWindowGrid.FlickerProbability`), not universal. The old
  `LightBehaviorKind.Window` wobbled every lit window; the task's own
  "only a small percentage of windows should flicker" asked for this
  explicitly.
- **Window quads, not thin cubes.** A single outward-facing quad replaces
  the old thin `Cube` prim -- half the vertices, visually identical from
  outside a building (the only place these are ever seen).
- **`WindowBay`'s PropLibrary mesh-swap key removed** (`FacadeKit.
  KeyWindowBay`) -- a punched window pane is GPU state now, not a
  swappable authored prop. `OrielBay`'s projecting box keeps its
  PropLibrary key; only the glass pane on its face moved onto the grid.

## 6. Validation performed vs. not performed

Performed (reasoning-level, matching this project's own established
no-Editor discipline -- see docs/28 §5's citation of the same limit):
traced every formula in `BuildingWindowGrid.Build()`/`WindowGrid.shader`
against the C# it replaces; confirmed via project-wide grep that
`BaseDresser.cs` is the only other `LightBehaviorKind.Window` call site and
left it alone; confirmed `GlowPointRegistry`/`DynamicLightBudget`'s
existing Transform-based callers (streetlamps, neon, headlights) are
unaffected by the new `RegisterPosition`/`PositionAt`/`IsAliveAt` additions
(all existing call sites keep going through the original `Transform`-based
`Register`/`TransformAt` path, untouched).

Not performed, and not possible in this environment: an actual Play-mode
run; the task's own requested before/after profiler comparison (draw
calls, SetPass calls, GameObject count, CPU frame time) needs a live
Editor/build to produce real numbers, not estimates.
