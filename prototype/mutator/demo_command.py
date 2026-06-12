"""Command & rebellion scenario: out/command.svg + a narrative log.

A three-tier chain of command under one Mastermind overlord. We run it for
70 ticks and assassinate the overlord at tick 35 to show the cascade:
decapitation shock, willful grunts breaking free, and an ambitious lieutenant
leading a breakaway warband.

Run: python3 demo_command.py [seed]
"""

import os
import random
import sys

from genome import BrainGenes
from command import (Unit, assign, step, kill, capacity, cost, radius,
                     WAVER, REBEL, FERAL, REBEL_STATE, WAVERING)
from render_svg import svg_document, INK, PALE, ACCENT

DEATH_TICK = 35
TICKS = 70


def brain(tier, command, will, temperament, guile, fury=0.1):
    return BrainGenes(tier, (command, will, temperament, guile, fury))


def build():
    # tier,        cmd  will temp guile
    over = Unit("Overmind", brain("mastermind", 0.92, 0.10, 0.20, 0.25), pos=(50, 50))
    vaska = Unit("Lt. Vaska", brain("gifted", 0.62, 0.30, 0.30, 0.15), pos=(46, 52))   # loyal
    mordax = Unit("Lt. Mordax", brain("gifted", 0.70, 0.55, 0.55, 0.85), pos=(54, 48))  # schemer
    units = [over, vaska, mordax]

    grunts = []
    specs = [
        ("Grub-1", vaska, 0.20, (47, 54)),
        ("Grub-2", vaska, 0.35, (45, 50)),
        ("Snark",  vaska, 0.90, (66, 60)),   # willful AND far out of range -> early break
        ("Grub-4", mordax, 0.25, (55, 46)),
        ("Grub-5", mordax, 0.30, (56, 50)),
        ("Brute-6", mordax, 0.55, (53, 45)),
    ]
    for name, boss, will, pos in specs:
        guile = 0.6 if name == "Snark" else 0.2
        g = Unit(name, brain("dim", 0.15, will, 0.4, guile), pos=pos)
        grunts.append(g)
        units.append(g)

    assign(over, vaska); assign(over, mordax)
    for name, boss, *_ in specs:
        assign(boss, next(u for u in grunts if u.name == name))
    return units, over


def run(seed):
    rng = random.Random(seed)
    units, over = build()
    tracked = [u for u in units if u is not over]
    state_log = {u.name: [] for u in tracked}
    narrative = []

    for t in range(TICKS):
        if t == DEATH_TICK:
            ev = []
            kill(over, units, ev)
            narrative.append((t, f"** {over.name} is assassinated **"))
            for e in ev:
                narrative.append((t, e))
        # mild rising battlefield stress in the back half
        stress = {}
        if t > 45:
            stress = {u.name: 0.01 for u in tracked}
        for e in step(units, rng, stress=stress):
            narrative.append((t, e))
        for u in tracked:
            state_log[u.name].append(u.state if u.alive else "dead")

    return units, over, tracked, state_log, narrative


# ----------------------------------------------------------------------------
# rendering
PALETTE = ["#8c4a3c", "#3c6e8c", "#6e8c3c", "#8c3c6e", "#b07a1f", "#3c8c7a", "#7a3c8c"]
W, H = 940, 560
PLOT = dict(x=70, y=70, w=700, h=300)


def _x(t):
    return PLOT["x"] + PLOT["w"] * t / (TICKS - 1)


def _y(v):
    return PLOT["y"] + PLOT["h"] * (1.0 - v)


def render(units, over, tracked, state_log):
    s = []
    p = PLOT
    s.append(f'<text x="{W/2}" y="34" font-size="16" text-anchor="middle" fill="{INK}" '
             f'font-family="Georgia, serif">MadDr.MCs — command &amp; rebellion: loyalty over time</text>')
    s.append(f'<text x="{W/2}" y="52" font-size="9.5" text-anchor="middle" fill="{INK}" '
             f'font-family="Georgia, serif" font-style="italic">three-tier chain under one Mastermind; '
             f'the overlord is assassinated at tick {DEATH_TICK}</text>')

    # plot frame
    s.append(f'<rect x="{p["x"]}" y="{p["y"]}" width="{p["w"]}" height="{p["h"]}" '
             f'fill="white" stroke="{INK}" stroke-width="1.2"/>')
    # threshold bands
    s.append(f'<rect x="{p["x"]}" y="{_y(WAVER):.1f}" width="{p["w"]}" height="{_y(REBEL)-_y(WAVER):.1f}" '
             f'fill="#f0e2c8" opacity="0.6"/>')
    s.append(f'<rect x="{p["x"]}" y="{_y(REBEL):.1f}" width="{p["w"]}" height="{p["y"]+p["h"]-_y(REBEL):.1f}" '
             f'fill="#e8cfc8" opacity="0.6"/>')
    for v, lab in ((1.0, "1.0"), (WAVER, f"waver {WAVER:.2f}"), (REBEL, f"rebel {REBEL:.2f}"), (0.0, "0")):
        s.append(f'<line x1="{p["x"]}" y1="{_y(v):.1f}" x2="{p["x"]+p["w"]}" y2="{_y(v):.1f}" '
                 f'stroke="{INK}" stroke-width="0.5" stroke-dasharray="2 3"/>')
        s.append(f'<text x="{p["x"]-6}" y="{_y(v)+3:.1f}" font-size="8" text-anchor="end" '
                 f'fill="{INK}" font-family="Georgia, serif">{lab}</text>')
    # death marker
    s.append(f'<line x1="{_x(DEATH_TICK):.1f}" y1="{p["y"]}" x2="{_x(DEATH_TICK):.1f}" '
             f'y2="{p["y"]+p["h"]}" stroke="{ACCENT}" stroke-width="1.4" stroke-dasharray="4 3"/>')
    s.append(f'<text x="{_x(DEATH_TICK):.1f}" y="{p["y"]-4}" font-size="8.5" text-anchor="middle" '
             f'fill="{ACCENT}" font-family="Georgia, serif">overlord slain</text>')
    s.append(f'<text x="{p["x"]+p["w"]/2}" y="{p["y"]+p["h"]+24}" font-size="9" text-anchor="middle" '
             f'fill="{INK}" font-family="Georgia, serif">tick →</text>')

    # one polyline per tracked unit + state-change markers
    for i, u in enumerate(tracked):
        color = PALETTE[i % len(PALETTE)]
        pts = " ".join(f"{_x(t):.1f},{_y(v):.1f}" for t, v in enumerate(u.history[:TICKS]))
        s.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="1.8"/>')
        states = state_log[u.name]
        for t in range(1, len(states)):
            if states[t] != states[t-1] and states[t] in (WAVERING, FERAL, REBEL_STATE):
                glyph = {"wavering": "?", "feral": "✶", "rebel": "✦"}[states[t]]
                s.append(f'<text x="{_x(t):.1f}" y="{_y(u.history[t])-4:.1f}" font-size="10" '
                         f'text-anchor="middle" fill="{color}">{glyph}</text>')

    # legend
    lx, ly = p["x"] + p["w"] + 16, p["y"] + 4
    s.append(f'<text x="{lx}" y="{ly}" font-size="9" fill="{INK}" font-family="Georgia, serif" '
             f'font-weight="bold">units</text>')
    for i, u in enumerate(tracked):
        color = PALETTE[i % len(PALETTE)]
        yy = ly + 16 + i * 15
        s.append(f'<line x1="{lx}" y1="{yy-3}" x2="{lx+16}" y2="{yy-3}" stroke="{color}" stroke-width="2.4"/>')
        s.append(f'<text x="{lx+21}" y="{yy}" font-size="8.2" fill="{INK}" '
                 f'font-family="Georgia, serif">{u.name}</text>')
    s.append(f'<text x="{lx}" y="{ly+16+len(tracked)*15+10}" font-size="7.6" fill="{INK}" '
             f'font-family="Georgia, serif">? waver  ✶ feral  ✦ rebel</text>')

    # final factions summary
    fy = p["y"] + p["h"] + 56
    s.append(f'<text x="{p["x"]}" y="{fy}" font-size="11" fill="{INK}" '
             f'font-family="Georgia, serif" font-weight="bold">Final state — who marches under whom</text>')
    fy += 16
    leaders = [u for u in units if u.alive and u.commander is None]
    line = fy
    for u in units:
        if not u.alive:
            continue
        if u.commander is None:
            led = [s2.name for s2 in u.subordinates if s2.alive]
            tag = f"{u.name} ({u.brain.tier}, cmd {u.command:.2f})"
            desc = f"commands {', '.join(led)}" if led else ("roams FERAL" if u.state == FERAL else "stands alone")
            s.append(f'<text x="{p["x"]}" y="{line}" font-size="9" fill="{INK}" '
                     f'font-family="Georgia, serif">▸ {tag} — {desc}</text>')
            line += 15
    return "".join(s)


def main(seed=2026):
    units, over, tracked, state_log, narrative = run(seed)

    print(f"\n=== Command & rebellion scenario (seed {seed}) ===")
    print(f"Overlord {over.name}: capacity {capacity(over.brain):.1f}, radius {radius(over.brain):.1f}")
    last_t = -1
    for t, e in narrative:
        if t != last_t:
            print(f"[t={t:02d}]")
            last_t = t
        print(f"        {e}")
    print("\nFinal factions:")
    for u in units:
        if u.alive and u.commander is None:
            led = [s.name for s in u.subordinates if s.alive]
            print(f"  {u.name}: {('leads ' + ', '.join(led)) if led else u.state}")

    body = render(units, over, tracked, state_log)
    here = os.path.dirname(__file__) or "."
    os.makedirs(os.path.join(here, "out"), exist_ok=True)
    path = os.path.join(here, "out", "command.svg")
    with open(path, "w") as f:
        f.write(svg_document(body, W, H))
    print(f"\nwrote {path}")
    return path


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 2026)
