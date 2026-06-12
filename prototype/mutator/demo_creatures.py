"""Generate the body-plan gallery: out/creatures.svg.

Exhibits for the body-plan layer (docs/15-part-genetics.md):
  1. The posture continuum -- one tetrapod genome swept from biped to quadruped
  2. Populations          -- random bipeds, monkey-types, quadrupeds, blobs
  3. Cross-plan splice    -- blob x biped children

Run: python3 demo_creatures.py [seed]
"""

import os
import random
import sys

from genome import BodyGenes, Genome
from operators import random_allele, splice
from render_creature import render_creature_cell
from render_svg import svg_document, INK

CELL = 152
SLOTS = (("hand", "hand"), ("sensor", "sensor"), ("eye", "eye"), ("leg", "leg"))


def _creature(rng, plan, posture=None, limb=None):
    body = BodyGenes(plan, (
        posture if posture is not None else rng.random(),
        rng.random(),
        limb if limb is not None else rng.random(),
        rng.random(),
    ))
    return Genome(tuple((s, random_allele(h, rng)) for s, h in SLOTS), body)


def _row(genomes, x0, y, labels):
    return "".join(render_creature_cell(g, x0 + i * CELL, y, label=labels[i])
                   for i, g in enumerate(genomes))


def _heading(text, y, x=20):
    return (f'<text x="{x}" y="{y}" font-size="12" fill="{INK}" '
            f'font-family="Georgia, serif" font-weight="bold">{text}</text>')


def main(seed=2026):
    rng = random.Random(seed)
    body, y = [], 44

    # -- Exhibit 1: the posture continuum -------------------------------------
    body.append(_heading("Exhibit 1 — One tetrapod, posture gene swept 0 to 1: biped, monkey-type, quadruped — one breedable family", y))
    y += 8
    base = _creature(rng, "tetrapod", posture=0.0, limb=0.75)
    sweep, labels = [], []
    for i, p in enumerate((0.0, 0.2, 0.4, 0.55, 0.7, 0.85, 1.0)):
        b = base.body
        sweep.append(Genome(base.slots, BodyGenes("tetrapod", (p, b.params[1], b.params[2], b.params[3]))))
        labels.append(f"posture {p:.2f}")
    body.append(_row(sweep, 20, y, labels))
    y += CELL

    # -- Exhibit 2: populations ------------------------------------------------
    y += 24
    body.append(_heading("Exhibit 2 — Random populations per creature type", y))
    y += 8
    rows = (
        ("upright bipeds", [_creature(rng, "tetrapod", posture=rng.uniform(0.0, 0.15)) for _ in range(7)]),
        ("monkey-types", [_creature(rng, "tetrapod", posture=rng.uniform(0.42, 0.62), limb=rng.uniform(0.65, 1.0)) for _ in range(7)]),
        ("quadrupeds", [_creature(rng, "tetrapod", posture=rng.uniform(0.85, 1.0)) for _ in range(7)]),
        ("blobs", [_creature(rng, "blob") for _ in range(7)]),
        ("serpentines", [_creature(rng, "serpentine") for _ in range(7)]),
        ("winged", [_creature(rng, "winged") for _ in range(7)]),
    )
    for name, genomes in rows:
        body.append(_row(genomes, 20, y, [name] * 7))
        y += CELL

    # -- Exhibit 3: cross-plan splice -------------------------------------------
    y += 24
    body.append(_heading("Exhibit 3 — Cross-plan splice: blob x upright biped — five children", y))
    y += 8
    blob = _creature(rng, "blob")
    biped = _creature(rng, "tetrapod", posture=0.05, limb=0.4)
    cells, labels = [blob, biped], ["parent A: blob", "parent B: biped"]
    for i in range(5):
        child = splice(blob, biped, rng)
        cells.append(child)
        labels.append(f"child {i + 1}: {child.body.plan}")
    body.append(_row(cells, 20, y, labels))
    y += CELL

    # -- Exhibit 4: the atavism -------------------------------------------------
    y += 24
    body.append(_heading("Exhibit 4 — Atavism: serpentine carries silent leg genes; a cross to winged re-expresses them", y))
    y += 8
    serp = _creature(rng, "serpentine")
    wing = _creature(rng, "winged")
    cells, labels = [serp, wing], ["parent A: serpentine", "parent B: winged"]
    shown = 0
    while shown < 5:
        child = splice(serp, wing, rng)
        cells.append(child)
        labels.append(f"child {shown + 1}: {child.body.plan}")
        shown += 1
    body.append(_row(cells, 20, y, labels))
    y += CELL + 20

    width = 20 + 7 * CELL + 20
    doc = svg_document("".join(body), width, y,
                       title="MadDr.MCs — body plans: blob, biped, monkey-type, quadruped (seed %d)" % seed)
    here = os.path.dirname(__file__) or "."
    os.makedirs(os.path.join(here, "out"), exist_ok=True)
    path = os.path.join(here, "out", "creatures.svg")
    with open(path, "w") as f:
        f.write(doc)
    print(f"wrote {path} ({width}x{y})")
    return path


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2026)
