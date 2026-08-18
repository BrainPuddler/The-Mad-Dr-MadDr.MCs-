# Pending live verification — 2026-08 sessions (Order Sheet, VFX, Electric Arc, Secondary Attack Variety)

Everything below is **implemented, committed, and pushed** on
`claude/mad-doctors-game-design-wacvlu`, but **none of it has been seen
running**: the agent environments used for this work had no Unity
Editor, no browser, and no .NET SDK. TypeScript was genuinely verified
(real `npm test`, real `npm run build`); everything C# and visual was
verified only by brace/paren balance, grep sweeps, and direct signature
cross-checking against the real source.

This file is the checklist for the next session that DOES have an
Editor. Delete or trim entries as they're confirmed.

## 1. Factory Order Sheet (C key / click factory)

Commits `67f8b42`, `eeb504b`, `fe6ff5f`, `25b81f0`, `2ecd3dd`.

- **Open it**: press **C** with the cursor near an own Factory, and
  separately **click the Factory body**. Both should toggle the same
  panel (they call the same `FactoryOrdersHud.Toggle`). Clicking a
  different Factory should switch to it; clicking empty ground should
  leave the sheet's own open/closed state alone.
- **Slot row look** (StarCraft-2-style): a horizontal row of portrait
  tiles docked above `ProductionQueueHud`'s own tile row, slot 0 with a
  live progress bar, a quantity badge per tile, a trailing empty "+"
  slot. Unverified: exact layout/scale at real resolutions, whether
  `UiScale` docking lands correctly against `TileRowTop`.
- **Drop into a slot** while carrying a monster or a battalion. This is
  the one most worth testing hard — it was broken once already by the
  generic "an OnGUI panel claimed this click" guard swallowing the drop
  click before `HoveredSlotIndex` was ever read (fixed in `eeb504b` by
  moving the slot-drop checks ahead of that guard). Confirm a drop
  precisely on a slot still registers, AND that a click elsewhere over
  the panel still does NOT fall through to a world action.
- **Drop onto the bottom-right tile row** (no C key) — same mechanic via
  `ProductionQueueHud.HoveredTileIndex`. While carrying with an empty
  queue, the panel should stay drawn showing a single empty "+" tile as
  a target.
- **Queue is strict FIFO** (`2ecd3dd`): repeatedly dropping monsters on
  a Factory roof must NOT interrupt or reset the in-progress build.
  Builds should complete in order. This was the creator-reported bug
  ("cued monsters not completing") — worth explicitly watching a queue
  of 3+ drain end to end.
- **Roof specimen tracks the ACTIVE build only** (`fe6ff5f`): dropping
  a monster while something else is already building should leave the
  roof display untouched.
- **Multi-Factory fairness** (`eeb504b`): two Factories both producing
  against limited Blood income. Before the fix one Factory won the
  wallet race every frame forever. Needs two real Factories to confirm.

## 2. Area Attack / Psionic VFX

Commit `20f49da`. `SpecialAttackVfx.cs`.

- **Area** (Ground Stomp, Flamethrower Burst, Area Shock): white core →
  blue glow shell → irregular jittering arcs → ground ring at the real
  radius → fade.
- **Psionic** (Psionic Tractor Beam): brightening core → 3 staggered
  translucent ripple spheres, deliberately no ground ring.
- **The projectile is no longer invisible** — `WebAttackAbility`'s bolt
  had no mesh at all before this pass; it now carries a small spinning
  glow.
- Unverified and most likely to need tuning: exact colors, timing,
  emission strength, whether effects read at real RTS camera distance,
  and behavior with many simultaneous attacks. The brief's own final
  question is the test: *"if I saw this for half a second, would I
  understand something supernatural and dangerous just happened?"*
- Check the pool (`VfxPool`) actually recycles rather than growing —
  this is the project's first GameObject pool, no precedent to copy.

## 3. Electric Arc hand family

Commit `e6ca428`.

- **Lab**: breed/mutate until an `electric_arc` hand appears (or graft
  one). Confirm the geometry draws at all — `site/creature-renderer.js`'s
  hand switch has **no `default:` case**, so a missing/broken case
  renders an invisible arm rather than a fallback shape.
- **Unity**: confirm the same hand renders in-battlefield. Unity draws
  from `packages/creature-mesh/src/CreatureBuilder.cs`'s switch, NOT
  `MonsterBody.BuildWeapon` (that primitive path is effectively dead
  code — `CreatureBuilder.Build` never returns null for a well-formed
  genome). Both got a case; only the former actually matters.
- **The two renderers must match** — they're hand-kept in lockstep with
  no shared source. Compare the Lab preview against the in-game model.
- **Arc weapon visual**: jagged crackling line, clearly distinct from
  `laser_array`'s clean straight beam.
- **Arc hits buildings**: confirmed generic in code (no per-weapon-kind
  branch in the AttackBuilding path) but never actually run.
- **Area Shock**: 10-second stun. Confirm the stun really lasts 10s —
  `UnitCombat.ApplyStun` has no cap, verified by reading, not running.

## 4. Lab portrait backfill

Commit `fe6ff5f`, `site/main.js`.

Root cause of "the queue is not showing a picture of the monster" was
that portraits only ever uploaded from the "Save to Stable" button,
which no-ops for an already-saved creature. `renderStable()` now
backfills one sync per creature per session. **Open the Stable, let it
sync, then check the in-game queue tiles show portraits.** Creatures
saved before the portrait feature existed should heal themselves the
first time the Stable is viewed.

## 5. Secondary Attack Variety Expansion (4 races, 21 abilities)

No single commit yet (in progress as this section is written) — see
docs/26 §8 and docs/12's matching entry for full design/reasoning.

- **Every race now has 5-6 secondary abilities, not 1.** Watch a Mad
  Doctor monster, an alien-handed one, an electric_arc-handed one, and
  a Tank in real combat for a while each — confirm each one actually
  uses MORE than just Ground Stomp/Psionic Tractor Beam/Area Shock/
  Flamethrower Burst over time, not always the same single ability
  every equipped unit had before this pass.
- **Context-aware selection**: drop a Mad Doctor monster to low health
  or surround it with several enemies, confirm it reaches for a
  flagged-defensive ability (Defensive Spore Burst) rather than
  continuing to compete on offensive catch-count. Also confirm a
  HEALTHY, unsurrounded monster never fires a defensive ability
  (`IsDefensive` abilities are excluded from the normal offensive
  competition entirely — see `EvaluateBestAbility`'s own doc comment).
- **Fear** (Defensive Spore Burst, Panic Shriek, Psychic Shield,
  Neural Disruption, Discharge Burst, Smoke Grenade): confirm a feared
  unit genuinely can't fire for the duration, then resumes normally —
  and that it does NOT get stuck permanently unable to fire (the timer
  must actually expire).
- **Weaken/Boost** (Mutagenic Pulse, Psychic Pulse, EMP Pulse,
  Suppressive Fire, Combat Stim): confirm a Weakened unit's own attacks
  visibly slow down (longer gap between shots) and a Boosted unit's
  speed up — and confirm `WeaponFx.cs`'s actual per-shot damage/visual
  is IDENTICAL either way (only the rate should change, never the
  damage-per-hit).
- **Possess** (Spore Cloud, Mind Control): this is a LOW-percentage
  roll (3-5% per caught target) — may take several casts to actually
  observe. When it lands, confirm the possessed unit goes quiet
  (doesn't fire/re-target) for its duration, then resumes normally.
- **Toxic Sac's `HazardZoneEffect`**: confirm the thrown sac lands,
  a visible pulsing green patch persists for several seconds, and
  anyone standing in it gets periodically Weakened (not just once on
  landing). Confirm the patch actually disappears/returns to the pool
  after its duration — it should never linger forever or accumulate
  unboundedly if cast repeatedly.
- **`IsPossessed` behavior change**: this field used to be permanently
  `false` (docs/26 Phase 5's own inert placeholder) and is now REAL —
  double-check nothing elsewhere in the game was quietly relying on it
  always reading false (the only other reader found by grep is
  `WebAttackAbility.ShouldCatchCombatant`'s own same-faction exclusion,
  which was explicitly written to handle this case already).

- **Deployment gotcha (found, not a code bug):** if you push a fix to
  `claude/mad-doctors-game-design-wacvlu` and the live Lab still looks
  stale, don't assume the code is wrong first -- check whether the
  change actually reached `main`. `pages.yml` lists the feature branch
  in its `on.push.branches` trigger, but the `deploy` job's
  `environment: github-pages` silently rejects every run from a
  non-default branch (~2s failure, no build steps run). Confirmed via
  GitHub Actions run history 2026-08-18: every feature-branch Pages run
  has failed; only pushes landing on `main` publish. See docs/12's
  matching entry for the full root-cause writeup.

## 6. Known latent issues found but NOT fixed

Deliberately left alone, flagged rather than silently touched:

- **Destroyed buildings leak registry entries** (docs/28 §6): a
  collapsed building's window renderers are never destroyed, only
  squished and recolored, so `EmissiveAnimator`/`DynamicLightBudget`
  self-prune (`if (e.Renderer == null)`) never fires. Produces no
  visible bug — pure CPU/light-budget hygiene. Fix is either
  `Object.Destroy` on collapse or a real `Unregister`.
- **Two pre-existing brace/paren imbalances** in comment prose
  (`packages/roster-client/src/Weapon.cs:80`,
  `packages/creature-mesh/src/CreatureBuilder.cs:253`) — confirmed via
  `git show HEAD` to predate this session. Harmless; noted only so the
  next balance-check sweep doesn't mistake them for new damage. (A third
  such imbalance, in `RuntimeCityBuilder.cs`, WAS fixed in `eeb504b`.)
- **`Citizen.cs` is the last capsule holdout** (docs/34 §0) — still
  waiting on the Civilian Victims work before it moves onto
  `HumanCharacterKit`.
