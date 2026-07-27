# 28. City lighting system

**Status: Phases 1-3 implemented, then debugged against a real Editor
through several rounds (2026-07).** This doc covers the architecture for
every light in the city — streetlamps, house/apartment windows, neon
signs, marquee chasers — after the first real Editor look at docs/23
Phase 10's street lamps showed them as "big opaque balls of light...
turning the playfield white." That symptom, and the ask for "a bunch of
different lights... keeping them performant," are what this plan
answers.

**If you're picking this up cold, read §0.5 first, not the rest of the
doc in order.** It's the single table of every bug this system has hit
and its current status — everything else here is architecture that
mostly hasn't changed since Phase 1.

## 0.5. Bug history + current status (read this first)

The creator debugged this live against a real Editor across many
rounds — this repo has no Editor, so every fix here was reasoned from
symptoms/screenshots/console output the creator supplied, not directly
observed. That's a real risk of a fix being wrong in a way only the
Editor can catch (see rows 6→7 below, where fixing one bug's root cause
directly caused the next one). Full blow-by-blow reasoning for each row
lives in the docs/12 decision log (search "docs/28"); this table is the
condensed, current-state version.

| # | Symptom reported | Root cause | Fix | Status |
| --- | --- | --- | --- | --- |
| 1 | "some effect but even set to 0 way too large" (bloom) | `nightBloom` was blended TOWARD (weighted by `nightAmount`, which decays through the back half of the night phase) instead of being a real multiplier | Renamed to `bloomScale`, true always-on multiplier on the whole curve, same model as `emissiveScale` | **Confirmed fixed** — creator verified bloom size responds correctly |
| 2 | "it's the real lights under market-stall-canopy, ornate-lamppost-pole" too large | Checked geometry first (hex spacing 20m, furniture offset ±3m — ruled out cross-prop bleed); real cause: `DynamicLightBudget.range` (7m) is a Point light illuminating any nearby geometry, not just the registered prop | Tightened range 7f→3f | **Was WRONG** — see #3 |
| 3 | "light is not pooling on the ground" (regression from #2) | The ornate lamppost's globes mount 5.9m up; 3m of range can't physically reach the ground from there. `range` is a straight-line radius, not a ground-projected size | Reverted range to 8f (clears the mount height with margin) | **Confirmed fixed** |
| 4 | "still not working" / crash on toggling `enableRealLights` off | Two bugs: (a) `Refresh()` threw `ArgumentOutOfRangeException` when `activeBudget == 0` (empty-list fallback path); (b) fresh pooled `Light`s defaulted to Mixed bake mode, causing a GI console warning | (a) Guarded the fallback branch; (b) explicit `lightmapBakeType = Realtime` | **Confirmed fixed** — no more crash/warning |
| 5 | "objects still black, no light I can see" | Both URP Pipeline Assets had Additional Lights set to **Per Vertex**; this city is built entirely from low-poly primitives (a ground quad can have 4-8 vertices), so a point light's contribution can vanish between vertices even when perfectly configured | Set both `PC_RPAsset`/`Mobile_RPAsset` to Per Pixel (flagged mobile perf trade-off, not silently taken) | **Confirmed fixed** (ground pools now visible) |
| 5b | (same report, second cause) | `RenderSettings.ambientMode` was never set anywhere — Unity only reads `ambientLight` in `Flat` mode; scenes default to `Skybox`, so every ambient value this controller ever computed was silently discarded | `LumenCycleController.Start()` now sets `ambientMode = Flat` once | **Confirmed fixed** |
| 6 | ornate-lamppost-pole / market-stall-canopy rendered **pure black** even sitting inside a correctly-working light pool | `ProceduralMeshKit` emitted every face in both windings (deliberate anti-mistake guard) — `RecalculateNormals` averages face normals per vertex, so +N and -N cancelled to exactly zero, and a zero normal makes `dot(N,L)` zero for *any* light | Single winding + `FaceOutward()` (re-winds any triangle facing the mesh centroid). Verified numerically in the flightcheck harness (22/22 zero normals → 0/0) since there's no Editor to look at | **Was INCOMPLETE** — see #7 |
| 7 | Same two props **vanished entirely** (regression from #6) | Fixing #6 reintroduced exactly the risk double-winding existed to prevent: whether Unity's front-face culling agrees with `FaceOutward()`'s notion of "outward" can't be verified without an Editor — it disagreed, so the correctly-wound faces got back-face culled | `PropLibrary.Spawn` clones the material and sets `_Cull = Off`, but ONLY for registered-builder (ProceduralMeshKit) meshes, never the primitive fallback | **Reasoned fix, awaiting creator re-verification** — robust to either winding direction by construction, but not yet confirmed against a real render |
| — | (side finding, not a symptom report) | Ornate lamppost registered a real light per globe (3, half a metre apart) — ~3x stacked intensity on one pavement patch, 3 of 24 budget slots on one fixture | Register one real light per fixture; all 3 globes still glow (emissive, unaffected) | Bundled into the #6/#7 commits, not separately re-verified |
| — | "I believe you put the lights in the wall" | Not yet root-caused — raised while the props were invisible (row 7's bug), so hard to judge whether it was a real position bug or just confusing to evaluate against invisible geometry | Not yet attempted | **Open** — re-check after #7 is confirmed |

**As of the last commit (`5fd5a4f`): rows 1, 3, 4, 5, 5b, 6 are creator-
confirmed working. Row 7 (culling fix) and the "lights in the wall"
report are NOT YET re-verified against a real render — that's the
active open thread, not a settled state.**

## 0. The core problem with "just add more Lights"

A real-time `Light` component is not free: URP shades it per affected
pixel, and even a modest scene tips over into stutter somewhere in the
low hundreds of simultaneous lights depending on target hardware — long
before "a 1950s city full of lit windows" gets anywhere close to
realistic density. Treating every glowing thing as its own `Light`
was *never* going to scale to "hundreds on screen at once," independent
of the brightness bug.

The fix is a **two-tier model**, not a brighter/dimmer numbers tweak:

| Tier | What it is | Cost | Count |
| --- | --- | --- | --- |
| **1. Emissive glow** | An emissive material on the prop itself (`_EmissionColor`) | Effectively free — no extra draw call, no per-pixel lighting pass, renders in the same batch as the mesh | Unlimited |
| **2. Real dynamic light** | An actual `Light` component, illuminating *nearby geometry* (the ground, a wall) | A real per-pixel cost | Budgeted — one shared pool across the WHOLE city |

Every light in the city is Tier 1 by default. Only the
`RealLightBudget` nearest-to-camera glow points (across every kind
combined — streetlamps and windows and neon and marquee all draw from
the SAME pool, not one pool each) get promoted to Tier 2. This is
exactly the technique real 3D games use for "a skyline full of lit
windows at night" — the windows glow, they don't each cast light.

## 1. CityLightingProfile — the tuning surface

`CityLightingProfile.cs` is a `ScriptableObject`
(`Assets > Create > MadDr > City Lighting Profile`) holding every number
that was previously a hardcoded constant: real-light budget/peak
intensity/range, base emissive brightness, the night boost ceiling,
night ambient/bloom, and flicker/buzz/chase timing. Assign one to
`RuntimeCityBuilder.lightingProfile`; leave it unassigned and
`CityLightingProfile.Default` (safe in-code values) is used instead, so
no scene ever breaks from a missing asset.

**Why a ScriptableObject and not just more Inspector fields on
RuntimeCityBuilder:** every light-emitting system (`RoadDresser`,
`BuildingDresser`, `DynamicLightBudget`, `EmissiveAnimator`) needs to
read the same numbers, and several of them are static generator classes
with no scene-object identity of their own. A shared asset referenced
via one static (`CityLightingProfile.Active`, set once at city-build
time by `RuntimeCityBuilder`) is the natural fit — and it's an asset
players/testers can duplicate per-region or per-mood later without
touching code.

### Where to actually tune at runtime (corrected 2026-07)

The first version of this got the ergonomics wrong: half the values were
baked into `_grades`/cached materials at city-build time, and with no
profile asset assigned `CityLightingProfile.Default` is a runtime-created
object that appears nowhere in the Inspector — so the creator's report
was exactly right, **nothing they could reach changed anything.**

The live knobs are now **plain fields on the two MonoBehaviours**
(`LumenCycleController`, `DynamicLightBudget`, both on the
`RuntimeCityBuilder` GameObject), read every frame / every refresh, so
dragging them in Play mode changes the picture immediately. The profile
asset is the *authored defaults* layer: assigned, it seeds those fields
at city-build time; unassigned, the components keep their own values and
nothing is overwritten.

| Symptom | Knob |
| --- | --- |
| Glowing ball too **bright** | `LumenCycleController.emissiveScale` |
| Glowing ball too **large** | `LumenCycleController.bloomScale` |
| Whole scene washed out | `LumenCycleController.nightAmbient` |
| Lit ground patch too strong | `DynamicLightBudget.peakIntensity` |
| Lit ground patch too wide | `DynamicLightBudget.range` (note: this is a straight-line radius from the light's own position, not a ground-projected size -- it must comfortably exceed the fixture's mount height or there's no ground patch at all, see the 2026-07 correction in the field's own comment) |
| No pool on the ground at all | Check `DynamicLightBudget.range` isn't shorter than the fixture's mount height (e.g. the ornate lamppost globes sit 5.9m up) |
| Isolate lights vs. glow | `DynamicLightBudget.enableRealLights` (off) |

### Why "altering the DynamicLight" specifically did nothing

Two independent reasons up front, both fixed early — see §0.5 for the
FULL list, including several found only after these two:

1. **The glowing balls are not the dynamic lights.** They are the
   emissive bulb *geometry* (small spheres with an emissive material),
   spread into much larger soft blobs by **Bloom**. A `Light` component
   illuminates *other* surfaces — it does not itself render as a ball on
   screen. So no amount of changing light intensity/range could ever fix
   the reported symptom; `emissiveScale` and `bloomScale` are the knobs
   that actually target it. (The bulb spheres were also literally
   oversized — 0.5 m across at RTS camera height — now ~0.25 m.)
2. **The pooled `DynamicLight` GameObjects are overwritten ~3×/second**
   by `DynamicLightBudget.Refresh()`, which repositions/recolors/resizes
   them from its own fields. Hand-editing those objects in the hierarchy
   could never stick. The component's fields are the real source.

## 2. GlowPointRegistry + DynamicLightBudget — the shared real-light pool

Any prop that glows registers its transform + a tint color with
`GlowPointRegistry`, regardless of which dresser spawned it or what kind
of light it represents. `DynamicLightBudget` (one instance per scene,
added by `RuntimeCityBuilder`) refreshes on a timer (a few times a
second, not every frame): finds the nearest `RealLightBudget` registered
points to the camera, and gives exactly those a real `Point` light
(color-matched, intensity/range from the profile, no shadows — these are
fill lights, not key lights). A small pool of `Light` components is
repositioned/recolored/toggled each refresh rather than
created/destroyed.

**One shared budget, not one per kind**, is the important design
decision here: if streetlamps, windows, and neon each got their own
separate budget of (say) 24, that's 72 real lights the moment all three
kinds are in view — exactly the performance cliff this whole design
exists to avoid. One pool means the budget always goes to whatever's
actually nearest the camera right now, whatever kind it is.

## 3. EmissiveAnimator — batched flicker/buzz/chase at scale

The direct answer to "turn on/off and fade with hundreds of them on
screen." `EmissiveAnimator` is a single manager (`EmissiveAnimatorDriver`
runs its one `Update()` per scene) holding a flat list of registered
renderers and pushing a scaled emission color into each one's own
`MaterialPropertyBlock` — **not** a `MonoBehaviour.Update()` per light,
and **not** a separate `Material` instance per light (which would break
SRP batching). This is the standard Unity technique for "many instances
share one Material, each needs its own per-instance tweak," and it's
genuinely cheap: a couple of trigonometry calls plus one `SetColor` per
*animated* instance per frame.

Four behavior kinds (`LightBehaviorKind`):

- **Steady** — no animation at all. Deliberately a no-op: it does NOT
  install a property-block override, so the renderer just shows the
  shared material's own plain color (still correctly riding
  `NeonRegistry`'s day/night boost). Registering a Steady light would
  actually be *worse* than not registering it — a property-block
  override, once installed, takes priority over the material's color for
  that renderer forever, so a "snapshot" registration would freeze that
  one instance at whatever brightness happened to be active the moment
  it registered, ignoring the day/night cycle from then on. Most props
  in a 1950s city (plain streetlamp bulbs, most signage) are Steady and
  need no registration at all.
- **Flicker** — windows, occasional neon dropout: a slow, per-instance,
  out-of-phase brightness wobble. Used for house/apartment windows today.
- **Buzz** — a failing neon tube: fast, small-amplitude flutter plus
  occasional brief full dropouts. Used for the movie palace's neon today.
- **Chase** — a marquee "clique" sequencer: a shared clock advances a lit
  index along a row of bulbs, each comparing its own slot to the current
  step. Used for the movie palace's marquee bulb row today.

Every registered instance also needs `DayNightState.NeonBoost` folded
in (published by `LumenCycleController` every frame, the same value
`NeonRegistry.SetBoost` receives) — a `MaterialPropertyBlock` override
otherwise ignores the day/night cycle entirely.

## 4. What's wired up today vs. what's still a stub for later

| Light kind | Tier 1 (glow) | Tier 2 (real light) | Behavior |
| --- | --- | --- | --- |
| Streetlamp bulb | `RoadDresser.Bulb()`, profile-driven brightness | Yes, registered | Steady |
| Ornate multi-globe lamppost | same `Bulb()` material, 3 globes | Yes, ONE registered per fixture (2026-07: was all 3, ~0.5m apart — stacked to ~3x intensity on one patch of pavement and burned 3 budget slots on a single fixture) | Steady |
| Apartment windows | new `BuildingDresser.WindowGlow()`, ~2-in-5 floors lit | Yes, registered | **Flicker** |
| Movie palace neon (underglow/blade/letters) | existing `NeonTeal`/`SignWhite`/`NeonRed` | No (decorative, not budgeted) | **Buzz** |
| Movie palace marquee chaser row | new small bulb row | No (decorative, not budgeted) | **Chase** |

**Deliberately not attempted this pass** (real, separate follow-ups, not
silently skipped):
- Office-tower window bands (`DressOffice`'s single tall strip per face,
  not per-floor) — no per-floor granularity exists there yet to flicker
  individually; would need the same per-floor-strip treatment
  `DressApartment` got.
- Individual window-pane geometry — every "window" here is still one
  whole floor-height strip, not individual panes; true single-window
  lit/dark granularity needs real per-pane geometry, a bigger
  `BuildingDresser` change.
- Per-kind real-light tinting beyond color (e.g. a wider cone/spread for
  a window's spill vs. a streetlamp's pool) — every promoted light is a
  plain `Point` light today regardless of kind.
- A generic "any prop can flicker" author-time hookup — today's Flicker/
  Buzz/Chase wiring is hand-written at each specific spawn site; a
  future pass could expose this as a per-prop-kind config table instead.

## 5. Adding a new light kind (for whoever's next)

1. Spawn the prop with its emissive material as usual.
2. If it should compete for a real light: `GlowPointRegistry.Register(
   transform, tintColor)`.
3. If it should animate: `EmissiveAnimator.Register(renderer,
   baseEmissionColorBeforeBoost, kind, seed, ...)` — `baseEmission` is
   the material's own `color * emissive` (matching how `M()` computes
   it), NOT a `Color(r,g,b,a)` alpha.
4. Nothing else — `DynamicLightBudget` and `EmissiveAnimatorDriver`
   already run once per scene and pick up every new registration
   automatically.

## Verification

`flightcheck` compiles clean after every change in this doc (stubs have
been extended repeatedly along the way — real `Vector3`/`Mathf`
trig/`RecalculateNormals`/etc, not just enough to compile; see §0.5 row
6, where a stub that was a `{ }` no-op would have made the regression
test pass vacuously). Compiling clean is necessary, not sufficient —
this session has no Unity Editor, so nothing here is visually confirmed
by *this* session; every fix past the first two rows in §0.5 was
reasoned from creator-supplied screenshots/console output/live Inspector
values, iterating in real time against their actual Editor. That process
found real regressions this session's own reasoning introduced (rows 3
and 7) — read §0.5's status column before assuming anything below Phase
1 is settled, and check whether a newer docs/12 entry supersedes it.
