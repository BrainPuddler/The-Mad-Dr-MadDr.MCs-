# 12 — Open Questions & Decision Log

Status: Living document. Open questions carry an ID, the docs they block, the live options, and a decide-by phase ([11-roadmap.md](11-roadmap.md)). Resolved questions move to the decision log below — **append-only, with rationale** — so we never re-litigate silently.

## Open questions

| ID | Question | Blocks | Options on the table | Decide by |
| --- | --- | --- | --- | --- |
| Q1 | **Monetization model.** Deliberately undecided; hard-constrained by the no-pay-to-win pillar ([01](01-vision.md)) and the meta-components-only-in-the-Mutator rule ([05](05-component-economy.md)). | Phase 5 planning | Cosmetic-only (pigment palettes, Notebook themes, Vat skins); battle-pass of *part-family discovery* (aesthetic, not power — needs scrutiny against pillar 1); premium title | Phase 4 |
| Q2 | **Match-server sim implementation.** Headless Unity build (shares code with client) vs. custom sim in Go/Rust/C# (leaner fleet, duplicated logic). The fixed-point sim core ([04](04-combat-model.md)) is small enough to keep this genuinely open. | [09](09-multiplayer-architecture.md), [10](10-engine-evaluation.md) | Headless Unity; custom Go; custom Rust; C# shared-source library used by both | End of Phase 2 (before netcode build) |
| Q4 | **Team sizes beyond 1v1.** 2v2 and FFA change map anatomy, pause rules, and matchmaking math. | [02](02-gameplay-overview.md), [09](09-multiplayer-architecture.md) | Defer entirely; design-doc-only in Phase 4; 2v2 in live roadmap | Phase 5 |
| Q5 | **Genome trading/sharing between players.** Huge feature: marketplace, gifting, breeding-with-a-friend's-monster. The immutable-genome data model already supports it ([07](07-mutator-server-architecture.md)) — *deliberately deferred*, not forgotten; economy and moderation implications are large. | none (by design) | Gifting only; splice-rights lending; full marketplace | Post-launch |
| Q6 | **Name, trademark & IP clearance.** (a) "Mad Doctor's Construction Set" / "MadDr.MCs" needs a trademark and store-listing search (also vs. historical "Construction Set" marks — EA's *Adventure Construction Set* lineage), plus broader naming work — the current name is charming but unsearchable ([13-lens-review.md](13-lens-review.md)). (b) Professional IP clearance review of the part library against the classic-monster analysis and guardrails in [14-ip-licensing.md](14-ip-licensing.md), including attorney confirmation of the 1931-film public-domain date (Jan 2027). | store listing, [08](08-creature-visualization.md) part catalog | Keep; rename · clearance scope per [14](14-ip-licensing.md) budget | Naming: Phase 4 · Clearance: before Phase 5 content lock |
| Q7 | **Offline single-player scope.** Phase 2's skirmish-vs-AI exists for development; how much ships? Mobile players expect *something* on the subway besides the lab queue. | [02](02-gameplay-overview.md) FTUE, retention | Tutorial-only; full offline skirmish; offline "haunting" puzzle campaign | Phase 4 |
| Q8 | **Input-automation / screen-reading cheats.** Accepted surface for v1 per [09](09-multiplayer-architecture.md); revisit if competitive play emerges. | none | Behavioral detection; report tooling; ignore | Post-launch |
| Q9 | **Soft-launch retention targets** (D1/D7) and the region choice. | [11](11-roadmap.md) Phase 5 exit | — | Start of Phase 5 |
| Q12 | **How deep should the chain of command go for v1?** ([16](16-brains-behavior-command.md)) Flat (purpose-built commanders only, one tier); two-tier (commanders of commanders — the prototype's scope); or full arbitrary-depth chains with cascades. Deeper is richer but harder to read on a phone ([13](13-lens-review.md)) and to balance. Also: should the Lumen Cycle ([03](03-mana-system.md)) weaken control at Night / on affinity mismatch? | [16](16-brains-behavior-command.md), [04](04-combat-model.md), [06](06-mutator-design.md) | Flat; two-tier; full chain · Lumen-control coupling yes/no | Phase 1 (combat sandbox) |
| Q13 | **Faction scope** ([17](17-factions.md)): are the human army and alien hive campaign/AI antagonists only, or eventually playable factions with their own meta loops (requisition vs. the Mutator; constrained hive evolution)? V1 recommendation: AI factions for Phase-2 single-player skirmish. | [17](17-factions.md), [02](02-gameplay-overview.md), [11](11-roadmap.md) | AI-only; playable at launch; playable post-launch | Phase 2 planning |
| Q11 | **Archetypes: discrete rigs or continuous plan families?** The prototype shows biped/monkey/quadruped working as one tetrapod family on a posture axis ([15](15-part-genetics.md)) — possibly collapsing `biped`/`quadruped`/`hulking` from [06](06-mutator-design.md)'s archetype list into one rig with posture/bulk blend, while `serpentine`/`winged`/`amorphous` stay discrete. Fewer rigs, smoother breeding vs. rig/animation complexity per family. | [06](06-mutator-design.md), [08](08-creature-visualization.md) | All discrete (v1 list); tetrapod family + 3 discrete; maximal continuity | Start of Phase 1 (with the rig spike, [10](10-engine-evaluation.md)) |
| Q14 | **City Battlefields roadmap placement.** Does the track ([18](18-city-battlefields.md)/[19](19-citizens.md)) need its own validation spike (à la the Phase 1 engine spike, [10](10-engine-evaluation.md)) before or alongside Phase 3 netcode, and where does it sit relative to the phase table? | [18](18-city-battlefields.md), [11](11-roadmap.md) | Own spike gating a new parallel phase; folded into Phase 3; deferred until post-launch live-ops | Before this track's own spike |
| Q15 | **Engagement-zone LOD tuning.** The 150–200 m / ~1 km zone radii and the Citizen Calm→Alarmed promotion trigger ([18](18-city-battlefields.md) §5, [19](19-citizens.md) §5) are v0.1 proposals with no playtest behind them yet. | [18](18-city-battlefields.md), [19](19-citizens.md), [09](09-multiplayer-architecture.md) | Proximity-only trigger; proximity + noise; proximity + noise + line-of-sight | Same spike as Q14 |
| Q16 | **Building-destruction interactions.** Does destroying a landmark building disable or relocate the emitter it hosts ([03](03-mana-system.md))? Do building ruins drop faction-flavored salvage ([17](17-factions.md))? | [18](18-city-battlefields.md), [03](03-mana-system.md), [17](17-factions.md) | Emitters immune to building state (simplest); emitter disabled on landmark destruction; no salvage vs. flavor-matched rubble salvage | This track's tuning pass |
| Q17 | **Harvested-Brains meta-conversion rate.** Does in-match-harvested (bulk) Brains convert to the meta wallet at the same 25% residual rate as everything else ([05](05-component-economy.md)), or a bespoke rate given it's single-purpose feedstock for one Mutator op? | [05](05-component-economy.md), [20](20-harvest-and-repair.md) | Same 25% rule (no special-casing); higher dedicated rate; no conversion at all (must spend in-match) | Phase-1 sandbox |
| Q18 | **Reconcile the Bones-cost formula.** [06](06-mutator-design.md) proposes `Bones = 4×sizeClass + 0.1×Vitality + 2×Armor` (v0.1, today's `{blood,bones}` wallet); [17](17-factions.md) already ships `structure = 2 + 8·bulk` (Phase-2 per-flavor wallet). Do these converge, or does one supersede the other when the sparse-material wallet ships? | [06](06-mutator-design.md), [17](17-factions.md), [07](07-mutator-server-architecture.md) | 06's formula is a v0.1 stand-in 17 supersedes; merge the Vitality/Armor terms into 17's formula; keep both, different purposes | Phase-2 wallet migration |
| Q19 | **Should bulk Brains mechanically unify with the discrete Brain tier-item or [17](17-factions.md)'s Phase-2 `brain` MaterialType?** All three now share the name "Brains" (renamed from "Grey Matter" per creator direction — same word, three senses: the tier-item, the bulk harvest currency, and 17's Control-class material). Does bulk Brains become `materials.brain` at the Phase-2 wallet migration, with the tier-item staying a separate discrete roll; does the tier-item itself get built *from* enough bulk Brains (e.g., "N raw Brains = one tier-quality Brain") instead of staying a pure reanimation-time roll; or do all three stay permanently distinct concepts sharing one name? | [05](05-component-economy.md), [06](06-mutator-design.md), [17](17-factions.md), [20](20-harvest-and-repair.md) | Bulk Brains becomes `materials.brain` at Phase 2, tier-item stays a separate roll (current default); N bulk Brains convert into one tier-quality Brain (full mechanical unification); keep three permanently distinct senses of one name | Phase-2 wallet migration |
| Q20 | **Collection Station vs. the existing Hospital world-source node.** Same landmark hosting two independent mechanics (static building stock vs. citizen-death harvest) — confirmed as coexisting in [18](18-city-battlefields.md)/[20](20-harvest-and-repair.md); revisit if it plays as redundant. | [17](17-factions.md), [18](18-city-battlefields.md), [20](20-harvest-and-repair.md) | Stack both mechanics at the same landmark (current default); merge into one combined node; make them mutually exclusive per node | This track's tuning pass |
| Q21 | **Megabrain Augmentation stacking, radius, and power-budget interaction.** Should it be repeatable (diminishing returns) instead of one-time? Does the unchanged Radius formula need its own bonus so a 40-unit platoon is spatially commandable, not just capacity-legal? Does `capacityBonus` need to feed the [09](09-multiplayer-architecture.md) matchmaking power budget the way `command`/`will` already do? | [06](06-mutator-design.md), [16](16-brains-behavior-command.md), [09](09-multiplayer-architecture.md) | One-time flat (v0.1 default); repeatable with diminishing returns; radius bonus bundled in; power-budget surcharge required | Phase-1 sandbox |
| Q22 | **Repair's scope: in-match only, or a persistent between-match damage model?** No existing doc establishes that HP persists between matches; v0.1 recommendation is in-match-only ([20](20-harvest-and-repair.md)) rather than assuming a meta wounded-roster system that doesn't otherwise exist. | [20](20-harvest-and-repair.md), [09](09-multiplayer-architecture.md) | In-match only (v0.1 recommendation); full persistent-health meta model (needs its own design pass) | Before this feature ships |
| Q23 | **In-match Cannibalize's channel time and spam tuning.** Recalling and dismantling your own living creature at the Vat is voluntary and safe (unlike corpse salvage), which is exactly why it needs its own friction — a channel time, cooldown, or both — so it doesn't outcompete the risk/reward of fighting for corpse salvage. No number proposed yet. | [20](20-harvest-and-repair.md), [06](06-mutator-design.md) | Match Repair's channel-time shape (`max(Xs, Ys×value)`); a flat channel time; a per-match cooldown/cap instead of (or alongside) a channel | Phase-1 sandbox |
| Q24 | **Can a destroyed bridge be rebuilt in-match?** [18](18-city-battlefields.md)'s terrain layer makes bridges destructible (Large tier, reverts to water on destruction) — deliberately, since scarcity is what makes them choke points. Whether a side can spend resources to repair/rebuild one mid-match (turning the choke point back into a contested engineering objective) or a destroyed bridge stays gone for the rest of the match is undecided. | [18](18-city-battlefields.md), [20](20-harvest-and-repair.md) | Permanently gone once destroyed (simplest, keeps the choke-point stakes highest); rebuildable via the existing Repair action/cost shape at a Vat-adjacent structure; a dedicated engineering unit/ability | Phase-1 sandbox |
| Q25 | **Wallet-zero decay reconciliation.** [22](22-economy-system.md) supersedes [05](05-component-economy.md)'s "wallet at zero Blood → 2% max-HP/s decay on all fielded monsters" with per-unit efficiency floors (no death spiral, per the never-annoying contract). Doc 05's text still carries the old rule; the two need a reconciling edit pass once 22's model survives a playtest. | [05](05-component-economy.md), [22](22-economy-system.md) | Edit 05 to point at 22 (simplest); keep decay as an *optional* hardcore/ranked modifier; revert 22 if floors prove toothless in the sandbox | Phase-2 sandbox |
| Q26 | **Stitchworks destruction: refund queued bills?** A forward factory dying with a full queue is a huge tempo swing. Refunding unstarted queue items softens a blowout (anti-snowball) but reduces the incentive to snipe factories. | [22](22-economy-system.md) | Full refund of unstarted items; 50% refund; no refund (queue is risk) | Phase-2 sandbox |
| Q27 | **Sawbones auto-triage tuning.** Auto-healing AI must make ignoring the system viable without making massed medics a deathball. The one-medic-per-patient rule is the first guardrail; triage thresholds (60% HP / 50% pool) and whether medics prioritize commanders are open. | [22](22-economy-system.md), [16](16-brains-behavior-command.md) | Flat thresholds; priority weighting by unit cost; player-set triage stances (aggressive/conservative) | Phase-2 sandbox |
| Q28 | **Wallet caps: values and behavior when storage dies.** [22](22-economy-system.md) introduces wallet caps extended by storage structures. Do resources above the cap vanish when a Blood Bank is destroyed (brutal, strong denial), stop accruing (gentle), or drain slowly (readable)? Base cap values are pure guesses. | [22](22-economy-system.md), [05](05-component-economy.md) | Overflow vanishes instantly; overflow freezes (no gain past cap, no loss); slow drain toward cap | Phase-2 sandbox |
| Q29 | **Brain-charge × Megabrain interplay.** The +7.2-Capacity Megabrain commander ([16](16-brains-behavior-command.md), [20](20-harvest-and-repair.md)) issues far more commands than a normal Mastermind — does its grey-matter tank scale with Capacity (else the 40-subordinate platoon drains its brain pool constantly, which may be *good* tension or may be annoying), and does Megabrain Augmentation also grant bonus brain capacity? | [22](22-economy-system.md), [16](16-brains-behavior-command.md) | Capacity-scaled brain tank; flat Mastermind tank (command pressure is the cost); Augmentation grants +brain capacity too | Phase-2 sandbox |

## Decision log (append-only)

| Date | Decision | Rationale | Recorded in |
| --- | --- | --- | --- |
| 2026-06 | Design docs before any code | Greenfield; two existential risks need cheap paper-stage definition first | [11](11-roadmap.md) Phase 0 |
| 2026-06 | 3D creatures & map | Creator decision; the Mutator's output deserves silhouette-level variety | [08](08-creature-visualization.md) |
| 2026-06 | Real-time 1v1 multiplayer from the start | Creator decision; drives the server-authoritative architecture | [09](09-multiplayer-architecture.md) |
| 2026-06 | Server-authoritative state sync, **not** lockstep | Mobile jitter, cross-platform determinism burden, reconnect simplicity, ≤60 entities | [09](09-multiplayer-architecture.md) |
| 2026-06 | Modular socketed parts, **not** Spore-style procedural mesh or texture swaps | Risk/team-size vs. pillar-1 variety; full rationale recorded | [08](08-creature-visualization.md) |
| 2026-06 | Engine recommendation: **Unity** (provisional on the Phase-1 spike) | Weighted matrix 49/39/40 vs. Godot/Unreal; runtime skinned-mesh assembly maturity decisive | [10](10-engine-evaluation.md) |
| 2026-06 | Dual currency: mana = energy, components = material | Keeps territory (tempo) and harvesting (size) as separate strategic dials | [03](03-mana-system.md) |
| 2026-06 | No to-hit rolls; bounded magnitude luck only | Misses feel terrible at mobile match length; pillar 3 | [04](04-combat-model.md) |
| 2026-06 | Meta components spend only in the Mutator | The pay-to-win firewall, whatever Q1 resolves to | [05](05-component-economy.md) |
| 2026-06 | Immutable genome rows with parent lineage | Pedigrees, auditability, signing, and Q5 future-proofing for free at ~80 GB/1M-player worst case | [07](07-mutator-server-architecture.md) |
| 2026-06 | *Impossible Creatures* (Relic, 2003) recorded as studied prior art | Closest precedent for creature-combining RTS; its failure mode (combos collapsing to dominant builds) is what the power/brain budget exists to prevent | [13](13-lens-review.md), [06](06-mutator-design.md) |
| 2026-06 | Classic monsters: pastiche with guardrails, **no studio licensing** | Underlying characters are public domain; specific film designs avoided per authoring guardrails; Universal licensing is cost-prohibitive and would undermine pillar 1 | [14](14-ip-licensing.md) |
| 2026-06 | **Q10 resolved: genome v2 adopted** as the production schema | Prototype validated breedability + recognizability; six shared part axes, body genes, brain genes, part origins. Production implementation in `packages/genome-core` | [15](15-part-genetics.md), [16](16-brains-behavior-command.md) |
| 2026-06 | **Q3 resolved: TypeScript/Node** for the genome core and Mutator service | Doc 07's lead recommendation; JSON-native schema work, ecosystem, hiring. Canonical deterministic RNG (sfc32) defined in `packages/genome-core/src/rng.ts` — the TS implementation is the reference | [07](07-mutator-server-architecture.md) |
| 2026-06 | **Heart organ + surgical grafting** added to genome v2 | Parts can be harvested off one creature and sewn onto another; the heart is the supply organ whose circulatory capacity gates viability ("if the heart isn't big enough, the limb or creature dies on the table — but the parts survive"). Heart is itself transplantable. Implemented in `packages/genome-core` (`surgery.ts`, `energy.ts`) | [06](06-mutator-design.md), [15](15-part-genetics.md) |
| 2026-07 | **Faction resource matrix adopted**: three material classes × three flavors — Structure (Bone/Steel/Chitin), Motive (Muscle/Motors/Sinew), Control (Brain/Tubes/Ganglion), Energy already Blood/Fuel/Ichor. Bill of materials derives from the genome (per-part origin picks the flavor); corpse salvage pays in the corpse's flavors; 2:1 in-class rendering, control never converts; all world sources are Earth locations (hospitals, junkyards, farms) with asymmetric per-faction value; the Hive mines only biomass and refines at high cost | Creator direction (harvest bone/brain/muscle from the vanquished; unit formulas = build requirements; Earth-only setting); generalizes docs/05 components exactly as energy generalized in docs/17 | [05](05-component-economy.md), [17](17-factions.md) |
| 2026-07 | **Faction wallet schema + Human production loop.** Wallet widens from `{blood,bones}` to a sparse per-material map (`MaterialType` over 3 classes × 3 flavors + `biomass`); energy stays in-match, not banked. Humans **requisition** (fabricate blueprints from Steel/Motors/Tubes) rather than breed/grow, and are the cross-race **scavengers**: they render harvested Structure/Motive from any corpse 2:1 into their own materials, but Control (Brain/Ganglion) is inert to them — Tubes come only from Earth electronics infrastructure, their designed weakness | Creator direction (Humans need an equivalent; can harvest from other races). Mirrors Hive biomass economy; schema is Phase-2 with the Postgres store | [07](07-mutator-server-architecture.md), [17](17-factions.md) |
| 2026-07 | **Catalog expanded: 5 more discrete body plans, 4 more part families.** Body plans grew from 4 to 9 (`crab`, `arachnid`, `avian`, `treant`, `floater` added; the latter two `ignoresSlots: ["leg"]`). Hands gained `chain_blade` (tech) and `spore_launcher` (biotech); legs gained `jet_leg` (tech) and `tendril_leg` (biotech) — the origin roster is now balanced at ≥2 families per homolog per origin (previously the Hive had zero biotech legs, the Army one tech hand). This is a deliberate, versioned RNG-stream break: `tests/golden.txt` regenerated via `npm run test:update-golden` (the choice-pool size change shifts which family/plan a given seed draws) | Creator direction ("5 more body types, 2 more legs, 2 more arms"). Data-only catalog change — `BODY_PLANS`/`FAMILIES` records plus renderer geometry; genome v2's schema shape is unchanged, so this is not a docs 06/07/08 schema co-change | [06](06-mutator-design.md), [15](15-part-genetics.md), [08](08-creature-visualization.md) |
| 2026-07 | **Deploy drift found and made visible: `mutator-service` on Render does not autodeploy from this repo.** Diagnosed from a real symptom — a creator report that Human-faction tech legs (`piston_leg`) "never" spawn. Bulk-simulating the current `randomGenome` RNG confirmed the odds are correct (~9% per human spawn); the live service was instead still running a build from before the 2026-07-07 commit that made `/spawn` honor the `origins` parameter at all, so every faction was drawing organic-only regardless of what the Lab sent. Added a public, unauthenticated `GET /version` (commit + process `startedAt`, mirroring `/health`) baked from Render's `RENDER_GIT_COMMIT` build arg (`Dockerfile`), and a matching `site/version.json` stamped by the Pages workflow on every deploy. The Lab's footer now shows both commits side by side so this class of silent staleness is visible without diffing git log against observed behavior | Creator direction ("add that build indicator") after the piston_leg investigation surfaced the deploy gap | [07](07-mutator-server-architecture.md) |
| 2026-07 | **[19](19-citizens.md)'s "Citizens are not economic actors" line reversed.** Citizens are now harvestable via Collection Stations (Blood/Bones/Brains), feeding a new resource-gated construction and Repair economy — [20-harvest-and-repair.md](20-harvest-and-repair.md). Recorded as an explicit reversal, not a silent edit, since the original line was written earlier the same session it's now being undone | Creator direction: citizen harvesting is a core game system ("this is a core component of the game"), not battlefield flavor | [19](19-citizens.md), [20](20-harvest-and-repair.md) |
| 2026-07 | **"Grey Matter" renamed to "Brains."** The bulk citizen/vanquished-foe harvest resource introduced this session as "Grey Matter" is renamed to share the existing "Brains" name, disambiguated as two senses of one word (the discrete per-monster tier-item vs. the bulk harvested currency) rather than kept as a separate coined term. Doc 20 also gained a faction-harvest section connecting Human Army and Alien Hive corpse salvage ([17](17-factions.md), already-existing) and Part-item surgery ([06](06-mutator-design.md), already-existing) into the same harvest narrative — hybrid monsters built from scavenged tech and biotech, not just citizen bulk resources. All cross-references updated (00, 05, 06, 12, 16, 19, 20) | Creator direction: "Brains not grey matter internally," plus "we can harvest alien and human faction parts too... obviously harvest from human tech too" | [05](05-component-economy.md), [20](20-harvest-and-repair.md) |
| 2026-07 | **The Workshop named and Cannibalize added: the Lab's resource-driven construction consolidated into one section, plus a new sacrifice-for-parts operator.** Doc 06's previously scattered "Bones cost," "Megabrain Augmentation," and "The three operators" sections are rolled into one `## The Workshop` section (Mutate/Splice/Graft/Megabrain Augmentation/Cannibalize as its five tools, all priced in Bones/Parts/Brains/Blood). **Cannibalize** is new: retire an owned genome at the Workshop (meta wallet) or recall and dismantle a living fielded creature at the Vat mid-match (in-match wallet, [20](20-harvest-and-repair.md)) — both pay 50% of the source's build cost, reusing the existing salvage rate rather than inventing a new one. Small additive follow-ons: `POST /cannibalize` + a `retiredAt` genome-row marker ([07](07-mutator-server-architecture.md), explicitly not a genome-schema change); `repair`/`cannibalize` added to [09](09-multiplayer-architecture.md)'s client command list (repair had been described in doc 20 but never actually added there — fixed here); a new Sources row in [05](05-component-economy.md) | Creator direction: "the lab is where you build units from resources... users may cannibalize their own units to build a stronger one," flagged explicitly as "a major core mechanic" | [06](06-mutator-design.md), [20](20-harvest-and-repair.md), [07](07-mutator-server-architecture.md), [09](09-multiplayer-architecture.md) |
| 2026-07 | **Cannibalize shipped in `packages/mutator-service` (`POST /cannibalize`) and `packages/genome-core` (`bonesCost()`, `packages/genome-core/src/cost.ts`) — the first real code against doc 20's harvest/construction track.** Building it surfaced two things worth recording rather than quietly patching over: (1) **docs 04/06's `statGenes` stat block (`Vitality`/`Power`/`Armor`/`Reach`/`Speed`/`Ferocity`/`Cunning`) never shipped** in the genome v2 schema actually adopted (Q10) — there is no such field on `Genome`, so doc 06's `Bones = 4×sizeClass + 0.1×Vitality + 2×Armor` referenced genes that don't exist; `bonesCost()` computes the same intent from real fields (body `bulk`, heart tier, summed limb `length×girth`) instead, and doc 06's genome JSON sketch is now flagged stale. (2) **Cannibalize's real recovery is better than specified**: it reuses the existing, tested `harvestPart`/`harvestHeart` surgery functions for 100% Parts+heart recovery (not 50%), plus the 50% Bones bonus on top — cheaper to build than a parallel 50%-everything system, and there's no discrete Brain-tier-item field to roll for in the real schema anyway. Docs 06/07/20 updated to match. A genome is retired via a new `Store.retireGenome`/`isRetired` pair (a separate tracking set, not a field on the immutable genome row) — `setMenagerie` now rejects retired genomes | Creator direction: "sure do that" (green-lighting the Cannibalize build) | `packages/genome-core/src/cost.ts`, `packages/mutator-service/src/service.ts`, [06](06-mutator-design.md), [07](07-mutator-server-architecture.md), [20](20-harvest-and-repair.md) |
| 2026-07 | **Unity project created: `unity-client/`, Unity 6000.3.13f1 (Unity 6.3), 3D (URP) template** — created by the creator in Unity Hub, pushed to `main`; refines [10](10-engine-evaluation.md)'s "Unity" recommendation to a concrete Editor version and render pipeline (URP with the stock Mobile/PC render-pipeline asset split, matching the mobile-first budgets of [08](08-creature-visualization.md)/[09](09-multiplayer-architecture.md)). `packages/citygen-core` restructured as a dual-toolchain local UPM package (`package.json` + `MadDr.CityGen.asmdef`, `noEngineReferences`; xunit tests moved to `Tests~/`, dotnet outputs to `bin~`/`obj~` — tilde paths are invisible to Unity's importer) and referenced from `unity-client/Packages/manifest.json` via `file:`. Repo mechanics fixed in the same pass: `.gitattributes` `[attr]` macros moved to the repo root (git rejects macro definitions in nested files — the Unity template assumes it IS the repo root), with **`lfs` deliberately defined as plain `-text`, not Git LFS** — real LFS would make git-lfs a hard requirement for every clone; deferred until that's a deliberate choice, upgrade path documented in the root file | Creator created the project in Unity Hub per the setup steps in `unity-client/README.md`; version/pipeline recorded so they're a logged decision, not an accident of whichever Hub default was current | `unity-client/`, `packages/citygen-core/`, [10](10-engine-evaluation.md) |
| 2026-07 | **`citygen-core`'s `src/` rewritten for C# 9 — confirmed against a real Unity build, not assumed.** The creator's first Editor open of `unity-client/` failed compiling `HexCoord.cs`: `error CS8773: Feature 'file-scoped namespace' is not available in C# 9.0`. Unity's asmdef compiler caps at C# 9 and has no implicit usings, independent of `src/CityGenCore.csproj`'s own `TargetFramework` (Unity ignores that file — it compiles `src/*.cs` itself via the `.asmdef`). Rewrote `HexCoord` from a C# 10 `record struct` to a plain `readonly struct` with hand-written `Equals`/`GetHashCode`/operators, switched both `src/` files from file-scoped to braced namespaces, added explicit `using` directives, and pinned `LangVersion=9.0` + `ImplicitUsings=disable` in `src/CityGenCore.csproj` so `dotnet test` now fails on the same syntax Unity would, instead of passing here and only breaking in the Editor. All 26 tests still pass unchanged — this was a syntax-compatibility fix, not a behavior change. `Tests~/` is untouched (Unity never compiles it) | Real Editor build failure reported by the creator, not caught by `dotnet test` beforehand since dotnet's default LangVersion for `net8.0` is far newer than Unity's | `packages/citygen-core/src/HexCoord.cs`, `packages/citygen-core/src/Facing.cs`, `packages/citygen-core/src/CityGenCore.csproj` |
| 2026-07 | **Two new alien weapons (`laser_array`, `photon_blaster`, biotech `hand`-homolog) and a fiction note pinning Megabrain Augmentation as "the doctors' mind control."** The creator asked for "lasers and photonic blasters" for aliens (`plasma_lance` already existed, now joined by two canalized-bounds-distinct siblings — a rigid emitter cluster and a broad bioluminescent maw) and "mind control on very big brain units" for doctors. The latter is **not a new mechanic**: it's the existing Megabrain Augmentation ([16](16-brains-behavior-command.md)) — a Mastermind-tier brain commanding a 40-strong platoon — reframed explicitly as the faction's mind-control fiction, since that's what it already does mechanically. A genuine enemy-hijacking ability (seizing control of an opposing creature mid-fight) was the other reading and is NOT what got built — flagged to the creator as a much bigger, separate system if that's actually wanted. Humans' "bullets and 1950s tech" needed no new content: `rifle_arm`/`chain_blade` and the **Tubes** material ("vacuum-tube racks — it's the '50s–'70s") already cover it verbatim | Creator: "Let's give the aliens laser and photonic blasters. Humans bullets and 1950's tech. And Mad Doctor Biological strength, mind control on very big brain units." | `packages/genome-core/src/catalog.ts`, [16](16-brains-behavior-command.md), [17](17-factions.md), [00](00-index.md) |
| 2026-07 | **Monsters render and move in `unity-client/` for the first time — a new package, `packages/roster-client`, plus `RosterFetcher`/`MonsterAvatar`/`RuntimeCityBuilder`.** The creator asked to see bred monsters running around the generated city, reachable "from a user account... most bullet proof possible, local and from internet as backup." Real OAuth (docs/07's stated plan) needs external Google/Apple developer credentials nobody here can provision, so this builds against docs/07's own already-documented interim stand-in (`x-account-id`) instead of inventing a parallel auth story — the Lab website (`site/`) gained a header "🆔 Account ID" button (clipboard copy, `prompt()` fallback) since that value previously existed only in localStorage with no way for a player to find it. **Deliberately avoided adding a second external Unity package dependency** (e.g. `com.unity.nuget.newtonsoft-json`) right after this session's own Package Manager resolution failure — `packages/roster-client` is a ~250-line hand-rolled JSON parser plus typed DTOs instead, dependency-free, unit tested against fixtures captured **verbatim from a real running `mutator-service`** (spawn → menagerie → creature fetch), not hand-written guesses. `RosterFetcher` does the live fetch with local-disk cache fallback (live is primary, matching docs/09's server-authoritative posture; cache is the offline safety net); `MonsterAvatar` is a placeholder capsule (doc08's real genome-to-mesh pipeline doesn't exist in code) wandering only across hexes `BattlefieldState` (built earlier this session) says are passable, crossing water if and only if `body.plan` is `crab`/`serpentine`; `RuntimeCityBuilder` instantiates the generated city as real primitive GameObjects (not gizmos) so all of this is visible in Play mode, not just the Scene view. All 3 new Unity scripts were compiled (not just eyeballed) against a hand-built UnityEngine API stub plus the real `citygen-core`/`roster-client` assemblies under Unity's actual C# 9 constraint before being committed — 0 errors. Explicitly logged as **not** the real docs/09 match-start handshake (a match server fetching both players' signed rosters); a clarifying note was added there so the two are never conflated later | Creator: "the next step I really want is to be able to transfer monsters from the lab website to the games battlefield... most bullet proof method possible... I want to see my monsters running around the city" | `packages/roster-client/`, `unity-client/Assets/Scripts/RosterFetcher.cs`, `MonsterAvatar.cs`, `RuntimeCityBuilder.cs`, `site/index.html`, `site/main.js`, [07](07-mutator-server-architecture.md), [09](09-multiplayer-architecture.md), [18](18-city-battlefields.md) |
| 2026-07 | **Fixed: buildings/monsters rendered bright magenta with no Console error on first real Editor test.** `RuntimeCityBuilder`/`MonsterAvatar` hardcoded `Shader.Find("Standard")` — the Built-in Render Pipeline's shader — in a project created with the **URP** template; URP can't render it, and Unity's fallback for a pipeline-incompatible shader is silent magenta, not an exception. New `unity-client/Assets/Scripts/ShaderUtil.cs` tries `"Universal Render Pipeline/Lit"` first (this project's actual pipeline), then `"Standard"`, then `"Unlit/Color"` as a last resort — portable across pipelines instead of hardcoding one. Re-verified with the same stub-compile check (0 errors) before pushing | Creator, testing in the real Editor: "pink buildings no errors" | `unity-client/Assets/Scripts/ShaderUtil.cs`, `RuntimeCityBuilder.cs`, `MonsterAvatar.cs` |
| 2026-07 | **`RosterFetcher`/`RuntimeCityBuilder`'s default `baseUrl` flipped from `http://localhost:8787` to the deployed `https://maddr-mutator.onrender.com`.** Found by the creator's own testing: pointed at localhost with nothing running there, the roster fetch correctly reported 0 creatures — but the *Lab website itself* is hardcoded to the deployed URL (`site/main.js`'s `MUTATOR_URL`), so a creature spawned there was never reachable from a localhost-pointed Unity client. The deployed URL is the one thing that actually matches what a player would use by default; localhost only makes sense when someone's also separately running `npm start` locally. Two more things this surfaced, documented in `unity-client/README.md`: (1) the deployed service is free-tier hosting that cold-starts (30-60s) after inactivity, longer than `RosterFetcher`'s 8s timeout, so a fallback-to-cache on the first try after idle time is expected, not a bug; (2) that fallback can return a **stale/empty** cached snapshot from an earlier test against a different `baseUrl`, which reads confusingly like "the Menagerie is empty" when it's actually "last successful live fetch was empty, for unrelated reasons." Separately: since this is a Render Blueprint (`render.yaml`) connected to this repo, it very likely **auto-deploys on every push to `main`** — and since the store is in-memory, every auto-deploy wipes it. A creature spawned and saved to the deployed Lab, followed by an unrelated push landing before the Unity test, would explain a "0 creatures" result with no code bug involved at all | Creator: "Yes change the default to web: in the next push" | `unity-client/Assets/Scripts/RosterFetcher.cs`, `RuntimeCityBuilder.cs`, `README.md`, `render.yaml` |
| 2026-07 | **Found the actual root cause of every "0 creatures" result so far: `site/main.js` never called `PUT /menagerie` anywhere.** After ruling out deploy timing (the creator confirmed a fresh, deliberate Render redeploy still returned "0 creatures, live"), traced it further: spawning a creature only ever wrote to `local.stable`/`local.locationOf` (client-side, `localStorage` only) — no code path in the Lab ever pushed a roster to the server-side Menagerie `RosterFetcher` reads. This would have returned 0 for every player, redeploys or not; the deploy-timing risk logged two entries up was real but was never the actual blocker. Fix: the Stable now **is** the Menagerie for v0.1 — `doSaveStable`/`doUnsaveStable` fire-and-forget a `PUT /menagerie` (most-recent 12 win if the Stable exceeds the server's cap, so a new save is never silently dropped) alongside the existing local save, logging a warning on failure rather than blocking the UI. No separate "pick which of your saved creatures are active" screen exists yet — flagged as a real UI gap for later, not solved here, but "save to Stable = reachable from Unity" is a legible v0.1 story on its own | Creator: "same problem... I did redeploy from Render" | `site/main.js` |
| 2026-07 | **Monsters confirmed moving live ("coloured pills") — and three fixes off that first success.** (1) **Spawn/wander happened OUTSIDE the city**: monsters were anchored at axial `(0,0)`, which is the offset rectangle's top-left *corner*, not its middle — and neither landing spots nor wander targets checked map bounds ("not in the blocked set" silently includes every off-map hex, since those are in no blocked set). Fixed with two new `citygen-core` primitives, `CityModel.Contains(hex)` and `CityModel.CenterHex`, used by `RuntimeCityBuilder`/`MonsterAvatar` (+3 regression tests, 133 total — including one asserting `(0,0)` IS on the map, which is exactly what made the bug quiet). (2) **Lab UX**: ⭐ stars now mark stabled creatures on Lab roster cards and Stable cards (the star = "on the battlefield roster", making the new Stable≡Menagerie rule visible), and a true **Delete** exists in both Lab and Stable — `deleteEverywhere()` removes from bench + stable + server Menagerie at once, replacing the old half-delete that kept a hidden stable copy on the battlefield roster (the same hidden-state pattern that caused the "0 creatures" debugging saga). Server genome rows stay, immutable, for descendants' lineage (docs/07). (3) **Crab identity disk**: the faction chest decoration (human control-panel dial / alien sac-cluster plate) anchored at the shell's front edge under the crab's fused head; `planCrab` now passes an `up: true` anchor and both chest decorations render flat on the carapace top instead — the crab's back is its billboard. Front-facing math untouched for every other plan | Creator: "coloured pills moving around, but Not within the city but outside of it... add stars to the creatures in the stable, and option to delete a monster from stable or Lab. AND when we have a low flat crab character let's put the Identity disk on their back NOT below the head" | `packages/citygen-core/src/CityModel.cs`, `unity-client/Assets/Scripts/RuntimeCityBuilder.cs`, `MonsterAvatar.cs`, `site/main.js`, `site/creature-renderer.js` |
| 2026-07 | **Crab identity disk, round 2: first attempt anchored it below the shell's actual peak, not on top of it.** `planCrab`'s carapace is a 5-level lathed dome; the previous anchor sat at `y0+h*0.90`, but the dome's topmost `levels` entry — its true apex — is at `y0+h`, so the disk sat embedded in the still-rising dome surface rather than above it ("below the surface of carapace not on top"). Raised the anchor to `y0+h*1.05` (clearly above the apex, mounted like a coin on a dome rather than sunk into it) and widened the internal clearance offset in both `robotChest`'s and `alienChest`'s `up` branches from a token `+0.02` to `+0.1`. Also shrank the anchor's `rx`/`rz` from `0.55` to `0.42` to roughly match the small apex cap's own footprint instead of overhanging it | Creator: "the identity disk is below the surface of carapace not on top!" | `site/creature-renderer.js` |
| 2026-07 | **Found the real "monster spawns without arms" bug: `laser_array`/`photon_blaster` were never added to the renderer.** Traced through canvas/WebGL init, the `_detail` triangle-budget safeguard, and `_lastPortraitId` cache-invalidation looking for a "first render after reset" timing bug — all checked out fine on inspection. The creator's next report named the actual cause directly: the Chop Shop showed "Hand: Laser Array" as a real, removable part, just never rendered. Both families were added to `packages/genome-core`'s catalog earlier this session (alien weapons) but `site/creature-renderer.js`'s `buildPart` — a `switch (family)` with no `default` case — silently draws nothing for any family it doesn't recognize. Confirmed as alien-only (biotech origin) matches the creator's "happen often on the alien race." Added both as real geometry: `laser_array` a rigid fan of narrow crystalline emitters (count-scaled, cyan glow, distinct from plasma_lance's warm ICHOR/BLTGLO), `photon_blaster` a broad bioluminescent maw with a girth/ornament-scaled pulsing iris (warm near-white glow) — plus their `TEX_FAM` skin-texture entries, which had the same gap. `renderPartThumbnail` (the Chop Shop's per-part tray icon) reuses the same `buildPart` switch via a synthetic mannequin genome, so one fix covers both the main portrait and the tray. No other family-keyed lookup table in the file needed the same fix — display names come from a generic `family.replace(/_/g,' ')`, not a table, which is why the name rendered correctly while the geometry didn't | Creator: "in chop shop the name: Hand: Laser Array shows up... but are not rendered or are hidden... happen often on the alien race" | `site/creature-renderer.js` |
| 2026-07 | **Fixed a real desynced-bicep bug in `armDrop`, the shared arm-building function every hand family (claw, pincer, rifle, plasma lance, chain blade, spore launcher, and the two new alien weapons) draws through.** The line immediately before the bicep bulge sets the animation gait to `armGait(1)` -- the WRIST's phase, "hands and weapons swing with the wrist" -- and the bicep block never overrode it for its own position at the elbow, so the bulge animated on the wrist's swing while sitting near the shoulder: "floats to the front side and back of the arm... not locked in sync." Also addressed the creator's second observation, that a bicep should read as elongated, not a ball: replaced the single fat ellipsoid with a 3-point tapered tube running ALONG the shoulder-to-elbow direction (a real bicep's long axis), whose own animFn/gaitFn sample the exact same global `t` range (1/3 at the shoulder-exit point, 2/3 at the elbow) the main arm tube already used across that stretch -- every point on the bulge now moves with the tube surface underneath it, not a single borrowed phase. Gait is explicitly restored to `armGait(1)` after the bicep block so the hand/weapon geometry drawn next is unaffected | Creator: "there is a ball just below the shoulders... when the arms move it floats the the front side and back of the arm. Not locked in sync... also a bicep should be elongated... The main thing is to fix the motion tracking tho" | `site/creature-renderer.js` |
| 2026-07 | **Bicep bulge removed entirely, two commits after the sync fix.** Correctly synced and reshaped into an elongated tube along the real shoulder-to-elbow axis, it still read as a separate piece stuck onto the arm rather than grown from it -- a visible seam, not blended. Simpler to cut than keep tuning a shape that doesn't merge with the tube underneath it. Confirmed to the creator it was gated behind `girth > 0.45` the whole time, so it was never on every arm | Creator: "remove the bicep it doesn't blend properly. it's only on the large arm any ways right?" | `site/creature-renderer.js` |
| 2026-07 | **The battlefield became playable: A* waypoint navigation, target locking, genome-driven articulated bodies with no-skate distance-driven gait, and edible Citizens.** Three layers, each tested at its own level: (1) **`citygen-core` gained `HexPathfinder`** — deterministic A* over the hex grid (ties break by (R,Q), never hash order), consuming `BattlefieldState`'s blocked sets so one pathfinder serves ground and amphibious plans and reacts to live destruction; includes multi-goal `FindPathToBuilding` after the single-hex "path adjacent to X" query proved structurally impossible against a landmark whose center is ringed by its own footprint (7 new tests, 140 total: bridge-crossing proof, water-as-wall vs water-as-highway per movement class, rubble opening paths, path determinism). (2) **`roster-client` gained `Locomotion`** — a line-by-line port of the Lab's `locomotionProfile` (heart=engine, mass=brake, sprint gated by circulatory headroom), verified against golden values captured from the real JS running in node, same discipline as the Rng port — the Lab and the battlefield must agree on how fast a genome moves (13 new tests, 31 total). (3) **Unity**: `MonsterBody` (plan→silhouette+legs, bulk→scale, hand family→weapon shape, brain tier→head; **planted feet stored as world positions and never moved while planted — a leg swings only when actual body displacement pulls its hip past the stride threshold, and body bob is phased by distance traveled, not time** — no skating by construction), `MonsterAgent` (waypoint queue, attack-until-Destroyed with placeholder damage since docs/04's statGenes never shipped, chase-and-eat with re-pathing when the fleeing target moves), `Citizen` (docs/19 cosmetic crowd: wander/flee/edible, docs/20 yields 2/1/1 into a session wallet), `WaypointCommander`/`SimpleCameraRig`/`HudStatus` (all NEW-Input-System-only — this project's activeInputHandler setting makes the legacy Input class throw). Honest scope note: bodies are simplified genome-driven silhouettes, NOT a port of the Lab's ~3500-line WebGL renderer — that port is its own future project (docs/08). All 12 Unity scripts stub-compiled (0 errors) against a hand-built stub including the InputSystem API before commit; `MonsterAvatar` (the placeholder capsule) deleted as superseded | Creator: "Put the actual models in the game and allow actual movement and navigation around building... Movement should be based on the gate of the monster and NO skating. Motion must match the distance traveled by the placed foot. Speed based on physiology... Give me a waypoint navigation system, with target locking, and some humans to eat as per game specs" | `packages/citygen-core/src/HexPathfinder.cs`, `packages/roster-client/src/Locomotion.cs`, `unity-client/Assets/Scripts/` (7 new/rewritten), [18](18-city-battlefields.md), [19](19-citizens.md), [20](20-harvest-and-repair.md) |
| 2026-07 | **Fixed the "weird stretched out lines" from the first live run of the playable battlefield — the no-skate rule itself caused it, plus a second latent init-order bug found in the re-audit.** (1) `MonsterAgent.Init` built the body (which plants feet as world-locked positions at the CURRENT transform, per the no-skate rule) while the monster still stood at the world origin, and only THEN teleported it to its home hex — every foot stayed obediently planted near (0,0,0), and every leg rendered as a hundreds-of-meters line converging back to spawn-zero, exactly the screenshot. Fixed by positioning before building, plus a public `SnapFeetToGround()` (re-plants feet and the serpentine tail trail under the body's current position, with a per-group stagger so first steps alternate) that Build now always ends with — any future teleport has a correct, documented recovery call. (2) Re-auditing per the creator's "check your work": `BodyHeight` was a live getter derived from `_legs.Count`, read BEFORE `BuildLegs()` ran — so the torso height and first hip used the legless fallback value on every legged plan. Replaced with a `_standHeight` fixed at Build start from `Locomotion.LegsFor(plan)`. (3) New runtime sanity check `WarnIfFeetImplausible`: a planted foot farther than 2×stride from its hip logs a Console warning with numbers — loud failure instead of silent spaghetti diagnosed from a screenshot. On "build an in-game monster regenerator from the DNA the lab generates": `MonsterBody` IS that regenerator, v0.1 — genome in, body out, in-engine; the full-fidelity version (Lab-renderer visual parity) remains the docs/08 port, the known next big brick | Creator: "This is what the monsters came in as. weird stretched out lines. give it one more go try to fix this, check your work... We need to build a in game monster regenerator from the DNA the lab generates" | `unity-client/Assets/Scripts/MonsterBody.cs`, `MonsterAgent.cs` |
| 2026-07 | **Deploy-wipes finally mitigated after the creator hit "0 creatures, live" for the third time.** Root pattern: every push to `main` auto-deployed the Render service, and the in-memory store wiped whatever had just been spawned and stabled for testing. Two fixes from both ends: (1) `render.yaml` gained a `buildFilter` scoped to exactly what the Dockerfile COPYs (`packages/genome-core`, `packages/mutator-service`, `Dockerfile`, `render.yaml`) — Unity scripts, the Lab site, and design docs, the bulk of recent pushes, no longer trigger deploys at all. (2) `RosterFetcher` no longer lets a successful-but-EMPTY live roster overwrite a good local cache — the original code destroyed the backup with emptiness at exactly the moment it was needed, defeating the "local as backup" design; "server 0, cache N>0" now falls back to the cache loudly, with a Console message explaining the wipe and the fix (re-stable in the Lab). Real persistence (Postgres) remains the known next brick (docs/07); this shrinks the wipe blast radius until it lands | Creator: "when I run the unity again with the same id, it does not find any monsters" (third occurrence) | `render.yaml`, `unity-client/Assets/Scripts/RosterFetcher.cs` |
| 2026-07 | **Gait scheduler rebuilt twice against live "legs sticking/stretching" reports, landing on distance-phased windows + beetle turning (v4).** v2's turn-taking barrier could deadlock (both groups waiting) and got rescued by fail-safes — which is what residual sticking *was*. v3 phased step windows purely by distance traveled (group 0 steps in phase [0,0.5), group 1 in [0.5,1)) — no barrier left to deadlock, but its phase clock only counted LINEAR distance, so turning generated leg strain with no step windows opening ("turning is problematic, legs get all stretchy"). v4 treats rotation as footwork the way insects do: the gait clock advances by linear + rotational displacement (\|yaw rate\| x average hip radius), and each leg leads its step along its own rest-point velocity (body velocity + rotation's contribution at that hip) — outside legs naturally take long arcs, inside legs short ones. Sim-verified standalone at 6 speed/turn combos including rotate-in-place before porting to `MonsterBody.cs` | [18](18-city-battlefields.md) |
| 2026-07 | **Lab renderer port to Unity begun: `packages/creature-mesh` (pass 1 = full geometry engine + tetrapod at full fidelity).** Engine-agnostic C# port of `site/creature-renderer.js` (docs/08): all primitives (ellipsoid/tube/torus/lathe/curved-cone/limb-joint), full palette + skin genetics, and the tetrapod plan complete — torso lathe, bolted brass belt pelvis, neck, all four brain-tier heads including the mastermind's brain under a riveted glass dome, franken face (brow/jaw/tusks/heart-tier neck bolts), tail, and all 10 hand + 4 sensor + 5 eye part families, with graft-hue, dormant-sensor, and headless rules. Legs stay on the Unity gait rig (mounted at the socket frame the builder returns) so the no-skate contract holds. Deliberately dropped in pass 1: per-vertex color gradients (flat color per material chunk), texture tiling, blink/gaze/breath vertex channels, glow halos; other 8 plans keep placeholder bodies and migrate in follow-up passes. Winding is fixed up against analytic normals at build time because Unity single-sides materials where the Lab shader was two-sided — a flipped face there is a lighting quirk, here an invisible hole. 33 xunit tests (determinism, valid indices, per-family geometry, tier/gene gating, winding) | [08](08-creature-rendering.md), [18](18-city-battlefields.md) |
| 2026-07 | **Lab renderer port pass 2: all nine body plans + dressed rig legs (`LegKit`).** The remaining eight plans ported at full fidelity (blob's translucent gelatin over visible organs, serpentine's coil/hood/fangs/forked tongue with its own skull at every brain tier, winged's bat wings — membranes emitted double-sided because Unity backface-culls where the Lab shader two-sided them — crab's low carapace with reach-capped chelipeds, arachnid's two-segment body and pedipalps, avian's raptor lean and long neck, treant's trunk-and-roots, floater's finned drone hull with thruster ring), honoring `tiny` part scaling and arm-reach caps. Answering "the legs are just sticks": legs stay on the no-skate gait rig but are now dressed in the family's real geometry — `LegKit` authors hip joint hardware plus tapered upper/lower segments on the rig's y∈[−1,+1] segment convention (proximal radius at −1) and family feet (hoof, side-mirrored talon fans, insect needle points, piston struts, jet nozzles, tendril tips, ring-stitched stumps); pair count follows the family like the Lab (insect 2–3 pairs, piston spider quad, else one mirrored pair). Known deviations, logged deliberately: piston tank-tread variant deferred (no feet for a stepping rig), lab-side jet legs never touch down but the battlefield rig steps them, treant/serpentine/blob/floater move as rigid lab bodies (no leg slot to honor). 68 tests | [08](08-creature-rendering.md), [18](18-city-battlefields.md) |
| 2026-07 | **Battlefield combat: weapons fire, units have health and can't overlap, enemy tanks test it.** Weapon/health numbers derive from the genome in `roster-client` (`Combat.Profile` -> `WeaponProfile` + `CombatProfile`, tested like Locomotion): hand family picks the archetype (laser_array->instant cyan beam, photon/plasma->slow phaser bolt, rifle->fast bullet, spore->lobbed, claws/blades->melee), `count`/`girth` genes scale damage, and health scales with bulk + heart tier. Unity's `UnitCombat` is the shared fight component (monsters and tanks alike); `WeaponFx` renders the attack pattern (LineRenderer beams, homing `Projectile` bolts/bullets, a flame cone) and applies damage; `HealthBars` shows a bar over any unit in battle. A few `Tank`s spawn at the city edge (half flamethrowers "because it's cool", half cannons), roll in, and fight -- monsters auto-retaliate/auto-engage in aggro range and can be ordered onto a tank by right-click. No unit walks through another: `RuntimeCityBuilder.ApplySeparation` resolves overlaps each frame (citizens excluded -- they're prey). Also fixed the tendril "left leg facing the wrong way" IN THE LAB (site/creature-renderer.js): the curl-wiggle term in the leg path lacked the `side` factor the outward-lean already had, so the wiggle bent both legs the same absolute way. Known deferred: real docs/04 damage formula (needs statGenes the v2 schema never shipped), tank A* (they steer straight), a flamethrower creature limb (would need a new genome hand family), FX pooling | [18](18-city-battlefields.md), [08](08-creature-rendering.md) |
| 2026-07 | **Stopped groups now pack together, staying clear of buildings and water.** ApplySeparation only ever pushed overlapping units APART, never pulled them together -- so a group ordered to a spot via FormationHexes (one hex slot each, ~20m apart, so walking doesn't collide) just stayed a full hex apart forever once parked, which read as too spread out at rest ("walking spacing looks good, but when they stop they can be closer together"). Fix: OrderMove gained a settleTarget overload -- the commander's group-move (AssignFormation) now passes the clicked ground point through; once a unit finishes its walk and goes idle, MonsterAgent.TickSettle creeps it toward that shared point at a slow shuffle, with ApplySeparation (already called every frame) naturally halting the creep once neighbors are touching -- the group packs down to combined-radius spacing instead of hex spacing. Terrain-aware by construction ("must be cognizant of building and natural features"): each settle step is checked against the same blocked-hex set (buildings, water) pathfinding uses before committing, and the moment a step would land in one, the creep stops for good right at the boundary rather than clipping in. Single-unit moves pass no settle target and never drift after arriving. Gameplay layer stub-compiles clean against the real DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **"Why is the stable cleared out when I rerun" — diagnosed and given a client-side safety net.** The Stable's ID *list* lives in localStorage and survives fine; what vanishes is the genome DATA behind those IDs, which only ever lived in `mutator-service`'s in-memory `Store` (Map objects in server RAM). Any process restart loses it all — a redeploy, or (the actual everyday cause) Render's free tier spinning the service down after ~15 min idle and starting a fresh empty process on the next request. Re-polling can't fix this: the data isn't stale, it's genuinely gone server-side, so the creator's "couldn't it just re-read the stable list" instinct doesn't hold — there's nothing left to re-read. Fix (client-side only, no new infra): the Lab now caches a signed `{genome, signature}` pair for every Stable creature in `localStorage` (`local.stableBackup`, written on save and on every sync); when a sync finds the server missing an ID the Stable list still names, it replays the cached pair through a new `POST /restore` endpoint. The signature is the anti-cheat gate (the same "clients submit requests, never genomes" invariant every other op protects, docs/07): only a genome the server itself signed at some point can pass `verifyGenome` against `SIGNING_KEY`, so this can't mint an arbitrary hand-crafted genome for free — it can only resurrect a row that legitimately existed, and it comes back under its ORIGINAL id (deliberately bypasses the shared `mint()` helper, which always stamps a new one) so the client's existing Stable/Menagerie references keep working with no remap. 28 mutator-service tests (was 23), including a full wipe-then-restore round trip and a rejected-forged-signature case. Explicitly a safety net, not the real fix: it only covers what a given browser actually cached, and does nothing for the op log/wallet/tray. Real persistence (Postgres behind the existing `Store` interface, already designed for exactly this swap) remains the pending fix and needs an external DB the creator will need to provision | [07](07-mutator-server-architecture.md) |
| 2026-07 | **Winged units can walk or fly; ground units without a ranged weapon confirmed to need building proximity.** Flight is decided per order at path-compute time (`MonsterAgent.DecideFlight`): "far" (straight-line hex distance clears a threshold) or "high up" (no ground route exists at all, or the ground route is a heavy detour around buildings/water vs. a direct flight) -- both explicit creator conditions. Flying still runs the SAME A* over the SAME hex grid as walking, just against the amphibious-style blocked set (buildings block, water doesn't) -- "same navigation rules apply as walking, no going through buildings" is never bypassed, so this is never a straight-line ignore-everything hop. A unit that flew to its target stays airborne while it fights (an aerial attack) rather than landing first, and only lands (`GoIdle`, the single choke point every `_order = Idle` transition now goes through) once its order is genuinely done. `MonsterBody.SetFlying` handles the visual: torso AND every leg hip lift together by the same smoothly-eased amount (nothing floats free of its own legs), legs fold into a tucked mid-air pose instead of trying to plant a ground step, and the selection BoxCollider tracks the lift too (a flying unit's clickable box would otherwise stay pinned at ground height while the model floats up above it). Also confirmed as an existing invariant, not a new one: "ground units without projectile weapons must be near the building to attack it" already held (`TickAttack`'s `armed ? Mathf.Max(6f, weapon.Range) : 32f` reach -- melee weapons carry a tiny Range by construction) but was undocumented as a deliberate rule; now called out explicitly in a comment. No wing-flap vertex animation (the mesh's wings are baked into the same static chunk as the rest of the body, not a separately posable transform, docs/08 port scope) -- deferred. Whole gameplay layer (including the real MonsterBody.cs, not a stub) compiles clean against the real citygen/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **Settled groups were packing with zero gap and creeping in far too slowly -- fixed both.** ApplySeparation's own doc comment admitted the old design: "a pair settles exactly touching" -- zero clearance was the intent, which read as bodies stacked together once a group settled. Added a SeparationGap constant (1m) into the push threshold (`minDist = Radius + Radius + SeparationGap`), so any two units -- not just settled ones, this applies everywhere separation runs -- keep at least a meter of daylight between bodies. Separately, TickSettle's creep speed was a single flat constant (1.3 m/s, "a slow shuffle") for every creature regardless of its own physiology; it now scales off the creature's own walk speed (`Locomotion`-derived, same numbers RunOrWalkSpeed already uses) with a floor, so a fast creature settles briskly and a slow one still isn't glacial. RuntimeCityBuilder.cs (previously only stub-compiled) now included for real in the gameplay-layer compile check alongside MonsterAgent/MonsterBody -- compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **"Tanks can NOT spawn within building" -- spawn placement was already building-aware; the real issue was a tank driving into one mid-chase.** SpawnTanks() already filtered candidate hexes against BlockedFor(false) (buildings), so a tank was never actually PLACED inside a building's footprint -- but Tank's own header admitted straight-line steering with "no A* -- these are combat targets, not navigators," so a tank chasing a monster on the far side of a building drove right through it, visually indistinguishable from "spawned there" a couple seconds into a session. Fixed at both ends: (1) Tank.SteerAroundBuildings probes a short look-ahead in the desired direction, then widening deflection angles each side, and only commits to a step whose landing hex is unblocked -- straight-line steering when nothing's in the way (the common case), a cheap zig-zag around a building otherwise, holding position rather than ramming if boxed in on every probed angle; still explicitly not full A* (a combat test dummy doesn't need one). (2) SpawnTanks() now also requires every IMMEDIATE NEIGHBOR of a candidate hex to be unblocked, not just the hex itself -- closes the (already low-probability) edge case where ApplySeparation shoving a tank off a crowded ring slot had nowhere to go but into an adjacent building. Compiles clean against the real citygen-core/roster-client/creature-mesh DLLs (RuntimeCityBuilder.cs and Tank.cs both compiled for real, not stubbed) | [18](18-city-battlefields.md) |
| 2026-07 | **Wings actually flap now -- bat-style, fast off the ground and to land, slower once thoroughly airborne.** Previously logged as a deliberate cut: wing geometry was baked into the SAME static mesh chunk as the rest of the body (no separately posable transform). Fixed by giving wings the exact same treatment legs already had: `CreatureMeshResult.Wing` (new `WingSocketInfo`) returns each side's full membrane/bone/finger/joint-hoop geometry as its OWN chunk set, built ROOT-RELATIVE (the shoulder joint sits at local origin, `BuildWingInto` subtracts the root from every vertex) instead of in absolute creature-space -- so Unity can parent it at the root's world position and rotate the whole thing as a rigid hinge with zero per-frame vertex work. `MonsterBody.BuildWings` mounts each side under its own pivot Transform (parented under `_torso`, so it automatically inherits the existing gait bob and flight lift for free); `UpdateWingFlap` drives the hinge: fast beats (2.3 Hz, 42 degrees) while `_flightLift` is still climbing/descending toward its target, a slower cruise beat (0.9 Hz, 22 degrees) once actually holding altitude (`_flying && liftFraction > 0.97`), folded to rest (identity rotation) once grounded. Left/right rotate with OPPOSITE local-Z signs (`angle` / `-angle`) despite otherwise-identical code, because the wing geometry itself is mirrored (side=+-1 baked into vertex data, not a negative Unity scale) -- verified via the underlying 2D rotation math that this sign flip is what makes them beat together rather than seesaw. `packages/creature-mesh`: 72 tests (was 69) including root-relative-geometry and left/right-mirror checks on the new socket. Whole gameplay layer still compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [08](08-creature-rendering.md), [18](18-city-battlefields.md) |
| 2026-07 | **Fixed "feet still stuck to the ground" during flight, and gave flying creatures a nose-down lean + turn banking.** Root cause: each leg's brass hip-joint HARDWARE (LegKit's Hip chunk -- the brace/joint ball, a separate static Transform from the articulated Upper/Lower/Foot segments) was positioned once at Build time and never touched again, so it stayed at ground-relative height forever while _flightLift correctly raised the torso and the articulated leg segments -- a literal piece of the leg staying planted on the ground while the rest of the creature flew off. Fixed by storing the hip hardware transform on the Leg record and repositioning it every frame alongside the same _flightLift offset everything else already gets. Also added flight attitude, "more rooted in actual flight": nose-down pitch proportional to forward speed (capped 22 degrees) and bank proportional to yaw rate (capped 30 degrees), both eased toward their target and applied ONLY to _torso's local rotation -- never the root transform, which stays pure-yaw for MonsterAgent's steering and the leg-rig math, so the tilt is purely cosmetic on the visible body+wings and can't fight navigation or desync the tucked legs' own frame. Settles back level the moment the creature isn't airborne. Bank's sign convention (which way it rolls into a given turn direction) depends on Unity's internal Quaternion.Euler handedness, which couldn't be verified without an Editor -- flagged as a one-constant-sign flip (FlightBankPerYawRate) if it reads backward in play. Compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **Flight gets two cruise tiers picked by energy cost, and turns became arcs instead of snaps.** Creator direction: "winged creatures should be able to fly over low buildings and decide to fly up and over others depending on what would take less energy... turns are too sharp, should be more arcs." Two cruise altitudes now exist (`MonsterBody.LowFlightAltitude`/`HighFlightAltitude`): Low is the original single-tier altitude (clears small/medium BuildingTier roofs with margin), High climbs above every tier including landmarks. `RuntimeCityBuilder.BlockedForFlight(clearAltitude)` (new -- reuses the SAME height table `BuildBuildings()` renders with, via an extracted `HeightForTier` helper, so gameplay and visuals can never drift apart) marks only buildings TALLER than the given altitude as blocking; water never blocks flight either way. `MonsterAgent.DecideFlightTier` picks Low vs High per path-compute by comparing total energy: hex-distance actually flown (weaving around tall buildings at Low, or the direct line at High) plus a one-time cost proportional to that tier's climb -- tuned so a short detour beats climbing over a building but a long one loses to it; a fully boxed-in Low route always loses to High, since nothing blocks it. Separately, arc turns: FollowPath now uses a much wider "close enough, advance to the next waypoint" radius while flying (8m vs the ground 0.6m) to cut hex-grid corners early, plus a slower heading-catch-up rate (1.8x/sec vs 5x/sec) so the turn sweeps through instead of snapping -- both scoped to `_flying` only, ground gait/steering untouched. Composes with the existing bank-into-turns tilt from the previous pass for a genuinely aircraft-like arcing turn. Whole gameplay layer compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **Winged units can land on rooftops, and heading changes sweep through smooth arcs.** Perching: the game's ground plane stopped being a single hardcoded y=0 -- `MonsterBody` gained `_groundY` (set via `SetGroundHeight`; every foot-plant, rest position, swing arc, landing target, and the shared `Airborne` definition now live relative to it), and `RuntimeCityBuilder.SurfaceHeightAt` reports the standing surface per hex (a STANDING building's roof height on its footprint, cached per city version; rubble and street are 0). The order surface: right-clicking a building's ROOF (hit normal facing up) sends winged units to perch -- fly there (cruise tier auto-bumped if the target itself is taller than low cruise), final-approach to the roof-hex center (FollowPath's wide flying arrive-radius alone could leave the landing point off the roof edge), then land with the roof as the surface; a WALL click stays an attack for everyone, so both verbs ride a plain right-click with no modifier. Perched units hold the roost (no auto-engage -- otherwise every perch would instantly dissolve into a tank chase), take off automatically for any new order (DecideFlight: no walking off a roof), and if the building is destroyed under them, the per-city-version surface re-sync eases them down onto the rubble. Ground-plane separation and avoidance now exempt airborne/perched flyers (transform y stays 0 for everyone -- altitude lives on the torso -- so a ground unit walking past a building's base could literally shove a perched flyer off its roof sideways). Smooth heading transitions: while airborne, velocity follows the NOSE while the nose slerps toward the target (carve, not strafe) -- a fresh order in any direction, even straight behind, sweeps through a banked arc instead of instantly translating sideways with the heading lagging; turn radius (speed/turn-rate, a few meters) stays comfortably inside the flying arrive radius so orbiting a waypoint is impossible. Compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **Flyers no longer descend THROUGH buildings (legs stretching/distorting); they hold altitude until the descent column is clear.** Root cause was two decoupled bugs. (1) The vertical altitude ease (_flightLift MoveTowards) was independent of horizontal position, so a takeoff off a tall roof -- or a drop to a lower cruise tier over a shorter building -- sank straight down through the structure it was over. Fixed with `MonsterBody.SetDescentFloor`: MonsterAgent pushes `RuntimeCityBuilder.SurfaceHeightAt(current position)` (the roof height of any building directly below) every frame, and the ease clamps `Max(target, floor)` -- climbing is unaffected, but descent HOLDS at the roof height until the creature has moved horizontally clear (the horizontal pathfinder already routes around tall footprints at each cruise tier; this covers the vertical dimension the ease ignored). Exactly "see if they have a clear path before they descend." (2) `_groundY` (the standing surface) stayed at the old roof height after takeoff, so the instant altitude eased below it Airborne flipped false and the legs tried to plant on a 40m plane while the torso hung at 14m -- the stretched/distorted legs. Fixed by resetting `_groundY` to street level on the takeoff edge (false->true) in SetFlying; landing sets the real new surface via SetGroundHeight just before SetFlying(false), so only takeoff resets. No margin on the floor deliberately -- a margin would stop a perch landing from ever reaching the roof surface (target == roof height there); the sub-meter tucked-foot graze during the fraction-of-a-second takeoff hold is negligible vs. the reported gross clip. Compiles clean against the real citygen-core/roster-client/creature-mesh DLLs | [18](18-city-battlefields.md) |
| 2026-07 | **The battlefield became a 1950s miniature movie set (docs/21) -- terrain, buildings, roads -- without touching the generator or the gameplay-vertical math.** Phase-1 finding that shaped everything: citygen-core ALREADY generates hills (`CityPreset.HillCount` -> `CityModel.Ridges`, the docs/04 high-ground set) -- the presentation just drew them as green blocks, so elevation SCULPTS existing gameplay data rather than inventing noise. The one rule that made real elevation safe against this session's flight/gait/perch systems: **ground under every building plot, road, and bridge is flat-locked to exactly y=0; only open terrain rolls (<=~1.5m), ridges mound to +3m (the old block height, so the high-ground read is preserved), water carves to -1.4m beds on its own already-impassable hexes.** Roof heights (6/12/30/40), flight tiers, descent floors, perch surfaces, bridge decks, and rubble all keep their existing absolute math. `TerrainField` = seeded per-hex targets + inverse-distance smoothing (banks/shorelines/hill skirts emerge); chunked generated ground meshes replace the plane's visual while the flat click-plane collider stays (<=3m hills skew a ground click well under half a hex -- accepted, docs/21 SS5); water is a sunken translucent slab inside the carved bed; units (monsters/tanks/citizens/markers) terrain-follow via `GroundHeightAt`, and MonsterBody gained a ground SAMPLER so each foot plants on the slope under itself (three-site touch to the gait: SnapFeetToGround, restW, swing-arc y-lerp -- deliberately minimal on the most creator-fought-over code in the repo). `BuildingDresser`: 1950s dressing (gables, gas-station canopies, diner chrome, brick walk-ups with window bands + fire escapes, stepped deco offices with pilasters, archetype-aware landmarks incl. a marquee'd movie palace, rooftop water towers/antennas/vents/billboards) registered INTO `_cubesByBuilding`'s damage list so it crushes into rubble and tints with cracked walls; damage tints now instantiate per-renderer materials (the dresser shares cached materials across the city -- tinting a shared one would darken every building at once). `RoadDresser`: hub-and-spoke road tiles (center pad + connector strip per road/bridge neighbor -- straights/corners/T/X/dead-ends EMERGE from adjacency, no tile catalog to desync), sidewalk+curb, lane dashes, crosswalks at >=3-way intersections, seeded street furniture (streetlights, telephone poles, hydrants, trash cans, pastel tail-finned parked cars), all colliderless so clicks/pathing are untouched. Everything hashes off (seed, hex) -- no UnityEngine.Random anywhere, the determinism contract holds. Whole gameplay layer compiles clean against the real DLLs; 212 package tests green. Known accepted: click skew on hills, cosmetic (walk-through) street furniture, coarser terrain resolution on BigCity, bridge decks still floating slightly (queued in docs/21 SS6's next-10 list) | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 2: bridges got structure, street furniture got knockable, damage got smoke/dust/rubble, billboards got period ad art, buildings got district palettes, and the map got a table edge.** Worked straight down docs/21 SS6's next-10 list (items 1, 2, 3, 5, 7, 8, 10; night mode and citizen traffic deferred as full standalone features). `BridgeDresser` (new) replaces the flat brown deck slab: guardrails along both edges, a through-truss arch over hexes that actually cross water (skipped on dry approach/embankment hexes of the same span), and piers dropping from under the deck to `TerrainField.WaterBedDepth` -- real foundations reaching down to the carved riverbed even though the deck hex itself is flat-locked to y=0. `KnockableProp` (new): street poles/hydrants/cans/parked cars topple when a combatant walks near (a throttled, staggered distance check against `RuntimeCityBuilder.Combatants`, not a per-frame cost spike) -- multi-piece props (pole+arm+bulb, car chassis+cabin+fins) are parented under a shared holder Transform first specifically so the whole assembly tips as one rigid unit instead of its pieces falling apart independently; a timed `AngleAxis` tween, no physics engine, stays down permanently once tipped. `DamageFx` (new): a Damaged building grows a lazy `SmokePlume` (spawns a fading, rising, growing puff every ~0.7-1s) for as long as it stands damaged; a Destroyed building fires a one-shot five-puff `DustBurstFx` at the collapse instant. Both reuse a shared `SmokePuff` component and deliberately avoid `ParticleSystem` (not in the compile-check stub, and consistent with the project's Update-driven-no-coroutines animation idiom used everywhere else). `RubbleDresser` (new): destroyed buildings now scatter 4-7 tumbled, unevenly-sized, randomly-rotated chunks per footprint hex over the crushed pancake, hashed off (hex, salt) -- addresses "rubble piles with silhouette, not just crushed cubes." Billboard art: `BuildingDresser`'s existing office billboard frame gained `DressPoster` (three period-ad styles -- a red bullseye soda disc, stacked movie-one-sheet color blocks, bold headline bands -- picked by hash, faked entirely with flat-color primitives since there's no texture pipeline here), and `RoadDresser` gained a sixth street-furniture option: a double-stilt roadside billboard. District palettes: `BuildBuildings` now computes each building's hex distance from `CenterHex` as a stand-in for road-graph radius (the generator seeds density outward from the same center) and picks a warmer/residential massing tint past ~55% of that radius vs. a cooler/institutional one closer in, for Small/Medium tiers (Large/Landmark keep their single tier color -- they cluster near downtown by construction anyway); dressing-level (window/wall) palette bias by district is a logged future step, not done here. `BuildTableEdge` (new, `RuntimeCityBuilder`): a raised wooden rim just past the sculpted terrain plus a flat-color painted backdrop ring well beyond it, so the map reads as a diorama on a table instead of trailing into the void at the RTS camera's typical framing -- purely decorative, outside every gameplay hex range. `UnityStub.cs` gained `Quaternion.AngleAxis` and a `Quaternion * Quaternion` operator (needed for the knock tween's tip-then-restore composition). Whole gameplay layer (five new files plus the RuntimeCityBuilder/BuildingDresser/RoadDresser edits) compiles clean against the real citygen-core/roster-client/creature-mesh DLLs; 140 citygen-core tests still green, untouched by this batch | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 3 closes out docs/21 SS6's next-10 list: neon night mode, a railyard/industrial district, and citizen traffic.** `NightMode` (new, `RuntimeCityBuilder` adds it in `Start()`): pressing N eases the city between a day and dusk lighting preset over ~1.5s. Entirely code-driven since this environment has no Editor to hand-place scene lighting -- creates its OWN `GameObject` for a directional "sun" (deliberately never touching `RuntimeCityBuilder`'s own `transform`, which every generated GameObject in the city is parented under; rotating that would rotate the whole city) and eases its color/intensity, `RenderSettings.ambientLight`/`fog`/`fogColor`/`fogDensity`, and every registered neon material between day and dusk values. `NeonRegistry` (new): `BuildingDresser.M()` and `RoadDresser.M()` now register every emissive material's BASE emission color at mint time (once, at cache-miss), so `NightMode.SetBoost` can scale from that recorded base every frame without compounding drift across repeated toggles -- neon reads faint by day (0.35x) and properly glows by night (2.2x). Railyard/industrial district: `BuildBuildings` locates the `rail_depot` landmark's site (if the preset generated one) once at `Start()`, and any building within `RoadDresser.RailyardRadius` (4 hexes) gets `industrial: true` threaded into `BuildingDresser.Dress` -- Small/Medium tiers re-skin via new `DressIndustrial` (corrugated flat roof, loading dock canopy + bollards, primary hexes add a smokestack and roof vents) instead of the usual house/gas-station/diner or apartment look; `RoadDresser.Build` takes the same landmark site and lays a parallel rail siding (two steel rails + periodic ties) alongside straight road hexes in that radius, tying the depot into a small coherent district rather than one isolated set piece. Large/Landmark tiers keep their existing look (a factory reads fine as a stepped office shell; the depot itself already IS the archetype set piece). Citizen traffic (docs/19): `TrafficCar` (new) drives the same road-hex-plus-bridge-deck network `RoadDresser` already computes (now cached on `RuntimeCityBuilder.RoadNetworkHexes()`), hopping to a neighbor network hex on arrival -- wandering picks a stable pseudo-random neighbor, but the moment `NearestMonsterTo` reports a threat within range it immediately re-picks toward whichever reachable neighbor is FARTHEST from the threat's position (redirecting instantly, not waiting for the current hop to finish) and shifts into a faster flee speed, mirroring `Citizen.cs`'s established flee pattern but constrained to the road graph instead of any passable hex. Colliderless and excluded from `_combatants` -- cosmetic crowd, not an order target or an obstacle, same scoping as parked cars and Citizens. `UnityStub.cs` gained `Light`/`LightType`/`LightShadows`/`RenderSettings` (none of the compile-check stub's surface previously touched lighting at all -- every other visual system in this whole arc, terrain through table edge, was pure geometry/material, so this is the stub's first lighting-API extension) and a `Keyboard.nKey` field. Whole gameplay layer (three new files plus the RuntimeCityBuilder/BuildingDresser/RoadDresser edits) compiles clean against the real DLLs; 140 citygen-core tests still green, untouched. docs/21 SS6's ten-item list is now fully closed | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md), [19](19-citizens.md) |
| 2026-07 | **Fixed "roads running north south are zig-zagging" in SmallTown/BigCity -- a hex-grid geometry issue, not a rendering glitch.** Root cause, confirmed by deriving `HexCoord.ToWorld`'s axial-to-world formula by hand: a pointy-top hex has exactly one direction family that's perfectly straight in world space (fixed R, i.e. row-based E/W streets -- z depends only on R) plus two straight 60-degree diagonals, but NO single edge points due south. `CityGenerator.IsRoad`'s Grid (`col % pitch == 0`, BigCity) and MainStreet (same, for the perpendiculars) patterns use offset-COLUMN roads, which approximate "south" the only way integer coordinates can: alternating between two different diagonal hex edges every row (SE from an even row, SW from an odd row) -- this makes the true world-space x of a "vertical" road hex take exactly TWO values, HexMeters/2 (10m) apart, alternating every single hex. `RoadDresser` rendered this literally (a strip toward each true neighbor, from the raw hex center), so it sawed left-right by 10m at every hex -- invisible back when roads were flat abstract blocks, glaringly visible once they became real hub-and-spoke street geometry (docs/21 Phase 4). Confirmed the road hex SET itself (gameplay/pathing truth) is completely fine -- this is purely how RoadDresser was drawing it. Fix, scoped entirely to `RoadDresser.cs` (citygen-core untouched -- no risk to determinism, the golden test, or pathfinding): new `TryStraightenCardinal` identifies a hex whose ONLY road connections are that row-parity-specific diagonal pair (a real turn, dead-end-against-a-cross-street, or 3+-way intersection always has a different connector set and is deliberately left alone by the exact-count check) and, for those hexes only, rewrites its connectors to a due north/south bearing and shifts its render anchor by exactly +-HexMeters/4 (the precise midpoint between the two alternating raw offsets) -- proven algebraically and numerically (a standalone harness against the REAL `HexCoord`/`HexEdge` types, not a stub: a 13-hex synthetic column all landed on the exact same corrected x, a synthetic junction hex was correctly left unstraightened) that adjacent corridor hexes' corrected anchors coincide exactly, turning the sawtooth into one continuous straight street with at most a single small jog right at a junction (absorbed by the intersection's wide pad). Row-based E/W streets and Village's radial spokes were never affected (already exactly straight by construction) -- matches the report, which only named north-south. Whole gameplay layer compiles clean against the real DLLs. Known related but out of scope: `BridgeDresser` computes its own per-hex bearing independently and could show a smaller residual version of the same artifact if a north-south street happens to cross a river; not fixed here since the report was specifically about roads | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Fixed bridges rendering as static brown diamonds instead of following the road's own heading.** `BridgeDresser.Build` computed the crossing's direction (`facing`/`deckRot`) and correctly rotated the guardrails, through-truss beams, and piers to match it -- but never applied that rotation to the deck slab itself, which stayed a fixed 18x18m axis-aligned square. Wherever a bridge runs at an angle to world axes (effectively always, since hex grids don't align to world axes the way a square grid would), the unrotated square's corners poked out past the correctly-angled rails, reading as a plain brown diamond sitting under a structure that was otherwise facing the right way. Fix: compute `facing`/`deckRot` FIRST (moved ahead of the deck spawn, was previously computed after), then spawn the deck as a RECTANGLE -- narrow across (rail-to-rail span, so the rails land right at its edges) and long along the direction of travel (matching the rails' own length, a near-full hex pitch so consecutive bridge hexes tile with no visible gap) -- rotated to `deckRot`. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **CityGizmo's seed/preset now drives RuntimeCityBuilder when they share a GameObject -- checked for a reason they shouldn't, found none.** Both components had their own independent `PresetChoice` enum plus a `seed` field, with nothing wiring them together: `CityGizmo` is the Scene-view preview (docs/18 SS2 smoke test, no Play needed), `RuntimeCityBuilder` is the actual Play-mode build -- the natural workflow is tune-in-Editor then hit Play, which silently broke if you forgot to copy the seed/preset into RuntimeCityBuilder's separate fields too (Play would build a DIFFERENT city than the one just previewed). Considered reasons to keep them decoupled and rejected all of them: the two components are often on DIFFERENT GameObjects for genuinely separate previews, but `GetComponent` only ever finds one on the SAME GameObject, so a same-GameObject sync can't affect that case; the two `PresetChoice` enums are distinct nested types, but that's a two-line name-based mapping, not a real obstacle; there's no gameplay/determinism risk since this is pure Editor-configuration plumbing, zero effect on the generator or the golden test. Conclusion: the disconnect was purely historical (`CityGizmo` predates `RuntimeCityBuilder`, per the README's own "the first proves the package reference works, the second draws a full city" framing) -- nobody had wired them together, not a deliberate design choice. Fix: `RuntimeCityBuilder.Start()` now checks `GetComponent<CityGizmo>()` first; if present, it adopts the gizmo's `seed` and (via a new name-based `ConvertPreset`, reorder-safe against the two enums drifting apart) its `preset`, before generating -- RuntimeCityBuilder's own Seed/Preset Inspector fields are simply ignored in that case, documented in their tooltips. `CityGizmo.cs` was never in the compile-check harness before (only `RuntimeCityBuilder.cs` and the gameplay scripts were) -- added it, plus a `Gizmos` stub (`DrawCube`/`DrawWireSphere`/`DrawLine`/`color`), so it's now part of the real-DLL compile check going forward too. Whole gameplay layer compiles clean | [18](18-city-battlefields.md) |
| 2026-07 | **Bridges: fixed units visually clipping through the deck, and RoadDresser silently double-dressing bridge hexes underneath it -- a "gap in geometry placement" audit prompted directly by the creator after the diamond-deck fix.** Two compounding bugs on the same hexes. (1) `TerrainField` flat-locks bridge footprint hexes to exactly y=0 -- the SAME rule as roads and buildings -- and every ground unit's `GroundHeightAt` puts its feet at that flat-locked height. But the deck spanned y=[0, 1.2] (its BOTTOM face at 0, top at 1.2): a unit crossing a bridge had its feet exactly at the deck's underside, so it visually clipped through/under a meter-plus of solid "deck" instead of standing on top of a crossing. Fixed by dropping `DeckHeight` to 0.5 and `DeckY` to 0.05, putting the deck's TOP at ~0.3 -- matching `RoadDresser`'s own established "slightly proud of the ground" asphalt height (its strips top out around 0.34), so a bridge now sits at the SAME height convention every ordinary road already uses instead of being ~1m off from it. Truss beam/top-chord anchor heights, previously magic offsets from the deck's CENTER, were re-expressed relative to a computed `deckTop` local so the arch keeps its proportions if the deck constants ever change again; guardrails and piers were already deck-top/deck-bottom relative and needed no change. (2) Independently discovered while tracing (1): `CityModel.Roads` already includes every bridge deck hex (`CityGenerator.cs` unions `bridgeHexes` into `roadSet` -- "Drowned road segments vanish; bridge decks survive as road"), so `RoadDresser.Build`'s `foreach (var hex in city.Roads)` loop was ALSO independently dressing bridge hexes with its own thin street pad, connector strips, dashes, crosswalk stripes, and street furniture (parked cars, poles, hydrants could spawn ON a bridge deck) -- competing, z-fighting geometry mostly buried under (or poking through) `BridgeDresser`'s deck. Fixed by collecting bridge footprint hexes into their own set and skipping `DressHex`/rail-siding entirely for any hex in it -- the hex STAYS in the connectivity `network` set, so a bank hex's own connector strip still reaches into the bridge threshold for visual continuity, only the bridge hex's OWN rendering is now solely `BridgeDresser`'s. Known still-open, logged previously and unaffected by this fix: `BridgeDresser` computes bearing independently of `RoadDresser`'s north-south-corridor straightening, so a bridge crossing a zigzagging corridor could still show a small residual kink. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Bridges were askew -- not lining up with roads or with each other -- because they zigzag too, and only RoadDresser knew how to straighten it.** Direct follow-up creator report after the height/overlap fix. Root cause: bridge deck hexes are ordinary members of `city.Roads` (`CityGenerator` unions them in), so a "vertical" Grid/MainStreet corridor that happens to cross a river zigzags through its bridge hexes by the exact same offset-column mechanism that used to zigzag ordinary roads (docs/12's earlier "roads running north south are zig-zagging" entry) -- but `RoadDresser.TryStraightenCardinal` only ever ran on `city.Roads` hexes it was itself dressing, and `BridgeDresser` computed its own facing independently (searching only for neighbors within the SAME bridge's own footprint), with no straightening applied at all. Result: each bridge hex's deck kinked relative to its neighbors (both other bridge hexes AND the now-straightened approach road on the bank), reading as visibly askew even after the previous fixes made everything else about a bridge's geometry coherent. Fix: made `TryStraightenCardinal` `public` and had `BridgeDresser` call the IDENTICAL function against the IDENTICAL `network` (`new HashSet<HexCoord>(city.Roads)`, built once) that `RoadDresser` uses -- one shared source of truth instead of two independently-computed bearings, so a hex on the seam between the two dressers gets the exact same corrected anchor from whichever one draws it. `BridgeDresser`'s per-hex direction search was also widened from "same bridge footprint only" to the full road network (still just building a `connectors` list the same shape `RoadDresser` already builds), which incidentally fixes a separate latent bug: a single-hex bridge has no same-footprint neighbor to search, so it was defaulting to a hardcoded `Vector3.forward` regardless of the crossing's real direction -- it now correctly finds its bank-hex neighbors on both sides. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Fixed trees spawning through buildings on ridge hexes -- continuing the geometry-placement audit the bridge fixes started.** `CityModel.Ridges` is only filtered against roads/water in the generator ("Ridges never coincide with roads or water" -- `CityGenerator.cs`), but buildings are placed in a LATER pass (`PlaceLandmarks`/`PlaceBuildings`) that treats ridge hexes as ordinary buildable open land (`blocked` for block-finding is only `roadSet ∪ allWater`, never ridges) -- so a ridge hex can end up carrying a building footprint. `TerrainField` already handles this correctly (its `flat` set, checked FIRST, flat-locks building/road/bridge hexes to y=0 regardless of ridge status -- buildings win over the mound, no terrain conflict). But `RuntimeCityBuilder.ScatterVegetation`'s ridge-tree loop never cross-checked this: it unconditionally spawned 2-3 trees per ridge hex, so a ridge hex claimed by a building would still sprout trees, positioned (via a +-6m offset from hex center) well inside that building's ~18m-wide massing cube -- trees growing through/inside a building. The shore-bush loop two lines below already had the correct pattern (`if (... || blocked.Contains(nb)) continue`), just never applied to the tree loop above it. Fix: skip ridge hexes already in `blocked` (`BlockedFor(false)`, already computed once at the top of the method) before spawning trees -- one line, reusing data the method already had. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 4: plaza/school/old-age-home landmarks stopped defaulting to the movie palace, and destruction got a scorch mark.** With the geometry-placement gap audit clean, this batch is fresh visual variety rather than a bug fix. Found while reviewing `DressLandmark`'s archetype switch: it only special-cased `church`/`cathedral`, `town_hall`, `rail_depot`, and `hospital` -- every OTHER archetype fell through to a `default` case explicitly commented "plaza / school / old_age_home -> THE MOVIE PALACE". Checking `CityPreset.cs` made this worse than it sounds: `plaza` is in ALL THREE presets' `EmitterArchetypes` list (Village, SmallTown, BigCity all include it), and `school`/`old_age_home` are 2 of the 3 fixed `Hubs = { hospital, school, old_age_home }` used identically by every preset -- meaning plaza/school/old-age-home are among the MOST COMMON landmark archetypes in any generated city, and every single one of them rendered as an identical movie theater. Gave each its own set piece, keeping the existing 40m landmark-tier massing cube (not touched -- that's `HeightForTier`'s domain, out of scope and working correctly) but dressing it differently: `plaza` gets a colonnade + entablature (a grand civic building FRONTING the square, since the tall cube can't literally BE an open square), a rooftop clock cupola, and a small fountain on the plaza pavement in front of it; `school` gets a plainer, smaller columned entrance, a bell cupola, and a flagpole; `old_age_home` gets a wraparound porch roof, a dormer-ish roof projection, and a garden trellis (new `GardenGreen` palette color) -- reading residential/homely instead of institutional. `default` now only catches genuinely unlisted future archetypes, still as the movie palace (a sensible fallback, not the common case anymore). Separately: destroyed buildings now leave a dark, flat, terrain-following scorch decal under each footprint hex (`RuntimeCityBuilder.SpawnScorchDecal`, called alongside the existing rubble scatter and dust burst) -- the rubble pass already darkened the wreckage itself, but the ground it fell on stayed unmarked; a scorched-earth read was the obvious missing beat. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 5: district palette bias now reaches the dressing itself, closing the last logged gap in docs/21 SS6 item 10.** Batch 2 (2026-07) tinted only the massing cube by district and explicitly logged "dressing-level (window/wall) palette bias by district is a logged future step, not done here." This batch does it: `BuildingDresser.Dress` gained a `suburb` parameter (threaded from `RuntimeCityBuilder.BuildBuildings`'s existing district computation, unchanged), passed through to `DressSmall`/`DressApartment` only -- `DressIndustrial` stays utilitarian regardless of district (a warehouse doesn't care), and Large/Landmark are untouched (they cluster near downtown by construction, same reasoning the massing-tint pass already used). `DressSmall`'s house/gas-station/diner TYPE pick is now suburb-weighted (60% house in suburbs vs 20% downtown, the rest split gas/diner) instead of an even three-way split, and the suburban-house case's roof color leans warm terracotta in suburbs vs cool slate downtown. `DressApartment`'s wall material (previously an even Cream/Brick/Seafoam three-way split) now leans 50% cream in suburbs vs 50% seafoam downtown, with the other two colors splitting the remainder either way -- reweighted, not monotone, so a district reads as a TENDENCY, not a uniform color block. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 6: cathedral got its own grander look, traffic got delivery vans, and hillsides got the occasional boulder.** With the original docs/21 SS6 list closed at full depth (batch 5), this is a fresh small-scope batch. `DressLandmark`'s `church`/`cathedral` cases were previously identical (a shared switch fallthrough to one spire) -- split them the same way plaza/school/old_age_home were split from the movie-palace default in batch 4: `church` keeps the single spire, `cathedral` gets twin flanking towers (each taller than the parish spire) plus a rose window accent on the front face, so picking cathedral over church actually reads as a grander building instead of a recolor of the same one. `TrafficCar` gained a second body style: a boxy 1950s delivery van (one tall rectangular body plus a dark windshield-band accent) picked via the same FNV-style hash idiom every other dresser uses, roughly a quarter of spawned cars, alongside the existing sedan -- deliberately a single-shape body rather than a second multi-piece rig, to keep the part-count/positioning risk low for a variant that can't be visually checked before shipping. `RuntimeCityBuilder.ScatterVegetation` gained `SpawnRocks`: a quarter of ridge hexes (that pass the existing building-blocked check from the tree fix two entries back) also get 1-2 tilted gray boulders, terrain-following and deterministically placed/rotated, so a hillside reads as mostly trees with the occasional rocky outcrop rather than uniform forest. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 7: eaten citizens leave a fading blood splatter, and the rail depot got a platform and ticket booth.** Two small, independent additions. `DamageFx.BloodSplatter` (new, alongside the existing `SmokePlume`/`DustBurstFx`): a dark, flat ground decal spawned at a citizen's position the instant it's eaten, via a new `GroundStain` component -- holds for 9s, then fades over the next 5s and self-destructs, so a long match's running citizen-eaten count (`CitizensEaten`) doesn't accumulate into permanent ground clutter, while still giving the "something just got eaten here" horror-movie beat the mechanic deserved but never had a visual for. Wired into `RuntimeCityBuilder.OnCitizenEaten` -- samples `GroundHeightAt` for the decal's y (a citizen's own transform.y carries its +0.9 body-height offset per `Citizen.cs`'s convention, wrong for a ground decal) and reads the citizen's position BEFORE it gets destroyed the line after. `rail_depot`'s landmark dressing was just the trainshed cylinder, floating with no sense of a working station -- added a concrete platform slab and a small cream ticket booth with a rust-red roof out front, tying the depot visually into the railyard/industrial district work from batch 3 instead of reading as an isolated shed. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 8: landmark mechanic radii finally visible in-game, and sheared hydrants gush water.** (1) The docs/03 emitter aura (3 hexes) and docs/18/20 Collection Station harvest radius (5 hexes) are real gameplay data that `CityGizmo` draws as wire spheres in the SCENE view -- but the actual game, in Play mode, never showed them at all; a player had no way to see where an aura ends. `RuntimeCityBuilder.BuildLandmarkAuras` (new): each landmark gets a ring of 18 short emissive pylons at its mechanic's world radius, teal for emitters / red for hubs (the exact color code the gizmo already established), terrain-following, colliderless. Pylons whose position lands on a building-blocked or water hex are SKIPPED rather than force-placed -- the ring reads fine through a gap, while a pylon poking out of a roof or floating on water would read as a glitch (the same placement-mindfulness the bridge/tree fixes established). Registered with `NeonRegistry`, so the rings brighten at night with every other emissive. (2) `KnockableProp` gained a `SpawnsWaterJet` flag, set only by RoadDresser's fire-hydrant case (`MakeKnockable` now returns the component to make that one-line settable): when the hydrant tips, `DamageFx.WaterJet` spawns a `WaterSpout` that fires blue-tinted droplets hard upward (a new `SmokePuff.InitJet` overload takes a fully-specified drift, unlike smoke's lazy rise or dust's outward roll) for 6 seconds, then peters out and self-destructs -- the classic B-movie sheared-hydrant street beat. Stub additions this batch: `Transform.parent`, `Behaviour.enabled`. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md), [03](03-mana-system.md) |
| 2026-07 | **Fixed destroyed buildings reading as "radiating puddles" instead of building chunks -- a same-turn creator correction, fixed before the pond/bank work it interrupted.** Root cause: the Destroyed-stage crush squished EVERY entry in a building's `cubes` list uniformly -- including the massing cube itself, an 18m-wide (hexSize*0.9) full-footprint slab that got flattened to 12% of its original height IN PLACE. A wide, uniform, flat rectangle read as a spreading stain from the RTS camera, not broken masonry; the batch-4 scorch decal (a 9m-radius circle per hex) compounded it, reading as a second "radiating" shape. Fix: `cubes` has a reliable structural invariant (verified against `BuildBuildings` before relying on it) -- the first `footprint.Count` entries are always the massing cubes, the next `footprint.Count` are the dressing holders, in that order, since the footprint loop appends cubes first and `BuildingDresser.Dress` appends exactly one holder per hex right after. `ApplyBuildingDamage` now uses that index split: massing-cube entries are destroyed outright and replaced by `RubbleDresser.Shatter` (new) -- 3-5 large tilted wall-section-scale slabs (5-9.8m wide, 0.8-2m thick, steeply varied rotation) scattered across the hex instead of one flat pancake; dressing-holder entries still squish in place as before (already smaller/varied pieces -- windows, cornices, water towers -- so flattening them already read as debris, not a slab). `SpawnScorchDecal` shrunk from one 9m-radius disc per hex to 2-3 small (2.2-3.8m) irregular patches, so it reads as scattered scorch marks, not a second puddle. `RubbleDresser.Scatter`'s existing small debris chunks are unchanged, now layering on top of the shattered slabs instead of a pancake. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **World upgrade batch 9: ponds got lily pads and cattails distinct from the river, and shorelines got a smooth indented bank instead of a straight ramp.** Two additions, both presentation-only (citygen-core untouched). Banks: `TerrainField` gave open-ground hexes touching water a new case -- instead of their normal `Roll()` noise, a shoreline hex targets a shallow recessed height (`BankRecess = -0.55f`, gently varied +-0.2 off the SAME per-hex noise function so it isn't perfectly uniform) before the EXISTING inverse-distance blend runs unchanged. Since the blend already turns per-hex targets into continuous slopes, this one new per-hex case is enough to turn "open ground ramping straight down to the bed" into "open ground -> a recessed lip -> the bed", i.e. a smooth indented bank, with zero changes to the sampling/meshing code itself -- exactly the architecture's existing pattern (assign targets, let blending do the smoothing) extended by one rule. Ponds vs. river: citygen-core's `CityModel.Water` doesn't distinguish which hexes are the river vs. a pond (both generated separately -- `CarveRiver`/`CarvePonds` -- then unioned into one set with no tag surviving). `RuntimeCityBuilder.PondHexes` (new) infers it at the presentation layer with a plain BFS over hex adjacency: the river is carved as a single band guaranteed to span the full map width, so it's reliably the LARGEST connected component; everything else is called a pond. Pond hexes grow floating lily pads (`SpawnLilyPads`, sitting just above the water slab's surface) and their shoreline grows cattail reeds (`SpawnCattails`) instead of the plain shore bushes the river keeps -- so a player can now tell "this is a pond" from "this is the river" by looking, not just by memory of the map layout. Whole gameplay layer compiles clean against the real DLLs | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **The economy became a living, per-unit system: docs/22 written (onboard blood/bone/brain pools, medics, storage, factories), on direct creator direction.** Creator brief: harvesting of blood (fuel), bone (armour), brains (grey matter); units burn fuel and lose armour when attacked; resources built INTO units so a damaged unit loses blood/bone and works less efficiently until it consumes or refills, RPG-style; medical/repair units drawing from harvested stores; blood banks and bone piles built by specialized gatherer units, monsters able to go there directly; StarCraft-style factories creating units from the stable and upgrading them; blood/bone/brain capacities; and above all "it must be fun, and NEVER annoying." New [22-economy-system.md](22-economy-system.md) designs exactly that, building on rather than replacing the existing wallet economy ([05](05-component-economy.md)/[20](20-harvest-and-repair.md)): three onboard pools with genome-DERIVED capacities (no schema change -- heart tier->blood, Bones bill->bone, brain tier->brain, same derived-stat pattern as Locomotion/Combat.Profile); damage spills blood and chips bone (melee chips more, energy less); depletion degrades toward explicit FLOORS (~two-thirds effectiveness worst case, degradation capped, no death spiral) -- the superseding decision: doc 05's wallet-zero 2%/s decay damage rule is deleted as exactly the annoyance pattern the creator forbade (Q25 tracks reconciliation). Five refill paths ranked by automation (eat citizens -- now also refills the eater onboard; slurp battlefield blood spills; stand near storage; Sawbones medic; the Vat, where upkeep also pauses). New unit classes: Sawbones (auto-triage medic, field Repair at half Vat rate + pool transfusion, one-per-patient no-stack rule as the anti-deathball guardrail) and Ghoul (auto-scavenging gatherer that makes the 15s salvage window catchable without micro, and the sole builder of structures). New structures: Blood Bank / Bone Pile / Brain Trust (storage + wallet-cap extension -- wallet caps are new, the classic supply-structure shape, Q28) and the Stitchworks factory (forward reanimation from the Menagerie at 1.5x Vat time with a queue, plus match-scoped +50% capacity field augments -- explicitly runtime state, never genome, same boundary Repair established). SS1 is a five-rule fun-first/never-annoying design contract (floors not stalls; auto-first; one-glance readability; refills staged as power-up beats; legible degradation) that future changes to the system are required to pass. Origin-energy rule honored throughout (organic blood / tech fuel / biotech ichor, [17](17-factions.md)). Q25-Q29 opened; 00-index gained rows for 21+22 and seven glossary entries; 11-roadmap Phase 2 note added. Docs only -- no code this pass; the Unity battlefield already has the harvest wallet and eat-yields to build on when implementation starts | [22](22-economy-system.md), [05](05-component-economy.md), [20](20-harvest-and-repair.md) |
| 2026-07 | **Harvester morphology implemented in the Lab as real genetics -- six new catalog families, a harvest.ts derived-stat module, and Lab renderer support; the golden digest regenerated (deliberate versioned change).** Creator brief: harvesting units in the Lab with speedy legs; claws/saws/suction that pull blood from units alive or dead; blob body storage capacity; human units a metal tank, aliens an alien storage option; "this should feel like a part of the lab system not a bolt on"; flying units must take weight into account. Realized as PARTS, not a unit class: three hand-homolog harvest tools, one per origin (`lamprey_maw` organic suction -- Blood x3 and drains LIVING targets; `bone_saw` tech surgical saw -- Bone x3, corpses only; `ichor_siphon` biotech drinking tubes), and three sensor-homolog storage vessels (`storage_bladder` organic sloshing sac, `steel_tank` tech riveted tank with a blood-level sight gauge, `amber_vesicle` biotech glowing cluster) -- storage rides the EXISTING sensor slot rather than adding a new one (no schema change, the Hox grammar untouched), making the trade real: a tank on your back is a sensor you don't have. New `harvest.ts` (pure derived stats, the energy.ts pattern): gather rates by hand family x expressed tool size, with generic hands gathering badly and guns barely at all ("speedy legs" need no new mechanism -- speed was always the legs' job, so fast haulers are assembled from existing axes); capacity = base + bulk + vessel expression, blob plan x1.5 ("an amorphous body IS a bag"); laden speed factors floored per docs/22 SS1 (ground -25% max, floor 0.6; FLIGHT -50% max, floor 0.4 -- weight counts double aloft, but a full flyer is slow-and-juicy, never grounded). 10 new genome-core tests (61 total green across genome-core+mutator-service after); catalog addition shifted `familiesInClass` and therefore the deterministic mutation stream, so the golden lineage digest was regenerated via the sanctioned `npm run test:update-golden` path -- the exact "versioned breaking change, only deliberately" case the golden test exists to police. Lab site: all six families get real draw cases in creature-renderer.js (sucker mouth with tooth rings, spinning-look saw disc, translucent pulsing siphons, sloshing half-filled sac via setAlpha layering, riveted tank, breathing amber vesicles); vendored site/lib refreshed (harvest.js now ships to Pages). Since mutation/generation pull from familiesInClass, the new parts enter the breeding pool automatically -- players can create, breed, or build harvesters with zero new UI. Known follow-ups, logged not hidden: C# ports (creature-mesh renders the six families as default shapes for now; roster-client Weapon.cs maps harvest tools to Unarmed via its existing default -- graceful, not broken) and Unity battlefield gather/carry behavior wiring harvestProfile into MonsterAgent | [22](22-economy-system.md), [15](15-part-genetics.md), [17](17-factions.md) |
| 2026-07 | **Harvester follow-ups landed: C# harvest twin, battlefield visuals for the six families, and a live gather/carry/weight slice in Unity.** The two bricks flagged when harvester morphology first shipped, both done. (1) `roster-client/Harvest.cs` -- a line-faithful C# port of genome-core's harvest.ts (gather rates by hand tool x expressed size, capacity from a storage vessel + bulk + the blob x1.5 bag bonus, and the floored ground/flight weight factors), GOLDEN-VERIFIED against the real JS running in node (7 fixtures captured, exact-match to 1e-3), the identical discipline as the Locomotion and Weapon ports -- so a harvester gathers/carries/slows identically in the Lab preview and on the battlefield instead of drifting. Required embedding the length/girth canalized bounds for the affected families (a subset of catalog.ts), kept honest by the golden test. (2) `creature-mesh/CreatureBuilder.cs` gained real geometry for all six families -- lamprey sucker-mouth with concentric tooth rings, bone-saw blade on an articulated boom, translucent-rendered-opaque siphon tubes (the per-vertex-alpha channel is the same one dropped for the blob's gelatin in the pass-1 port, docs/12), a fluid-filled storage sac, a riveted steel tank with a red sight gauge, and a glowing amber vesicle cluster; 8 new tests (80 total, was 72) assert each builds real geometry and reads in the right material (metal tank, amber cluster, ichor siphon). (3) Unity `MonsterAgent` now reads `Harvest.Profile`: eating a citizen strips a load into an onboard `_carriedLoad` scaled by the creature's blood-gather rate (a lamprey-and-tank build becomes a genuine hauler); that load slows the carrier through the exact `Harvest.GroundSpeedFactor`/`FlightSpeedFactor` (empty = no-op, so nothing non-harvesting is touched; floored so it never strands; DOUBLED for flyers -- the creator's explicit "flying units take weight into account" rule, now real); and a laden harvester idled back within ~2.5 hexes of its spawn banks the load to the wallet automatically and recovers speed. Deliberately never-annoying per docs/22 SS1: the auto part is the unloading (no button), but the HAULING is the player's order -- no unit walks off on its own. Whole gameplay layer compiles clean against the rebuilt real DLLs; genome-core 51 / roster-client 56 / creature-mesh 80 / citygen-core 140 all green. Still design-only: storage structures, medics, factories, and the full three-pool onboard economy of docs/22 S2 (the single pooled `_carriedLoad` is its first working slice) | [22](22-economy-system.md), [08](08-creature-visualization.md), [18](18-city-battlefields.md) |
| 2026-07 | **Storage vessels fixed: on the BACK, not the head, and coloured RED for blood / WHITE for bone (creator correction).** When storage vessels first shipped they rode the sensor homolog and so mounted on the HEAD (where antenna/horn/mast sit), rendered a pair, and were coloured by origin (amber for the alien one). The creator: tanks belong on the creature's BACK, and read RED for blood / WHITE for bone. Both fixed in BOTH renderers (site JS + C# creature-mesh, kept in lockstep). Back-mount: a new `dorsalSock`/`DorsalSock` derives a single dorsal mount GENERICALLY from each plan's own geometry -- the eye socket gives the torso's FRONT depth, so its negation is the back face; height sits on the upper back below the head; the normal points up-and-back so the tank rests on the spine -- so it works for all nine plans without touching any plan builder, and mounts ONE tank (mirror off), not a head-flanking pair. Contents colour: driven by the HARVEST TOOL, not the vessel (a container doesn't know at breed time what it'll hold, but the tool does) -- bone-dominant tools (`bone_saw`/`chain_blade`/`pincer`) fill the tank with bone (white), every other tool with blood (red). Applied to the fluid (bladder), the end-caps + sight gauge (steel tank, whose metal shell stays for the human-army origin read), and the vesicle glow (the biotech cluster, which stopped being amber). The origin-by-SHAPE / resource-by-COLOUR split keeps BOTH earlier creator asks intact: metal tank = tech, sac = organic, cluster = alien (shape), and red/white = blood/bone (contents). C# threads the colour through `BuildPart` and a new `Ctx.StoreIsBone`; JS through the slot loop's `o.store`. Tests updated (the old "amber" assertion replaced by a red-for-blood / white-for-bone check across all three vessels, plus a back-mount test asserting the vessel geometry sits behind the body's mid-plane); creature-mesh 82 green (was 80), roster-client 56, genome-core 51, whole Unity layer compiles clean. Site JS re-checked with node --check | [22](22-economy-system.md), [08](08-creature-visualization.md) |
| 2026-07 | **Storage vessels re-seated: dead centre of the back, sunk INTO the trunk, tech gets a real backpack (creator correction: they were floating).** After the head->back move, the vessels still sat wrong -- too near the neck/tail and floating proud of the surface. Fixed in both renderers (site JS + C# creature-mesh, lockstep). `dorsalSock`/`DorsalSock` now seats DEAD CENTRE of the trunk: height is the midpoint between the waist and the shoulder/hand mount (not up by the neck, not down by the tail), and the normal is the near-vertical back's true outward normal (mostly backward, slightly up) rather than the earlier up-and-back guess. Because a torso back is near-vertical, the vessel geometry was rewritten world-axis (vertical tanks, Z-depth) and PUSHED INTO the body (+Z is into the trunk) so nothing floats: the tech `steel_tank` is now a riveted BACKPACK -- a rectangular frame plate seated flat (rear half sunk into the trunk) with two cylinder tanks INSET into the frame front-flush, plus a contents-coloured sight gauge and corner rivets for the functional read; the organic `storage_bladder` is 2-3 pus-filled sacs half-sunk in the trunk and bulging out THROUGH a taut skin cap ("puss filled blobs pushing through the skin"); the biotech `amber_vesicle` is a cluster fused ~40% into the back, swelling out through the hide and glowing. All three keep the origin-by-form / resource-by-colour split (metal backpack / sac / vesicle cluster; RED blood / WHITE bone). creature-mesh 82 tests still green (back-mount + red/white checks unchanged), whole Unity layer compiles clean against the rebuilt DLL, site JS node-checked | [22](22-economy-system.md), [08](08-creature-visualization.md) |
| 2026-07 | **Storage packs stopped floating off necks and tails: every plan now declares its own Back mount, and packs lie flat on top of horizontal bodies (creator correction + "check your work").** The report: on arachnid/crab the backpack/vessel hung near the neck or tail, floating -- it must sit ON TOP of those bodies, horizontal, like a real backpack, never near the tail. The check-your-work audit confirmed worse: the one-size generic `dorsalSock` derivation (eye-socket depth -> a point behind the body, vertically oriented) only ever made sense for an upright torso -- on the serpentine it produced a pack floating in midair behind the S-neck, and on the floater a pack buried INSIDE the hull. Fix, both renderers in lockstep: (1) the generic guess is demoted to a fallback; each of the NINE plan builders now returns an explicit `back` socket computed from its own real geometry in-scope -- tetrapod/winged vertical mid-back at the chest level's true rear face (winged deliberately BELOW the wing roots), treant mid-trunk bark, floater the fuselage waist's rear surface, avian the sloped upper back between chest and shoulders (clear of the tail counterbalance), CRAB flat on top of the carapace biased forward, ARACHNID flat on top of the abdomen's crown biased toward the waist (nothing over the spinneret end), serpentine strapped on top of the thickest coil at the neck base, blob on top of the mound. (2) Pack geometry is now authored ONCE in a local frame (across/along/out, positive out = away from the body, negative = sunk in) and mapped by mount orientation -- `packP`/`packR` (JS) / `PackP`/`PackR` (C#): a mount whose normal points mostly UP (crab/arachnid/serpentine/blob) lays the whole pack HORIZONTALLY (tanks lying along the body axis, plate flat on the shell); a backward normal lays it vertically as before. Same seated-into-the-body offsets in both orientations, so nothing floats on either kind of body. This also fixed a latent bug the frame conversion surfaced: the steel tank's tanks/rivets were positioned in absolute coordinates (assuming mount x=0) rather than relative to the mount point. In the Lab, packs ride each plan's own breath/gait vertex channels so they move WITH the body. New theory test (crab + arachnid): every metal chunk of a steel tank must centroid ABOVE the waist and never trail behind the rear half -- 84 creature-mesh tests green (was 82); whole Unity layer compiles clean against the rebuilt DLL; site JS node-checked | [22](22-economy-system.md), [08](08-creature-visualization.md) |
| 2026-07 | **Steel tank redesigned as a single cylinder backpack (creator direction: "human monsters always use the cylinder backpack with proper orientation").** The tech `steel_tank` had settled into a rectangular frame plate with two inset cylinder tanks -- solid and functional, but not the classic silhouette asked for. Replaced in both renderers (site JS + C# creature-mesh, lockstep) with ONE barrel: a saddle collar seats it against the mount (sunk deepest, `-sink*2.2`), the barrel itself sits mostly PROUD of the hide rather than embedded (`sink = tR*0.18`, far shallower than the organic/biotech vessels' ~0.4-0.45 sink -- a strapped-on tank should read as hardware bolted on, not flesh grown around it), contents-coloured end caps (RED blood / WHITE bone), a filler/valve cap, a contents-coloured sight gauge running the barrel's length, and two rows of strap rivets. Reuses the existing `packP`/`packR` (`PackP`/`PackR`) pack frame untouched, so the orientation fix from the previous entry carries over for free: the tank still stands vertical on upright bodies (tetrapod, winged, avian, treant, floater) and lies flat on top for horizontal ones (crab, arachnid, serpentine, blob), still dead centre of the back, still clear of the tail. No test changes needed -- all 84 creature-mesh tests (including `SteelTankShellIsMetal`, the red/white contents check, the back-mount check, and the crab/arachnid on-top-and-metalChunks>=2 check) passed against the new geometry unmodified, because the barrel + saddle collar still emit two distinct METAL chunks (different gloss) sitting above the waist. Whole Unity layer compiles clean against the rebuilt DLL, site JS node-checked | [22](22-economy-system.md), [08](08-creature-visualization.md) |
| 2026-07 | **Group moves now settle facing one direction, set by whichever unit reaches the waypoint first (creator direction).** Previously each unit in a group order just held whatever heading its own last step happened to leave it facing, so a squad arriving from different angles (formation slots aren't in a line) ended up looking every which way once stopped. Fix: a new `MonsterAgent.GroupFacing` token (a class, so every unit in the group shares the same instance) -- `WaypointCommander.AssignFormation` mints ONE per group-move click and threads it through a new `OrderMove(hex, queue, settleTarget, groupFacing)` overload alongside the existing settle-cluster-point plumbing (single-unit moves still pass null, unaffected). `GoIdle` -- the single choke point an order finishes at -- locks the token to the arriving unit's CURRENT heading the instant it happens, but only if the token is still unlocked ("first reaches the waypoint" means finishing the path, not the settle-creep afterward). `TickSettle` (already the per-frame idle tick that creeps a unit toward the shared cluster point and rotates it to face that creep direction) now, once the creep phase ends, Slerps the unit toward the locked facing every frame -- applies to every unit in the group including the one that set the lock, so a unit that drifted while creeping snaps back straight too. No test harness exists for MonsterAgent/WaypointCommander (Unity-only classes, no Editor here); verified via the flightcheck compile harness against the real files (unchanged: genome-core 51, roster-client 56, creature-mesh 84) | [18](18-city-battlefields.md) |
| 2026-07 | **Water rebuilt as continuous flowing bodies with real depth, instead of a grid of flat blue tiles (creator direction: "ponds and rivers need to be smooth and flowing, NOT just blue tiles ... visual depth to the river and pond banks").** The old renderer drew ONE translucent cube per water hex (`SpawnCube` in `BuildTerrainAndRoads`) -- flat cube tops butting edge-to-edge read as a blocky tiled surface with no flow and no readable depth. Replaced with `RuntimeCityBuilder.BuildWater`: `_city.Water` is split (reusing the existing `PondHexes` river-vs-pond BFS) into the river and the ponds, and each becomes two welded **hex-fan** sheets -- a `HexFanMesh` emits, per hex, a centre vertex plus its six pointy-top corners at `HexCoord`'s own circumradius (`HexMeters/sqrt(3)`), six triangles wound `(centre, next, cur)` to face up. Because every hex derives its corners from its own centre with the identical offset, two neighbours' shared corners land on byte-identical world positions, so the sheet is genuinely seamless (proven numerically against the real `HexCoord`: every one of a hex's six neighbours shares exactly 2 corners, and all six fan tris' `RecalculateNormals` cross-products point +Y). (1) A **translucent, glossy surface** sheet rides at the new `TerrainField.WaterLevel` (-0.55) and is animated by the new `WaterSurface` MonoBehaviour, which every frame displaces each vertex by two travelling sine waves and recomputes normals so light glints and rolls across it -- a river gets a `RiverFlow` direction (its longer world axis, since the generator carves it as a map-spanning band) so its waves march downstream, while ponds pass zero flow for a gentle standing chop that reads as still water (Time-driven like NightMode/TrafficCar, ~5.5cm amplitude -- a miniature-set pond, not an ocean). (2) A **dark murky bed** sheet hugs the carved riverbed (`_terrain.HeightAt + 0.05`, so it hides the green grass terrain that renders under the water) -- seen down through the translucent surface, THAT is the depth cue. (3) Banks: `TerrainField` deepened the carved bed to -1.7 (was -1.4) and turned the shoreline ring from a below-water recessed lip into a low bank CREST at -0.28 (`ShoreLip`, was `BankRecess` -0.55) sitting just ABOVE the -0.55 waterline, so the existing inverse-distance blend now sculpts open ground -> bank crest -> waterline -> deep bed as one continuous bank a viewer reads as land rising out of the water. Also fixes the old cube loop's latent bug of drawing water over flat-locked bridge/road hexes (BuildWater skips them). Lily pads re-floated onto the new surface level. Verified: `WaterSurface.cs` compiles clean against a UnityEngine stub, and `HexFanMesh`/`RiverFlow` compile against the real citygen-core DLL + stub; seam/normal geometry numerically proven as above; citygen-core 140 tests untouched (presentation-only, no schema/generator change) | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Water round 2: hex-fan tiling replaced by a depth-clipped single sheet, so rivers/ponds read as organic curves instead of a hexagon outline (creator correction with a screenshot after round 1: "water looks exactly the same ... rivers not big tile blocks").** Round 1's per-hex fan already flowed and had depth, but its silhouette was still a chain of hexagons -- a real shoreline doesn't follow the hex grid. Replaced `BuildWater`'s hex-fan surface with `BuildWaterSheet`: ONE regular quad-grid mesh spanning the whole wet region's world bounding box (padded 1.5 hexes so it always overshoots into the banks), sitting flat at `TerrainField.WaterLevel`, animated by the same `WaterSurface`. No waterline is ever explicitly drawn -- it EMERGES from the ordinary URP depth test: the opaque terrain mesh (already sculpted with a bank crest above the waterline and a carved bed below it, round 1) occludes every part of the flat sheet that's over land, so the visible edge is exactly where TerrainField's smoothed inverse-distance-blended noise crosses WaterLevel -- a continuous curve with the SAME organic wobble as the hills/banks, zero hex artifacts. The dark bed sheet stays a hex-fan (it only needs to roughly underlie the water hexes, never seen at its own edge -- the surface sheet's silhouette is the only edge a player sees). Two bugs caught before shipping this round: (1) `WaterSurface`'s wavelength (9m) was close enough to `BuildTerrainMesh`'s OLD quad spacing intuition that a naive port would have undersampled the new sheet's coarser grid (`BuildWaterSheet` uses `>=4m` quads, capped like the terrain chunker); fixed by widening the wavelength to 17m (~4x the finest grid spacing) and documenting the constraint in `WaterSurface`'s own doc comment. (2) the wave's time term wasn't scaled by the wavenumber, so `Speed` wasn't actually metres/second; fixed (`t = (Time.time * Speed + phase) * k`). Verification this round went further than compiling: wrote a standalone harness (`Program.cs`, kept out of the repo, scratch-only) that compiles the REAL `TerrainField.cs` unmodified against a UnityEngine stub, runs the REAL `CityGenerator` for Village/seed42 and SmallTown/seed7, computes the exact sets `BuildWater`/`BuildWaterSheet` compute, and (a) asserts every wet hex's terrain height sits below the waterline (109/109 and 290/290, PASS) and every shoreline hex's bank crest sits above it (137/137 and 259/259, PASS), and (b) renders a top-down image replicating the depth-clip (sheet visible only where terrain height < WaterLevel) so the shoreline shape could actually be SEEN before shipping -- both renders show a winding river with a scalloped organic edge and round ponds, no hex tiling, sent to the creator as proof | [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Traffic fixed: cars were ping-ponging back and forth instead of driving anywhere (creator report), rebuilt as bounded trips that end in curb parking, with proactive monster-avoidance and a docs/19 traffic-field knob for the live fraction of the fleet driving.** Root cause: `TrafficCar.PickNext`'s wander pick scored every neighbor of the current hex by a hash of the CANDIDATE'S OWN coordinates plus the car's instance ID only -- nothing keyed to where the car had just come from. Whenever a hex's hash-best neighbor happened to be the hex the car had just arrived FROM, the car reversed, and the identical deterministic hash sent it right back next arrival too -- an infinite two-hex bounce, exactly the reported behavior. First fix (exclude `_from` from candidates unless it's a true dead end) turned out to be necessary but not sufficient: caught by a purpose-built regression test simulating a 3-way street-stub junction (one through hex with three dead-end arms), a car would correctly stop bouncing between just two hexes, but because the wander hash for a FIXED coordinate pair never changes, whichever arm hashed lower than BOTH of the other two would never win against either one individually -- the car settled into a permanent loop across two of the three arms, never visiting the third. Real fix: fold a `_hopCounter` that increments every pick into the hash input, so the ranking rotates hop to hop instead of staying fixed forever -- every reachable branch eventually takes its turn. Also added, per the creator's other two asks: (1) bounded trips -- `_hopsRemaining` (5-14 hops, rolled per trip) counts down on arrival; hitting zero triggers `ParkHere` instead of picking another hex, which pulls the car to the curb (same lane offset RoadDresser's own set-dressing parked cars use) facing the way it arrived and starts a park timer, so cars now genuinely go somewhere and stop, not wander forever. (2) proactive monster avoidance -- the existing panic-flee (drop everything within FleeRadius=16m) is unchanged, but the normal wander score now also subtracts a steep penalty for any candidate hex within a wider MonsterAwareRadius=28m of a monster, so a car steers off a threatened block on its OWN route choice before it would ever need to full-panic; a PARKED car also now checks for a nearby threat every frame and immediately pulls out and flees rather than sitting still near danger. (3) the docs/19 traffic field -- new `RuntimeCityBuilder.trafficMovingPercent` Inspector knob (0.05-1, default 0.55): the target long-run fraction of the fleet driving at any moment. Derived once per car at spawn into an average park-stay duration relative to an average trip's drive time (`parkDuration = avgDriveTime * (1/pct - 1)`), with each car's INITIAL state independently rolled against the same target (and, if starting parked, a partial park timer) so the fleet doesn't drive-then-park in lockstep. `RuntimeCityBuilder.TrafficMovingFraction` reads the LIVE measured percentage back out (count of cars with `IsDriving` true / fleet size); `HudStatus` now shows both live and target percentages on screen. Verified beyond compiling: a headless harness compiles the REAL `TrafficCar.cs` (unmodified) against a from-scratch stub of just the UnityEngine/RuntimeCityBuilder surface it touches, invokes its private `Update()` via reflection (no test hooks added to production code) over a fake clock. Two regression checks confirm the star-junction fix (12 independently-seeded cars each visit all 4 hexes of a synthetic 3-arm junction over 2000 simulated ticks, zero 4-cycles detected -- FAILED before the hop-counter fix, caught it) and three convergence checks spawn a 60-car fleet on a real filled hex-disk network for 3 simulated hours at trafficMovingPercent 0.30/0.55/0.85 with zero monsters, measuring the live fraction driving at 0.296/0.547/0.843 respectively -- all within 0.01 of target | [21](21-world-upgrade-report.md), [19](19-citizens.md), [18](18-city-battlefields.md) |
| 2026-07 | **Traffic keeps its moving fraction in a loose +-20% band (not a rigid per-park swap), cars swerve around monsters mid-drive, and citizens now prefer sidewalks and cross only at corners (three creator directions in one pass, refining the just-shipped bounded-trip traffic).** (1) Moving-fraction band: each car's own independent park timer already targets `trafficMovingPercent` on average, but a bad-luck run could leave the LIVE fraction driving well below it with nobody due to depart soon. `RuntimeCityBuilder` now runs a lightweight `Update()` (new for this MonoBehaviour) every 4s that wakes ONE currently-parked car (`TrafficCar.DepartNow()`, a new public override of its own timer) only once the measured fraction has drifted more than 20% below target -- explicitly NOT an immediate per-park-event trigger ("the next car(s) do not have to start immediately, we can have more cars on longer journeys"), so trip-length variety and staggered timing are untouched; a rotating cursor spreads the early wake-ups across the fleet. (2) Mid-drive swerve: the existing MonsterAwareRadius penalty only steers which hex gets picked NEXT at a junction, and FleeRadius is full panic -- neither touches the literal path toward the car's CURRENT target hex, so a car could still drive straight at a monster before reacting. New `TrafficCar.SwerveOffset`, evaluated every driving frame between FleeRadius and MonsterAwareRadius (22m): nudges just that frame's steering point laterally away from a monster ahead (strength scales with proximity and how directly ahead it is; zero if behind or well off to the side), leaving `_target`/hop bookkeeping untouched -- purely cosmetic curve-around, never stacked with full panic-fleeing (already moving away by definition). (3) Pedestrian sidewalks: `Citizen`'s wander (`PickSidewalkTarget`, replacing the old first-match-neighbor pick) now strongly prefers a neighbor OFF the road network (`RuntimeCityBuilder.RoadNetworkHexes`) and only steps onto a road hex at a new `IsRoadCorner` hex -- a junction (3+ road neighbors), a bend (2 neighbors not roughly opposite), or a dead end (0-1), i.e. anything that ISN'T a plain straight mid-block segment -- never a mid-block jaywalk; a last-resort fallback (any open neighbor) keeps a boxed-in citizen from freezing. The flee branch (any monster within FleeRadius) is deliberately UNTOUCHED by any of this -- panic already ignores sidewalks entirely, matching "unless fleeing from monster" verbatim. Whole Unity gameplay layer compiles clean against the rebuilt DLLs; the scratch flightcheck harness itself needed catching up for the two prior water-rendering commits (RangeAttribute, Mathf.Deg2Rad, Time.time, and a fleshed-out Vector2 stub, plus WaterSurface.cs added to its compile list) since they'd only been verified through a separate one-off harness -- no production code changed by that catch-up, compile-check plumbing only | [19](19-citizens.md), [21](21-world-upgrade-report.md), [18](18-city-battlefields.md) |
| 2026-07 | **Tank overhaul: turret/hull decoupled, muzzle smoke, road-preferring navigation with a fordable-water rule, larger than the cars, and a permanent wreck on death (five creator directions in one pass).** (1) Turret vs. hull: the old code Slerped BOTH toward the same target-facing rotation, so a tank driving a steer correction around an obstacle would visibly strafe sideways instead of turning into its travel direction. Now the turret alone tracks the target (world-space Slerp, unconditionally -- moving, turning, or standing still firing, a real tank's traverse); the hull Slerps toward its own TRAVEL direction at a limited rate and only ever drives along its OWN current forward vector, with forward speed additionally gated by `clamp01(dot(forward, steer))` -- a sharp turn slows the hull toward a near-pivot instead of gliding sideways, the actual "move realistically like a treaded vehicle" ask. (2) Muzzle smoke: new `DamageFx.MuzzleSmoke`, a small one-shot puff reusing the existing `SmokePuff` primitive (already backing building smoke/dust), fired whenever `UnitCombat.TryFire` returns true (it already reported success, just wasn't watched). (3) Navigation: `SteerTank` (replacing `SteerAroundBuildings`) scores every deflection angle candidate by `|angle| + (onRoad ? 0 : 35)` instead of taking the first unblocked one, so a mild detour onto a nearby road beats a straight-line off-road path but a wild detour doesn't (verified numerically in a throwaway harness); the blocked check is a new `BlockedForTank` -- buildings always block, water only blocks past a fordable depth (new `RuntimeCityBuilder.WaterDepthAt`, continuous per TerrainField's own inverse-distance blend: shallow at the banks, ~1.15m at the mid-channel bed) -- threshold `TankHeight(3.0) * 0.3 = 0.9m`, so tanks can ford a shallow bank but not the channel (creator direction: "only cross water < their 0.3 of their height"). (4) Size: every position/scale number in `BuildModel` multiplied by a new `Scale = 1.55f` constant, so the whole assembly grows uniformly and reads clearly larger than both car bodies (sedan 2.2x0.8x5.2, truck 2.4x1.7x4.4). (5) Wreck: `OnDied` no longer sinks-and-fades -- a new `SpawnWreck` leaves a permanent, colliderless wreck (scorched slumped hull, the turret knocked off at a hashed angle beside it, a few loose plate/track chunks), same convention as `RubbleDresser`'s building rubble, plus a `DamageFx.DustBurst` for the collapse beat; parented under the same "Tanks" host the live tank was. Scoped deliberately as the VISUAL breakdown only -- wiring an actual harvest/resource pickup for these wrecks (docs/20's faction corpse-salvage materials, a new resource class) is a separate economy feature, logged as a follow-up, not attempted here. Verified: whole Unity gameplay layer compiles clean against the rebuilt DLLs; a standalone throwaway harness re-derived the three pure-math formulas (road-preference cost, alignment/speed curve, fordable-depth threshold) outside Unity and confirmed each behaves as designed at sample inputs -- on-screen appearance still unconfirmed, no Editor in this environment | [18](18-city-battlefields.md), [20](20-harvest-and-repair.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **Village stopped laying out as hex-connected/radial roads around a plaza -- rebuilt on a real North American town, 1950s, population 8,000-30,000 (creator correction: "Do NOT layout Villages with Hex connected roads").** The old Village preset used `RoadPattern.Radial` -- ring roads every pitch hexes plus 6 spokes along the hex grid's own axial directions, a "wagon wheel" that read as hex-grid-driven rather than a real town. Since Radial was Village's ONLY user (SmallTown/BigCity already used MainStreet/Grid) and the creator's direction makes it wrong for Village too, it's REMOVED entirely rather than left dead: the enum value, its `IsRoad` switch case, and the plaza-block-reordering special case in `PlaceLandmarks` are all gone (`IsRoad`/`PlaceLandmarks` also dropped their now-unused `hex`/`center` parameters). Village now uses the SAME `RoadPattern.MainStreet` SmallTown already does (one arterial + perpendicular grid + sparser parallel residentials) -- a real small-town Main Street downtown, not a different pattern, just smaller/sparser DATA: resized 50x50 -> 70x70 hexes (~1 km² -> ~1.96 km², reflecting a real 8,000-30,000-population town rather than a tiny hamlet), buildDensity 0.35 -> 0.42, tier weights shifted toward slightly more Medium/Large (0.80/0.18/0.02 -> 0.75/0.22/0.03), and `town_hall` added to its emitter archetypes (a real Main-Street town's civic landmark) alongside the existing plaza/church. Fallout fixed: one test had Village's OLD 50x50 size hardcoded (`Axial_origin_is_the_corner_not_the_center`, now derives corner/center from `preset.WidthHexes/HeightHexes`); the landmark-count theory's village row updated for the new area (2/1 -> 3/1 emitters/hubs, same formula); `Village_anchors_an_emitter_on_the_central_plaza` deleted outright since it tested the now-removed Radial plaza-anchor behavior specifically, not anything a MainStreet town does or should guarantee. citygen-core 139 tests green (was 140, one net removal); whole Unity gameplay layer compiles clean against the rebuilt CityGen DLL. Verified beyond tests: a throwaway console harness against the real DLL dumped Village's road hexes as an ASCII map at seed 42 -- a clean rectangular grid of blocks with one wider arterial row, zero ring/spoke structure, confirming the fix visually rather than just trusting the pattern-name swap. docs/18 SS1 preset table and the SS7 tuning table updated to match (footprint ~1 km -> ~1.4 km, Village's row now describes the Main-Street-town identity instead of "organic/radial") | [18](18-city-battlefields.md) |
| 2026-07 | **Village's Main Street widened to a real 3-4 lane arterial, junctions render as European roundabouts instead of crosswalk-striped X's/Y's, and roads stopped bleeding hill elevation through their own pavement (three creator reports on the just-rebuilt grid: "There is a zig-zag road down the middle... Replace it with a 3-4 lane road", "Replace the Y cross roads with Cross or T configurations or for European styling proper Roundabouts", "elevation is eating into the roads, road tiles should match the terrain perfectly").** Root-caused each before touching code: the Main Street "zig-zag" wasn't actually crooked -- `HexCoord.ToWorld()` proves a fixed-row arterial is mathematically dead straight (z depends only on R) -- the real defect was every road hex rendering at the SAME fixed 7.5m width/5.2m pad regardless of role, so Main Street read no different from a residential side street and pinched at every hex-pad/strip seam; and "Y" junctions were genuinely a bearing artifact, not a rendering bug: this hex grid's "vertical" street direction is a true diagonal (RoadDresser's existing `TryStraightenCardinal` doc comment), not perpendicular to an east/west arterial, so a real 4-way crossing's arms were never going to meet at clean 90 degrees without corrupting the alignment fix from the PREVIOUS zig-zag pass. Fixes: (1) new `CityModel.ArterialRoads` -- CityGenerator tags the MainStreet pattern's `row==height/2` hexes (and any bridge deck that survives on that row) as a distinct subset of Roads; `RoadDresser` widens pad/apron/strip geometry to a new 14m `ArterialRoadWidth` (vs the residential 7.5m) for connectors where BOTH ends are arterial, and paints a double-yellow centerline plus a dashed white lane divider instead of a single dash row -- furniture/parked-car curb offsets were re-derived from the ACTUAL road width in use (previously hardcoded for 7.5m only, which would've sat a parked car mid-lane on the new arterial). (2) `DressHex`'s 3+-connector branch no longer draws a small pad with crosswalk stripes -- it draws a roundabout (asphalt ring + raised curb + grass island), chosen explicitly over forcing Cross/T symmetry because a circular hub accepts any arm bearing gracefully, sidestepping the diagonal-bearing problem entirely rather than fighting it. (3) `TerrainField.HeightAt`'s inverse-distance blend only ever pinned a flat-locked (road/building/bridge) hex's height at its exact CENTER sample -- a non-flat neighbor (a hill, a roll) still bled its own height in everywhere else in the hex, including out near the edge where the road's own pavement geometry actually sits. Fixed with a distance-TAPERED weight boost (`FlatBoost`, dominance 34x at the flat hex's own center, quadratically fading to none by 13m) -- a flat multiplier was tried first and rejected: since the blend kernel is symmetric, an untapered boost also suppressed a NEIGHBORING hill's OWN center reading 20m away just for bordering a road, a real regression a throwaway verification harness caught before it shipped (a ridge hex measurably lower than pre-fix baseline despite the fix having nothing to do with that hill's own hex). The taper keeps the boost dominant out to a road's own outer geometry while reverting fully to baseline by the time you reach a neighbor's own center. Verified numerically (a real Village generated, real TerrainField.cs run against a from-scratch harness with actual math implementations, not the flightcheck harness's dummy Mathf/Vector3 stubs which had silently made an earlier pass of this same harness return nonsense): worst-case road-edge bleed toward a genuine hill neighbor down to ~17cm (was blowing well past RoadDresser's 0.24m raised-roadbed clearance before), road-hex-own-center bleed ~0.6cm, and zero measurable extra suppression on any of Village's 18 genuine flat-adjacent (non-building-occupied) ridge hexes. Also confirmed via ASCII-dumping the real generator's arterial tagging (row 35 of a 70-tall Village solidly `=`, exactly matching `height/2`) and citygen-core's 142 tests still green; whole Unity gameplay layer compiles clean against the rebuilt DLL | [18](18-city-battlefields.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **Roads rebuilt on WORLD-CARDINAL rendering: the persistent zig-zag and Y-intersections are gone (straight parallel streets + clean North-American 4-way crosses), and the "green cylinder" roundabout replaced with a fully detailed European one that traffic actually circulates (creator: "it's still doing zigzag roads. Not parallel roads. and generating Y intersections Not the 4 way cross... a European-style roundabout... THEY ARE NOT GREEN CYLINDER DOTS!... Cars must follow the curve").** ROOT CAUSE, finally pinned: RoadDresser built each hex's road arms from `hex.Neighbors()` -- the six HEX-ADJACENCY neighbors, which sit at 60-degree diagonals on a pointy-top grid. A "vertical" street is a set of hexes sharing an offset COLUMN, and those are diagonal hex-neighbors, so drawing toward them sawed the street left-right every row (the zig-zag) and fanned every junction into a Y. The prior `TryStraightenCardinal` only patched pure 2-connector through-hexes, so junctions stayed Y-shaped -- a partial fix that could never make a clean cross. Real fix: build arms from OFFSET-coordinate neighbors instead (`col/row +-1` = due E/W/N/S in world space), via new `RoadDresser.CardinalNeighbors`/`Offset`/`CardinalAnchor`. Every vertical-street hex is nudged onto its column's straight centerline (+-HexMeters/4, cancelling the odd-r sawtooth). Proven numerically against the real generator: worst-case anchor-x spread within ANY of Village's 70 vertical streets is 0.000000 m (perfectly straight/parallel), and junctions resolve to 64 four-way crosses + 132 T-junctions with ZERO diagonal Y's possible (all arms are cardinal by construction). Junction rendering: 3+ arm hexes now draw a proper North-American 4-way cross / T (asphalt infill + set-back crosswalk zebras), NOT the old crosswalk-striped diagonal pad. Roundabouts: `CityModel.Roundabouts` (new) tags the 1-2 arterial intersections nearest town center (generator, `MaxRoundabouts=2`); those render via a new detailed `DrawRoundabout` -- circulating asphalt ring, raised curb, domed grass island with 6 shrubs + a stone obelisk sculpture, 20 dashed white lane markings following the circle, 5 evenly spaced streetlamps, and per-entry flared aprons + give-way shark-teeth + set-back pedestrian crossings + European blue-circular-roundabout and red-triangular-yield signage on posts -- every element from the creator's spec, replacing the two-cylinder "green dot". Traffic: `TrafficCar` now CIRCULATES a roundabout (new `CirculateRoundabout`/`PickExit`) -- on entering, it picks an exit spoke and arcs counter-clockwise (right-hand European traffic) around the circulating lane at `RuntimeCityBuilder.RoundaboutLaneRadius`, leaving only once it's swept far enough AND lined up with the exit; fleeing a monster overrides circulation (drives straight out, "unless fleeing"). `BridgeDresser` switched to the same shared `CardinalAnchor`/`CardinalNeighbors` so a bridge lands on the now-straight vertical centerline of the road it carries. `TryStraightenCardinal` removed (its whole job is subsumed). citygen-core 145 tests green (3 new: roundabout placement is on-arterial + capped, grid has none); whole Unity layer compiles clean against the rebuilt DLL; the straight/parallel + cross-shape claims verified in a throwaway harness against the real generator, not just eyeballed | [18](18-city-battlefields.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **Roundabouts moved OFF the multi-lane arterial to ordinary residential 4-way junctions (creator correction: "it's either a Cross or a roundabout NOT BOTH. They also DO NOT occur in the middle of multi lane roads").** The prior pass placed roundabouts at Main-Street-x-cross-street junctions -- i.e. ON the arterial. Since the arterial renders as a wide 3-4 lane road running straight THROUGH that hex, the result read as a multi-lane road with a roundabout dropped in the middle of it, i.e. "both" a through-road and a roundabout. Fix (generator only): roundabout candidates are now the full 4-way crossings of a NON-arterial through cross-street (row % (pitch*2) == 0 and != arterialRow) with a vertical street, nearest town center, capped at 2 -- never the arterial row, never a drowned/bridge hex, and required to be a genuine 4-way (new `IsFourWay` cardinal-neighbour check, matching what the renderer draws). Every arterial crossing now stays a plain 4-way cross, so a junction is strictly EITHER a cross OR a roundabout and no roundabout ever sits on the through-arterial. Verified against the real generator (seed 42 Village): both roundabouts land at row 36 (one row off the arterial's row 35), both genuine 4-way, and all 70 arterial junctions are crosses (zero roundabouts on the arterial). citygen-core 145 tests green (the roundabout test flipped from asserting on-arterial to asserting off-arterial + 4-way); whole Unity layer recompiles clean against the rebuilt DLL | [18](18-city-battlefields.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **Roundabout cleared fully off Main Street, traffic given lane discipline + following gaps + monster U-turns, and citizens given cross-town destinations while pinned to the sidewalk (four creator corrections in one pass).** (1) The prior "off-arterial" roundabout was still only ONE row (~17 m) off the arterial, and a ~13 m-radius roundabout beside a 14 m-wide road still touches it -- so it read as sitting on the multi-lane street. Now the generator requires >=2 rows of separation from the arterial row (`Math.Abs(row-arterialRow) < 2` excluded), landing Village's roundabouts ~190 m clear of Main Street (verified). (2) "cars are driving all over the road, not in straight lines": ROOT CAUSE was TrafficCar driving to RAW hex centers while RoadDresser now draws the strip at the CORRECTED cardinal centerline (the +-HexMeters/4 vertical-street nudge) -- so a car sawed down a straightened street. New `TrafficCar.RoadPoint` aims at the same `RoadDresser.CardinalAnchor` the road is drawn at, plus a `LaneOffset` (2 m to the right of travel) so opposing traffic stays apart and each car holds a lane; proven straight (0.000000 m lane-x spread down a vertical street). (3) Following gaps: new `RuntimeCityBuilder.DistanceAhead` finds the nearest car/tank/citizen in the lane ahead, and a car eases its throttle to zero between `FollowRange` (15 m) and `FollowGap` (~5.5 m, one car length + ~0.2*size) so cars queue instead of overlapping. (4) Monster U-turn: `PickNext` now, WHEN FLEEING ONLY, allows re-selecting `_from` (normally forbidden to stop ping-pong), so a car threatened ahead turns around and drives back the way it came instead of only sidestepping. (5) Citizens: reworked from aimless adjacent-hex wander to DESTINATION-based -- each picks a random sidewalk hex up to 40 hexes away (`RandomSidewalkNear`) and greedily steps toward it, but every step is constrained to a SIDEWALK hex (`IsSidewalkHex`: in-city, non-road, non-water, non-building, bordering a road -- 896 of them on Village, all validated) or a corner road hex for a legal crossing; only fleeing a monster lifts the constraint ("then they can run anywhere"). citygen-core 145 tests green; whole Unity layer compiles clean against the rebuilt DLL; sidewalk set, lane straightness, and roundabout clearance all checked numerically against the real generator, not eyeballed | [18](18-city-battlefields.md), [19](19-citizens.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **Minimap added (docs/02's one-line fog-of-war spec finally implemented too) -- a dev-tunable OnGUI overlay, default bottom-left, showing the whole baked city plus live unit blips, click-to-navigate, right-click-to-order, rotation, zoom, and a fog-of-war dimming layer (creator direction, 2026-07: "add a UI quick mini-map that can be tuned to the fog of war... navigate around the map quickly. Work with rotation and Zoom etc. bottom left of the screen but movable anywhere on the screen by the developer").** No fog-of-war OR minimap code existed anywhere before this -- docs/02 had a single sentence ("monsters reveal a radius; emitter status always visible") and nothing implementing it. New `FogOfWar.cs`: tracks EXPLORED (seen once, stays revealed) and VISIBLE-NOW (within `visionRadiusHexes` of a currently-alive player monster right now) hex sets, recomputed on a 0.35s timer via `HexCoord.Range(radius)` around each alive monster only -- cheap regardless of map size, a Big City's 250x250 hex field is never walked in full. New `Minimap.cs` (OnGUI, matching this project's only-IMGUI convention, docs/12/HudStatus precedent): bakes the whole generated city (water/ridges/roads/bridges/buildings-by-tier/landmarks) into a single 256x256 texture ONCE at Init (the layout never changes after generation), using CityGizmo's own color palette except roads (lightened -- the gizmo's near-black disappears at minimap scale); a separate 128x128 fog overlay texture is repainted from FogOfWar's sets every 0.4s (unexplored = opaque black, explored-not-visible = dimmed, visible = fully transparent/lit). Placement: `corner` (default BottomLeft) + `marginPixels` + `sizePixels`, OR `useCustomPosition` for pixel-exact placement anywhere -- all public Inspector fields, satisfying "movable anywhere ... by the developer" literally. Rotation: `rotateWithCamera` spins the whole minimap via `GUIUtility.RotateAroundPivot` so the camera's forward always points up (Civ/Total-War style) instead of fixed north-up. Zoom: mouse-wheel-over-map narrows the displayed texture region (via `GUI.DrawTextureWithTexCoords`) around the camera's XZ position, from the whole map (zoom 1) to a close-in view (zoom up to 8); a camera-frustum indicator box tracks the live view. Click-to-navigate: left click/drag calls the EXISTING `SimpleCameraRig.FocusOn` (no camera-rig changes needed). Right-click-to-order: new `WaypointCommander.OrderSelectionTo(Vector3, bool)`, mirroring the existing ground-order branch of `HandleOrders` (single unit vs `AssignFormation`) so the minimap doesn't duplicate that logic. New `Minimap.PointerOver` static flag (set every OnGUI call) that `WaypointCommander.Update()` checks first -- OnGUI's event queue and the New Input System's `Mouse.current` are two separate, non-communicating input paths, so without this guard a minimap click would ALSO fire a 3D-raycast select/order underneath it. `RuntimeCityBuilder` gained `Citizens`/`TrafficCars` accessors (mirroring the existing `Monsters`/`Combatants`) so the minimap can plot every unit type, and wires `FogOfWar`+`Minimap` into `Start()` the same way HudStatus/HealthBars/WaypointCommander already are. Verified: whole Unity gameplay layer compiles clean against the rebuilt DLLs (needed real stub fidelity work in the shared flightcheck harness -- Texture2D/Color32/GUI.DrawTextureWithTexCoords/GUIUtility/Event/EventType/Transform.eulerAngles/Mathf int-overloads/InverseLerp had never been needed by any prior script and were either missing or dummy placeholders); a standalone harness confirmed the world<->UV coordinate round-trip is exact (0 error) across Village/SmallTown/BigCity map spans and that the terrain-texture stamp radius scales sensibly (2px/2px/1px respectively at the 256px bake resolution). citygen-core 145 tests unaffected/still green. On-screen appearance (rotation direction, exact fog dimming feel, blip legibility at 220px) unconfirmed -- no Unity Editor in this environment | [02](02-gameplay-overview.md), [18](18-city-battlefields.md), [21](21-world-upgrade-report.md) |
| 2026-07 | **docs/23 written: the RTS master build plan (creator direction: "Layout a complete programming documentation to be executed later by sonnet Claude model, for this game to be expanded to a full real time strategy game").** A phase-by-phase execution document for a Sonnet-class implementing agent, grounding every requested feature in the systems that already exist rather than inventing parallel ones: three named/themed faction bases (The Sanatorium / Fort Vigilance / The Brood Nest) with a shared building roster (Blood Banks, Fuel Depots, Armouries, Collection Stations, factories) riding BattlefieldState's existing solid-until-destroyed passability; Blood/Fuel/Ichor promoted to full match currencies with generator-seeded fuel nodes (1950s gas stations); an RPG layer (XP/levels 1-10/trait picks/salvaged-part gear) plus an Archon-style FUSION mechanic that doubles as the unlock path for the 4th hybrid category (Chimera Track -- grafts from all three origins, honoring the origins invariant); flocking (boids steering layered on the existing A*, clamped to the blocked set so solidity survives); full three-faction combat implementing docs/04's damage formula for the first time, all-parts salvage, in-match Discovery feeding the Lab, and roaming cycling power-ups (Loose Experiments wandering between roundabouts); the Lumen Cycle made real as a day/dusk/night clock with per-faction bonuses driving NightMode; New York / Paris / Montreal 1950 as preset+dressing-kit region triples (Paris introducing a Boulevard road pattern around a grand etoile roundabout); a URP-based Mafia-school AAA graphics ladder (post stack, lighting, PBR materials, mesh-swap PropLibrary with primitive fallback, creature/FX passes -- explicitly presentation-only so determinism never regresses); and deterministic-lockstep 4v4 netcode (match-core as a pure tick function, a thin open-source relay package, hash-based desync defense, replay-based reconnect, LiteNetLib transport) with docs/24-netcode.md specified as the open-source protocol deliverable. Ground rules section binds the executor to the repo's constitution (determinism, normative genome schema, origins/energy invariants, no-Editor verification discipline, docs/12 append ritual). docs/00 index row added | [23](23-rts-master-build-plan.md), [17](17-factions.md), [22](22-economy-system.md), [09](09-multiplayer-architecture.md) |
| 2026-07 | **docs/23 evaluated by a four-expert panel and revised (creator direction: "Create a team of experts to evaluate and execute the outline").** Four specialist lenses reviewed the RTS master build plan in parallel -- veteran RTS systems designer, deterministic-lockstep netcode engineer, Unity/URP tech artist, and repo-fidelity auditor -- each returning a structured verdict + blockers + edits, verified against the actual code, not just the doc. Verdicts: RTS-design and netcode NEEDS_REWORK, tech-art and repo-fidelity SOUND_WITH_EDITS. The load-bearing findings, all folded into docs/23 (inline where the original text was actively wrong; §13 amendments ledger for the rest): (A) the plan's biggest hidden cost -- NO phase ported the live unit sim out of Unity MonoBehaviours (MonsterAgent ~950 lines, UnitCombat, Tank, Citizen, TrafficCar are all frame-driven float Update() loops) into tick-driven match-core, yet §11 lockstep assumed it was already a pure function; added an explicit Phase 1.5 porting workstream flagged as the true critical path, plus a §0 float-discipline clause (no Sin/Cos/Exp in tick paths, bitwise float hashing, entity-ID-ordered iteration, an in-player-build replay-hash test since a headless CoreCLR harness can't catch IL2CPP FMA divergence). (B) emitters + the Lumen mana currency had no implementing phase despite the victory condition and a docs/04 golden example depending on them -> new Phase 3.5. (C) phase order was backwards (XP/attack-move need combat, which sat in Phase 6) -> core damage/death loop moved early. (D) Phase 6 was a six-system scope bomb -> split 6a/6b/6c. (E) no supply cap or army-size target (the single most load-bearing RTS number, undefined; "hundreds of units" vs docs/02's ~15) -> adopted 60 supply/player, ~20-40 units, reconciling 4v4 as 8x~35. (F) XP was off an order of magnitude (fusion mathematically unreachable) -> rescaled kill=40+4xlvl, thresholds 60..3300. (G) four-arm fusion render claim factually wrong (GenomeDto has ONE hand slot) -> corrected to a secondary-hand graft attach point. Plus: Fuel Depot conflated storage with extraction (split into node-locked Fuel Pump vs anywhere Fuel Depot); Chimera unlock unreachable in 1v1 (repredicated to "all three ORIGINS present," with tank wrecks/anomalies dropping off-origin Parts); missing control groups + rally points added to the "SC2 verb set"; a uGUI migration milestone added (IMGUI already forced the Minimap.PointerOver hack and won't survive build menus/toasts/dial); graphics acceptance changed from "flightcheck compiles" (which provably cannot catch visual failure -- cf. the silent-magenta-buildings history) to checked-in dev-build screenshots, with Forward+/no-SSR/NightMode-vs-Volume-ownership corrections; and the relay/transport contradiction resolved (HTTP-on-Kestrel lobby + separate LiteNetLib/UDP tick relay, two listeners one process). Panel cleared the plan for phased execution starting at Phase 1 with these amendments binding | [23](23-rts-master-build-plan.md), [12](12-open-questions.md) |
| 2026-07 | **RTS Phase 1 EXECUTED: packages/match-core deterministic foundation shipped (creator direction: the expert team is to "evaluate and execute the outline" -- panel done, execution begun at docs/23 Phase 1 as amended).** New engine-agnostic C# package `packages/match-core` (same conventions as citygen-core: no UnityEngine, C# 9 / no implicit usings so dotnet fails on anything Unity's asmdef compiler would reject, `Tests~`/`bin~`/`obj~` tilde dirs, asmdef + package.json, added to unity-client/Packages/manifest.json and CLAUDE.md repo layout). Ships the pure `(seed, command-stream) -> state` skeleton docs/23 §11 lockstep is built on: `SimRng` (deterministic sfc32 exposing RAW uint32 draws for integer-only sim math -- bit-identical to citygen-core's proven stream, verified by a cross-package test asserting `sim.NextUInt()/2^32 == citygen.Rng.Next()`); `FnvHash` (streaming FNV-1a state digest, little-endian ints + bitwise floats, never ToString/JSON -- the §13-J serialization contract); `Origin`/`ResourceKind`/`Resources` (three origins, six resources, energy-follows-origin invariant); `FactionDef` (the three factions with canon themed base names The Sanatorium/Fort Vigilance/The Brood Nest); `PlayerState` (integer wallets with validation-not-clamping spend, supply used/cap at the §13-E 60 default, and the Chimera-Track origin mask that opens on ALL THREE origins per §13-F, reachable in 1v1); `Command`+`MatchState` (fixed 10-tick/s advance that is a pure function of its inputs, a monotonic entity-ID allocator, and the canonical `Hash()` over frame+commands+entity-counter+full RNG state+every player). Honors the §0 float-discipline constitution literally: all sim state is integer, RNG draws are integer, the only floats are in the cross-check test. 13 xunit tests green including the Phase-1 acceptance (10,000-tick 8-player empty match hashes identically across two runs) plus a standalone `Tools~/DetHarness` that prints the hash twice (94F13654C8B8941B == 94F13654C8B8941B). NOT yet ported: units/buildings/economy/combat -- those are the §13-A porting workstream landing in Phases 1.5+ (the doc and CLAUDE.md both now warn the executor not to add gameplay to MonsterAgent.Update()). citygen-core 145 tests untouched | [23](23-rts-master-build-plan.md), [12](12-open-questions.md) |
| 2026-07 | **Group-settle spacing exposed in the Inspector (creator direction: "expose the setting in the editor that sets the distance the monsters are spaced around the destination waypoint").** `RuntimeCityBuilder.ApplySeparation`'s "how much daylight stays between two units' bodies once a settled group stops packing in around a shared destination" was a hardcoded private `SeparationGap = 1f` constant (itself a 2026-07 creator fix for units settling exactly touching). Promoted to a public `[Range(0,5)] groupSpacing` field in the existing "Tuning" Inspector header, alongside citizenCount/trafficCarCount/etc.; `ApplySeparation` and its doc comment updated to reference it, plus the cross-reference in `MonsterAgent.TickSettle`'s doc comment. Mechanically unchanged (same formula, same default 1m, same "each unit pushes half the overlap so a pair settles at Radius+Radius+groupSpacing apart"), purely a code-to-Inspector promotion. Whole Unity layer compiles clean against the flightcheck harness; no stray references to the old constant name remain | [18](18-city-battlefields.md) |
| 2026-07 | **Group waypoint arrival fixed: units now ring AROUND the waypoint instead of clumping ON it (creator correction: "monsters are NOT adhering to the spacing rules... They MUST distribute themselves around the waypoint NOT ON the Waypoint").** Root cause: `WaypointCommander.AssignFormation` passed the SAME shared cluster point (the exact clicked waypoint) as the settle target to EVERY unit in the group, so `MonsterAgent.TickSettle` crept them all onto that one point and only body separation (`ApplySeparation`) held them apart -- producing a tight clump centred on the marker, i.e. "on the waypoint." Fix: each unit now gets a DISTINCT settle target on a ring around the centre, via a new `RingTarget(center, index, spacing)` helper using golden-angle phyllotaxis (sunflower packing) with a CLEAR central hole -- nobody's target is the centre, so the marker stays visible and the group distributes around it. Both the hole radius and the ring pitch scale with the Inspector `groupSpacing` knob (2.5 + spacing each), so widening the spacing widens the whole formation coherently; body separation still enforces the exact pairwise gap on top. Verified numerically against the real formula across group sizes 2-16 and spacings 0/1/3: min radius always equals the hole (centre provably clear), and no two units share an angle (golden-angle guarantees angular spread, ~222deg max gap at N=2 = the two on opposite-ish sides, tightening to ~32deg by N=16). Single-unit moves are unchanged (nothing to distribute -- one unit on the point is correct). Whole Unity layer compiles clean against the flightcheck harness | [18](18-city-battlefields.md) |
| 2026-07 | **docs/25 written and approved: hybrid steering + deadlock-recovery migration plan for monster movement (creator direction: analyze-only first -- "Do Not Write Code Yet" -- then "I approve the plan, now capture it").** Analysis-before-code exercise: inspected the actual movement architecture (no assumptions) and found movement is 100% transform-based, zero Rigidbody/CharacterController/physics on any monster -- `MonsterAgent.cs` owns the `_order` state machine AND does the actual `transform.position` writes (FollowPath/TickSettle/TickPerch/TickEat); `MonsterBody.cs` is a pure view that only consumes the RETURNED velocity to drive animation (stride/wingflap/lift), never writes XZ position -- that return-a-velocity contract is the one hard interface boundary the whole plan hangs off. Root cause of the reported collision/permanent-stuck behavior, pinned precisely: `RuntimeCityBuilder.ApplySeparation` (a hard positional overlap-resolver, O(N^2)) and `AvoidanceDir` (a positional ahead-cone heading deflection, O(N^2)) run in SEQUENCE not blended with seek, so a blocked unit's seek and separation fight every frame (oscillation), separation can shove a unit off its path line with no "blocked by units" re-path trigger (permanently wedged), there's no speed modulation (a blocked unit keeps applying full seek into the jam), and there's no deadlock DETECTION at all -- nothing notices a unit hasn't moved in N seconds. The just-shipped ring-settle fix already solved DESTINATION clumping; en-route corridor congestion through a 1-2 hex gap is the unsolved case this plan targets. Approved architecture: `MonsterSteeringController` (seek+separation-as-force+predictive avoidance+deadlock nudge, combined, replacing the two O(N^2) reaction scans at their existing call sites in FollowPath/Update -- never touches `_order`), a `SpatialGrid` uniform-grid neighbour system (cell size = one HexMeters, rebuilt once/frame, allocation-free) for the perf win, and a rare-path-only `DeadlockManager` (detects want-to-move+valid-destination+stalled-for-T, grants temporary priority, blockers yield/sidestep into non-blocked hexes, releases on progress -- never becomes the primary mover). No NavMesh, no A* (HexPathfinder stays the global router, untouched), no full boids, no per-frame sorting/allocation, per the creator's explicit performance constraints. Five independently-testable phases (A spatial grid perf-only parity, B steering-controller behavioral parity, C predictive avoidance + speed modulation, D deadlock manager, E cleanup+tune), each with its own numeric-harness test plan since there's no Editor to verify visually. Explicitly flagged forward-looking: this lives Unity-side today, not in the docs/23 match-core deterministic tick sim -- designing the math integer/fixed-point-friendly and neighbour-iteration-ordered now avoids a second rewrite when docs/23 SS13-A ports unit movement into that sim later. Status: PLAN ONLY -- no code written, no files modified; execution begins at Phase A on a future turn. docs/00 index row added (using docs/25, not 24 -- 24 is already reserved in docs/23 SS11 for the netcode protocol spec) | [25](25-monster-movement-steering-plan.md), [18](18-city-battlefields.md), [23](23-rts-master-build-plan.md) |

## 2026-07 — Monster movement steering: Phase A (SpatialGrid) implemented

Executed docs/25's Phase A on explicit creator direction ("Proceed with
Phase 1 only. Make the minimum required changes. Do not implement future
phases yet."). Scope was strictly the uniform-grid neighbour system, not
Phases B-E.

- New `unity-client/Assets/Scripts/SpatialGrid.cs`: a generic
  (`SpatialGrid<T> where T : class`) uniform spatial hash over the XZ
  plane, cell size = `HexCoord.HexMeters` (20m). `Clear()`/`Insert`/
  `QueryRadius` only; cell `List<T>` instances are pooled across `Clear()`
  calls so steady-state per-frame cost is allocation-free. Generic on
  purpose: it lets the shipped class compile and run directly (via a
  `<Compile Include>` file reference, not a reimplementation) in a
  standalone console harness against plain test objects, no MonoBehaviour
  runtime needed.
- `RuntimeCityBuilder.cs`: added `_combatantGrid` +
  `RebuildCombatantGridIfNeeded()` (lazy per-frame rebuild triggered from
  inside `ApplySeparation`/`AvoidanceDir`, not `Update()`, so it has no
  dependency on Unity's script execution order against
  `MonsterAgent.Update()`); both methods now query the grid instead of
  scanning `_combatants` directly. Every per-pair distance/push/deflection
  calculation is byte-for-byte unchanged -- pure perf refactor, per docs/25
  Phase A's "behaviour-identical to today" requirement.
- `MonsterAgent.cs`: no changes. Its two call sites into
  `ApplySeparation`/`AvoidanceDir` keep identical signatures.
- Verified two ways, no Unity Editor available in this environment: (1)
  the flightcheck stub-compile harness (Unity gameplay scripts +
  citygen/roster/creature-mesh DLLs) compiles clean with the new file
  added; (2) a standalone console harness compiling the real
  `SpatialGrid.cs` against a real-math Vector3/Mathf stub confirmed
  docs/25 Phase A's two explicit test requirements: grid neighbour set ==
  brute-force neighbour set (0/200 mismatches across randomized layouts,
  varying query centers and radii spanning the separation/avoidance call
  sites' actual ranges), and per-query cost stays flat (~0.6-1.0ms) from
  100 to 8000 units while brute-force scan cost grows roughly linearly
  (9.6ms->6.5ms over the same range). No on-screen/visual verification was
  performed or claimed.

Status: Phase A done. Phases B (steering controller scaffold), C
(predictive avoidance/speed modulation), D (`DeadlockManager`), E
(cleanup) remain not started, per explicit creator instruction to
implement only this phase.

## 2026-07 — Monster movement steering: Phase B (`MonsterSteeringController` scaffold) implemented

Executed docs/25's Phase B, scoped as written: parity-first, not new
capability. New `unity-client/Assets/Scripts/MonsterSteeringController.cs`
-- a stateless static class (same dependency-free style as `SpatialGrid`,
so it compiles in a standalone harness): `SeparationForce` (the old
`ApplySeparation` per-pair math extracted verbatim, including its
cumulative-push order -- each neighbour after the first is checked against
the position already nudged by earlier neighbours in the SAME call, not the
original position, since that's what the old inline loop actually did),
`AvoidanceBias` (the old `AvoidanceDir` ahead-cone math extracted
verbatim), and `Combine` (new: blends seek + a softened separation nudge +
the avoidance bias into one heading for `MonsterAgent.FollowPath` to steer
by, replacing the old bare `AvoidanceDir` call).

Root cause #1 from the Phase-A-era analysis (seek and separation applied in
*sequence*, not blended, so they fight every frame) is only partially
addressed by design, on purpose: an early numeric-harness run proved a soft
heading blend ALONE is not sufficient to guarantee two bodies never
interpenetrate -- two units driving straight at a shared destination
overlapped past their combined radii once `ApplySeparation`'s hard
positional correction was skipped for path-following units. `Combine`'s
separation term is therefore an earlier-reacting NUDGE layered on top of
the heading choice, not a replacement for the hard correction --
`RuntimeCityBuilder.ApplySeparation` keeps firing unconditionally every
frame from `MonsterAgent.Update()`, completely unchanged in when/how it's
called (still also `Tank.cs`'s own separation call, untouched, docs/25
explicitly keeps tanks out of scope). Fully blending separation into a pure
force with no standalone hard correction is deferred to Phase C
(predictive avoidance + speed modulation), which is where the plan's own
test criteria actually call for smooth head-on/crossing resolution rather
than parity.

- `RuntimeCityBuilder.cs`: `ApplySeparation` now delegates its per-pair math
  to `MonsterSteeringController.SeparationForce` (pure extract, same
  numbers) but is otherwise unchanged -- same call sites, same signature,
  same unconditional per-frame call from both `MonsterAgent` and `Tank`.
  `AvoidanceDir` is retired; its one call site (`MonsterAgent.FollowPath`)
  is replaced by new `SteerFollowPath(self, desiredDir)`, which queries the
  Phase-A neighbour grid (radius now covers whichever of separation's or
  avoidance's own reach is larger, since `Combine` needs the union of both
  from one candidate list) and calls `MonsterSteeringController.Combine`.
- `MonsterAgent.cs`: one call-site rename (`AvoidanceDir` ->
  `SteerFollowPath` inside `FollowPath`) plus an updated doc comment on the
  `ApplySeparation` call in `Update()` explaining why it's still
  unconditional post-Phase-B. State machine, flight, perch, eat, harvest,
  group-facing, ring-settle: unchanged in logic (none of those paths call
  `FollowPath`, so none of them touch `Combine`).
- Verified two ways, no Unity Editor available in this environment: (1) the
  flightcheck stub-compile harness compiles clean with the new file added;
  (2) a standalone real-math console harness (fresh for this phase --
  compiles the real `MonsterSteeringController.cs` against a real Vector3/
  Mathf stub and a minimal `UnitCombat` stand-in, not the flightcheck
  harness's dummy-math stubs) checked: `SeparationForce` is an EXACT match
  (not just qualitative) against a hand-transcribed copy of the pre-Phase-B
  inline math across 500 randomized neighbour configurations, 0 mismatches
  above 1e-5 -- this is the function `Tank.cs`'s separation now runs
  through too, so drift here would be a real regression, not a tuning
  choice; scripted 2-unit scenarios (overtake, shared-destination approach,
  co-linear same-speed follow) run against both the OLD sequential
  pipeline (hand-transcribed `AvoidanceDir` + `ApplySeparation`, an
  independent oracle) and the NEW `SteerFollowPath`/`Combine` pipeline
  confirm: no interpenetration in either pipeline, the overtake asymmetry
  (faster-from-behind arcs around, front unit undeflected) survives, and
  co-linear same-speed following settles to a stable gap (<1.5m swing in
  the final second) in both pipelines rather than oscillating. No on-screen
  visual verification was performed or claimed.

Status: Phase B done. Phase C (predictive avoidance + speed modulation,
where separation actually becomes purely force-based) starts fresh on a
future turn; D (`DeadlockManager`) and E (cleanup) remain not started.

## 2026-07 — Monster movement steering: Phase C (predictive avoidance + speed modulation) implemented

Executed docs/25's Phase C on explicit creator direction to continue
straight through B into C in the same session. Replaced Phase B's
ahead-cone `AvoidanceBias` (removed -- nothing else called it) with
`MonsterSteeringController.PredictiveAvoidance`, a time-to-collision
(RVO-lite) check: for each neighbour, predicts the closest approach
assuming both self and the neighbour keep their current velocity, and only
reacts if that closest approach is inside their combined radii AND within
a 2.5s horizon -- something merely nearby but on a diverging or
non-closing course contributes nothing. This needed a new published
per-unit `LastVelocity` field on `UnitCombat` (the "neighbours' last-known
velocity" docs/25's approved architecture called for) -- `MonsterAgent`
writes it every frame unconditionally (including zero while idle);
`Tank.cs` never sets it, so a tank predictively reads as momentarily
stationary, a safe default given tanks stay out of scope for this plan.
`Combine` also now returns a speed scale (a new `SteeringResult` struct,
`Direction` + `SpeedScale`) computed from how aligned the chosen heading
still is with the original seek direction -- a unit fighting a strong
deflection eases its own throttle instead of shoving full-speed into
whatever's ahead, floored so steering alone never fully stops a unit
(that escalation stays DeadlockManager's job, Phase D).

Contrary to the "separation actually becomes purely force-based" framing
in the Phase B status line above: it does NOT, and that's deliberate, not
a missed step. `RuntimeCityBuilder.ApplySeparation`'s hard positional
correction is still completely unchanged and still fires unconditionally
every frame -- Phase B's own harness already proved a soft blend alone
lets two closing bodies interpenetrate, and nothing about adding
prediction on top of that changes that finding. `Combine`'s separation
term stays what it was in Phase B: an earlier-reacting nudge layered on
the hard correction, not a replacement for it.

- `MonsterSteeringController.cs`: new `PredictiveAvoidance` (replacing
  `AvoidanceBias`), new `SteeringResult` struct, `Combine` now takes the
  caller's actual speed (needed to build a velocity estimate for the TTC
  math, not just a direction) and returns direction + speed scale instead
  of a bare direction.
- `UnitCombat.cs`: new public `LastVelocity` field, doc-commented with the
  Tank.cs caveat above.
- `RuntimeCityBuilder.cs`: `RebuildCombatantGridIfNeeded` now also tracks
  `_maxCombatantSpeed` (from each live combatant's `LastVelocity`, same
  single-pass O(N) style as `_maxCombatantRadius`); `SteerFollowPath`'s
  query reach now adds however far a neighbour closing at
  `_maxCombatantSpeed` plus this unit's own speed could travel within the
  predictive horizon -- Phase B's purely spatial reach would have missed a
  fast-closing neighbour that's still distant right now. `ApplySeparation`
  itself: unchanged.
- `MonsterAgent.cs`: publishes `LastVelocity` unconditionally right after
  the order-dispatch switch; `FollowPath` now threads `speed` into
  `SteerFollowPath`, reads back `SteeringResult.SpeedScale`, and applies it
  to both the actual step distance and the returned (animation-driving)
  velocity, so a slowed-down unit visibly strides slower too, not just
  moves slower. Flying is still a full opt-out (steer = raw seek dir,
  speedScale = 1) -- unchanged from Phase B.
- A standalone numeric harness (extended from Phase B's) caught a real bug
  before it shipped: the first `PredictiveAvoidance` draft computed
  relative velocity as `selfVel - neighbourVelocity` instead of
  `neighbourVelocity - selfVel`, the wrong sign relative to `relPos`'s
  other-minus-self convention -- every genuinely closing pair produced a
  negative predicted time-to-collision and got silently discarded as
  "already past," so predictive avoidance never fired for the exact case
  it exists for. A "blocked unit slows" test (speedScale pinned at 1 the
  whole approach) caught it directly; fixed and re-verified. A second,
  smaller bug in the same pass: the "already inside the buffer, that's
  SeparationForce's job" skip used the PADDED combined radius instead of
  the bare body radius, leaving a dead zone just outside actual contact
  where neither predictive avoidance nor separation reacted -- also fixed.
- Verified two ways, no Unity Editor available in this environment: (1)
  flightcheck stub-compile clean; (2) the standalone harness (real
  `MonsterSteeringController.cs`, real Vector3/Mathf math) reconfirmed
  `SeparationForce` parity (500/500 trials) and added four new checks, all
  passing after the two fixes above: a head-on pair (small lateral offset
  to avoid the exact-zero degenerate case) never interpenetrates and
  passes each other within budget; a 90-degree crossing pair never
  interpenetrates and both reach their goals; a unit approaching a single
  stationary blocker in open space never interpenetrates, reaches its
  goal, starts and ends at full speed scale, and measurably eases off
  (speedScale dips below 0.98, never floors below `MinSpeedScale`) while
  passing it. A first draft of that last scenario used a tight
  three-obstacle corridor and caught the steering blend permanently
  wedging a unit in a three-way local minimum with no way out -- correct
  behaviour for a phase that explicitly excludes deadlock recovery
  (Phase D's whole job), but not what "a blocked unit slows" is meant to
  isolate, so the scenario was simplified to a single obstacle with open
  space to arc around. No on-screen visual verification was performed or
  claimed.

Status: Phase C done. Phase D (`DeadlockManager`) and Phase E (cleanup +
tune) remain not started.

## 2026-07 — Monster movement steering: Phase D (`DeadlockManager`) implemented

Executed docs/25's Phase D on explicit creator direction to continue
straight through B and C into D in the same session. New
`DeadlockManager.cs`: polled periodically (not every frame -- "rare-path
only," per the approved architecture) from `RuntimeCityBuilder.Update()`
on its own 1s timer, independent of the traffic-car timer that Update()
already hosted (split that method so a scene with zero traffic cars still
polls for monster deadlocks). For each unit with `MonsterAgent.WantsToMove`
true (new property: has an active path leg, isn't airborne), tracks
distance moved since the last poll; under `ProgressEpsilon` (1m) for
`StallWindow` (2.5s) counts as stalled. A stalled unit's nearby neighbours
(`YieldRadius`, 6m) each get a temporary sidestep target -- a neighbouring
hex, filtered to the non-blocked set BEFORE any distance comparison (so a
blocked hex is never even a candidate, not merely rejected after the
fact), chosen to maximize distance from the stalled unit -- held for
`YieldDuration` (3s) on new `UnitCombat.YieldTarget`/`YieldUntil` fields
(the "per-unit priority/yield flag the steering controller honours" the
architecture calls for). `RuntimeCityBuilder.SteerFollowPath` reads them:
while a yield is active, the seek direction fed into
`MonsterSteeringController.Combine` is overridden to point at the yield
target instead of wherever the unit's own path was taking it -- separation
and predictive avoidance still run normally against that redirected
heading, so a yielding unit steps aside without shoving through anyone
else. `DeadlockManager` itself never moves, paths, or re-orders the
stalled unit -- "never becomes the primary mover" is satisfied by
construction: it only ever writes a BLOCKER's yield fields.

Two real bugs surfaced only once a standalone harness ran the actual
grant-and-move loop over multiple cycles (not just single-call checks) --
both are worth recording since they're the kind of thing that reads
obviously wrong in hindsight but wasn't obvious from the code:

1. **Mutual retreat.** The first draft granted yields to every unit that
   crossed the stall threshold in a given poll pass. A head-on pair
   jammed against each other both cross the threshold in the SAME pass,
   so both got granted a "back away from the other" target simultaneously
   -- an eternal synchronized retreat, net progress zero, positions
   drifting apart forever. Fixed by granting at most ONE unit's blockers
   per poll pass, matching the architecture's "grants ONE temporary
   priority" wording literally (previously read as just descriptive
   phrasing, not a load-bearing constraint).
2. **Starvation.** Even with one grant per pass, always resolving
   whichever unit happens to be scanned first (list order) meant one
   side of a head-on pair permanently won every contested pass -- its
   partner never got its own "I'm stalled, please yield to me" moment,
   and kept getting shoved further and further past its OWN goal every
   time the other re-stalled nearby. Fixed with a rotating scan cursor
   (`_scanCursor`), the exact same fairness pattern
   `RuntimeCityBuilder`'s traffic-wake cursor already uses elsewhere in
   this file -- not a new idiom, an existing one applied here too.

Even after both fixes, a symmetric two-unit position SWAP through a fully
sealed single-file corridor (goals on each other's original side, so the
yielding unit has to reverse and cross the SAME contested hex a second
time) did not reliably converge -- at equal speed neither unit ever opens
a durable gap, so the pair just migrates down the corridor as a single
never-resolving unit. This was diagnosed as a genuinely different, harder
problem (mutual exclusion / rendezvous, not funnelling) than what docs/25
section 2 actually describes as the target case ("many units FUNNELLING
through a one- or two-hex gap... in the SAME general direction"), and than
what a lightweight "rare-path, never the primary mover" nudge is designed
to solve -- resolving it properly would need the yielding unit to clear
the WHOLE pinch in one grant (not one hex per grant) or a smarter
sidestep heuristic aware of the blocker's own goal direction, neither of
which is in scope here. Documented as a known limitation rather than
silently working around it in the test: the acceptance scenario was
rebuilt around docs/25's own stated problem (a same-direction funnel: N
units converging on one passable hex toward a shared goal beyond it, not
a two-way swap), which the current design handles correctly.

- `MonsterAgent.cs`: new `WantsToMove` property; publishes nothing new
  itself (DeadlockManager reads `Fighter`/`transform` directly) but is the
  thing `RuntimeCityBuilder.Update()` polls over (`_monsters`, already
  existed).
- `UnitCombat.cs`: new `YieldTarget`/`YieldUntil` fields, doc-commented as
  the plan's "priority/yield flag."
- `RuntimeCityBuilder.cs`: implements new `IHexObstacleQuery`
  (`CityContains`/`IsBlocked` -- ground-blocked set only, deliberately
  conservative so a sidestep target is never water regardless of the
  blocker's own amphibious-ness; `HexAt`/`WorldOf` already existed);
  `Update()` split so the new deadlock-poll timer runs independent of the
  pre-existing traffic-car timer; `SteerFollowPath` now checks for an
  active yield before building its query, overriding the seek direction
  when one is present.
- Verified three ways, no Unity Editor available in this environment: (1)
  flightcheck stub-compile clean; (2) a fresh standalone harness (real
  `DeadlockManager.cs`, real `HexCoord`/citygen-core types via
  `MadDr.CityGen.dll`, a small `IHexObstacleQuery` fake backed by an
  actual hex grid, real Vector3/Mathf math) checked the stall-decision
  arithmetic in isolation (fires exactly once per stall window, resets on
  real progress) and `PickSidestepHex` against 300 randomized
  blocker/stalled/blocked-pattern trials: 0 trials ever returned a
  blocked hex, confirming "solid buildings are never entered by a
  sidestepping unit" holds by construction, not by luck; (3) the same
  harness ran `DeadlockManager.Poll` end-to-end against real
  `MonsterAgent`-shaped units in a simple hand-rolled movement/separation
  loop, confirming docs/25's own acceptance scenario (a same-direction
  funnel through a one-hex pinch) clears within budget after the two bug
  fixes above. No on-screen visual verification was performed or claimed.

Status: Phase D done. Phase E (cleanup + tune -- remove any remaining
shims, tune weights against ring-settle and corridor-jam cases, final
docs/12 entry closing the plan) remains not started.

## 2026-07 — Fix: group ring-settle could walk a unit into a building overhang

Creator report: "units picking parking spots... sometimes end up within a
building... they must ALWAYS be cognisant of their environment."

Diagnosis (two candidates were checked, one falsified before fixing the
real one):

- First suspected `TrafficCar.ParkHere`'s fixed +-2.5m curb offset landing
  in a neighbouring building. Built a standalone harness sweeping every
  heading (0-360 degrees) and both offset signs against every real hex
  neighbour direction (`MadDr.CityGen.HexCoord`): 0/864 configurations ever
  reached a building, because a building's rendered footprint is at
  minimum ~7.3m from the ROAD hex's own center (hex spacing 20m minus the
  building cube's ~12.73m half-diagonal), well past a 2.5m offset. This
  theory was geometrically impossible and no code was changed here.
- The real, reachable bug: `MonsterAgent.TickSettle`'s per-step check
  (`!Blocked().Contains(hex)`) is hex-membership only. A building's
  rendered cube (SpawnCube's localScale = HexCoord.HexMeters * 0.9,
  axis-aligned, no rotation) has a half-diagonal (~12.73m) LARGER than a
  hex's own circumradius (~11.55m), so it overhangs past its own hex
  boundary into a neighbour's space -- a neighbour hex that is never
  itself in the blocked set. `WaypointCommander.RingTarget` (the group
  ring-settle target a unit creeps toward once idle) has an UNBOUNDED
  radius that grows with group size (`r = hole + pitch * sqrt(index)`),
  so a large group ordered near a building can produce ring targets whose
  straight-line steps land in that overhang -- the per-step check waves
  them through because the step's own hex was never flagged blocked.

Verified with a standalone harness compiling the real `RingTarget` (copied
verbatim from `WaypointCommander.cs`) and the real hex-cube-overlap math
against the real `HexCoord` (`MadDr.CityGen.dll`): for a 60-unit group
ring-settling next to one building, 7 of 60 targets were geometrically
inside the building's real footprint; the OLD hex-only check would have
let 1 of those 7 through untouched (the reachable bug, reproduced); the
NEW check catches all 7.

Fix:
- `RuntimeCityBuilder.cs`: extracted the existing `SurfaceHeightAt` roof
  cache into a shared `EnsureRoofCache()`; added `InsideBuildingFootprint
  (Vector3)`, checking a world position against the actual rendered
  footprint of the candidate hex AND its six neighbours (not hex
  membership alone), using the same roof cache so no extra bookkeeping.
- `MonsterAgent.cs`: `TickSettle`'s per-step check now also rejects a step
  that clips a building's footprint (`!_flying &&
  _builder.InsideBuildingFootprint(next)`), on top of the existing
  hex-blocked check. Ground-only -- a flyer's own altitude-aware
  `Blocked()` already governs what it can clear, and this XZ-only check
  has no altitude awareness of its own.
- `TrafficCar.cs`: unchanged -- the originally-suspected bug there was
  disproven, not fixed.

Verified: flightcheck stub-compile clean. No visual verification (no
Editor in this environment).

## 2026-07 — Reinstate double-tank backpack as tank_backpack; weighted storage preference

Creator direction: "the Lab and Game... Human faction should statistically
prefer double tank backpacks, single tank, over skin pustules, in that
order." Two things had to be found before this could be built: "double
tank backpack" meant the ORIGINAL `steel_tank` geometry (a rectangular
frame plate with two cylinder tanks inset, one per side) from before it
was redesigned to a single barrel (commit e9dff23, "human monsters always
use the cylinder backpack with proper orientation") -- the creator
confirmed: "Two tanks on either side of a rectangular backpack." "Skin
pustules" is `storage_bladder`, described in its own commit as "pus-filled
sacs... bulging out through the skin." "Human faction" is docs/17's Human
Army spawn pool, which mixes `origins: ['organic', 'tech']` for a
creature's whole genome -- meaning the sensor slot already competes
`storage_bladder` (organic) against the tech tanks on a flat uniform draw
today, exactly the mismatch reported.

Changes:
- `genome-core/catalog.ts`: reinstated the old geometry as a new,
  separate family `tank_backpack` (tech, sensor homolog) alongside the
  current single-barrel `steel_tank` -- not a replacement, both exist and
  breed independently. Added an optional `weight` field to `PartFamily`
  (default 1, i.e. today's plain uniform choice, unchanged for every
  family that doesn't set one): `tank_backpack: 4`, `steel_tank: 2`,
  `storage_bladder: 1` explicitly. Nothing else touched -- antenna/horn/
  sensor_mast/amber_vesicle stay at the implicit default.
- `genome-core/rng.ts`: new `Rng.weightedChoice` -- one `next()` draw
  (same cost as the existing uniform `choice`), so it doesn't shift how
  many random numbers anything called afterward consumes.
- `genome-core/operators.ts`: `randomAllele` (initial spawn generation)
  now calls `weightedChoice` instead of plain `choice`. Mutation's
  family-jump was deliberately left untouched -- tech parts never mutate
  at all (short-circuited earlier in `mutate()`, docs/17's "tech never
  mutates"), so a tank-vs-tank or tank-vs-pustule weight comparison can
  only ever actually matter at initial generation, never at a jump.
- Verified mathematically and by build: for a PURE ORGANIC pool (the
  default `randomGenome` uses, and what `golden.txt` actually exercises),
  every candidate keeps weight 1, so `weightedChoice` collapses to
  exactly the same index as the old `choice` for the same draw -- the
  golden test passed UNCHANGED, no `test:update-golden` needed, because
  this change is a genuine no-op on the tested path. A 20,000-draw
  statistical check of the mixed organic+tech pool (matching the Human
  spawn pool) landed almost exactly on the intended ratio:
  `tank_backpack` ~40.6%, `steel_tank` ~19.9%, everything else (including
  `storage_bladder`) ~10% each -- double tank > single tank > pustule, in
  that order, as asked.
- `genome-core/harvest.ts` + `roster-client/Harvest.cs` (C# twin, kept in
  lockstep): added `tank_backpack` to `STORAGE_FAMILIES` (capacity 85 --
  two tanks beat one, short of a flat doubling) and to `Harvest.cs`'s
  girth/length `Express` mirror (same bounds as `steel_tank`).
- `creature-mesh/CreatureBuilder.cs` + `site/creature-renderer.js` (both
  renderers, lockstep): new `tank_backpack` case restoring the exact
  pre-e9dff23 geometry (frame plate, two inset tanks, contents-coloured
  end caps + sight gauge, corner rivets), reusing the same `PackP`/`PackR`
  mount frame `steel_tank` already uses so per-plan orientation (vertical
  on upright bodies, flat-on-top on crab/arachnid/serpentine/blob) carries
  over for free.
- `docs/22-economy-system.md`: storage family table + battlefield-render
  prose updated to four families and the new weighting.

Tests: creature-mesh 86 green (was 84 -- two new: `TankBackpackShellIsMetal`
plus a new `StorageVesselsBuildRealGeometry` InlineData case;
`StorageContentsReadRedForBloodAndWhiteForBone`'s loop also covers the new
family); roster-client 56 green (unchanged); genome-core 51 green
including the unchanged golden digest; mutator-service 28 green
(genome-core rebuilt and reinstalled as its `file:` dependency). Vendored
`site/lib/*.js` recopied from the genome-core build per the repo's
documented workflow; node --check clean. Whole-Unity-layer compile not
re-run this turn (no unity-client script changed) -- creature-mesh/
roster-client compiling clean covers the packages that did change.

## 2026-07 — Fix: tank_backpack mounted on the head, and mirrored to two

Creator report, immediately after tank_backpack shipped: "The backpack is
showing up in on the head and two of them. I needs be mounted on the back.
Position the same as the other canisters."

Root cause: both renderers decide which socket a "sensor" allele mounts at
via a small hardcoded family list (`IsStorageVessel` in
`creature-mesh/CreatureBuilder.cs`, the `STORAGE_FAMILIES` Set in
`site/creature-renderer.js`) -- membership routes a family to the single,
unmirrored dorsal `back` socket every plan declares; everything else falls
through to the DEFAULT sensor socket, which is head-mounted AND mirrored
(paired, like antenna/horn). `tank_backpack`'s new render case was added
to both files' `switch`/`case` geometry, but never added to either
dispatcher list -- so it fell through to the head+paired default, exactly
matching the report (on the head, and two of them).

Fix: added `tank_backpack` to both lists (one line each). Re-verified with
creature-mesh's existing back-mount test, now parameterized across all
four storage families (`storage_bladder`/`steel_tank`/`tank_backpack`/
`amber_vesicle`) instead of hardcoding just `steel_tank` -- the narrower
version would not have caught this, and won't catch the same class of bug
for a FIFTH storage family later either, which is the actual point of
widening it. (A second attempted regression test asserting the vessel
isn't mirrored, by checking for X-negated vertex twins, was written, run,
and thrown out: it failed on every family including correctly-mounted
ones, because a creature's whole body -- legs, arms -- is already
bilaterally symmetric, so the signal doesn't isolate the vessel at all.
Caught before it shipped by actually running the new test, not just
reasoning about it.)

Verified: creature-mesh 89 green (was 86 -- the parameterized theory adds
3 net cases over the single `steel_tank`-only fact it replaced); flightcheck
stub-compile clean against the rebuilt creature-mesh DLL. No visual
verification (no Editor in this environment) -- the back-mount assertion
(geometry's minZ well behind the body) is the closest available proxy for
"is this actually on the back."

## 2026-07 — Fix: storage vessels rendered untilted on avian's sloped back

Creator report: "On everything they look good BUT on the AVIAN body
torso. It need to tilt and be centred on the back. Parallel to the angle
of the geometry of the back. DO NOT change the position or orientation on
the other bodies, Just the AVIAN."

Root cause: the shared storage-pack frame (`PackP`/`PackR` in
creature-mesh, `packP`/`packR` in the site renderer) maps a vessel's local
(across, along, out) coordinates to world space using two FIXED world-axis
orientations selected by a single boolean (`topMount`) -- "vertical"
(along=+Y, out=-Z) for upright backs, "horizontal" (along=+Z, out=+Y) for
low bodies like crab/arachnid. It never looks at the mount socket's actual
normal beyond that one threshold check. Every "vertical" plan's back
socket has SOME small Y-component in its normal (a slight tilt, e.g. 0.10
-0.15), but only avian's is large (0.5, in
`Nrm = (0, 0.5, -0.87)`) because avian's whole torso genuinely leans
forward as it rises (PlanAvian's lathe levels) -- so avian is the one
plan where the fixed world-vertical pack visibly mismatches the actual
sloped surface underneath it.

Fix, scoped to avian only per the explicit instruction not to touch
anything else:
- Added an optional `PackTilt` field to `Sock` (creature-mesh C#) /
  `packTilt` property (site JS), defaulting to 0/undefined for every
  plan -- left unset everywhere except avian's `Back` socket.
- `PackP`/`packP` now accept a `tilt` parameter (default 0) and rotate
  the (along, out) pair around the across-axis before mapping to world
  Y/Z. At tilt=0 this reduces ALGEBRAICALLY to the exact original
  formula (cos(0)=1, sin(0)=0) -- not just visually close, bit-identical
  -- so every plan besides avian is provably unaffected.
- Avian's `Back` socket sets `PackTilt`/`packTilt` to
  `atan2(0.5, 0.87)`, derived directly from its own existing normal
  (rather than a second hand-tuned constant that could drift from it).
- Threaded the tilt through all four storage families' `PackP`/`packP`
  call sites in both renderers (creature-mesh C# + site JS, lockstep) --
  `Prims.Ellipsoid`/`ellipsoid` themselves have no rotation, so an
  ellipsoid's own bounding shape stays axis-aligned even though its
  CENTRE now sits on the tilted spine; `Tube`/`tube` primitives (the
  dominant barrel/frame geometry) DO visually tilt, since their
  orientation comes entirely from the two tilted endpoint positions, not
  a separate rotation matrix. A documented, honest limitation, not
  silently glossed over.

Verified numerically (creature-mesh): a vertex-set diff against a
`sensor_stub` baseline isolates the vessel's own added geometry (naive
chunk-index diffing was tried first and discarded -- sensor is built
BEFORE eye/leg, so a chunk-count mismatch between the two builds silently
pulled in unrelated eye geometry and produced meaningless numbers; caught
by actually inspecting the probe's output, not assumed correct). Avian's
isolated tank geometry shows Z shifting with Y in the predicted direction
(the top of the tank sits closer to the body, matching the torso leaning
away from it up there) -- dz > 0 confirmed nonzero. Tetrapod/winged/treant
show dz = 0.000 EXACTLY (not approximately) across the same probe,
confirming PackTilt=0 truly reproduces the untouched original math for
every other plan.

Tests: creature-mesh 93 green (was 89 -- new
`AvianStorageVesselTiltsParallelToTheSlopedBack` plus a 3-case
`OtherPlansStorageVesselStaysUntilted` theory). flightcheck stub-compile
clean against the rebuilt creature-mesh DLL. Site JS re-checked with
`node --check`; every `packP` call site in the file (not just the four
storage cases) confirmed to pass the new tilt argument or be the function
definition itself. No visual verification (no Editor/browser in this
environment) -- numeric proof only, per this repo's standing discipline.

## 2026-07 — Follow-up: raise avian's storage-vessel mount higher on the back

Creator, after confirming the tilt fix looked right: "Avian tanks angle
is good, but need to be higher up on the back."

The Back socket's position was the exact midpoint between the chest
(`levels[2]`) and shoulder (`levels[3]`) levels along PlanAvian's own
rear-surface interpolation. Raised by biasing that same interpolation
toward the shoulder level instead of the midpoint (`BackBlend`/
`backBlend`, 0.5 -> 0.85, in both `CreatureBuilder.cs` and
`creature-renderer.js`) -- both Y and Z still come from the SAME
level2/level3 blend as before, just further up it, so the mount stays
seated on the actual sloped surface at the new height rather than an
arbitrary offset disconnected from the body geometry. `BackBlend`/
`backBlend` is mathematically guaranteed to move the mount strictly
higher (shoulder Y > chest Y by construction), independent of body
bulk/leg-length params.

Verified: creature-mesh 93/93 green unchanged (the existing back-mount
theory test still passes at the new position; no new test added for this
follow-up since the height increase is a direct, provably-correct
proportional shift along an already-tested interpolation, not new
behavior needing its own coverage). flightcheck stub-compile clean
against the rebuilt DLL. Site JS re-checked with `node --check`. No
visual verification (no Editor/browser in this environment).

## 2026-07 — Fix (Lab only): storage vessels missing their body's gait channel

Creator: "Verify that that is true of the Avian body and tank combo(s) and
pustules. In the lab it is certainly not parented properly." (Following up
on a claim that Unity-side rendering correctly parents the tank under the
torso.)

Verified both halves precisely rather than assuming either:

- **Unity**: confirmed clean. `packages/creature-mesh` (the C# package
  Unity's `MonsterBody.cs` calls) has NO per-vertex animation-channel
  concept at all (grepped for `SetAnim`/`SetGait`/`.anim`/`.gait` across
  every `.cs` file in the package -- zero hits). `MonsterBody.cs` animates
  the whole rigid body mesh (torso, head, hands, eyes, AND the storage
  vessel, all one merged static mesh) via a single `_torso` Transform's
  `localPosition`/`localRotation` (walk bob, flight lean/bank, hover,
  blob squash) -- since the vessel is a rigid descendant of that Transform
  with no independent positioning anywhere else in the file, it's carried
  along automatically. This part of the report was correct as stated.
- **The Lab (`site/creature-renderer.js`)**: the creator's report was also
  correct, and the mechanism is different from Unity entirely -- this
  renderer bakes a `[anim0..3]`/`[gait0..3]` value into every VERTEX
  (`MeshB.vert`), consumed by the vertex shader to compute the
  breathing/locomotion deformation live. `buildPart` sets these from
  `sock.anim`/`sock.gait` (defaulting to a literal zero channel,
  `ANIM0`/`GAIT0`, if the socket doesn't specify one) BEFORE emitting the
  part's own geometry. The `back` socket (storage vessels) on 4 of the 9
  body plans set `anim` but never `gait`, or set neither: **avian**
  (missing `gait` -- the one directly reported), **winged** (missing
  `gait`), **treant** (missing `gait`), **serpentine** (missing BOTH --
  arguably the worst case, since serpentine's whole body is permanently
  mid-slither, never at rest, so a channel-less vessel would hover
  visibly motionless while the coil animates under it). The other 5 plans
  (tetrapod, blob, crab, arachnid, floater) already correctly paired
  `anim` with a matching `gait` on their own `back` socket -- this was
  never a systemic design gap, just 4 plans that missed the pattern the
  other 5 already established.

Fix: added the missing `gait` (and, for serpentine, `anim` too) to each
of the 4 plans' `back` socket, copying the EXACT value that plan's own
torso/coil already uses for `mb.setGait(...)` in the region the vessel
mounts to (avian/winged: `[0,0,0,0.1]`; treant: `[0,0,0,0.03]`;
serpentine: `anim: SWAY_H, gait: [0,0,5.0,0.30]`, matching what its own
hand/sensor/eye sockets already do) -- not invented numbers.

Verified: `node --check` clean. No headless WebGL harness exists in this
environment (the module exports only browser entry points, not the
internal plan builders, and rendering needs a live canvas context), so
this was verified by tracing the exact `sock.gait -> mb.setGait ->
MeshB.vert -> shader` consumption path rather than by a live render --
consistent with this repo's standing "no visual verification available"
discipline. Every plan's `back` socket now sets both `anim` and `gait`,
confirmed by re-grepping all nine after the fix.

## 2026-07 — Fix: tilted-tank caps/collar/plate were still axis-aligned (avian only)

Creator: "Now the alignment on the top and bottom of the Single tank is
misaligned and the mounting disk on the double tank (the actual double
tanks are perfect do not alter), On the Avian body, ONLY the Avian
torso, DO NOT touch the other body types."

Root cause: this is the exact limitation flagged (but not yet fixed) in
the earlier avian-tilt entry above. `PackP` correctly moves a part's
CENTRE onto avian's sloped back, but every cap/collar/plate on a tank is
drawn with `Prims.Ellipsoid`/`ellipsoid()`, which had no rotation concept
at all -- an ellipsoid's own SHAPE stayed aligned to world Y/Z regardless
of where its centre moved to. On steel_tank this reads as the top and
bottom end caps sitting at an angle to the barrel instead of flush
across it ("alignment on the top and bottom... misaligned"); on
tank_backpack the equivalent piece is the frame plate/end-cap geometry
("the mounting disk"). The TUBE-based barrels themselves (both tanks'
actual cylinders) were never wrong -- a `Tube`'s orientation comes
entirely from its two endpoint positions, both already correctly moved
by `PackP`'s tilt -- which is exactly why the creator could tell the
double tank's own barrels were "perfect" while something else on it
wasn't.

Fix: added the same kind of provably-safe optional parameter used for
`PackTilt` itself, this time to the shared `Prims.Ellipsoid`/
`ellipsoid()` primitive (`tilt = 0`, rotating the ellipsoid's own (Y,Z)
shape around the across-axis before mapping to world space). Derived by
hand so tilt=0 reduces ALGEBRAICALLY to the exact original per-vertex
expressions (not approximately -- confirmed token-for-token, and by 93
existing tests across the whole renderer staying green unchanged, since
this primitive is used by essentially every piece of geometry in the
game, not just tanks). Threaded `packTilt` through every `Ellipsoid` call
in all four storage families (steel_tank, tank_backpack, storage_bladder,
amber_vesicle) in both renderers; deliberately left every `Tube` call
untouched, matching "the actual double tanks are perfect do not alter."

Verified with two new direct tests against `Prims.Ellipsoid` itself
(bypassing the whole avian body, so the check is exact, not
inferred from an assembled mesh): `EllipsoidTiltZeroReproducesThe
UntiltedShapeExactly` confirms tilt=0 is byte-identical to the pre-fix
shape; `EllipsoidTiltRotatesTheCapToFaceAlongTheTiltedAxis` confirms
that at avian's own derived tilt angle, a cap's position AND normal
rotate to face the SAME (Y,Z) direction `PackP` moves the cap's centre
along -- i.e. the cap and the tube it closes off are finally facing the
same way, not just co-located. creature-mesh 95 green (was 93). Diffed
the C# change and confirmed every `Prims.Tube` call line is untouched.
flightcheck stub-compile clean against the rebuilt DLL. Site JS
`node --check` clean; ported the identical formula. No visual
verification (no Editor/browser in this environment) -- verified via the
primitive-level geometric proof above instead.

## 2026-07 — Special Attacks System: design + Phase 1-3 (docs/26)

Creator direction: design a modular special-attacks framework for enemy
AI (worked example: an Arachnid Web Attack -- capture/pull human-scale
targets, slow heavy ones), following an explicit "understand the
architecture, inspect before proposing, plan, identify risks, wait for
approval" process -- no code until the design was reviewed.

Research (four parallel deep-dives, no code written) established: the
project's one cooldown idiom (Time.deltaTime-decremented float, reload-
on-trigger, `<= 0f` gate -- UnitCombat._cooldown, MonsterAgent.
_attackCooldown, RuntimeCityBuilder's poll timers all match); that
`packages/match-core` (the future deterministic RTS sim) has genuinely
zero unit/combat code today, in tension with CLAUDE.md's own "don't add
gameplay decisions to MonsterAgent.Update()" directive; that no
capture/pull/AoE/status-effect/mass-classification mechanic exists
anywhere in the current combat system (TickEat's "consume" is a single-
frame contact trigger, not a multi-frame capture state); and that
`YieldTarget`/`YieldUntil` (docs/25 Phase D) is the closest existing
"temporary externally-imposed movement override" precedent but is
snapshot-position-only and path-gated, not directly reusable for a
moving captor.

Two genuine architectural forks were surfaced and put to the creator
rather than decided silently: (1) build against the current Unity
MonoBehaviour architecture vs. an engine-agnostic-friendly core (matching
the MonsterSteeringController/DeadlockManager/SpatialGrid precedent from
docs/25) vs. waiting for match-core -- **creator chose pure Unity, no
portability layer**; (2) SpecialAttackDefinition as a plain C# class
(matching WeaponProfile) vs. a ScriptableObject -- **creator chose
ScriptableObject**, the first one in this codebase.

Implemented Phases 1-3 of the resulting plan (see docs/26 for the full
architecture, files-touched list, and remaining Phase 4-8 roadmap):
- `SpecialAttackDefinition.cs` (new, ScriptableObject): name/description/
  cooldown/range/AoE/valid-targets/effect-type/AI-use-requirements/VFX-
  SFX hooks.
- `SpecialAttackInstance.cs` (new, plain C#): per-unit runtime cooldown
  state, wrapping a (possibly shared) Definition -- Tick/IsReady/
  TriggerCooldown, identical idiom to UnitCombat._cooldown.
- `UnitCombat.cs`: `Mass` field (continuous, populated from the same
  plan-mass table `Combat.Profile` already computes for HP -- not a
  per-species tag table) and `Abilities` list, ticked unconditionally in
  Update() so a cooldown counts down through any AI state change, not
  just while that ability's own order is active. `Configure(...)` gained
  an optional `mass = 1f` trailing parameter; every existing call site is
  unaffected.
- `Tank.cs`: explicit `mass: 10f` -- the project's one concrete "heavy
  target" example.
- `MonsterAgent.cs`: `OrderKind.SpecialAttack`, `OrderSpecialAttack(...)`,
  `TickSpecialAttack(dt)` (approach into range exactly like
  TickAttackUnit, then TriggerCooldown and return to Idle -- no ability
  EFFECT yet, deliberately, per the phased plan), wired into the
  Update() dispatch switch, ClearTargets(), OnDied(), and the debug
  OrderDescription string.

Verified: flightcheck stub-compile clean (added ScriptableObject/
CreateAssetMenuAttribute/TextAreaAttribute/AudioClip to the shared Unity
stub -- the first time this project needed them). A standalone harness
compiling the real SpecialAttackDefinition.cs/SpecialAttackInstance.cs
directly confirmed the cooldown state machine: reload-on-trigger exactly
to Definition.Cooldown; correct accumulation across irregular (non-
fixed-step) dt values, matching real Time.deltaTime variance rather than
assuming a frame rate; and -- the requirement most worth checking twice
-- two units sharing one SpecialAttackDefinition ASSET do not share a
cooldown, since each has its own SpecialAttackInstance. No visual
verification (no Editor in this environment).

Status: Phases 1-3 done (state-machine wiring only). Phase 4
(WebAttackAbility targeting + AoE resolution, no capture/consume yet) is
next, explicitly not started this session per the "small, safe,
independently testable steps" plan docs/26 lays out.

## 2026-07 — Special Attacks System Phase 4: Web Attack targeting + AoE

Continuing docs/26's phased plan (creator: "continue").

Implemented Phase 4 -- `WebAttackAbility` targeting + area-of-effect
resolution only, deliberately no pull/capture/consume/slow effect yet
(that's Phases 5-7): the phase's whole point is that targeting and
target-classification are proven correct in isolation before any harder
multi-frame capture-state work is built on top of them.

- `Projectile.cs`: one additive `OnArrive` hook (a `System.Action<Vector3>`
  fired once at arrival, before the object is destroyed, with the real
  impact position). Every existing caller (`WeaponFx`) never sets it, so
  every shot fired today is completely unaffected -- purely an extension
  point for a non-homing effect that needs to do something on arrival
  beyond "damage the one target," which the existing `_homing` branch
  already owns.
- `WebAttackAbility.cs` (new): `Launch` spawns a non-homing web bolt at a
  SNAPSHOT of the target's position at cast time (an area effect resolves
  at a location once it lands, not on whichever single unit happens to
  still be standing there -- it does NOT track the target after launch,
  unlike a normal weapon shot). On arrival (`ResolveImpact`): queries
  `RuntimeCityBuilder.QueryCombatantsInRadius` (new, thin wrapper over
  the EXISTING docs/25 neighbour grid -- no second grid built) for
  monsters/tanks, and linearly scans `RuntimeCityBuilder.Citizens` for
  citizens -- confirmed by the original research pass, not assumed, that
  `Citizen` carries no `UnitCombat` at all and has no spatial grid of its
  own, so this matches the project's existing citizen-scanning
  convention (`DistanceAhead`) rather than inventing a citizen grid for
  one ability.
- The actual decision logic is three small PURE functions, deliberately
  pulled out of `ResolveImpact`'s side-effecting body so they're directly
  testable: `IsHeavy(mass)` (the mass-threshold check), `MatchesFilter
  (category, filter)` (plain flag membership), and `ShouldCatchCombatant`
  (alive, not the caster itself, not a same-faction ally -- no
  friendly-fire capture -- within the exact circular radius, and of a
  category the ability's `ValidTargets` actually allows). A first draft
  of `ShouldCatchCombatant`'s equivalent inline logic had a real bug,
  caught before it shipped by writing the standalone harness rather than
  just reading the code: it checked "does the filter allow EITHER
  Human OR Monster at all" instead of "does the filter allow the
  category THIS specific combatant represents" -- meaning a Human-only
  web would have wrongly caught monsters too, as long as the Human flag
  happened to also be set. Fixed to map each combatant to its actual
  category (`Faction == "human" -> TargetFilter.Human`, else `Monster`)
  before checking the filter.
- `MonsterAgent.TickSpecialAttack`: on reaching range, now actually casts
  (`WebAttackAbility.Launch`, keyed off `Definition.EffectType ==
  PullAndConsume`) instead of only triggering the cooldown -- cooldown
  still starts at the moment of casting/deployment, matching
  `UnitCombat.TryFire`'s own "reload only once a shot actually goes out"
  timing, and the task brief's own "after deployment... enters cooldown
  state" wording.

Verified: flightcheck stub-compile clean (0 warnings), including new
`ScriptableObject`/`AudioClip`/`AudioSource`/`Object.Instantiate` stub
additions the shared harness needed for the first time. A standalone
harness compiling the real `WebAttackAbility.cs` (plus the real
`UnitCombat.cs`/`Projectile.cs`/`WeaponFx.cs`, with lightweight
placeholder `RuntimeCityBuilder`/`Citizen` types since this phase's
tests never call into either) drove 8 checks directly against real
`UnitCombat` instances: the mass boundary is exactly `>=` (not `>`);
filter matching; in-range vs. out-of-range; the caster is never caught
by its own web; no friendly-fire capture; a dead target is excluded;
and a target whose category the filter disallows is excluded (this is
the exact check that caught the bug described above). No visual
verification (no Editor in this environment).

Status: Phases 1-4 done. Phase 5 (heavy-target slow effect, the simpler
of the two remaining branches) is next.

## Special Attacks System Phase 5: heavy-target slow + possessed-unit friendly fire (2026-07)

Creator direction: "continue phase 5. Make sure it applies to all
monsters. Obviously friendly fire has no effect, unless the unit is
possessed. Which should be in the design docs!" Two requirements, both
addressed:

**Generic to all monsters.** The slow-status mechanic was deliberately
built on `UnitCombat` (`_slowRemaining`/`_slowMultiplier`,
`SpeedMultiplier` property, `ApplySlow(multiplier, duration)` — takes
the stronger multiplier and the longer remaining duration on
reapplication, so a weak reapplication can never dilute an active
stronger slow), not on `MonsterAgent` or `Tank` individually. Every
mover reads the same `SpeedMultiplier`: `MonsterAgent.RunOrWalkSpeed()`
now multiplies in `_fighter.SpeedMultiplier` (covers every bred/genome
monster with zero per-species wiring), and `Tank.cs`'s own
hull-movement line multiplies in `_combat.SpeedMultiplier` (a tank is
this project's one concrete heavy-target example, so it visibly slows
too). `WebAttackAbility.ResolveImpact`'s heavy branch now calls
`c.ApplySlow(HeavySlowMultiplier, HeavySlowDuration)` (new v0.1
placeholder constants: 0.35x speed for 3s) instead of only logging.

**Possessed units and friendly fire.** The existing no-friendly-fire-
capture rule in `WebAttackAbility.ShouldCatchCombatant` was
`if (c.Faction == caster.Faction) return false;` — read literally,
"same faction is always safe." The creator's direction reframes it as
"an ally is safe unless it's no longer really an ally." Added
`UnitCombat.IsPossessed` (default `false`, fully behavior-inert today —
nothing sets it true anywhere) and changed the check to
`if (c.Faction == caster.Faction && !c.IsPossessed) return false;`, so a
possessed same-faction unit WOULD be caught by its own side's web. No
possession/mind-control mechanic is being built now — this is a
forward-compatible hook plus a documented rule, connecting to the
creator's earlier, separate direction on record ("Mad Doctor Biological
strength, mind control on very big brain units") so a future
mind-control ability doesn't require revisiting every special attack's
friendly-fire logic. Documented in docs/26 under "Possessed units and
friendly fire."

Verified: flightcheck stub-compile clean across `UnitCombat.cs`,
`WebAttackAbility.cs`, `MonsterAgent.cs`, `Tank.cs`. The `webattackverify`
harness (compiling the real shipped files, not a reimplementation)
gained 7 new checks: default-unaffected `SpeedMultiplier`, applying a
slow reduces it, a weaker reapplication doesn't dilute an active
stronger one, a stronger reapplication does deepen it, reapplication
takes the longer remaining duration (read via reflection since
`_slowRemaining` is intentionally private — no test-only field added to
shipped code), `WebAttackAbility`'s heavy-branch constants actually
compose with `ApplySlow`, and a possessed same-faction unit IS caught
while an ordinary ally still is not. All 15 checks (8 from Phase 4, 7
new) pass.

Next: Phase 6 (`CaptureState` + pull-toward-captor for human-class
targets — the riskiest step, new interruptible multi-frame state).

## Special Attacks System Phase 6: CaptureState + pull-toward-captor (2026-07)

Creator direction: "Do it" (continue to the next approved phase in the
docs/26 plan). Phase 6 delivers the riskier of the two remaining
branches: a caught non-heavy target is now actually dragged toward its
captor, not just logged.

New `CaptureState.cs`: `Captor`, `Speed`, `Active` (captor non-null and
alive), `Begin(captor, speed)`, `TickPull(transform, dt)` (moves toward
the captor at `Speed`, clamped so it never overshoots, and simply stops
once within `ArriveRadius` — consumption is explicitly out of scope for
this phase, see below). Built as its own standalone class rather than
fields directly on `UnitCombat`, because the identical pull logic has to
work for `Citizen` too — Phase 4's research already established that
`Citizen` carries no `UnitCombat` component at all, so it can't share
one. `UnitCombat` gained `IsCaptured`/`Captor`/`Capture(...)`/
`TickCapture(dt)` (owns one `CaptureState`); `Citizen` gained its own
separate `Capture(...)` + a `_capture` field, checked at the very top of
`Update()` ahead of even its flee logic (capture overrides everything —
a caught citizen is being dragged, not choosing to run).

Auto-release needed no explicit cleanup call: `IsCaptured`/`Active` read
the captor's live `Alive` state directly, so a captor's death is
reflected the very next check — the "ability interrupted, captor dies
mid-cast" risk from docs/26 §5, handled for free by construction rather
than by a separate event/callback.

Generic-to-all-monsters (same lesson as Phase 5): the capture check was
wired into every mover, not just Citizen. `MonsterAgent.Update()` checks
`_fighter.IsCaptured` right after its existing death check and runs a
new `TickCaptured(dt)` instead of the `_order` switch while true — the
paused order is never touched, so whatever the unit was doing resumes
automatically once released. `Tank.cs` got the identical check at the
top of its own `Update()`. Both are reachable only in edge cases today
(an ordinary monster is never a valid target of its own faction's web —
see the Phase 5 possessed-unit entry above — so only a *possessed*
monster caught by its own side could ever be captured; a Tank's Mass is
always 10, always heavy, so it's never captured either) but were wired
anyway, matching the same "inert today, real hook" precedent as
`IsPossessed`.

`WebAttackAbility.ResolveImpact`'s non-heavy branch (both the
`UnitCombat` combatant loop and the `Citizen` linear scan) now calls
`.Capture(caster, CapturePullSpeed)` (new placeholder, 6 m/s) instead of
only logging.

Explicitly NOT built this phase (by design, per the existing phase
boundary, not an oversight): consumption on arrival. A captured target
that reaches its captor holds position there today — visibly restrained,
still following if the captor moves — until Phase 7 wires the actual
eat/destroy step (trivial for citizens, reusing the existing
`OnCitizenEaten`; genuinely new design work for a non-Citizen captured
target, since no generic "consume a UnitCombat" path exists yet).

Verified: flightcheck stub-compile clean across every touched file
(`UnitCombat.cs`, `CaptureState.cs`, `MonsterAgent.cs`, `Tank.cs`,
`Citizen.cs`, `WebAttackAbility.cs`). `webattackverify` gained 6 new
checks against the real shipped files: `Capture()` sets `IsCaptured`/
`Captor`; `IsCaptured` reads false the instant the captor dies; a tick
closes exactly `Speed * dt` toward the captor without overshooting; it
holds position once within `ArriveRadius`; re-capture retargets to the
newest captor (last web wins, no stacking); and the non-heavy branch's
capture effect is confirmed mutually exclusive with the heavy branch's
slow effect on the same catch. All 21 checks (15 from Phases 4-5, 6 new)
pass.

Next: Phase 7 (consume-on-arrival, wired into `OnCitizenEaten` for
citizens; the non-Citizen path remains a real open design question).

## Special Attacks System Phase 7: consume-on-arrival for captured citizens (2026-07)

Creator direction: "Proceed" (continue to the next approved phase).
Phase 7 closes the loop Phase 6 deliberately left open: a captured
target that reaches its captor no longer just sits there.

`CaptureState.TickPull` now returns `true` once the victim is within
`ArriveRadius` (was `void`); `UnitCombat.TickCapture` and `Citizen`'s own
capture branch both propagate this. `Citizen.Update()` acts on it
directly: the instant a dragged citizen arrives, it calls
`_builder.OnCitizenEaten(this)` -- the exact same method a chased-and-
caught citizen already goes through via `MonsterAgent.TickEat`, so
wallet credit (Blood 2 / Bones 1 / Brains 1), blood-splatter FX, and
despawn are identical either way. No new consumption logic was needed
for citizens -- just wiring the existing, already-tested method to a new
trigger.

Flagged, not hidden: a web-captured citizen does NOT fill the eating
monster's harvest tank the way a direct chase-and-eat order does
(docs/22's `_carriedLoad` credit lives inside `MonsterAgent.TickEat`
specifically). `Citizen` has no reference back to the capturing
`MonsterAgent` -- only to the `UnitCombat` it's being pulled toward -- so
crediting this would need a new back-reference or an owner-lookup on
`RuntimeCityBuilder`. Not attempted here; logged as a real follow-up,
matching this project's existing convention (e.g. `Tank.SpawnWreck`'s
"visual breakdown only" note) rather than silently under-scoping it.

Non-Citizen consume path: designed, not built, exactly per the existing
phase-7 boundary. No light, non-heavy `UnitCombat` target exists
anywhere in the project to build and test this against today (a Tank is
always heavy; an ordinary monster is never a valid target of its own
faction's web -- only a possessed one, per the Phase 5 note). Building
it now would be untestable premature generality. The designed shape:
`UnitCombat.TickCapture` already returns the same arrival signal
`Citizen` uses; the owning mover (`MonsterAgent.TickCaptured`, or a
future `Tank` equivalent) would read `true` and apply lethal damage to
itself via its own existing `TakeDamage`, routing through the
already-correct death/`_onDied`/wreck-cleanup path -- no second, parallel
destroy path needed on `UnitCombat` itself.

Verified: flightcheck stub-compile clean (`CaptureState.cs`,
`UnitCombat.cs`, `Citizen.cs`). `webattackverify` gained one new check:
`TickCapture` returns false while still approaching and true once within
`ArriveRadius` -- the exact signal `Citizen.Update()` acts on. All 22
checks (21 from Phases 4-6, 1 new) pass. No live-scene test exists for
the citizen-eaten trigger itself (same compile+pure-logic verification
limit as every other Unity behaviour this session).

Next: Phase 8 (AI decision heuristic -- `EvaluateBestAbility`-equivalent:
distance, weighted target count in AoE, cooldown state, a minimum
usefulness threshold), the last phase in the approved plan.

## Special Attacks System Phase 8: AI decision heuristic -- plan complete (2026-07)

Creator direction: "Do it" (continue to the last approved phase). This
closes the docs/26 phased plan: all 8 phases are now implemented.

New `WebAttackAbility.CountCatchable(builder, caster, definition,
impactPoint)`: runs the exact same query and the exact same
`ShouldCatchCombatant`/`MatchesFilter` decisions `ResolveImpact` itself
uses, but tallies instead of applying effects, so the AI heuristic can
never pick a target the resolver would then fail to catch -- one query
implementation backs both "would this land" and "how good would it be."

New `MonsterAgent.EvaluateBestAbility(out ability, out anchor)`: for
every equipped ability off cooldown, scans combatants within that
ability's own Range via `QueryCombatantsInRadius`, treats each as a
candidate anchor (validated with `ShouldCatchCombatant` at the
candidate's own position), scores every valid anchor with
`CountCatchable`, and accepts only a score clearing the ability's own
`Definition.MinTargetsInArea` -- a Phase 1 field that had gone unused
until now. The highest-scoring ability+anchor across every equipped
ability wins. Wired into `AcquireTarget` ahead of both retaliation
(`LastAttacker`) and the plain nearest-enemy engage: a special attack
that clears its own bar is treated as more valuable than a single
regular shot at whoever's nearest or last hit this unit.

`AcquireTarget`'s old all-or-nothing guard (`_fighter == null ||
_fighter.Weapon == null || !_fighter.Weapon.CanAttack`) was narrowed to
just `_fighter == null` up front, moving the weapon check down to guard
only the plain-attack fallback -- so a future special-attack-only
creature with no conventional weapon could still use its ability.
Behaviorally inert today for every existing monster (this refactor
changes nothing observable), since `Abilities` is empty everywhere --
see below.

Explicitly noted, not hidden: no monster anywhere is actually equipped
with a `SpecialAttackInstance` yet. Equipping one means dragging a
`SpecialAttackDefinition` ScriptableObject asset onto a creature in the
Unity Editor -- a creator/Editor-side step with no code path, and (per
docs/26 Fork 2) the entire reason ScriptableObject was chosen over a
plain class in the first place. `EvaluateBestAbility` is fully built and
fully tested; it starts doing real work the moment a creature is
actually equipped.

Verified: flightcheck stub-compile clean. `webattackverify` gained 3 new
checks against the real `WebAttackAbility.CountCatchable` -- needed a
small stub upgrade first: `RuntimeCityBuilder`'s
`QueryCombatantsInRadius`/`Citizens` are now settable and genuinely
radius-filtered (were previously hardcoded to always return empty), so a
scene can actually be populated without a real spatial grid. Checks:
tallies every valid combatant in range; excludes an ally/out-of-
range/dead combatant while still counting the one valid target; citizens
count only when `ValidTargets` allows `Human`. `EvaluateBestAbility`
itself needs a live `Transform`/`_builder`/`_fighter` to exercise and
wasn't tested directly, matching this session's standing discipline --
only the pure/query logic it composes (`CountCatchable`) is
harness-tested, the same way `ResolveImpact` itself was never tested
directly either. All 25 checks (22 from Phases 4-7, 3 new) pass.

**docs/26's 8-phase plan is complete.** Three follow-ups remain, all
flagged in the doc rather than hidden: no creature is actually equipped
with a special attack yet (an Editor-side task outside this plan); the
non-Citizen consume path is designed but not built (nothing testable
exists to build it against); web-captured citizens don't credit the
eating monster's harvest tank (Citizen has no back-reference to the
capturing MonsterAgent).

## Special Attacks System follow-up: harvest-tank credit for web-captured citizens (2026-07)

Creator direction: "You should definitely do step 3. Unless it is
covered in the consume phase." Checked: Phase 7's consume-on-arrival
wiring only called `OnCitizenEaten` (wallet/gore FX/despawn) -- it
explicitly did NOT credit the harvest tank, and the doc had flagged this
as a known, not-hidden gap rather than silently covering it. Not covered
by any phase, so implemented now as a standalone follow-up.

Fix: extracted the harvest-credit lines already living inside
`MonsterAgent.TickEat` into a new private `CreditHarvestForEatenCitizen()`
(identical formula: `Mathf.Min(_harvest.Capacity, _carriedLoad + 3 *
_harvest.GatherBlood)`), and added a new public
`MonsterAgent.NotifyCapturedCitizenEaten()` that calls it. `Citizen.
Update()`'s capture-arrival branch now looks up the capturing
`MonsterAgent` via `_capture.Captor.GetComponent<MonsterAgent>()` and
calls it before `OnCitizenEaten` -- this is the exact back-reference the
docs/26 Phase 7 entry said would be needed, resolved via `GetComponent`
rather than a new field, since `MonsterAgent.Init` adds `_fighter` to
`gameObject` itself, so the `UnitCombat` a `Citizen` was dragged toward
and the `MonsterAgent` that owns it are always co-located. A web-captured
citizen now counts as the exact same kill as a directly chased-and-eaten
one in every respect.

Verified with a new dedicated harness, `harvestcreditverify` (compiles
the REAL `MonsterAgent.cs` plus its full dependency chain -- same file
list as `flightcheck`, since `MonsterAgent.cs` pulls in `MonsterBody`,
`RuntimeCityBuilder`, and the whole gameplay layer regardless of which
harness compiles it). Reads/writes `MonsterAgent`'s private `_harvest`/
`_carriedLoad` fields via reflection (same discipline as `UnitCombat.
_slowRemaining`), since nothing outside `MonsterAgent` should otherwise
touch them. 3 checks: crediting matches `TickEat`'s own formula exactly;
credit caps at the vessel's `Capacity` instead of overflowing; a monster
with no `HarvestProfile` at all is a safe inert no-op. All 3 pass.

Worth recording: the first run of these checks all "failed" at 0,
because this new harness's `UnityStub.cs` was seeded from `flightcheck`'s
own copy -- and `flightcheck` is a pure compile-check harness that never
inspects a computed value, so its `Mathf` stub hardcodes every float
function to `return 0f`. Silently correct for compile-checking, silently
WRONG for a harness that asserts on real numbers. Caught immediately
(the harvest math obviously couldn't produce 0 for both the plain-credit
and the capacity-cap checks) and fixed by patching `Mathf` to real math,
matching `specialattackverify`/`webattackverify`'s existing stubs. Flagged
here in case a future harness copies `flightcheck`'s stub again.

This closes the last of the three follow-ups noted when docs/26's 8-phase
plan completed. Two remain, both genuinely out of scope for code alone:
no creature is actually equipped with a special attack yet (an
Editor-side task), and the non-Citizen consume path is designed but not
built (nothing testable exists to build it against).

## Special Attacks System Phase 9: secondary attacks for all races (2026-07)

Creator direction: "Roll secondary attacks for all races into the lab
and all monsters. Humans get flamethrowers (damage only). Aliens get
psionic attack, short tractor beam is the same as web. mad dr. Ground
stomp stun effect. With hooks for a bunch of future additions."

Researched before writing any code: "Alien" is a real, established
faction in design docs (docs/17-factions.md) and match-core's
FactionDef.cs (FactionId.MadDoctor/HumanArmy/AlienHive), but the LIVE
Unity battle code only ever had two faction strings, "monster"/"human" --
no alien unit exists in the actual game. FactionId.MadDoctor turned out
to BE the player's own monster faction, not a separate in-game unit --
there's no standalone "Mad Doctor" combatant to build. So "Aliens" and
"Mad Dr" here read as FLAVORS of the existing monster population, not
new live factions: Combat.WeaponFor (roster-client) already classifies a
monster's hand family into alien-tech weapons
(laser_array/photon_blaster/plasma_lance -- confirmed as a real, named
group in packages/genome-core/src/catalog.ts, origin: "biotech") vs
organic ones, so that SAME signal was reused to decide which secondary
attack a monster gets, with zero new genes and zero new faction plumbing.

Implementation:
- New `SpecialAttackEffectType.Stun` + `SpecialAttackDefinition.
  DamageAmount`/`StunDuration` fields.
- New `UnitCombat` stun state (`IsStunned`/`ApplyStun`), separate from
  the Phase 5 slow pair since stun is binary (no magnitude to protect on
  reapplication) and also gates `ReadyToFire` (a slow never stops
  firing). `SpeedMultiplier` reads 0 while stunned, overriding any
  active slow, and reuses the exact Phase 5 mover plumbing
  (RunOrWalkSpeed/Tank hull movement) with zero new mover-side code.
- New `SpecialAttackResolver.cs`: one shared `ResolveInstant(...)` +
  `ApplyEffect` switch for every non-projectile effect (Damage, Stun),
  reusing `WebAttackAbility`'s already-generic `ShouldCatchCombatant`/
  `MatchesFilter` for the catch/classify step instead of duplicating it.
  This is the "hooks for a bunch of future additions" piece: adding a
  5th effect kind is one enum value + one switch case + (if needed) a
  new tunable field, not a new ability class.
- New `SecondaryAttackCatalog.cs`: builds `Flamethrower()` (Damage only),
  `PsionicTractorBeam()` (PullAndConsume -- literally the same mechanic
  Web Attack already uses, just a shorter-range/AoE definition, since
  WebAttackAbility's resolver was never actually web-specific), and
  `GroundStomp()` (Stun, self-centered, Range=0) via
  `ScriptableObject.CreateInstance<T>()`. `ForMonster(handFamily)` is
  the single switch routing alien-tech hands to Psionic, everything
  else to Ground Stomp.
- Wired equip into `Tank.Init()` (every Tank gets Flamethrower) and
  `MonsterAgent.Init()` (every monster gets
  `SecondaryAttackCatalog.ForMonster(creature.Genome.Slots.Hand.Family)`).
- `MonsterAgent.TickSpecialAttack` now dispatches on `EffectType`
  (PullAndConsume -> projectile via WebAttackAbility.Launch; Damage ->
  instant at the target's position; Stun -> instant at the CASTER's own
  position). `EvaluateBestAbility` gained a self-anchor branch for
  Stun-type abilities (scores against this unit's own position, sets
  `anchor = _fighter`) since there's no target position to anchor a
  self-centered ability's scoring on; `TickSpecialAttack`'s existing
  approach-distance check against a self-anchor naturally reads as
  "already in range" with no separate special case needed there.

**Corrects a standing assumption in this doc**: docs/26 previously said
equipping a `SpecialAttackDefinition` needed an Editor drag-and-drop
step, unavailable in this environment. That was only true for
hand-authored Inspector *assets* -- `ScriptableObject.CreateInstance<T>()`
is a normal runtime API call, so code-built definitions (this catalog)
needed no Editor at all. Every Tank and every monster is now actually,
really equipped -- the long-flagged "nothing is equipped yet" gap is
closed.

**Explicitly NOT done: "into the Lab."** Read narrowly (site/, the
browser test bench itself) rather than as "roll this out broadly": the
Lab has zero combat-simulation or attack-display code today (confirmed
via research before implementation -- it's purely a creature-appearance/
roster gallery), and `Combat.WeaponFor`'s hand-family logic has no
genome-core TypeScript twin at all, unlike Locomotion/Harvest which DO.
Building a Lab-side "this creature's secondary attack" display would
mean inventing that twin from scratch purely for a display feature --
a real, separable follow-up, not attempted without further direction on
what the Lab should actually show. The classification rule itself is
trivial to port whenever that direction comes, since it keys off data
(`Slots.Hand.Family`) the Lab already has.

Verified: flightcheck stub-compile clean across every touched/new file
(one stub addition needed: `ScriptableObject.CreateInstance<T>()`, the
first code anywhere to build a ScriptableObject at runtime rather than
only compile its class). `webattackverify` gained 8 new checks: stun
halts both movement and firing on an otherwise-ready armed unit; stun
reapplication takes the longer duration; stun overrides an active slow;
`SpecialAttackResolver.ResolveInstant` applies Damage/Stun only to
in-range opposing targets; and `SecondaryAttackCatalog.ForMonster`
routes all three alien-tech hand families to Psionic and everything else
(including unarmed) to Ground Stomp, with Flamethrower confirmed
Damage-only. All 33 checks (25 from Phases 4-8, 8 new) pass.

## Special Attacks System Phase 10: Blood/Bones cast cost + Lab display (2026-07)

Creator direction: "Yes let's get that info into the lab, keep It
compatible with the chop shop but weapons must have a blood and bones
cost. Keep it reasonable as in follow the guidelines to challenging, but
not annoying in terms of the actual cost per unit so the user never
completely runs out of bullets or fuel as should be outlined in the
game development document." Closes the "into the Lab" item Phase 9 had
explicitly left undone, plus a new cast-cost requirement.

**Cast cost.** `SpecialAttackDefinition` gained `BloodCost`/`BonesCost`
(v0.1 ints: Flamethrower 4/2, Psionic Tractor Beam 3/1, Ground Stomp
2/4). New `RuntimeCityBuilder.SpendWalletForCast(blood, bones)` draws
down the existing session wallet on every cast, clamped at
`Mathf.Max(0, wallet - cost)` and NEVER blocking the cast itself. The
"never completely runs out" requirement turned out to already be
written down, word for word in spirit, in docs/22 SS1's "Floors, not
stalls" design contract: "A depleted resource degrades a unit; it never
disables, strands, or kills it... a player who ignores this entire
system must still have a functional army." A hard ammo gate was
considered and rejected specifically because it would let an opponent
economy-starve a caster into uselessness -- the exact death-spiral
pattern that contract forbids. Cross-referenced into docs/22 itself
(after SS11's "still design-only" closing note) so the economy doc and
the special-attacks doc don't drift out of sync on this point; also
noted there that this is a WALLET-level sink, simpler than and not yet
integrated with the (still unbuilt) onboard Brain-pool "ability casts
(1-3 each)" drain SS2's table already anticipates -- an open question
for whenever that system gets built, not resolved now.

**Into the Lab.** New `packages/genome-core/src/attacks.ts`
(`secondaryAttackFor`/`secondaryAttackForGenome`) is the TypeScript twin
of `SecondaryAttackCatalog.ForMonster` -- same two monster-side outcomes
(alien-tech hands -> Psionic Tractor Beam, everything else -> Ground
Stomp), same v0.1 cost numbers, hand-kept in sync (flagged in the
file's own header -- no automated golden test backs this particular
pairing, unlike Locomotion/Weapon/Harvest, since it's a lookup table
rather than a numeric formula). Humans/Tanks aren't genome creatures
(Flamethrower is fixed Tank.cs archetype data), so the Lab twin only
covers the two monster outcomes. Exported from the package index,
built, and copied into `site/lib/` per the project's existing vendoring
step.

Surfaced in `site/main.js` in two places, both chop-shop-safe (a
disassembled/stump hand or a freshly grafted alien hand reads correctly
with zero special-casing, since the classifier already treats anything
outside its 3-family alien set as the default): the Lab's per-creature
"vital signs" panel gained a new "Secondary Attack" section right after
the existing Parts table; the Chop Shop's own slab label gained a
compact one-line summary, so the info stays visible and correct while a
creature is mid-surgery -- literally the "keep it compatible with the
chop shop" ask. New `--bones`/`.bones` CSS convention alongside the
pre-existing `--blood`/`--fuel`/`--ichor` one, added to all three
faction skins.

Verified: `packages/genome-core`'s test suite gained
`tests/attacks.test.ts` (5 checks) -- full suite (56 tests) passes,
including the golden lineage digest (unaffected: pure additive
derived-stat module, no RNG stream or catalog change). A manual Node
smoke test confirmed the vendored `site/lib/index.js` build actually
exports and runs the new functions end-to-end, and `node --check
site/main.js` confirmed the edited Lab script parses clean.
`unity-client` side: flightcheck stub-compile clean across every
touched file; `harvestcreditverify` gained 2 new checks against the
real `RuntimeCityBuilder.cs` (deducts exactly the requested blood/bones;
an overdraw clamps at exactly 0, never negative) -- both pass.

This closes the last of the two Phase-9-era follow-ups that were code-
buildable. One remains, genuinely out of scope for code alone: the
non-Citizen consume path (designed, not built -- nothing testable exists
to build it against).

## docs/23 RTS Phase 1 review (2026-07)

Creator direction: "Review document created by fable relating to
finishing a full StarCraft like game. Phase 1 and execute phase 1."
Read docs/23 in full (including the §13 panel-review amendments) and
checked its Phase 1 acceptance criteria plus every amendment that binds
Phase 1 specifically (E: supply cap, F: Chimera three-origins predicate,
J-i: entity-ID allocation + canonical serialization, K-iv: golden-fixture
note) against the actual `packages/match-core` code, rather than assuming
the prior session's "completed" task label was still accurate.

Result: Phase 1 was already fully executed (commit 863530e, after the
panel review landed) and every amendment above was already implemented
in the original pass -- `MatchState.DefaultSupplyCap = 60`,
`PlayerState.ChimeraTrackOpen` (all three origin bits, not "two enemy
factions"), `MatchState.AllocateEntityId()` + `Command.TargetEntity`
(entity IDs, never object references), `FnvHash`'s fixed-field-order
bitwise hashing (never ToString/JSON). Re-ran both acceptance checks
live rather than trusting the commit message: `dotnet test` — 13/13
green; `Tools~/DetHarness` — 10,000-tick 8-player match, hash
`94F13654C8B8941B` printed twice, identical, matching the value already
recorded in the original Phase 1 docs/12 entry (confirms nothing has
silently drifted since).

One genuine gap found and closed: amendment K(iv)'s specific
documentation requirement ("Phase 1 must add a note that its golden
fixtures now gate sim determinism, not just display") had never actually
been written anywhere -- not in `match-core`'s README, not in
`roster-client`'s. Added a new section to `packages/roster-client/
README.md` explaining that `Locomotion.cs`/`Weapon.cs`/`Harvest.cs`'s
golden tests, originally written to keep the Lab-preview/battlefield
display in sync, now also gate `match-core` lockstep determinism once
Phase 1.5 promotes this code to the sim's source of truth -- a
regression there stops being cosmetic and starts being a desync bug.

docs/00's docs/23 row updated to reflect Phase 1 as implemented (it
previously just said "Execution plan (panel-reviewed)," with no signal
that Phase 1 had actually shipped).

No code changes to `match-core` itself -- Phase 1 needed a review and a
missing note, not new implementation. Next: Phase 1.5 ("Port the live
sim into match-core," §13 amendment A) — the plan's stated true critical
path, and NOT yet started.

## docs/23 Phase 1.5: port unit movement into match-core (2026-07)

Creator direction: "Continue to the next step" (following the Phase 1
review). Per docs/23's own corrected dependency spine (§13 amendment C:
`1 -> 1.5 -> 2 -> 3 -> ...`), the next step is Phase 1.5 -- amendment
A's "port the live sim into match-core," explicitly named the plan's
true critical path, since none of `MonsterAgent`/`UnitCombat`/`Tank`/
`Citizen`/`TrafficCar` is tick-driven or hashable today.

Amendment A actually asks for two things: (1) port the *movement +
order state machine* of one unit type into `match-core` as a
deterministic fixed-tick entity, and (2) rewrite Unity's `MonsterAgent`
to render that sim state via interpolation with ZERO
`Time.deltaTime`-driven gameplay decisions left. Did (1) in full this
pass; deliberately did NOT attempt (2).

**What shipped (match-core side):** new `SimUnit` (`Idle`/`MoveTo`
order state, double X/Z hashed bitwise, ticked by consuming a
`Speed * dt` budget across as many path nodes as it covers per tick --
never leaves fractional-tick motion on the table, matching what Unity's
FollowPath already does per-frame). `MatchState.SpawnUnit` (a direct,
setup-time call, same precedent as the existing `AllocateEntityId` --
match-start placement isn't a replayable player order). New
`CommandKind.MoveTo` -- the canonical, replayable way to ORDER a unit
(targets an entity ID per §13-J, never an object reference), resolving
a path via the SAME `HexPathfinder`/`BattlefieldState.
BlockedToGround()` citygen-core already exposes and the live Unity game
already uses, so sim pathing and Unity pathing agree by construction,
not by coincidence, once the Unity side is ported. Units are iterated
for `Tick`/`Hash` strictly in entity-ID allocation order (a parallel
dictionary gives O(1) command dispatch without touching iteration
order) -- the §0 "never object reference or hash-set order" rule is now
structural, not just a comment.

**What did NOT ship (Unity side), on purpose:** `MonsterAgent` rewritten
to render interpolated sim state only. This is a ~950-line file that
ten already-shipped phases of this session's own work (docs/26 Special
Attacks System Phases 1-10, docs/22 harvest, docs/25 steering) all
depend on, and there is no Unity Editor in this environment to visually
verify a rewrite of that size actually still plays correctly --
attempting it blind would directly violate this project's own "never
claim visual verification" rule (ground rule #4 in docs/23 itself).
Flagged in docs/23 (a new inline status note under amendment A) and
here, not silently skipped or half-attempted.

**Verified:** `dotnet test` -- 19 tests green (13 original Phase-1
tests + 6 new: spawn-idle-at-hex, MoveTo-reaches-and-goes-idle,
unreachable/unknown-entity is a silent no-op (never throws, never
creates a phantom unit), mid-path redirect recomputes from the unit's
actual current position, entity-ID-ordered iteration). `packages/
citygen-core`'s 145 tests untouched (no citygen-core changes).
`Tools~/DetHarness` now runs BOTH acceptance proofs: the original
Phase-1 10k-tick empty-match check, and a new Phase-1.5 check (100
units, scripted `MoveTo` orders over a real generated `CityPreset.
Village()`, 3,000 ticks) -- both hash identically across two runs. Note:
the Phase-1 empty-match hash value changed (was `94F13654C8B8941B`, now
`EC265E3CF8E6B74B`) because `Hash()` now includes the unit list/count --
expected and harmless (the hash only needs to be internally consistent
between two runs of the SAME code, never stable across a code version
that deliberately changed what it hashes); recorded here so nobody
mistakes it for a regression later.

Next: the Unity-side interpolated-view contract for `MonsterAgent` --
probably an incremental/parallel cutover design (not a single blind
rewrite) that the creator can actually check in their own Editor before
it's trusted, rather than another environment-blind pass.

## docs/27: sim/view migration contract, Phase A implemented (2026-07)

Creator direction: "Ok do that" (in response to the proposed next step
after docs/23 Phase 1.5's sim-side landing: "design the Unity-side
interpolated-view contract... so it can actually be checked in a real
Editor before being trusted"). Wrote docs/27-sim-view-migration-plan.md
(mirroring docs/25's proven design-doc structure/rigor) and then
implemented its first phase in the same pass.

**The design (docs/27).** The classic lockstep-with-interpolation split:
`match-core` is the sole source of truth for position/order, ticking at
its fixed 10/s; Unity holds the last TWO tick snapshots per unit and
renders `Lerp(prev, curr, alpha)` where `alpha` is "how far between the
last completed tick and the next are we right now" -- ONE shared value
per frame (not duplicated per unit, an explicit correction from an
earlier draft of the doc that would have let two units' interpolation
drift apart for no reason). Velocity is always the ACTUAL measured
render-position delta divided by dt, never a separately-computed
"intended" value -- the same trick docs/26 Phase 6's `TickCaptured`
already established for a directly-position-writing order. The cutover
is INCREMENTAL, one order kind at a time: `OrderKind.Move` first (the
only one `match-core` implements), every other order kind (Attack/Eat/
SpecialAttack/Perch) untouched, gated behind a per-unit `SimDriven` flag
that's false everywhere until a scene explicitly opts a unit in -- so
this is provably zero-risk to every currently-working scene.

**Phase A, implemented in the same pass.** New `SimBridge.cs` (owns the
`MatchState`, the fixed-timestep accumulator, the outgoing command
queue) and `SimUnitView.cs` (the interpolated view, one component per
sim-driven unit). `MonsterAgent` gained `EnableSimDriven(...)` (opt-in,
called after `Init`, not a parameter threaded through it),
`OrderMoveViaSim`, `TickMoveViaSim`, and a `SimDriven` property --
`WaypointCommander` needed ZERO changes, since both its single-unit move
call sites already funnel through `MonsterAgent`'s existing 2-arg
`OrderMove(hex, queue)` overload, which is where the sim-driven
interception was added instead -- a smaller, cleaner seam than the
design doc originally guessed, corrected there once discovered.

One real design refinement made during implementation, also corrected in
the doc: `SimBridge.Update()` is now a one-line call to a new `public
Pump(float dt)` method that takes `dt` as a parameter rather than reading
`Time.deltaTime` internally -- the same convention every
`MonsterAgent.TickX(dt)` method already follows. This wasn't just
stylistic: `Time.deltaTime` cannot be faked outside a live Unity
Editor/Player, so without this seam the fixed-timestep accumulator's own
correctness (monotonic alpha, the catch-up cap, never going negative)
would have been completely unverifiable in this environment.

**Verified:** flightcheck stub-compile clean across the whole gameplay
layer (needed one addition: a real `MadDr.MatchCore.dll`, built from the
package and copied in like every other package reference flightcheck
already uses). A standalone numeric harness (`harvestcreditverify`,
extended -- compiles the REAL `SimBridge.cs`/`SimUnitView.cs`, not
reimplementations) drove 6 new checks: the interpolation formula itself
(exact halfway lerp, Y always untouched, velocity = measured delta/dt),
zero velocity at rest across five alpha values, correct 20 m/s for a
known 2m-in-one-tick motion, the accumulator's `Alpha` strictly
increasing within a tick and always staying in [0,1], a monstrous
single-frame `dt` never hanging/throwing with the catch-up cap correctly
dropping the remainder, and a full integration check (spawn a unit over
a real generated `CityPreset.Village()`, queue a `MoveTo`, pump 400
frames, confirm the rendered snapshot lands exactly on the goal hex's
world position). 15/15 total pass; `match-core`'s own 19 tests and
citygen-core's 145 untouched.

Two real bugs caught building this harness, both fixed, both worth
recording so they don't recur: (1) the harness's own `Vector3` stub was
seeded from `flightcheck`'s copy -- a pure compile-check harness whose
`Vector3` operators all return `default(Vector3)`, correct there since
nothing ever inspects the value, silently wrong here since this harness
asserts on real interpolation math. Patched to real math, matching
`webattackverify`'s already-correct copy -- the THIRD time this exact
"flightcheck's dummy stub leaks into a harness that actually needs real
math" class of bug has been caught this session (previously: `Mathf`,
twice). (2) the first draft of the integration test looped "pump until
the unit's order reads Idle" -- which exits on the very first check
without pumping even once, because a freshly-spawned unit already reads
Idle before its queued command has even been applied. Fixed to a
fixed-budget pump instead (guaranteed to cover the scenario), with a
comment recorded in the harness so nobody reintroduces that exact
loop-condition bug.

**Explicitly not claimed:** that a unit visibly moves smoothly on
screen. Nothing in this pass touches a live scene -- `SimDriven` stays
false everywhere until a dev/test scene explicitly calls
`EnableSimDriven`, which none does yet. The creator's own Editor check is
the real, still-outstanding gate before docs/27's Phase B (queued/
grouped moves) is attempted.

## docs/27 follow-up: the actual Editor smoke-test toggle (2026-07)

Creator direction: "what am I lookig for?" -- a fair question, since
Phase A shipped fully opt-in with nothing in any scene actually calling
`EnableSimDriven`. There was genuinely nothing to look at yet.

Added `RuntimeCityBuilder.simDrivenDemo` (default off, so every existing
scene is byte-for-byte unaffected) and a `SimBridge` field. When on,
`HandleRosterReady`'s existing spawn loop additionally calls
`EnableSimDriven` on the FIRST monster the roster spawns, right after
its normal `Init(...)` -- nothing else about it changes, every other
monster is untouched. This reuses the exact existing dev workflow this
class's own header comment already documents ("Hit Play: left-click
your monster, right-click the world") rather than inventing a separate
demo scene: check the box, hit Play, click that one monster, right-click
to move it.

What to actually watch for, spelled out since this is a genuine first
live run of code that's only been numerically verified so far: the unit
should walk SMOOTHLY (interpolation working) rather than jitter/stutter
(would indicate something else is still writing its position); a small
(~100ms, one tick) delay before it starts moving after the click is
CORRECT lockstep input latency per docs/27 SS5, not a bug; footstep
animation should look completely normal throughout (proves MonsterBody
genuinely doesn't care where its velocity came from -- the whole point
of the interpolation boundary docs/25 established and this plan reuses);
and every OTHER monster in the scene should behave completely normally
(proves the opt-in boundary actually holds in practice, not just in the
numeric harness).

Verified: flightcheck stub-compile clean across the whole gameplay layer
including the modified RuntimeCityBuilder.cs. This is still code-only --
the actual "does it look right" answer is the creator's own Editor
session, which is what this toggle exists to make possible at all.

## docs/27 Phase B: single-unit waypoint queueing (2026-07)

Following the creator's Editor confirmation that Phase A works ("looks
like it works, go on to the next phase."), implemented docs/27 §7 Phase
B — but narrowed its scope from the doc's original text ("waypoint list +
group token") to single-unit waypoint queueing only. Group moves
(multi-unit formations, settle points, the shared `GroupFacing` token
`WaypointCommander.AssignFormation` uses) are explicitly deferred:
`match-core` has no representation of a token shared across N units yet,
and building that deserves its own design pass rather than being folded
silently into this one. `AssignFormation`'s 4-arg `OrderMove` call site
is not intercepted by `SimDriven` and keeps using the legacy path
regardless of the toggle — the same documented boundary Phase A already
drew for queued/grouped moves, just not yet moved for the group half.

What shipped: `match-core` gained `CommandKind.MoveQueue` (APPEND —
starts immediately if Idle, else enqueues behind what's in flight);
`SimUnit` gained a `Queue<HexCoord>` waypoint queue, included in the
canonical hash (`Queue<T>` enumerates in insertion order, a documented
guarantee, so hashing it is safe); `MatchState.Tick` advances to the next
queued waypoint in the SAME tick a leg completes, so a multi-waypoint
walk never idles between legs; `SimBridge.QueueWaypointCommand` is the
Unity-facing twin of `QueueMoveCommand`; `MonsterAgent.OrderMoveViaSim`
widened to handle `queue == true`. `WaypointCommander` needed zero
changes again — both single-unit call sites already funnel through the
2-arg `OrderMove(hex, queue)` overload where all sim-driven interception
lives.

Verification: match-core gained 4 tests (23/23 total) including a
same-seed/same-queued-orders hash-determinism check; `harvestcreditverify`
(compiling the real `SimBridge.cs`) gained an integration check driving
`QueueMoveCommand` + `QueueWaypointCommand` through a real generated city
and confirming the view's tick snapshots pass through waypoint A en route
to B (16/16 pass); `flightcheck` still compiles clean. As with Phase A,
`MonsterAgent.EnableSimDriven`/`OrderMove` itself can't be exercised in
either stub harness (`Component.gameObject` reads `null` there), so only
`SimBridge`'s own API surface is exercised directly — verification of the
real `MonsterAgent` path stops at compilation, same precedent as Phase A.

Not yet done: an Editor smoke test for queued (shift-click) moves
specifically. The creator's existing Phase A confirmation only exercised
a plain single-destination move; whether to add a queued-move check to
`simDrivenDemo` now or defer it to Phase C is still open.

## docs/27 Phase C: sim-side separation, scoped to close the accepted gap (2026-07)

Following "Looks good continue, make sure the in place navigation rules,
avoidance systems, are not changed" -- implemented docs/27 §7 Phase C,
narrowed to separation only (docs/23 §5 also specifies alignment and
cohesion; both need an "order group" concept match-core still doesn't
have, the same reason Phase B deferred group tokens -- flagged, not
guessed at).

This closes a real, previously-accepted gap: docs/27 §5 documented that
`ApplySeparation` (Unity's hard per-frame position correction) had no
sim-side equivalent and had to be skipped for sim-driven units -- but the
actual code never implemented that skip. `MonsterAgent.Update()`'s
`ApplySeparation` call ran unconditionally regardless of `SimDriven`,
meaning a sim-driven unit's `transform.position` was being overwritten by
two disagreeing writers every frame (the hard correction, then
`SimUnitView.Advance`'s interpolated render clobbering it again next
frame) -- a discrepancy the docs claimed was handled but wasn't. Found by
re-reading docs/27 §5 against the actual `Update()` call site before
starting this phase.

What shipped: `match-core` gained `Flocking.cs` (`Flocking.Separate`,
pure static math, identical formula to
`MonsterSteeringController.SeparationForce` -- same cumulative-push
idiom, same "half the overlap" push, weight 1.0 per docs/23 §5's table).
`SimUnit` gained a `Radius` (fixed at spawn). `MatchState.Tick` runs a
new `ApplySeparationPass` every tick across every unit regardless of
Order, entity-ID order, rejecting any nudge that would land a unit in a
blocked/off-map hex (docs/23 §5's "blocked-hex clamp never violated"
bar). `MatchState.SpawnUnit`/`SimBridge.SpawnUnit` both gained an
optional `radius` param defaulting to 1.5 (matching Unity's own
`UnitCombat.Radius` default) so every existing call site keeps compiling
unchanged. `MonsterAgent.EnableSimDriven` now sources the real radius
from `_fighter.Radius` (already meaningful by the time it's called, set
by `Init`'s own `Configure`).

The one Unity-side behavior change: `MonsterAgent.Update()`'s existing
unconditional `_builder.ApplySeparation(_fighter)` call gained one more
condition, `&& !SimDriven`. Verified this is the ONLY change to any
existing navigation/avoidance code: `git diff --stat` after this phase
shows only `packages/match-core/src/{MatchState,SimUnit}.cs` (both
additive) and `unity-client/Assets/Scripts/{MonsterAgent,SimBridge}.cs`
touched; `MonsterSteeringController.cs`, `RuntimeCityBuilder.
ApplySeparation`'s own body, `Tank.cs`, `HexPathfinder`, and
`BattlefieldState` are byte-for-byte untouched. Since `SimDriven` is
false for every unit in every real scene today (the toggle stays
opt-in, off by default), the new condition is currently a no-op for all
actual gameplay -- legacy (non-sim-driven) separation/steering behaves
exactly as before.

Verification: match-core gained `FlockingTests.cs` (7 tests, 30/30
total) covering the pure math, a convergence proof (two overlapping
units approach but never overshoot their combined radius+spacing), a
10,000-check stress test (20 units x 500 ticks) proving the blocked-hex
clamp holds, a widely-spaced-units-undisturbed sanity check, and a
hash-determinism re-proof with separation engaged. `harvestcreditverify`
gained an integration check through the real `SimBridge.SpawnUnit`
radius parameter (17/17 pass). `flightcheck` still compiles clean.

Not yet done: no live scene spawns more than one sim-driven unit, so
there's nothing to visually confirm yet for multi-unit separation (or
Phase B's queued moves, still outstanding from before). Alignment and
cohesion remain unimplemented, waiting on the same "order group" design
pass Phase B's own deferred group-move half also needs.

## docs/23 Phase 2: bases & building roster, match-core sim-side slice (2026-07)

Following "on to the next phase" after docs/27 Phase C -- the master
build plan's own dependency spine (§13 amendment C: 1 -> 1.5 -> 2 -> ...)
makes Phase 2 (bases & the building roster) the unambiguous next step,
distinct from docs/27's own remaining Phase D (which is gated on combat/
citizen/ability systems that haven't landed sim-side yet, so isn't
actually startable right now).

Scoped the same way Phase 1.5 was: match-core (sim-side) first, Unity
(`BaseDresser`, build-menu IMGUI, ghost-placement cursor) deferred as a
separate follow-up design pass, not attempted blind.

Two real open questions surfaced and are recorded here rather than
resolved unilaterally:

1. **docs/22 vs docs/23 §2 reconciliation.** docs/22 §6 defines a
   specific storage set (Blood Bank/Bone Pile/Brain Trust, real costs)
   from an earlier design pass; docs/23 §2's later, per-faction-skinned
   roster table (Blood storage/Fuel pump/Fuel storage/Parts storage/
   Harvest post/Factory/Defense) doesn't map 1:1 onto it and gives no
   cost numbers of its own for most rows. Implemented docs/23 §2's fuller
   roster (it's the newer, more complete spec, explicitly says "reuses
   docs/22 storage"), reusing docs/22's real Blood Bank numbers (20
   Bones + 10 Blood, +100 cap) verbatim for `BloodStorage` -- the one
   clean 1:1 mapping -- and clearly-flagged v0.1 placeholder numbers for
   every other buildable kind (`FuelPump`/`FuelStorage`/`PartsStorage`/
   `HarvestPost`/`Factory`/`Defense`), matching this project's own
   standing policy (CLAUDE.md: "v0.1 economy/upkeep numbers everywhere
   are placeholders; real balance is a Phase-2 sandbox pass"). HP/Armor
   for every kind reuse docs/18 §3's real structure tiers (Small 300/2,
   Medium 600/4, Large 1500/6, Landmark 3000/8) rather than invented
   numbers.

2. **Which resource does `BloodStorage`'s cap bonus target per faction?**
   docs/23 §2's table function column reads "Raises Blood/Ichor cap" for
   the Blood-storage roster slot, while a *separate* Fuel-storage slot
   exists for Fuel specifically -- ambiguous whether a Human Army
   player's "Plasma Reserve" skin should raise Blood (literally, as
   named) or generalize to that faction's own energy resource. NOT
   resolved here: `StorageCapBonus` is stored as inert DATA
   (`(ResourceKind.Blood, 100)` literally) and nothing in Phase 2 reads
   or enforces it -- docs/23 §3's own task list puts "storage caps from
   buildings" under Phase 3, not Phase 2, so this is correctly Phase 3's
   call to make when it actually wires cap enforcement, not a gap in
   this phase.

What shipped: `BuildingKind` (8-slot roster) + `BuildingDef` (static
per-kind cost/build-time/HP/armor/cap-bonus data) + `SimBuilding`
(entity-ID-order lifecycle: UnderConstruction -> Complete, `IsDamaged`
as a pure HP-threshold function per docs/18 §3 rather than its own
state, `ApplyDamage` -> Destroyed reopens the hex). `CommandKind.
BuildStructure` repurposes the `TargetEntity` slot to carry the
building kind (no existing entity to target) -- explicitly the
documented "generic arg slots are interpreted per Kind" contract
`Command.cs`'s own header already established, not a new liberty.
`MatchState.SpawnHqForPlayer` is a setup-time API (mirrors `SpawnUnit`):
the HQ is generator-placed, Complete immediately, never a
`BuildStructure` target. `ApplyBuildStructure` validates an on-map,
unblocked hex and full multi-resource affordability BEFORE debiting
anything (all-or-nothing, never a partial spend on failure). A known,
flagged (not silent) gap: a unit's already-computed path isn't
invalidated when a building newly blocks a hex mid-path -- match-core
has no reactive "city changed, recompute" pass at all yet, extending an
existing Phase 1.5 limitation rather than introducing a new one.

Verified: `packages/match-core/Tests~/BuildingTests.cs`, 12 new tests
(42 match-core total) covering placement legality (occupied/off-map
hexes rejected), exact cost debit, unaffordable builds as a true no-op
(wallet untouched), construction completing at the exact tick boundary,
destruction reopening the hex for rebuilding, `IsDamaged`'s threshold,
hash-determinism with buildings+units mixed, and the literal docs/23 §2
acceptance bar itself (every buildable kind built from one scripted
command list, deterministic across two runs). citygen-core's 145 tests
and the 30 pre-existing match-core tests remain untouched.
`harvestcreditverify` (17 checks) and `flightcheck` still compile/pass
against the refreshed DLL -- no Unity-side files changed this phase at
all, by design.

Not yet done: `BaseDresser.cs`, build-menu IMGUI panel, ghost-placement
cursor (Unity-side, no design doc written yet -- deferred the same way
docs/27 was split out from Phase 1.5's Unity half); real balance-tuned
costs for 6 of 7 buildable kinds; multi-hex building footprints; wallet-
cap enforcement (Phase 3's own stated job).

## Bug fix: flying monsters couldn't land on A-frame/stacked high-rise roofs (2026-07)

Creator report: "Flying monster should be able to land on any surface,
including building features like A-frame roofs and stacked roofs on
high-rises. You'll probably have to reclassify those features as solid.
Do not alter the smaller features."

Root cause: `RuntimeCityBuilder.SurfaceHeightAt`/`BuildingHeight`/
`EnsureRoofCache` (the flyer landing-height pipeline `MonsterAgent.
GoIdle`/`TickPerch` reads) keyed purely off `HeightForTier`, a flat
4-value table (Small/Medium/Large/Landmark = 6/12/30/40m) matching only
the tier-colored massing cube. `BuildingDresser` (added later, docs/21
Phase 3) draws real, contiguous, colliderless roof geometry ON TOP of
that cube -- the suburban house's A-frame/gable roof (`DressSmall` case
0, Small tier, picked per-hex by a private hash) and the unconditional
stacked deco setback every Large-tier "high-rise" gets (`DressOffice`)
-- but nothing fed either shape's real height back into the landing
pipeline. A perch settled the creature at the flat tier height, below/
inside that geometry.

Fix: `RuntimeCityBuilder` gained `_roofHeightOverrides` (a per-hex
Dictionary<HexCoord,float>) and `RegisterRoofLandingHeight(hex, height)`
(keeps the max ever registered for a hex). `EnsureRoofCache` and
`BuildingHeight` both now take `Max(flatTierHeight, override)`.
`BuildingDresser.DressSmall`'s gable case calls
`RegisterRoofLandingHeight(hex, height + GableApexOffset)` (0.6 base
lift + 5.5*sqrt(2) for the 45-degree-rotated 11x11 square's half-
diagonal -- derived from the actual mesh numbers, not guessed);
`DressOffice` calls it unconditionally for every footprint hex with
`height + SetbackTopOffset` (6.5+1.5 = 8, the real top-tier box's
height). A since-destroyed building's hex is naturally excluded by
`EnsureRoofCache`'s existing `BlocksMovement` check, same as the flat-
tier case, so registered overrides for a rubbled building are correctly
ignored.

Explicitly NOT touched, per the creator's own instruction: the "rooftop
kit" (water towers, vents, antenna masts, chimneys, signage --
`BuildingDresser.Rooftop()` and the other small per-archetype props) and
the Landmark archetype's own similarly-shaped tapering stacked boxes
(church spire etc.) -- the creator named only A-frame roofs and
stacked/high-rise roofs, and Landmark set pieces weren't mentioned, so
they're out of scope here (flagged for a future ask, not silently
extended to).

Verified: flightcheck (whole gameplay layer, both changed files compiled
directly) still compiles clean. `harvestcreditverify` gained 2 new
checks (19/19 total) -- `RegisterRoofLandingHeight` correctly keeps the
taller of any two registrations for a hex, and `BuildingHeight` reports
the override across a multi-hex footprint. Since the actual bug is
purely visual/gameplay-feel (does a flyer's landing spot look right on
screen), the real confirmation is the creator's own Editor check, same
discipline as every other Unity-side change this session -- not yet
run.

## Follow-up: roof "parking" / distribution rules (2026-07)

Creator follow-up to the roof-landing height fix above: "Same parking,
distributions rules should apply to roof features. If there is not
enough space for the monster(s) it should pick a different roof nearby
before landing."

Before this, ordering multiple selected flyers onto the same building
(already possible -- `WaypointCommander.HandleOrders`' roof-click branch
looped every selected flyer onto `OrderPerch(building)` independently)
had ZERO distribution logic: each unit computed its own "nearest
footprint hex to wherever I currently am" and, since a selected group
usually starts clustered together, most/all of them converged on
approximately the same point and stacked/overlapped. Ground orders
already solve exactly this (`RuntimeCityBuilder.FormationHexes` --
literally commented "one parking slot per unit" -- + `WaypointCommander.
AssignFormation`/`RingTarget` for the settle-phase ring spread), but
nothing analogous existed for rooftops.

What shipped: `RuntimeCityBuilder.RoofCapacity(building)` (one perch
slot per footprint hex -- a roof has no open neighbourhood to spread
into the way `FormationHexes` searches outward from a ground point; it
IS the footprint). `AvailableRoofSlots(building)` reads live occupancy
off every spawned monster's new public `MonsterAgent.PerchedOn`
property (`_targetBuilding`, which survives `GoIdle()` -- only a NEW
order clears it) rather than a separate counter that could drift.
`FindNearbyPerchableBuilding(preferred, neededSlots, exclude)` scans
`_battlefield.Buildings` for the nearest OTHER standing building with
enough free room, skipping any already tried -- same reject-then-rank
shape `DeadlockManager.PickSidestepHex` uses for ground traffic
unblocking, applied here to whole buildings instead of single hex
neighbours.

`WaypointCommander.AssignPerch` ties it together: nearest-unit-to-
nearest-free-slot greedy assignment on the clicked building (same
algorithm shape as `AssignFormation`), and whatever doesn't fit rolls
over to the nearest nearby building with room, repeating outward until
every unit has a spot or nothing nearby has space -- at which point the
leftover units perch on the originally-clicked roof anyway rather than
being left with no order (the same "pad rather than fail" call
`FormationHexes` already makes). `MonsterAgent.OrderPerch` gained an
optional `targetHex` parameter and a new `_perchTargetHex` field so
`TickPerch` lands each unit on its SPECIFIC assigned hex instead of
independently recomputing "nearest" and converging with its neighbors.

Deliberately NOT built (flagged, not silently skipped): a golden-angle
ring micro-spacing WITHIN a single footprint hex (ground's
`RingTarget`/`groupSpacing` equivalent) -- footprint hexes are already
~20m apart, which reads as reasonably distributed at RTS camera height
without needing sub-hex ring placement; multi-hop alternate-building
search (only one "look elsewhere" hop is attempted, not a chain of
several); and a live Editor demo -- same as every other change this
session, the actual "does a flock spread out and overflow correctly on
screen" confirmation is the creator's own Editor check, not yet run.

Verified: flightcheck (all three changed files: RuntimeCityBuilder.cs,
MonsterAgent.cs, WaypointCommander.cs) compiles clean.
`harvestcreditverify` gained 2 new isolated checks (21/21 total):
`RoofCapacity` matches footprint hex count; `AvailableRoofSlots` returns
every footprint hex when nobody's perched yet. Honestly flagged limit:
the actual OCCUPANCY-EXCLUSION path (a monster genuinely perched on one
of the hexes) depends on `MonsterAgent.Perched`'s own
`SurfaceHeightAt`/`_battlefield` dependency, which this stub harness has
no way to stand up without a live city -- that half is verified by
compilation and code review only, not exercised by a runnable check,
same boundary the roof-height fix's own harness check already hit.

## docs/23 Phase 3: wallet caps from storage buildings (2026-07)

Following "continue to next phase of the full game" after Phase 2 --
the master build plan's dependency spine (§13 amendment C: 1 -> 1.5 ->
2 -> 3 -> ...) makes Phase 3 (the economy) the next step. Read the
full Phase 3 task list before starting and found every item except
wallet-cap enforcement is genuinely gated on a prerequisite that
doesn't exist yet, not just unstarted busywork:

- **Income ticks from Collection Stations** need Citizens as sim
  entities -- docs/20's own yield model is citizen-death-driven ("the
  Collection Station is what citizen deaths nearby, this match,
  produce"), not a flat per-tick number, and Citizens aren't sim
  entities in match-core (the identical gap docs/27 already flagged
  for the `EatCitizen` order kind).
- **Income from Fuel Depots** needs the `CityModel.FuelNodes` generator
  task from this same section -- a Fuel Depot's whole defined role is
  "MUST sit on a fuel-node hex," so there's no honest way to gate its
  income without that generator work landing first.
- **Upkeep drains** need genome-linked per-unit cost data that has no
  path into `match-core.SimUnit` at all today -- docs/05's real upkeep
  table (Blood upkeep 10/25/18 per minute for the three sample
  archetypes) is computed per-creature in `packages/roster-client`,
  never wired into `SpawnUnit`. Inventing a parallel, ungrounded number
  here would create exactly the "two sources of truth that drift"
  problem this project avoids elsewhere.
- **Onboard per-unit pools** need `Harvest.cs`'s capacity/spill logic
  promoted to sim-side -- a separate, real port in its own right.

Only "storage caps from buildings" was actually ready: `BuildingDef.
StorageCapBonus` (stored as inert data since Phase 2) now applies for
real. `PlayerState` gained a per-resource `_walletCap` array (defaults
to `int.MaxValue` -- uncapped -- for every resource) and
`RaiseWalletCap` (raise-only, matching `RaiseSupplyCap`'s own existing
raise-only shape; no `LowerSupplyCap` exists either). `MatchState.Tick`
detects the UnderConstruction -> Complete transition (not "every tick
it's Complete") and applies the building's `StorageCapBonus` exactly
once. `Grant` clamps at the cap via a "room to the cap" calculation
(`wallet += Min(amount, Max(0, cap - wallet))`), NOT
`Min(wallet+amount, cap)` -- the latter would actively DECREASE an
already-over-cap wallet the next time anything is granted, silently
confiscating resources nobody spent. Caught and fixed while writing
the first test for it, before it shipped.

The base cap itself (before any storage exists) is left at
`int.MaxValue` -- an honest non-guess, not an oversight: docs/22 §6
says "base caps come from the Vat" but never gives that base a real
number, flagging it as its own open question (**Q28**, still
unresolved), so match-core doesn't invent one either. Docs/22 §6's Q28
also explicitly leaves open "whether caps apply retroactively when
storage dies" -- so destroying a cap-raising building does NOT lower
the cap back down in this pass; that's a real, separate design
decision for whoever resolves Q28, not decided here.

Verified: `packages/match-core/Tests~/EconomyTests.cs`, 9 new tests
(51 match-core total) covering: cap defaults to uncapped; the first
raise sets the cap exactly (not `int.MaxValue + amount`, which would
overflow and wrap negative) while later raises accumulate normally;
`Grant` clamps at the cap; `Grant` never retroactively confiscates an
existing over-cap balance (the bug caught above); `Clone()` copies the
cap array; a `BloodStorage` completing raises exactly docs/22's real
100 (not before, not re-applied on later ticks); a `FuelStorage`
completing raises Fuel only, never Blood; a `Factory` (no
`StorageCapBonus`) completing raises nothing at all; and hash-
determinism with a cap-raising build plus an over-cap `Grant` in the
same run. citygen-core's 145 tests and every one of the 42
pre-existing match-core tests remain untouched -- the 9 new
`EconomyTests.cs` cases are the only additions (42 + 9 = 51).
`harvestcreditverify` (21 checks) and `flightcheck` still pass/compile
against the refreshed DLL -- no Unity-side files touched this phase,
same as Phase 2.

Not yet done, explicitly deferred (see docs/23 §3's own updated status
note): income ticks (both sources), upkeep drains, onboard per-unit
pools, the `FuelNodes` generator, and the whole Unity half (gas-station
dressing, wallet/cap HUD line).

## docs/23 Phase 3.5: emitters + the Lumen mana currency (2026-07)

Following "continue" after Phase 3 -- docs/23 §13 amendment C's corrected
dependency spine explicitly inserts Phase 3.5 (amendment B) here, before
Phase 4/6, because Phase 6's damage formula depends on `emitterMod` and
the anti-turtle Dominion victory condition needs live emitter ownership.

Unlike Phase 3's economy tasks (mostly gated on missing prerequisites),
docs/03-mana-system.md is a fully v0.1-spec'd document with real numbers
already: Lumen Cycle durations (90/30/90/30s), the polarity/phase output
table, an 8s capture channel with contested-pause, a 100 mana cap. Ported
these directly rather than inventing anything.

What shipped: citygen-core gained `EmitterPolarity` (Solar/Lunar/
Twilight) and `Landmark.Polarity`, assigned round-robin at generation
time using the existing per-emitter `emitterIndex` counter -- "roughly
balanced mix" (docs/03) satisfied trivially since round-robin can never
differ by more than 1 between any two polarities. match-core gained
`LumenPhase`/`LumenClock` (deliberately NOT stateful -- the current
phase is a pure function of `MatchState.Frame`, nothing to keep in sync
or drift), `SimEmitter` (capture is automatic, reading live unit
positions every tick -- there is no capture Command, matching docs/03's
own "captured by a monster standing on the hex" framing), and
`PlayerState.Mana` (a currency deliberately DISJOINT from the
`ResourceKind` wallet array, per docs/03's own "Mana is energy...
Components are material" framing -- never a seventh `ResourceKind`).
Mana income grants once per simulated second (docs/03's table is already
whole mana/second, so this is exact, not a fractional approximation).

**A real gap discovered while writing the capture tests, not introduced
by this phase:** a `Landmark`'s own `Site` hex is part of its
Landmark-tier building's footprint, which `BattlefieldState` blocks to
ground movement -- confirmed by direct inspection (every generated
emitter's site hex reads `blocked=True`). This means a GROUND unit can
never actually walk onto the exact hex docs/03's capture rule names, for
ANY landmark, today -- this predates Phase 3.5 entirely (it's a Phase 1
city-generation decision from the original "landmark = a Landmark-tier
building occupying the site + its footprint" design) and was only
surfaced now because emitter capture is the first mechanic that actually
needs a unit standing on that specific hex. Flying units aren't excluded
by the ground-blocked set, so the mechanic isn't fully inert, but a real
design decision is needed (open the site hex itself back up, capture
from an adjacent hex instead, or something else) before ground armies
can capture emitters in a real match. NOT resolved here -- flagged for
whoever picks it up, the same discipline as every other open question in
this log. (Practical effect on this phase's own tests: two capture tests
that needed to move a unit AWAY from an emitter hex found that
`HexPathfinder.FindPath` returns null starting from that blocked hex, so
they reposition the test unit directly via reflection instead of issuing
a real `MoveTo` -- a test-only workaround for this same gap, documented
inline in `EmitterTests.cs`.)

Explicitly deferred, not faked: unit affinity (solar/lunar/neutral) and
the `emitterMod` attack/speed modifiers docs/03 defines for it -- needs
genome-linked per-unit data in `SimUnit` AND a combat formula, neither
exists yet (Phase 4's job per the corrected dependency spine); the
Dominion victory timer -- needs a match end-condition system that
doesn't exist at all; the Unity-side moon-dial HUD, capture progress
bar, and mana display (today's only emitter visualization is the
existing `BuildLandmarkAuras` aura rings, from an earlier batch).

Verified: `packages/citygen-core/Tests~/CityGeneratorTests.cs` gained 7
new tests (152 total) for polarity assignment (emitter-only, roughly
balanced, deterministic). `packages/match-core/Tests~/EmitterTests.cs`
gained 33 new tests (84 match-core total) covering: exact Lumen phase
boundaries at every tick threshold including the cycle wraparound;
emitter seeding count; capture progressing while uncontested, staying
frozen (not reset) while contested, resetting when abandoned, flipping
ownership at exactly the 80th tick, and recapture by a different player;
mana income matching docs/03's table exactly for every polarity/phase
combination, the 100-cap overflow-loss rule, and unowned emitters
granting nothing; and hash-determinism with emitters/capture/mana all in
play. citygen-core's other 145 pre-existing tests and match-core's other
51 remain untouched. `harvestcreditverify` (21 checks) and `flightcheck`
still pass/compile against the refreshed DLL -- no Unity-side files
touched this phase, same as Phase 3.

## docs/23 Phase 4 (combat core slice): damage formula, arcs, death/salvage (2026-07)

Following "continue" after Phase 3.5 -- docs/23 §13 amendment C moves
the core combat loop (damage formula + arcs + death/salvage event) out
of Phase 6 into Phase 4, since Phase 4's own XP-on-kill needs combat to
exist first, and Phase 6 consumes it either way. Like docs/03, docs/04
is a fully v0.1-spec'd document with a real formula and real worked
examples -- ported directly.

The recurring "genome data has no path into match-core" gap (hit for
Phase 3's upkeep, and again for Phase 3.5's affinity) didn't block this
phase: `CombatStats` (Vitality/Power/Armor/Reach/Ferocity/Cunning/
Affinity) is an optional `SpawnUnit` parameter, the exact same "accept
the genome-derived NUMBER as a spawn parameter, match-core never touches
the genome itself" pattern already proven for Speed/Radius (docs/27
Phase C). This sidesteps the gap entirely rather than needing to solve
it -- the caller (Unity's `packages/roster-client` `Combat.Profile`,
already computing these from a genome) supplies the whole stat block.

What shipped: `CombatMath` (pure, integer-percent damage formula --
docs/04's own "determinism requirements" section explicitly calls for
integer/fixed-point math here, stricter than the IEEE-double position
math used elsewhere in match-core, since multipliers are "exact
hundredths by design"). `CommandKind.AttackUnit` + `UnitOrderKind.
AttackUnit`. posMod reuses `Facing.ArcOf` -- ALREADY EXISTING in
`packages/citygen-core`, untouched by this phase, confirming the arc
math was ready and waiting. emitterMod reuses Phase 3.5's aura
infrastructure (`Landmark.EmitterAuraRadiusHexes`, `_emitters`) plus a
new per-unit `UnitAffinity` (Solar/Lunar/Neutral), implementing docs/03's
full affinity/phase/aura table exactly (auras don't stack, so "is this
position within ANY aura" is the whole boolean needed -- the modifier
never depends on WHICH aura, unlike mana income which does). luckRoll/
crit reuse the existing seeded `SimRng`. Death sets `DeathTick` and
exposes a 150-tick (15s) `IsSalvageable` window as sim state.

A bug caught while writing the first integration test, not shipped:
`ApplyAttackUnit` originally didn't check the DEFENDER also has
`CombatStats` -- attacking a pure-movement unit (Combat == null) would
have thrown reaching for `defender.Combat!.Value.Armor` in `TickCombat`.
Fixed by requiring both attacker AND defender to have combat stats
before an attack order is even accepted, matching the "a unit with no
combat stats isn't a combatant at all" framing already used for
`IsAlive`.

Explicitly deferred, not faked (see `SimUnit.cs`/`CombatStats.cs`'s own
header comments): **ranged (Reach>=2) posMod** -- `Facing.ArcOf` requires
exact hex adjacency and throws otherwise, so a Reach>=2 attacker gets a
flat front-equivalent 100 today rather than the real "still have arcs,
just not gated by adjacency" geometry docs/04 describes; widening
`ArcOf` (or adding a distance-tolerant sibling) is the real fix. **Real
turn-time-gated facing** -- docs/04's `turnTime = 0.15s x sizeClass` per
hex-edge ("this is what makes flanking real") needs a sizeClass stat no
unit carries yet; facing here snaps instantly to whatever a unit last
attacked instead of turning over time, a documented simplification that
still preserves the core "flanking a distracted defender" mechanic even
without the time cost. **Chase-to-attack-range movement** -- `AttackUnit`
is a silent no-op if attacker and target aren't already within Reach; no
auto-approach exists yet (a real, separate feature: combine the existing
MoveTo pathing with re-checking range each tick). **Actual salvage
resource payout and the harvest/looting command itself** -- `IsSalvageable`
is sim state only; docs/04's "harvesting a corpse is a 3-second channel"
action and the 40-60% component drop need genome-linked construction-bill
data with no path into match-core yet, the same category of gap as
docs/12's Phase 3 upkeep entry. **All of Phase 4's own remaining tasks**
(XP/Level/Traits/Gear/Fusion) -- correctly sequenced after combat core,
since XP is earned from kills that couldn't be resolved before now.

Verified: `packages/match-core/Tests~/CombatTests.cs`, 24 new tests (108
match-core total) covering: `CombatMath.ResolveDamage` matching docs/04's
own worked examples EXACTLY (Power22/Armor3 vs Power20/Armor3 front-on ->
19/17; the aura-boosted retreat example -> 22); the full posMod and
emitterMod tables; luckRoll's uniform band and crit edge cases (0%/100%
cunning); out-of-range and non-combatant `AttackUnit` orders as silent
no-ops; an adjacent fight resolving to death and the salvage window
opening/closing at the exact tick boundaries; Ferocity gating attack rate
to the exact tick (a 0.5/s attacker's second hit lands on exactly the
20th tick, not the 19th or 21st); a ranged (Reach 3) non-adjacent attack
resolving without `Facing.ArcOf` throwing; and hash-determinism with
combat in play. citygen-core's 152 tests and match-core's other 84
pre-existing tests remain untouched. `harvestcreditverify` (21 checks)
and `flightcheck` still pass/compile against the refreshed DLL -- no
Unity-side files touched this phase, same as Phase 3/3.5.

## docs/23 Phase 4 (RPG layer): XP, levels, per-level stat bonuses (2026-07)

Following "execute the next phase" -- with Phase 4's combat-core prerequisite
done, its own listed RPG tasks (XP/Level/Traits/Gear/Fusion) became
buildable. Scoped this pass to XP/Level only, the one piece with a fully
real, numeric v0.1 spec and no unresolved content gap.

What shipped: `UnitLeveling` (kill XP = 40 + 4xvictim level; the 10-entry
cumulative XP threshold table; a linear, not compounding, per-level stat
multiplier -- read from docs/23's "+8% MaxHP... per level" phrasing as
additive percentage points, a documented interpretation choice since the
doc doesn't disambiguate linear vs. compounding). `SimUnit` gained
`XP`/`Level` (Level is a pure function of XP, never stored independently)
and `EffectiveMaxVitality`/`EffectivePower`/`EffectiveSpeed`, applying the
bonus on top of the untouched genome-derived base stats -- `TickCombat`
already uses `EffectivePower` for damage, `Tick`'s movement math already
uses `EffectiveSpeed`. Kill XP credits the attacker's `GrantXp` the
instant `TickCombat` detects a kill.

Level-up preserves "missing HP" rather than granting a full heal
(current Vitality rises by the exact same delta the effective max just
did) -- a documented interpretation choice, since docs/23 doesn't specify
either way.

A bug caught while writing the golden multi-kill test, not shipped: the
first draft used the `Fighter()` helper's default Ferocity (1.0 =
1s/10-tick cooldown) for a unit meant to score six kills in six single-
`Tick()` calls -- only the FIRST kill ever resolved, since the attacker
was still on cooldown from it during every subsequent spot's single tick,
silently failing to reproduce the intended scenario. Fixed by giving that
specific test's attacker a much higher Ferocity (100/s) so each tick has
time to both apply the command and resolve the attack.

Explicitly deferred, not faked (see `UnitLeveling.cs`'s own header and
docs/23 §4's updated status note for the full reasoning): **assist XP**
-- docs/23 never specifies the assist-tracking window (how recent a hit
counts, how many attackers can share credit), a real content/design gap;
**building-destruction XP** -- match-core has no `AttackBuilding` command/
order kind yet, so there's no attacker to credit when
`SimBuilding.ApplyBuildingDamage` (a generic, not-attacker-tied hook)
fires; **Trait choices at levels 3/6/9** -- docs/23 names only 3 of the
9 required traits (Thick Hide/Adrenal Rush/Scavenger's Eye); inventing
the other 6 would be fabricating game content, not translating given
numbers -- a genuinely different kind of gap than every other deferral
logged so far, flagged rather than guessed at; **Gear (grafted salvage)**
-- needs the salvage/harvest system already deferred in the combat-core
entry above; **Fusion** -- the stat-derivation math itself (HP=sum*0.85,
level=max+1, Power/Ferocity=max*1.1, Speed=min, upkeep=sum) is pure and
computable, but the render side (dominant-parent genome, four-hand-part
creature-mesh rig) is Unity/genome-core territory, and Fusion also needs
a per-unit Bones-cost stat match-core doesn't track anywhere -- a
separate, bigger slice.

Verified: `packages/match-core/Tests~/LevelingTests.cs`, 19 new tests
(127 match-core total) covering: `KillXp`/`LevelForXp` matching docs/23's
own numbers exactly (including the table's 10th entry never actually
triggering a level-up past the level-10 cap); the linear stat multiplier;
effective stats at level 1 matching base stats unscaled; level-up
scaling MaxVitality/Power/Speed and preserving missing HP (not a full
heal); a kill granting exactly `KillXp(victimLevel)`; a golden six-kill
scenario leveling a unit up and measurably speeding up its movement and
hitting harder; and hash-determinism with leveling in play. citygen-core's
152 tests and match-core's other 108 pre-existing tests remain untouched.
`harvestcreditverify` (21 checks) and `flightcheck` still pass/compile
against the refreshed DLL -- no Unity-side files touched this phase.

## docs/23 Phase 5: flocking alignment + cohesion (2026-07)

Shipped the two remaining boid forces docs/23 §5 names — **alignment**
(match average heading of nearby groupmates) and **cohesion** (gentle pull
toward group centroid) — alongside separation, which docs/27 Phase C
already ported to `match-core`.

**match-core** (`Flocking.cs`): `Alignment`/`Cohesion` as pure math only —
normalized average of moving neighbours' headings, and a normalized pull
toward neighbour centroid, respectively. Deliberately **not** wired into
`MatchState`'s tick loop: docs/23 §5's own task list puts the live steering
integration under the *Unity* line ("wire into `MonsterAgent.FollowPath`"),
not match-core's, and match-core still has no "order group" concept to
scope neighbours by — the same gap docs/27 Phase B flagged and deferred
for queued group moves. `FlockingTests.cs` gained 6 tests (heading
convergence, zero-with-no-moving-neighbours-or-cancelling-headings,
bounded centroid pull, zero-at-centroid); full match-core suite: 133 tests
green.

**Unity** (`MonsterSteeringController.cs`): matching `Alignment`/`Cohesion`
static methods, scoped to same-`Faction` neighbours within 12m using each
neighbour's own published `LastVelocity` as its heading — `Faction` is the
existing two-value stand-in for docs/23's undefined "order group," already
used elsewhere in this file (`NearestEnemyOf`). Wired into `Combine`
**additively**: the existing `avoid*1.2f + sepBias*0.8f` terms and their
weights are byte-for-byte unchanged; the new `alignBias*0.35 +
cohesionBias*0.15` terms are zero whenever a unit has no same-faction
groupmates in range, so a solo unit — or one surrounded only by enemies —
sees zero behavior change from before this phase. This directly honors
this session's standing instruction not to alter existing
navigation/avoidance behavior.

**Explicitly deferred, not faked:** attack-move (`A`+click) and patrol
orders in `WaypointCommander` + the HUD hint line docs/23 §5 also asks
for — a separate, real player-facing command feature, not core to the
flocking math itself.

**Aside — harness staleness bug found and fixed:** `steercheck` (the
standalone harness compiling the real `MonsterSteeringController.cs`/
`DeadlockManager.cs`/`UnitCombat.cs` for numeric verification, previously
used for docs/25) had gone stale: its csproj never picked up
`SpecialAttackInstance`/`CaptureState`, two types the real `UnitCombat.cs`
gained in later docs/26 work, so it failed to compile (`CS0246`). First
fix attempt (compiling in the real `SpecialAttackDefinition.cs`/
`WebAttackAbility.cs`/`CaptureState.cs`/etc. files) cascaded into needing
`ScriptableObject`/`RuntimeCityBuilder` — far outside this harness's
scope. Fixed correctly by giving the harness its own minimal stub
definitions for just `SpecialAttackInstance` (bare, only needs to exist as
a type) and `CaptureState` (`Active`/`Captor` properties, `Begin`/
`TickPull` methods) matching only the exact surface `UnitCombat.cs` itself
calls (grep-confirmed), leaving the real csproj file list otherwise
unchanged. Re-verified all 11 pre-existing `steercheck` checks still pass
against the now-compiling real `UnitCombat.cs`, then added 3 new checks
for `Alignment`/`Cohesion` plus 3 regression checks proving `Combine`'s
additive wiring (solo unit unchanged, enemy-only neighbours unchanged,
same-faction groupmate actually bends the direction) — 18 checks total,
all passing.

**Verification:** 133 match-core tests, 152 citygen-core tests (untouched),
18 `steercheck` checks, `flightcheck` clean.

## docs/23 Phase 6a: salvage drops + harvest command (2026-07)

Shipped the salvage-drop half of Phase 6a (per §13 amendment D's 6a/6b/6c
split) — "every unit death drops 40-60% of its construction components,
lootable by either side for 15 seconds" (docs/04), sim-side.

**match-core:** `Salvage.cs`'s `SalvageMath` — a pure, all-integer 40-60%
uniform roll (docs/23 §0 determinism discipline: no double in a resource
payout, same bar `CombatMath` already holds itself to) plus a separate,
independent 10% "genome fragment" roll. `SimUnit.SalvageValue` is an
optional spawn parameter (default 0) — match-core never derives a unit's
"construction components" total, same "accept the genome-derived NUMBER,
stay genome-agnostic" pattern already used for Speed/Radius/`CombatStats`.
On death, `RollSalvage` fills `SalvageRemaining` (the loot still waiting)
and `YieldsGenomeFragment` (sim state only — see below). `CommandKind.
SalvageCorpse` + a new `UnitOrderKind.Salvaging` order drive a 3-second
harvest channel (`MatchState.TickSalvage`), re-validating range and
corpse-still-lootable every tick — an interrupted channel (corpse decays,
harvester or corpse moves out of range) cancels cleanly back to Idle
rather than erroring, the same "an order can go stale mid-channel" idea
`TickCombat` already applies to Reach. Completion pays the corpse's whole
remaining pile into the harvester's owner's `ResourceKind.Parts` wallet
(docs/04 describes one channel, not a partial-harvest system) via the
existing `PlayerState.Grant`.

**A real, pre-existing bug fixed along the way:** `ApplySeparationPass`
never filtered out dead units — a corpse could get shoved by a living
neighbour's separation nudge (or itself shove a living unit), drifting off
the hex it died on. This bug predates this phase (it's been there since
Phase 4 introduced death) but only started to matter now that a corpse's
exact position is something a harvest command actually range-checks.
Fixed by skipping `!self.IsAlive` units on both sides of the separation
pass — corpses are now inert, matching "a stable loot location," not a
moving body.

**Explicitly deferred, not faked:** `SimUnit.YieldsGenomeFragment` decides
only WHETHER a corpse yields a genome fragment, deterministically and
replayably — match-core has zero reference to genome-core or the Mutator
catalog (a repo invariant: genome-core has no engine/graphics deps, and
match-core doesn't reach into it either), so it cannot say WHICH part
family, whether the player already owns it, or apply the +5% stat
affinity/permanent Lab-unlock docs/23 §6 also promises. That translation
is a real, separate, not-yet-built job for whatever system reads the
match transcript afterward (mutator-service or a future match-summary
consumer) — this is sim STATE for that job to build on, the same
"flag the state, defer the cross-system consumer" shape `DeathTick` itself
used for salvage before this phase. Roaming Loose Experiment anomalies
(their own spawn/movement/aura-cycling/capture-on-kill system, also part
of 6a's own bundle) remain a separate, not-yet-started slice. Phase 6b
(enemy faction rosters as genome data) and 6c (utility-driven skirmish
commander AI) are unstarted, separate phases per amendment D.

**Verification:** 147 match-core tests (14 new: pure salvage-math bounds,
death-time roll, full harvest payout, empty-corpse/out-of-range/decayed-
corpse rejection, mid-channel cancellation on the corpse moving away,
corpse-stays-put-under-separation, and a same-seed-same-hash determinism
proof), 152 citygen-core tests (untouched).

## docs/23 Phase 6a: roaming Loose Experiment anomalies (2026-07)

Shipped the second half of Phase 6a (per §13 amendment D's split) — "2-4
Loose Experiments wander the neutral streets per match... cycling their
aura every 20s through Damage-Speed-Regen-XP-gain. Killing-blow player
captures it: the buff attaches to the killing unit for 90s, then the
anomaly respawns at a random roundabout" (docs/23 §6) — minus the wander
half, sim-side.

**`Anomaly.cs`'s `SimAnomaly`:** a deliberately SEPARATE, lightweight
entity kind from `SimUnit` — no owning player, no `CombatStats` of its
own, no facing/arc, no salvage/XP mechanics on "death." Same "a map
feature with its own runtime state, not shoehorned into the player-unit
model" relationship `SimEmitter` already has to a Landmark, extended here
to something that's also directly attackable. `CurrentBuff(frame)` is a
PURE function of `(frame - SpawnFrame) / CycleTicks % 4` — no internal
mutable timer to drift, the same shape `LumenClock` already established
for the match-wide day/night cycle, just re-based per anomaly so a
respawn restarts its own cycle at Damage.

**Spawning:** `MatchState.SpawnAnomaly(hex)` is a setup-time API (same
direct-call precedent as `SpawnUnit`/`SpawnHqForPlayer`) — the caller
picks which of `CityModel.Roundabouts` to use and how many (docs/23: "2-4
per match"); match-core doesn't decide placement itself.

**Combat:** `CommandKind.AttackAnomaly` + a new `MatchState.
TickAnomalyCombat` loop, deliberately separate from `TickCombat` (an
anomaly isn't a `SimUnit`) but reusing the exact same `CombatMath`
machinery (aura/affinity emitterMod, luck/crit roll, Ferocity-gated
cooldown) with a flat posMod of 100 (no facing to flank) and 0 Armor. The
instant an anomaly's Vitality reaches 0, the attacker's current tick
snapshots `CurrentBuff(Frame)`, grants that buff for 90s (docs/23's own
real duration number), and the anomaly respawns immediately at a new
random roundabout hex (`_rng.IntRange`) with its cycle restarted — no
corpse, no salvage/XP mechanics apply to an anomaly at all.

**The buff itself:** `SimUnit.ActiveBuff` (nullable enum) + a countdown
decremented every tick alongside the existing attack cooldown. Damage
(+25%) and Speed (+25%) multiply `EffectivePower`/`EffectiveSpeed`
directly. Regen (5%/simulated-second) heals via a NEW `MatchState.
ApplyAnomalyBuffRegen`, gated the same "once per simulated second, exact
integer, no fractional-tick drift" way `GrantEmitterManaIncome` already
grants mana. XpGain (+50%) scales the amount inside `SimUnit.GrantXp`.
**Every one of these four magnitudes is an invented v0.1 placeholder** —
docs/23 names which four buffs exist and their shared 90s duration, but
gives no numbers for what any of them actually DO, a genuinely different
kind of gap than every other "missing mechanism" logged so far this
session (the mechanism here is real and wired end-to-end; only the
tuning is a guess, flagged as such in `SimUnit.cs`'s own doc comments).

**Explicitly deferred, not faked:** wander movement ("drift along
sidewalks, Citizen movement reuse") — match-core has no Citizen-as-sim-
entity walker to reuse at all, the same missing-prerequisite gap already
logged against Citizens/upkeep in this file's Phase 3 entry. An anomaly
sits still at its (re)spawn hex between captures; it is still fully
functional as a contested, timed capture point, just not roaming yet. A
city preset that generates zero `CityModel.Roundabouts` (not every
`RoadPattern` does — only `MainStreet`, confirmed by reading
`CityGenerator.cs`'s own `isMainStreet` gate) has nowhere valid to
respawn an anomaly to; handled by respawning in place rather than a
silent failure, a real, flagged content-coverage gap, not resolved here.

**A test-writing lesson, not a production bug:** an early draft of the
Regen-buff test gave the "wounder" unit a normal Ferocity, which kept
re-attacking every tick for the ~400 ticks the test spent waiting for the
anomaly's cycle to reach Regen, killing the healable unit outright before
it could capture the buff. Fixed by giving the wounder a near-zero
Ferocity (one guaranteed hit, then effectively never again) — the
production combat/cooldown code was never wrong, the test's own setup was
under-constrained.

**Verification:** 157 match-core tests (10 new: buff-cycle ordering and
per-anomaly epoch, spawn placement, out-of-range rejection, capture +
respawn + buff-cycle-restart, Damage buff raising EffectivePower, buff
expiry at exactly 90s, Regen healing exactly once per simulated second,
XpGain scaling kill XP, and a same-seed-same-hash determinism proof), 152
citygen-core tests (untouched).

## docs/23 Phase 6b: Army + Hive faction rosters as data (2026-07)

Shipped Phase 6b per §13 amendment D's split: "the two enemy faction
rosters as genome data" — Human Army and Alien Hive, the exact unit
archetypes docs/23 §6 names (Army: Rifleman/Half-Track/Tank/Zeppelin
Gunship; Hive: Drone/Spitter/Floater Queen), sim-side.

**`FactionRoster.cs`'s `UnitRosterDef`:** a static per-`RosterUnitKind`
data table, deliberately mirroring `BuildingDef.cs`'s own already-
established pattern (a private array indexed by `(int)kind`, `Get(kind)`/
`AllDefs` accessors, DATA not simulation state so it's outside the tick
hash). Each entry carries a full `CombatStats`/Speed/Radius/SalvageValue
block — everything `MatchState.SpawnUnit` needs. `FactionId.MadDoctor`
gets no roster at all: the Doctor's whole identity is fielding CUSTOM bred
creatures through the Mutator, never a fixed unit list, so there's
nothing for a roster table to enumerate for that faction.
`MatchState.SpawnRosterUnit(playerIndex, hex, kind)` is a new setup-time
API (same direct-call precedent as `SpawnUnit`/`SpawnHqForPlayer`) that
resolves a def and spawns it — throws (not a silent no-op) if the
kind's own faction doesn't match the player's, since this is a
setup-time programming error, not a replayable command.

**Every stat number is an invented v0.1 placeholder, not a real figure —
and this is a genuinely different flavor of "placeholder" than earlier
ones.** docs/17-factions.md is rich, detailed, and REAL about each
faction's behavioral/economic identity (control-snap morale math,
origin-tag energy/material flavors, the Queen-as-Vat decapitation
mechanic) — but it never gives a numeric combat-stat table for any
individual unit archetype, the same way docs/04's damage formula or
docs/22's Blood Bank numbers were real, reusable figures. More
fundamentally: docs/17's own "the bill of materials IS the genome"
section describes these units as PRODUCTS of genome-core's real
expression math (bulk scaling, canalized part bounds, brain tier) —
match-core has ZERO reference to genome-core at all (a repo invariant:
genome-core has no engine/graphics deps, and match-core doesn't reach
into TypeScript either). There is no bridge from a real genome's
expression output to this C# table today. This table exists so Phase 6c's
skirmish AI and Phase 7's balance smoke test have real units to field
with AT ALL — it is explicitly NOT a claim that a Human Rifleman fielded
in a match is the same genome docs/17 describes. That translation is a
real, separate, not-yet-built integration job — the same "genome data has
no path into match-core" category of gap logged for Phase 3's upkeep and
Phase 3.5's affinity, now a third time here.

**Verification:** 169 match-core tests (12 new: every roster kind
resolves to its documented faction, `AllDefs` covers every enum value
exactly once with no array/enum-index drift, every entry has a sane
positive stat floor, `SpawnRosterUnit` copies the def's exact stat block
onto the spawned `SimUnit`, a faction/kind mismatch throws, a real mixed
4-unit Army-vs-Hive skirmish fields and ticks deterministically), 152
citygen-core tests (untouched).

## docs/23 Phase 6c: utility-driven skirmish commander AI (2026-07)

Shipped the last piece of §13 amendment D's Phase 6 split — "AI opponents
for skirmish use a utility-driven commander in match-core... so 1-player
matches work before netcode."

**Architecture decision, and it's the load-bearing one: the commander is a
command SOURCE, not part of the simulation.** `SkirmishCommander` reads a
`MatchState` and RETURNS `Command`s; the caller feeds them to the next
tick. Nothing in it runs inside `MatchState.Tick`. Three things fall out
of that, all of which would have been problems the other way:

- lockstep (docs/23 §11) already replicates a command stream, so an AI
  that emits commands needs no new netcode — one peer runs the commander
  and its orders replicate like any human's;
- a replay is exact without re-running the AI at all, since its commands
  are already in the log;
- its `double` utility math can never threaten cross-platform determinism,
  because docs/23 §0's float discipline governs the TICK path, and a
  command source sits outside it exactly the way Unity's mouse handler
  does.

Decisions themselves are RNG-free and evaluated in entity-ID order, so an
AI-vs-AI match is hash-identical run to run (pinned by a test).

**`ThreatMap`** is docs/23's other named ingredient, implemented as an
on-demand falloff FUNCTION rather than a materialized grid: a `BigCity`
preset is ~20k hexes and a match fields tens of units (§13-E targets
20-40/player), so scanning the small list per query is both cheaper and
exact, with no cell-resolution artifacts.

**Personality, and the two ways to author it (the creator's explicit
ask).** `CommanderPersonality` is six axes in three designed tension
pairs — Aggression↔Caution, Greed↔Territoriality, Opportunism↔Discipline.
Deliberately not a fresh vocabulary: it's docs/16's brain-gene idea
(`command`/`will`/`temperament`/`guile`/`fury`) lifted one level, from
"how does this ONE creature behave under stress" to "how does a commander
spend its turn" — the same "a faction is an expression profile, not a new
system" principle docs/17 applies to factions, applied to AI. Every
commander runs the SAME scoring code over the SAME action set; only the
weights differ.

1. **Dial it in.** Six named archetypes (`Berserker`, `Turtle`, `Hoarder`,
   `Warlord`, `Opportunist`, `Balanced`) plus a chainable
   `.With(trait, value)` for single-axis tuning:
   `CommanderPersonality.Turtle().With(CommanderTrait.Greed, 0.9)`.
2. **Generate it procedurally.** `Generate(seed)` / `Generate(SimRng)`
   rolls one off the project's canonical seeded RNG (never `Math.Random`
   — CLAUDE.md's determinism invariant), advancing the stream by a FIXED
   9 draws so pulling N commanders off one stream is reproducible
   position-for-position.

Generation is explicitly **not** six independent uniform rolls. That
reliably produces a field of indistinguishable ~0.5 commanders — every
axis regressing to the mean is exactly what makes procedural personality
feel same-y. Instead each rolled commander gets a **signature**: one
tension pair is driven apart (one side into [0.7,1], its opposite into
[0,0.3]), while the other two pairs roll freely for texture but
anti-correlated. Decision CADENCE is derived from Discipline rather than
configured separately, because "how long do you commit to a plan" is what
that trait means — a scattered opportunist re-reads the field every 2
ticks and abandons approaches halfway; a methodical commander locks in for
20 and is slow to react. Both are legible weaknesses rather than one
being strictly better.

**Four real defects, three of them found by printing a seed gallery and
actually looking at it** rather than by a failing test (all fixed, all now
regression-tested):

1. Generation only decorrelated the SIGNATURE pair and left the other two
   independent — so seed 7 rolled maximum Aggression *and* maximum
   Caution. That commander isn't interestingly conflicted, it's noisy: its
   charge and retreat utilities cancel and it dithers. Fixed by
   anti-correlating every pair; the guarantee is now exposed as
   `IsCoherent`/`CoherenceLimit`.
2. Damping was always applied to each pair's SECOND member, which quietly
   biased every generated commander away from Caution/Territoriality/
   Discipline (they are the `B` of their pair) — the gallery came out
   overwhelmingly "Grasping." Fixed by drawing which side gets damped.
3. A flat 0.5-everywhere personality labelled itself "Reckless," purely
   because Aggression sorts first among six identical values. Fixed with a
   `Spread` check and a "Nondescript" label.
4. Caught while wiring, not by gallery: a low-Discipline commander
   re-decides every 2 ticks, and re-issuing `SalvageCorpse` RESTARTS
   docs/04's 3-second all-or-nothing channel — so a greedy twitchy
   commander would have collected nothing, all match. Guarded by never
   interrupting a salvage channel in progress.

**Explicitly deferred, not faked:** docs/23's other named 6c ingredient,
*build order scripts*. `CommandKind.BuildStructure` exists (so structures
COULD be scripted), but match-core has no unit-PRODUCTION command
whatsoever — units are setup-time spawns, and "a Factory produces a unit
over time" is not a mechanic any shipped phase has. A build order that
can't produce units isn't a build order, so the whole scripted-opening
layer waits on that prerequisite rather than being half-built here; this
commander fights, loots, and takes ground with the army it is handed.

**Discovered, NOT fixed (flagged for a real decision):** no command
handler verifies that `Command.PlayerIndex` actually OWNS the unit in
`TargetEntity` — `ApplyMoveTo`/`ApplyAttackUnit`/`ApplySalvageCorpse`/
`ApplyAttackAnomaly` all look the entity up and act on it regardless. It
is harmless today (every caller commands its own units, and the commander
is asserted to do so), but it becomes a real exploit the moment commands
arrive over a wire from an untrusted peer. Command AUTHORIZATION is its
own concern that belongs with the netcode phase (§11), not a drive-by fix
inside an AI phase — logged here so it is a decision rather than an
oversight.

**Verification:** 193 match-core tests (24 new: personality validation/
`With`/default-safety/archetype distinctness, cadence bounds, seeded
reproducibility, signature guarantee over 200 seeds, coherence over 500
seeds, no-runaway-identity over 600 seeds, stream reproducibility, threat
falloff and living-enemies-only, the headline "identical board, two
personalities, two different orders" test, real-command acceptance, the
salvage-channel guard driven end-to-end, ownership scoping, cadence
throttling, a deterministic AI-vs-AI skirmish, and 12 generated
commanders all actually acting), 152 citygen-core tests (untouched), and
the `Tools~/DetHarness` acceptance harness still prints identical hashes
twice.

## docs/23 Phase 7: the Lumen Cycle faction modifier table (2026-07)

Shipped Phase 7's genuinely new piece. `LumenClock` and emitter-polarity
output already shipped with Phase 3.5 — Phase 7's own task list predates
that phase landing and still lists them as its job, which this status
note corrects rather than silently re-doing the work.

**`FactionLumenModifier`/`FactionLumenTable`** carry docs/23 §7's own
table VERBATIM — a rare case this session where the numbers are real,
first-party figures (not an invented v0.1 placeholder the way Phase 6b's
roster stats were), because docs/23 §7 itself authored them directly.

**Three of the six cells are wired into real gameplay:**

- **Army's Day +15% weapon damage** — `CombatMath.ResolveDamage` gained a
  new optional `lumenModPercent` parameter (default 100), folded into the
  existing integer-percent product exactly like `posModPercent`/
  `emitterModPercent` already are (docs/04's own "no double in the actual
  damage computation" bar, now a 100^4 scale instead of 100^3). Computed
  fresh at the point of attack in both `TickCombat` and `TickAnomalyCombat`
  via a new `LumenDamagePercentFor` helper.
- **Hive's Day -10% / Doctor's Night +10% speed** — `SimUnit.Tick` gained
  a new `speedMultiplier` parameter (default 1.0), multiplying the
  existing movement budget alongside `EffectiveSpeed`. Computed fresh
  per-unit, per-tick, in `MatchState`'s own unit-movement loop (the only
  place that knows both a unit's owning Faction and the current
  `LumenPhase` — `SimUnit` itself stays self-contained and knows
  neither).
- **Doctor's regen swing** — scales docs/06's REAL `regeneration` quirk
  rate ("1% max HP/s out of combat, gene-dependent — not every creature
  has it"), not an invented baseline. New `SimUnit.HasRegenerationQuirk`
  (optional spawn param, same genome-agnostic pattern as `CombatStats`/
  `SalvageValue`) and `SimUnit.LastCombatFrame` (stamped for free at the
  two points combat already resolves — `ApplyDamage` for the receiving
  side, the now-`int`-taking `ResetAttackCooldown` for the dealing side —
  rather than a new call site `MatchState` has to remember to hit). New
  `MatchState.ApplyRegenerationQuirk`, granted once per simulated second
  (same exact-integer idiom `GrantEmitterManaIncome`/`ApplyAnomalyBuffRegen`
  already use), gated by a v0.1 "3 simulated seconds since last combat
  activity" out-of-combat threshold (docs/06 names the mechanic, not this
  number).

**A real design tension resolved with no invented number needed:**
docs/23 §7's table reads as if every Doctor unit has SOME baseline regen
rate to modify (-10%/+15%), but docs/06 says regeneration is an OPT-IN
quirk, not a universal trait. Squaring the two: the Lumen modifier only
ever matters for a unit that actually rolled the quirk — for one that
didn't, 0%/s times any multiplier is still 0%/s. No baseline had to be
invented to make the table make sense.

**Explicitly deferred, not faked:** Army's -15% vision-radius and Hive's
+15% Dusk/Dawn Ichor income are real numbers, stored on
`FactionLumenModifier`, consumed by nothing. Match-core has no fog-of-war/
vision system at all (`BattlefieldState`'s blocked-hex set governs
PATHING, not visibility — a Unity/minimap concern per Phase 1's own task
list); ordinary per-faction resource income (as opposed to emitter Mana,
which Phase 3.5 already grants) still doesn't exist anywhere in
match-core — the same gap docs/12's Phase 3 entry already logged, still
unresolved. Both numbers are on record now so the moment either
prerequisite system lands, the real figure is already there rather than
requiring a second trip back to docs/23 §7 to look it up again.

**A test-design lesson, not a production bug:** the first draft of the
"Army's damage is boosted at Day" test tried to bound the comparison by
computing "Day's worst-case luck roll" vs "Night's best-case luck roll" —
but a flat +15% modifier can never dominate a wider ±15% luck band from
either side (the two ranges genuinely overlap), so that assertion was
mathematically impossible to satisfy, not a bug in the modifier. Fixed by
eliminating randomness from the comparison entirely (`cunningPercent: 100`
forces a guaranteed crit, so `luckOrCritPercent` is a fixed 150 with no
RNG draw at all) rather than trying to out-argue the luck band. A second,
related fix: the same test's first attempt placed attacker and defender
ADJACENT, so `Facing.ArcOf`'s own Front/Flank/Rear classification (driven
by the defender's default facing) silently added a SECOND uncontrolled
multiplier neither hand-computed expectation accounted for. Fixed by
using `Reach: 3` against a hex at exactly distance 2 — `TickCombat`'s own
"posMod is flat 100 unless the pair is exactly adjacent" rule then applies
deterministically.

**Verification:** 217 match-core tests (24 new: the golden 12-cell
modifier table, the two DATA-only numbers, `ResolveDamage`'s new parameter
including its backward-compatible default, Army/Hive/Doctor's damage and
speed modifiers wired into real combat/movement, the regeneration quirk's
baseline rate/Day-Night scaling/quirk-gating/out-of-combat gating, a
same-seed-same-hash proof spanning a real phase transition, and the
scripted Day-win/Night-loss duel), 152 citygen-core tests (untouched), and
the `Tools~/DetHarness` acceptance harness still prints identical hashes
twice (the hash values themselves changed from prior phases, as expected
-- `SimUnit.WriteTo` now hashes two new fields, so it hasn't drifted, it's
covering more state).

## docs/23 Phase 8: New York/Paris/Montreal region presets (2026-07)

Shipped Phase 8's citygen-core half: three flagship-region presets as
docs/18's own "one generator, a small authored kit of style presets"
economy — no bespoke per-region generator, just new DATA (`CityPreset.
NewYork()`/`Paris()`/`Montreal()`) plus one genuinely new piece of
GEOMETRY (`RoadPattern.Boulevard`).

**`CityRegion`** (`Generic`/`NewYork`/`Paris`/`Montreal`) is a new field
on both `CityPreset` and `CityModel`, copied straight through generation
— lets Unity's future dressing branch switch on one field instead of
string-matching `PresetName`. Every pre-Phase-8 preset is `Generic`.

**New York** reuses `BigCity`'s own scale/pattern/density knobs VERBATIM
— docs/23 §8's own words are "the Big City preset, personified," so
there was a real, sourced number to reuse rather than invent, exactly the
way `BuildingDef`'s `BloodStorage` reused docs/22's real Blood Bank
figures back in Phase 2. Re-skinned only with its own real named
emitter-archetype strings ("liberty_statuette_plaza," "grand_terminal").

**Paris and Montreal's SIZES are invented v0.1 placeholders** — a
genuinely different situation from New York's: docs/23 §8 gives no
explicit km² figure for either (unlike Village/SmallTown/BigCity/New
York, which all have one), so Paris reuses SmallTown's own scale and
Montreal a modestly larger one (for "the big ridge cluster" Mount Royal
needs), both flagged in `CityPreset.cs`'s own doc comments. Their
landmark archetype strings ("iron_tower," "sacré-cœur," "marche_tower,"
"forum_arena") ARE the real names docs/23 §8 gives, used verbatim.

**`RoadPattern.Boulevard`** (Paris's own pattern) is the real new
geometry this phase adds: the cardinal grid (read as Grid's own dense
net — docs/23 §8's prose says "the cardinal grid" without specifying
which existing pattern that means; Grid's is the documented reading,
since MainStreet's is a single-arterial-plus-sparse-residential net, not
a grid) plus two diagonal avenues. Each avenue is traced as a walk in ONE
FIXED hex direction, radiating from the map center in both directions
until it exits the map — deliberately not a generic point-to-point
Bresenham hex-line algorithm, since the avenues always pass through the
center by construction and a fixed-direction walk makes "straight in
world space" true BY CONSTRUCTION (`HexCoord.ToWorld` is a linear map, so
a constant per-step axial delta is provably a constant per-step
world-space delta) rather than something to approximate afterward. The
two directions used — `NW`/`SE` and `NE`/`SW` — are specifically the only
two (of the six primitive hex directions) whose world-space step has a
NONZERO component on both axes; `E`/`W` is the one direction that's
PURELY horizontal, already the "row" direction MainStreet's own arterial
and Grid's own row-streets use, so using anything else here is what makes
these read as genuinely diagonal against the cardinal grid rather than a
third cardinal axis. The two avenues are unioned into both the road set
and the (Boulevard-only) arterial set BEFORE `ChooseBridges` runs, so a
river crossing gets a bridge automatically, exactly like any other road
— no special-case bridge logic needed for Boulevard at all.

**l'Étoile sits deliberately ON the crossing** — the literal opposite of
MainStreet's own "a roundabout must never sit in the middle of the
through-arterial" rule two paragraphs earlier in the same file. This is
a documented EXCEPTION, not a violation: Boulevard is not MainStreet, and
a grand traffic circle at the meeting of grand avenues is the entire
point of the real Place Charles-de-Gaulle this preset models. The
existing MainStreet rule itself is completely untouched (`isMainStreet`
stays false for Boulevard, so none of that code path is even reached)
and is re-verified as this phase's own explicit regression test, on top
of the whole pre-existing `CityGeneratorTests.cs` suite continuing to
pass unmodified — the literal "off the arterial rule preserved" acceptance
line docs/23 §8 itself asks for.

**A test-design lesson, not a production bug:** the first draft of the
diagonal-avenue test asserted every intermediate hex along a walked
avenue must itself be in `ArterialRoads` — an "unbroken line" assumption
that isn't actually guaranteed: the river can drown an avenue hex that
isn't one of the few `ChooseBridges` happened to pick, the SAME "drowned
road segments vanish" rule every other road in this generator already
follows (MainStreet's own single arterial row is subject to the identical
rule and always has been). Fixed by dropping the unbroken-line assertion
and keeping only what's actually guaranteed: every arterial hex that DOES
survive is exactly `center + k * direction` for some integer k, and lies
exactly on its avenue's own straight world-space line (a cross-product
check). A second, related miscount in the same test: two BIDIRECTIONAL
avenues show up as four distinct direction vectors (each avenue radiates
both ways from center), not two — fixed by asserting four vectors forming
exactly two opposite pairs, not two vectors outright.

**Explicitly deferred, not faked:** every Unity task docs/23 §8 names —
region-keyed `BuildingDresser`/`RoadDresser` style branches, the three
signature props (fire escape / Métro entrance / spiral staircase),
palettes, and the `RuntimeCityBuilder`/CityGizmo region picker. citygen-
core's own "skin pass is renderer-side data, not logic" principle
(already stated in `CityPreset.cs`'s own header) keeps this whole layer
out of scope for this slice; no Unity design pass was attempted.
`flightcheck` was not re-run since no Unity source references any of this
phase's new API surface yet (every change here is additive — new enum
values, a new optional constructor parameter defaulting to the old
behavior) and its own DLL copy is therefore unaffected.

**Verification:** 168 citygen-core tests (16 new: deterministic
generation for all three region presets, `Region` correctly threaded
through for both new and every pre-Phase-8 preset, l'Étoile sitting
exactly at the map center and nowhere else, the diagonal avenues'
provable world-space collinearity, the MainStreet off-arterial regression
proof, and a non-Boulevard preset's center NOT auto-becoming a
roundabout), 217 match-core tests (untouched, confirming citygen-core's
API stayed backward compatible), plus a deterministic ASCII-dump text-art
rendering of all three regions committed to `docs/23-balance/` (a real,
non-fabricated textual rendering, not a claim of an actual screenshot).

## docs/23 Phase 9: solidity/boundaries/destruction audit (2026-07)

Shipped both halves of the hardening audit against the creator's law:
"Must adhere to the physical boundaries of the playfield, buildings are
solid and cannot be walked through unless they are destroyed."

### match-core's own mover: the fuzz harness, at literal scale

`ContainmentFuzzTests.cs` drives docs/23 §9's acceptance bar EXACTLY as
written — 200 units, 100,000 ticks, seed-driven random `MoveTo`/
`MoveQueue` orders (never `Math.Random` — docs/23 §0), containment
asserted every single tick — rather than a scaled-down stand-in. It runs
~4 minutes, a real cost accepted deliberately: this is a dedicated audit/
stress test, not a routine unit test, and shrinking it would have reduced
confidence in exactly the property it exists to prove. Two companions:
a smaller building-churn fuzz that mutates the blocked set LIVE via real
`BuildStructure`/`ApplyBuildingDamage` calls while units keep moving
(proving containment survives blocked-set MUTATION, not just a static
map, and that a destroyed building's exact footprint is both out of the
blocked set and actually walkable by a real unit — not merely absent
from a set in the abstract); and a geometric proof that `SimUnit.Tick`'s
path-following is tunnel-proof by construction (it snaps exactly onto
each path node regardless of how large one tick's speed budget is, so
there's no code path that advances by a raw distance without first
landing on an already-validated node).

**The 100k-tick run found a real, subtle production bug on its very
first try — exactly the point of building it at full scale.** At tick
2004, unit 77 drifted into a blocked hex. Root cause, once diagnosed
(two dead ends first: an apparent off-by-one in `NearestHex` turned out
to be an entity-ID-vs-array-index mixup in the DIAGNOSTIC code, not a
production bug; a brute-force nearest-hex search confirmed the hex
conversion itself was exactly correct): `ApplySeparationPass` validates
its OWN nudge's destination every tick (correct, and already true since
docs/27 Phase C) — but regular path-following movement (`SimUnit.Tick`)
was NEVER independently re-validated against the blocked set at all. It
didn't need to be, on its own: `HexPathfinder` only ever hands out open
path nodes, and straight-line interpolation between two ADJACENT open
hex centers is geometrically confined to those two hexes' own Voronoi
cells — provably, it can never stray into a third. But that geometric
guarantee silently assumes the unit sits exactly on the path's own
centerline, and many individually-valid separation nudges (each checked
and passed on its own) can accumulate into a lateral drift OFF that
centerline over enough ticks, until a LATER path-following step — taken
from the drifted position rather than the line the geometry proof
assumed — clips a hex neither system ever explicitly checked.

**Fixed** with `SimUnit.RevertToSafePosition` (reverts position, drops
the path, returns to Idle) plus a re-validation check added right after
the existing per-unit `u.Tick(...)` call in `MatchState.Tick`'s own
movement loop: the hex is checked again immediately after every regular
movement step, and any violation reverts that tick's movement outright
rather than let containment slip — a unit stalled for one tick costs far
less than tunneling through solid ground. Confirmed by re-running the
full 100k-tick/200-unit fuzz to completion afterward (clean), plus the
rest of the match-core suite (220 tests) and citygen-core (168, untouched)
and the `Tools~/DetHarness` determinism harness (still self-consistent —
the hash VALUES changed from Phase 8's own run for scenarios that
actually exercise this code path, which is the expected shape of a real
behavior fix, not a regression).

### Unity's named movers: audited by reading, not running

No Editor exists in this environment to fuzz-test Unity's own movers the
way match-core's could be. Delegated a focused, read-only static audit
(an Explore-type agent, one pass over MonsterAgent.cs/Tank.cs/
TrafficCar.cs/Citizen.cs/MonsterSteeringController.cs/WaypointCommander.cs/
CaptureState.cs/RuntimeCityBuilder.cs) against the same three properties
docs/23 §9 names: blocked-set respect, map-bounds respect, no-tunneling
at speed. docs/23 §9 also names "fusion channel drift" and "anomaly
wander" — neither exists as Unity code at all yet (Fusion stayed deferred
at docs/23 §4's own RPG-layer status note; match-core's own anomalies
have no wander movement either, per Phase 6a's status note), so there
was nothing to audit for those two — not an oversight, just nothing built
yet to check.

**Findings, most to least severe:**

1. **`MonsterAgent.cs`'s flying `FollowPath` branch — a real violation,
   FIXED.** The grounded branch already clamps its per-tick step to
   `Mathf.Min(scaledSpeed*dt, dist)`; the flying branch's
   `transform.position += nose * (speed * dt)` had no such clamp at all,
   so a frame hitch (a `dt` spike) combined with a fast flyer's
   configured speed could overshoot `FlightArriveDist` and cut through a
   hex-corner obstacle mid-turn. Fixed with a one-line magnitude clamp
   to `FlightArriveDist` (8m — already documented in that constant's own
   comment as "well under a hex... never cuts through a corner
   obstacle") — a genuine no-op at any normal frame timing (speed×dt is
   far smaller than 8m for any sane cruise speed), engaging only during
   an actual dt spike, and touching nothing about the "carve, don't
   strafe" banking/nose-direction behavior the surrounding code
   documents. `flightcheck` recompiled clean against the real file.
   **Not visually verified — no Editor exists in this environment.**

2. **`CaptureState.TickPull` (shared by `MonsterAgent`'s captured-monster
   drag and `Citizen`'s captured-victim drag) — a real violation, NOT
   fixed.** Pulls the captured unit in a dead straight line toward its
   captor with zero blocked-hex check, zero bounds check, and no
   step-vs-hex-size bound whatsoever — a capture that spans across a
   building drags the victim straight through it. Deliberately left
   unfixed this pass: `CaptureState` (docs/26 Phase 6's own capture-and-
   consume mechanic) has no reference to city/blocked-set data at all
   today, so a real fix is a genuine interface change touching two call
   sites, with no Editor available to confirm the web-pull still *feels*
   right afterward (docs/26's own creator-facing tuning target) — exactly
   the "risky to fix blind" category this project's whole session has
   held back from rather than guessing at. Flagged here as a real,
   confirmed gap for whoever next touches capture, not silently
   discovered and dropped.

3. **`Tank.cs`'s steer-then-move gap, and `MonsterSteeringController.
   Combine`'s unclamped force blend — identified, NOT fixed, lower
   confidence than #1/#2.** `Tank.cs` validates a single probe point 6m
   ahead (`BlockedForTank`) before steering but never re-checks the
   actual hex the subsequent movement writes to; under a frame hitch the
   per-frame step could exceed that 6m probe distance into unchecked
   territory. `Combine` blends separation/avoidance/alignment/cohesion
   and renormalizes with no clamp bounding how far the blended result can
   deviate from the original path-seek direction, so in principle a
   strong simultaneous multi-force blend near a building edge could steer
   a heading into an adjacent hex the path never intended to cross (the
   actual position write happens back in `FollowPath`, which has no
   independent obstacle re-check of its own either). Both need a specific,
   comparatively rare condition to manifest (a real frame hitch; a strong
   simultaneous force blend right at an edge) and both would need
   Editor-side tuning verification to touch safely — left as documented,
   unresolved risk.

4. **`TrafficCar.cs`, `Citizen.cs`'s own movement (its capture-drag is
   #2 above), and `WaypointCommander.cs`: confirmed SAFE.** Every
   destination in all three comes from an already-validated network/
   path/bounds check before any transform write, and every per-frame
   step is already clamped to the remaining distance — no raw click
   coordinate or unclamped step anywhere in these three files.

5. **Destroyed-building footprint reopening: confirmed correct, no fix
   needed.** `RuntimeCityBuilder.ApplyBuildingDamage` updates the SAME
   `_battlefield` blocked-hex representation every mover's own blocked
   check reads from, and invalidates the derived cache on change — one
   source of truth, no desync between "the building is gone" and "the
   hex is walkable again," for both generator-placed and player-built
   structures. `RubbleDresser`/`BuildingDresser` are purely cosmetic and
   hold no independent passability data of their own to drift out of
   sync.

**Verification:** 220 match-core tests (3 new: the literal 200-unit/
100k-tick fuzz, the building-churn companion, the no-tunneling geometric
proof), 168 citygen-core tests (untouched), `Tools~/DetHarness` still
self-consistent, `flightcheck` recompiled clean against the one applied
Unity fix.

## docs/23 Phase 10: daytime mood-board reference added (2026-07)

Creator supplied a concrete visual reference (an isometric 1930s-40s
city-street illustration, in the spirit of Omerta: City of Gangsters'
own period art direction) for what Phase 10's DAYTIME look should be.
docs/23 §10's existing target-look paragraph is written almost entirely
for NIGHT ("warm sodium nights... unhurried noir palette"); Day needed
its own equally-considered mood rather than reading as just "noir minus
the neon," especially since §7's Lumen clock already cycles through both
in every real match.

Folded into §10 as a mood-board addition, not implemented code: sun-baked
desaturated-sepia warmth (distinct from Night's saturated neon/sodium
palette), a low sun angle with long legible cast shadows as Day's own
signature rather than a neutral noon angle, individually legible brick
coursing/weathering (today's `BuildingDresser` bricks are a flat color —
real texture is §10.3's PBR-atlas job), hand-painted-looking shop
signage, an embedded tram-rail street + streetcar prop (likely New York/
§8-scoped, alongside the existing railyard district), ornate multi-globe
lampposts, a denser period-dressed pedestrian crowd, and chrome-trimmed
period sedans in muted body colors.

**Explicitly not done:** no code or asset was touched. Two of the three
reference image URLs the creator first supplied (media.craiyon.com,
gaminglives.com) were blocked outright by this session's own outbound
network policy ("gateway answered 403 to CONNECT (policy denial)") — not
a transient failure, a deliberate restriction — so the actual reference
used was a directly-uploaded local image file. Turning this mood board
into real render results (a Day-specific color-grading LUT, brick/
signage textures, the tram-rail prop, lamppost variety) still needs the
same real assets + Editor-side iteration every Phase 10 sub-phase already
requires (no Editor exists in this environment); nothing here was
visually verified, consistent with this whole project's standing
discipline against claiming verification it can't back up.

## docs/23 Phase 5 follow-up: attack-move + patrol orders shipped (2026-07)

Implemented the piece of Phase 5 explicitly deferred when the flocking
slice landed: attack-move (`A` + click) and patrol (`P` + click) orders,
plus the HUD hint line.

`MonsterAgent.cs` gained `OrderKind.AttackMove` (patrol reuses it via an
`_isPatrolling` flag rather than a separate order kind) and a
`TickAttackMove` that scans `NearestEnemyOf` every tick — the same
aggro-range check `AcquireTarget` already made, now pulled into a shared
`AggroRangeMeters` constant instead of two copies of the same inline
`130f`. Finding an enemy detours straight into a real `AttackUnit` fight:
`_targetUnit`/`_order` are set directly rather than through the public
`OrderAttackUnit`, specifically so the pending attack-move/patrol
destination (`_attackMoveDestination`/`_isPatrolling`/`_patrolOtherEnd`)
survives the detour instead of being wiped by `ClearTargets()` — but
`ClearTargets()` itself still resets those three fields, so any
genuinely new player order correctly cancels a pending attack-move or
patrol.

A new `GoIdleOrContinueAttackMove` helper stands in for a plain
`GoIdle()` at exactly the three call sites where the distinction
matters: `TickMove`'s arrival and unreachable-path branches, and
`TickAttackUnit`'s target-gone branch. With no pending attack-move
(`_attackMoveDestination` null — true for every ordinary Move/AttackUnit/
etc. order) it is byte-for-byte `GoIdle()`, so none of the other ~13
existing `GoIdle()` call sites or order kinds changed behavior at all.
Otherwise: an unreachable leg gives up and lands; a one-shot arrival
lands; a patrol arrival flips to the other end and keeps walking; and a
detour ending (the enemy chased down mid-attack-move died) resumes
toward the original destination.

`WaypointCommander.cs` binds `A`/`P` + left-click the same way
`Ctrl+left` already stands in for right-click (both gate the plain
selection-click handler so the same press doesn't also start a
marquee-drag). Grep-confirmed both keys were free before this change —
`SimpleCameraRig.cs`'s own WASD camera pan also reads `aKey`, but that's
a different input context, not a real keybinding collision; it does mean
holding `A` to click an attack-move order also pans the camera left for
that instant, a known, minor, accepted UX overlap, not a bug. The HUD
hint line was added to `HudStatus.cs` alongside the existing
control-reference lines (that file, not `WaypointCommander.cs`, is where
every other hint line already lives).

**Known, accepted gap:** no `SimDriven` (docs/27 opt-in sim-driven
movement) equivalent — match-core has no sim-side attack-move concept
yet, so a sim-driven unit's attack-move would need to fall back to
`TickMoveViaSim`'s plain move with no auto-engage; in practice this never
triggers today since `OrderAttackMove`/`OrderPatrol` don't check
`SimDriven` at all (only `OrderMove` has a `SimDriven` branch) — a
sim-driven unit issued an attack-move gets the plain legacy `TickMove`/
`TickAttackMove` path, same as every other order kind except Move.

Verified via the `flightcheck` scratchpad harness only (compiles the
real `MonsterAgent.cs`/`WaypointCommander.cs`/`HudStatus.cs` against a
Unity API stub — the stub's `Keyboard` needed a `pKey` field added,
harness-only, the real Input System package already has one). No Unity
Editor exists in this environment, so this is NOT visually or
runtime-verified — consistent with this whole project's standing
discipline against claiming verification it can't back up.

## docs/23 Phase 10 (Graphics): sub-phases 1-2 shipped (2026-07)

Implemented the first two of Phase 10's six sub-phases: Post stack and
Lighting. Chose these two first because they're the only sub-phases
achievable purely in code — sub-phases 3-6 (materials, meshes,
creatures, FX) each genuinely need real texture/mesh assets from an
Editor/DCC pipeline, which this environment does not have.

`NightMode.cs` — a manual `N`-key binary day/dusk toggle from docs/21
batch 3 — is superseded by `LumenCycleController.cs`. The new
controller keeps its own cosmetic fixed-tick counter (10 ticks/s,
matching `MatchState.TicksPerSecond`) and reads
`MadDr.MatchCore.LumenClock.PhaseAt(frame)` every tick — the exact same
pure function match-core's own Phase 7 faction modifiers already use —
purely for presentation. It never reads or writes a live `MatchState`,
so this stays entirely Unity-layer, satisfying the phase's own "no
determinism regression" line. Four keyframes (Dawn/Day/Dusk/Night)
cross-fade continuously (eased via `Mathf.SmoothStep`, not a hard
snap) across sun color/intensity/elevation, ambient light, fog, the
existing `NeonRegistry` boost, and a runtime-built URP Volume.

Per the 2026-07 daytime mood-board addition already folded into §10's
prose, Day's own elevation keyframe is capped low (30°) rather than a
high noon angle, for long, legible cast shadows through most of the
cycle; sun yaw stays fixed across the whole cycle so cast-shadow
DIRECTION stays consistent (only elevation and color animate). Day's
`ColorAdjustments` grade lands closer to sun-baked sepia-warm
(saturation -18, warm color filter) than a neutral render, distinct
from Night's saturated neon-noir push (saturation +22, cool-noir base
filter, the highest bloom of the four phases — "tuned for neon").

**Region grading is a deliberate substitution, flagged explicitly, not
silently passed off as the real thing.** docs/23 §10 asks for
per-region color-grading LUTs — a baked 3D lookup texture via URP's
`ColorLookup` component, which needs an authored asset (a DCC bake or
an Editor LUT-strip export) this environment cannot produce. What
shipped instead is a parametric `ColorAdjustments` tint keyed off
`CityModel.Region` (docs/23 §8's own `CityRegion` enum) — NY pushes
steel-blue and grittier, Paris pushes warm cream and softer, Montreal
pushes cold pastel and flatter, Generic gets the untouched baseline.
Same visual INTENT (a distinct per-region mood), a different mechanism
— written down here rather than calling it "the LUT system."

**Street lamps are real lights now, on a budget.** `RoadDresser`'s
existing streetlight prop was a primitive bulb sphere with only an
emissive material — no actual light source, so its "warm sodium
nights" read was implied by material glow alone. Both of `RoadDresser`'s
bulb-spawn sites (the per-street prop and the roundabout ring) now also
register their bulb `Transform` with a new `StreetLampRegistry` (the
same loose static-registry idiom `NeonRegistry` already established, so
the static `RoadDresser` generator never needs a reference to the new
system). `StreetLampLightBudget` refreshes on a 0.35s timer, finds the
nearest `Budget` (default 24) registered bulbs to `Camera.main`, and
promotes exactly those to a real warm-sodium `Point` light (no
shadows — these are budget fill lights, not key lights), reusing a
small pool of `Light` components across refreshes instead of
create/destroy churn. Every other registered bulb is completely
unaffected — still lit by its pre-existing emissive material only.
Light intensity rides the same day/night blend the post stack uses,
published via a new `DayNightState.NightAmount` static so the budget
system needs no direct reference to `LumenCycleController`.

**Deferred within this sub-phase, and why:** SSAO is a URP Renderer
Feature that has to be added to the project's `UniversalRendererData`
asset — an Editor-authored `.asset` file this environment has no way
to create or safely inspect/mutate without an Editor session to verify
nothing broke; light cookies need an actual cookie texture asset, a
DCC deliverable. Depth of field for "the Lab podium" (§10's post-stack
line) was not even stubbed in as an inactive component — grep-confirmed
zero hits for `Podium`/`LabScene`/etc. anywhere in `unity-client`, so
there is no such scene to focus on yet; a real, separate prerequisite
gap, not this sub-phase's to solve.

Every numeric keyframe (sun colors/intensities/elevations, region tint
deltas, lamp intensity/range, budget size, refresh interval) is an
invented v0.1 placeholder — docs/23 §10 gives mood/target-look
language, not real numbers, same as every other phase's placeholder
convention in this log.

Verified via `flightcheck` only, after two additions to the harness
itself: (1) `packages/citygen-core`'s compiled DLL vendored into the
harness was stale (predated Phase 8's `CityRegion` enum) and had to be
rebuilt and re-copied — a harness-maintenance step, not a source
change (the real citygen-core source was untouched; its 168 tests still
pass); (2) a new, minimal `UnityEngine.Rendering`/
`UnityEngine.Rendering.Universal` stub section (`Volume`,
`VolumeProfile`, `VolumeParameter<T>` and its float/clamped-float/
color/bool subtypes, `ColorAdjustments`, `FilmGrain`, `Vignette`,
`Bloom`, `Tonemapping`, `UniversalAdditionalCameraData`) matching only
the exact surface `LumenCycleController.cs` calls, not URP's real API
breadth — the same "stub only what's grep-confirmed used" discipline
the `steercheck` harness fix used earlier this session. No Unity
Editor exists in this environment, so none of this is visually
verified — consistent with this whole project's standing discipline
against claiming verification that didn't happen.

## docs/23 Phase 10 (Graphics): sub-phases 3-4 shipped (2026-07)

Following on from sub-phases 1-2 (post stack + lighting): implemented
Materials and Meshes, the same way — real, working code, but every
texture/mesh is a PROCEDURALLY GENERATED placeholder rather than an
authored asset, because this environment still has no Editor/DCC
pipeline to author real ones.

**Materials.** `PbrTextureAtlas.cs` builds six small (64x64) placeholder
`Texture2D`s in code (brick coursing + mortar + per-brick jitter;
two-octave mottled limestone; grained asphalt with a few brighter "wet
streak" rows; banded chrome; scratched/riveted painted metal; a
diagonal-sheen glass). Wired into the EXISTING dresser material cache
functions — `BuildingDresser.Brick()`/`Concrete()`/`Chrome()`/
`WindowBand()`, `RoadDresser.Asphalt()`/`ChromeTrim()`/`PoleMetal()` —
with zero geometry changes and zero changes to any other existing flat
color (`Cream`, `Seafoam`, `Mustard`, etc. are all untouched). Every
textured material gets one fixed tiling scale (3x3) rather than a
per-object scale computed from world size — Unity's built-in primitive
UVs aren't world-scale-aware, and getting that right would need a
`MaterialPropertyBlock` touch at every `SpawnPrim` call site across both
dresser files, out of scope for a placeholder pass. Flagged, not
silently implied as scale-correct.

**Meshes.** `PropLibrary.cs` ships the actual infrastructure docs/23
§10.4 asks for: "swap CreatePrimitive calls for a PropLibrary lookup
(mesh assets by key, with primitive fallback so the game never breaks
without assets)." `PropLibrary.Spawn(...)` mirrors
`RuntimeCityBuilder.SpawnPrim`'s own calling convention (world-center
position, local scale, one material) so no dresser call site needs to
know whether a key resolves to a real mesh or a plain primitive — a
future real imported-mesh asset only ever needs a one-line change to
this file's registration, never a dresser rewrite.

Backing it today: `ProceduralMeshKit.cs`, two hand-authored placeholder
meshes for shapes `CreatePrimitive` doesn't offer (a tapered-cylinder
`Frustum`, a lean-to-awning `Wedge`/right-triangular-prism) — built via
the same manual vertex/triangle authoring `LabMeshBuilder` already uses
for creature-mesh chunks, not an imported asset. Both emit every face in
BOTH triangle windings, a deliberate safety net against a winding-order
mistake this environment has no Editor to visually catch (doubles the
triangle count on these small, few-per-scene props — an explicit,
acceptable tradeoff, not a pattern for anything performance-sensitive).

Two new signature props from the 2026-07 daytime mood-board addition use
this infrastructure, wired into `RoadDresser`'s existing street-furniture
switch as new cases 6-7 (its modulo range widened from `%6` to `%8` —
the same incremental-variety pattern every earlier furniture pass
already used, e.g. docs/21 batches 4-8): an ornate multi-globe lamppost
(the `Frustum` pole plus three independently `StreetLampRegistry`-
registered warm globes, so Phase 10.2's light budget can promote any of
the three) and a market/vendor stall (the `Wedge` canopy over a plain
counter box).

**Deliberately NOT attempted: the mood-board's third new prop, a
streetcar on embedded tram rails.** This is a materially bigger unit of
work than a static prop — a moving vehicle (comparable to `TrafficCar.cs`),
a distinct embedded-rail road-surface treatment, and region-gating logic
that doesn't exist in any Unity dresser today (Phase 8's `CityRegion` is
citygen-core-only; `LumenCycleController`, from this same Phase 10, is
still the only Unity-side consumer of `CityModel.Region`, for lighting
grade only). Shipping a shallow version of three different systems to
check a box would cut against this project's own "flag, don't fake"
discipline more than an honest deferral.

Every numeric value (texture jitter amounts, tiling scale, frustum
taper/segment counts, prop dimensions) is an invented v0.1 placeholder.
Verified via `flightcheck` only (added `Texture.wrapMode`/
`TextureWrapMode` and `Material.SetTexture`/`SetTextureScale` to the
harness stub, matching only the exact surface this code calls). No
Unity Editor exists in this environment, so none of this — texture
pattern legibility, tiling frequency, the frustum/wedge silhouettes, or
the winding-safety-net's actual rendered result — is visually verified.
Full shipped/deferred accounting: `docs/23-balance/graphics-3-notes.md`
and `graphics-4-notes.md`.

## docs/23 Phase 10: street-lamp/night-ambient creator correction (2026-07)

First real Editor feedback on any of Phase 10's work (everything shipped
so far was flightcheck-compiled only, never visually verified). Creator
report, verbatim intent: "the street lights are way too bright and
default: spot lights pointing at the ground from above. The effect
should be pools of lights, moving to full night with no or little
ambient light."

Grep-confirmed there is no actual `LightType.Spot` anywhere in the
codebase (`StreetLampLightBudget`'s pooled lights are explicitly
`LightType.Point`; the only other live light is `LumenCycleController`'s
`Directional` sun) -- "spot lights pointing at the ground from above" is
a description of the RESULT, not a literal type bug: a bright Point
light mounted at lamp-head height reads exactly like a harsh downward
spotlight once it's too intense to look like a soft glow.

Root cause was brightness stacking, not geometry: `RoadDresser.Bulb()`'s
emissive material (1.4) multiplied by `NeonRegistry`'s own Night boost
(2.2x) pushed the bulb's rendered emissive past 3.0 -- before even
counting the real `StreetLampLightBudget` point light (up to 3.2
intensity) sitting at the same spot, under Night's own high Bloom
(1.3). Two independently-bright sources plus aggressive bloom
guaranteed a blown-out glare, never a "pool." Separately, Night's
ambient (0.14, 0.13, 0.26) and sun intensity (0.18) kept the WHOLE scene
lit regardless of any lamp, so even a perfectly-tuned lamp glow could
never read as a distinct pool against genuine darkness -- there wasn't
enough contrast for anything to look like an actual pool of light.

Fixed three places, each independently:
- `RoadDresser.Bulb()`'s emissive: 1.4 -> 0.7 (0.7*2.2 ~= 1.5 at Night,
  down from ~3.1).
- `StreetLampLightBudget`'s Night-peak point-light intensity: 3.2 -> 1.1.
- `LumenCycleController`'s Night keyframe: ambient (0.14,0.13,0.26) ->
  near-black (0.02,0.02,0.05); sun intensity 0.18 -> 0.05.

Deliberately did NOT touch `NeonBoost`/Bloom/Vignette (those govern all
neon signage broadly, not specifically streetlamps, and weren't named in
the complaint) or switch the light to an actual downward Spot (the
complaint explicitly calls THAT look "too bright and default" -- doubling
down on a directional spotlight-down effect would reproduce exactly what
was rejected, just for a different technical reason).

Not re-verified visually (still no Editor access from this session) --
flightcheck-compiled only. This is a real, values-driven correction
(matches `maddr-aesthetic-preferences`'s "1950s monster-movie noir, not
grimdark/washed-out" target), not a guess; if the pools still don't read
right once someone can actually look at it, the next lever to pull is
`StreetLampLightBudget`'s `range` (currently unchanged at 9) before
touching intensity again.

## docs/28: city lighting system -- ScriptableObject profile + scalable architecture (2026-07)

Follow-up to the previous street-lamp brightness correction: the creator
reported the lights were STILL "big opaque balls of light on the screen
that obscure the view... turning it completely white," and asked for
(a) an Inspector setting or ScriptableObject to tune intensity directly,
and (b) a real plan for scaling to "a bunch of different lights" (house/
apartment windows, marquee "clique" chase lights, buzzing/flickering
neon, streetlights) while staying performant with hundreds on screen at
once, able to turn on/off and fade.

Full architecture write-up: docs/28-city-lighting-system.md. Summary of
what changed and why:

**Root cause of the still-too-bright complaint**: the previous fix
lowered numbers but they were still hardcoded, unverifiable guesses
(no Editor access to confirm), and more importantly the ARCHITECTURE
itself didn't scale -- `StreetLampLightBudget` was streetlamp-only, so
adding windows/neon/marquee as their own separate real-light systems
would have multiplied the total live-light count exactly the way this
whole system needs to avoid.

**`CityLightingProfile.cs`** (new): a ScriptableObject
(`Assets > Create > MadDr > City Lighting Profile`) gathering every
lighting-related number that used to be a hardcoded constant scattered
across `LumenCycleController`/`RoadDresser`/`BuildingDresser`/the old
`StreetLampLightBudget` -- real-light budget/peak intensity/range, base
emissive brightness, the night boost ceiling, night ambient/bloom,
flicker/buzz/chase timing. `RuntimeCityBuilder` gets a `lightingProfile`
Inspector field; unassigned falls back to `CityLightingProfile.Default`.
This is the direct, literal answer to "give me an inspector setting...
or a scriptable object that I can change" -- the creator can now retune
brightness themselves without another code round-trip.

**Generalized the real-light budget**: `StreetLampLightBudget`/
`StreetLampRegistry` are replaced by `GlowPointRegistry` (any glowing
prop, any kind, registers here with its own tint color) +
`DynamicLightBudget` (one shared pool of real `Light` components spent
on whichever registered points are nearest the camera RIGHT NOW, across
EVERY kind combined). This is the actual answer to "keeping them
performant" at scale -- one budget of ~24 real lights total for the
whole city, not 24-per-kind multiplying every time a new light type is
added.

**`EmissiveAnimator.cs`** (new): the answer to "turn on/off and fade
with hundreds of them on screen at once." A single manager
(`EmissiveAnimatorDriver`, one `Update()` per scene) drives per-instance
emissive color via `MaterialPropertyBlock` -- no per-object Update(), no
per-instance Material (which would break SRP batching). Four behavior
kinds: Steady (no-op -- deliberately does NOT install a property-block
override, since a frozen one-time snapshot would actually be WORSE than
no registration, permanently ignoring the day/night cycle for that
instance), Flicker (windows), Buzz (failing neon tube), Chase (marquee
sequencer). A real correctness bug was caught and fixed while building
this: a `MaterialPropertyBlock` override on a renderer takes priority
over the shared Material's own color for that renderer, so an animated
light would silently ignore `NeonRegistry`'s whole day/night boost cycle
unless `EmissiveAnimator` ALSO folds in the same boost value -- fixed by
publishing it as `DayNightState.NeonBoost` (the exact value
`NeonRegistry.SetBoost` was just called with) and multiplying it into
every animated instance's color each tick.

**Wired end-to-end** (not just designed): `BuildingDresser.DressApartment`
now spawns roughly 2-in-5 floor/face window strips as a new `WindowGlow`
material with an independent per-instance Flicker registration ("house
and apartment windows" -- some lit, some dark, occasionally changing,
not one uniform building-wide glow); the movie-palace landmark's existing
neon (underglow/blade sign/letters) now buzzes independently per strip;
its marquee grew a 10-bulb chaser row using Chase. Office-tower windows
(`DressOffice` uses one tall strip per face, not per-floor) and true
individual window-PANE granularity are explicitly deferred -- real,
separate follow-ups, not silently skipped.

**Not visually verified** -- still no Unity Editor in this environment.
The whole point of shipping the ScriptableObject first is that the next
round of "still not right" tuning is a slider drag, not another commit.

## docs/28 fix: CityLightingProfile crashed on load (2026-07)

Real runtime crash from the creator's own Editor session (the first
Play-mode run of any of this lighting work): `UnityException:
CreateScriptableObjectInstanceFromType is not allowed to be called from
a ScriptableObject constructor (or instance field initializer), call it
in OnEnable instead`, thrown from `CityLightingProfile`'s own static
constructor.

Cause: `public static CityLightingProfile Active = Default;` is a static
FIELD INITIALIZER, which runs as part of the type's `.cctor` the moment
anything touches the class -- and that eagerly called `Default`'s
getter (`CreateInstance<CityLightingProfile>()`) from within static
type-construction, exactly the context Unity's error names. The
existing `Default` lazy-getter pattern was fine in isolation (matches
`SecondaryAttackCatalog`'s own working lazy-CreateInstance idiom); the
bug was specifically triggering it eagerly via a field initializer
instead of from ordinary runtime code.

Fixed by making `Active` a property backed by a plain (uninitialized,
defaults to null) static field, resolving to `Default` lazily in the
getter instead of eagerly in a field initializer:
```
private static CityLightingProfile _active;
public static CityLightingProfile Active
{
    get { return _active != null ? _active : Default; }
    set { _active = value; }
}
```
Grepped every other `CreateInstance` call in the codebase
(`LumenCycleController`'s Volume profile, `SecondaryAttackCatalog`'s
three definitions) -- all already called from safe runtime contexts
(`Start()`/lazy getters invoked at gameplay time), not static
initializers; no other instance of this bug exists.

Not yet re-confirmed against a live Play session from this side (no
Editor access), but this is a real, understood, mechanical fix for a
real reported crash, not a guess.

## docs/28 fix: lights still too big/bright, and nothing was tunable (2026-07)

Creator report: "The lights are too big and too bright on the screen.
Nothing changes when I alter the DynamicLight." The second half is the
important diagnostic -- it says the knob and the symptom were never
connected.

**Two independent causes, both real, both fixed.**

1. **The glowing balls are not the dynamic lights at all.** They're the
   emissive bulb GEOMETRY -- small spheres with an emissive material --
   spread into much larger soft blobs by URP Bloom. A `Light` component
   illuminates OTHER surfaces; it never renders as a ball on screen. So
   changing `DynamicLightBudget`'s light intensity/range could not
   possibly have fixed what was being looked at. The knobs that actually
   target it are emissive brightness and bloom. (The spheres were also
   literally oversized for RTS camera height -- 0.5m across -- now
   ~0.25m, with the roundabout ring and ornate-lamppost globes shrunk to
   match.)

2. **Nothing reachable was tunable.** Two compounding mistakes in the
   previous commit: (a) the profile-driven values (night ambient, night
   bloom, neon-boost ceiling) were BAKED into `_grades` once in
   `BuildGrades()` at city-build time, so even editing a profile asset
   mid-Play did nothing; (b) with no profile asset assigned -- the
   creator's actual situation -- `CityLightingProfile.Default` is a
   runtime-created ScriptableObject that appears in no Inspector at all,
   and `DynamicLightBudget` had had its own `public int Budget` field
   REMOVED in favor of reading the profile, so selecting that component
   showed literally nothing to edit. A ScriptableObject was the right
   idea for authored defaults and the wrong single answer for live
   tuning.

**Fix:** the live knobs are now plain public fields on the two
MonoBehaviours (`LumenCycleController.emissiveScale`/`nightBloom`/
`nightAmbient`, `DynamicLightBudget.budget`/`peakIntensity`/`range`/
`enableRealLights`), read every frame (or every ~0.35s refresh) rather
than baked -- so dragging them in Play mode takes effect immediately.
`ApplyBlend` now blends the authored keyframe toward those live values
weighted by how far into night it is, instead of `BuildGrades` stamping
them in once. The profile asset keeps its role as the AUTHORED DEFAULTS
layer: `RuntimeCityBuilder` calls `ApplyProfile(lightingProfile)` on both
components at build time, and that method early-returns on null -- so an
unassigned profile leaves whatever the creator typed into the component
Inspector alone instead of silently overwriting it with defaults (the
trap the naive "always seed from profile" version would have had).

Added `DynamicLightBudget.enableRealLights` as a diagnostic toggle
specifically so the emissive-geometry-vs-real-light question can be
answered in one click next time rather than by reasoning about it.

Profile defaults also lowered to match the new component defaults
(BulbEmissiveBase 0.45->0.25, MaxNightBoost 1.5->1.0, NightBloom
0.5->0.25, RealLightPeak 0.9->0.7, RealLightRange 8->7) so an assigned
profile can't silently undo this correction.

Still not visually verified from this side (no Editor) -- but unlike the
previous two attempts, the point here is that the numbers no longer need
to be right the first time: every one of them is now a live slider.

## docs/28: force Fixed exposure -- creator asked "autoexposure?" (2026-07)

Good catch, and a real gap: nothing built so far touched URP's actual
`Exposure` volume component at all -- `ColorAdjustments.postExposure`
(which `LumenCycleController` does animate) is a DIFFERENT, manual EV
offset layered on top of whatever the real exposure/metering does, not
the same thing. If the project's URP template scene shipped a default
Volume with Exposure set to Automatic (common in fresh URP template
scenes), it would have been silently active this whole time, invisible
to every knob added so far -- auto-exposure metering a scene this
project just made genuinely dark at night would crank up gain, inflating
every bright emissive point BEFORE Bloom even sees it, independent of
`emissiveScale`/`nightBloom`/anything else.

Fixed by adding an explicit `Exposure` override to
`LumenCycleController`'s runtime-built Volume: Mode=Fixed,
fixedExposure=0, compensation=0, all override-stated. Also raised that
Volume's own `priority` from 0 to 100 so its overrides win over ANY
pre-existing scene Volume regardless of tie-breaking order -- the
authored look should never lose a priority tie to a template default.

Not confirmed from this side whether the project's actual scene had an
Automatic-exposure Volume in the first place (no Editor access) -- but
removing the possibility entirely costs nothing and closes a real gap
either way.

## docs/28: "dragging sliders, toggling lights on/off no effect" (2026-07)

Creator report after the live-tunability fix. Code review confirmed the
wiring itself is correct (emissiveScale/nightBloom/nightAmbient/budget/
peakIntensity/range/enableRealLights are all genuinely read fresh every
frame or refresh cycle, no baking bug) -- the likely cause is a UX trap,
not a logic bug: `LumenCycleController`/`DynamicLightBudget` each
auto-create separate GameObjects at runtime (`"LumenCycleSun"`,
`"LumenCyclePostStack"`, and one `"DynamicLight"` per pooled real light)
that are easy to find in the Hierarchy and easy to mistake for "the
light" -- but none of them carry the tunable fields. Editing them
directly (e.g. a pooled light's own Intensity) gets silently overwritten
by the owning component on its next refresh (`DynamicLightBudget`
repositions/recolors/resizes its whole pool ~3x/second), which reads as
"no effect" even though the drag itself worked for one instant.

The ACTUAL fields live on the `LumenCycleController`/`DynamicLightBudget`
script components themselves, sitting on the `RuntimeCityBuilder`
GameObject (added there by `RuntimeCityBuilder.Start()`), not on any of
their auto-created children.

Renamed every auto-created object defensively so this can't happen
again: `"(auto) Sun -- edit LumenCycleController instead"`, `"(auto)
Post Stack -- edit LumenCycleController instead"`, `"(auto) pooled light
-- edit DynamicLightBudget instead"`.

Also flagged (not yet confirmed) a second, simpler possibility: several
of these fields are weighted by how far into night the cycle currently
is (`nightAmount`) -- testing at midday would show near-zero effect from
`nightBloom`/`nightAmbient`/real-light intensity by design, independent
of any bug. Pointed the creator at the existing `N`-key 20x time-lapse
toggle to reach full night quickly for testing.

Not re-confirmed from this side (no Editor access) -- this is a
plausible, reasoned diagnosis based on the actual object-creation code,
not a guess at new numbers.

## docs/28: Exposure component doesn't exist in URP -- reverted (2026-07)

Real compile error from the creator's Editor: `CS0246: The type or
namespace name 'Exposure' could not be found`, in
`LumenCycleController.cs`.

Root cause: my own mistake, not a project issue. I invented
`UnityEngine.Rendering.Universal.Exposure` in response to the creator's
"does it have anything to do with autoexposure?" question -- URP has NO
general scene-referred auto-exposure/eye-adaptation Volume component at
all (unlike HDRP, which does have one by that name). It compiled clean
against `flightcheck`'s own hand-written Unity/URP stub because I ALSO
added a matching fabricated stub for it in that same commit -- the
harness can only verify internal self-consistency against whatever it
already mocks, it can never catch an invented type, and I stubbed the
one thing that needed catching. Confirmed only when the real Editor
failed with CS0246.

Fixed by reverting: removed the `Exposure`/`ExposureMode` component
creation from `LumenCycleController.BuildVolume()`, the `_exposure`
field, and the matching fabricated stub types from the flightcheck
harness (left a comment there instead, warning against re-adding a stub
for an API without confirming it's real first). The actual, only
exposure-adjacent control URP's Volume stack offers is
`ColorAdjustments.postExposure` -- a manual EV offset, already wired
since Phase 10.1 -- so the autoexposure theory for the brightness bug
doesn't apply at all; there was never an "automatic" mode in play to
disable.

Lesson for this harness going forward: flightcheck compiling clean is
NOT evidence that referenced Unity/URP API surface is real when the
stub for that surface was added in the SAME change as the code using
it -- only that the two are internally consistent with each other. Only
trust a flightcheck pass for URP-specific types when the stub predates
the calling code, or when the type's existence is independently
confirmed (docs, an existing working call site elsewhere in the repo).

## docs/28: bloom knob set to 0 still too large -- was blended, not multiplied (2026-07)

Creator: "some effect but even set to 0 way too large" -- referring to
`nightBloom`. Real bug, found by re-reading `ApplyBlend()`: `nightBloom`
was a value the code blended TOWARD, weighted by `nightAmount` (how far
into night). `nightAmount` decays continuously from 1.0 back down
through the ENTIRE second half of the night phase as it blends onward
toward Dawn -- so `nightBloom = 0` only ever produced true zero bloom at
the single instant `nightAmount` hit exactly 1.0. For most of what reads
as "night" to a player, a real chunk of the old hardcoded per-phase
baseline (0.4 to 1.3 across Dawn/Day/Dusk/Night) was still mixed in
regardless of the field. `emissiveScale` did not have this problem --
it was already a true always-on multiplier on the whole curve -- which
is exactly why it showed "some effect" while the bloom knob showed none.

Fixed by renaming `nightBloom` -> `bloomScale` and changing it to the
same multiplier model as `emissiveScale`: `finalBloom =
Lerp(a.BloomIntensity, b.BloomIntensity, blend) * bloomScale`, applied
at every time of day, never blended toward. 0 now means zero bloom,
always, full stop -- no partial mixing regardless of time-of-day.
Default changed from 0.25 (an absolute target) to 0.3 (a scale on a
curve that already ranges 0.4-1.3), giving a similar effective result at
night while fixing the actual bug.

**Known parallel gap, not yet reported as a problem, so not touched:**
`nightAmbient` has the exact same "blended toward, weighted by
nightAmount" shape as the old buggy bloom field -- setting it very low
will ALSO only approach true darkness asymptotically through the back
half of the night phase, for the same underlying reason. Left as-is
because (a) it hasn't been reported as wrong, and (b) unlike bloom, a
flat always-on multiplier doesn't fit ambient the same way -- ambient
SHOULD stay bright at midday, so "multiply the whole curve" isn't the
right fix shape here the way it was for bloom. If this needs fixing
later, revisit with a construction that reaches a hard target
specifically once nightAmount is unambiguously ~1 (e.g. a steeper
easing curve on nightAmount itself, or gating on the Night phase
directly rather than the LampBoost-derived blend), not a straight port
of the bloomScale fix.

## docs/28 follow-up: "real lights under market-stall-canopy, ornate-lamppost-pole" too large (2026-07)

Creator clarified the previous "even set to 0 way too large" report: the
objects showing the symptom are the REAL dynamic lights (Tier 2, actual
`Light` components from `DynamicLightBudget`) near these two street-
furniture props, not (only) bloom on the bulb geometry.

Checked the geometry before touching anything: hex centers are exactly
20m apart (`HexCoord.HexMeters`, packages/citygen-core/src/HexCoord.cs:27),
and `RoadDresser`'s per-hex furniture offset is only ±3m along the road
axis, so two separate furniture slots can never end up closer than ~14m
apart -- beyond the old 7m light range. So this wasn't cross-prop bleed
between a lamppost's hex and a market stall's hex.

Simpler cause: `DynamicLightBudget`'s pooled `Light` is a raw Unity Point
light. It illuminates ANY nearby geometry within `range` -- the ground,
the pole itself, a market stall's canopy surface if it's anywhere close
-- not just the prop that registered the glow point. A 7m range is a 14m-
diameter dome, which reads as a big soft wash across everything nearby
rather than a contained "pool of light AT the fixture." Same "small
source, big footprint" shape as the bloom bug, just via real-time
lighting instead of post-process.

Fixed by tightening the default: `DynamicLightBudget.range` and
`CityLightingProfile.RealLightRange` 7f -> 3f. Left `peakIntensity`
alone -- the report was specifically about size, not brightness, and
shrinking range while holding intensity constant naturally reads as a
*more* concentrated pool near the fixture (steeper falloff over a
shorter distance), which is the "real pool of light" look the code
comments already describe as the goal.

## docs/28 correction: range=3f broke ground pooling entirely (2026-07)

Direct regression from the previous range-tightening commit (7f -> 3f).
The ornate lamppost's globes are mounted 5.9m up (RoadDresser.cs "case 6"
globeSpot). `DynamicLightBudget.range` is a straight-line radius from the
light's OWN position, not a ground-projected pool size -- a light with
3m of range mounted 5.9m up cannot reach the ground AT ALL. Screenshot
evidence confirmed it: no lit patch on the ground, and the pole itself
rendered pure black (near-zero night ambient, nothing else was lighting
it either). The prior commit's math check (hex spacing 20m, furniture
offset +/-3m => no cross-prop bleed possible under 7m) was right, but
the conclusion drawn from it -- "so just make the dome smaller" -- didn't
account for the light needing to physically reach down from its own
mount height first.

Restored `range` to 8f (both `DynamicLightBudget` and
`CityLightingProfile.RealLightRange`), comfortably above the tallest
street fixture's mount height, leaving a real ground pool radius (~5.4m
diameter for the globes specifically: sqrt(8^2-5.9^2)). If the on-screen
glow still reads as oversized after this, that's the bloom knob
(`bloomScale`) or the emissive geometry itself, not this field -- range
governs ground reach, not the screen-space halo size around the source.

Also fixed the reported console warning ("Realtime indirect bounce
shadowing is only supported for Directional") by explicitly setting the
pooled lights' `lightmapBakeType = Realtime` at creation -- a fresh
`AddComponent<Light>()` defaults to Mixed bake mode, which asks for
GI/baked participation these repositioned-every-refresh runtime lights
were never going to meaningfully provide.

## docs/28 root cause found: Additional Lights set to Per Vertex, not Per Pixel (2026-07)

The real culprit behind "no lit patch on the ground, pole solid black,"
after `bloomScale`/`range` fixes made zero visible difference: both URP
Pipeline Assets (`Assets/Settings/PC_RPAsset.asset` and
`Mobile_RPAsset.asset`) had `m_AdditionalLightsRenderingMode: 1`
(PerVertex) rather than `2` (PerPixel). Every prop this city builds is a
raw primitive (Cube/Cylinder/Sphere via `SpawnPrim`, no subdivision) --
a ground quad might have 4-8 vertices, a pole cylinder vertices only at
its top/bottom rings. Per-vertex lighting evaluates a light's
contribution ONLY at mesh vertices and interpolates across faces, so a
small point light positioned mid-face on this kind of low-poly geometry
can produce visibly ~zero illumination across most of the surface even
though the light itself is completely correctly configured -- exactly
matching "the light appears not to exist" despite `range`/`intensity`
both being right. This is why two separate code-side fixes (`range`
7->3->8, `bloomScale`) made no visible difference to the ground
pool/pole blackness specifically, while `bloomScale` DID visibly change
the halo (bloom is a post-process, unaffected by this setting).

Fixed both Pipeline Assets to PerPixel. Deliberate trade-off, flagged
here rather than silently: PerPixel additional lights cost more on
`Mobile_RPAsset` specifically (that's presumably why PerVertex was
chosen there originally) -- correctness (the whole `DynamicLightBudget`
system being visually inert otherwise) was prioritized over the
unmeasured mobile performance cost. Revisit if mobile profiling shows a
real problem; the fallback would be capping the additional-lights budget
lower on mobile specifically rather than reverting to PerVertex, since
PerVertex effectively defeats this whole feature on primitive geometry.

## docs/28: Refresh() crash when real lights disabled (2026-07)

Creator-found crash, reproducible: toggling `enableRealLights` off (or
dragging `budget` to 0) threw `ArgumentOutOfRangeException` in
`DynamicLightBudget.Refresh()` every 0.35s refresh, forever. Cause: with
`activeBudget == 0`, the "is this closer than my current worst pick"
fallback ran on the very first registered glow point while `_pickedSq`
was still empty, indexing `[0]` into an empty list. Fixed by only taking
that branch when `_pickedSq.Count > 0` -- with nothing picked yet (which
is exactly the `activeBudget == 0` case), there's nothing to replace, so
the point is correctly just skipped.

Side note for future debugging: disabling the sun and checking whether
shadows shift when moving a prop around is NOT a valid way to check
whether one of these pooled lights exists -- they are deliberately
created with `shadows = LightShadows.None` (never shadow casters, a
performance choice for budget fill lights), so absence of shadow change
is expected either way and proves nothing either direction.

## docs/28: RenderSettings.ambientLight was a no-op -- ambientMode never set (2026-07)

Found while chasing "objects still black, no light I can see" after the
Per-Vertex fix. `RenderSettings.ambientLight` (LumenCycleController's
whole day/night ambient blend, including `nightAmbient`) only has any
effect when `RenderSettings.ambientMode == AmbientMode.Flat`. A fresh
Unity scene defaults to `AmbientMode.Skybox`, and nothing in this
codebase ever set it to Flat -- meaning every ambient value this
controller has ever computed was silently discarded, and real scene
ambient was instead coming from the skybox material, which (for a
procedural sky tied to the Sun) can go dark or behave unpredictably the
moment the directional light is disabled or pushed below the horizon for
Night -- exactly the conditions the creator was testing under.

Fixed: `LumenCycleController.Start()` now sets
`RenderSettings.ambientMode = AmbientMode.Flat` once. Note this doesn't
by itself explain a still-invisible REAL LIGHT (ambient mode only
affects the ambient term, not a Point light's own direct contribution,
which shouldn't depend on it) -- still need to confirm via the Hierarchy
whether `DynamicLightBudget`'s pooled Light GameObject actually exists
and what its live Intensity/Range/enabled values are, to know if the
remaining "no visible real light" symptom is a separate bug or was
actually fixed by the Per-Vertex correction and just wasn't visible
against a still-broken (Skybox-mode) ambient background.

## docs/28 ROOT CAUSE of the black props: double-winding zeroed every normal (2026-07)

The actual reason `ornate-lamppost-pole` and `market-stall-canopy`
rendered as pure black silhouettes -- through every previous fix
attempt, and even while sitting inside a blown-out white pool of light.
Nothing to do with the lights at all.

`ProceduralMeshKit.Tri()` emitted every face in BOTH triangle windings
(`a,b,c` and `a,c,b`). That was deliberate -- the class comment explains
it as belt-and-braces against a winding-order mistake, since this
environment has no Editor to visually catch an inside-out face. But
`Mesh.RecalculateNormals()` sets each vertex normal to the average of
the face normals sharing it, so with every face present in both
windings, each contributes +N and -N, which cancel to EXACTLY ZERO. A
zero normal makes `dot(N, L)` zero for every light, so the surface is
pure black under any lighting whatsoever -- unfixable by range,
intensity, budget, bloom, ambient, or render mode. This is why only
these two props were affected: everything else in the city is a stock
Unity primitive with correct normals.

Fixed by emitting each face once, and replacing the double-winding
safety net with `FaceOutward()` -- a pass that re-winds any triangle
whose normal points toward the mesh centroid. That's strictly better
than the old trick: it gives the same "can't get winding wrong without an
Editor" guarantee, keeps normals intact, and halves the triangle count.
Exact for convex shapes (both of these are); a concave mesh would need
real authored winding. It deliberately uses the same cross product
`RecalculateNormals` does, so the check is self-consistent with the
normals actually generated.

Verified numerically in the scratchpad flight-check harness rather than
by eye (no Editor here): the old double-wound frustum yields 22/22 zero
normals, the fixed one 0 zero and 0 inward-facing, triangle count 80 ->
40. The harness's `Mesh.RecalculateNormals`/`Vector3` stubs had to be
given real implementations first -- they were `{ }` / `return 0f`
no-ops, which would have made any such test silently vacuous.

**LabMeshBuilder checked, not affected:** it assigns `mesh.normals`
explicitly from creature-mesh chunk data and never calls
`RecalculateNormals`, so creature geometry never had this bug despite
ProceduralMeshKit's comment citing it as the pattern it copied.

### Also: one real light per lamppost, not one per globe

The ornate lamppost registered a glow point for EACH of its three globes,
0.5m apart -- so three real lights stacked on the same patch of
pavement (~3x intensity, a real contributor to the blown-out white
pools) and three of the city-wide budget's slots went to a single
fixture. Now registers one. All three globes still glow; that's their
emissive material, independent of light promotion.

## docs/28 follow-up: single-winding fix made the props vanish entirely (2026-07)

Direct regression from the previous commit. Fixing the double-winding
zero-normal bug (switching to single, "outward" winding via
`FaceOutward()`) reintroduced exactly the risk double-winding existed to
prevent in the first place: whether Unity's front-face culling agrees
with what this code computes as "outward" can't be verified without an
Editor to look at, and it turned out to disagree -- the creator reported
`ornate-lamppost-pole` and `market-stall-canopy` had gone from rendering
black to not rendering AT ALL (back-face culled).

Rather than gamble a second time on getting the exact cross-product
handedness right by reasoning alone (unverifiable in this environment),
made it robust to being wrong either way: `PropLibrary.Spawn` now clones
the material and sets `_Cull` to `Off` specifically for meshes that came
from a REGISTERED builder (i.e. only the ProceduralMeshKit-driven props,
never the primitive fallback path, which doesn't need it). Renders
correctly regardless of which way the winding actually landed, while
keeping the correct, non-degenerate, consistently-outward normals from
the previous fix for correct lighting.

Deliberately a per-instance clone, not applied to the shared cached
material -- `PoleMetal()`/`AdRed()`/`SignBlue()` etc. are also used by
ordinary, correctly-wound stock primitives elsewhere (hydrant posts,
billboard posts, other cylinders), which don't have this problem and
shouldn't pay the double-sided fill-rate cost. Only 1-3 procedural-mesh
prop instances exist per scene, so the extra material instances are
negligible.

## docs/28 root cause found: street furniture had no wall-clearance check at all (2026-07)

Root-caused the last open row in the bug table -- "I believe you put the
lights in the wall," left unresolved because it was raised while the
props were still invisible (the previous row's culling bug), so hard to
judge whether it was a real position bug or just confusing to evaluate
against invisible geometry.

It's real, and it isn't limited to the two `PropLibrary` props -- it hits
`RoadDresser`'s entire street-furniture switch (streetlight, telephone
pole, hydrant, trash can, billboard, ornate lamppost, market stall),
because every one of them spawns at the same `propSpot`, and nothing in
`RoadDresser.cs` ever queried a building's position. The curb offset
(`curbLineOffset`, derived from `RoadWidth`) was pure arithmetic on the
assumption that a neighboring building is always at least a fixed
distance away -- an assumption two different things could silently
break:

- `CardinalAnchor`'s road-straightening nudge (added so a north/south
  street renders on one straight world-x line instead of the hex grid's
  natural sawtooth) shifts a vertical street's whole hex up to
  `HexMeters/4` (5m) along world X -- the SAME axis a north/south
  street's sideways curb offset uses. The two aren't correlated (the
  nudge's sign comes from hex row parity, the curb offset's sign from an
  independent hash bit), so on about half of affected hexes they add
  instead of cancelling: `5m nudge + 6.2m residential curb offset =
  11.2m`, past an 18m-wide building's 11m near-wall distance (`HexMeters`
  20m hex spacing minus `BuildingDresser.Half` 9m) -- the fixture lands
  inside the building. On an arterial street (9.45m curb offset) the
  overshoot is worse (14.45m, 3.45m inside the wall).
- Independent of the nudge, an EAST/WEST street's own arterial curb
  offset (9.45m) can exceed the raw north/south row-to-row gap to a
  building (~17.32m spacing, 8.66m half, minus the 9m building half =
  8.32m to the wall) -- an arterial street's furniture can end up just
  inside a north/south-adjacent building's wall with no nudge involved
  at all.

Fixed by checking the actual building position instead of trusting the
fixed margin. `RoadDresser.Build` now collects every building footprint
hex into a set (`city.Buildings` was already available there, unused for
this). A new `ClearLateralOffset` clamps the sideways placement offset:
it looks up whether the hex directly across the curb (in the same
cardinal direction the furniture is being offset toward) is a building
hex, and if so, computes the real distance to that building's world
position and clamps the offset to stay `BuildingDresser.Half + 1.5m`
clear of it (exposed `BuildingDresser.Half`, previously private, since
`RoadDresser` needs the same number rather than a second guess at it).
Worst case (a building hard up against the curb on a dead-end), furniture
slides toward the road centerline instead of sitting at the curb --
never into the wall, which is the failure mode that mattered. Not
visually confirmed (no Editor in this environment, same standing caveat
as every other row in this table) but reasoned directly from the actual
constants in play (`HexCoord.HexMeters`, `BuildingDresser.Half`,
`RoadDresser`'s own width/offset constants), not from a guess.

## docs/28: temporary diagnostic -- peakIntensity widened + defaulted high (2026-07)

Creator ran the wall-clearance/culling fixes and still can't see any
real lights, and asked to crank intensity to at least 40-120 as a blunt
test of whether it's simply too dim to notice versus something not
rendering at all. Couldn't actually do that before this change --
`peakIntensity` was `[Range(0f, 5f)]` on both `DynamicLightBudget` and
`CityLightingProfile`, so the Inspector slider itself capped any value
at 5 regardless of what was typed in.

Widened both to `[Range(0f, 150f)]` and bumped the default 0.7f -> 80f
on both (kept in sync so assigning a profile asset later wouldn't reset
the diagnostic value). This is intentionally NOT a good final look --
the point right now is maximum visibility for the diagnostic, not
balance. Turn back down toward 0.5-1.5 once a real light is confirmed
visible at all.

If lights are STILL invisible even at intensity ~80, that rules out
"just too dim" definitively and points at something else entirely --
worth checking next: is DynamicLightBudget actually promoting THIS
prop's glow point (nearest-N budget competition), or is the Light
GameObject disabled/inactive, or is there a camera/rendering-layer
mismatch not yet considered.

## docs/28: intensity diagnostic confirmed lights render; backed off from blown-out default (2026-07)

Creator confirmed real lights ARE visible with `peakIntensity` at the
deliberately blown-out diagnostic value of 80 -- rules out "not
rendering at all" for good. Also confirms row 7 (the culling fix for
ornate-lamppost-pole/market-stall-canopy) actually worked: the props are
visible, not back-face culled.

No config bug found to explain why the ORIGINAL 0.7 default was
apparently invisible -- checked specifically for a Physical Light Units
mismatch (would show up as an intensity/lumens scale gap exactly like
this) on the Pipeline Asset; not present. Simplest remaining explanation
is 0.7 was just genuinely too dim against this scene's ambient/exposure,
not a bug.

Backed `peakIntensity` off from 80 (diagnostic extreme) to 12 (untuned
middle-ground starting point) on both `DynamicLightBudget` and
`CityLightingProfile`. Explicitly NOT claiming 12 is correct -- nobody
has tested intermediate values between the old invisible 0.7 and the
working-but-blown-out 80, so the actual "looks right" number is still
unknown. This field is read live every refresh specifically so it can
be nudged in Play mode without another code round-trip; that's the
fastest path to the real number now that end-to-end visibility is
confirmed working.

## docs/28: "lights" now hold through night, fade off in daytime (2026-07)

Creator direction: "make the lights fade a lot faster and hold for
duration of the night, then fade off during the daytime." The OLD
`nightAmount` (drives real-light intensity + ambient darkness) came
from the SAME continuous per-phase cross-fade `ApplyBlend` uses for
sun/fog/color-grading -- which never actually held steady: it kept
drifting toward the next phase's value across the ENTIRE current phase
(this is literally the mechanism `bloomScale` was invented to route
around earlier this session).

Added `ComputeNightIntensity`, a dedicated trapezoid independent of that
per-phase blend: fast ease-in during the first 25% of Dusk (30s phase ->
7.5s fade, ~4x quicker than the old full-phase ramp), flat 1.0 through
the rest of Dusk + all of Night + all of Dawn (real streetlights stay on
through early morning, not just "night" by the clock), an eased fade-out
during the first 50% of Day, flat 0.0 for the rest. `nightAmount` and
`neonBoost` (bulb/window/sign brightness) both now read from this --
`neonBoost` switched from the old 4-stop Dawn/Day/Dusk/Night lerp to a
2-stop Day/Night lerp weighted by the same trapezoid, so the glow and
the real lights snap on/off together instead of drifting apart. Sun
color/intensity/elevation, fog, and post-processing color-grading
(exposure/saturation/contrast/vignette/film grain) deliberately kept the
OLD continuous 4-stop cross-fade unchanged -- the ask was specifically
about "the lights," not the whole day/night mood; bloom similarly
untouched (still tracks the old per-phase curve, scaled by
`bloomScale`) unless a follow-up asks for it too.

Verified numerically against the actual compiled method (via reflection
-- it's private) rather than by eye: sampled the boundary and midpoint
of every hold/fade segment across the full 2400-tick cycle, all match
the intended shape. This also surfaced a real gap in the flightcheck
harness itself: `Mathf.SmoothStep`/`Clamp01`/`Clamp`/`Lerp` (the single
most-called Mathf function in this whole file) and several other Mathf
members were STILL `return 0f;` no-op stubs, left over from before this
session started fixing them one call at a time as each was needed.
Fixed the whole remaining batch (`Lerp`, `SmoothStep`, `Clamp01`,
`Clamp`, `Sign`, `Exp`, `InverseLerp`, `MoveTowards`, `Atan2`,
`DeltaAngle`, `Round`) rather than just the ones this specific check
needed, since a stub silently returning 0 makes any future numeric
verification of ANYTHING touching it meaningless without warning. Harness-
only change, not part of the shipped game -- lives in the session
scratchpad, not the repo.

## docs/28: overhanging streetlight -> Spot light, pointing down, 48deg cone (2026-07)

Creator direction: "Change the overhanging street lights to spotlights,
pointing down at the road. Make cone angle 48 degrees wide." "The
overhanging street lights" is unambiguous -- exactly one fixture in
RoadDresser's furniture roster has an arm reaching OVER the road
(case 0, "streetlight: pole, arm reaching back over the road, warm
bulb"); every other glow point (ornate lamppost globes, windows, neon,
roundabout bulb) isn't aimed at anything and stays a Point light.

`GlowPointRegistry`'s `Point` struct and `Register()` now carry a
`LightType` (defaults to `Point`, so every existing call site is
unchanged without editing it). `DynamicLightBudget` re-applies
`pooled.type` from the registered point's `LightType` every refresh
(not just at pool-creation time), since which registered point lands on
which pooled slot reshuffles as the camera moves -- a slot that was
Point last refresh can become Spot this refresh. For Spot-type slots,
also sets `spotAngle` from a new live `spotConeAngle` field (default
48, matching the ask) and rotates the pooled light to
`SpotDownRotation = Quaternion.Euler(90, 0, 0)` -- Unity's
forward-is-local-+Z convention, the SAME rotation convention
LumenCycleController's sun already relies on and documents ("an X-euler
of 90 points straight down").

`RoadDresser`'s case 0 now registers with `LightType.Spot` instead of
the implicit default.

Verified numerically (no Editor here): built out a real `Quaternion`
implementation in the flightcheck harness (`AngleAxis`/`Euler`/multiply/
rotate-vector -- were `default(Quaternion)` stubs, an all-zero quaternion
isn't even a valid rotation) and confirmed
`SpotDownRotation * Vector3.forward` actually comes out to (0, -1, 0),
not up or sideways. Also confirmed via reflection: a plain
`Register(t, color)` call still yields `LightType.Point` (so every
untouched call site is unaffected), an explicit
`Register(t, color, LightType.Spot)` call yields `LightType.Spot`, and
`spotConeAngle` defaults to exactly 48. Not yet seen lighting up an
actual road in a real render.

## docs/28 round 2: spotlight brightness, true day/night on-off, per-window occupancy (2026-07)

Creator direction, four parts in one message:

1. **"the down facing spotlight needs to be a lot brighter to read
   properly."** Not a bug -- a Spot's cone concentrates the same
   `intensity` into a narrower solid angle than a Point light, and
   Unity's spot attenuation reads noticeably dimmer per-pixel at a
   wide-ish 48 degree cone than an omnidirectional Point at an equal
   value. Added `DynamicLightBudget.spotIntensityMultiplier` (default
   5), applied only to Spot-type promoted lights on top of
   `peakIntensity` -- Point lights (windows, the ornate lamppost, etc.,
   which weren't the complaint) are unaffected. Untuned guess, same
   status as `peakIntensity` itself.

2. **"ALL the lights should turn off during the day."** Two separate
   non-zero floors were quietly preventing this: `DynamicLightBudget`'s
   promoted-light `intensity` floor was 0.02 (near-zero, not off), and
   `LumenCycleController`'s `neonBoost` floor was Day's own authored
   0.35 ("barely visible against daylight" -- the ORIGINAL, pre-this-
   effort design intent, now explicitly superseded). Both now `Lerp`
   from a hard `0f`.

3. **"Ramp on quickly, hold for the duration of the night, and turn
   shortly after dawn"** -- a correction to the PREVIOUS round's shape
   (docs/28 row 10), which held through all of Dawn and faded out
   gradually across the first half of Day (45s). That wasn't "shortly
   after dawn." `ComputeNightIntensity` reshaped: the fade-out now
   happens over the first 35% of DAWN itself (~10.5s, moved from Day),
   with Day held at a flat hard 0 the entire phase, no fade there at
   all anymore. The Dusk fast-ramp-in (first 25%, ~7.5s) and the Night
   hold are unchanged.

4. **"The building can turn on randomly approaching night time, as if
   real humans were in the room and realize it's getting too dark. The
   same goes for late at night... people going to bed... this can vary
   greatly. BUT not all light go off."** Mid-turn, the creator clarified
   this should be "not real lights but lit texture like the bulbs" --
   i.e. purely emissive/`MaterialPropertyBlock`, never
   `GlowPointRegistry`/`DynamicLightBudget`. Since `nightAmount` now
   holds perfectly flat through the whole night (point 3 above), it
   structurally can't distinguish "just got dark" from "3am" -- a
   per-window bedtime needs its own clock. Added
   `DayNightState.CycleProgress` (raw 0..1 position, doesn't hold flat)
   and a new `LightBehaviorKind.Window` in `EmissiveAnimator`: each
   registration gets its own randomized (deterministic from the
   existing per-window `seed` -- same "same seed always furnishes the
   same city" approach used everywhere else in this codebase, not
   `UnityEngine.Random`) arrival time in [37.5%, 75%) of the cycle and
   bedtime in [75%, 98%) of the cycle, dark outside that span, lit
   (with the existing Flicker-style wobble) inside it, ~2.4s smoothstep
   transitions at the edges instead of a hard pop. 15% of windows are
   `AlwaysOn` and skip the bedtime check entirely. `BuildingDresser`'s
   window registration switched from `Flicker` to `Window`.

Verified numerically against the actual compiled/reflected code, not by
eye (still no Editor in this environment): re-sampled every hold/fade
boundary and midpoint of the reshaped `ComputeNightIntensity` against
the new Dawn-fade/Day-hold shape; registered 400 Window entries and
confirmed every arrival/bedtime fraction landed in its intended range
with correct ordering, the AlwaysOn rate came out to 15.25% against a
15% target, and the occupancy gate function reads 0 outside a window's
span, 1 inside it, and permanently 1 for an AlwaysOn entry regardless of
cycle position.

**Second harness gap found and fixed while writing this check** (same
pattern as the SmoothStep/Clamp01/Lerp gap found earlier this session):
`Mathf.Floor`/`FloorToInt` were STILL `return 0f;`/`return 0;` no-op
stubs, which silently broke `EmissiveAnimator`'s own `Frac()` helper
(`v - Floor(v)`, used by Flicker/Buzz/Chase AND this round's new Window
hashing) -- the first AlwaysOn-rate check came back as exactly 0/400
before this was caught, not because the shipped logic was wrong, but
because `Frac(x) == x` (never actually fractional) under the broken
stub made the `< 0.15` comparison compare against values like 7-98
instead of a genuine 0..1 fraction. Fixed the whole remaining batch
(`Floor`, `FloorToInt`, `Approximately`, `RoundToInt`, `CeilToInt`) while
in there, same reasoning as before: a silently-wrong stub makes any
future check meaningless without warning. Harness-only, lives outside
the repo.

None of this round's four changes have been seen in a real render yet.

## docs/28: roads weren't reflective at all -- no material ever set Smoothness (2026-07)

Creator confirmed the lighting timing (docs/28 row 12) is working, then:
"make the roads more reflective, I can barely see the lights on them."

Root cause: nothing in RoadDresser.cs's `M()`/`MTextured()` material
helpers ever set `_Smoothness` or `_Metallic` on ANY material, ever --
every prop in the city, including the road surface, has been rendering
at the URP/Lit shader's own default smoothness (~0.5, a middling
matte-ish response) since the very first version of this file. A
mid-smoothness surface spreads whatever light hits it into a broad, dim
specular response rather than concentrating it into a tight, bright
highlight -- so even with the real streetlights now genuinely bright
(docs/28 row 11's `spotIntensityMultiplier`) and correctly positioned,
they were never going to read as a visible GLINT on the pavement itself,
independent of how bright the light source is.

Added an optional `smoothness` parameter to `MTextured()` (sentinel
`-1` = leave the shader default alone, so `PoleMetal()`/`ChromeTrim()`
-- the two other callers -- are untouched) and set `Asphalt()`
specifically to 0.92 (high, tight highlight). Metallic deliberately left
alone: wet asphalt's shine comes from a thin water film acting as a
dielectric coating, not the road being literal metal -- boosting
Metallic would tint reflections with the road's own dark albedo color
instead of reading as a clean glint.

Not numerically checkable the way earlier rows were (this is a shader
property with no meaningful pass/fail math -- confirmed the code path
compiles and passes the right constant, nothing more). Purely visual;
needs an actual look in the Editor.

## docs/28 row 14 correction: road too shiny, recolored instead (2026-07)

Creator: "the road is too shiny put it back to the original setting.
Change the road from black to a textured mid dark gray, that should
help us see the light better." Reverted `Asphalt()`'s smoothness
override (0.92 -> unset, back to shader default, matching every other
material in the file) and changed its base color from near-black
(0.17/0.17/0.18) to a mid dark gray (0.35/0.34/0.36) instead -- contrast
against the road color, not surface glossiness, is the mechanism now
being relied on to make a warm streetlight glow visible.

## docs/28 row 16: fog now dims + diffuses real lights, thicker overall (2026-07)

Creator: "I think the fog isn't transmitting or limiting the
transmission of the lights." Checked the actual scene file
(`SampleScene.unity`) before touching anything: `m_FogMode: 3`
(ExponentialSquared) and `m_AmbientMode: 0` (Skybox) are both already
correctly serialized -- NOT the same class of bug as the earlier
ambientMode fix (that one really did default to something that silently
discarded tuned values; this one doesn't). The actual gap: basic
`RenderSettings` fog fades a rendered SURFACE's color toward the fog
color based on camera distance -- it has no concept of a light SOURCE's
own reach, so it structurally cannot make a lamp look "swallowed" by fog
regardless of how fogDensity/fogColor are tuned. That's a real,
explainable limitation of the technique, not a bug to hunt further.

Asked a clarifying question rather than guess at another numeric fix
blind (two of this session's earlier guesses already needed correcting)
-- creator answered "both [throttle lights AND thicken fog], plus a
diffusing glow like real lights in the fog, and give me max/min settings
for the overhang streetlights vs all others."

Implementation:
- `DynamicLightBudget`'s old single `peakIntensity` (shared by every
  Point-type light) + `spotIntensityMultiplier` (a flat Spot factor)
  replaced by four explicit fields: `pointIntensityMax`/
  `pointIntensityMin`/`spotIntensityMax`/`spotIntensityMin`. "Overhang
  streetlights vs all others" maps directly onto the EXISTING Point/Spot
  split (`GlowPointRegistry.LightType`) -- the overhanging streetlight is
  the only Spot; the ornate lamppost, roundabout bulb, and windows are
  all Point. No new categorization infrastructure needed.
- New `fogDimReferenceDensity`: current `RenderSettings.fogDensity`
  divided by this and clamped 0..1 gives `fogT`, used to `Lerp` each
  type's ceiling from Max (clear) toward Min (heavy fog) -- Min stays
  above 0 deliberately, since real fog dims a light's core, it doesn't
  extinguish it.
- New `LumenCycleController.fogGlowBoost`: `1 + fogDensity *
  fogGlowBoost`, multiplied into the existing `bloomScale`-scaled bloom
  curve -- the "diffusing glow" half, since bloom (already the mechanism
  that turns a small bright point into a soft spread halo) is the
  closest approximation this toolset can give to real light scattering
  without a volumetric rendering system.
- `FogDensity` bumped ~1.5-1.7x across all four phase grades (Dawn
  0.006->0.010, Day 0.003->0.005, Dusk 0.008->0.014, Night 0.014->0.022)
  for "thicker overall" -- chosen to stay readable (Night's new value
  puts ~70% fog blend at a 50m view distance under Exp2 falloff, not a
  total whiteout).
- All new fields mirrored onto `CityLightingProfile` (`RealLightPoint/
  SpotIntensityMax/Min`, `FogDimReferenceDensity`, `FogGlowBoost`) for
  the same seed-defaults-at-build-time pattern every other tunable here
  already follows.

Mid-turn, before this was finished, the creator ALSO reverted the
previous "roads more reflective" fix: "the road is too shiny put it
back to the original setting. Change the road from black to a textured
mid dark gray, that should help us see the light better." Reverted
`Asphalt()`'s 0.92 smoothness override (back to shader default) and
recolored from near-black (0.17/0.17/0.18) to mid dark gray
(0.35/0.34/0.36) -- contrast against the road's own color is now the
mechanism relied on, not surface glossiness. Handled first, as its own
commit, before returning to the fog work.

Verification for the fog/streetlight math: hand-checked the algebra
(fogT=0 -> ceiling=Max; fogT=1 -> ceiling=Min), which composes entirely
from `Mathf.Lerp`/`Clamp01`, already verified for real via the harness
stub fixes earlier this session -- no new dedicated numeric test written
for this round, since `Refresh()` (where the new math lives) is a
private instance method needing a live Camera/GameObject pool to
exercise meaningfully, unlike the earlier trapezoid/window-occupancy
logic which was cleanly isolable as pure static methods. Nothing in this
round has been seen in a real render yet.

## docs/28 row 17: all street lights 90% brighter (2026-07)

Creator: "it's better. Make all the street lights 90% brighter." Flat
x1.9 across both Point and Spot Max/Min (row 16's split already covers
"all the street lights" as its two halves): pointIntensityMax 12->22.8,
pointIntensityMin 4->7.6, spotIntensityMax 60->114, spotIntensityMin
18->34.2. Mirrored onto CityLightingProfile. Simple numeric scale, no
new math to verify beyond the arithmetic itself.

## SimpleCameraRig: Shift+Up/Down moves camera vertically, floor-clamped (2026-07)

Creator: "if I press shift+down arrow or up arrow. Move the camera Down
or Up. Do not allow moving below the ground." Up/down arrows already
drove forward/back pan; while Shift is held they're now excluded from
pan and instead move the camera along world Y at a new
`verticalMoveSpeed` (40 units/sec), clamped to `[MinHeight, MaxHeight]`.

Extracted `MinHeight`/`MaxHeight` (8f/400f) as named consts shared with
the EXISTING zoom clamp, which previously had its own inline `8f`/`400f`
literals -- both ways of changing camera height now agree on the same
floor/ceiling instead of two copies that could drift apart. `MinHeight`
is 8, not 0: ground is y=0 (docs/18, "feet stand on y=0"), and a camera
sitting exactly on the ground plane is degenerate (near-zero effective
FOV) -- 8 was already the zoom code's own established floor before this
change, reused rather than inventing a second number for "how low can
this camera usefully go."

## docs/28 row 18: bloom scatter/threshold wired up + maddr-lighting-system skill (2026-07)

Creator: "I want the lights to truly pop, bright and diffuse through the
fog. use something like this: [Medium article, 'Creating Light-Source
Fog In Unity HDRP']. add it to your lighting skill." Fetched the article
(direct fetch 403'd, corroborated via search results instead) to confirm
the exact mechanism before responding: HDRP's `HDAdditionalLightData`
"Volumetric" light toggle + a Fog Volume override with volumetric fog
enabled, or a `LocalVolumetricFog` box component. Both are HDRP-only --
`UnityEngine.Rendering.HighDefinition` namespace, no URP equivalent.
This project is confirmed URP (CLAUDE.md, `ShaderUtil.
FindRenderableShader()`, the whole Volume stack in LumenCycleController).
Flagged the mismatch rather than attempting a literal port; creator
confirmed "stay in URP tho."

Found a real, useful lever within URP's own Bloom component instead:
`Bloom.scatter` (spread/softness of the blur -- the actual "diffuse"
knob, distinct from `intensity`) had never been touched anywhere in this
codebase. `Bloom.threshold` was WORSE than untouched -- its
`overrideState` was set to `true` since Phase 1, but the VALUE itself
was never assigned, so it silently rode URP's own default (~0.9) the
entire time. Same "flag/mode set, value never actually driven" shape as
the ambientMode bug (docs/28 row 5b) and the Per-Vertex rendering-mode
bug (row 5) -- worth naming as its own recurring pattern now that it's
shown up a third time.

Added: `bloomScatter` (base scatter) + `fogDiffusionBoost` (fog-density-
driven additive boost to scatter, clamped to 1) for "diffuse through the
fog"; `bloomThreshold` (explicit, lower than URP's own default) for
"pop." All three wired into `ApplyBlend()`'s per-frame Bloom assignment
and mirrored onto `CityLightingProfile`.

Per the "add it to your lighting skill" request: created `.claude/
skills/maddr-lighting-system/SKILL.md`, mirroring the existing
`maddr-aesthetic-preferences` skill's format/depth. Captures (1) the
URP-only pipeline boundary and this specific HDRP-article mismatch as a
concrete example of how to handle a reference technique from the wrong
pipeline, (2) the two-tier light-budget architecture, (3) the actual
"light source fog" mechanism now in place, (4) the recurring "property
never explicitly set, silently used SOME default" bug pattern across
ambientMode/Per-Vertex/double-winding-normals/this-round's-threshold as
one named class of bug to check for first, and (5) this session's
verification discipline (flightcheck harness, catching broken stubs,
asking rather than guessing a third number). Points back to docs/28 §0.5
as the source of truth rather than duplicating its full detail.

Reasoned, not numerically checkable (pure Bloom/shader parameters, no
meaningful pass/fail math) -- not yet seen in a real render.

## docs/28 row 19: URP-VolumetricFog-ForwardPlus evaluated, kept as documented option (2026-07)

Creator: "study this too. https://github.com/mseonKim/URP-VolumetricFog-
ForwardPlus see if it can be used." Fetched the repo (direct README
fetch 403'd, corroborated via search + raw file fetches of LICENSE,
package.json, FPVolumetricFog.cs, FPVolumetricFogVolume.cs instead) and
cross-checked against this project's actual serialized settings before
answering:

- `Assets/Settings/PC_Renderer.asset`: `m_RenderingMode: 2` (ForwardPlus)
  -- the package's hard requirement, already satisfied on the PC target.
- `Mobile_Renderer.asset`: `m_RenderingMode: 0` (plain Forward) -- this
  package would be PC-only.
- package.json requires `com.unity.render-pipelines.universal` >=14.0.8;
  this project's manifest.json has 17.3.0. Has a RenderGraph path "only
  implemented for Unity 6" -- this project is 6000.3.13f1.
- License: Unity Companion License, compatible with this project.
- Integration surface: `FPVolumetricFog` (a `ScriptableRendererFeature`,
  one serialized field) needs Editor-side registration on
  `PC_Renderer.asset` via the Inspector's "Add Renderer Feature" button
  -- not safely hand-authorable as raw YAML without an Editor to verify
  against (that asset backs the whole pipeline). `FPVolumetricFogVolume`
  (the Volume Component) has `enablePointAndSpotLight`/
  `localScatteringIntensity` -- built to have actual local lights
  (this project's `DynamicLightBudget`-promoted real lights) genuinely
  scatter into the fog, the real version of what row 18's Bloom
  approximation fakes.
- Real GPU cost (froxel-based: MaxZ pass + volumetric lighting pass +
  denoise, every frame), tunable via `screenResolutionPercentage`
  (default 12.5%) / `volumeSliceCount` (default 128) down to a
  cheaper-but-still-genuinely-volumetric middle ground.

Asked whether to proceed (adding a new external git dependency is a
real decision, not a parameter tweak) rather than just doing it. Creator
asked for an ease/quality/performance comparison against row 18's Bloom
approach instead of an immediate yes/no. Answered honestly that there's
no option winning all three -- row 18 is free and already shipped but
structurally can't produce true light-through-fog scattering (2D
screen-space trick, not 3D); this package IS the real mechanism but has
a genuine per-frame cost even at reduced settings. Creator decided:
stay with row 18 for performance, keep this package noted "just in
case."

Documented as a NEW section (§3) in `.claude/skills/maddr-lighting-
system` rather than only in this log entry, since the explicit ask was
to preserve it as a revisitable future option, not just a record of a
conversation -- includes the concrete compatibility findings above so a
future session doesn't have to re-research feasibility, only decide
whether to spend the performance budget. No code changes this round;
purely a researched-and-declined-for-now decision.

## docs/28 row 20: sun sweeps the compass, long shadows at dawn/dusk (2026-07)

Creator: "I need the sun / light source to move across the sky in a
realistic manner, so shadows move and shift realistically. I want those
long shadows at sunrise and sunsets." Root cause: `SunYawDeg` was a
single fixed constant (-35f, "only elevation/color animate" per its own
comment) -- elevation already animated per-phase, but yaw never did, so
the sun bobbed up and down in place without ever sweeping the compass:
shadow LENGTH changed over the cycle, shadow DIRECTION never did.

Added `SunYawDeg` to `PhaseGrade` (per-phase, blended the same way
`SunElevationDeg` already was), tracing ONE continuous 360-degree sweep
across the full cycle: Dawn 60->105 (45deg over 300 ticks), Day 105->240
(135deg over 900 ticks), Dusk 240->285 (45deg over 300 ticks), Night
285->(285+135=)420=60 (135deg over 900 ticks, wrapping seamlessly back
to Dawn's own 60). Each phase's share of the sweep is proportioned to
its own share of the 2400-tick cycle specifically so angular speed --
and therefore how fast shadows visibly swing -- stays constant instead
of visibly speeding up during the shorter Dawn/Dusk phases.

The Night->Dawn boundary is the one place a naive `Mathf.Lerp(a.SunYawDeg,
b.SunYawDeg, blend)` would have been wrong: Night's raw value (285) is
LARGER than the next Dawn's raw value (60), so lerping straight toward
60 would sweep BACKWARD across the daytime side of the sky (285->240->
180->120->60) instead of continuing forward through the below-horizon
side (285->330->360/0->60). Fixed with a Night-specific `+360` added to
the interpolation target before lerping -- Quaternion.Euler handles any
angle beyond 360 fine (periodic), the +360 only matters for getting the
interpolation DIRECTION right.

Also: "long shadows at sunrise and sunset" -- Dawn's own SunElevationDeg
dropped from 8 to 3, Dusk's from 4 to 3 (now symmetric), so both
transitions sit right at the dramatic near-horizon angle at their
respective phase boundary (Dawn START, Dusk END) instead of Dawn already
being partway risen by the time its own keyframe is read.

Verified against the ACTUAL compiled `Start()`/`ApplyBlend()` via
reflection (not a hand-derived model of the math) -- sampled the sun's
resulting `Light.transform.rotation` at 240 points across a full cycle,
extracted yaw by rotating `Vector3.forward` and reading the horizontal
angle (sign-convention-agnostic, doesn't assume which way "increasing"
points), then checked: yaw genuinely changes (not the old constant);
total sweep over one full cycle is 360.00003 degrees; the sweep is
monotonic the ENTIRE way round INCLUDING across the Night->Dawn wrap
(the specific risk the +360 code exists to prevent); no anomalous single-
sample jump. All passed, and the observed max per-sample step (2.25 deg)
exactly matches the predicted peak of SmoothStep's derivative (1.5x the
linear average of 1.5deg/sample), a clean independent confirmation the
easing curve itself is behaving as designed.

Building this test surfaced two more stub gaps in the flightcheck
harness, both `return null;`/`return default(T);` no-ops that would have
NullReferenceException'd the moment `Start()` was actually exercised
(the first test this session to do so) rather than just called via a
static method: `Component.transform` and `GameObject.AddComponent<T>()`.
Both now have real backing (a lazily-created `Transform`, and actual
`new T()` construction). Harness-only, lives outside the repo.

Same round: creator, mid-turn: "lighten up the roads even more they are
still too dark" -- `Asphalt()`'s base color raised again, from the mid
dark gray (0.35/0.34/0.36) two rounds ago to a genuinely light gray
(0.52/0.51/0.53). Not numerically checkable (pure color choice).

Neither this round's sun changes nor the road color have been seen in a
real render yet.

## docs/28 row 22: shadows weren't rendering at all -- URP shadow distance shorter than the default camera view (2026-07)

Creator: "I see the light shifting but not the shadows." Not a bug in
last round's sun-sweep math (already verified against the compiled
code) -- checked the actual camera framing instead. `RuntimeCityBuilder.
Build()` calls `rig.SnapTo(WorldOf(_city.CenterHex), 70f)`, and
`SimpleCameraRig.SnapTo` positions the camera at `focus + (0, distance,
-distance*0.8)` -- i.e. `(0, 70, -56)` relative to the city center, a
straight-line distance of sqrt(70^2+56^2) =~ 89.6 units. Both
`PC_RPAsset.asset` and `Mobile_RPAsset.asset` had `m_ShadowDistance: 50`
-- shorter than even the DEFAULT starting camera distance, before any
zooming out at all (`SimpleCameraRig.MaxHeight` allows zooming out to
400). Ambient/color/sun-tint changes aren't distance-limited (they're
whole-scene RenderSettings), so they stayed visible everywhere -- actual
shadow casting requires being within `m_ShadowDistance` of the camera,
which wasn't true for most of the visible city even at the default
zoom. This is why "the light shifting" was visible but "the shadows"
were not: the shadow system was never getting the chance to render at
all in the typical view, regardless of how correct the sun's rotation
math was (row 20 already verified that math independently).

Also checked and ruled out, since they're both quick, real ways this
class of bug can happen: `_sun.shadows` IS set (`LightShadows.Soft`,
`LumenCycleController.Start()`); nothing in `RoadDresser`/
`BuildingDresser`/`RuntimeCityBuilder` marks generated geometry static
or touches `Renderer.shadowCastingMode`/`receiveShadows` away from
Unity's own defaults (On/true).

Fixed: `m_ShadowDistance` raised on `PC_RPAsset.asset` (50->150, several
times the default view distance, room to zoom in/out) and
`Mobile_RPAsset.asset` (50->100, more conservative for mobile GPU
budget but still clears the ~90-unit default view with margin). Pure
asset-value edit, no C# involved -- no flightcheck build applicable;
confirmed correct by the direct distance arithmetic above, not yet seen
in a real render.

## docs/28 row 23: shadow distance now scales with camera zoom, not a fixed worst-case value (2026-07)

Creator: "Limit the shadows then to objects in the camera view and close
to the visible area." Row 22's fix (raising `m_ShadowDistance` to a
static 150/100) solved "shadows don't render at all" but left shadow
distance fixed at a value sized for the default camera framing --
either too short when zoomed out further, or wastefully long (URP
culls/draws shadow casters out to that whole radius every frame) at the
much more common close/medium zoom levels.

Added dynamic shadow-distance control to `SimpleCameraRig` (which
already owns the camera and already uses height as a zoom proxy
elsewhere in the file): `UpdateShadowDistance()` writes directly to
`(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).
shadowDistance` every frame (plus once from `SnapTo`, to avoid a
one-frame flash of the static asset default). Formula is proportional-
with-a-cap, not a straight Lerp across the camera's full height range:
`shadowDistancePerHeight` (1.9) is the exact `SnapTo` camera-to-ground
ratio (offset `(0, h, -h*0.8)` -> distance `h * sqrt(1+0.8^2) = h*1.28`)
plus margin so the covered area reaches the actual visible frustum, not
just the exact center point; `shadowDistanceFloor` (15) keeps extreme
close zoom from shrinking distance to something that flickers/pops;
`shadowDistanceCap` (250) holds the line past a certain height rather
than letting distance keep growing linearly out to what height=400
would otherwise demand (~780) -- individual shadows are visually tiny
at extreme zoom-out anyway, and spreading URP's cascades over an
ever-larger area for no visible benefit was exactly the waste this
request was about avoiding.

Verified against the actual compiled `UpdateShadowDistance()` via
reflection, specifically re-checking the EXACT scenario that started
this whole thread (docs/28 row 22): at the default camera height (70,
`RuntimeCityBuilder`'s own `SnapTo` call), computed shadow distance is
148 -- comfortably above the ~89.64-unit actual camera-to-focus
distance, confirming this dynamic version doesn't reintroduce the
original "shadows silently don't render" bug. Also confirmed: distance
at min zoom (30.2) is well under half of the default value (the actual
performance win being asked for); distance at max zoom is capped at
exactly 250, not the ~780 the uncapped proportional formula would give;
the cap engages well before height=400; and the whole curve is
non-decreasing across the entire height range. All values matched hand
derivation exactly.

Note: the static `m_ShadowDistance` values set in `PC_RPAsset.asset`/
`Mobile_RPAsset.asset` last round are now effectively just the
pre-first-Update()/Editor-preview fallback -- this component overwrites
them within the same frame during actual play. Left them in place
rather than reverting; harmless, and still a sane fallback.

## 2026-07: Sun elevation not visibly moving during Day/Night (docs/28 row 24)

Creator, after confirming the shadow-distance fix (row 23): "so are the
shadows baked? is that why they don't animate when the sun rises and
sets?" then, correcting the actual target once I'd ruled baking out:
"The sun should be moving throughout the day not just sunrise and
sunset!"

Checked the baked-lighting hypothesis first, since it's a reasonable
thing to suspect and I hadn't explicitly ruled it out before: no
GameObject in this codebase ever sets `isStatic`/`staticEditorFlags`
(confirmed by grep across `unity-client/Assets/Scripts`), and
`SampleScene.unity`'s `LightmapSettings` block has `m_LightingSettings:
{fileID: 0}` -- no Lighting Settings asset is assigned to the scene at
all. Baked lightmaps need static geometry to bake onto and a Lighting
Settings asset to bake with; neither exists here. The sun itself
(`LumenCycleController.Start()`) is a plain `AddComponent<Light>()`
created fresh every session with `shadows = LightShadows.Soft` and its
`lightmapBakeType` never touched -- worth noting as the same "property
never explicitly set" pattern as elsewhere in this file, but with zero
static geometry to bake against it can't be the cause of anything
looking frozen. Structurally: nothing here is baked.

That redirected the real question to why elevation specifically reads
as static-looking. Found it in `ApplyBlend()`: `SunYawDeg` (compass
direction) already got the "proportion the sweep to phase duration"
treatment in row 20, but `SunElevationDeg` was still just
`Mathf.Lerp(a.SunElevationDeg, b.SunElevationDeg, blend)` between
adjacent phase keyframes -- locking the SWEEP MAGNITUDE to whatever the
two authored values happen to differ by, with no regard for how long
that phase lasts. Dawn (30s) and Day (90s) both authored a 27-degree
swing (3->30, 30->3 respectively), so elevation moved 3x faster during
Dawn than during Day; Dusk/Night had the same 3x mismatch (11 degrees
over 30s vs 11 degrees over 90s). Yaw sweeps the compass at a constant,
clearly-visible rate all day; elevation all but crawled through the
long Day/Night phases and only visibly bobbed during the short Dawn/
Dusk windows -- exactly "moving throughout the day, not just sunrise
and sunset."

Fix: `LumenCycleController.ComputeSunElevationDeg(int cycleT)`, a new
dedicated function (same shape as `ComputeNightIntensity` -- this file's
established pattern for "the plain per-phase Lerp doesn't fit this
field") that treats elevation as one continuous arc across the WHOLE
2400-tick cycle instead of four independent phase-to-phase blends. The
peak and trough sit at each long phase's MIDPOINT (solar noon at
tick 750 = Dawn's 300 ticks + half of Day's 900; solar midnight at
tick 1950 = Dusk's end (1500) + half of Night's 900) rather than only
at phase boundaries, so the climb from Dawn's low anchor (3 deg) to
Day's peak (30 deg) spans Dawn plus the FIRST half of Day, and the
descent from that peak to Dusk's low anchor (3 deg) spans the SECOND
half of Day plus all of Dusk -- every tick of Day sits inside an
actively-moving segment. Night mirrors this around its own trough
(-8 deg). Reuses the exact same four authored elevation values already
in `BuildGrades` (Dawn/Dusk 3, Day 30, Night -8) -- no new tuning
numbers, purely a different interpolation between them, so this doesn't
touch "long shadows at sunrise/sunset" (still the elevation floor at
both transition boundaries) or "kept low, never a high overhead noon
angle" (peak is still 30, same as before).

Each of the four segments still eases with `SmoothStep`, matching this
controller's existing "unhurried" style. That does mean the two
PASS-THROUGH points (the Dawn-anchor/cycle-wrap and the Dusk-anchor,
where the sun is mid-climb/mid-descent, not actually turning around)
get a brief, momentary softening right at that instant from both
adjoining segments' SmoothStep flattening toward their own endpoints --
a small cosmetic wrinkle, not a functional dead zone, and confirmed
below to be far smaller than the bug it replaces.

Verified via reflection against the compiled `ComputeSunElevationDeg`
(temporary harness, removed after verifying): the four keyframe values
(3 at cycleT=0, 30 at cycleT=750, 3 at cycleT=1200, -8 at cycleT=1950)
all matched exactly; the function is continuous across the full cycle
(max single-tick step 0.09 deg, no jump); every 90-tick sliding window
inside Day and inside Night moves at least 0.5 degrees EXCEPT the two
windows straddling the genuine peak/trough themselves (expected --
a real sun legitimately slows near solar noon/midnight, confirmed by
checking a window well clear of each extremum still moves several
degrees); and the old bug's headline number -- average rate across Dawn
vs. average rate across Day's climb half -- went from the old code's
exact 3.0x mismatch to 0.81x (i.e. now roughly comparable, no longer a
one-phase-crawls-the-other-doesn't split). Not yet seen in a real
render.

## 2026-07: Stray scene Directional Light + HDR-style night fill (docs/28 row 25)

Creator, after row 24's elevation fix: "Works now if I change the the
Directional to a point light or area light, but now the night is too
dark what we need is a hdr like fill. So everything isn't so dark."
Rather than guess what "change the Directional to a point/area light"
meant or whether to make that permanent, asked directly via
AskUserQuestion. Answer: "The scene had a directional built into it.
So there are 2, thus the shadows appear fixed, never animating."

That's the real explanation, and it's a much better one than anything
I'd been chasing in rows 20-24. Checked `SampleScene.unity` directly:
it ships Unity's own stock default "Directional Light" GameObject --
`m_Intensity: 2`, soft shadows enabled (`m_Shadows.m_Type: 2`), fixed
rotation `m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}` (the literal
Unity new-scene default, confirmed by the exact numbers matching what
every fresh empty scene ships with) -- sitting there, enabled, the
entire time, completely untouched by any script in this project.
`LumenCycleController.Start()` creates its OWN separate "(auto) Sun"
GameObject every session and never looks for or reuses this one, so
the scene ended up with two enabled Directional lights simultaneously.
URP only gives ONE directional light the full Main Light / shadow-
casting treatment, and it was evidently picking the static stock one,
not the procedurally animated one -- which means every rotation fix in
rows 20 (yaw sweep) and 24 (elevation arc), BOTH independently verified
correct via reflection against the compiled math, was animating a
light that was never the one actually casting shadows on screen. The
creator's manual workaround (retyping the STRAY light to Point/Area in
the Inspector) worked by accident, for the right underlying reason:
it stopped competing as a second Directional light. It also explains
the new "too dark" symptom as a side effect, not a separate design
regression -- that stray light's constant, always-on intensity-2
contribution (never tied to day/night at all) had apparently been
propping up the overall brightness the whole time; removing it as a
Directional light removes that contribution too.

Verified via `grep -n "^Light:$"` against the whole scene file: exactly
one match, confirming there's no OTHER duplicate light lurking
somewhere else. Disabled the stray GameObject directly in
`SampleScene.unity` (`m_IsActive: 0`, renamed to
"Directional Light (disabled -- stray Unity default, see docs/28 row
25)" so it reads as intentional if the creator opens the Hierarchy)
rather than leaving the creator's manual per-session Inspector edit as
the only fix -- that edit doesn't persist across scene reloads/version
control, a real scene-file edit does. This is a genuinely different
class of fix than everything else in this file so far: every other row
is a C# script change; this is the first one where the SCENE ASSET
ITSELF had a latent bug niether script reasoning nor the reflection
harness could ever have caught (the harness has no concept of a .unity
scene file's object graph at all).

Second half of the request: since disabling the stray light removes
the same always-on brightness contribution the creator's workaround
already showed vanishes ("now the night is too dark"), that half of
the ask still needs a real answer, not just "the scene edit undoes
your workaround's side effect too." "HDR like fill... so everything
isn't so dark" was interpreted specifically: NOT a flat ambient-light
bump (that knob already exists as `nightAmbient`, and was deliberately
dropped near-zero a few rounds back specifically so the street lamps
would read as distinct "pools of light" against genuine darkness --
raising it again would undo that on purpose). What "HDR-like" actually
names is the standard photography/color-grading technique of lifting
shadow DETAIL without touching highlights, preserving local contrast
instead of flattening it. URP ships exactly this as a Volume override:
`ShadowsMidtonesHighlights`, the same three-wheel-grader family as
Lift/Gamma/Gain, but tonal-range-based (shadows/midtones/highlights)
rather than exposure-based. New `LumenCycleController.nightFillLift`
([Range(0,1)], default 0.35, mirrored onto `CityLightingProfile` as
`NightFillLift`) drives only the `shadows` wheel's W (luminance-offset)
component, leaving X/Y/Z at the neutral 1/1/1 (never recolors) and
midtones/highlights untouched at their own neutral defaults (never
touches already-bright pixels) -- blended by `nightAmount` exactly like
every other night-only effect in this file (0 through Day, ramping to
the full value across the Dusk/Night hold).

Honesty note distinct from every other row: `ShadowsMidtonesHighlights`
could NOT be checked against the live Unity manual this session --
every WebFetch attempt at docs.unity3d.com returned a 403 from the
destination itself (confirmed NOT an org egress policy block: the
agent proxy's own status endpoint showed zero recentRelayFailures, so
the request reached Unity's server and Unity's own bot-blocking turned
it away). Existence and field names (`shadows`/`midtones`/`highlights`
as Vector4, `shadowsStart`/`shadowsEnd`/`highlightsStart`/
`highlightsEnd` as floats) are corroborated a different way instead:
WebSearch surfaced distinct "Shadows Midtones Highlights | Universal
RP" manual page titles spanning URP 7.1 through 6000.0 (i.e. stable
across nearly the package's entire history, not a one-off), plus a
real Unity Discussions thread with working code calling
`TryGet<ShadowsMidtonesHighlights>()` and reading `shadowsStart`/
`shadowsEnd`. That's meaningfully stronger evidence than existed for
the fictional `Exposure` mistake earlier in this file (which had no
such trail at all) but is still not the same as reading the real docs
or an Editor session -- flagged as provisional in both the flightcheck
stub's own comment and docs/28's row 25 status column.

Verified via reflection against the compiled `ApplyBlend()` (temporary
harness, removed after verifying): `shadows.value.w` is exactly 0 at
every sampled point in Day (matches nightAmount's own hard-0 floor,
confirmed the daytime image is untouched), exactly `nightFillLift`'s
value (0.35 default) deep into the Night hold, and the color channels
(x/y/z) stay pinned at 1 in both cases -- confirming the mechanism only
ever lifts luminance, never recolors. Not yet seen in a real render,
and additionally gated on `ShadowsMidtonesHighlights` actually
compiling against the real URP package the way it did against this
session's own hand-written (and, on this specific type, not fully
docs-verified) stub.

## 2026-07: nightAmbient raised 0.02 -> 0.08 (docs/28 row 26)

Creator, immediately after row 25's ShadowsMidtonesHighlights fill
shipped: "still need an ambient light so we can see in the darkest
part of the night."

Row 25's `nightFillLift` is a post-processing color grade -- it only
adjusts the FINAL rendered pixel's luminance, after lighting is
already done. It can lift a crushed-black pixel toward gray, but it
can't make an unlit wall or the far side of a building actually
readable as a SHAPE, because nothing about how that surface was lit
changed -- there's no real light hitting it to reveal its form either
way. "An ambient light" is a more literal, specific ask than the
previous "hdr like fill" framing: real scene ambient (`RenderSettings.
ambientLight`, driven by `LumenCycleController.nightAmbient`) is what
actually lights unlit-otherwise surfaces, and it was still sitting at
0.02 -- deliberately crushed near-black several rounds back so the
street lamps would read as distinct "pools of light" against real
darkness, but too dark on its own for the creator to see anything in
the gaps between lamps.

Raised the default 0.02 -> 0.08 on both `LumenCycleController.
nightAmbient` and the mirrored `CityLightingProfile.
NightAmbientBrightness`. Chosen as a real, visible floor without
fully undoing the lamp-pool-contrast design goal -- still well under
Dawn/Dusk's own ambient values (0.3-0.55 range on those Color
channels), so full night stays the darkest point in the cycle, just no
longer literally near-zero. This is a plain default-value change, not
new logic -- flightcheck compiles clean, nothing to numerically verify
beyond that. Complements row 25 rather than replacing it: nightAmbient
fixes actual scene lighting (can I make out shapes at all), nightFillLift
fixes the final image's tonal floor (does the darkest pixel read as
crushed pure black or a readable near-black) -- both were asked for by
name, in two back-to-back messages, and address genuinely different
mechanisms.

## 2026-07: nightAmbient tripled 0.08 -> 0.24 (docs/28 row 27)

Creator, immediately after row 26 shipped: "still way too dark. triple
it."

Straightforward: `nightAmbient` 0.08 -> 0.24 on both
`LumenCycleController` and `CityLightingProfile.NightAmbientBrightness`,
exactly the tripling asked for. Also widened `nightAmbient`'s own
`[Range]` ceiling from 0.3 to 1.0 (matching the profile asset's
already-wider 0-1 range) -- 0.24 was already most of the way to the old
0.3 cap, and this is the second "still too dark" correction in a row,
so leaving headroom for a third seemed better than assuming 0.24 is
final. Plain arithmetic, no new logic -- flightcheck compiles clean.

## 2026-07: nightAmbient hard floor at 0.12 (docs/28 row 28)

Creator, alongside the clock request below: "the minimum nightAmbient
should be at least 0.12."

Distinguished this from row 27's plain default bump: a `[Range]`
attribute's minimum only guards direct Inspector slider drags, it does
NOT stop `ApplyProfile()` copying in a lower value from a
`CityLightingProfile` asset, nor a future default edit from silently
regressing back toward the near-black 0.02 this whole back-and-forth
has been about escaping. Implemented as a genuine runtime floor instead:
new `LumenCycleController.MinNightAmbient` const (0.12f), applied via
`Mathf.Max(nightAmbient, MinNightAmbient)` at the actual point of use in
`ApplyBlend()`, not just as the `[Range]` slider's lower bound (which
was ALSO raised to 0.12, for Inspector-level consistency, but is now
the belt to the code-level floor's suspenders). Mirrored the `[Range]`
floor onto `CityLightingProfile.NightAmbientBrightness` too.

Verified via reflection against the compiled `ApplyBlend()` (temporary
harness, removed after verifying) -- while writing this check, found
and fixed a REAL gap in the flightcheck stub itself: `Color.Lerp` had
been a `default(Color)` no-op this entire session (along with
`Color.white`/`black`/`gray`, the `*` operator, and the
`Color`->`Color32` implicit conversion), which made the floor check
read back a flat 0 regardless of the actual computed value -- the exact
"stub that passes vacuously" risk this file's own comments already
flag for other types. None of THIS session's earlier checks happened
to assert on a `Color.Lerp`-derived value (the elevation/yaw checks
used raw `Mathf.Lerp` floats, not `Color.Lerp`), so nothing already
verified this session was actually compromised by it -- but it was a
live landmine for any future one. Fixed to real arithmetic; re-ran the
floor check afterward and confirmed: `nightAmbient` forced to 0.05
(simulating a mistuned/old profile) still reads back exactly 0.12 in
`RenderSettings.ambientLight` at full Night; `nightAmbient` at the
current 0.24 default passes through unmodified.

## 2026-07: analog clock HUD with sweeping hands + ticking pendulum

Creator: "Give me a analog clock in the top right corner of the screen
for the time of day, the arms should sweep, and there should be a
ticking granfather clock swinging arm. but it should only take up
1/10th of the screen." Then, mid-turn: "and the clock should be editor
resizable and repositionable."

New `AnalogClockHud.cs`, following this project's established HUD
conventions to the letter rather than introducing a new UI paradigm:
IMGUI (`OnGUI`), same as `Minimap`/`HudStatus` (this project's ONLY UI
layer -- see `HudStatus`'s own header for why that's fine alongside the
New Input System), and the exact same "bake a small texture once, then
stretch/rotate it at draw time" technique `Minimap` already uses for
its own terrain bake and camera-relative rotation
(`GUIUtility.RotateAroundPivot`).

Time source: `DayNightState.CycleProgress` (already published every
frame by `LumenCycleController`, 0..1 raw position through the whole
Lumen day/night cycle -- exactly what `EmissiveAnimator`'s window-
occupancy scheduling already reads). One full Lumen cycle (Dawn->Day->
Dusk->Night->Dawn) = one 12-hour dial revolution for the hour hand,
with the minute hand doing the usual 12 revolutions per hour-hand
revolution (the real analog-clock gear ratio) -- a stylized "how far
through today's cycle" reading, not meant to line up with any specific
real hour, since there's no natural 1:1 mapping between a 240-second
game cycle and a 12/24-hour clock face anyway.

"The arms should sweep" vs. "a TICKING grandfather clock" was read as
two deliberately different motions, not the same thing applied twice:
the hour/minute hands are a pure, continuous function of
`CycleProgress` (genuinely smooth, no stepping); the pendulum instead
snaps between two fixed angles once per `tickIntervalSeconds` (default
1s) -- a real mechanical tick-tock, distinct from the hands' sweep.

"1/10th of the screen": implemented as a `sizeFraction` field (default
0.1) applied to the SHORTER screen dimension, not a fixed pixel count
-- a fixed pixel size would be a different fraction of the screen on
every resolution/window size, so fraction-based is what actually keeps
the stated constraint true. "Editor resizable and repositionable":
mirrors `Minimap`'s own established pattern exactly -- `corner` (4-way
preset, default TopRight) + `marginPixels`, `sizeFraction` as a
`[Range]` slider, and `useCustomPosition`/`customTopLeftPixels` to
bypass the presets for pixel-exact placement -- all public Inspector
fields, all live-tunable in Play mode as this project's other HUD knobs
already are. Wired into `RuntimeCityBuilder.Build()` alongside
`Minimap`/`HudStatus`, no `Init()` needed since it only reads the
already-public static `DayNightState.CycleProgress`.

Verification, and an explicit limit on what could be verified: the pure
angle math (`HourHandDeg`, `MinuteHandDeg`, `PendulumDeg` -- deliberately
kept free of any GUI/Texture call so they're checkable without a real
render) was confirmed via direct calls in a temporary flightcheck
harness: hour hand at exactly 0/90/180/270 degrees at cycle progress
0/0.25/0.5/0.75; minute hand wraps exactly at 1/12 of the cycle;
summing the minute hand's unwrapped delta across 2400 samples of one
full cycle comes out to exactly 4320 degrees (12 x 360, confirming
precisely 12 revolutions, no drift); the pendulum only ever produces
the two extreme +/-swing values, snapping (never an intermediate lerp
value) at exactly the tick boundary. What could NOT be verified, and is
a first for this session: the actual IMGUI DRAWING -- does the baked
face texture read as a clock face, do the rotated rects read as hands,
does the pendulum look like it's hanging below the case -- since
`flightcheck` has no rendering at all, only compiled logic. Every prior
fix this session at least had a creator screenshot or console-log
symptom to reason from; this is the first visual feature built with
zero rendering feedback of any kind. Flagged prominently in the file's
own header comment -- expect this to need real visual tuning once it's
actually on screen, more so than anything else shipped this session.

## 2026-07: analog clock invisible -- contrast, not a crash

Creator: "I don't see the clock." Two follow-up rounds of diagnosis
before touching code, matching this session's "don't guess blindly"
lesson from earlier: (1) asked whether there were compile/runtime
errors and whether other HUD elements (top-left status text) were
showing -- answer: no compile errors, other HUD text visible; (2) asked
specifically about RUNTIME (red) console errors and whether there was
even a faint shape in the top-right corner -- answer: "no red at all."

That combination (clean compile, other OnGUI elements confirmed
working, zero runtime errors) rules out a crash or the component
simply not running, and points at the much more mundane remaining
explanation: contrast. The original palette was a near-black face
(0.08/0.07/0.05) with only a 3-pixel-wide rim baked into a 128px
texture -- once stretched down to ~1/10th of a real screen (roughly
100-150px on a typical display), that rim is a handful of SCREEN
pixels, easy to lose entirely against a varied, often-dark 3D city
backdrop.

Rebuilt around three changes, all in `AnalogClockHud.cs`:
1. An opaque-ish backing halo drawn behind the whole widget (same
   "translucent dark frame so a HUD element reads against ANY
   background" trick `Minimap` already uses for its own map).
2. Palette flipped to the actual high-contrast convention a real clock
   uses -- light ivory face, dark hands -- rather than the original
   dark-on-dark.
3. Every stroke substantially thickened: rim 3px -> 9px (of the 128px
   bake), main/minor ticks 1.6/0.9px -> 4/2.2px, hour/minute hand width
   fractions 0.045/0.03 -> 0.07/0.05 of the clock's on-screen size,
   pendulum rod/bob similarly thickened.

Also added a defensive guard + one-time `Debug.LogWarning` in
`GetScreenRect()` for `Screen.width`/`height` reporting 0 (would
silently produce a zero-size, invisible rect with no exception --
indistinguishable from "not running" from the creator's side without a
log line to point at it specifically).

Explicitly flagged to the creator (and in the script's own header
comment) what this fix does and doesn't rule out: if the face is now
visible but the hands/pendulum aren't moving or aren't visible, that
isolates the problem to `GUIUtility.RotateAroundPivot` specifically --
worth calling out because `Minimap`'s own use of that same API is
gated behind `rotateWithCamera`, which defaults OFF, meaning it has no
other CONFIRMED-working caller in this codebase to lean on. If nothing
shows up at all even now, that would point back toward
`GetScreenRect`/component lifecycle instead of contrast. flightcheck
compiles clean; the angle math itself was untouched this round (already
verified in the prior entry) so wasn't re-checked.

## 2026-07: lights now start turning on at the clock's 5:00 position

Creator: "The lights should start turning on by 5:00 pm on the clock."

This ties two systems built earlier this session together directly:
`AnalogClockHud.HourHandDeg` maps one full Lumen cycle to one 12-hour
dial revolution (`cycleProgress * 360`), so dial position 5:00 is 5/12
of a full revolution -- cycle tick `2400 * 5/12 = 1000` exactly. That
lands solidly inside DAY (ticks 300-1200), well before Dusk even starts
(1200) -- interesting side note, Dusk's own start lands EXACTLY on the
dial's 6:00 (`2400 * 0.5 = 1200`, `0.5 * 360 = 180deg = 6:00`), a clean
coincidence in how the phase proportions were set up, but not itself
what was asked for.

`LumenCycleController.ComputeNightIntensity`'s ease-in previously
started 25% into Dusk (tick 1275ish); moved the trigger to tick 1000
(the dial's 5:00) instead, keeping the same fast ~75-tick (7.5s) ramp
duration as before -- just detached from being expressed as "a fraction
of Dusk" now that it starts earlier, inside Day. Lights are fully on by
dial ~5:22, comfortably ahead of Dusk. Everything else (the Dawn
fade-out, the flat hold through the rest of Day/Dusk/Night) is
unchanged.

Verified via reflection against the compiled `ComputeNightIntensity`
(temporary harness, removed after verifying), including a cross-check
against the actual compiled `AnalogClockHud.HourHandDeg` to confirm
tick 1000 really does read as 150 degrees (5:00) on the dial, not just
asserted by hand-derivation: intensity is 0 everywhere from Dawn's end
through tick 999, starts ramping at exactly 1000, is strictly between 0
and 1 mid-ramp, reaches exactly 1 at 1075, and stays 1 through the rest
of Day, all of Dusk, all of Night, with the existing Dawn fade-out
(unrelated to this request) unchanged. Not yet seen in a real render.

## 2026-07: docs/23 Phase 3.5's deferred moon-dial/capture-bar/mana HUD, shipped

Creator direction: pivot from world/lighting polish to gameplay
mechanics and UI/UX; this is the first UI slice. Phase 3.5 (emitters +
Lumen mana) shipped its sim-side math back when 84 match-core tests
existed, but explicitly deferred "the Unity moon-dial/capture-bar/mana
HUD" (see 00-index's Phase 3.5 status note) because nothing displayed
any of it. All three data sources (`LumenClock`, `SimEmitter`, `PlayerState.Mana`)
were already real and tested; this closes the display gap, not a sim
gap.

New `unity-client/Assets/Scripts/LumenHud.cs` (IMGUI, same layer as
`HudStatus`/`Minimap`/`AnalogClockHud`): a fixed bottom-right panel (the
one corner none of the other three HUD elements already default to)
showing the current Lumen phase name with a countdown, switching to a
warning color + a discrete flash once <=10s remain (the glossary's own
"moon dial" definition: "the always-public HUD clock showing the
current Lumen phase and the 10-second transition warning"); the local
player's mana bar against `PlayerState.ManaCap` (100); and one text
label + progress bar per emitter currently mid-capture
(`SimEmitter.CapturingPlayer != null`), reusing `CaptureProgressTicks`
over `SimEmitter.CaptureChannelTicks` for the fraction -- exactly the
use that field's own doc comment named ("exposed for a future
capture-progress HUD bar ... and for tests").

Scoped down from the ideal on purpose: this panel is a fixed on-screen
list, NOT a world-space marker hovering over the actual emitter hex --
building a hex-to-screen projection has no precedent in this codebase
and is a real, separate follow-up. Also deliberately does NOT share
`Minimap.ScreenCorner` (unlike `AnalogClockHud`, which does) -- `LumenHud`
declares its own identical-shaped enum instead, keeping Minimap's much
larger camera/input/fog-of-war dependency chain out of this file's
compile graph entirely.

New match-core primitive: `LumenClock.TicksUntilNextPhase(frame)` --
walks the exact same cascade `PhaseAt` already does so the two can never
disagree about which phase owns a given frame; at a boundary tick it
returns the FULL next-phase duration, not 0 (the specific off-by-one a
naive "ticks since last boundary" reading would get backwards for a
countdown). Six new narrow `SimBridge` pass-throughs
(`CurrentFrame`/`CurrentLumenPhase`/`TicksUntilNextLumenPhase`/
`PlayerMana`/`EmitterCount`/`EmitterAt`), same defensive-default style
as the pre-existing `OrderOf` (0/Dawn/false-shaped answers when no match
is running, never a throw). 239 match-core tests total (up from 220):
boundary-exact `TicksUntilNextPhase` values at every phase edge
(including the cycle-2 wraparound) plus a consistency check that
`PhaseAt(frame)` and `PhaseAt(frame + TicksUntilNextPhase(frame))`
always disagree by exactly one phase, for a spread of arbitrary frames
including 2400's wraparound.

Verified three ways, no Unity Editor available in this environment:
(1) the real `dotnet test` match-core suite, 239/239 green; (2) `LumenHud`'s
pure formatting/threshold statics (`PhaseLabel`/`IsTransitionWarning`/
`FormatCountdown`/`Fraction`/`FlashOn` -- none touch a UnityEngine type)
copied into a throwaway console app and run for real against match-core's
actual built DLL, including a real `MatchState`/`CityGenerator` capture
sequence, not just isolated unit inputs -- a genuine step up from earlier
HUD work's "verified by reflection," since this is an actual compile-and-run,
not reflection into a stub; (3) a stub-compile of the REAL `LumenHud.cs`
file (not a copy) against a minimal UnityEngine stub plus a
signature-matched `SimBridge` shape stub (exposing exactly the six new
members, to catch a mismatch between what `LumenHud.cs` calls and what
the real `SimBridge.cs` diff actually added) -- compiled clean, 0
warnings, referencing the real built `MadDr.MatchCore.dll`/`MadDr.CityGen.dll`.
This flightcheck caught one real bug before it shipped: `LumenHud.cs`
used `HexCoord` (from `SimEmitter.Hex`) without a `using MadDr.CityGen;`
line -- fixed. Not seen in a real render.

Remaining from the deferred trio: world-space capture markers, and the
region picker (a separate docs/23 Phase 8 item) are still not started.
The Phase 2 build-menu/ghost-placement cursor is also still open --
next candidate for this same UI/UX push.

## 2026-07: docs/23 Phase 2's Unity half, shipped -- build menu, ghost cursor, BaseDresser

Direct follow-up to the HUD trio above, same UI/UX push. Phase 2's
sim-side (`BuildingDef`/`SimBuilding`/`CommandKind.BuildStructure`) has
been done and tested since the original Phase 2 pass, but nothing in
Unity could ever actually place, preview, or SEE a building -- a player
could not build anything, full stop, despite the mechanic being real
underneath.

**New match-core surface** (both additive, both real refactors rather
than duplicated logic):
- `MatchState.CanPlaceBuilding(playerIndex, kind, hex)` -- pulled the
  exact validation `ApplyBuildStructure` already ran (real kind, on-map
  + unblocked hex, full affordability) into its own public method, which
  `ApplyBuildStructure` now calls too. This was a deliberate refactor,
  not a hand-copied duplicate check: a ghost cursor's live preview and
  the sim's actual outcome share one code path, so they cannot drift
  apart the way two independently-maintained copies eventually would.
  (Fixed a CS8602 nullable-warning regression this refactor introduced
  at the `_blockedToGround.Add(hex)` call site -- the null-check moved
  into the new shared method, so the compiler's flow analysis can no
  longer see it from `ApplyBuildStructure` alone; a documented
  null-forgiving `!` closes it, since `CanPlaceBuilding` having already
  returned true is what guarantees non-null there.)
- `SimBuilding.TicksUntilComplete` -- exposes the previously-private
  construction countdown, for a scaffold-percent visual (combine with
  the same kind's `BuildingDef.BuildTimeTicks` for a 0-1 fraction; not
  worth duplicating that number onto the instance itself).

246 match-core tests total (up from 239): `CanPlaceBuilding` covered for
affordable/unaffordable/blocked/off-map/Hq-kind cases, PLUS a test that
building on a hex flips a live `CanPlaceBuilding` read from true to false
(the exact "does the live preview track the SAME blocked set the sim
mutates" property a ghost cursor depends on); `TicksUntilComplete`
covered counting down to exactly 0 and staying 0 for both a normal build
and an immediately-complete HQ spawn.

**New `SimBridge` surface** (six more narrow pass-throughs, same
defensive-default style as the emitter/mana trio): `QueueBuildCommand`,
`CanPlaceBuilding`, `BuildingCount`, `BuildingAt`, `PlayerWallet`.

**New Unity scripts**, all in `unity-client/Assets/Scripts/`:
- `BuildMenuHud.cs` -- `BuildingDef.AllDefs` (minus `Hq`) as a hotkeyed
  1-9 list, cost-per-`ResourceKind` labels, unaffordable rows grayed
  (still clickable) via the new `PlayerWallet`. Owns ONLY selection
  state (`SelectedKind`) -- placement itself is `BuildGhostCursor`'s
  job, the same selection/order split `WaypointCommander` already keeps
  for units. Exposes `PointerOverPanel` (mirroring `Minimap.PointerOver`
  exactly) so a menu-row click can't also register as a world-space
  placement click underneath it.
- `BuildGhostCursor.cs` -- while a kind is selected, raycasts the mouse
  the same way `WaypointCommander.RaycastCursor` does (duplicated
  locally rather than exposing that private helper, since the two
  scripts have no other coupling), resolves the hex via the existing
  `RuntimeCityBuilder.HexAt`, previews red/green from a live
  `CanPlaceBuilding` call every frame, and left-click-confirms via
  `QueueBuildCommand`. Right-click or Escape cancels. Ground elevation
  follows the existing `RuntimeCityBuilder.GroundHeightAt` -- the ghost
  sits on sculpted terrain, not floating at a flat y=0.
- `BaseDresser.cs` -- the actual construction-lifecycle visual docs/23
  §2 asks for (Ghost is Unity-only by design, never a sim state --
  that's `BuildGhostCursor`'s job; this owns UnderConstruction onward).
  Walks `SimBridge.BuildingCount`/`BuildingAt` every frame (match-core's
  own building list only grows -- destroyed buildings stay in it with
  `State == Destroyed` instead of being removed, so this is the one
  place that gets turned into/out of GameObject existence): spawns a
  primitive cube per new `EntityId`, scales it up translucent as
  `TicksUntilComplete` counts down, swaps to a solid per-`BuildingKind`
  hue at `Complete`, darkens that same material when `IsDamaged`
  (docs/18 §3's derived-from-HP visual state, not its own persisted
  one), and destroys the GameObject outright once `Destroyed` (no
  rubble/wreck FX yet -- a real follow-up reusing the existing `DamageFx`
  system, not attempted here).

**Explicitly out of scope, flagged rather than attempted** (docs/23 §2's
own "HQ dressing per faction" phrase only gets the generic-hue,
Landmark-tier-sized treatment here -- real per-faction skin variety is
separate scope); every kind shares one placeholder cube shape (no
per-kind silhouette yet); multi-hex footprints (still `SimBuilding`'s
own single-hex-only v1, unchanged); attacking a player-built structure
(`SpawnPrim` strips the collider the same way every other cosmetic
dressing object does, so these aren't even raycast-hittable yet -- moot
anyway, since match-core has no `AttackBuilding` command at all).

**Wiring gap closed in passing**: `LumenHud` (the previous entry's HUD
trio) had been built and flightchecked but never actually instantiated
in any scene -- true of `BuildMenuHud`/`BuildGhostCursor`/`BaseDresser`
too until now. All four are wired together in
`RuntimeCityBuilder.HandleRosterReady`'s `simDrivenDemo` block, the one
place in any scene a real `SimBridge`/`MatchState` gets created today.

Verified three ways, no Unity Editor available in this environment: (1)
the real `dotnet test` match-core suite, 246/246 green, plus a
`dotnet build` of just `MatchCore.csproj` confirming 0 warnings after
the null-forgiving fix; (2) a stub-compile of the three real new script
files (not copies) against a minimal UnityEngine(.InputSystem) stub plus
signature-matched shape stubs for `SimBridge`/`RuntimeCityBuilder`/
`Minimap`/`LabMeshBuilder`/`ShaderUtil` -- compiled clean, 0 warnings,
`ImplicitUsings` deliberately DISABLED to match real Unity's compilation
model exactly (a first pass with it left on produced a false-positive
`Object` ambiguity between `System.Object` and `UnityEngine.Object` that
would never occur in the real project, since these files carry no
`using System;`); (3) manual re-read of the `SimBridge.cs` diff itself
(not stub-compiled directly, to avoid pulling in its real `SimUnitView`
dependency chain just for six one-line pass-throughs) -- same posture as
the previous HUD-trio entry. Not seen in a real render.

Remaining UI/UX candidates: a capture bar/moon dial upgrade to
world-space markers, the region picker, and per-faction/per-kind real
building art are all still open.

## 2026-07: world-space capture markers + region picker

Two more items off the running UI/UX list, both direct follow-ups to the
two entries above.

**Capture bars, upgraded to world-space.** The previous entry's
`LumenHud` capture-progress rows lived in the fixed bottom-right panel
as a text list. This upgrade moves them to float directly over the
actual emitter hex in 3D space, matching docs/03's own phrasing
("progress bar visible to both players") literally rather than just
functionally. New `LumenHud.TryWorldToGui(Camera, Vector3)`: wraps
`Camera.WorldToScreenPoint` (bottom-left origin, Y-up, z <= 0 meaning
behind the camera -- must skip drawing, not mirror to the wrong side of
the screen) and flips into OnGUI's own Rect space (top-left origin,
Y-down) via `Screen.height - screenPoint.y` -- the first HUD element in
this project to project a 3D point onto the 2D overlay at all (every
prior one -- Minimap, AnalogClockHud, the fixed panels -- only ever
positioned themselves by screen corner, never by a world position). New
`LumenHud.builder` field (a `RuntimeCityBuilder` reference, for
`WorldOf`/`GroundHeightAt`) and a matching `Init` signature change,
propagated to its one real call site in `RuntimeCityBuilder`'s
`simDrivenDemo` wiring block. The fixed panel is now phase+mana only --
fixed height, since it no longer needs to grow with capture count.
Markers simply don't draw when off-screen or behind the camera (no
fallback list) -- the same "just skip it" contract every other
screen-space overlay here already follows for an undefined case.

**Region picker.** docs/23 Phase 8 shipped real `CityPreset.NewYork()`/
`.Paris()`/`.Montreal()` factories in citygen-core, but nothing in Unity
could ever choose one -- `RuntimeCityBuilder.preset` was (and largely
still is) an Inspector-only field a developer sets before hitting Play,
never a runtime choice. `RuntimeCityBuilder.PresetChoice` gained the
three region values; `ResolvePreset()` routes them to citygen-core's own
factories -- their first Unity-side consumer since Phase 8 shipped.

The real work was `Start()` itself: it used to run city generation,
lighting, camera framing, every HUD's `Init`, and the roster fetch
unconditionally, in one block. Extracted everything from `_city =
CityGenerator.Generate(...)` onward into a new public `BeginMatch()`,
leaving `Start()` with just the CityGizmo seed/preset sync plus a
branch: a new opt-in `showRegionPicker` field (**default false**) calls
`BeginMatch()` immediately, byte-for-byte the same call `Start()` always
made -- every existing scene, the Inspector preset field, CityGizmo sync,
all of it, completely unchanged. Turning it on instead adds a new
`RegionPickerHud` component and returns, deferring `BeginMatch()` until
that screen's own confirm click sets `preset` and calls it directly --
the exact same generation path, not a second one.

New `RegionPickerHud.cs`: a centered IMGUI panel, six buttons (the three
existing local presets plus the three new regions, each with a one-line
blurb), `GUI.Button` confirms and self-destroys
(`Object.Destroy(this)`). The one HUD element in this project centered
on screen rather than corner-anchored, since there's no city/camera/
gameplay yet for a corner to avoid overlapping.

Verified two ways, no Unity Editor available in this environment: (1) a
stub-compile of all five HUD/build scripts together (the three from the
prior two entries plus the changed `LumenHud.cs` and new
`RegionPickerHud.cs`) against real match-core/citygen-core types and
signature-matched `SimBridge`/`RuntimeCityBuilder`/etc. shape stubs --
`RuntimeCityBuilder`'s stub gained the new `PresetChoice` values,
`preset` field, and `BeginMatch()` to keep `RegionPickerHud.cs`'s calls
checked against the real signatures -- compiled clean, 0 errors/
warnings; (2) manual re-read of every edited line in the real
`RuntimeCityBuilder.cs` (too large a file to stub-compile whole, same
posture every prior entry in this log has taken) confirming the
`Start()`/`BeginMatch()` split changes nothing about call order or
arguments -- only where the code lives. Not seen in a real render.

Remaining: per-faction/per-kind real building art, and giving the
region picker a live preview (currently just labeled buttons, no
thumbnail/gizmo-style rendering of each preset) are still open.

## 2026-07: region picker gained live thumbnails

Direct follow-up to the entry above -- "giving the region picker a live
preview" closed the same session it was flagged.

Each of the six options in `RegionPickerHud` now generates a real
`CityModel` (`CityGenerator.Generate` against a fixed preview seed,
`0xCAFE1950u` -- arbitrary but constant, so docs/18's own determinism
contract means the picker shows the SAME six thumbnails every time, not
a fresh reroll per session; deliberately distinct from
`RuntimeCityBuilder.seed`, which is still the actual match seed) and
bakes it into a small (160x160) top-down texture. The bake reuses
`Minimap.BakeTerrain`'s own exact palette (water/ridge/road/bridge/
building-tier/landmark colors) and hex-stamp technique, but couldn't
call that method directly -- it's private and instance-bound to a live
`RuntimeCityBuilder`'s own `WorldOf`/`_origin`, neither of which exist
yet at picker time (no city has been placed in the world -- these are
throwaway preview models, never built into the scene). Reimplemented
against a standalone `CityModel` via `HexCoord.ToWorld()` directly
instead. Hovering a button (tracked via `Rect.Contains(Event.current.
mousePosition)`, the same pattern `Minimap.PointerOver` already uses)
swaps the preview panel to that option's thumbnail; nothing hovered
defaults to the first option so a thumbnail is always visible
immediately, no blank-until-first-mouse-move gap.

`RuntimeCityBuilder.ResolvePreset()` (private instance method, only
ever resolved `this.preset`) was split into itself (a one-line
forwarder) plus a new `public static ResolvePreset(PresetChoice choice)`
-- a pure function of the enum value all along, so making it static
cost nothing behaviorally and let the picker resolve any of the six
candidate presets without first having to point a live instance's field
at each one in turn.

**A real finding, not silently smoothed over**: a standalone console
check (real citygen-core DLL, all six actual presets, no stubs) showed
BigCity and NewYork -- both dense-by-design presets -- come back with
~100% of the 160x160 canvas touched by some non-ground color. First
instinct was to treat that as a bug and shrink the minimum `stampRadius`
below `Minimap.BakeTerrain`'s own proven `Clamp(..., 1, 6)`; tried a
floor of 0, which dropped BigCity/NewYork to ~95% but didn't fix
SmallTown/Paris/Montreal (all independently >90%, meaning stamp size
was never the actual constraint for them) -- and would have meant this
code no longer matches the ONE existing, presumably-already-tuned
baking convention in the project for no proven benefit, since there is
no way to see the actual rendered result in this environment and
confirm a lower floor genuinely reads better. Reverted to matching
Minimap's exact clamp; the ~100% coverage figure is recorded as an
honest, unresolved observation (a dense preset may simply read as a
busy, richly colored mosaic rather than a problem -- roads/buildings/
landmarks remain visually DISTINCT colors even when nearly every texel
is touched by one of them) rather than a claimed fix nobody can verify.

Verified three ways, no Unity Editor available in this environment: (1)
the six-script stub-compile from the entry above, extended with
`Texture2D`/`Color32`/`FilterMode`/`TextureFormat` stubs (the real
`Texture2D` derives from `UnityEngine.Object` -- the stub didn't
originally, which surfaced as a real compile error the moment
`RegionPickerHud`'s `OnDestroy` tried to `Object.Destroy` a cached
thumbnail, fixed by inheriting the stub from `Object` too) plus
`Mathf.Max`/`Clamp`/`RoundToInt`/`InverseLerp` and a static
`RuntimeCityBuilder.ResolvePreset` -- compiled clean, 0 errors/
warnings; (2) a standalone console check duplicating the bake math in
plain float/int (no UnityEngine types) against all six REAL generated
`CityModel`s via the real citygen-core DLL -- no exceptions, no
degenerate bounds, and a determinism check (same seed baked twice,
identical touched-texel count); (3) that same check is what surfaced
the coverage finding above, worked through rather than hidden. Not seen
in a real render.

Remaining: per-faction/per-kind real building art is still open. The
region picker itself has no further open items on this list.

## 2026-07: traffic headlights + brake lights, docs/28's first moving light

Creator direction: "let's add forward and slightly down facing lights
and brake lights to driving cars at night. Make it performant by only
rendering the ones in regions the user can see." Read `docs/28` §0.5
(the bug-history table) before touching `DynamicLightBudget`/
`GlowPointRegistry` at all, per the lighting skill's own standing
advice.

**The "performant, only in visible regions" half of the ask maps
directly onto the existing two-tier model** -- it didn't need a new
mechanism, just a new registrant. Headlights are Tier 1 (a small
emissive bulb, always cheap) + Tier 2 (a real `Light`, spent from the
SAME shared `DynamicLightBudget.budget` pool every streetlamp/window/
neon/marquee already competes in, nearest-to-camera wins). A far-away
or off-screen car's headlight simply never wins a budget slot and stays
Tier-1-only -- exactly "only render the ones in regions the user can
see," with zero new perf-management code. Brake lights don't need any
of this: they're an indicator, not something that should ever cast
light on its surroundings, so they're Tier 1 only, no budget
competition at all.

**Two real extensions were needed, though** -- the existing
`GlowPointRegistry`/`DynamicLightBudget` was built for STATIC registrants
(a streetlamp/window/neon prop registers once, forever, at dresser
build time):

1. **`spotAimsWithTransform`** (new optional param on `GlowPointRegistry.
   Register`, default `false`): the one existing Spot use case (the
   overhanging streetlight) always gets the SAME shared hardcoded
   straight-down aim (`DynamicLightBudget.SpotDownRotation`) -- correct
   for a fixture bolted to a pole, wrong for a car whose facing changes
   every frame as it drives and turns. When true, the promoted Light's
   rotation is instead copied from the registered Transform's own LIVE
   world rotation every refresh. `TrafficCar` gives it a dedicated child
   ("HeadlightAim") whose LOCAL rotation carries the fixed "slightly
   down" tilt (14 degrees), so its WORLD rotation always combines that
   tilt with wherever the car is currently facing.
2. **`isEligible`** (new optional `Func<bool>` param, default `null` =
   "always eligible," the exact behavior every EXISTING call site already
   has): a car's headlight is only relevant while driving at night. Doing
   this the "obvious" way (unregister when parked, re-register when
   departing) would have added real add/remove lifecycle to an otherwise
   append-only registry every other caller assumes never shrinks. Instead
   a parked or daylight car's headlight is registered once, for the
   car's whole lifetime, same as everything else -- but `Refresh()` now
   skips a point whose `isEligible` predicate returns false BEFORE it's
   even considered a budget candidate, so it never competes for or holds
   a slot, and a real Light never ends up shining out of a car whose
   headlights read as "off" to the player.

Both defaults were chosen specifically so every existing streetlamp/
window/neon/marquee call site is provably unaffected -- neither param
changes behavior unless a caller opts in.

**Materials**: one shared `Material` each for headlights (warm white)
and brake lights (red) across the WHOLE fleet -- SRP-batcher-friendly,
same caching idiom `BuildingDresser`/`RoadDresser`'s own `M()` helpers
already use -- registered with the existing `NeonRegistry` boost
pipeline exactly once, at first mint, so brightness tracks day/night
through the SAME global boost every other emissive prop already rides,
with no per-car per-frame color work. Per-car ON/OFF (driving vs
parked, braking vs not) is necessarily a SEPARATE per-instance concern
(a shared material's brightness is the same for every renderer using
it) -- handled by toggling each car's own bulb `Renderer.enabled`,
driven from `TrafficCar.UpdateLights`.

**Braking signal**: `MoveToward` is the one function every driving
`Update()` path (cruise, fleeing, roundabout circulation) already
funnels through, so it's the one place that needed to notice a
frame-to-frame drop in the `speed` value the caller asked for
(`BrakeDecelEpsilon` = 0.2). This naturally covers the existing
follow-traffic slowdown (a car easing off because something's ahead in
its lane) -- the primary real-world brake-light trigger. It does NOT
cover the abrupt stop-to-park transition (`ParkHere()` teleports
straight to the curb spot with no gradual deceleration modeled at all
today) -- a real, deliberate scope limit recorded here, not an
oversight; adding braking dynamics to the park transition is a
movement-behavior change, not a lighting one, and wasn't asked for.

**A real bug caught by flightcheck, not by inspection**: the first draft
of `MakeBulb` was `static` and never parented the spawned bulb
GameObject to the car's own transform at all -- `localPosition`/
`localScale` would have been interpreted as WORLD values on an unparented
object, scattering bulbs at the origin instead of mounting them on each
car. Made the method an instance method (needs `this.transform` as the
parent) and added the missing `SetParent` call; caught while re-reading
the diff before compiling, confirmed fixed by the stub-compile below
actually succeeding against the real file.

Verified two ways, no Unity Editor available in this environment: (1) a
`dotnet build` stub-compile of the REAL `DynamicLightBudget.cs`,
`TrafficCar.cs`, `ShaderUtil.cs`, `NeonRegistry.cs`, and `DayNightState.cs`
(the last three included verbatim, not stubbed -- they're either
self-contained or have no UnityEngine dependency at all) together
against the real citygen-core DLL, plus shape stubs for
`RuntimeCityBuilder`/`RoadDresser`/`MonsterAgent`/`CityLightingProfile`
(the four types these files reference that weren't worth pulling in
whole, same posture as every prior TrafficCar-adjacent entry in this
log) -- compiled clean, 0 errors/warnings; (2) manual re-read of every
`Update()`/`MoveToward` call path to confirm `UpdateLights` is reached
from every branch that resolves a frame (Parked, fleeing, roundabout
circulation, normal cruise) -- the one gap found (the `ParkHere(); return;`
transition doesn't call `UpdateLights` that same frame) self-corrects
within one frame via the Parked branch's own explicit `UpdateLights(false,
false)`, judged an acceptable, imperceptible lag rather than a bug worth
restructuring the existing movement logic for. Not seen in a real render
-- bulb position/scale/tilt numbers are reasoned from the same
fractional-offset convention `BuildBody`'s existing cabin/windshield
already use, not confirmed against an actual rendered car.

## 2026-07: streetcar/tram-rail, the docs/23 mood-board's last open item

Creator direction: "add streetcars/tram-rail props next." This is the
Phase 10 daytime mood-board's own deferred item (`docs/00-index.md`'s
Phase 10 status line: "the mood-board's streetcar/tram-rail prop is
explicitly deferred -- a materially bigger vehicle+region-gating system,
not a static prop"). Researched before writing anything: the mood-board
language (`docs/23-rts-master-build-plan.md` §10/§8, echoed in an
earlier `docs/12` entry) consistently hedges with "likely" every time it
names a region, but always the SAME region -- New York, tied to that
region's own real-world "elevated-rail segments" callout. No per-hex
district/region classification exists anywhere in citygen-core or any
Unity dresser (confirmed before writing this) -- the closest precedent,
`RoadDresser.DressRailSiding`, is a FREIGHT rail siding beside a road
near the cargo `rail_depot` landmark, a distinct concept from embedded
passenger tram track down a street. New York's own `Grid` road pattern
has no distinguished-arterial subset either (`CityModel.ArterialRoads`
is empty for `Grid`) -- so there was no existing per-hex "this road is
special" tag to key a tram line off of at all.

**The route itself had to be computed, not looked up.** New
`TramDresser.TraceLine(CityModel)`: starting from the nearest road hex
to `CityModel.CenterHex`, walks outward in each of the four world-
cardinal directions one offset-column/row step at a time (the exact
same stepping the 2026-07 cardinal-road rewrite already established,
`RoadDresser.Offset`) for as long as the road network keeps going,
including straight through a busy junction hex (correct -- a real
streetcar runs through an intersection, it doesn't stop tracing there).
Keeps whichever opposing pair (East+West or North+South) combines into
the longer total run -- the same "trace a fixed hex-direction walk"
technique Paris's own diagonal boulevards already use
(`CityPreset.Paris`), applied to find a route instead of generate one.
Below a minimum length (8 hexes), returns empty rather than drawing an
embarrassing two-block stub.

**A real design problem, found only by verifying against actual
generated cities, not assumed correct from reading the code.** The
first version had no upper bound. Running the real trace against a real
generated New York model (via a standalone console check, the real
citygen-core DLL) found it: a downtown grid's own straight run doesn't
naturally stop before the map edge -- **250+ hexes, ~5 km**. At
`TramCar`'s cruise speed that's roughly an 18-minute one-way trip; with
only two cars on a line that long, a player would essentially never see
one complete a loop -- present in the world but functionally invisible.
Added a `MaxLineLengthHexes` cap (40, split evenly across both arms and
kept centered on the start hex rather than skewed to one end), verified
down to a 39-hex/~780 m/~3-minute-one-way line. A second, smaller bug
surfaced by the SAME verification pass: the first cap implementation
(`MaxLineLengthHexes / 2` per arm) actually produced 41 hexes, not 40 --
off by exactly the shared start hex, caught by a real `<=` assertion in
the harness rather than eyeballing the arithmetic. Fixed by halving
`MaxLineLengthHexes - 1` instead.

**Track geometry** (`TramDresser.Build`, also new): two thin rail bars
(`RoadDresser.RailSteel`, now `public` specifically for this reuse --
same rail color as the existing freight siding, since they're visually
the same MATERIAL even though they're a different concept) embedded
barely proud of the road surface (0.03 m, vs. the freight siding's 0.12
m trackside bars) along each hex's own travel direction, positioned at
the SAME straightened centerline (`RoadDresser.CardinalAnchor`) every
road hex's own strip already uses. `Build` returns those exact world
points as `TramCar`'s path -- deliberately the same list the rails were
drawn from, not a second independently-computed set that could drift
out of alignment with what's visually on the ground.

**`TramCar`** is deliberately a much simpler vehicle than `TrafficCar`:
no wander, no park/depart cycle, no roundabout circulation -- a
streetcar is rail-bound, none of those apply. It walks the fixed path
back and forth forever (ping-pong on out-of-range index), at a slower,
steadier 4.5 vs `TrafficCar`'s 6.5 cruise speed. Since it can't swerve
around a threat the way road traffic does, it just pauses in place if a
monster is within a short lookahead radius directly ahead, resuming on
its own once clear -- a minimal safety check, not real flee/reroute
logic, which wouldn't make sense for a vehicle that has no alternate
route to take.

**Wiring**: `RuntimeCityBuilder.SpawnTram()`, called right after
`SpawnTraffic()` in `BeginMatch()`. Gated on `_city.Region ==
CityRegion.NewYork` -- a no-op, same as every other real-prerequisite
gate already in that method, for every other region/preset. Spawns 2
`TramCar`s spread evenly along the traced line rather than bunched at
one end.

Verified three ways, no Unity Editor available in this environment: (1)
a `dotnet build` stub-compile of the real `TramDresser.cs`/`TramCar.cs`/
`ShaderUtil.cs` against the real citygen-core DLL plus shape stubs for
`RuntimeCityBuilder`/`RoadDresser`/`MonsterAgent` -- caught one real
compile error that turned out to be a HARNESS gap, not a code bug: the
UnityEngine stub was missing `Mathf`'s `int` overloads of `Min`/`Max`
(real Unity has both `float` and `int` overloads; the stub only had
`float`), producing a false-positive `CS1503` on `Mathf.Max(0, i - 1)`
-- fixed the stub, not the real code, per the lighting skill's own
"always suspect the harness first" guidance; (2) a standalone console
check duplicating `TraceLine`'s exact logic in plain C# (no UnityEngine
types needed at all -- the pure hex-walk half doesn't touch any) run
against all six REAL generated presets via the real citygen-core DLL --
this is what found and confirmed both real problems above (the 5 km
unbounded trace, the 41-vs-40 off-by-one), plus confirmed every traced
hex is a genuine contiguous road hex, no duplicates, and determinism
across two runs; (3) manual re-read of the `RuntimeCityBuilder.cs` wiring
sites (too large a file to stub-compile whole, same posture as every
prior entry in this log). Not seen in a real render -- rail bar
thickness/gauge/height and the streetcar body's own proportions are
reasoned from the same fractional-offset/primitive-kit conventions
`RoadDresser`/`TrafficCar` already establish, not confirmed against an
actual rendered scene.

This closes the LAST item docs/00-index's Phase 10 status line still
listed as deferred from the original daytime mood-board pass.

## 2026-07: ResourceHud -- the last obvious gap in the UI/UX push

Creator direction: "continue with the UI/UX implementation." Most of
the original UI/UX punch list from earlier this session was already
done (build menu, ghost cursor, BaseDresser, moon-dial/mana/capture-bar
HUD, region picker + thumbnails) -- checked docs/00-index for what was
still genuinely missing rather than picking something arbitrary.

Found it by checking what `SimBridge` exposes versus what
`PlayerState` actually carries: `PlayerWallet` (a single resource's
balance) existed, added for the build menu's cost previews, but nothing
showed the STANDING balance across all six docs/05 currencies, and
nothing showed supply at all -- both real, tested sim state since Phase
1 (`SupplyUsed`/`SupplyCap`) and Phase 3 (`WalletCap`), completely
invisible in Unity. The exact same "sim ready, display missing" shape
every other HUD this session closed for its own system.

New `SimBridge.PlayerWalletCap`/`PlayerSupplyUsed`/`PlayerSupplyCap`
(three narrow pass-throughs, same defensive-default style as every
other accessor -- `PlayerWalletCap` returns `PlayerState`'s own
`int.MaxValue` "uncapped" sentinel when no match is running, so a HUD
reads "no cap yet" the same way a live match itself would rather than a
misleading 0). New `ResourceHud.cs`: Supply used/cap plus all six
`ResourceKind` wallets (icon + amount, `/cap` suffix only when finite),
tinting a line orange when a wallet is sitting AT its cap. Deliberately
its OWN panel, not folded into `LumenHud` -- docs/03 is explicit that
mana and components are never-interchangeable currencies, and every
other system already keeps them structurally separate (`PlayerState.
Wallet` vs `.Mana`, different grant/spend methods); merging them into
one glance-panel would blur a distinction the sim itself is careful to
keep. Default placement is top-right, below `AnalogClockHud` -- same
"developer nudges the final layout live, no Editor here to check it
against" honesty every other HUD element in this project already
carries.

Verified the same way as every HUD/build entry above: a `dotnet build`
stub-compile of the six real HUD/build scripts together (this one
included), extended with the three new `SimBridge` members -- 0
errors/warnings. Not seen in a real render.

With this, the original UI/UX list from earlier in the session has no
further open items; per-faction/per-kind building art (visual, not
UI/UX) remains the one thing still flagged open overall.

## 2026-07: real per-kind building silhouettes, closing the last flagged item

Creator direction: "sure go ahead and do that" (per-faction/per-kind
building art, the one item flagged open at the end of the entry above).
Loaded the maddr-aesthetic-preferences skill before touching any
geometry, per its own trigger condition ("world/city dressing... even
if the request doesn't explicitly ask about style").

The skill's §5 named the exact problem with `BaseDresser`'s existing
v1: "shape communicates origin/function, color communicates contents/
state... don't let one visual property carry two different facts."
v1's Complete-state visual was ONE uniformly-sized cube for every
`BuildingKind`, distinguished only by an HSV hue keyed off the kind --
color was doing the job shape should have been doing, and nothing was
using color for what it should actually carry in an RTS (whose building
is this).

**Shape now carries kind.** Each of the seven buildable kinds gets a
real two-primitive silhouette, still primitive-kit only (`RuntimeCityBuilder.
SpawnPrim`, no new mesh assets -- matching every other dresser's own
constraint in this environment): BloodStorage/FuelStorage share a tank
body + domed cap (the vessel read the skill's own example names
directly); FuelPump gets a pump house + offset nozzle pole; PartsStorage
a warehouse body + roof vent; HarvestPost a watchtower pole + platform
near the top; Factory a body + offset smokestack; Defense a bunker +
turret dome; Hq a keep + offset turret (Landmark-tier's own larger scale
already makes it the biggest footprint too, now also the most distinct
silhouette). `UnderConstruction` deliberately stays ONE generic scaling
cube regardless of kind -- you can't tell what a construction site will
become until it's built, so giving the scaffold real per-kind shape
would be less honest, not more.

**Color now carries owner + damage, not kind.** A two-color palette
keyed by PLAYER INDEX, flavored off docs/17's own per-faction registers
(a sickly organic green for index 0 -- today's demo always fields
MadDoctor there -- olive-drab military for index 1 -- Human Army; any
other index a neutral gray rather than guessing). This is a deliberate
APPROXIMATION, recorded as such rather than oversold: `SimBridge` has no
`FactionId` accessor for a given player index today, so this keys off
the index itself, not a real faction lookup. A true per-faction lookup
is real, separate, not-yet-built plumbing -- flagged, not faked.
Damaged state still darkens the same tint, same idiom v1 already used.

Structural change to support this: `BaseDresser` used to track one
`Dictionary<uint, GameObject>` (`_visuals`) with a DIRECT renderer on
the tracked object. Split into `_scaffolds` (UnderConstruction, one
scaling cube, unchanged from v1 behavior) and `_completed` (Complete/
Damaged, a ROOT GameObject holding the real per-kind shape as CHILDREN,
built once since buildings never move after placement, then only
re-tinted). `TintShape` walks `root.transform`'s children and reassigns
material on each -- shape-building and coloring are now genuinely
separate passes, matching the skill's own channel split structurally,
not just cosmetically.

Verified the same way as every prior HUD/build entry: a `dotnet build`
stub-compile of the six real HUD/build scripts (this one included)
against real match-core/citygen-core types and shape stubs -- caught
several real compile errors that were all harness gaps, not code bugs:
the stub was missing `Vector3.right`/`.forward`, `Mathf.Min(float,
float)`, and `Transform.childCount`/`.GetChild(int)` -- all completely
standard `UnityEngine` API this stub simply hadn't needed until this
file exercised it. Fixed the stub, not the code, per the lighting
skill's own "always suspect the harness first" guidance (same pattern
as the `Mathf.Min(int,int)` gap the streetcar entry above already hit).
Compiled clean afterward, 0 errors/warnings. Not seen in a real render
-- the shape proportions (tank radius fractions, pole/turret offsets,
etc.) are reasoned geometry, not confirmed against an actual rendered
building.

This was the only item still flagged open from the UI/UX-and-adjacent
work earlier in this session.

## 2026-07: worker-economy epic, Phase 1 (building HP/rubble/occupant disgorge)

Creator direction, delivered directly rather than as a design-doc
proposal: a five-part causal chain --

1. City/base buildings get larger HP and richer rubble when attacked/destroyed.
2. Destroyed buildings disgorge fleeing human occupants.
3. A new Collector unit captures and possesses those fleeing humans.
4. Possessed humans become Worker units (explicit SCV-from-StarCraft analogy).
5. Workers are required to construct buildings (at minimum Factories).

...with three faction-specific production mechanics layered on top: Mad
Doctor fields a "Big Brain" control unit costing 20 harvested Brains that
can control 100 humans; Aliens use mind control fueled by energy from
captured citizens; Humans recruit volunteers at lower unit cost but
requiring more resource types, explicitly meant to balance against the
other two.

Two clarifying-question attempts (`AskUserQuestion`, once for the
3-faction production specifics, once for build-order/scope) both came
back "the user did not answer the questions" -- the creator was sending
rapid sequential elaborating messages rather than answering structured
ones. Per this project's own "flag assumptions, don't silently guess OR
block forever" posture, proceeding on best-judgment interpretation,
stated back in prose for correction, building in dependency order.

**Resolved ambiguity before implementing:** whether "buildings" meant
citygen-core's procedurally-generated `Building` (cosmetic city dressing)
or match-core's player-constructed `SimBuilding`. Checked directly:
citygen-core's `Building` (`CityModel.cs`) is purely geometric --
footprint/tier/archetype, no HP field, no damage/destruction concept at
all, and `BuildingDresser.cs` has zero damage/rubble pipeline. Only
`SimBuilding` has real HP/armor/destruction. So "buildings that get
destroyed" can only mean player-constructed bases -- there is no other
system where "destroyed" is currently meaningful.

**This slice (dependency-free link #1 of the chain):**

- `BuildingDef.cs`: tier HP constants bumped 50% (Small 300→450, Medium
  600→900, Large 1500→2200, Landmark 3000→4500) -- armor left unchanged,
  so buildings take more hits to fell without becoming harder to damage
  per hit. v0.1 rebalance, same placeholder policy as every other number
  in this file. New per-kind `Occupants` field (static data, not
  simulated -- no decay, no SimBuilding state change) that Unity reads
  the instant a building flips to Destroyed. `Factory`'s 6 is the one
  deliberately-sized figure (it's what feeds the rest of this epic);
  everything else is a small flat garrison loosely scaled by tier
  (Hq=10, storage/pump kinds=2, HarvestPost/Defense=3).
- `DamageFx.cs`: new `BuildingRubble(at, parent, footprintScale)` --
  the existing one-shot `DustBurst` plus a new `RubblePileFx`, a
  lingering pile of scattered debris chunks sized off the building's own
  footprint, that fades and self-cleans after 40s (same fade convention
  as `GroundStain`) rather than accumulating forever in a long match.
- `BaseDresser.cs`: the Destroyed branch used to just despawn the
  GameObject with a comment flagging "no rubble/wreck FX yet" as a
  reasonable follow-up -- now fires that follow-up. A new
  `_destroyedHandled` HashSet guards it to once-per-EntityId (match-core's
  building list only grows, so without the guard this would refire every
  frame forever). Also spawns `def.Occupants` fleeing Citizens via a new
  `RuntimeCityBuilder.SpawnFleeingOccupant(HexCoord)`.
- `RuntimeCityBuilder.cs`: new `SpawnFleeingOccupant(HexCoord)`, same
  Citizen-creation shape as the existing match-start `SpawnCitizens`
  scatter, plus `NearestOpenHex` (the destroyed building's own hex is
  still blocked terrain at the instant of disgorge, so an occupant needs
  the nearest actually-open neighbor to stand on).
- `Citizen.cs`: new `InitFleeingFrom(origin)` starts a 4-second forced
  panic sprint away from the wreck point, independent of live monster
  proximity (a disgorged occupant should read as fleeing the collapse,
  not calmly window-shopping until a monster happens to wander within
  `FleeRadius`). Inserted between the existing capture-override and the
  existing proximity-flee check, so a future Collector can still capture
  a disgorged citizen mid-panic. Falls back to normal AI once the timer
  expires.

**Verification:** a new flightcheck harness
(`building-destruction-verify`) drives a REAL `MatchState`/`SimBridge`
through actual building placement, construction (ticked to Complete),
and destruction (`MatchState.ApplyBuildingDamage` via reflection on
`SimBridge`'s private `_match` field), then invokes the REAL
`BaseDresser.Update()` over it via reflection -- not a fake-shaped
building, an actual sim-driven one. Confirmed: exactly 6 Citizens spawn
the instant the Factory reaches Destroyed (not at Complete, not on a
second `Update()` call -- the once-only guard holds), and a disgorged
citizen's `transform.position` genuinely moves away from the wreck point
over simulated time (real `Citizen.Update()` invoked via reflection, the
same technique the earlier TrafficCar/TramCar driving-verify harness
used). `RuntimeCityBuilder` itself is stubbed narrowly rather than
compiled whole (2154 lines, drags in traffic/trams/lighting/minimap far
outside this check's scope) -- but `SpawnFleeingOccupant`/`NearestOpenHex`
in the stub are verbatim copies of the real new methods, so the stub
still exercises the actual logic, not a paraphrase of it.

**The harness caught a real bug, not a harness gap.** Every disgorged
occupant spawns exactly on its hex's dead-center (`Citizen.Init`'s own
`WorldOf(home)`, zero sub-hex offset). The forced-flee step distance (6m,
matching the existing proximity-flee code's own magnitude) is smaller
than a hex's inradius (~10m, from `HexCoord.HexMeters=20`'s pointy-top
geometry) -- so a flee step computed from dead-center can never cross
into a neighboring hex. The original code snapped the flee target to
`HexAt(fleeTo)`'s hex CENTER, which from dead-center rounds right back to
the citizen's own starting position -- it would have stood frozen for the
entire 4-second forced-flee window on every single destroyed building,
every time, not an edge case. Fixed by using the raw `fleeTo` world point
as the target (still gated by the same city/blocked legality check)
instead of re-snapping to a hex center, scoped to the new forced-flee
block only -- the older proximity-flee block (which in practice almost
never starts from exact dead-center, since a citizen is normally already
mid-walk when a monster gets close) is untouched.

All 246 match-core tests still pass with the HP/Occupants changes. Not
seen in a real render (no Unity Editor in this environment).

**Deferred, not faked, to later phases of the same epic** (tracked as
open tasks): the Collector unit and possess-into-Worker mechanic (a
strong candidate to reuse/extend the existing `CaptureState`
pull-toward-captor/consume-on-arrival system rather than building a
parallel one, per docs/26 Phase 6/7 -- not yet attempted); Worker units
and worker-gated construction; the shared `CommandKind.TrainUnit`
production-queue plumbing and the three faction-specific mechanics on top
of it (Mad Doctor Big Brain/Controlled-Human, Alien mind-control costed
in captured-citizen energy, Human cheaper-but-multi-resource recruiting).

## 2026-07: worker-economy epic, Phase 2 (Collector + possess-into-Worker)

Second link in the same chain: "collector units that capture and possess
those humans, who become worker units, like SCV in starcraft" (creator's
own words). Resolved before implementing: the deferred question from
Phase 1's entry above -- reuse the existing `CaptureState` pull mechanic
rather than build a parallel one. Confirmed the right call by reading
`WebAttackAbility.cs`/`UnitCombat.cs`/docs/26 directly: `Citizen.Capture`
already IS the generic capture entry point (any `UnitCombat` can be a
captor), and `UnitCombat.IsPossessed` already exists as a forward-
compatible hook for a DIFFERENT possession concept (Mad Doctor mind-
control on monster units, for friendly-fire immunity, docs/26 §5) --
NOT unit-conversion, so it's a related but distinct mechanic, not
something to overload.

Also resolved: whether a Collector/Worker needs to be a match-core
`SimUnit`. Grepped `packages/match-core/src/*.cs` for "Capture"/"Possess"
-- zero hits. Confirmed via docs/26 §1's own framing: today's ENTIRE
combat layer (`MonsterAgent`/`Tank`/`UnitCombat`/`Citizen`/`CaptureState`)
is Unity-side only, client-cosmetic-to-fully-live depending on the unit;
match-core's parallel sim-driven combat is still opt-in demo scope
(`simDrivenDemo`), not the default live path. So a Collector/Worker
following `Tank.cs`'s established pattern -- a bespoke, non-genome
`MonoBehaviour` with a plain `UnitCombat`, same tier as Tank, not
MonsterAgent's genome-driven tier -- is the consistent choice, not a
shortcut.

**Shipped:**

- `Citizen.cs`: `Capture(UnitCombat captor, float speed, bool possess =
  false)` -- the existing web-attack call site (`WebAttackAbility.cs`)
  keeps compiling unchanged via the default. New `IsCaptured` public
  getter (a Collector needs to skip a citizen someone else already has a
  hook into). Arrival branch now checks `_possessOnArrival`: true routes
  to a new `RuntimeCityBuilder.OnCitizenPossessed`, false (the existing
  behavior, unchanged) still routes to `OnCitizenEaten`.
- `RuntimeCityBuilder.cs`: new `OnCitizenPossessed(citizen, collector)`
  -- spawns a `Worker` at the citizen's position, destroys the citizen,
  no wallet credit (the worker IS the payoff). New `SpawnCollector(hex)`
  -- a manual test/dev entry point, NOT wired into any match-start spawn
  flow. Real reason, not laziness: the actual way a player should field
  a Collector ties into Phase 4's still-unbuilt Mad-Doctor production
  mechanic (the "Big Brain" control unit / 20-harvested-Brains cost) --
  auto-spawning some arbitrary count now would be inventing a number
  Phase 4 should own. `Collector`/`Worker` accessor lists added alongside
  the existing `Monsters`/`TrafficCars`/`Citizens` pattern.
- `Collector.cs` (new): seeks the nearest uncaptured Citizen within
  45m, closes to 3.5m, then calls `Citizen.Capture(_combat, 5f, possess:
  true)`. No weapon, no combat -- pure gatherer, closest in spirit to the
  existing Ghoul's auto-scavenge role. Simple direct-seek movement, no
  Tank-style road-preference steering (a lighter mover doesn't need it).
  Violet-hulled boxy-cart-with-funnel silhouette -- distinct shape from
  every other unit kind, reading as Mad Doctor apparatus.
- `Worker.cs` (new): low-HP (40), no-weapon economic unit -- exists, has
  a real `UnitCombat`, can be targeted/killed like anything else, stands
  where it was possessed. No move orders or construction behavior yet;
  Phase 3 of this epic is what puts it to work. Capsule-plus-hard-hat
  silhouette, dull khaki -- distinct from a Citizen's random civilian hue
  and from either faction's combat palette (aesthetic-preferences skill
  §5: shape carries kind, color carries owner/state).

**Verification:** a new flightcheck harness (`collector-worker-verify`)
builds a real `MadDr.RosterClient.dll` (for the real `WeaponProfile`/
`WeaponKind` types `UnitCombat.cs` needs) and compiles the REAL
`UnitCombat.cs`/`CaptureState.cs`/`Citizen.cs`/`Collector.cs`/`Worker.cs`
together (narrow stubs only for the special-attack FX/cooldown plumbing
these two new units never actually reach, since both configure with
`weapon: null`). Reflection-drives a real `Collector` chasing a real
`Citizen` ~20m away over simulated time: confirms it closes the distance,
captures, and the citizen becomes exactly one real `Worker` with its own
live `UnitCombat`; a parallel regression citizen (default, non-possess
`Capture()`) confirms the original eat-for-resources path is provably
unchanged.

**The harness caught a real bug -- in the harness, not the shipped
code, and worth recording as its own lesson.** The stub's `Object.
Destroy` was a no-op, so reflection-invoking `Citizen.Update()`
unconditionally every simulated frame kept re-running the arrival
branch on an already-"destroyed" citizen -- 529 Workers spawned instead
of 1. Real Unity stops calling `Update()` on a destroyed `GameObject`
starting the very frame `Destroy()` fires, so this can never reproduce
in actual gameplay; confirmed by fixing the STUB (tracking a `Destroyed`
flag, having the harness loop respect it) rather than the real code,
and rerunning clean. Distinguishing "a real gameplay bug" from "a
harness that doesn't model one specific piece of engine behavior" is
exactly the judgment call this project's own verification discipline
keeps asking for -- recorded here as a concrete example of getting that
call right, not just the earlier Phase 1 entry's opposite case (a real
bug the harness correctly caught).

Not seen in a real render. **Deferred to Phase 3/4 of the same epic**:
worker move orders/selection, worker-gated building construction, and
the actual spawn trigger for Collectors (Phase 4's Mad-Doctor
production mechanic).

## 2026-07: worker-economy epic, Phase 3 (worker-gated Factory construction)

Third link: "Workers required to construct buildings." Scoped to
Factory only, per the creator's own exact words ("Factories are built
by possessed human workers") -- not a guess, not "every building now
needs a worker."

`BuildGhostCursor.cs`'s existing `canPlace` check (already `bridge.
CanPlaceBuilding(...)`, the same check match-core applies when the
command actually lands) gained one AND term:
`&& (!RequiresWorker(kind.Value) || builder.Workers.Count > 0)`. New
`RequiresWorker(BuildingKind)` -- a method, not an inline comparison,
so it reads as a deliberate, easily-widened policy point -- returns
true only for `Factory`. This single boolean feeds BOTH the ghost's
red/green tint preview and the left-click confirm gate identically,
so an unavailable-worker placement reads exactly like an unaffordable
or illegal one already does -- no separate error state to design.

**Explicitly, honestly scoped as UI-layer-only, not sim-enforced.**
match-core's `ApplyBuildStructure`/`CanPlaceBuilding` have no concept
of a Worker at all -- Workers are a Unity-legacy-combat-layer unit
(`Tank.cs`'s pattern, per Phase 2's own entry above), not a match-core
`SimUnit`, and match-core has zero engine/Unity reference by design. So
a `SimBridge.QueueBuildCommand` call that bypasses this ghost cursor
(a bot, a different UI, a future networked client) would still place a
Factory with zero workers -- a real gap, flagged here rather than
silently left undocumented. Closing it for real needs either (a) a new
match-core-side "requires worker" concept threaded through
`BuildingDef`/`ApplyBuildStructure`, which would need SOME way for the
sim to know a worker exists (Workers becoming real `SimUnit`s, most
likely), or (b) accepting this as a permanently UI-only convenience gate
for a single-client game. Not decided here -- flagged for whoever picks
up real multi-client/bot support.

Verified by reflection-invoking the new `RequiresWorker` method
directly against every real `BuildingKind` value from the actual
match-core DLL: Factory true, all seven other kinds false. The
containing `Update()` loop itself needs a real mouse/camera raycast to
exercise end to end, which was judged out of proportion for this
change's actual risk -- a single boolean AND against an existing,
already-tested `CanPlaceBuilding` check, not new placement/cost/
construction logic.

Not seen in a real render. **Deferred to Phase 4**: the actual spawn
trigger for both Collectors and the first Worker a player ever has
(today a player literally cannot build a Factory until some Collector,
itself not auto-spawned either, manually possesses a citizen) -- Phase
4's Mad-Doctor production mechanic is what's supposed to close that
bootstrapping gap.

## 2026-07: worker-economy epic, Phase 4 (three-faction production)

Final link, and the one this epic's earlier phases (and docs/12's own
Phase 6c entry: "no unit-production command exists in match-core at
all") had been building toward. Full spec, creator's own words: "Mad
doctor must make a big brain control unit requiring harvesting 20 brain
units, that can control 100 humans. Aliens use mind control, requiring
energy from captured (something similar) and Humans have a recruiting
volunteers, some smaller cost but require more resources, that balance
against Aliens and Mad Dr."

**Resolved before implementing:** Mad Doctor gets no `RosterUnitKind`
at all (`FactionRoster.cs`'s own header: "the Doctor's whole identity
is fielding CUSTOM bred creatures... there is nothing for a roster
table to enumerate for that faction"). So the Big Brain "control unit"
can't be a trainable roster entry the way Human/Alien units are -- it's
modeled as a `BuildingKind` instead (the same category as `Hq`), and
"controls 100 humans" becomes a Supply-cap raise, reusing `PlayerState.
RaiseSupplyCap` (existing since Phase 1's own summary flagged it as
"currently, currently-unused" -- now used for the first time) rather
than inventing a parallel "population" concept.

**Shared plumbing** (the part actually gated on before this phase):

- `Command.cs`: `CommandKind.TrainUnit = 8` -- TargetEntity is the
  producing building's entity ID, ArgA is the `RosterUnitKind` to
  train (cast to int), same "generic slots reinterpreted per Kind"
  contract every other command already uses.
- `SimBuilding.cs`: `TrainingKind`/`TrainTicksRemaining` plus
  `BeginTraining`/`TickTraining` (internal, MatchState-only, same split
  as construction's own `Tick()`). **v0.1 deliberately ships ONE
  in-progress slot per building, not docs/22 §7's Stitchworks 5-deep
  queue** -- a real, smaller scope than that design, flagged rather
  than either faked as deeper or silently left unbounded. Both new
  fields are in `WriteTo`'s determinism hash (`-1` sentinel for "no
  training kind," since the enum starts at 0 and never needs a second
  bool).
- `MatchState.cs`: `CanTrainUnit` (pure validity check: building
  exists/owned/Complete/slot-free, kind belongs to the player's own
  faction, every `Cost` line affordable) and `ApplyTrainUnit` (debits
  all-or-nothing, same order as `ApplyBuildStructure`, BEFORE opening
  the slot) -- one shared check for both the eventual UI preview and
  the actual command, same "can never disagree" precedent as
  `CanPlaceBuilding`. `SpawnTrainedUnit`/`FindOpenHexNear` resolve a
  spawn point by ring-searching outward from the producing building's
  own (necessarily blocked) hex -- the match-core-side twin of Phase 1's
  Unity-side `RuntimeCityBuilder.NearestOpenHex`, independently
  reimplemented since match-core has no reference to that file. The
  per-tick building loop (`Tick()`) now calls `TickTraining()` right
  after construction's own `Tick()`, spawning on completion via the
  same `SpawnUnit` entry point `SpawnRosterUnit` already uses --
  NOT through `SpawnRosterUnit` itself, since that method's own doc
  comment explicitly names "mid-match production from a Factory" as
  the "different, not-yet-built feature" this phase now IS, and its
  throw-on-faction-mismatch contract is wrong for a command handler
  (a bad TrainUnit command must be a silent no-op, never an
  exception) -- `CanTrainUnit` enforces the faction match instead,
  before `ApplyTrainUnit` ever reaches the spawn path.
- `UnitRosterDef` (`FactionRoster.cs`): new `Cost`/`TrainTimeTicks`
  fields on every existing entry.
- `SimBridge.cs`: `QueueTrainCommand`/`CanTrainUnit` pass-throughs,
  same one-tick-latency/never-self-validates contract as the existing
  `QueueBuildCommand`/`CanPlaceBuilding` pair. Not yet consumed by any
  Unity HUD -- a training-queue UI is real, separate, not attempted
  here (mirrors `BuildMenuHud`'s own eventual job, not built this
  pass).

**Per-faction costs**, v0.1 placeholder NUMBERS (same standing policy
as every other cost table in this codebase) but a deliberately real
SHAPE match to the creator's own spec:

- **Human Army**: existing roster (Rifleman/Half-Track/Tank/Zeppelin
  Gunship), each costed in Bones + Fuel -- two resource kinds, "smaller
  cost but require more resources." A `HumanArmy_recruitCostSpansMultipleResourceKinds`
  test asserts the SHAPE (>=2 resource kinds), not just that training
  works at all.
- **Alien Hive**: existing roster (Drone/Spitter/Floater Queen), each
  costed in a single, heavier Ichor line -- "mind control... energy,"
  Ichor being the Hive's own real energy currency (`FactionDef.
  Get(AlienHive).Energy == Ichor`, confirmed via an existing test). The
  "energy from CAPTURED (something similar)" clause -- i.e. Ichor
  income actually being tied to captured citizens rather than a flat
  faction income tick -- is explicitly NOT built here; only the Ichor
  SINK (spending it to train) is real, the SOURCE stays deferred, same
  ambiguity flagged since the epic's very first message (the creator's
  own phrasing trailed off: "energy from captured (something similar)").
- **Mad Doctor**: `BuildingKind.BigBrain`, 20 Brains (the one
  deliberately-sized, non-placeholder cost in this entire epic --
  every other number here is an honest guess), Large tier HP/armor,
  zero `Occupants` (a control apparatus, not a staffed building --
  nothing to disgorge if it falls, unlike every other buildable kind
  from Phase 1). Raises Supply cap by 100 once Complete, via a new
  `BuildingDef.SupplyCapBonus`/`MatchState.ApplySupplyCapBonus` --
  copy-pasted in spirit from `StorageCapBonus`/`ApplyStorageCapBonus`
  rather than generalizing that existing mechanism, since Supply and
  wallet resources are genuinely different currencies with different
  caps (`PlayerState.SupplyCap` vs `WalletCap`) and forcing one field
  to mean either would be the wrong kind of code reuse.

**A real, flagged architectural tension, not smoothed over:**
`BuildingDef.cs`'s own header states the existing design law plainly
-- "stats are shared across factions, only names are themed." This
phase's three costs are NOT shared: Mad Doctor pays Brains, Aliens pay
Ichor, Humans pay Bones+Fuel, for mechanically different buildings/
units entirely (not the same `BuildingKind`/`RosterUnitKind` with a
per-faction cost override, which the current data model has no way to
express anyway). This was a deliberate, examined choice: implementing
the three-way "balance against each other" the creator asked for
inherently requires faction-differentiated economics, which the
existing generic-stats law was never designed for. Reusing three
SEPARATE mechanisms (existing roster training for two factions, a new
building kind for the third) was judged less architecturally invasive
than bending `BuildingDef`/`UnitRosterDef` to carry per-faction
overrides on top of their current shared-data shape -- but that reuse
itself docs/17's real per-faction economic design (a much larger,
already-written document this phase only lightly touches) may call
for later. Flagged for whoever does a real faction-balance pass.

**Verification:** 9 new xunit tests in `TrainUnitTests.cs`
(`packages/match-core/Tests~`), the project's own strongest form of
verification for match-core changes (a real `dotnet test` run, not a
flightcheck harness) -- `CanTrainUnit` gating on Complete state, cost
debited all-or-nothing (checked as a DELTA against a pre-training
balance snapshot, not an absolute zero, since the test's own Factory-
build setup leaves leftover Bones/Blood on the wallet -- an early draft
asserted the wrong thing here and the tests correctly failed, a test
bug caught by the tests themselves, not a product bug), the single-slot
no-double-debit guard, faction-mismatch rejection, completion timing
(spawns on the exact tick training finishes, not before/after -- same
off-by-one-aware style as the existing `Construction_completesExactlyAtItsBuildTimeTicks`
test), the Alien Ichor-cost shape, the Human multi-resource-cost shape,
and BigBrain's Supply-cap-once-not-on-every-subsequent-tick behavior.
255 match-core tests total, all passing. `SimBridge.cs`'s new
`QueueTrainCommand`/`CanTrainUnit` pass-throughs were flightcheck-
compiled standalone against the real match-core DLL (0 errors) -- no
Unity Editor available in this environment, same standing limitation
every other Unity-side entry in this log already notes.

**Deliberately NOT attempted, flagged rather than faked:**
`SkirmishCommander` (the AI) never issues `TrainUnit` -- a real,
separate AI-wiring job. No Unity HUD consumes the new `SimBridge`
methods -- a training-queue panel is real, separate scope (mirrors
`BuildMenuHud`'s own job for buildings). And, most importantly, this
phase does NOT close the epic's own bootstrapping gap: a player still
has no way to get their first Collector or Worker without one already
existing (Phase 2/3's `SpawnCollector` is still a manual-only entry
point) -- BigBrain raising Supply cap doesn't by itself produce a
Worker or unlock Collector training, since Collector was never given a
`RosterUnitKind`/roster-training path either. Closing that loop for
real (does the Big Brain building itself produce Collectors? does it
need its own dedicated production slot, separate from the generic
Factory queue this phase built?) is a genuine open design question the
creator's own spec doesn't fully answer, left here rather than guessed
at.

This closes all four tracked phases of the worker-economy epic
(buildings/rubble/occupants -> Collector/possession -> Worker-gated
construction -> three-faction production). The bootstrapping gap noted
just above is the one loose thread carried forward, not a phase of its
own.

## 2026-07: traffic simulation gated by camera proximity

Creator direction, verbatim: "change where cars are driving to the
areas close to or near the player view area. Let's not waste
processing power." Two changes, both in `TrafficCar.cs`/
`RuntimeCityBuilder.cs`:

1. **Activity freeze.** A car farther than a new `RuntimeCityBuilder.
   trafficActiveRadius` (Inspector-tunable, default 130m -- a v0.1
   placeholder tuned to roughly a typical RTS camera's visible ground
   footprint, not measured against a real Editor session) from the
   camera skips its ENTIRE `Update()` -- the very first line, before
   the threat scan, roundabout circulation math, follow-distance check,
   or any movement. It simply holds its exact position/state until the
   camera comes back near it, then resumes from exactly where it froze
   -- imperceptible to a player who wasn't looking at it, by
   construction.
2. **Route bias.** `TrafficCar.PickNext`'s existing wander-hash scoring
   (already penalizes candidates near a monster, per the pre-existing
   `MonsterAwareRadius` term) gained a camera-distance penalty, so an
   ACTIVE car's multi-hop trip statistically curls back toward the
   view instead of wandering off to drive circles nobody will ever
   see -- addressing the "change WHERE cars are driving to" half of
   the direction, not just the "don't simulate what's off-screen"
   half.

**Central refresh, not per-car polling.** A new `RuntimeCityBuilder.
RefreshTrafficActivity()` recomputes near/far for the WHOLE fleet plus
caches the camera's own ground position once, on a throttled 0.35s
cadence -- the exact same cadence and "cam == null: bail" defensive
convention `DynamicLightBudget.Refresh` already established for the
same reason (many objects, one shared camera lookup, refreshed
periodically rather than every object polling `Camera.main` every
frame). Folded into the SAME shared `Update()` the existing traffic-
band check and deadlock poll already share, as a third independent
rare-path timer -- matching that method's own documented convention
rather than adding a fourth separate `MonoBehaviour`.

**Weight tuning, and a real tuning mistake caught before landing.**
`PickNext` only ever compares NEIGHBORING candidate hexes (~20m apart,
one `HexCoord.HexMeters` step) against each other -- never the whole
city span -- so the bias weight has to matter at THAT scale, not a
city-scale one. An initial `CameraBiasWeight` of 45 was reasoned as
"comparable to the 0..65535 wander hash over city-scale distances,"
which is the wrong scale entirely: at a single hex-step's distance
delta (~20m), a weight of 45 produces a penalty difference of under
1000 against a hash whose typical spread between two random candidates
is on the order of 25,000+ -- statistically negligible, confirmed
directly by the verification harness (identical outcomes to the
decimal place regardless of camera direction). Retuned to 250 -- still
a real bias, not a hard override (verified statistically, not as a
guaranteed flip on every single junction).

**Verification, and two real test-authoring bugs it caught (fixed in
the test, not the shipped code)** -- the existing `driving-verify`
flightcheck (real `TrafficCar.cs` reflection-driven over simulated
time, same technique as every other traffic/tram verification this
project has used):

- Confirmed the freeze directly: a car marked far from the camera
  produces byte-identical position/rotation over 60 simulated seconds;
  marking it near again resumes real movement immediately.
- The FIRST attempt to verify the route bias used a long straight
  1-hex-wide corridor network and measured "final X position after
  many simulated hops." This failed even after the weight fix, which
  is what caught it: `PickNext` always excludes `_from`, so on a line
  network every hop past the very first has exactly ONE candidate (no
  real choice at all) until a dead end forces a reversal -- "final
  position" was actually measuring which phase of an endless back-and-
  forth oscillation the car happened to be in after N seconds,
  completely unrelated to which direction the bias had originally
  preferred at the one hop where it could matter. Diagnosed by reading
  `PickNext`'s own `_from`-exclusion logic directly, not by guessing.
- Fixed by switching to a real branching disk network and a
  statistical test across many independent junctions (the bias is a
  NUDGE, not a guarantee, so a single sample isn't a fair test either)
  -- which then produced a perfectly consistent, but COMPLETELY
  INVERTED result: moving the camera east made every single trial pick
  a hex to the west. Diagnosed by rereading `HexCoord.ToWorld()`
  directly: world X is `size*(sqrt(3)*Q + sqrt(3)/2*R)`, mixing both Q
  and R -- the test's trials swept a whole hex disk (many different R
  values) but compared raw axial `.Q` as a stand-in for "east/west,"
  which is only valid exactly on the R=0 row. Fixed by comparing real
  `ToWorld()` X coordinates instead, which produced the expected,
  correctly-signed result. Both bugs are recorded here in full because
  they're a useful concrete pair: one caught a genuine PRODUCT
  weakness (the tuning), the other two were purely TEST-side (in this
  same feature, no less) -- worth distinguishing carefully rather than
  either dismissing a failing check OR assuming a failing check always
  means the product is wrong.

Not seen in a real render (no Unity Editor in this environment) --
the 130m active radius and 250 bias weight are both reasoned
estimates, not measured against an actual camera/city at real scale.

## 2026-07: naturalistic driving -- speed variance + aggressive passing

Creator direction, verbatim across two messages: "Verify naturalistic
driving. Speed up and some aggressive passing to slow cars. try
again." Two changes to `TrafficCar.cs`:

1. **Personal speed variance.** `_personalSpeedMult` (0.8x-1.35x of
   the base `CruiseSpeed`), hash-rolled once at `Init` off the car's
   start hex + `GetInstanceID()`, same deterministic-per-car-variance
   idiom every other roll in this file already uses (park-timer
   stagger, body truck/sedan pick, curb-side sign, etc.). This isn't
   just flavor -- it's the actual PRECONDITION that creates a
   genuinely slow car for a faster one to catch up to and want to pass
   in the first place. Applied to the car's own cruise/follow speed;
   deliberately NOT applied to `FleeSpeed` (panic is uniform -- every
   car floors it the same when scared, a deliberate scope limit, not
   an oversight).

2. **Passing maneuver.** New `_blockedTimer`/`_passing`/`_passTimer`
   state. Sustained close following (`_blockedTimer >
   PassBlockedTriggerTime`, ~1s) behind something reading as
   genuinely slow (`clear < FollowRange * PassTriggerClearFraction`,
   not just momentarily close) checks whether the OPPOSITE side of the
   road is clear far enough ahead (`DistanceAhead` queried from a
   laterally-offset position, `PassLaneCheckRange` -- longer than the
   normal following check, since committing to a pass needs more
   runway than just keeping a gap does) and, if so, commits: steers
   toward a close lookahead point offset to that side, at
   `PassSpeedBoost` (1.45x) speed, re-validating the passing side is
   still clear EVERY frame it's committed (real-time abort back to
   normal following if something appears there, not a one-time check),
   with a `PassMaxDuration` safety cap. Ends successfully once the
   car's own original lane reads clear ahead again -- the whole point
   of the maneuver.

**Two real issues surfaced and fixed before this landed, both worth
recording distinctly (one product, one nothing-to-do-with-testing):**

- **Sign error, caught by re-reading the code, not by testing.**
  `RoadPoint`'s own `right` vector convention (`(dir.z, 0, -dir.x)`,
  the SAME direction a car's own lane offset already sits at, per its
  own doc comment: "to the RIGHT of travel") means a car's current
  position already sits at roughly `+right*LaneOffset` from the road
  centerline. The first draft of the passing offset ADDED more
  `+right*PassLaneOffset` on top of that -- pushing the car FURTHER
  into its own existing side, not across to the opposite one. Fixed
  by negating it (`-right*PassLaneOffset`), confirmed by re-deriving
  `RoadPoint`'s own geometry directly rather than guessing which sign
  "felt right."
- **Shallow-angle geometry, caught by the verification harness.** The
  fixed-sign version still under-delivered: steering toward the
  far-off next-hex ROAD TARGET (`_target`, typically ~`HexMeters`
  =20m away) offset sideways by a fixed `PassLaneOffset` produces only
  a SHALLOW angle relative to the remaining travel distance -- the
  harness measured out real, honest numbers here (a first attempt hit
  only ~1.0m, then ~1.9m of actual lateral clearance after retuning
  the test's own obstacle placement) before the maneuver concluded,
  nowhere near a visually convincing "crossed to the opposite side"
  read. Root cause diagnosed by working through the actual swerve
  trajectory's trigonometry (angle = atan(offset / remaining
  distance) -- shrinks as remaining distance grows), not by trial and
  error. Fixed by aiming at a close, continuously-recomputed
  LOOKAHEAD point (`PassLookahead`, 6m ahead of the car's CURRENT
  position each frame) instead of the far-off hex target -- a real
  product change, verified to produce a genuine ~3.3m swerve (clear
  of the own-lane check's own `LaneHalfWidth`=2.4m filter, so it
  actually reads as a lane change to anything else's own following
  check, not just a wobble).

**Verification.** The existing `driving-verify` flightcheck's own
`RuntimeCityBuilder` stub had `DistanceAhead` hardcoded to always
report "clear" (a deliberate simplification when that harness was
first built, since nothing in its scope needed real blocking) -- with
nothing for the new follow/pass logic to react to, this feature
couldn't be verified at all as-is. Extended the stub with a real (if
simplified) `DistanceAhead`: a verbatim port of the actual method's
dot-product-projection + lateral-filter logic (confirmed line-for-line
against `RuntimeCityBuilder.cs`, not reimplemented from memory),
scanning a simple injected `Obstacles` list instead of the real
`_trafficCars`/`_tanks`/`_citizens` collections -- sufficient for a
synthetic "something is blocking this lane" test without pulling in
those other systems. Every pre-existing check in that harness
(movement, camera-proximity gating/bias) never populates `Obstacles`,
so their behavior is provably unchanged. New checks added: (1) 8 cars
run unblocked for 20s show a real >1.35x spread in distance covered,
confirming actual speed variance, not just a per-car field that's
never read meaningfully; (2) a car blocked by a stationary obstacle
placed at a realistic following distance (not spawned already on top
of it -- an early version of this check placed it too close, which is
what surfaced the shallow-angle issue above) genuinely enters the
passing state, swerves a real, measured distance wide of its own lane,
and clears the obstacle within the simulated window rather than idling
behind it forever.

Not seen in a real render (no Unity Editor in this environment) -- the
speed-variance range, the passing trigger thresholds, and the
lookahead/offset distances are all reasoned v0.1 placeholders tuned
against the flightcheck's own numeric output, not an actual Editor
session's visual read.

## 2026-07: safety net -- nearest-K traffic cars always stay active

Added alongside the camera-focus fix below, before the ACTUAL root
cause (the entry two below this one) was found: a new
`trafficActiveMinimumCount` (default 12) guarantees the fleet's own
nearest N cars stay active regardless of what the radius/focus-point
calculation says -- the same "always promote the closest N, don't rely
on a single absolute threshold" principle `DynamicLightBudget` already
uses for real lights. Even if a future camera-geometry miscalculation
(this one, or one this project hasn't hit yet) makes the radius check
wrong again, it becomes structurally impossible for that alone to
freeze 100% of visible traffic -- whatever's nearest the camera keeps
driving no matter what the rest of the math says. Default (12) is
deliberately just above the default `trafficCarCount` (10), so this
entire feature is a genuine no-op for the out-of-the-box scene --
every car always qualifies as one of the "nearest 12" out of only 10.
Verified with a pure-algorithm test (`camera-focus-verify`, no Unity
dependency needed): confirms the nearest N are correctly selected even
when the radius check finds zero cars in range, and confirms a
smaller-than-floor fleet is unconditionally 100% active regardless of
distance.

## 2026-07: FIX -- traffic freeze regression (wrong camera ground-focus point)

Creator report, verbatim: "still non of the cars are moving, in the
editor." A real, severe regression from the camera-proximity gating
feature two entries above -- traced to a geometry mistake in that
feature's own implementation, not a new bug in the passing/speed-
variance work that followed it.

**Root cause.** `RuntimeCityBuilder.RefreshTrafficActivity` computed
its "where is the player looking" reference point as `Camera.main.
transform.position` (Y zeroed) -- i.e., treated the camera RIG's own
transform as if it sat directly above whatever it's looking at. It
doesn't. `SimpleCameraRig.SnapTo` places the camera at `focus + (0,
height, -height*0.8)` -- offset both UP (by `height`) and BACKWARD (by
`0.8*height`) from the actual look-at point, a completely ordinary and
correct way to build an angled RTS camera rig, just incompatible with
treating the rig's raw position as a ground coordinate. At the game's
own actual match-start camera height (70, from the `rig.SnapTo(...,
70f)` call in `BeginMatch`), that's an error of exactly `70*0.8 = 56`
meters -- not a rounding error, a completely different neighborhood of
the map. Since the default 10-car traffic fleet spawns scattered
across the ENTIRE generated road network (`SpawnTraffic` distributes
across all of `RoadNetworkHexes()`, not clustered near city center),
and the feature's active radius was ALSO a fixed, likely-too-small
130m guess, this combination very plausibly froze the whole fleet
within the first 0.35-second refresh of every single match -- exactly
matching "still none of the cars are moving," not an intermittent or
edge-case failure.

**Fix, two parts:**

1. **Correct reference point.** `RefreshTrafficActivity` now raycasts
   from the camera through the VIEWPORT CENTER to the ground plane
   (`y=0`) to find the actual point the camera is looking at --
   exactly the same question `SimpleCameraRig.FocusOn` already answers
   for its own G-key-jump-to-unit feature via a near-identical ground
   raycast (`GroundUnderScreen`). Reimplemented locally in
   `RuntimeCityBuilder` rather than calling into `SimpleCameraRig`,
   since the only thing needed is the math, not that component's own
   drag-state/instance. "Fail open" on every degenerate case: no
   camera, the look-ray running parallel to the ground (never actually
   produced by `SnapTo`'s fixed pitch, but player-driven scroll/rotate
   input could in principle), or the ground plane being behind the
   camera along that ray -- all keep the LAST known good focus rather
   than computing (or silently defaulting to) a wrong point, so a bad
   frame can only ever leave traffic in its last correctly-computed
   state, never actively break it further.
2. **Camera-aware active radius**, replacing the fixed 130m guess.
   `SimpleCameraRig` already has an analogous, ALREADY-TUNED formula
   for a related question -- "how far does the visible ground extend
   from the camera's look-at point" -- used to size URP's shadow
   distance (`shadowDistancePerHeight=1.9`, tied explicitly by its own
   comment to `SnapTo`'s exact camera-to-ground ratio, `height*1.28`,
   plus margin "so the covered area extends to the actual visible
   frustum, not just the exact center point"). Reused that same ratio
   directly (`trafficActiveRadiusPerCameraHeight=1.9`, floor 60,
   cap 320) instead of inventing a second, independent guess for
   what's really the same underlying question. At the actual
   match-start height of 70, this gives an active radius of ~193m,
   comfortably covering a scattered 10-car fleet near the start
   position, versus the old fixed 130m radius centered on a point that
   was ALSO wrong by 56m.

**Verification.** No Unity Editor exists in this environment, so this
couldn't be re-checked visually the way the creator's own report
found it -- but the bug and its fix are both pure vector geometry, not
engine behavior, so a plain C# console program (no Unity types
involved at all) that replicates `SnapTo`'s EXACT camera placement
formula and fixed 50-degree pitch was written and run for real. It
confirms, at camera heights from 8 to 400 and multiple arbitrary focus
points (not just the origin, ruling out a coincidental match there):
the OLD approach was off by precisely 80% of camera height at every
single height tested (not approximately -- exactly, since it's a
direct consequence of the `0.8*height` term in `SnapTo`'s own offset
formula); the FIX recovers the true focus point to within about 4% of
camera height. That remaining ~4% residual is real but harmless and
NOT a flaw in the fix -- it's a separate, pre-existing, small
mismatch between `SnapTo`'s fixed 50-degree pitch and the angle its
own `(height, -0.8*height)` offset would need to point EXACTLY at the
focus (`atan(1/0.8) ≈ 51.3°`, not 50°) -- confirmed by working out
that arithmetic directly rather than assuming the residual was a bug
in the new ray-plane code and chasing it further. At the actual
match-start height (70), that's roughly 2.7m of residual error against
a 193m active radius -- 1.4%, negligible.

Flagged honestly: this is a real Editor-reported bug that no
flightcheck in this project caught in advance, because no flightcheck
in this environment can render an actual camera/scene to notice "100%
of visible traffic is frozen." A flightcheck can prove a formula's
math is correct once told what to check; it can't discover on its own
that a feature silently breaks something a real Editor session would
show at a glance. Worth remembering for the next camera-relative
feature this project adds: `Camera.main.transform.position` is NEVER
automatically "where the player is looking" for an offset/angled rig
like this one -- always resolve the actual look-at point first.

## 2026-07: FIX -- the ACTUAL root cause ("cars are just parked... no lights either")

The camera-focus fix directly above was a REAL bug, correctly diagnosed
and correctly fixed -- but it was NOT the cause of the reported "cars
not moving." The creator confirmed as much directly: "this was
happening before the camera addition," ruling that whole feature (both
its buggy and its fixed form) out as the cause of THIS symptom. Two
follow-up clarifying questions (`AskUserQuestion`) were explicitly
dismissed as "stupid" -- the creator wanted the actual bug found by
digging into the code, not more diagnostic small talk, and gave the
one clue that actually cracked it directly: "The cars are just parked
... a clue is that I don't see an cars with light on either."

**Root cause, reproduced (not guessed) via a real fault-injection
flightcheck.** `TrafficCar.Init()`'s call order was:

```
transform.position = RoadPoint(start, start);
_target = transform.position;
BuildBody(body, ...);     // <-- new Material(ShaderUtil.FindRenderableShader()) is its FIRST line
BuildLights();
... (personal speed roll, _parkDurationBase, THEN the _state/_hopsRemaining/PickNext() setup)
```

Real `UnityEngine.Material`'s constructor throws if handed a null
`Shader`. `ShaderUtil.FindRenderableShader()` tries three candidate
shader names and returns null if none resolve -- a real, if unconfirmed
-in-THIS-environment (no Unity Editor here to determine WHY it might
fail), code path. If it ever does, `BuildBody()` throws, and since
that's called BEFORE any of `_state`/`_hopsRemaining`/`PickNext()` are
set, `Init()` aborts right there. `_state` is then left at its C#
default (`Driving`, enum value 0 -- the first entry in `private enum
State { Driving, Parked }`) with `_target` still equal to the car's own
just-assigned spawn position and `_hopsRemaining` still 0. The very
FIRST `Update()` call reads `toTarget.magnitude < ArriveRadius` as true
(distance is exactly 0) and `_hopsRemaining <= 0` as true, and calls
`ParkHere()` immediately -- and because `BuildLights()` never ran
either (never reached), the bulb arrays stay null forever too. The car
ends up BOTH permanently parked (with no real trip ever having been
set up to redepart from) AND with no working lights -- the EXACT two
symptoms reported, together, from one single root cause.

This was diagnosed by reading `TrafficCar.Init()`'s actual call order
line by line (not assumed), forming the hypothesis that a lighting-
related exception could be blocking movement (directly per the
creator's own clue), and then PROVING it with a real fault-injection
test rather than just patching blind:

1. Grepped `GlowPointRegistry.Register`/`NeonRegistry.Register`'s real
   implementations directly -- both are trivial `List.Add` calls that
   cannot throw, ruling out the lighting REGISTRY code as a plausible
   fault site (an earlier, weaker hypothesis).
2. Identified `new Material(ShaderUtil.FindRenderableShader())` as the
   ONE real, plausible throw site in the whole chain (Unity's own
   documented `Material` constructor contract).
3. Extended the `driving-verify` flightcheck's own `Material`/`Shader`
   stubs to reproduce that EXACT real contract (throws on a null
   Shader) plus a `Shader.SimulateUnavailable` toggle to force the
   failure on demand, then ran `TrafficCar.Init()` through it for
   real. The unhandled exception's own stack trace named the exact
   line and method (`BuildBody`, not `BuildLights` -- an assumption
   from the first hardening pass that the trace itself corrected)
   confirming the hypothesis precisely rather than approximately.

**Fix:** wrapped both `BuildBody()` and `BuildLights()` in `Init()`
independently (each logs a warning and degrades gracefully rather than
aborting the rest of setup), made `SetBulbsActive` null-safe, and
wrapped `UpdateLights()` itself defensively too (it's called from the
very TOP of the Parked branch, before the park-timer/redeparture
check -- any future fault reachable from there would reproduce this
exact failure mode by a different path, regardless of whether THIS
particular shader-lookup theory is the literal, complete explanation).
The same defensive wrapping was added to `TramCar.Init()`'s equivalent
`BuildBody()` call for consistency -- though `TramCar` was confirmed
structurally SAFE already (its own critical state -- `_builder`/
`_path`/`_index` -- is assigned before `BuildBody()` runs, so a
`BuildBody()` fault there was never able to block its `Update()` logic
the way `TrafficCar`'s ordering could). This is deliberate,
demonstrated extra insurance on a working script, not a claim that
`TramCar` had the same bug.

**Verification:** the fault-injection flightcheck (`driving-verify`)
extension, run for real: with `Shader.SimulateUnavailable = true`
(forcing every shader lookup to fail, so `BuildBody()` genuinely throws
inside `Init()` exactly as it would in the hypothesized real-Editor
scenario), a fresh `TrafficCar` still ends up correctly `IsDriving`
with a real, non-degenerate `PickNext()` target (not stuck aimed at its
own spawn point), still visibly drives over 20 simulated seconds (79m
covered, not frozen), and -- forced into `Parked` state directly via
reflection to specifically re-exercise that exact branch -- still
redeparts on schedule despite the SAME fault firing on every single
frame it stays parked. All prior checks in that same harness (movement,
camera-proximity gating/bias, speed variance, passing) still pass
unchanged.

**Honest limits, stated plainly rather than overclaimed:** this
environment has no real Unity Editor, so there is no way to confirm
`ShaderUtil.FindRenderableShader()` was ACTUALLY the specific thing
failing in the creator's own session -- only that (a) it is a real,
live, documented-behavior throw site that (b) sits at EXACTLY the
right place in the EXACT right call order to produce EXACTLY the two
symptoms reported together, and (c) the fix makes an entire CLASS of
failure at that call order structurally impossible going forward,
regardless of the precise trigger. If cars still don't drive after
this fix, the Console window's actual error text (if the underlying
fault is something this fix's `Debug.LogWarning` calls would now
surface) is the one piece of ground truth this project has no way to
obtain except by asking directly -- and per the creator's own stated
preference this round, that should be a last resort after digging
through the code first, not a first move.

## 2026-07: the REAL likely root cause -- 57 scripts had no committed .meta file

The creator reported the `CityBuilt` GameObject (the one carrying
`RuntimeCityBuilder`) had disappeared -- "the empty gameobject with the
citybuilder is gone." `SampleScene.unity` itself, unchanged by any
commit this whole session, still listed it correctly. Digging into WHY
a correctly-committed scene reference could still fail in a real
Editor surfaced something much bigger than a scene-sync hiccup: **57 of
this repo's 59 C# scripts under `Assets/Scripts` had never had a
committed `.meta` file** -- including `RuntimeCityBuilder.cs` itself.
Only `CityGizmo.cs`/`HexGridGizmo.cs` (plus the two stock
`TutorialInfo` template scripts) had one; `.gitignore` doesn't exclude
`.meta` files, so this wasn't deliberate -- every script this project
has added was apparently written directly (no real Unity Editor
available in this environment to auto-generate the `.meta` the normal
way) without a matching `.meta` ever being authored or committed
alongside it.

**Why this breaks everything, not just one GameObject.** A Unity
`.meta` file's `guid` is the ONLY thing a scene/prefab uses to resolve
"which script does this component actually run" -- `SampleScene.unity`
stores `m_Script: {fileID: 11500000, guid:
59f1bcb7b35f24dca88b3126df764dd0, ...}` on the `CityBuilt` GameObject,
a guid that has been sitting in the committed scene this entire time
with NOTHING in git ever backing it. On the creator's own original
machine, at whatever point they first set this up in a real Editor,
Unity would have auto-generated a real `.meta` with that exact guid
LOCALLY -- but since it was never committed, anyone else (or the same
machine after a `Library/` reset, a fresh clone, or anything else that
makes Unity re-import the script fresh) would have Unity mint a BRAND
NEW, DIFFERENT random guid for `RuntimeCityBuilder.cs`, which can never
match the scene's own stored reference. The result: "Missing (Mono
Behaviour)" on that component -- and a missing script reference means
Unity calls NONE of its lifecycle methods. `Start()` (and therefore
`BeginMatch()`, `SpawnTraffic()`, city generation, everything) would
simply never run. This is a complete, sufficient explanation for
"cars are just parked" and "the citybuilder is gone" BOTH, independent
of the `BuildBody`/lights hardening fixed in the entry above -- if
`RuntimeCityBuilder` itself never started running at all, NOTHING in
the whole match would ever exist, regardless of how correct
`TrafficCar.cs`'s own logic is. This also explains the repo's own
earlier "Restore creator's CityBuilt test GameObject into
SampleScene.unity" commit (from a prior session) -- the same
underlying gap, recurring, because the actual missing piece (committed
`.meta` files) was never addressed, only the symptom (the scene entry)
patched over.

**Fix:** created `RuntimeCityBuilder.cs.meta` with the EXACT guid
(`59f1bcb7b35f24dca88b3126df764dd0`) the scene has always expected --
confirmed by grepping the scene file directly, not assumed -- and
fresh, collision-checked guids for the other 56 previously-meta-less
scripts (verified against every existing guid in the project, not just
generated blind). Also verified: zero OTHER assets or folders under
`Assets/` are missing a `.meta` (only the 57 `.cs` files were
affected), and zero duplicate guids exist anywhere in the project
after the fix.

**Honest framing:** this environment still has no real Unity Editor,
so there is no way to press Play here and confirm this resolves what
the creator is seeing -- but unlike the `BuildBody`/shader-lookup
theory (a real, demonstrated bug class, just not confirmed as THE
trigger in this specific case), this one is closer to certain: the
scene's own stored guid for `RuntimeCityBuilder` provably had no
committed backing anywhere in this repo's history, which is not a
"maybe" -- it is a structural gap that WILL break the script reference
on any environment that doesn't happen to still have the creator's own
original local `Library` cache. If the game still doesn't run after
this, the next thing worth checking is whether OTHER GameObjects in
the scene (not just `CityBuilt`) show "Missing Script" warnings in the
Inspector, and whether Unity needed to reimport on this pull (a
"reimporting" progress bar, or a changed `Library/` folder) -- both
would confirm or rule out this exact theory further.

## 2026-07: FIX -- one car drove, lights on, then stopped for good; no other car ever seen moving

Creator report, after the `.meta`-file fix above got the match actually
running: "one car drove for a bit, had lights but it stopped and did
not find any other cars moving on the map." Distinct symptom from the
earlier "cars are just parked, no lights either" bug -- this car WAS
driving, with working headlights, and then permanently stopped while
still (per the lights) in the `Driving` state, not `Parked`
(`UpdateLights` only lights the headlights on the `driving: true`
path; a genuinely `Parked` car's lights would be off). That distinction
is the thread that unravels it: something in the DRIVING path itself
can grind a car to a permanent, un-recovering halt.

**Root cause, found by re-reading `TrafficCar.Update()`'s passing logic
line by line.** The follow-distance check (`DistanceAhead`) treats
every other traffic car as a potential lane obstacle regardless of its
own state -- including a `Parked` one sitting at the curb. That's
deliberate (a curbed car is a real obstacle a driver has to get around,
same as the creator's own "slow down if there's a car in front"
direction), and `ParkHere()`'s curb offset (`CurbOffset` = 2.5m) sits
only 0.5m further from the road centerline than the driving lane
offset itself (`LaneOffset` = 2.0m) -- well inside `LaneHalfWidth`
(2.4m), so roughly half of all parked cars (whichever curb `sign` they
rolled) sit squarely "in lane" for same-direction traffic that later
drives the same stretch. The passing maneuver (`_passing`) exists to
handle exactly this: swerve onto the opposite side, boost speed, and
merge back once past. But its own "have I gotten past it" check,
`ownLaneClear`, queried `DistanceAhead` from `transform.position`
directly -- and DURING a pass, that position is already offset
sideways by up to `PassLaneOffset` (4m) toward the opposite lane. A
stationary blocker sitting near the ORIGINAL lane centerline reads as
"more than `LaneHalfWidth` away, therefore clear" within the first
frame or two of the swerve, purely from the LATERAL offset -- long
before the car has actually traveled far enough ALONG the road to be
past the blocker. That false-positive "clear" immediately ends the
pass (`_passing = false`), which re-centers the car's steering back
onto the original lane line, heading straight back at the still-
parked, still-not-moved blocker; `_blockedTimer` resets to 0 and the
whole cycle (wait past `PassBlockedTriggerTime`, commit, immediately
false-abort) repeats roughly once a second, forever, with no net
forward progress -- a car that LOOKS stopped (because it net-is), still
reads as `Driving` (lights stay on), and never reaches `ArriveRadius`
so `_hopsRemaining` never ticks down to a real `ParkHere()` either.
Because any car whose route crosses a same-side-curbed parked car hits
this the same way, and the fleet's `trafficMovingPercent` (0.55
default) means roughly half the cars are parked (i.e. candidate
blockers) at any moment, this plausibly explains why the creator saw
the WHOLE fleet read as stopped, not just the one car they happened to
be watching when it happened.

**Fix:** `ownLaneClear` now queries from `transform.position + right *
PassLaneOffset` -- i.e. the passing car's current along-track position
projected BACK onto the original lane (undoing its own swerve offset)
before checking clearance, instead of checking from wherever the
swerve has currently put it. This reports "still blocked" for as long
as the blocker is genuinely still ahead along the road, regardless of
how far sideways the car itself has swerved to get around it, and
correctly reports "clear" once the car's own along-track position has
actually carried it past the blocker (at which point the blocker's
`along` value from that projected point goes negative -- behind, not
ahead -- and `DistanceAhead`'s own `Consider` already skips anything
behind). `passSideClear`'s own check (from `transform.position - right
* PassLaneOffset`, the opposite-lane point) was already correct and is
unchanged.

**Honest limits:** no real Unity Editor in this environment to press
Play and watch a car actually clear a parked blocker after this fix --
same posture as every other `TrafficCar` entry in this log. The
reasoning above is a line-by-line trace of the actual geometry
(`LaneOffset` vs `CurbOffset` vs `LaneHalfWidth`, and what `transform.
position` is during a committed pass vs. what the check assumed it
was), not a guess -- but if the fleet still reads as fully stopped
after this, the next thing worth checking is whether it's this SAME
mechanism in a different guise (e.g. two driving cars mutually
blocking after a fleeing U-turn) rather than the parked-car case this
fix targets specifically.

## 2026-07: destination-based traffic routing, and a second passing-bug find

**Creator report:** raising `trafficCarCount` to 40 made traffic
visible at last, "but they are driving erratically all over the road.
They should drive or be picking destinations on the map to go to, be
more realistic." `TrafficCar.PickNext` had no concept of "going
somewhere" at all -- every hop was a fresh pseudo-random wander-hash
pick (rotated by `_hopCounter` to avoid ping-ponging a fixed 2-3 hex
loop), nudged only by monster-avoidance and a camera-proximity bias.
Locally sensible (never immediately reverses, dodges threats), it adds
up to a car with no destination, which reads exactly as "erratic."

**Fix:** ported `Citizen.cs`'s own destination pattern (`_destination`
+ a greedy walk that favors whichever neighbor gets closer) onto the
road network instead of the sidewalk set:

- `TrafficCar.PickDestination()` (called once per trip, from `Init()`'s
  driving branch and `BeginTrip()`) picks a real hex via
  `RandomRoadHexNear` -- a hash-ranked pick within `MaxTripHops` (14)
  hexes of the car's current position, biased toward
  `RuntimeCityBuilder.CameraGroundFocus` by `CameraBiasWeight` (250,
  reused unchanged from the earlier camera-gating work). A flee
  (`awayFrom.HasValue`) skips picking a destination entirely -- panic
  overrides the errand, unchanged from before.
- `PickNext`'s per-hop score is now PRIMARILY `-DistanceTo(_destination)
  * DestinationWeight` (mirroring `Citizen.StepTowardDestination`'s own
  greedy scoring), with the wander hash demoted to a tie-breaker and
  the monster-aware penalty kept as a safety override.
  `DestinationWeight` (90000) is deliberately sized to sit strictly
  between the wander hash's own 0-65535 spread (so the errand is never
  a coin flip) and the monster-penalty's own max of 112000 (so a
  threat genuinely close to a candidate hex can still steer the car
  away from it -- safety still beats the errand).
- The camera-bias term MOVED out of `PickNext` (where, once
  `DestinationWeight` dominates every hop, it had no measurable effect
  left at all -- caught by the existing route-bias flightcheck
  regressing to a coin flip) and into `PickDestination`/
  `RandomRoadHexNear` instead: it now shapes WHICH destination gets
  picked, once per trip, rather than fighting the greedy walk every
  single hop. Same net effect (trips trend toward the view over time),
  cleaner separation of "where am I going" from "how do I get there."
- Trip completion is now PRIMARILY "arrived at `_destination`" (checked
  alongside the existing hop-count check in `Update()`'s arrival
  branch), not just "used up N hops." `RandomHopBudget()` still exists
  but is now only a safety cap -- `hexDistance(_to, _destination) *
  SafetyHopMultiplier` (3x), generous slack since the greedy walk is
  constrained to the road GRAPH (real detours around blocks), not a
  straight hex line -- so an unreachable or looping destination still
  parks eventually instead of wandering forever.
- `PickExit` (the roundabout-exit choice) got the same destination-
  first scoring, so a car doesn't lose its errand's sense of direction
  just because its route happened to pass through a traffic circle.

**Second passing-bug find, same underlying shape as the last one:** the
destination-routing flightcheck's own passing-a-slow-car test (same
one the previous log entry fixed) reproduced the identical "stuck
passing forever, no net progress" symptom -- just via a different
route direction than before, which is exactly why it hadn't shown up
until this pass changed what gets picked first. Tracing it: the
PREVIOUS fix's `ownLaneClear` query point
(`transform.position + right * PassLaneOffset`) assumed the car has
ALREADY swerved the full `PassLaneOffset` by the time this check runs.
On the very first frame(s) after committing to a pass, the car is
still essentially ON the original lane -- adding a FULL assumed
`PassLaneOffset` to "undo the swerve" instead overshoots past the
centerline to the far side, reproducing the exact same false-clear/
immediate-abort failure the previous fix already diagnosed once, just
from a different trigger (an on-axis blocker directly ahead, rather
than a same-side-curbed parked car). **Fix:** track `_passStartPos`
(the car's actual position the instant it commits to a pass) and
project back onto the original lane using the car's own REAL lateral
displacement since then (`Dot(transform.position - _passStartPos,
right)`), not an assumed constant -- on frame one that displacement is
genuinely ~0 (no overshoot), and it grows to the real swerve amount as
the car actually moves.

**Verified (flightcheck, no real Editor here):** extended
`driving-verify` with a destination-routing test (a car has a real
destination immediately after `Init`, holds it for a whole trip rather
than re-picking every hop, and `_to` genuinely equals `_destination` at
some point with a park stay beginning there) and a destination-vs-
danger test (a monster planted directly on the destination-favored hex
still steers the pick elsewhere -- safety still overrides the errand).
The pre-existing camera-bias and passing-a-slow-car tests both caught
real regressions during this pass (an over-large `DestinationWeight`
swamping monster-avoidance entirely, and the second passing bug above)
before being fixed and re-verified passing. All of it lives in real
`TrafficCar.cs`/`RuntimeCityBuilder.cs`, run through the same real-
DLL-plus-stub harness every other `TrafficCar` entry in this log uses;
`Mathf.CeilToInt` was missing from the harness's own `Mathf` stub
(Unity's real one has it) and was added to match.

**Honest limits:** no real Unity Editor here to watch a car actually
drive a purposeful-looking route end to end -- same posture as every
other entry in this log. The reasoning and the flightcheck are as far
as verification goes in this environment.

## 2026-07: FIX -- cars parking diagonally across roads

**Creator report:** "Cars are parking diagonally across roads. Not
properly parallel, I think it is too close to corner issue."

**Root cause:** `RoadDresser.CardinalAnchor` nudges a road hex's raw
world position onto its street's straight centerline whenever that hex
is on a VERTICAL (N/S) street -- cancelling the pointy-top odd-r grid's
own per-row sawtooth (adjacent rows alternate the nudge sign, since row
parity always differs between them). `TrafficCar.RoadPoint` (the
target a car actually DRIVES to) already used this corrected anchor --
its own doc comment even names the exact failure mode this fixes for
driving ("driving to the RAW hex center instead is exactly why cars
zig-zagged down a straightened street"). `ParkHere`, though, still
computed its parking direction and spot from the RAW
`WorldOf(_to) - WorldOf(_from)` -- the same class of bug, just left
unfixed in the parking code path. A same-row (horizontal) hop is
already collinear in raw coordinates and never shows this, which is
exactly why it wasn't caught earlier; a VERTICAL hop always diverges,
and a corner -- where the incoming hex is unnudged but the corner hex
itself IS "vertical" (it has a N/S neighbor) -- is the most visible
single-hop case, matching the creator's own diagnosis, though the
underlying bug is really about any vertical hop, not corners
specifically.

**Fix:** extracted `CardinalAnchorOf(hex)` (the same
`CardinalNeighbors`+`CardinalAnchor` call `RoadPoint` already made) and
had `ParkHere` use it for BOTH the parking direction and the spot
position, instead of raw `WorldOf`. `RoadPoint` itself is unchanged in
behavior, just refactored to call the shared helper.

**Verified (flightcheck, no real Editor here):** the `driving-verify`
harness's own `RoadDresser` stub had previously simplified away the
real straightening nudge entirely ("not load-bearing for 'does the car
move'") -- which meant `CardinalAnchorOf(hex) == WorldOf(hex)` always
in that harness, silently hiding this exact bug from every existing
test. Replaced it with a verbatim port of the real
`Offset`/`CardinalNeighbors`/`CardinalAnchor` logic, and gave the
stub's previously-inert `Quaternion` a real implementation (AngleAxis/
Euler/a yaw-only `LookRotation` shortcut, justified since every real
call site here only ever rotates on the horizontal plane/nlerp-based
`Slerp`) so a test could inspect `Transform.forward` instead of just a
method's internal `dir` variable. New test: an explicit vertical-
street network where two adjacent hexes are guaranteed opposite nudge
signs, confirming (a) the raw and corrected directions genuinely
diverge in this scenario (so the check isn't trivially vacuous), (b)
`ParkHere`'s actual chosen facing matches the corrected direction, not
the raw one, and (c) the parked spot sits near the corrected anchor's
curb line. Making the stub honest this way also surfaced one test-only
brittleness (a "parked car redeparts within 2s" check that only
sampled its FINAL frame, which a very short destination-based trip
could legitimately complete and re-park within that same window) --
fixed to sample every frame instead of asserting on a coincidence.

**Honest limits:** no real Unity Editor here to actually watch a
parked car sit flush against the curb -- same posture as every other
`TrafficCar` entry in this log. The reasoning is a direct trace of
`CardinalAnchor`'s own documented nudge against what `ParkHere` used to
compute, not a guess, and the flightcheck now exercises a scenario
proven (via its own `RoadDresser` port) to genuinely diverge.

## 2026-07: faction picker, `FactionId.Mixed`, and a starting Factory

Creator direction, verbatim: "initially give the player one fully
functional factory on startup/new game. Player first must choose
faction from one of the races or the mixed. With all the rules bonuses
and handicaps associated with that faction." Follow-up, on whether
Mixed's per-unit-race rule application could give it an unfair
advantage, and whether Mixed contradicts docs/23 §13's "not a fourth
lobby button" ruling for the Chimera Track: "for each unit in mixed the
rules will apply to the race of the unit, evaluated if that will give
mixed undo advantanages... yes it will be an achievement after winning
the campaign."

**What existed before this entry (see the "worker-economy epic" entries
above, Phases 1-4):** no player had ANY starting building at all in the
actual playable game -- `MatchState.SpawnHqForPlayer` existed and was
tested but never called from Unity; `_simBridge`/`MatchState` itself
only ever got created behind the dev-only `simDrivenDemo` Inspector
toggle, with a hardcoded 2-player `{MadDoctor, HumanArmy}` list; no UI
let a player pick a faction at all.

**FactionId.Mixed.** Added as a real 4th enum value (`FactionDef.cs`).
This directly reverses docs/23 §1's own prior ruling ("Hybrids are the
endgame reward for playing the salvage game hard, not a fourth lobby
button") -- a deliberate, creator-directed change, not an oversight; the
doc itself now carries a `2026-07 update` blockquote saying so rather
than silently contradicting the code. The PRE-EXISTING Chimera Track
(salvaging parts of all three origins mid-match, docs/23 §13 amendment
F) is completely unchanged and still the only way a MONO-faction player
reaches hybrid grafted parts in-match -- Mixed-as-a-starting-faction is
an additional, separate path, not a replacement.

**"No undue advantage," made structural, not just claimed.** The
creator's own framing ("rules apply to the race of the unit") became
the actual implementation, not just flavor text: `SimUnit` gained a
nullable `RaceOverride` (`FactionId?`, defaults null -- every pre-
existing spawn call site unaffected). `SpawnRosterUnit`/`CanTrainUnit`
grew a Mixed-only exception letting a Mixed player field/train ANY
faction's roster kind, and the spawned/trained unit's `RaceOverride` is
set to that roster kind's OWN faction. `MatchState.EffectiveFaction(unit)`
(`RaceOverride ?? owner.Faction`) is now what every `FactionLumenTable`
lookup (damage, speed, regen) resolves against, not the player's raw
`Faction` -- so a Rifleman fielded under Mixed gets EXACTLY Human Army's
real Day/Night modifiers, a Drone gets EXACTLY Alien Hive's, etc.
`FactionLumenTable`'s own Mixed row (widened from a 3x4 to a 4x4 table)
stays `FactionLumenModifier.None` for every phase -- Mixed itself grants
no faction-wide bonus on top. Net effect, verified by
`MixedFactionTests.cs` (including a real Tick-level movement-distance
assertion, not just a data-table check): fielding one of each race under
Mixed nets exactly the SUM of what those units would get mono-faction,
never a stacked bonus. The reward is roster breadth (one Factory can
train any race's units), not raw power -- answering the "undo
advantanages" question directly rather than leaving it unverified.
`SimUnit.RaceOverride` is hashed (`WriteTo`) since it's real gameplay-
affecting state, same "everything gets hashed" discipline every other
fixed identity stat in that class follows; `Tools~/DetHarness` re-run
after the change, still bit-identical across two runs.

**Achievement-gated, not free from turn one.** The creator's own words
("an achievement after winning the campaign") map to a persistent,
account-level unlock flag rather than an in-match condition --
`MixedFactionUnlock` (Unity layer, `PlayerPrefs`-backed,
`IsUnlocked`/`MarkUnlocked`). Honest limit, stated in that file's own
header rather than papered over: **no campaign mode exists anywhere in
this codebase yet** (grepped the whole repo for "campaign" -- every hit
is a design-doc mention in docs/01/docs/12/docs/17, never a built
feature). Rather than fake a campaign system just to have something call
`MarkUnlocked`, the flag is wired for real and left permanently locked
by default until a real campaign-completion event exists to call it --
the correct, non-broken default, not a stub pretending to gate
something. `FactionPickerHud` draws Mixed greyed-out/unclickable while
locked.

**The starting Factory.** `MatchState.SpawnFactoryForPlayer` mirrors
`SpawnHqForPlayer` exactly (Complete immediately, free, setup-time API,
not a `Command`) -- closes the worker-economy epic's own bootstrapping
gap (Phase 3/4 entries above) for the ONE starting building per kind per
player, by skipping the Collector->Worker->Factory chain entirely for
it. Every Factory built AFTER the starting one still goes through the
normal Worker-gated `BuildGhostCursor.RequiresWorker` path, completely
unchanged. `BuildingTests.cs` covers it (Complete immediately, blocks
its hex, zero cost, same as the HQ's own existing test).

**Unifying match creation.** `RuntimeCityBuilder.BeginMatch` now creates
a real `MatchState`/`SimBridge` and calls `SpawnStartingBases()`
(HQ+Factory for both players) UNCONDITIONALLY, right after city
generation -- not gated behind `simDrivenDemo` anymore. The build-menu/
ghost-cursor/BaseDresser/resource-HUD wiring, which used to live inside
the `simDrivenDemo`-gated roster-ready callback ONLY because that was
the one place a real match was guaranteed to exist, moved into
`BeginMatch` for the same reason -- it was never actually specific to
the docs/27 sim-driven-movement demo, just co-located with it out of
convenience. `simDrivenDemo`'s own block is now exactly what its own
doc comment always said it was: opting the first spawned monster into
sim-driven MOVEMENT, nothing else. For every existing default scene
(no faction picker, no region picker, `simDrivenDemo` off or on), the
two-player faction list resolves to the exact same `{MadDoctor,
HumanArmy}` the old hardcoded call used -- byte-for-byte unchanged
default behavior, just reached through a real player-facing path now
instead of a dev-only one.

**Faction picker.** `FactionPickerHud.cs` copies `RegionPickerHud.cs`'s
exact opt-in wiring shape (`RuntimeCityBuilder.showFactionPicker`, off
by default; IMGUI; centered; sets one field then calls the same
`BeginMatch` entry point). Shown BEFORE the region picker when both are
on, per the creator's own word order ("player first must choose
faction"). Blurbs on each option are `FactionLumenTable`'s own real
docs/23 §7 numbers, not invented flavor text.

**AI opponent faction.** Never Mixed (Mixed is the human player's own
unlocked reward, not something an AI antagonist spontaneously gets,
matching Q13's existing "AI-only Army/Hive antagonists" recommendation)
-- simple default: Army unless the human picked Army, in which case
Hive.

**Honest limits:** no Unity Editor here to visually confirm the picker
screens render/click correctly, that BaseDresser actually shows a
visible Factory model, or that the two starting bases don't visually
overlap on every city preset -- same standing posture as every other
Unity-side entry in this log. Verified for real: all 263
`packages/match-core` tests green (including the new
`MixedFactionTests.cs` and the updated `FoundationTests.cs`/
`BuildingTests.cs`), `dotnet build` clean, `Tools~/DetHarness` bit-
identical across two runs post-hash-change, and every new/changed C#
file's braces/parens verified balanced across the whole file (no Unity
assemblies exist in this environment to run a real Roslyn compile
against `UnityEngine`, so this is the honest ceiling of static
verification available, same as every prior Unity-side "flightcheck" in
this log). Site selection for the starting HQ/Factory hexes (near
center for the human, offset toward a map edge for the AI) is a real,
flagged v0.1 placeholder, not the "themed landmark site" docs/23 §2
eventually describes.

## 2026-07: SC2-style building navigation bar (BuildingNavHud.cs)

Creator direction, verbatim: "Like starcraft 2 I need icons that quick
navigate to my building. When you click on the building icon it will
hilite icon quick scroll to the building. then on the hilited icon the
&lt;&gt; keys will jump to the next building of that type if there are
more than one. or use arrow icon below the building to do the same. All
building that have been built will show up in small / medium icons on
the bottom of the screen."

**Implementation, one-to-one with the request.** `BuildingNavHud.cs`
draws one icon per COMPLETE building this local player owns (`SimBridge.
BuildingCount`/`BuildingAt`, filtered by `PlayerIndex` and `BuildingState.
Complete` — "have been built," not mid-construction), bottom-center of
the screen, sorted by `(Kind, EntityId)` so same-kind icons sit
physically adjacent. Clicking an icon sets `HighlightedEntityId` and
glides the camera there via `SimpleCameraRig.FocusOn` — the SAME method
`WaypointCommander.JumpToNearestUnit` (the existing G-key jump-to-
nearest-unit) already uses, not a new camera-movement code path. While an
icon is highlighted, the `<`/`>` keys (mapped to the UNSHIFTED comma/
period keys — SC2-style paging shouldn't need a held Shift) OR two small
`<`/`>` buttons drawn directly below the highlighted icon page through
every OTHER building sharing that icon's own `BuildingKind`, wrapping in
both directions; both paths call the same `CycleSameKind` so keyboard and
mouse can never disagree. The arrow buttons simply don't draw when
there's nothing else of that kind to jump to (an arrow that does nothing
reads as broken, not as disabled).

**Why comma/period, not the literal arrow keys.** `SimpleCameraRig`
already binds the arrow keys to camera panning (WASD's own alternate
binding) — reusing them for building-nav paging would silently steal
camera control the instant a building is highlighted. Comma/period are
what a US keyboard's `<`/`>` legends actually sit on without a held
Shift, matches the creator's own "the &lt;&gt; keys" wording, and doesn't
collide with anything else already bound in this project.

**No new icon art.** This repo has no icon sprite/texture assets
anywhere — every existing IMGUI panel (`BuildMenuHud`, `RegionPickerHud`,
`FactionPickerHud`) already represents its own options as colored
swatches plus short text, never real icon graphics. `BuildingNavHud`
follows the same established idiom (a per-`BuildingKind` colored square
plus a 2-3 letter abbreviation — "HQ", "Fac", "Bld", etc.) rather than
inventing new asset infrastructure this feature didn't ask for.

**Extends a known, already-flagged debt, doesn't create a new kind of
it.** docs/23 §13 amendment H already warned "IMGUI already forced the
`Minimap.PointerOver` hack and won't survive build menus/toasts/dial."
This feature needed the exact same hack (a static `BuildingNavHud.
PointerOver`, wired into both `WaypointCommander` and `BuildGhostCursor`
alongside their existing `Minimap.PointerOver`/`BuildMenuHud.
PointerOverPanel` checks) so clicking a building icon doesn't ALSO fire a
world-space select/order/placement click underneath it. Recorded here
rather than silently added as one more copy of a pattern the panel review
already flagged as not scaling — the uGUI migration amendment H asked
for is still the real fix, still not done.

**Honest limits:** no Unity Editor here to confirm the icons actually
render at a legible size/position, that the highlighted icon's arrow
buttons land in a sensible spot relative to the row, or that
`GUI.skin.label.alignment` mutate-then-restore behaves as expected across
a real frame -- same standing posture as every other Unity-side entry in
this log. Verified for real: every touched C# file's braces/parens
balanced across the whole file (`BuildingNavHud.cs`, and the
`RuntimeCityBuilder.cs`/`WaypointCommander.cs`/`BuildGhostCursor.cs`
wiring edits) -- the honest ceiling of static verification available
with no `UnityEngine` assembly to compile against in this environment.

## 2026-07: idle-monster auto-eat + G-key grab/clone (GrabCursor.cs)

Two creator directions landed together this session.

**"If not attacking and humans are around monsters will chase and
consume them."** `MonsterAgent.AcquireTarget()` (the existing idle
auto-engage entry point, previously combat-only: retaliate against a
last attacker, else engage the nearest enemy in aggro range) now falls
back to `_builder.NearestCitizenTo(...)` + the EXISTING `OrderEat`/
`TickEat` order path once combat finds nothing to engage. Deliberately
NOT nested inside the existing `_fighter.Weapon == null || !CanAttack`
early-return -- `OrderEat`/`TickEat`'s own bodies never touch `_fighter`/
`Weapon` at all, so gating the citizen-fallback behind a weapon check
would have silently starved exactly the units least able to also fight
back (a real correctness bug caught before it shipped, not a stylistic
choice). `RuntimeCityBuilder.NearestCitizenTo` mirrors the existing
`NearestMonsterTo`/`NearestEnemyOf` nearest-of-type pattern exactly.

**"Press G, pointer becomes a claw, click a monster to pick it up (it
wiggles/squirms), drop it onto the Factory to clone it, spawning more
based on resources required."** Real key conflict surfaced and resolved
by the creator directly: G was already "jump camera to nearest unit" --
moved to **J** (`WaypointCommander.cs`), freeing G for `GrabCursor.cs`'s
new grab mode (a toggle, not a held modifier).

`MonsterAgent` gained a `_held` state (`BeginHeld`/`EndHeld`/`IsHeld`)
that suspends its OWN `Update()` entirely (no orders, no separation, no
terrain-follow) while grabbed -- `TickHeld(worldPos, dt)`, driven
externally by `GrabCursor` every frame, hovers it above the cursor's
ground point and layers a sine/cosine wobble + slow spin on top ("wiggle
and squirm," the creator's own words). `GrabCursor` itself: G toggles
Armed (real OS cursor swap via `Cursor.SetCursor` to a procedurally-drawn
32x32 claw glyph -- no cursor/icon asset files exist anywhere in this
repo, same reasoning `BuildingNavHud`'s colored-swatch icons already
established); a left-click on a monster while Armed picks it up
(Carrying); a second left-click drops it -- if the drop point lands
within `dropRangeHexes` of one of the LOCAL player's own Complete
Factory buildings, `CloneOnto` fires.

**Cloning is not routed through match-core.** The Mad Doctor has no
fixed `RosterUnitKind` roster at all (`FactionRoster.cs`'s own header:
bred creatures only) -- cloning an already-live genome doesn't fit that
model, and a full new match-core `CommandKind` for it (mirroring
`TrainUnit`'s cost-debit/queue machinery) is real, separate,
not-yet-attempted scope, flagged here rather than silently invented as a
parallel spend path match-core's own wallet never sees. Instead:
`RuntimeCityBuilder` factored a reusable `SpawnMonster(StoredGenomeDto,
HexCoord)` out of `HandleRosterReady`'s own original inline spawn loop
(both the match-start roster fetch and GrabCursor's clone now call the
SAME method, not a drifting second copy of it), and gained a new
`TrySpendBlood(int)` -- a GATED spend (false + unchanged if
unaffordable), the deliberate opposite of the existing
`SpendWalletForCast`'s "never blocks, floors at 0" contract, since
cloning a whole creature is a real purchase, not an unblockable sink.
Cost is a flat, invented v0.1 placeholder (`cloneCostBlood = 60`, CLAUDE.md's
standing policy for every unsourced number in this project) spent from
`WalletBlood` -- the SAME wallet eating citizens already fills, so
cloning literally spends the Blood harvested from citizens, a real
thematic fit rather than an arbitrary currency choice. `CloneOnto` keeps
spawning (each clone fanning out to the nearest unclaimed open hex near
the Factory) for as long as the wallet affords the next one, hard-capped
at `maxClonesPerDrop = 10` regardless of remaining Blood. The carried
monster itself is never consumed -- dropping it is what clones MORE, not
a sacrifice of the original; no instruction said otherwise, and
destroying the player's own creature on every drop would have been a
needlessly punishing reading of "drop it onto the factory to clone that
monster."

**A real bug caught before commit, not after:** the first draft of
`CloneOnto`'s loop spent Blood BEFORE checking whether an open hex even
existed to place the clone on, meaning a crowded Factory neighborhood
could silently burn the player's Blood for clones that then never
spawned. Reordered to find-the-spot-first, spend-only-if-a-spot-exists.

**Honest limits:** no Unity Editor here to confirm the claw cursor glyph
is actually legible at real cursor size, that the wiggle/squirm reads as
intended, or that a real OS `Cursor.SetCursor` call behaves as expected
in a live Player -- same standing posture as every other Unity-side
entry in this log. Verified for real: every touched/new C# file's
braces/parens balanced across the whole file (`MonsterAgent.cs`,
`RuntimeCityBuilder.cs`, `WaypointCommander.cs`, `GrabCursor.cs`) -- the
honest ceiling of static verification available with no `UnityEngine`
assembly to compile against in this environment.

### 2026-07 follow-up: mechanical claw, tucked feet, slower roll/pitch wiggle, roof landing + park-around-Factory

Five refinements to the grab/clone feature above, all creator-directed:

- **"The Claw should be a mechanical Claw for all races."** Redrew
  `ClawTexture()` from the original gothic-red creature-pincer into a
  brushed-steel, riveted arcade-claw-machine glyph (a cable-mount hub +
  three hinged prongs) -- race-neutral by construction, not skinned per
  faction. Also fixed a real bug the redraw surfaced: the cursor hotspot
  passed to `Cursor.SetCursor` was never converted from the Color32
  array's bottom-up row order to `SetCursor`'s own top-down pixel
  convention -- the claw would have grabbed several pixels off from
  where it visually pointed.
- **"Disengage the feet from the ground when grabbed."** `MonsterBody`
  gained `ForceTuckLegs` (defaults false, so every non-grabbed creature
  is byte-for-byte unchanged) -- ORed into the existing `Airborne` check
  that already tucks a flying creature's legs mid-air, reusing that exact
  mechanism rather than inventing a second one. `MonsterAgent.Update()`
  still calls `UpdateLocomotion(Vector3.zero, dt)` while held/roof-
  displaying (not a bare early-return) specifically so the tucked legs
  stay synced to the wiggling/spinning torso instead of frozen mid-
  stride.
- **"Wiggling should be in roll and pitch and a lot slower."** Dropped
  the yaw-spin component entirely and cut `WiggleSpeed` from 9 to 1.6
  rad/sec -- roll (Z) and pitch (X) only now, on two different sine
  rates/phases so they never lock into one repeating figure.
- **"When dropped into the factory, it should land on the roof and
  rotate slowly in the Y axis."** New `MonsterAgent.BeginRoofDisplay` /
  `TickRoofDisplay`: the ORIGINAL (never-consumed) creature settles atop
  the Factory's own roof height (`BaseDresser.RoofHeightFor`, a public
  wrapper around the SAME tier-height table `BaseDresser` already
  renders buildings with, not a second copy of those numbers) and spins
  slowly around Y, persisting until a real order is issued (hooked into
  `ClearTargets`'s existing "any fresh order cancels a pending X" law --
  same "stays put until manually disturbed" contract `Perch` already
  has for rooftop-resting flyers).
- **"When clones pop out they should emerge and park themselves around
  the factory."** Clones now spawn AT the Factory's own hex (visibly
  emerging from it) and get a settle-creep destination via a new
  `MonsterAgent.SetSettleTarget`, reusing the EXISTING `TickSettle`
  direct-line-creep machinery group-move arrival already uses --
  deliberately not a new movement system.

### 2026-07 second follow-up: faster struggle, real per-leg kicking, orientation reset on drop, glowing pickup disc

- **"The wiggling needs to be a bit faster, with arm and legs
  struggling."** `WiggleSpeed` bumped 1.6 -> 3.0 rad/s, plus a NEW faster
  small thrash layered on top of the slow roll/pitch (11 rad/s, ±6°) so
  the weapon/limb geometry rigidly mounted on the torso reads as
  flailing at a distinct rate from the body's own slower squirm -- the
  honest ceiling for "arms struggling" given this rig has no independent
  arm IK (`MonsterBody` only articulates legs; arms/weapons are static
  torso-mounted geometry). Legs get REAL independent struggle: a new
  `MonsterBody.StrugglePhase` (driven from `TickHeld`, index-phase-offset
  per leg) layers a kicking offset onto the existing flight-tuck fold
  whenever `ForceTuckLegs` is set -- actual per-leg animation, not
  reusing the whole-body wiggle.
- **"When the user drops the wiggling stops and the monster reset to
  normal orientation: body, arms and legs, before it is mounted on the
  roof of the factory."** `EndHeld`/`BeginRoofDisplay` now both reset
  `transform.rotation` to identity and `StrugglePhase` to 0 the instant
  the drop happens -- a clean, calm pose (legs still tucked on the roof,
  since there's genuinely no ground up there, but STATIC, not kicking)
  rather than whatever mid-squirm frame the drop happened to land on.
- **"Add a glowing disk under the monster with light that light up them
  model, make it luminous with a soft glow."** A flat emissive disc
  (`MonsterAgent.EnsureGrabGlow`), built once per agent lazily on first
  grab and toggled active/inactive on every later grab/drop rather than
  destroyed/recreated. Registered with the EXISTING `GlowPointRegistry`
  (docs/28's Tier-1-emissive/Tier-2-budgeted-real-Light model, the same
  one every streetlamp/window/car headlight already competes under) via
  an `isEligible: () => _held` predicate -- that registry is explicitly
  append-only with no unregister lifecycle by design (its own header:
  "rather than adding true register/unregister lifecycle... an
  ineligible point simply never competes"), the exact precedent
  TrafficCar headlights already established for "only lit while driving
  at night." A held creature does NOT get to skip the shared city-wide
  light budget other props already live under -- the disc's own emissive
  material is always visible for free; a real promoted `Light` only
  fires if this point wins a budget slot like anything else. Cool
  energy-cyan, race-neutral, matching the mechanical (not gothic-red)
  claw redesign from the prior pass. Parented as a CHILD of the agent
  (not the shared monsters host) specifically so it's auto-destroyed if
  the creature dies without ever having been grabbed again -- position/
  rotation are still set in WORLD space every frame regardless, so being
  a child of the wiggling root causes no visual drift.

**Honest limits, unchanged from the prior entries:** no Unity Editor
here to confirm any of this actually reads correctly at real render
time -- same standing posture as every Unity-side entry in this log.
Verified for real: `MonsterAgent.cs`/`MonsterBody.cs` braces/parens
balanced across the whole file, the honest ceiling of static
verification available with no `UnityEngine` assembly to compile
against in this environment.

### 2026-07 third follow-up: real shoulder-joint rotation for arms

Creator direction, verbatim: "for arm actions just Rotate the shoulder
joints up and down a naturalist way do not go over 30 degrees of motion.
NOT IN THE Y Axis." A direct, corrective follow-up to the prior entry's
own honest admission that "arms struggling" only reached the torso's own
whole-body thrash, since `MonsterBody` had no independent arm rig at
all -- this closes that real gap rather than leaving it as a permanent
ceiling.

`BuildWeapon` (every hand/weapon family: rifle_arm, plasma_lance,
laser_array, photon_blaster, spore_launcher, the organic claw/pincer/
tentacle default) now builds its geometry under one new `_shoulder`
pivot Transform instead of straight off `_torso` -- the pivot sits at
the exact same `mount` point the geometry was already offset from, so
every part's position became relative to the pivot (`mount + offset` ->
`offset`) with zero change to how anything looks at rest. Not an
anatomically real shoulder socket (this rig has no such socket data for
the hand/weapon the way legs have real hip sockets) -- an armature to
rotate the existing static assembly around, built at the one point that
already made sense to pivot from.

`MonsterBody.UpdateShoulderSwing` (called every frame from
`UpdateLocomotion`, after the leg loop) rotates `_shoulder.localRotation`
via `Quaternion.Euler(swingDeg, 0f, 0f)` -- X only, Y and Z always
exactly 0, so it is structurally impossible for this to introduce any
yaw ("NOT IN THE Y Axis," satisfied by construction, not by convention).
`swingDeg = Sin(StrugglePhase * 0.9) * 30`, so 30 degrees is the
amplitude in EITHER direction from rest, never a peak-to-peak span --
"do not go over 30 degrees of motion" read as a hard per-side cap, the
stricter of the two possible readings. Driven by the SAME
`StrugglePhase` the legs already kick to (not a second, independently-
phased clock) so the whole creature reads as one struggling gesture.
Rests at identity automatically whenever `ForceTuckLegs` is false (never
grabbed, or dropped) -- no separate reset needed since `StrugglePhase`
itself is already reset to 0 on drop (`MonsterAgent.EndHeld`/
`BeginRoofDisplay`, from the prior entry).

**Honest limits, unchanged:** no Unity Editor here to confirm the swing
actually reads as naturalistic (vs. too fast/slow/robotic) at real
render time. Verified for real: `MonsterBody.cs`/`MonsterAgent.cs`
braces/parens balanced across the whole file; every `BuildWeapon` case
was individually checked to confirm its `mount +` prefix was correctly
dropped (not just the ones this description happened to quote) so no
weapon silently drifted position when its parent changed from `_torso`
to `_shoulder`.

### 2026-07 correction: the glowing disc belongs on the Factory roof, not under a carried monster

Creator correction, verbatim: "I asked for a lit disk on the roof of the
factory that illuminated the monster being built." The prior entry's own
"Add a glowing disk under the monster... make it luminous with a soft
glow" was genuinely ambiguous between two phases of the grab/clone
feature (being carried vs. resting on the roof afterward), and the first
pass guessed wrong -- it attached the disc to the CARRIED state
(`BeginHeld`/`TickHeld`/`EndHeld`), tracking the cursor's ground point.

Moved wholesale to the roof-display state instead: `_grabGlow`/
`EnsureGrabGlow`/`TickGrabGlow` (carry-phase, cursor-tracking, deleted)
became `_roofGlow`/`EnsureRoofGlow` (roof-phase, fixed local child of
the agent's own root -- no more per-frame world-space tracking needed,
since the root only ROTATES in place on the roof rather than
translating, and a flat symmetric disc looks identical at any Y
rotation). Activated in `BeginRoofDisplay` alongside the existing
land-on-roof-and-spin behavior; deactivated in `ClearTargets`'s existing
"any fresh order ends the roof-display beat" clause, right alongside the
leg-untuck it already does there. Same `GlowPointRegistry`
append-only/`isEligible`-gated wiring as before, just the predicate
switched from `() => _held` to `() => _roofDisplay`.

Reads correctly now against the creator's own framing: "the monster
being built" is the ORIGINAL specimen resting in a lit dais atop the
Factory while its clones are produced -- a cloning-vat spotlight, not a
tractor-beam glow under whatever's currently being dragged around by the
claw.

### 2026-07 follow-up: carried monster auto-snaps above the Factory roof while dragging

Creator direction: "when I move the pointer with the grabbed monster it
should automatically position the monster above the roof of the
factory." A drag-and-drop snap preview, `GrabCursor.HoverTargetFor`:
every frame while carrying, once the cursor's ground point falls within
`dropRangeHexes` of the local player's own Factory -- the EXACT same
`FindOwnFactoryNear` check `Drop` itself uses to decide whether to
clone, not a second, potentially-drifting copy of that range logic --
the carried monster's hover target snaps to that Factory's own hex
center at roof height (`BaseDresser.RoofHeightFor`, the same helper
`BeginRoofDisplay` already uses) instead of following the raw cursor
position. Falls back to the raw ground point everywhere else on the
map, so nothing about normal carrying changes outside a Factory's own
drop range. Same "the preview can never disagree with the actual
outcome" principle `BuildGhostCursor`'s own placement preview already
established for building placement, applied here to the clone-drop
target instead.

**Honest limits:** no Unity Editor here to confirm the snap actually
reads as a clear, well-timed magnetic pull rather than a jarring pop
once the cursor crosses into range. Verified for real: `GrabCursor.cs`
braces/parens balanced across the whole file.

## 2026-07: AttackBuilding combat + fire VFX -- RTS SimBuildings could not actually be attacked until now

Creator direction (resumed after the grab/clone feature above): "Building
need decent amount of HPs and should show damage and some low-poly fire
when being attacked." The HP half of this was already done (task #95,
the worker-economy epic's Phase 1 HP bump). Investigating the rest
surfaced a real gap, not a tuning question: `MatchState.
ApplyBuildingDamage` existed and worked, but **nothing in the sim ever
called it** -- a player-built `SimBuilding` (HQ/Factory/storage/etc) had
no attack path at all, unlike units (`AttackUnit`) and anomalies
(`AttackAnomaly`), both of which already had one. Closing that gap was
the actual work here, mirrored deliberately close to `AttackAnomaly`'s
own existing shape rather than inventing a new pattern:

- **`CommandKind.AttackBuilding = 9`** (`Command.cs`): TargetEntity is
  the attacker, ArgA is the building's entity ID cast to int -- the same
  reinterpreted-slot contract every other two-entity command already
  uses. `SimUnit` gained `UnitOrderKind.AttackBuilding`,
  `AttackBuildingTargetId`, and `BeginAttackingBuilding(uint)`, cleared on
  death exactly like `AttackAnomalyTargetId` already is.
- **`MatchState.ApplyAttackBuilding`** validates existence/alive/Reach
  (silent no-op otherwise, same "bad input never queues" contract as
  every other command) and calls `BeginAttackingBuilding`.
  **`TickBuildingCombat`** (a new tick-loop, deliberately separate from
  `TickCombat`/`TickAnomalyCombat` for the same reason `AttackAnomaly`
  got its own loop instead of reusing `TickCombat`: a building has no
  facing/arc/Level/XP of its own) resolves cooldown-gated hits through
  the existing `CombatMath.ResolveDamage`, reading the target building's
  OWN per-kind `Armor` (`BuildingDef.Get(building.Kind).Armor`) --
  verified with a dedicated test that an HQ (armor 8) takes less net
  damage than a BloodStorage (armor 2) from an identical hit, not just
  that damage happens at all. No XP is granted for a building kill,
  matching `AttackAnomaly`'s own "nothing to credit" reasoning -- an
  anomaly/building isn't a leveled combatant either. Like every other
  attack command in this codebase, there is no owner/faction check
  (`ApplyAttackUnit` doesn't have one either) -- friendly fire on
  buildings is allowed by the same existing precedent, not a new
  decision made here.
- **Tests** (`AttackBuildingTests.cs`, new file, 5 cases, all passing):
  damage-to-destruction, out-of-reach no-op, already-destroyed no-op, the
  "target destroyed mid-channel, attacker just waits" idiom (same
  contract `TickCombat` already documents for a dead unit defender), and
  the armor-respected check above. Full `dotnet test
  Tests~/MatchCore.Tests.csproj` suite re-run clean afterward (no
  regressions from the new tick loop touching shared state).
- **`SimBridge.QueueAttackBuildingCommand`** wraps the new command for
  Unity, but nothing calls it yet -- no UI path exists to actually ISSUE
  an attack-building order from the RTS demo (that's `WaypointCommander`
  right-click-on-enemy-building scope, not attempted here since the
  creator's own words were about HP/damage-visibility/fire, not a new
  attack-order UI flow). Flagged as a real, separate gap rather than
  silently built anyway.
- **Fire VFX** (`DamageFx.cs`): a new `FirePlume` component, `AttachFire`
  entry point, mirroring the existing `SmokePlume`/`SmokePuff` "no
  ParticleSystem, primitive-kit + Update-driven" idiom exactly --
  spawns small EMISSIVE (`_EMISSION` keyword + warm orange/yellow
  `_EmissionColor`) puffs on a much faster, more agitated cadence
  (~0.12-0.21s) and shorter life (~0.5-0.74s) than smoke's own lazy
  0.7-1.0s drift, and lower on the building (25% height vs. smoke's 90%)
  since flame licks near where it's burning while the smoke it produces
  rises above it. `BaseDresser.cs` gained `_damagedHandled` (a
  `HashSet<uint>`, same one-shot-per-EntityId pattern `_destroyedHandled`
  already uses) so the Intact -> Damaged transition fires BOTH
  `AttachSmoke` (pre-existing helper, never actually wired to RTS
  SimBuildings before now -- only the LEGACY world-generated `Building`
  system in `RuntimeCityBuilder.cs` called it) and the new `AttachFire`
  exactly once, parented under the building's own root so both
  self-destruct automatically when the building collapses to rubble (no
  separate cleanup needed, same reasoning `DustBurst`/`RubblePileFx`
  already rely on for their own parenting).

**Honest limits:** no Unity Editor here to confirm the fire reads as
"agitated flame" rather than just a faster smoke clone at real render
time, or that the emissive glow is visible/legible under URP's default
lighting. Verified for real: `dotnet test Tests~/MatchCore.Tests.csproj`
passes in full (all suites, not just the new file); `DamageFx.cs`/
`BaseDresser.cs` braces/parens balanced across each whole file.

### 2026-07 follow-up: dropping a new monster on a Factory boots the current roof occupant to a parking spot

Creator direction, verbatim: "when a new monster is dropped on a factory,
the current monster is booted to the next parking spot closest to the
factory and the new monster replaces the old on on the factory roof.
Ready to be cloned." A real gap in the grab/clone feature above: nothing
tracked which monster was currently resting on a given Factory's roof, so
a second drop onto the same Factory would have landed the new arrival
directly on top of whatever was already there instead of making room for
it.

`MonsterAgent` gained `BootFromRoof(Vector3 parkWorldPos)`: ends the
roof-display state with the exact same reset `EndHeld` already gives a
dropped carry (identity rotation, legs re-engaged, struggle phase
zeroed, glow disc off) rather than leaving the evicted specimen spinning
in place, then hands it a walk-away destination via the existing
`SetSettleTarget` -- the SAME direct-line creep every freshly-spawned
clone already uses to park itself, so the booted monster reads as
stepping aside, not vanishing.

`GrabCursor` gained `_roofOccupant` (`Dictionary<uint, MonsterAgent>`,
keyed by the Factory's own `EntityId` -- one roof slot per Factory).
`Drop`, right before calling `BeginRoofDisplay` for the newly-dropped
monster: if a DIFFERENT agent already holds that Factory's slot, it gets
`FindOpenHexNear(factory.Hex, ...)` -- the SAME parking-spot search
`CloneOnto`'s own fan-out already uses -- and is booted there via
`BootFromRoof` before the new arrival takes the roof. Dropping the SAME
agent back onto its own Factory is a no-op boot (nothing to evict).
`_roofOccupant` is kept in sync at the only other place a roof
occupant's real state can change -- `TryPickUp`, via a new
`RemoveFromRoofOccupancy` helper -- so a monster grabbed back off one
Factory's roof and dropped somewhere else doesn't leave a stale
reference that would wrongly evict it a second time later (a roof-
displaying monster never leaves on its own; `MonsterAgent.Update()`
early-returns for as long as `_roofDisplay` is true, so idle target-
acquisition/orders never fire on it -- grab and boot are genuinely the
only two ways occupancy changes).

**Flightcheck harness catch-up, not just this feature's own check:** the
scratch compile harness in this session's scratchpad had drifted well
behind the actual codebase (missing `BaseDresser.cs`/`GrabCursor.cs`/
`Collector.cs`/`Worker.cs` from its compile list entirely, several
`UnityEngine` stub members it had never needed before -- `Cursor`/
`CursorMode`, `Space`, `Transform.Rotate`/`childCount`/`GetChild`,
`Camera.ViewportPointToRay`, `Renderer.enabled`, `Keyboard.jKey` -- and,
once those were added, a genuinely confusing false-negative: `SimBridge.
SpawnFactoryForPlayer`/`CanTrainUnit`/`CommandKind.TrainUnit`/
`AttackBuilding`/`BuildingDef.Occupants` all reported "does not exist"
even though a from-scratch isolated reference check proved the rebuilt
`MadDr.MatchCore.dll` genuinely has every one of them. Root cause: half a
dozen unrelated leftover one-off verification projects from EARLIER in
this session (`build-stub-compile/`, `car-lights-stub-compile/`,
`tram-trace-verify/`, etc.) were sitting as SUBDIRECTORIES inside the
`flightcheck/` folder itself, each with its own stale `MadDr.MatchCore.
dll` copy in its own `bin/`; `EnableDefaultCompileItems=false` only
disables the SDK's default **Compile** item glob, not its default
**None**-item glob, so those nested `.dll` files were still being swept
in as implicit `None` items and fed into `ResolveAssemblyReference`'s
`{CandidateAssemblyFiles}` search path -- which is consulted BEFORE
`{HintPathFromItem}` in the default search order, so a same-named stale
DLL several directories over silently outranked the correct, explicitly-
`HintPath`'d one every time, even with an absolute path. Fixed with
`<EnableDefaultItems>false</EnableDefaultItems>` (turns off every
default glob, not just Compile's), plus a new `MissingPeerStubs.cs` for
the still-missing `FactionPickerHud`/`RegionPickerHud`/`LumenHud`/
`BuildMenuHud`/`BuildGhostCursor`/`ResourceHud`/`BuildingNavHud`/
`TramDresser`/`TramCar` types RuntimeCityBuilder.cs/WaypointCommander.cs
reference but this harness had never compiled for real -- empty
`MonoBehaviour`s with matching `Init(...)` signatures, explicitly NOT
claiming to verify those other files' own actual behavior (docs/12's own
established "no production code changed by harness catch-up, compile-
check plumbing only" precedent). With all of that: **the whole Unity
gameplay layer (36 real files, MadDr.MatchCore/CityGen/CreatureMesh/
RosterClient referenced live) now compiles clean against the freshly
rebuilt match-core DLL** -- the first time this session's harness has
actually proven that for GrabCursor.cs/BaseDresser.cs/DamageFx.cs at all,
not just the two files this specific feature touched.

**Honest limits:** no Unity Editor here to confirm the booted monster's
walk-away reads as a deliberate "making room" beat rather than an
abrupt teleport-then-walk, or that two rapid drops onto the same Factory
don't visibly overlap for a frame before the boot's `BootFromRoof` call
takes effect (both happen in the same `Drop()` call, so this should be a
non-issue, but only a real Player run proves it). Verified for real: the
harness catch-up above, plus a targeted isolated-DLL check confirming
`CommandKind.AttackBuilding`/`TrainUnit` and `MatchState.
SpawnFactoryForPlayer` resolve correctly outside the harness's own
(now-fixed) reference-resolution bug.

### 2026-07: per-phase + global fog density knobs, and a real fix for "the middle of the night is unplayable"

Creator asked two things at once: "Give me both per phase and a
multiplier fog density," plus a diagnostic question -- "Is the fog
density the reason the night is so dark or is it just the lack of
ambient light? ... it needs to be significantly lighter which is what
actually happens due to light pollution."

**The diagnosis, stated plainly before touching code:** fog is a minor
contributor at most. URP's Exp2 fog blends toward `RenderSettings.
fogColor` by DISTANCE -- it only visibly darkens/hazes geometry far from
the camera, and even Night's fog color (`(0.18, 0.15, 0.28)`) isn't
pure black. The actual "how lit does the whole picture look" lever has
always been the ambient-light system: `nightAmbient` (a flat uniform
light) and `nightFillLift` (an HDR shadow floor lift), both already
built and documented in docs/28 for exactly this recurring complaint.
Bumping fog density would not have fixed "unplayably dark" -- it would
have made distant objects hazier without touching the actual brightness
of anything nearby.

**Fog (the literal ask):** `FogDensity` previously lived only as four
hardcoded literals inside `BuildGrades`'s per-phase `PhaseGrade` table,
with no live global scale the way `bloomScale`/`emissiveScale` already
give every other per-phase-authored value. Promoted to four public
`fogDensityDawn`/`Day`/`Dusk`/`Night` Inspector fields (`BuildGrades`
changed from `private static` to an instance method specifically so it
can read them -- bakes in at Init/city-build time, same "rebuild to see
it" caveat every other `BuildGrades`-authored number already has) plus a
new `fogDensityScale` (`[Range(0,3)]`, default 1) that multiplies the
already-blended curve LIVE every frame in `ApplyBlend`, same model as
`bloomScale`. Both mirrored onto `CityLightingProfile` (`FogDensityDawn`/
etc, `FogDensityScale`) and wired through `ApplyProfile`, consistent
with how every other dual component-field/profile-override pair in this
file already works.

**Night brightness (the actual fix, not fog):** two changes, matching
what the creator explicitly named as the target -- real light pollution,
not moonlight. (1) `nightAmbient`'s default raised 0.24 -> 0.45 (a
genuine "significantly lighter" jump, still under the 1.0 ceiling --
this field's own history is a long series of "still too dark" bumps:
0.02 -> 0.08 -> 0.24, each one previously reported insufficient too, so
this is not assumed to be the last word either). (2) A real color-
accuracy miss, not just a magnitude one: the OLD formula (`new
Color(flooredNightAmbient, flooredNightAmbient, flooredNightAmbient *
2f)`) put double weight on BLUE only -- a cool moonlight tint, not the
warm amber/orange skyglow real urban light pollution actually produces
(scattered sodium-vapor/LED streetlight color). New `nightAmbientTint`
field (default `(1.2, 0.95, 0.65)`, warm) is multiplied onto
`nightAmbient`'s brightness instead of the old hardcoded blue-boost
formula, so the COLOR is now a live tunable knob too, not just the
brightness -- matching this project's own "everything editable live"
convention for every other post-2026-07 lighting field. `nightFillLift`
(the crushed-black floor lift, the OTHER half of what real light
pollution actually does -- scattered light means true black shadows
basically don't exist in a lit city) bumped 0.35 -> 0.45 alongside it.
Both mirrored onto `CityLightingProfile` (`NightAmbientTint` new,
`NightAmbientBrightness`/`NightFillLift` defaults updated to match) and
the profile's own "where to tune" doc-comment quick-reference updated
with the new fog/ambient-tint entries.

**Honest limits:** no Unity Editor here to confirm 0.45 ambient + the
warm tint actually reads as "light pollution" rather than just "flatter/
duller night," or that the lamp "pools of light" contrast (the whole
reason `nightAmbient` was dropped near-zero several rounds back in this
same file's own history) survives a near-doubled ambient floor -- this
is exactly the kind of visual-feel tradeoff docs/28 §5 says has no
meaningful pass/fail math, stated honestly rather than invented.
Verified for real: `LumenCycleController.cs`/`CityLightingProfile.cs`
braces/parens balanced across each whole file; the flightcheck harness
(fixed for real last entry, not just papered over) compiles the whole
Unity gameplay layer clean against the rebuilt DLL with both files'
changes in place.

## 2026-07 follow-up: cinematic-night lighting pass, explicitly scoped to "only files directly related to lighting" -- corrects the prior entry's own color choice

Creator gave a full cinematic brief in the same session, one round after
the fog/night entry above: "1950's sci-fi cinema... darkness remains
dominant... shadows are deep but readable... ambient city light softly
reveals surfaces... use a cool night color palette... do not globally
brighten the scene... avoid flattening the image... explain each
lighting parameter changed and why," plus explicit scope-fencing: "use
only files directly related to lighting."

**A real correction, stated plainly, not smoothed over:** the prior
entry's `nightAmbientTint` (warm amber, "light pollution") was the wrong
DIRECTION for this fuller brief. A flat warm ambient washes the whole
frame one color -- exactly the "flattening" this brief warns against --
and reads as dusk, not night. Real day-for-night cinematography keeps
ambient/fill COOL specifically so the WARM practical lights
(streetlamp/window/neon materials + `LampBoost`/`NeonBoost`, both
already warm-toned and untouched all session) read as contrast against
it; that contrast, not raw brightness, is what makes a scene both
legible and still feel like night.

Five changes, all in `LumenCycleController.cs`/`CityLightingProfile.cs`
only (per the scope fence):

- **`nightAmbientTint`** reversed cool -> `(0.55, 0.72, 1)` -- a moodier
  teal-blue, not a plain revert to the pre-fog-entry default.
- **`nightAmbient`** pulled back 0.45 -> 0.35 ("do not globally
  brighten" -- it's a FLAT uniform light, brightens lamp pools and unlit
  gaps by the same amount, the opposite of selective readability).
- **`nightFillLift`** raised instead, 0.45 -> 0.6, now the PRIMARY
  night-readability knob (both fields' tooltips and the profile's
  "where to tune" quick-reference updated to say so) -- it's the one
  post-process grade that structurally can't wash out the lamp-pool
  contrast, since it only ever touches near-black pixels.
- **Night's baked `Contrast`** 18 -> 24 and **`PostExposure`** -0.35 ->
  -0.4, a deliberate counterweight to the higher fill lift (a shadow
  lift alone reduces apparent contrast by raising the black point) --
  "deep but readable" needs both halves, not just the readable one.
- **A new code comment** at `RenderSettings.ambientMode = Flat` in
  `Start()` documents, for whoever asks "review baked lighting /
  improve indirect bounce" next: nothing in this project is baked
  (already established by docs/28 row 24), so the Flat `ambientLight`
  value IS the entire indirect-light stand-in; real bounce light via
  URP Adaptive Probe Volumes is flagged as a genuine but substantial
  Editor-only future option, not attempted here.

**Explicitly declined, per the scope fence, not silently dropped:**
monster-specific rim/fill lighting ("threatening silhouettes... subtle
separation lighting... do not make monsters look artificially lit")
would need a `GlowPointRegistry` registration call from `MonsterAgent.cs`
or a material change in `MonsterBody.cs` -- neither is a file "directly
related to lighting," so this round didn't touch them. The ambient/
shadow-lift changes above DO generically help monster silhouettes read
(monsters are lit by the same `RenderSettings.ambientLight` + shadow
lift as everything else), but a dedicated accent/rim light is separate,
not-yet-attempted scope, flagged rather than assumed out of the brief.
Wet-surface reflections likewise need Reflection Probes or a URP
Screen-Space-Reflections Renderer Feature -- Editor-only setup, not a
code-only change, also flagged rather than attempted.

Full row-by-row account (row 31) added to docs/28's own bug-history
table, including an explicit note that this reverses row 30's color
choice one round later rather than pretending row 30 was right all
along.

**Honest limits:** no Unity Editor here to confirm the cool ambient +
raised fill lift + contrast counterweight actually reads as "1950s
sci-fi noir" rather than just "differently dark," or that monsters
genuinely read as legible silhouettes against it without the
dedicated accent lighting this round explicitly declined to add --
exactly the kind of visual-feel judgment docs/28 §5 says this
environment can't verify. Verified for real: both files' braces/parens
balanced; flightcheck harness compiles the whole Unity gameplay layer
clean with these changes in place.

## 2026-07 follow-up: the first real screenshot of this whole system -- and it was broken, not moody

Creator sent an actual Editor screenshot for the first time in this
entire lighting saga -- at Dusk, "13s to next" phase. The result: roads,
terrain, and buildings were essentially invisible, near-total black,
with only the HQ/Factory tint squares and a couple of unit blips
legible at all. Asked directly: "does this look visible, playable to
you?" Answer given plainly: no -- this reads as broken, not
atmospheric. Confirmed it was the latest push; creator's goal stated
without qualification: "visibility/playability."

**Diagnosis:** because `nightAmount` (the day/night blend weight) ramps
to its held value well before Dusk itself ends (rows 12/29 in docs/28),
a screenshot at "13s to next" is already running essentially full-NIGHT
lighting values -- so this is a direct, real consequence of the
previous entry's numbers, not a mid-transition artifact. The prime
suspect: that entry's `Contrast` 18->24 + `PostExposure` -0.35->-0.4
counterweight, added specifically to keep the raised `nightFillLift`
from "flattening" the image. A steeper contrast curve stacked with a
darker exposure baseline can crush a shadow lift's gains right back
toward black -- and this is the first claim in the whole thread with
actual visual proof behind it rather than reasoning/reflection alone.

**Fix, prioritizing the stated goal over strict mood-preservation:**

- Night's baked `Contrast`/`PostExposure`/`VignetteIntensity` reverted
  PAST their pre-previous-round baseline: `Contrast` 24 -> 10 (below
  the original 18), `PostExposure` -0.4 -> -0.15 (less dark than the
  original -0.35), `VignetteIntensity` 0.42 -> 0.3 (a strong vignette
  also darkens frame edges, same problem).
- `nightAmbient` raised past every prior round in this saga -- 0.35 ->
  0.55 -- no longer traded against mood, since the creator's own
  stated goal is now visibility first.
- `nightFillLift` raised further, 0.6 -> 0.75, still the primary
  readability tool (unchanged reasoning from the prior entry).
- `nightAmbientTint` KEPT cool (that direction is still the right
  cinematic call) but lightened `(0.55,0.72,1)` -> `(0.68,0.8,1)` --
  a deeply-saturated tint multiplies DOWN whatever `nightAmbient`'s
  magnitude is, which was fighting the very increase it's applied to.
- All five mirrored onto `CityLightingProfile`; its own "where to
  tune" quick-reference gained a new "reads as near-total black, not
  just moody -> check Night's Contrast/PostExposure first" entry
  pointing straight at this round's own root cause, so a future
  session doesn't have to re-diagnose it from scratch.

Full row (32) added to docs/28's bug-history table, explicitly marked
as "diagnosed and corrected" rather than "confirmed fixed" -- this is a
reasoned response to real evidence, not a verified result, since
there's still no way to render it here.

**Honest limits:** no Unity Editor here to confirm this correction
actually restores visibility rather than just changing WHICH way it's
broken, or that 0.55 ambient + 0.75 fill lift + the reverted contrast
lands somewhere between "unplayable black" and "washed-out daytime."
This is the first round in the whole saga with real photographic
evidence of a problem, but the FIX itself is still unverified the same
way every prior round was -- a follow-up screenshot is the only way to
actually close this out. Verified for real: `LumenCycleController.cs`/
`CityLightingProfile.cs` braces/parens balanced; flightcheck harness
compiles the whole Unity gameplay layer clean with these changes in
place.

## 2026-07: two real traffic bugs, both root-caused to the same gap -- no real turn ARC through a corner, just a straight-line jump

Creator report, scoped explicitly to driving/parking: "Cars turn the
wrong way to avoid each other and park across the road instead of
parallel to the road... maybe because of the hex nature of navigation
vs not using cardinal geometry." Investigated both symptoms in
`TrafficCar.cs`/`RoadDresser.cs` before touching anything.

**Bug 1 (confirmed, fixed): `RoadDresser`'s STATIC decorative parked
cars mis-orient at corners.** This is a DIFFERENT code path than the
row-108 fix (that one was the DYNAMIC `TrafficCar.ParkHere`, which was
already confirmed correct -- it derives its direction from the actual
hop a specific car just drove, not a hex-level classification). `DressHex`'s
`connectors.Count == 2` branch, though, was treated as "a true straight"
unconditionally -- but Count==2 ALSO matches a genuine 90-degree bend
(e.g. an E arm + an N arm, never opposite). `axis = connectors[0].dir`
then picks whichever of the two arms happened to be checked first (E,
W, N, S, that fixed order) -- for a bend that's an arbitrary choice of
ONE of the two roads meeting there, so the 5.2m parked-car chassis
(and any furniture using the same `axis`) gets oriented along only one
of them, reading as sitting across the other. The exact "is this hex a
bend" concept ALREADY existed elsewhere in this codebase
(`RuntimeCityBuilder.IsRoadCorner`, used for citizen crossings) but
RoadDresser's own car-spawn condition never used it. Fixed: a new
`isStraightThrough` check (same `Vector3.Dot(...) < -0.5f` "roughly
opposite" threshold `IsRoadCorner` already established) gates the
parked-car spawn -- corners now simply don't get a decorative parked
car, same as junctions/dead-ends already didn't, rather than getting a
misoriented one.

**Bug 2 (root cause of "turn the wrong way to avoid each other"):
no code path here ever draws a real curved arc through an
intersection.** A car's steering target jumps straight from one hop's
lane-offset point directly to the next hop's, and `MoveToward` drives
in a straight line toward it -- meaning a car literally cuts a
diagonal chord across a junction or bend instead of curving through it
on its own side of the road. That's harmless for the ordinary follow/
slow-down behavior (still just "is something ahead of me, in my
lane"), but the aggressive-passing system (2026-07, "some aggressive
passing to slow cars") assumes a simple two-lane straight with a
coherent "opposite lane" to fully swerve into -- there IS no such
concept at a turn, and a car committing that full-lane swerve while
also corner-cutting can swing directly into another car's own
corner-cut path. This is the most plausible mechanism for "turn the
wrong way to avoid each other": both symptoms are two different
surface effects of the SAME underlying gap the creator's own hypothesis
named (hex-hop navigation with no real cardinal-geometry arc through a
turn), not two unrelated bugs.

**Fix (a scoped mitigation, not the full turn-arc rewrite):** new
`TrafficCar.IsStraightRoad(hex)`, the identical "exactly two arms,
roughly opposite" test as Bug 1's fix, gates STARTING a new pass --
`if (!_passing && ... && IsStraightRoad(_to))`. A car approaching or
sitting at a junction/bend now just follows/slows through it (unchanged,
safe behavior) instead of ever committing a full-lane swerve there. An
already-in-progress pass isn't interrupted by this check (it only gates
the initial commit), and ordinary driving/lane-offset through corners
is completely untouched -- this doesn't fix the missing turn-arc
itself, only stops the ONE maneuver (a full committed swerve) that
turns "cutting a corner" into "swerving into oncoming/crossing
traffic."

**Scope note:** a real fix for the underlying gap (an actual curved
path through every junction/bend, so a car's `fwd` continuously points
along its own side of the road even mid-turn) would be a genuinely
bigger change -- flagged here as the deeper structural item this
session's fix does NOT attempt, in case the corner-cutting itself
(as opposed to the passing-through-it behavior) is still visible/
undesirable after this round.

**Honest limits:** no Unity Editor here to confirm parked cars now
read as sitting flush at every corner (rather than just "not
diagonally placed by the old bug"), or that the passing gate actually
eliminates the wrong-way-swerve symptom rather than just reducing how
often it can happen. Verified for real: both `RoadDresser.cs`/
`TrafficCar.cs` braces/parens balanced (modulo a comment-text grep
artifact, resolved by an actual compile); flightcheck harness compiles
the whole Unity gameplay layer clean with both files' changes in
place.

## 2026-07 follow-up: fog defaults, and "realistic driving" -- three more concrete gaps closed

Creator: set `fogDensityScale` default to 0.41 and `fogDensityNight` to
0.034 (done, mirrored onto `CityLightingProfile`); then, still on
driving/parking, explicitly: "Still parking diagonal, driving through
parked cars," with a scoped goal -- "naturally realistic cars being
driven by people with personalities, avoiding collisions, smooth turns
and slowing down and speeding up to pass... Avoiding monsters do NOT
count in these basic rules of the road model." Investigated why the
prior round's fixes weren't enough before writing any new code.

**"Driving through parked cars" -- a real, previously-undiscovered gap,
not a re-occurrence of a fixed bug.** `RoadDresser`'s static decorative
parked cars were pure visual `GameObject`s with a `KnockableProp` for
physics knockback, but `TrafficCar`'s own `DistanceAhead` obstacle
check (the thing that makes a car slow/stop/decide to pass) only ever
iterated the moving fleet, tanks, and citizens -- it had ZERO awareness
these decorative cars existed at all. A driving car had no reason to
avoid one; it would drive straight through. New `RuntimeCityBuilder.
_parkedObstacles` (a `List<Transform>`, populated once by `RoadDresser.
SpawnCar` via a new `RegisterParkedObstacle`, since this dressing never
moves after city-build) is now checked by `DistanceAhead` alongside
everything else -- the SAME check that already gates following AND the
"is the opposite lane clear" half of the passing maneuver, so this one
fix covers both slowing for a parked car ahead and not swerving into
one while overtaking.

**A second, distinct "still parking diagonal" cause: `TrafficCar.
ParkHere`'s curb offset never scaled with road width.** The prior round
fixed the ANGLE (using the cardinal-corrected anchor instead of the raw
hex center). This round found the DISTANCE was also wrong on arterial
roads: `ParkHere` used a flat `CurbOffset = 2.5f` constant -- exactly
`RoadDresser.RoadWidth / 3f` for a RESIDENTIAL street (RoadDresser's
own static-car formula), but RoadDresser's formula scales with
`hexRoadWidth / 3f` (residential 7.5m -> arterial 14m) while
`ParkHere`'s never did. On a 14m arterial, parking 2.5m from centerline
sits well inside the arterial's own lane markings -- a dynamically-
"parked" car was really parking mid-lane, which reads as sitting wrong
AND is a direct collision hazard for anyone driving that lane. New
`RuntimeCityBuilder.IsArterial` (lazily-cached `HashSet<HexCoord>`,
same pattern as the existing `IsWaterHex`/`IsRoundabout`) lets
`ParkHere` read the SAME per-hex road width RoadDresser used to dress
that exact hex, and apply the identical `/3f` formula -- both parking
systems now agree on the physical curb line. `RoadDresser.RoadWidth`/
`ArterialRoadWidth` promoted from `private` to `public const` so
`TrafficCar` can reuse the exact numbers instead of duplicating them
(avoiding future drift between the two).

**"Smooth turns" -- the deeper structural gap flagged last round,
now actually addressed (scoped, not the full rewrite).** A car's
steering point used to jump straight from one hop's lane-offset target
to the next's the instant it arrived, cutting a hard-angled chord
across every junction/bend -- this was already identified as the root
cause behind the "wrong way to avoid each other" fix, and is exactly
what "smooth turns" is asking to fix at the source rather than just
mitigate. New `TrafficCar._hopEnterDir`/`_hopStartPos`, set in
`PickNext` right as a new hop begins (capturing the ENDING hop's own
direction before `_from`/`_to` get reassigned): for the first
`TurnBlendDistance` (6m) of a new hop, the steering point's direction
eases from the old hop's heading into the new one via `Vector3.Slerp`,
instead of snapping straight onto the far-off `_target`. A genuine
straight continuation (dot near 1, no real direction change) skips the
blend entirely -- no extra cost on ordinary hops, only at real
junctions/bends. Deliberately scoped to `PickNext`'s normal hop
transition only -- roundabout exits already have dedicated smooth
circulation via `CirculateRoundabout` and weren't touched.

**Scope respected:** nothing here touches monster-avoidance
(`SwerveOffset`, flee/panic routing) at all -- every change is in the
car-vs-car/car-vs-parked-obstacle "rules of the road" path the creator
explicitly carved out, per their own "avoiding monsters do NOT count in
these basic rules" instruction.

**A real stub gap found and fixed along the way, not worked around:**
`Vector3.Slerp` doesn't exist in the flightcheck harness's `UnityStub.cs`
(a genuine UnityEngine API this codebase had just never needed before) --
rather than downgrade the turn-blend to `Lerp` to dodge the compile
error, added a real `Vector3.Slerp`/`Mathf.Acos` to the stub (matching
real Unity's own direction-and-magnitude spherical interpolation
contract) and, while there, fixed `Vector3.Lerp` itself, which had been
a `default(Vector3)` no-op stub the whole session -- the same "silently
vacuous check" risk this harness's own comments already warn about
elsewhere, just never surfaced until this was the first thing to
actually call it.

**Honest limits:** no Unity Editor here to confirm the turn blend reads
as a genuine smooth curve (vs. still visibly kinked, or over/under-
shooting through a tight corner), that parked cars are now reliably
avoided rather than just less-often-clipped, or that arterial parking
now visibly sits at the true curb. Verified for real: `RuntimeCityBuilder.cs`/
`RoadDresser.cs`/`TrafficCar.cs`/`LumenCycleController.cs`/
`CityLightingProfile.cs` braces balanced; flightcheck harness (including
the newly-fixed `Vector3.Lerp`/`Slerp`/`Mathf.Acos` stubs) compiles the
whole Unity gameplay layer clean with every change in place.

## 2026-07: starting bases off roads, a Big Brain glass-jar silhouette, and autonomous harvester hauling

Creator, four items in one message: "The factory and central base is in
the middle of a road" (bug); "The big brain base... should have a big
brain in a glass jar on it. And used for tech upgrades"; "Any monster
units with backpacks should act as harvesters and collect resources then
navigate back to the factory dumping their load there and go back to
harvesting"; and, flagged as background/future rather than an immediate
ask, "human workers controlled big brain that build structures... to add
tech wings on the base for upgrades of units." Researched all four
before writing any code (a dedicated Explore pass across match-core and
Unity) to separate "quick bug fix" from "large undesigned feature."

**1. Starting-base-on-road: a genuine, previously-unflagged gap, not a
placement-search bug.** Roads are deliberately NOT in `BattlefieldState.
BlockedToGround()` (units must be able to walk/drive on them), but
nothing else ever excluded them from BUILDING placement either --
`RuntimeCityBuilder.FindOpenHexWide`/`SpawnStartingBases` and match-core's
own `MatchState.CanPlaceBuilding` both only ever checked `_city.Contains`
+ the same road-permissive blocked set. `BuildingTests.cs`'s own class
doc comment even already named "roads/water/occupied rejected" as the
Phase 2 acceptance bar -- this was a real gap between the documented
contract and what the code actually enforced, not new behavior invented
here. Fixed on both sides: `SpawnStartingBases` now unions
`RoadNetworkHexes()` into its own local `blocked` copy (doesn't touch
`BlockedToGround`'s own broader, deliberately road-permissive "can a
unit walk here" contract); match-core's `CanPlaceBuilding` gained a new
lazily-built `_roadHexes` `HashSet<HexCoord>` (mirroring `_blockedToGround`'s
own construction) so PLAYER-issued builds later are held to the same
rule, not just the two starting buildings. Six match-core test files'
own `FindOpenHex` helpers (`BuildingTests`/`AttackBuildingTests`/
`EconomyTests`/`TrainUnitTests`/`MixedFactionTests`) needed the identical
road-exclusion added -- 19 tests were silently relying on the old
gap (picking a hex that happened to be a road, which used to
"work" only because nothing checked) and failed once the real bug was
fixed; all 268 pass again after updating the shared helper pattern.

**2. Big Brain: visual dressing only, "tech upgrades" flagged as a real,
NOT-yet-designed system.** The building already exists fully at the
data layer (`BuildingKind.BigBrain`, 20 Brains cost -- the creator's own
exact number, called out in `BuildingDef.cs` as deliberately non-
placeholder -- Large tier, raises Supply cap), but `BaseDresser.
BuildCompleteShape` had no case for it, falling through to the generic
box every other unhandled kind gets. New `BuildBigBrainShape`: an owner-
tinted pedestal (keeping the roster's own "shape=kind, color=owner"
language) topped by a glass jar -- transparent shell, a faintly glowing
green fluid, a cluster of overlapping emissive-pink sphere "lobes"
reading as an organic brain mass rather than one ball, a plain chrome
lid. The jar assembly is parented under its own holder transform with no
`Renderer` of its own, specifically so `TintShape`'s single-level
`GetChild` sweep (owner-tint overwrite) never reaches these grandchildren
and flattens the glass/brain materials to the plain owner color the way
it would if they sat directly under `root`. Confirmed via a fresh grep
before writing anything: no tech-tree/upgrade mechanic exists ANYWHERE
in match-core today -- "used for tech upgrades" is real, unimplemented
design intent, not a request this pass silently built or silently
dropped; it's the visual half only.

**3. Harvester backpacks now autonomously haul to the Factory --
deliberately REVERSES an earlier design decision, not an extension of
it.** docs/22's original design was explicit: "auto-first, but the
HAULING is the player's decision -- no unprompted walk-off" (a laden
harvester only banked its load if the PLAYER happened to walk it near
its home spawn point). The creator's new direction -- "collect
resources then navigate back to the factory dumping their load there
and go back to harvesting" -- is a real behavior change, not a
misunderstanding of the old design, so it's implemented as one: once a
harvester's tank reaches capacity (eating more once full gains nothing,
since `CreditHarvestForEatenCitizen` already clamps there), `AcquireTarget`
now autonomously issues a real `OrderMove` to the player's own nearest
Complete Factory, ahead of the idle-eat fallback but still behind real
combat retaliation/engagement (self-defense still wins). New
`MonsterAgent.FindOwnFactory` reads a NEW `RuntimeCityBuilder.SimBridge`
accessor -- this monster's OWN per-unit `_simBridge` field is only ever
set for a docs/27 sim-driven unit (at most one today), but the MATCH's
own bridge always exists once a match has started (task #115), so this
works for the whole roster, not just that one demo unit. Player index 0
is hardcoded as "the local human player," the same convention every
other Unity-side script (`GrabCursor.localPlayerIndex`, etc.) already
uses, since MonsterAgent has no general per-unit ownership field to read
instead. The existing auto-bank-on-idle-near-unload-point check now
targets the Factory too (falling back to the old `_homeHex`/spawn-point
"Vat stand-in" only if no Complete Factory exists yet), so both halves
of the loop -- the autonomous walk AND the eventual bank -- agree on the
same destination.

**4. SCV-style Worker construction of "tech wings" -- explicitly NOT
attempted this round, flagged rather than guessed at.** Confirmed:
`Worker.cs` performs zero construction of its own (its own doc comment
says so explicitly -- v0.1 scope is "the unit itself, not yet the job");
the existing worker-GATE (`BuildGhostCursor.RequiresWorker`) only covers
`Factory`, is Unity-side/cosmetic only (match-core's own
`ApplyBuildStructure` has no concept of a Worker at all); and there is no
concept anywhere of a building "add-on"/"wing" that upgrades an EXISTING
building -- `CommandKind.BuildStructure` can only ever target a fresh,
unoccupied hex, never an existing building's entity ID. Genuinely
inventing this system's mechanics (what tech wings exist, what they
unlock, a new command shape for "attach to an existing building" instead
of "place at a hex") without real design input risks building the wrong
thing and needing a redo -- surfaced as a real, scoped question rather
than silently built or silently dropped.

**Honest limits:** no Unity Editor here to confirm starting bases now
visibly clear every road, that the glass jar reads as intended at real
render scale, or that the autonomous haul-to-Factory walk looks natural
rather than abrupt. Verified for real: match-core's full 268-test suite
passes (the genuine behavior change from item 1, not just a compile
check); `RuntimeCityBuilder.cs`/`BaseDresser.cs`/`MonsterAgent.cs`/
`MatchState.cs` braces balanced; flightcheck harness compiles the whole
Unity gameplay layer clean against the freshly rebuilt match-core DLL.

## 2026-07: low-poly procedural brain mesh + PBR texture kit, replacing the pink-sphere-cluster placeholder -- and a from-scratch numeric verification harness that caught three real bugs before they ever reached the Editor

Creator brief, verbatim in full: a stylized-but-believable low-poly
(500-2,000 tri) human brain, geometry limited to the major anatomical
landmarks (two hemispheres, central fissure, cerebellum, brainstem),
all fine surface detail carried by a PBR material set -- normal map
(broad/medium/fine folds), height/parallax map, AO darkening the
grooves, mottled pale-gray/pink/cream/blue albedo with overlaid
branching vessels, a roughness map that reads peaks smoother and
valleys rougher, and "subtle subsurface scattering or translucency."

**New files.** `BrainMesh.cs`: a hand-authored UV-sphere builder (not
added to `ProceduralMeshKit` -- that file's own `FaceOutward` winding
helper only works against a single shared centroid, which doesn't hold
for a multi-lobe mesh with three lobes offset well away from one
another). `BuildBrainMass` merges two flattened, offset hemisphere
lobes (visible central-fissure groove between them) plus a smaller
cerebellum lobe into one 646-triangle mesh; `BuildBrainstem` is a
separate tapered-cylinder primitive (plain flesh tone, no detail
texture needed). `BrainTextureKit.cs`: five 256x256 procedural maps
(albedo, normal, occlusion, metallic/gloss-in-alpha, height) all
derived from ONE shared multi-octave value-noise heightfield (broad +
medium + fine frequency bands, amplitudes summing to 1) so the bump
shading, AO shadowing, smoothness split, and albedo shading all agree
with each other instead of drifting as independently-authored maps
could. Branching blood vessels use the same "difference of two shifted
noise fields, threshold near zero" technique this project already uses
elsewhere for crack/vein networks. `BaseDresser.BuildBigBrainShape` now
spawns the mesh (via `PropLibrary`, inheriting its established
defensive `_Cull = Off` fix for unverifiable custom-mesh winding) with
a new material wiring all five maps into URP/Lit's standard property
names (`_BumpMap`/`_OcclusionMap`/`_MetallicGlossMap`/`_ParallaxMap`
plus matching keywords), replacing the old 5-sphere lobe cluster.

**Honest limit, stated rather than silently attempted: no real
subsurface scattering.** URP's standard Lit shader has no SSS slot
(that's HDRP-only); authoring a custom Shader Graph approximation blind,
with no Editor here to compile or render it against, was judged too
likely to ship something silently broken. Approximated instead with
Lit's own existing levers -- a translucent-reading base palette,
moderate smoothness, and a faint warm low-level emission -- the same
"closest achievable approximation, plainly labeled as one" call already
made for lighting elsewhere in this project.

**The real story of this pass: a from-scratch numeric verification
harness, separate from the usual flightcheck compile check, caught
three concrete bugs that would otherwise have shipped silently broken
-- exactly the docs/28 failure class ("winding disagreement causes
back-face culling to vanish props," "double-winding cancels normals to
zero") this project has been bitten by before.** A standalone
`brainverify` console project (real `BrainMesh.cs`/`BrainTextureKit.cs`
against the shared `UnityStub.cs`) independently recomputed each lobe's
own centroid and checked every triangle's face normal points outward
from it, checked for zero-area triangles, round-tripped the normal
map's RGB encoding back to unit-length vectors, and checked every
noise-driven map actually varies instead of reading flat. First run:
**646/700 hemisphere/cerebellum triangles wound inward** (the per-quad
UV-sphere triangulation had its winding backwards from the start;
hand-verified via cross-product math and fixed by swapping the last two
indices of each triangle), **24/48 brainstem triangles also wound
inward** (a second, independent triangulation bug in the tapered-
cylinder's side quads -- the cap triangles were already correct, only
the two side triangles per segment were backwards), and **the AO map
failed its own "not flat" check** (red channel only spanning 244-255 --
the original 3-pixel blur radius was well inside a single fine-noise
grid cell, so the "am I in a valley relative to my neighborhood" sample
barely differed from the pixel itself; fixed by widening the blur to an
8-point compass ring at a radius closer to the fine octave's own
wavelength and raising the darkening gain). Re-running the harness
after all three fixes: every check passes (the sphere's polar rows also
left 6 duplicate wrap-seam vertices unreferenced by any triangle --
harmless, since an unreferenced vertex is never rasterized, but the
harness was extended to tell that apart from a REFERENCED vertex with a
cancelled-to-zero normal, which would be the real bug).

**Honest limits:** no Unity Editor here to confirm the brain reads as
intended at real render scale/lighting, that the four newly-used URP/Lit
property and keyword names (`_BumpMap`/`_OcclusionMap`/
`_MetallicGlossMap`/`_ParallaxMap` and their `_NORMALMAP`/
`_OCCLUSIONMAP`/`_METALLICSPECGLOSSMAP`/`_PARALLAXMAP` keywords) are
exactly right against a real Editor (only `_BaseMap`/`_Smoothness` were
previously proven working in this codebase), or that the parallax depth
reads as intended at gameplay camera distance. Verified for real,
beyond compile: the new `brainverify` numeric harness (winding,
degeneracy, normal-map round-trip, map variation) passes clean after
three genuine bug fixes; the main flightcheck harness still compiles
the whole Unity gameplay layer against the real match-core DLL with
these two new files included.

## 2026-07 follow-up: the mastermind-tier brain in the Lab gets the same upgrade -- as the existing genome tier, not a new schema field

Creator: "That brain needs to be in the lab as well." Site/ (`site/` +
`packages/genome-core`, "the Lab" per CLAUDE.md/docs/23's own glossary)
already renders a brain today -- it's not a new concept to add, it's an
existing one to fix. `BRAIN_TIERS` (`site/lib/genome.js`) already has a
`mastermind` tier whose head visual (`buildHead`, `site/creature-
renderer.js`) is "exposed pulsing brain with two lobes" sealed under
`buildGlassDome`'s riveted glass jar -- the same fiction as the Big
Brain base building, just on a creature's own head instead of a
building. Asked the creator to confirm scope before touching anything
(three read options: a standalone dev-preview panel outside the genome
system, a real selectable Brain-tier genome part touching the
normative schema, or a plain non-interactive gallery entry); the answer
was **the existing `mastermind` tier's OWN visual, upgraded** -- not a
new schema field, not a new tier, not a dev-only preview panel.

**What changed, `buildHead`'s mastermind branch only:** the old code
built one big blob ellipsoid plus two brighter "highlight" ellipsoids
layered on top under `TILE.slick` (a smooth-skin texture -- wrong
material for brain tissue) with no fissure and no cerebellum. Replaced
with real twin-hemisphere geometry (two separate lobe ellipsoids, not a
mass-plus-highlight illusion), `TILE.veins` (the same "veined
membrane -- the 1950s brain-alien hide" texture `alienDetails` already
uses for the Alien faction's own exposed brain, correctly a brain
texture this time), a central-fissure sulcus (the exact tube-along-the-
midline technique `alienDetails` already uses, previously missing from
the mastermind tier entirely), and a new small cerebellum lobe tucked
low at the back -- bringing this closer to the Big Brain base's own
`BrainMesh.cs` topology (two hemispheres + cerebellum) so the same
creature-fiction "brain" reads consistently whether it's on a
creature's head or in a base's jar, without literally porting the
Unity mesh/texture-kit code into WebGL (this renderer builds geometry
from its own primitive kit -- `ellipsoid`/`tube`/`torus` -- same as
every other body part here, not from a hand-rolled triangle mesh, so a
1:1 code port would be foreign to the file's own conventions).

**Deliberately NOT a schema change:** `BRAIN_TIERS`, `BRAIN_AXES`, and
`BRAIN_SIZE` (`site/lib/genome.js`, mirrored in `packages/genome-core`)
are untouched -- this is a pure rendering swap keyed off the tier value
that already exists, so `packages/genome-core/tests/golden.txt` (which
pins RNG draws, including `rng.choice(BRAIN_TIERS)`) needed no update,
and docs 06/07/08's normative-schema rule doesn't apply (nothing about
the genome schema itself changed). `site/creature-renderer.js` lives
directly in `site/`, not `site/lib/` (the vendored genome-core
compile), so no vendored-copy sync step was needed either.

**Honest limits:** no automated visual regression test exists for this
renderer (none existed before this change either). Verified for real:
`node --check` on the edited file; a standalone Playwright smoke test
(Chromium, this environment's pre-installed browser) served `site/`
over a real local HTTP server (module scripts need one, `file://`
can't load them), constructed a `mastermind`-tier genome directly via
`randomGenome(rng, {tier:'mastermind'})`, called the real exported
`initRenderer`, and screenshotted the live WebGL canvas -- confirmed no
console/page errors, and a before/after screenshot comparison against
the pre-change code (same seed) shows the intended change: a flat
single pink mass under `TILE.slick` before, a mottled twin-lobe veined
brain after.

## 2026-07 follow-up: cerebellum lobe cut from both brains -- tucked below the hemispheres, it wasn't contributing anything

Creator, on review: "the cerebellum is below the hemispheres. so we can
probably exclude them." Both the Big Brain base's `BrainMesh.cs`
(Unity) and the Lab's `buildHead` mastermind branch
(`site/creature-renderer.js`) positioned the cerebellum lobe below and
behind the hemisphere mass, the real anatomical arrangement -- but
posed vertically in a jar (Unity) or under a glass dome on top of a
head (Lab), "below and behind" reads as "almost entirely out of frame,"
not a visible secondary lobe. Geometry that never shows isn't buying
anything, so it's cut from both rather than kept as dead triangles:

- `BrainMesh.BuildBrainMass` (Unity): dropped the cerebellum
  `AddUvSphere` call entirely. Triangle count drops from 646 to 520 --
  still comfortably inside the requested 500-2,000 range, just two
  hemisphere lobes now.
- `buildHead`'s `mastermind` branch (Lab): dropped the cerebellum
  `ellipsoid` call added in the prior follow-up, keeping the twin-lobe
  veined hemispheres + central fissure.
- The brainstem is untouched in both places -- it's a separate,
  deliberately visible element (a stalk hanging below the jar / a
  distinct head-to-neck taper), not subject to the same "posed
  vertically, ends up hidden" problem the cerebellum had.

**Verified for real:** the `brainverify` numeric harness (updated to
drop its cerebellum-lobe centroid/vertex-range expectations) passes
clean at the new 520-tri count, 0 winding failures, 0 degenerate
triangles; the flightcheck harness still compiles the whole Unity
gameplay layer clean; `node --check` plus a fresh Playwright/Chromium
render of the Lab's `mastermind` tier confirms the visible silhouette
is unchanged from before the cut (as expected, since the cerebellum
was already occluded from the angles that matter) -- confirming the
cut geometry really was dead weight, not a visible regression.

## 2026-07: FIX -- harvester deliveries banked into a legacy field ResourceHud never reads, so "the list under the clock" genuinely never updated

Creator: "the list under the clock, never seems to change, fill up are
monsters delivering supplies to factory?" -- a real bug, not a
perception issue. `ResourceHud.cs` (the panel `topOffsetPixels = 210f`
below `AnalogClockHud`, i.e. "the list under the clock") polls
`SimBridge.PlayerWallet` fresh every `OnGUI()` -- a live poll, not a
cached snapshot, so it wasn't a display/refresh bug. The harvester-
to-Factory delivery loop itself was also genuinely completing every
time (load carried, arrival detected, `RuntimeCityBuilder.
BankHarvestLoad` called, log line fired). The break was in between:
`BankHarvestLoad` only ever did `WalletBlood += banked` on
`RuntimeCityBuilder`'s own plain auto-property -- a legacy field that
predates match-core's real economy and was never wired to it. Every
successful harvest run was banking into a pool `ResourceHud` never
reads (`SimBridge.PlayerWallet` reads match-core's real
`PlayerState.Wallet`, credited only via `PlayerState.Grant`), so the
list was stuck at 0 no matter how many deliveries happened.

**The fix, matching match-core's own established command-queue
discipline rather than reaching in and calling `PlayerState.Grant`
directly:** a new `CommandKind.BankHarvestLoad` (`Command.cs`) +
`MatchState.ApplyBankHarvestLoad` (`Grant(ResourceKind.Blood, ArgA)`,
silent no-op on a non-positive amount or an out-of-range player index,
same bad-input contract every other command kind already follows) +
`SimBridge.QueueBankHarvestLoadCommand`, mirroring `QueueTrainCommand`/
`QueueAttackBuildingCommand` exactly. One genuine architectural
wrinkle, called out rather than glossed over: every other command kind
validates against a real match-core entity (`TargetEntity`); a
harvester monster isn't itself a `SimUnit` yet (its movement/AI is
still Unity-side, docs/27's migration not yet reached this system), so
there's no source entity to check -- the command carries only
`PlayerIndex` and the amount. `RuntimeCityBuilder.BankHarvestLoad` now
queues this command IN ADDITION TO its old `WalletBlood +=` line (left
in place only because `HudStatus.cs` still reads that field as a
separate legacy debug display -- not because the real fix depends on
it), so the actual gameplay-visible economy is now match-core's, and
the stray legacy counter is flagged as genuinely obsolete rather than
silently duplicated forever.

**Verified for real:** a new `BankHarvestLoadTests.cs` (grant amount,
repeated-delivery accumulation, per-player isolation, non-positive-
amount and out-of-range-player-index no-ops, same-seed determinism)
plus the full existing suite -- `274/274` passing (268 baseline + 6
new). The flightcheck harness recompiled the whole Unity gameplay layer
against a freshly rebuilt match-core DLL clean, confirming
`RuntimeCityBuilder.cs`/`SimBridge.cs` actually compile against the new
API, not just that match-core's own tests pass in isolation.

## 2026-07 follow-up: FIX -- a full harvester could never actually unload, and an empty one just stood there instead of "going searching for more"

Creator: "once a monster delivers the supply it should go searching for
more." Investigating turned up a real control-flow bug, not just a
missing feature -- `MonsterAgent.Update()` (`MonsterAgent.cs`) called
`AcquireTarget()` (line 919, at the time) BEFORE the harvest-tank bank
check (line 930). `AcquireTarget`'s own full-tank branch unconditionally
re-issues `OrderMove(factory)` every single frame this unit is Idle
with a still-full tank -- and `GoIdle()` (which flips `_order` back to
`Idle` on arrival) only fires at the END of the arrival frame, inside
`TickMove`, called by the switch statement AFTER both checks. So the
EARLIEST the bank check could ever see `_order == Idle` was the frame
AFTER arrival -- and on that very frame, `AcquireTarget` (running
first, per the old ordering) had ALREADY re-armed `OrderMove` to the
same hex before the bank check's own `_order == Idle` condition got
evaluated. Net effect: a harvester that arrived at its own Factory
would perpetually re-order itself to the exact same spot, forever,
never once seeing `_order == Idle` and `_carriedLoad > 0` true at the
same time -- so `BankHarvestLoad` (and therefore last follow-up's
match-core wallet credit) may never have actually fired via this
autonomous path in real play at all, only the small residual "player-
driven, wanders near the unload point" case the original docs/22 design
covered.

**Fix: swap the two checks' order.** The bank check now runs BEFORE
`AcquireTarget`, breaking the cycle -- once banked, `_carriedLoad` is
already 0 by the time `AcquireTarget` runs later THIS SAME frame, so
its full-tank branch no longer fires and control falls straight through
to the empty-tank search. Verified this doesn't reorder combat
priority: the bank check never touches `_order`, so whether it runs
before or after `AcquireTarget` doesn't change whether a freshly-
attacked unit still retaliates the same frame -- the only thing the
swap changes is that the harvest-move branch can no longer perpetually
win the race against banking.

**"Go searching for more," the actual feature ask:** even with delivery
fixed, an empty-tank harvester's only search was the same
`AggroRangeMeters` (130m) every idle unit uses for its opportunistic
idle-eat -- fine for a unit reacting to whoever wanders close, useless
for a harvester standing at the Factory (often nowhere near a citizen)
that's supposed to actively go looking. Added `ForageRangeMeters`
(100km, functionally the whole map at this game's scale) used ONLY when
`_harvest != null && _harvest.Capacity > 0.01f` -- a harvester with room
in its tank now searches map-wide for the nearest citizen instead of
standing idle; non-harvesters are untouched, still purely reactive
within `AggroRangeMeters` (a defender shouldn't abandon its post to
chase a citizen across the map). `TickEat`/`ComputePath` already do
real pathfinding over long distances (confirmed by reading -- the
existing 130m fallback already required routing around buildings), so
widening the search radius alone was enough; no new movement machinery
needed.

**Honest limits:** no Unity Editor here to actually watch a harvester
complete this loop on screen -- the fix is derived from a careful,
statement-by-statement trace of `Update()`'s synchronous execution
order within a single frame (not a runtime observation), the same kind
of reasoning that has been wrong before in this project's own history
(docs/28's winding bugs looked right by inspection too, which is why
this session leans on numeric harnesses where one is practical -- a
full `MonsterAgent` simulation harness wasn't, given how deeply it's
coupled to `UnityEngine.MonoBehaviour`/`transform`/`_builder`/`_fighter`
state a headless harness would have to fake most of anyway). Verified
for real: `MonsterAgent.cs` brace-balanced; flightcheck recompiles the
whole Unity gameplay layer clean.

## 2026-08: SelectionHud.cs -- per-type creature icons docked beside the minimap

Creator: "the game needs icons of the different creature next to the
minimap." docs/23 §13 amendment G already flagged the SC2 "control
groups" gap, and this is that same family of missing HUD verb --
confirmed with the creator as the SC2-style selection panel (one icon
per distinct creature TYPE currently selected, with a count badge),
not a production-queue widget or the pre-RTS Menagerie loadout screen.

**"Type" was already a defined concept, not a new one to invent.**
`MonsterAgent.BodyPlan`'s own doc comment already calls itself "the
type for SC2-style double-click 'select all of this type on screen,'"
and `WaypointCommander`'s double-click handler already groups by it
(`UnitsOfTypeOnScreen(cam, agent.BodyPlan)`). `SelectionHud.cs` buckets
the live selection (`WaypointCommander.Selected`, a new public pruned
accessor over the existing private `_selected` list) by that exact same
`BodyPlan` string, so a mixed army reads as N icons (one per body plan
present), each showing a count badge once >1. Clicking an icon calls
the also-newly-public `WaypointCommander.SetSelection` to narrow the
current selection down to just that type -- useful the instant a mixed
selection needs a type-specific order.

**Icon idiom: reuses `BuildingNavHud`'s established colored-swatch-plus-
abbreviation "icon," not new art.** This repo still has no icon sprite/
texture assets anywhere. The swatch color is a new `MonsterBody.
PlanColor(plan)` -- a one-line wrapper around the EXISTING private
`SkinColor(plan, creatureId)` the real creature bodies already render
with, passing `plan` itself as the hash seed instead of a creature id so
every plan (listed or not in `SkinColor`'s switch) gets one fixed,
stable color instead of `SkinColor`'s per-INDIVIDUAL hash -- a type-
level HUD icon can't use a per-instance color without being
misleading about what it's grouping. This also means a plan's HUD icon
color and its in-world body color always agree for the 8 named plans
(crab/serpentine/winged/avian/arachnid/treant/floater/blob); tetrapod
(the unnamed default case) gets its own stable hash-derived color too,
just not a hand-picked one.

**Placement: docked to the minimap's actual live rect, not a hardcoded
corner.** New `Minimap.ScreenRect` (a one-line public wrapper over the
existing private `GetScreenRect()`) lets `SelectionHud` sit immediately
right of the minimap's right edge, bottom-aligned with it, regardless of
which corner the developer has the (fully repositionable, per its own
existing Inspector fields) minimap parked in.

**Wired into the existing `PointerOver` guard chain.** Same "OnGUI's
event queue and the New Input System's `Mouse.current` don't talk to
each other" problem every other HUD panel here already solves --
`SelectionHud.PointerOver` added alongside `Minimap.PointerOver`/
`BuildingNavHud.PointerOver` at all three existing guard call sites
(`WaypointCommander.Update`, `BuildGhostCursor.Update`, `GrabCursor.
Update`) so clicking a selection-panel icon doesn't ALSO fire a
world-space select/order/placement click underneath it. Wired into the
match the same way `BuildingNavHud`/`Minimap` already are, in
`RuntimeCityBuilder`'s existing HUD-wiring block, right after
`minimap.Init`.

**Honest limits:** no Unity Editor here to confirm the icons render at a
legible size/position next to the minimap, or that the count badge
doesn't clip at the corner -- same standing posture as every other
Unity-side entry in this log. Verified for real: every touched file
brace-balanced; `SelectionHud.cs` itself compiled clean (`dotnet build`,
net8.0) against hand-written stubs that mirror the exact new public
signatures added this session (`WaypointCommander.Selected`/
`SetSelection`, `Minimap.ScreenRect`, `MonsterBody.PlanColor`) -- a
targeted compile check of the new surface, not the full flightcheck
harness (this session's cached copy of that harness predates several
HUD systems `RuntimeCityBuilder.cs` now references and would need
real reconstruction work to catch up, out of scope for this change).

## 2026-08: flightcheck harness reconstruction -- the gap SelectionHud's own entry flagged, closed for real

The scratch flightcheck harness this whole session has leaned on had
quietly drifted: `BuildGhostCursor.cs`, `BuildMenuHud.cs`,
`BuildingNavHud.cs`, `FactionPickerHud.cs`, `LumenHud.cs`,
`RegionPickerHud.cs`, `ResourceHud.cs`, `TramCar.cs`, `TramDresser.cs`
(and `SelectionHud.cs`, `HexGridGizmo.cs`, `MixedFactionUnlock.cs`) were
never actually in its compile set -- a separate `MissingPeerStubs.cs`
hand-stubbed each one's public `Init` signature instead, satisfying
`RuntimeCityBuilder.cs`/`WaypointCommander.cs`'s references without
ever compiling those files' own real bodies. Several past entries in
this very log claimed "flightcheck + docs update" for exactly these
files -- true of whatever the harness looked like AT THE TIME, but the
scratch copy itself apparently never kept pace, so by now those claims
were quietly checking less than they said.

**Fixed for real:** every one of those files now compiles as its
actual real body in `FlightCheck.csproj` (RosterFetcher.cs stays
hand-stubbed in `ProjStub.cs`, on purpose -- it needs
`UnityEngine.Networking`, explicitly out of scope). `MissingPeerStubs.cs`
emptied out (kept as a file, its own header now explains why). Real,
newly-found stub gaps this surfaced in `UnityStub.cs` (fixed, not
routed around): `TextAnchor`, `GUIContent`/`GUIStyle`/`GUISkin` +
`GUI.skin`/`GUI.Button`/`GUI.enabled` (several HUD panels' shared
`DrawShadowedLabel`/button idiom), nine more `Keyboard` digit/
punctuation/escape keys (`BuildMenuHud`'s 1-9 hotkeys, `BuildingNavHud`'s
`,`/`.` cycle keys, `BuildGhostCursor`'s Esc-cancel), and an in-memory
`PlayerPrefs` stand-in (`MixedFactionUnlock`'s persistent-unlock flag --
real persistence is out of scope for a headless compile check, only the
shape mattered here).

**Honest limits:** same as always -- no Unity Editor here, so this
confirms the whole Unity gameplay layer compiles as one coherent
program against the real match-core DLL, not that any of it renders or
behaves correctly on screen. Verified for real: full `dotnet build`
clean, 0 errors, 0 warnings, every file in the list above now compiled
from its actual repo path rather than a hand-written stand-in.

## 2026-08: monsters spinning around each other while trying to pass -- partial fix, honest remainder documented

Creator: "monsters will spin around each other trying to pass one
another. Possible solution, make one turn clockwise the other counter
clockwise. Or make one stop while the other passes." A real avoidance
bug, root-caused rather than guessed at.

**Root cause: `MonsterSteeringController.PredictiveAvoidance`'s side
tie-break has no memory.** For each closing neighbour it picks a pass
side from `onRight = Vector3.Dot(relPos, right)`'s sign, recomputed
from scratch every frame. For a near-exact head-on pair `onRight`
hovers close to zero, and since it's evaluated independently on BOTH
units (once as `self`, once as `c`), ordinary per-frame noise --
`ApplySeparation`'s own hard positional correction re-centering the
pair, a neighbour's own avoidance push, group alignment pulling
headings around -- can flip its sign on one unit but not the other,
producing mismatched sides that visibly spin instead of committing.

**Built a standalone numeric harness first** (`steerverify`, same
pattern as `brainverify`) rather than trusting code-reading alone --
this exact class of "looks right by inspection" bug is the one docs/28
has bitten this project on before. A lone pair, even started
near-perfectly symmetric, resolved cleanly either way (2-4 lateral
side-reversals over a 16m approach, not "spinning"). It took a REALISTIC
scenario to reproduce the reported symptom: two 3-unit same-faction
squads passing head-on down a corridor -- 33 total side-reversals
across the group, one unit reversing 8 times before finally getting
through. (Also found and fixed a harness gap of its own along the way:
the first version only simulated `Combine`'s SOFT separation blend, not
`ApplySeparation`'s separate HARD positional correction that also runs
unconditionally every real frame -- adding that second stage was
necessary to reproduce the bug at all; a pair-only, single-stage
harness had stayed clean regardless of the fix.)

**The fix -- the creator's own suggested "one clockwise, one counter-
clockwise," made stable rather than reactive:** within a new
`TieBreakDeadband` (0.3m) of the ambiguous `onRight≈0` boundary, fall
back to a per-PAIR-stable decision built from `Mathf.Min` of both
units' `GetInstanceID()`s -- evaluates identically regardless of which
of the two is `self` this call (unlike a naive `self &lt; c` compare,
which would itself flip between the pair's own two calls and defeat
the point), so it can never mismatch between the two units the way the
raw geometric sign could. Same structural idea `TrafficCar.cs` already
uses for lane assignment (a fixed per-car offset, not reactive to the
other car), just keyed off identity instead of a road lane -- and the
same idiom (`GetInstanceID()` as a stable per-unit deterministic input)
`TrafficCar.cs` already leans on elsewhere. A deadband sweep (0.3-2.0m,
all identical results; 3.0m made things WORSE by overriding real
geometric signal on clearly-lopsided approaches) confirmed 0.3m is a
reasonable, conservative choice, not an arbitrary one.

**Honest result, not oversold:** the 3v3-squad harness scenario
improved from 33 to 28 total side-reversals with the fix -- real,
measured, not a full cure. Isolating JUST avoidance+separation (zeroing
Alignment/Cohesion's contribution) was already far cleaner (8
reversals) even WITHOUT the tie-break fix, pointing at a second,
separate contributor this pass does not fix: `Alignment`/`Cohesion`
treat "groupmate" as same-`Faction`, not "same order/destination" (the
exact gap docs/27 Phase B already flagged for queued group moves) --
so two OPPOSING squads of the same faction currently try to align
toward each other's blended average heading while also trying to avoid
each other. Fixing that is a real design question (what should
"groupmate" mean once more than one order is in flight?), not a
mechanical bug, and is flagged in `MonsterSteeringController`'s own
class header rather than silently re-scoped into this pass or silently
left undocumented. The creator's second suggested fix ("make one
stop") already exists as a slower fallback --
`DeadlockManager`'s yield-grant mechanism -- but only triggers after a
2.5s stall window, well after a fast oscillation would already read as
"spinning" on screen; not touched this pass.

**Verified for real:** the `steerverify` harness (isolated pair,
realistic 3v3 squad, and an Alignment/Cohesion-ablated variant) backs
every number above; the fix does not regress the already-clean pair
case (byte-identical flip counts before/after). Flightcheck recompiles
the whole Unity gameplay layer clean. No Unity Editor here to confirm
how this actually reads on screen -- same standing limit as every
other Unity-side entry in this log.

## 2026-08 follow-up: CORRECTION -- the previous entry's own numbers were wrong, from a real bug in the verification harness itself, not just the game

Creator, following up on the same "spin around each other" bug: "monsters
in a groupmate should pick parking spots around and near the target but
not the same target, based on their ETA, speed. verify if this a viable
solution to the dosey doe problem." Investigated first (see below), then
implemented the actual fix this surfaced -- and in the process, caught
that the PREVIOUS entry's own "already clean either way" claim about a
lone pair was flat wrong, from a bug in `steerverify` itself.

**Part 1 -- was ETA/speed-based parking-spot assignment viable?** No,
and not because it's a bad idea -- it targets a different, real problem.
`WaypointCommander.AssignFormation`/`RingTarget` already give every unit
in a single ordered GROUP a distinct destination hex (creator's own
earlier direction: "distribute themselves around the waypoint NOT ON
the waypoint") -- greedy-nearest, not ETA-aware, but already solved for
the case of "many units converging on one shared point." The reported
"dosey doe" bug reproduces with two SQUADS already headed to opposite,
distinct destinations, just crossing paths mid-corridor -- distinct
parking spots don't touch a mid-transit crossing problem. The real gap
this surfaced: `FormationHexes` only dedupes destinations WITHIN one
`AssignFormation` call -- two SEPARATELY issued orders to the same
target (e.g. two squads independently sent to attack the same building)
don't coordinate at all. That's a genuine, different bug worth its own
pass; not built this round (scoped, not silently skipped).

**Part 2 -- finishing the flagged Alignment/Cohesion fix turned up a
much bigger correction.** Added `OpposingHeadingCutoff`: `Alignment`/
`Cohesion` now exclude a same-`Faction` neighbour whose velocity is
`Vector3.Dot(velocity, fwd) < 0` (more than 90 degrees off THIS unit's
own intended heading) from the "groupmate" pool, instead of same-
`Faction`-alone. Re-ran `steerverify` to confirm it helped -- and got
BYTE-IDENTICAL numbers to the unfixed baseline. Tracing it down: the
harness never published a moved unit's `LastVelocity` between frames
(a real bug in the TEST, not the game) -- `MonsterAgent.Update()`
publishes it every real frame (line ~989), but `steerverify` just moved
`transform.position` and left `LastVelocity` at its default `Vector3.
zero` forever. `PredictiveAvoidance`'s math still degraded plausibly
with a "stationary" neighbour (so the previous entry's tie-break
findings for THAT function held up), but `Alignment` explicitly skips
near-zero velocity -- it was a complete, silent no-op for the ENTIRE
previous verification pass, which is exactly why the earlier "a lone
pair was already clean either way" conclusion was wrong: with
`LastVelocity` never contributing, that pass could never have observed
what Alignment does to a real, moving encounter.

**Corrected, with LastVelocity now actually published:** a lone pair
walking straight at each other was NOT clean -- the worst unit reversed
its pass side roughly 19-21 times over one approach, the single worst
result across every scenario tested. `OpposingHeadingCutoff` alone
fixes this completely (0 reversals); `TieBreakDeadband` alone does
NOTHING for it (byte-identical to no-fix baseline) -- the previous
entry's claim that the tie-break was "the fix" for the reported symptom
had the two mechanisms' real contributions backwards. For the tougher
3v3-squad scenario, both fixes together (19 reversals) beat either one
alone (Alignment-only: 27; tie-break-only: 17) or neither (20) -- genuine
improvement, still not a full cure for a dense multi-unit scrum.

| Config | Lone pair (worst unit) | 3v3 squad (total) |
|---|---|---|
| Neither fix | 19-21 reversals | 20 |
| `TieBreakDeadband` only | 19-21 (unchanged) | 17 |
| `OpposingHeadingCutoff` only | **0** | 27 |
| Both (shipped) | **0** | **19** |

**Honest limits, same as ever:** no Unity Editor to confirm this reads
right on screen. This correction itself is the argument for why this
project leans on numeric harnesses at all -- and also the reminder that
a harness is only as trustworthy as its own fidelity to the real
per-frame order of operations; this one had a real gap for two rounds
before it was caught. Verified for real this time: four labeled
`steerverify` configurations (neither/tie-break-only/alignment-only/
both) with `LastVelocity` correctly published, matching `MonsterAgent.
Update()`'s own real per-frame publish point; flightcheck recompiles
the whole Unity gameplay layer clean.

## 2026-08 follow-up: "monster went into the factory and never left it" -- found and fixed; plus monster-size-aware parking + ETA-based formation slots

Creator report, then follow-up direction: "increase the boundary around
building so parking spots take into account monster size, monster are
circling each other less but not solved. See if the speed based
solution with coordinate their landing spots is viable."

**Root cause of the stuck-in-factory report, found and fixed:**
`MonsterAgent.TickSettle`'s per-step validity check
(`InsideBuildingFootprint` + `Blocked().Contains(hex)`) has no notion of
"the hex I'm already standing on" -- it just asks "is the NEXT step's
hex/footprint clear," unconditionally. A freshly-cloned monster
(`GrabCursor.CloneOnto` deliberately spawns it AT the Factory's own hex
-- "it visibly comes out of the building that made it") or a roof
occupant just evicted back onto that hex (`BootFromRoof`) starts its
very first settle-creep step only centimetres from where it's already
standing -- still the SAME hex, which the Factory's own footprint keeps
permanently blocked. The check rejected literally every possible first
step, `_settleTarget` got nulled before the unit ever moved, and it sat
exactly on/inside the Factory's rendered footprint forever -- an exact
match for the report. Fixed by excluding the unit's own current hex
from both the blocked-hex check and `RuntimeCityBuilder.
InsideBuildingFootprint`'s overhang check (new optional `exclude`
param) -- forgives only the ONE hex a unit already legitimately
occupies; a real walk-into-a-building step from open ground is still
caught exactly as before.

**Monster-size-aware parking boundary:** `GrabCursor.FindOpenHexNear`
(clone parking around a Factory, and the eviction "boot to nearest open
spot" path) used to accept the first hex that merely wasn't
`IsBlocked`, regardless of the monster's own body radius -- fine for
most creatures (a hex's ~20m step already clears the building's ~9m
footprint half-extent), but a big-bodied monster's collision radius
could still reach back into the building's real footprint (corner
overhang included) or crowd an already-claimed neighbour. Now checks
the NEAR EDGE of where the monster's body would actually sit (offset
`bodyRadius` back toward the building) against the real footprint
geometry, plus a `2*bodyRadius + groupSpacing` minimum gap between
claimed spots -- the effective search boundary grows with the monster
automatically instead of a fixed ring count. `MonsterAgent.Radius` is
now a public passthrough of `_fighter.Radius` for this (and any future)
caller to read.

**ETA-based formation slot assignment, viability confirmed and
shipped, scoped honestly:** `WaypointCommander.AssignFormation`'s
greedy nearest-slot-to-nearest-unit pick now ranks by ETA (distance /
`MonsterAgent.WalkSpeed`, a new public passthrough of
`_profile.WalkMetersPerSecond`) instead of raw distance -- in a
mixed-speed group, a slow unit that merely started nearer a slot than a
faster unit could still take longer to actually reach it, so the two
crossed paths converging on their (mismatched) slots. Viable and real,
shipped for ground formations (`AssignFormation`); NOT applied to roof
perch assignment (`AssignPerch`) this pass -- flyers separate by
altitude far more than ground units cross paths, lower payoff for the
same change, left as a clean follow-up if it turns out to matter.
**Honest scope note:** this is a DESTINATION-assignment fix, not a
moment-to-moment steering one. It doesn't touch, and isn't expected to
fix, the residual close-quarters circling the previous entry above
already measured for a tight multi-squad scrum (19 reversals, down from
20, still not a full cure) -- that's steering-time
`Alignment`/`PredictiveAvoidance` territory, a different mechanism from
"which slot did this unit get assigned to."

**Also found, NOT fixed this pass (scoped out, tracked separately):**
a real, independent bug in the harvester full-tank auto-delivery path.
`MonsterAgent.FindOwnFactory` returns the Factory's own (permanently
blocked) hex; the full-tank branch in `AcquireTarget` orders a Move
straight at it; `HexPathfinder`'s pathfinding rejects a blocked goal hex
instantly; `TickMove`'s failure path collapses to a plain `GoIdle()` for
an ordinary (non-attack-move) order; next frame `AcquireTarget` sees
`Idle` with a still-full tank and reissues the exact same doomed order.
A harvester that tops off its tank more than the ~2.5-hex bank radius
from its Factory (very possible -- `ForageRangeMeters` is effectively
unbounded) freezes in place forever, spinning through Move/Idle, never
delivering and never resuming foraging. Independent of the settle-creep
fix above; not what the creator's literal report reproduced (that one
strands a unit AT the Factory, this one strands a unit wherever it
happened to fill up), but real and worth a dedicated pass.

**Verified for real:** flightcheck recompiles the whole edited set
(`RuntimeCityBuilder.cs`, `MonsterAgent.cs`, `GrabCursor.cs`,
`WaypointCommander.cs`) clean against the real Unity project files. No
Unity Editor here to confirm any of this on screen -- same standing
limit as ever.

## 2026-08 follow-up: CORRECTION -- the harvester "infinite loop" diagnosis was wrong; the REAL gap was RTS buildings having zero footprint in Unity's own pathfinding

Creator direction: "fix that bug" (the harvester full-tank pathing bug
flagged above), followed a beat later by "increase the building no
parking area to take into account the monster size" (a follow-up
widening the parking-boundary fix, see below).

**The harvester bug as originally diagnosed does not reproduce.**
Reading `HexPathfinder.FindPath`/`MonsterAgent.ComputePath` to actually
implement the fix surfaced the load-bearing assumption underneath it:
that `Blocked().Contains(factoryHex)` is true, so the pathfinder rejects
the Factory as a goal. Tracing `Blocked()` -&gt; `RuntimeCityBuilder.
BlockedFor` -&gt; `_battlefield` all the way down: `_battlefield` is built
ONCE from the procedural `_city` at `BeginMatch` (`BattlefieldState.
FreshFrom`) and only ever updated for EXISTING procedural buildings
taking damage (`WithBuildingDamage`). A `grep` across every `.cs` file
in the repo for anything mutating `_city.Buildings`/`BattlefieldState.
Buildings` turned up nothing -- an RTS building placed via
`SimBridge`/`MatchState` (`SpawnHqForPlayer`/`SpawnFactoryForPlayer`,
or mid-match worker construction) was NEVER added to it. `SpawnStarting
Bases`'s own doc comment already said as much ("match-core's own
building-blocked set... isn't visible to Unity's own BlockedFor
query") -- missed on the first read. So the Factory's hex was never
actually blocked, `FindPath` never rejected it as a goal, and (tracing
the rest of the flow) a full-tank harvester should already path onto
it and bank successfully regardless of distance. The originally-
reported "monster went into the factory and never left it" bug (fixed
in the prior entry, `TickSettle`'s self-block on a unit's own current
hex) is ALSO now uncertain to have been caused by the Factory's own hex
being blocked/footprint-covered specifically -- that fix is still a
real, correct, harmless improvement (any hex genuinely blocked for
other reasons is handled right), just possibly not proof positive of
the reported symptom's exact mechanism. Told the creator directly
rather than shipping a fix for a bug that doesn't exist.

**The real gap, found in the process and now fixed: RTS buildings had
ZERO footprint in Unity's own collision/pathfinding at all.** A ground
unit could walk straight through a standing Factory or HQ -- nothing
in `BlockedFor`/`HexPathfinder` knew it was there. Fixed in
`RuntimeCityBuilder.BlockedFor`: every standing (non-`Destroyed`)
`SimBuilding` is now unioned into the ground/amphibious blocked set on
top of the existing procedural-only cache, same "destruction reopens
the hex" policy `BattlefieldState.BlockedToGround`'s own doc already
states for procedural buildings. Kept as a separate cache layer (not
merged into the base cache) so the no-active-match case (menus, the
Lab) stays a zero-copy cached-reference return exactly as before; once
a match exists, a cheap signature over every building's (EntityId,
State) -- not `BuildingCount` alone, which wouldn't change on a
Complete/Destroyed transition -- decides whether the small combined-set
rebuild is actually needed. Scoped to ground/amphibious only: flight
blocking and the footprint-overhang/roof-height systems
(`BlockedForFlight`, `InsideBuildingFootprint`, `_roofCache`) are
untouched -- flyers still cruise over RTS buildings exactly as before,
a real, separate design question (does a flyer treat an RTS building's
roof like any other perchable roof?) left open rather than guessed at.

**Necessary companion fix, or this would have shipped the EXACT bug
originally (mis)diagnosed:** once the Factory's own hex genuinely
blocks, `MonsterAgent.FindOwnFactory`'s existing callers -- which
target that hex DIRECTLY -- would have started failing for real. Added
`FindOwnFactoryApproachHex`: same "approach the rim, not the centre"
idea `ComputeApproachPath`/`HexPathfinder.FindPathToBuilding` already
use for attack orders, simplified since a `SimBuilding`'s footprint is
always exactly one hex (its own class doc says so) -- nearest open
neighbour of the Factory's hex, falling back to the hex itself if
every neighbour is somehow also blocked. The full-tank delivery
`OrderMove` now targets this instead of the Factory's own hex. This
also RETROACTIVELY VALIDATES the prior entry's `TickSettle` fix: a
Factory clone/evicted roof occupant spawns ON the Factory's own hex,
which is now genuinely blocked going forward -- the "exclude the hex
I'm already standing on" exemption that fix added is exactly what
keeps that still working.

**Also shipped, same pass (creator follow-up: "increase the building
no parking area to take into account the monster size"):** a new
`GrabCursor.buildingClearanceMargin` Inspector field (default 2m),
added on top of a monster's own body radius in `FindOpenHexNear`'s
near-edge check -- the prior pass's exact-geometric-fit boundary left
zero breathing room; this widens the effective no-parking zone around
every building for every monster size at once, independent of the
per-monster radius math.

**Verified for real:** flightcheck recompiles the whole edited set
(`RuntimeCityBuilder.cs`, `MonsterAgent.cs`, `GrabCursor.cs`) clean
against the real Unity project files, including the new `MadDr.
MatchCore.BuildingState`/`SimBuilding` reads in `BlockedFor`. No Unity
Editor here to confirm any of this on screen. One real, checked knock-on
effect: `Citizen.cs` calls `_builder.BlockedFor(false)` three times of
its own (flee-hex validation, destination picking) -- these now also
correctly avoid RTS building hexes (a citizen fleeing INTO a standing
Factory wall was arguably already wrong), a believed-benign side effect
of the same fix, not a separate change, but not independently verified
in a live match either. `TrafficCar.cs` routes off the road network
instead and doesn't call `BlockedFor` at all -- unaffected.

## 2026-08 follow-up: building HP bumped again, and all three harvest resource lanes now actually get banked

Creator direction: "give buildings much larger hit points. And when I
building disgorges it's large number of humans, the humans should
spawn and flee. Any collecting units will try to grab as many of them
as it can and then unload to the factory. Humans have all the
resources. make sure that those are properly being harvested as well.
Specialized harvesting units is not viable in this game, all
harvesters can collect all resources."

**Building HP, bumped again.** There were TWO parallel HP tables for
two separate building systems, and only one had ever been bumped
before: `MadDr.MatchCore.BuildingDef` (the RTS-buildable roster --
HQ/Factory/storage/etc.) got a 50% bump in an earlier pass
(300/600/1500/3000 -> 450/900/2200/4500), but `MadDr.CityGen.
BuildingStats` (the SEPARATE table for the procedural CIVILIAN city --
houses, shops, landmarks a player attacks mid-match) was still at the
ORIGINAL, never-touched docs/18 baseline. Bumped BOTH tables to the
SAME absolute figures this time -- 1000/2000/5000/10000 (Small/Medium/
Large/Landmark), armor unchanged on both, same "more hits to fell, no
harder to actually damage per hit" reasoning as the first pass. Bridges
share `BuildingStats.StructureHp` too (Large tier), so a bridge is now
5000 HP as a side effect -- correct per the same table, not a separate
decision. docs/18's own tier table (the file's own doc comment names it
as the tuning source of truth) updated to match, including the bridge
stats line. Five citygen-core tests asserted the old hardcoded numbers
as "the docs/18 table" -- updated to the new numbers, not deleted or
loosened; all 168 citygen-core tests and all 274 match-core tests pass.

**Disgorge-flee-harvest pipeline: verified already built, not
re-invented.** Traced the full chain the creator described and confirmed
each link already exists from earlier phases: a building's `Occupants`
count (BuildingDef/BuildingRuntimeState) disgorges that many fleeing
Citizens the instant it flips to Destroyed (`BaseDresser` ->
`RuntimeCityBuilder.SpawnFleeingOccupant`, task #96); each one starts a
forced panic sprint away from the wreck before falling back to normal
citizen AI (`Citizen.InitFleeingFrom`, task #97); an idle harvester with
tank room forages the WHOLE MAP for the nearest citizen (unbounded
`ForageRangeMeters`, task #126) and chains from one kill straight into
searching for the next (docs/22's "once a monster delivers the supply
it should go searching for more"), which is exactly what "grab as many
of them as it can" looks like against a fresh burst of disgorged
citizens without any new swarm-specific code. No changes made here --
flagged as verified-not-invented rather than silently claimed as new
work.

**The real gap, and the one actually acted on: only Blood was EVER
banked, regardless of what a harvester's own tool actually gathered.**
`HarvestProfile` (`packages/roster-client/src/Harvest.cs`, ported from
genome-core's harvest.ts) already computes THREE separate gather rates
per creature -- `GatherBlood`/`GatherBone`/`GatherBrain`, weighted by
hand-tool family (a `bone_saw` yields 3.0 Bone but only 0.5 Blood; a
`lamprey_maw` the reverse) -- exactly the "every harvester collects
everything, just at different RATES" design docs/22 already describes.
But `MonsterAgent.CreditHarvestForEatenCitizen` only ever read
`GatherBlood`, and `RuntimeCityBuilder.BankHarvestLoad` banked the
WHOLE pooled load as pure Blood -- so a Bone/Brain-favoring build
wasn't "specialized," it was just BAD at the one thing that mattered,
with zero offsetting benefit. That's the real "specialized harvesting
isn't viable" bug, not the existence of different gather rates per
tool (which is the intended design and stays).

Fixed end to end:
- `MonsterAgent`'s single pooled `_carriedLoad` float is now three
  separate running totals (`_carriedBlood`/`_carriedBones`/
  `_carriedBrains`), summed via a new `TotalCarriedLoad` property for
  every place that used to read the old pooled field (capacity gate,
  full-tank check, idle bank-check, load-speed-penalty). `Credit
  HarvestForEatenCitizen` now credits all three lanes from one eaten
  citizen at once, at this creature's own three gather rates, still
  capped to the tank's total `Capacity` -- if the combined yield would
  overflow the remaining room, every lane scales down by the SAME
  factor rather than filling whichever lane happens to be credited
  first, so the banked mix still reflects this creature's own
  gather-rate ratios (a lamprey-handed harvester still comes home
  mostly Blood, just with real Bone/Brain too, not exclusively Blood).
- `RuntimeCityBuilder.BankHarvestLoad(blood, bones, brains)` now banks
  each nonzero lane separately.
- `SimBridge.QueueBankHarvestLoadCommand` gained a `ResourceKind
  resource = ResourceKind.Blood` parameter (default preserves every
  untouched call site's old meaning, since `Blood` is enum value 0 --
  the same value `ArgB` carried implicitly when it was unused).
- `MadDr.MatchCore.Command`'s `BankHarvestLoad` kind now reads `ArgB`
  as the `ResourceKind` selector instead of leaving it unused;
  `MatchState.ApplyBankHarvestLoad` validates it's in range (silent
  no-op otherwise, same bad-input contract every other command kind
  already has) and grants that specific resource. `Command`'s own
  struct shape (still all-integer, still hashes byte-for-byte) is
  unchanged -- this is a NEW MEANING for an already-existing, already-
  serialized field, not a new field, so old replays/tests needed no
  migration.

**Verified for real:** four new `BankHarvestLoadTests` cases cover
ArgB resource selection, backward-compatible Blood-default omission,
multiple resources banked independently in one tick, and an
out-of-range ArgB silent no-op -- all pass alongside the pre-existing
six. Full match-core suite (278 tests including the new four) and full
citygen-core suite (168 tests) both pass. flightcheck recompiles the
whole edited Unity set against FRESH `MadDr.MatchCore.dll`/`MadDr.
CityGen.dll` builds (not the stale pre-built ones it normally
references) clean. No Unity Editor here to confirm the multi-resource
HUD readout or the disgorge/flee/harvest loop on screen -- same
standing limit as ever.

## 2026-08 follow-up: harvester "fill the tank vs deliver now" starvation guard

Creator direction: "will need a balance between filling the tank and
chasing humans for a long time vs getting resources to the factory so
it can build units, this needs to be thought out so players are not
starved."

**The gap:** `AcquireTarget`'s foraging fallback had exactly two modes
-- literally FULL (deliver, unconditionally) or NOT full (search the
WHOLE MAP, `ForageRangeMeters` = 100km, for the nearest citizen,
however far). Nothing in between: a harvester sitting on, say, 80% of
its tank would still trek across the entire map for one more citizen
rather than bank what it already had, so the Factory's production
queue could stall for however long that one trek took -- exactly the
starvation risk named.

**Fix:** a new `PartialLoadReturnFraction` (0.5, a real v0.1 number,
not sourced from any doc -- same placeholder policy as every other
economy constant) gates the search radius. Below half a tank, behavior
is UNCHANGED -- search the whole map, since a return trip would
deliver next to nothing anyway, nothing lost by continuing to hunt.
At or above half a tank, the search shrinks to the ordinary
`AggroRangeMeters` (130m, the same "nearby" radius a non-harvester
already uses) -- still eagerly tops off from whatever's actually
close, but stops treating a distant straggler as worth delaying
delivery for. Either way, if the search (at whichever radius applies)
comes up empty and the tank isn't literally empty, the harvester now
delivers its partial load immediately instead of standing idle with it
-- covers both "nothing worth chasing nearby" and the rare late-match
case where no citizens remain anywhere at all, the same "don't just
stand there" reasoning `ForageRangeMeters` itself was originally added
for.

**Honest scope:** this is a structural fix (WHEN to stop hunting and
go deliver), not a numeric-balance pass -- `PartialLoadReturnFraction`
and `AggroRangeMeters` are both placeholders, real tuning is the
Phase-2 sandbox pass CLAUDE.md already calls for. No new player-facing
control was added (no Inspector slider) -- kept as a private const,
matching how `ForageRangeMeters`/`AggroRangeMeters` are already
declared in this file, not a new tunability surface inconsistent with
the rest of the class.

**Verified for real:** flightcheck recompiles `MonsterAgent.cs` clean.
No Unity Editor here to actually TIME a harvester's return cadence or
confirm the Factory doesn't stall in a live match -- the mechanism is
reasoned through and code-reviewed, not measured, same standing limit
as ever.

## 2026-08 follow-up: circling bug, CORRECTED again -- TieBreakDeadband never scaled with a pair's own size

Creator direction: "monsters are still circling each other. It seems
to happen with larger monster."

**Found it, and it's an honest gap this pass's OWN prior verification
should have caught but didn't:** every `steerverify` scenario run
across the whole "dosey doe" saga (this file's several prior entries)
used the SAME default 1.5m `Radius` for every test unit. Nothing was
ever tested at a larger size. That mattered because `PredictiveAvoidance`'s
`TieBreakDeadband` -- the per-pair-stable tie-break window that decides
which side each unit swerves to once an approach is close enough to
head-on that the raw geometric sign gets noisy -- was a FLAT 0.3
*meters*, never scaled against the pair's own `combined` collision
envelope (`bodyRadius + AvoidancePadding`). For the default size,
`combined` ~4.5m, so 0.3m is a real ~7% slice of it -- narrow but
meaningful. For a genuinely large monster (say 5m radius each,
`combined` ~11.5m), the SAME flat 0.3m is under 3% of the envelope --
a much WIDER band of "almost but not quite head-on" approaches fell
OUTSIDE that thin window and back onto the raw, flip-prone geometric
sign the whole mechanism exists to avoid. Exactly "circling... seems
to happen with larger monster."

**Fixed:** `TieBreakDeadbandFor(combined)` replaces the flat constant,
returning `Max(TieBreakDeadband, combined * TieBreakDeadbandFraction)`
-- `TieBreakDeadbandFraction` is `1/15`, chosen as the EXACT crossover
for the default 1.5m-radius case (`4.5 * 1/15 = 0.3`, the original
constant, used here as a floor) so small/default bodies are BYTE-FOR-
BYTE unregressed, while anything larger gets a proportionally wider
window instead of staying pinned to a small body's own scale.

**Verified for real, and this time actually at the size that matters:**
extended `steerverify` with four new scenarios (4-7) mirroring
scenarios 0-3 but at 5m radius instead of the default 1.5m (squad
spacing widened to clear `2*radius` so bodies don't spawn already
overlapping). Before this fix, large-radius scenarios weren't run at
all; after it, all four are CLEAN or near-clean (0/0 lone pairs, 4
total/worst-2 and 2 total/worst-1 for the two large-squad scenarios,
down from what a flat deadband would produce at that size). The
small-radius scenarios 0-3 are UNCHANGED from the prior entry's own
numbers (19/5 for the hardest squad case) -- confirmed by re-running,
not assumed. flightcheck recompiles clean. No Unity Editor here to
confirm this on screen with an actual large creature model -- same
standing limit as ever, but this is the first pass in this whole saga
to actually exercise a large body size at all, which is the concrete
gap the creator's report pointed at.

## 2026-08 follow-up: procedural-building disgorge gap, and a monster recall button

Creator direction: "I don't see people fleeing from the wreckage of the
building. So monsters can chase them. Also give me a monster recall
button that will gather my troupes in one place."

**Found the gap: occupant disgorge only ever fired for the RTS building
roster, never for procedural civilian buildings -- the vast majority of
the map, and fully attackable/destructible.** This codebase has two
separate building systems: `MadDr.MatchCore.SimBuilding` (the RTS
roster -- HQ/Factory/storage, tracked via `SimBridge`) and `MadDr.
CityGen.Building` (the procedural civilian city -- houses/shops/
landmarks, tracked via `RuntimeCityBuilder`'s own `_battlefield`).
Occupant disgorge (`RuntimeCityBuilder.SpawnFleeingOccupant`, "when
they are destroyed they disgorge their human occupants that flee") was
wired ONLY into `BaseDresser`'s per-frame watch of `SimBuilding.State`
-- confirmed by `SpawnFleeingOccupant`'s own doc comment, and by
`ApplyBuildingDamage` (the PROCEDURAL destruction path, reached via
`MonsterAgent.TickAttack` -> `OrderAttack(Building)`, the ordinary way
a player actually smashes most of the city) having zero Citizen-related
code in its entire `Destroyed` branch -- rubble, dust, scorch decal,
nothing else. `MadDr.CityGen.Building` also had no `Occupants` concept
of its own at all; that field only ever existed on `BuildingDef`, the
RTS-only table.

**Fixed:** added `BuildingStats.Occupants(BuildingTier)` (`packages/
citygen-core/src/BuildingTier.cs`), a parallel small-flat-count-by-tier
table alongside the existing `StructureHp`/`Armor` (3/5/10/15 for
Small/Medium/Large/Landmark -- a real v0.1 placeholder, not sourced
from any doc, same standing policy as `BuildingDef.Occupants`'s own).
`RuntimeCityBuilder.ApplyBuildingDamage`'s `Destroyed` branch now loops
that count and calls the SAME `SpawnFleeingOccupant` the RTS path
already uses, right alongside the existing rubble/dust/decal calls --
a house or shop dying now disgorges fleeing citizens exactly like an
RTS base already did. 5 new citygen-core tests cover the new method
(positive for every tier, monotonically increasing by tier); all 173
citygen-core tests pass.

**Also shipped: a recall button.** `WaypointCommander.RecallAll()`
selects every currently-alive monster and orders them to rally at the
player's own Factory (falling back to the HQ if no Factory exists yet;
a silent no-op if neither exists -- never walks the army toward an
arbitrary point). Uses the SAME `FormationHexes`/`AssignFormation`
machinery a manual multi-select order already uses, so a big recalled
army spreads out around the rally point instead of stacking onto one
hex -- exactly the class of crowding this whole steering-fix history
has been fighting, so the recall button doesn't reintroduce it. New
`RecallHud.cs` (IMGUI, matching every other HUD panel's own style)
docks a single "Recall" button directly above the minimap; wired into
`WaypointCommander`'s existing click-guard list (`Minimap.PointerOver`
et al.) so clicking it doesn't also fire a world-space order
underneath, same pattern `SelectionHud`/`BuildingNavHud` already
established.

**Verified for real:** flightcheck recompiles the whole edited/new set
(`RuntimeCityBuilder.cs`, `WaypointCommander.cs`, `RecallHud.cs`)
clean against fresh `MadDr.CityGen.dll`. citygen-core's full 173-test
suite passes (168 prior + 5 new). No Unity Editor here to confirm
either change on screen -- same standing limit as ever, but the
disgorge fix is traced to an exact, confirmed root cause (not a
guess), and the recall button reuses machinery already exercised by
every other multi-unit order path in this file.

## 2026-08 follow-up: in-game battalion control groups + Factory production, StarCraft-style build icons, Collector/harvester marker -- and an honest scope line on the Lab half

Creator direction, across several messages: "Build me a Battalion
grouping system. One where I can group select using drag highlight. Or
one where in lab but the stable area. Where can shift plus quick
select monsters and hit G key. It will then pop up with the name
requester those will show up in the game that battalion group and I
can make the factory build that battalion group of monsters. Naming of
in game battalion groups is automatic with an incremental number. We
can assign battalion groups to the number keys zero through 9 for
quick selection." Then: "Let's use menu icon system for which building
to build. Like in StarCraft." Then: "I need a quick way for the player
to recognize a monster is a collection unit in both the lab and game."

**Investigated first: does a named-battalion-template "stable" concept
already exist anywhere?** Real, but narrower than described. `site/`
(the Lab) already has a "Stable" view (`local.stable`, `PUT
/menagerie`) -- but it's ONE unnamed flat list of individual creature
IDs per account (12-cap), explicitly documented in `main.js` as "no
separate 'pick which of your saved creatures are active' UI yet." No
naming, no multiple groups, no shift-select. `packages/mutator-
service`'s `Store` interface has no "named group of genomes" entity at
all -- `Menagerie` is `{accountId, creatureIds[], updatedAt}`,
singular. A repo-wide search for "battalion" returns zero hits
anywhere, ever. Building the FULL Lab half as described -- shift-
select in the stable, G-key name-requester popup, multiple named
templates, and a real path for those templates to "show up in the
game" -- would touch four separate layers (Lab JS UI, mutator-service
store + new HTTP routes, roster-client DTOs, a new Unity-side
production UI) that don't currently share any "named group" concept at
all. That's real, substantial, cross-stack work, not a quick add-on.
**Scoped OUT of this pass, flagged rather than half-built** -- happy to
take it on as its own dedicated pass if wanted.

**What WAS built: the in-game half, which had solid existing scaffolding
(`WaypointCommander`'s live `_selected` list + marquee box-select) to
build directly on top of.**

- **Battalion control groups.** `WaypointCommander` now tracks up to 10
  battalions (one per digit 0-9). Ctrl+[0-9] binds the CURRENT selection
  to that slot, auto-named "Battalion N" off one running counter (never
  reused, never restarts per-slot) -- "naming of in game battalion
  groups is automatic with an incremental number," exactly as specced.
  **Could NOT use plain [0-9] to reselect** (the classic RTS
  convention) -- confirmed by reading `BuildMenuHud.Update()` directly
  that its own build-roster hotkeys already claim plain digit1Key
  through digit9Key, unconditionally, any time a match exists. Used
  Alt+[0-9] to select instead; both modifiers were otherwise free in
  this file. New `BattalionHud.cs` lists every defined battalion (digit,
  name, live count) docked above `RecallHud`, each row clickable to
  reselect.
- **Factory builds a battalion.** New `GrabCursor.
  BuildBattalionAtOwnFactory` clones ONE new specimen per LIVE battalion
  member (not deduped by genome -- reproduces the squad's own
  proportions, e.g. 3 Tetrapods + 2 Winged makes 3 more + 2 more, not
  just "one of each type") at whichever of the player's own Complete
  Factories sits nearest the battalion's average position. Reuses
  `CloneOnto`'s exact spend/park mechanics, generalized from "N copies
  of one held specimen" to "one copy of each member's own genome."
  Triggered via a "Build" button on each `BattalionHud` row.
- **StarCraft-style build menu.** `BuildMenuHud` rebuilt from a
  hotkey-numbered TEXT LIST to a command-card ICON GRID -- fixed-size
  colored-swatch tiles (this project has no real icon sprite art
  anywhere, so a colored square + abbreviation IS the established
  "icon" idiom, per `SelectionHud`/`BuildingNavHud`) with the hotkey
  digit badged in the corner, full name + cost now shown in one info
  line below the grid for whichever tile is currently hovered (SC2's
  own command card has the same "hover for detail" shape). Reuses
  `BuildingNavHud.IconColorFor`/`IconAbbrevFor` directly (made public)
  instead of inventing a second, possibly-drifting color mapping for
  the same building kinds.
- **Collector/harvester visual marker, Lab + game.** New `MonsterAgent.
  IsHarvester` (true when a creature's hand tool actually yields
  nonzero Blood/Bone/Brain -- not just "has a nonzero base capacity,"
  which every genome technically has even with no hand tool at all) and
  `HarvestFillFraction`. New `HarvesterMarkerHud.cs`: an ALWAYS-visible
  (not battle-gated like `HealthBars`) floating badge over every
  on-screen harvester, with a small fill-bar underneath showing tank
  level -- same IMGUI world-to-screen billboard idiom `HealthBars.cs`
  already established. Lab side: new `isHarvesterGenome(g)` in
  `main.js` (same threshold as the Unity side, via the already-vendored
  `harvestProfile` from `lib/harvest.js`) adds a 🪣 badge to both the
  main lab roster cards AND the Stable grid cards -- the two places a
  creature already shows up as a gallery item.

**Verified for real:** flightcheck recompiles the whole edited/new
Unity set (`WaypointCommander.cs`, `BattalionHud.cs`, `GrabCursor.cs`,
`BuildMenuHud.cs`, `BuildingNavHud.cs`, `MonsterAgent.cs`,
`HarvesterMarkerHud.cs`, `RuntimeCityBuilder.cs`) clean -- including two
real gaps in the local flightcheck harness's own input stub
(`leftAltKey`/`rightAltKey`/`digit0Key` were simply never needed before
this pass), fixed in that stub, not worked around. `site/main.js`
passes `node --check` (syntax-valid ES module). No Unity Editor or
browser here to confirm any of this on screen -- same standing limit as
ever, and unlike the C# side there's no automated test suite covering
the Lab's own JS at all in this repo, so the Lab-side change is
syntax-verified only, not behavior-verified.

## 2026-08 follow-up: real Factory production queue -- clones and battalion builds now stagger over time instead of popping instantly, plus a mid-implementation architecture question answered in code

Creator direction: "Factories, like in StarCraft make x number of
units. So the same happens here in the build a battalion. The monsters
line up at the cloning door of the factory and one at a time walk get
cloned. When the battalion is done, it parks itself away from factory
and assigned a name to it, adding it to the battalion list. There is a
cued icons with numbers in the lower right hand corner that specify
the number of units to make of each type that includes battalions and
individual units. When the user clicks on the factory those cued items
pop-up in the bar next to the mini map showing what's going to be made
and in one order... a small icon will float over top with a number
showing where we are in the build process. Click on the factory and
can abort all builds."

**What changed under the hood.** Both `GrabCursor.CloneOnto` (repeat-
drop cloning) and `BuildBattalionAtOwnFactory` (battalion builds) used
to spawn every clone in the same frame, in a tight loop. They now push
a `QueueItem` (`SingleUnit`: one genome + a remaining count; or
`Battalion`: a snapshot list of `(genome, radius)` pairs taken at queue
time, so a battalion's later membership changes don't retroactively
change what's mid-build) onto a shared `_queue`, and a new
`TickProduction` advances the FRONT item's timer once per frame,
spawning exactly one clone every `productionSecondsPerUnit` (4s, v0.1
placeholder like every other economy number in this repo) via the
SAME spend-Blood/`FindOpenHexNear`/`SpawnMonster`/`SetSettleTarget`
mechanics the old instant-loop used -- "the monsters line up at the
cloning door... and one at a time walk get cloned" falls straight out
of staggering real spawns over real time, no separate queueing-
animation state needed, since each clone's own existing walk-out-and-
settle behavior already reads as "leaving the factory and going to
park." A repeat drop of the same creature stacks onto the existing
queued count rather than adding a duplicate entry. When a Battalion
item finishes, `WaypointCommander.FormBattalionFromProduction` claims
the first empty slot 0-9 and names it off the same running counter as
manually-assigned battalions -- "parks itself away from factory and
assigned a name to it, adding it to the battalion list," exactly as
specced (a full slot bank of 10 just logs and drops the request rather
than silently overwriting one).

**Answered mid-implementation: "why not just have a variable in the
object saying monster belongs to battalion group id?"** Both are now
true, deliberately. `WaypointCommander` still OWNS membership (each
`Battalion`'s `Members` list is the source of truth `Battalions`/
`BattalionMembers`/`SelectBattalion` all read from) because a monster
needs to belong to at most one battalion and the commander is what
already tracks assignment identity end to end (Ctrl+digit, Alt+digit,
auto-formed-from-production). But a monster asking "which battalion,
if any, am I in" is a real, cheap, frequently-useful query (HUD
display, this same production system's own battalion-build label), so
`MonsterAgent` now also carries `int? BattalionSlot` +
`SetBattalionSlot`, mirrored by `CreateBattalion` the exact same way
`Selected`/`SetSelected` already mirrors `WaypointCommander._selected`
onto each monster for the selection-ring visual -- same established
pattern, not a new one. Reassigning a battalion slot clears the OLD
members' mirrored field first (only if it still points at this slot,
so a monster that's since moved to a different battalion isn't
clobbered), then sets it on the new members.

**The three UI pieces, and two honest scope calls on them.** New
`ProductionQueueHud.cs`, anchored to the screen's bottom-right corner
(the one corner nothing else in this project currently claims --
minimap/RecallHud/BattalionHud own bottom-left, BuildMenuHud/
BuildingNavHud own top-left/bottom-center): one tile per queued item in
build order, each with a remaining-count corner badge, the FRONT tile
showing a live fill-bar toward its next spawn, and a "Cancel All"
button wired to a new `GrabCursor.CancelAllProduction`. A second
billboard badge floats over the actual Factory (`GrabCursor.
FindAnyOwnCompleteFactory`, made public for this) showing the front
item's remaining count, using the same world-to-screen IMGUI idiom
`HealthBars`/`HarvesterMarkerHud` already established -- this part is
built exactly as described. Two pieces are folded into that same
single panel rather than built as separate, literal features, because
there is currently no click-to-select machinery for RTS/SimBuilding
buildings at all (`RuntimeCityBuilder._buildingByCollider` only
registers PROCEDURAL CityGen buildings for click detection, confirmed
by reading that registration code, not assumed -- SimBuilding entries
rendered by `BaseDresser` were never added to it): (1) "click the
factory, queue pops up next to the minimap" becomes the same
always-visible panel, since there is normally only ever one queue to
show and a second copy gated behind a building click would just be a
redundant path to the identical list; (2) "click the factory to abort
all builds" becomes the panel's own Cancel All button instead of a 3D
raycast against a building that isn't selectable yet. Flagging both
here rather than presenting them as literal reads -- a future pass
adding real RTS-building selection could revisit either.

**Verified for real:** flightcheck recompiles the full edited/new set
(`GrabCursor.cs`, `WaypointCommander.cs`, `MonsterAgent.cs`,
`ProductionQueueHud.cs`, `RuntimeCityBuilder.cs`) clean against the
real match-core/citygen-core DLLs, including catching (then fixing) a
real stale two-argument call site left over from `CloneOnto`'s
signature change (the factory lookup moved from queue-time to
production-time, since a queue can outlive the drop that started it).
No Unity Editor here to confirm the HUD panel or the walk-to-park
animation on screen -- same standing limit as every other Unity-side
change in this project's history.

## 2026-08 follow-up: the Lab "stable" half of battalion grouping, built for real -- named templates persisted server-side, four layers deep

Creator direction, following a direct question ("did you implement the
lab battalion stable system") whose honest answer was no -- an earlier
entry in this same log explicitly scoped that half OUT as "real,
substantial, cross-stack work," and the creator's reply was simply
"sure do it." This entry is that work.

**The shape, unchanged from the original scoping-out analysis:** a
named, reusable group of creature ids, built in the Lab's Stable view
via shift-click + a G-key name prompt, persisted server-side, and
fetched by the Unity client so a Factory can build that exact
composition -- the Lab-side counterpart to the in-game battalion
control groups (Ctrl/Alt+[0-9]) that shipped earlier. Four layers, all
touched:

- **`packages/mutator-service` (store + service + HTTP).** New
  `BattalionTemplate` row in `store.ts`
  (`{id, accountId, name, creatureIds[], updatedAt}`) with the usual
  `InMemoryStore` CRUD (`listBattalions`/`getBattalion`/
  `saveBattalion`/`deleteBattalion`), following the exact shape every
  other entity in this file already uses. `MutatorService` gets
  `listBattalions`/`createBattalion`/`updateBattalion`/
  `deleteBattalion`, reusing `requireOwned` + the not-retired check
  `setMenagerie` already runs -- **but deliberately allowing duplicate
  creature ids**, unlike the Menagerie's own dedup rule: "3 Tetrapods +
  2 Winged" is a real, intended composition, mirroring how the in-game
  battalion feature already permits multiple live clones sharing one
  genome id. New routes `GET/POST /battalions` and
  `PUT/DELETE /battalions/:id` (CORS's allowed-methods header gained
  DELETE, which nothing had needed until now). 7 new tests (3
  service-level, 1 HTTP-level, covering the round trip, ownership/
  retirement/size-cap/empty-name rejection, cross-account isolation,
  and the duplicate-id case specifically) -- full suite still 32/32
  (was 21 before this pass on the service side).
- **`packages/roster-client` (C# DTOs).** New `BattalionTemplateDto`
  in `GenomeDto.cs`, parsed/serialized the same field-for-field way
  `MenagerieDto` already is. Fixture captured from a REAL local
  mutator-service response (this project's own stated convention: "not
  hand-written approximations"), deliberately with a repeated
  creatureId to exercise the duplicate-allowed contract through a real
  round trip. 2 new tests, suite now 58/58 (was 56).
- **`site/` (the Lab).** The Stable view's card grid gains shift-click:
  a plain click still drives the detail panel (unchanged), a
  shift-click toggles a card into a transient, NOT-persisted
  `battalionSelection` staging set (rendered as a `--fuel`-colored
  border, distinct from the detail panel's `--acid` selection border so
  a player can tell "what I'm looking at" from "what's about to become
  a battalion" at a glance) -- the same "one click, two jobs,
  disambiguated by a modifier" shape `WaypointCommander`'s own
  shift-select already uses in-game. A `G` keydown (guarded to the
  Stable view, and to not fire while a text field has focus) prompts
  for a name and `POST`s the staged set as a new template. A new
  `#stable-battalions` panel (`renderBattalionsPanel`) lists every
  saved template with rename/delete buttons; `index.html`'s stable
  detail column was wrapped in a new `.stable-side` flex column to
  stack the two panels (`.stable-detail` lost its own
  width/position:sticky rules to the new wrapper, everything else
  unchanged). Verified via `node --check` only -- same standing "no
  automated JS suite, no browser here" limit as every prior Lab-side
  change.
- **Unity (the consumer).** `RosterFetcher` gains a second, independent
  fetch (`FetchBattalions`/`OnBattalionsReady`) alongside its existing
  Menagerie fetch -- independent deliberately, so a battalions-endpoint
  hiccup can't block spawning the player's actual creatures.
  `RuntimeCityBuilder` keeps the fetched roster around now
  (`RosterCreatures`, previously only a local variable inside the
  match-start spawn loop) plus the new `LabBattalions` list, so a
  template's bare creature ids can be resolved back into real genomes
  without a second network round-trip. New `LabBattalionHud.cs` lists
  every Lab template with a Build button, docked above the existing
  in-game `BattalionHud` -- which required giving `BattalionHud` a new
  public `StackTop` property (its own row count is dynamic, so a panel
  wanting to stack above it can't hardcode a height; this mirrors the
  exact "read a neighbour's own size" contract `BattalionHud` already
  uses to stack above `RecallHud`).

  **The one real design gap, resolved and documented, not glossed
  over:** the existing production queue's per-member park-spot search
  needs a body radius BEFORE a clone spawns, and for a battalion built
  from LIVE selected monsters (the existing feature) that radius comes
  straight off the real `MonsterAgent.Radius`. A Lab template has no
  live monster to read that from -- `MonsterAgent.Radius` itself is
  only known once the genome's mesh is actually built
  (`_body.BodyHeight * 0.55f`, confirmed by reading `MonsterAgent.cs`
  directly), and building a body just to measure it before deciding
  whether to spawn it there would be real, avoidable extra work. New
  `QueueItemKind.LabBattalion` uses a shared default radius for the
  pre-spawn search instead -- `UnitCombat`'s own existing 1.5m
  body-radius default (the same fallback `MonsterAgent.Radius` itself
  returns before a fighter exists), not a new made-up number. Worst
  case a large creature's real footprint parks a touch tighter than
  the search assumed; the search still confirms the hex is open before
  anything spawns there, so this is a cosmetic approximation, never a
  correctness bug. `GrabCursor.BuildLabBattalion(name, creatureIds[])`
  resolves each id against `RosterCreatures` and silently skips
  (logging a count, not per-id spam) any that don't resolve -- a
  creature removed from the Stable since the template was saved --
  same "don't crash on a stale reference" posture the Lab's own
  template storage already takes on its side.

**Verified for real:** `npm test` in both `genome-core` (dependency
build) and `mutator-service` (32/32, including the 7 new battalion
tests). `dotnet test` in `packages/roster-client` (58/58, including
the 2 new DTO tests, against a REAL captured fixture). `node --check`
on `site/main.js`. Flightcheck recompiles the full edited/new Unity set
(`RosterFetcher.cs`, `RuntimeCityBuilder.cs`, `GrabCursor.cs`,
`BattalionHud.cs`, `WaypointCommander.cs`, `LabBattalionHud.cs`) clean
against a FRESH `MadDr.RosterClient.dll` rebuilt from the changed
roster-client source (the stale-DLL trap this project has hit before)
-- including catching a real gap in flightcheck's own `RosterFetcher`
stub (`ProjStub.cs`), which stands in for the real networking-heavy
file and hadn't been told about the new battalions event/method until
this pass. No Unity Editor here to confirm the Lab Battalions panel or
a resolved build on screen -- same standing limit as every other
Unity-side change in this project's history.

## 2026-08 follow-up: "where is my Big Brain base and human build units" -- investigated, and a real Brain-income gap fixed

Creator report: "where is my big brain base and human build units, I
don't see them." Investigated both before touching anything.

**Human build units: a real, confirmed gap, NOT fixed this pass (out of
scope for what was asked next).** The Human Army roster (Rifleman/
Half-Track/Tank/Zeppelin Gunship, `FactionRoster.cs`) and match-core's
own `TrainUnit`/`QueueTrainCommand` machinery both exist and are
sim-tested, but grepping every Unity script turned up zero callers of
either outside `SimBridge.cs` itself -- no button, hotkey, or menu ever
issues the command. The only production UI that ever shipped is the Mad
Doctor's own G-key clone-a-monster mechanic, which is explicitly
Doctor-only (its own doc comment says so). Compounding this, the player
defaults to Mad Doctor with `showFactionPicker` off
(`RuntimeCityBuilder.cs`), so there's currently no way to even BE Human
Army in a normal match -- matching docs/17 Q13's "humans/aliens are
AI-only campaign factions at V1." Not addressed here; flagged as its
own future pass (a real build menu for a second faction, plus making
faction selection reachable).

**Big Brain: reachable, not faction-gated, but effectively invisible
because Brains -- its only cost -- had no passive income anywhere in
the simulation.** `BuildingKind.BigBrain` (`BuildingDef.cs`) is real,
undamaged, and `BuildMenuHud`/`MatchState.CanPlaceBuilding` never gate
it by faction. But it costs 20 Brains, and Brains could ONLY ever be
earned by a monster eating a citizen or a harvester physically
delivering a load -- both requiring active play with a Brain-favoring
creature. `HarvestPost`'s own doc comment already called it "the
player-BUILDABLE version of docs/20's Collection Stations" (which
convert nearby Citizen deaths into banked resources), but nothing ever
actually granted it income once built -- a real, silent gap between
what the comment claimed and what the code did. This also let me
confirm task #135 ("fix harvest crediting to bank all three lanes") was
in fact ALREADY fixed in an earlier pass -- `BankHarvestLoad` and
`CreditHarvestForEatenCitizen` both correctly credit Blood/Bones/Brains
today, verified by reading the code directly, not assumed; that task
entry was simply stale.

**The fix:** new `MatchState.GrantHarvestPostIncome`, granting 1 Brain
per owned Complete `HarvestPost` every 200 ticks (20 simulated seconds
-- `TicksPerSecond` is 10), gated on the match's own absolute Frame the
same way `GrantEmitterManaIncome` already gates mana, just a slower
interval since Brains are meant to be scarce/high-value rather than a
whole-second trickle. One Complete HarvestPost alone reaches BigBrain's
20-Brain cost in 20 grants (~6.5 simulated minutes) -- a real,
unhurried, non-combat path, not a substitute for actually harvesting.
v0.1 placeholder rate, same standing policy as every other unbalanced
economy number in this project. No Unity-side changes were needed --
`ResourceHud` already displays the Brains wallet lane and
`BuildMenuHud`'s affordability check already reads the live wallet, so
the fix is visible the moment match-core grants it.

**Verified for real:** new `EconomyTests.HarvestPost_grantsABrainsTrickle_
onlyOnceComplete_andOnlyToItsOwner` (construction-in-progress grants
nothing, the exact interval boundary triggers exactly one grant, a
second interval grants a second, and the opposing player never sees
it) -- caught and fixed a real bug in the TEST itself on first run (it
assumed the grant interval was relative to the building's own
completion tick; it's actually gated on the match's absolute Frame,
so a slow-building structure like HarvestPost can cross a grant
boundary well before an interval's worth of ticks have passed since
completion). Full `MatchCore.Tests.csproj` suite passes (279/279, up
from 278 -- the new test is the only addition). `Tools~/DetHarness`
reconfirms 10k-tick 8-player and 3k-tick 100-unit determinism unchanged
after adding a new per-tick income gate.

## 2026-08 follow-up: a delivered harvester now heads back to the patch it was working, not wherever's nearest the Factory

Creator direction: "Once the harvesting units dump their resources in
the factory they should return to where they were collecting to see
if there are any more humans."

**What was actually happening.** `MonsterAgent.AcquireTarget`'s forage
fallback already had a real "go searching for more" behavior (a prior
pass's own fix for "monsters don't go back to searching after they
deliver") -- but the search itself was always centered on `transform.
position`, i.e. wherever the unit currently stands. Right after a
delivery, that's the Factory, which is very often nowhere near the
citizens this unit had actually been eating. `NearestCitizenTo`
searches essentially the whole map already (`ForageRangeMeters` =
100000m), so the search never came up empty -- it just picked whatever
was nearest the FACTORY, which could send a harvester clear across the
map to a totally different neighbourhood from the one it had just
walked all the way back from, even if its old patch still had
citizens left in it.

**The fix.** New `MonsterAgent._lastForagePos`: a remembered world
position, set every time `OrderEat` fires (self-issued by
`AcquireTarget`'s own fallback, or player-issued via
`WaypointCommander`) to that citizen's position -- a stand-in for "the
patch this unit is working," with no real notion of citizen clusters
needed. The forage fallback's `NearestCitizenTo` call now searches
from `_lastForagePos ?? transform.position` instead of always
`transform.position`. Net effect: after banking a load, the very next
search is centered on wherever this unit was last actually eating, so
it naturally walks back to that same neighbourhood first and only
drifts to a new one once that patch is genuinely out of citizens
(`_lastForagePos` keeps updating to the latest kill, so it tracks the
unit's own current patch as it works through it, converging correctly
either way). No behavior change to the ordinary "still out foraging,
haven't returned yet" case -- current position and `_lastForagePos`
are nearly identical then, since it just walked to and ate whatever it
last targeted.

**Verified for real:** flightcheck recompiles `MonsterAgent.cs` clean
against the real match-core/citygen-core DLLs. No Unity Editor here to
watch a harvester actually walk back to its old patch on screen --
same standing limit as every other Unity-side behavior change in this
project's history; the fix is a small, targeted change to an existing,
already-tested code path (only the search's ORIGIN point changed, not
its logic), not a new untested mechanic.

## 2026-08 follow-up: give-way -- "pick one to give way to the other" implemented per the creator's own design, measured (not assumed) to help

Creator direction, a follow-up to the still-open circling report: "It
might be if they are the same speed, and they can't get around, what if
when you detect another monster near, the nav system picks one to give
way to the other, until they are body size + X distance apart, then
they can resume their normal speed to their destination."

**Discovered while investigating:** `UnitCombat` already carries
`YieldTarget`/`YieldUntil` fields, and `RuntimeCityBuilder.
SteerFollowPath` already honours them -- but that whole mechanism
belongs to `DeadlockManager` (docs/25 Phase D), which is deliberately
"rare-path only": it grants a yield ONLY after a unit has made under 1m
of net progress for a full 2.5 continuous seconds, polled periodically,
not every frame. A unit actively circling (still moving, just not
making NET progress toward its goal) can easily clear that 1m/2.5s bar
without ever registering as "stalled," so the existing yield machinery
was never actually engaging for the symptom being reported. The
creator's proposed fix is structurally a DIFFERENT, faster, everyday
mechanism -- exactly what this file's own header already flagged as
the missing piece ("dense multi-unit combat likely needs real
hysteresis/state this file's own 'Stateless' design deliberately
doesn't have yet").

**What got built: `MonsterSteeringController.GiveWaySpeedScale`/
`IsYieldingTo`**, layered into `Combine`'s existing speed-modulation
output (alongside, not replacing, the alignment-based easing already
there). Implemented the creator's own design close to verbatim:

- **"When you detect another monster near"** -- checked every single
  frame per close pair, not gated behind any stall/timer detection.
- **"Picks one to give way"** -- the exact same pairwise-stable
  `GetInstanceID` comparison `PredictiveAvoidance`'s own tie-break
  already relies on: the lower-ID unit of a pair always proceeds at
  full speed, only the higher-ID one's speed drops.
- **"Until they are body size + X distance apart, then resume normal
  speed"** -- `X` = `AvoidancePadding`, the SAME personal-space buffer
  `PredictiveAvoidance` already uses (one canonical buffer instead of a
  second invented constant), and the release is real DISTANCE
  re-checked fresh every frame, not a timer -- no stored grant/expiry
  anywhere, matching this file's "Stateless" design instead of adding a
  second `DeadlockManager`-style state machine for what should be the
  everyday, first-line case.

**Two things the design doc didn't spell out, settled by testing
against `steerverify` rather than guessed:**

1. **The trigger needs a heading-opposition gate.** A first cut
   triggered on proximity + "is the neighbour actually moving" alone
   (closer to a literal reading of "detect another monster near"). It
   compiled clean and looked reasonable, but made `steerverify`'s
   toughest scenario (two 3-unit squads passing head-on in a corridor)
   measurably WORSE -- 19 total lateral-sign flips before, 30 after.
   Root cause: squadmates marching shoulder-to-shoulder toward the
   SAME destination sit well within the trigger's own proximity range
   of EACH OTHER (2m squad spacing vs. a 4.5m default trigger radius),
   so half of every squad was being randomly throttled against its own
   packmates by `InstanceID` alone, for no reason at all -- pure
   self-inflicted chaos. Adding the same `OpposingHeadingCutoff`
   `Alignment`/`Cohesion` already use (only a neighbour whose own
   heading actually opposes this unit's intended direction counts)
   fixed it: 18 flips, a real (if modest) improvement over the 19
   baseline, and zero regression anywhere else across all 8
   `steerverify` scenarios (default AND large-radius bodies, lone
   pairs AND squads, with AND without flocking).
2. **Yielding should only throttle SPEED, not exclude the neighbour
   from steering DIRECTION.** Tried excluding a yielded-to neighbour
   from `PredictiveAvoidance`/`Alignment`/`Cohesion` entirely (the
   yielding unit just holds `fwd`, no longer actively dodging, since
   the OTHER unit is now "responsible" for routing around it) --
   plausible in theory, measurably worse in practice: a previously
   CLEAN large-radius squad scenario picked up new flips (4 -&gt; 6)
   and took 40% longer to resolve (8.4s -&gt; 12.0s). A yielding unit
   walking dead straight with zero avoidance input turned out to make
   the actual geometry HARDER for the other unit to route around, not
   easier. Reverted that half; a yielding unit keeps its normal
   avoidance/separation/flocking steering, just throttled down to as
   low as 0.15x speed (`GiveWayMinSpeedScale`, deliberately below the
   ordinary 0.35x avoidance floor -- a much stronger "just wait" signal
   than everyday easing).

**Honest result, not oversold:** this is a real, measured improvement
on top of the already-shipped `TieBreakDeadband`/`OpposingHeadingCutoff`
fixes (lone pairs, already clean, stay clean; the toughest tested
multi-squad scrum improves 19 -&gt; 18 flips with zero regression
anywhere), not a claim that circling is now fully eliminated in every
configuration -- the class header's own prior admission about dense
multi-unit combat needing real hysteresis/state stands. If the creator
still sees circling after this ships, the next diagnostic step is
pinning down the SPECIFIC geometry (converging on one shared
destination rather than crossing paths? near a building corner? three
or more units at once?), since `steerverify`'s existing scenario set is
now either clean or only marginally improved and doesn't obviously
match a "still circling" report on its own.

**Verified for real:** `steerverify` (the real `MonsterSteeringController.cs`
compiled directly, not a re-implementation) re-run across all 8
scenarios after each iteration of this fix, not just the final one --
the two reverted approaches above were caught BECAUSE of this, not
despite it. Flightcheck recompiles the real edited file clean against
match-core/citygen-core. No Unity Editor here to watch this on screen
-- same standing limit as every other Unity-side change in this
project's history.

## 2026-08 follow-up: rubble-clearing delay -- a destroyed building's own hex blocks NEW construction for 20 seconds, but movement reopens it immediately as before

Creator direction: "once a building is destroyed and after 20 seconds,
its area becomes clear and we can build on it."

**What was actually happening.** `MatchState.ApplyBuildingDamage`
already removes a Destroyed `SimBuilding`'s hex from `_blockedToGround`
the INSTANT it falls -- that's the existing, unchanged, correct
"destruction reopens the hex" behavior for MOVEMENT (docs/18). But
`CanPlaceBuilding` (the SAME shared check both `BuildGhostCursor`'s
red/green preview and the actual `BuildStructure` command use) reads
that exact same `_blockedToGround` set, so a fresh ruin was ALSO
immediately buildable-over -- zero delay, not the 20-second window
being asked for. Scoped to RTS-buildable structures (`SimBuilding`)
only, not procedural civilian buildings: those live in a completely
separate system (`RuntimeCityBuilder`/`BattlefieldState`, Unity-only,
with no coupling back into match-core's own blocked-hex model at all)
that was already, independently, never buildable-over in match-core's
view -- a real, pre-existing, unrelated gap, not something this pass
touched or claims to fix.

**The fix.** New `SimBuilding.DestroyedAtFrame` (stamped the instant
HP hits 0, via a new `frame` parameter on `ApplyDamage` -- same "pass
the caller's own Frame in" idiom `SimUnit.ApplyDamage` already uses)
plus a new `MatchState.IsRubbleStillClearing(hex)` check inside
`CanPlaceBuilding`, gated on a NEW, SEPARATE constant
`RubbleClearTicks` (20 * `TicksPerSecond` = 200 ticks -- the creator's
own number, not a placeholder). Deliberately a second, independent
gate rather than delaying the `_blockedToGround.Remove` itself:
walking through fresh rubble and dropping a brand-new building on top
of it are different questions with different answers, and conflating
them would have also broken the already-correct "rubble reopens
pathing immediately" behavior docs/18 specifically calls out. A
destroyed `SimBuilding` entity is never removed from the roster
(`Tick()`'s own doc: destruction is terminal, not deleted), so a plain
linear scan over `_buildingsInOrder` finds it reliably -- same "a
handful to dozens of bases, not hundreds" scale this file's own
`BlockedFor` doc comment already reasons a full-roster walk is fine
for.

**Free ride:** `BuildGhostCursor`'s red/green placement preview and
`SimBridge.CanPlaceBuilding` both already route through this exact
same shared `MatchState.CanPlaceBuilding` method (by design, per that
method's own doc comment: "a Unity ghost-placement cursor's red/green
preview can never disagree with what actually happens") -- so the new
rubble-clearing gate is visible in the placement preview with ZERO
additional Unity-side code. Nothing in `unity-client/` needed to
change for this pass.

**Existing tests updated, not just added:**
`ApplyBuildingDamage_destroysAtZeroHpAndReopensTheHexForRebuilding`
used to assert a SECOND structure could be built on the ruin with zero
ticks elapsed -- exactly the old (zero-delay) behavior this creator
direction asked to change, so that assertion was the thing being
fixed, not a regression to preserve. Split into
`ApplyBuildingDamage_destroysAtZeroHp` (still-correct baseline: HP/
State transition, unaffected) and a new
`ApplyBuildingDamage_blocksRebuildingUntilRubbleClearTicksPass_thenReopensTheHex`
(rebuild attempt right after destruction is a silent no-op; one tick
short of `RubbleClearTicks` still blocked; the exact tick it crosses,
`CanPlaceBuilding` flips true and a real `BuildStructure` command
lands).

**Verified for real:** full `MatchCore.Tests.csproj` suite passes
(280/280, up from 279 -- net +1 after replacing one test with two).
`Tools~/DetHarness` reconfirms 10k-tick 8-player and 3k-tick 100-unit
determinism unchanged after adding `DestroyedAtFrame` to
`SimBuilding.WriteTo`'s canonical hash (a real, deliberate addition --
this new field affects future sim behavior, so it belongs in the
hash, same "every field that matters gets hashed" law every other
entity in this file already follows). Flightcheck recompiles the real
edited match-core against a freshly rebuilt DLL, confirming
`BuildGhostCursor`/`SimBridge` still compile clean against the changed
`CanPlaceBuilding` (unchanged signature, so this was never really in
doubt, but checked anyway).

## 2026-08 follow-up: FIX -- holding Ctrl/Alt to hit a battalion hotkey was ALSO toggling a build order on the same keypress

Creator direction: "you need to disable the build orders when the
control key is pressed."

**Root cause.** `BuildMenuHud.Update()` claims plain `digit1Key`
through `digit9Key` unconditionally, whenever a match exists, to
toggle which building kind is selected to place -- this predates the
battalion system entirely. When the in-game battalion hotkeys
(`WaypointCommander`'s Ctrl+[0-9] assign / Alt+[0-9] select) were
added on top of the SAME digit keys, `BuildMenuHud`'s own key-read
never learned to check for either modifier -- it has no concept of
Ctrl/Alt at all, just "was this digit pressed this frame," so holding
Ctrl to bind battalion slot 3 also fired `ToggleSelect` for whichever
building kind hotkey 3 maps to, on the exact same keypress. Two
unrelated systems both listening to the raw digit key, only one of
them aware the key was overloaded.

**Fix.** `BuildMenuHud.Update()` now bails out immediately, before
touching any digit key at all, whenever either Ctrl or Alt is held
(same `leftCtrlKey.isPressed || rightCtrlKey.isPressed` /
`leftAltKey`/`rightAltKey` check `WaypointCommander`'s own battalion
hotkey handler already uses) -- both modifier combinations belong to
the battalion system now, so the build menu simply steps aside while
either is down, plain digits untouched.

**Verified for real:** flightcheck recompiles `BuildMenuHud.cs` clean
against the real match-core/citygen-core DLLs. No Unity Editor here to
confirm holding Ctrl/Alt no longer double-fires a build toggle on
screen -- same standing limit as every other Unity-side change in this
project's history; the fix itself is a single early-return guard on
already-read keyboard state, not new untested logic.

## 2026-08 follow-up: the Lab Stable shift-click bug was really a deploy gap, plus a real UI pass -- green highlight, Ctrl/Cmd-click, a Battalion+ toggle button, a pre-filled name prompt, and bottom-of-screen tutorial text

Creator report: "Lab -> Stable -> shift+click is not working. it
should green hi-lite the monster(s). Bug is click select deselects
previous, with out without shift. Also should have some tutorial text
on bottom of the screen. Make a multi-select toggle button, for mobile
and normal web call it build Battalion+ and a done button will allow
you to name it. but pre-filled with a valid entry. Airborn 1 for
example. Crab 2 etc." Follow-ups: "on Website keep shift and control
clicks as well" and "verify it works in all major browsers, PC, MAC
and Linux."

**The reported bug's real root cause: nothing was ever deployed.**
The entire Lab battalion-template system (shift-click staging, the
Battalions panel, `POST /battalions`) shipped on this session's own
branch across two earlier passes -- but `main` (what GitHub Pages
actually serves) was never fast-forwarded to include it. Checked
directly: `git merge-base HEAD origin/main` equaled `origin/main`'s
own HEAD, meaning the live site had been running code from BEFORE any
of this work, whose Stable click handler was `() => { local.selectedId
= ...}` with no shift-key check of any kind -- explaining the exact
symptom reported ("deselects previous, with or without shift": shift
was simply never read, so every click behaved identically). This pass
fast-forward-merges the session branch into `main` (CLAUDE.md's own
standing workflow: "session branches merge into main promptly...
Merging to main = publishing"), the real fix for the bug as reported,
independent of everything else below.

**The UI pass, on top of that.**
- **Green highlight, always.** `.battalion-selected` used to reuse
  `--fuel` (amber); now a new, FIXED `--battalion-pick` (#33d17a)
  defined once in the base `:root` and deliberately never redefined by
  the Army/Hive faction-skin overrides (unlike `--acid`, which flips to
  amber/violet under those skins) -- green every time, matching the
  literal ask, not just under the default faction. A small ✓ badge
  overlays the corner too, so a picked card reads clearly even next to
  `.selected`'s own (still faction-tinted) detail-view border.
- **Ctrl-click and Cmd-click, alongside shift-click** (creator
  follow-up: "keep shift and control clicks as well"). `e.ctrlKey ||
  e.metaKey` added next to the existing `e.shiftKey` check --
  `metaKey` is the DOM's own name for whichever key a given OS calls
  its primary modifier (Cmd on macOS, the Windows key on Windows), so
  checking both `ctrlKey` and `metaKey` covers Windows/Linux (Ctrl) and
  macOS (Cmd) with the SAME code path, nothing OS-specific to branch
  on.
- **A `Battalion+` toggle button**, for touch devices with no modifier
  key at all: while active, a PLAIN tap on a stable-card battalion-
  selects it instead of driving the detail panel; shift/ctrl/cmd-click
  keep working identically regardless of this button's state, it's
  purely additive.
- **`Done` button**, next to `Battalion+`, doing exactly what the `G`
  hotkey already did (disabled until something's picked) -- for anyone
  who hasn't found the keyboard shortcut, or is on a device with no
  keyboard.
- **The name prompt is now pre-filled**, not blank -- new
  `suggestBattalionName`: the DOMINANT body plan among the staged
  picks (genome-core's real 9-plan `BODY_PLANS` set) names the
  suggestion, mapped through a one-word display name (`winged` ->
  "Airborn", `crab` -> "Crab" -- the creator's own two examples,
  matched directly), then the lowest N not already used by an existing
  battalion name (`Crab 1`, `Crab 2`, ... never colliding). Still just
  `prompt(message, defaultValue)` -- the browser's own native
  pre-filled-prompt parameter, no custom modal needed.
- **A persistent tutorial strip** at the bottom of the Stable screen
  (`.stable-tutorial`, `order: 99` in the flex layout so it always
  lands last regardless of DOM position) explaining all of the above in
  one line -- separate from the existing `.battalion-hint` inside the
  Battalions panel (which changes text with selection state); this one
  never changes, for a player who hasn't found that panel yet.

**Verified for real, not just `node --check` this time:** a genuine
Playwright-driven Chromium session, loaded against the ACTUAL
`site/index.html`/`main.js`/`style.css` served locally, talking to a
REAL local `mutator-service` instance (network-intercepted so the
site's own hardcoded deployed-service URL never needed touching) --
spawned and stabled 4 real creatures, then drove the exact reported
bug scenario: shift-click card 1, ctrl-click card 2 (asserted card 1
was STILL green -- the literal "deselects previous" bug, now proven
NOT to reproduce), meta/cmd-click card 0 (three at once), turned on
`Battalion+` and plain-clicked card 3 (all four at once, no modifier
key touched at all), read the computed CSS `border-color` and
confirmed it renders as literal `rgb(51, 209, 122)` (`#33d17a`, real
green) not just "has the class," clicked `Done`, captured the native
`prompt()`'s own `defaultValue` and confirmed it matched
`suggestBattalionName`'s real output ("Tetrapod 1" for an all-tetrapod
test roster) rather than blank, accepted it, and confirmed the saved
battalion appeared in the panel under that exact name with the
selection cleared. All 15 assertions passed. Honest limit: this sandbox
only has Chromium available (`playwright install` is explicitly
disallowed here per this environment's own setup) -- Firefox/WebKit
were NOT independently driven. The code itself uses nothing
browser-specific (`shiftKey`/`ctrlKey`/`metaKey` are baseline DOM
`UIEvent` properties, CSS custom properties and flexbox `order` are
universally supported), so there's no known reason it would behave
differently there, but that's an engineering argument, not the same
kind of proof the Chromium run provides.

## 2026-08 follow-up: FIX -- the Lab's battalion hotkey moves off G, onto B ("G key is grab use B key for Battalion")

Creator direction: "G key is grab use B key for Battalion."

**Root cause.** `G` is already the in-game grab-mode hotkey
(`GrabCursor.cs`, unchanged, unrelated to this file) -- the Lab's own
Stable-view "name and save the staged battalion" shortcut had also
landed on `G` (a separate, independent choice made when that feature
was first built, since the Lab and the game are two different
applications with no shared keymap to collide against at the time).
Once both existed side by side, `G` meant two different things
depending on which screen you were looking at -- exactly the kind of
mnemonic collision the earlier `Ctrl`/`Alt`-for-battalion decision in
the IN-game half of this same system was made specifically to avoid
(see this log's own entry on that).

**Fix.** The Lab's `keydown` listener now checks `e.key === "b" ||
"B"` instead of `"g"`/`"G"` -- `G` no longer does anything in the
Stable view. Every place the shortcut is surfaced to the player
(`.battalion-hint`'s live text, the bottom-of-screen tutorial strip)
updated to say `B` instead.

**Verified for real:** a Playwright-driven Chromium session (same
harness as the previous entry) staged a battalion selection, pressed
`g` and confirmed NO save prompt opened, then pressed `b` and
confirmed one did. Both assertions passed. Same Chromium-only honest
limit as the previous entry -- the rebind itself is a two-character
diff plus matching text, not a new mechanism, so the risk profile here
is low regardless.

## 2026-08 follow-up: FIX -- attack fire VFX was never wired to the buildings monsters actually damage, plus a real "start with 1, grow with size, up to 8" fire cluster + glow/sway upgrade

Creator report: "what happen to my low poly fire for when buildings
were under attack." Follow-ups: "it should start with 1 but then
others popup in different places based on the building size up to 8"
and "glowing and fire like movement."

**Root cause, found by reading the actual code, not assumed.**
`DamageFx.AttachFire`/`FirePlume` were real and already shipped (task
#117's own epic) -- but this codebase has TWO entirely separate
building-damage systems (an established fact from many earlier
entries in this log), and fire was wired to only one of them:
`BaseDresser.cs` (the RTS `SimBuilding` roster -- HQ/Factory/storage)
calls `AttachFire` the instant `b.IsDamaged` flips true. But
`b.IsDamaged` can only become true via `MatchState.ApplyAttackBuilding`,
reachable only through `SimBridge.QueueAttackBuildingCommand` --
which is **never called from anywhere in the Unity client**. This
codebase's own decision log already says so, at the time it was
built: *"nothing calls it yet -- no UI path exists to actually ISSUE
an attack-building order... flagged as a real, separate gap."* RTS
buildings can't actually BE damaged in play today, so fire (and even
`IsDamaged`'s darken-tint) never had anywhere to trigger.

The building system monsters DO actually damage -- procedural
CityGen buildings (houses/shops/landmarks, confirmed by reading
`MonsterAgent.TickAttack`, which targets a `Building` and calls
`RuntimeCityBuilder.ApplyBuildingDamage`, never touching `SimBuilding`
at all) -- had its own `Intact -> Damaged` branch
(`RuntimeCityBuilder.cs`) call `DamageFx.AttachSmoke` only. `AttachFire`
was never called from there. Not a regression from any later pass
(the HP bumps, the RTS-footprint pathfinding fix, the rubble-clearing
delay all leave this code untouched) -- a scope gap that was there
from the day the epic shipped: fire was built for the building system
players can't actually damage, and never reached the one they can.

**The fix, and the two follow-up asks built in from the start rather
than as an afterthought.**

- New `MadDr.CityGen.BuildingStats.FireCount(BuildingTier)` (citygen-
  core, alongside the existing `Occupants`/`StructureHp`/`Armor`
  tier tables): Small=1, Medium=3, Large=5, Landmark=8 -- the creator's
  own numbers ("start with 1... up to 8"), monotonically scaling with
  size like every other tier table in this file.
- New `DamageFx.AttachFireCluster(holder, height, footprintRadius,
  targetCount)`: the FIRST fire point lands the instant a building
  crosses into Damaged (so it's never sitting Damaged with zero fire
  showing -- "should start with 1"), then a new `FireCluster`
  component stages in one MORE fire point every 2-5 seconds (randomized,
  not metronomic) at a NEW scattered spot on the footprint, until
  reaching `targetCount`. Wired into BOTH building systems: procedural
  (`RuntimeCityBuilder.cs`'s Damaged branch, the actual fix for the
  reported bug, using `BuildingStats.FireCount(building.Tier)` and a
  radius derived from the building's own real footprint hex count) and
  RTS (`BaseDresser.cs`, replacing its old single-point `AttachFire`
  call, using a new local `FireCountFor(def)` mirroring the SAME
  Small/Medium/Large/Landmark numbers off `BuildingDef.MaxHp` -- the
  same "duplicate the tier boundary constants, not a cross-package
  type" precedent `FullScaleFor` in that same file already set, since
  an RTS `BuildingDef` has no citygen `BuildingTier` of its own to hand
  the shared table directly).
- **"Glowing"**: `FirePlume` now adds a real flickering `Light`
  component (warm orange, `LightShadows.None` -- a purely cosmetic
  beat across a whole burning skyline isn't worth real-time shadow
  cost) with two mismatched sine frequencies beating against each
  other for an irregular flicker, not a single clean pulse. Previously
  fire only self-lit its own puff meshes via emissive material color --
  it never actually cast light onto the building or ground around it,
  which is what "glowing" reads as from a real distance.
- **"Fire like movement"**: new `SmokePuff.InitFlame` -- every other
  puff kind in this file (smoke, dust, water) keeps its original
  dead-straight drift, completely unchanged; a flame puff now RISES
  faster and sways side to side on a sine curve whose amplitude grows
  with the puff's own age (a flame licks wider the higher it climbs,
  not a fixed wobble from the moment it's born), instead of traveling
  in a straight line the way every puff in this file always has.

**Verified for real:** `dotnet test` on `packages/citygen-core`
(180/180, up from 176 -- 4 new `FireCount` tests: positive for every
tier, exactly 1 for Small, exactly 8 for Landmark, monotonic scaling).
Flightcheck recompiles the full edited set (`DamageFx.cs`,
`BaseDresser.cs`, `RuntimeCityBuilder.cs`) clean against a freshly
rebuilt `MadDr.CityGen.dll`, including the new `Light`/`LightType`/
`LightShadows` usage against the local stub's own existing (already-
complete) `Light` type. No Unity Editor here to watch multiple fires
stagger in and sway/glow on screen -- same standing limit as every
other Unity-side visual change in this project's history.

## 2026-08 follow-up: FIX -- smoke was already correctly wired, but too small to see against a real building

Creator report: "I've never seen the smoke either" -- arriving right
after the fire fix above shipped.

**Root cause, found by reading the actual code before assuming it was
the same bug as fire.** Unlike `AttachFire`, `DamageFx.AttachSmoke`
was NOT a wiring gap: `RuntimeCityBuilder.cs`'s `Intact -> Damaged`
branch was already calling it, unconditionally, before any change in
this session -- confirmed by reading that method's code as it stood
prior to touching anything. So the bug had to be something else.

The actual cause: a `SmokePlume` puff was a single fixed-size sphere,
starting at scale 0.8 and growing to about 3.0 over a 2.2s life, dim
medium-gray (0.35/0.34/0.32) at 0.75 alpha fading to 0 -- sized and
colored for nothing bigger than a Small 6m house. A `Landmark` tops
out at 40m tall with an 18m+-wide massing footprint per hex, plus a
rooftop kit of water towers/antenna masts/billboards from
`BuildingDresser`; a ~3-unit, low-opacity, medium-gray puff spawned
near that roofline is genuinely hard to pick out from typical RTS
camera height and distance, especially once it's most of the way
through its own fade. Technically running, practically invisible --
same category of problem fire had, but the fix fire needed (rewire to
the reachable building system) doesn't apply here; this one needed
scale, contrast, and hang-time instead.

**The fix.**

- New `MadDr.CityGen.BuildingStats.SmokeScale(BuildingTier)` (citygen-
  core, alongside `FireCount`/`Occupants`/`StructureHp`/`Armor`):
  Small=1.0 (renders identically to before this fix), Medium=1.5,
  Large=2.2, Landmark=3.0 -- same "small flat number scaled loosely by
  tier" placeholder policy as the rest of that table.
- `DamageFx.AttachSmoke` now takes a `scale` parameter, threaded
  through a new `SmokePlume.Init(scale)` into puff sizing. New
  `SmokePuff.InitPlume` (distinct from `InitBurst`, which flattens the
  vertical drift rate for a quick one-shot burst -- reusing it would
  have undone the puff's own lazy climb) keeps `Init`'s original rise
  speed while overriding life/growth/alpha: puffs now start at
  `1.1 * scale`, grow by `3.6 * scale` over a longer 3.2s life (was
  2.2s), colored dark sooty gray (0.16/0.15/0.14, up from
  0.35/0.34/0.32 -- real contrast against a building's own
  concrete/roof palette instead of blending into it) at 0.88 alpha (was
  0.75). Spawn height raised slightly (`height * 1.05`, was `* 0.9`) so
  the plume clears the roofline before it starts fading instead of
  spawning inside the same clutter it needs to read against.
- Wired into BOTH building systems, same split as the fire fix:
  `RuntimeCityBuilder.cs`'s Damaged branch passes
  `BuildingStats.SmokeScale(building.Tier)` directly; `BaseDresser.cs`
  gets a new local `SmokeScaleFor(def)` mirroring the SAME
  Small/Medium/Large/Landmark boundaries off `BuildingDef.MaxHp`, same
  "duplicate the tier boundary constants, not a cross-package type"
  precedent `FullScaleFor`/`FireCountFor` in that file already set.

**Verified for real:** `dotnet test` on `packages/citygen-core`
(186/186, up from 180 -- 3 new `SmokeScale` tests: positive for every
tier, exactly 1.0 for Small, monotonic scaling). Flightcheck recompiles
the full edited set (`DamageFx.cs`, `BaseDresser.cs`,
`RuntimeCityBuilder.cs`) clean against a freshly rebuilt
`MadDr.CityGen.dll`. No Unity Editor here to confirm the plume actually
reads as visible smoke on screen -- same standing limit as every other
Unity-side visual change in this project's history; this fix is a
best-effort scale/contrast pass based on the building-size math, not a
guarantee it's now unmissable at every zoom level.

## 2026-08 follow-up: FIX -- fire was too large, plus a real low-poly faceted shard replacing the round sphere puffs

Creator report: "the fire is too large. it should look like [reference:
small, angular, faceted low-poly flame art]" -- two reference images,
both small stylized geometric flame shards, not round glowing blobs.

**What was actually wrong.** The multi-point cluster + glow fix (the
entry above this one) made fire visible and correctly wired, but never
addressed size or shape -- it inherited the original `AttachFire`'s
puff geometry unchanged: a `PrimitiveType.Sphere`, which is smooth and
round no matter how small it's scaled, plus a 6m-range/2.5-intensity
point `Light` throwing a glow bigger than the flame it was meant to
light. A shrunk sphere is still a shrunk sphere -- it was never going
to read as "low-poly" the way the reference images do, so this needed
an actual shape change, not just smaller numbers on the existing one.

**The fix.**

- New `ProceduralMeshKit.FlameShard(segments, seed)`: a small jagged
  shard mesh, hand-authored the same way that file's existing
  `Frustum`/`Wedge` already are (explicit vertex/triangle lists,
  `FaceOutward` winding fix-up, `RecalculateNormals`) -- an irregular
  low-poly base ring (per-vertex radius jitter, deterministic off
  `seed` via a GLSL-style sine hash) tapering to an off-center apex,
  where the off-center bend is what reads as a "licking" flame lean
  instead of a symmetric party-hat cone. `FirePlume.SpawnPuff` now
  builds a `MeshFilter`/`MeshRenderer` GameObject from this instead of
  `GameObject.CreatePrimitive(PrimitiveType.Sphere)`.
- Puff footprint shrunk to roughly a third of the old sphere's size
  (spawn scale 0.28, vs. the old sphere's 0.55 with the shared 0.8
  puff-growth floor every puff kind used to share).
- New `SmokePuff._baseScale` field: every existing puff kind (smoke,
  dust, water, muzzle) keeps the original hardcoded 0.8 floor exactly
  as before via their existing `Init`/`InitBurst`/`InitPlume`/`InitJet`
  calls -- only `InitFlame` now takes an explicit `baseScale` argument
  (0.32) and overrides it. This is deliberately NOT a shared global
  shrink: reusing the old 0.8 constant across all puff kinds would have
  also shrunk the smoke-visibility fix that shipped immediately before
  this one, undoing it as a side effect.
- `FirePlume`'s point `Light` shrunk to match: range 6->3, base
  intensity 2.5->1.1 (flicker peak scaled down to match), so the glow
  reads as a small contained flame's light instead of a bonfire's.

**Verified for real.** A standalone console harness
(`flameshard-verify`, same UnityStub-backed pattern as every other
flightcheck verify folder) instantiates `FlameShard` across 20 different
seeds and checks: no zero-length (cancelled) normals -- the exact
double-winding regression `ProceduralMeshKit`'s own header comment
warns about, and the actual mechanism this codebase caught it by before
-- all vertices finite and within a sane bound (catches an
exploding/NaN mesh), and the topology matches what a 5-segment shard
should produce exactly (10 triangles, 7 vertices: 5 ring + base-center
+ apex). All 20 seeds x 5 checks passed. Flightcheck recompiles the
full edited set (`DamageFx.cs`, `ProceduralMeshKit.cs`) clean. No Unity
Editor here to confirm the shard actually reads as "low-poly" the way
the reference images do on a real screen -- same standing limit as
every other Unity-side visual change in this project's history.

## 2026-08 follow-up: resize fire + smoke 70%

Creator direction: "resize it 70% and the smoke as well." A flat 0.7
multiplier layered on top of both effects' existing geometry -- puff
spawn scale, `InitFlame`'s growth/base-scale args, and `FirePlume`'s
`Light` range/intensity -- NOT on `BuildingStats.SmokeScale`/`FireCount`
themselves, so the per-tier scaling ratios (Small vs. Landmark) are
unchanged; this is a flat trim of the whole effect's size, not a
retune of how much bigger a Landmark's fire/smoke is than a Small
building's. Flightcheck recompiles `DamageFx.cs` clean.

## 2026-08 follow-up: fire attached to the roofline + shrunk further; smoke gets a real start-small-grow-big arc

Creator direction: "the fire should come from the building be attached
to the roof, or the windows, and a lot smaller. The smoke should start
small and float upward getting bigger and dissipating."

**Fire placement.** `FireCluster.SpawnOne`'s old placement put every
fire point at a quarter of the way up the building's own height, with
the very FIRST point forced to `dist = 0` -- dead center of the
footprint, floating in open air disconnected from any wall or roof
surface. Windows themselves aren't addressable from `DamageFx` today
(`BuildingDresser.SpawnWindowStrip` spawns window-band geometry
per-floor during dressing but never returns/exposes those world
positions anywhere `RuntimeCityBuilder.ApplyBuildingDamage` or
`BaseDresser` could look them up later -- wiring that up would mean
threading a window-socket list out of the dresser and through both
damage-effect call sites, a real separate piece of plumbing). Given the
creator's own phrasing offered roof OR windows as alternatives, this
pass moved fire to the roofline instead: height changed from `_height *
0.25f` to `_height * 0.92f`, and EVERY point (the first one included,
not just the later staggered-in ones) now lands 30-90% of the way out
toward the footprint's own edge rather than at dead center -- so fire
reads as erupting from the roof's own surface/edge instead of hanging
in the open air above the building's interior.

**Fire size.** Two more flat multipliers stacked on top of the
existing 0.7x resize: puff mesh spawn scale, `InitFlame`'s growth/
base-scale, and the `Light`'s range/intensity/flicker-peak all cut by
another 0.5x. Fire is now roughly a sixth the size of the original
sphere-puff version (0.7 x 0.5 on top of the shard-mesh trim that had
already cut the sphere's own footprint by about two-thirds).

**Smoke's growth arc.** The smoke-visibility fix (two entries back)
already had a puff GROW over its life (`_baseScale + t * _growth`), but
it started AT `_baseScale` the instant it spawned -- no visible small-
to-big ramp, just an immediate pop-in followed by modest further
growth. New `SmokePuff._startScaleFraction` field (default 1.0, a
complete no-op for every other puff kind -- fire/dust/water/muzzle all
keep spawning at their own existing full base size, unchanged) lets a
puff kind override its OWN starting fraction of `_baseScale`; `InitPlume`
(smoke's own init, and only smoke's) sets it to 0.2. The `Update()` scale
formula changed from `_baseScale + t * _growth` to `_baseScale *
Lerp(_startScaleFraction, 1, t) + t * _growth` -- for the unchanged
default (`_startScaleFraction = 1`), `Lerp(1, 1, t)` is always 1, so
this reduces to the EXACT original formula bit-for-bit for every puff
kind except smoke. Smoke now visibly starts at 20% of its base size and
grows into its full (already-tuned) end size across its 3.2s life while
rising and fading -- the small-to-big-to-gone arc the creator described,
instead of an instant pop to near-full-size.

**Verified for real:** flightcheck recompiles `DamageFx.cs` clean
against the full edited set. The `Lerp` reduction to the pre-existing
formula for `_startScaleFraction = 1` was checked by hand (`Mathf.Lerp(1,
1, t) == 1` for all `t`, so `scale = _baseScale * 1 + t * _growth =
_baseScale + t * _growth`, identical to every prior puff kind's
behavior) rather than assumed. No Unity Editor here to confirm the
roofline placement or the growth arc read correctly on a real screen --
same standing limit as every other Unity-side visual change in this
project's history.

## 2026-08 follow-up: smoke gets its own faceted shape, a lighter color, a coherent wind lean, and a size bump

Creator direction: a reference image ("use this for scale and shape
reference for fire and smoke, and position on buildings"), followed by
three clarifying questions once the sandbox's network policy turned
out to block the image host (`encrypted-tbn0.gstatic.com` -- same
class of external-domain 403 this log has hit before with
`maddr-mutator.onrender.com` and `brainpuddler.github.io`; confirmed
via the proxy's own `/__agentproxy/status` endpoint rather than
guessed). Creator's answers: shape "1, angular"; color/drift "yes"
(lighten + diagonal lean); scale "yes" (scale up).

**Shape.** New `ProceduralMeshKit.CloudShard(segments, seed)`: a
jittered, twisted low-poly barrel -- an irregular wider bottom ring and
a narrower top ring, the top ring rotated by a random twist so the side
facets read as angular quads rather than a smooth cylinder, capped top
and bottom. Deliberately a DIFFERENT shape family from `FlameShard`
(which tapers to a single off-center apex) rather than reusing it at a
different scale -- a smoke puff is rounder/chunkier than a flame lick,
so a taper-to-a-point cone would have read as another (bigger, grayer)
flame rather than a cloud chunk. `SmokePlume.SpawnPuff` builds this via
`MeshFilter`/`MeshRenderer` the same way `FirePlume.SpawnPuff` already
does for `FlameShard`, replacing the `CreatePrimitive(Sphere)` call.

**Color.** Lightened from the near-black sooty gray
(0.16/0.15/0.14) the smoke-visibility fix (several entries back) chose
specifically for contrast against the building palette, to a cool pale
gray (0.68/0.7/0.74). That earlier reasoning doesn't automatically
still apply here: this same pass ALSO changes shape (angular facets
catch light differently than a smooth sphere) and scale (bigger reads
farther regardless of color), so a return to a more traditional pale
smoke color no longer risks the original "blends into the roofline"
failure mode on its own.

**Diagonal lean.** New `SmokePlume._lean` (a `Vector2`, computed ONCE
in `Awake` off the plume's own `GetInstanceID` -- i.e. once per
building, not once per puff) threaded into a new `SmokePuff.InitPlume`
overload that takes a `lean` parameter and adds it to the puff's own
existing small per-puff horizontal wobble. Every puff a given
`SmokePlume` ever spawns shares the exact same `lean` value, so the
whole column reads as one coherent wind-blown trail leaning a
consistent direction, instead of the previous per-puff independent
random wobble (which never accumulated into a visible "lean" no matter
how long you watched it, since each puff's own small sideways
component pointed a different random way).

**Scale.** A new `ScaleUpPct = 1.6f` multiplier layered ON TOP of the
existing `ResizePct = 0.7f` resize (not a replacement for it) -- net
effect is a plume noticeably bigger than the pre-resize original,
scaled up specifically so it reads as bigger than the fire burning
beneath it, per the reference.

**Verified for real.** A standalone `cloudshard-verify` harness (same
UnityStub-backed pattern as `flameshard-verify`) instantiates
`CloudShard` across 20 seeds and checks: no zero-length (cancelled)
normals (the same double-winding regression class this file's own
header warns about), all vertices finite and within a sane bound, and
exact topology for a 6-segment shard (24 triangles/72 indices, 14
vertices: 12 ring + 2 caps). All 20 seeds x 5 checks passed.
Flightcheck recompiles `DamageFx.cs`/`ProceduralMeshKit.cs` clean
(one real compile error caught and fixed along the way: the local
`UnityStub.cs` `Vector2` doesn't define an `operator*(Vector2, float)`
the way real Unity's does, so the lean-vector construction was
rewritten to multiply each component directly instead of relying on
that operator -- avoids depending on an operator the stub doesn't
have, rather than patching the stub itself). No Unity Editor here to
confirm the shape/color/lean/scale actually read the way the reference
image does on a real screen -- same standing limit as every other
Unity-side visual change in this project's history.

## 2026-08 follow-up: smoke was hiding the fire, and FX now fires on the first hit instead of the Damaged threshold

Creator reports, same session: "the smoke is way too big and I can not
see the fire, make sure it is on the outside of the building" and,
separately, "as soon as a building is in combat we need to see the
smoke and fire."

**Size.** The immediately-prior entry's `ScaleUpPct = 1.6f` size-up is
GONE -- `SmokePuff._baseScale`/growth are back to exactly the
`ResizePct = 0.7f`-only baseline (`1.1f * ResizePct * _scale` /
`3.6f * ResizePct * _scale`). That baseline was the last
creator-unchallenged size; the 1.6x layered on top of it is what made
the plume big enough to visually swallow the fire cluster it rises
from.

**Position ("outside the building").** `DamageFx.AttachSmoke` gained a
`footprintRadius` parameter (the caller already computes this for
`AttachFireCluster` -- both call sites now share one value instead of
computing it twice). The plume's origin is no longer dead-center above
the roof (directly over where `AttachFireCluster` scatters its points);
it's offset outward past the footprint's own edge, `footprintRadius *
1.2f`, in a deterministic per-building angle (hashed off the holder's
own `GetInstanceID`, same "cosmetic jitter, no gameplay meaning"
precedent every other per-building visual variety in this codebase
already uses). `SmokePlume.Init` now takes that SAME `outwardAngle`
directly (rather than deriving its own separate angle from its own
`GetInstanceID` the way the immediately-prior entry did) so the wind
lean keeps drifting further in the direction the plume already started
in, instead of potentially wandering back over the roof it just moved
away from.

**Trigger timing ("as soon as a building is in combat").** Both fire/
smoke call sites (`RuntimeCityBuilder.ApplyBuildingDamage` for
procedural buildings, `BaseDresser.Dress` for the RTS roster) used to
gate the attach behind the SAME `Damaged` stage crossing (<=50% HP,
docs/18 SS3) their own material-darkening logic uses. That threshold
was fine when Structure HP was small, but the recently-bumped high-HP
tiers (1000-10000, several entries back) could now sit in combat a
long time with zero fire/smoke feedback before crossing 50%. Changed
to fire on the very FIRST hit instead:
- `RuntimeCityBuilder.ApplyBuildingDamage`: `current.CurrentHp ==
  current.MaxHp` -- true only on a building's first-ever
  `ApplyDamage` call (HP only decreases, no repair path), so this
  still fires exactly once. Restructured the method's `if/else if` into
  `if (Destroyed) {...} else { if (Damaged-crossing) {darken} if
  (first-hit) {attach FX} }` -- the darkening and the FX attach are now
  two independent conditions in the same branch, not one combined
  gate; a building can show fire/smoke while still Intact-tinted.
- `BaseDresser.Dress`: was `b.IsDamaged` (<=50% HP); now
  `b.State == BuildingState.Complete && b.Hp < b.MaxHp`, computed
  locally in Unity code (not a new match-core field -- both `Hp`/`MaxHp`
  are already public on `SimBuilding`, so this needed no sim-side
  change, no determinism/hash impact, and no new combat math, matching
  this being a purely visual gating decision). `_damagedHandled` still
  guards it to fire exactly once, same reasoning as before (HP never
  regresses). `TintShape`'s own Damaged-tier darkening is UNCHANGED,
  still keyed on the real `b.IsDamaged` (50%) threshold -- deliberately
  kept as its own separate signal from the combat-FX trigger now.

**Verified.** Flightcheck recompiles `DamageFx.cs`,
`RuntimeCityBuilder.cs`, and `BaseDresser.cs` clean (confirmed all
three are in the flightcheck harness's own compile list, not assumed).
No sim-side (`match-core`/`citygen-core`) files touched by this
entry's changes, so no determinism/golden-hash risk and no C# test
suite re-run needed for those packages. No Unity Editor here to
confirm the new position/timing read correctly on a real screen -- same
standing limit as every other Unity-side visual change in this
project's history.

## 2026-08 follow-up: smoke shrunk to 0.2, a smooth fade curve, and Inspector knobs for both effects

Creator direction, same session: "smoke way way smaller. 0.2 resize.
and always smooth fading out of upper large chunks of smoke." Then,
separately: "add inspector for smoke size and and fire size."

**0.2 resize.** `SmokePlume.SpawnPuff`'s resize constant dropped from
0.7 straight to 0.2 -- a further cut on top of (not a reversal of) the
immediately-prior entry's fix.

**Smooth fade.** `SmokePuff` gained a smoke-only `_easeFade` bool
(false/linear, completely unchanged, for every other puff kind --
fire/dust/water/muzzle). When true, `Update`'s alpha fade swaps from a
constant-rate linear ramp (`1f - t`) to a smoothstep ease (`t*t*(3-2t)`
applied to alpha's fade-in-progress, i.e. `1f - smoothstep(t)`): alpha
barely drops during the first stretch of a puff's life, falls through
the middle, then eases toward zero rather than declining at the exact
same rate the whole way. The practical effect is on the "upper large
chunks" specifically -- the biggest, oldest puffs in the column (the
ones nearest the end of their own life) are visibly present longer and
then dissolve gradually, instead of already being close to fully
transparent well before they're at their biggest size the way a
constant linear rate works out to.

**Inspector knobs.** New `DamageFxProfile.cs` -- a `ScriptableObject`
following the EXACT pattern `CityLightingProfile` already established
for lighting (`[CreateAssetMenu]`, `[Range]`-attributed public fields,
a lazy `Default` fallback, an `Active` static holder DamageFx's static
methods read from since there's no MonoBehaviour instance to hang an
Inspector field off directly). Two fields: `SmokeResizePct` (default
0.2, replacing the constant above) and `FireResizePct` (default 0.35 --
folding the "resize it 70%, then a lot smaller (0.5 cut)" history
`FirePlume`'s glow range/puff size/growth all separately hardcoded into
ONE number, so raising/lowering fire size can't leave the glow
mismatched with the flame mesh it lights). `RuntimeCityBuilder` gained
a `damageFxProfile` field wired the same way `lightingProfile` already
is, setting `DamageFxProfile.Active` at city-build time.

Unlike `CityLightingProfile` (whose values only take effect at the next
city rebuild), `DamageFx` reads `DamageFxProfile.Active` FRESH on every
puff spawn and every `FirePlume.Update` flicker tick -- an Inspector
slider change takes effect on the very next puff/frame, including on an
ALREADY-burning building, with no rebuild needed. One correctness catch
caught while wiring this: `FirePlume.Update`'s per-frame flicker
intensity used a SEPARATE hardcoded `0.35f` literal that was never
actually tied to the 0.7*0.5 resize history (Awake's own
resize-scaled intensity assignment was dead code, immediately
overwritten by Update every frame) -- naively multiplying that literal
by `FireResizePct` would have compounded into an unintended ~3x
dimming at the profile's own default. Fixed by REPLACING the literal
with the live profile value instead of multiplying both together;
since the profile's default (0.35) numerically matches the literal it
replaced, default behavior is unchanged.

**Verified.** A new standalone `damagefxprofile-verify` harness checks:
`Default.SmokeResizePct == 0.2`, `Default.FireResizePct == 0.35`
(byte-for-byte reproducing prior hardcoded behavior when unassigned),
`Active` falls back to `Default` before anything is set, `Active`
reflects an explicitly assigned profile once one is set, and falls back
to `Default` again once cleared -- all passing. Flightcheck recompiles
`DamageFx.cs`, `DamageFxProfile.cs`, and `RuntimeCityBuilder.cs` clean.
No sim-side files touched, no determinism risk. No Unity Editor here to
confirm the fade curve or Inspector wiring read correctly on a real
screen or actually appear in the Inspector -- same standing limit as
every other Unity-side visual change in this project's history.

## 2026-08 follow-up: two real bugs -- smoke "going solid" at 50% HP, and wind drift too subtle to read

Creator report, same session: "the smoke radiates from one point and
does not travel upward drift away at an diagonal based on wind speed.
Bug is that it goes solid at about 50% destruction; this must be left
over code." Both halves turned out to be real, distinct bugs, and the
creator's own diagnosis ("left over code") was correct for the first
one.

**Bug 1: solidifying at the Damaged threshold.** Root cause found in
`RuntimeCityBuilder.ApplyBuildingDamage`'s Intact->Damaged darkening
block (docs/21 batch 2, item 3 -- written well before fire/smoke FX
existed). It calls `cube.GetComponentsInChildren<Renderer>()` --
RECURSIVE, not single-level -- over `cubes[0]`, the SAME transform
`DamageFx.AttachSmoke`/`AttachFireCluster` use as their `holder`
(several entries back). Fire/smoke puffs are parented several levels
under that same cube (cube -> SmokePlume/FireCluster -> individual
puff), so the darkening sweep was ALSO catching whatever puff
GameObjects happened to be alive at the exact instant a building
crossed 50% HP. For each caught renderer it built a brand-new Material
and called `renderer.sharedMaterial = mat` -- but that new material was
never passed through `LabMeshBuilder.MakeTransparent` (this loop
predates transparent dressing being a concern) and is a completely
separate instance from the one `SmokePuff`'s own `_mat` field still
points to. The net effect: the renderer's VISIBLE material became a
frozen, opaque, one-time-darkened snapshot, while `SmokePuff.Update`
went on mutating the OLD material every frame with zero visible effect
-- reading exactly as "goes solid," for exactly the puffs unlucky
enough to be alive at that one instant. `BaseDresser.TintShape` (the
RTS-roster equivalent) was already safe from this by design -- its own
header comment notes its sweep is deliberately single-level `GetChild`,
specifically so nested assemblies without their own tint intent (the
Big Brain jar, and as of now implicitly fire/smoke too) aren't swept
up. `RuntimeCityBuilder`'s procedural path had no equivalent guard.
Fixed by skipping any renderer with a `SmokePlume` or `FireCluster`
ancestor (`Renderer.GetComponentInParent<T>()`), rather than
restructuring the darkening sweep to single-level (which would risk
missing legitimate nested dressing pieces the recursive sweep is
otherwise relied on to reach).

**Bug 2: wind drift too subtle to read.** `SmokePlume`'s wind-lean
magnitude was a hardcoded `LeanStrength = 0.55f` (several entries
back). Over a puff's whole 3.2s life that's under 2 world units of
total sideways travel -- already modest, and became far LESS visible
once the immediately-prior entry shrank puffs themselves down to
roughly 1 world unit under `SmokeResizePct = 0.2`. The net read was a
column of small puffs popping in and fading near a fixed origin point,
not a visibly diagonal wind-blown trail -- matching the report exactly.
Replaced the hardcoded constant with a new `DamageFxProfile.
SmokeWindSpeed` field (default 1.8, same live-Inspector-tunable pattern
as the size knobs added in the immediately-prior entry), read once per
building when its `SmokePlume` is created (a build-time value, unlike
the per-puff-spawn size knobs -- an Inspector change takes effect on
the next building that catches fire, not an already-burning one; this
distinction is documented on the field's own tooltip).

**Verified.** Flightcheck recompiles `DamageFx.cs`,
`DamageFxProfile.cs`, and `RuntimeCityBuilder.cs` clean.
`damagefxprofile-verify` extended with a check for
`Default.SmokeWindSpeed == 1.8`, still all passing. Bug 1's fix could
NOT be behaviorally verified by the standalone harness -- the local
`UnityStub.cs`'s `GetComponentInParent<T>()` is a dumb stub that always
returns `default(T)` regardless of actual hierarchy, so this is a
compile-time/logical-review confirmation only, not a runtime one. No
Unity Editor here to confirm either fix actually reads correctly on a
real screen -- same standing limit as every other Unity-side visual
change in this project's history.

## 2026-08 follow-up: one shared compass wind for smoke, and fire reverted back up to a visible size

Creator direction, same session: "because the camera is above the
smoke the smoke must travel far to get the correct angle N, S, E or W.
as if in a very strong fast wind. you are trying to correctly
reproduce the smoke in the picture, with the fading. I still do not
see the fire. check placement on various building to make sure it is
visible to the player."

**Why a per-building random angle didn't work.** The RTS camera looks
down at the city from mostly-overhead, not from the side -- meaning
horizontal (X/Z-plane) drift is what actually registers as motion on
screen; vertical rise contributes comparatively little from that angle.
Two compounding problems with the prior approach: (1) each building's
own wind-lean angle was independently hashed off its `GetInstanceID`,
so thirty burning buildings would show thirty different drift
directions -- from overhead this reads as noise, not "there is wind,"
even if any single plume's own drift were strong; (2) the magnitude
itself (`SmokeWindSpeed`, 1.8 as of the entry before this one) was
still comparatively timid once actually judged against "must travel
far... very strong fast wind."

**Fix: one shared compass direction.** New `DamageFxProfile.
CompassDirection` enum (North/East/South/West -- deliberately NOT a
free angle) + `SmokeWindDirection` field (default North) +
`SmokeWindAngleRadians` helper property that converts it to the same
`Mathf.Sin(angle)*x, Mathf.Cos(angle)*z` convention every other
per-building angle in `DamageFx.cs` already uses (0 rad = +Z/North, 90
deg = +X/East -- confirmed against `Minimap.cs`'s own documented
"fixed north-up" default orientation, so this doesn't invent a
convention that would contradict the minimap's own N marker).
`DamageFx.AttachSmoke` no longer hashes a per-building angle at all --
every building's plume now leans (and originates from) the exact SAME
compass direction, so the whole city reads as one coherent wind instead
of each plume pointing an arbitrary way. `SmokeWindSpeed`'s own default
jumped again, 1.8 -> 5 (up from an original hardcoded 0.55 three
entries back) -- at 5 units/sec over a puff's 3.2s life that's 16 world
units of travel, several times a typical Small building's own
footprint radius.

**Fire reverted back up to a visible size.** `DamageFxProfile.
FireResizePct` default REVERTED from 0.35 (0.7 * 0.5, two separate "a
lot smaller" passes stacked without re-checking fire's own visibility
independently of smoke's) up to 1.0. At 0.35 the flame-shard puff's
actual world-space footprint was `0.28 * 0.35 = 0.098` units -- under
10cm, and roughly a TENTH the size smoke's own puffs were sitting at by
that same point in the history (`SmokeResizePct = 0.2` gives puffs up
to ~0.94 units). That size gap is the likely root cause of "I still do
not see the fire": not a placement or wiring problem (both had already
been fixed in earlier entries), but the mesh itself being too small to
register. 1.0 restores the size from the FIRST shard-mesh pass ("small,
angular, faceted" per the original reference images) -- the last point
in this history the size itself was actually confirmed acceptable
rather than immediately shrunk again in the very next round.

**Placement check across building tiers.** `FireCluster.SpawnOne`'s
roofline height moved from `_height * 0.92f` to `_height * 1.0f`.
0.92 sat BELOW the actual roofline -- for Landmark-tier buildings,
which carry the heaviest roof clutter (water towers, antenna masts;
the exact class of geometry a much-earlier entry already identified as
swallowing smoke visibility at that tier), a fire point at 92% height
could land embedded inside that clutter rather than poking clear of
it. Moved to right at the roofline (100%) -- still reads as "attached
to the roof" per the standing creator direction that put it there, but
no longer nested a few percent below the tallest roof props on the
tier most likely to have any.

**Verified.** `damagefxprofile-verify` extended with checks for the new
defaults (`FireResizePct == 1.0`, `SmokeWindSpeed == 5`,
`SmokeWindDirection == North`) and the compass->radians conversion for
all four directions (North=0, East=90deg, South=180deg, West=270deg,
each checked against the exact `Sin`/`Cos` values `DamageFx.cs` uses) --
all 12 checks passing. Flightcheck recompiles `DamageFx.cs`,
`DamageFxProfile.cs` clean. No sim-side files touched. No Unity Editor
here to confirm the wind direction or fire size actually read correctly
on a real screen, or that fire is now visibly clear of roof clutter on
every building kind -- same standing limit as every other Unity-side
visual change in this project's history; this entry is a best-effort
correction based on the numbers involved (the ~10x fire/smoke size gap,
the roofline-vs-clutter height comparison), not a confirmed-fixed
screenshot.

## 2026-08 follow-up: the actual root cause of "fire is missing" -- a ground-level math bug, plus a from-scratch smoke growth/placement rewrite

Creator direction, same session: "smoke must start from low ON the
building and travel upward. growth in size should never exceed 2 times
the size of the original. give me inspector setting to alter drift.
growth size. wind strength. Fire Flames are STILL MISSING!"

**The real bug behind "fire is missing."** Every prior fire-visibility
pass (size, timing, roofline placement) turned out to be correcting
symptoms of a single upstream bug nobody had traced yet. `DamageFx.
AttachSmoke`/`AttachFireCluster` both compute their spawn height as
`holder.position.y + height * someFraction`, which is only correct if
`holder.position.y` IS ground level. For `RuntimeCityBuilder`'s
procedural-building call site, `holder` is `cubes[0]` -- the building's
own massing cube, built via `SpawnCube(hex, height / 2f, height, mat,
...)` (`RuntimeCityBuilder.cs` line ~1803). A primitive cube "sitting on
the ground" is positioned at its own vertical CENTER, so that `y`
parameter of `height / 2f` means `cubes[0].transform.position.y` is
HALF the building's height above ground, not ground level itself. Every
height-fraction offset computed on top of that landed a half-building-
height too high: fire (at the time, `_height * 1.0f` for the roofline)
was actually rendering at `groundY + height*0.5 + height*1.0 =
groundY + height*1.5` -- floating 50% of the building's OWN height
above its real roofline. For a Small building that's a couple of extra
meters (borderline noticeable); for a Landmark it could be 15-20+
extra meters, almost certainly out of the frame a player would
actually be looking at. `BaseDresser`'s RTS-roster call site was NEVER
affected -- its root transform (`root.transform.position = new
Vector3(hexWorld.x, groundY, hexWorld.z)`, `BaseDresser.cs` line ~153)
really is ground level, which is exactly why nobody had reason to
suspect this before: the same shared `AttachSmoke`/`AttachFireCluster`
code was correct for ONE of its two callers and silently wrong for the
other, and the wrong one (`RuntimeCityBuilder`) is what handles "the
vast majority of the map" per several earlier entries in this log.

**Fix.** Both methods gained a `holderGroundOffset` parameter
(default 0, so `BaseDresser`'s call sites need no change at all).
`RuntimeCityBuilder.ApplyBuildingDamage` now passes `-height * 0.5f`,
computed from the SAME `height` variable `SpawnCube` used to place the
cube in the first place -- the exact inverse of the error. Documented
at length on `AttachSmoke`'s own doc comment (the natural place a
future reader investigating a similar "why is X floating in the wrong
place" bug would look first).

**Smoke starts low and travels upward.** Creator direction: "smoke
must start from low ON the building and travel upward." Origin height
fraction dropped from 1.05 (previously already ABOVE the roof --
barely anywhere to visibly rise TO) to 0.3 (low on the wall, well under
where `AttachFireCluster`'s own points sit at the roofline). The
horizontal outward-offset multiplier was also simplified from
`footprintRadius * 1.2` (pushing the origin outside the footprint
immediately) to `footprintRadius * 1.0` (right at the building's own
wall) -- with fire now safely separated by height alone (0.3 vs 1.0),
the extra horizontal push is no longer needed to avoid the two
effects overlapping.

**Growth capped at 2x, and a real bug found while wiring it.** While
implementing "growth in size should never exceed 2 times the size of
the original," found that `SmokeResizePct` had never actually reached
a smoke puff's rendered size at all: `SmokePlume.SpawnPuff` computed a
`startSize` from it, but that value only ever set `go.transform.
localScale` for ONE frame before `SmokePuff.Update` overwrote it every
subsequent frame from its OWN `_baseScale` field -- which `InitPlume`
had never actually touched, leaving it at the shared 0.8 default every
non-smoke puff kind also uses. Separately, the growth formula itself
(`_baseScale * Lerp(_startScaleFraction, 1, t) + t * _growth`) added a
flat amount unrelated to the puff's own starting size, and could reach
roughly 9-10x that starting point by end of life -- nowhere near a 2x
cap. Both fixed together: `InitPlume`'s `growth` parameter is gone,
replaced by `startSize` (this method's own actual starting size,
finally assigned to `_baseScale`); a new `_useGrowthMultiplier`/
`_growthMultiplier` pair drives a clean `_baseScale * Lerp(1,
growthMultiplier, t)` formula (smoke only -- every other puff kind
keeps the original formula, branched on `_useGrowthMultiplier`,
completely unchanged). `growthMultiplier` is read from
`DamageFxProfile.Active.SmokeGrowthMultiplier` and clamped to `[1, 2]`
IN CODE (`Mathf.Clamp`), not just via the Inspector's own `[Range]`
attribute -- a `[Range]` only constrains the Editor's slider UI, not an
assignment from script or a stale serialized asset, so the code-level
clamp is what actually guarantees the ceiling the creator asked for.

**Inspector knobs.** Three new `DamageFxProfile` fields answer "give
me inspector setting to alter drift. growth size. wind strength":
`SmokeRiseSpeed` (drift -- vertical climb rate, default 1.4, replacing
the flat constant `SmokePuff.Init` used to bake in for every puff
kind, now overridden per-smoke-puff in `InitPlume`), `SmokeGrowthMultiplier`
(growth size, default 2, see above), and the already-existing
`SmokeWindSpeed` (wind strength, unchanged this entry).

**Verified.** A new standalone `smokegrowth-verify` harness uses
reflection to call the real `SmokePuff.InitPlume` (not a reimplementation)
and inspect its private fields directly: confirms `SmokeGrowthMultiplier`
values of 5 and 0.3 (values that would bypass the Inspector's own
`[Range]`) clamp to 2 and 1 respectively, a mid-range value of 1.5
passes through unclamped, `startSize` genuinely becomes `_baseScale`
(the bug above, now fixed), `_useGrowthMultiplier` gets set, and an
analytic sweep of the growth formula across `t` in `[0, 1]` confirms
its maximum is exactly `baseScale * growthMultiplier` for every
sampled point, not just at `t=1` -- 6 checks, all passing. The ground-
offset fix itself could NOT be similarly exercised: the local
`UnityStub.cs`'s `Transform.SetParent`/`GetChild`/`childCount` are
no-op stubs with no real scene-graph bookkeeping, so there's no way to
spawn `AttachFireCluster`/`AttachSmoke` in this environment and
inspect the resulting child transform's actual position -- this half
of the fix is a traced-through-the-math correctness argument (repeated
above) and a flightcheck recompile, not an executed test.
`damagefxprofile-verify` re-run clean against the new
`FireResizePct == 1.0`/`SmokeWindSpeed == 5` defaults from the entry
before this one. Flightcheck recompiles `DamageFx.cs`,
`DamageFxProfile.cs`, and `RuntimeCityBuilder.cs` clean. No sim-side
files touched. No Unity Editor here to confirm any of this actually
reads correctly on a real screen -- same standing limit as every other
Unity-side visual change in this project's history, though the
ground-offset fix in particular is a strong, mechanically-verifiable
(re-derived by hand above) correction rather than a guess.
