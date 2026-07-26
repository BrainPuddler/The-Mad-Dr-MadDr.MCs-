# docs/23 Phase 10.1 (Post stack) — shipped vs deferred

## Shipped

`LumenCycleController.cs` builds a runtime URP `Volume` (global, no scene
asset needed — `ScriptableObject.CreateInstance`, same "no Editor step
needed" idiom docs/26 already used for special-attack definitions) with:

- `ColorAdjustments` — post-exposure, saturation, color filter, contrast.
  Four keyframes (Dawn/Day/Dusk/Night), cross-faded continuously by the
  same Lumen Cycle math match-core's own `LumenClock` uses (see
  graphics-2-notes.md for the clock wiring). Day lands closer to
  sun-baked sepia-warm (saturation -18, warm color filter) than a neutral
  grade, distinct from Night's saturated neon-noir push (saturation
  +22, cool-noir base filter) — per the 2026-07 daytime mood-board
  addition to docs/23 §10.
- `Tonemapping` — ACES, satisfying "filmic tonemapping."
- `FilmGrain`, `Vignette`, `Bloom` — all four phases carry their own
  intensities; Night's bloom is the highest of the four ("tuned for
  neon").
- Region grading (`ApplyRegionTint`) — a parametric tint (color-filter
  multiply + saturation/contrast deltas) keyed off `CityModel.Region`
  (docs/23 §8's `CityRegion` enum): NY pushes steel-blue and grittier
  (lower saturation, higher contrast), Paris pushes warm cream and
  softer, Montreal pushes cold pastel and flatter. Generic (every
  pre-Phase-8 preset) gets the untouched baseline grade.
- `EnsurePostProcessingOnMainCamera` defensively adds/enables
  `UniversalAdditionalCameraData.renderPostProcessing` on `Camera.main`
  so the Volume has visible effect regardless of how the scene's camera
  was set up.

## Deliberate substitution, not the plan's literal ask

docs/23 §10 asks for **per-region color-grading LUTs** — a baked 3D
lookup texture via URP's `ColorLookup` volume component. That needs an
authored texture asset (baking a neutral grade through a DCC tool or the
Editor's own LUT-strip export), which this environment cannot do (no
Unity Editor, no DCC pipeline). The region variation shipped here is a
**parametric `ColorAdjustments` tint** instead — same visual intent
(distinct per-region mood), different mechanism. Flagged here rather than
silently calling it "the LUT system."

## Deferred

- Depth of field for "the Lab podium" — no such scene exists in
  `unity-client` yet (grep-confirmed zero hits for `Podium`/`LabScene`/
  etc.); there's nothing to focus on, so DoF wasn't even stubbed in as an
  inactive component. A real, separate prerequisite gap.
- Wet-street shader (roads darken/reflect at night) — listed under §10.3
  (Materials), not this sub-phase; not attempted here.

## Verification

`flightcheck` compiles clean against a Unity/URP API stub (`Volume`,
`VolumeProfile`, `ColorAdjustments`, `FilmGrain`, `Vignette`, `Bloom`,
`Tonemapping`, `UniversalAdditionalCameraData` — the exact surface this
code calls, not the real API's full breadth). **Not visually verified**
— no Unity Editor exists in this environment, and this project's
standing discipline is to never claim verification that didn't happen.
Every numeric grade value is an invented v0.1 placeholder (docs/23 gives
mood/target language, not real numbers) — tune for real once an Editor
session exists for this project.
