# 02 — Gameplay Overview: Core Loops & Match Structure

Status: Draft v0.1 · Pillars served: all four ([01-vision.md](01-vision.md)) · Terms defined in the [glossary](00-index.md#glossary).

## The three loops

### Minute loop (inside a match)

1. **Harvest** — your monsters gather components (blood, bones, parts, brains) from graveyards, bone pits, and controlled territory ([05](05-component-economy.md)).
2. **Reanimate** — spend components + a mana surge at your **Vat** to bring a monster from your Menagerie onto the field ([05](05-component-economy.md), [03](03-mana-system.md)).
3. **Position** — move monsters relative to **emitters** and enemy facing; watch the moon dial for the next Lumen phase ([03](03-mana-system.md), [04](04-combat-model.md)).
4. **Fight** — combat resolves from position, power, and bounded luck ([04](04-combat-model.md)).
5. **Salvage** — destroyed monsters (yours and theirs) drop components on their death hex; fight over the corpses ([04](04-combat-model.md) → [05](05-component-economy.md)).
6. Repeat, with the Lumen Cycle continually shifting which emitters — and whose monsters — are strong.

### Session loop (one match)

Pick a Menagerie loadout → real-time 1v1 match (10–15 min) → results written into the Notebook: components earned, occasionally an enemy **genome fragment** recovered from a kill.

### Meta loop (between matches)

Earnings flow into the **Mutator** ([06](06-mutator-design.md)): mutate, splice, and graft new designs — from phone or PC ([07](07-mutator-server-architecture.md)) — then promote them into the Menagerie for the next match. The loop that makes losses interesting: even a defeat yields feedstock for a better monster.

## Match anatomy (v0.1 numbers)

| Parameter | Value | Rationale |
| --- | --- | --- |
| Format | 1v1, real-time | First version; >1v1 deferred ([12](12-open-questions.md)) |
| Target length | 10–15 minutes | Exactly **3 full Lumen Cycles** at 4 min/cycle plus endgame ([03](03-mana-system.md)) — every match guarantees each player at least three "strong hours" |
| Map | Hex grid, ~24×24 hexes | Hexes make facing/flanking legible ([04](04-combat-model.md)) |
| Emitters | 6–10 per map, mixed Solar/Lunar/Twilight | Enough to contest, few enough that each matters |
| Bases | Two **Vats**, opposite corners | The Vat is reanimation point and the destroy-to-win target |
| Concurrent monsters | Soft cap ~15 per side (blood upkeep enforces it) | Mobile perf budget ([08](08-creature-visualization.md)) and readability |

### Map features

- **Graveyards / bone pits**: harvestable component nodes, placed contestably between Vats.
- **High ground**: ridge hexes granting the +0.1 posMod ([04](04-combat-model.md)).
- **Emitter hexes**: capture points with a 3-hex aura ([03](03-mana-system.md)).
- Fog of war: light — monsters reveal a radius; emitter status is always visible to both players (the moon dial is public information by design; pillar 2).
- The abstract hex map above is what this doc's rules run on; [18-city-battlefields.md](18-city-battlefields.md) realizes it as a continuous, procedurally generated 3D city (Unity), with the hex grid preserved underneath as the pathing/positioning index.

## Victory conditions

A match ends when the first of these occurs:

1. **Vat destruction** — destroy the enemy Vat.
2. **Dominion** — control ≥60% of emitters continuously for one full Lumen Cycle (4 min).
3. **Time** — at the 15-minute cap, higher territory score (hexes controlled + emitters held, weighted) wins.

Three conditions so that turtling, rushing, and map control are all viable plans, and no game outlasts 15 minutes (mobile session promise, [01](01-vision.md)).

## The Menagerie

- Up to **12 active monster designs** form your Menagerie (loadout). The full collection in the Notebook is unbounded; the Menagerie is what you can reanimate in a match.
- Each design's reanimation cost is its component bill ([05](05-component-economy.md)) — a Menagerie of all titans means a slow, expensive match plan.
- Matchmaking reads the Menagerie's total power budget ([06](06-mutator-design.md) → [09](09-multiplayer-architecture.md)).

## Cross-device surface map

| Surface | Mobile | PC |
| --- | --- | --- |
| Real-time matches | ✅ primary | later phase ([11](11-roadmap.md)) |
| Mutator lab (mutate/splice/graft) | ✅ commute mode: quick operations, queued offline ([07](07-mutator-server-architecture.md)) | ✅ deep bench: lineage trees, side-by-side comparison, bulk experiments |
| Menagerie management | ✅ | ✅ |
| Notebook (collection, history) | ✅ | ✅ |

## First-time user experience (sketch)

1. Cold open: the will is read; you inherit the Notebook. First page teaches reanimation — you build the starter **Shambler** from given components.
2. Scripted skirmish vs. a scripted rival doctor teaches harvest → position → fight → salvage on a small map with one emitter.
3. The first loss (scripted) yields parts; the game walks you through your first Mutate — your Shambler comes back *changed*. The hook lands here: that monster is now yours alone.
4. First real match vs. AI; multiplayer unlocks after two AI matches.

FTUE details are Phase 2+ work ([11](11-roadmap.md)); this sketch fixes the teaching order: build → fight → mutate.
