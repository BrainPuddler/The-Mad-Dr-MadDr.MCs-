"""Berserk scenario -- the werewolf problem: out/berserk.svg + narrative log.

A houndmaster commands Lupex (a high-fury, lunar-tempered bruiser) and two
packmates. A skirmish breaks out in the evening and runs into the Night.
Under the moon, Lupex's rage outruns its loyalty: it goes berserk -- harder
to kill and hitting far harder -- but it savages whatever is nearest,
including its own pack. At dawn the fury burns out and the houndmaster
re-asserts control over whatever is left.

Run: python3 demo_berserk.py [seed]
"""

import os
import random
import sys

from genome import BrainGenes
from command import (Unit, assign, step, berserk_threshold, berserk_power_mult,
                     berserk_armor_bonus, BERSERK, EXHAUSTED)
from render_svg import svg_document, INK, ACCENT

TICKS = 75
NIGHT = (25, 45)           # the Night phase (docs/03 Lumen Cycle)
SKIRMISH = (22, 32)        # combat stress window -- evening into the Night


def brain(tier, command, will, temperament, guile, fury):
    return BrainGenes(tier, (command, will, temperament, guile, fury))


def build():
    master = Unit("Houndmaster", brain("gifted", 0.80, 0.15, 0.20, 0.10, 0.05), pos=(48, 49))
    lupex = Unit("Lupex", brain("average", 0.25, 0.50, 0.60, 0.20, 0.85), pos=(53, 50))
    hound1 = Unit("Hound-1", brain("dim", 0.10, 0.15, 0.30, 0.10, 0.20), pos=(54, 51))
    hound2 = Unit("Hound-2", brain("dim", 0.10, 0.20, 0.30, 0.10, 0.20), pos=(52, 53))
    units = [master, lupex, hound1, hound2]
    for u in units[1:]:
        assign(master, u)
    return units, master, lupex


def lumen(t):
    return "night" if NIGHT[0] <= t < NIGHT[1] else "day"


def run(seed):
    rng = random.Random(seed)
    units, master, lupex = build()
    narrative = []
    for t in range(TICKS):
        stress = {}
        if SKIRMISH[0] <= t < SKIRMISH[1]:
            stress = {"Lupex": 0.025, "Hound-1": 0.01, "Hound-2": 0.01}
        for e in step(units, rng, stress=stress, lumen=lumen(t)):
            narrative.append((t, e))
    return units, master, lupex, narrative


# ----------------------------------------------------------------------------
W, H = 940, 540
PLOT = dict(x=70, y=70, w=700, h=290)


def _x(t):
    return PLOT["x"] + PLOT["w"] * t / (TICKS - 1)


def _y(v):
    return PLOT["y"] + PLOT["h"] * (1.0 - v)


def render(units, lupex):
    p = PLOT
    s = []
    s.append(f'<text x="{W/2}" y="34" font-size="16" text-anchor="middle" fill="{INK}" '
             f'font-family="Georgia, serif">MadDr.MCs — berserk: the werewolf problem</text>')
    s.append(f'<text x="{W/2}" y="52" font-size="9.5" text-anchor="middle" fill="{INK}" '
             f'font-family="Georgia, serif" font-style="italic">a skirmish runs into the Night; '
             f'Lupex (fury {lupex.fury:.2f}) outgrows its leash</text>')
    s.append(f'<rect x="{p["x"]}" y="{p["y"]}" width="{p["w"]}" height="{p["h"]}" '
             f'fill="white" stroke="{INK}" stroke-width="1.2"/>')

    # night band + skirmish band
    s.append(f'<rect x="{_x(NIGHT[0]):.1f}" y="{p["y"]}" width="{_x(NIGHT[1])-_x(NIGHT[0]):.1f}" '
             f'height="{p["h"]}" fill="#cfd3e8" opacity="0.45"/>')
    s.append(f'<text x="{(_x(NIGHT[0])+_x(NIGHT[1]))/2:.1f}" y="{p["y"]-4}" font-size="8.5" '
             f'text-anchor="middle" fill="{INK}" font-family="Georgia, serif">☾ Night</text>')
    s.append(f'<line x1="{_x(SKIRMISH[0]):.1f}" y1="{p["y"]+p["h"]}" x2="{_x(SKIRMISH[1]):.1f}" '
             f'y2="{p["y"]+p["h"]}" stroke="{ACCENT}" stroke-width="3"/>')
    s.append(f'<text x="{(_x(SKIRMISH[0])+_x(SKIRMISH[1]))/2:.1f}" y="{p["y"]+p["h"]+14}" '
             f'font-size="8.5" text-anchor="middle" fill="{ACCENT}" '
             f'font-family="Georgia, serif">skirmish (combat stress)</text>')

    # berserk threshold line for Lupex
    th = berserk_threshold(lupex.brain)
    s.append(f'<line x1="{p["x"]}" y1="{_y(th):.1f}" x2="{p["x"]+p["w"]}" y2="{_y(th):.1f}" '
             f'stroke="{ACCENT}" stroke-width="0.9" stroke-dasharray="5 3"/>')
    s.append(f'<text x="{p["x"]+p["w"]+4}" y="{_y(th)+3:.1f}" font-size="7.5" fill="{ACCENT}" '
             f'font-family="Georgia, serif">berserk at {th:.2f}</text>')
    for v in (0.0, 0.5, 1.0):
        s.append(f'<line x1="{p["x"]}" y1="{_y(v):.1f}" x2="{p["x"]+p["w"]}" y2="{_y(v):.1f}" '
                 f'stroke="{INK}" stroke-width="0.4" stroke-dasharray="2 4"/>')
        s.append(f'<text x="{p["x"]-6}" y="{_y(v)+3:.1f}" font-size="8" text-anchor="end" '
                 f'fill="{INK}" font-family="Georgia, serif">{v:.1f}</text>')

    series = [
        ("Lupex rage", ACCENT, 2.6, lupex.rage_history),
        ("Lupex loyalty", "#3c6e8c", 1.6, lupex.history),
    ]
    for u in units:
        if u.name.startswith("Hound-"):
            series.append((f"{u.name} hp", "#6e8c3c" if u.name.endswith("1") else "#8c3c6e", 1.6,
                           [h / 100.0 for h in u.hp_history]))
    for label, color, width, hist in series:
        pts = " ".join(f"{_x(t):.1f},{_y(v):.1f}" for t, v in enumerate(hist[:TICKS]))
        s.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="{width}"/>')

    # legend
    lx, ly = p["x"] + p["w"] + 16, p["y"] + 26
    s.append(f'<text x="{lx}" y="{ly - 14}" font-size="9" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">series</text>')
    for i, (label, color, width, _) in enumerate(series):
        yy = ly + i * 15
        s.append(f'<line x1="{lx}" y1="{yy-3}" x2="{lx+16}" y2="{yy-3}" stroke="{color}" stroke-width="{width+0.6}"/>')
        s.append(f'<text x="{lx+21}" y="{yy}" font-size="8.2" fill="{INK}" '
                 f'font-family="Georgia, serif">{label}</text>')

    # outcome lines
    fy = p["y"] + p["h"] + 40
    s.append(f'<text x="{p["x"]}" y="{fy}" font-size="11" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">Outcome</text>')
    fy += 15
    s.append(f'<text x="{p["x"]}" y="{fy}" font-size="9" fill="{INK}" font-family="Georgia, serif">'
             f'▸ berserk Lupex: ×{berserk_power_mult(lupex.brain):.2f} power, '
             f'+{berserk_armor_bonus(lupex.brain):.1f} armor — and no friend on the field</text>')
    fy += 14
    for u in units:
        if u.name.startswith("Hound-") or u.name == "Houndmaster":
            fate = "slain by its own packmate" if not u.alive else f"survives at {u.hp:.0f} hp"
            s.append(f'<text x="{p["x"]}" y="{fy}" font-size="9" fill="{INK}" '
                     f'font-family="Georgia, serif">▸ {u.name}: {fate}</text>')
            fy += 14
    lup_fate = lupex.state if lupex.alive else "dead"
    s.append(f'<text x="{p["x"]}" y="{fy}" font-size="9" fill="{INK}" font-family="Georgia, serif">'
             f'▸ Lupex at dawn: {lup_fate}, back under the Houndmaster'
             f'{"" if lupex.commander else " -- masterless"}</text>')
    return "".join(s)


def main(seed=2026):
    global units
    units, master, lupex, narrative = run(seed)

    print(f"\n=== Berserk scenario: the werewolf problem (seed {seed}) ===")
    print(f"Lupex: fury {lupex.fury:.2f}, berserk threshold {berserk_threshold(lupex.brain):.2f}, "
          f"x{berserk_power_mult(lupex.brain):.2f} power, +{berserk_armor_bonus(lupex.brain):.1f} armor when berserk")
    last_t = -1
    for t, e in narrative:
        if t != last_t:
            print(f"[t={t:02d}] ({lumen(t)})")
            last_t = t
        print(f"        {e}")

    body = render(units, lupex)
    here = os.path.dirname(__file__) or "."
    os.makedirs(os.path.join(here, "out"), exist_ok=True)
    path = os.path.join(here, "out", "berserk.svg")
    with open(path, "w") as f:
        f.write(svg_document(body, W, H))
    print(f"\nwrote {path}")
    return path


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2026)
