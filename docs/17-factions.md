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
