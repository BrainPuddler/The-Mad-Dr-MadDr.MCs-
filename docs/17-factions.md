# 17 — Factions: One Engine, Three Armies

Status: Draft v0.1 · Pillars served: 2, 3 · Extends [15-part-genetics.md](15-part-genetics.md) and [16-brains-behavior-command.md](16-brains-behavior-command.md) · Prototyped in [`/prototype/mutator/factions.py`](../prototype/mutator/factions.py) · Scope decision tracked as Q13 in [12-open-questions.md](12-open-questions.md).

## The principle: a faction is an expression profile, not a new system

Everything built so far — genome, part homologs, canalized expression, brain genes, the loyalty/rage simulation — runs unchanged for every faction. A faction is three presets layered over the shared engine:

1. **A behavior profile**: what a control snap *means*, how hard decapitation hits, how much formation cohesion steadies loyalty.
2. **Brain canalization**: the gene ranges its members can occupy (the same trick the part families use for geometry, applied to minds).
3. **A part-origin policy**: which catalog its bodies draw from — flesh, issued steel, or grown biotech.

This is the Archon lesson applied to factions: asymmetric sides that remain *comparable* because they obey the same underlying rules. One simulation to balance, three completely different armies to play.

| | Monsters (the doctors) | Human army | Alien hive |
| --- | --- | --- | --- |
| Control snap | **Feral** (low guile) or **Rebellion/usurp** (high guile) | **Rout** — soldiers run, they don't turn | **Frenzy** — a drone without the song is an animal |
| Recovery | Exhausted berserkers re-leashed; rebels are lost | **Rally** — routed troops recover and rejoin the line | None. Hive units don't come back |
| Decapitation shock | −0.45 | −0.50 (officers matter) | **−0.85 — the hive dies with its Queen** |
| Cohesion bonus | none — only the leash | +0.10 (drill: formation steadies morale) | **+0.30 (pheromone pinning: near the Queen, near-unbreakable)** |
| Brain ranges | full spectrum — that's the *point* of the Mutator | low will (drilled), low fury, modest guile | drones: will ≈ 0, command ≈ 0; Queen: command ≈ 1, mastermind tier |
| Parts | **organic** — breeds freely | **tech** — issued, never mutates | **flesh + biotech** — grown machines that breed |

## Humans: technology as issued equipment

Human units express through the same genome — body plan locked to upright tetrapod, but their hand/eye/sensor/leg slots carry **tech-origin part families**: `rifle_arm`, `optic_visor`, `sensor_mast`, `piston_leg`. The origin tag changes the breeding rules, and this is the design heart of the faction:

> **Tech never mutates and never blends.** A rifle passes through a splice whole or not at all; Gaussian drift doesn't touch a trigger group. Equipment changes only by **Graft** — which for humans is fictionally the *quartermaster*, not the vat. The same deterministic operator, re-skinned.

Behaviorally, humans are the **morale** faction: drilled (low will — cheap to command, so officers hold real squads), nearly fury-less, with a cohesion bonus for holding formation. When loyalty breaks they **rout** — flee the fray, *keeping* their commander link — and the routed **rally** back once stress lifts and an officer still stands. Humans bend and recover; their armies degrade gracefully and reform. Their structural weakness: officers are load-bearing (decap −0.50) and their gear can't evolve — what they bring to the field is what they have.

## Aliens: the hive, flesh fused with grown technology

The alien hive is the inverted command experiment. The **Queen** is a mastermind brain with command ≈ 1 — capacity ≈ 4.8 — while **drones** have almost no will (cost ≈ 0.3 each), so one Queen holds a dozen-strong swarm: the capacity formula from [16](16-brains-behavior-command.md) produces hive scale *with no new rules*. Pheromone cohesion (+0.30) pins drone loyalty at its ceiling; the prototype shows a barrage that routs human squads leaving the hive merely *twitchy* — drones waver and snap back, every tick, because their equilibrium sits at 1.0. (A nice emergent inversion: high `temperament` — volatility — is the hive's *strength*, since volatile integration means snapping back to devotion as fast as being shaken.)

The price of the song is the singer. **Decapitation shock −0.85**: kill the Queen and every drone collapses to frenzy *at once* — no rally, no succession, just a field of feral animals. The hive is the faction that turns the command system's headline tactic ([16](16-brains-behavior-command.md)) into its entire win condition: against aliens, *the Queen is the Vat*.

Their bodies answer the brief — **a mix of technology and physical alien flesh**: chitin legs, compound eyes, antennae (organic families), carrying **biotech**-origin parts like the `plasma_lance` — grown machines. Biotech's breeding rule is the alien advantage and the alien horror in one line:

> **Biotech breeds like flesh.** Where the human rifle is inert in the genome, the plasma lance mutates, blends, and crossbreeds. Alien weapons *evolve*. (And if a player's Mutator ever ingests an alien genome fragment — [06](06-mutator-design.md) discovery — that's the late-game content faucet: stitched horrors with grown guns.)

## Monsters: the baseline, and the player

The doctors' menagerie keeps the full system from [15](15-part-genetics.md)/[16](16-brains-behavior-command.md): unrestricted brain spectrum, organic parts, feral/rebel/usurp snaps, berserkers. Monsters are the *expressive* faction — the widest possibility space, the least institutional safety. Humans have drill and aliens have the song; the doctor has only the leash he stitched himself.

## Energy: blood, fuel, and ichor

The upkeep identity from [05-component-economy.md](05-component-economy.md) (Blood as the flowing commodity that fielded monsters consume) generalizes across factions through the part-origin system — **the energy type follows the part's origin**, computed per-genome in `packages/genome-core/src/energy.ts`:

| Origin | Energy | Who | Fiction |
| --- | --- | --- | --- |
| organic | **Blood** | the doctors' monsters; every living frame | flesh must be fed |
| tech | **Fuel** | the human army's machines | engines burn; supply lines matter |
| biotech | **Ichor** | the hive's grown weapons and organs | the alien fuel — secreted, not refined |

Demand rules (v0.1, implemented and tested):

- The **living frame always drinks blood** — body (scaled by bulk) plus brain (bigger minds are hungrier). Even a rifleman must eat.
- Each **part pays by its origin**, scaled by its expressed mass (length × girth through the canalized bounds): a vestigial arm sips, a titan arm gulps.
- **Mixed bodies pay mixed bills.** An alien drone (chitin legs + plasma lance) drinks blood *and* ichor; graft one piston leg onto a monster and a fuel line appears on its bill. Cross-origin grafting is thus a real strategic decision, not a free upgrade.
- **Silent genes are free**: slots a body plan ignores ([15](15-part-genetics.md) atavism) cost nothing to carry.
- **Starvation is typed**: running a pool dry starves exactly the parts that pool feeds — blood-starved flesh takes decay damage ([05](05-component-economy.md)), fuel-starved equipment goes *inert* (a rifleman with an empty tank still has fists), ichor-starved biotech decays *and* misfires (match-sim behavior, Phase-2 tuning).
- The **reanimation mana surge** scales with total upkeep demand plus a brain premium — hungrier designs take a bigger jolt to start ([03](03-mana-system.md) dual-currency split).

Supply is each faction's strategic texture, and deliberately asymmetric: monsters get the **territory blood trickle** and corpse salvage ([05](05-component-economy.md)); humans get **fuel depots and convoys** — point-supply that makes their logistics *attackable* (cut the convoy and the platoon's machines wind down); the hive gets **digestion pits** that convert biomass (corpses included) into ichor — the swarm eats the battlefield. Supply-side numbers are Phase-2 sandbox work.

## Materials: the class × flavor matrix

Adopted 2026-07 (decision log, [12](12-open-questions.md)). The material economy
generalizes exactly like energy did: **three material classes, one flavor per
faction**. The arithmetic is shared; only the nouns and the map change. All
action takes place **on Earth** — every faction mines the same planet, each
seeing different wealth in it.

| Class | What it prices | MadDr (organic) | Human Army (tech) | Alien Hive (biotech) |
| --- | --- | --- | --- | --- |
| **Structure** | frame: bulk, plan, chassis | **Bone** | **Steel** | **Chitin** |
| **Motive** | actuation: limb mass | **Muscle** | **Motors** (servos, engines) | **Sinew** |
| **Control** | the mind; gates tier | **Brain** | **Tubes** (vacuum-tube racks — it's the '50s–'70s) | **Ganglion** |
| **Energy** | upkeep (already adopted) | **Blood** | **Fuel** | **Ichor** |

Concrete **Part items** (the surgical tray / harvested limbs) remain
cross-faction as-is — a harvested rifle arm is a rifle arm in anyone's tray.

### The bill of materials IS the genome

A unit's build cost derives from the same genes that build its body — no
separate stat sheet (the pillar-1 promise: what you bred is what you pay for).
v0.1 formulas, Phase-2 sandbox tunes:

```
structure = 2 + 8·bulk (+4 mech chassis, +2 per heavy leg pair)   [Structure class]
motive    = Σ over expressed hand/leg parts: 1 + 3·(length·girth) [Motive class]
control   = brain tier: Dim 1 · Average 2 · Gifted 4 · Mastermind 7 [Control class]
energy surge at deploy = reanimationSurge(genome)                  [Energy class]
```

**Each line item is paid in the flavor of the part's origin.** A pure monster
bills Bone + Muscle + Brain. Graft a rifle arm onto it and the bill grows a
Steel + Motors line — the Doctors must salvage Human wrecks or raid junkyards
to field it. Mixed bodies pay mixed bills, in materials as in energy.

### Harvesting the vanquished

Corpse salvage (40–60% of the bill, [05](05-component-economy.md)) pays out in
the **corpse's own flavors** — kill a tank-monster, harvest Steel. Foreign
materials then have three uses:

1. **Spend directly** on grafted parts of that origin (the good use).
2. **Render** at the lab: 2:1 conversion **within a class** (Steel↔Bone↔Chitin,
   Motors↔Muscle↔Sinew). Rendering is lossy on purpose — raiding for the real
   thing beats boiling down scrap.
3. **Control materials never convert.** Brains, Tubes, and Ganglia are each
   faction's identity; an enemy Tube rack is only good for driving a grafted
   tech part's fire-control. This keeps control the rare, faction-defining
   bottleneck.

### World sources on Earth

Map nodes trickle materials on a channel-harvest, like graveyards in
[05](05-component-economy.md). **Factions value the same Earth differently** —
node contention is asymmetric by design:

| Earth node | MadDr | Human Army | Alien Hive |
| --- | --- | --- | --- |
| Hospital / blood bank | **Blood+++, Brains+** | – | biomass++ |
| Graveyard / crypt | **Bone++, Parts+** | – | biomass+ |
| Stockyard / butcher row | **Muscle++** | – | biomass++ |
| Junkyard / scrapyard | Steel+ (graft feedstock) | **Steel+++** | – |
| Refinery / gas station | – | **Fuel+++** | – |
| Factory / motor pool | – | **Motors++** | – |
| Radio station / electronics plant | – | **Tubes++** | – |
| Farm / feedlot / silo | Muscle+ | – | **biomass+++** |
| Sewage works / landfill | – | – | **biomass++** |
| Forest / park | – | – | biomass+ (slow, vast) |

**The Hive mines one thing: biomass.** Digestion pits ([above](#energy-blood-fuel-and-ichor))
refine it into everything, at Protoss-grade cost — few sources, expensive
units, quality output (v0.1 rates): `1 biomass → 1 Ichor · 2 → 1 Chitin ·
2 → 1 Sinew · 6 → 1 Ganglion`.

The asymmetry writes the map strategy by itself: Doctors and Hive both covet
the hospital (blood vs. biomass); Doctors contest the Human junkyard the
moment they start grafting; nobody but the Hive wants the sewage works — so
the swarm quietly out-economies anyone who lets it eat in peace.

## Strategic asymmetry (intent)

- **vs Humans**: kill officers to shatter squads — but expect rallies; humans must be *broken repeatedly* or routed off objectives. Their tech is reliable and un-counterable by breeding tricks.
- **vs Aliens**: the swarm cannot be morale-broken — fighting drones is fighting weather. Everything is a Queen hunt under a clock, against pheromone-pinned defense in her radius (which behaves like an emitter aura, [03](03-mana-system.md)).
- **Matchmaking/power budget** ([09](09-multiplayer-architecture.md)): the same power-budget arithmetic prices all three (drone cheapness × swarm size ≈ squad cost ≈ monster value), because all three are built from the same genes.

## The prototype (run it)

```
python3 demo_factions.py   # -> out/factions.svg + both narrative logs
python3 test_mutator.py    # incl: humans rout-and-rally (never feral/rebel),
                           # hive holds under barrage + collapses on queen death,
                           # tech inert under breeding while biotech drifts
```

`out/factions.svg`: Exhibit 1 renders all three factions from genomes with the same creature renderer (troopers with rifles and visors; drones with insect legs and plasma lances; the Queen as a massive blob; a monster brute). Exhibits 2–3 are the behavioral signature charts: human morale dipping into rout and rallying back, versus the hive's flat-pinned loyalty ending in a single vertical cliff labeled *Queen slain*.

## Open scope (Q13)

Are humans and aliens **campaign/AI factions** (the nephew's antagonists — the army that hunts your experiments, the things that fell from the sky) or eventually **playable** with their own meta loops (requisition instead of the Mutator; hive evolution as a constrained Mutator)? V1 recommendation: AI factions for single-player skirmish ([11-roadmap.md](11-roadmap.md) Phase 2) — they exercise the engine without needing three balanced meta-economies. Parked in [12-open-questions.md](12-open-questions.md).
