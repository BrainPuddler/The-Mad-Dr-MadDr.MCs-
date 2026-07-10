# 20 — Harvest & Repair: The Citizen Economy and Field Recovery

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*), 3 (*Honest combat*) · This is the narrative/system doc tying together mechanics that actually live in [05-component-economy.md](05-component-economy.md), [06-mutator-design.md](06-mutator-design.md), [16-brains-behavior-command.md](16-brains-behavior-command.md), [18-city-battlefields.md](18-city-battlefields.md), and [19-citizens.md](19-citizens.md) — formulas are reproduced here for the story, not re-derived. Terms: [glossary](00-index.md#glossary). Open items tracked as Q17–Q22 in [12-open-questions.md](12-open-questions.md).

## Scope

This doc doesn't introduce new formulas of its own except for Repair (§3), which has no other home. Everything else — Collection Station yields, the Bones-cost formula, the Megabrain Augmentation, Community Hub density — is specified where it structurally belongs (05/06/16/18/19) and just told here as one continuous player-facing story.

## 1. The harvesting loop

**Why a station, not hand-looting.** Corpse salvage already works fine for the handful of monster kills a normal fight produces ([04-combat-model.md](04-combat-model.md)): drop on the death hex, loot within 15 s. It doesn't scale to a city battlefield's Citizen population — hundreds of bodies spread across up to 5 km² ([18](18-city-battlefields.md)). A **Collection Station** solves this the same way an Emitter already solves "how do you claim a map location" ([03-mana-system.md](03-mana-system.md)): capture it once (8 s stand-and-hold, contested-pause), and it passively banks resources for its controller from then on — no per-body channel, no manual looting.

**Where they are.** One Collection Station per **Community Hub** — a hospital, school, or old-age home, generated as a landmark-node subtype at ~1 per 2 km² of built area ([18](18-city-battlefields.md)). Community Hubs run **4× the standard Citizen density** of an ordinary block ([19](19-citizens.md)) — large buildings, large populations, exactly the locations the creator named.

**Yield (v0.1).** Per Citizen death inside a captured station's 5-hex (100 m) radius:

| Yield | Amount |
| --- | --- |
| Blood | 2 |
| Bones | 1 |
| Grey Matter | 1 |

Full definition of Grey Matter — why it's a new resource rather than a reinterpretation of the existing discrete "Brains" tier-item — lives in [05](05-component-economy.md).

## 2. Community Hubs vs. the existing Hospital world-source node

[17-factions.md](17-factions.md) already ships a `Hospital / blood bank` Earth world-source node (Blood+++, Brains+ for MadDr), channel-harvested exactly like a graveyard — a static, always-available background trickle representing the building's own medical stock, unrelated to whether anyone is currently fighting there. It predates Citizens and doesn't model combat-driven yield at all.

**These two mechanics coexist at the same landmark, not merged**: the world-source node is what the *building* passively holds; the Collection Station is what *citizen deaths nearby, this match* produce. A captured hospital Community Hub can pay out both simultaneously — a steady trickle plus whatever the fighting adds. Flagged as **Q20** rather than silently resolved, in case this turns out redundant once it's actually played.

## 3. Relative yield: vanquished foes vs. citizens

Reproduced from [05](05-component-economy.md)'s worked example, since it's the doc that directly answers "vanquished foes have more resources than humans":

> Shambler bill 20 Bones, salvaged at the 50% midpoint = 10 Bones — the same as **10 average citizens** through a Collection Station. Stitched Brute bill 60 Bones → 30 Bones salvaged = **30 citizens' worth**. And the comparison understates it: a monster kill also drops Body Parts and a chance at a tier Brain — resource classes no number of citizens ever yields.

## 4. The Megabrain path, end to end

The creator's own worked example, told as a player-facing walkthrough rather than a formula (the math lives in [06](06-mutator-design.md)/[16](16-brains-behavior-command.md)):

1. Capture a Community Hub's Collection Station on the battlefield.
2. Fight, or simply hold ground — Citizen deaths nearby bank Blood/Bones/Grey Matter automatically.
3. Accumulate 100 Grey Matter (residual in-match stock converts to the meta wallet like everything else, rate open — Q17).
4. At the Lab, spend 100 Grey Matter on a Mastermind-tier genome's **Megabrain Augmentation** — a one-time, deterministic operation, the same shape as Graft.
5. The genome gains a flat `+7.2` Capacity bonus, bringing a maxed Mastermind's command capacity from 4.8 to 12.0 — enough to hold **40 minimum-cost subordinates**, exactly the platoon size asked for.

## 5. Repair

A wholly new mechanic — no existing doc establishes a way to heal a damaged-but-living creature. Explicitly distinct from two things that already live in adjacent conceptual space:

- **Not surgery/graft** ([06](06-mutator-design.md)): harvest/sew replaces a *severed* slot with a new part. Repair never touches slots — it restores lost HP on a still-attached body.
- **Not the `regeneration` quirk** ([06](06-mutator-design.md)): a passive, free, 1% max-HP/s trickle, out-of-combat only, and gene-dependent (not every creature has it). Repair is active, resource-spent, instant, and available to anything at a Vat.

**What**: current HP (Vitality) lost in combat, restored back toward the genome's max Vitality stat.

**Cost (v0.1):**

```
Bones = 0.10 × missingHP
Blood = 0.20 × missingHP
time  = max(2 s, 0.05 s × missingHP)
```

**Worked example**: a Stitched Brute at 100/320 HP (220 missing) costs `0.10×220 = 22 Bones`, `0.20×220 = 44 Blood`, `max(2, 0.05×220) = 11 s` channel — no Body Parts, no Brain, no mana surge. Compare a full reanimation of the same archetype: 60 Bones / 8 Parts / an Average Brain / 35 mana / 10 s. Repair is cheap relative to rebuilding from nothing, expensive enough that "just tank it and repair after" isn't free.

**Where/when**: at the Vat, mirroring reanimation's location constraint — retreat to repair, or fight on, is a real decision, not a menu action. **Scoped to in-match only for v0.1**: no existing doc establishes that combat damage persists between matches (every reanimation reference assumes a fresh, full-Vitality field), so this plan doesn't quietly assume a persistent wounded-roster meta-model. That would be a bigger, separate design question — parked as **Q22**.

**API shape**: Repair never touches the genome — HP is runtime battle state, Vitality is the gene it recovers *toward*, not a schema change. It doesn't belong in [07-mutator-server-architecture.md](07-mutator-server-architecture.md)'s Mutator REST surface the way Megabrain Augmentation does; it belongs as a new real-time client command in [09-multiplayer-architecture.md](09-multiplayer-architecture.md)'s existing list (`move, attack, harvest, capture-channel, reanimate, ability`):

```
{cmd: "repair", creatureId, hpToRestore, idempotencyKey}
  → {hpRestored, bonesCost, bloodCost}
```

Still idempotent and server-validated per [07](07-mutator-server-architecture.md)'s pattern: reject a request that exceeds missing HP or that the wallet can't afford, never silently clamp.

## 6. v0.1 tuning table (consolidated)

| Knob | Value | Lives in |
| --- | --- | --- |
| Per-citizen yield (Blood / Bones / Grey Matter) | 2 / 1 / 1 | [05](05-component-economy.md) |
| Collection Station radius / capture channel | 5 hex (100 m) / 8 s | [18](18-city-battlefields.md) |
| Community Hub density | ~1 per 2 km² of built area | [18](18-city-battlefields.md) |
| Community Hub Citizen density multiplier | 4× standard block | [19](19-citizens.md) |
| Bones cost formula | `4×sizeClass + 0.1×Vitality + 2×Armor` | [06](06-mutator-design.md) |
| Megabrain Augmentation cost / effect | 100 Grey Matter / +7.2 Capacity, one-time | [06](06-mutator-design.md), [16](16-brains-behavior-command.md) |
| Repair cost formula | `0.10×missingHP` Bones, `0.20×missingHP` Blood | this doc |
| Repair channel time | `max(2s, 0.05s × missingHP)` | this doc |

## 7. Open questions

Logged in [12-open-questions.md](12-open-questions.md): **Q17** (Grey Matter meta-conversion rate), **Q18** (reconciling the Bones formula with doc 17's `structure = 2 + 8·bulk`), **Q19** (whether Grey Matter folds into doc 17's Phase-2 `brain` material at the sparse-wallet migration), **Q20** (Collection Station vs. the existing Hospital world-source node), **Q21** (Megabrain Augmentation stacking/radius/power-budget interaction), **Q22** (Repair's in-match-only scope vs. a persistent between-match damage model).
