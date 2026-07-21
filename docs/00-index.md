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
| [06-mutator-design.md](06-mutator-design.md) | **The genome schema (normative)**, brain budget, **the Workshop**: Mutate/Splice/Graft/Megabrain Augmentation/Cannibalize, resource-driven construction | Draft |
| [07-mutator-server-architecture.md](07-mutator-server-architecture.md) | The cross-device Mutator service: data model, API, commute mode, scaling | Draft |
| [08-creature-visualization.md](08-creature-visualization.md) | Genome → 3D: socketed parts, blend shapes, uber-shader, perf budgets | Draft |
| [09-multiplayer-architecture.md](09-multiplayer-architecture.md) | Server-authoritative netcode, mobile resilience, matchmaking, anti-cheat | Draft |
| [10-engine-evaluation.md](10-engine-evaluation.md) | Unity vs Godot vs Unreal — scored matrix, recommendation, validation spike | Draft |
| [11-roadmap.md](11-roadmap.md) | Risk-first phases from paper playtest to launch; de-scope ladder | Draft |
| [12-open-questions.md](12-open-questions.md) | Open questions with decide-by dates; append-only decision log | Living |
| [13-lens-review.md](13-lens-review.md) | Book of Lenses design review: uniqueness, fun, marketability ratings | Reviewed |
| [14-ip-licensing.md](14-ip-licensing.md) | Classic-monster IP analysis, public-domain timeline, authoring guardrails | Reviewed |
| [15-part-genetics.md](15-part-genetics.md) | How parts mutate & crossbreed yet stay recognizable; prototyped in [`/prototype/mutator/`](../prototype/mutator/) | Draft |
| [16-brains-behavior-command.md](16-brains-behavior-command.md) | Brain genes → behavior: commanders, loyalty, rebellion, berserk; prototyped in [`/prototype/mutator/command.py`](../prototype/mutator/command.py) | Draft |
| [17-factions.md](17-factions.md) | Factions as expression profiles: human army (issued tech, rout/rally), alien hive (biotech, queen cohesion); prototyped in [`/prototype/mutator/factions.py`](../prototype/mutator/factions.py) | Draft |
| [18-city-battlefields.md](18-city-battlefields.md) | Unity battlefield layer: 5 km match scale, procedural city generation, destructible buildings, the engagement-zone LOD scheme, Unity↔Lab integration | Draft |
| [19-citizens.md](19-citizens.md) | Civilian city population: age/body type, aggression & weapon access, sync-tier LOD; distinct from the Human Army faction | Draft |
| [20-harvest-and-repair.md](20-harvest-and-repair.md) | Citizen and vanquished-foe harvesting (Collection Stations, faction corpse salvage, hybrid-monster parts), resource-gated construction (Bones cost, Megabrain Augmentation), field Repair, and in-match Cannibalize | Draft |
| [21-world-upgrade-report.md](21-world-upgrade-report.md) | Unity battlefield presentation upgrade: world architecture report, sculpted terrain, 1950s building/road dressing, and its follow-up batches | Living |
| [22-economy-system.md](22-economy-system.md) | The living economy: per-unit onboard blood/bone/brain pools (capacities, damage spill, efficiency floors), medic + harvester units, storage structures (blood banks, bone piles), factories (the Stitchworks), fun-first/never-annoying design contract | Draft |
| [23-rts-master-build-plan.md](23-rts-master-build-plan.md) | **Execution plan** for the full-RTS expansion: three-faction combat + hybrid 4th category, themed bases & building rosters, Blood/Fuel/Ichor economy, RPG levels/gear/Fusion, flocking control, Lumen-clock time-of-day bonuses, roaming power-ups, NY/Paris/Montreal 1950s regions, Mafia-school graphics ladder, deterministic-lockstep 4v4 netcode. **§13 = four-expert panel review + accepted amendments.** | Execution plan (panel-reviewed) |

**Status legend**: *Draft* (numbers are v0.1 proposals) → *Reviewed* (survived Phase-0 paper playtest and a read-through) → *Locked* (implementation depends on it; changes require a decision-log entry in [12](12-open-questions.md)).

## Conventions

- **Cross-references** are relative links; the genome schema in [06](06-mutator-design.md) is *normative* — docs 04, 07, and 08 consume it and must be updated together with it.
- **Tuning numbers** are tagged "v0.1" and consolidated in per-doc tuning tables; they are proposals to be validated in the Phase-0/1 playtests ([11-roadmap.md](11-roadmap.md)), not commitments.
- Terms below are defined **only here**; other docs link rather than redefine.

## Glossary

| Term | Definition |
| --- | --- |
| **Affinity** | A monster's Lumen coupling gene: `solar`, `lunar`, or `neutral`. Decides aura buffs by phase ([03](03-mana-system.md)) |
| **Aggression** | A Citizen's 0–1 fight/flee propensity, rolled independently of age and body type ([19](19-citizens.md)) |
| **Allele** | One part slot's gene triple `{partFamilyId, sizeGene, variantGene}` ([06](06-mutator-design.md)) |
| **Archetype / body plan** | One of 6 base skeletons (biped, quadruped, serpentine, winged, hulking, amorphous); selects rig, animation set, and slot list |
| **Aura** | The 3-hex radius around an emitter where affinity modifiers apply ([03](03-mana-system.md)) |
| **Blood Bank** | A player-built, Ghoul-constructed storage structure: refills nearby friendly units' onboard blood and extends the Blood wallet cap; distinct from doc 17's hospital world-source node ([22](22-economy-system.md)) |
| **Bone Pile** | A player-built storage structure: refills onboard bone (armour stock) and extends the Bones wallet cap ([22](22-economy-system.md)) |
| **Brain Trust** | A player-built storage structure: refills onboard grey-matter charge and extends the greyMatter wallet cap ([22](22-economy-system.md)) |
| **Brain budget** | The stat-point and quirk cap imposed by a monster's brain quality (Dim/Average/Gifted/Mastermind) ([06](06-mutator-design.md)) |
| **Brains** | Player-facing term for two things: the discrete, one-per-monster Brain tier-item that gates the genome's stat budget (see **Brain budget**, above); and the bulk, counted resource harvested from Citizens and vanquished foes, individually weak, whose only sink is Megabrain Augmentation — internally tracked as the distinct field `greyMatter` so the two never collide in code ([05](05-component-economy.md), [20](20-harvest-and-repair.md)) |
| **Cannibalize** | Retire one of your own genomes at the Workshop, or recall a living fielded creature at the Vat mid-match, converting it back into Bones/Body Parts/a Brain-tier roll at 50% of its build cost — the same operation in two contexts ([06](06-mutator-design.md), [20](20-harvest-and-repair.md)) |
| **Citizen** | A non-combatant city NPC — age, body type, and personality generated, never bred; distinct from the Human Army faction ([17](17-factions.md)) ([19](19-citizens.md)) |
| **Collection Station** | A capturable structure (captures like an emitter) that converts Citizen deaths within its radius into banked Blood/Bones/Brains for its controller; one per Community Hub ([18](18-city-battlefields.md), [20](20-harvest-and-repair.md)) |
| **Community Hub** | A high-population landmark building type — hospital, school, or old-age home — generated at 4× standard Citizen density ([18](18-city-battlefields.md), [19](19-citizens.md), [20](20-harvest-and-repair.md)) |
| **Commute mode** | Offline-queued Mutator operations from the mobile lab; results delivered by push ([07](07-mutator-server-architecture.md)) |
| **Components** | The four material resources: **Blood** (upkeep), **Bones** (structure), **Body Parts** (capability + feedstock), **Brains** (gating) ([05](05-component-economy.md)) |
| **Distant skyline** | The outermost engagement-zone LOD tier: pure visual backdrop beyond ~1 km of any live engagement, no simulation ([18](18-city-battlefields.md)) |
| **Dominion** | Victory by holding ≥60% of emitters for one full Lumen Cycle ([02](02-gameplay-overview.md)) |
| **Emitter** | A capturable map point that banks mana to its owner at a rate set by its polarity and the current phase ([03](03-mana-system.md)) |
| **Engagement zone** | The ~150–200 m radius around live combat that runs full server-authoritative simulation on a city battlefield; the existing ~60-entity budget applies per-zone, not per-map ([18](18-city-battlefields.md)) |
| **Failed experiment** | The comic, fiction-friendly result of an invalid Mutator operation — never silent clamping ([06](06-mutator-design.md)) |
| **Genome** | The complete, immutable, server-signed description of a creature (~200–400 B). The normative schema lives in [06](06-mutator-design.md) |
| **Genome fragment** | A salvage drop that reveals one enemy part family to your catalog ([04](04-combat-model.md)) |
| **Ghoul** | The gatherer unit class: auto-scavenges corpse salvage and blood spills into the wallet, and constructs storage structures and the Stitchworks; unarmed, flees when threatened ([22](22-economy-system.md)) |
| **Graft** | The deterministic Mutator operator: pay parts to set a slot directly ([06](06-mutator-design.md)) |
| **Local city** | The mid engagement-zone LOD tier (~1 km): buildings static, Citizens run as client-side crowd only ([18](18-city-battlefields.md)) |
| **Lumen Cycle** | The 4-minute Day→Dusk→Night→Dawn match clock driving emitter output and affinity buffs ([03](03-mana-system.md)) |
| **Mana** | The *energy* currency, earned only from emitters; pays reanimation surges and abilities. Distinct from components (*material*) ([03](03-mana-system.md)) |
| **Megabrain Augmentation** | A Mastermind-only, one-time Mutator operation: 100 harvested Brains for a flat +7.2 Capacity bonus — the doctors' "mind control" fiction: one swollen brain possessing a 40-strong platoon ([06](06-mutator-design.md), [16](16-brains-behavior-command.md)) |
| **Menagerie** | The ≤12 active monster designs you can reanimate in a match ([02](02-gameplay-overview.md)) |
| **Moon dial** | The always-public HUD clock showing the current Lumen phase and the 10-second transition warning |
| **Mutate** | The one-parent Mutator operator: biased random mutation, steered by fed components ([06](06-mutator-design.md)) |
| **Onboard pools** | The three per-unit resource tanks — blood (fuel), bone (armour stock), brain (grey-matter charge) — with genome-derived capacities; drained by fighting and damage, refilled by eating/storage/medics, degrading efficiency toward floors when empty ([22](22-economy-system.md)) |
| **The Mutator** | The server-side genetic-algorithm laboratory: Mutate, Splice, Graft ([06](06-mutator-design.md), [07](07-mutator-server-architecture.md)) |
| **The Notebook** | Dr. Frankenstein's inherited journals — the game's unifying UI metaphor and your collection record ([01](01-vision.md)) |
| **Part family** | A catalog entry of part meshes (claw-arm, bat-wing…) with slot/archetype compatibility and authored scale bounds ([08](08-creature-visualization.md)) |
| **Power budget** | A genome's normalized stat-point sum; capped by brain quality, read by matchmaking ([06](06-mutator-design.md), [09](09-multiplayer-architecture.md)) |
| **Quirk** | A minor rule-bending genome trait (regeneration, fear aura…); max 2 per monster ([06](06-mutator-design.md)) |
| **Reanimation** | Fielding a Menagerie design at the Vat: component bill + mana surge + 5–20 s build time ([05](05-component-economy.md)) |
| **Repair** | An in-match Vat command that restores a living creature's lost HP for a Bone+Blood cost scaled to missing HP; distinct from surgery/graft and the `regeneration` quirk ([20](20-harvest-and-repair.md)) |
| **Salvage** | The 40–60% component drop on a monster's death hex, lootable by either side for 15 s ([04](04-combat-model.md)) |
| **Sawbones** | The medic unit class: auto-triage AI, channels field Repair at half Vat speed and transfuses onboard pools from the wallet; one medic per patient, no attack ([22](22-economy-system.md)) |
| **Splice** | The two-parent Mutator operator: crossover breeding, with low-odds cross-archetype hybrids ([06](06-mutator-design.md)) |
| **Stitchworks** | A player-built forward factory: reanimates Menagerie designs at 1.5× Vat build time with a 5-deep queue, and channels match-scoped field augments (+50% to an onboard capacity) ([22](22-economy-system.md)) |
| **Structure HP** | A building's Vitality-equivalent stat, resolved through the existing damage formula ([04](04-combat-model.md), [18](18-city-battlefields.md)) |
| **Style preset** | An authored parameter set (road layout, density, facade/prop kit) skinning the shared city generator per theme ([18](18-city-battlefields.md)) |
| **Territory tick** | The per-controlled-hex blood trickle (+0.1 blood/min) ([05](05-component-economy.md)) |
| **Vat** | Your base: the reanimation point and the destroy-to-win target ([02](02-gameplay-overview.md)) |
| **The Workshop** | The resource-facing section of the Lab: build a monster from Bones/Parts/Brains/Blood, priced live as you build. Its tools are Mutate, Splice, Graft, Megabrain Augmentation, and Cannibalize ([06](06-mutator-design.md)) |
