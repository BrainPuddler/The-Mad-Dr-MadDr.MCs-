# 29. Fire propagation system

**Status: implemented and iterated through several real rounds (2026-08),
no Unity Editor available to confirm any of it visually.** This doc
consolidates the fire/smoke attack-damage VFX system — `DamageFx.cs`'s
`FireCluster`/`FirePlume`/`SmokePlume`/`SmokePuff` classes, plus the two
call sites that drive them (`RuntimeCityBuilder` for procedural civilian
buildings, `BaseDresser` for the RTS building roster) — into one place,
instead of leaving it spread across a dozen `docs/12` decision-log
entries. If you're picking this system up cold: read §0.5 first for how
it got here, then §1-3 for how it actually works today, then §5-6 for
what's deliberately not built yet.

## 0.5. History (read this first)

Full round-by-round reasoning for every row lives in the `docs/12`
decision log (search "fire" or "DamageFx") — this table is the
condensed, current-state version, same convention `docs/28`'s own §0.5
uses.

| # | Report / direction | Root cause | Fix | Status |
| --- | --- | --- | --- | --- |
| 1 | "spawn fire when under attack" | Ignition was gated behind a landed hit (weapon cooldown), not "under attack" | `MonsterAgent.TickAttack` calls `RuntimeCityBuilder.IgniteBuildingIfNeeded` the instant an attacker is in range, before any damage lands | **Confirmed fixed** |
| 2 | "I still do not see the fire" (repeated) | `FireCluster`'s Y-offset math assumed its parent transform sat at true ground level; `SpawnCube` actually centers the massing cube at half the building's height | `holderGroundOffset` (`-height * 0.5f`) corrects both `AttachSmoke`/`AttachFireCluster` back to real ground level | **Confirmed fixed** |
| 3 | "I SEE SMOKE NO FIRE" | Fire's height formula (`height * 1.0`) WAS the roofline exactly — a point sitting exactly at a roofline is exactly where a parapet lip/roof clutter occludes it | Height moved into a 0.30-0.65 WALL band, varied per point | **Confirmed fixed** |
| 4 | "could fire spawn inside an adjacent building?" | Real risk — adjacent massing cubes can overlap into a neighbouring hex's space on a dense block | `PickClearAngle`/`PickSurfacePoint` (see §2) searches candidate angles for one with unobstructed camera line-of-sight, falling back to the original angle if every candidate is blocked | **Confirmed fixed** |
| 5 | "FireCluster and firedebug sphere are way up in the air... NOT on the buildings" | `FireCluster`/`SmokePlume`'s own wrapper `GameObject` inherited the building's own non-uniform massing-cube `localScale` (`SpawnCube` sets it to e.g. `(18, 14.4, 18)`), multiplying every local-space offset by that scale | `NormalizeScale` sets the wrapper's own `localScale` to the inverse of the parent's `lossyScale` right after `SetParent`, cancelling the inheritance | **Confirmed fixed** |
| 6 | "yes now on building now. Remove firedebug spheres. Increase the size of fire by 18%... spawn a number of fires... Larger building get more fires and larger fires. Set a sensible limit based of building's burnable surface area" | Debug sphere no longer needed; fire count/size were a flat 4-tier table with no notion of a building's real size | Debug marker code deleted; `DamageFxProfile.FireSizeBoostPct` (1.18 default); fire count/size now derive from `8 * footprintRadius * height` (a burnable-wall-area proxy) instead of a tier lookup, tier count kept as a floor | **Confirmed fixed** (count/size scaling not independently re-confirmed) |
| 7 | Full fire-propagation brief: "use something like this to spawn fire on buildings. BUT Keep to visible area... keep system simple and performant" | The flat random-timer spawner had no concept of heat, ventilation, or impact-driven ignition at all | Rewrote as the two-layer internal-heat-network / external-renderer model this doc describes (§0-3) | **Reasoned, not independently re-confirmed** |
| 8 | "fires is sometimes in the tile but not on the building. BURNING is ONLY allowed ON the building surfaces... Cheat probability of flames on surfaces facing the camera closest to the camera. winder spread... not a burn line. Allow some crawling. increase the speed of the spread based on number of attack points and amount of time before building is destroyed" | (a) placement used one fixed radial distance for every angle, wrong for a non-circular footprint; (b) every ongoing hit fed one fixed cell, and the strong upward bias turned that into a single vertical column | (a) `PickSurfacePoint` raycasts against the building's own massing-cube collider instead of guessing a distance; (b) `RegisterHit` spreads across a camera-weighted set of angle columns, `SidewaysBias` raised 1.0→1.6; `_urgency`/`_hitRateEma` speed up ticking and raise the fire-count ceiling | **Reasoned, not independently re-confirmed** |

**Rows 1-6 are creator-confirmed against real reported symptoms in
sequence** (each report describes what the previous fix actually
produced). **Rows 7-8 are the current architecture** — internally
consistent and traced against the creator's own brief/report numbers,
but this environment has no Editor, so nothing past row 6 has been seen
rendered.

## 0. The model: two layers, not one

A single system that both simulates "where should fire be" and
"what does fire look like" tends to either overfit the visuals to the
simulation's own coordinate system, or bolt randomness onto placement
that's hard to reason about later (this is exactly what row 7 above
replaced). The fix is the same two-tier shape `docs/28` uses for
lighting (Tier 1 free/cheap, Tier 2 budgeted/expensive), applied to
placement instead of performance:

| Layer | What it is | Owns |
| --- | --- | --- |
| **Internal heat network** (invisible) | A tiny per-building simulation — a fixed 5×3 grid of `FireCell` structs (`FireCluster`'s private state) | Heat, ventilation, ignition timing — WHEN a point should exist |
| **External renderer** (visible) | `FirePlume`/`FirePuff` — the actual low-poly flame-shard mesh, point light, flicker | WHERE (via a real raycast, see §2) and HOW BIG a point reads once the internal layer says it should exist |

The external layer never decides anything on its own — it only reacts
to what `FireCluster.TickFireNetwork` already decided. This is
deliberate: every "why is fire here" question should be answerable by
reading the heat network's state, not by chasing a random-number call
buried inside a rendering method.

## 1. FireCluster — the internal heat network

One `FireCluster` component per building (`DamageFx.AttachFireCluster`,
called from `RuntimeCityBuilder.IgniteBuildingIfNeeded` for procedural
buildings and `BaseDresser` for the RTS roster), parented directly under
the building's own massing-cube transform.

**The grid.** 5 angle columns (`GridAngleOffsets = {-28,-14,0,14,28}`)
spanning ONLY the existing ±35° camera-facing arc (`_baseAngle`, read
once at ignition and never recomputed — "keep it locked to that side" —
so a building's fire always stays on whichever face was camera-facing
when it started burning) × 3 height bands (`GridHeightFracs =
{0.28,0.5,0.72}`, all safely under the roofline — "not on roofs" is a
standing constraint, never relaxed). 15 `FireCell` structs total, one
array allocation at `Init`, no further heap churn.

**Each cell** tracks `Heat` and `Ventilation` only — no separate
Fuel/StructuralDamage/BurnState. This is a deliberate simplification:
nothing in this system ever needs a building to stop burning once lit
(matching every other FX class in `DamageFx.cs`'s own "never removes an
effect once started" convention), so tracking fuel depletion would be
pure unused complexity today. See §6 if that ever changes.

**Simulation tick** (`TickFireNetwork`, throttled — see
`CurrentSimTickInterval` in §3, base 0.5s): every hot cell radiates heat
to its neighbours, biased:

| Direction | Bias | Source |
| --- | --- | --- |
| Upward | 2.5x | Literal from the creator's brief ("+250%") |
| Sideways | 1.6x | Brief said "100%" (1.0x); raised after row 8's "burn line" report — see §0.5 |
| Downward | 0.2x | Literal from the brief ("20%") |

...scaled by the RECEIVING cell's own `Ventilation` (a cell that's
already broken out lets heat in faster — "ventilation dramatically
increases spread"). When a cell's heat crosses `IgnitionThreshold`
(bounded by `_maxIgnitedCells`, see §3), `IgniteCell` spawns its real
`FirePlume`. An already-ignited cell's continuing heat instead grows its
EXISTING flame (`FirePlume.SetHeatScale`) rather than spawning a new one
— "fire growth should use heat energy, not simply counting flames."

**Embers** (`MaybeSpawnEmber`): a small per-tick chance for an
already-hot ignited cell to instantly push ONE grid-adjacent unignited
neighbour to ignition, bypassing the normal gradual climb — "rare but
dramatic." Since a grid neighbour is only a cell-width away, this can
never manifest as fire racing across an intact wall (the brief's own
1-2m secondary-crawl cap, satisfied by the grid's own geometry rather
than a separate distance check).

## 2. Placement — PickSurfacePoint

`IgniteCell` never guesses a fire point's world position — it asks
`PickSurfacePoint` for one. That method casts a real `Physics.Raycast`
inward from well outside the footprint (`footprintRadius * 3`) at the
cell's own angle/height, and uses the ACTUAL hit point (nudged out
along the hit normal by `SurfaceOffset`, 0.12m, so the flame sits just
proud of the mesh instead of clipped into it).

**Why a raycast and not a fixed distance.** The pre-row-8 version placed
every point at one fixed radial distance (`footprintRadius * 1.6`)
regardless of angle. That's only exactly correct for ONE angle of a
non-circular (square) footprint — everywhere else it either falls short
of the real wall (fire floating in open tile-air) or overshoots past
it. A raycast against the building's own real collider is correct for
every angle and every footprint shape (including multi-hex/irregular
ones) by construction, not by tuning a constant.

**Only the massing cube has a collider.** `RuntimeCityBuilder.SpawnCube`
creates it with `keepCollider = true` and registers it into
`_buildingByCollider` — a confirmed, already-relied-upon raycast target,
not a collider added just for this. `BuildingDresser`'s dressing (
windows, cornices, water towers, "roof decoration and other features")
has no collider of its own (confirmed by grep) — a raycast can only ever
land on the shared massing-cube silhouette. Since dressing is built
directly onto/around that same box, a point pinned to its surface still
reads as on the building, near its decoration, without needing to add
colliders to every dressing piece across `BuildingDresser.cs`.

**Camera line-of-sight** is checked from a point pushed further out
(`LosProbeOffset`, 0.6m — enough to clear the collider's own surface so
the test doesn't self-occlude against the very building it's standing
on) to `Camera.main`. A candidate angle is rejected for EITHER a missed
raycast (no real surface there) OR an occluded line of sight, searching
progressively wider offsets within the same ±35° arc before falling
back to the old fixed-distance guess as a last resort — this never
blocks a fire point from spawning, it only risks (rarely) not sitting
exactly on real geometry that one time.

## 3. Spread control — camera weighting, urgency, attack rate

**Camera-facing weight.** `RegisterHit` (called once per landed hit from
`RuntimeCityBuilder.ApplyBuildingDamage`) no longer feeds every hit into
one fixed cell — `PickWeightedColumn` picks a column weighted
`{1,2,3,2,1}` across the 5 angle columns (center 3x more likely than
either edge), so heat starts from several lateral points across the
visible facade, skewed toward — never exclusive to — the most directly
camera-facing angle. This is what fixed the "burn line" (§0.5 row 8):
combined with the strong upward bias, a single fixed origin reliably
produced one vertical stack.

**Urgency and attack rate.** `RegisterHit(float energy, float
hpFraction01)` receives the building's current HP fraction alongside
the hit's own damage amount. `_urgency` (0 at full HP, → 1 as HP nears
zero) and `_hitRateEma` (a smoothed hits-per-second reading off real
`Time.time` gaps between calls — a proxy for "number of attack points,"
since there's no real attacker-identity tracking anywhere in this
pipeline, but more simultaneous attackers necessarily means hits land
closer together in time) both feed:

- `CurrentSimTickInterval` — the sim ticks faster (down to
  `MinSimTickInterval`, 0.12s) as either climbs.
- `_maxIgnitedCells` — raised above its area-based floor
  (`_baseMaxIgnitedCells`) by up to `MaxUrgencyBonusCells` (3), capped at
  `MaxFireCountCeiling` (10).

Net effect: a building near destruction, or one being hit by several
attackers at once, visibly grows fire faster and further than the same
building taking occasional single hits — "shorter time [to destruction],
more spawns."

## 4. DamageFxProfile — the tuning surface

`DamageFxProfile.cs` (a `ScriptableObject`, same pattern as
`CityLightingProfile`) holds the fire/smoke SIZE knobs:
`SmokeResizePct`/`SmokeGrowthMultiplier`/`SmokeRiseSpeed`/
`SmokeWindSpeed`, `FireResizePct` (point-light range/intensity only),
`FireSizeBoostPct` (flame-mesh size, default 1.18). Everything ELSE in
this doc — grid layout, heat/ventilation constants, bias ratios, urgency
tuning — lives as `private const` fields directly on `FireCluster` in
`DamageFx.cs`, not on the profile asset; they're simulation tuning, not
per-playtest visual knobs, so there was no ask (yet) to expose them at
runtime.

## 5. What's wired up today vs. what's still a stub for later

| Path | Status |
| --- | --- |
| Procedural civilian buildings (`RuntimeCityBuilder`) | Full pipeline: ignition on in-range, `RegisterHit` on every landed hit, raycast placement |
| RTS building roster (`BaseDresser`) | Ignition + placement shared (same `FireCluster`/`AttachFireCluster`); **`RegisterHit` is NOT wired here** — `BaseDresser`'s own damage path never calls it, so RTS-roster buildings ignite but their fire never speeds up/spreads from urgency or attack rate the way procedural buildings' does |
| Real per-hit 3D impact points | Not implemented — see §6 |
| Fuel depletion / fire going out | Not implemented — deliberate, see §1 |
| Fire spreading to an ADJACENT building | Not implemented — the grid is scoped to one building only |
| Structural/roof collapse triggered by fire | Not implemented — "not on roofs" stands; fire never reaches the true roofline band |
| Water/hydrant dousing fire | Not implemented — `WaterJet`/hydrant FX (`DamageFx.WaterJet`) is a separate, unrelated system today |

## 6. Future expansion ideas (not started)

- **Real per-hit impact points.** `RegisterHit` currently seeds a
  weighted-random column because no true 3D impact-surface point exists
  anywhere in the attack pipeline (`MonsterAgent.TickAttack`'s own `bp`
  is a footprint-hex-center approximation already spent on the weapon FX
  beam/shot endpoint). A real mesh-surface raycast at the moment of
  impact (reusing the same collider `PickSurfacePoint` already raycasts
  against) would let fire genuinely start where a shot actually landed.
- **Wire `RegisterHit` into `BaseDresser`'s own damage path** so RTS
  roster buildings get the same urgency/attack-rate speed-up procedural
  buildings do (see §5's gap).
- **Cross-building embers.** Today's embers (§1) only ever jump within
  one building's own 15-cell grid. A building fully engulfed next to a
  dense block could plausibly ignite a neighbour — would need a way for
  one `FireCluster` to reach a nearby building's `IgniteBuildingIfNeeded`.
- **Fuel depletion / extinguishing.** Currently fire never goes out once
  lit (§1's deliberate simplification). A real fuel value per cell,
  draining over time or when water/foam FX interacts with a cell, would
  let a building's fire genuinely burn out or be fought.
- **Roof collapse.** The original brief mentions "roof collapse areas"
  as the final stage of breakout order — deliberately NOT implemented;
  every height band stays under the roofline per the standing "not on
  roofs" direction. Would need its own trigger (e.g. sustained heat in
  the highest band, or building HP crossing a threshold) tied into
  building-destruction VFX (`RuntimeCityBuilder.ApplyBuildingDamage`'s
  `Destroyed` branch) rather than the fire grid itself.
- **Smoke/fire heat tie-in.** `SmokePlume` still sizes purely off
  `BuildingStats.SmokeScale(tier)`, independent of `FireCluster`'s own
  heat state — a building burning hotter doesn't currently produce
  visibly heavier smoke. Could read `FireCluster`'s aggregate heat the
  same way `FirePlume.SetHeatScale` already does.

## Verification

No Unity Editor exists in this environment — every fix past §0.5 row 6
is reasoned from the creator's own reports/briefs and traced against
real code (`SpawnCube`'s actual collider setup, `BuildingDresser`'s
actual lack of one, the brief's own literal percentages), not guessed.
Nothing here has been confirmed by an actual render since the heat-
network rewrite (row 7) shipped — treat rows 7-8 as "reasoned and
internally consistent," not "confirmed working," until a real
Play-mode/screenshot report comes back, the same standard `docs/28`
holds its own unconfirmed rows to.
