"""The three Mutator operators over part genomes (docs/06, docs/15).

All randomness comes from a caller-supplied seeded RNG -- mirroring the
server-seeded determinism requirement (docs/07): same seed, same monster.
"""

import random

from genome import AXES, Genome, PartAllele
from catalog import FAMILIES, families_in_class, homolog_of


def _clamp(x):
    return max(0.0, min(1.0, x))


def random_allele(homolog: str, rng: random.Random) -> PartAllele:
    family = rng.choice(families_in_class(homolog))
    return PartAllele(family, tuple(rng.random() for _ in AXES))


def random_genome(slot_spec, rng: random.Random) -> Genome:
    """slot_spec: iterable of (slot_name, homolog_class)."""
    return Genome(tuple((s, random_allele(h, rng)) for s, h in slot_spec))


def mutate(g: Genome, rng: random.Random, rate=0.45, sigma=0.16,
           family_jump=0.10, bias_slot=None) -> Genome:
    """One-parent mutation: Gaussian drift on axes, rare family jumps.

    bias_slot models component feeding (docs/06): feeding e.g. an arm makes
    the hand slot 3x as likely to change and to jump family.
    """
    new = []
    for slot, allele in g.slots:
        boost = 3.0 if slot == bias_slot else 1.0
        family = allele.family
        if rng.random() < family_jump * boost:
            choices = [f for f in families_in_class(homolog_of(family)) if f != family]
            if choices:
                family = rng.choice(choices)
        params = tuple(
            _clamp(p + rng.gauss(0.0, sigma)) if rng.random() < rate * boost else p
            for p in allele.params
        )
        new.append((slot, PartAllele(family, params)))
    return Genome(tuple(new))


def splice(a: Genome, b: Genome, rng: random.Random, noise=0.05) -> Genome:
    """Two-parent crossover: family from one parent per slot, axes blended.

    Because the axes are shared semantics, blending is meaningful even when
    the family comes from the other parent: a claw child of a tentacle
    parent inherits its long, thin, curling build -- expressed as a claw.
    """
    new = []
    for (slot, al_a), (_, al_b) in zip(a.slots, b.slots):
        family = al_a.family if rng.random() < 0.5 else al_b.family
        params = tuple(
            _clamp(pa + rng.random() * (pb - pa) + rng.gauss(0.0, noise))
            for pa, pb in zip(al_a.params, al_b.params)
        )
        new.append((slot, PartAllele(family, params)))
    return Genome(tuple(new))


def graft(g: Genome, slot: str, family: str, params: tuple) -> Genome:
    """Deterministic slot replacement -- the player-control valve."""
    if homolog_of(family) != homolog_of(g.get(slot).family):
        raise ValueError(f"{family} does not fit the {slot} slot (homolog grammar)")
    return g.replace(slot, PartAllele(family, tuple(_clamp(p) for p in params)))
