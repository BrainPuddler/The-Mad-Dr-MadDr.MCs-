"""Three factions, one engine: out/factions.svg + narrative logs.

Exhibit 1  the same genome/renderer expressing three factions: human
           troopers (issued tech: rifles, visors, piston legs), alien
           drones (flesh mixed with grown biotech), a monster brute
Exhibit 2  HUMANS -- a barrage breaks one squad; the routed RALLY back
           when the guns fall silent (soldiers run, they don't go feral)
Exhibit 3  ALIENS -- the same barrage cannot crack hive cohesion; then
           the Queen is slain and the hive collapses into frenzy at once

Run: python3 demo_factions.py [seed]
"""

import os
import random
import sys

from command import step, kill, ROUT, FERAL, CONTROLLED
from factions import human_platoon, alien_hive, alien_queen, alien_drone, monster_brute, human_trooper
from render_creature import render_creature_cell
from render_svg import svg_document, INK, ACCENT

TICKS = 60
BARRAGE = (10, 28)
QUEEN_DEATH = 38
CELL = 152


# ---------------------------------------------------------------- scenarios

def run_humans(seed):
    rng = random.Random(seed)
    units, captain, (squad_a, squad_b) = human_platoon(rng)
    narrative = []
    for t in range(TICKS):
        stress = {}
        if BARRAGE[0] <= t < BARRAGE[1]:
            stress = {u.name: 0.14 for u in squad_a}        # squad A takes the shelling
            stress.update({u.name: 0.02 for u in squad_b})  # squad B is dug in
        for e in step(units, rng, stress=stress):
            narrative.append((t, e))
    return units, narrative


def run_aliens(seed):
    rng = random.Random(seed)
    units, queen, drones = alien_hive(rng, n_drones=10)
    narrative = []
    for t in range(TICKS):
        stress = {}
        if BARRAGE[0] <= t < BARRAGE[1]:
            stress = {d.name: 0.14 for d in drones[:5]}     # same shelling, same side of the line
            stress.update({d.name: 0.02 for d in drones[5:]})
        if t == QUEEN_DEATH:
            ev = []
            kill(queen, units, ev)
            narrative.append((t, "** The Queen is slain **"))
            for e in ev:
                narrative.append((t, e))
        for e in step(units, rng, stress=stress):
            narrative.append((t, e))
    return units, narrative


# ---------------------------------------------------------------- rendering

W = 1100
PLOT_W, PLOT_H, PLOT_X = 760, 240, 80


def _series_panel(s, title, units, y0, snap_label, snap_states, extra=None):
    x0 = PLOT_X

    def X(t):
        return x0 + PLOT_W * t / (TICKS - 1)

    def Y(v):
        return y0 + PLOT_H * (1.0 - v)

    s.append(f'<text x="{x0 - 60}" y="{y0 - 12}" font-size="12" fill="{INK}" '
             f'font-family="Georgia, serif" font-weight="bold">{title}</text>')
    s.append(f'<rect x="{x0}" y="{y0}" width="{PLOT_W}" height="{PLOT_H}" '
             f'fill="white" stroke="{INK}" stroke-width="1.2"/>')
    s.append(f'<rect x="{X(BARRAGE[0]):.1f}" y="{y0}" width="{X(BARRAGE[1]) - X(BARRAGE[0]):.1f}" '
             f'height="{PLOT_H}" fill="#e8cfc8" opacity="0.5"/>')
    s.append(f'<text x="{(X(BARRAGE[0]) + X(BARRAGE[1])) / 2:.1f}" y="{y0 - 3}" font-size="8.5" '
             f'text-anchor="middle" fill="{ACCENT}" font-family="Georgia, serif">barrage</text>')
    if extra:
        extra(s, X, Y)
    for v, lab in ((1.0, "1.0"), (0.45, "waver"), (0.20, "snap"), (0.0, "0")):
        s.append(f'<line x1="{x0}" y1="{Y(v):.1f}" x2="{x0 + PLOT_W}" y2="{Y(v):.1f}" '
                 f'stroke="{INK}" stroke-width="0.45" stroke-dasharray="2 4"/>')
        s.append(f'<text x="{x0 - 6}" y="{Y(v) + 3:.1f}" font-size="8" text-anchor="end" '
                 f'fill="{INK}" font-family="Georgia, serif">{lab}</text>')

    palette = ["#8c4a3c", "#3c6e8c", "#6e8c3c", "#8c3c6e", "#b07a1f", "#3c8c7a",
               "#7a3c8c", "#4a6e3c", "#6e3c4a", "#3c4a8c"]
    followers = [u for u in units if u.history]
    for i, u in enumerate(followers):
        color = palette[i % len(palette)]
        pts = " ".join(f"{X(t):.1f},{Y(v):.1f}" for t, v in enumerate(u.history[:TICKS]))
        s.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="1.5" '
                 f'stroke-opacity="0.85"/>')
    # snap markers
    marked = sum(1 for u in units if u.state in snap_states)
    s.append(f'<text x="{x0 + PLOT_W + 10}" y="{y0 + 14}" font-size="9" fill="{INK}" '
             f'font-family="Georgia, serif">{snap_label}: {marked}</text>')
    rallied = sum(1 for u in units if u.state == CONTROLLED and u.commander is not None)
    s.append(f'<text x="{x0 + PLOT_W + 10}" y="{y0 + 30}" font-size="9" fill="{INK}" '
             f'font-family="Georgia, serif">in line at end: {rallied}</text>')


def main(seed=2026):
    rng = random.Random(seed)
    s = []
    s.append(f'<text x="{W / 2}" y="34" font-size="16" text-anchor="middle" fill="{INK}" '
             f'font-family="Georgia, serif">MadDr.MCs — three factions, one engine</text>')

    # Exhibit 1: faction lineup, rendered from genomes
    y = 56
    s.append(f'<text x="20" y="{y}" font-size="12" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">Exhibit 1 — One genome system, three catalogs: '
             f'issued tech, grown biotech, stitched flesh</text>')
    y += 8
    lineup = [
        (human_trooper("trooper", rng, pos=(0, 0)), "human trooper"),
        (human_trooper("trooper", rng, pos=(0, 0)), "human trooper"),
        (alien_drone("drone", rng, pos=(0, 0)), "alien drone"),
        (alien_drone("drone", rng, pos=(0, 0)), "alien drone"),
        (alien_queen("queen", rng, pos=(0, 0)), "the Queen"),
        (monster_brute("brute", rng, pos=(0, 0)), "monster brute"),
        (monster_brute("brute", rng, pos=(0, 0)), "monster brute"),
    ]
    for i, (u, label) in enumerate(lineup):
        s.append(render_creature_cell(u.genome, 20 + i * CELL, y, label=label))
    y += CELL + 30

    # Exhibit 2: humans
    s.append(f'<text x="20" y="{y}" font-size="12" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">Exhibit 2 — Humans: morale breaks, soldiers rout… and rally when the guns stop</text>')
    y += 26
    h_units, h_narr = run_humans(seed)
    _series_panel(s, "morale", [u for u in h_units if u.name.startswith("Pvt")], y,
                  "routed during run", (ROUT,))
    y += PLOT_H + 44

    # Exhibit 3: aliens
    s.append(f'<text x="20" y="{y}" font-size="12" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">Exhibit 3 — Aliens: hive cohesion shrugs off the barrage; kill the Queen and it all ends at once</text>')
    y += 26
    a_units, a_narr = run_aliens(seed)

    def queen_marker(s2, X, Y):
        s2.append(f'<line x1="{X(QUEEN_DEATH):.1f}" y1="{Y(1.0):.1f}" x2="{X(QUEEN_DEATH):.1f}" '
                  f'y2="{Y(0.0):.1f}" stroke="{ACCENT}" stroke-width="1.6" stroke-dasharray="4 3"/>')
        s2.append(f'<text x="{X(QUEEN_DEATH):.1f}" y="{Y(1.0) - 3:.1f}" font-size="8.5" '
                  f'text-anchor="middle" fill="{ACCENT}" font-family="Georgia, serif">Queen slain</text>')

    _series_panel(s, "loyalty", [u for u in a_units if u.name.startswith("Drone")], y,
                  "feral at end", (FERAL,), extra=queen_marker)
    y += PLOT_H + 40

    doc = svg_document("".join(s), W, y, title="")
    here = os.path.dirname(__file__) or "."
    os.makedirs(os.path.join(here, "out"), exist_ok=True)
    path = os.path.join(here, "out", "factions.svg")
    with open(path, "w") as f:
        f.write(doc)

    print(f"\n=== Humans (seed {seed}) ===")
    for t, e in h_narr:
        print(f"[t={t:02d}] {e}")
    print(f"\n=== Aliens (seed {seed}) ===")
    for t, e in a_narr:
        print(f"[t={t:02d}] {e}")
    print(f"\nwrote {path}")
    return path


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2026)
