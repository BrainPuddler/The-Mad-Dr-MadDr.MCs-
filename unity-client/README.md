# unity-client

The Unity project for the City Battlefields track ([docs/18](../docs/18-city-battlefields.md)).

**Status: created** — Unity **6000.3.13f1** (Unity 6.3), **3D (URP)**
template, with Unity's default Mobile/PC render-pipeline asset split
(matches the mobile-first perf posture of
[docs/08](../docs/08-creature-visualization.md)/[09](../docs/09-multiplayer-architecture.md)).
Contents are still the stock template (SampleScene, TutorialInfo) plus:

- **`Assets/Scripts/HexGridGizmo.cs`** / **`CityGizmo.cs`** — Scene-view-only
  smoke tests (gizmos, not real GameObjects): the first proves the
  `citygen-core` package reference works at all, the second draws a full
  generated city (roads/buildings/water/ridges/bridges) in the Scene view
  while the Editor isn't playing.
- **`Assets/Scripts/RuntimeCityBuilder.cs`** — the playable battlefield hub:
  drop it on an empty GameObject, hit **Play**. Builds the city as real
  geometry, fetches your Menagerie, spawns commanded monsters and
  Citizens, wires the camera/orders/HUD, and owns live building damage
  (rubble opens paths) plus the session harvest wallet.
- **`Assets/Scripts/MonsterAgent.cs`** / **`MonsterBody.cs`** — one commanded
  monster. The agent does A* waypoint navigation over the hex grid
  (around buildings — unless the order IS the building), target locking
  (attack a building until Destroyed; chase and eat a Citizen), with
  speeds from the creature's own physiology (`roster-client`'s tested
  `Locomotion` port). **All nine body plans regenerate the Lab
  website's actual body from DNA** via `packages/creature-mesh` +
  `LabMeshBuilder.cs` — torso lathes, brass belts, franken faces,
  brain-tier heads (mastermind's brain under glass), bat wings, cobra
  hoods, see-through blob organs, and every hand/sensor/eye part
  family, same geometry the site renders. Legged plans keep
  **distance-driven stepping: planted feet are world-locked and never
  slide** — a leg only swings when the body's real displacement pulls
  its hip far enough from the planted foot, and body bob is phased by
  distance traveled, not a clock — with the rig's legs dressed in the
  family's real geometry (`LegKit`: hoofs, talon fans, chitin struts,
  piston struts, brass hip joints) instead of stick cylinders.
  **Winged units can walk or fly**, decided per order — "far" (a
  straight-line hex-distance threshold) or "high up" (no ground route
  at all, or a heavy detour around buildings/water) tips it into flight
  (`MonsterAgent.DecideFlight`). Flying runs the SAME A* over the SAME
  hex grid, never a straight-line ignore-everything hop — but at TWO
  cruise tiers: Low clears short buildings (small/medium) and weaves
  around anything taller, High climbs above every tier and flies the
  direct line. Which tier a leg actually flies is an energy call
  (`MonsterAgent.DecideFlightTier`) comparing hex-distance-flown against
  the one-time cost of the extra climb — a short detour beats climbing
  over a building, a long one loses to it. `MonsterBody.SetFlying`
  smoothly lifts torso, wings, AND every leg's hip hardware together to
  whichever altitude was picked (missing the hip hardware was the
  literal "feet still stuck to the ground" bug) and tucks the legs
  mid-air; a unit that flew to its target stays airborne to fight (an
  aerial attack) and only lands once its order is fully done. Turns are
  arcs, not snaps, while airborne — a wider "close enough, aim at the
  next leg" radius rounds off hex-grid corners, a slower heading
  catch-up sweeps through them, and velocity follows the NOSE while the
  nose chases the target (carving, not strafing), so a fresh order in
  any new direction — even straight behind — transitions through a
  smooth banked arc. **Winged units can land on buildings**: right-click
  a building's ROOF (its flat top face) and winged units fly over and
  perch on it — the roof becomes their standing surface, feet planted
  on the roof plane, wings folded; a wall click is still an attack
  order for everyone. Perched units hold their roost (no auto-engage)
  until ordered; if the building collapses under them they ease down
  onto the rubble. **Wings actually flap** — bat-style, fast
  powerful beats while still climbing or descending, a slower cruise
  beat once holding altitude, folded to rest once grounded — since
  `packages/creature-mesh` returns each wing as its own root-relative
  socket (not baked into the static body mesh, same treatment as legs)
  that `MonsterBody.BuildWings`/`UpdateWingFlap` mount and hinge live.
- **`Assets/Scripts/Citizen.cs`** — docs/19 client-side cosmetic crowd:
  wanders the streets, flees monsters, edible (docs/20 yields: Blood 2 /
  Bones 1 / Brains 1 into the session wallet).
- **The miniature-set world** (`TerrainField.cs` / `BuildingDresser.cs` /
  `RoadDresser.cs`, docs/21) — the battlefield renders as a 1950s
  monster-movie miniature. Deterministic sculpted terrain (the
  generator's own ridge hexes become rolling high-ground mounds, rivers
  and ponds get carved beds with visible banks and sunken translucent
  water, open ground rolls gently) with the **flat-lock rule**: ground
  under every building plot, road, and bridge stays exactly y=0, so
  roof heights, flight tiers, perch/descent math, and rubble never
  drift. Units terrain-follow (`GroundHeightAt`), and `MonsterBody`
  plants each foot on the slope under it via a ground sampler.
  Buildings get era dressing parented into the damage pipeline
  (crushes/tints with the massing): suburban gables, gas-station
  canopies, diner chrome, brick walk-ups with fire escapes, stepped
  deco offices, archetype-aware landmarks (spired churches, columned
  town halls, a marquee'd movie palace), and rooftop water towers /
  antennas / vents. Roads are hub-and-spoke tiles (corners/T's/
  crossroads emerge from hex adjacency, seamlessly) with sidewalks,
  lane dashes, crosswalks, streetlights, telephone poles, hydrants,
  and pastel tail-finned parked cars — all colliderless, all hashed
  from the city seed (same seed = same city, dressed identically).
  Grid/MainStreet's "vertical" streets (SmallTown, BigCity) render dead
  straight, not the raw hex grid's inherent sawtooth: a pointy-top hex
  has no edge that points due south, so offset-column roads alternate
  between two diagonal edges every row -- `RoadDresser` detects pure
  through-hexes of such a corridor and renders them at a corrected
  anchor with a due north/south bearing instead of the true kinked
  diagonal (pathing/generation untouched; presentation only).
  **Batch 2** (`BridgeDresser.cs` / `KnockableProp.cs` / `DamageFx.cs` /
  `RubbleDresser.cs`): bridges get guardrails, a through-truss arch over
  water spans, and piers dropping to the carved riverbed depth -- the
  deck itself is a rectangle rotated and shaped to the crossing's own
  heading (not a fixed axis-aligned square, which reads as a static
  brown diamond wherever a bridge runs at an angle to world axes, i.e.
  almost always on a hex grid); poles,
  hydrants, trash cans, and parked cars are **knockable** (a monster or
  tank walking through one tips it over, a timed tween, no physics
  engine); Damaged buildings breathe a slow smoke plume and a collapse
  fires a one-shot dust burst; destroyed buildings scatter tumbled
  rubble chunks over the crushed pancake instead of one flat slab;
  office billboards and occasional roadside boards carry period-poster
  color-block art (soda bullseyes, movie one-sheets, headline bands);
  buildings tint warmer/residential toward the outskirts and
  cooler/institutional near downtown (hex-distance from `CenterHex`
  standing in for road-graph radius); and a wooden table rim plus a
  flat-color backdrop ring frame the whole map so it reads as a
  diorama on a table rather than trailing into the void at its edge.
  **Batch 3** (`NightMode.cs` / `NeonRegistry.cs` / `TrafficCar.cs`,
  finishing the docs/21 SS6 list): press **N** to ease the whole city
  from day to a dusk lighting preset (a code-created directional light
  and ambient/fog colors, since this environment has no Editor to
  hand-place one) while every registered neon material -- signage,
  bulbs, billboard art -- brightens to actually read as neon against
  the dim. Buildings within a `rail_depot` landmark's radius re-skin as
  warehouses (corrugated roofs, loading docks, a smokestack) and nearby
  straight road hexes grow a parallel rail siding, tying the depot into
  a small industrial district. `trafficCarCount` cars drive the road
  network hex-to-hex and flee like Citizens when a monster gets close,
  peeling off toward whichever reachable hex is farthest from the
  threat.
- **Combat** (`UnitCombat.cs` / `WeaponFx.cs` / `Projectile.cs` /
  `Tank.cs` / `HealthBars.cs`) — every unit has health and a weapon
  derived from its genome (`roster-client`'s tested `Combat.Profile`):
  laser arrays fire instant cyan beams, photon/plasma blasters lob slow
  phaser bolts, rifles spit fast bullets, claws/blades strike in melee.
  A few enemy **tanks** spawn at the city edge to fight — half carry a
  flamethrower (a short fire cone), half a 75mm cannon — and roll in
  toward the nearest monster; monsters auto-retaliate and auto-engage,
  or you can right-click a tank to order the group onto it. Units in
  battle show a floating **health bar**. Nothing walks through anything
  else: overlapping units are pushed apart each frame (citizens
  excepted — they're prey). Tune `tankCount` on `RuntimeCityBuilder`.
- **`Assets/Scripts/WaypointCommander.cs`** / **`SimpleCameraRig.cs`** /
  **`HudStatus.cs`** — mouse orders (left-click select · right-click:
  ground = waypoint, Shift queues; citizen = eat; building = attack),
  WASD/Q/E/scroll camera, and the on-screen wallet/orders/help readout.
  Input goes through the NEW Input System exclusively — this project's
  `activeInputHandler` is Input System only, so the legacy `Input` class
  would throw at runtime.
- **`Assets/Scripts/RosterFetcher.cs`** — the roster fetch RuntimeCityBuilder
  drives: live HTTP fetch from `mutator-service`, local-disk cache
  fallback if the service is unreachable. Usable standalone too.

## Seeing your monsters running around the city

`RuntimeCityBuilder`/`RosterFetcher` default to `https://maddr-mutator.onrender.com`
— the same deployed instance [The Lab](https://brainpuddler.github.io/The-Mad-Dr-MadDr.MCs-/)
(`site/main.js`'s hardcoded `MUTATOR_URL`) uses. **No local server needed**
for the common case:

1. Open [The Lab](https://brainpuddler.github.io/The-Mad-Dr-MadDr.MCs-/)
   and spawn a creature. **Then click "⭐ Save to stable" on it** — the
   Stable *is* your Menagerie for v0.1 (there's no separate "pick your
   active roster" screen yet); spawning alone only saves to your
   browser, never to the account-level roster `RosterFetcher` reads.
2. In the Lab's header, click **"🆔 Account ID"** — copies this browser's
   account ID (docs/07's dev-mode `x-account-id` stand-in for real auth;
   there's no cross-device login yet, so this is literally how a monster
   gets from the Lab to the battlefield today).
3. In Unity: empty GameObject → add `RuntimeCityBuilder` → paste that ID
   into its **Account Id** field → **Play**. First run has nothing to
   fall back to if the service isn't reachable; once one live fetch
   succeeds, a local cache exists for offline runs after that.

**Two gotchas found testing this against the real deployed service:**

- **Cold starts.** Free-tier hosting spins the service down after
  inactivity; the first request after a while can take 30-60 s to wake
  it, longer than `RosterFetcher`'s 8 s default `timeoutSeconds`. If the
  Console shows a fallback-to-cache warning on the first try, just hit
  Play again a minute later.
- **A failed live fetch falls back to whatever's cached, which can be
  stale or empty** (e.g. an earlier test against a different `baseUrl`
  that genuinely had zero creatures). If Console says "0 creatures, from
  local cache," that 0 is old news, not necessarily today's answer --
  delete `Application.persistentDataPath`'s `roster_cache_*.json` (or
  just retry until a `(..., live)` fetch succeeds) before concluding the
  Menagerie is actually empty.

**Running against a local Mutator service instead** (e.g. testing your
own unpushed service changes): `cd packages/genome-core && npm install
&& npm run build`, then `cd ../mutator-service && npm install && npm run
build && npm start` (defaults to `:8787`), and change both
`RuntimeCityBuilder`'s and `RosterFetcher`'s **Base Url** fields to
`http://localhost:8787`. Note the *Lab website itself* is hardcoded to
the deployed URL (`site/main.js`'s `MUTATOR_URL`), so a creature spawned
there won't exist on your local service unless you also edit that
constant and serve `site/` locally.

## The citygen-core, roster-client, and creature-mesh package references

[`packages/citygen-core`](../packages/citygen-core/) (engine-agnostic hex
grid + attack-arc math, docs/18 §1 / docs/04 posMod),
[`packages/roster-client`](../packages/roster-client/) (dependency-free
JSON parsing for `mutator-service`'s response shapes, docs/07), and
[`packages/creature-mesh`](../packages/creature-mesh/) (the docs/08 Lab
renderer port that regenerates a creature's real body from its genome)
are referenced in `Packages/manifest.json` as local packages:

```
"com.maddr.citygen-core": "file:../../packages/citygen-core",
"com.maddr.creature-mesh": "file:../../packages/creature-mesh",
"com.maddr.roster-client": "file:../../packages/roster-client"
```

Notes for whoever opens the Editor:

- **First open after pulling this**: Unity resolves these packages,
  compiles `MadDr.CityGen`/`MadDr.RosterClient`/`MadDr.CreatureMesh`
  from source (each has its own `.asmdef`), and **generates `.meta`
  files inside the package folders** — local `file:` packages are
  mutable, so Unity metadata lands in the package folder itself.
  **Commit those `.meta` files when they appear**; they're how asset
  identities stay stable.
- Each package's xunit tests live in its own `Tests~/` and its dotnet
  build outputs go to `bin~`/`obj~` — tilde-suffixed folders are invisible
  to Unity's importer by convention, which is what keeps the dotnet and
  Unity toolchains from tripping over each other. From each package
  folder: `dotnet test Tests~/CityGenCore.Tests.csproj`,
  `dotnet test Tests~/RosterClient.Tests.csproj`,
  `dotnet test Tests~/CreatureMesh.Tests.csproj`.

## Two Editor settings to verify before heavy work

`Edit → Project Settings → Editor`:

- **Version Control → Mode**: `Visible Meta Files`
- **Asset Serialization → Mode**: `Force Text`

Both are Unity 6 defaults, so a fresh 6000.x project should already have
them — verify rather than assume, since changing them later re-serializes
half the project.

## Git notes

- `.gitignore` here is Unity's standard template (Library/Temp/obj etc.),
  correct now that a real project sits at this folder's root.
- `.gitattributes` here is Unity's standard template too, but its
  `[attr]` macro definitions moved to the **repo-root** `.gitattributes`
  (git only allows macros in the top-level file — nested definitions
  produced warnings on every git operation). The `lfs` macro currently
  means *plain binary, not Git LFS* — deliberately, so clones don't
  require git-lfs; the upgrade path is documented in the root file.
