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
