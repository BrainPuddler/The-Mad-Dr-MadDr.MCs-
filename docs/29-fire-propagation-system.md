# 29. Fire propagation system

**Status: implemented and iterated through many real rounds (2026-08),
no Unity Editor available to confirm any of it visually.** This doc
consolidates the fire/smoke attack-damage VFX system — `DamageFx.cs`'s
`FireCluster`/`FirePlume`/`SmokeCluster`/`SmokePlume`/`SmokePuff`
classes, the two call sites that drive them (`RuntimeCityBuilder` for
procedural civilian buildings, `BaseDresser` for the RTS building
roster), and — as of §1.6 — the `MonsterAgent`-side attack-hierarchy
logic that decides which building even reaches this pipeline in the
first place — into one place, instead of leaving it spread across two
dozen `docs/12` decision-log entries. If you're picking this system up
cold: read §0.5 first for how it got here, then §1-3.5 for fire and §7
for smoke to see how it actually works today, then §5-6 for what's
deliberately not built yet.

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
| 9 | "if an attacked target has multiple building in it's template then any buildings hit my monster's weapon fire should catch fire first. decrease but do not eliminate[] the spawn facing camera preference" + a visual-variation brief (persistent per-instance seed for height/width/brightness/flicker/lean/emissive/growth/lifetime; organic, non-grid-readable spread; no new objects, no per-frame allocations, no expensive searches) | Multi-hex buildings (Medium/Large tiers really do span 1/2/4 hexes) always ignited/fed heat to their FIRST hex's cube regardless of where an attacker actually was; every flame's visual properties were driven by shared constants, not per-instance, so adjacent flames could look identical; heat diffusion had no per-cell variance, so spread read as a uniform radiating circle | `hitHex` (from `MonsterAgent`'s existing `bp`) threaded through `IgniteBuildingIfNeeded`/`ApplyBuildingDamage`/`RegisterHit` via `FootprintIndexOf`, so each hex ignites/feeds independently; `AngleColumnWeights` pulled `{1,2,3,2,1}`→`{2,3,4,3,2}`; `FirePlume` samples 9 persistent jittered multipliers once in `Awake` (§1.5); new per-cell `FireCell.Flammability` (§1) folded into `SpreadHeat` | **Reasoned, not independently re-confirmed** |
| 10 | "Monsters should try to attack only the target building(s)... try not to shoot through other building" + "Based on monster aggression, monster could collaterally destroy other building if blocked... Player owned building are the only buildings immune" + "Non projectile equipped monsters should be able to damage buildings through weapon swings or melee attacks and building contact" | No obstruction check existed anywhere in the firing pipeline (`WeaponFx.Beam`/`ShotAtPoint` drew straight through anything); no per-creature aggression stat existed to gate a new collateral-attack decision; player-owned `SimBuilding` visuals are collider-less so a raycast can never detect them | `MonsterAgent.HasClearLineOfSight`/`TickAttackReposition` gate projectile weapons only (`isProjectile = armed && Weapon.Kind != Melee` — melee/unarmed always get a clear shot, no line-of-fire to obstruct); a civilian blocker rolls this creature's own genome `fury` (reused, no new schema) as aggression for a FULL retarget (`_originalTargetBuilding`, resumed once the blocker dies); a player-building blocker (detected via a `SimBridge` position scan, not the raycast) always forces reposition instead, no roll; `MonsterCombatProfile.MobMentalityBonus` adds an aggression bonus when a nearby monster is already mid collateral-attack | **Reasoned, not independently re-confirmed; melee/unarmed damage+ignition-eligibility confirmed unchanged by re-reading the actual gated code path** |
| 11 | "randomize if projectile will cause a fire. Goal make the fire pattern look organic, NOT non procedural" | Every landed hit fed `RegisterHit`'s heat network unconditionally — a strict 1:1 relationship between damage numbers and fire growth, itself perfectly deterministic regardless of how organically the heat then diffused | New `DamageFxProfile.FireIgnitionChancePerHit` (default 0.6): a per-hit roll gates only the heat-injection portion of `RegisterHit`; hit-rate/urgency tracking still sees every real hit unconditionally (real attack pressure, not this hit's own visible response) | **Reasoned, not independently re-confirmed** |
| 12 | "monsters should attack all buildings that will be destroyed in the attack. goal make it realistic that the building are logically destroyed" | HP is shared across a multi-hex building's WHOLE footprint (destruction was already structure-wide, `ApplyBuildingDamage`'s `Destroyed` branch already shatters every cube at once) but only the specifically-hit hex ever showed fire leading up to that — a whole footprint could suddenly turn to rubble while only one corner had ever visibly burned | `ApplyBuildingDamage` now progressively ignites more of a multi-hex building's OTHER footprint hexes as its HP fraction falls (`targetIgnitedHexes = Ceil(footprintCount * urgency)`), on top of the always-first-ignited hit hex — reuses the existing idempotent per-hex ignition guard entirely, no new fire mechanics | **Reasoned, not independently re-confirmed** |
| 13 | ASCII-diagram brief: fire should read as "expanding islands of burning material" — a few DENSE clusters, "one bigger fire... and maybe 1-2 smaller ones in the same block of windows," not "3-4 small fires" scattered evenly | `PickWeightedColumn` re-rolled a fresh independent camera-weighted random column on EVERY hit, with no memory of where heat already was — over several hits that reliably seeded 3-4 separate low-heat columns each limping toward ignition on its own, instead of one column's heat snowballing into an obviously bigger fire | New `PickColumnForHit` spends most hits (`ClusterBiasChance`, 0.72) feeding whichever column is already hottest (`HottestColumn`, sums heat across all 3 bands per column); the rest still fall through to the old `PickWeightedColumn` random pick, which is what still lets 1-2 genuine satellite fires start elsewhere. No change to diffusion/ignition/embers — purely which column a landed hit's heat goes into | **Reasoned, not independently re-confirmed** |
| 14 | Full smoke brief: "the smoke system is no longer behaving correctly or is not rendering at all... decouple smoke from the fire renderer... smoke always travels away from the building (surface normal + up)... multiple smoke emitters, not one per fire... smoke before flames... smoke growth/motion/colour should scale smoothly with fire energy" | Investigated against the prior single-`SmokePlume`-per-building design (§7 has the full writeup): not a shader/render-queue/pool bug — `MakeTransparent` is the same setup already confirmed working for water/the glass dome, and this file has never pooled any puff kind. Real causes: (a) ONE plume total per building regardless of size/fire-point count; (b) a multi-round size-shrinking history (0.7→0.2) left it too small to read once stacked with (a); (c) zero connection to `FireCluster`'s own heat state — a flat always-on loop from the moment of first damage, nothing for "smoke before flames" to hang off | New `SmokeCluster` (§7) replaces `SmokePlume` at both `AttachSmoke` call sites — up to `FireCluster.VentColumnCount` (5) shared per-column vents instead of 1 fixed plume, each reading `FireCluster`'s new public `ColumnPeakHeat`/`ColumnTotalHeat`/`ColumnHasFlame`/`Urgency01`/`GetVentPoint` accessors (additive-only — not one line of `FireCluster`'s own ignition/diffusion/ember logic touched) to drive onset timing, intensity, colour, and a real (surface-normal + up) drift direction. Also fixed in passing: `RuntimeCityBuilder`'s Damaged-transition darkening pass skips renderers under a live `SmokePlume`/`FireCluster` (a previously-fixed "puff goes solid" `MaterialPropertyBlock` bug) — that skip-check is keyed off component type, so it would have silently stopped protecting damage smoke the moment its wrapper type changed; `SmokeCluster` added to the same skip-check | **Reasoned, not independently re-confirmed** |

**Rows 1-6 are creator-confirmed against real reported symptoms in
sequence** (each report describes what the previous fix actually
produced). **Rows 7-14 are the current architecture** — internally
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

**Flammability — organic, non-grid-readable spread.** Each `FireCell`
also carries a fixed `Flammability` multiplier (0.6-1.5x), hashed once
per cell in `Init` off `FireCluster`'s own `GetInstanceID()` and folded
into `SpreadHeat` alongside `Ventilation`. This single extra float per
cell (no new objects, no per-frame allocation) is what turns an
otherwise perfectly even radiating diffusion into "irregular islands...
hesitating in some areas, suddenly accelerating in others" — some cells
inherently catch a little faster or slower than a uniform model would
predict. Deterministic within any ONE ignition (same instance ID → same
map, reproducible for debugging) but freshly different every time a
`FireCluster` is created (a fresh GameObject always gets a fresh
instance ID, even for "the same" building burned again in a later
match) — "a unique burn pattern even when the same building is
destroyed multiple times," from the same mechanism, no extra state
needed.

## 1.5. Multi-hex buildings — which hex actually catches fire

Medium/Large-tier buildings genuinely span multiple hexes (1/2/4 per
`CityGenerator.cs`'s own tier table), each with its own massing cube
(`RuntimeCityBuilder`'s `cubes` list, one per footprint hex, same index
order as `building.Footprint`). Ignition and ongoing heat feed both
resolve WHICH hex via `hitHex` — a `HexCoord` `MonsterAgent.TickAttack`
already computes (`NearestFootprintPoint`, converted via
`_builder.HexAt`) as the same point its weapon FX beam/shot converges
on, now threaded through `IgniteBuildingIfNeeded`/`ApplyBuildingDamage`.
`RuntimeCityBuilder.FootprintIndexOf(Building, HexCoord)` maps that hex
to its massing cube (a plain linear scan, capped at 4 iterations for
the largest tier — nowhere near "expensive," and it only runs once per
landed hit). Ignition tracking is per-cube (`HashSet<GameObject>
_ignitedCubes`), not per-building, so each hex of a multi-hex structure
ignites independently from wherever it's actually being hit, and a
later-rebuilt hex (a fresh `GameObject`, fresh reference) automatically
starts unignited again.

**Progressive whole-structure ignition as HP falls.** HP is shared
across a multi-hex building's WHOLE footprint (`_battlefield.Buildings`
is keyed by `Building`, not by hex) — every hex was already going to
collapse together the instant HP hit zero (`ApplyBuildingDamage`'s
`Destroyed` branch already shatters every cube in one pass). The gap:
only the specifically-hit hex ever showed fire on the way there, so a
whole footprint could suddenly turn to rubble while only one corner had
ever visibly burned — not "logically destroyed," a surprise instead of
an earned collapse. Fixed in `ApplyBuildingDamage`: as the building's own
HP fraction falls, `targetIgnitedHexes = Ceil(footprintCount * urgency)`
(`urgency = 1 - hpFraction`) more of its OTHER footprint hexes get
`IgniteBuildingIfNeeded` calls too, on top of the hit hex (which still
always ignites first, unconditionally). At full health only the hit hex
burns; near death, nearly (or fully) every hex is alight. Reuses the
SAME idempotent per-hex guard `_ignitedCubes` already provides — calling
it again for an already-lit hex is a cheap no-op, so this is purely
"ignite more hexes over time," no new state, no new mechanics.
Single-hex buildings (`Footprint.Count == 1`, the common case) are
completely unaffected — the loop only runs for genuinely multi-hex
structures.

## 1.6. Which building gets attacked at all — the attack hierarchy (MonsterAgent, not this file)

Everything above assumes `MonsterAgent` has already decided which
`Building` (and which of its hexes) to hit this tick. That decision
itself lives in `MonsterAgent.TickAttack`, NOT in `DamageFx.cs`, but
it's worth cross-referencing here since it's what feeds every
`RegisterHit`/`IgniteBuildingIfNeeded` call in this whole system:
**Target > Collateral > Reposition.** A monster with a real projectile
weapon (`isProjectile = armed && Weapon.Kind != Melee` — melee/unarmed
are exempt entirely, no line of fire to obstruct) checks line of sight
to its target before firing; if blocked by ANOTHER building, a player-
owned one is never a valid collateral target (forces reposition, no
roll), a civilian one rolls this creature's own genome `fury` (reused as
"aggression," no new genome schema) — success does a FULL retarget
through this exact fire/damage/collapse pipeline, not a scripted one-off
smash, resuming the original target once the collateral one is
destroyed. `MonsterCombatProfile.MobMentalityBonus` lets a nearby
monster's own ongoing collateral attack raise THIS creature's odds of
joining in, without ever guaranteeing it. Full detail: `docs/12`'s own
"attack hierarchy" entries.

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
`RuntimeCityBuilder.ApplyBuildingDamage`, now for the SPECIFIC hex it
landed on — see §1.5) no longer feeds every hit into one fixed cell —
`PickWeightedColumn` picks a column weighted `{2,3,4,3,2}` across the 5
angle columns (center 2x more likely than either edge — pulled down
from an original `{1,2,3,2,1}`/3x pass per "decrease but do not
eliminate the spawn facing camera preference"), so heat starts from
several lateral points across the visible facade, skewed toward — never
exclusive to — the most directly camera-facing angle. This is what
fixed the "burn line" (§0.5 row 8): combined with the strong upward
bias, a single fixed origin reliably produced one vertical stack.

**Cluster bias — islands, not a scatter.** §0.5 row 13: picking
`PickWeightedColumn`'s independent random column on every single hit
reliably seeded 3-4 separate low-heat columns that each limped toward
ignition on their own — camera weighting controls WHICH angle hits land
near, but says nothing about whether repeat hits reinforce the SAME
column or keep re-seeding new ones. `PickColumnForHit` now sits in front
of `PickWeightedColumn`: most hits (`ClusterBiasChance`, 0.72) instead
feed whichever column is already hottest (`HottestColumn`, sums a
column's heat across all 3 height bands), so heat concentrates into one
dominant, visibly-growing cluster rather than diluting evenly. The
remaining ~28% of hits still fall through to the old camera-weighted
random pick — enough to let a couple of genuine satellite fires start
elsewhere, matching "a bigger fire... and maybe 1-2 smaller ones," not
zero variety. No change to diffusion, ignition, or embers — this only
changes which column a landed hit's heat goes into.

**Urgency and attack rate.** `RegisterHit(float energy, float
hpFraction01)` receives the building's current HP fraction alongside
the hit's own damage amount. `_urgency` (0 at full HP, → 1 as HP nears
zero) and `_hitRateEma` (a smoothed hits-per-second reading off real
`Time.time` gaps between calls — a proxy for "number of attack points,"
since there's no real attacker-identity tracking anywhere in this
pipeline, but more simultaneous attackers necessarily means hits land
closer together in time) both feed:

- `CurrentSimTickInterval` — the sim ticks faster (down to
  `MinSimTickInterval`, 0.12s) as either climbs, ALSO multiplied by the
  creator-facing `DamageFxProfile.FireSpawnRateMultiplier` (default 1.0,
  see §4).
- `_maxIgnitedCells` — raised above its area-based floor
  (`_baseMaxIgnitedCells`) by up to `MaxUrgencyBonusCells` (6, raised
  from 3 — "make it look logical the building would collapse"), capped
  at `MaxFireCountCeiling` (15, the grid's own true max, raised from 10 —
  a building right at the edge of destruction can now have nearly its
  ENTIRE visible facade on fire, not still capped a third short of full
  coverage).

Net effect: a building near destruction, or one being hit by several
attackers at once, visibly grows fire faster and further than the same
building taking occasional single hits — "shorter time [to destruction],
more spawns."

## 3.4. Randomized per-hit ignition — "not mechanical"

"randomize if projectile will cause a fire. Goal make the fire pattern
look organic, NOT non procedural" (read as "not [obviously] procedural"
— the throughline of this whole system, from the heat-network rewrite
through per-cell `Flammability`, has been making spread read as organic
rather than a visibly mechanical simulation response). Every landed hit
used to feed `RegisterHit`'s heat network unconditionally — a strict 1:1
relationship between damage and fire growth, itself perfectly
deterministic no matter how organically the heat then diffused.

`RegisterHit` now rolls `DamageFxProfile.FireIgnitionChancePerHit`
(default 0.6, `[0.1, 1]`) per hit BEFORE the heat-injection lines run — a
failed roll still deals real damage (this happens in
`RuntimeCityBuilder.ApplyBuildingDamage`, upstream of `RegisterHit`
entirely), it just has no visible fire consequence that time. Deliberately
narrow: `_hitRateEma`/`_urgency` tracking still runs on EVERY real hit
unconditionally — that represents actual attack pressure, not this hit's
own visible response, so randomizing it too would have made the ignition-
pacing/fire-count-ceiling math itself noisy, which wasn't asked for. The
very first ignition (`IgniteBuildingIfNeeded`, "spawn fire when under
attack") is untouched — it was never gated on `RegisterHit` to begin
with, so a building still always shows fire the instant combat starts.

## 3.5. Persistent per-instance visual variation

"Each visible fire instance should receive a persistent random seed...
adjacent flames should never look cloned." One `FirePlume` IS a "visible
fire instance" — its own `GetInstanceID()` (this file's standing
deterministic-hash convention, never `UnityEngine.Random`) is sampled
ONCE in `Awake` via a small local `Jitter(seed, salt, min, max)` helper
into 9 persistent multipliers: height, width, brightness, emissive
intensity, growth rate, lifetime, flicker speed, plus a fixed lean
yaw/tilt. "Animation phase" was already persistent-per-instance via the
pre-existing `_flickerPhase` seed — unchanged, just counted among the
nine.

Every `FirePuff` a plume spawns over its life reads these instead of a
flat shared constant, so puffs from the SAME plume share a coherent
"personality" (a consistent lean direction, a consistently faster/
slower flicker) while DIFFERENT plumes — each with their own instance
ID — never line up. No new fields beyond plain floats, no new
GameObjects/components, nothing computed per-frame beyond a handful of
multiplies already happening.

**Height/width independence needed one shared-class extension.**
`SmokePuff` (reused by smoke/dust/water/muzzle, not just fire) always
applied a UNIFORM scale every frame off its own `_baseScale` — setting a
non-uniform `localScale` at spawn alone would get silently overwritten
the next frame. New `SmokePuff.SetScaleAxisMultiplier(Vector3)` defaults
to `Vector3.one`, so every OTHER consumer is byte-for-byte unaffected;
only `FirePlume.SpawnPuff` calls it, with `(widthJitter, heightJitter,
widthJitter)`.

## 4. DamageFxProfile / MonsterCombatProfile — the tuning surfaces

`DamageFxProfile.cs` (a `ScriptableObject`, same pattern as
`CityLightingProfile`) holds the fire/smoke knobs:
`SmokeResizePct`/`SmokeGrowthMultiplier`/`SmokeRiseSpeed`/
`SmokeWindSpeed`, `FireResizePct` (point-light range/intensity only),
`FireSizeBoostPct` (flame-mesh size, default 1.18), `FireSpawnRateMultiplier`
(default 1.0, multiplies `CurrentSimTickInterval`'s existing speedup
factor — "give me an inspector setting for spawn rate of fires"), and
`FireIgnitionChancePerHit` (default 0.6, §3.4's per-hit organic-pattern
roll). Everything ELSE in this doc — grid layout, heat/ventilation
constants, bias ratios, urgency tuning — lives as `private const` fields
directly on `FireCluster` in `DamageFx.cs`, not on the profile asset;
they're simulation tuning, not per-playtest visual knobs, so there was
no ask (yet) to expose them at runtime.

A SEPARATE new `MonsterCombatProfile.cs` (same ScriptableObject pattern,
its own `Assets > Create > MadDr > Monster Combat Profile` menu, wired
into `RuntimeCityBuilder` identically) holds the first entry in a
combat/AI-behavior tuning domain, distinct from either fire/smoke or
lighting: `MobMentalityBonus`/`MobMentalityRadius` (§1.6's mob-mentality
aggression bonus). Not a fire-system asset itself, but referenced here
since it directly shapes which building `MonsterAgent` ends up feeding
into this whole pipeline.

## 5. What's wired up today vs. what's still a stub for later

| Path | Status |
| --- | --- |
| Procedural civilian buildings (`RuntimeCityBuilder`) | Full pipeline: ignition on in-range (gated behind line-of-sight for projectile weapons, §1.6), per-hex `RegisterHit` on every landed hit (§1.5), randomized per-hit fire contribution (§3.4), raycast placement, persistent visual variation |
| Multi-hex buildings (Medium/Large tiers) | Each footprint hex ignites/feeds independently from whichever hex is actually under fire; OTHER hexes progressively ignite too as the building's shared HP falls, so a full-footprint collapse reads as earned (§1.5) |
| Collateral/attack-hierarchy targeting | Live in `MonsterAgent`, cross-referenced §1.6 — which building even reaches this pipeline is now itself a real decision (Target > Collateral > Reposition), not just "whatever's under the cursor" |
| RTS building roster (`BaseDresser`) | Ignition + placement shared (same `FireCluster`/`AttachFireCluster`); **`RegisterHit` is NOT wired here** — `BaseDresser`'s own damage path never calls it, so RTS-roster buildings ignite but their fire never speeds up/spreads from urgency or attack rate the way procedural buildings' does; also single-hex only today (no multi-hex footprint concept on that roster), so the progressive-ignition mechanic above never applies to it either |
| Smoke (`SmokeCluster`, §7) | Full pipeline at BOTH call sites (procedural + RTS roster) — reactive to each building's own real `FireCluster` heat state, since both call sites attach fire first and hand it straight to `AttachSmoke`. Standalone ambient `ChimneySmoke` (`BaseDresser`, ties to nothing here) still uses the older `SmokePlume` directly, unaffected |
| Real per-hit 3D impact points | Not implemented — see §6 |
| Fuel depletion / fire going out | Not implemented — deliberate, see §1 |
| Fire spreading to an ADJACENT (separate) building | Not implemented — the grid is scoped to one `Building`'s own footprint only; a "collateral" attack (§1.6) retargets a whole separate building deliberately via monster AI, which is different from fire itself leaping across |
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
- **Give player-owned `SimBuilding`s real colliders + a `BaseDresser`-side
  registry**, mirroring `RuntimeCityBuilder._buildingByCollider`/
  `BuildingFromCollider` -- would let §1.6's line-of-sight check detect
  them via the SAME raycast civilian buildings already use, instead of
  the separate `SimBridge` position-scan (`IsBlockedByPlayerBuilding`)
  it needs today purely because they're collider-less.
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
- ~~**Smoke/fire heat tie-in.**~~ **Implemented, 2026-08 — see §7.**
  `SmokeCluster` now reads `FireCluster`'s real per-column heat directly.

## 7. SmokeCluster — reactive smoke, decoupled from the fire renderer

§0.5 row 14's full implementation. One `SmokeCluster` per building,
attached by the SAME `DamageFx.AttachSmoke` call site both `RuntimeCityBuilder`
and `BaseDresser` already used for the old `SmokePlume` — only the class
that gets created there changed. "Decoupled" has a precise meaning here:
`SmokeCluster` reads a handful of PUBLIC, read-only accessors off its
building's `FireCluster` (`ColumnPeakHeat`, `ColumnTotalHeat`,
`ColumnHasFlame`, `Urgency01`, `ColumnHottestBandHeightFrac`,
`GetVentPoint`, `VentColumnCount`, `FlameIgnitionHeat`) and nothing else
— `FireCluster` has no reference to `SmokeCluster` and no idea it exists;
every one of those accessors is additive (new methods/properties only),
so not one line of `FireCluster`'s own ignition/diffusion/ember/embers
logic changed to make this possible. Deleting `SmokeCluster` entirely
would leave fire behaving exactly as it did before this row.

**Why the old design read as "smoke missing."** Investigated against
`SmokePlume`, the class `AttachSmoke` used to create — NOT a shader,
render-queue, culling, LOD, or object-pool bug (`LabMeshBuilder.
MakeTransparent` is the identical transparency setup already confirmed
working for the battlefield's water surfaces and the mastermind's glass
dome; this file has never used a real object pool for any puff kind
anywhere, so there was never a pool to be malfunctioning). Three real,
compounding causes: (1) exactly ONE plume, total, per building, however
big it was — fine against a Small house, lost against a Landmark's much
taller multi-column blaze; (2) a genuine multi-round size-shrinking
history (`SmokeResizePct`'s own doc comment traces 0.7 → a since-reverted
1.6x → 0.2, each a real fix for a real "smoke swallows the fire" report
at the time) left it too small to read once stacked with (1); (3) zero
connection to the fire simulation — the old plume started the instant a
building took ANY damage and puffed at one flat rate/size forever, with
nothing for a "smoke before flames" sequence to hang off.

**Shared vents, not one source per flame.** `SmokeCluster` reuses
`FireCluster`'s own 5-angle-column grid as its vent slots (`Vent.Column`,
one struct per column, no GameObject until a slot actually activates) —
never one smoke source per individual fire CELL (up to 15 per building).
A column with all 3 height bands ablaze (routine now that fire itself
clusters into fewer, bigger columns, §0.5 row 13) still gets exactly ONE
smoke vent, just a more intense one — "large flame clusters may share one
larger emitter" falls out of reusing fire's own grid rather than needing
separate bookkeeping.

**Smoke before flames.** Each active tick (`ClusterTickInterval`, 0.35s —
deliberately slower than `FireCluster`'s own sim tick, down to 0.12s
under urgency: "update smoke simulation at a lower frequency than flame
animation") reads `ColumnPeakHeat` — the hottest SINGLE band in that
column — against `SmokeOnsetFraction` (0.3) of `FireCluster.
FlameIgnitionHeat`. A column starts smoking once its peak heat crosses
that fraction, well before any one band reaches the FULL ignition line a
real `FirePlume` needs — so as heat diffuses into a new column, smoke
shows there before flame does. The one place this lead time can't apply:
a cluster's own very first hit, where `FireCluster.Init` seeds its origin
cell at `InitialImpactHeat` (1.2, already past ignition) so that ONE cell
ignites the same instant it's created ("starts with 1 immediately," an
existing, untouched fire contract) — smoke can't out-race a flame that's
already alight before `SmokeCluster`'s first tick ever runs. Every other
column this fire spreads to still gets the full lead time.

**Smoke growth + colour, continuously, not in tiers.** Once a column is
smoking, `ColumnTotalHeat` (summed across all 3 bands, so it keeps
climbing well past first ignition as more bands catch — `FireCell.Heat`
never cools, §1's own convention) divided by `SmokeSaturationHeat` (3.2,
"fully involved") gives one continuous `intensity01`, smoothed frame to
frame (`Mathf.Lerp(..., 0.5f)` each tick) rather than snapped — puff
size, alpha, spawn rate, and colour (`SmokeColorForIntensity`: light grey
→ dark grey/near-black, `Color.Lerp` off the SAME `intensity01`, boosted
by `Urgency01` once flame is confirmed) all read straight off this one
number, so "should increase smoothly rather than in discrete steps"
holds for every visual property at once instead of needing separate
tiering per property. No fuel-depletion "lightens again" stage: `FireCell.
Heat` never depletes in this simulation (§1's own deliberate
simplification, unchanged) so there's no real signal to drive a genuine
"as fuel is consumed, smoke lightens" phase — faking one off a timer
would contradict the fire sim it's reacting to, so it's honestly left out
rather than faked.

**Motion: surface normal + up, never straight through the building.**
`GetVentPoint` reuses `FireCluster`'s OWN collider raycast
(`PickSurfacePoint`, now with a normal-returning overload) at whichever
height fraction that column's heat currently peaks (`ColumnHottestBand
HeightFrac`, plus a small `VentHeightAboveFlame` nudge, capped at
`MaxVentHeightFrac` — "not on roofs" holds for smoke too) — so a vent
point is pinned to real geometry exactly like a flame point is. Each
puff's drift is `(surfaceNormal + Vector3.up * VentUpBias).normalized`
scaled by intensity — "surface normal + up vector," so a wall fire's
smoke peels outward while rising, a corner fire's smoke exits diagonally
(the blended normal of whichever face the raycast actually lands on), and
a roof-level fire's smoke rises nearly vertically (normal ≈ up already).
Layered on top: a perpendicular sway (`SmokePuff.SetTurbulence`, a NEW
method — `_swayAxis` defaults to `Vector3.right` so `FirePlume`'s
existing fire-lick sway via `InitFlame` stays byte-identical) gives the
"curl/turbulence/random variation" the brief asks for, amplitude and
speed both scaling with `Urgency01` ("large fires: turbulent motion").
Puff size growth-over-life (existing, capped `SmokeGrowthMultiplier`
mechanic, unchanged) is what makes a plume visibly widen as it rises —
no separate widening mechanic needed.

**Performance.** Vents are capped at 5 per building (`FireCluster.
VentColumnCount`) regardless of how many individual fire cells are lit
(up to 15) — the real lever this brief's "share smoke sources" asks for.
Each vent only spawns a puff GameObject on its OWN throttled timer
(`Mathf.Lerp(MaxPuffInterval, MinPuffInterval, intensity)`, 2.4s→0.6s),
so a lightly-smoking building costs close to nothing and only a fully-
involved, high-urgency structure with several active vents approaches
the ceiling (~8 puffs/sec worst case for one building, still short of a
real particle system, each puff a cheap 6-sided low-poly mesh with no
collider/physics — the SAME `SmokePuff`/`ProceduralMeshKit.CloudShard`
machinery every other puff kind in this file already uses). No true
object pool (this file has never used one for any puff kind, see this
section's own root-cause writeup above) — "pooling" here means capping/
sharing emitter COUNT relative to flame count, not reusing GameObjects.

**A real, unrelated bug fixed in passing.** `RuntimeCityBuilder`'s own
Intact→Damaged darkening pass (a `MaterialPropertyBlock` override loop)
already explicitly skips any renderer parented under a live `SmokePlume`
or `FireCluster` — a previously-fixed "puff freezes solid" bug (the
darkening block's own `Color` constructor defaults to alpha 1, so a puff
caught by that sweep gets permanently forced opaque, since a
`MaterialPropertyBlock` override always wins over the puff's own
per-frame `_mat.color` mutation). That skip-check is keyed off component
TYPE, so swapping the damage-smoke wrapper from `SmokePlume` to
`SmokeCluster` would have silently reopened it for the new class. Fixed
by adding `SmokeCluster` to the same skip-check.

## Verification

No Unity Editor exists in this environment — every fix past §0.5 row 6
is reasoned from the creator's own reports/briefs and traced against
real code (`SpawnCube`'s actual collider setup, `BuildingDresser`'s
actual lack of one, `CityGenerator.cs`'s own tier-footprint-size
comment, `GenomeDto.cs`'s own `Brain.Params` shape, the brief's own
literal percentages), not guessed. Nothing here has been confirmed by an
actual render since the heat-network rewrite (row 7) shipped — treat
rows 7-14 as "reasoned and internally consistent," not "confirmed
working," until a real Play-mode/screenshot report comes back, the same
standard `docs/28` holds its own unconfirmed rows to. Row 14 (`SmokeCluster`)
carries the same caveat, PLUS one extra unknown worth flagging explicitly:
its tuning constants (`SmokeOnsetFraction`, `SmokeSaturationHeat`, puff
interval/size/alpha ranges) are reasoned from first principles against
the brief, not calibrated against a single prior confirmed-working smoke
render the way most of this file's fire constants at least started from.
If you're picking this up cold and want the single most likely next
real-world check: a
sustained attack on a genuinely multi-hex (Medium/Large-tier) building,
watched all the way to collapse, would exercise nearly everything in
rows 9-12 at once (per-hex ignition, progressive whole-structure
spread, randomized per-hit contribution, and — if a second building
happens to be in the way — the collateral-attack hierarchy) in one
session.
