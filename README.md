# Mad Doctor's Construction Set (MadDr.MCs)

A real-time strategy game about building movie monsters. You are the nephew of Dr. Frankenstein, bequeathed his notebooks. From blood, bones, body parts, and brains you reanimate monsters and deploy them to conquer territory on a battlefield whose mana emitters wax and wane with the time of day (an Archon homage). Between battles, a server-side genetic algorithm — **the Mutator** — lets you breed, splice, and mutate your creations from your phone on the commute or your PC at home, El-Fish style. No two players ever field the same army.

**Status: design + early code.** The complete design documentation lives in [`docs/`](docs/). The exploratory Python prototype lives in [`prototype/mutator/`](prototype/mutator/) (sample output: [gallery.svg](prototype/mutator/out/gallery.svg)). Production code begins with [`packages/genome-core`](packages/genome-core/) — the TypeScript genome library (schema v2, operators, validation, behavior expression; engine- and graphics-independent by design).

## Start here

Read [docs/01-vision.md](docs/01-vision.md), then [docs/02-gameplay-overview.md](docs/02-gameplay-overview.md). After that, browse by interest via the map below or the full index at [docs/00-index.md](docs/00-index.md).

## Document map

| Doc | Purpose |
| --- | --- |
| [00-index.md](docs/00-index.md) | Doc map, conventions, glossary |
| [01-vision.md](docs/01-vision.md) | Pitch, fiction frame, the four pillars |
| [02-gameplay-overview.md](docs/02-gameplay-overview.md) | Core loops, match anatomy, victory conditions |
| [03-mana-system.md](docs/03-mana-system.md) | The Lumen Cycle, emitters, affinity auras |
| [04-combat-model.md](docs/04-combat-model.md) | The damage formula: positioning, strength, bounded luck |
| [05-component-economy.md](docs/05-component-economy.md) | Blood, Bones, Body Parts, Brains |
| [06-mutator-design.md](docs/06-mutator-design.md) | The genome schema and the genetic-algorithm lab |
| [07-mutator-server-architecture.md](docs/07-mutator-server-architecture.md) | The cross-device Mutator service |
| [08-creature-visualization.md](docs/08-creature-visualization.md) | How genomes become 3D monsters |
| [09-multiplayer-architecture.md](docs/09-multiplayer-architecture.md) | Real-time server-authoritative netcode |
| [10-engine-evaluation.md](docs/10-engine-evaluation.md) | Unity vs Godot vs Unreal |
| [11-roadmap.md](docs/11-roadmap.md) | Risk-first phases from paper playtest to launch |
| [12-open-questions.md](docs/12-open-questions.md) | Open questions and the decision log |
| [13-lens-review.md](docs/13-lens-review.md) | Book of Lenses design review (uniqueness, fun, marketability) |
| [14-ip-licensing.md](docs/14-ip-licensing.md) | Classic-monster IP analysis and art guardrails |
| [15-part-genetics.md](docs/15-part-genetics.md) | Part genetics: recognizable yet breedable parts (with runnable prototype) |
| [16-brains-behavior-command.md](docs/16-brains-behavior-command.md) | Brains → behavior: commanders, loyalty, rebellion, berserk (with runnable prototype) |
| [17-factions.md](docs/17-factions.md) | Factions: human army (tech) and alien hive (biotech) on the same engine (with runnable prototype) |

## A note on the engine

The engine decision is **provisional** — see [docs/10-engine-evaluation.md](docs/10-engine-evaluation.md). The Unity `.gitignore` in this repo reflects the leading candidate, not a final commitment; the decision locks after the Phase-1 validation spike ([docs/11-roadmap.md](docs/11-roadmap.md)).
