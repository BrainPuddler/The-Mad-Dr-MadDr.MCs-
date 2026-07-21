# 23 — RTS Master Build Plan: "StarCraft 2 meets RPG with B-Movie Monsters, 1950"

Status: **Execution plan** (written 2026-07 to be executed phase-by-phase by a
Claude Sonnet-class implementing agent) · Realizes the full-RTS expansion of
everything docs 01–22 designed · Pillars served: all four.

> **Who this document is for.** A future implementing agent (target: Claude
> Sonnet) working in this repo with no memory of this conversation. Every phase
> below is written to be executable in isolation: it names the files to touch,
> the invariants that must survive, the tests to write, and the acceptance
> criteria to meet before moving on. Read `CLAUDE.md` first, then this doc's
> §0, then execute phases in order. **Never skip the per-phase verification
> block.** When a phase's design collides with reality, append the deviation to
> the [docs/12](12-open-questions.md) decision log (via `cat >>`, never an
> editor insert) and keep going.

**The elevator pitch this plan builds:** *StarCraft 2 meets RPG, with b-movie
monsters, set in a 1950s that never was.* You are a Mad Doctor (or the Human
Army sent to stop you, or the Alien Hive that wants the planet). You have a
themed base. You harvest Blood, Fuel, and Ichor from a living city. You build
Blood Banks, Fuel Depots, Armouries, Collection Stations, and Factories. You
field armies with StarCraft-style waypoint control softened by flocking. Your
units level up, RPG-style, and wear the salvaged parts of their kills. Roaming
power-ups drift through the streets cycling their gifts. The day-night dial
turns the tide. Late game, two monsters fuse into something the world has no
name for yet. And it all happens in New York, Paris, or Montreal, rendered to
the standard of the Mafia games' loving 1950s noir. The Lab (the existing
browser Workshop, docs/06) is the between-match side game where the monsters
themselves are bred.

---

## 0. Ground rules for the implementing agent

These override anything else in this document when they conflict.

1. **Determinism is the constitution.** All simulation-affecting logic lives in
   engine-agnostic C# packages (`packages/citygen-core` pattern: zero
   `UnityEngine`, sfc32 RNG only, no wall-clock, no hash-order dependence,
   xunit tests in `Tests~/`). Unity scripts consume; they never decide. This is
   what makes §11's lockstep 4v4 possible at all — treat every new gameplay
   system as "will this hash identically on 8 machines?"
   **Float discipline (added per panel review):** match-core state is
   integer/fixed-point where practical; where floats are used, restrict
   simulation math to the IEEE-exact ops (+, −, ×, ÷, sqrt) and **forbid**
   `Math.Sin/Cos/Exp/Pow/Atan2` and any `Rng.NextGaussian` in tick paths
   (precompute into fixed-point tables instead). Hash floats **bitwise**
   (`BitConverter`), never via `ToString`/JSON. All neighbor/entity iteration
   is ordered by a deterministic **entity ID** (allocated by a sim-side
   monotonic counter), never by object reference or hash-set order. The
   Phase-11 hash test cannot catch IL2CPP/Mono FMA-contraction divergence from
   a headless CoreCLR harness alone — so Phase 11 **also** ships a
   recorded-replay hash harness that runs inside an actual dev player build
   (no Editor needed) and compares its tick-hash sequence against the CI
   golden.

**The single biggest correction from the panel review (read before Phase 1).**
The plan as first written assumed the unit simulation already lived in
engine-agnostic code. **It does not.** Today `MonsterAgent` (~950 lines),
`UnitCombat`, `Tank`, `Citizen`, and `TrafficCar` are all frame-driven
(`Update()`/`Time.deltaTime`) `MonoBehaviour`s holding float world positions —
they ARE the current game, and none of it is tick-driven or hashable. Porting
that logic into `match-core` as a deterministic fixed-tick sim (with the Unity
scripts demoted to pure interpolated *view* of sim state) is therefore a
first-class, load-bearing workstream, **not** a free consequence of §11. It is
folded explicitly into Phases 1–6 below (each phase ports the slice it needs)
and is the true critical path. Budget for it; do not let a phase-isolated
executor "reuse `MonsterAgent`'s Update loop" and quietly reintroduce
nondeterminism.
2. **The genome schema (docs/06) is normative and frozen** except through the
   golden-test ritual (`npm run test:update-golden`, documented as a versioned
   change in docs/12). RTS systems consume genomes; they do not extend them.
3. **Existing invariants hold**: origins never cross-breed (organic breeds,
   tech grafts only, biotech stays biotech — docs/17); energy follows origin
   (organic→Blood, tech→Fuel, biotech→Ichor); "nothing is wasted" (failed
   surgery returns the part; every corpse is salvage); buildings block pathing
   until Destroyed, then their hexes open (already true in
   `BattlefieldState.BlockedToGround` — **preserve it**, the creator direction
   "buildings are solid and can not be walked through unless they are
   destroyed" is already law).
4. **Verification without an Editor.** No Unity Editor exists in the build
   environment. Every phase must be verified by: (a) the package test suites,
   (b) the scratch "flightcheck" stub-compile harness pattern (see docs/12
   entries from 2026-07 for how it works), and (c) standalone numeric harnesses
   for any geometry/balance math. Never claim visual verification.
5. **v0.1 numbers.** Every tuning number in this doc is a placeholder to be
   balanced in playtests. Ship them, tag them, don't agonize.
6. **One phase per session-arc, commit and push each,** fast-forwarding `main`
   per the repo workflow. Update docs/12 (append-only) every phase.

### Existing assets the plan builds ON, not around

| Already built | Where | Reused by phase |
| --- | --- | --- |
| Deterministic city generator (roads/water/ridges/buildings/landmarks/bridges/roundabouts) | `packages/citygen-core` | 1, 8, 9 |
| Destructible-building runtime state + passability | `BattlefieldState` | 2, 6 |
| Hex A* pathfinding, arcs/facing | `HexPathfinder`, `Facing` | 5, 6 |
| Genome→stats ports (locomotion, weapons, harvest) | `packages/roster-client` | 4, 6 |
| Genome→mesh renderer | `packages/creature-mesh` | 10 |
| Unit control: selection, waypoint orders, formations, group facing | `WaypointCommander`, `MonsterAgent` | 5 |
| Onboard economy slice (gather/carry/bank), wallets | `MonsterAgent`, `RuntimeCityBuilder` (docs/22) | 3 |
| Fog of war + minimap | `FogOfWar.cs`, `Minimap.cs` | 5, 11 |
| Day/night presentation (press N) | `NightMode.cs` | 7 |
| Traffic, citizens, sidewalks, salvageable tank wrecks | `TrafficCar`, `Citizen`, `Tank` | 6, 9 |
| Mana design: Lumen Cycle moon dial, emitter polarities | docs/03 (design only) | 7 |
| Faction expression profiles (Human Army, Alien Hive) | docs/17 + `prototype/mutator/factions.py` | 6 |
| The Lab (breed/mutate/graft side game) | `site/` + `packages/genome-core` + `mutator-service` | unchanged — it IS the meta-game |

---

## 1. Match structure & the three (then four) factions

**Factions at match start — exactly three, per docs/17:**

| Faction | Fantasy | Origin bias | Resource | Base name (§2) |
| --- | --- | --- | --- | --- |
| **The Mad Doctor** (player default) | Stitched b-movie monsters | organic + grafts | **Blood** | *The Sanatorium* |
| **The Human Army** | 1950s military-industrial | tech | **Fuel** | *Fort Vigilance* |
| **The Alien Hive** | Saucer-people biotech | biotech | **Ichor** | *The Brood Nest* |

**The 4th category — Hybrids — is unlocked, not picked.** An advanced-stage
match mechanic (§6.4): once a player has salvaged and grafted parts from BOTH
enemy factions onto one creature line, the **Chimera Track** opens — hybrid
units that mix all three origins' parts (grafts only, honoring invariant #3)
and a hybrid super-unit created by an Archon-style **Fusion** of two leveled
monsters (§4.4). Hybrids are the endgame reward for playing the salvage game
hard, not a fourth lobby button.

**Victory conditions** (docs/02 unchanged): destroy every enemy HQ, or hold a
majority of emitters through a full Lumen Cycle. 1v1 through 4v4 (§11).

### Phase 1 tasks (foundation: match/faction state)
- New package `packages/match-core` (same csproj/Tests~ conventions as
  citygen-core, references citygen-core + roster-client): `MatchState`,
  `PlayerState` (faction, wallets, upgrades, chimera-track progress),
  `FactionDef` (data: names, resource, building list, color).
- Deterministic fixed-tick simulation loop skeleton (`Tick(int frame)`), all
  randomness from one seeded sfc32 stream per match. This is the object §11's
  lockstep will hash.
- Tests: same-seed same-inputs → identical serialized `MatchState` after N
  ticks; faction defs load; chimera unlock predicate.
- **Acceptance:** `dotnet test` green; a headless console harness runs 10,000
  ticks of an empty 8-player match deterministically (hash printed twice,
  identical).

---

## 2. Bases & the building roster

Every player starts with a **themed HQ** placed by the generator at a
faction-appropriate landmark site, plus a builder mechanism in faction flavor
(Doctor: Ghouls, docs/22; Army: Engineer Corps trucks; Hive: Larval tillers).

**HQ theming (names are canon, use them in code and UI):**
- Mad Doctor — **The Sanatorium**: a gothic hospital-manor grafted onto a 1950s
  city block; lightning rod spire, tesla coils, stained glass. (Reuses the
  `hospital` archetype massing as a base, new dressing pass.)
- Human Army — **Fort Vigilance**: sandbagged civic-fortress; radar dish, flag,
  motor pool, floodlights, chain-link.
- Alien Hive — **The Brood Nest**: a crashed saucer half-buried in a city
  block, chitin growth spreading over the pavement (emissive ichor veins).

**Common building roster** (per-faction skins; stats shared, names themed):

| Generic | Doctor skin | Army skin | Hive skin | Function |
| --- | --- | --- | --- | --- |
| HQ | The Sanatorium | Fort Vigilance | The Brood Nest | Builder production, salvage drop-off, defeat condition |
| Blood storage | **Blood Bank** | Plasma Reserve | Ichor Cistern | Raises Blood/Ichor cap (docs/22 storage) |
| Fuel **pump** (node-locked) | Crude Still | **Fuel Pump** | Methane Tap | Extracts Fuel — MUST sit on a fuel-node hex (§3); this is the income building |
| Fuel storage (anywhere) | Bile Boiler | **Fuel Depot** | Methane Sac | Raises Fuel cap only; placeable on any open hex |
| Parts storage | **Armoury** | Quartermaster's | Chitin Midden | Stores salvaged Parts (§6.3), enables grafting in-match |
| Harvest post | **Collection Station** | Requisition Post | Digestion Pool | docs/20 capture-radius passive harvest (already designed) |
| Factory | **The Stitchworks** | Assembly Line | Birthing Chamber | Queues unit production from roster genomes (docs/22 factory) |
| Defense | Tesla Fence | Pillbox | Spine Turret | Static defense (docs/04 stats) |

### Phase 2 tasks
- `match-core`: `BuildingDef` data table (costs in Blood/Fuel/Ichor/Bones,
  build time in ticks, HP/armor reusing docs/18 tier table, storage/production
  effects), placement validation (must be on open hexes, becomes blocked —
  through `BattlefieldState`, so new buildings are solid and destructible like
  generated ones **automatically**).
- Construction lifecycle: ghost → under-construction (scaffold %) → complete →
  Damaged → Destroyed (rubble hexes reopen; invariant #3 satisfied by reuse).
- Unity: `BaseDresser.cs` — HQ dressing per faction on the primitive-kit
  pipeline (BuildingDresser conventions); build-menu IMGUI panel (HudStatus
  conventions); ghost-placement cursor with red/green validity tint.
- Tests: placement legality (roads/water/occupied rejected), cost debit,
  storage caps raise/fall on build/destroy, determinism of build queues.
- **Acceptance:** headless harness builds each faction's full tech tree from a
  scripted command list, deterministically, twice, identical hashes; flightcheck
  compiles the Unity layer.

---

## 3. The economy: Blood, Fuel, Ichor + Bones/Parts/Brains

Docs/05 + docs/22 are the spec; this phase makes them real in `match-core`.

- **Blood** — from Citizens (Collection Stations, docs/20), harvester monsters
  (docs/22 slice already live in `MonsterAgent`), Blood Banks store it.
  Currency of organic units and healing.
- **Fuel** — pumped by Fuel Depots placed on **fuel-node hexes** the generator
  seeds (gas stations! — 1950s filling stations become the resource nodes; add
  `CityModel.FuelNodes`, placed on arterial-adjacent blocks). Currency of tech
  units, vehicles, and Army production.
- **Ichor** — distilled at Digestion Pools from biomass; Hive currency. The
  Doctor faction gets Ichor only via salvage-and-render (2:1 lossy, docs/17).
- **Bones/Parts/Brains** — construction and RPG-upgrade currencies (docs/05,
  §4, §6.3). Unchanged sources, new sinks.

### Phase 3 tasks
- Port the docs/22 economy contract into `match-core`: wallets per player,
  storage caps from buildings, onboard per-unit pools (capacity/spill already
  prototyped in `Harvest.cs` — promote to sim-side), income ticks from
  Collection Stations/Fuel Depots, upkeep drains per docs/05.
- Generator: `FuelNodes` in `CityModel` (deterministic, arterial-adjacent,
  count by preset; test like Roundabouts was tested).
- Unity: gas-station dressing on fuel nodes (pumps, canopy, rotating sign —
  RoadDresser prop conventions); wallet/cap HUD line.
- **Acceptance:** scripted 2-player economy duel in the headless harness
  reaches deterministic wallet states; income/upkeep curves logged to a table
  committed under `docs/23-balance/` for the balance pass.

---

## 4. RPG layer: XP, levels, gear, and Fusion

**Every unit is a character.** This is the "meets RPG" half of the pitch.

- **XP** for kills/assists/structures. **v0.1 numbers corrected per panel
  review:** the first draft priced kill-XP at the victim's Bones cost (20–60),
  which put Level 6 at 40–120 killing blows on a single unit — fusion would
  never trigger in a real match. Rescaled so a unit that survives a couple of
  engagements reaches the fusion gate: **kill = 40 XP flat + 4×victim level,
  assist = half, building = tier index × 30** (Small 30 … Landmark 120).
  Carried by `UnitRuntime` in match-core, not the genome — the genome is
  nature, the level is nurture.
- **Levels 1–10**, thresholds rescaled to match the corrected XP rates so
  Level 6 ≈ 8–12 concentrated kills, not 40+: **60, 150, 280, 460, 700, 1000,
  1400, 1900, 2500, 3300**. (Both XP and threshold numbers are v0.1 and get
  beaten into shape by the `docs/23-balance/` transcripts — but they now sit in
  the same order of magnitude as real per-match kill counts.) Per level: +8% MaxHP, +4% damage, +2% speed
  (multiplicative with genome stats, never replacing them), and at levels
  3/6/9 a **Trait choice** queued to the player (RPG build moment): e.g.
  Thick Hide / Adrenal Rush / Scavenger's Eye (extra salvage) — 9 traits v0.1,
  data-driven table.
- **Gear = grafted salvage.** In-match, the Armoury lets a leveled unit take a
  salvaged Part item (§6.3) as a temporary graft: a `rifle_arm` on your
  shambler, a `plasma_lance` on your brute. Match-scoped (the permanent version
  is the Lab's job between matches); costs Bones + the part; honors origin
  rules (grafts allowed cross-origin per docs/06 surgery).
- **Fusion (the "archon" mechanic).** Two level-6+ monsters of the same player
  merge over an 8s channel into ONE unit. **Stat derivation (specified per
  panel review — the first draft only named HP/level/plan and left the rest
  undefined):** HP = sum × 0.85 (the merge is lossy), level = max(a,b)+1,
  body plan of the higher-Bones-cost parent, Power/Ferocity = max of the two +
  10%, Speed = min of the two (the fused mass is slower), upkeep = sum (you pay
  for what you fused). **Explicit tradeoff:** fusion trades two board presences
  and two attack sources for one tougher, higher-value unit — strong when
  supply-capped (§ amendments: army size), a liability when spread map presence
  matters. **Render (corrected per repo-fidelity audit):** `GenomeDto` has
  exactly ONE hand slot — the earlier "four arms via slot mirroring" claim was
  false. The fused unit renders as the dominant parent's genome with the
  second's hand-part **added as an extra grafted prop** through the same
  attach-a-part path §4 gear uses (creature-mesh gains one explicit "secondary
  hand graft" attach point; a creature-mesh `Tests~` chunk-count assertion
  covers it), not by cloning a second full arm chain. If the player's Chimera
  Track (§1, and see amendment C) is open and the two units carry parts of all
  three origins between them, the fusion is a **Chimera** — the 4th-category
  unit, glowing tri-color (blood-red/fuel-amber/ichor-green cycling emissive).
  Deliberately irreversible and loud: a b-movie transformation scene with
  lightning.

### Phase 4 tasks
- `match-core`: `UnitRuntime` (xp/level/traits/gear), trait table, fusion
  rules; all deterministic, all tested (level math golden table).
- Unity: XP bar under health bars; trait-choice toast (IMGUI); fusion channel
  VFX (DamageFx conventions: lightning arcs = LineRenderer beams, smoke);
  four-armed render check in creature-mesh `Tests~` (chunk-count assertion).
- **Acceptance:** golden test — scripted match transcript where two units level
  to 6 and fuse; identical hashes across two runs; creature-mesh tests green
  with a four-hand-part rig.

---

## 5. Army control: waypoints + flocking, StarCraft grammar

Control grammar is **already** SC2-style (`WaypointCommander`: click-select,
box-select, double-click type-select, right-click orders, shift-queue,
formations, group arrival facing, minimap orders). This phase adds the feel.

- **Flocking (boids) layered on A\*.** Path solving stays `HexPathfinder` (the
  determinism + solid-buildings guarantee). Steering between path nodes gains
  three group forces for units sharing an order group: separation (exists —
  `ApplySeparation`), **alignment** (match average heading of groupmates within
  12 m), **cohesion** (gentle pull toward group centroid, capped so it never
  fights the path). Weights v0.1: sep 1.0 / align 0.35 / coh 0.15. Result:
  armies flow like flocks down the streets instead of marching as beads on
  strings — but never clip buildings, because steering output is clamped to
  never enter a hex in the blocked set (the existing solid-buildings law).
- Flocks respect the roads' width; big groups naturally split around blocks
  and re-merge (A* per-unit already staggers via formation slots).
- Attack-move (`A` + click) and patrol added to close the SC2 verb set.

### Phase 5 tasks
- `match-core`: `Flocking.cs` (pure math, unit positions in, steering out —
  testable numerically: alignment converges heading variance, cohesion bounded,
  separation min-distance holds, blocked-hex clamp never violated across 10k
  random steps).
- Unity: wire into `MonsterAgent.FollowPath`; attack-move + patrol orders in
  `WaypointCommander` (+ HUD hint line).
- **Acceptance:** numeric harness proves the four flocking properties; existing
  84+ creature-mesh / 56 roster-client / 145 citygen tests untouched.

---

## 6. Full three-faction combat, salvage, discovery

- **Army & Hive become playable/simulated armies**, not test dummies: unit
  rosters generated from docs/17 expression profiles (the faction genomes
  already exist as design; roster-client already parses genomes — Army:
  riflemen squads, the existing `Tank`, half-tracks, a zeppelin gunship; Hive:
  drones, spitters, a floater queen). AI opponents for skirmish use a
  utility-driven commander in `match-core` (build order scripts + threat maps)
  so 1-player matches work before netcode.
- **All enemies' parts are salvageable** (creator law, and docs/17/20 already
  promise it): every unit death drops its genome's expressed parts on the
  death hex as Part items (15s despawn, lootable by any Ghoul/builder —
  docs/04). Tank wrecks (already visual) become lootable Fuel+Steel caches.
- **In-game discovery**: first salvage of a part family the player has never
  owned fires a **Discovery** — a codex toast + permanent +5% stat affinity
  with that family this match + the family unlocks for Lab breeding after the
  match (genome fragments, docs/04 — the RTS now FEEDS the side game).
- **Roaming cycling power-ups** ("like archon", the moving kind): 2–4
  **Loose Experiments** wander the neutral streets per match — escaped
  glowing anomalies that drift along sidewalks (Citizen movement reuse),
  cycling their aura every 20s through Damage ↔ Speed ↔ Regen ↔ XP-gain.
  Killing-blow player captures it: the buff attaches to the killing unit for
  90s, then the anomaly respawns at a random roundabout. Contested, mobile,
  timed — the SC2 xel'naga-tower/DotA-rune hybrid this game's streets deserve.

### Phase 6 tasks
- `match-core`: docs/04 damage formula implementation (finally — it has never
  been implemented), arcs from `Facing`, bounded-luck rolls from the match
  stream; faction rosters as genome data files; salvage drops + loot;
  discovery table; anomaly entities + aura cycling; skirmish commander AI.
- Unity: Army/Hive unit spawning through the same `MonsterAgent`/`MonsterBody`
  pipeline (genomes in = meshes out — no new renderer needed); loot sparkle;
  discovery toast; anomaly visual (pulsing sphere + cycling tint).
- **Acceptance:** headless 3-faction AI-vs-AI-vs-AI skirmish runs 50k ticks
  deterministically; damage-formula golden tests match docs/04's worked
  examples exactly; salvage conservation test (parts dropped == parts
  expressed).

---

## 7. Time of day: the Lumen Cycle made real

Docs/03's moon dial stops being lore. A match-long **day → dusk → night →
dawn** cycle (v0.1: 8 real minutes per full cycle, public to all players —
"the moon dial is public information by design").

| Window | Doctor | Army | Hive | World |
| --- | --- | --- | --- | --- |
| Day | −10% regen | **+15% weapon damage** | −10% speed | Citizens dense, traffic full |
| Dusk/Dawn | — | — | **+15% Ichor income** | Long shadows (graphics showcase) |
| Night | **+15% HP regen, +10% speed** | −15% vision radius | — | Neon on (NightMode reuse), citizens sparse |

Monsters are stronger at night, the Army under the sun, the Hive at the
in-between — b-movie logic, legible strategy (time your push with your hour).
Emitter polarity flips (docs/03) sync to this same clock.

### Phase 7 tasks
- `match-core`: `LumenClock` (tick-driven, deterministic), faction modifier
  table, emitter polarity hook.
- Unity: drive `NightMode` from the clock (replacing the N-key toggle with a
  continuous blend + keep N as a dev override); sun angle/shadow animation;
  moon-dial HUD widget (the docs/03 dial, finally drawn — IMGUI circle).
- **Acceptance:** modifier golden table test; a scripted duel where the same
  army wins at night and loses at noon (balance smoke test committed as a
  transcript).

---

## 8. Regions: New York, Paris, Montreal (1950s)

Three flagship map regions as **CityPreset + dressing kit + palette** triples
(the docs/18 "one generator, authored style presets" economy — no bespoke
generators). Each keys off systems that already exist:

- **New York, 1950** — *the Big City preset, personified.* Dense Manhattan
  grid (existing Grid pattern, pitch 7), brownstone walk-ups + setback
  skyscrapers (new massing tops on BuildingDresser), Checker-cab yellow in
  the traffic palette, elevated-rail segments on the railyard system, Times
  Square neon district (NightMode emissive density ×3 zone), fire escapes on
  Medium tier. Landmarks: Liberty Statuette plaza, Grand Terminal (rail
  depot skin).
- **Paris, 1950** — *the roundabout showcase.* MainStreet pattern variant with
  **radial boulevard overlay** (new `RoadPattern.Boulevard`: the cardinal grid
  plus 2 diagonal avenues meeting at a grand roundabout — l'Étoile — reusing
  the European roundabout renderer built 2026-07 at 2× radius), Haussmann
  blocks (uniform 6-story massing, mansard roof caps, cream limestone
  palette), Métro entrances as street props, plane trees, café awnings.
  Landmarks: Iron Tower (landmark tier, 40 m — the skyline read), Sacré-Cœur
  hill (ridge-cluster + cathedral skin).
- **Montreal, 1950** — *the character piece.* Mid-density MainStreet, duplex
  rows with **external spiral staircases** (signature prop — one bent-tube
  primitive kit item), bilingual shop signage strings, Mount Royal as the
  big ridge cluster crowned with the illuminated cross (emissive), Old Port
  crane row along the river (which the generator already carves), winter
  variant palette optional. Landmarks: Marché tower with clock, Forum arena.

### Phase 8 tasks
- citygen-core: `CityPreset.NewYork()/Paris()/Montreal()` (sizes, patterns,
  Boulevard pattern + tests proving the diagonal avenues are straight lines in
  world space and the étoile roundabout sits at their crossing, off the
  arterial rule preserved), region field on `CityModel`.
- Unity: per-region dressing branches (BuildingDresser/RoadDresser style
  switches keyed on region), palettes, the three signature props (fire
  escape / Métro entrance / spiral staircase), region picker in
  `RuntimeCityBuilder` Inspector + CityGizmo.
- **Acceptance:** all three presets generate deterministically with tests
  (ASCII-dump visual check committed to docs/23-balance/ as text art);
  flightcheck green; existing presets untouched (their tests still pass).

---

## 9. Solidity, boundaries, destruction (audit phase)

Mostly built; this phase is a hardening audit against the creator's law:
*"Must adhere to the physical boundaries of the playfield, buildings are solid
and cannot be walked through unless they are destroyed."*

- Audit every mover (MonsterAgent walk/fly, Tank steer, TrafficCar, Citizen,
  flocking output, minimap-ordered moves, fusion channel drift, anomaly
  wander) against: blocked-set respect, map-bounds respect (`City.Contains`),
  and no-tunneling at speed (step length vs hex size at max speed).
- Write the **containment property test**: a headless fuzz harness drives 200
  units with random orders for 100k ticks and asserts zero frames where any
  unit's hex is in the blocked set or off-map. Fix every violation found.
- Destroyed buildings must reopen exactly their footprint (existing rubble
  behavior) and new §2 player buildings must close/open identically.

**Acceptance:** the fuzz test runs clean and joins the permanent suite.

---

## 10. Graphics: to AAA, Mafia-school

Target look: the Mafia series' 1950s Americana — warm sodium nights, wet
asphalt reflections, period chrome, film grain, unhurried noir palette. The
pipeline stays URP (docs/10 decision) — AAA here means art direction +
modern URP features, not an engine swap.

Sequenced sub-phases (each independently shippable):
1. **Post stack**: URP Volume — filmic tonemapping, per-region color-grading
   LUTs (NY steel-blue noir / Paris warm cream / Montreal cold pastel), film
   grain, vignette, bloom tuned for neon, depth of field for the Lab podium.
2. **Lighting**: real sun animation from the Lumen clock (§7), street lamps as
   actual pixel lights on a budget (nearest-N to camera), light cookies for
   window spill, SSAO.
3. **Materials**: replace flat colors with a small PBR atlas set (brick,
   limestone, asphalt-wet, chrome, painted metal, glass) applied through the
   existing dresser material cache — dressers keep their geometry logic,
   gain material richness. Wet-street shader (roads darken + reflect at night
   / after §7 dusk).
4. **Meshes**: primitive-kit → authored-mesh swap points. Keep the
   deterministic dresser *placement* logic; swap `CreatePrimitive` calls for
   a `PropLibrary` lookup (mesh assets by key, with primitive fallback so the
   game never breaks without assets). Assets themselves are creator-side
   (Editor/DCC work); the code side ships the library + fallback.
5. **Creatures**: creature-mesh gains normal-mapped skin material params
   (gloss/emissive already flow through), vertex-blend seams at sockets,
   corpse-part gore caps for salvage scenes.
6. **FX**: particle upgrade for muzzle smoke/fusion lightning/ichor splats
   (still DamageFx-orchestrated; VFX Graph optional-later).

**Acceptance per sub-phase:** flightcheck compile + a written
`docs/23-balance/graphics-N-notes.md` of what shipped vs deferred; no
determinism regression (visuals must never feed back into sim state — enforce
by keeping all of this in Unity-layer scripts only).

---

## 11. Server side: 4v4 multiplayer on open source

Docs/09 chose server-authoritative for the *service*; for match netcode at
4v4 scale with hundreds of units, this plan commits to **deterministic
lockstep** — the classic RTS answer (AoE2, SC1, Factorio) — because §0's
constitution already paid its price: the whole sim is a pure function of
(seed, command stream).

**Architecture:**
- **Sim**: `match-core` headless, fixed 10 ticks/s sim rate (interpolated
  rendering), commands scheduled 2 ticks ahead (200 ms input delay, standard
  lockstep feel; adaptive later).
- **Relay server**: a thin open-source .NET relay (no game logic — receives
  each player's command packet per tick, rebroadcasts the merged tick bundle;
  ~200 lines). Runs anywhere Kestrel runs; the mutator-service zero-deps
  philosophy applied to UDP/WebSocket transport. Ship it in
  `packages/match-relay` with its own tests + a `docker run` one-liner.
- **Desync defense**: per-tick FNV-1a hash of `MatchState` every 50 ticks,
  compared at the relay; on mismatch, dump divergent state JSON for diffing
  (the determinism tests exist to make this never fire).
- **Reconnect**: relay retains the full command log; a rejoining client
  re-simulates from tick 0 at max speed (Factorio's model — feasible because
  the sim is headless-fast; target: 50k ticks < 30 s, measured in Phase 1).
- **Matchmaking/lobby**: reuse mutator-service's HTTP conventions — a lobby
  endpoint on the relay (create/join/list, room codes), account IDs from the
  existing Lab accounts. 8 slots = 4v4; teams share vision (fog union) and an
  optional shared-control toggle.
- **Open-source documentation deliverable** (explicitly requested):
  `docs/24-netcode.md` — protocol spec (packet formats, tick pipeline, hash
  protocol, reconnect flow) written so a third party could implement a
  compatible relay; plus an annotated reading list of the open-source
  precedents the design cribs from (ENet, LiteNetLib, Mirror/FishNet docs,
  Factorio & AoE2 lockstep postmortems, the 1500-archers paper).

**Transport choice:** LiteNetLib (MIT, pure C#, reliable-UDP) as the default;
WebSocket fallback for restrictive networks. Both behind one `ITransport`.

### Phase 11 tasks
- `packages/match-relay` (+tests: relay echo, tick bundling, late-join log
  replay, hash-mismatch detection with an intentionally-poisoned client).
- Client: `NetDriver` Unity script bridging WaypointCommander orders →
  command packets → match-core ticks; interpolation layer for render.
- `docs/24-netcode.md` per above.
- **Acceptance:** 8 headless bot clients + relay complete a scripted 4v4 to
  HQ destruction with zero desyncs in CI (a dotnet test that actually spins
  up the relay in-process); kill-one-client reconnect test passes.

---

## 12. Phase order, dependencies, and the definition of done

```
1 match-core skeleton ──► 2 bases/buildings ──► 3 economy ──► 4 RPG/fusion
                                   │                              │
                                   ▼                              ▼
                            5 flocking/control ──────► 6 three-faction combat
                                                              │
                     7 Lumen clock ◄──────────────────────────┤
                     8 regions (parallel-safe after 1)        │
                     9 solidity audit (after 5+6)             ▼
                    10 graphics (anytime after 2, sub-phased) 11 netcode (last)
```

Each phase: implement → test → flightcheck → docs/12 entry → commit → push →
fast-forward main. **Definition of done for the whole plan:** the Phase 11
acceptance test (4v4 bots, zero desync) passes in CI, all package suites
green, and a human has played one full match per region in the Editor and
signed off in docs/12.

## v0.1 tuning appendix (consolidated placeholders)

Lumen cycle 8 min · lockstep 10 tps, 2-tick delay · XP table §4 · flocking
weights 1.0/0.35/0.15 · anomaly cycle 20 s, buff 90 s, respawn at roundabouts ·
fusion: level 6+, 8 s channel · discovery affinity +5% · fuel nodes: 2
(Village-class) to 8 (NY) per map · trait picks at 3/6/9 · relay hash every 50
ticks. All to be beaten into shape by the docs/23-balance/ transcripts.

---

## 13. Panel review — accepted amendments (2026-07)

A four-expert panel (RTS systems design, deterministic-lockstep netcode, Unity
tech-art, repo-fidelity audit) evaluated this plan before any execution. Their
accepted findings are binding amendments to the sections above. Inline
corrections already applied: §0 (float discipline + the sim-porting reality),
§4 (XP rescale + fusion stat rule + four-arm render correction), §2 (Fuel
Pump vs Fuel Depot split). The rest, by ID:

**A. New Phase 1.5 — "Port the live sim into `match-core`" (critical path).**
The current game (`MonsterAgent`/`UnitCombat`/`Tank`/`Citizen`/`TrafficCar`) is
frame-driven float MonoBehaviours; nothing is tick-driven or hashable. Insert a
phase between 1 and 2 that ports the *movement + order state machine* of one
unit type into `match-core` as a fixed-tick (10 tps) deterministic entity, with
the Unity `MonsterAgent` rewritten to **render interpolated sim state only** (no
gameplay decisions in `Update()`). Every later phase ports the slice it adds the
same way. Acceptance: a headless harness ticks 100 units through scripted orders
twice → identical `MatchState` hash; the Unity layer renders it via
interpolation with no `Time.deltaTime`-driven gameplay left in `MonsterAgent`.
This is the plan's true long pole — treat Phases 2–6 as "design system + port it
into the tick sim," never "add to the Unity Update loop."

**B. New Phase 3.5 (or fold into 3) — Emitters + the Lumen mana currency.**
Nothing in the first draft implemented docs/03's emitter capture (8 s
stand-and-hold, contested-pause), ownership, the Dominion timer, affinity
auras, or the mana pool/surge costs — yet §1's second victory condition and one
of Phase 6's docs/04 golden examples (`emitterMod 1.25`) depend on them. Build
emitter runtime + mana as its own economy sibling in `match-core` before Phase
6 needs it. Without this the anti-turtle victory path ships as dead code.

**C. Phase reorder — combat resolution moves EARLY.** The docs/04 damage
formula, target acquisition, and the death→salvage event are prerequisites of
XP (§4), attack-move (§5), and everything in §6. Move the **core combat loop**
(damage formula + arcs + death/salvage event) out of Phase 6 into Phase 4 (or a
Phase 3.75), leaving Phase 6 to consume it. Corrected dependency spine:
`1 → 1.5 (port) → 2 → 3 → 3.5 (emitters/mana) → 4 (RPG **+ combat core**) →
5 (control/flocking) → 6 (rosters/salvage/AI) → 7 → 8 → 9 → 10 → 11`.

**D. Split Phase 6 (scope bomb).** Phase 6 bundled six phase-sized systems.
Split into **6a** damage-model consumers + all-parts salvage + Discovery + the
roaming anomalies; **6b** the two enemy faction rosters as genome data; **6c**
the utility-driven skirmish commander AI (build orders + threat maps) — the AI
alone is a full phase, and a weak AI invalidates Phase 7's balance smoke test.

**E. Supply / population cap + army-size TARGET (the missing load-bearing
number).** Adopt an explicit supply model: each unit costs supply by Bones
tier (1–4); **cap 60 supply/player** (raised by HQ + a supply building), giving
~20–40 units/player and reconciling §11's "hundreds of units" as *8 × ~35 at
4v4*, not hundreds each. docs/05's upkeep numbers were tuned for ~15-unit
armies — Phase 3 must re-tune income/upkeep against the 20–40 target and commit
the curves to `docs/23-balance/`. This number drives economy curves, the
lockstep perf budget, flocking readability, and fusion's supply-efficiency
rationale — pick it in Phase 1, honor it everywhere.

**F. Chimera unlock predicate (unreachable in 1v1).** "Grafted parts from BOTH
enemy factions" can't happen in a 1v1 vs one faction. Ruling: the Chimera Track
opens when a player has salvaged **parts of all three ORIGINS** (organic +
tech + biotech), sourced from any combatant OR neutral drop — and to make that
reachable in a mono-faction match, tank wrecks drop **tech** Parts (not just
Fuel+Steel) and roaming anomalies drop a random off-origin Part on capture.
Phase 1's unlock-predicate test encodes "three origins present," not "two enemy
factions."

**G. SC2 verb set is incomplete without control groups + rally points.** Grep
confirms neither exists. Add **control groups (Ctrl+1–9 assign / 1–9 recall)**
to Phase 5 and **rally points on HQ/Factory** to Phase 2's production tasks —
without rally points every produced unit needs manual collection, unplayable
under Phase 6 AI pressure. Only with these does §5's "closes the SC2 verb set"
claim hold.

**H. UI: schedule a uGUI migration milestone; stop pretending IMGUI scales.**
IMGUI already forced the `Minimap.PointerOver` hack (OnGUI clicks are invisible
to the New Input System). The build menu, trait toasts, and moon dial multiply
that pain. Add a **Phase 2.5 uGUI (Canvas) migration** that ports HUD + minimap
+ build menu onto uGUI with proper event consumption, and build all new
interactive UI there. Keep IMGUI only for dev/debug overlays.

**I. Graphics: `flightcheck` cannot verify visuals — say so, and add a real
gate.** The repo's own history (silent magenta buildings, zero console errors)
proves a stub-compile catches nothing visual. Each §10 sub-phase's acceptance
is amended to require a **checked-in screenshot from a dev player build** (the
creator runs it; the agent cannot) plus the notes file — never "flightcheck
compiles" as the visual gate. Also: (i) PC_Renderer is **Forward+**
(`m_RenderingMode: 2`) so §10.2's "nearest-N pixel lights, 8-light limit"
framing is wrong — there is no per-object light cap; rewrite around Forward+
clustered lights with a shadow-caster budget instead. (ii) URP has **no
built-in SSR** — §10.3 "wet asphalt reflections" means smoothness + darkened
albedo + reflection-probe cubemaps at night, not screen-space reflections; say
so. (iii) **NightMode owns `RenderSettings` today and §10.1's Volume stack
wants the same knobs** — declare ONE owner (fold NightMode's ambient/fog into
the Lumen-clock-driven Volume in §7/§10.1); they must not both write ambient.
(iv) §10.2/10.3 depend on Phase 7's `LumenClock` — fix the "graphics anytime
after 2" line in §12 to "10.1 after 2; 10.2/10.3 after 7."

**J. Netcode specifics.** (i) Add to Phase 1: deterministic entity-ID
allocation + a canonical command/state serialization spec (fixed field order,
bitwise-hashed floats, never JSON/ToString) — orders target entity IDs, never
object references. (ii) Move the reconnect resim-speed budget measurement from
Phase 1 (empty match, meaningless) to Phase 6a's real workload. (iii) Resolve
the relay contradiction: "runs anywhere Kestrel runs" (HTTP) vs LiteNetLib/UDP
transport — the **lobby/matchmaking** is HTTP-on-Kestrel (reuses
mutator-service conventions); the **per-tick command relay** is a separate
LiteNetLib/UDP (or WebSocket-fallback) listener behind `ITransport`. Two
listeners, one process; state it so the executor doesn't try to serve UDP from
Kestrel.

**K. Repo-fidelity corrections.** (i) §2's "new buildings are solid &
destructible *automatically*" overstates it: `BattlefieldState` is immutable
with only `FreshFrom` + `WithBuildingDamage/WithBridgeDamage` and no
add-building API — Phase 2 must extend citygen-core with a runtime
add/place-building path (and its determinism tests) first; the passability
*rule* is reused, the *insertion* is new work. (ii) §3's "promote `Harvest.cs`
to sim-side / damage spill" mis-cites: `roster-client/Harvest.cs` is already
engine-agnostic stateless profile math and contains **no** spill concept
("spill" lives in the docs/22 *design*, not code) — Phase 3 implements the
onboard-pool spill fresh in `match-core`, using `Harvest.cs` only as the
gather/capacity profile source. (iii) §6's "Army rosters through the same
genome→mesh pipeline" is only true for the biological/infantry units; `Tank.cs`
is a bespoke primitive vehicle with its own steering (not a `MonsterAgent`), so
the half-track/zeppelin/tank line needs a **vehicle path** ported into
match-core separately, not "genomes in = meshes out." (iv) roster-client is
documented as the *display-side twin* of genome-core kept honest by golden
tests — promoting it to sim source-of-truth is fine but Phase 1 must add a note
that its golden fixtures now gate sim determinism, not just display.

**L. Time-of-day specifics (§7).** Window durations for the 8-min cycle follow
docs/03's 90/30/90/30 shape: **Day 3:00 / Dusk 1:00 / Night 3:00 / Dawn 1:00**.
Reconcile the Dominion timer — docs/02 defines it as one 4-min cycle; this plan
must either state the 8-min cycle as a deliberate v0.1 change (with a docs/12
entry) or halve to 4 min. Add the **citizen-density → Blood-income multiplier**
as an explicit line in the Phase 7 modifier table (night sparse-citizens can
swing Collection-Station income more than any ±15% combat modifier — an
untracked whipsaw where the Doctor's army peaks at night exactly as its economy
starves; make it visible so the golden test and balance tables capture it), and
give the Hive a combat-relevant window bonus or mark the economic-only
asymmetry intended.

**Panel verdicts:** RTS-design *needs_rework* (→ addressed by A/B/C/D/E/F/G/L),
netcode *needs_rework* (→ §0 float clause + A/J), tech-art *sound_with_edits*
(→ H/I), repo-fidelity *sound_with_edits* (→ K). With these amendments the plan
is cleared for phased execution starting at Phase 1.
