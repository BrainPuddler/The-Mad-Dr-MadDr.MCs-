# 19 — Citizens: City Population, Personality & Armed Bystanders

Status: Draft v0.1 · Pillars served: 2 (*the battlefield breathes*), 3 (*honest combat*) · Reuses the behavior *pattern* of [16-brains-behavior-command.md](16-brains-behavior-command.md) — not the genome schema; explicitly disambiguated from the Human Army faction of [17-factions.md](17-factions.md); placed spatially by [18-city-battlefields.md](18-city-battlefields.md); combat resolves through [04-combat-model.md](04-combat-model.md) unchanged; sync cost governed by [18](18-city-battlefields.md) §5's engagement-zone LOD. Terms: [glossary](00-index.md#glossary). Open items tracked as Q15–Q16 and Q20 in [12-open-questions.md](12-open-questions.md). Economic-actor status revised, see §6.

## Naming disambiguation (read this first)

**"Citizens" is the only term used for this system.** [17-factions.md](17-factions.md) already has a **Human Army** faction — bred-and-requisitioned *combat units* (tetrapod body plan, tech-origin `rifle_arm`/`optic_visor`/`sensor_mast`/`piston_leg` parts, rout/rally morale). Citizens are unrelated to it: no genome, no Mutator entry, no Menagerie, no meta-economy, no chain of command. They are city population — bystanders, not an army.

## Design principle: reuse the pattern, not the system

A Citizen reuses [16](16-brains-behavior-command.md)'s *vocabulary and thresholded-state-machine pattern* — calm state, a stress/threat input, a snap threshold, server-authoritative resolution — without any of the command/loyalty/radius network doc 16 builds for actual armies (citizens don't command each other, there's no chain, no decapitation). This is deliberately the lightest possible model that still reads as "an individual with a mind," and it's *why* this doc never touches the genome schema in [06-mutator-design.md](06-mutator-design.md) — Citizens are generated, not bred.

## 1. Profile: age & body type

- **Age bracket** — child / adult / elderly. Affects animation set and base Speed only; not a combat-stat multiplier by itself (see §3 on why frailty and danger are deliberately decoupled).
- **Body type** — 3–4 mobility classes (able-bodied; mobility-limited, e.g. wheelchair; slow/infirm; child-scale), reusing [08](08-creature-visualization.md)'s rig/LOD economics: a handful of base Citizen rigs plus dressing variation, not full genome-part combinatorics.

## 2. Personality: two axes

Deliberately simpler than [16](16-brains-behavior-command.md)'s five-axis brain — Citizens don't need a command/capacity model, just enough to decide *fight or flee*:

| Axis | Range | Meaning |
| --- | --- | --- |
| `aggression` | 0–1 | Propensity to fight rather than flee |
| `resolve` | 0–1 | Stress threshold before that propensity gives way to panic |

Named to echo [16](16-brains-behavior-command.md)'s `fury`/`will` in spirit, without inheriting their formulas — a Citizen's model is generated fresh per instance, not bred.

| Aggression band | Behavior |
| --- | --- |
| Passive (0.0–0.3) | Always flees a threat |
| Defensive (0.3–0.6) | Fights only if cornered or attacked first |
| Aggressive (0.6–1.0) | May attack proactively if armed (§3) |

## 3. Weapon access (rolled independently of age and body type)

| Roll | Weight | Power | Reach |
| --- | --- | --- | --- |
| Unarmed | 85% | 2–5 | 1 |
| Improvised melee | 10% | 8–12 | 1 |
| Handgun | 4% | 15–18 | 2 |
| Shotgun/rifle-tier | 1% | 25–35 | 1–2 |

**Design rule, stated explicitly:** aggression and weapon access are rolled independently of age and body type. This is deliberate, not an oversight — it's what makes an aggressive, armed elderly or mobility-limited Citizen a designed outcome of the system rather than an edge case to special-case away.

## 4. Worked example (sanity anchor)

Matching [04](04-combat-model.md)'s own convention of a worked numeric example:

> An elderly Citizen in a wheelchair (mobility-limited body type, Speed ≈0.2 hex/s) rolls aggression 0.9 (Aggressive band — a high roll, independent of the frail archetype) and, on the 1% weapon roll, a shotgun-tier weapon: Power 30, Reach 2. Her own stat block is low — Vitality 40, Armor 0, Speed 0.2. Run through [04](04-combat-model.md)'s unchanged damage formula, she front-loads serious damage against a low-Vitality target at close range (30 × posMod × emitterMod × luckRoll, easily 25+ per hit before Armor) but dies in one or two hits herself once anything turns to face her — a glass-cannon environmental hazard, using zero new combat math.

## 5. Population density vs. the entity budget

[09-multiplayer-architecture.md](09-multiplayer-architecture.md) budgets ~60 synced entities for an entire match. A Big City preset at naive density (40–80 Citizens per city block) would blow that on its own — this is the system's hard constraint, not a detail.

**v0.1 resolution** (ties directly into [18](18-city-battlefields.md) §5's engagement-zone LOD, rather than inventing a second sync scheme):

- **Calm** Citizens are client-side cosmetic crowd — not individually server-synced, no combat participation.
- A server-rolled transition (proximity to combat, noise, direct threat — never client-decided, per [09](09-multiplayer-architecture.md)'s anti-cheat boundary) promotes a Citizen to **Alarmed**, at which point it becomes a full server-authoritative synced entity inside the engagement zone's existing budget ([18](18-city-battlefields.md) §5).
- Citizens demote back to Calm (and drop out of sync) once no engagement remains nearby.

Flagged as Q15 — the promotion trigger and its distance/noise thresholds are the first numbers to retune once this track's validation spike runs ([18](18-city-battlefields.md) §7, [12-open-questions.md](12-open-questions.md)).

## 6. Lumen Cycle coupling

At **Night**, Citizens shelter indoors: visible street density drops. This ties back to [03-mana-system.md](03-mana-system.md)'s day/night theme directly (the Archon homage this whole game is built around) while easing the Calm-crowd rendering load at the same time — theme and performance pulling the same direction.

**Revision (2026-07, [20-harvest-and-repair.md](20-harvest-and-repair.md) introduces Collection Stations): Citizens are now economic actors.** A Citizen's death inside a captured Collection Station's radius converts to banked Blood/Bones/Grey Matter for its controller ([05-component-economy.md](05-component-economy.md), [18](18-city-battlefields.md)). This doc's earlier "not economic actors" line is explicitly reversed here, not silently overwritten, per the decision-log convention ([12](12-open-questions.md)). What's still true: Citizens still never interact with **emitters** specifically — that boundary holds unchanged. What's new: a Citizen's death location relative to a Collection Station is now as economically legible as a monster's death hex already is for salvage ([04](04-combat-model.md)).

## 7. v0.1 tuning table (consolidated)

| Knob | Value |
| --- | --- |
| Age brackets | child / adult / elderly |
| Body types | 3–4 mobility classes |
| Aggression bands | Passive 0–0.3 / Defensive 0.3–0.6 / Aggressive 0.6–1.0 |
| Weapon rarity (unarmed / improvised / handgun / shotgun-tier) | 85% / 10% / 4% / 1% |
| Weapon Power (by tier above) | 2–5 / 8–12 / 15–18 / 25–35 |
| Night density multiplier | reduced vs. Day (exact factor TBD — Q15) |
| Calm→Alarmed promotion | server-rolled; proximity/noise/threat trigger (Q15) |
| Community Hub density multiplier vs. standard block | **4×** baseline (160–320 Citizens per hub vs. the 40–80/block baseline, [18](18-city-battlefields.md)) |
| Per-citizen harvest yield | Blood 2 / Bones 1 / Grey Matter 1 ([05](05-component-economy.md), [20](20-harvest-and-repair.md)) |

## Open questions

Logged in [12-open-questions.md](12-open-questions.md): **Q15** (sync-tier promotion trigger, shared with [18](18-city-battlefields.md)), **Q16** (building-destruction interactions, shared with [18](18-city-battlefields.md)), **Q20** (Collection Station vs. doc 17's Hospital world-source node, shared with [18](18-city-battlefields.md)).
