# 26 — Special Attacks System (enemy AI)

Status: **Approved architecture, Phases 1–8 implemented + Phase 9
(secondary attacks for all races)** (design produced
2026-07 via a research-then-design pass over the existing combat/AI
architecture before any code was written; creator approved "pure Unity"
+ ScriptableObject-based definitions before implementation began) ·
Extends [04-combat-model.md](04-combat-model.md)'s combat design and
[16-brains-behavior-command.md](16-brains-behavior-command.md)'s
threshold-crossing AI pattern · Pillars served: 3 (*honest combat*).

## 0. Problem statement

A modular framework so different enemy types can equip reusable special
abilities (cooldown-gated, area-of-effect-capable, target-classifying) —
worked example: an Arachnid Web Attack that pulls and consumes
human-scale targets, but only slows heavy/armored ones.

## 1. Architectural decision (read before touching this system)

Two forks were resolved by direct creator decision before implementation:

- **Where the system lives**: today's actual, playable combat is entirely
  Unity `MonoBehaviour`s (`UnitCombat`, `MonsterAgent`, `WeaponFx`,
  `Projectile`). `packages/match-core` (the future deterministic RTS sim,
  docs/23) has **zero unit/combat code today** — confirmed empty, not a
  placeholder. CLAUDE.md's own directive says not to add gameplay
  decisions to `MonsterAgent.Update()`, since the unit sim is meant to
  port there eventually. Presented as a genuine fork (build portable-
  core-now vs. pure-Unity vs. wait-for-match-core); **creator chose pure
  Unity** — build directly against the existing MonoBehaviour
  architecture, no engine-agnostic abstraction layer. A future
  match-core port, if it happens, is accepted as a real rewrite at that
  time, not hedged against now.
- **`SpecialAttackDefinition` format**: plain C# class (matching
  `WeaponProfile`/`HarvestProfile`) vs. `ScriptableObject`. **Creator
  chose ScriptableObject** — the first ScriptableObject asset type in
  this codebase (every existing stat block is a plain class instead).
  Chosen for Inspector-editable, drag-and-drop-equippable designer
  content; the tradeoff (no existing precedent to follow, less friendly
  to this project's "verify with a standalone C# harness, no Editor
  available" testing discipline) was surfaced and accepted.

## 2. Current architecture (as researched, 2026-07)

Researched via parallel deep-dives before any design was proposed:

- **Cooldown idiom** (`UnitCombat._cooldown`, `MonsterAgent._attackCooldown`,
  `RuntimeCityBuilder`'s `_trafficCheckTimer`/`_deadlockPollTimer`): a
  plain private `float`, decremented by `Time.deltaTime` once per
  `Update()`, reloaded to a duration on trigger, gated by a `<= 0f`
  check. No coroutines, no `Time.time` deadline stamps, no
  `InvokeRepeating` anywhere in the project. This is the ONE idiom every
  new cooldown in this system must match — `SpecialAttackInstance.Tick`
  is a direct copy of this shape.
- **`WeaponProfile`/`WeaponFx`/`Projectile`**: one weapon per unit,
  instant (beam/melee/flame) or projectile (bolt/bullet/spore) kinds,
  cadence-gated through `UnitCombat.TryFire`/`TryFireAtPoint`.
  `Projectile` already does true per-frame homing and fizzles gracefully
  if its target dies mid-flight — the reusable pattern for a web
  projectile, not something to reinvent.
- **`MonsterAgent`'s `_order` state machine**: `OrderKind` enum,
  `Update()` dispatches to one `Tick*(dt)` method per kind, and **every**
  `Tick*` must return the frame's `Vector3` velocity — `MonsterBody`
  consumes it for footstep/wing animation and `MonsterSteeringController`
  reads it (via `UnitCombat.LastVelocity`) for neighbours' predictive
  avoidance (docs/25). Any new order must honor this contract exactly.
- **`TickEat`/consumption**: today's only "consume a target" mechanic —
  approach within 3m, then a single-frame `RuntimeCityBuilder.
  OnCitizenEaten` call (wallet credit + `Object.Destroy`). No capture
  state, no pull, no multi-frame hold exists anywhere today — a web
  attack's capture/pull is genuinely new territory, not an extension of
  something that already exists.
- **`YieldTarget`/`YieldUntil`** (docs/25 Phase D): the closest existing
  "temporary externally-imposed movement override" precedent, but it's
  position-snapshot-only and only read inside `SteerFollowPath` (i.e.
  only for a unit already path-following) — a live, moving "pulled
  toward my captor" effect needs its own state, following the *shape* of
  this precedent (expiry-driven, single-writer) rather than reusing the
  fields directly.
- **Hex/spatial**: `HexCoord.Range(n)` (filled-disc hex query) is the
  city-generation/dressing convention (landmark auras, railyard radius);
  combat/steering already uses **meter-radius** queries via
  `SpatialGrid<UnitCombat>.QueryRadius` (bounding-square, caller does the
  exact-circle filter — docs/25's own convention). A web attack's AoE
  should follow the combat convention, not the dressing one.
- **No existing precedent anywhere** for: line-of-sight/occlusion checks
  (all targeting today assumes unobstructed range), area-of-effect
  combat, status effects/crowd control, or a mass/weight classification
  stat (`UnitCombat.Radius` is the only existing quantitative
  differentiator; tanks are simply bigger/tougher, not flagged "heavy").
- **Genome/catalog**: every `hand`-homolog weapon family is a purely
  cosmetic + stat-multiplier part; no existing family carries a special
  mechanic. `arachnid` is a body-plan only, with zero weapon/ability
  special-casing anywhere — a web attack is unmodeled territory, not an
  extension of anything arachnid-specific already in the catalog.

## 3. Approved architecture

```
UnitCombat (existing, extended)
  + Mass (float)                          -- continuous, not a per-species tag
  + Abilities (List<SpecialAttackInstance>) -- ticked unconditionally in Update()

SpecialAttackDefinition : ScriptableObject (NEW)
  -- Name, Description, Cooldown, Range, AreaOfEffect, ValidTargets
     (TargetFilter flags), EffectType, AI use requirements, VFX/SFX hooks

SpecialAttackInstance (NEW, plain C# -- per-unit runtime state)
  -- Definition + CooldownRemaining, IsReady, Tick(dt), TriggerCooldown()
  -- identical decrement/reload/gate idiom to UnitCombat._cooldown

MonsterAgent (existing, extended)
  + OrderKind.SpecialAttack + TickSpecialAttack(dt) + OrderSpecialAttack(...)
  -- follows the EXACT Order*/Tick*/velocity-return contract every other
     order already uses

WebAttackAbility (Phase 4+, not yet built)
  -- the first concrete effect: projectile -> AoE query via SpatialGrid ->
     Mass-threshold branch -> capture-and-pull (human-class) or slow
     status (heavy-class)
```

**Mass classification** (design question: tags vs. components vs.
interfaces vs. stats): a continuous `float Mass` on `UnitCombat`,
populated at `Configure()` time from the same plan-mass table
`packages/roster-client`'s `Combat.Profile` already computes for HP
(Tank sets `mass: 10f` explicitly — see `Tank.cs`, the project's one
concrete "heavy" example today). Effects branch on **thresholds** over
this value, so a new enemy type is classified correctly for free from
its own genome-derived mass, with no new per-type lookup table to
maintain — matches docs/16's established pattern of deriving behavioral
quantities as pure functions of existing stats rather than authoring new
per-type flags.

## 4. Files touched

**Modified:**
- `SpecialAttackDefinition.cs` — Phase 9: new `Stun` enum value on
  `SpecialAttackEffectType`; new `DamageAmount`/`StunDuration` fields.
- `UnitCombat.cs` — `Mass` field, `Abilities` list, ability cooldown
  ticking in `Update()`, `Configure(...)` gains an optional `mass = 1f`
  trailing parameter (every existing call site unaffected); Phase 5 adds
  `IsPossessed` (default false), `_slowRemaining`/`_slowMultiplier`
  (ticked in `Update()`), `SpeedMultiplier`, `ApplySlow(...)`; Phase 6
  adds `IsCaptured`, `Captor`, `Capture(...)`, `TickCapture(dt)` (owns
  one `CaptureState`); Phase 7: `TickCapture` now returns whether the
  victim arrived this frame. Phase 9: `_stunRemaining`, `IsStunned`,
  `ApplyStun(...)` (ticked in `Update()`); `SpeedMultiplier` reads 0 and
  `ReadyToFire` reads false while stunned.
- `MonsterAgent.cs` — `OrderKind.SpecialAttack`, `_activeSpecialAttack`/
  `_targetSpecialAttackUnit` fields, `OrderSpecialAttack(...)`,
  `TickSpecialAttack(dt)`, wired into the `Update()` dispatch switch,
  `ClearTargets()`, `OnDied()`, and the debug `OrderDescription` string;
  Phase 5 multiplies `_fighter.SpeedMultiplier` into `RunOrWalkSpeed()`;
  Phase 6 adds an `IsCaptured` check + `TickCaptured(dt)`, run instead of
  the `_order` switch while true; Phase 8 adds `EvaluateBestAbility(...)`
  and wires it into `AcquireTarget` ahead of retaliation/engage, and
  narrows `AcquireTarget`'s old all-or-nothing weapon guard so a
  weaponless special-attack-only creature could still use one. Follow-up:
  extracts `CreditHarvestForEatenCitizen()` out of `TickEat` and adds
  public `NotifyCapturedCitizenEaten()` so a web-captured citizen credits
  the harvest tank too. Phase 9: `EvaluateBestAbility` gains a self-anchor
  case for `Stun`-type abilities; `TickSpecialAttack` dispatches
  `Damage`/`Stun` to the new `SpecialAttackResolver`; `Init` equips every
  monster with `SecondaryAttackCatalog.ForMonster(handFamily)`.
- `Tank.cs` — explicit `mass: 10f` on its `Configure(...)` call, the
  concrete heavy-target example; Phase 5 multiplies
  `_combat.SpeedMultiplier` into the hull-movement line; Phase 6 adds the
  same `IsCaptured` check at the top of `Update()` (inert today — a tank
  is always heavy, never captured). Phase 9: `Init` equips every Tank
  with `SecondaryAttackCatalog.Flamethrower()`.
- `Citizen.cs` — Phase 6: its own `Capture(...)` + `_capture` field
  (a `Citizen` has no `UnitCombat`, so it can't share one), checked at
  the top of `Update()` ahead of even the flee logic; Phase 7: calls
  `_builder.OnCitizenEaten(this)` the instant its capture-tick reports
  arrival. Follow-up: also looks up the capturing `MonsterAgent` via
  `GetComponent` and calls `NotifyCapturedCitizenEaten()` on it first.
- `Projectile.cs` — one additive `OnArrive` hook (Phase 4), existing
  callers unaffected.
- `RuntimeCityBuilder.cs` — `QueryCombatantsInRadius` (Phase 4), a thin
  public wrapper over the existing docs/25 neighbour grid.
- `WebAttackAbility.cs` — Phase 5: `HeavySlowMultiplier`/
  `HeavySlowDuration` constants; the heavy branch now calls
  `c.ApplySlow(...)` instead of only logging; `ShouldCatchCombatant`'s
  friendly-fire check now respects `IsPossessed`. Phase 6: adds
  `CapturePullSpeed`; both the combatant and citizen non-heavy branches
  now call `.Capture(...)` instead of only logging. Phase 8: adds
  `CountCatchable(...)`, reusing `ResolveImpact`'s own query/
  classification logic to score candidate targets for the AI heuristic.

**New:**
`SpecialAttackDefinition.cs`, `SpecialAttackInstance.cs` (Phase 1);
`WebAttackAbility.cs` (Phase 4 — targeting/classification only);
`CaptureState.cs` (Phase 6); `SpecialAttackResolver.cs`,
`SecondaryAttackCatalog.cs` (Phase 9).

**Explicitly untouched:** `WeaponProfile`/`WeaponFx` (additive parallel
system, not a modification of the existing weapon path),
`packages/genome-core` (abilities are equipped data, not bred genome
traits, for this first version), `packages/match-core` (per §1).

## 5. Risks & edge cases (full list; see design-doc discussion for detail)

Multiple targets caught in one web; target escapes before pull
completes; ability interrupted (captor dies mid-cast); enemy dies during
cooldown (no cleanup needed — `SpecialAttackInstance` lives on
`UnitCombat`, destroyed with the unit, same as `_cooldown` today); no
save/load system exists yet in this project (not applicable); no
networking layer exists yet (not applicable, flagged forward-looking
only); pulling a target must reuse the `TickSettle`/
`InsideBuildingFootprint` precedent (hex-membership alone isn't
sufficient — this exact gap was a real, separately-fixed bug this
session), not just `Blocked()`.

## 6. Phased implementation plan

- **Phase 1 — `SpecialAttackDefinition` + `SpecialAttackInstance`.** Pure
  data/runtime classes, zero integration. **Status: done (2026-07).**
- **Phase 2 — `UnitCombat.Mass` + `Abilities` list + cooldown ticking.**
  **Status: done (2026-07).**
- **Phase 3 — `MonsterAgent` state-machine wiring**
  (`OrderKind.SpecialAttack`/`TickSpecialAttack`/`OrderSpecialAttack`),
  approach-then-trigger-cooldown-then-idle, **no real ability effect
  yet** — proves the contract before any effect exists. **Status: done
  (2026-07).** Verified: flightcheck stub-compile clean (including a new
  `ScriptableObject`/`CreateAssetMenuAttribute`/`TextAreaAttribute`/
  `AudioClip` stub, since this is the first ScriptableObject in the
  project); a standalone harness compiling the real
  `SpecialAttackDefinition.cs`/`SpecialAttackInstance.cs` confirmed the
  cooldown state machine directly: reload-on-trigger, frame-rate-
  independent accumulation across irregular `dt` steps (not a fixed-step
  simulation), and that two units sharing one `SpecialAttackDefinition`
  asset do NOT share a cooldown (per-instance, not per-definition or
  global — the design brief's explicit requirement).
- **Phase 4 — `WebAttackAbility` targeting + AoE resolution only** (no
  pull, no consume yet — logs what it would do). **Status: done
  (2026-07).** New `WebAttackAbility.cs`: `Launch` spawns a non-homing
  `Projectile` at a SNAPSHOT of the target's position (an AoE resolves at
  a location, not on whichever unit is still standing there when it
  lands); `Projectile` gained a small additive `OnArrive` hook (fires on
  arrival with the impact position, existing callers unaffected since
  they never set it) so a non-damage effect can resolve without touching
  `WeaponFx`. On arrival: queries `RuntimeCityBuilder.
  QueryCombatantsInRadius` (new, thin wrapper over the existing docs/25
  neighbour grid -- no second grid) for monsters/tanks, and linearly
  scans `RuntimeCityBuilder.Citizens` for citizens (no spatial grid
  exists for them -- confirmed by research, not assumed -- so this
  matches the project's existing citizen-scanning convention,
  `DistanceAhead`). The actual decision logic
  (`ShouldCatchCombatant`/`IsHeavy`/`MatchesFilter`) is exposed as pure,
  independently-testable static functions rather than inlined, since this
  phase's whole point is that targeting/classification is proven correct
  before any harder capture-state work builds on it. Verified: flightcheck
  clean; a standalone harness compiling the real `WebAttackAbility.cs`
  drove 8 checks directly against real `UnitCombat` instances -- the mass
  boundary (exactly at threshold IS heavy, matching `>=`), filter
  matching, in-range/out-of-range, caster self-exclusion, no-friendly-
  fire-capture, dead-target exclusion, and category mismatch -- all pass.
- **Phase 5 — heavy-target slow effect** (the simpler branch, no new
  state machine). **Status: done (2026-07).** The mechanic lives on
  `UnitCombat` (not `MonsterAgent` or `Tank`) specifically because the
  creator asked that it "apply to all monsters" — putting it on the
  shared combatant class means every mover reads the same
  `SpeedMultiplier`, with zero per-species wiring: `_slowRemaining`/
  `_slowMultiplier` fields ticked in `Update()` alongside the existing
  `_cooldown`/`_battleTimer` timers (same `Time.deltaTime`-decrement
  idiom), a public `SpeedMultiplier` property (1 = unaffected), and
  `ApplySlow(multiplier, duration)` which takes the STRONGER multiplier
  and the LONGER remaining duration on reapplication, so a weak
  reapplication can never dilute an already-active stronger slow.
  `WebAttackAbility.ResolveImpact`'s heavy branch now calls
  `c.ApplySlow(HeavySlowMultiplier, HeavySlowDuration)` (two new v0.1
  placeholder constants, 0.35x speed for 3s, same "unbalanced on
  purpose" status as `HeavyMassThreshold`) instead of only logging.
  Both consumers of movement speed were updated to multiply in
  `SpeedMultiplier`: `MonsterAgent.RunOrWalkSpeed()` (covers every
  bred/genome monster automatically — this is the "all monsters" half of
  the requirement) and `Tank.cs`'s own hull-movement line (a tank is
  this project's one concrete heavy-target example, so it must visibly
  slow too, not just monsters). Verified: flightcheck stub-compile clean
  (`UnitCombat.cs`/`WebAttackAbility.cs`/`MonsterAgent.cs`/`Tank.cs` all
  compile); the `webattackverify` harness gained 6 new checks driven
  directly against the real `UnitCombat.cs` (default-unaffected,
  applying a slow reduces `SpeedMultiplier`, a weaker reapplication
  doesn't dilute a stronger active one, a stronger reapplication does
  deepen it, reapplication takes the longer remaining duration — read
  via reflection since `_slowRemaining` is intentionally private, no
  test-only field added to shipped code — and that
  `WebAttackAbility`'s heavy-branch constants actually compose with
  `ApplySlow`); all pass alongside the existing 8 Phase 4 checks and one
  new possessed-unit check (below).

  **Possessed units and friendly fire** (creator direction, 2026-07:
  "friendly fire has no effect, unless the unit is possessed — which
  should be in the design docs"): the existing no-friendly-fire-capture
  rule in `ShouldCatchCombatant` (`if (c.Faction == caster.Faction)
  return false;`) is not actually "same faction is always safe" — it's
  "an ally is safe unless it's no longer really an ally." `UnitCombat`
  gained a new `public bool IsPossessed` field (default `false`,
  completely behavior-inert today) and the friendly-fire check now reads
  `if (c.Faction == caster.Faction && !c.IsPossessed) return false;`, so
  a possessed same-faction unit IS caught by its own side's web despite
  matching faction. **No possession/mind-control mechanic exists yet** —
  nothing anywhere sets `IsPossessed = true`; this is a forward-
  compatible hook plus a documented rule, added now specifically so a
  future mind-control ability (this connects to the creator's earlier,
  separate direction: "Mad Doctor Biological strength, mind control on
  very big brain units") doesn't require revisiting every special
  attack's friendly-fire logic later. Verified via the harness's
  `CheckCatchDecision_PossessedAllyNotExcluded` (a possessed ally in
  range IS caught; an ordinary unpossessed ally in range still is not).
- **Phase 6 — `CaptureState` + pull-toward-captor** for non-heavy
  targets (the riskiest step — new interruptible multi-frame state).
  **Status: done (2026-07).** New `CaptureState.cs`: a small standalone
  class (`Captor`, `Speed`, `Active` = captor non-null-and-alive,
  `Begin(captor, speed)`, `TickPull(transform, dt)` — moves toward the
  captor at `Speed`, clamped, and simply stops once within
  `ArriveRadius` rather than consuming or overshooting). It is
  deliberately its own class rather than fields on `UnitCombat`,
  because the identical pull logic has to be usable by `Citizen` too —
  confirmed by Phase 4's research that `Citizen` carries no `UnitCombat`
  component at all, so it can't share one. `UnitCombat` gained
  `IsCaptured`/`Captor`/`Capture(captor, speed)`/`TickCapture(dt)`
  (owns one `CaptureState` instance); `Citizen` gained its own separate
  `Capture(...)` and a `_capture` field checked at the very top of its
  `Update()` — capture overrides even fleeing, since a caught citizen is
  being dragged, not choosing to run. Re-capturing an already-captured
  target (a second web lands on it) simply retargets to the newest
  captor — last web wins, no stacking, v0.1. Auto-release needs no
  explicit cleanup call: `IsCaptured`/`Active` read the captor's live
  `Alive` state, so a captor's death is reflected the very next check
  (the exact "ability interrupted — captor dies mid-cast" risk from §5,
  handled for free).

  `WebAttackAbility.ResolveImpact`'s non-heavy branch (both the
  `UnitCombat` combatant loop and the separate `Citizen` linear scan) now
  calls `.Capture(caster, CapturePullSpeed)` (a new v0.1 placeholder
  constant, 6 m/s) instead of only logging.

  Consistent with Phase 5's "apply to all monsters" lesson: the
  capture check was wired generically into every mover, not just
  Citizen. `MonsterAgent.Update()` checks `_fighter.IsCaptured` right
  after its existing death check and, while true, calls a new
  `TickCaptured(dt)` (derives a velocity from actual displacement,
  since `CaptureState.TickPull` moves the transform directly rather
  than returning an intended direction*speed like the other `Tick*`
  methods) INSTEAD of running the `_order` state machine — the paused
  order is never touched, so it resumes automatically once released.
  `Tank.cs` got the same check at the top of its `Update()`. Both are
  reachable only in edge cases today (an ordinary monster is never a
  valid target of its own faction's web — see "Possessed units and
  friendly fire" above — so only a *possessed* monster caught by its
  own side could ever be captured; a Tank's Mass is always 10, always
  heavy, so it is never captured either) — kept anyway so a future
  possessed unit or a lighter vehicle isn't a special case later,
  mirroring the same "inert today, real hook" precedent as
  `IsPossessed` itself.

  Verified: flightcheck stub-compile clean across every touched file.
  `webattackverify` gained 6 new checks against the real
  `UnitCombat.cs`/`CaptureState.cs`: `Capture()` sets `IsCaptured`/
  `Captor`; `IsCaptured` reads false the instant the captor dies (no
  stuck pull toward a corpse); `TickCapture` closes exactly `Speed *
  dt` toward the captor without overshooting; it holds position once
  within `ArriveRadius` instead of overshooting past the captor;
  re-capture retargets to the newest captor; and the non-heavy branch's
  effect (capture) is confirmed mutually exclusive with the heavy
  branch's effect (slow) on the same catch. All 21 checks (15 from
  Phases 4-5, 6 new) pass.
- **Phase 7 — consume-on-arrival**, wired into `OnCitizenEaten` for
  citizens; parallel path designed (not yet built) for non-`Citizen`
  captured targets. **Status: done (2026-07) for citizens; non-Citizen
  path designed, not built.**

  `CaptureState.TickPull` now returns `true` once the victim is within
  `ArriveRadius` (previously `void`) — `UnitCombat.TickCapture` and
  `Citizen`'s own capture branch both propagate this. `Citizen.Update()`
  reads it directly: the instant a dragged citizen arrives, it calls
  `_builder.OnCitizenEaten(this)` — the SAME method a chased-and-caught
  citizen already goes through via `MonsterAgent.TickEat`, so wallet
  credit (Blood 2 / Bones 1 / Brains 1), the blood-splatter FX, and
  despawn are identical either way; no new consumption path needed for
  citizens, just wiring the existing one to a new trigger.

  **Harvest-tank credit gap — closed (follow-up, 2026-07).** Originally
  flagged here as a known, not-hidden gap: a web-captured citizen didn't
  fill the eating monster's harvest tank the way a direct chase-and-eat
  order does, because docs/22's `_carriedLoad`/`HarvestProfile.
  GatherBlood` credit lived inside `MonsterAgent.TickEat` specifically,
  which the capture-arrival path never ran. Fixed by extracting that
  credit into a new private `MonsterAgent.CreditHarvestForEatenCitizen()`
  (identical formula, `Mathf.Min(Capacity, _carriedLoad + 3 *
  GatherBlood)`, now called from both `TickEat` and the new public
  `NotifyCapturedCitizenEaten()`) and having `Citizen.Update()` look up
  the capturing `MonsterAgent` via `_capture.Captor.GetComponent<
  MonsterAgent>()` — the back-reference this doc originally said would be
  needed — the moment it's eaten on arrival. `MonsterAgent.Init` adds
  `_fighter` to `gameObject` itself, so the `UnitCombat` a `Citizen` was
  dragged toward and the `MonsterAgent` that owns it are always on the
  SAME GameObject, making `GetComponent` the correct (and only) lookup —
  no new field needed on either class. A web-captured citizen now counts
  as the exact same kill as a directly chased-and-eaten one, in every
  respect (wallet, gore FX, despawn, AND harvest credit).

  Verified with a new dedicated harness, `harvestcreditverify` (compiling
  the REAL `MonsterAgent.cs` plus its full real dependency chain, same
  file list as flightcheck): 3 checks, using reflection to set/read
  `MonsterAgent`'s private `_harvest`/`_carriedLoad` fields (same
  discipline as `UnitCombat._slowRemaining`) since nothing outside
  `MonsterAgent` should otherwise touch them — crediting matches
  `TickEat`'s own `3 * GatherBlood` formula exactly; credit caps at the
  vessel's `Capacity` rather than overflowing; a monster with no
  `HarvestProfile` at all is a safe inert no-op, not a null-ref. All 3
  pass. (Building this harness caught an unrelated pitfall worth
  recording: its `UnityStub.cs` was seeded from flightcheck's own copy,
  which stubs every `Mathf` float function as a hardcoded `return 0f` --
  correct for a pure compile-check harness that never inspects a computed
  value, but silently wrong for a harness that asserts on real numbers;
  the first run of these checks all "failed" at `0` before this was
  caught and `Mathf` was patched to real math, matching
  `specialattackverify`/`webattackverify`'s stubs.)

  **Non-Citizen consume path (designed, not built)**: no light,
  non-heavy `UnitCombat` target exists anywhere in the project today to
  build and test this against (Tank is always heavy; a monster is only
  ever a valid web target of its own faction if possessed, per the
  Phase 5 note above) — building an untestable path now would be
  exactly the kind of premature generality this project's engineering
  discipline avoids. The shape it would take, so a future light unit
  doesn't require revisiting this design: `UnitCombat.TickCapture`
  already returns `true` on arrival, matching `Citizen`'s own signal;
  the owning mover (`MonsterAgent.TickCaptured` / a future `Tank`
  equivalent) would read that return value and, on `true`, apply lethal
  damage to itself via its own `TakeDamage` (routing through the
  existing death/`_onDied`/wreck-cleanup path for free, exactly as any
  other kill does) rather than inventing a second destroy path — no new
  `Consume()` method needed on `UnitCombat` itself.

  Verified: flightcheck stub-compile clean (`CaptureState.cs`,
  `UnitCombat.cs`, `Citizen.cs`). `webattackverify` gained one new
  check confirming `TickCapture` returns `false` while still approaching
  and `true` once within `ArriveRadius` — the exact signal
  `Citizen.Update()` acts on. All 22 checks (21 from Phases 4-6, 1 new)
  pass. (No live-scene test exists for the citizen-eaten trigger itself,
  same "compile + pure-logic" verification limit as every other Unity
  behaviour this session — see standing verification discipline.)
- **Phase 8 — AI decision heuristic** (`EvaluateBestAbility`-equivalent:
  distance, weighted target count in AoE, cooldown state, a minimum
  usefulness threshold), added last once the ability is fully functional
  and can be triggered/tested without it. **Status: done (2026-07) — the
  last phase in this plan.**

  New `WebAttackAbility.CountCatchable(builder, caster, definition,
  impactPoint)`: runs the exact same query + the exact same
  `ShouldCatchCombatant`/`MatchesFilter` decisions `ResolveImpact` itself
  uses, but tallies instead of applying effects -- so the AI heuristic
  can never pick a target the resolver would then fail to catch, and
  never drift out of sync with it (one query implementation, read by
  both the "would this land" question and the "how good would it be"
  question).

  New `MonsterAgent.EvaluateBestAbility(out ability, out anchor)`: for
  every equipped ability that's off cooldown (**cooldown state**), scans
  combatants within that ability's own `Range` of this unit
  (**distance**) via `QueryCombatantsInRadius`, and considers each one a
  candidate anchor -- using `ShouldCatchCombatant` at the candidate's OWN
  position (`impactPoint = candidate.position`) to check firing there
  would even catch it at all. Every anchor that passes is scored by
  `CountCatchable` (**weighted target count in AoE**) at that position;
  only a score clearing the ability's own `Definition.MinTargetsInArea`
  (**minimum usefulness threshold** -- a Phase 1 field left unused until
  now) is accepted, and the highest-scoring ability+anchor across every
  equipped ability wins. Wired into `AcquireTarget` ahead of both
  retaliation (`LastAttacker`) and the plain nearest-enemy engage: a
  special attack that clears its own bar is treated as more valuable
  (**tactical value**) than a single regular shot at whoever's nearest or
  last hit this unit.

  `AcquireTarget`'s old guard (`if (_fighter == null || _fighter.Weapon
  == null || !_fighter.Weapon.CanAttack) return;`) was narrowed to just
  `if (_fighter == null) return;` up front, with the `Weapon`/`CanAttack`
  check moved down to guard only the plain-attack fallback -- so a
  future special-attack-only creature with no conventional weapon can
  still use its ability. Behaviorally inert today: no monster anywhere
  is actually equipped with a `SpecialAttackInstance` yet (`Abilities` is
  always empty), because equipping one means dragging a
  `SpecialAttackDefinition` ScriptableObject asset onto a creature in the
  Unity Editor -- a creator/Editor-side task with no code path, and (per
  §1's Fork 2) the entire reason ScriptableObject was chosen over a plain
  class in the first place. `EvaluateBestAbility` is fully built,
  fully tested, and will start doing real work the moment a creature is
  actually equipped with one.

  Verified: flightcheck stub-compile clean. `webattackverify` gained 3
  new checks against the real `WebAttackAbility.CountCatchable` (needed
  a small stub upgrade: `RuntimeCityBuilder`'s
  `QueryCombatantsInRadius`/`Citizens` are now settable and genuinely
  radius-filtered, rather than always returning empty, so a scene can
  actually be populated without a real spatial grid): tallies every
  valid combatant in range; excludes an ally/out-of-range/dead
  combatant while still counting the one valid target; and citizens
  count only when `ValidTargets` allows `Human`. `EvaluateBestAbility`
  itself is a private `MonsterAgent` method that needs a live
  `Transform`/`_builder`/`_fighter` to exercise -- consistent with this
  session's standing discipline, only `CountCatchable` (the pure/query
  logic it depends on) is harness-tested directly; the decision method
  itself is straight-line composition of already-tested pieces, same as
  `ResolveImpact` was never tested directly either. All 25 checks (22
  from Phases 4-7, 3 new) pass.

**All 8 phases of this plan were implemented as scoped to the Web
Attack worked example.** What's real and working today, end to end: a
monster with an equipped, ready `SpecialAttackInstance` will
autonomously choose to fire it (over retaliating/engaging normally)
whenever a candidate target's AoE would catch enough valid targets to
clear the ability's own usefulness bar; a Web Attack bolt travels to
that snapshot position, and on arrival heavy targets are slowed
(visibly, on any monster or Tank) while non-heavy targets are dragged
toward the caster and eaten (with full harvest-tank credit, same as a
direct chase-and-eat kill) on arrival if they're a `Citizen`.

- **Phase 9 — secondary attacks for all races.** Creator direction,
  2026-07: "Roll secondary attacks for all races into the lab and all
  monsters. Humans get flamethrowers (damage only). Aliens get psionic
  attack, short tractor beam is the same as web. mad dr. Ground stomp
  stun effect. With hooks for a bunch of future additions." **Status:
  done for Unity/all monsters+Tanks (2026-07); explicitly not done for
  "the Lab" (site/) -- see below.**

  This closed follow-up (1) from the paragraph above ("no creature is
  actually equipped with a `SpecialAttackDefinition` yet") for good: it
  turns out that assumption was simply wrong for CODE-authored
  definitions. `ScriptableObject.CreateInstance<T>()` is a normal
  runtime API, not an Editor-only operation -- only hand-authored
  Inspector *assets* need the Editor. New `SecondaryAttackCatalog.cs`
  builds one shared `SpecialAttackDefinition` instance per kind this way
  and every monster/Tank is equipped with one automatically at spawn,
  with zero Editor involvement:
  - **Humans (`Tank.cs`)** -- `Flamethrower()`: `EffectType.Damage` only
    (creator: "damage only"), no pull/slow/stun. Every Tank gets this as
    its SECONDARY attack regardless of which PRIMARY weapon it rolled
    (`WeaponProfile.TankFlamethrower`/`TankCannon`, unrelated and
    untouched) -- two independent flamethrower-flavored things, not the
    same system.
  - **Alien-tech-handed monsters** -- `PsionicTractorBeam()`:
    `EffectType.PullAndConsume`, literally the SAME mechanic as Web
    Attack (creator: "short tractor beam is the same as web") at a
    shorter Range/AreaOfEffect, since `WebAttackAbility`'s resolver was
    never actually web-specific -- it's parameterized entirely by
    `definition`. "Alien" is derived from the SAME hand-family strings
    already driving `Combat.WeaponFor` in roster-client
    (`laser_array`/`photon_blaster`/`plasma_lance` -- the exact families
    from the creator's earlier "aliens laser and photonic blasters"
    direction, confirmed in `packages/genome-core/src/catalog.ts` as an
    existing, named part-family group under `origin: "biotech"`), so a
    monster's weapon and its secondary attack always read as
    thematically consistent for free -- no new gene.
  - **Everything else (the Mad Doctor's default creature)** --
    `GroundStomp()`: a new `SpecialAttackEffectType.Stun` (creator:
    "Ground stomp stun effect"), self-centered (`Range = 0`, resolves at
    the caster's own feet the instant it triggers -- see
    `MonsterAgent.TickSpecialAttack`'s Stun-type dispatch and
    `EvaluateBestAbility`'s self-anchor special case below).
  - `SecondaryAttackCatalog.ForMonster(handFamily)` is the single switch
    point mapping hand family -> ability; wired into `MonsterAgent.Init`
    right after `_fighter.Configure(...)`, keyed on
    `creature.Genome.Slots.Hand.Family` (already-synced genome data, no
    schema change).

  **New effect type + resolver ("hooks for a bunch of future
  additions").** `SpecialAttackEffectType` gained `Stun`;
  `SpecialAttackDefinition` gained `DamageAmount`/`StunDuration`. Rather
  than a third near-duplicate of `WebAttackAbility`'s Launch/
  ResolveImpact for each new flavor, a new `SpecialAttackResolver.cs`
  handles every INSTANT (non-projectile) effect through one shared
  `ResolveInstant(builder, caster, definition, originPoint)` +
  `ApplyEffect` switch on `EffectType` -- reusing
  `WebAttackAbility.ShouldCatchCombatant`/`MatchesFilter` (already
  generic, not web-specific) for the catch/classify step. Adding a 5th
  kind is one enum value + one `ApplyEffect` case + (if it needs new
  tunable numbers) a field on `SpecialAttackDefinition` -- no new
  ability class, unless the delivery mechanism itself differs (a
  travelling projectile still belongs with `WebAttackAbility.Launch`).
  `MonsterAgent.TickSpecialAttack` now dispatches on `EffectType`:
  `PullAndConsume` launches a projectile (Web Attack, Psionic Tractor
  Beam); `Damage` resolves instantly at the target's position
  (Flamethrower); `Stun` resolves instantly at the CASTER's own position
  (Ground Stomp).

  **`UnitCombat` stun state**, mirroring the slow-status pair (docs/26
  Phase 5) but deliberately separate rather than reusing
  `SpeedMultiplier = 0` as "a slow": stun is binary (`ApplyStun` just
  takes the longer remaining duration on reapplication -- there's no
  "weaker stun" to protect against the way `ApplySlow` protects a
  stronger slow) and also halts FIRING (`ReadyToFire` now additionally
  requires `!IsStunned`), which a slow never does. `SpeedMultiplier`
  reads `0` while stunned, overriding any active slow entirely (a frozen
  unit doesn't "slowly" move) -- reusing the exact same
  `RunOrWalkSpeed()`/Tank-hull-movement plumbing Phase 5 already wired,
  so movement halts automatically with no new mover-side code.

  **`EvaluateBestAbility`'s self-anchor case.** A self-centered ability
  (`EffectType.Stun`) has no target position to anchor scoring on, so
  it's scored once against this unit's OWN position and, if it clears
  `MinTargetsInArea`, `anchor` is set to `_fighter` itself -- not a real
  target, just a non-null `UnitCombat` so `OrderSpecialAttack`'s
  existing contract is satisfied. `TickSpecialAttack`'s approach-distance
  check against a self-anchor then naturally reads as "already in range"
  (distance to self is exactly zero) and fires immediately -- no
  separate no-travel-needed special case needed in the approach logic
  itself.

  **Verified**: flightcheck stub-compile clean across every touched and
  new file (needed one stub addition: `ScriptableObject.CreateInstance<T>()`,
  since this is the first code anywhere that builds a ScriptableObject
  at runtime rather than only compiling its class definition).
  `webattackverify` gained 8 new checks against the real shipped files:
  stun halts both movement (`SpeedMultiplier == 0`) and firing
  (`ReadyToFire == false`) on an otherwise-ready armed unit; stun
  reapplication takes the longer duration, never the shorter; stun
  overrides an active slow; `SpecialAttackResolver.ResolveInstant`
  applies Damage only to in-range opposing targets (out-of-range and
  same-faction untouched) and Stun only to in-range targets (the caster
  never stuns itself); and `SecondaryAttackCatalog.ForMonster` correctly
  routes all three alien-tech hand families to the Psionic Tractor Beam
  (confirmed `PullAndConsume`), every other hand family (including
  unarmed) to Ground Stomp (confirmed `Stun`, `Range == 0`), and
  Flamethrower confirmed `Damage`-only. All 33 checks (25 from Phases
  4-8, 8 new) pass.

  **Explicitly NOT done: "into the Lab."** Read narrowly (literally
  adding to `site/`, the browser test bench) rather than as a colloquial
  "roll out broadly": the Lab has zero combat-simulation or attack-
  display code today (confirmed by research before starting this work --
  it's purely a creature-appearance/roster gallery), and genome-core has
  no weapon/attack twin at all (`Combat.WeaponFor`'s hand-family switch
  exists only in `roster-client` C#, never duplicated into TypeScript,
  unlike Locomotion/Harvest which DO have genome-core twins). Adding a
  Lab-side "this creature's secondary attack" display would mean
  inventing a first-of-its-kind combat-stat twin in genome-core purely
  for display purposes -- a real, separable follow-up, not attempted
  here without further direction on what the Lab should actually show.
  The classification rule itself (`SecondaryAttackCatalog.ForMonster`'s
  hand-family switch) is trivial to mirror into TypeScript whenever that
  direction is given, since it keys off data the Lab already has
  (`Slots.Hand.Family`).

## v0.1 tuning appendix

To be filled in as playtesting begins: Web Attack cooldown/range/AoE radius.
Placeholders set so far: `WebAttackAbility.HeavyMassThreshold = 3f` (the
Mass threshold separating "non-heavy" from "heavy-class"),
`HeavySlowMultiplier = 0.35f` / `HeavySlowDuration = 3f` (Phase 5 — a
heavy target moves at 35% speed for 3s while caught), `CapturePullSpeed
= 6f` m/s (Phase 6 — how fast a non-heavy target is dragged toward its
captor), `CaptureState.ArriveRadius = 1.5f` (how close counts as
"arrived" — now eaten-on-arrival for citizens, Phase 7). All
placeholders until playtested, per this repo's general v0.1 numbers
policy.
