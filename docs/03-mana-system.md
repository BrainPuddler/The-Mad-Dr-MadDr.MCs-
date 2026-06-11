# 03 — Mana, Emitters & the Lumen Cycle

Status: Draft v0.1 · Pillars served: 2 (*The battlefield breathes*) · Terms: [glossary](00-index.md#glossary). All numbers are **v0.1 — to be validated in the Phase-1 combat sandbox** ([11-roadmap.md](11-roadmap.md)).

## Fiction

Mana is galvanic essence — the lightning your uncle bottled. It pools at **emitters**: standing stones, lightning rods, ley wells scattered across the map. Their output follows the heavens: some drink the sun, some the moon. The **moon dial** at the top of the match HUD shows the current phase to both players at all times — the sky is public information.

## The Lumen Cycle

A global match clock cycles through four phases. One full cycle = **4 minutes**; a standard match spans ~3 cycles ([02-gameplay-overview.md](02-gameplay-overview.md)).

| Phase | Duration | Notes |
| --- | --- | --- |
| **Day** | 90 s | Solar hour |
| **Dusk** | 30 s | Transition — Twilight hour |
| **Night** | 90 s | Lunar hour |
| **Dawn** | 30 s | Transition — Twilight hour |

A **10-second warning** (the dial glows, audio sting) precedes every transition, so timing play is skill, not surprise. Matches always start at Dawn (symmetric for both players).

## Emitter polarities & output

Each emitter is **Solar**, **Lunar**, or **Twilight**. Output in mana/second to its controller:

| Polarity | Day | Dusk/Dawn | Night |
| --- | --- | --- | --- |
| Solar | **5** | 3 | 1 |
| Lunar | 1 | 3 | **5** |
| Twilight | 3 | **6** | 3 |

Twilight emitters reward transition-window play — the 30-second scrambles at Dusk and Dawn are designed to be the match's most contested moments (the Archon homage made spatial).

Maps carry 6–10 emitters in a roughly balanced polarity mix, mirrored for fairness ([02](02-gameplay-overview.md)).

## What mana is for (the dual-currency split)

> **Load-bearing economic decision:** the game has two currencies with disjoint jobs. **Mana is energy** — spent to reanimate monsters (the "mana surge" part of the bill) and to fire monster abilities. **Components are material** — the physical bill of a monster ([05-component-economy.md](05-component-economy.md)). Mana is earned only from emitters and cannot buy material; components cannot substitute for energy. Holding territory therefore answers "how *often* can I act," while harvesting answers "how *much* monster can I build."

Mana banks to a per-player pool (cap 100; overflow is lost — banked mana hoarding is bounded by design).

## Unit affinity & the emitter aura

Every monster carries an **affinity gene** — `solar | lunar | neutral` ([06-mutator-design.md](06-mutator-design.md)). Within an emitter's **aura radius of 3 hexes**, affinity interacts with the current phase. These multipliers feed the `emitterMod` term of the damage formula in [04-combat-model.md](04-combat-model.md):

| Situation | Attack power | Move speed | emitterMod |
| --- | --- | --- | --- |
| Matching affinity, strong phase (solar in Day / lunar at Night, in any aura) | +25% | +15% | 1.25 |
| Neutral affinity, in any aura | +10% | — | 1.10 |
| No aura, or matching affinity outside its phase | — | — | 1.00 |
| Mismatched affinity in its weak phase, in an aura | −15% | — | 0.85 |

Auras don't stack; the strongest applicable single modifier wins. The aura's polarity doesn't matter for the buff — *any* emitter focuses the ambient essence; the phase and the monster's gene decide the sign.

### Strategic intent & counterplay

- A lunar-heavy army owns the Night but must survive the Day: retreat, hold un-aura'd ground, or fight outside auras (where everyone is 1.0).
- Neutral monsters are the steady-pressure option: never great, never weak — a deliberate genome trade-off ([06](06-mutator-design.md)).
- The 10-second warning makes "disengage before Dawn" an executable, learnable skill.

## Capture rules

- An emitter is captured by a monster standing **on the emitter hex, uncontested, for 8 seconds** (progress bar visible to both players).
- **Contested capture pauses** (any enemy monster within the 3-hex aura freezes progress; progress does not reset). This is an anti-steamroll mechanism — a sweep of the map still takes time, giving the defender windows ([05](05-component-economy.md) lists the others).
- Captured emitters stay owned until re-captured. Ownership is shown map-wide (public information).
- Capturing grants the aura to the *controller's* monsters only? **No** — auras affect all monsters by phase/affinity regardless of owner; ownership decides only who banks the mana. (Keeps fights *at* emitters honest: you can attack into an aura that buffs your monsters even if the enemy owns it.)

## Edge cases

- A monster on an emitter hex when it dies drops salvage there ([04](04-combat-model.md)) — corpse fights at emitters are intended.
- If the dominion victory timer ([02](02-gameplay-overview.md)) is running and an emitter flips, the timer resets.
- Reanimation mana surge can be paid mid-phase; reanimation time is genome-driven ([06](06-mutator-design.md) brain budget), not phase-driven.

## v0.1 tuning table (consolidated)

| Knob | Value |
| --- | --- |
| Full Lumen Cycle | 240 s (90/30/90/30) |
| Transition warning | 10 s |
| Solar/Lunar output (strong / transition / weak) | 5 / 3 / 1 mana/s |
| Twilight output (transition / otherwise) | 6 / 3 mana/s |
| Aura radius | 3 hexes |
| Affinity modifiers (strong / neutral-in-aura / none / weak) | 1.25 / 1.10 / 1.00 / 0.85 |
| Move-speed bonus (strong phase match) | +15% |
| Capture channel | 8 s, pause-on-contest |
| Mana pool cap | 100 |

All values marked for Phase-0 paper playtest and Phase-1 sandbox validation ([11-roadmap.md](11-roadmap.md)).
