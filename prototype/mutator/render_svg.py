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


def leg_height(allele):
    """Drawn height of a leg part from its top anchor (50, 8) to the foot.

    Shared across all leg families so creature renderers can place hips:
    hip_y = GROUND - scale * leg_height(allele).
    """
    e = _e(allele)
    return 64 + 24 * e["length"]


def draw_hoofed_leg(allele):
    e = _e(allele)
    out = []
    x0, y0 = 50, 8
    h = leg_height(allele)
    w = 5 + 8 * e["girth"]
    bend = (e["curl"] - 0.3) * 18
    kx, ky = x0 + bend, y0 + h * 0.52
    fx, fy = x0 + bend * 0.4, y0 + h - 7
    out.append(_path(f"M {x0} {y0} Q {x0 + bend * 0.5:.1f} {y0 + h * 0.25:.1f} {kx:.1f} {ky:.1f} "
                     f"Q {kx:.1f} {ky + h * 0.2:.1f} {fx:.1f} {fy:.1f}",
                     fill="none", width=w))
    # cloven hoof (invariant)
    out.append(_path(f"M {fx - w * 0.9:.1f} {fy:.1f} L {fx - w * 0.7:.1f} {fy + 7:.1f} "
                     f"L {fx - 0.8:.1f} {fy + 7:.1f} L {fx - 0.8:.1f} {fy:.1f} Z", fill=INK, width=1))
    out.append(_path(f"M {fx + 0.8:.1f} {fy:.1f} L {fx + 0.8:.1f} {fy + 7:.1f} "
                     f"L {fx + w * 0.7:.1f} {fy + 7:.1f} L {fx + w * 0.9:.1f} {fy:.1f} Z", fill=INK, width=1))
    for i in range(round(e["ornament"] * 5)):          # shaggy fetlock strokes
        out.append(_path(f"M {kx - w * 0.5 + i * w * 0.25:.1f} {ky:.1f} l -2 7", fill="none", width=1))
    return out


def draw_talon_leg(allele):
    e = _e(allele)
    out = []
    x0, y0 = 50, 8
    h = leg_height(allele)
    w = 2.5 + 5 * e["girth"]
    bend = 10 + (e["curl"]) * 14
    kx, ky = x0 + bend, y0 + h * 0.45              # backward knee (invariant)
    ax, ay = x0 - bend * 0.3, y0 + h * 0.8
    fx, fy = x0 + 2, y0 + h - 4
    out.append(_path(f"M {x0} {y0} Q {x0 + bend * 0.7:.1f} {y0 + h * 0.2:.1f} {kx:.1f} {ky:.1f} "
                     f"Q {kx - bend * 0.5:.1f} {ky + h * 0.18:.1f} {ax:.1f} {ay:.1f} L {fx:.1f} {fy:.1f}",
                     fill="none", width=w))
    n = 2 + round(e["count"] * 2)                  # 2-4 splayed fore-toes (invariant: 3+ incl. hallux)
    for i in range(n):
        t = (i / (n - 1) - 0.5) if n > 1 else 0
        out.append(_path(_digit(fx, fy, math.pi / 2 * 0.15 + t * 1.5 + math.pi * 0.45,
                                7 + 6 * e["length"], 2.2, 0.8, 0.35)))
    out.append(_path(_digit(fx, fy, math.pi * 1.05, 5 + 3 * e["length"], 2.0, 0.7, -0.3)))  # hallux
    return out


def draw_insect_leg(allele):
    e = _e(allele)
    out = []
    x0, y0 = 50, 8
    h = leg_height(allele)
    w = 2 + 4 * e["girth"]
    spread = 12 + 16 * e["curl"]
    j1 = (x0 + spread, y0 + h * 0.3)               # coxa/femur joint
    j2 = (x0 + spread * 0.2, y0 + h * 0.62)        # femur/tibia joint
    j3 = (x0 + spread * 0.55, y0 + h - 5)          # tarsus
    out.append(_path(f"M {x0} {y0} L {j1[0]:.1f} {j1[1]:.1f} L {j2[0]:.1f} {j2[1]:.1f} "
                     f"L {j3[0]:.1f} {j3[1]:.1f}", fill="none", width=w))
    for (jx, jy) in (j1, j2):                      # joint nodes
        out.append(_circle(jx, jy, w * 0.8, fill=INK, width=0.6))
    out.append(_path(f"M {j3[0]:.1f} {j3[1]:.1f} q 5 2 7 6 q -5 -1 -7 1 Z", fill=INK, width=1))  # tarsal hook
    for i in range(round(e["ornament"] * 6)):      # spines
        t = i / 6
        sx = j1[0] + (j2[0] - j1[0]) * t
        sy = j1[1] + (j2[1] - j1[1]) * t
        out.append(_path(f"M {sx:.1f} {sy:.1f} l 4 -3", fill="none", width=1))
    return out


STEEL = "#7d8088"


def draw_rifle_arm(allele):
    e = _e(allele)
    out = []
    sx, sy = 50, 92                                   # shoulder anchor
    # arm: two segments to a grip position
    ex, ey = sx + 16, sy - 22                          # elbow
    gx, gy = sx + 30, sy - 34                          # grip
    out.append(_path(f"M {sx} {sy} L {ex} {ey} L {gx} {gy}", fill="none",
                     width=4.5 + 2.5 * e["girth"]))
    # the gun (invariant: barrel, stock, trigger group), pitched upward
    blen = 26 + 26 * e["length"]
    bw = 2.2 + 3.0 * e["girth"]
    out.append(f'<g transform="rotate(-18 {gx} {gy})">')
    out.append(f'<rect x="{gx - 12:.1f}" y="{gy - bw:.1f}" width="{12 + blen:.1f}" height="{bw * 2:.1f}" '
               f'fill="{STEEL}" stroke="{INK}" stroke-width="1.4"/>')                  # receiver+barrel
    out.append(f'<rect x="{gx + blen - 2:.1f}" y="{gy - bw * 0.45:.1f}" width="9" height="{bw * 0.9:.1f}" '
               f'fill="{INK}"/>')                                                       # muzzle
    out.append(f'<path d="M {gx - 12:.1f} {gy - bw:.1f} l -9 {bw * 2 + 7:.1f} l 7 0 l 5 -{bw + 2:.1f} Z" '
               f'fill="{STEEL}" stroke="{INK}" stroke-width="1.2"/>')                  # stock
    out.append(f'<path d="M {gx:.1f} {gy + bw:.1f} q 1 5 5 5" fill="none" stroke="{INK}" stroke-width="1.4"/>')  # trigger
    if e["ornament"] > 0.5:                            # scope
        out.append(f'<rect x="{gx + 4:.1f}" y="{gy - bw - 5:.1f}" width="14" height="4" rx="2" '
                   f'fill="{INK}"/>')
    for i in range(round(e["count"] * 4)):             # rail ticks
        out.append(f'<rect x="{gx + 2 + i * 5:.1f}" y="{gy - bw - 1.6:.1f}" width="2.6" height="1.6" fill="{INK}"/>')
    out.append('</g>')
    return out


def draw_piston_leg(allele):
    e = _e(allele)
    out = []
    x0, y0 = 50, 8
    h = leg_height(allele)
    cw = 5 + 6 * e["girth"]                            # cylinder width
    lean = (e["curl"]) * 10
    cyl_h = h * 0.45
    # cylinder (invariant)
    out.append(f'<rect x="{x0 - cw:.1f}" y="{y0:.1f}" width="{cw * 2:.1f}" height="{cyl_h:.1f}" rx="2.5" '
               f'fill="{STEEL}" stroke="{INK}" stroke-width="1.6"/>')
    for i in range(1, 2 + round(e["count"] * 3)):      # cooling rings
        yy = y0 + cyl_h * i / (2 + round(e["count"] * 3))
        out.append(f'<line x1="{x0 - cw:.1f}" y1="{yy:.1f}" x2="{x0 + cw:.1f}" y2="{yy:.1f}" '
                   f'stroke="{INK}" stroke-width="0.9"/>')
    # piston rod (invariant)
    rod_w = max(1.6, cw * 0.35 * (1 - 0.4 * e["taper"]))
    fx = x0 + lean
    out.append(f'<path d="M {x0:.1f} {y0 + cyl_h:.1f} L {fx:.1f} {y0 + h - 4:.1f}" '
               f'fill="none" stroke="{STEEL}" stroke-width="{rod_w * 2:.1f}"/>')
    out.append(f'<path d="M {x0:.1f} {y0 + cyl_h:.1f} L {fx:.1f} {y0 + h - 4:.1f}" '
               f'fill="none" stroke="{INK}" stroke-width="{rod_w * 2:.1f}" stroke-opacity="0.25"/>')
    out.append(_circle(x0, y0 + cyl_h, cw * 0.55, fill=INK, width=0.8))   # joint
    # foot plate (invariant)
    out.append(f'<rect x="{fx - 9:.1f}" y="{y0 + h - 4:.1f}" width="18" height="4.5" rx="1.5" '
               f'fill="{INK}"/>')
    return out


def draw_optic_visor(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 58
    vw, vh = 24 + 14 * e["length"], 9 + 7 * e["girth"]
    out.append(f'<rect x="{cx - vw / 2:.1f}" y="{cy - vh / 2:.1f}" width="{vw:.1f}" height="{vh:.1f}" '
               f'rx="3" fill="{STEEL}" stroke="{INK}" stroke-width="1.8"/>')
    n = 1 + round(e["count"] * 2)                      # 1-3 lenses (invariant)
    for i in range(n):
        lx = cx + (i - (n - 1) / 2) * (vw / (n + 0.4))
        r = min(vh * 0.38, 3.2 + 3.2 * e["girth"])
        out.append(_circle(lx, cy, r, fill=ACCENT, width=1.2))
        out.append(_circle(lx, cy, max(0.8, r * 0.4), fill=INK, width=0.6))
    if e["ornament"] > 0.4:                            # side strap rivets
        for sx in (cx - vw / 2 - 2.5, cx + vw / 2 + 2.5):
            out.append(_circle(sx, cy, 1.4, fill=INK, width=0.6))
    return out


def draw_sensor_mast(allele):
    e = _e(allele)
    out = []
    cx, cy = 50, 92
    out.append(f'<rect x="{cx - 9:.1f}" y="{cy - 5:.1f}" width="18" height="7" rx="2" '
               f'fill="{STEEL}" stroke="{INK}" stroke-width="1.6"/>')
    h = 36 + 44 * e["length"]
    lean = (e["curl"] - 0.3) * 16
    tx, ty = cx + lean, cy - h
    out.append(f'<line x1="{cx}" y1="{cy - 5}" x2="{tx:.1f}" y2="{ty:.1f}" '
               f'stroke="{INK}" stroke-width="{1.8 + 3 * e["girth"]:.1f}"/>')
    for i in range(1, 2 + round(e["count"] * 3)):      # guy-ring stages
        t = i / (2 + round(e["count"] * 3))
        out.append(f'<line x1="{cx + lean * t - 5:.1f}" y1="{cy - 5 - (h - 5) * t:.1f}" '
                   f'x2="{cx + lean * t + 5:.1f}" y2="{cy - 5 - (h - 5) * t:.1f}" '
                   f'stroke="{INK}" stroke-width="1"/>')
    # dish or vane at the tip (invariant)
    if e["ornament"] > 0.45:
        out.append(f'<path d="M {tx - 8:.1f} {ty:.1f} a 8 8 0 0 1 16 0 Z" '
                   f'fill="{STEEL}" stroke="{INK}" stroke-width="1.4" '
                   f'transform="rotate(-25 {tx:.1f} {ty:.1f})"/>')
    else:
        out.append(f'<path d="M {tx:.1f} {ty:.1f} l 9 -4 l 0 8 Z" fill="{INK}"/>')
    out.append(_circle(tx, ty, 1.8, fill=ACCENT, width=0.8))
    return out


def draw_plasma_lance(allele):
    e = _e(allele)
    out = []
    sx, sy = 50, 92
    # fleshy arm (organic half)
    ex, ey = sx + 10, sy - 24
    wx, wy = sx + 18, sy - 44
    out.append(_path(f"M {sx} {sy} Q {ex:.1f} {ey:.1f} {wx:.1f} {wy:.1f}", fill="none",
                     width=5.5 + 4 * e["girth"]))
    for i in range(round(e["ornament"] * 4)):          # vein nodules
        t = (i + 1) / 5
        vx = sx + (wx - sx) * t + 3
        vy = sy + (wy - sy) * t
        out.append(_circle(vx, vy, 1.6, fill=ACCENT, width=0.7))
    # charge bulb (invariant)
    out.append(_circle(wx, wy, 4.5 + 4 * e["girth"], fill=ACCENT, width=1.6))
    # the lance emitter (invariant): glowing taper
    llen = 22 + 26 * e["length"]
    lw = (3.5 + 3 * e["girth"]) * (1 - 0.5 * e["taper"]) + 1.5
    tipx, tipy = wx + llen * 0.45, wy - llen
    out.append(f'<path d="M {wx - lw:.1f} {wy:.1f} Q {wx - lw * 0.3 + 4:.1f} {wy - llen * 0.5:.1f} '
               f'{tipx:.1f} {tipy:.1f} Q {wx + lw * 0.6 + 4:.1f} {wy - llen * 0.5:.1f} {wx + lw:.1f} {wy:.1f} Z" '
               f'fill="#d8c8e8" stroke="{INK}" stroke-width="1.6"/>')
    out.append(_circle(tipx, tipy, 2.2, fill="#d8c8e8", width=1.0))     # glow tip
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
    "hoofed_leg": draw_hoofed_leg,
    "talon_leg": draw_talon_leg,
    "insect_leg": draw_insect_leg,
    "rifle_arm": draw_rifle_arm,
    "piston_leg": draw_piston_leg,
    "optic_visor": draw_optic_visor,
    "sensor_mast": draw_sensor_mast,
    "plasma_lance": draw_plasma_lance,
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
