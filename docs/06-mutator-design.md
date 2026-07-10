# 06 — The Mutator: Genetic Algorithm & Lab Design

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*), 4 (*Design anywhere*) · Terms: [glossary](00-index.md#glossary).

> **Normative schema notice.** The genome schema in this document is the single source of truth for creature representation. [07-mutator-server-architecture.md](07-mutator-server-architecture.md) stores and transports it; [08-creature-visualization.md](08-creature-visualization.md) renders it; [04-combat-model.md](04-combat-model.md) derives stats from it. Any change to this schema requires updating all three consumers in the same revision.
>
> A v2 extension of the slot allele (six shared parameter axes, prototyped and working) is proposed in [15-part-genetics.md](15-part-genetics.md) — adoption tracked as Q10 in [12-open-questions.md](12-open-questions.md).

## The fantasy (the El-Fish promise)

Feed the machine. Drop in a creature — or just an arm, a brain, a jar of blood — pull the lever, and get back something *surprising but recognizably descended* from what you gave it. The player should feel like a gleeful experimenter, not a slot-machine puller: inputs visibly bias outputs, lineage is browsable, and total control is available (at a price) via grafting.

## The genome

A creature is fully described by a **genome**: a structured, versioned record of ~200–400 bytes. The genome is the only thing stored, transmitted, and rendered — there is no per-creature art or hand-tuned stat data.

```jsonc
{
  "genomeVersion": 1,
  "creatureId": "uuid",          // assigned by server; genomes are immutable
  "parentIds": ["uuid", "uuid"], // 0 (primordial), 1 (mutation), or 2 (splice)
  "bodyPlan": "biped",           // archetype gene, see below
  "slots": {                      // part-slot alleles, slot set defined by bodyPlan
    "head":   { "partFamilyId": 14, "sizeGene": 180, "variantGene": 40 },
    "torso":  { "partFamilyId": 3,  "sizeGene": 120, "variantGene": 200 },
    "armL":   { "partFamilyId": 22, "sizeGene": 255, "variantGene": 90 },
    "armR":   { "partFamilyId": 7,  "sizeGene": 100, "variantGene": 90 },
    "legL":   { "partFamilyId": 9,  "sizeGene": 128, "variantGene": 15 },
    "legR":   { "partFamilyId": 9,  "sizeGene": 128, "variantGene": 15 }
  },
  "statGenes": {                  // 0–255 each; map to combat stats per archetype curve
    "vitality": 140, "power": 90, "armor": 30, "reach": 60,
    "speed": 110, "ferocity": 100, "cunning": 25
  },
  "affinity": "lunar",           // solar | lunar | neutral — couples to the Lumen Cycle (03)
  "pigment": { "primary": 130, "secondary": 40, "accent": 220, "pattern": 5 },
  "quirks": [ "regeneration" ],  // 0–2 minor traits
  "brainQuality": "average"      // dim | average | gifted | mastermind — set at reanimation, gates budget
}
```

### Genes in detail

- **Body plan** (archetype gene): one of **6 archetype skeletons** at launch — `biped`, `quadruped`, `serpentine`, `winged`, `hulking`, `amorphous`. The body plan selects the rig, the animation set, and the slot list ([08](08-creature-visualization.md)). Slot lists range from 4 (serpentine: head, torso, tail, appendage) to 10 (hulking: head, torso, 4 limbs, 2 shoulders, tail, appendage).
- **Part-slot alleles**: each slot holds `{partFamilyId, sizeGene, variantGene}`.
  - `partFamilyId` indexes the **part catalog** — claw-arm, hammer-fist, bat-wing, lantern-jaw, etc. Each family declares which slots and body plans it fits.
  - `sizeGene` (0–255) scales the part within artist-authored min/max bounds.
  - `variantGene` (0–255) selects/weights blend shapes (gnarled / sleek / bloated…) ([08](08-creature-visualization.md)).
- **Stat genes**: seven 0–255 scalars mapping to the [04](04-combat-model.md) stat block through **per-archetype curves**. Example curve (biped, vitality): `HP = 50 + (gene/255)^1.2 × 280` — hulking uses a steeper curve, serpentine a flatter one, so the same gene means different things in different bodies. One published curve per archetype per stat lives in the tuning appendix of [04](04-combat-model.md).
- **Affinity gene**: `solar | lunar | neutral`. Couples the creature to the Lumen Cycle ([03](03-mana-system.md)).
- **Pigment genes**: 3-channel palette indices + a pattern gene driving the uber-shader tint masks ([08](08-creature-visualization.md)). Cosmetic only — never stats.
- **Quirk genes**: 0–2 minor rule-bending traits from a small catalog (launch set: `regeneration` — 1% max HP/s out of combat; `fearAura` — adjacent enemies −5% power; `nightEyes` — no vision penalty at Night; `gravedigger` — +50% salvage harvest rate; `thickHide` — +2 armor vs. ranged). Quirks are deliberately small; they flavor, never define.

### The brain budget

**Brain quality** is the power governor and the reason Brains are precious ([05](05-component-economy.md)):

| Brain | Total stat-gene points allowed (sum of 7 genes) | Quirks allowed | Reanimation time |
| --- | --- | --- | --- |
| Dim | ≤ 600 | 0 | 5 s |
| Average | ≤ 850 | 1 | 10 s |
| Gifted | ≤ 1100 | 2 | 15 s |
| Mastermind | ≤ 1300 | 2 | 20 s |

A design whose genome exceeds the equipped brain's budget cannot be reanimated with that brain. The **power budget** (stat-point sum, normalized) is also what matchmaking reads ([09](09-multiplayer-architecture.md)) — Mutator balance and matchmaking interlock through this one number.

## The Workshop: building a monster from resources

This is the section the creator asked for by name: the resource-facing side of the Lab, rolled into one place. Every tool below — Mutate, Splice, Graft, Megabrain Augmentation, and Cannibalize — is a way of spending or recovering the same currencies ([05-component-economy.md](05-component-economy.md)): Bones, Body Parts, Brains, Blood. "I want a tank, I need a lot of Bones" isn't a slogan here; the Workshop shows it to you as a live number while you build, not something you discover at the reanimation screen — and if you don't have the Bones, the Workshop is also where you get them: gather more, or **Cannibalize** a design you don't need anymore.

### The bill: Bones, Parts, and a Brain

[05-component-economy.md](05-component-economy.md) states plainly that Bone cost "scales with Vitality, Armor, and size" — this is the formula that claim was always implicitly obeying, made explicit (v0.1):

```
Bones = 4 × sizeClass + 0.1 × Vitality + 2 × Armor
```

`sizeClass` (1–4) is the same size-mass bucket [04-combat-model.md](04-combat-model.md) already uses for `turnTime`. Checked against doc 05's existing sample table (Vitality is published there; Armor isn't, so the fit below is approximate where marked):

| Archetype | sizeClass | Vitality | Armor | Formula | Table's actual Bones |
| --- | --- | --- | --- | --- | --- |
| Shambler (biped) | 1 | 150 | ~0.5* | 4 + 15 + 1 ≈ 20 | 20 |
| Stitched Brute (hulking) | 4 | 320 | 6* | 16 + 32 + 12 = 60 | 60 (exact) |
| Winged Horror (winged) | 2 | 120 | ~2.5* | 8 + 12 + 5 ≈ 25 | 25 |

*Armor isn't published in doc 05's table today; these are the values the formula implies, not independently sourced — recommend publishing Armor alongside the other stats when this formula ships.

Worked example — why "a tank-type unit costs a lot of bone" is a formula, not a slogan: a max-stat build (sizeClass 4, Vitality 400, Armor 10) costs `16 + 40 + 20 = 76` Bones; a min-stat build (sizeClass 1, Vitality 50, Armor 0) costs `4 + 5 + 0 = 9` Bones — an **~8.4× spread** between the lightest and heaviest thing you can breed.

This is a **readout, not a parallel construction system**: it's what Mutate/Splice/Graft below already implicitly charge (doc 05's component bills), shown as a live number next to the stat sliders as you build.

**Reconciliation note**: [17-factions.md](17-factions.md) already ships a Phase-2 Structure-class formula, `structure = 2 + 8·bulk`, for the eventual sparse-material wallet. That formula and this one are not yet reconciled — this is the v0.1 stand-in for today's `{blood, bones}` wallet; 17's is the forward-looking generalization. Tracked as **Q18** ([12-open-questions.md](12-open-questions.md)).

### Mutate — one parent, biased randomness

Feed one creature plus **optional components**; components bias the outcome (the El-Fish move):

- Base: each gene independently mutates with **~5% probability**. Scalar genes drift by Gaussian noise (σ = 12 on the 0–255 scale); slot alleles have a **1% chance of a part-family jump** (re-roll within compatible families); body plan never mutates (that's Splice territory).
- **Component bias**: feeding a Body Part of family *F* triples mutation probability on limb slots and makes any family jump land on *F* with 50% weight. Feeding a Brain raises stat-gene mutation rates ×2; feeding Blood biases pigment/quirk genes. The feeding UI shows these biases in plain language ("this arm makes limb changes likely").
- Cost: components fed are consumed + a flat Mutator fee in blood ([05](05-component-economy.md)).

### Splice — two parents, crossover

- **Uniform crossover on slots**: each slot allele comes whole from one parent (compatible-slot mapping when slot lists differ).
- **Blended scalars**: stat, pigment genes = per-gene random lerp of parents ±10% hybrid noise.
- **Same body plan**: always succeeds. **Cross-archetype splice**: 25% success; on success the offspring takes either parent's body plan and remaps slots — this is the hybrid-jackpot moment (a winged hulk!). On failure: a comic **failed experiment** (see Viability) and the fee is partially refunded; *parents are never harmed*.
- Affinity: child inherits one parent's affinity 45/45, 10% flips to the third option.

### Graft — deterministic, the control valve

Pay Body Parts to set one slot's allele directly — choose the family, set size/variant sliders. Costs **3× the Mutate fee** plus the part itself. Graft exists so the GA never feels like a pure slot machine (pillar 1 demands ownership, not gambling): when you need *that claw* on *that monster*, you can have it — expensively.

This is the **lab** graft: composing a genome on the bench, where viability is checked *before* you commit. Its violent twin is **surgery** below.

### Megabrain Augmentation — a Mastermind-only upgrade

A fourth tool, narrower than the three above — not one of the historical "three operators," but the same server-computed, deterministic-cost shape as Graft.

**Gate**: Mastermind-tier brains only. **Cost**: 100 harvested **Brains** (player-facing; internal field name `greyMatter`) — a bulk resource harvested from Citizens and vanquished foes ([20-harvest-and-repair.md](20-harvest-and-repair.md)). This shares its display name with, but is mechanically separate from, the discrete Brain tier-item above (`brainQuality` in the genome — the two-senses disambiguation lives in [05](05-component-economy.md)) — this operation spends the bulk resource only, and never touches or consumes the genome's actual Brain tier-item. **Effect**: a one-time, flat `capacityBonus: 7.2` added to the genome, feeding the Capacity term in [16-brains-behavior-command.md](16-brains-behavior-command.md)'s command-capacity formula — the mechanism that lets a single Mastermind commander actually hold a large platoon (the worked example lives in doc 16).

Mechanically identical in shape to Graft: server-computed, deterministic (no RNG), produces a new immutable child genome (`parentIds: [thisGenome]`). Not repeatable in v0.1 — one augmentation per genome, a deliberate anti-snowball dial rather than an assumed-safe unlimited stack (open, **Q21**).

**Two structural costs, stated plainly, not glossed over:**
- Adding `capacityBonus` to the genome is a real schema change and trips this doc's own normative-schema notice at the top — [04](04-combat-model.md), [07](07-mutator-server-architecture.md), and [08](08-creature-visualization.md) all need the co-update in the same revision that ships this.
- `command`/`will` already feed the [09-multiplayer-architecture.md](09-multiplayer-architecture.md) matchmaking power budget. A capacity bonus left out of that same sum is a free-power hole — recommend it contribute to the power-budget sum proportionally, not left unpriced. Open, **Q21**.

### Cannibalize — scrapping your own designs for parts

The Workshop's other direction: instead of feeding it fresh materials, feed it one of your own genomes. **Cannibalize** retires an owned design — Menagerie-active or bench-only, your choice — and converts it back into Bones, Body Parts, and a Brain-tier roll, deposited straight into the same wallet the Workshop spends from. Want a tank? Recycle what's in the way.

**Formula (v0.1)**: 50% of the design's own Bones-cost bill (above, "The bill") in Bones, 50% of its Body Parts, and its Brain tier salvages at the existing 50% rate ([05](05-component-economy.md)) — the same numbers doc 05 already uses for corpse salvage, reused rather than inventing a new rate.

**The genome isn't deleted.** Per this doc's own immutable-row rule ([07](07-mutator-server-architecture.md)), a cannibalized genome is marked **retired**, not erased — it drops out of the Notebook's active catalog and can never be Menagerie-loaded or bred from again, but its lineage stays intact for any descendant's pedigree view.

**Worked example — "I want a tank, so I scrap what's in the way":** four obsolete Shambler-tier designs (20 Bones bill each) cannibalized return 10 Bones apiece — 40 Bones pooled, over half of a Stitched-Brute-class 76-Bones tank build (above) without waiting on a fresh harvest.

**In-match twin**: the same fantasy, mid-battle, on a *living* creature instead of a bench design — recall and dismantle a fielded creature at the Vat for immediate in-match resources. Full spec in [20-harvest-and-repair.md](20-harvest-and-repair.md), since it's a real-time Vat command, not a Mutator-service op — this doc's Cannibalize only ever touches genomes you own, never something on the battlefield.

Mechanically identical in shape to Graft and Megabrain Augmentation: server-computed, deterministic — no RNG beyond the existing Brain-salvage roll — one new REST op ([07](07-mutator-server-architecture.md)).

**Anti-snowball note, stated plainly**: Cannibalize always returns *less* than a design cost to build (50%, matching every other salvage rate in this game), so build-then-immediately-scrap is a guaranteed net loss — the value is recycling designs that already did their job (an FTUE Shambler once you've bred better bipeds), not laundering resources for free.

## Grafting as surgery: cut parts off, sew them on

The literal fantasy: **cut a body part off one creature and sew it onto another.** A creature is not a sealed unit — it is a bag of reusable parts, and so is every corpse on the battlefield ([05](05-component-economy.md), "nothing is wasted").

- **Harvest.** Cut a part off a donor (living or dead). The slot heals to a **stump**; the part comes away as a durable **part item** that carries its genes intact and expresses identically wherever it is sewn next. You can also harvest the **heart** — but that leaves a corpse.
- **The heart is the gate.** Every part demands energy to run ([05](05-component-economy.md), [17](17-factions.md)); the **heart** is the organ that supplies it, with a finite **circulatory capacity** (set by its tier and vigor — a heritable, mutable, *transplantable* organ). Sewing a part on is only safe if the heart can drive the result:
  - **Within capacity** → the graft takes.
  - **Over capacity, within the shock factor** → the new limb **necrotizes and is rejected** — it doesn't take, but the creature survives and the part is returned.
  - **Far over capacity** → the **heart stops: the creature dies on the table.**
- **Nothing is wasted.** In *both* failure cases the part is handed back **still usable** — a botched operation costs you the operation, never the part. (A failed *lab* graft is a comic "failed experiment"; a failed *surgery* is a body on the slab and a part back in the tray.)
- **The counterplay is a bigger heart.** Want to hang a titan's arm on a small frame? Transplant in a stronger heart first (`sewHeart`) — the old heart pops out as a reusable item. "If the heart isn't big enough, the limb or the creature dies" is therefore a real resource decision, not a dead end: grow the supply to match the demand.

Implemented in `packages/genome-core` (`surgery.ts`, `energy.ts`): `harvestPart`/`harvestHeart`, `sewPart`/`sewHeart`, `viability()`.

## Viability & balance constraints (server-enforced)

All operators execute server-side ([07](07-mutator-server-architecture.md)). The server validates every resulting genome:

1. **Schema validity**: slots match body plan; part families fit their slots; genes in range.
2. **Power budget**: stat-point sum within Mastermind cap (1300) — nothing un-reanimatable can exist.
3. **Authored bounds**: size genes within the part family's min/max.
4. **Viability (heart vs. body)**: the heart's circulatory capacity must meet the body's total upkeep demand. This is *separate* from schema validity — a structurally valid genome can still be a non-viable corpse — and it is what the surgery gate above enforces on the table.

An operation that would produce an invalid genome returns a **failed experiment**: a fiction-friendly comic result (the Notebook sketches the abomination, the marginalia mock you, you get a small component refund). **Never silent clamping** — players must be able to trust that what the machine returns is exactly what the dice said.

## Rarity & discovery

- Part families have **discovery states**: families appear in your catalog when first obtained — from salvaged enemy genome fragments ([02](02-gameplay-overview.md)), from mutation jumps, or from event drops. The catalog is a Notebook chapter that fills in like a field guide.
- No rarity tiers on stats (anti-gacha, [01](01-vision.md)); rarity is *aesthetic and morphological* — an uncommon part family is a new shape, not a bigger number.

## Workshop UX: commute mode vs. bench mode

| | Mobile (commute) | PC (bench) |
| --- | --- | --- |
| Core ops | Mutate / Splice / Graft / Cannibalize, ≤3 taps each | Same, plus bulk queueing |
| Offline | Queue operations; they run when connectivity returns; push notification "your experiment is ready" ([07](07-mutator-server-architecture.md)) | Assumed online |
| Lineage | Simple parent/child view | Full pedigree tree, side-by-side genome diff |
| Preview | Single 3D turntable | Multi-creature compare, animation preview |

Both surfaces call the same server API; the Workshop's state is server-side so the train-to-desk handoff is seamless (pillar 4).

## Anti-cheat posture (design level)

Clients never compute or submit genomes — they submit *operation requests* (parent IDs + fed components + RNG-free parameters) and receive server-computed results. Architecture in [07](07-mutator-server-architecture.md); match-time genome signing in [09](09-multiplayer-architecture.md).
