"""Generate the part-genetics gallery: out/gallery.svg.

Three exhibits, one per strategy claim in docs/15-part-genetics.md:
  1. Variation within a family  -- different yet recognizable
  2. Mutation lineage           -- gradual drift, identity preserved
  3. Splice inheritance         -- shared axes make children resemble parents

Run: python3 demo.py [seed]
"""

import os
import random
import sys

from genome import Genome, PartAllele
from operators import mutate, splice
from render_svg import render_part_cell, svg_document, INK

CELL = 104


def _row(alleles, x0, y, labels=None):
    return "".join(
        render_part_cell(a, x0 + i * CELL, y, label=(labels[i] if labels else None))
        for i, a in enumerate(alleles)
    )


def _heading(text, y, x=20):
    return (f'<text x="{x}" y="{y}" font-size="12" fill="{INK}" '
            f'font-family="Georgia, serif" font-weight="bold">{text}</text>')


def main(seed=2026):
    rng = random.Random(seed)
    body, y = [], 44

    # -- Exhibit 1: variation within families --------------------------------
    body.append(_heading("Exhibit 1 — Eight random genomes per family: different, yet recognizable", y))
    y += 8
    for fam in ("claw_hand", "tentacle", "pincer", "antenna", "bug_eyes", "stalk_eyes",
                "hoofed_leg", "talon_leg", "insect_leg"):
        alleles = [PartAllele(fam, tuple(rng.random() for _ in range(6))) for _ in range(8)]
        body.append(_row(alleles, 20, y, labels=[fam.replace("_", " ")] * 8))
        y += CELL

    # -- Exhibit 2: mutation lineage ------------------------------------------
    y += 26
    body.append(_heading("Exhibit 2 — Mutation lineage: one claw, seven generations of drift", y))
    y += 8
    g = Genome((("hand", PartAllele("claw_hand", (0.5, 0.5, 0.5, 0.5, 0.5, 0.3))),))
    lineage, labels = [g.get("hand")], ["parent"]
    for i in range(7):
        g = mutate(g, rng, family_jump=0.0)   # pure drift: identity never jumps
        lineage.append(g.get("hand"))
        labels.append(f"gen {i + 1}")
    body.append(_row(lineage, 20, y, labels=labels))
    y += CELL

    # -- Exhibit 3: splice inheritance ----------------------------------------
    y += 26
    body.append(_heading("Exhibit 3 — Splice: tentacle (long, thin, curled) x claw (stubby, thick) — six children", y))
    y += 8
    tent = Genome((("hand", PartAllele("tentacle", (0.95, 0.10, 0.85, 0.90, 0.5, 0.6))),))
    claw = Genome((("hand", PartAllele("claw_hand", (0.10, 0.90, 0.25, 0.20, 0.9, 0.5))),))
    cells = [tent.get("hand"), claw.get("hand")]
    labels = ["parent A: tentacle", "parent B: claw"]
    for i in range(6):
        child = splice(tent, claw, rng).get("hand")
        cells.append(child)
        labels.append(f"child {i + 1}: {child.family.replace('_', ' ')}")
    body.append(_row(cells, 20, y, labels=labels))
    y += CELL + 20

    width = 20 + 8 * CELL + 20
    doc = svg_document("".join(body), width, y,
                       title="MadDr.MCs — Mutator part-genetics prototype (seed %d)" % seed)
    os.makedirs(os.path.join(os.path.dirname(__file__) or ".", "out"), exist_ok=True)
    path = os.path.join(os.path.dirname(__file__) or ".", "out", "gallery.svg")
    with open(path, "w") as f:
        f.write(doc)
    print(f"wrote {path} ({width}x{y})")
    return path


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2026)
