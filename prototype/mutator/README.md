# Mutator Part-Genetics Prototype

A runnable demonstration of the part-genetics strategies in
[docs/15-part-genetics.md](../../docs/15-part-genetics.md): parts that mutate
and crossbreed freely while always staying recognizable as what they are
(claws read as claws, antennae as antennae, bug-eyes as bug-eyes).

Pure Python 3, no dependencies.

```
python3 test_mutator.py        # property tests
python3 demo.py [seed]         # parts gallery -> out/gallery.svg
python3 demo_creatures.py [seed]  # body plans  -> out/creatures.svg
```

| File | What it is |
| --- | --- |
| `genome.py` | Body genes (plan + posture/bulk/limb/tail) and slot alleles with six shared semantic axes |
| `catalog.py` | Part families (homolog classes, invariants, canalized bounds) and body plans |
| `operators.py` | Mutate / Splice / Graft (seeded-RNG deterministic) |
| `render_svg.py` | Procedural ink-sketch part renderer (2D stand-in for the 3D pipeline) |
| `render_creature.py` | Full-creature assembly: tetrapod (biped↔monkey↔quadruped posture continuum) and blob plans |
| `demo.py` | Parts gallery: family variation, mutation lineage, splice inheritance |
| `demo_creatures.py` | Creature gallery: posture sweep, populations, cross-plan splice |
| `out/gallery.svg`, `out/creatures.svg` | Committed sample output (seed 2026) |

This is a Phase-1 Track B exploration ([docs/11-roadmap.md](../../docs/11-roadmap.md)).
Python is for prototyping speed only — the genome schema is the contract, not
the language ([docs/07-mutator-server-architecture.md](../../docs/07-mutator-server-architecture.md)).
