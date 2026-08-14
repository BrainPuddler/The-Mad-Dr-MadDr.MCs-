# 34. Character system overhaul

**Status: implemented, unverified in a real Editor** (this environment has
none — see §5). Replaces `Worker`'s single capsule-plus-hard-hat body with
a shared, modular, low-poly humanoid rig + procedural animation library,
and adds `HumanSoldier`, a new cosmetic Army-faction dressing unit built
on the same system.

## 0. Why (creator brief, condensed)

A full "Character System Overhaul" brief: replace every capsule-based
human character with a shared low-poly modular rig (head/torso/upper+
lower arm/upper+lower leg/hands/feet, ~120-300 triangles, shared material,
color via MaterialPropertyBlock), a shared lightweight procedural
animation set (idle/walk/run/build/harvest/carry/death), distinct visual
identities for Human Workers/Human Soldiers/Mad Doctor Workers/Alien
Workers, plus an entirely new "Civilian Victim" rescue-mechanic category.

This is genuinely a multi-week feature by scope. Two rounds of
AskUserQuestion (recorded in docs/12) scoped this pass down before any
code was written:

1. **Starting scope**: "Foundation + all 4 unit reskins/new units" —
   build the shared rig/animator, reskin `Worker`, and add `Human Soldier`
   + `Alien Worker` as real units this pass. **Civilian Victims (the
   rescue-mechanic system — calm/alert/panic/injured/trapped/rescued
   states, ~10 variants, ~15 animations) are explicitly OUT of scope for
   this pass** — confirmed as the highest-risk, most novel piece of the
   whole brief (a wholly new gameplay system, not a reskin), deferred to
   a later session. `Citizen.cs`'s own capsule is therefore
   **intentionally untouched** by this doc — not an oversight.
2. **Alien Worker scope**: matches Worker's existing construction/
   scavenge role almost exactly (hover instead of walk) — built as a
   **faction-conditional visual variant of the SAME `Worker` class**, not
   a new unit type, exactly like the existing Mad-Doctor "Zombie" skin.
3. **Human Soldier scope**: has aim/fire poses and a rifle silhouette —
   real ranged combat, unlike Worker's weak melee, and nothing in this
   codebase has an infantry-unit roster slot for it. Built as a **new,
   purely cosmetic, client-side-only unit** (`Citizen.cs`'s pattern: no
   match-core sync, no real stats, no production entry) rather than a
   real new combatant — "aim"/"fire" are flavor animation only, no
   projectile, no damage.

## 1. Files

- **`HumanCharacterKit.cs`** — pure geometry. `HumanCharacterKit.Build(parent,
  profile)` constructs a Transform-hierarchy rig (pivots + `PrimitiveType.Cube`
  parts, no skinned mesh — see §2 for why) from a `HumanCharacterProfile`
  and returns a `HumanCharacterRig` (every pivot/Renderer an animator or
  caller needs). One shared, instanced `Material`
  (`HumanCharacterKit.SharedMaterial()`); every part's color is a
  `MaterialPropertyBlock` override, never a per-instance Material.
  `HumanCharacterProfile` has four static presets so far: `HumanWorker()`,
  `MadDoctorWorker()`, `AlienWorker()`, `HumanSoldier()`.
- **`HumanCharacterAnimator.cs`** — pure procedural transform animation,
  no bones, no IK solver, no ragdoll/physics. `Tick*` methods for
  Locomotion (walk/run, distance-synced), Carry, Build, Harvest, Idle
  (plain breathing or Mad-Doctor twitchy), Hover (Alien idle/move),
  Aim (Soldier guard/aim/fire), Death (quick collapse). One
  `HumanCharacterAnimState` per character instance carries phase/timer
  state between ticks.
- **`Worker.cs`** — reskinned onto the kit. `BuildModel()` now picks a
  profile from `RuntimeCityBuilder.chosenFaction` (Human/Mad-Doctor/
  Alien); every existing movement method additionally drives the
  animator; death defers `Destroy` by `DeathDestroyDelay` (0.5s) so the
  collapse animation has time to play, instead of vanishing instantly.
  **State machine, AI priority, combat, and economy are byte-for-byte
  unchanged** — only visuals were touched.
- **`HumanSoldier.cs`** — new. Standing-guard/short-patrol dressing unit
  around the local human player's own HQ, spawned only when they picked
  Human Army (`RuntimeCityBuilder.SpawnStartingSoldiers`, gated in
  `SpawnStartingBases`). Turns to aim and pulses a fire-recoil flavor
  animation when a monster is nearby — no damage, no `UnitCombat`.

## 2. Why cubes, not skinned meshes, and not the stock Capsule primitive

This environment has no Unity Editor and no DCC/animation-clip authoring
tool (CLAUDE.md; every existing mesh in this codebase — `LabMeshBuilder`,
`PbrTextureAtlas`, `BuildingWindowGrid`, `MonsterBody`'s creature
geometry — is procedural C#, built at runtime). A real skinned mesh +
bone rig + animation clips simply cannot be authored here, so procedural
transform animation over a pivot hierarchy (this doc's approach) is the
only path that was ever viable — the same conclusion `MonsterBody`
already reached for creature limbs (foot-planted IK, shoulder-swing, all
plain transform math).

Every part is `PrimitiveType.Cube` (12 triangles), not Capsule/Cylinder/
Sphere — Unity's stock Capsule alone is several hundred triangles, which
on its own blows the brief's ~120-300-triangle-per-character budget
before a single limb is added. A full biped (torso, head, 2× upper+lower
arm, 2× hand, 2× upper+lower leg, 2× foot = 13 parts) is 156 triangles;
the legless Alien variant is 84. Blocky cube limbs are also a deliberate
style choice, not just a budget compromise — chunky stop-motion-model
geometry reads naturally as "simple and exaggerated for readability"
(the brief's own animation-style line) and fits this project's 1950s-
monster-movie register (maddr-aesthetic-preferences skill §1) better
than smooth capsules would have.

## 3. The "no skating" rule, and how the rig hierarchy enables it

maddr-aesthetic-preferences skill §7, verbatim: "No skating, ever — a
walk cycle's stride length must match actual distance traveled." Every
`Tick*` locomotion method takes `distanceMoved` — the EXACT displacement
the caller already computed this frame (`speed * dt`, or the real
post-collision step in `Worker`'s case) — and advances gait phase by
that distance, never by raw elapsed time. A stopped character's legs
hold mid-stride exactly like a real stopped walker's would, instead of
continuing to swing in place. `Worker.cs` threads a `_frameMoveDistance`
field through every one of its own movement methods (`TickPlayerMove`,
`TickSeekBuild`, `TickSeekScavenge`, `TickCombat`'s closing-distance
branch) specifically so the animator always receives the real number,
not an assumed one.

Rig hierarchy: the Torso pivot is also the "spine" — hunch, lean-into-
work, idle twitch, death collapse, and hover bank all rotate/offset it,
carrying the head and both arms (its children) along for free. Hip
pivots are parented under the character's own ROOT instead, so torso
lean never drags the legs — a real person doesn't tip their hips when
craning their neck to work. Every `Tick*` method computes an ABSOLUTE
pose from the rig's own rest pose (`TorsoRestLocalPos`/
`TorsoRestPitchDeg`, set once at `Build()` time) plus the current phase,
never accumulating deltas — switching animation modes frame-to-frame
(walk → idle → build) never drifts or needs an explicit reset.

## 4. Faction/kind mapping

| Brief category | Implementation |
| --- | --- |
| Human Worker | `Worker` + `HumanCharacterProfile.HumanWorker()`, selected when `chosenFaction == HumanArmy` (or `Mixed`, the no-single-origin fallback) |
| Mad Doctor Worker | `Worker` + `HumanCharacterProfile.MadDoctorWorker()` (hunched, thin, long-armed, asymmetric, twitchy idle), selected when `chosenFaction == MadDoctor` — this is the pre-existing "Zombie" fiction layer, same class, new skin |
| Alien Worker | `Worker` + `HumanCharacterProfile.AlienWorker()` (no legs at all — a geometry fact, not an unanimated-legs promise — hover hold height, bob/drift/wobble), selected when `chosenFaction == AlienHive` |
| Human Soldier | New `HumanSoldier` class, cosmetic-only, spawned around the human's own HQ only when they picked Human Army |
| Civilian Victims | **Deferred, not built this pass** — see §0 |

Worker only ever belongs to the local human player today (no
per-instance owner field — confirmed by reading `OnCitizenPossessed`'s
own comment before assuming otherwise), so reading the single
`RuntimeCityBuilder.chosenFaction` field is correct and sufficient; there
was no need for a per-instance faction lookup.

## 5. What's verified, what isn't

**Verified by direct code inspection** (this environment's actual
verification ceiling, same as every other doc in this project): every
`HumanCharacterAnimator`/`HumanCharacterKit` call site's argument count
and order checked against its definition; every `HumanCharacterRig`/
`HumanCharacterProfile` field reference checked against its declaration;
brace/paren/bracket balance checked file-by-file; no duplicate type
names introduced; `Worker`'s selection raycast path (`WaypointCommander.
WorkerUnderCursor`/`WorkersInBox`) re-checked against the new single-
root-BoxCollider scheme and confirmed unaffected (neither ever depended
on a Renderer or collider count, only `transform.position` and
`GetComponentInParent`). One real bug was caught and fixed during this
review: the root selection-collider's height was computed by summing
already-`HeightScale`-scaled values and then multiplying the sum by
`HeightScale` again — silently quadratic for any profile whose
`HeightScale` isn't ~1 (today's four presets are all 0.92-1.05, so the
practical error was small, but the formula itself was wrong).

**NOT verified** (no Editor exists in this environment, same standing
caveat as docs/33's own §5-§12): the shader/material actually compiles
and renders (`ShaderUtil.FindRenderableShader()`'s URP/Lit path, same as
every other procedural material in this codebase); proportions read as
proportions rather than an ambiguous blob at RTS camera distance; any
animation actually LOOKS like walking/hunching/hovering/hammering rather
than just satisfying the math on paper; the selection collider's actual
size feels right for click accuracy; performance at "hundreds or
thousands of simultaneous characters" (no profiling tool exists here
either — the brief's own perf asks, shared meshes/material/MPB/single
collider, were followed by construction, not measured).

## 6. Explicitly deferred / not done this pass

- **Civilian Victims** (calm/alert/panic/injured/trapped/rescued states,
  ~10 variants, rescue mechanics) — see §0. `Citizen.cs` is untouched.
- **Human Soldier as a real combat unit** — no `UnitCombat`, no
  match-core registration, no roster/production entry. Aim/fire is
  flavor animation only.
- **Alien Worker / Mad Doctor Worker for AI opponents** — `Worker`
  itself is still local-human-only (pre-existing scope, unchanged by
  this pass); an AI opponent's Workers, if any ever exist, would need
  their own faction lookup, not `chosenFaction` (which is specifically
  the LOCAL human's pick).
- **`HumanSoldier` at AI-opponent Army bases** — scoped to the local
  human's own HQ only, an explicit cost/benefit cut (see
  `SpawnStartingSoldiers`'s own comment), not a limitation of the
  system itself.
- **LOD** (the brief's own "LOD-ready assets" ask) — the rig's flat part
  list is a reasonable LOD0 starting point, but no actual LOD group /
  reduced-triangle variant was built this pass.
