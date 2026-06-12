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
from render_svg import DRAWERS, INK, PALE

GROUND = 132


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

    hip = (92.0, GROUND - 26 - 8 * bulk + 10 * posture)
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

    # hind legs: hip to ground
    leg_w = 4.5 + 4 * bulk
    for off in (-7, 7):
        out.append(_limb(hip[0], hip[1], hip[0] + off + 3 * posture, GROUND, 6 + off * 0.4, leg_w))
        out.append(f'<ellipse cx="{hip[0] + off + 3 * posture:.1f}" cy="{GROUND}" rx="6" ry="2.6" '
                   f'fill="{INK}"/>')

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


PLAN_DRAWERS = {"tetrapod": draw_tetrapod, "blob": draw_blob}


def render_creature_cell(genome, x, y, label=None):
    parts = "".join(PLAN_DRAWERS[genome.body.plan](genome))
    ground = (f'<path d="M 18 {GROUND} L 132 {GROUND}" stroke="{INK}" '
              f'stroke-width="0.8" stroke-dasharray="3 3"/>')
    lab = (f'<text x="75" y="12" font-size="8" text-anchor="middle" fill="{INK}" '
           f'font-family="Georgia, serif" font-style="italic">{label}</text>') if label else ""
    return f'<g transform="translate({x},{y})">{lab}{ground}{parts}</g>'
