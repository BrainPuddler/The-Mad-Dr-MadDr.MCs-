# docs/23 Phase 10.2 (Lighting) — shipped vs deferred

## Shipped

**Real sun animation from the Lumen clock.** `LumenCycleController`
(supersedes the old `NightMode.cs`, which only toggled between two fixed
states on a manual `N` key) keeps its own cosmetic fixed-tick counter
(10 ticks/s, matching `MatchState.TicksPerSecond`) and reads
`MadDr.MatchCore.LumenClock.PhaseAt(frame)` — the SAME pure function
match-core's own Phase 7 faction modifiers use — every tick, purely for
presentation. It never touches or reads a live `MatchState`/`SimBridge`,
so every scene gets a continuous Dawn→Day→Dusk→Night→Dawn cycle even when
nothing has opted into sim-driven movement (docs/27's `SimDriven` scope
is unrelated to this). This is Unity-layer-only, satisfying the phase's
own "no determinism regression" acceptance line.

The sun's elevation, color, and intensity all cross-fade (eased via
`Mathf.SmoothStep`, not a linear snap) between the current phase's
keyframe and the next, so the cycle reads as one continuous day. Per the
2026-07 daytime mood-board addition, Day's own elevation keyframe is
capped low (30°) rather than a high noon angle, for the "long, legible
cast shadows" look through most of the daytime — sun yaw stays fixed
across the whole cycle (only elevation/color animate) so cast-shadow
DIRECTION stays consistent, matching the "unhurried" target look.

The `N` key survives but changes meaning: with a real auto-cycling clock,
a binary day/dusk override no longer makes sense, so `N` now toggles a
20x time-lapse speed (a dev/demo convenience to see the whole cycle
quickly), not a mode pick.

**Street lamps as actual pixel lights, on a budget.** `RoadDresser`'s
existing streetlight prop (a primitive bulb sphere with an emissive
material only — no real light source) and its roundabout-ring lamps now
also register their bulb `Transform` with a new `StreetLampRegistry`
(same loose-coupling idiom as the existing `NeonRegistry`, so the static
`RoadDresser` generator never needs to know this system exists). A new
`StreetLampLightBudget` component refreshes every 0.35s: it finds the
nearest `Budget` (default 24) registered bulbs to `Camera.main` and
promotes exactly those to a real warm-sodium `Point` light (no shadows —
these are budget fill lights, not key lights), reusing a small pool of
`Light` components rather than creating/destroying them each refresh.
Every other registered bulb keeps exactly its pre-existing behavior (an
emissive material only, dimmed/boosted by `NeonRegistry` as before).
Light intensity rides the same day/night blend the post stack uses,
published via a new small `DayNightState.NightAmount` static (0..1) so
the budget system doesn't need a direct reference to
`LumenCycleController` — negligible at Day, a real warm glow at Night,
literalizing "warm sodium nights" instead of only implying it via the
emissive material's own boost.

## Deferred

- **SSAO.** URP's `ScreenSpaceAmbientOcclusion` is a Renderer Feature
  that has to be added to the project's `UniversalRendererData` asset —
  an Editor-authored `.asset` file this environment has no way to
  create, inspect, or safely mutate via reflection without an Editor
  session to verify the result didn't break the renderer. Not attempted;
  a genuine Editor-only gap, not faked.
- **Light cookies for window spill.** Needs an actual cookie texture
  asset (a DCC/Editor deliverable); no runtime substitute attempted.
- Physically-accurate sun azimuth/elevation astronomy — the cycle uses
  four hand-authored keyframes cross-faded, not a real solar-position
  calculation; sufficient for this game's stylized needs, called out
  explicitly rather than implied as "real" sun math.

## Verification

`flightcheck` compiles clean (adds `Mathf.SmoothStep`, `Light.range`,
and `Keyboard.pKey`/URP stubs to the harness as needed). `citygen-core`'s
168 tests still pass unchanged (only its compiled DLL was refreshed for
the flightcheck harness to see the Phase 8 `CityRegion` type it already
shipped with; no citygen-core source changed). **Not visually verified**
— no Unity Editor exists in this environment. Every numeric keyframe
(sun colors/intensities/elevations, lamp intensity range, budget size) is
an invented v0.1 placeholder.
