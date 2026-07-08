# 15 — Part Genetics: Growing & Mixing Recognizable Parts

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*) · Extends [06-mutator-design.md](06-mutator-design.md); demonstrated by the runnable prototype in [`/prototype/mutator/`](../prototype/mutator/). Schema implications tracked as Q10 in [12-open-questions.md](12-open-questions.md).

## The problem

The genetic algorithm wants variety; the player needs **legibility**. A claw must read as a claw at battlefield zoom — even after fifty generations of mutation, even spliced with a tentacle. If mutation can produce unrecognizable mush, players can't read fights ([04-combat-model.md](04-combat-model.md) readability requirement) and the Mutator's output stops feeling like *parts* and starts feeling like noise. The El-Fish magic is precisely that offspring are *surprising but recognizable*.

Six strategies, designed to interlock:

## Strategy 1 — Identity invariants vs. variation parameters

Every part family splits its design into two layers:

- **Identity invariants** — the features that make the part read as what it is. These are *hard-coded into the part's construction* (in 3D: the base mesh topology and socket conventions, [08-creature-visualization.md](08-creature-visualization.md); in the prototype: the drawing logic). **No gene can touch them.**
- **Variation parameters** — everything else, fully owned by the genome.

| Family | Invariant (never varies) | Varies (gene-driven) |
| --- | --- | --- |
| Claw hand | A palm bearing 2–5 hard, curved, tapering talons | Talon count, length, thickness, curvature, knuckle spikes |
| Pincer | Two opposing crescent jaws meeting at a gap | Jaw reach, thickness, gap/closure, shell studs |
| Tentacle | One smooth limb tapering to a curling tip | Length, girth, taper rate, curl, sucker density |
| Antenna | Thin *paired* stalks, segmented, tip bulbs | Stalk length, bend, segment count, bulb size |
| Bug eyes | A clustered constellation of **3+** round eyes with pupils | Eye count, sizes, cluster spread, lashes |
| Stalk eyes | Eyeballs held aloft on flexible stalks | Stalk length, bend, eyeball size |

Recognizability is not a tuning outcome — it's a structural guarantee. The genome literally cannot express "a claw with no talons."

## Strategy 2 — Shared semantic axes (the breedability key)

Every part family interprets the **same six parameter genes**, identically named:

`length · girth · taper · curl · count · ornament`

Each axis means the same *kind* of thing everywhere: `curl` bends a tentacle, hooks a talon, closes a pincer, kinks an antenna. This is what makes **cross-family breeding meaningful** rather than nonsensical: when a tentacle (long, thin, strongly curled) is spliced with a claw (stubby, thick, straight), a child that lands in the claw family still inherits the tentacle parent's *build* — it comes out as a long, thin, hooked claw. The prototype's Exhibit 3 shows exactly this, and its property test verifies children always land between their parents on every axis.

Without shared axes, cross-family crossover would have to either discard parameters (children resemble one parent only) or blend incompatible meanings (noise). With them, every pair of parts in the catalog is breedable by construction.

## Strategy 3 — Homologous slot grammar (the Hox rule)

Parts occupy **homolog classes**, named after the biology that inspired them: claw, pincer, and tentacle are all *hand* homologs; antenna and horn are *sensor* homologs; bug-eyes, cyclops, and stalk-eyes are *eye* homologs. The grammar rule:

> Crossover and mutation family-jumps only ever swap a part for another part **in the same homolog class**.

A hand can become a different hand; it can never become an eye. Like Hox genes in real development, this guarantees every child is anatomically coherent — no creature with an eyeball for a fist (unless we someday *author* an eye-fist as a hand-homolog family, which is a content decision, not a GA accident). Graft ([06](06-mutator-design.md)) enforces the same grammar and rejects violations as failed experiments.

## Strategy 4 — Canalized expression

Genotype space is always the full 0–1 range on every axis; **phenotype space is the family's authored-safe sub-range**. Each family declares per-axis bounds (an antenna's `girth` expresses into a thin band; a horn's into a thick one), and genes map into those bounds at expression time. Borrowed from developmental biology's *canalization*: extreme genes produce visual *outliers*, never *broken* parts. This also means a gene's value survives family jumps intact — `girth 0.9` is "as thick as this family allows," whatever the family — which keeps lineages coherent across jumps.

## Strategy 5 — Authored extremes, interpolated interiors (the 3D mapping)

The prototype draws parts procedurally from the six axes. In the production 3D pipeline ([08-creature-visualization.md](08-creature-visualization.md)) the same axes drive: `length`/`girth` → socket bone scaling; `taper`/`curl` → bone-chain scale and rotation distributions; `count` → variant mesh selection or repeated socket elements (talon count, eye count); `ornament` → detail-layer blend shapes and the stitch/stud overlays. Artists author the *extremes* (the blend-shape endpoints and min/max bounds); the GA only ever explores the interpolated interior — every reachable phenotype is, by construction, within authored quality.

## Strategy 6 — Recognizability validation (the back-stop)

Strategies 1–5 make unrecognizable parts structurally unreachable, but content will grow and rules will get bent. The server-side viability check ([06](06-mutator-design.md)) gains one rule: **ornament may never obscure an invariant** (e.g., sucker density that buries a tentacle's silhouette caps at the value where the silhouette test still passes). For 3D, the planned check is a silhouette-envelope test at part-import time ([08](08-creature-visualization.md) authoring validation): each family ships authored silhouette envelopes, and a part configuration whose rendered silhouette drifts outside its family envelope fails import — catching authoring mistakes before the GA can find them.

## Body plans: discrete where necessary, continuous where possible

The same recognizability-vs-variety logic applies one level up, at the body-plan layer — with a finding the prototype makes concrete. Creature types like *upright biped*, *monkey-type knuckle-walker*, and *quadruped* do not need to be three separate archetypes: they are one **continuous plan family** (`tetrapod`) whose **posture axis** sweeps upright (0) → brachiator (~0.5) → all-fours (1), with `bulk`, `limb` (arm reach), and `tail` as further body axes. Everything inside a continuous plan family interbreeds smoothly — a biped × quadruped child is simply a mid-posture monkey-type, no special case needed.

Truly different anatomies remain **discrete plans**, and crossing between them is the rare cross-plan splice jackpot ([06](06-mutator-design.md) cross-archetype rules). The prototype implements three: the **blob** (no rigid skeleton; parts surface-mount on its membrane), the **serpentine** (one long tapering body; the hand part re-expresses as a tail appendage — a stinger or grasper), and the **winged** (a small body slung between membrane wings; hand parts become wing-claws at the wrists). Body axes are shared across plans with plan-specific expression (for a blob, `posture` expresses as membrane wobble and `limb` as pseudopod reach; for the winged, `posture` is wing raise and `limb` is wingspan) — the same canalization idea as the part axes, so body genes survive plan jumps meaningfully.

Production (`packages/genome-core/src/catalog.ts`) has since grown the discrete-plan roster to eight, past the prototype's original three, all following the same rules above (own body-axis expression, `ignoresSlots` where an anatomy has no room for a homolog, silent genes riding along as atavism): **crab** (wide low shell, sideways stance), **arachnid** (hunched two-part body, crowded with legs), **avian** (forward-leaning runner, long neck), **treant** (rooted trunk; `ignoresSlots: ["leg"]` — it stands on roots, not feet) and **floater** (a sleek hovering drone-pod on a thruster ring, fin-stabilized; `ignoresSlots: ["leg"]` — built for speed, not the old bell-hooded drifter). `tetrapod` remains the sole continuous family. The renderer ([08](08-creature-visualization.md)) is the normative reference for exactly how each plan expresses its sockets; this doc records the breeding rules, not the geometry.

### Legs are parts too, and slots can fall silent

Legs form their own homolog class exactly like hands (prototyped families: **hoofed**, **talon**, **insect** — each with hard identity invariants: the cloven hoof, the backward bird-knee with splayed toes, the chitinous zigzag). Creature plans mount leg parts at the hip, and the creature's stance height follows the leg's own length gene.

Production has since balanced the origin roster on both homologs, so every faction's issued/grown gear has more than one option: hands gained **chain_blade** (tech — a motorized rotary blade on a guide bar) and **spore_launcher** (biotech — a veined pod venting glowing spore motes); legs gained **jet_leg** (tech — a gimbaled thruster, no foot ever touches down) and **tendril_leg** (biotech — a boneless rippling pseudopod, the leg-homolog counterpart to the hand's `tentacle`). Before this the Alien Hive had zero biotech leg options and the Human Army only one tech hand; each origin now has at least two families per homolog.

Plans may also **ignore slots**: the serpentine has a leg slot in its genome but never expresses it — mirroring [08](08-creature-visualization.md)'s rule that serpentine rigs ignore leg animation channels. The payoff is the **atavism**: silent genes ride along through generations and re-express when a descendant jumps back to a legged plan. A serpentine lineage spliced into winged stock walks out on its grandmother's hooves (the prototype's Exhibit 4). This costs nothing — genomes stay full-width — and gives lineages long memory, which is exactly the kind of discovery story ([06](06-mutator-design.md) rarity & discovery) that makes breeding feel deep.

Implication for the archetype list in [06](06-mutator-design.md): `biped`, `quadruped`, and `hulking` may collapse into one tetrapod plan family with posture/bulk axes (fewer rigs to author, smoother breeding), while `serpentine`, `winged`, and `amorphous` stay discrete. Tracked as **Q11 in [12-open-questions.md](12-open-questions.md)**. For the 3D pipeline ([08](08-creature-visualization.md)), a continuous plan family means one rig whose rest pose and locomotion blend by posture — standard blend-tree territory — versus a full extra rig and animation set per discrete archetype; that authoring-cost difference is the main reason to prefer continuous families wherever anatomy allows.

The prototype's second gallery (`demo_creatures.py` → `out/creatures.svg`) shows the posture sweep on one fixed genome; random populations of bipeds, monkey-types, quadrupeds, blobs, serpentines, and winged creatures; blob × biped splice children; and the serpentine × winged atavism row.

### The heart: the supply organ (and a transplant target)

Parts cost energy to run ([05](05-component-economy.md), [17](17-factions.md)); something has to pay for them. That organ is the **heart** — a genome-level gene (a quality **tier**: faint / steady / strong / titan, plus a **vigor** parameter that tunes output within the tier) that sets the body's **circulatory capacity**. A creature is **viable** only if its heart's capacity meets the sum of its parts' upkeep demand. This is deliberately a *separate* axis from schema validity: a structurally perfect genome can still be a non-viable corpse because its heart is too small for its body.

The heart matters most under **surgical grafting** ([06](06-mutator-design.md)): sew on a limb the heart can't afford and the limb necrotizes (rejected) or, if the overload is severe, the patient dies on the table — but the part is always recovered, still usable. The heart is itself a **harvestable, transplantable part** (it rides the same six part axes so it can be expressed like any organ), which makes "grow a bigger heart, then hang the heavier arm" a real progression loop rather than a hard ceiling. Implemented in `packages/genome-core` (`heart` in the genome schema; `energy.viability()`; `surgery.ts`).

## The prototype (run it)

[`/prototype/mutator/`](../prototype/mutator/) — pure-Python, no dependencies:

```
python3 test_mutator.py   # property tests: closure, grammar, determinism, inheritance
python3 demo.py           # writes out/gallery.svg (committed sample included)
```

The gallery's three exhibits map to the claims above: **(1)** eight random genomes per family — different yet recognizable (Strategies 1, 4); **(2)** seven generations of mutation drift on one claw — identity preserved (Strategies 1, 3); **(3)** tentacle × claw splice children — shared-axis inheritance visible (Strategy 2). The property tests pin the contracts: operators are closed over valid genomes, family jumps never leave the homolog class, equal seeds give equal lineages (the server-determinism requirement, [07](07-mutator-server-architecture.md)), and splice children land between parents on every axis.

The 2D ink renderer is a stand-in for the 3D pipeline, chosen because silhouette is exactly what recognizability is about — if identity survives at sketch fidelity, the 3D version starts from proof, not hope.

## Proposed genome schema extension (v2) — decision pending (Q10)

The v1 slot allele `{partFamilyId, sizeGene, variantGene}` ([06](06-mutator-design.md)) compresses all variation into two scalars. This doc proposes widening it to:

```jsonc
{ "partFamilyId": 22, "paramGenes": [180, 40, 220, 90, 128, 60] }  // six axes, 0–255
```

Migration: `sizeGene` → `length`+`girth`; `variantGene` → remaining axes. Costs ~4 bytes per slot (genome stays well under the 400 B envelope, [07](07-mutator-server-architecture.md)). Per the normative-schema rule in [06](06-mutator-design.md), adopting this requires updating 06/07/08 together — tracked as **Q10 in [12-open-questions.md](12-open-questions.md)**, decide at the start of Phase 1 Track B ([11-roadmap.md](11-roadmap.md)).
