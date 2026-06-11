# 01 — Vision & Pillars

Status: Draft v0.1 · See [00-index.md](00-index.md) for the glossary and doc map.

## Elevator pitch

**Mad Doctor's Construction Set (MadDr.MCs)** is a real-time strategy game about building movie monsters. You are the nephew of Dr. Frankenstein, bequeathed his notebooks. From blood, bones, body parts, and brains you reanimate monsters in your laboratory, then deploy them on a living battlefield to conquer territory — a battlefield whose mana emitters wax and wane with the time of day. Between battles, a server-side genetic algorithm — the **Mutator** — lets you breed, splice, and mutate your creations from your phone on the commute or your PC at home. No two players ever field the same army.

## Fiction frame: the Notebook

The unifying UI metaphor is **the Notebook** — your uncle's inherited journals. Every screen in the game is a page of the Notebook:

- The **Menagerie** (your monster roster) is a bestiary chapter, sketched in ink with stitching diagrams.
- The **Mutator lab** is the experiments chapter — splice diagrams, failed-experiment marginalia, lineage trees drawn as family pedigrees.
- The **map screen** is a surveyor's chart of the territory.
- Match results are written in as journal entries.

The fiction gives every system a diegetic home and keeps the macabre-comic tone consistent across mobile and PC.

## The four pillars

Every design decision in this suite must serve at least one pillar; each later doc cites the pillars it serves.

1. **Every monster is yours.** Monsters are generated from genomes, not picked from a catalog. The Mutator ([06](06-mutator-design.md)) and the genome-to-3D pipeline ([08](08-creature-visualization.md)) exist so that two players never field identical armies, and a veteran's menagerie is a personal museum of experiments.
2. **The battlefield breathes.** The day/night **Lumen Cycle** ([03](03-mana-system.md)) makes *when* as important as *where*. This is our homage to Archon's light/dark cycle, made spatial: emitters strengthen and weaken on a clock everyone can see, so timing an assault for your monsters' hour is a core skill.
3. **Honest combat.** No rock-paper-scissors counter charts. Outcomes come from positioning, raw strength, and bounded dice ([04](04-combat-model.md)). A stronger, better-positioned monster reliably wins; luck seasons fights, it never decides campaigns.
4. **Design anywhere, fight anywhere.** The lab follows you. The Mutator runs on a server ([07](07-mutator-server-architecture.md)); you queue a splice on the train and the result is waiting on your PC at home. Matches are real-time on mobile ([09](09-multiplayer-architecture.md)).

## Influences

| Influence | What we take | What we leave |
| --- | --- | --- |
| **Archon** (EA, 1983) | The light/dark power cycle that buffs pieces by time; fighting over power squares | Turn-based chess framing; 1:1 duels as the whole game |
| **El-Fish** (1993) | The joy of breeding: feed in a creature or parts, get surprising, ownable offspring; lineage as a collection | Pure aquarium passivity — our creatures fight |
| **Classic RTS** (StarCraft et al.) | Real-time territory control, economy under pressure | Counter-chart combat, worker micromanagement, 300 APM expectations |
| **Universal horror films** (1930s) | Tone, silhouettes, the lab, the stitches | Grimdark seriousness — we are macabre-*comic*, closer to *Dungeon Keeper*'s wit |

## Audience & platforms

- **Audience**: strategy players who love expression and collection more than ladder grinding; lapsed RTS players who want 10–15 minute sessions; creature-collector players who want their collection to *matter* in skilled play.
- **Platforms**: **mobile (iOS/Android)** is the primary match platform. The **Mutator lab** ships on mobile *and* PC (the PC client is the deep-editing bench; mobile is the commute lab). PC match client is a later expansion ([11](11-roadmap.md)).
- **Session shape**: one match = 10–15 minutes; one lab visit = 2–10 minutes; both are complete, satisfying sessions on their own.

## What this game is NOT

- **Not a deck-builder or gacha.** You don't pull monsters; you make them.
- **Not rock-paper-scissors.** There is no unit type that "hard counters" another. See [04](04-combat-model.md).
- **Not pay-to-win.** Real money never buys match power or better mutation rolls. Meta components are earned in play and spent only in the Mutator ([05](05-component-economy.md)). Monetization is an open question ([12](12-open-questions.md)) but is constrained by this rule.
- **Not a base-builder.** Your base is the Vat. Territory is held by monsters, not turrets.

## Tone & art direction constraints

Universal-horror pastiche: fog, gothic ruins, a full moon that actually matters mechanically. Humor in the marginalia — failed experiments are funny, not gory. Stitches are the unifying visual motif: every monster visibly *assembled*, every UI panel hand-sewn into the Notebook. These constraints bind [08-creature-visualization.md](08-creature-visualization.md).

## Success criteria for the design phase

1. A stranger can read [02](02-gameplay-overview.md) and correctly describe a match.
2. The combat and mana numbers ([03](03-mana-system.md), [04](04-combat-model.md)) survive a paper/spreadsheet playtest (Roadmap Phase 0).
3. The genome schema ([06](06-mutator-design.md)) is precise enough that the server ([07](07-mutator-server-architecture.md)) and renderer ([08](08-creature-visualization.md)) teams could build against it without meeting.
