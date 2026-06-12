"""Full-creature assembly: body plans + surface-mounted parts.

Like render_svg.py, this is a pure function of the genome -- no randomness.
The tetrapod plan demonstrates the continuous-posture strategy: the posture
axis spans upright biped (0) -> knuckle-walking "monkey" (~0.5) -> all-fours
quadruped (1) within one plan family. The blob plan is discrete: an
amorphous membrane that parts surface-mount onto.

Creatures draw inside a 150x150 cell; the ground line is y = 132.
"""

import math

from genome import BODY_AXES
from render_svg import DRAWERS, INK, PALE, leg_height

GROUND = 132
LEG_ANCHOR = (50, 8)   # legs hang from a top anchor; see render_svg leg drawers


def _b(body, name):
    return body.params[BODY_AXES.index(name)]


def _mount(allele, px, py, scale, rot=0.0, anchor=(50, 92)):
    """Attach a rendered part at (px, py), rotated and scaled, by its anchor."""
    parts = "".join(DRAWERS[allele.family](allele))
    return (f'<g transform="translate({px:.1f},{py:.1f}) rotate({rot:.1f}) '
            f'scale({scale:.2f}) translate(-{anchor[0]},-{anchor[1]})">{parts}</g>')


def _limb(x0, y0, x1, y1, bow, width):
    """A bowed two-point limb stroke."""
    mx, my = (x0 + x1) / 2 + bow, (y0 + y1) / 2
    return (f'<path d="M {x0:.1f} {y0:.1f} Q {mx:.1f} {my:.1f} {x1:.1f} {y1:.1f}" '
            f'fill="none" stroke="{INK}" stroke-width="{width:.1f}" stroke-linecap="round"/>')


def draw_tetrapod(genome):
    body = genome.body
    posture = _b(body, "posture")          # 0 upright .. 1 all-fours
    bulk = _b(body, "bulk")
    limb = _b(body, "limb")
    tail = _b(body, "tail")
    out = []

    # hind legs are genome leg parts: hip height follows the leg's own length
    leg = genome.get("leg")
    leg_scale = 0.40 + 0.10 * bulk
    hip = (92.0, GROUND - leg_scale * leg_height(leg) + 10 * posture * 0.4)
    torso_len = 30 + 16 * bulk
    lean = math.radians(8 + 78 * posture)   # torso angle from vertical
    shoulder = (hip[0] - torso_len * math.sin(lean), hip[1] - torso_len * math.cos(lean))
    torso_w = 11 + 12 * bulk

    # tail (behind everything)
    if tail > 0.12:
        tl = 14 + 40 * tail
        out.append(f'<path d="M {hip[0]:.1f} {hip[1]:.1f} Q {hip[0] + tl * 0.9:.1f} '
                   f'{hip[1] - tl * 0.7:.1f} {hip[0] + tl:.1f} {hip[1] - tl * 1.15:.1f}" '
                   f'fill="none" stroke="{INK}" stroke-width="{3 + 3 * bulk:.1f}" stroke-linecap="round"/>')

    for off in (-5, 5):
        out.append(_mount(leg, hip[0] + off, hip[1] - 2, leg_scale, anchor=LEG_ANCHOR))

    # torso
    midx, midy = (hip[0] + shoulder[0]) / 2, (hip[1] + shoulder[1]) / 2
    out.append(f'<ellipse cx="{midx:.1f}" cy="{midy:.1f}" rx="{torso_w:.1f}" ry="{torso_len * 0.62:.1f}" '
               f'transform="rotate({math.degrees(lean):.1f} {midx:.1f} {midy:.1f})" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2.2"/>')
    # the stitching motif
    out.append(f'<path d="M {midx - torso_w * 0.5:.1f} {midy:.1f} l 4 -2 l 4 2 l 4 -2 l 4 2" '
               f'fill="none" stroke="{INK}" stroke-width="1"/>')

    # arms: hang when upright, reach the ground when monkey/quadruped
    arm_len = 22 + 30 * limb
    arm_w = 3.8 + 3.5 * bulk
    for off in (-6, 6):
        hx = shoulder[0] - 8 - posture * 14 + off
        hy = min(GROUND, shoulder[1] + arm_len)
        reach = posture > 0.32 or arm_len > (GROUND - shoulder[1])
        if reach:
            hy = GROUND
        out.append(_limb(shoulder[0] + off * 0.4, shoulder[1] + 2, hx, hy, -8 - 6 * limb, arm_w))
        hand = genome.get("hand")
        ang = -28 - 30 * posture if not reach else 0.0
        out.append(_mount(hand, hx, hy + (0 if reach else 2), 0.30 + 0.10 * bulk, rot=ang))

    # head along the torso axis past the shoulder
    head_r = 8.5 + 4.5 * bulk
    head = (shoulder[0] - (head_r + 3) * math.sin(lean), shoulder[1] - (head_r + 3) * math.cos(lean))
    out.append(f'<circle cx="{head[0]:.1f}" cy="{head[1]:.1f}" r="{head_r:.1f}" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2.2"/>')
    out.append(_mount(genome.get("eye"), head[0], head[1] + head_r * 0.15, 0.30 + 0.06 * bulk,
                      anchor=(50, 60)))
    out.append(_mount(genome.get("sensor"), head[0], head[1] - head_r + 1.5, 0.34,
                      rot=-10 + 20 * posture))
    return out


def draw_blob(genome):
    body = genome.body
    wobble = _b(body, "posture")           # posture expresses as membrane wobble
    bulk = _b(body, "bulk")
    reach = _b(body, "limb")               # limb expresses as pseudopod reach
    drip = _b(body, "tail")                # tail expresses as dripping
    out = []

    cx, cy = 75.0, 96.0
    R = 24 + 20 * bulk
    pts = []
    for i in range(40):
        th = i / 40 * 2 * math.pi
        r = R * (1 + 0.16 * wobble * math.sin(3 * th + 1.3) + 0.10 * wobble * math.sin(7 * th))
        pts.append((cx + r * math.cos(th), cy + r * 0.78 * math.sin(th)))
    d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts) + " Z"
    out.append(f'<path d="{d}" fill="{PALE}" stroke="{INK}" stroke-width="2.4" stroke-linejoin="round"/>')

    # drips pooling at the base
    for i in range(round(drip * 5)):
        dx = cx - R * 0.8 + i * (R * 1.6 / 4.2)
        out.append(f'<ellipse cx="{dx:.1f}" cy="{cy + R * 0.82:.1f}" rx="{3 + 2 * bulk:.1f}" ry="1.8" '
                   f'fill="{PALE}" stroke="{INK}" stroke-width="1.2"/>')

    # pseudopod: the hand part emerging from the membrane
    px = cx + R * (0.55 + 0.35 * reach)
    py = cy - R * 0.18
    out.append(_mount(genome.get("hand"), px, py, 0.32 + 0.16 * reach, rot=55))

    # sensor cresting the membrane, eyes floating in the mass
    out.append(_mount(genome.get("sensor"), cx - R * 0.25, cy - R * 0.62, 0.36))
    out.append(_mount(genome.get("eye"), cx, cy - R * 0.05, 0.40 + 0.10 * bulk, anchor=(50, 60)))
    return out


def draw_serpentine(genome):
    """Serpentine IGNORES the leg slot: those genes ride along silently and
    re-express if a descendant jumps back to a legged plan (an atavism)."""
    body = genome.body
    coil = _b(body, "posture")             # posture expresses as coil amplitude
    girth = _b(body, "bulk")
    app = _b(body, "limb")                 # limb expresses as tail-appendage size
    rattle = _b(body, "tail")
    out = []

    # head at the left (t=0), tail at the right (t=1), body waving over ground
    n = 26
    spine = []
    for i in range(n):
        t = i / (n - 1)
        x = 34 + 86 * t
        y = GROUND - 7 - (4 + 15 * coil) * abs(math.sin(t * math.pi * 1.7 + 0.4))
        spine.append((x, y))
    pts_l, pts_r = [], []
    for i, (x, y) in enumerate(spine):
        t = i / (n - 1)
        if i == 0:
            dx, dy = spine[1][0] - x, spine[1][1] - y
        else:
            dx, dy = x - spine[i - 1][0], y - spine[i - 1][1]
        m = math.hypot(dx, dy) or 1.0
        px, py = -dy / m, dx / m
        w = (5 + 9 * girth) * (1 - 0.78 * t) + 0.8
        pts_l.append((x + px * w, y + py * w))
        pts_r.append((x - px * w, y - py * w))
    d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts_l + pts_r[::-1]) + " Z"
    out.append(f'<path d="{d}" fill="{PALE}" stroke="{INK}" stroke-width="2.2" stroke-linejoin="round"/>')

    # belly-scale strokes
    for i in range(3, n - 4, 3):
        x, y = spine[i]
        w = (5 + 9 * girth) * (1 - 0.78 * i / (n - 1))
        out.append(f'<path d="M {x - w * 0.6:.1f} {y + w * 0.55:.1f} q {w * 0.6:.1f} 2.5 {w * 1.2:.1f} 0" '
                   f'fill="none" stroke="{INK}" stroke-width="0.9"/>')

    # rattle ticks toward the tail
    for i in range(round(rattle * 4)):
        x, y = spine[n - 2 - i]
        out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{2.2 - i * 0.3:.1f}" fill="{INK}"/>')

    # tail appendage: the hand part as a stinger/grasper
    tx, ty = spine[-1]
    out.append(_mount(genome.get("hand"), tx + 2, ty - 1, 0.20 + 0.16 * app, rot=55))

    # head: raised, with eyes and sensor
    hx, hy = spine[0]
    hr = 7.5 + 5 * girth
    out.append(f'<circle cx="{hx - 2:.1f}" cy="{hy - hr * 0.45:.1f}" r="{hr:.1f}" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2.2"/>')
    out.append(f'<path d="M {hx - hr - 1:.1f} {hy - hr * 0.3:.1f} l -7 1.5 l 7 1.5" '
               f'fill="none" stroke="{INK}" stroke-width="1.2"/>')   # forked tongue
    out.append(_mount(genome.get("eye"), hx - 2, hy - hr * 0.35, 0.27 + 0.05 * girth, anchor=(50, 60)))
    out.append(_mount(genome.get("sensor"), hx - 2, hy - hr * 1.35, 0.30))
    return out


def draw_winged(genome):
    body = genome.body
    raise_ = _b(body, "posture")           # posture expresses as wing raise
    bulk = _b(body, "bulk")
    span = _b(body, "limb")                # limb expresses as wingspan
    streamer = _b(body, "tail")
    out = []

    leg = genome.get("leg")
    leg_scale = 0.30
    lh = leg_scale * leg_height(leg)
    br = 9 + 7 * bulk                      # body radius
    cx, cy = 75.0, GROUND - lh - br * 0.7  # body slung above standing legs

    # wings: membrane fans with finger struts (drawn behind the body)
    for side in (-1, 1):
        sx, sy = cx + side * br * 0.6, cy - br * 0.3
        tipx = cx + side * (30 + 30 * span)
        tipy = cy - 24 - 26 * raise_
        edge = []
        for k in range(4):                 # scalloped trailing edge (invariant)
            t = (k + 1) / 4
            ex = sx + (tipx - sx) * (1 - t * 0.85)
            ey = tipy + (cy + br * 0.5 - tipy) * t
            edge.append((ex, ey))
        d = (f"M {sx:.1f} {sy:.1f} Q {cx + side * (16 + 22 * span):.1f} {tipy - 8:.1f} "
             f"{tipx:.1f} {tipy:.1f} ")
        px, py = tipx, tipy
        for ex, ey in edge:
            d += f"Q {(px + ex) / 2 + side * 3:.1f} {(py + ey) / 2 + 7:.1f} {ex:.1f} {ey:.1f} "
        d += "Z"
        out.append(f'<path d="{d}" fill="{PALE}" stroke="{INK}" stroke-width="2" stroke-linejoin="round"/>')
        for ex, ey in edge[:-1]:           # finger struts
            out.append(f'<path d="M {sx:.1f} {sy:.1f} L {ex:.1f} {ey:.1f}" '
                       f'fill="none" stroke="{INK}" stroke-width="1.1"/>')
        # wing-claw: the hand part, tiny, at the wrist
        out.append(_mount(genome.get("hand"), tipx, tipy, 0.16, rot=side * 40))

    # legs to the ground
    for off in (-6, 6):
        out.append(_mount(leg, cx + off, GROUND - lh, leg_scale, anchor=LEG_ANCHOR))

    # body, tail streamer, head
    out.append(f'<ellipse cx="{cx}" cy="{cy:.1f}" rx="{br * 0.85:.1f}" ry="{br:.1f}" '
               f'fill="{PALE}" stroke="{INK}" stroke-width="2.2"/>')
    if streamer > 0.15:
        sl = 10 + 26 * streamer
        out.append(f'<path d="M {cx:.1f} {cy + br:.1f} Q {cx + 6:.1f} {cy + br + sl * 0.6:.1f} '
                   f'{cx - 4:.1f} {cy + br + sl:.1f}" fill="none" stroke="{INK}" '
                   f'stroke-width="1.6" stroke-linecap="round"/>')
    hr = br * 0.62
    hy = cy - br - hr * 0.55
    out.append(f'<circle cx="{cx}" cy="{hy:.1f}" r="{hr:.1f}" fill="{PALE}" stroke="{INK}" stroke-width="2.2"/>')
    out.append(_mount(genome.get("eye"), cx, hy + hr * 0.15, 0.26 + 0.05 * bulk, anchor=(50, 60)))
    out.append(_mount(genome.get("sensor"), cx, hy - hr + 1, 0.30))
    return out


PLAN_DRAWERS = {
    "tetrapod": draw_tetrapod,
    "blob": draw_blob,
    "serpentine": draw_serpentine,
    "winged": draw_winged,
}


def render_creature_cell(genome, x, y, label=None):
    parts = "".join(PLAN_DRAWERS[genome.body.plan](genome))
    ground = (f'<path d="M 18 {GROUND} L 132 {GROUND}" stroke="{INK}" '
              f'stroke-width="0.8" stroke-dasharray="3 3"/>')
    lab = (f'<text x="75" y="12" font-size="8" text-anchor="middle" fill="{INK}" '
           f'font-family="Georgia, serif" font-style="italic">{label}</text>') if label else ""
    return f'<g transform="translate({x},{y})">{lab}{ground}{parts}</g>'
