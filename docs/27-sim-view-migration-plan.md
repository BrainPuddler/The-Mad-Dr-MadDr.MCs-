# 27 — MonsterAgent → Interpolated View: the Sim/View Migration Contract

Status: **Approved architecture, Phase A implemented (2026-07)** · Realizes
docs/23 §13 amendment A's Unity-side half ("MonsterAgent rewritten to render
interpolated sim state only") · Extends
[23-rts-master-build-plan.md](23-rts-master-build-plan.md)'s Phase 1.5 and
[25-monster-movement-steering-plan.md](25-monster-movement-steering-plan.md)'s
steering work · Pillars served: 1, 3 (a monster that's still yours, still
fighting, and now also lockstep-hashable).

## 0. Problem statement

`packages/match-core` (docs/23 Phase 1.5, shipped 2026-07) now has a
deterministic, fixed-tick `SimUnit`/`MatchState.MoveTo` that reproduces
Unity's own `HexPathfinder`/`BattlefieldState` pathing exactly, hash-provably
identical across two runs. Nothing in Unity consumes it yet — `MonsterAgent`
still decides its own movement every `Update()`, in float world-space, off
`Time.deltaTime`. docs/23 §13 amendment A calls the Unity-side half of this
port "the plan's true long pole" and specifies a hard bar: **zero gameplay
decisions left in `MonsterAgent.Update()`**, only rendering of interpolated
sim state.

Rewriting `MonsterAgent` for that bar in one blind pass is not attempted here.
Ten already-shipped phases of this session's own work (docs/26 Special
Attacks System Phases 1–10, docs/22 harvest, docs/25 steering) sit on top of
its current `_order` state machine, and this environment has no Unity Editor
to visually verify the result. This document is the **design** the creator
asked for: the interpolation contract, and an INCREMENTAL cutover — one order
kind at a time, each independently shippable and each leaving every
not-yet-ported order kind exactly as correct as it is today — so the first
real cutover is small enough to actually check in a real Editor before
trusting the next one.

## 1. Current architecture (as it stands, 2026-07)

- `MonsterAgent.Update()` runs every frame: death check → capture check
  (docs/26 Phase 6) → idle auto-acquire/special-attack evaluation → a switch
  on `_order` (`Idle/Move/AttackBuilding/AttackUnit/EatCitizen/Perch/
  SpecialAttack`) that calls one `TickX(dt)` method, which **both decides
  movement/behavior AND returns a `Vector3` velocity**.
- `MonsterBody.UpdateLocomotion(velocity, dt)` is a PURE VIEW: it never
  writes XZ position, only consumes the returned velocity to drive
  stride/wingflap/lift animation (docs/25's load-bearing interface boundary,
  already established and already the reason docs/26 Phase 6's
  `TickCaptured` derives velocity from measured displacement rather than
  intended direction — see below, same trick applies here).
- Player input never calls `match-core` at all: `WaypointCommander` calls
  `MonsterAgent.OrderMove(hex, queue, settleTarget, groupFacing)` directly, a
  plain method call, not a message/event.
- `OrderMove` supports queueing (`Queue<HexCoord> _waypoints`), a shared
  group-settle creep point, and a shared `GroupFacing` token for
  group-arrival facing (docs from the "Group arrival facing" batch) — richer
  than `match-core`'s current single-destination `CommandKind.MoveTo`.

**The one fact this whole plan hangs off**, already true today per docs/25:
*nothing downstream of a `TickX` method's returned velocity cares how that
velocity was produced.* `MonsterBody` doesn't know or care whether the
position that produced it came from `FollowPath`'s hex-by-hex advance, from
`CaptureState.TickPull`'s direct position write (docs/26 Phase 6), or —
after this plan — from interpolating between two `match-core` tick
snapshots. That's the seam this migration threads through.

## 2. Target architecture: what "render interpolated sim state" means

`match-core` ticks at a fixed 10/s. Unity renders at a variable, usually
higher, frame rate. The classic lockstep-with-interpolation split (the same
shape AoE2/Factorio/every serious lockstep RTS uses, and the one docs/23 §11
already committed this project to for netcode):

- **The sim is the single source of truth for WHERE a unit is and WHAT it's
  doing** (`SimUnit.X/Z`, `SimUnit.Order`). It advances only inside `Tick()`,
  only 10 times per simulated second, identically on every client.
- **Unity never simulates.** It holds the last TWO tick snapshots (`prev`,
  `curr`) for each unit and renders a position `Lerp(prev, curr, alpha)`,
  where `alpha = accumulatedRealTime / tickInterval` (`tickInterval = 1/10s`)
  clamped to `[0, 1]`. This is pure presentation math — never fed back into
  the sim, matching docs/23 §10's "visuals must never feed back into sim
  state" acceptance rule for the whole graphics ladder, extended here to
  movement.
- **Player intent becomes a `Command`, not a direct call.** Where
  `WaypointCommander` calls `agent.OrderMove(hex, ...)` today, the ported
  path instead builds a `Command(playerIndex, CommandKind.MoveTo,
  targetEntity: agent.SimEntityId, argA: hex.Q, argB: hex.R)` and hands it to
  a per-match command queue that the driving loop feeds into `MatchState.
  Tick(commands)` on the next tick boundary. (In a real match this queue is
  what the relay fills from the network, per docs/23 §11 — a single-player
  skirmish or this migration's own local testing just short-circuits the
  relay and applies the command locally next tick.)
- **`MonsterBody` is untouched.** It still only ever receives a `Vector3`
  velocity and never learns where that velocity came from. This is the whole
  point of the interface boundary docs/25 established: this migration is
  invisible below `MonsterBody`.

### 2.1 Interpolation math (as implemented)

`Alpha` is deliberately computed ONCE per frame, at the bridge, and shared
by every sim-driven unit's view — an earlier draft of this section gave
each view its own accumulator, which risked two units' interpolation
drifting out of step with each other for no benefit, since "how far
between ticks are we" is a property of the MATCH, not of any one unit.

```csharp
// SimBridge.Pump(dt), once per Unity Update() (variable rate):
tickAccumulator += dt;
while (tickAccumulator >= TickInterval && ticksRun < MaxTicksPerFrame)
{
    match.Tick(pendingCommands);           // rotates every unit's prev/curr via OnTick
    tickAccumulator -= TickInterval;
    ticksRun++;
}
Alpha = Mathf.Clamp01((float)(tickAccumulator / TickInterval));

// SimUnitView.OnTick(x, z), called once per completed Tick():
prevX = currX; prevZ = currZ;
currX = x; currZ = z;

// SimUnitView.Advance(alpha, dt, transform), once per Update() per unit:
var renderX = Mathf.Lerp((float)prevX, (float)currX, alpha);
var renderZ = Mathf.Lerp((float)prevZ, (float)currZ, alpha);
var before = transform.position;
transform.position = new Vector3(renderX, before.y, renderZ);   // Y untouched
var velocity = (transform.position - before) / Mathf.Max(dt, 1e-5f);
```

Velocity is derived from **measured render-position delta**, exactly the
same trick `TickCaptured` (docs/26 Phase 6) already uses for `CaptureState`
-pulled units — not a second, possibly-divergent "intended velocity" value.
One rule, applied consistently everywhere a view renders a sim-driven
position: *velocity is always what actually moved this frame, never what was
merely intended to.*

This is UI-layer float math (`Time.deltaTime`, `Mathf.Lerp`) deliberately
OUTSIDE `match-core` — docs/23 §0's float discipline governs the
DETERMINISTIC sim, not the render interpolation sitting on top of it, the
same way `MonsterBody`'s existing animation math (stride phase, wing flap)
was never held to that bar either. Nothing here is hashed; nothing here
needs to be.

## 3. Incremental cutover: one order kind at a time

docs/23 §13-A: *"Every later phase ports the slice it adds the same way."*
Applied literally: **`OrderKind.Move` cuts over now; every other order kind
stays exactly as it is today, unmodified, running its existing `TickX`
method.** A per-unit flag decides which regime governs a given tick:

```csharp
private bool _simDriven;   // true only while _order == OrderKind.Move AND sim wiring is present

// inside Update(), replacing the single line `case OrderKind.Move: ... break;`
case OrderKind.Move:
    velocity = _simDriven ? TickMoveViaSim(dt) : TickMove(dt);   // TickMove(dt) unchanged, legacy fallback
    break;
```

`_simDriven` is false unless a `MatchState`/`SimEntityId` was actually wired
up for this agent (a skirmish/dev-harness scene that opts in) — so a scene
that never sets this up (every current scene) is **byte-for-byte unchanged
behavior**, not a parallel code path quietly diverging. This is the
"leave every not-yet-ported order kind exactly as correct as it is today"
guarantee from §0, made structural rather than aspirational.

**Why `Move` first, and specifically not the others yet:**

| Order kind | Cut over now? | Why / why not |
| --- | --- | --- |
| `Move` | **Yes** | The only order kind `match-core`'s `SimUnit` implements (docs/23 Phase 1.5). Pure position; no combat/economy/genome coupling to reconcile yet. |
| `AttackBuilding`/`AttackUnit` | No | Needs docs/23 §13-C's combat core (damage formula, arcs, death/salvage) ported first — nothing to render yet. |
| `EatCitizen` | No | Needs Citizens as sim entities too (today they're not even `UnitCombat`, docs/26 research) — a bigger, separate slice. |
| `SpecialAttack` | No | Needs the whole docs/26 ability system (10 phases of it) ported — deliberately deferred; this is a LOT to re-verify blind. |
| `Perch` | No | Flight/altitude has no sim-side representation at all yet (`SimUnit` is ground-plane XZ only). |

**Known, accepted gap for this first cut:** `match-core`'s
`CommandKind.MoveTo` is a single destination — no waypoint queueing, no
group-settle creep point, no `GroupFacing` token (see §1). `TickMoveViaSim`
therefore only handles the SAME single-destination case `WaypointCommander`
already sends for `queue == false`; a queued/grouped move keeps using the
legacy `TickMove` path (`_simDriven` stays false whenever `OrderMove` is
called with `queue == true` or a non-null `groupFacing`). Widening
`CommandKind.MoveTo` to carry a waypoint list and a group token is a real,
separate follow-up (flagged here, not hidden), not attempted in this pass.

## 4. Files touched (as implemented)

**New (all additive — zero risk to existing behavior until explicitly
wired into a scene):**
- `unity-client/Assets/Scripts/SimBridge.cs` — one per match: owns a
  `MatchState`, the outgoing command queue, and the fixed-tick pump.
  `Update()` is a one-line call to `Pump(Time.deltaTime)` — the actual
  accumulator logic is the `public` method `Pump(float dt)`, taking `dt`
  as data rather than reading `Time.deltaTime` internally (the same
  convention every `MonsterAgent.TickX(dt)` method already follows), so a
  standalone harness can drive it with controlled values — `Time.deltaTime`
  itself can't be faked outside a live Editor/Player, so this seam is what
  makes the accumulator's own correctness verifiable at all without one.
  Also owns `Alpha` (the one shared "how far into the current tick are we"
  value every sim-driven unit's view reads, computed once per pump rather
  than duplicated per unit) and `SpawnUnit`/`QueueMoveCommand`/`OrderOf`.
- `unity-client/Assets/Scripts/SimUnitView.cs` — a component (§6.1
  resolved: component, not fields on `MonsterAgent`, for easy strip-back).
  Holds ONLY `prevX/prevZ/currX/currZ` (no accumulator of its own — see the
  file's own header comment for why an earlier draft's per-unit accumulator
  idea was wrong) and implements §2.1's interpolation via `Advance(alpha,
  dt, transform)`, returning `Vector3` velocity with the same contract
  every other `TickX`-adjacent method already has.

**Modified (minimal, additive-only diff — smaller than this doc originally
guessed):**
- `MonsterAgent.cs` — new fields (`_simBridge`/`_simView`/`_simEntityId`/
  `_simPlayerIndex`, a `SimDriven` property), `EnableSimDriven(...)` (the
  opt-in entry point, §6.3), `OrderMoveViaSim(hex)`, `TickMoveViaSim(dt)`,
  one changed line in the `OrderKind.Move` switch case. `TickMove` (the
  legacy path) is untouched, not deleted.
- **`WaypointCommander.cs` needed NO changes at all** — both single-unit
  move call sites already funnel through `MonsterAgent`'s 2-arg
  `OrderMove(hex, queue)` overload, so intercepting there (`if (SimDriven
  && !queue) { OrderMoveViaSim(hex); return; }`) covers the exact same
  "single-unit, non-queued" case §3 scoped, entirely inside `MonsterAgent`,
  with zero footprint on the commander. A smaller, cleaner seam than this
  doc originally anticipated — noted here so the design record matches
  what shipped, not what was guessed before writing the code.

**Explicitly untouched:** every other `TickX` method, `MonsterBody.cs`
(the interpolation boundary is designed specifically so this file needs zero
changes), `UnitCombat.cs`, the entire docs/26 Special Attacks System,
`WaypointCommander.cs` (see above).

- `RuntimeCityBuilder.cs` — a new `simDrivenDemo` Inspector toggle
  (default off) and a `SimBridge` field, wired into `HandleRosterReady`'s
  existing spawn loop: when on, the first spawned monster additionally
  gets `EnableSimDriven` called on it. This is the actual Editor smoke
  test (see below) — everything else in this file is unchanged.

## 5. Risks & edge cases

- **Two positions disagreeing.** While `_simDriven` is true, `match-core`'s
  `SimUnit` is the ONLY writer of this unit's authoritative position — the
  existing `ApplySeparation`/steering calls (docs/25) that currently nudge
  `transform.position` directly must be SKIPPED for a sim-driven unit (they
  have no sim-side equivalent yet and would fight the interpolated render
  position every frame, a visible jitter). `TickMoveViaSim` must NOT call
  `ApplySeparation`; this is an intentional, documented capability
  regression for the sim-driven path specifically (no separation/flocking
  yet), not an oversight — docs/23 §5 (flocking) is itself a `match-core`
  port waiting to happen, and separation should arrive there, sim-side,
  not be patched onto the interpolation layer.
- **Terrain-follow Y.** `SimUnit` is XZ-only (ground plane); Unity still
  owns and writes the Y coordinate every frame from `GroundHeightAt(pos)`,
  exactly as today, for every order kind including `Move` — this never
  moves into `match-core` at all (docs/23 §0 doesn't require terrain height
  to be simulated, only that gameplay-affecting state is).
  Rotation/facing likewise stays Unity-side (`Quaternion.LookRotation`
  toward the interpolated velocity), unchanged.
- **Command timing / one-tick latency.** A move issued mid-frame doesn't
  take effect until the next tick boundary (up to 100ms at 10 tps) — this is
  the correct, standard lockstep input-delay behavior (docs/23 §11 already
  budgets 200ms/2 ticks for the networked case), not a bug; a local-only dev
  harness will feel a small, real click-to-move delay it didn't have before
  for sim-driven units specifically.
- **Fixed-timestep accumulator drift/spiral-of-death.** `SimBridge.Update()`
  must cap how many catch-up ticks it runs in one frame (a stalled frame
  must not try to run 50 sim ticks to "catch up" and stall harder) — the
  standard fixed-timestep guard, same shape as Unity's own `FixedUpdate`
  substep cap.
- **Never claim visual verification.** Every acceptance item below that says
  "flightcheck compiles" is exactly that and no more; the FIRST real
  confirmation this behaves correctly on screen is the creator's own Editor
  session, not this session's output.

## 6. Open questions — resolved when Phase A landed

1. **Where does interpolation state live** — resolved: a component
   (`SimUnitView`), matching `MonsterBody`'s own precedent of being a
   separate component rather than fields on `MonsterAgent`, for the same
   easy-strip-back-out reason.
2. **One `SimBridge` per scene** — resolved as designed: a scene creates
   one, calls `StartMatch`, and every unit that wants sim-driven movement
   opts in through it. No scene does this yet, so it's fully inert
   everywhere.
3. **`SimEntityId` assignment** — resolved differently than first
   guessed: not a parameter on `Init`, but a separate opt-in method,
   `MonsterAgent.EnableSimDriven(bridge, playerIndex, atHex, speed)`,
   called AFTER `Init` by whatever scene wants this unit sim-driven. Zero
   `Init` call sites needed touching — an even purer addition than the
   original plan.

## 7. Phased implementation plan

- **Phase A — the bridge + single-unit Move cutover, opt-in only.**
  **Status: done (2026-07).** `SimBridge.cs` (fixed-tick pump + command
  queue), `SimUnitView.cs` (the interpolation view), `MonsterAgent`'s
  `SimDriven`/`EnableSimDriven`/`OrderMoveViaSim`/`TickMoveViaSim`. **No
  existing scene's behavior changes** — every field/method this phase adds
  is either brand new or gated behind `SimDriven`, which stays false
  (`_simEntityId` unset) unless a scene explicitly calls
  `EnableSimDriven`; nothing does yet.
  **Verified:** flightcheck stub-compile clean across the whole gameplay
  layer (needed one addition: a `MadDr.MatchCore` reference, built from
  the real package and copied in, same as every other package DLL
  flightcheck already references). A standalone numeric harness
  (`harvestcreditverify`, compiling the REAL `SimBridge.cs`/
  `SimUnitView.cs`) proved the interpolation formula itself — exact
  halfway lerp at alpha=0.5, Y always untouched, velocity = measured
  delta/dt (not a separately-guessed value), zero velocity at rest across
  five alpha values, correct 20 m/s for a known 2m-in-one-tick motion —
  plus the accumulator itself: `Alpha` strictly increases within a tick,
  always stays in [0,1], a monstrous single-frame `dt` (100 ticks' worth)
  never hangs/throws and the catch-up cap correctly drops the remainder
  rather than spiraling, and a full `SimBridge`+`MatchState` integration
  check (spawn a unit over a real generated city, queue a `MoveTo`, pump
  400 frames, confirm the unit's rendered snapshot lands exactly on the
  goal hex's world position). 15/15 pass; match-core's own 19 tests and
  citygen-core's 145 remain untouched. Two bugs were caught building this
  harness, both fixed, both worth recording (docs/12): the harness's own
  `Vector3` stub was seeded from `flightcheck`'s copy (a pure compile-check
  harness whose every `Vector3` operator returns `default(Vector3)` — fine
  there, silently wrong here) — patched to real math, matching
  `webattackverify`'s already-correct copy; and the FIRST draft of the
  integration test looped "pump until Idle," which exits immediately
  without pumping at all because a freshly-spawned unit already reads
  Idle before its queued command has even applied — fixed to a
  fixed-budget pump instead, with a comment recorded so nobody
  re-introduces that exact bug.
  **Explicitly NOT claimed:** that a unit visibly moves smoothly on
  screen — that is the creator's own Editor check, still outstanding, and
  the actual gate before Phase B is attempted.

  **The actual Editor check, made concrete.** `RuntimeCityBuilder` gained
  a single Inspector toggle, `simDrivenDemo` (default off — every
  existing scene is unaffected), plus a `SimBridge` field. When on, the
  FIRST monster the roster spawns is additionally routed through
  `EnableSimDriven` right after its normal `Init(...)` — nothing else
  about it changes, and every other monster is completely untouched. This
  reuses the EXACT existing workflow this class's own header comment
  already documents ("Hit Play: left-click your monster, right-click the
  world"): check the box, hit Play, click that one monster, right-click
  to move it. What to watch for: the unit walks smoothly (interpolation
  working) vs. jittering/stuttering (a real bug, since nothing else
  should be writing its position while sim-driven); a roughly one-tick
  (~100ms) delay before it starts moving after the click (correct
  lockstep input latency, not a bug, per §5); normal footstep
  animation throughout (proves `MonsterBody` genuinely doesn't care where
  its velocity came from); and every OTHER monster in the scene behaving
  completely normally (proves the opt-in boundary actually holds).
- **Phase B — queued/grouped moves.** Widen `CommandKind.MoveTo` (or add a
  `CommandKind.MoveQueue`) to carry a waypoint list + group token;
  `match-core` gains queued-order support on `SimUnit`; `WaypointCommander`'s
  remaining branches cut over. Only attempted after Phase A is confirmed
  working in a real Editor session — no point widening a foundation nobody
  has checked yet.
- **Phase C — flocking (docs/23 §5) lands sim-side**, restoring
  separation/alignment/cohesion for sim-driven units (closing §5's
  documented regression).
- **Phase D+ — every remaining order kind**, each following docs/23 §13-A's
  own rule: design the system, port it into the tick sim, THEN wire the
  Unity view — never the other order.

## v0.1 tuning appendix

`TickInterval = 1f / MatchState.TicksPerSecond` (0.1s, fixed by docs/23 §11,
not a v0.1 placeholder). Catch-up tick cap: TBD in Phase A implementation,
suggest 4 (matches common fixed-timestep guidance) — flagged as a real
number to pick, not yet chosen.
