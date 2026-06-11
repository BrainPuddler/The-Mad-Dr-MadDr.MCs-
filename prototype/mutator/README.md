# Mutator Part-Genetics Prototype

A runnable demonstration of the part-genetics strategies in
[docs/15-part-genetics.md](../../docs/15-part-genetics.md): parts that mutate
and crossbreed freely while always staying recognizable as what they are
(claws read as claws, antennae as antennae, bug-eyes as bug-eyes).

Pure Python 3, no dependencies.

```
python3 test_mutator.py   # property tests
python3 demo.py [seed]    # writes out/gallery.svg
```

| File | What it is |
| --- | --- |
| `genome.py` | Slot/allele genome with six shared semantic axes |
| `catalog.py` | Part families: homolog classes, invariants, canalized bounds |
| `operators.py` | Mutate / Splice / Graft (seeded-RNG deterministic) |
| `render_svg.py` | Procedural ink-sketch renderer (2D stand-in for the 3D pipeline) |
| `demo.py` | Generates the three-exhibit gallery |
| `out/gallery.svg` | Committed sample output (seed 2026) |

This is a Phase-1 Track B exploration ([docs/11-roadmap.md](../../docs/11-roadmap.md)).
Python is for prototyping speed only — the genome schema is the contract, not
the language ([docs/07-mutator-server-architecture.md](../../docs/07-mutator-server-architecture.md)).
