# 00 — Index: Mad Doctor's Construction Set Design Suite

The design documentation for **Mad Doctor's Construction Set (MadDr.MCs)** — a real-time strategy game about building movie monsters. Start with [01-vision.md](01-vision.md), then [02-gameplay-overview.md](02-gameplay-overview.md); after that, read by interest.

## Document map

| Doc | Purpose | Status |
| --- | --- | --- |
| [01-vision.md](01-vision.md) | Pitch, fiction frame, the four pillars, influences, what this is NOT | Draft |
| [02-gameplay-overview.md](02-gameplay-overview.md) | Core loops, match anatomy, victory conditions, the Menagerie, FTUE sketch | Draft |
| [03-mana-system.md](03-mana-system.md) | The Lumen Cycle, emitter polarities, affinity auras, capture rules — concrete numbers | Draft |
| [04-combat-model.md](04-combat-model.md) | Stat block, the damage formula, positioning/facing, bounded luck, salvage | Draft |
| [05-component-economy.md](05-component-economy.md) | Blood/Bones/Parts/Brains: sources, sinks, reanimation costs, anti-snowball | Draft |
| [06-mutator-design.md](06-mutator-design.md) | **The genome schema (normative)**, Mutate/Splice/Graft, brain budget, lab UX | Draft |
| [07-mutator-server-architecture.md](07-mutator-server-architecture.md) | The cross-device Mutator service: data model, API, commute mode, scaling | Draft |
| [08-creature-visualization.md](08-creature-visualization.md) | Genome → 3D: socketed parts, blend shapes, uber-shader, perf budgets | Draft |
| [09-multiplayer-architecture.md](09-multiplayer-architecture.md) | Server-authoritative netcode, mobile resilience, matchmaking, anti-cheat | Draft |
| [10-engine-evaluation.md](10-engine-evaluation.md) | Unity vs Godot vs Unreal — scored matrix, recommendation, validation spike | Draft |
| [11-roadmap.md](11-roadmap.md) | Risk-first phases from paper playtest to launch; de-scope ladder | Draft |
| [12-open-questions.md](12-open-questions.md) | Open questions with decide-by dates; append-only decision log | Living |
| [13-lens-review.md](13-lens-review.md) | Book of Lenses design review: uniqueness, fun, marketability ratings | Reviewed |
| [14-ip-licensing.md](14-ip-licensing.md) | Classic-monster IP analysis, public-domain timeline, authoring guardrails | Reviewed |
| [15-part-genetics.md](15-part-genetics.md) | How parts mutate & crossbreed yet stay recognizable; prototyped in [`/prototype/mutator/`](../prototype/mutator/) | Draft |
| [16-brains-behavior-command.md](16-brains-behavior-command.md) | Brain genes → behavior: commanders, loyalty, rebellion; prototyped in [`/prototype/mutator/command.py`](../prototype/mutator/command.py) | Draft |

**Status legend**: *Draft* (numbers are v0.1 proposals) → *Reviewed* (survived Phase-0 paper playtest and a read-through) → *Locked* (implementation depends on it; changes require a decision-log entry in [12](12-open-questions.md)).

## Conventions

- **Cross-references** are relative links; the genome schema in [06](06-mutator-design.md) is *normative* — docs 04, 07, and 08 consume it and must be updated together with it.
- **Tuning numbers** are tagged "v0.1" and consolidated in per-doc tuning tables; they are proposals to be validated in the Phase-0/1 playtests ([11-roadmap.md](11-roadmap.md)), not commitments.
- Terms below are defined **only here**; other docs link rather than redefine.

## Glossary

| Term | Definition |
| --- | --- |
| **Affinity** | A monster's Lumen coupling gene: `solar`, `lunar`, or `neutral`. Decides aura buffs by phase ([03](03-mana-system.md)) |
| **Allele** | One part slot's gene triple `{partFamilyId, sizeGene, variantGene}` ([06](06-mutator-design.md)) |
| **Archetype / body plan** | One of 6 base skeletons (biped, quadruped, serpentine, winged, hulking, amorphous); selects rig, animation set, and slot list |
| **Aura** | The 3-hex radius around an emitter where affinity modifiers apply ([03](03-mana-system.md)) |
| **Brain budget** | The stat-point and quirk cap imposed by a monster's brain quality (Dim/Average/Gifted/Mastermind) ([06](06-mutator-design.md)) |
| **Commute mode** | Offline-queued Mutator operations from the mobile lab; results delivered by push ([07](07-mutator-server-architecture.md)) |
| **Components** | The four material resources: **Blood** (upkeep), **Bones** (structure), **Body Parts** (capability + feedstock), **Brains** (gating) ([05](05-component-economy.md)) |
| **Dominion** | Victory by holding ≥60% of emitters for one full Lumen Cycle ([02](02-gameplay-overview.md)) |
| **Emitter** | A capturable map point that banks mana to its owner at a rate set by its polarity and the current phase ([03](03-mana-system.md)) |
| **Failed experiment** | The comic, fiction-friendly result of an invalid Mutator operation — never silent clamping ([06](06-mutator-design.md)) |
| **Genome** | The complete, immutable, server-signed description of a creature (~200–400 B). The normative schema lives in [06](06-mutator-design.md) |
| **Genome fragment** | A salvage drop that reveals one enemy part family to your catalog ([04](04-combat-model.md)) |
| **Graft** | The deterministic Mutator operator: pay parts to set a slot directly ([06](06-mutator-design.md)) |
| **Lumen Cycle** | The 4-minute Day→Dusk→Night→Dawn match clock driving emitter output and affinity buffs ([03](03-mana-system.md)) |
| **Mana** | The *energy* currency, earned only from emitters; pays reanimation surges and abilities. Distinct from components (*material*) ([03](03-mana-system.md)) |
| **Menagerie** | The ≤12 active monster designs you can reanimate in a match ([02](02-gameplay-overview.md)) |
| **Moon dial** | The always-public HUD clock showing the current Lumen phase and the 10-second transition warning |
| **Mutate** | The one-parent Mutator operator: biased random mutation, steered by fed components ([06](06-mutator-design.md)) |
| **The Mutator** | The server-side genetic-algorithm laboratory: Mutate, Splice, Graft ([06](06-mutator-design.md), [07](07-mutator-server-architecture.md)) |
| **The Notebook** | Dr. Frankenstein's inherited journals — the game's unifying UI metaphor and your collection record ([01](01-vision.md)) |
| **Part family** | A catalog entry of part meshes (claw-arm, bat-wing…) with slot/archetype compatibility and authored scale bounds ([08](08-creature-visualization.md)) |
| **Power budget** | A genome's normalized stat-point sum; capped by brain quality, read by matchmaking ([06](06-mutator-design.md), [09](09-multiplayer-architecture.md)) |
| **Quirk** | A minor rule-bending genome trait (regeneration, fear aura…); max 2 per monster ([06](06-mutator-design.md)) |
| **Reanimation** | Fielding a Menagerie design at the Vat: component bill + mana surge + 5–20 s build time ([05](05-component-economy.md)) |
| **Salvage** | The 40–60% component drop on a monster's death hex, lootable by either side for 15 s ([04](04-combat-model.md)) |
| **Splice** | The two-parent Mutator operator: crossover breeding, with low-odds cross-archetype hybrids ([06](06-mutator-design.md)) |
| **Territory tick** | The per-controlled-hex blood trickle (+0.1 blood/min) ([05](05-component-economy.md)) |
| **Vat** | Your base: the reanimation point and the destroy-to-win target ([02](02-gameplay-overview.md)) |
