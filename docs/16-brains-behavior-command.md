# 16 — Brains, Behavior & the Chain of Command

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*), 3 (*Honest combat*) · Extends the brain budget in [06-mutator-design.md](06-mutator-design.md); couples to combat ([04](04-combat-model.md)) and the aura model ([03](03-mana-system.md)); prototyped in [`/prototype/mutator/command.py`](../prototype/mutator/command.py). Schema/scope decisions tracked as Q12 and Q21 in [12-open-questions.md](12-open-questions.md).

## The idea

The genotype→expression split that drives parts ([15](15-part-genetics.md)) and bodies applies just as cleanly to **brains**: a brain's genes express into **behavior** instead of geometry. This turns the existing Brain resource ([05](05-component-economy.md)) and brain budget ([06](06-mutator-design.md)) from a passive stat-cap into an active battlefield system — **commanders** that control other units, a **chain of command**, and the ever-present risk that control **slips, snaps, or is seized**.

This makes Brains the most strategically loaded component and gives the game its decapitation drama: kill the right brain and a whole army can turn on itself (pillar 3 — outcomes from position and force, now including *who is still giving orders*).

## Brain genes

Four behavioral axes join the genome alongside the part and body axes (`genome.BRAIN_AXES`), plus the existing discrete **tier** (Dim/Average/Gifted/Mastermind, [06](06-mutator-design.md)) which now also sets brain **size**:

| Gene | Meaning |
| --- | --- |
| `command` | How strongly this brain projects control over others |
| `will` | Independence — resists being controlled; a willful unit costs more to hold |
| `temperament` | 0 steady … 1 volatile — how fast and how noisily loyalty swings |
| `guile` | Ambition — a guileful subordinate plots, and can usurp its commander |
| `fury` | Berserk tendency — rage builds under stress; past threshold the unit snaps (below) |
| **tier → size** | Dim 1 · Average 2 · Gifted 3 · Mastermind 4 — scales both capacity and cost |

These breed exactly like every other gene (shared-axis blending, canalized expression, tier can drift one step on mutation) — so command ability is something you *breed for*, and a brilliant commander bloodline is a genuine asset in your Menagerie.

## Expressed quantities

Three pure functions of the brain (prototype: `command.py`):

| Quantity | Formula (v0.1) | Meaning |
| --- | --- | --- |
| **Capacity** | `size × (0.4 + 0.8 × command)` | Control "points" a commander can project |
| **Cost** | `size × (0.3 + 0.7 × will)` | Points a subordinate consumes to hold |
| **Radius** | `4 + 8 × command` (hexes) | Range of control — works like an emitter aura ([03](03-mana-system.md)) |

A commander can hold subordinates whose summed **cost ≤ capacity**. So a Mastermind with high `command` can lead a horde of docile Dim grunts, or a few willful Gifted lieutenants — but not both. Bigger, more willful brains are expensive to command, which is the natural brake on god-armies: the smarter your soldiers, the fewer you can keep on a leash.

## Megabrain Augmentation closes the capacity gap

**This is the doctors' mind control** — the faction-defining fiction for a Mastermind-tier build, not a separate ability bolted on. A doctor's monster doesn't hijack an *enemy's* mind mid-fight (no such mechanic exists, or is planned); the horror is quieter and stranger: grow the brain big enough, feed it 100 harvested Brains, and it doesn't just lead its own platoon — it *possesses* it, forty bodies moving as extensions of one skull. Command/Capacity/Radius (this section) are the actual mechanics; "mind control" is what a 40-strong platoon obeying a single swollen brain *looks like* on the battlefield. No new formula, no new open question — a naming/fiction note, logged as such.

The formula above has a real ceiling, worth stating in numbers rather than leaving implicit: max base Capacity (Mastermind, `command=1`) is `4 × (0.4+0.8×1) = 4.8`. Min subordinate Cost (`size=1, will=0`) is `1 × 0.3 = 0.3`. Max headcount under the *unmodified* formula is `4.8 / 0.3 = 16` — nowhere near a 40-creature platoon. The gap: 40 subordinates at minimum cost need `40 × 0.3 = 12.0` total capacity, **7.2 more than the base formula can ever produce.**

[06-mutator-design.md](06-mutator-design.md)'s **Megabrain Augmentation** closes it — 100 harvested Brains, spent once, for a flat capacity bonus:

```
Capacity_total = size × (0.4 + 0.8 × command) + capacityBonus
```

Worked example (this doc's sanity anchor, matching [04](04-combat-model.md)'s convention): a Mastermind (size 4, `command=1`) with the Augmentation purchased: base Capacity 4.8 + `capacityBonus` 7.2 = **12.0 total**. At minimum subordinate cost 0.3, that's `12.0 / 0.3 = 40` — exactly the platoon size, for exactly 100 harvested Brains. Sourcing (harvesting Citizens and vanquished foes via Collection Stations and corpse salvage) lives in [20-harvest-and-repair.md](20-harvest-and-repair.md); the purchase itself is [06](06-mutator-design.md)'s.

**Not fully solved by this alone**: **Radius** (`4 + 8×command` hexes) is unchanged — a 40-strong platoon is capacity-*legal* but not automatically commandable at the existing radius if it's spread out. Flagged, not assumed fine — part of **Q21** ([12-open-questions.md](12-open-questions.md)).

## Loyalty: the running state

Every controlled unit has a **loyalty** ∈ [0,1] to its commander, integrated each tick toward an **equilibrium** set by its situation:

```
equilibrium = 0.55
  + 0.30 × (spare capacity)        commanders with room to spare reassure
  − 0.45 × (overload)              an over-extended commander corrodes everyone
  − 0.25 × will                    the willful chafe at any leash
  − 0.30 × (distance beyond radius) out of sight, out of control
  − 0.35 × ambition                the guileful plot (guile × how far it out-commands its master)
  + 0.20 × (commander.command − ½) a commanding presence holds them
```

Loyalty moves toward equilibrium at a rate set by `temperament` (volatile brains lurch; steady brains hold), plus bounded noise and immediate **stress** hits from combat damage. Two public thresholds:

- **Waver** (0.45): the unit still obeys, but unreliably (design intent: degraded order response, occasional refusal — a combat-readability cue, [04](04-combat-model.md)).
- **Rebel** (0.20): control **snaps**.

## When control snaps

What happens at the break depends on the brain — and this is where it gets dramatic:

- **Feral** (low guile): a mindless beast. It **abandons its own troops** (a leadership vacuum — each suffers a loyalty shock) and attacks the nearest unit, friend or foe.
- **Rebel** (high guile): an ambitious coup. It breaks from its commander but **marches off with its own subordinates** as a breakaway warband — and if it out-commands its former master, it can **usurp**, seizing the master's other troops too.

And the headline event: **decapitation**. Killing a commander hits every direct subordinate with a loyalty shock (−0.45 in v0.1). Against a deep chain of command this cascades — a Mastermind overlord's death can shatter its lieutenants, who then go feral or lead their grunts off in revolt. Targeting the enemy's brain becomes a real tactic, and protecting your own commander a real responsibility.

## Berserk: the werewolf problem

`fury` is the double-edged gene. Alongside loyalty, every unit carries a **rage** value that builds with combat stress — amplified by fury, and amplified again **at Night** ([03](03-mana-system.md): the moon stirs the blood; this is the lycanthropy coupling, and pairing high fury with lunar affinity is *the* werewolf build). Past a fury-set threshold, the unit goes **BERSERK** (v0.1 numbers, prototype `command.py`):

| Quantity | Formula | Effect |
| --- | --- | --- |
| Threshold | `1.05 − 0.6 × fury` | High fury snaps easily; **low fury can never berserk** (threshold sits above the rage cap) |
| Power | `× (1.3 + 0.5 × fury)` | Up to +80% damage |
| Armor | `+ (2 + 4 × fury)` | Up to +6 — harder to put down |
| Targeting | **nearest unit, friend or foe** | The price: it kills indiscriminately, including its own |
| Control | suspended | Loyalty freezes; orders mean nothing until the fury is spent |
| Burnout | rage decays in frenzy; below 0.35 → **EXHAUSTED** (~6 ticks helpless) | Then its commander can **re-assert control** — if still alive and in range |

The demo scenario (`demo_berserk.py` → `out/berserk.svg`) plays the cautionary tale: a skirmish runs into the Night, Lupex (fury 0.85) outgrows its leash, mauls both of its own packmates while shrugging off blows, collapses at dawn, and the Houndmaster quietly takes back the leash over what's left.

**Design intent:** berserkers are discounted power with a blast radius. The fury gene buys real combat stats cheaply (it should *lower* a design's power-budget cost, [09](09-multiplayer-architecture.md) matchmaking note), and the player manages the risk spatially — send the wolf in *alone and first*, keep the line back until the frenzy fades, never field it at Night next to anything you love. Counterplay writes itself: stall the wolf until it turns, bait it into its own formation, or simply outlast the burnout. Positioning skill again, per pillar 3.

## Harvested brains close the loop

Brains are already a battlefield commodity: corpses drop 40–60% of their components ([04](04-combat-model.md)) and brains specifically salvage at 50% ([05](05-component-economy.md)) — *from enemy corpses too*. With behavioral genes, that rule gets teeth:

- A salvaged brain keeps its **tier**: kill the enemy's Mastermind and you can carry its brain home — the single most valuable drop on any battlefield, and one more reason decapitation strikes pay.
- **Proposed (Q12):** a harvested brain used as Mutator feedstock ([06](06-mutator-design.md) component biasing) biases *brain-axis* mutations toward the donor's genes. Feed your bloodline the brain of a great enemy commander and `command` drifts up — but its `guile` comes along in the bargain. Treachery is contagious; the trophy might be a trojan. A very El-Fish risk/reward, and pure Frankenstein fiction: of course the doctor harvests the brains of brilliant rivals.

## Strategic intent

- **Risk/reward (triangularity, [13](13-lens-review.md)):** controlling a big army is powerful but fragile. The more you press past capacity or spread beyond radius, the closer the whole structure sits to collapse. Honest combat ([04](04-combat-model.md)) now has a psychological front.
- **Counterplay:** assassinate the commander; bait units out of control radius; win the war of attrition so battlefield stress erodes loyalty; or breed for it — high-`command`, low-`will` armies are stable but unambitious, while a guileful Gifted lieutenant is a gamble that might win you the battle or stab you in it.
- **Breeding depth (pillar 1):** "a loyal heavy line" and "a brilliant but treacherous captain" are now genome goals, not just stat lines. Lineages of famous commanders become Menagerie heirlooms.

## Coupling to existing systems

- **[03 Mana](03-mana-system.md):** control radius behaves like an emitter aura; a proposed option (Q12) is that Night or affinity mismatch *weakens* control, making the Lumen Cycle a loyalty pressure too.
- **[04 Combat](04-combat-model.md):** combat damage feeds loyalty stress; wavering units suffer an order-response penalty; `command` could grant a small aura buff to held subordinates (a leadership bonus) — flagged for tuning.
- **[06 Mutator](06-mutator-design.md):** the brain budget gains the four behavioral axes; `command`/`will` also feed the **power budget** matchmaking reads ([09](09-multiplayer-architecture.md)), so a control-heavy army is costed fairly.
- **[09 Multiplayer](09-multiplayer-architecture.md):** loyalty is server-authoritative state, integrated in the same seeded-RNG sim as combat — clients display loyalty, never decide it. The prototype's determinism test mirrors this.
- **[19 Citizens](19-citizens.md):** city-battlefield civilians reuse this doc's *vocabulary and thresholded-state-machine pattern* (calm state, a stress input, a snap threshold, server-authoritative resolution) at a much lighter weight — no command/capacity/radius network, no genome. Citizens are generated, not bred; this doc's brain genes and budget are unaffected.

## The prototype (run it)

[`/prototype/mutator/command.py`](../prototype/mutator/command.py) implements the model; [`demo_command.py`](../prototype/mutator/demo_command.py) runs a three-tier chain under one Mastermind overlord, assassinates the overlord mid-battle, and writes a loyalty-timeline chart (`out/command.svg`) plus a narrative log:

```
python3 demo_command.py        # chain of command + assassination -> out/command.svg
python3 demo_berserk.py        # the werewolf problem            -> out/berserk.svg
python3 test_mutator.py        # includes brain-expression, loyalty, rebellion,
                               # decapitation, berserk (trigger, friendly fire,
                               # recovery, low-fury immunity), determinism
```

In the sample run, a willful far-flung grunt rebels early; when the overlord falls, the loyal lieutenant goes feral and abandons its squad while the schemer lieutenant leads a breakaway warband — all emergent from the genes, all reproducible from the seed.

## Open scope (Q12)

How deep should command go for v1? Options, parked in [12-open-questions.md](12-open-questions.md): (a) **flat** — only purpose-built commanders control, one tier deep; (b) **two-tier** — commanders of commanders (the prototype's scope); (c) **full chain** — arbitrary depth with cascades. Deeper is richer but harder to read on a phone ([13](13-lens-review.md) touchscreen-legibility risk) and to balance. Recommendation: prototype at two-tier, ship flat-or-two-tier for v1, leave the data model (already arbitrary-depth in the prototype) open for more.
