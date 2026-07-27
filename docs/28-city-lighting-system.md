# 28. City lighting system

**Status: Phases 1-3 implemented (2026-07).** This doc covers the
architecture for every light in the city — streetlamps, house/apartment
windows, neon signs, marquee chasers — after the first real Editor look
at docs/23 Phase 10's street lamps showed them as "big opaque balls of
light... turning the playfield white." That symptom, and the ask for
"a bunch of different lights... keeping them performant," are what this
plan answers.

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
| Lit ground patch too wide | `DynamicLightBudget.range` |
| Isolate lights vs. glow | `DynamicLightBudget.enableRealLights` (off) |

### Why "altering the DynamicLight" specifically did nothing

Two independent reasons, both now fixed:

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
| Ornate multi-globe lamppost | same `Bulb()` material, 3 globes | Yes, all 3 registered | Steady |
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

`flightcheck` compiles clean (added `MaterialPropertyBlock`/
`Renderer.Get/SetPropertyBlock`, `Mathf.Floor`/`SmoothStep`, and
`[Range]`/`[Header]`/`[Tooltip]` attribute stubs — all already covered
except the new ones this system needed). **Not visually verified** — no
Unity Editor exists in this session; the specific brightness numbers in
`CityLightingProfile`'s defaults are best-effort corrections based on
the reported symptom, not a confirmed-good result. The whole point of
Phase 1 (the ScriptableObject) is that the next round of tuning doesn't
require another code round-trip.
