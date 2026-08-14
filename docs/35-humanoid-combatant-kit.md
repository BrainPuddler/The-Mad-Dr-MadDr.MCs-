# 35. Humanoid combatant kit (armed human variants)

**Status: implemented, unverified in a real Editor** (this environment has
none — see §5). Adds real combat to armed human units via one shared,
data-driven kit — `HumanCombatProfile` + `HumanoidCombatant` — instead of
per-variant code. `Human Soldier` upgraded from cosmetic flavor to a real
combatant on this kit; `Grandma-in-a-wheelchair` and `Armed Civilian`
added as new variants on the same kit.

## 0. Why (creator brief, condensed) and what didn't match this codebase

Full brief: "Refactor Human Soldiers & Armed Citizens into Monster
Variants" — treat armed humans as data-driven variants of "the existing
Monster system" (`MonsterAgent`, genome-driven creatures), via
ScriptableObjects, a generic weighted `MonsterSpawner`, object pooling,
"the existing LOD system," Burst-friendly updates, and general GPU
instancing, so a new variant is "primarily data... not new code."

Researched before writing anything (an Explore agent, read-only). The
brief's underlying GOAL is sound and matches this project's own recent
direction — docs/34's character-system overhaul already built exactly
this kind of shared, data-driven kit for VISUALS. But several of the
brief's specific architectural assumptions don't hold in this codebase:

- **`MonsterAgent` requires a bred `StoredGenomeDto`** — locomotion,
  combat stats, and the mesh itself all derive from genome, with no
  optional/pluggable non-genome path (`_body.Build(creature)` runs
  unconditionally in `Init`). There is no "the AI doesn't know if it's a
  zombie or a soldier" layer to plug into. Folding armed humans into it
  would mean either faking genome data for something that was never bred
  (conflicting with this project's own Origins/breeding invariants,
  CLAUDE.md) or forking the class — the second one defeating the brief's
  own "no duplicate AI" goal outright.
- **This was also already decided the other way, twice, with the
  creator.** Both `Worker` and docs/34's `HumanSoldier` explicitly state
  they're non-genome on purpose — HumanSoldier's own header cites an
  AskUserQuestion round confirming exactly this before it was built.
- **`MonsterSpawner`, object pooling, and an LOD system don't exist in
  this codebase.** LOD is a documented, acknowledged GAP
  (`RuntimeCityBuilder.cs`'s own comment: "zero static batching, mesh
  combining, or LOD anywhere in the city-building path"), not hidden
  infrastructure. Burst/DOTS aren't used anywhere. "GPU instancing"
  today means `Material.enableInstancing = true` on a handful of shared
  Materials (`HumanCharacterKit`, `BuildingWindowGrid`, `LowPolyFireSystem`).
- **ScriptableObject exists** (`MonsterCombatProfile`, `SpecialAttackDefinition`)
  but only ever instantiated via `CreateInstance<T>()` with in-code
  defaults — there is no Unity Editor in this environment to author a
  real `.asset` file, so the brief's "designer edits an Inspector asset"
  workflow isn't achievable here regardless of base class choice.

Two AskUserQuestion rounds resolved this before any code was written,
both toward the smaller-scope/lower-risk option:

1. **Architecture**: a shared NON-genome kit (`HumanCombatProfile`/
   `HumanoidCombatant`, extending docs/34's existing pattern) rather than
   forcing armed humans through `MonsterAgent` with a synthesized
   genome. `MonsterAgent.cs` is completely untouched by this work.
2. **Scope**: "Grandma + 2-3 more variants," not the full 9-variant
   roster plus new spawner/pooling/LOD infrastructure the brief also
   asked for. This pass ships three: **Grandma** (new, the brief's own
   named flagship), **Armed Civilian** (new, the low-drama contrast
   case), and **Human Soldier** (upgraded from docs/34's cosmetic-only
   flavor unit to a real combatant on this same kit — leaving it as the
   one variant NOT on real combat while everything else here is would
   have been inconsistent, and the brief explicitly lists it in the
   roster).

## 1. Files

- **`HumanCombatProfile.cs`** — the gameplay/behavior data layer, deliberately
  separate from `HumanCharacterProfile` (docs/34's pure-geometry layer,
  composed in via a `Visual` field). Faction, MaxHealth, Radius, AimHeight,
  Mass, MoveSpeed, `WeaponProfile Weapon` (reuses the existing engine-
  agnostic weapon-stats system, `packages/roster-client`), AggroRadius,
  `Aggressive` (proactive-hunt vs. only-fight-when-cornered), and
  `TurnBeforeMove` (Grandma's wheelchair-turning trait). Three static
  presets: `Grandma()`, `ArmedCivilian()`, `Soldier()`.
- **`HumanoidCombatant.cs`** — the ONE shared MonoBehaviour every variant
  runs on. Guard/Patrol (idle at a post, occasional short loop, reusing
  `GroundPathFollower` — the same real `HexPathfinder`-routed, locally-
  steered movement `Worker` got in docs/25 Phase F) → Combat (direct
  steer-and-close against the nearest enemy within range, deliberately
  NOT path-followed, same reasoning `Worker.TickCombat`/`MonsterAgent.
  TickAttackUnit` already established for a target that moves every
  frame) → death (mirrors `Worker.OnDied`'s "bookkeeping now, defer
  GameObject destruction for the collapse animation" shape). Body/
  animation via `HumanCharacterKit`/`HumanCharacterAnimator` (docs/34).
- **`WeaponProfile.Shotgun`/`.Revolver`/`.ServiceRifle`** (`packages/
  roster-client/src/Weapon.cs`) — new factories alongside the existing
  `TankCannon`/`TankFlamethrower`/`ZombieClaws`, all `WeaponKind.Bullet`
  (real firearms, not `Melee`'s instant-reach claws — and `Bullet`
  already has working `WeaponFx` rendering, no new case needed). Tests
  added to `WeaponTests.cs` (this environment has no `dotnet` SDK either
  — same "no verification tooling" ceiling as everywhere else this
  session — but the creator's own machine can run them).

## 2. Faction strings: a real, pre-existing wrinkle

`UnitCombat.Faction` is a plain string; `NearestEnemyOf` treats any two
different strings as mutually hostile. Reading the actual usages
revealed this ISN'T a per-player alignment system at this legacy Unity
layer — `Tank.cs`'s own header says it plainly: Tank ("human" faction) is
"the test dummy that fights the monsters," an environmental defender, not
a real player-controlled unit. "monster" faction covers every player's
own creature/economy-side units (Worker, Collector, MonsterAgent) — a
simplified binary split, not real N-player hostility resolution.

Grandma/Armed Civilian needed to be hostile to EVERYONE — the player's
own Workers/Collectors AND Tank's defenders alike, a genuine neutral
threat. Rather than guess which of the two existing strings might
accidentally mean that, they get a THIRD, new faction: `"hostile_civilian"`.
Soldier (now real combat) matches Tank's own `"human"` string, since it's
explicitly Army-faction infantry and should never fight alongside/against
Tank by accident.

## 3. Grandma, point by point against the brief

| Brief ask | Implementation |
| --- | --- |
| "Drive a manual wheelchair" | `HumanCharacterProfile.SeatedHeight` (new, additive field on docs/34's profile struct — 0 default keeps every existing preset, including Alien's hover, byte-for-byte unchanged) seats the torso low instead of hovering high. A wheelchair prop (seat + two wide side-wheel cubes) is built directly in `HumanoidCombatant`, same pattern `HumanSoldier.BuildRifle` established for weapon props. New `HumanCharacterAnimator.TickWheelchair` — deliberately NOT `TickHover` (the Alien's hover has vertical bob/side-drift/forward-lean that would read as levitating on something with wheels) — grounded, distance-synced (same "no skating" rule), a push-the-rim arm cycle instead of a walking swing. |
| "Move slower than most enemies but be aggressive" | `MoveSpeed` 1.6 (the slowest of the three variants) + `Aggressive = true` (proactively hunts, doesn't wait to be cornered). |
| "Rotate realistically before changing direction" | New `HumanCombatProfile.TurnBeforeMove` + `HumanoidCombatant.TurnInPlaceIfNeeded`: when the desired heading differs from current facing by more than 35°, she rotates in place (zero translation) before moving, instead of `GroundPathFollower`'s normal continuous turn-while-walking. Applies to both patrol and combat approach. |
| "Fire a powerful shotgun blast" | `WeaponProfile.Shotgun()`: short range (9m — a real shotgun's actual envelope, and the reason her slow speed doesn't make her harmless, she only needs you to get close), highest single-shot damage of any weapon in the codebase (44, above `TankCannon`'s 34), slowest cadence (2.0s — a pump/reload beat, not sustained fire). |
| "Amusing but believable idle animations" | Reuses `HumanCharacterAnimator.TickIdle`'s existing `Twitchy` path (docs/34, originally built for Mad Doctor Worker) for her standing-still fidget. Flagged honestly: this is a v0.1 REUSE, not a bespoke elderly-specific idle (knitting motions, adjusting a shawl) — a real, scoped-out follow-up if the twitch reads wrong for "old lady" specifically. |
| "Feel dangerous despite her slow speed" | `AggroRadius` 32 — the widest of the three variants, wider than her own weapon's 9m range. She notices and starts rolling toward a threat from well outside shotgun range, which is the actual source of dread, not raw speed. |
| "High Armour class, basically a tank" (creator, mid-conversation) | This engine has no separate armor/damage-reduction stat at the `UnitCombat` layer — confirmed before choosing an approach, not assumed. Expressed as a large health pool instead: 190 MaxHealth, above `Tank`'s own 150-210 range. |
| "Memorable without becoming cartoonish" / "and scary" (creator) | Modest 11° hunch (an elderly stoop, not a monster hunch), narrow shoulders that read as frail — deliberately CONTRASTING with her actual toughness/damage output, the "looks harmless, isn't" effect the rest of her stats are built around. |

## 4. Spawning

`RuntimeCityBuilder.SpawnStartingSoldiers` now builds `HumanoidCombatant`
+ `HumanCombatProfile.Soldier()` in place of the deleted `HumanSoldier`
class — same scope cut as before (local human's own HQ only, when they
picked Human Army). New `SpawnHostileCivilians`: 1 Grandma + 3 Armed
Civilians, scattered across the whole city (golden-ratio angle/radius
spacing off each one's own index — deterministic, no `UnityEngine.Random`,
same convention this codebase uses throughout) rather than clustered near
any base, since they're neutral to every player, not aligned with
whichever faction the human or an AI opponent picked. Counts are v0.1
placeholders (CLAUDE.md's standing policy).

**Scope note, stated honestly**: docs/19 §3/§4 already designed a
richer, more coherent version of this exact idea — ordinary Citizens
independently rolling weapon access and aggression regardless of age/body
type, where an elderly+wheelchair+aggressive+shotgun roll IS Grandma, a
worked example in that doc that was never actually built into real
gameplay until now. Wiring `SpawnHostileCivilians`'s flat, always-present
count into THAT system instead (Citizens becoming genuinely dangerous
some of the time, weighted by the existing docs/19 table, not a
guaranteed fixed count every match) is the natural, more design-coherent
follow-up — out of scope for this pass, which only needed these variants
to exist and be encounterable at all.

## 5. What's verified, what isn't

**Verified by direct code inspection**: every new/changed call site's
argument count and order checked against its definition; every new
`RuntimeCityBuilder`/`WeaponProfile`/`HumanCharacterKit` field or method
reference checked against its actual declaration (not assumed to exist —
`RegisterCombatant` was added as a new public method specifically because
no equivalent existed, mirroring the already-public `OnCombatantDied`);
brace/paren balance checked file-by-file; the `TickHover`-vs-`TickWheelchair`
mismatch was caught and fixed during this same review, before commit, not
after. `dotnet` is unavailable in this environment too, so even
`packages/roster-client`'s real xUnit test suite (tests added for the
three new `WeaponProfile` factories) could not actually be run here —
same standing verification ceiling as the Unity side, extended to a
package that, elsewhere on the creator's own machine, normally could be.

**NOT verified**: anything requiring an actual render or playtest —
proportions, the wheelchair's silhouette reading as a wheelchair at RTS
camera distance, Grandma's stats actually feeling "dangerous, not
cartoonish" in practice, faction hostility resolving as intended in a
real match, `WeaponFx` rendering the new `Bullet`-kind weapons correctly.

## 6. Explicitly deferred / not done this pass

- The remaining 6 named variants (Police, SWAT, Hunter, Militia, Elderly
  Survivor, Veteran) — the creator's own chosen scope was "Grandma + 2-3
  more," not the full roster.
- A real `MonsterSpawner`-equivalent generic weighted spawner, object
  pooling, an LOD system, Burst, general GPU instancing — none of these
  exist in this codebase today (§0); building any of them for real is
  new infrastructure work, not something this pass silently assumed away.
- docs/19's citizen-weapon-roll integration (§4's own scope note above).
- `HumanoidCombatant` selection/health-bar UI — these are hostile-to-
  the-player units; nothing in the brief asked for player-side selection
  UI, and Worker/HumanSoldier's own `SetSelected` convention wasn't
  extended here.
