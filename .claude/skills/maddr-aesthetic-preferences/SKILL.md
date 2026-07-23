---
name: maddr-aesthetic-preferences
description: The creator's aesthetic and design taste for MadDr.MCs (Mad Doctor's Construction Set) — art direction, creature/anatomy geometry rules, world-dressing style, color/material conventions, and animation feel, distilled from the project's own design docs and decision log. Consult this BEFORE doing any visual, creative, or design work in this repo — building or editing creature geometry (creature-mesh C# or site/creature-renderer.js), world/city dressing (RoadDresser, BuildingDresser, terrain, water, landmarks), faction visual language, animation/locomotion feel, or UI/HUD styling — even if the request doesn't explicitly ask about "style" or "aesthetics." Also useful when reviewing a screenshot or symptom report of something that "looks wrong," since the doc's "recurring correction patterns" section is a checklist of the specific mistakes this creator catches most often.
---

# MadDr.MCs aesthetic & design preferences

This is a style guide distilled from the creator's own words across `docs/01`,
`docs/08`, `docs/17`, `docs/21`, and the `docs/12` decision log — not
invented guidance. When in doubt, that's where the ground truth lives;
this skill exists so you don't have to re-derive it from scratch (or,
worse, re-learn it from a correction) every session.

The through-line worth internalizing before anything else: **this creator
corrects with precision, not vibes.** Feedback is rarely "make it look
better" — it's "the tank is on the head instead of the back," "the
roundabout normal is fine but the geometry sits below the surface," "fix
ONLY the avian body, don't touch the others." Match that precision back:
when you build or fix something visual here, reason about the actual
geometry/mount/silhouette, not an approximation of it.

## 1. Art direction — what this game is trying to look like

**The one-line reference**: *a 1950s monster movie set that happens to be
an RTS battlefield.* Not photorealism, ever, even where photorealism
would be easier or cheaper. Two touchstones anchor the tone:

- **Universal horror (1930s-50s)** for silhouettes, the lab, and the
  stitches — but rendered *comic-macabre*, not grimdark. Closer to
  Dungeon Keeper's wit than to Doom's seriousness. If a creature or scene
  reads as grim/edgy rather than gleefully monstrous, that's a tone miss
  even if every technical box is checked.
- **The Notebook** — the whole UI/presentation motif is Dr. Frankenstein's
  inherited journal. Every monster reads as visibly *assembled* (seams,
  stitches, mismatched parts), every panel feels hand-sewn into a journal,
  not a clean modern HUD.
- **Palette discipline is deliberate, not a limitation to work around.**
  Color comes from a curated gothic LUT, not a free RGB picker — "a
  thousand-hue rainbow horde would break the Universal-horror tone." If
  you're picking a new color for something, pull from the established
  palette family rather than inventing a fresh hue that happens to look
  fine in isolation.

**The genome pillar shapes the visual pillar**: every monster is
genome-bred, never catalog-picked, and that promise has to be visible.
Silhouette variety is the actual selling point — recolors/palette-swaps
alone read as a gacha skin system, which is exactly what this project is
built to not be. If a new part family or body plan doesn't change the
*silhouette*, it isn't pulling its weight.

## 2. Per-origin visual language (organic / tech / biotech)

Three origins, each with a distinct material read AND a breeding-rule
fiction that the visuals have to sell:

| Origin | Reads as | Breeding fiction | What this means for you |
| --- | --- | --- | --- |
| **organic** | flesh — veined, wet, pulsing, "grown" | breeds freely, drifts, mutates | soft/organic materials, never rigid |
| **tech** | issued hardware — riveted, painted, gauged | **never mutates or blends** — "a rifle passes through a splice whole or not at all"; changes only by Graft (the quartermaster, not the vat) | should read as *equipment strapped/bolted on*, not grown; sharp edges, visible fasteners, functional details (gauges, rivets, filler caps) sell "issued," not "evolved" |
| **biotech** | grown machinery — alive, unsettling | **breeds like flesh** — "alien weapons evolve," this IS the alien horror | should read as equipment that's somehow also meat — glossy, pulsing, fused into the body rather than mounted on it |

The tell for whether you've got this right: could someone identify a
part's origin from its shape alone, with the color removed? If not, the
shape isn't doing its job (color communicates *contents/state*, not
origin — see §5).

## 3. Per-faction visual language

- **The Doctors' monsters** — the baseline/expressive faction. Full
  organic-part vocabulary, the widest possibility space. "The doctor has
  only the leash he stitched himself" — nothing institutional or clean
  about this faction's look.
- **Human Army** — 1950s military/industrial: `rifle_arm`, `optic_visor`,
  `sensor_mast`, `piston_leg`, tin-toy-robot detailing (rivets, enamel
  panels, a scanner-lens visor, a chest control panel with dial/gauge/
  blinking lights) in the Robby-the-Robot/Gort register. Reads as
  *manufactured*, not born.
- **Alien Hive** — "a mix of technology and physical alien flesh": organic
  chitin/compound-eye/antenna anatomy carrying grown biotech weapons.
  The three alien weapon families are deliberately differentiated so they
  read as **distinct silhouettes, not palette swaps of each other**:
  `plasma_lance` (tapered single emitter with a charge bulb),
  `laser_array` (a rigid cluster of narrow crystalline emitters),
  `photon_blaster` (a broad bioluminescent pulsing maw). If you're adding
  a fourth alien weapon, it needs its own silhouette idea, not a recolor
  of one of these three.

## 4. Anatomy & geometry: precision rules (read this before touching any mount/socket code)

This is where corrections happen most often, so it gets the most detail.
The pattern across nearly every correction in `docs/12` is the same: **a
piece of geometry was positioned/oriented by convenience (a default
socket, a world-axis-locked frame, an approximate offset) instead of by
the actual body's real surface** — and it looked *almost* right, which is
exactly why it shipped before being caught.

- **Mount at the socket the anatomy actually calls for, not whatever's
  convenient or default.** A storage tank that falls through to the
  head/antenna socket instead of the dorsal one is a real, shipped bug
  class here — check that every new part family is registered wherever
  the renderer decides socket assignment, not just wherever it's drawn.
- **Orientation must follow the body's real local geometry, not a
  world-axis assumption.** A pack frame that's always laid out along
  world-up/world-back works fine for a near-vertical torso and looks
  visibly wrong on a body whose back is genuinely sloped (the avian
  raptor lean). If a body plan isn't a simple upright cylinder, don't
  assume a shared "vertical mount" helper is orientation-correct for it —
  check the actual surface normal.
- **When a per-plan fix is needed, scope it to that plan.** The standing
  instruction pattern is explicit: "DO NOT change the position or
  orientation on the other bodies, Just the AVIAN." A shared helper
  function is fine, but the fix for one outlier body should not perturb
  every other body's numbers, even by a little — if you can express the
  fix as "identical to before when a new parameter is 0/absent, and only
  set that parameter for the one plan that needs it," that's the shape of
  fix this creator wants.
- **Nothing floats disconnected from its own body.** Watch for parts
  animating on the wrong phase/socket relative to the limb they're
  attached to (a decorative bulge that doesn't track the arm it's
  supposedly part of), and for anything whose position doesn't visibly
  originate FROM a body surface.
- **Placement must sit ON the true surface, not floating above or sunk
  below it.** An identity marking or accessory anchored to an
  approximate offset instead of the shape's real apex/surface will drift
  wrong the moment body proportions change (bulk, tier, etc.) — anchor to
  the actual computed geometry, not a hand-tuned constant that happened
  to look right for one test case.
- **Bodies must be cognisant of their environment.** Units/parts should
  never clip into world geometry (buildings, terrain) — if something CAN
  end up inside solid geometry under some parameter combination, treat
  that as a bug even if it's rare, not an edge case to ignore.
- **Mechanical things should move mechanically.** A treaded vehicle's
  turret and hull should behave like a real vehicle's do (turret
  independent of hull heading, not welded to travel direction); things
  with wheels/treads shouldn't ford water past a small fraction of their
  own height.

**Before shipping any new visual geometry, ask**: does this sit on the
actual computed surface of the thing it's attached to? Does its
orientation track the real local geometry rather than a world-axis
assumption? If I fixed one body plan, did I verify (not assume) every
other body plan's output is byte-identical to before?

## 5. Color and material: two orthogonal channels, don't conflate them

**Shape communicates origin/faction. Color communicates contents/state.**
These are independent channels and mixing them up is a recurring mistake
class. The canonical example: a storage vessel's *shape* says what it's
made of (metal tank = tech, membrane sac = organic, vesicle cluster =
biotech) — its *color* says what's currently inside it (RED for blood,
WHITE for bone), driven by whatever tool filled it, not by the vessel's
own origin. If you're adding a new stateful/fillable thing, ask which
channel (shape or color) should carry which piece of information, and
keep them separable — don't let a single visual property try to encode
two different facts.

## 6. World & city dressing: the miniature-set read

The battlefield should read as **a diorama on a table**, viewed from RTS
camera height — not an abstract flat gameplay grid with props scattered
on it. Concretely:

- **Sculpted terrain, not a flat plane** — rolling ground, mounded
  ridges, carved riverbeds with real depth, a raised table-edge rim and a
  painted backdrop ring so the map doesn't trail into the void.
- **Water and roads must read as organically shaped, not as tiles.**
  Blocky/grid-aligned water or roads is treated as a bug, not a
  "stylized" choice — rivers and ponds need smooth, flowing banks with
  visible depth gradient; roads need to run in real, mostly-parallel
  lines with proper junction grammar (a real 4-way cross or a roundabout,
  **never both at once**, and never mid-block on a multi-lane arterial).
  Traffic should visibly follow curves, not cut through roundabout
  islands.
- **Anything that reveals the procedural generator's mechanism reads as
  broken, even if it's technically correct.** Zig-zagging streets,
  Y-intersections where a 4-way was intended, hex-tile seams in a
  river — these get caught immediately because they break the illusion
  that a person (not an algorithm) laid out this town.
- **Density and period specificity matter**: rooftop water towers, fire
  escapes, cornices, neon signage, a movie-palace marquee, pastel
  tail-finned parked cars, diner chrome. The 1950s isn't a vague "retro"
  gesture, it's a specific, researchable period — when adding set
  dressing, prefer a concrete period reference over a generic
  sci-fi/fantasy default.
- **Towns should read as real settlements, not hex-radial abstractions.**
  A direct standing instruction: villages must be laid out like an actual
  small North American town (population-appropriate grid, Main Street,
  etc.), not a hex-symmetric pattern that reveals the underlying tile
  grid.

## 7. Animation & movement feel

- **No skating, ever.** A walk cycle's stride length must match actual
  distance traveled — "motion must match the distance traveled by the
  placed foot," scaled by the creature's own physiology/speed, never a
  flat animation-speed multiplier disconnected from how far the body
  actually moved.
- **Flight should feel weighted and purposeful, not a straight-line
  hop.** Turns should arc, not snap; a flyer should plausibly choose to
  go up-and-over vs. weave around based on an energy-cost-flavored
  decision, and carried cargo should visibly cost speed (weight matters
  more in the air than on the ground, not less).
- **Group movement should read as a group of individuals, not a clump or
  a single blob.** Units arriving at a shared destination should
  distribute around it (a ring, spread out), not stack on the exact point
  clicked, and should settle facing a shared, coherent direction rather
  than whatever direction each happened to arrive from.
- **Traffic/crowd realism**: vehicles keep lanes and following gaps and
  swerve around danger instead of driving through it; pedestrians favor
  sidewalks and cross at corners except when fleeing.

## 8. The self-check pattern (recurring mistakes to catch before they ship)

If you're about to say a visual fix is done, run it against this list —
these are the specific failure modes this creator has caught repeatedly,
across otherwise-unrelated features:

1. **Wrong socket / wrong default.** A new variant of an existing family
   (new part, new weapon, new vessel) needs to be registered everywhere
   the OLD variant was, not just in the one place that draws it — dispatch
   tables for socket selection, mirroring, capacity, texture are all
   separate lists that are each easy to forget one entry in.
2. **Right on the common case, wrong on the outlier.** Test the geometry
   against the body plan that's most different from the norm (steepest
   slope, most extreme proportions), not just the default/first one you
   tried.
3. **A local fix silently perturbing something that already worked.**
   If a shared helper needs a new capability for one case, make sure the
   old cases are provably unchanged (ideally: the new parameter defaults
   to a no-op value, and only the one case that needs it sets it), not
   just "probably still fine."
4. **Approximate offsets instead of real computed geometry.** A
   hand-tuned constant that "looks right" for today's proportions will
   drift wrong the moment bulk/tier/params change; anchor to the actual
   surface/apex/socket math instead.
5. **Grid/procedural seams showing through.** Anything that reveals "a
   generator made this" instead of "someone designed this" — tile edges,
   perfectly regular spacing, snapped-to-grid roads/water — reads as
   broken even when it's geometrically valid.
6. **Shape and color both trying to carry the same information**, or
   either one carrying the wrong one (see §5).

## When you're not sure

Prefer checking `docs/08`, `docs/17`, `docs/21`, and recent `docs/12`
entries over guessing — this project keeps its own design rationale in
those docs, and CLAUDE.md's decision-log convention means the reasoning
for almost every non-obvious visual choice is written down somewhere in
`docs/12` already, phrased in the creator's own words.
