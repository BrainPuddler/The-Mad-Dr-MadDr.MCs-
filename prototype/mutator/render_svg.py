"""Procedural ink-sketch SVG rendering of part alleles.

Each family's draw function is a pure function of the allele (deterministic
assembly, docs/08): identity invariants are hard-coded in the drawing logic;
the six expressed axes drive everything that varies. This 2D renderer is the
prototype stand-in for the 3D socketed-part pipeline.

All parts draw inside a 100x100 cell, anchored near the bottom-center.
"""

import math

from catalog import express

INK = "#2f2a26"
PALE = "#efe7d4"
ACCENT = "#8c4a3c"


def _e(allele):
    """Expressed phenotype values for all six axes."""
    from genome import AXES
    return {a: express(allele.family, a, allele.params[i]) for i, a in enumerate(AXES)}


def _path(d, fill=PALE, width=2.0, stroke=INK):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" stroke-linejoin="round"/>'


def _circle(cx, cy, r, fill=PALE, width=1.6):
    return f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r:.1f}" fill="{fill}" stroke="{INK}" stroke-width="{width}"/>'


def _digit(bx, by, angle, length, base_w, tip_w, bend):
    """A curved tapering talon as a closed two-curve path."""
    dx, dy = math.cos(angle), math.sin(angle)
    px, py = -dy, dx                       # perpendicular
    tipx = bx + dx * length + px * bend * length
    tipy = by + dy * length + py * bend * length
    mx, my = bx + dx * length * 0.55, by + dy * length * 0.55
    c1x, c1y = mx + px * (bend * length * 0.6 + base_w * 0.5), my + py * (bend * length * 0.6 + base_w * 0.5)
    c2x, c2y = mx + px * (bend * length * 0.6 - base_w * 0.5), my + py * (bend * length * 0.6 - base_w * 0.5)
    blx, bly = bx + px * base_w * 0.5, by + py * base_w * 0.5
    brx, bry = bx - px * base_w * 0.5, by - py * base_w * 0.5
    return (f"M {blx:.1f} {bly:.1f} Q {c1x:.1f} {c1y:.1f} {tipx:.1f} {tipy:.1f} "
            f"Q {c2x:.1f} {c2y:.1f} {brx:.1f} {bry:.1f} Z")


def draw_claw_hand(allele):
    e = _e(allele)
    out = []
    prx, pry = 13 + 9 * e["girth"], 9 + 7 * e["girth"]
    cx, cy = 50, 82
    n = 2 + round(e["count"] * 3)                      # 2..5 talons (invariant range)
    length = 22 + 36 * e["length"]
    base_w = 4 + 8 * e["girth"]
    tip_w = base_w * (1 - 0.85 * e["taper"])
    bend = (e["curl"] - 0.5) * 1.1
    spread = math.radians(28 + 16 * e["girth"])
    for i in range(n):
        t = (i / (n - 1) - 0.5) if n > 1 else 0.0
        ang = -math.pi / 2 + t * spread * 2
        bx = cx + prx * 0.8 * math.sin(ang + math.pi / 2) * 0.9
        by = cy - pry * 0.6
        out.append(_path(_digit(bx, by, ang, length, base_w, tip_w, bend)))
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{prx:.1f}" ry="{pry:.1f}" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2"/>')
    # ornament: knuckle spikes
    for i in range(round(e["ornament"] * 5)):
        ox = cx - prx * 0.7 + i * (1.4 * prx / 4)
        out.append(_path(f"M {ox:.1f} {cy + pry - 1:.1f} l 2.5 6 l 2.5 -6 Z", fill=INK, width=1))
    return out


def draw_pincer(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 80
    r = 18 + 18 * e["length"]
    w = 5 + 9 * e["girth"]
    gap = 6 + 16 * (1 - e["curl"])                     # curl closes the jaws
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{12 + 6 * e["girth"]:.1f}" ry="{9 + 4 * e["girth"]:.1f}" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2"/>')
    for side in (-1, 1):                               # two opposing crescents (invariant)
        sx = cx + side * 8
        tipx, tipy = cx + side * gap * 0.5, cy - r
        c_out_x = sx + side * (r * 0.95)
        c_in_x = sx + side * (r * 0.95 - w * 2.2)
        d = (f"M {sx:.1f} {cy - 4:.1f} "
             f"Q {c_out_x:.1f} {cy - r * 0.65:.1f} {tipx:.1f} {tipy:.1f} "
             f"Q {c_in_x:.1f} {cy - r * 0.55:.1f} {sx + side * (w * 0.4):.1f} {cy - 2:.1f} Z")
        out.append(_path(d))
    for i in range(round(e["ornament"] * 4)):          # shell studs
        out.append(_circle(cx - 6 + i * 4.5, cy + 1, 1.3, fill=INK, width=0.8))
    return out


def draw_tentacle(allele):
    e = _e(allele)
    out = []
    bx, by = 50, 92
    length = 50 + 38 * e["length"]
    w0 = 6 + 12 * e["girth"]
    curl = (e["curl"] - 0.35) * 2.2
    # sample a curling spine, offset by tapering width
    pts_l, pts_r, spine = [], [], []
    for i in range(13):
        t = i / 12
        ang = -math.pi / 2 + curl * t * t * 2.2
        if i == 0:
            x, y = bx, by
        else:
            x = spine[-1][0] + math.cos(ang) * (length / 12)
            y = spine[-1][1] + math.sin(ang) * (length / 12)
        spine.append((x, y))
        w = w0 * (1 - e["taper"] * t) * 0.5 + 0.6
        px, py = -math.sin(ang), math.cos(ang)
        pts_l.append((x + px * w, y + py * w))
        pts_r.append((x - px * w, y - py * w))
    d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts_l + pts_r[::-1]) + " Z"
    out.append(_path(d))
    for i in range(2, 12, max(1, round(4 - e["ornament"] * 3))):  # suckers
        x, y = spine[i]
        out.append(_circle(x, y, 1.5 + e["girth"] * 1.5, fill=ACCENT, width=0.8))
    return out


def draw_antenna(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 92
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="11" ry="6" fill="{PALE}" stroke="{INK}" stroke-width="2"/>')
    length = 38 + 44 * e["length"]
    bend = 14 + 40 * e["curl"]
    segs = 2 + round(e["count"] * 5)
    for side in (-1, 1):                               # paired stalks (invariant)
        x0, y0 = cx + side * 5, cy - 4
        x1, y1 = cx + side * bend, cy - length * 0.55
        x2, y2 = cx + side * (bend * 0.7), cy - length
        out.append(_path(f"M {x0} {y0} Q {x1:.1f} {y1:.1f} {x2:.1f} {y2:.1f}",
                         fill="none", width=1.6 + 3.5 * e["girth"]))
        for i in range(1, segs):                       # segment ticks
            t = i / segs
            qx = (1 - t) ** 2 * x0 + 2 * (1 - t) * t * x1 + t ** 2 * x2
            qy = (1 - t) ** 2 * y0 + 2 * (1 - t) * t * y1 + t ** 2 * y2
            out.append(_circle(qx, qy, 1.0 + 1.2 * e["girth"], fill=INK, width=0.5))
        out.append(_circle(x2, y2, 2.5 + 5 * e["girth"] + 2 * e["ornament"], fill=ACCENT))  # tip bulb (invariant)
    return out


def draw_horn(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 92
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="13" ry="6" fill="{PALE}" stroke="{INK}" stroke-width="2"/>')
    h = 38 + 42 * e["length"]
    bw = 8 + 12 * e["girth"]
    lean = (e["curl"] - 0.35) * 40
    tipx, tipy = cx + lean, cy - h
    d = (f"M {cx - bw:.1f} {cy - 2} Q {cx - bw * 0.4 + lean * 0.5:.1f} {cy - h * 0.6:.1f} "
         f"{tipx:.1f} {tipy:.1f} Q {cx + bw * 0.6 + lean * 0.5:.1f} {cy - h * 0.6:.1f} {cx + bw:.1f} {cy - 2} Z")
    out.append(_path(d))
    for i in range(1, 2 + round(e["count"] * 5)):      # growth ridges
        t = i / (2 + round(e["count"] * 5))
        rx = cx + lean * t * t
        rw = bw * (1 - t * 0.8)
        out.append(_path(f"M {rx - rw:.1f} {cy - h * t:.1f} Q {rx:.1f} {cy - h * t - 3:.1f} {rx + rw:.1f} {cy - h * t:.1f}",
                         fill="none", width=1))
    return out


def draw_bug_eyes(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 60
    n = 3 + round(e["count"] * 6)                      # 3+ eyes (invariant)
    base_r = 5 + 7 * e["girth"]
    for i in range(n):                                 # spiral cluster
        ang = i * 2.4
        dist = 4 + 9 * math.sqrt(i) * (1 + 0.4 * e["length"])
        ex, ey = cx + math.cos(ang) * dist, cy + math.sin(ang) * dist * 0.8
        r = max(2.0, base_r * (1 - e["taper"] * 0.5) ** i if i < 3 else base_r * 0.55)
        out.append(_circle(ex, ey, r))
        out.append(_circle(ex + r * 0.25, ey - r * 0.15, max(0.8, r * 0.32), fill=INK))
        if e["ornament"] > 0.45:                       # lashes
            for k in (-0.6, 0, 0.6):
                lx, ly = ex + math.cos(-math.pi / 2 + k) * r, ey + math.sin(-math.pi / 2 + k) * r
                out.append(_path(f"M {lx:.1f} {ly:.1f} l {math.cos(-math.pi / 2 + k) * 4:.1f} {math.sin(-math.pi / 2 + k) * 4:.1f}",
                                 fill="none", width=1))
    return out


def draw_cyclops_eye(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 58
    rx = 16 + 16 * e["girth"]
    ry = rx * (0.62 + 0.3 * e["length"] * 0.5)
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx:.1f}" ry="{ry:.1f}" fill="white" stroke="{INK}" stroke-width="2.4"/>')
    ir = ry * (0.45 + 0.3 * e["taper"])
    out.append(_circle(cx, cy, ir, fill=ACCENT, width=1.6))
    out.append(_circle(cx, cy, ir * 0.45, fill=INK))
    out.append(_path(f"M {cx - rx:.1f} {cy - ry * 0.55:.1f} Q {cx:.1f} {cy - ry * (1.25 + 0.5 * e['curl']):.1f} "
                     f"{cx + rx:.1f} {cy - ry * 0.55:.1f}", fill="none", width=2))   # the lid (invariant)
    for i in range(round(e["ornament"] * 6)):          # lashes
        t = (i + 0.5) / max(1, round(e["ornament"] * 6))
        lx = cx - rx + 2 * rx * t
        out.append(_path(f"M {lx:.1f} {cy - ry - 1:.1f} l 0 -5", fill="none", width=1.2))
    return out


def draw_stalk_eyes(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 92
    out.append(f'<ellipse cx="{cx}" cy="{cy}" rx="11" ry="6" fill="{PALE}" stroke="{INK}" stroke-width="2"/>')
    length = 30 + 44 * e["length"]
    bend = 10 + 34 * e["curl"]
    er = 4 + 7 * e["girth"]
    for side in (-1, 1):
        x0, y0 = cx + side * 5, cy - 4
        x1, y1 = cx + side * bend, cy - length * 0.6
        x2, y2 = cx + side * (bend * 0.8), cy - length
        out.append(_path(f"M {x0} {y0} Q {x1:.1f} {y1:.1f} {x2:.1f} {y2:.1f}",
                         fill="none", width=2 + 4 * e["girth"]))
        out.append(_circle(x2, y2, er, fill="white", width=1.8))   # eyeball at tip (invariant)
        out.append(_circle(x2 + er * 0.3, y2, max(1.0, er * 0.4), fill=INK))
    return out


DRAWERS = {
    "claw_hand": draw_claw_hand,
    "pincer": draw_pincer,
    "tentacle": draw_tentacle,
    "antenna": draw_antenna,
    "horn": draw_horn,
    "bug_eyes": draw_bug_eyes,
    "cyclops_eye": draw_cyclops_eye,
    "stalk_eyes": draw_stalk_eyes,
}


def render_part_cell(allele, x, y, label=None, cell=100):
    parts = "".join(DRAWERS[allele.family](allele))
    lab = (f'<text x="50" y="9" font-size="7.5" text-anchor="middle" fill="{INK}" '
           f'font-family="Georgia, serif" font-style="italic">{label}</text>') if label else ""
    return f'<g transform="translate({x},{y})">{lab}{parts}</g>'


def svg_document(body, width, height, title=""):
    head = (f'<text x="{width / 2}" y="26" font-size="16" text-anchor="middle" fill="{INK}" '
            f'font-family="Georgia, serif">{title}</text>') if title else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
            f'viewBox="0 0 {width} {height}">'
            f'<rect width="{width}" height="{height}" fill="#f6efe0"/>{head}{body}</svg>')
