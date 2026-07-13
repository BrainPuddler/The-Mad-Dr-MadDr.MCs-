# 18 — City Battlefields: Scale, Procedural Generation & Destruction

Status: Draft v0.1 · Pillars served: 2 (*the battlefield breathes*), 3 (*honest combat*) · Realizes the hex battlefield of [02](02-gameplay-overview.md)/[03](03-mana-system.md) in continuous 3D space; consumes the damage formula from [04](04-combat-model.md) unchanged; perf budgets extend [08](08-creature-visualization.md); netcode extends [09](09-multiplayer-architecture.md); engine already decided in [10](10-engine-evaluation.md). Terms: [glossary](00-index.md#glossary). Scope/sequencing tracked as Q14–Q16 and Q20 in [12-open-questions.md](12-open-questions.md).

> **Implementation:** this doc's engine-agnostic core is built in [`packages/citygen-core`](../packages/citygen-core/) (C#, .NET 8, zero `UnityEngine` reference — mirrors [`packages/genome-core`](../packages/genome-core/)'s role for the genome): the hex grid index (§1: `HexCoord`, `HexMeters = 20`, `Ring`/`Range` for aura/radius queries), the attack-arc model ([04](04-combat-model.md) `Front`/`Flank`/`Rear`, `Facing.ArcOf`), a **bit-exact port of genome-core's sfc32 RNG** (proven identical to the TypeScript reference via golden values, not just independently deterministic), **the §2 generator itself** (seeded roads → blocks → emitter-xor-hub landmark allocation → building footprints, with the three §1 presets as data and §3's tier table verbatim), **the terrain layer** (river/ponds/hills + destructible bridges — proven to actually sever the map into two banks via a flood-fill connectivity test), **§3's destructible-building runtime state** (`DamageStage`/`BuildingRuntimeState`/`BridgeRuntimeState`: the Intact/Damaged/Destroyed staging and the resulting passability/high-ground changes — not the damage *formula*, which is docs/04's unchanged and still unimplemented since there's no match sim yet), and **§5's engagement-zone LOD classifier** (`EngagementZoneManager`: sorts any position into Engagement/LocalCity/DistantSkyline by distance to the nearest live engagement; radii are data, not constants, since they're flagged for retuning). None of this is wired into an actual match sim yet — there is no match sim in this repo to wire it into (Phase 1's own combat sandbox doesn't exist either). It **is** now visibly rendered, though only as a dev-time preview: `unity-client/`'s `RuntimeCityBuilder` builds the generated city as real primitive GameObjects and spawns a wandering placeholder body per creature fetched from the player's Menagerie (`packages/roster-client`, a dependency-free JSON layer for `mutator-service`'s response shapes, plus `RosterFetcher`'s live-fetch-with-local-cache-fallback — see [09](09-multiplayer-architecture.md)'s note distinguishing this dev tool from the real match-start handshake). The Unity project (`unity-client/`, Unity 6000.3.13f1, URP) references `citygen-core` and `roster-client` as local UPM packages — see all three READMEs for exactly what's built vs. not, and Q14 below for sequencing.

## Scope: this realizes docs 02–04, it doesn't replace them

Everything that decides a fight — the stat block, the damage formula, positioning, the Lumen Cycle, salvage — is unchanged from [03](03-mana-system.md)/[04](04-combat-model.md). What changes is the *space* those rules run in: a continuous 3D city instead of an abstract 24×24 hex grid. The hex grid doesn't go away; it becomes the pathing/positioning index laid *underneath* the city (§1), so "flank," "high ground," and "aura radius" keep exact meanings, just realized in meters instead of hex-counts.

**Explicit non-goal:** this is a separate, later, higher-production track, not a change to Phase 1's "ugly-box" combat sandbox ([11-roadmap.md](11-roadmap.md)). Its own roadmap placement is Q14, not asserted here.

## 1. Playfield scale

A single match's battlefield is **up to 5 km × 5 km, v0.1, "for now."** For calibration: StarCraft II's map editor caps out at 256×256 build-grid units, and its largest-ever ladder map is 232×209 — Blizzard never published a real-world scale, but community estimates (from unit speed and build-time math) put even the biggest SC2 maps at a few hundred meters across, not kilometers. A 5 km live match is a dramatic jump past any RTS precedent — closer in scope to a large open-world or battle-royale zone than a traditional RTS arena. That rules out simulating the whole map uniformly at [09](09-multiplayer-architecture.md)'s existing ~60-entity budget; §5 (engagement-zone LOD) is what makes a map this size actually run.

Map-size **presets** sit below that 5 km ceiling — small maps don't need to fill it:

| Preset | Footprint | Signature layout |
| --- | --- | --- |
| Village (culturally themed) | ~1 km × 1 km | Organic/radial streets around a central plaza |
| Small Town | ~2 km × 2 km | One Main Street arterial + a perpendicular residential grid |
| Big City | up to 5 km × 5 km | Dense grid, verticality, sightline-blocking canyons |

**"Options to expand"** means the generator (§2) and the LOD scheme (§5) are both already parameterized by map size — raising the 5 km ceiling later is a number change, not an architecture change.

### Hex grid, preserved underneath

1 hex = 20 m (chosen so the existing Speed range of 0.5–2.0 hex/s reads as 10–40 m/s, a plausible RTS movement scale). A Big City map at 5 km/side is therefore a 250×250 hex index for pathing, arc/facing, and aura-radius math — the same rules as [03](03-mana-system.md)/[04](04-combat-model.md), just a bigger grid than the abstract 24×24.

## 2. Procedural city generation

Pipeline: **seeded road-network growth** (arterial spine + branching, L-system-style) → **parcel subdivision** along the resulting blocks → **building footprint placement** per parcel → **style-palette skin pass** → **prop/dressing pass**.

Same economy principle as the part library ([08](08-creature-visualization.md)) and factions ([17](17-factions.md)): **one generator, a small authored kit of style presets** (road pattern, density, facade/prop sets) parameterizing "village from country X" vs. "small town" vs. "big city" — not a bespoke generator per theme. A style preset is data (weights, footprint palettes, prop sets), not code.

**Determinism requirement** (mirrors [08](08-creature-visualization.md) exactly): city generation is a **pure function of `(seed, preset, size)`**. Both clients in a match generate an identical city from the seed alone — the seed travels in the match-start handshake (§6), the city itself never needs to be transmitted.

Emitters ([03](03-mana-system.md)) place at generated landmark nodes (plaza, town hall, cathedral, rail depot — preset-dependent), 1–2 per km² of built area, preserving the existing 6–10-per-map density at Small Town/Big City scale.

### Community Hubs & Collection Stations

A second landmark-node subtype: **Community Hub** (`hospital | school | old-age-home`), density **~1 per 2 km²** of built area — half the Emitter density, so hubs read as true landmarks, not as common as emitters. Landmark nodes are allocated **either** an Emitter **or** a Community Hub by their generated archetype, never both — plaza/cathedral/town-hall get emitters, hospital/school/old-age-home get Community Hubs. Building tier reuses the existing **Landmark** tier from §3 unchanged (Structure HP 3000, Armor 8) — a new archetype skin on an existing tier, not a new HP class.

Each Community Hub hosts exactly one **Collection Station** — a capturable structure that converts Citizen deaths within its radius into banked resources for its controller. The capture rule **reuses [03](03-mana-system.md)'s emitter pattern exactly**: stand-and-hold 8 s uncontested, contested capture pauses, ownership persists until recaptured. Radius **5 hexes (100 m)** — deliberately larger than the emitter's 3-hex (60 m) aura, since a Community Hub footprint is a large campus building, not a point landmark. Yield rates and the full harvesting loop live in [20-harvest-and-repair.md](20-harvest-and-repair.md); Citizen population density at these hubs is [19-citizens.md](19-citizens.md) §7.

Community Hubs are placed independently of, and coexist with, [17-factions.md](17-factions.md)'s existing `Hospital / blood bank` Earth world-source node — that node taps a building's static medical stock via channel-harvest regardless of combat; a Collection Station taps *citizen deaths* nearby, combat-driven. Both can apply to the same hospital landmark. Flagged as **Q20**, not silently merged.

### Terrain: hills, water & destructible bridges (natural choke points)

Streets and destructible buildings give the city *urban* choke points; this layer adds the *natural* ones. Three features, generated in the same seeded pass as everything else (identical terrain from the seed alone):

- **Hills** — blobs of the existing **ridge hexes** ([02](02-gameplay-overview.md)/[04](04-combat-model.md)): +0.10 posMod for attackers on them, winged fly over. No new combat math — the generator now *places* the feature docs 02/04 always had.
- **Ponds** — small impassable water blobs, walked around rather than bridged. Local cover-and-flank texture.
- **The river** — the headline: a band of water hexes crossing the full map (width 1 hex on a Village up to 3 hexes / 60 m on a Big City), confined to the upper or lower half so the plaza/arterial/landmark belt stays dry. It severs the battlefield into **two banks**, joined only by bridges.

**Water rule** (the [04](04-combat-model.md) side of this): water hexes are impassable to ground plans; **amphibious body plans — `crab` and `serpentine`** (a catalog property in `packages/genome-core`, like a plan's slot list) — cross freely; winged/floater pass over. No speed penalty in v0.1: water *is* the amphibious breeds' perk, and breeding a crab lineage specifically to own the river is exactly the kind of build the Mutator exists for.

**Bridges are structures, and structures die.** A bridge is a road deck over water — it reuses the **Large** building tier from §3 unchanged (Structure HP 1500, Armor 6; the same damage formula as everything else). Destroy it and its hexes revert to water: the crossing is gone, ground armies must reroute to the next bridge (or hold what's left), while amphibious and airborne forces don't care — which is precisely the counterplay that keeps bridge-demolition from being strictly dominant. Bridges are deliberately scarce (2–3 per map): scarcity is what makes them choke points. Whether a destroyed bridge can be rebuilt in-match is open — **Q24**, not assumed either way.

## 3. Destructible buildings

No new combat math: a building is a stat block with **Structure HP** (≡ Vitality) and **Armor**, resolved through [04](04-combat-model.md)'s existing damage formula unchanged.

| Tier | Structure HP | Armor | Example |
| --- | --- | --- | --- |
| Small (house) | 300 | 2 | residential |
| Medium (storefront) | 600 | 4 | Main Street shop |
| Large (block/tower) | 1500 | 6 | city block |
| Landmark | 3000 | 8 | town hall, cathedral (often an emitter host, [03](03-mana-system.md); see Q16) |

**Destruction staging** (perf-bounded, not physics simulation): **Intact → Damaged** (≤50% HP; cracked/broken-window visual; may spawn a rubble hazard) **→ Destroyed** (0 HP; a pre-authored collapse mesh; changes pathing and sightlines). 3–4 authored states per building *archetype*, not per instance — the same "author once, combine at runtime" doctrine as the part library.

**Explicitly rejected for v1**: full physics/voxel destruction (Teardown-style chunk simulation). Reason: unbounded mobile performance risk at 5 km scale, for a visual upgrade the authored-state approach already delivers at a fixed, budgeted cost — the same call [08](08-creature-visualization.md) made rejecting Spore-style procedural mesh for parts.

**Gameplay hooks**: a Destroyed building removes hexes from the pathing index (§1) and can open new flank routes; its footprint gains the +0.10 high-ground posMod ([04](04-combat-model.md)) for units on the remaining upper floors/roofline.

## 4. Citizens (civilian population)

Buildings and streets are populated by **Citizens** — a wholly separate system specified in [19-citizens.md](19-citizens.md). Citizens are not a fourth faction and don't touch this doc's generation or destruction rules directly; they read building placement from §2 and plug their sync cost into §5 below.

## 5. Engagement-zone LOD (the mechanism that makes 5 km work)

Simulation cost scales with **where the fighting is**, not with total map area. Three zones, dynamically re-centered on live engagements as armies move:

| Zone | Radius from nearest live engagement | What runs there |
| --- | --- | --- |
| **Engagement zone** | ~150–200 m | Full server-authoritative sim: [04](04-combat-model.md) combat, individually synced entities. This *is* [09](09-multiplayer-architecture.md)'s existing ~60-entity budget, reinterpreted as a per-zone cap rather than a whole-match cap. |
| **Local city** | ~1 km | Lightweight: buildings are static/undamaged unless a zone escalates around them; Citizens run client-side cosmetic/crowd AI, not server-synced ([19](19-citizens.md) §5). Low or no server tick. |
| **Distant skyline** | beyond ~1 km | Pure visual LOD/impostors for scale and atmosphere. No simulation at all. |

This is the same pattern open-world games already use for large maps — a moving "hot zone" of full fidelity — applied to an RTS's existing server-authoritative model rather than replacing it. Flagged as a v0.1 proposal pending its own perf spike (Q14/Q15); the radii are the first thing to retune once that spike runs.

## 6. Unity ↔ Lab integration

**No new backend endpoint is needed.** `packages/mutator-service` already implements exactly the handshake [09](09-multiplayer-architecture.md) specifies: `GET /roster/:accountId` (`service.ts`'s `roster()` method, gated by the shared `x-internal-key` header; routed in `http.ts`) returns a player's Menagerie as HMAC-signed genomes. A Unity match client/server consumes this verbatim — never accepts a client-supplied genome, per the existing anti-cheat boundary ([09](09-multiplayer-architecture.md)).

What's actually new, both small additive changes to [09](09-multiplayer-architecture.md) rather than rewrites:

- The match-start handshake payload gains the **city seed, preset, and size** alongside both rosters, so step 3 of [09](09-multiplayer-architecture.md)'s handshake ("pre-assemble during the loading screen") also generates the identical city on both clients before the match starts.
- The bandwidth math gains one line item: **building Structure-HP deltas**, sent only for buildings with damage > 0, delta-compressed the same way monster state already is.

No new engine decision is required — Unity is already the recommendation ([10](10-engine-evaluation.md)).

## 7. v0.1 tuning table (consolidated)

| Knob | Value |
| --- | --- |
| Match battlefield ceiling | 5 km × 5 km |
| Hex-to-meter scale | 1 hex = 20 m |
| Village / Small Town / Big City footprint | ~1 km / ~2 km / up to 5 km |
| Emitter density | 1–2 per km² of built area |
| Building Structure HP (house/storefront/block/landmark) | 300 / 600 / 1500 / 3000 |
| Building Armor (house/storefront/block/landmark) | 2 / 4 / 6 / 8 |
| Damage-state threshold (Intact→Damaged) | ≤50% Structure HP |
| Engagement / Local-city / Distant-skyline zone radius | 150–200 m / 1 km / beyond 1 km |
| Community Hub density | ~1 per 2 km² of built area |
| Collection Station radius / capture channel | 5 hex (100 m) / 8 s |
| River width (Village / Small Town / Big City) | 1 / 2 / 3 hexes (20/40/60 m) |
| Bridges per map (Village / Small Town / Big City) | 2 / 2 / 3 |
| Bridge stats | Large tier: 1500 Structure HP / 6 Armor; destroyed → reverts to water |
| Ponds / hills per map | 2–5 / 3–6 blobs by preset |

All values marked for validation in this track's own spike (Q14) before the Phase-3 netcode build ([11-roadmap.md](11-roadmap.md)).

## Open questions

Logged in [12-open-questions.md](12-open-questions.md): **Q14** (roadmap placement and validation spike), **Q15** (engagement-zone LOD radii and sync-tier promotion trigger), **Q16** (destroyed-landmark/emitter and building-salvage interactions), **Q20** (Collection Station vs. doc 17's existing Hospital world-source node).
