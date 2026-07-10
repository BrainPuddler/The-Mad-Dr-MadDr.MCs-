# 20 — Harvest & Repair: The Battlefield Economy and Field Recovery

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*), 3 (*Honest combat*) · This is the narrative/system doc tying together mechanics that actually live in [05-component-economy.md](05-component-economy.md), [06-mutator-design.md](06-mutator-design.md), [16-brains-behavior-command.md](16-brains-behavior-command.md), [17-factions.md](17-factions.md), [18-city-battlefields.md](18-city-battlefields.md), and [19-citizens.md](19-citizens.md) — formulas are reproduced here for the story, not re-derived. Terms: [glossary](00-index.md#glossary). Open items tracked as Q17–Q23 in [12-open-questions.md](12-open-questions.md).

## Scope

This doc doesn't introduce new formulas of its own except Repair (§6) and in-match Cannibalize's channel time (§7, open — Q23), which have no other home. Everything else — Collection Station yields, the Bones-cost formula, the Megabrain Augmentation, Cannibalize's recovery rate, Community Hub density, faction corpse salvage, surgery grafting — is specified where it structurally belongs (05/06/16/17/18/19) and just told here as one continuous player-facing story.

## 1. The harvesting loop

**Why a station, not hand-looting.** Corpse salvage already works fine for the handful of monster kills a normal fight produces ([04-combat-model.md](04-combat-model.md)): drop on the death hex, loot within 15 s. It doesn't scale to a city battlefield's Citizen population — hundreds of bodies spread across up to 5 km² ([18](18-city-battlefields.md)). A **Collection Station** solves this the same way an Emitter already solves "how do you claim a map location" ([03-mana-system.md](03-mana-system.md)): capture it once (8 s stand-and-hold, contested-pause), and it passively banks resources for its controller from then on — no per-body channel, no manual looting.

**Where they are.** One Collection Station per **Community Hub** — a hospital, school, or old-age home, generated as a landmark-node subtype at ~1 per 2 km² of built area ([18](18-city-battlefields.md)). Community Hubs run **4× the standard Citizen density** of an ordinary block ([19](19-citizens.md)) — large buildings, large populations, exactly the locations the creator named.

**Yield (v0.1).** Per Citizen death inside a captured station's 5-hex (100 m) radius:

| Yield | Amount |
| --- | --- |
| Blood | 2 |
| Bones | 1 |
| Brains | 1 |

Full definition of harvested Brains — how this bulk resource relates to, without replacing, the existing discrete Brain tier-item — lives in [05](05-component-economy.md).

## 2. Community Hubs vs. the existing Hospital world-source node

[17-factions.md](17-factions.md) already ships a `Hospital / blood bank` Earth world-source node (Blood+++, Brains+ for MadDr), channel-harvested exactly like a graveyard — a static, always-available background trickle representing the building's own medical stock, unrelated to whether anyone is currently fighting there. It predates Citizens and doesn't model combat-driven yield at all.

**These two mechanics coexist at the same landmark, not merged**: the world-source node is what the *building* passively holds; the Collection Station is what *citizen deaths nearby, this match* produce. A captured hospital Community Hub can pay out both simultaneously — a steady trickle plus whatever the fighting adds. Flagged as **Q20** rather than silently resolved, in case this turns out redundant once it's actually played.

## 3. Vanquished foes: Human Army and Alien Hive, not just Citizens

Citizens are unarmed civilians — no Body Parts, and nothing beyond bulk Brains in the Control class. **Vanquished faction units are strictly richer harvest targets**, and this was already true before this doc existed: [17-factions.md](17-factions.md)'s "Harvesting the vanquished" section (already shipped, not new here) has corpse salvage pay out in **the corpse's own flavor** — kill a Human Army unit, harvest **Steel/Motors/Tubes**; kill an Alien Hive drone, harvest **Chitin/Sinew/Ganglion**. Foreign materials then either spend directly on grafted parts of that origin, render 2:1 lossy within a class at the Lab, or — for Control materials (Tubes, Ganglion) — stay locked to their own faction's use and never convert ([17](17-factions.md#harvesting-the-vanquished)).

**"Obviously harvest from human tech too"** (the creator's own framing): a defeated Human rifleman or an Alien drone isn't just a materials drop — it's a **body**, and every body on the battlefield is harvestable exactly the way [06](06-mutator-design.md)'s surgery system already allows ("nothing is wasted"). Cut the `rifle_arm` off a dead Human Army soldier or the `plasma_lance` off a fallen Alien drone and it comes away as a durable **part item** — usable on *any* compatible slot, on *any* origin's body, gated only by the receiving creature's heart capacity ([06](06-mutator-design.md)'s "Grafting as surgery"). Nothing here is a new mechanic; it's the existing surgery/harvest system and the existing per-flavor corpse-salvage rule, told for the first time as one connected harvest story.

**This is the hybrid-monster path.** Each source hands the Doctor something the others can't:

| Harvested from | Materials (own flavor) | Notable Part items | What it buys a hybrid |
| --- | --- | --- | --- |
| Citizens | Bones, Blood, bulk Brains | — (unarmed) | Feedstock only, no Parts |
| Human Army | Steel, Motors, Tubes | `rifle_arm`, `optic_visor`, `sensor_mast`, `piston_leg` | Tech limbs that never mutate or blend — reliable, breeding-inert capability grafted onto an organic frame |
| Alien Hive | Chitin, Sinew, Ganglion | `plasma_lance`, biotech legs/eyes | Biotech that *breeds* once grafted — the only foreign material class that keeps evolving in your bloodline |

**Worked example — a genuine hybrid monster:** a Doctor grafts a harvested Human `rifle_arm` (tech) onto one arm slot of an organic Stitched Brute and a harvested Alien `plasma_lance` (biotech) onto the other. Per [17](17-factions.md)'s energy rule ("mixed bodies pay mixed bills"), the creature now drinks **Blood** (its living frame and brain), **Fuel** (the rifle arm), and **Ichor** (the plasma lance) simultaneously — three upkeep lines on one monster, the price of a body built from three factions' scrap. The rifle arm never mutates on a future Splice (tech is Graft-only, [17](17-factions.md)); the plasma lance can, and its offspring inherit a grown weapon no catalog entry ever authored by hand. This is the same surgery/heart-capacity gate as any other graft ([06](06-mutator-design.md)) — a small frame with a big harvested limb still risks rejection or death on the table.

**Human Army is just as valid a target as Alien Hive** — worth stating plainly, since Citizens (unarmed, tech-free) dominate §1's framing above: a defeated soldier or drone is strictly richer than a citizen kill on every axis §4's relative-yield comparison already makes for monster-vs-citizen, and now also brings Part items citizens never carry.

No new formulas or open questions here — this section connects [06](06-mutator-design.md)'s existing surgery system and [17](17-factions.md)'s existing corpse-salvage-by-flavor rule into the same harvest narrative this doc otherwise tells for Citizens.

## 4. Relative yield: vanquished foes vs. citizens

Reproduced from [05](05-component-economy.md)'s worked example, since it's the doc that directly answers "vanquished foes have more resources than humans":

> Shambler bill 20 Bones, salvaged at the 50% midpoint = 10 Bones — the same as **10 average citizens** through a Collection Station. Stitched Brute bill 60 Bones → 30 Bones salvaged = **30 citizens' worth**. And the comparison understates it: a monster kill also drops Body Parts and a chance at a tier Brain — resource classes no number of citizens ever yields, and a *faction* kill (§3) drops harvestable Parts on top of that.

## 5. The Megabrain path, end to end

The creator's own worked example, told as a player-facing walkthrough rather than a formula (the math lives in [06](06-mutator-design.md)/[16](16-brains-behavior-command.md)):

1. Capture a Community Hub's Collection Station on the battlefield — or fight through Human Army/Alien Hive lines instead (§3), which pay faster per kill.
2. Fight, or simply hold ground — Citizen deaths nearby bank Blood/Bones/Brains automatically; faction kills bank their own-flavor materials plus a chance at harvestable Parts (§3).
3. Accumulate 100 harvested Brains (residual in-match stock converts to the meta wallet like everything else, rate open — Q17).
4. At the Lab, spend 100 harvested Brains on a Mastermind-tier genome's **Megabrain Augmentation** — a one-time, deterministic operation, the same shape as Graft.
5. The genome gains a flat `+7.2` Capacity bonus, bringing a maxed Mastermind's command capacity from 4.8 to 12.0 — enough to hold **40 minimum-cost subordinates**, exactly the platoon size asked for.

## 6. Repair

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

**API shape**: Repair never touches the genome — HP is runtime battle state, Vitality is the gene it recovers *toward*, not a schema change. It doesn't belong in [07-mutator-server-architecture.md](07-mutator-server-architecture.md)'s Mutator REST surface the way Megabrain Augmentation does; it belongs as a new real-time client command in [09-multiplayer-architecture.md](09-multiplayer-architecture.md)'s existing list:

```
{cmd: "repair", creatureId, hpToRestore, idempotencyKey}
  → {hpRestored, bonesCost, bloodCost}
```

Still idempotent and server-validated per [07](07-mutator-server-architecture.md)'s pattern: reject a request that exceeds missing HP or that the wallet can't afford, never silently clamp.

## 7. Cannibalize (in-match): recall and render down

A living decision, not a battlefield inevitability: recall one of your own fielded creatures to the Vat and dismantle it there, converting it directly into in-match Bones/Parts/Brains — the same fantasy as retiring a design at the Workshop ([06](06-mutator-design.md)), just mid-match and on a living body instead of a design on paper. Want a tank right now? Recycle the grunt that isn't pulling its weight.

**Not corpse salvage** ([04](04-combat-model.md)): corpse salvage is involuntary — a monster died in a fight, either side can loot 40–60% of its bill within 15 s. Cannibalize is voluntary and safe — walk it home, no combat required — but pays a flat, lower recovery rate than a contested kill, so it's never strictly better than fighting well.

**Formula (v0.1)**: recovers 50% of the creature's own Bones-cost bill ([06](06-mutator-design.md)) in Bones, 50% of its Body Parts, and rolls its Brain tier at the existing 50% salvage rate ([05](05-component-economy.md)) — the same recovery rate as Cannibalize's Workshop twin, so the two read as one mechanic wearing two hats.

**Worked example**: cannibalizing a fielded Stitched Brute (60 Bones bill, 8 Parts, Average Brain) at the Vat returns 30 Bones, 4 Parts, and a 50% chance at the Average Brain — enough, with a matching haul from a second grunt, to reanimate something meaningfully bigger from the Menagerie.

**Where/when**: at the Vat only (mirrors Reanimation and Repair's location constraint). Channel time isn't set yet — parked as **Q23**, since it needs its own tuning pass to make sure it isn't spammable enough to trivialize the risk/safety tension corpse salvage already relies on.

**API shape**: another new real-time client command in [09-multiplayer-architecture.md](09-multiplayer-architecture.md)'s list:

```
{cmd: "cannibalize", creatureId, idempotencyKey}
  → {bonesRecovered, partsRecovered, brainRecovered}
```

Server-validated, idempotent, per [07](07-mutator-server-architecture.md)'s pattern. The creature is removed from the field permanently — no un-cannibalizing.

## 8. v0.1 tuning table (consolidated)

| Knob | Value | Lives in |
| --- | --- | --- |
| Per-citizen yield (Blood / Bones / Brains) | 2 / 1 / 1 | [05](05-component-economy.md) |
| Collection Station radius / capture channel | 5 hex (100 m) / 8 s | [18](18-city-battlefields.md) |
| Community Hub density | ~1 per 2 km² of built area | [18](18-city-battlefields.md) |
| Community Hub Citizen density multiplier | 4× standard block | [19](19-citizens.md) |
| Bones cost formula | `4×sizeClass + 0.1×Vitality + 2×Armor` | [06](06-mutator-design.md) |
| Faction corpse salvage flavor (Human / Alien) | Steel+Motors+Tubes / Chitin+Sinew+Ganglion, 40–60% of bill | [17](17-factions.md) |
| Foreign-material rendering rate | 2:1 lossy, in-class only; Control never converts | [17](17-factions.md) |
| Megabrain Augmentation cost / effect | 100 harvested Brains / +7.2 Capacity, one-time | [06](06-mutator-design.md), [16](16-brains-behavior-command.md) |
| Repair cost formula | `0.10×missingHP` Bones, `0.20×missingHP` Blood | this doc |
| Repair channel time | `max(2s, 0.05s × missingHP)` | this doc |
| Cannibalize recovery rate (Workshop & in-match) | 50% Bones / 50% Parts / 50% Brain-salvage roll | [06](06-mutator-design.md), this doc |
| Cannibalize channel time | Not yet set — **Q23** | this doc |

## 9. Open questions

Logged in [12-open-questions.md](12-open-questions.md): **Q17** (harvested-Brains meta-conversion rate), **Q18** (reconciling the Bones formula with doc 17's `structure = 2 + 8·bulk`), **Q19** (whether/how bulk Brains, the discrete Brain tier-item, and doc 17's Phase-2 `brain` material mechanically unify), **Q20** (Collection Station vs. the existing Hospital world-source node), **Q21** (Megabrain Augmentation stacking/radius/power-budget interaction), **Q22** (Repair's in-match-only scope vs. a persistent between-match damage model), **Q23** (in-match Cannibalize's channel time / spam tuning). §3's faction-harvest narrative introduced no new open questions — it connects existing mechanics from [06](06-mutator-design.md) and [17](17-factions.md) only.
