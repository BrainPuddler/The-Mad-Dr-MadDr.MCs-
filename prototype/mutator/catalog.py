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
}


def homolog_of(family: str) -> str:
    return FAMILIES[family]["homolog"]


def families_in_class(homolog: str):
    return sorted(f for f, spec in FAMILIES.items() if spec["homolog"] == homolog)


def express(family: str, axis_name: str, gene: float) -> float:
    """Canalized expression: map a [0,1] gene into the family's phenotype range."""
    lo, hi = FAMILIES[family]["bounds"][axis_name]
    return lo + gene * (hi - lo)
