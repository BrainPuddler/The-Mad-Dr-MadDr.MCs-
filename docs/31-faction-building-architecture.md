# 31 — Faction Building Architecture: Windows, Antennas, Lighting, Silhouette

Status: Draft v0.1 · Extends [17-factions.md](17-factions.md) (creature-level
faction identity) into the RTS layer's own **buildings** (`BaseDresser.cs`) ·
Decision trail in [12-open-questions.md](12-open-questions.md) · Prompted by
creator direction, 2026-08 ("Faction Architecture, Windows, Lighting &
Communications Antennas" gauntlet).

This doc is the authoritative reference for `BaseDresser.cs`'s per-faction
building visuals — the RTS-layer Factory/Control Centre/Hq/Big Brain roster,
**not** `BuildingDresser.cs`'s procedurally-generated civilian city (that
system has its own docs/21/docs/30 lineage and a different, period-generic
1950s-American-town brief; it is not in scope here). [17-factions.md](17-factions.md)
covers the *creature* genome's part-origin/material/energy identity per
faction; this doc is the parallel statement for the *buildings* those same
three factions construct.

## 0. The principle, inherited from docs/17

A faction is an expression profile layered over one shared engine, not a
forked codebase. The building system already proves this pattern works:
`BaseDresser.BuildFactoryShape`/`BuildHqShape` dispatch on `PlayerFactionFor`
to one of three per-faction methods (or the Mixed-faction generic fallback,
deliberately kept plain — see §7), each dressing the **same shared
silhouette/footprint** (`root`'s body cube, chimney-slot cylinder, turret
cube) with faction-specific materials and **grandchild "Trim" holder**
geometry. Every rule below extends that existing split; nothing here
proposes a parallel building system.

## 1. Global rule: silhouette before color

The player must be able to identify a faction from a distant black
silhouette, before any material or lighting resolves. Concretely: if every
material in a screenshot were swapped for flat gray, the three factions
should still read as three different *kinds of construction* — a fortress,
a machine, a spacecraft — not three identically-shaped boxes in different
paint. See §5's "final test" for how this is checked in practice.

Preserve the established owner-color identity (`BaseDresser.OwnerBaseColor`,
unchanged by this doc): **Human Army → blue, Alien Hive → purple, Mad
Doctor → green.** Color is still the load-bearing "whose building is this"
signal (docs/12's own "shape communicates kind, color communicates owner"
rule, `TintShape`'s entire reason for existing) — the architectural language
below must not fight that by, say, painting an Alien saucer's dominant
surface area in a non-owner hue. Owner color stays on the body (the direct
children of `root`); everything faction-specific below lives on the
non-tinted Trim holder, same split as today.

## 2. Windows

| Faction | Form | Framing | Read |
| --- | --- | --- | --- |
| Human Army | square/rectangular | painted wood, simple trim | army-barracks utilitarian |
| Alien Hive | round porthole | substantial brass ring, visible rivets, recessed | bolted into a machine |
| Mad Doctor | narrow vertical slit | deep masonry recess, irregular placement | castle arrow slit |

**Human.** The existing rectangular `HumanBlueLightMat` treatment in
`BuildHumanControlCentre` (a steady maintenance-bay glow, deliberately not
on the day/night occupancy schedule — see that method's own comment)
already matches the *shape* brief. Untouched by this doc; a future pass
could add painted-wood trim framing without changing its steady-light
behavior.

**Alien.** `IMPLEMENTED this pass.` Round, not square — the single
biggest shape-language break from the pre-existing flat rectangular
window void every faction used to share. A porthole is a flat brass disc
(a short `Cylinder`, its own flat cap rotated to face the wall's outward
normal), a recessed lit-or-dark glass disc set back into it, and a ring
of `Steel` rivet spheres placed in the WINDOW'S OWN FACE PLANE (derived
from `Quaternion.LookRotation(outwardDir, Vector3.up)`, not the world
X/Z plane `SpawnRivets`' own horizontal-cap convention assumes — a
porthole mounts on a vertical hull face, so that existing helper doesn't
fit here without a wrong-axis rivet ring). See
`BaseDresser.SpawnAlienPorthole`.

**Real design conflict found and resolved during implementation.**
`BuildAlienFactory`/`BuildAlienControlCentre` already carried an explicit
PRIOR 2026-08 creator direction: *"a living energy organism... no visible
bolts/rivets anywhere... avoid visible bolts and human engineering."*
This gauntlet's own brief asks for the literal opposite for the same
faction — brass rings, visible rivets, "bolted into a machine." Flagged
to the creator directly rather than guessed at; confirmed answer: **the
new gauntlet supersedes the prior organic-only direction outright** for
Alien windows, antennas, and the eventual saucer-massing overhaul (§6) —
not a hybrid, not a partial rollback. The organic crystal/energy-sac/rib
detail already built stays (nothing about that geometry was wrong or
asked to be removed), but Alien buildings are no longer "no visible
human engineering" — the mechanical/riveted vocabulary now layers
directly onto the same hull.

**Mad Doctor.** `PROTOTYPE STATUS: implemented this pass.` A tall, narrow
recessed void (width a small fraction of height) surrounded by an
oversized stone-block frame — the opposite proportions of every other
faction's window, which is exactly the point. See
`BaseDresser.SpawnArrowSlit`. Slits are irregularly spaced (jittered
placement, not a uniform strip) per the brief's "irregularly distributed."

Every LIT window (any faction) reuses the existing
`EmissiveAnimator.LightBehaviorKind.Window` human-schedule mechanism
(docs/12's 2026-08 "AAA upgrades" entry) — a per-window randomized
occupancy schedule, `MaterialPropertyBlock`-only (no shadow-casting
`Light`), distance-culled in `EmissiveAnimator.Tick`. Alien portholes and
Doctor arrow slits both get a lit/dark glass variant via the same
dark-void/lit-glow material pair pattern `PedestalWindowMat`/
`PedestalWindowGlowMat` already established — no new lighting mechanism
invented, only new window *shapes* wrapped around the existing one.

## 3. Materials (extends docs/17 §"Materials: the class × flavor matrix")

Already-shipped per-faction materials (2026-08 Factory/Control Centre
pass, `BaseDresser.cs`) map directly onto this brief and are **reused, not
replaced**:

| Faction | Structure/frame | Trim/accent | Existing helper |
| --- | --- | --- | --- |
| Human Army | brushed aluminum | carbon-fiber panel | `HumanAluminum()`, `HumanCarbon()` |
| Alien Hive | alien crystal, membrane | — | `AlienCrystal()`, `AlienMembrane()` materials |
| Mad Doctor | cast iron, dark brick | brass, oxidized copper, limestone | `DoctorIron()`, `DoctorDarkBrick()`, `Brass()`, `DoctorCopper()`, `DoctorStone()` |

The castle-transform brief's "large stone blocks, not decorative brick"
direction meant `DoctorDarkBrick()`'s existing brick-coursing texture
(`PbrTextureAtlas.Brick`) would have been the WRONG texture for Phase 3's
new tower/battlement/buttress geometry — that phase needed a new,
larger-scale "dressed stone block" texture, not a reuse of the brick
atlas entry. **Resolved in Phase 3 (§7): `PbrTextureAtlas.DressedStone` +
`DoctorCastleStone()`**, both new, both reused across
`BuildDoctorFactory`/`BuildDoctorControlCentre`'s new castle geometry
rather than adding a third stone material.

## 4. Lighting language

Already covers the letter of §4's brief without new engineering, because
the whole city lighting system (docs/28) was already built emissive-only,
budget-capped, and distance-culled:

- **Windows**: §2 above, per-faction shape + the existing Window schedule.
- **Doctor's Tesla arc**: `BuildDoctorControlCentre`'s existing
  `TeslaArc.cs` real `LineRenderer` bolt + `SpawnPulseLight`'s pulsing
  green core — already exactly "architectural lighting emphasizing
  faction-specific technology," predating this doc.
- **Alien/Human**: no per-faction emphasis lighting beyond windows exists
  yet. Real gap, not attempted this pass (folds into §7 Phase 2/4 —
  antenna indicator lamps, saucer rim lighting).

No new dynamic `Light` budget category proposed — `DynamicLightBudget`'s
existing nearest-N-to-camera cap (docs/28) already governs every real
light in the city, faction buildings included, and stays the enforcement
point for any future per-faction lighting addition.

## 5. Antennas — design grammar (not yet implemented, see §7 Phase 2)

Specified here in full so Phase 2 has a real brief to build against, not
a placeholder.

**Human — RKO-inspired Art Deco broadcast tower.** Tall narrow central
mast, strict vertical symmetry, tapered steel framework, a large
circular/oval radiating element near the top, thin precise structural
members, stepped geometric detailing. Reads as 1930s Hollywood's idea of
a radio transmitter — iconic, theatrical, geometric. Never modern
telecom/cellular.

**Alien — 1950s-70s B-movie mechanical apparatus.** Combinations of
oversized parabolic dishes, horn antennas, telescoping rods, loop
antennas, stacked directional elements. Heavy rotating base, exposed
pivots/gears/bearings, thick bundled cables, analog meters, indicator
lamps, vacuum-tube-like components, hand-painted numbers/warning labels,
weathering. Asymmetrical, uneven silhouette — the opposite of Human's
strict symmetry. Never sleek/futuristic/Starlink-like.

**Mad Doctor — Tesla/mad-scientist apparatus.** Extends the ALREADY-BUILT
`TeslaArc.cs` real-arc mechanic rather than inventing a parallel one:
central mast, oversized copper coil, circular electromagnetic ring,
branching asymmetrical rods, ceramic insulators, Bakelite switches, analog
gauges, glowing indicator lamps, dangling cable. Irregular, asymmetrical
— overbuilt and eccentric, never a clean modern tower.

**Procedural hierarchy (for whoever implements Phase 2):** primary
silhouette-defining pieces (tower/mast/dish/coil/ring) → secondary
medium-scale variation (brackets/crossbars/housings/rods) → tertiary small
details (bolts/rivets/cables/meters/lights), matching this project's own
existing `SpawnRivets`-as-tertiary-detail precedent (already used for
exactly this purpose on Factory/Control Centre trim) rather than a new
convention. Do not randomize the primary silhouette per-instance — that's
where faction identity lives; randomize secondary/tertiary only.

## 6. Alien buildings: saucer massing (Shipped, §7 Phase 4)

The brief's own strongest claim, restated precisely because it's a real
architectural-massing change, not decoration: **the current Alien Factory/
Control Centre massing (a box body + an offset cylinder/sphere element,
same shared shape every other faction's body uses) is the wrong starting
point.** A saucer IS the body, not something placed on top of one.

**Shipped.** `ProceduralMeshKit.Saucer(domeRadiusFrac, rimHalfThickness,
elevation, segments)` — a real revolved lathe mesh (tapered underside
cone → flat rim band → tapered dome cap), hand-authored (not a
`CreatePrimitive` composite, since no built-in primitive produces a true
saucer cross-section), same "-0.5..0.5, size via `scale` alone" calling
convention `Frustum`/`GableRoof`/`Wedge` already established. Cached once
as `BaseDresser.AlienSaucerMesh()` and reused at different `scale` per
call site — same "one mesh, many instance scales" idiom
`alien-crystal-spike` already established via `PropLibrary`.

`BuildAlienFactory`'s own body is now this saucer mesh directly (still a
`Placeholder()`-material DIRECT child of `root`, so `TintShape` keeps
re-tinting it unchanged — the mesh changed, not the split that makes
tinting work). `BuildAlienControlCentre` connects TWO saucer modules — a
main saucer (the same approach as the Factory) and a smaller secondary
module riding above it — via a passage-tube connector
(`alien-passage-tube`, a `ProceduralMeshKit.Frustum`-backed slight taper)
with two flat docking-collar rings at each end. The hive-mind crystal,
orbiting rings, curved struts, and the B-movie antenna rig (§5, Phase 2)
are all re-anchored to the SECONDARY module (replacing the old turret-
cube anchor) rather than the main saucer body. Brass-riveted portholes
(§2) are repositioned onto the main saucer's own round rim band (an
angle-around-Y placement, not the old flat-cube-face X-offset) so they
sit flush on the curved hull instead of floating past it.

**Load-bearing constraint from the brief itself, satisfied by
construction:** existing footprint must not grow. Both saucer bodies keep
their predecessor cube's exact `bodyW`/`bodyD` (unchanged from before this
phase); only `bodyH` shrinks (Factory: 0.65→0.42 of `fullScale.y`;
Control Centre: 0.8→0.5) — a WIDE, SHORT saucer, matching a saucer's own
canonical proportions anyway, which conveniently costs nothing against
the brief's own "flattened central body" description.

## 7. Implementation status and phasing

Broken into six phases; **Phases 1-4 are implemented as of this doc's
latest revision.** Each remaining phase is real, separately-scoped work,
not a placeholder stub.

1. **Windows** (§2) — Alien portholes, Doctor arrow slits. **Shipped.**
2. **Antennas** (§5) — full per-faction modular antenna systems. **Shipped**
   (Control Centre only, matching Phase 1's own scope — the Factory
   methods keep their existing roofline detail). Human's old spinning
   radar dish/sensor-tower/antenna-cluster block (`BuildHumanControlCentre`)
   is replaced by a real Art Deco setback mast: three tapering drum tiers,
   four fluted fin blades, a stepped setback ledge, and a slender finial
   capped by a STEADY (not pulsing — matching `HumanBlueLightMat`'s own
   established "clean and functional" language) blue beacon. Mad Doctor's
   old symmetric 4-rod roofline ring (`BuildDoctorControlCentre`) is
   replaced by a Tesla apparatus extending the pre-existing rod/arc
   (`SpawnArc`) rather than a second disconnected mast: a copper coil
   climbing the mast with ceramic insulator studs at each ring, two
   deliberately uneven branching rods, and a glowing lamp at the tip.
   Alien's old five-fold radially-symmetric crystalline-antenna ring
   (`BuildAlienControlCentre`) is replaced by a real bolted-on B-movie
   apparatus mounted at ONE asymmetric roofline point — brass-riveted
   rotating drum, tilted oversized dish, horn antenna, telescoping rod
   mast, loop antenna, a sagging bundled cable between the two rigs, and
   an analog meter + pulsing indicator lamp — the brief's own "opposite
   of Human's strict symmetry" is why this rig is singular and off-center
   rather than repeated radially like the geometry it replaces.
   **Two design conflicts surfaced and were resolved by proceeding under
   this phase's own brief** (see the note at the end of this list).
3. **Mad Doctor gothic castle transform** (massive stone construction,
   towers, battlements, buttresses). **Shipped**, both `BuildDoctorFactory`
   and `BuildDoctorControlCentre` — real trim geometry on the SAME
   owner-tinted body cube/footprint every other phase in this doc
   preserves (the body stays a `Placeholder()`-material direct child of
   `root` so `TintShape` keeps working unchanged, per §0's own standing
   split), not a body reshape: four round dressed-stone corner towers
   (`SpawnCornerTower`) with witch-hat conical iron caps replace the old
   flat corner pilasters at the SAME four corner positions; a
   crenellated battlement ring (`SpawnBattlements`, real alternating
   merlon/embrasure geometry around the roofline, not a solid cornice)
   replaces the plain roof edge; two sloped stone buttresses
   (`SpawnButtress`, `ProceduralMeshKit.Wedge` mounted flush against the
   wall via `Quaternion.LookRotation`) flank the arrow-slit window wall.
   A new `PbrTextureAtlas.DressedStone` texture (large staggered blocks,
   not `DoctorDarkBrick`'s small-brick coursing — see §3's own flag)
   backs the new `DoctorCastleStone()` material used for all three. The
   Mixed-faction generic Hq/Factory fallback is explicitly OUT of scope
   for this (and every other) faction-specific phase — a standing
   decision from the prior 2026-08 pass ("no bespoke fourth
   architectural style was asked for"), unchanged by this doc.
4. **Alien saucer massing** (§6) — full body-massing replacement plus the
   saucer-module/passage-tube generator. **Shipped**, both
   `BuildAlienFactory` (single saucer body) and `BuildAlienControlCentre`
   (main saucer + secondary module + passage tube) — see §6's own full
   writeup for what changed and how the existing crystal/ring/strut/
   antenna/porthole detail was re-anchored onto the new massing.
5. **Big Brain lighthouse base.** Mount the EXISTING, UNCHANGED Big Brain
   jar (`BuildBigBrainShape`) on a new squat circular stone/metal base
   with a small door — a new `BuildLighthouseBase`-style holder call
   inserted between `BuildPedestal` and the jar, not a modification to
   either. `BuildPedestal`/`BuildBigBrainShape` themselves must not
   change a single line — the creator's own explicit constraint. Not
   started.
6. **Per-faction emphasis lighting beyond windows/Tesla arc** (§4's real
   gap: antenna indicator lamps once Phase 2 exists, saucer rim lighting
   once Phase 4 exists). Not started; depends on Phases 2 and 4 existing
   first.

**Phase 2 design conflicts, resolved during implementation (creator asked
implementers to report these):**

- **Alien "no visible bolts/rivets" vs. this gauntlet's "full mechanical
  apparatus."** Already surfaced and resolved during Phase 1 (see §2's
  own conflict writeup) — the creator's explicit call, "New gauntlet
  wins — go full mechanical," is the standing resolution this phase's
  Alien antenna rig (brass rivets, gunmetal housings) applies without a
  second pause.
- **Human's existing "communication dish... reads as a radar sweep" vs.
  this gauntlet's explicit "never modern telecom/cellular... avoid
  modern military radar dishes" for the Art Deco tower spec.** New: the
  prior Control Centre doc comment described the dish as deliberately
  reading like a radar sweep, which is precisely what §5's Human brief
  rules out. No prior creator quote defends the radar-dish read the way
  the Alien "no rivets" direction was explicitly defended, so this was
  judged lower-stakes than the Alien conflict and resolved the same
  direction (new gauntlet brief wins) without a second confirmation
  round, consistent with "continue with execution." The dish/SlowSpin
  mount is fully replaced by the Art Deco setback mast described above;
  `SlowSpin.cs` itself is untouched and still used elsewhere (Alien
  crystal spike, orbiting rings, Human cooling-tower wheel).

## 8. Performance discipline (unchanged, restated)

Every rule in this doc inherits the existing performance discipline
already enforced elsewhere in this codebase, not a new policy:

- Shared, cached materials (`TexturedCache`-keyed `MTextured` calls),
  never a per-instance `new Material`.
- Small decorative elements (rivets, bolts, cables) as real but cheap
  geometry via the existing `SpawnRivets`-style jittered-placement helper,
  not hundreds of independently-authored unique meshes.
- Lighting stays emissive-`MaterialPropertyBlock`-first; any real `Light`
  goes through the existing `DynamicLightBudget` nearest-N cap, never an
  unbounded per-building light.
- New hand-authored meshes (saucer hulls, castle stone modules) follow
  `ProceduralMeshKit`'s own established convention: one shared `Mesh`
  instance per shape, reused via `RuntimeCityBuilder.SpawnMesh`/
  `BaseDresser`'s own equivalent, scaled per-instance via the Transform,
  never regenerated per building.
