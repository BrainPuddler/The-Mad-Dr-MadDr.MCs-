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

## 7. Win/loss states (docs/37)

No commit hash yet as this section is written -- see docs/37 and
docs/12's matching entry for full design/reasoning.

- **The `MatchEndHud` overlay itself** -- never seen rendering. Play a
  match to a real conclusion (easiest: let an AI opponent's Hq fall, or
  destroy the human's own via the Editor/debug tools) and confirm: the
  dark modal actually appears and is readable, VICTORY/DEFEAT/DRAW
  colors read correctly from the human player's own perspective, the
  reason line matches what actually happened, and "Play Again" genuinely
  reloads the scene into a clean, playable fresh match (not a half-reset
  state with leftover GameObjects from the finished one).
- **Elimination via real combat**, not just direct `ApplyBuildingDamage`
  calls (all match-core coverage uses those) -- confirm a Hq actually
  dying to normal unit/building combat in a live match triggers the
  overlay, and that `SimBridge.Pump` genuinely stops advancing the sim
  afterward (units should freeze in place, not keep fighting).
- **Dominion in a real match**: hold ≥60% of a real map's emitters for a
  full 4-minute Lumen Cycle and confirm the overlay fires with the right
  reason text. The match-core test suite proves the tick-counting math;
  it says nothing about whether a human can actually plausibly hold that
  much map in a real game, which is a genuine balance question this pass
  never claimed to answer.
- **The 15-minute time cap** in practice -- confirm a match that runs
  the full 15 minutes actually ends rather than running forever, and
  that the territory-score reason text reads sensibly given whatever
  state the map is actually in by then.

## 8. Win-progress HUD, top-center one-liner (docs/37 §6-§8)

New `WinProgressHud.cs` -- never seen rendering. Moved (§8) from
docking above the minimap to a self-positioned top-center panel; the
checklist below reflects the CURRENT (top-center) placement.

- **Positioning**: confirm the panel actually sits centered at the top
  of the screen (`UiScale.Width * 0.5f`, y=16px) at different `UiScale`
  reference resolutions/aspect ratios, and that it genuinely does NOT
  overlap `HudStatus`'s top-left lines, `AnalogClockHud`/`ResourceHud`'s
  top-right column, or `HudStatus`'s own centered help popup when
  opened -- this was verified by reading the real Rect math (docs/37
  §8), not by eye, so the actual on-screen check is the thing that's
  genuinely still outstanding.
- **Legibility**: one row, three segments (Army/Dominion/Territory),
  bold red/green percentage text -- confirm it's actually readable
  against a busy city background at real HUD scale, and that the three
  segments don't feel cramped or misaligned on one line.
- **Live values feel meaningful**: watch all three percentages during an
  actual match against a real AI opponent (docs/30) and confirm they
  move in directions that make sense -- Army % should track visibly
  losing/winning fights, Dominion % should jump to a real nonzero value
  the moment 60% emitter control is captured and reset the instant it's
  lost, Territory % should track building/emitter swings. These are
  flagged heuristics (docs/37 §6), not a claimed-accurate win-probability
  model -- the check here is "does it feel informative," not "is the math
  provably optimal."

## 9. Match duration selector (docs/37 §7)

New "Match Length" row in `MatchSetupHud` -- never seen rendering.

- **The button itself**: confirm it appears correctly alongside the own-
  race row (not overlapping/cramped now that the panel grew a row
  taller), cycles 15 min -> 30 min -> 45 min -> Unlimited -> back to
  15 min on repeated clicks, and that the panel's own background/height
  actually accounts for the extra row (no visual overflow/clipping).
- **Each option actually changes match behavior**: pick each of the
  four durations in turn, start a real match, and confirm the time cap
  genuinely fires at the chosen length (or never, for Unlimited) --
  the match-core test suite proves the tick-counting math is correct in
  isolation; it says nothing about whether the value picked in the menu
  actually reaches `MatchState.Create` correctly through the real
  `RuntimeCityBuilder.BeginMatch` -> `SimBridge.StartMatch` call chain
  in a live scene.
- **`showMatchSetupHud` off (default scenes)**: confirm
  `matchDurationMinutes`'s own Inspector default (15) still produces the
  original, unchanged 15-minute-cap behavior for every scene that never
  shows this menu at all.

## 10. Worker orphan-rescue (docs/12)

New `RuntimeCityBuilder.NearestOrphanedConstructionSite`/
`Worker.StationedBuildingId` -- never seen running.

- **The core guarantee**: queue several buildings at once with too few
  Workers to staff them all immediately, then either let debris keep
  distracting every idle Worker OR let the one Worker en route die
  mid-walk, and confirm every site eventually gets a Worker within
  roughly `RuntimeCityBuilder.OrphanRescueSeconds` (12s) of going
  unstaffed -- not sooner by luck, not never.
- **No redundant double-dispatch**: while a Worker is legitimately
  walking toward a site (`SeekBuild`), confirm no SECOND Worker also
  gets pulled toward the same site once the 12s mark passes (`Worker.
  StationedBuildingId`'s whole job).
- **Ordinary debris-first behavior unaffected**: confirm normal play
  (a site staffed well within 12s by the ordinary idle-priority cadence)
  looks and feels exactly like before this change -- the rescue path
  should be invisible unless something has genuinely gone wrong.

## 11. Minimap blip zoom-scaling (docs/12)

`Minimap.cs`'s `BlipSizePixels` -- never seen rendering.

- **Actually scales**: zoom the minimap in and out and confirm unit/
  citizen/traffic dots visibly grow/shrink in step with the terrain
  features around them, rather than staying a fixed pixel size.
- **Readability at both extremes**: confirm `MinBlipPixels`/
  `MaxBlipPixels` (1.5/24) keep a dot visible at max zoom-out and
  non-obnoxious at max zoom-in -- these are invented v0.1 numbers, not
  tuned against a real screen.
- **`unitBlipWorldMeters`/`crowdBlipWorldMeters`** (8m/4m) -- confirm
  these read as sensible relative sizes against real building/hex scale
  (`HexCoord.HexMeters` = 20m) rather than needing retuning once seen.

## 12. Known latent issues found but NOT fixed

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
- **`Citizen.cs`'s capsule holdout is FIXED (2026-09-16)** — see entry
  24 below for the checklist. (Civilian Victims itself -- the rescue-
  mechanic gameplay system, docs/34 §0/§6 -- is still not built; only
  the capsule-to-rig reskin closed here.)

## 13. Grandma wheelchair: real circular wheels + seated legs (`HumanoidCombatant.cs`)

Creator report: the wheelchair's "wheels" were reading as flattened
blocks, not wheels, and the whole variant didn't visibly read as a
person sitting. Fixed by replacing the two flattened-cube wheels with
`PrimitiveType.Cylinder` discs (rotated so the circular face is what a
side-on camera sees) and adding a new `BuildSeatedLegs()` — a static
thigh+shin silhouette parented under the character root, independent of
`HumanCharacterKit`'s shared (walking) leg path, since `HasLegs` stays
`false` on purpose for her `TickWheelchair` animation routing.

- **Wheels actually read as circular**, not a fender/panel, from the
  normal RTS camera angle — confirm at both close and default zoom.
- **Seat/wheel/leg vertical alignment**: `BuildWheelchair`'s seat height
  and `BuildSeatedLegs`' hip height both derive from the same
  `SeatedHeight` profile value independently (not a shared computed
  constant) — confirm the torso doesn't visibly float above or sink
  below the seat cushion, and that the thighs meet the torso's hip
  convincingly rather than gapping.
- **Legs don't clip the seat/frame or the wheels** — the thighs project
  forward past the seat's front edge on purpose; confirm that reads as
  "sitting" and not as legs poking through solid geometry.
- Untested numbers (v0.1, reasoned not measured): `wheelDiameter` 0.64,
  `wheelThickness` 0.06, `legOffsetX`/`thighLength`/`thighThickness`/
  `shinThickness`/`footClearance` in `BuildSeatedLegs`.

## 14. Creature LODGroup (docs/39 §11 item 1) -- never seen rendering

New `LabMeshBuilder.AttachLodded`, `MonsterBody.Build` now builds three
`CreatureBuilder.Build` passes per monster instead of one. The C# mesh
math itself (`packages/creature-mesh`) is real-verified (103 passing
`dotnet test` runs, including 5 repeats to rule out the thread-race
flake found and fixed during this work) -- only the Unity wiring below
is unverified.

- **Spawn a monster, zoom through all four bands** (Close 8–25 m, Normal
  25–110 m, Overview 110–250 m, Map 250–400 m) and confirm the body mesh
  visibly changes detail at the right points, doesn't pop at the wrong
  moment, and is fully culled (body invisible, minimap blip still shown)
  past 250 m.
- **Check every body plan at LOD2** (`Detail=0.3`), not just tetrapod —
  the new `Lathe` floor (6) is untested visually; confirm the torso
  still reads as a body and not a faceted mess on blob/serpentine/
  treant/floater's larger lathe-built masses especially.
- **Winding/normals at reduced segment counts** — confirm no inside-out
  faces or lighting artifacts appear at LOD1/LOD2 that aren't present at
  LOD0 (the geometry math is unchanged, only segment counts are lower,
  but a Frame Debugger/Editor viewport is the only way to actually see
  shading artifacts).
- **Legs/wings still align correctly** under the LOD-swapped torso —
  they're built once at full detail and don't LOD-swap themselves, so
  confirm they don't visibly separate from the body as the torso mesh
  changes underneath them.
- **Frame Debugger / Profiler capture** per docs/39 §10.2 at the default
  70 m framing with a realistic monster count on screen (the §10.3
  50-monster curve), recorded back into docs/12 — this is the actual
  performance claim this whole item exists to deliver, and nothing in
  this environment could measure it.
- **`lodBias` PC-tier change (1 instead of 2)** — confirm no other
  system was quietly relying on the old doubled LOD thresholds (grep
  found none, but grep isn't a substitute for seeing it run).

## 15. Camera zoom-out ceiling: 400 m -> 300 m, now an Inspector field

`SimpleCameraRig.maxHeight` (new public field, default 300) replaces the
old `private const float MaxHeight = 400f`.

- **Scroll-zoom and Shift+up stop at 300 m**, not 400 — confirm both
  input paths actually respect the new default and that the camera
  can't be pushed past it by any combination of the two in the same
  frame.
- **The Inspector field actually shows up and works** — change
  `maxHeight` on the `SimpleCameraRig` component in the Inspector
  (try something below 300, like 150, and something above, like 500)
  and confirm zoom/Shift-move immediately respect the new value with no
  recompile needed.
- **Minimap frustum box** at the new ceiling — confirm `DrawCameraFrustum`
  still reads sensibly at max zoom-out (its clamp already saturates well
  under both 300 and the old 400, so this should be a no-op, but it's
  worth a look since the Map band shrank).
- **Map band feel** — confirm 250–300 m still feels like a useful
  "strategic overview" range and doesn't feel suddenly cramped compared
  to the old 250–400 m now that it's 100 m narrower.

## 16. Creature vertex-color merge + new shader (docs/39 §11 item 2) -- never seen rendering, never compiled

New `Assets/Shaders/CreatureVertexColor.shader` and `LabMeshBuilder
.AttachChunksMerged`. This is the highest-risk unverified item in the
backlog so far, because a shader is the one kind of change read-through
literally cannot validate (HLSL either compiles or it doesn't) — but
also the easiest to spot-check, because a broken shader fails loud.

- **Step one, before anything else: open the Editor and look at a
  spawned monster.** If it's solid magenta, the shader failed to
  compile — check the Console for the exact HLSL error before looking
  at anything else below. If it renders any actual color, the shader at
  least compiled.
- **Per-chunk color survives the merge** — compare a monster's colors
  against what it looked like before this change (or against the Lab's
  own preview, which is unaffected by this Unity-only change): skin
  tone, iron/brass hardware, franken-face details, etc. should all
  still read as visually distinct from each other, just now sharing one
  mesh instead of separate GameObjects.
- **Emissive parts still glow** — eyes, neon-ish parts, heart bolts
  (whatever the 3 emissive chunks turn out to be on a given creature)
  should still visibly glow, dimmer or brighter than before is expected
  (the shared 0.6 `_EmissionStrength` approximates three different real
  values of 0.30/0.85/1.00 — see docs/12 for which), but "glows at all"
  is the actual bar.
- **Translucent parts unaffected** — the mastermind's glass dome / the
  blob's gelatin shell should look exactly as before (this path wasn't
  touched).
- **Lighting/shading looks reasonable** — no fully-black or fully-flat-
  white creatures, shadows still cast/receive correctly, no z-fighting
  or inside-out faces at the merge seams between what used to be
  separate chunks.
- **Renderer count actually dropped** — confirm via the Frame Debugger
  or a quick Profiler/hierarchy check that a spawned monster now shows
  2-3 renderers under its LabBody holder instead of 12-23. This is the
  actual performance claim this item exists to deliver.
- **SRP Batcher picks it up** — Frame Debugger should show the shared
  opaque/emissive materials batching across multiple monsters on screen
  (no `MaterialPropertyBlock` is used on these renderers, by design, so
  this should just work — but "should" is exactly what needs confirming
  here).
- **Legs/wings unaffected** — they still use the old unmerged
  `AttachChunks` path on purpose; confirm they still look and attach
  correctly (this item didn't touch their code, but confirm nothing
  about the shared body holder's restructuring broke their parenting).

## 17. Low-poly sphere/cylinder swap (docs/39 §11 item 3) -- never seen rendering

`RuntimeCityBuilder.SpawnPrim`, `MonsterBody.Part`, `Tank.Prim`, and
`TrafficCar.MakeBulb` now redirect `PrimitiveType.Sphere`/`Cylinder` to
new `ProceduralMeshKit.IcoSphere`/`LowPolyCylinder` meshes instead of
Unity's stock primitives. This is a choke-point fix — every existing
call site across `BaseDresser`/`BuildingDresser`/`RoadDresser`/
`MonsterBody`/`Tank`/`TrafficCar` changes automatically, with none of
those individual call sites themselves edited.

- **Every sphere/cylinder-shaped prop in the game still looks
  spherical/cylindrical** — streetlamp bulbs, Factory/Control Centre
  domes and rivets, fire hydrants, tank turrets, traffic car head/brake
  lights, roundabout globes, etc. At normal play zoom an 80-tri icosphere
  should be indistinguishable from the old 760-tri stock sphere; check
  it doesn't read as visibly faceted at the Close band either.
  `FaceOutward` should mean no inside-out faces, but this is exactly the
  kind of thing that's only actually confirmed by looking at it.
  Consciously accepted risk: this is the SAME winding-computation
  approach (`FaceOutward`) that has already needed one live-Editor
  correction on this project's other `ProceduralMeshKit` shapes (the
  2026-07 double-siding fix in `PropLibrary.Spawn`) — if a new sphere/
  cylinder prop renders invisible (back-face culled) rather than
  correctly, that's the known failure mode to check first.
- **World-scaled UV tiling still works** on any Sphere/Cylinder spawned
  with a textured material (`ApplyWorldScaledTiling` is re-applied
  explicitly in `SpawnLowPolyPrim` specifically to preserve this —
  confirm a tiled-texture sphere/cylinder, if any exist, doesn't look
  stretched or untiled compared to before).
- **Run `unity-client/Tools~/check-no-stock-primitives.sh`** after any
  future change that touches primitive spawning, to catch a
  reintroduced stock Sphere/Capsule outside the VFX/Big Brain jar
  exception — this is the actual lint rule docs/39 item 3 asks for
  (there's no CI to run it automatically yet).
- **Triangle/renderer count actually dropped** — confirm via the same
  `LogCityBuildCensus`/Profiler numbers docs/39 §10.2 asks for elsewhere
  in this backlog; this item's own contribution should show up as a
  measurable per-sphere/cylinder triangle reduction across the whole
  scene, on top of items 1/2's creature-specific wins.

## 18. `MaterialPropertyBlock` batching break fixed via a cached tiling-Material cache (docs/39 §11 item 4)

`RuntimeCityBuilder.ApplyWorldScaledTiling` no longer overrides
`_BaseMap_ST` per-instance via `MaterialPropertyBlock` (confirmed, from
a real Frame Debugger capture this session, to be exactly why opaque
draw events went from 1218 at the default height to 8601 above ~200 m
without a matching drop in SRP-batched entries). It now looks up/builds
a cached, shared Material variant per (base material, tile-count bucket
rounded to the nearest 0.25) and assigns that to `renderer.
sharedMaterial` instead — same pattern `PropLibrary.DoubleSidedCache`
already uses for a different property.

- **Take a fresh Frame Debugger capture at the same >150 m height this
  bug was originally found at** (the two Frame Debugger + Profiler
  screenshots from this session are the baseline — before: 1218 opaque
  events, 33 SRP-batched / 1185 raw `RenderLoop.Draw`, at the default
  height; 8601 at the wide-zoom capture with no batcher breakdown taken
  yet). Confirm the fix actually moves most of those events from raw
  `RenderLoop.Draw` into `RenderLoop.DrawSRPBatcher`, and confirm actual
  frame time improves at wide zoom, not just the draw-call count.
- **Every textured building/prop still tiles correctly** — walk a few
  differently-sized building walls and small props and confirm the
  texture still reads at a sensible density (rounding the tile count to
  the nearest 0.25 before caching should be visually invisible, but
  "should be" is exactly what needs confirming here).
- **Low-poly sphere/cylinder props (item 3, `PropLibrary`-routed) still
  tile correctly too** — these compose the double-sided-clone cache
  (`PropLibrary.DoubleSidedCache`) with the new tiling-variant cache, a
  two-level lookup that was never live-tested; confirm a tiled sphere/
  cylinder (e.g. a lamppost bulb) looks right, not stretched, doubled,
  or reverted to a flat default tiling.
- **Watch the Material variant cache doesn't balloon** — check the
  Profiler's Memory module for total Material count after a full city
  build; this is bounded by (distinct base materials) × (distinct tile
  buckets actually generated), not by prop instance count, but that's a
  read-through claim, not a measured one yet.
- **Roof matte finish (`ApplyMatteFinish`) was deliberately left alone**
  — it still uses its own `MaterialPropertyBlock` (smoothness override
  on `GableRoof` shapes only), a known, smaller, NOT-fixed-this-session
  contributor to the same class of batching break. Out of this item's
  scope; flag separately if it turns out to matter once measured.

## 19. Camera zoom-out ceiling: 300 -> 150 -> back to 300, same session (verifying item 4's fix)

`SimpleCameraRig.maxHeight` dropped from 300 to 150 as a stopgap once
the item 4 root cause was identified but not yet fixed, then raised
straight back to 300 once the fix (entry 18) landed -- specifically so
a fresh Frame Debugger capture above 200 m can confirm the fix holds,
rather than leaving the question open indefinitely.

- **THE thing to check now: take a fresh Profiler/Frame Debugger
  capture at 250-300 m**, the same wide-zoom framing as the original
  2026-09-16 capture that found the problem (docs/12), and compare
  against these two numbers from that capture: opaque draw events
  should stay far below the old 8601 (ideally close to what a similarly
  wide view now shows with proportionally more going through
  `RenderLoop.DrawSRPBatcher` and fewer as raw `RenderLoop.Draw`), and
  actual frame time/ms should hold up, not just the draw-call count.
- **If the cliff is gone:** `maxHeight` can stay at 300 (or go back
  toward the old 400 m if there's appetite for it) and docs/39 §11 item
  7 (Map-band impostor) becomes buildable again.
- **If the cliff is still there:** drop `maxHeight` back to 150 and
  treat item 4's fix as incomplete rather than shipping a known-bad
  ceiling — check whether `ApplyMatteFinish`'s still-`MaterialPropertyBlock`
  roof override (deliberately left alone in item 4/5, docs/12) turns out
  to matter more than assumed, or whether something else entirely is
  the remaining cost at that range.
- **Scroll-zoom and Shift+up now stop at 300 m again**, not 150 —
  confirm both input paths respect the restored default, same check as
  entry 15 already asked for.

## 20. Shadow hygiene pass (docs/39 §11 item 5 / §8)

- **Window grids no longer cast a shadow** — spawn a building with lit
  windows and confirm the facade's own shadow (from the wall mesh
  behind the grid) still reads correctly; the grid itself should visibly
  stop throwing its own separate shadow shape. Watch specifically for
  any facade that looked "right" only because the window grid's shadow
  was filling in for something else.
- **Small props and thin ground slabs stopped casting** — walk a
  sidewalk, some lane-paint dashes, and a crosswalk, plus a hydrant/
  mailbox-scale prop, and confirm none of them throw a shadow anymore.
  Then check a handful of larger dressed props (roofs, lampposts, market
  stalls) that should be UNAFFECTED — the two triggers (largest
  dimension < 1 m, or Y-scale < 0.3 m) were sized off a read-through of
  this file's own scale literals, not measured against every call site,
  so a false-positive catch (something that should still cast losing its
  shadow) is the actual risk to check for here, not just "did the
  intended things turn off."
- **Cascades 4 → 2 on PC** — confirm shadow quality at the Normal band
  (25–110 m, where >80% of play happens) doesn't visibly degrade;
  docs/39 §8's own reasoning is that 2 cascades at the unchanged
  resolution should give MORE texels where units actually stand, not
  fewer, but that's a claim to verify by looking, not assume.
- **Additional-light shadow resolution 2048 → 1024** — no light in the
  codebase currently casts one (see docs/39 §11 item 5's own note), so
  this should be a total no-op today; flagging only so a future light
  that DOES opt into shadows doesn't get a surprise resolution.

## 21. LOD-aware monster animation tick rate (docs/39 §11 item 6, monsters only)

New `AnimationLodBudget.cs` (a plain script file, needs Unity to
generate its own `.meta` on first open -- same standing note every new
script in this repo gets, commit it once it appears). `MonsterBody.
UpdateLocomotion` now skips its own body when the camera is in the
Overview band (every second frame) or Map band (frozen), folding
skipped `dt` into the next tick that runs.

- **This is currently near-impossible to actually exercise** — the
  camera's `maxHeight` cap sits at 150 m (entry 19), and `Overview`
  doesn't start until 110 m, so there's only a narrow 110–150 m sliver
  where the every-second-frame throttle can even engage, and `Map`
  (250 m+) is fully unreachable. Confirm the throttle at least doesn't
  misbehave in that narrow sliver — no visible stutter/slow-motion gait,
  no popping — since that's the one part of this that's reachable today.
- **The real test comes once `maxHeight` is raised back** (after entry
  18's fix is confirmed) — at that point, re-check this entry: spawn a
  crowd of monsters, zoom to Overview (110–250 m) and confirm gait/
  breath/wing-flap visibly slows to every-other-frame without looking
  like slow motion (the accumulated-dt fold-in is what's supposed to
  prevent that), then zoom to Map (250 m+) and confirm animation fully
  freezes with no idle-pose jitter.
- **Landing/liftoff transitions still look right** even when throttled
  — a flyer's `SnapFeetToGround()` call only fires on the tick that
  detects the airborne→grounded transition, which could now land up to
  one throttled tick later than before; confirm this doesn't produce a
  visible foot-through-ground moment at the transition itself.
- **Humanoid animation (`HumanCharacterAnimator`) is NOT part of this
  fix** — see docs/39 §11 item 6's own note on why (four separate call
  sites, no shared choke point found yet). Not a regression, just an
  intentionally unfinished half of this item.

## 22. Map-band body cull (docs/39 §11 item 7)

`MonsterBody.SetBodyVisible` hides `_torso` (body/wings/weapon) and
every leg segment once the camera enters the Map band (250 m+, reachable
again now that `maxHeight` is back at 300 — entries 18/19), leaving the
selection ring, click hitbox, and minimap blip untouched.

- **The actual thing to look at:** zoom out past 250 m with a monster
  selected and confirm the body visibly disappears while the selection
  ring and minimap blip both keep showing exactly where the unit is —
  this is the one item this session where the pass/fail is genuinely
  "did it look right," not just "did it not crash."
- **Zoom back in through 250 m** and confirm the body reappears cleanly
  — no pop-in glitch, no frozen mid-gait pose (the throttle from entry
  21 should mean it resumes wherever its accumulated dt puts it).
- **Legless plans** (blob/serpentine/floater/treant — `_legs.Count == 0`)
  only have `_torso` to hide; confirm one of these specifically, not
  just a legged monster, since the leg-hiding code path is untested by a
  legless creature entirely.
- **A creature mid-special-attack or mid-gait-fail-safe when it crosses
  into Map band** — confirm nothing about `SetBodyVisible` fighting with
  those other systems (e.g., a special-attack VFX anchored to a now-
  hidden torso transform still tracking the right position, even though
  the torso itself isn't rendering).
- **Weapon and wings specifically** — these were NOT covered by item 1's
  own LODGroup cull (only the LOD0/1/2 body mesh was), so this is the
  first time they've ever been hidden by distance at all; confirm they
  actually disappear along with the rest of the body rather than
  floating disembodied (the exact failure mode this item exists to fix).

## 23. LOD-aware animation, humanoid half (docs/39 §11 item 6 completion)

Finishes item 6 (entry 21 was monsters only). `HumanoidCombatant`,
`Worker`, and `RosterInfantryView` each gate every
`HumanCharacterAnimator.TickXxx` call through the same
`AnimationLodBudget.TryGetAnimDt` helper `MonsterBody` uses indirectly --
movement/combat/state-machine code in those classes is untouched, only
the animator calls themselves throttle.

- **Citizens/soldiers/police/militia (`HumanoidCombatant`) and Workers**
  visibly slow their gait/idle/aim animation to every-other-frame in the
  Overview band and freeze in Map, same as monsters -- confirm this at
  a crowd of these specifically, not just monsters, since this is a
  DIFFERENT code path with its own accumulator fields per instance.
- **Roster infantry (`RosterInfantryView`'s Riflemen/Flamethrower
  Troopers)** -- this one shares ONE animation-tick decision across
  EVERY infantry unit per frame (a single manager, not one MonoBehaviour
  per unit) rather than each unit having its own accumulator; confirm
  a squad of these throttles together correctly, and that a unit that
  spawns mid-Overview-band doesn't get a broken first tick (it should
  just inherit whatever `animTick`/`animDt` that frame already computed,
  same as every other live unit).
- **A dying unit crossing into Map/Overview band** — `TickDeath` is now
  throttled/frozen too for all three classes; confirm a death animation
  doesn't look stuck/frozen mid-collapse in a way that reads as a bug
  rather than "distant unit, animation throttled." The destroy-after-
  timer logic itself (`_deathTimer`/`v.DeathTimer`) still counts down on
  the REAL per-frame `dt`, unaffected by animation throttling, so a
  frozen-looking corpse should still despawn on schedule even if its
  pose never finished animating -- confirm that's actually true, not
  just intended.
- **`HumanCharacterKit.cs` is untouched** — investigation found it's a
  geometry/rig-definition file with no `Update()` or `Tick` calls of its
  own at all, so there was nothing to gate there; not a gap, just a
  correction of the original item 6 note's assumption that it was one
  of the four call sites.

## 24. Citizen capsule-to-rig reskin (docs/34 §0/§6, docs/36 §12 holdout closed)

`Citizen.cs` builds a real `HumanCharacterKit` rig via a new
`HumanCharacterProfile.Civilian(int variant)` (8 fixed plain-clothes
looks + a little height jitter, picked by hashing `GetInstanceID()`)
instead of styling a stock Capsule primitive. Walk/flee/forced-flee/
captured-drag all now drive `TickLocomotion`/`TickIdle` from the REAL
displacement each state's own movement code already computes (never a
speed*dt guess), gated by the same `AnimationLodBudget` throttle every
other humanoid uses (docs/39 §11 item 6). `RuntimeCityBuilder.SpawnCitizens`/
`SpawnFleeingOccupant` spawn a plain GameObject now instead of a Capsule
primitive; `check-no-stock-primitives.sh`'s citizen-specific allowlist
exception was removed (confirmed the script still passes clean with
zero exceptions needed for `RuntimeCityBuilder.cs` now).

- **The actual visual check:** spawn a match, watch a citizen walk its
  sidewalk errand and confirm it reads as a small person (torso/head/
  arms/legs swinging in a walk cycle), not a floating pill or a
  T-posed/frozen rig. Check several citizens at once for the 8-look
  palette variety (plus the height jitter) actually reading as "a crowd
  of different people," not 8 visibly-cloned ranks.
- **Fleeing (both proximity and forced-building-collapse) should read as
  a SPRINT** — faster gait, bigger arm/leg swing (`running: true`) than
  the ambling walk cycle, not just a faster slide.
- **Being captured/dragged toward a monster** — this is the ONE new,
  never-before-existing behavior (the old capsule had no gait to begin
  with, so nothing could look wrong here before): confirm the citizen's
  legs actually move while it's pulled toward its captor, rather than
  a static rig sliding along the ground (the exact "skating" the
  project's own animation rule exists to prevent) — this is measured
  from the real per-frame drag displacement, but has never been seen
  rendered.
- **Right-click "order eat this citizen" still works**
  (`WaypointCommander.cs`'s `hit.Value.collider.GetComponentInParent<Citizen>()`)
  — `HumanCharacterKit.Build` gives every citizen a fresh `BoxCollider`
  sized to the rig's own bounds (replacing the stock Capsule's own
  collider 1:1, not removing collision entirely) -- confirm clicking a
  citizen on-screen still targets the right one and a monster still
  visibly arrives and eats it.
- **Sub-1m-prop shadow rule (docs/39 §11 item 5) doesn't apply here** --
  Citizens don't go through `RuntimeCityBuilder`'s `SpawnPrim`/`SpawnMesh`
  choke points at all (their geometry comes from `HumanCharacterKit
  .Build` directly), so they get whatever shadow-casting default a
  fresh `MeshRenderer` has (`.On`) same as every other rig-based
  humanoid -- consistent with Worker/HumanoidCombatant, not a new gap,
  but worth confirming a crowd of citizens doesn't reintroduce a
  shadow-cost regression at scale (`citizenCount` can be large).

## 25. Facade normal-map rollout (docs/40 §3 item 0)

**2026-09-16 update, from the creator's own `~/Library/Logs/Unity/
Editor.log` (read directly, not asked about): confirmed compiles AND
survives a real play session.** The log shows a real Unity 6000.3.13f1
session on this exact working directory: `*** Tundra build success
(2.75 seconds)` compiling `Assembly-CSharp.dll` with this item's
changes included (`WeatherController.cs.meta`/`RainToggleHud.cs.meta`
exist on disk, proving the AssetDatabase imported item 1's new files
from the same session), then a real match played on the Village preset
(seed 42, "City build census... 86696 GameObjects, 83889 renderers")
with 24 citizens eaten and several harvest banks over the session,
ending in a clean Editor shutdown -- zero `LogError`/exception/shader-
compile-error lines anywhere in the 1040-line log. This resolves the
property/keyword-correctness risk this entry originally flagged
(`_BumpMap`/`_NORMALMAP` do exist and do not throw in this project's
URP version). **Still NOT confirmed: the actual visual read** -- the
log has no way to show whether the brick/stone bump depth is visible
or looks right at a raking light angle; that half of this entry's own
checklist below still needs the creator's eyes, not just a clean log.

New `PbrTextureAtlas.BuildNormalFromHeight` (central-difference
height-to-tangent-space-normal, same technique `BrainTextureKit.
BuildNormal` uses) plus three independent height functions
(`BrickHeight`/`LimestoneHeight`/`DressedStoneHeight`) that each
re-read their matching albedo builder's own per-pixel mortar/joint/
jitter rule -- not shared code, so this pass never touched the
already-shipped `BuildBrick`/`BuildLimestone`/`BuildDressedStone`
albedo output. `_BumpMap`/`_NORMALMAP` wired into `BuildingDresser.
MTextured` and `BaseDresser.MTextured` (both pre-existing per-file
copies of the same idiom) and every wall material built from Brick/
Limestone/DressedStone: `BuildingDresser.Brick`/`Cream`/`Seafoam`/
`Mustard`/`Concrete`/`RustRed`, `BaseDresser.DoctorDarkBrick`/
`DoctorStone`/`DoctorCastleStone`/`PedestalPlaqueMat` -- every
civilian building wall and every faction masonry surface in the game.
Also fixed `RuntimeCityBuilder.ApplyWorldScaledTiling` to copy the
albedo's world-scaled tiling bucket onto `_BumpMap` too, so the normal
map can't drift out of registration with its own albedo once an
object gets its per-size tiled material variant.

**Genuinely higher-risk than most entries in this file, stated
plainly rather than glossed over**: `_BumpMap`/`_NORMALMAP` are stock
URP/Lit property/keyword names, correct per Unity's own shader source,
but this is the actual FIRST live test of whether they render
correctly in this project at all. The one prior usage
(`BaseDresser.BrainMaterial`, the Big Brain jar) was never confirmed
either -- its own doc comment already said so, and a later 2026-08
weathering-pass session (docs/12) explicitly declined to build more
normal-map work on top of it for exactly that reason. That decision
was never carried into this file as a checklist item, which is a real
gap this entry also retroactively covers (see the un-numbered note
right below).

- **The actual visual check**: any brick or dressed-stone building wall
  at Close/Normal zoom, ideally under a raking/low-angle light (dawn,
  dusk, or a nearby streetlamp) where a working normal map reads as
  real coursing depth and a broken one reads as either flat (map not
  actually applied/enabled) or visibly WRONG -- inverted bumps reading
  as engraved instead of raised, or a moire/banding artifact from the
  central-difference step being too large or too small at this
  texture's 64x64 size.
- **Property/keyword correctness**: confirm `_BumpMap`/`_NORMALMAP`
  actually engage URP/Lit's normal-mapping path in this project's
  specific Unity/URP version (6000.3.13f1) -- the property names are
  correct per Unity's long-stable source, but "correct name" and
  "actually wired to the right shader variant in THIS project" are
  different claims, the same gap that made the Big Brain jar's own
  usage unconfirmed for two sessions running.
- **Tiling registration**: zoom in on a large building (apartment/
  office tier, several world-scale tiling buckets) and confirm the
  brick/stone bump pattern lines up with the color pattern -- the
  `ApplyWorldScaledTiling` fix above is reasoned, not rendered.
- **If this looks broken**: the fallback is trivial and low-risk to
  apply -- drop the `normalTex` argument at each of the 9 call sites
  listed above (or pass `null`) to instantly revert to the pre-this-
  entry flat-shaded look; no other system depends on these normal maps
  existing.

**Retroactive gap, not new work**: the Big Brain jar's own
`BrainMaterial` (`BaseDresser.cs`, `_BumpMap`/`_NORMALMAP`/
`_OcclusionMap`/`_MetallicGlossMap`/`_ParallaxMap`, shipped 2026-07/08)
was never added to this checklist despite its own doc comment flagging
it as unconfirmed from day one. Folding it in here since it's the same
underlying question as item 25 above: **confirm the Big Brain jar
itself actually shows visible fold/vein bump detail, AO darkening in
the vessel grooves, and a metallic-gloss response on its brass rings**
-- if that FIRST usage turns out to be broken, item 25's newer usage is
almost certainly broken the same way, and vice versa; check them
together, not independently.

## 26. Wet-response weather toggle (docs/40 §3 item 1)

**2026-09-16 update: same real play-session evidence as entry 25 above
covers this item too** (same session, same commit's changes) --
`RoadDresser`'s new registration wrappers around `Asphalt`/`Sidewalk`/
`RoundaboutCurb`/`IslandStone` ran during that session's real city
build (the census log line confirms `RoadDresser` built successfully),
and `WeatherController.Tick`/`WetSurfaceRegistry.SetWetness` ran every
frame via `LumenCycleController.Update` for the whole ~24-citizen
session with no exception. **Not confirmed: whether the rain toggle was
actually clicked, or whether wet asphalt visually reads as wet** -- the
log has no record of button clicks or visual state, only that the code
path never crashed. This item's own HUD-stack edits (`RainToggleHud`'s
new position, and the `BuildMenuHud`/`BarracksHud`/`CollectorLabHud`
anchor updates that came with it) were part of the same commit this
play session covers, so "renders without an OnGUI exception" is
confirmed for that stack too -- but "nothing visually overlaps" is
still a real screen check, not a log one. Item 2's own `RainSystem` was
written AFTER this play session closed and has none of this coverage
-- see entry 27.

**2026-09-16, second update: a real correctness bug was found and fixed
in this item AFTER the play session above, so that session's "ran
without exception" coverage does NOT mean "the wet effect actually
worked" -- full story in docs/12's own new entry, condensed here.**
`RoadDresser.Asphalt()` (and every Cylinder/Sphere-shaped wet-registered
prop -- the roundabout's own circular asphalt/curb/sidewalk) routes
through TWO separate material-cloning layers (`PropLibrary
.GetDoubleSidedVariant`, then `RuntimeCityBuilder
.ApplyWorldScaledTiling`), each producing a NEW `Material` object
distinct from whatever `WetSurfaceRegistry.Register` originally
recorded -- meaning `SetWetness` was mutating a `Material` nothing on
screen actually used, a silent no-op with no exception and no compile
signal. Fixed by propagating the registration through both clone
layers (`WetSurfaceRegistry.TryGetParams`, checked and re-registered in
both `GetDoubleSidedVariant` and `ApplyWorldScaledTiling`). This fix
itself has NOT been through any Editor session -- it's pure reasoning
about the actual call chain, verified by reading every hop, not by
seeing it render. **This raises this item's own risk above the
"probably fine" level entry 25 could claim for item 0** -- the render
check below is now the one to actually pay attention to.

New `WeatherController` (a real `IsRaining` toggle + an eased 0..1
`Wetness` value, ticked once per frame from `LumenCycleController.
Update`) and `WetSurfaceRegistry` (same "record each material's base
value once at mint time, blend a live override onto every registered
SHARED Material" shape `NeonRegistry` already establishes for night
emissive boost, applied here to `_Smoothness`/albedo darkening
instead). `RoadDresser.Asphalt`/`Sidewalk`/`RoundaboutCurb`/
`IslandStone` each register themselves once, at first mint, via a new
one-time-cache-field wrapper around their existing `MTextured`/`M`
calls -- lane/cross paint, grass, and shrubs are deliberately NOT
registered (docs/40 scopes this to road/sidewalk/plaza surfaces only).
New `RainToggleHud` (a visible IMGUI button, same idiom as the
existing `WindowLightsHud`) is the one current trigger, inserted into
the top-left HUD chain between `WindowLightsHud` and `BuildMenuHud` --
`BuildMenuHud`/`BarracksHud`/`CollectorLabHud`'s own upstream-anchor
reads were updated to chain through it instead of skipping straight to
`WindowLightsHud.Bottom`.

- **The actual visual check**: click the new "🌧 Rain: ON" button (top-
  left HUD stack, just below the window-lights toggle) and confirm
  asphalt/sidewalk/roundabout-curb/plaza materials visibly darken and
  gain a specular sheen over roughly a 6-second ease, most obviously
  under an existing streetlamp/window light pool at night (docs/39 §2's
  own "wet cobble reflecting streetlamp pools" reference shot is
  exactly this effect) -- and that toggling back OFF eases back to the
  ordinary matte dry look over the same ~6 seconds, not an instant pop.
- **Dry-state regression check**: with rain OFF (the default), confirm
  every road/sidewalk/plaza surface looks EXACTLY as it did before this
  change -- `WetSurfaceRegistry` reads each material's own pre-existing
  smoothness/color as its "dry" baseline rather than assuming a fixed
  value, but that's a reasoned claim, not a rendered one yet.
- **HUD stacking**: with the rain toggle now a 5th panel in the top-left
  chain (`HudStatus` -> `WindowLightsHud` -> `RainToggleHud` ->
  `BuildMenuHud` -> `CollectorLabHud`), confirm nothing overlaps at any
  HudStatus state (traffic present, a unit selected, etc.) -- this
  chain has broken before (docs/28's own row-8-adjacent HUD history)
  purely from an anchor field going stale, not from bad math.
- **If this looks broken**: reverting is cheap and localized --
  `WeatherController.IsRaining` defaults `false` and nothing else in
  the game reads `Wetness`/calls into `WetSurfaceRegistry` except the
  four registered materials, so deleting `RainToggleHud`'s
  `AddComponent` call in `RuntimeCityBuilder` (and reverting the three
  chain-reference edits back to `WindowLightsHud.Bottom`) fully
  disables the feature with no other system depending on it.

## 27. Falling rain streaks + splashes (docs/40 §3 item 2)

**2026-09-16 update: real creator-reported exception, fixed.**
`Graphics.DrawMeshInstanced` threw `InvalidOperationException:
Material needs to enable instancing for use with DrawMeshInstanced`
every frame `RainSystem.Update` ran with `Wetness > 0` (i.e., rain was
genuinely toggled on and this code genuinely executed -- useful
confirmation in its own right, buried inside a real bug report). Root
cause: `BuildStreakMaterial`/`BuildSplashMaterial` never set
`mat.enableInstancing = true` -- `LowPolyFireSystem.MakeFireMaterial`
(this file's own cited precedent) DOES set it, immediately after
construction; the flag itself got missed when mirroring the technique,
not the `Graphics.DrawMeshInstanced` call shape. Fixed by adding it to
both material builders. This is exactly the kind of gap the project's
own risk calibration expects from mirroring a precedent read-through-
only (a missing one-line flag, not a structural misunderstanding) --
confirmed working now depends on the creator re-running it, not
re-read here.

**2026-09-16, second update: a full visual pass on creator direction
("rain streaks should be longer, motion blurred tip and tail. a rain
impact splash and ripples on surfaces, plus wet surface, darker and
shiny areas. Clumps of heavier mist floating slowly through
viewport").** All five asks landed:
- **Longer streaks**: length range roughly doubled (1.1-1.9 m ->
  2.6-4.2 m), count trimmed slightly (260 -> 220) to compensate.
- **Motion-blurred tip/tail**: the streak mesh gained real per-vertex
  UVs (V tracks local Y, seam-free across all 6 faces since every
  vertex has one unambiguous Y) paired with a new procedural alpha-
  gradient texture (`BuildStreakGradientTexture`) that fades to fully
  transparent at both ends and stays opaque through the middle -- the
  old geometry-only box read as a solid rod regardless of fall speed.
- **Impact splash + ripples**: the splash mesh changed from a
  flattened box to a flat ground quad (safe from the vertical-
  billboard edge-on risk since the camera always looks down at a fixed
  pitch), paired with a new radial-gradient texture
  (`BuildRippleTexture`) baking a bright impact core NEAR the center
  plus a thin ring farther out into ONE static texture -- as the
  instance's own world-space scale grows over its lifetime (eased, not
  linear, so the ripple loses energy realistically), the ring's fixed
  UV-space radius reads as an expanding ring in world space with zero
  per-instance texture work.
- **Wet surface, darker/shinier**: `RoadDresser`'s four
  `WetSurfaceRegistry.Register` calls all got stronger smoothness/
  darken values (e.g. Asphalt 0.85/0.55 -> 0.94/0.38) now that the
  clone-chain bug keeping them from ever reaching the screen is fixed
  (entry 26's own update) -- no point tuning a value nobody could see
  before. Also added scattered patchiness: `DressHex` now spawns a
  `PuddleDecal()` at roughly 1 in 6 ordinary (non-roundabout) hexes, at
  a hashed offset, so wetness reads as patchy "areas" rather than one
  flat city-wide tint.
- **Mist clumps**: new `MistSystem.cs` (docs/36 has no prior entry for
  this, tracked here since it's the same commit) -- a 12-clump pool of
  `ProceduralMeshKit.CloudShard` blobs (3 shape variants), individually
  faded via `MaterialPropertyBlock` (a small bounded set, exactly
  docs/39 §7's sanctioned exception), drifting slowly and recentering
  around the camera's ground focus when they wander too far, scaled by
  `Wetness` the same way rain itself is.

None of this pass has been through a real Editor session -- pure
reasoning against the mesh/UV/texture math, same standing ceiling as
every other item in this file.

New `RainSystem`, a pure visual consumer of item 1's
`WeatherController.Wetness` (no weather state of its own). GPU-
instanced via `Graphics.DrawMeshInstanced` on a hand-authored unit-box
mesh -- the SAME technique `LowPolyFireSystem` already ships, confirmed
by reading that file directly (an earlier draft of docs/40 named the
wrong overload, `RenderMeshInstanced`; corrected once this file was
written against the real precedent). 260 falling streaks scaled by
`Wetness`, respawning around a camera-ground-focus point (a ray-plane
intersection against y=0 from `Camera.main`, computed fresh each
frame); each landing spawns a short growing splash disc from a
separate 40-slot pool. Both pools skip entirely in the Map band
(`AnimationLodBudget.CurrentBand`) and cost nothing while `Wetness` is
0. The hand-authored box mesh's winding is unverified like every other
procedural mesh in this project written blind -- mitigated with the
same `_Cull = Off` double-sided safety net `PropLibrary`/
`RoofPortraitHologram` already carry after docs/28 rows 6/7's real
winding incident, so even a wrong triangle order should stay visible
rather than vanish.

- **The actual visual check**: toggle rain ON (the button added in
  entry 26, same panel) and confirm streaks are visibly longer than
  before and read as SOFT-EDGED (fading at both ends), not a solid
  rod -- `StreakLengthMin/Max` and `BuildStreakGradientTexture`'s own
  two `InverseLerp` bands are the tuning knobs if the fade is too
  sharp/soft or too short/long.
- **Splash/ripple read**: confirm a landing shows a bright core
  followed by a visibly EXPANDING RING (not a filled disc growing
  uniformly) -- if the ring doesn't read as separate from the core,
  `BuildRippleTexture`'s `core`/`ring` distance bands need retuning,
  not a structural fix.
- **Wet-area patchiness**: confirm scattered puddle patches appear on
  ordinary streets (not just at roundabouts) when rain is on, and that
  asphalt/sidewalk generally read visibly darker and shinier than the
  pre-this-pass look -- if the STREET puddles don't appear but the
  roundabout one does (or vice versa), that's informative: both use
  the identical `PuddleDecal()`/`WetSurfaceRegistry` path, so a
  difference between them would point at `DressHex`'s own hash gate
  specifically, not the shared wet-response system.
- **Mist read**: confirm 1-2 large, soft, slowly-drifting gray blobs
  are visible somewhere in view when rain is on, fading in smoothly
  (not popping) -- and that NONE are visible when rain is off.
  `MistSystem.cs`'s `MaxClumps`/`MinScale-MaxScale`/`BaseAlpha` range
  are the tuning knobs.
- **Camera-proximity depth cue** (2026-09-16 third update, creator
  direction "fall faster and be fast and plentiful close to the
  camera"): base fall speed raised (24-34 m/s, was 16-24), and streaks
  within `NearCameraRadius` (32 m) of the camera's own ground position
  -- `CameraGroundXZ`, deliberately NOT `GroundFocusPoint` (the point
  the camera is looking AT, which sits ahead of it, not under it) --
  get up to 1.7x that speed, recomputed live each frame. `NearSpawnBias`
  (60%) of every respawn also lands inside that same radius (sampled
  via `sqrt(random)` for even density, not peaked at the exact center),
  so the pool visibly concentrates near the camera instead of spreading
  uniformly. Confirm: rain in the foreground/near part of the view
  looks noticeably faster AND denser than rain farther out, and that
  panning the camera doesn't leave a stale dense patch behind (the
  bias recenters on the camera's CURRENT position every frame, not
  where it was when a streak last spawned).
- **Winding check specifically**: confirm the streak box and the
  splash quad don't look inside-out or show any missing face from a
  typical yaw angle -- `_Cull = Off` should make even a wrong winding
  fully visible, just possibly with backwards-looking normals/shading,
  which is a lesser bug than "invisible."
- **Map-band cull**: zoom out past the Map-band threshold (docs/39 §1.2,
  ≥250 m) with rain on and confirm streaks/splashes/mist all disappear
  rather than being tiny far-away dots -- this reuses `MonsterBody`'s
  own Map-band check but has never been seen triggering for either file.
- **If this looks broken**: `RainSystem`/`MistSystem` are both self-
  contained MonoBehaviours with no other system depending on them --
  deleting either `AddComponent` call in `RuntimeCityBuilder` fully
  disables that piece with zero ripple effects. The puddle-scatter
  addition in `RoadDresser.DressHex` is a single `if` block, equally
  safe to delete on its own.

## 28. Monster rim/fill light (docs/40 §3 item 3)

New rim/fresnel term in `CreatureVertexColor.shader` (the hand-authored
shader docs/39 §11 item 2 shipped, confirmed compiling by a real Editor
per that item's own docs/12/36 history) -- `_RimColor`/`_RimPower`/
`_RimIntensity` as three new per-material-group Properties (default
values apply automatically to every `LabMeshBuilder`-created material,
no C# change needed there), scaled at runtime by a new GLOBAL
`_MadDrNightAmount` set once per frame from `LumenCycleController.
ApplyBlend` via `Shader.SetGlobalFloat` -- zero per-material update
cost, every creature reacts identically and instantly to the existing
day/night curve with no new registration system. Rim is 0 in daylight
by construction (`_MadDrNightAmount` starts and stays 0 through Day)
and strengthens through Dusk/Night on the same curve `nightAmount`
already drives for lamps/windows/ambient elsewhere.

- **The actual visual check**: watch a monster from Dusk through Night
  and confirm a cool blue-white rim/edge light appears against its
  silhouette, strengthening as the sky darkens, and is genuinely absent
  in full Day -- `_RimIntensity`/`_RimPower` in the shader's Properties
  block are the tuning knobs if it reads too strong/weak/tight/wide.
- **Property-declaration correctness**: this is the first GLOBAL
  (non-per-material) HLSL uniform this project's hand-authored shaders
  have used -- confirm `Shader.SetGlobalFloat("_MadDrNightAmount", ...)`
  actually reaches the shader (expected to "just work" per Unity's
  standard global-uniform binding-by-name convention, but every other
  hand-authored shader gotcha in this project's history — WindowGrid's
  glazing, CreatureVertexColor's own flat-shading miss — was also
  "should just work" until seen rendered).
- **SRP-batching check**: confirm merged creature meshes are still
  batching correctly (docs/39 §11 item 4's own Frame Debugger check) --
  the new CBUFFER fields are per-material like the existing ones, and
  the global sits outside the CBUFFER entirely, so this SHOULD have no
  batching impact, but hasn't been confirmed against a real capture.
- **If this looks broken**: the three new properties and the rim
  computation in `CreatureVertexColorFragment` are additive and
  isolated -- deleting the `half3 rim = ...` line and the `+ rim` in
  the final `color` composition (plus optionally the global float call
  in `LumenCycleController`) fully reverts to the pre-item-3 shader
  with no effect on diffuse/specular/emission.

## 29. Plaza puddle decals + the two-layer clone-chain fix (docs/40 §3 item 4)

New `RoadDresser.PuddleDecal()`: a static, dark, warm-tinted,
transparent patch (not a live mirrored-skyline reflection -- that needs
a Render-Texture camera, Editor-only setup docs/28 row 19 already
flagged, so this is the "classic pre-SSR fake puddle" trick instead),
one per roundabout at a hash-deterministic position, faded in/out by
`WeatherController.Wetness` via the same `WetSurfaceRegistry` item 1
built (near-invisible dry, visible wet). Tinted once at creation with
`RoadDresser.LampColor`, the same color every roundabout lamp already
registers with `GlowPointRegistry` -- not a live per-frame query.

**This item is also where a real, non-obvious bug in item 1 was found
and fixed** -- see entry 26's own second update and docs/12 for the
full story: a two-layer material-cloning chain
(`PropLibrary.GetDoubleSidedVariant` then `RuntimeCityBuilder
.ApplyWorldScaledTiling`) silently disconnects any `WetSurfaceRegistry`
-registered material spawned as a Sphere/Cylinder (which is every
roundabout surface, puddle decal included) from the object
`SetWetness` actually mutates. Fixed by propagating the registration
through both clone layers. **Both items 1 and 4 share this exact fix
and this exact remaining risk** -- if the puddle doesn't fade in with
rain, check whether asphalt/sidewalk/curb wetness ALSO isn't showing
(same root cause), not just this decal in isolation.

- **The actual visual check**: at a roundabout, toggle rain on and
  confirm a dark, glossy puddle patch fades in on the circulating
  asphalt over the same ~6s transition item 1 uses, warm-tinted rather
  than a flat gray blob -- and fades back to invisible when rain turns
  off. Absence here most likely means the clone-chain fix above didn't
  fully close the loop somewhere, not that the puddle itself is wrong.
- **Position check**: confirm the patch actually sits ON the asphalt
  (not floating above it or sunk into it) and doesn't overlap the
  dashed lane markings or curb in a way that reads as a rendering
  glitch rather than a puddle -- `RndCurb`/`RndAsphalt`-derived radius
  and the 0.37 height are reasoned against the lane markings' own
  proven-clear 0.36, not measured.
- **The clone-chain fix itself**: with rain ON, confirm ALL of
  Asphalt/Sidewalk/RoundaboutCurb/IslandStone visually darken/gain
  sheen (item 1's own check) AND the puddle decal appears (this item's
  check) -- if item 1 alone works but the puddle doesn't (or vice
  versa), that would mean the two effects are reaching different
  materials somehow, worth a closer look at whether the puddle's own
  Cylinder spawn is hitting a genuinely different code path than
  expected.
- **If this looks broken**: `PuddleDecal()`'s own spawn call in
  `DrawRoundabout` is a single line -- deleting it removes the puddle
  entirely with no effect on anything else. The clone-chain fix itself
  (in `PropLibrary.cs`/`RuntimeCityBuilder.cs`) is additive and
  defensive (an `if (TryGetParams(...))` that does nothing when the
  material isn't wet-registered), so it's safe to leave in place even
  if the puddle decal itself is reverted.

## 30. Volumetric fog patches (docs/40 §3 item 5 follow-up)

Creator direction: "add the volumetric fog patches." **Not** the real
URP volumetric-fog Renderer Feature docs/28 row 19 evaluated and the
creator already chose not to integrate for performance -- that
decision is untouched, and a genuine ray-marched volumetric fog feature
still needs the same Editor-only Renderer-Feature-registration step
every other item-5 option does. New `VolumetricFogPatchSystem.cs` is
the cheap approximation instead: 8 stationary ground-fog patches, each
a 3-layer stack of soft `ProceduralMeshKit.CloudShard` blobs (wide and
relatively opaque near the ground at 0.3m, smaller and fainter at 1.4m
and 2.8m) -- the standard "billboard stack" trick for a fog silhouette
that reads as having real height, without any actual density field.
Intensity tracks `DayNightState.NightAmount` (thick at night, gone by
day) plus a smaller boost from `WeatherController.Wetness` (fog and
rain read as one weather system, not two unrelated ones). Patches
reposition (not respawn -- the same GameObjects move) around the
camera's ground focus when the camera wanders far enough away, and skip
entirely in the Map band, same pattern as `RainSystem`/`MistSystem`.

Deliberately NOT tied to actual low-lying terrain (rivers, dips) --
this environment has no simple terrain-height query available from a
standalone MonoBehaviour, so patches scatter across the visible area
generically. If the creator wants them anchored to real geography
later, that needs a query into `RuntimeCityBuilder`'s own terrain/water
data, a bigger follow-up, not attempted here.

- **The actual visual check**: at night (with or without rain),
  confirm 1-2 soft, layered fog patches are visible somewhere in view,
  reading as having real vertical extent (thicker near the ground,
  thinning with height) rather than a flat gray smear -- and that they
  fade toward nothing in full daylight. `LayerHeight`/`LayerRadiusScale`/
  `LayerBaseAlpha` in `VolumetricFogPatchSystem.cs` are the tuning
  knobs if the layering doesn't read as volume.
- **Rain interaction**: confirm turning rain on at night thickens the
  fog somewhat rather than replacing it or doing nothing -- the two
  contributions are meant to add, capped before summing.
- **Map-band cull**: zoom out past ≥250m and confirm patches disappear,
  same check as `RainSystem`/`MistSystem`'s own Map-band gate.
- **If this looks broken**: `VolumetricFogPatchSystem` is a single
  self-contained MonoBehaviour with no other system depending on it --
  deleting its `AddComponent` call in `RuntimeCityBuilder` fully
  disables it with zero ripple effects.

## 31. docs/28 §4 cleanup: per-kind light shaping + generic flicker hookup

Creator direction: pick up docs/28 §4's remaining follow-ups. Two of
the four turned out to be stale (office-tower windows and window-pane
granularity were already solved by the 2026-08 facade-grammar system --
corrected in docs/28 directly, no code changed). These two were real
and are now implemented:

**Per-kind real-light shaping**: `GlowPointRegistry.Register`/
`RegisterPosition` gained optional `range`/`coneAngle` parameters (-1
sentinel = fall back to `DynamicLightBudget`'s own shared `range`/
`spotConeAngle` fields, so every pre-existing call site -- overhanging
streetlight, ornate lamppost, roundabout bulb -- is byte-identical).
Three fixtures now use a real override: `BuildingWindowGrid`'s window
spill (4.5m, vs. the shared 8m streetlamp-tuned default),
`MonsterAgent`'s roof-glow display (5m), and `TrafficCar`'s headlight
(28° cone, vs. the streetlight's shared 48°).

**Generic flicker hookup**: `BaseDresser` gained `RollFactionWindowMat`/
`RegisterFactionWindowGlow`, collapsing three near-identical hand-
duplicated blocks (`SpawnPedestalWindow`/`SpawnArrowSlit`/
`SpawnAlienPorthole` each rolled the same 0.4 lit-chance, spawned the
same warm-glow material, and registered the same `LightBehaviorKind
.Window` color -- only the jitter salt constants (110/111, 120/121,
130/131) differed) into two shared calls, using the SAME salt
constants so the random stream is byte-identical to before.
`BuildingDresser` gained `RegisterBuzzingSign`/`RegisterChaserBulb`,
thin named wrappers replacing 6 direct `EmissiveAnimator.Register(...,
LightBehaviorKind.Buzz/Chase, ...)` calls at the landmark/movie-palace
sign spawn sites, with the exact same colors/seeds passed through
unchanged.

- **The actual visual check**: at night, confirm a lit window's glow
  pool reads distinctly smaller than a streetlamp's, a car's headlight
  reads as a narrower beam than the overhanging streetlight's wide
  cone, and every faction window shape (Doctor pedestal windows, castle
  arrow slits, Alien portholes) still shows the same ~2-in-5 lit
  pattern and warm color as before this refactor (a regression here
  would mean the salt-constant preservation missed something).
- **Landmark neon check**: confirm the Statue-of-Liberty-analogue torch,
  the iron-tower beacon, and the movie-palace's underglow/blade/letters/
  chaser-bulb row all still buzz/chase exactly as before -- this was a
  pure rename to named helpers, zero logic change, so any difference
  here would point at a mistake in the migration, not a design change.
- **If this looks broken**: every change here is either a pure rename
  (`RegisterBuzzingSign`/`RegisterChaserBulb`) or a byte-identical
  refactor with a documented preserved salt/threshold (the faction-
  window helpers) or a purely additive sentinel-gated parameter (the
  range/coneAngle overrides) -- reverting any one piece independently
  is safe and doesn't require touching the others.

## 32. Editor confirmation: entries 25-31 compile clean and survive a real play session

Found by reading `~/Library/Logs/Unity/Editor.log` directly (file mtime
2026-09-16 15:50, well after this session's tip commit `ae9d4f3` at
13:11) -- not asked about; same opportunistic check
`maddr-editor-verification-workflow` already established. This is a
NEW play session distinct from the earlier item-0/1 confirmation noted
in entry 25/26: `*** Tundra build success (2.62 seconds), 9 items
updated, 841 evaluated` followed by a real played match (23-24 citizens
eaten, several harvester bank events, clean domain reload) with ZERO
`LogError`/exception/shader-error lines across the entire 1086-line
log. Only non-project noise: offline `curl` failures resolving
`cdp.cloud.unity3d.com` and a roster-fetch timeout falling back to
cache -- both environment networking, unrelated to any shipped code.

This is the first real-Editor confirmation for entries 27-31 (rain
polish passes, monster rim light, puddle decals + the clone-chain fix,
volumetric fog patches, docs/28 §4 light-shaping/flicker cleanup) --
entries 25/26 already had their own separate item-0/1 confirmation
from an earlier session, but everything shipped after that (items 2-6
of docs/40, plus the docs/28 §4 follow-up) had never been through a
real Editor at all until now. **What this confirms:** the whole stack
through `ae9d4f3` compiles clean and runs a full match without
throwing -- including the clone-chain fix (entry 29) that a compile
check alone cannot validate the absence-of-exception for. **What this
does NOT confirm, same standing distinction as every prior entry:**
whether any of it actually LOOKS right -- rain density/streak fade,
puddle patchiness, fog layering, rim-light silhouette, window/headlight
cone sizing are all still open creator-eyes questions. No screenshot or
Frame Debugger capture exists for any of this yet.
