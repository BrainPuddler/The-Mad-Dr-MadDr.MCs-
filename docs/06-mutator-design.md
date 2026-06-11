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

## The three operators

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

## Viability & balance constraints (server-enforced)

All operators execute server-side ([07](07-mutator-server-architecture.md)). The server validates every resulting genome:

1. **Schema validity**: slots match body plan; part families fit their slots; genes in range.
2. **Power budget**: stat-point sum within Mastermind cap (1300) — nothing un-reanimatable can exist.
3. **Authored bounds**: size genes within the part family's min/max.

An operation that would produce an invalid genome returns a **failed experiment**: a fiction-friendly comic result (the Notebook sketches the abomination, the marginalia mock you, you get a small component refund). **Never silent clamping** — players must be able to trust that what the machine returns is exactly what the dice said.

## Rarity & discovery

- Part families have **discovery states**: families appear in your catalog when first obtained — from salvaged enemy genome fragments ([02](02-gameplay-overview.md)), from mutation jumps, or from event drops. The catalog is a Notebook chapter that fills in like a field guide.
- No rarity tiers on stats (anti-gacha, [01](01-vision.md)); rarity is *aesthetic and morphological* — an uncommon part family is a new shape, not a bigger number.

## Lab UX: commute mode vs. bench mode

| | Mobile (commute) | PC (bench) |
| --- | --- | --- |
| Core ops | Mutate / Splice / Graft, ≤3 taps each | Same, plus bulk queueing |
| Offline | Queue operations; they run when connectivity returns; push notification "your experiment is ready" ([07](07-mutator-server-architecture.md)) | Assumed online |
| Lineage | Simple parent/child view | Full pedigree tree, side-by-side genome diff |
| Preview | Single 3D turntable | Multi-creature compare, animation preview |

Both surfaces call the same server API; the lab state is server-side so the train-to-desk handoff is seamless (pillar 4).

## Anti-cheat posture (design level)

Clients never compute or submit genomes — they submit *operation requests* (parent IDs + fed components + RNG-free parameters) and receive server-computed results. Architecture in [07](07-mutator-server-architecture.md); match-time genome signing in [09](09-multiplayer-architecture.md).
