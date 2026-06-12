"""The part catalog: families, homolog classes, and canalized expression.

Each family declares:
  - homolog: which slot class it can occupy (hands swap with hands, never
    with eyes) -- the slot grammar that keeps every child viable.
  - bounds: per-axis (lo, hi) PHENOTYPE bounds. Genotype space is always the
    full [0,1]^6; expression maps it into the family's authored-safe range
    (canalization). Extreme genes give visual outliers, never broken parts.
  - invariants: prose, for the doc and for future silhouette validation --
    the features that may NEVER vary, because they are what makes the part
    read as what it is.
"""

DEFAULT_BOUNDS = {a: (0.0, 1.0) for a in ("length", "girth", "taper", "curl", "count", "ornament")}


def _bounds(**over):
    b = dict(DEFAULT_BOUNDS)
    b.update(over)
    return b


FAMILIES = {
    # ---- hand homologs -------------------------------------------------
    "claw_hand": {
        "homolog": "hand",
        "bounds": _bounds(count=(0.0, 1.0), curl=(0.1, 0.9)),
        "invariants": "a palm bearing 2-5 hard, curved, tapering talons",
    },
    "pincer": {
        "homolog": "hand",
        "bounds": _bounds(curl=(0.2, 1.0), count=(0.0, 0.6)),
        "invariants": "two opposing crescent jaws meeting at a gap",
    },
    "tentacle": {
        "homolog": "hand",
        "bounds": _bounds(girth=(0.0, 0.7), taper=(0.4, 1.0)),
        "invariants": "a single smooth limb tapering to a curling tip",
    },
    # ---- sensor homologs -----------------------------------------------
    "antenna": {
        "homolog": "sensor",
        "bounds": _bounds(girth=(0.0, 0.45), length=(0.3, 1.0)),
        "invariants": "a thin paired stalk, segmented, ending in a tip bulb",
    },
    "horn": {
        "homolog": "sensor",
        "bounds": _bounds(girth=(0.3, 1.0), curl=(0.0, 0.7)),
        "invariants": "a rigid ridged cone, broad base to sharp point",
    },
    # ---- eye homologs ---------------------------------------------------
    "bug_eyes": {
        "homolog": "eye",
        "bounds": _bounds(count=(0.2, 1.0)),
        "invariants": "a clustered constellation of 3+ round eyes with pupils",
    },
    "cyclops_eye": {
        "homolog": "eye",
        "bounds": _bounds(count=(0.0, 0.0), girth=(0.3, 1.0)),
        "invariants": "one single oversized lidded eye",
    },
    "stalk_eyes": {
        "homolog": "eye",
        "bounds": _bounds(length=(0.3, 1.0), girth=(0.0, 0.5)),
        "invariants": "eyeballs held aloft on flexible stalks",
    },
    # ---- leg homologs -----------------------------------------------------
    # Legs draw DOWNWARD from a top anchor (50, 8) so creatures can mount
    # them at the hip; see render_svg.leg_height for the mounting math.
    "hoofed_leg": {
        "homolog": "leg",
        "bounds": _bounds(girth=(0.35, 1.0), curl=(0.0, 0.6)),
        "invariants": "a sturdy column ending in a hard cloven hoof",
    },
    "talon_leg": {
        "homolog": "leg",
        "bounds": _bounds(girth=(0.0, 0.4), count=(0.2, 1.0)),
        "invariants": "a thin bird leg, backward knee, ending in splayed clawed toes",
    },
    "insect_leg": {
        "homolog": "leg",
        "bounds": _bounds(girth=(0.0, 0.35), curl=(0.3, 1.0)),
        "invariants": "a segmented zigzag of chitinous struts with a tarsal hook",
    },
}


# ---- body plans ----------------------------------------------------------
# "tetrapod" is one CONTINUOUS plan family: the posture axis spans upright
# biped (0) -> knuckle-walking brachiator/"monkey" (~0.5) -> all-fours
# quadruped (1), so those creature types interbreed smoothly within one rig.
# "blob", "serpentine", and "winged" are genuinely DISCRETE plans: crossing
# into or out of them is the rare cross-plan splice jackpot (docs/06).
#
# Plans may IGNORE slots (serpentine ignores the leg slot; its leg genes
# ride along silently and re-express if a descendant jumps back to a legged
# plan -- an atavism, mirroring docs/08's retarget rules).
BODY_PLANS = {
    "tetrapod": {
        "invariants": "a torso on limbs with a head: posture spans biped to quadruped",
    },
    "blob": {
        "invariants": "a single amorphous mass; parts surface-mount on the membrane",
    },
    "serpentine": {
        "invariants": "one long tapering body coiling along the ground, head at the fore",
    },
    "winged": {
        "invariants": "a small body slung between two membrane wings, standing on legs",
    },
}


def homolog_of(family: str) -> str:
    return FAMILIES[family]["homolog"]


def families_in_class(homolog: str):
    return sorted(f for f, spec in FAMILIES.items() if spec["homolog"] == homolog)


def express(family: str, axis_name: str, gene: float) -> float:
    """Canalized expression: map a [0,1] gene into the family's phenotype range."""
    lo, hi = FAMILIES[family]["bounds"][axis_name]
    return lo + gene * (hi - lo)
