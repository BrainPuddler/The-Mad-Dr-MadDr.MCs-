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

## 7. 2026-08 follow-up: WindowBay/apartment-strip panes z-fighting ("dots" regression), fixed

Creator report after seeing this in a real build: "Performance is
better but now the windows are dots NOT full lit rectangle windows."
§5's "visually identical from outside a building" claim for the quad
conversion (line ~173 above) turned out to be the unverified assumption
that broke, specifically for two of the three `AddWindow` call sites:

**Root cause**: converting a window from a thin `Cube` (real depth,
e.g. `Proud`/0.35) to a flat quad dropped the depth-derived outward
clearance those cubes got for free. `FacadeKit`'s `WindowBay` case used
to spawn `Along(tan, n, WindowBayWidth, floorH*0.52f, Proud)` (a real
box) centered at `pos`; its OUTWARD face sat `Proud*0.5` past `pos`,
clearly separated from the wall/recess geometry right behind it. The
docs/33 conversion passed plain `pos` (the old box's CENTER, not its
outward face) straight to `RegisterWindowGlow`/`AddWindow` — a flat,
zero-thickness quad now sitting too close to (in some cases functionally
coincident with) the wall surface behind it, fighting for the same
depth-buffer pixels. A flat surface losing that fight per-pixel, rather
than as a whole, reads as a scatter of dots, not a dim or missing
rectangle. Same root cause, same fix, in `BuildingDresser.SpawnWindowStrip`
(the apartment "continuous window strip" case — arguably the more
commonly-seen instance of this bug, since it's the Medium-tier default
across most of the city) — its old Cube's `scale.z` (0.35) depth gave
the same free clearance, silently dropped the same way.

**Not affected, confirmed by re-reading the diff**: `OrielBay`'s pane —
that call site already computed `boxCenter + n * (boxDepth * 0.5f +
0.02f)` (the projecting bay's own outward face, +2cm) when converting,
so it never lost its clearance to begin with. This is why the report
was "windows" broadly and not universal — the two call sites that
dropped the offset are the majority of windows in the city (every plain
punched window + every apartment strip); the one call site that kept it
was fine.

**Fix**: restore each pane to its old Cube's OUTWARD face position —
`pos + n * (Proud * 0.5f)` in `FacadeKit.WindowBay`, `pos + normal *
(scale.z * 0.5f)` in `BuildingDresser.SpawnWindowStrip` — rather than
inventing a new clearance constant, so the pane ends up exactly where
the old, previously-working geometry put its visible face.

**Verification discipline note (still no Editor here)**: this diagnosis
is reasoned from the code diff and standard z-fighting behavior (two
near-coincident opaque surfaces at typical BigCity view distances,
where depth-buffer precision is already thin), not from a render — flag
per docs/33 §5 and docs/28 §5's own standing policy for exactly this
situation. If windows still don't read as full rectangles after this,
the next thing to check with an actual Editor: `_BulbEmissiveBase`
(shader default 0.25, never pushed from `CityLightingProfile.Active`'s
own live value by `BuildingWindowGrid.SharedMaterial()`) and whether
`Shader.Find("MadDr/WindowGrid")` resolves at all in a built player
(untested runtime shader lookup, §5's own "shader compiles" caveat).

## 8. §7's fix wasn't it — real cause was back-face culling, `Cull Off` added

Creator confirmed (Editor Play mode, so §7's shader-stripping-adjacent
theories are moot too — Play mode always has every project shader
available) that §7's z-fighting fix made no difference: still "dots.
Not rectangles." §7 wasn't wrong that the depth offset was lost (that
part of the diagnosis still holds and the fix is still correct to keep)
— it just wasn't the dominant cause.

**Actual root cause**: `WindowGrid.shader` declared no `Cull` state, so
the default `Cull Back` applies. `BuildingWindowGrid.Build()`'s quad
winding is a fixed formula off `right x up` alone (`right` from the
CALLER-supplied tangent) — it never checks that winding actually faces
`w.Normal`, it just trusts the caller got tangent handedness right.
Two call sites don't: `BuildingDresser.SpawnWindowStrip` passes the
identical `Vector3.right` tangent for both its `Vector3.forward` AND
`Vector3.back` calls (one building, two opposite walls, same tangent);
`FacadeKit.Tangent(FacadeFace face)` does the same per axis (`PlusX`
and `MinusX` both return `Vector3.forward`). Whichever of each opposite
pair doesn't happen to match a consistent right-handed convention gets
a reversed winding — back-face culled, completely invisible, not dim,
not z-fighting-noisy, just gone. What's left visible across the city:
the sparse `DynamicLightBudget`-promoted real lights, which are
genuinely round point lights with their own bloom halo and were never
part of this bug — exactly "dots, not rectangles," and exactly why the
report used those words literally.

**Why this wasn't diagnosed first**: it requires reasoning about actual
triangle winding and Unity's CW/CCW front-face convention, which is
much easier to get backwards blind than the z-fighting theory was —
and getting it backwards would have culled the CURRENTLY-working side
too, making things worse with no way to render-check the result.

**Fix**: `Cull Off` on `WindowGrid.shader`'s `ForwardLit` pass, instead
of hand-deriving and fixing the winding itself. Deliberately the
lower-risk option: a window pane is never seen from its back side (no
interior geometry exists to view it from), so double-siding it has no
real downside, whereas a winding fix that guessed the wrong sign would
have culled the opposite, currently-fine side instead — a strictly
worse outcome, unrecoverable without another round-trip to the creator.
The `UsePass`-reused ShadowCaster/DepthOnly/DepthNormals passes (stock
URP/Lit, own header explains why they're reused rather than hand-
rolled) still cull by whatever URP/Lit's own `_Cull` property defaults
to — out of scope here, a shadow/depth-only miss on one side is a much
smaller cosmetic gap than "the pane doesn't render at all."

**Still not verified in a real render** (this fix, specifically) —
flagged per this doc's own §5 and standing policy. If windows are
still wrong after this, the report should look qualitatively different
now: no longer "dots instead of rectangles" (that specific symptom
should be gone), so whatever's next is very likely a different bug,
not a third attempt at this same one.

## 9. §8 confirmed the rectangle now renders; the glass texture itself was wrong

Creator confirmed §8's fix worked (rectangle visible now) and reported
the follow-on issue precisely: "the glass glazing texture looks like a
solid line not glass." Different bug, same file family (`PbrTextureAtlas.
BuildGlass()`), unrelated to culling/z-fighting.

**Root cause**: `BuildingWindowGrid.Build()` gives every individual
window its own private, un-tiled `0..1` UV square (`uv0.Add(new
Vector2(0,0))` etc. at build time) — one pane = exactly one full sample
of the shared `_BaseMap` texture, never repeated. `PbrTextureAtlas.cs`'s
own header already documents this project's texture convention as "no
per-object UV tiling... every material gets one fixed tiling scale," so
that part is correct and by design. But `BuildGlass()`'s diagonal-sheen
band was written as `(x + y) % Size` thresholded near both `0` and
`Size` — a pattern authored assuming wraparound TILING (so the band
reads as one continuous streak once repeated across a surface, the way
`BaseDresser` uses this same texture with `SetTextureScale(2,1)`).
Sampled exactly once per window (no tiling), that condition is only
true near TWO opposite corners of the square, not along one continuous
line — a single small on-screen pane showed two disconnected corner
slivers that blurred together under bilinear filtering into a flat
diagonal gradient, not a glint. Literally "a solid line," not glass.

**Fix**: rewrote the band math to be tiling-independent — `diag = x + y`
with no modulo, thresholded by distance from one fixed band center, so
a single un-tiled `0..1` sample shows exactly one soft diagonal sheen
streak (smooth falloff via `Clamp01`, not a hard on/off edge, so it
reads as a highlight rather than a stripe). Still procedurally
generated per this file's own established convention (§0 of this file's
header), not a real imported glass texture — this project has no
texture-asset pipeline in this environment (`PbrTextureAtlas.cs`'s own
top-of-file doc comment).

**Not verified in a real render** (same standing caveat as §5/§7/§8) —
reasoned from the pixel math and the texture's known sample footprint
per window, not from seeing it rendered.

## 10. Four-part follow-up: solid colors, warm-only, no flicker, hard switch (docs/28 row 37)

Creator direction, verbatim: "Loose the glazing effect. Just solid
colours. the blue lights windows are too blue just stay to the original
warm tones. The window lights should NEVER flash on and off in short
intervals. Lights always must be motivated as a human being, moving
from room to room, coming home, going to bed. We have docs about that
I'm sure. Adhere to that. Always like a light switch NOT a dimmer."
Full row: docs/28 row 37. Four changes, one file family
(`WindowGrid.shader`, `BuildingWindowGrid.cs`, `EmissiveAnimator.cs`):

1. **No glazing.** §9's texture fix wasn't the ask — the whole idea of
   a sampled glass texture was. `WindowGrid.shader`'s ForwardLit pass no
   longer declares/samples `_BaseMap` for color at all; `SharedMaterial()`
   no longer assigns `PbrTextureAtlas.Glass`. `_BaseMap` stays declared
   in Properties (unused, "white" default) only because the UsePass-
   reused stock ShadowCaster/DepthOnly/DepthNormals passes expect it to
   exist — same reasoning this shader already applies to `_Cutoff`.
2. **Warm only.** `_CoolColor` (0.75, 0.85, 1 — visibly blue) is gone.
   This wasn't a revert to something that used to exist: the docs/33 GPU
   port INTRODUCED the warm/cool split (a per-window `tintT` draw) as
   new behavior. The pre-existing CPU path this replaced
   (`EmissiveAnimator.LightBehaviorKind.Window`, still live for
   `BaseDresser.cs`'s faction-building windows) only ever used one
   color — confirmed by reading that call site:
   `new Color(1f, 0.85f, 0.55f)`, which is exactly `_WarmColor`'s
   existing default. So "stay to the original warm tones" is literal:
   every lit window now uses `_WarmColor` alone, and its value already
   matched what "original" means.
3. **No flicker.** Both the shader's `MadDrWindowMultiplier` and
   `EmissiveAnimator`'s CPU `Window` case had a continuous sine
   "wobble" while lit, inherited from the unrelated `Flicker` kind (a
   window that's occupied "still isn't perfectly steady" was the
   original reasoning). Removed from both — a lit window is now flatly
   lit, brightness-varied only by the STATIC per-window
   `brightnessVar` baked once at build time (not a runtime dimmer, just
   "not every bulb is identically bright"), never animated.
4. **Hard switch, not a fade.** Every occupancy gate — arrival,
   bedtime, activity-threshold — used to be `SmoothStep`/`InverseLerp`-
   banded (`OccupancyTransitionFrac`, `ActivityGateBand`): a deliberate
   PRIOR decision (docs/28 row 13, `EmissiveAnimator.OccupancyGate`'s
   own comment: "a person flipping a switch reads as a beat of motion,
   not a single-frame pop"). This creator direction explicitly reverses
   that specific choice — flagged as a correction, not silently
   overwritten. Every gate is now a hard `step`/`>=` comparison: a
   window is at 0 or 1, never anything between. The `OccupancyTransitionFrac`/
   `ActivityGateBand` constants (both files) and every shader global
   that fed them (`_MadDrWinTransitionFrac`, `_MadDrWinActivityGateBand`,
   `_MadDrWinFlickerSpeedMin/Max/Floor`) are removed, not just unused —
   there's nothing left to tune at runtime for a binary switch.

**What's deliberately UNCHANGED**: the per-window randomized arrival
time (`OnCycleFrac`/`onFrac`, mid-Day through early-Night), bedtime
(`OffCycleFrac`/`offFrac`, back half of night), the `AlwaysOn` held-out
fraction ("not all lights go off"), and the city-wide `ActivityThreshold`
staggering — this is docs/28 row 13's whole "motivated as a human
being... moving room to room, coming home, going to bed" system, and
the creator's own message points at it explicitly ("We have docs about
that I'm sure. Adhere to that."). Nothing about WHEN a window's switch
flips changed, only that the flip itself is now instant rather than a
short fade, and there's no wobble riding on top of "on" any more.

**Not verified in a real render** (same standing caveat as every prior
section) — this is a straightforward code-level removal/simplification
(fewer moving parts than before, not new logic), but still unseen.

## 11. §10's solid colors read as "flat and pasted on" — added a frame/sash inset

Creator report, immediately following §10: "The yellow window lights,
panels look too flat and pasted on." Expected consequence of §10, not
a bug in it — a flat, single-normal quad with a plain solid color and
no texture has genuinely zero shading variation of its own; there was
nothing left to distinguish "a window" from "a rectangle of that color
glued to the wall." §9's texture would have provided *some* variation,
but re-adding a texture isn't on the table (§10's "just solid colours"
was explicit and immediately prior).

**Fix, still within "solid colours"**: real windows aren't only glass,
they're glass inset into a frame/sash — a separate, plain-colored
material around the pane's edge (`_FrameColor`, default a near-black
warm brown, distinct from `_DarkGlassColor`'s navy so the two read as
different MATERIALS, not just different shades of the same one). The
frame is a solid color too (no texture, no gradient blur — a crisp
`step` edge to match this project's flat-color aesthetic per
`maddr-aesthetic-preferences`), computed from each window's own 0..1
UV distance-to-edge (`_FrameWidth`, default 0.12 of the pane per side)
— the UV channel §10 stopped sampling for a texture is reused here for
geometry, not color. The frame is lit like ordinary wall geometry
(ambient + main light diffuse) but is NEVER emissive and NEVER subject
to the on/off occupancy schedule — a sash doesn't light up, only the
glass does, which is itself an additional depth cue (the frame stays
constant while the pane's brightness changes with occupancy, exactly
like a real building).

Also gives an actual purpose back to `uv0` — §10 kept baking it into
the mesh only for UsePass-reused-pass safety since nothing sampled it
any more; now `WindowGridFragment` reads it again, genuinely.

**Not verified in a real render** (same standing caveat as §5 and every
section since) — reasoned from the UV/edge-distance math, not seen.
