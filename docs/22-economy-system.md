# 22 — The Living Economy: Onboard Resources, Medics, Storage & Factories

Status: Draft v0.1 · Pillars served: 1 (*Every monster is yours*), 3 (*Honest combat*) · Terms: [glossary](00-index.md#glossary). All numbers are **v0.1 placeholders — Phase-2 sandbox validation** ([11-roadmap.md](11-roadmap.md)). Open items tracked as **Q25–Q29** in [12-open-questions.md](12-open-questions.md).

**What this doc adds.** The existing economy ([05](05-component-economy.md), [20](20-harvest-and-repair.md)) is a *wallet* economy: Blood/Bones/Brains accumulate in a per-player pool, and units are built or repaired from it at the Vat. This doc pushes those same resources **into the units themselves**: every monster carries an onboard blood tank, bone plating stock, and grey-matter charge, drains them by fighting and being hit, degrades (never stalls) as they empty, and refills them by eating, visiting storage, or being tended by a medic — an RPG-style body economy layered onto the RTS wallet. It also adds the missing production/logistics layer around it: **harvester units**, **storage structures** (blood banks, bone piles), **medic units**, and **factories**.

**What this doc does NOT change.** The resource trio and their wallet-level sources/sinks ([05](05-component-economy.md)), Collection Stations ([20](20-harvest-and-repair.md)), mana as a disjoint energy currency ([03](03-mana-system.md)), the genome schema ([06](06-mutator-design.md) — normative), and the origin-energy rule (organic drinks **Blood**, tech burns **Fuel**, alien biotech drinks **Ichor**; mixed bodies pay mixed bills — [17](17-factions.md), `genome-core/src/energy.ts`). Everywhere this doc says "Blood," a tech part reads **Fuel** and a biotech part reads **Ichor** for its own share of the bill, exactly per the existing rule.

---

## 1. Design contract: fun first, NEVER annoying

This is a core mechanic. Its job is to make units feel *alive* — hungry, woundable, repairable — not to tax the player's attention. Every rule below passes these five tests, and any future change to this system must too:

1. **Floors, not stalls.** A depleted resource *degrades* a unit; it never disables, strands, or kills it. There is no starvation death, no out-of-fuel paralysis, no death spiral. Worst case, a unit is slower and softer — still yours, still fighting, still able to walk home.
2. **Auto-first.** Refilling, harvesting, hauling, and healing all happen *by default* through proximity and unit AI. Buttons and manual orders are optimizations for players who want the edge — never the required baseline. A player who ignores this entire system must still have a functional army.
3. **One-glance readability.** A unit's onboard state is three thin pips under its health bar (blood red / bone white / brain pink), shown only while selected or below full. No menus, no tooltips required to understand "he's hungry."
4. **Refills are power-ups, not chores.** The moment of refilling — a monster gulping at a blood bank, cracking a bone pile open, a medic's stitch-flash — is staged as a *reward beat* (animation, sound, a brief glow), the way a health pack feels in an FPS. The player should *want* to see it happen, not resent having to cause it.
5. **Degradation is legible drama, not hidden math.** A blood-starved monster visibly slumps and slows; a bone-stripped one shows cracked plating; a brain-drained one hesitates before acting. The player reads the state from the battlefield, and the fix is always one obvious action away.

Anti-annoyance guardrails, concretely: upkeep burn **pauses entirely** for units idle inside their own Vat/storage aura (parking your army at base is always safe, never a slow leak); low-resource states announce once (a single bark + HUD pip pulse), never nag; medics and harvesters self-assign by default (rally-point AI, not per-unit babysitting).

## 2. The three onboard pools

Every fielded creature carries three onboard pools, each with a **capacity derived from its genome** — the same genes that already exist, no schema change ([06](06-mutator-design.md) remains normative; these are *derived stats*, computed like Locomotion or Combat.Profile):

| Pool | Fiction | Capacity derives from (v0.1) | Drained by | Empty-state floor |
| --- | --- | --- | --- | --- |
| **Blood** (Fuel/Ichor per part origin) | The fuel in the veins | `20 + 30×heartTier + 20×bulk` | Upkeep burn (existing blood/min numbers, [05](05-component-economy.md)); sprinting/flight (2× burn); **spilled when hit** (see §3) | Move −30%, attack rate −25% ("running on fumes") |
| **Bone** (armour stock) | Plating, knuckles, greaves | `10 + 0.5×BonesBillCost` (i.e. bigger builds carry more plate) | Chipped by incoming hits (see §3) | Armor mitigation halved ("stripped to the frame") |
| **Brain** (grey-matter charge) | Concentration, glandular reserve | `5 + 10×brainTier` | Ability casts (1–3 each); command re-assignments for commanders ([16](16-brains-behavior-command.md)) | Ability cooldowns +50%, command lag +1 s ("concussed") |

**The floors are the whole design.** They're tuned so a fully-depleted unit fights at very roughly **two-thirds effectiveness** — weak enough that refilling matters strategically, strong enough that a player who lost the economy war still has an army, a fighting retreat, and a comeback path (contract rule 1). Depletion states also **never stack multiplicatively into oblivion**: total combined degradation is capped at −40% of any single stat.

**Interaction with the existing wallet-zero decay rule.** [05](05-component-economy.md) v0.1 says a player wallet at zero Blood causes 2% max-HP/s decay damage on all fielded monsters. That rule predates onboard pools and is **superseded by this doc**: wallet-zero now simply means *no refills available* — units run their tanks down to the floor state and stay there. Decay damage is deleted because it is precisely the death-spiral/annoyance pattern rule 1 forbids. Logged as a decision (this doc + [12](12-open-questions.md) decision log); reconciliation details tracked as **Q25**.

## 3. Damage as a resource event (the RPG layer)

When a unit takes damage, it doesn't just lose HP — it *bleeds and chips*:

```
bloodSpilled = 0.15 × hpDamage        (capped at current blood)
boneChipped  = 0.10 × hpDamage        (capped at current bone; melee/ballistic
                                       chips 1.5×, energy weapons 0.5×)
```

So a mauled unit limps out of a fight three ways at once: low HP, low blood (slower, hitting less often), low bone (softer against the next fight). This is the "works less efficiently until they consume or refill" loop asked for — the *fix* is §4's refill paths, and every one of them is availabile mid-match without returning to the Lab.

Two deliberate boundaries keep this honest and un-annoying:

- **HP and pools are separate.** Refilling blood/bone does NOT restore HP — that stays the job of Repair ([20](20-harvest-and-repair.md) §6), medics (§5), and the `regeneration` quirk. Pools govern *efficiency*; HP governs *survival*. One bar to die by, three pips to fight well by.
- **No bleed-out.** Spilled blood is gone from the tank, but an empty tank never converts to HP damage (see §2's superseded-decay note). Wounded ≠ doomed.

**Spilled resources are literal.** A fraction of blood spilled in combat pools on the ground as a lootable splatter (reusing the corpse-salvage pattern, [04](04-combat-model.md)): any unit — either side — standing on it for 2 s slurps it into its own tank. Fights leave the ground sticky and worth licking. (Visuals already exist: the eaten-citizen splatter decal from the Unity batch-7 work.)

## 4. Refill paths (all of them low-friction)

Ranked from most automatic to most deliberate; all coexist:

1. **Eat citizens** — the existing eat action ([19](19-citizens.md)) now *also* refills the eater: +8 blood, +2 bone, +1 brain onboard (on top of the unchanged wallet yield). Eating stays the fun, thematic, always-available snack path.
2. **Battlefield slurp** — spilled-blood pools, §3. Free, automatic, contested.
3. **Storage structures** (§6) — standing within 2 hexes of a friendly Blood Bank / Bone Pile / Brain Trust auto-refills the matching pool at 10/s, drawing from the player wallet. No button. Monsters "go there directly," exactly as asked.
4. **Medic units** (§5) — mobile refill + HP repair, for armies far from home.
5. **The Vat** — being at base refills all three pools at 20/s from the wallet *and* pauses upkeep. Home is comfort.

## 5. Medical units: the Sawbones

A new **support unit class**, buildable at the Vat or a factory (§7) like any monster — but its "weapon" is a stitching rig:

- **Auto-triage AI**: idles near its assigned group; when a friendly drops below 60% HP or 50% on any pool, it walks over and channels. No micromanagement required (contract rule 2); manual targeting overrides allowed for the players who want it.
- **What it does**: channels **field Repair** — same formula as Vat Repair (`0.10×missingHP` Bones + `0.20×missingHP` Blood, [20](20-harvest-and-repair.md)) at *half speed* — and **pool transfusion** (refills a patient's blood/bone/brain from the wallet at 5/s). The Vat stays strictly better; the medic buys *place*, not *price*.
- **Where the resources come from**: the wallet, same as everything — the medic is a walking tap, not a second wallet. If the wallet is dry the medic idles (and says so, once).
- **Costs & limits (v0.1)**: cheap frame (15 Bones, 1 Part, Dim brain, 5 mana), low HP, no attack. **Healing does not stack** — one medic per patient, diminishing nothing, just an exclusivity rule, so massed medics make a *wider* safety net, never an unkillable deathball (balance note, §9). Medics are priority targets by design — killing the enemy's Sawbones is real counterplay.

Fiction: a hunched assistant in a blood-stained apron, dragging a wheeled surgery cart. Igor finally gets a job.

## 6. Harvesters & storage: Ghouls, Blood Banks, Bone Piles

**The Ghoul** — a new **gatherer unit class** (cheap, unarmed, cowardly — flees like a citizen when threatened):

- **Auto-scavenges**: seeks corpse salvage ([04](04-combat-model.md)) and spilled-blood pools (§3) within its patrol radius, gathers, and hauls to the nearest storage/Vat, banking it into the wallet. This makes the 15-second salvage window *catchable without micro* — the Ghoul is automated looting, the answer to "I was fighting and missed the salvage" annoyance.
- **Builds storage**: the only unit that constructs the three storage structures, SC-worker-style (channel on site, resources from the wallet).
- **Explicitly optional**: Collection Stations ([20](20-harvest-and-repair.md)) still bank citizen deaths hands-free, and monsters can still eat and loot personally. Ghouls *scale* the economy; they never gate it.

**Storage structures** — player-built, destructible (never capturable — enemies deny by demolition, which is a comeback lever, §9):

| Structure | Stores/refills | Also grants (v0.1) | Cost (v0.1) |
| --- | --- | --- | --- |
| **Blood Bank** | Blood (Fuel/Ichor dispensed per-part automatically) | +100 player Blood wallet cap | 20 Bones, 10 Blood |
| **Bone Pile** | Bone | +100 Bones wallet cap | 15 Bones |
| **Brain Trust** | Brain | +25 greyMatter wallet cap | 10 Bones, 5 Brains |

Wallet caps are new (the wallet was previously uncapped except mana): base caps come from the Vat, storage extends them — the classic supply-structure shape, giving harvest-heavy strategies something to build toward and raids something to break. Cap values are pure v0.1 guesses (**Q28**).

*Naming note*: [17-factions.md](17-factions.md)'s `Hospital / blood bank` **world-source node** (a map feature you channel-harvest) is a different thing from the player-built **Blood Bank structure** — same words, deliberately: robbing the city's blood bank to stock your own is the fiction. Flagged alongside Q20's existing hospital-node overlap.

## 7. Factories: the Stitchworks

A player-built **production structure** (Ghoul-constructed, like storage), answering the StarCraft-shaped ask directly:

- **Reanimates from the Menagerie**: queues any of your ≤12 active designs ([02](02-gameplay-overview.md)), paying the standard component bill + mana surge ([05](05-component-economy.md)) — same price as the Vat, but *forward-deployed*. Rally points, build queue (depth 5), the familiar RTS furniture.
- **Slower than home**: builds at 1.5× the Vat's reanimation time. The Vat remains the throne room; a Stitchworks is a field kitchen.
- **Upgrades units**: the second production tab. A Stitchworks can channel a **field augment** onto a docked unit, spending wallet resources to raise that unit's *onboard capacities* for the rest of the match (match-scoped, never touching the genome — the same runtime-state-not-schema boundary Repair established):
  - **Extra Plating** — +50% bone capacity (cost: 15 Bones)
  - **Second Stomach** — +50% blood capacity (10 Blood, 5 Bones)
  - **Gland Booster** — +50% brain capacity (5 Brains)
- **Vulnerability is the balance**: a forward Stitchworks with a queue is a massive tempo investment standing in enemy territory — killing it refunds nothing to you and everything to your opponent's momentum. Whether its destruction refunds *queued* (unstarted) bills to its owner is **Q26**.

Genome-permanent upgrades stay where they belong — the Lab's Workshop between matches ([06](06-mutator-design.md)). The Stitchworks is strictly the in-match, temporary, StarCraft-style layer.

## 8. The loop, end to end

```
        fight ──────────► damage spills blood/chips bone (§3)
          ▲                        │
          │                        ▼
   full-efficiency          degraded unit (floors, §2)
        units                      │
          ▲                        │ eat citizen (§4.1) · slurp spill (§4.2)
          │                        │ visit Blood Bank/Bone Pile (§4.3)
          │                        │ Sawbones transfusion (§5) · go home (§4.5)
          └────────────────────────┘
                                   
  Ghouls scavenge corpses/spills ─► wallet ─► storage caps (§6)
  wallet + mana ─► Vat & Stitchworks reanimate/upgrade (§7) ─► more units
```

Every arrow that *drains* the player is automatic and legible; every arrow that *restores* has an automatic default and a skilled override. That asymmetry is the anti-annoyance design in one sentence.

## 9. Balance notes (Phase-2 sandbox agenda)

- **Anti-snowball**: storage/factories are destructible comeback targets; wallet caps stop the leader from banking infinitely; the efficiency floor means a starved army loses ground but not existence; spilled-blood pools feed whoever holds the field *after* the fight, which is often the defender.
- **Anti-deathball**: medics don't stack (§5); refill auras are small (2 hexes), so a turtled deathball refuels slowly through a straw while a mobile force eats the city.
- **Ghoul/medic supply overhead**: support units cost supply-less but compete for the same wallet — the classic worker-count tension, kept gentle (they're cheap) per the never-annoying contract.
- **The three capacities are a real build axis**: heart tier now buys endurance (blood), heavy Bones bills self-armor (bone), brain tier buys casting stamina (brain) — genome choices players already make gain a second, legible meaning. Watch for degenerate corners (e.g. max-heart kite builds that never run dry) in the sandbox.
- **Sanity anchors to preserve** ([04](04-combat-model.md)'s spirit): a fully-fueled unit vs. its fully-drained twin should win ~70% of even fights — decisive, not absolute.

## 10. v0.1 tuning table (consolidated)

| Knob | Value |
| --- | --- |
| Blood capacity | `20 + 30×heartTier + 20×bulk` |
| Bone capacity | `10 + 0.5×BonesBillCost` |
| Brain capacity | `5 + 10×brainTier` |
| Blood spilled / bone chipped per hp damage | 0.15× / 0.10× (melee 1.5×, energy 0.5× chip) |
| Empty-pool floors (move / attack / mitigation / cooldown) | −30% / −25% / half / +50%, total degradation cap −40% |
| Eat-citizen onboard refill | +8 blood, +2 bone, +1 brain |
| Spill-slurp channel | 2 s, either side |
| Storage refill rate / radius | 10/s, 2 hexes |
| Vat refill rate / upkeep | 20/s, upkeep paused at home |
| Sawbones repair speed / transfusion / cost | ½ Vat rate / 5/s / 15 Bones, 1 Part, Dim brain, 5 mana |
| Storage costs & wallet-cap grants | table in §6 |
| Stitchworks build-time multiplier / queue depth | 1.5× Vat / 5 |
| Field augment costs (+50% capacity each) | Plating 15 Bones · Stomach 10 Blood + 5 Bones · Gland 5 Brains |

## 11. Harvester morphology: bred in the Lab, never a bolt-on (IMPLEMENTED)

The Ghoul of §6 described a *unit class*; on creator direction it is realized instead as **genetics** — harvest capability is parts, and parts are the Lab's whole language. You don't buy a harvester off a menu: you **breed, mutate, or graft one**, the same way you make anything else. Implemented in `genome-core` (`catalog.ts` families + `harvest.ts` derived stats) and rendered in the Lab site.

**Harvest tools — hand-homolog families**, one per origin so every faction path has its option ([17](17-factions.md)):

| Family | Origin | Fantasy | Bias |
| --- | --- | --- | --- |
| `lamprey_maw` | organic | a fleshy hose-arm ending in a rasping sucker mouth | **Blood ×3** — and it **drains living targets**, not just corpses |
| `bone_saw` | tech | a motorized circular surgical saw on an articulated boom | **Bone ×3** — corpses only |
| `ichor_siphon` | biotech | translucent siphon tubes that drink from wounds, pulsing | Blood ×2.4, brain ×0.8 — drains living |

Ordinary hands still gather (claws, pincers, chain blades tear at corpses at reduced rates — "nothing is wasted"); a *gun* arm gathers almost nothing. Tool size genes (length/girth) scale the rate: a bigger maw drinks faster. **Speedy legs are simply legs** — pick `talon_leg` and high locomotion genes for a fast gatherer; gather rate rides the hand, speed rides the legs, so the classic fast-fragile-hauler is a build you assemble from existing axes, not a stat block we author.

**Storage vessels — sensor-homolog families** (dorsal-mounted, like antenna/horn/mast — the Hox grammar is untouched, no new slot, no schema change; the trade is real: *a tank on your back is a sensor you don't have*):

| Family | Origin | Fantasy |
| --- | --- | --- |
| `storage_bladder` | organic | a translucent distended sac that visibly sloshes as it fills |
| `steel_tank` | tech | a single riveted cylinder tank — filler cap, sight gauge showing the blood level, strapped to the back like a scuba tank — the human-army look |
| `amber_vesicle` | biotech | clustered amber vesicles fused along the spine, each faintly glowing — the alien look |

Capacity = base + body bulk + the vessel's expressed size; the **blob plan gets a ×1.5 bonus** — an amorphous body *is* a bag ("blob body storage capacity"). Onboard capacity from §2 and vessel capacity pool together.

**Weight and flight.** Carried load slows the carrier: up to −25% ground speed at full, and **−50% for winged/floater plans — flight pays double for every unit carried** (creator direction: "flying units will need to take weight into account"). Both floored per §1's contract (0.6× ground, 0.4× flight): a laden flyer is slow and juicy to intercept, never grounded — the risk/reward of an aerial blood-tanker is the *fun*, being stranded would be the *annoying*.

All of it is ordinary genetics end to end: these families mutate, splice, canalize, cost upkeep by origin ([energy](05-component-economy.md)), harvest/sew between bodies ("nothing is wasted"), and obey the heart-capacity gate. The catalog addition shifted the deterministic mutation stream, so the golden lineage digest was regenerated — a deliberate, versioned change ([12](12-open-questions.md) decision log).

**Battlefield-side, now wired in (Unity):**

- **`harvest.ts` has a C# twin** — `roster-client/Harvest.cs`, golden-verified against the real JS (the same Locomotion/Weapon discipline), so the Lab preview and the battlefield agree on gather rate, carry capacity, and how much a load slows a carrier.
- **The six families render on the battlefield** — `creature-mesh` gained real geometry for all of them (sucker-mouth-with-tooth-rings, saw blade on a boom, pulsing siphon tubes, plus the three storage vessels below), so a bred harvester looks like one in the field, not a default stub. **Every body plan declares its own `back` socket** (position + outward surface normal), so the mount is exact per-plan, never a generic guess. A shared **pack frame** (`packP`/`packR` in the Lab, `PackP`/`PackR` in Unity — local axes *across* / *along* / *out of the surface*) lets one geometry definition orient correctly whether the back normal points backward (upright bodies: tetrapod, winged, avian, treant, floater → pack stands vertical) or upward (low horizontal bodies: crab, arachnid, serpentine, blob → pack lies flat on top, away from the tail). Their form reads by origin and their contents by resource: the **tech `steel_tank` is a single cylinder barrel strapped to the mount** — mostly proud of the hide with a saddle collar seating it against the body, the classic scuba/oxygen-tank silhouette (solid/functional, not embedded); the organic `storage_bladder` is **pus-filled sacs half-sunk in the trunk, bulging out through the skin**; the biotech `amber_vesicle` is a **cluster fused into the back, swelling through the hide**. Across all three the **contents read RED for blood / WHITE for bone** by the harvest tool — a glance tells you what a hauler carries.
- **Gather / carry / weight is live in `MonsterAgent`** — a harvester that eats a citizen strips a load into its onboard tank scaled by its blood-gather rate (a lamprey-and-tank build is a real hauler); the carried load **slows the carrier via the exact `Harvest` speed factors — floored, and doubled for flyers** (the creator's weight rule, made real); and the player hauls a laden harvester back near its spawn, where it **banks automatically** into the session wallet and its speed recovers. Auto on arrival, but the hauling is the player's call — no unit ever walks off on its own.

Still design-only (the larger docs/22 build-out): dedicated storage structures, medics, factories, and the full onboard blood/bone/brain pools of §2 — the harvester's single pooled `_carriedLoad` here is the first working slice of that system.

## 12. Open questions

Logged in [12-open-questions.md](12-open-questions.md): **Q25** (wallet-zero decay rule superseded — full reconciliation with [05](05-component-economy.md)), **Q26** (Stitchworks destruction: refund queued bills?), **Q27** (medic auto-triage AI tuning vs. deathball risk), **Q28** (wallet-cap values and whether caps apply retroactively when storage dies), **Q29** (brain-charge interplay with Megabrain Augmentation and commander capacity — does the +7.2 Capacity monster also need a bigger grey-matter tank?).
