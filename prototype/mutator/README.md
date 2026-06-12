# Mutator Part-Genetics Prototype

A runnable demonstration of the part-genetics strategies in
[docs/15-part-genetics.md](../../docs/15-part-genetics.md): parts that mutate
and crossbreed freely while always staying recognizable as what they are
(claws read as claws, antennae as antennae, bug-eyes as bug-eyes).

Pure Python 3, no dependencies.

```
python3 test_mutator.py           # property tests (parts, bodies, brains, command)
python3 demo.py [seed]            # parts gallery   -> out/gallery.svg
python3 demo_creatures.py [seed]  # body plans      -> out/creatures.svg
python3 demo_command.py [seed]    # command/rebellion -> out/command.svg + log
```

| File | What it is |
| --- | --- |
| `genome.py` | Body genes (plan + posture/bulk/limb/tail), brain genes (tier + command/will/temperament/guile), and slot alleles with six shared semantic axes |
| `catalog.py` | 11 part families in 4 homolog classes (hand/sensor/eye/leg) and 4 body plans |
| `operators.py` | Mutate / Splice / Graft (seeded-RNG deterministic) over parts, bodies, and brains |
| `render_svg.py` | Procedural ink-sketch part renderer (2D stand-in for the 3D pipeline) |
| `render_creature.py` | Full-creature assembly: tetrapod (biped↔monkey↔quadruped posture continuum), blob, serpentine, winged |
| `command.py` | Control capacity/cost/radius, loyalty dynamics, feral/rebel/usurp/decapitation |
| `demo.py` | Parts gallery: family variation, mutation lineage, splice inheritance |
| `demo_creatures.py` | Creature gallery: posture sweep, populations, cross-plan splice, atavism |
| `demo_command.py` | Command scenario: a chain of command, an assassination, the cascade |
| `out/*.svg` | Committed sample output (seed 2026) |

This is a Phase-1 Track B exploration ([docs/11-roadmap.md](../../docs/11-roadmap.md)).
Python is for prototyping speed only — the genome schema is the contract, not
the language ([docs/07-mutator-server-architecture.md](../../docs/07-mutator-server-architecture.md)).
