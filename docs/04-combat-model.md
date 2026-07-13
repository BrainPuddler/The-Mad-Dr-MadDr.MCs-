# 04 — Combat Model

Status: Draft v0.1 · Pillars served: 3 (*Honest combat*), 2 · Terms: [glossary](00-index.md#glossary). All numbers **v0.1 — to be validated in the Phase-1 combat sandbox**.

## Design intent: why not counters

Rock-paper-scissors counter charts (StarCraft-style armor/damage type tables) reward memorizing a matrix and build-order scouting. They also collapse when every unit is player-designed — there is no fixed roster to balance a matrix against. Our combat instead derives outcomes from three legible sources:

1. **Positioning** — facing, flanks, high ground, allied support. The primary *skill* axis.
2. **Strength** — the monster's genome-derived stats. The primary *investment* axis (your Mutator work pays off here).
3. **Bounded luck** — dice season fights so identical engagements aren't identical, but variance is tight enough that the better position/army reliably wins.

Mana proximity ([03-mana-system.md](03-mana-system.md)) multiplies into the same formula, making *when and where* part of strength.

## The stat block

Eight stats, every one derived from the genome ([06-mutator-design.md](06-mutator-design.md) — stat genes through per-archetype curves, plus the affinity gene):

| Stat | Range | Meaning |
| --- | --- | --- |
| **Vitality** | 50–400 | Hit points |
| **Power** | 5–40 | Base damage per hit |
| **Armor** | 0–10 | Flat damage reduction per hit |
| **Reach** | 1–4 hexes | Attack range (1 = melee) |
| **Speed** | 0.5–2.0 hex/s | Movement |
| **Ferocity** | 0.5–2.0 /s | Attack rate |
| **Cunning** | 0–25% | Crit chance (see luck) |
| **Affinity** | solar/lunar/neutral | Lumen coupling ([03](03-mana-system.md)) |

## The damage formula

Per attack:

```
damage = max(1, round( Power × posMod × emitterMod × luckRoll ) − Armor)
```

### posMod — positioning (the centerpiece)

| Condition | Modifier |
| --- | --- |
| Front attack (within target's front arc) | ×1.00 |
| **Flank** (target's side arcs) | ×1.25 |
| **Rear** (target's rear arc) | ×1.50 |
| Attacker on high ground over target | +0.10 |
| Each additional adjacent ally engaging the same target | +0.10 (max +0.20) |

Arc model on hexes: a hex has 6 edges; front = the faced edge and its two neighbors (3 edges); rear = the single exact-opposite edge; the remaining two edges are flanks. (Corrected 2026-07 — the original phrasing, "front = the faced hex-edge ±1; rear = opposite edge ±1," gives front and rear 3 edges each and leaves none for "the remaining two flanks"; taken literally the two ±1 spans tile all six edges between them. This is the one reading that satisfies both "front spans 3 edges" and "two edges remain as flank" at once — a textual fix only, no multiplier below changed. First implemented in `Facing.ArcOf`, [`packages/citygen-core`](../packages/citygen-core/).) Modifiers above 1.0 stack additively onto the arc multiplier (e.g., rear + high ground + 2 allies = 1.5 + 0.1 + 0.2 = ×1.8 — the theoretical max).

On a realized city battlefield ([18-city-battlefields.md](18-city-battlefields.md)), "high ground" includes rooftops and upper floors of buildings still standing — a destroyed building's remaining structure grants the same +0.10 posMod term, no new formula.

**Water hexes** (added 2026-07 with [18](18-city-battlefields.md)'s terrain layer — rivers, ponds): impassable to ground movement; **amphibious body plans** (`crab` and `serpentine` — a catalog property of the plan, like its slot list, implemented in `packages/genome-core` `catalog.ts`) cross freely; winged and floater plans pass over, as they already do ridge hexes. Water grants no high-ground term and adds no combat math — it is pure passability. One deliberately emergent consequence, no new rule needed: a corpse whose death hex is water can only be salvaged by something that can *stand* there — harvest is already a stand-and-channel action, so the riverbed becomes the amphibious breeds' private larder.

### emitterMod — mana proximity

Direct from [03-mana-system.md](03-mana-system.md): **0.85 / 1.00 / 1.10 / 1.25** by aura, phase, and affinity.

### luckRoll — bounded luck

- Uniform roll in **[0.85, 1.15]** per attack.
- Plus a `Cunning`% chance of a **×1.5 critical** (replaces, not stacks with, the uniform roll; crits flash visibly).
- That's the *entire* luck model. No to-hit rolls, no dodges — every attack lands; only magnitude varies. Misses feel terrible on a 10-minute mobile clock; bounded magnitude variance gives texture without coin-flips (pillar 3).

## Facing & turning

Monsters have facing. Turning in place costs time proportional to size: `turnTime = 0.15 s × sizeClass per hex-edge` (sizeClass 1–4 derived from total sizeGene mass, [06](06-mutator-design.md)). A hulking titan caught from behind genuinely struggles to bring its front around — this is what makes flanking *real* rather than cosmetic, and makes Speed a defensive stat (repositioning) as well as offensive.

Reach ≥2 monsters attack any target in range regardless of arc but still *have* arcs as defenders.

## Death & salvage

A destroyed monster drops **40–60% (uniform roll) of its construction components** ([05-component-economy.md](05-component-economy.md)) on its death hex, lootable by **either side** for **15 seconds**, then the remains sink into the ground. Harvesting a corpse is a 3-second channel. Design intent: every kill creates a *fight-over-the-corpse* decision — chase the routed army, or stop and loot? — and feeds the circular economy and anti-snowball math in [05](05-component-economy.md). Salvaged enemy corpses occasionally (10%) yield a **genome fragment** revealing one part family to your Mutator catalog ([06](06-mutator-design.md)).

## Worked examples (sanity anchors)

Let monster A (Power 22, Armor 3, Vitality 200, Ferocity 1.0) fight monster B identical except Power 20 (A is 10% stronger). No auras, front-on, no crits:

- Expected damage/hit: A → B: 22×1.0×1.0×1.0 − 3 = 19; B → A: 17. Time-to-kill: A kills B in ~10.5 hits, B kills A in ~11.8. With the ±15% luck band, simulation of the full exchange gives **A winning ~70% of even, front-on engagements**.
- Same fight but A attacks from B's flank (×1.25): A's expected hit = 24.5; **A wins ~85%+**.
- B retreats into a matching aura in its strong phase (B's emitterMod 1.25, A's 1.0): B's expected hit = 22 → the fight flips to near-even despite A's stat edge.

These three anchors *are* the design: stats matter, position matters more, and the Lumen Cycle can overturn both — exactly one rung each. The Phase-1 sandbox must reproduce roughly these win rates or the numbers get retuned.

## Readability & feedback requirements

- Arc and aura states must be glanceable on a phone: facing wedge under every monster, aura ring tint by phase, crit flash, salvage timer ring on corpses.
- Damage numbers off by default (mobile clutter), on in a settings toggle.

## Determinism requirements (for netcode)

The match server is the sole authority ([09-multiplayer-architecture.md](09-multiplayer-architecture.md)). Constraints this doc imposes:

- All `luckRoll` and crit rolls come from a **server-owned, per-match seeded RNG**; clients receive results, never roll.
- The formula must be implementable in **integer/fixed-point math** (multipliers above are exact hundredths by design) so server and any future client-side prediction agree bit-for-bit across platforms.
