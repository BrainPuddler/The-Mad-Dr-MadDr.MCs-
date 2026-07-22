# 25 — Monster Movement: Hybrid Steering + Deadlock Recovery Migration Plan

Status: **Approved migration plan, Phases A-C implemented** (written 2026-07
after an analysis-only pass; creator approved the plan 2026-07 — "I approve
the plan, now capture it"; Phases A, B, and C all landed 2026-07, Phases
D-E not started) · Realizes a collision-behavior replacement for the
Unity-side monster mover · Pillars served: 2 (*the battlefield breathes*),
3 (*honest combat*).

> **Status discipline.** This doc is a plan. No code has been written under
> it yet. When Phase A (below) lands, update this doc's per-phase status
> lines and append a docs/12 decision-log entry — do not silently drift the
> plan out of sync with the implementation the way a stale design doc would.

## 0. Problem statement (creator direction, verbatim intent)

Multiple monsters moving toward the same waypoint currently collide, push
against each other, and can become permanently stuck. Replace the current
ad hoc collision reaction with:

- **Layer 1 — Natural steering**: Seek + Separation + Predictive collision
  avoidance + Speed modulation (slow when blocked, don't push endlessly).
- **Layer 2 — Deadlock recovery**: detect a unit that wants to move, has a
  valid destination, but hasn't made meaningful progress for a configurable
  period; temporarily grant it priority, make blockers yield/sidestep, then
  release back to normal steering once movement resumes.

Constraints: no NavMesh, no A* (already have `HexPathfinder` for the global
route — steering is *local* only), no full flocking/boids, no per-frame
sorting, no per-frame allocations, scale to many monsters, preserve current
performance.

## 1. Current architecture (as analyzed, 2026-07)

Movement ownership is split across two scripts and it matters which does
what:

| Concern | Script | Detail |
| --- | --- | --- |
| AI state machine + movement decisions + the actual transform move | `MonsterAgent.cs` | Owns everything |
| Procedural animation / view (legs, wings, flight-lift, selection box) | `MonsterBody.cs` | Owns nothing about *where* the monster goes on the XZ plane |
| Global routing (A*) | `HexPathfinder` (`packages/citygen-core`) | Engine-agnostic, pure, unchanged by this plan |
| Local collision reaction (today) | `RuntimeCityBuilder.cs` | `ApplySeparation` + `AvoidanceDir` |
| Orders / selection | `WaypointCommander.cs` | Issues orders only, unaffected |

- **State machine**: an `_order` enum (`Idle / Move / AttackBuilding /
  AttackUnit / EatCitizen / Perch`) dispatched every frame in
  `MonsterAgent.Update()` to `Tick*` methods, each **returning a `Vector3`
  velocity**.
- **Which script actually moves the monster**: `MonsterAgent` itself, via
  direct `transform.position` writes (`FollowPath`, `TickSettle`,
  `TickPerch`, `TickEat`). **No Rigidbody, no CharacterController, no
  physics stepping anywhere on a monster** — pure transform manipulation.
- **`MonsterBody.UpdateLocomotion(velocity, dt)`** is a pure *view*: it
  consumes the returned velocity to drive stride cadence, wing-flap, and
  flight-lift, and repositions the selection `BoxCollider` — it never writes
  the root XZ position. The velocity return is therefore a **contract**
  every `Tick*` must keep honoring: "here's how I moved, animate
  accordingly."
- **Waypoints / path**: `_waypoints` (`Queue<HexCoord>`) → `_path`
  (`List<Vector3>` from `HexPathfinder.FindPath`, via `ToWorldPath`). A* is
  the global route around solid buildings; re-run only on city-version
  change or a moving chase target, never per frame.
- **Collision handling today** — two pieces, both `O(N²)` per frame, both in
  `RuntimeCityBuilder`:
  - `ApplySeparation(self)` — called every frame from `MonsterAgent.Update()`.
    A **positional overlap resolver**: pushes `self` out by half the
    penetration against every other combatant with overlapping bodies.
    Minimum gap = `self.Radius + c.Radius + groupSpacing` (the Inspector
    field shipped just before this plan).
  - `AvoidanceDir(self, desiredDir)` — called inside `FollowPath`. A crude
    ahead-cone arc: deflects the desired heading sideways around anything
    within a forward cone. Returns a steering *direction*, not a force.
- **The only collider on a monster** is `MonsterBody._selectionCollider` (a
  `BoxCollider`) used solely for click-selection raycasts — never physics.

**Where steering logic slots in**: exactly at the two existing seams — the
`AvoidanceDir` call site inside `FollowPath`, and the `ApplySeparation` call
site in `Update`. Both already take a unit + direction/self and
mutate heading or position; the new controller replaces their bodies without
touching the `_order` state machine.

## 2. Root cause of collision/sticking (as analyzed, 2026-07)

**Not Unity physics** — monsters never collide physically. The problem is
three transform-based systems fighting each other with no arbiter:

1. **Seek and Separation are applied in sequence, not blended.** Seek moves a
   unit toward the jam; separation shoves it back out; next frame seek shoves
   it back in → oscillation.
2. **Separation is a hard position edit, not a force.** It can push a unit
   off its path line into a spot where the next path node is now occluded —
   and there is no "blocked by units" re-path trigger (re-path only fires on
   city-version change or a moving chase target). The unit is wedged with a
   target it can no longer approach.
3. **No speed modulation**: a blocked unit keeps applying full seek into the
   jam, maximizing the fight instead of easing off.
4. **No deadlock detection at all**: nothing notices "this unit has wanted to
   move for N seconds and gone nowhere," so wedges are permanent.
5. **`AvoidanceDir` only runs while path-following and is positional**
   (ignores where the other unit is *going*), so crossing/head-on courses
   collide first and let separation clean up after, rather than predicting
   the conflict.

The ring-settle change (docs/12, 2026-07: units settle on a ring around a
shared waypoint, not on top of it) already mitigates *destination* clumping.
**En-route corridor congestion** — many units funnelling through a one- or
two-hex gap between solid buildings — is unsolved and is where permanent
sticking happens. Both existing reaction systems are also `O(N²)` per frame,
capping unit-count scaling independent of the deadlock problem.

## 3. Approved architecture

- **`MonsterSteeringController`** — computes a desired velocity that
  `MonsterAgent`'s `Tick*` methods consume, replacing `AvoidanceDir` +
  `ApplySeparation`. Combines: **seek** (already computed in `FollowPath` as
  `dir`), **separation-as-a-force** (converted from today's position-edit so
  it blends with seek instead of fighting it), **predictive avoidance**
  (velocity-based; requires neighbours' last-known velocity, not currently
  published — add one stored field), and a **deadlock nudge** input from
  Layer 2. Never writes `_order`. Stays transform-based (no Rigidbody),
  matching every other mover in the project. Airborne/perched units keep
  their existing opt-out (`!_flying && !Perched`) — flight's carve-don't-
  strafe arc in `FollowPath` is untouched.
- **Spatial Neighbor System** — a **uniform grid**, cell size = one
  `HexMeters` (reuses the project's existing spatial unit), rebuilt once per
  frame, queried by both separation and avoidance. Pre-sized/reused buffers
  → allocation-free. Replaces both `O(N²)` scans; this is the primary perf
  win. No sorting, no NavMesh, no A*.
- **`DeadlockManager`** — rare-path only. Polls for units that *want to move
  + have a valid destination + made < ε progress for T seconds*; grants one
  temporary priority; makes its blockers briefly yield/sidestep into
  non-`Blocked()` hexes (solid-building law preserved); releases back to
  normal steering once movement resumes. Reads/writes a per-unit
  priority/yield flag the steering controller honours. Never becomes the
  primary mover.

## 4. Files touched

**Modified (surgical, logic-preserving elsewhere):**
- `MonsterAgent.cs` — swap the `AvoidanceDir` call site (inside `FollowPath`)
  and the `ApplySeparation` call site (in `Update`) for the steering
  controller's combined output; store per-unit last velocity; expose a
  minimal "want-to-move / progress" surface for `DeadlockManager`. State
  machine, flight, perch, eat, harvest, group-facing, ring-settle: unchanged
  in logic.
- `RuntimeCityBuilder.cs` — retire `ApplySeparation`/`AvoidanceDir` (thin
  shims during migration if useful); host and wire the neighbour grid and
  `DeadlockManager`; `groupSpacing` continues to feed the separation radius.

**New scripts** (created only as each phase below is executed):
`MonsterSteeringController.cs`, `SpatialGrid.cs`, `DeadlockManager.cs`.

**Explicitly untouched:** `HexPathfinder` / `packages/citygen-core` (A* stays
the global router; steering is local only), `MonsterBody.cs` (pure view; the
velocity-return contract is unchanged), `WaypointCommander.cs` (orders
unchanged, including ring-settle), `packages/match-core`, `Tank.cs` /
`Citizen.cs` / `TrafficCar.cs` (independent movers, out of scope for this
plan).

## 5. Risks & compatibility concerns

- **Ring-settle regression**: the "distribute around the waypoint, not onto
  it" result (docs/12, 2026-07) must survive the separation-as-force rewrite
  — verify against it explicitly, don't just assume.
- **Flight opt-out**: airborne/perched units must stay untouched by the new
  controller; flight's carve arc in `FollowPath` stays as-is.
- **Overtaking asymmetry**: today's "faster-from-behind arcs around, the
  unit in front isn't deflected" property (falls out of `AvoidanceDir`'s
  geometry) must survive the rewrite or overtaking breaks.
- **Forward-looking determinism**: this lives Unity-side today, not in
  `packages/match-core`'s deterministic tick sim — bit-determinism isn't
  required yet, but docs/23 §13-A will eventually port unit movement into
  that sim. Designing the steering math integer/fixed-point-friendly and
  neighbour-iteration-ordered now avoids a second rewrite at port time; a
  small upfront cost worth paying.
- **No Editor in this environment**: every phase verifies via standalone
  numeric harnesses (steering-vector math, grid-vs-brute-force neighbour
  parity, deadlock-resolves-within-T scenarios) plus the flightcheck
  stub-compile harness. Never claim visual verification.
- **Contract stability**: every `Tick*` must keep returning a velocity
  `MonsterBody` can animate from — this is the one hard interface boundary
  the whole plan hangs off.

## 6. Phased implementation plan

Each phase is independently testable and should land as its own commit with
a docs/12 decision-log entry, per repo convention.

- **Phase A — `SpatialGrid` neighbour system.** Drop-in uniform grid,
  rebuilt once per frame; route the *existing* separation + avoidance scans
  through it, behaviour-identical to today. *Test:* grid neighbour set ==
  brute-force neighbour set on random layouts; per-frame cost flat vs unit
  count (not growing `O(N²)`). *Risk: lowest — pure perf refactor, zero
  behaviour change.* **Status: done (2026-07).** New `SpatialGrid.cs`
  (generic, pooled, cell size = `HexCoord.HexMeters`); `RuntimeCityBuilder`'s
  `ApplySeparation`/`AvoidanceDir` now query it (lazy per-frame rebuild, no
  script-execution-order dependency) instead of scanning `_combatants`
  directly — every per-pair math line unchanged. `MonsterAgent.cs` untouched.
  Verified: flightcheck stub-compile clean; standalone harness compiling the
  real `SpatialGrid.cs` shows 0/200 neighbour-set mismatches vs brute force
  on randomized layouts, and query cost flat (~0.6-1.0ms) from 100 to 8000
  units while brute-force scan cost grows roughly linearly (9.6ms->6.5ms,
  N=100 low outlier is JIT warmup).
- **Phase B — `MonsterSteeringController` scaffold, parity first.** Move
  seek + separation-as-force + the ported ahead-cone avoidance into one
  `Combine()` returning a steering velocity, called from `FollowPath`;
  convert `ApplySeparation`'s position-edit into a blended force. Target:
  *behaviour parity* with today, not new capability yet. *Test:* scripted
  2-unit overtaking / shared-destination scenarios match current outcomes
  numerically. **Status: done (2026-07).** New `MonsterSteeringController.cs`
  (`SeparationForce` + `AvoidanceBias`, both extracted verbatim from the old
  inline math, plus new `Combine`); `RuntimeCityBuilder.SteerFollowPath`
  replaces `AvoidanceDir` at `FollowPath`'s one call site. A numeric harness
  found a soft heading blend alone does NOT stop two units driving straight
  at each other from interpenetrating, so `ApplySeparation`'s hard
  positional correction was deliberately kept unconditional (unchanged from
  before this phase) rather than folded away — `Combine`'s separation term
  is an earlier-reacting nudge on top of it, not a replacement; full
  force-only separation is deferred to Phase C. Verified: flightcheck
  stub-compile clean; a fresh standalone harness confirmed `SeparationForce`
  exactly matches the old inline math (500/500 randomized trials, <1e-5
  diff) and that scripted overtake / shared-destination / co-linear-follow
  scenarios produce the same qualitative outcomes (no interpenetration,
  overtake asymmetry preserved, stable settling) under both the old
  sequential pipeline and the new blended one. docs/12 has the full entry.
- **Phase C — predictive avoidance + speed modulation.** Add per-unit
  published last velocity; replace the ahead-cone with time-to-collision
  (RVO-lite) avoidance; slow seek when avoidance/separation dominate.
  *Test:* head-on and 90°-crossing pairs resolve smoothly without overlap; a
  blocked unit slows rather than shoving. **Status: done (2026-07).** New
  `UnitCombat.LastVelocity` (published by `MonsterAgent` every frame,
  never set by `Tank.cs` -- a tank predictively reads as stationary, a
  safe default); `MonsterSteeringController.PredictiveAvoidance` replaces
  `AvoidanceBias` with a proper TTC check (closest-approach time and
  distance from both units' current velocities, only reacting within a
  2.5s horizon); `Combine` now returns a `SteeringResult` (direction +
  speed scale, the scale from how aligned the chosen heading still is with
  the seek direction, floored so steering alone never fully stops a unit).
  `ApplySeparation` itself is unchanged -- still the hard, unconditional
  never-overlap guarantee; only the softer blend layered on top of it
  changed. A standalone harness caught and fixed a real relative-velocity
  sign bug (predictive avoidance never fired at all until corrected) and a
  smaller dead-zone bug between the padded and bare-body radius checks;
  after both fixes, head-on/90°-crossing/single-blocker scenarios all
  clear without interpenetration, with a measurable speed ease-off around
  the blocker that recovers once clear. See docs/12 for the full writeup.
- **Phase D — `DeadlockManager`.** Detect stalled-but-wanting-to-move units;
  grant temporary priority; blockers yield/sidestep into non-blocked hexes;
  release on progress. *Test:* a forced single-hex-corridor jam of N units
  clears within T seconds; solid buildings are never entered by a
  sidestepping unit. **Status: not started.**
- **Phase E — cleanup + tune.** Remove old `ApplySeparation`/`AvoidanceDir`
  entirely (no shims left); tune weights against the ring-settle and
  corridor-jam cases; final docs/12 entry closing this plan out.
  **Status: not started.**

## v0.1 tuning appendix

To be filled in as each phase lands: separation force gain, avoidance
lookahead/time-horizon, seek-slowdown curve, deadlock stall threshold T and
progress epsilon ε, priority-grant duration, sidestep hex-selection rule. All
placeholders until playtested per this repo's general v0.1 numbers policy.
