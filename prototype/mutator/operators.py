"""The three Mutator operators over part genomes (docs/06, docs/15).

All randomness comes from a caller-supplied seeded RNG -- mirroring the
server-seeded determinism requirement (docs/07): same seed, same monster.
"""

import random

from genome import (AXES, BODY_AXES, BRAIN_AXES, BRAIN_TIERS, BodyGenes,
                    BrainGenes, Genome, PartAllele)
from catalog import BODY_PLANS, FAMILIES, families_in_class, homolog_of, origin_of


def _clamp(x):
    return max(0.0, min(1.0, x))


def random_allele(homolog: str, rng: random.Random) -> PartAllele:
    family = rng.choice(families_in_class(homolog))
    return PartAllele(family, tuple(rng.random() for _ in AXES))


def random_body(rng: random.Random, plan=None) -> BodyGenes:
    plan = plan or rng.choice(sorted(BODY_PLANS))
    return BodyGenes(plan, tuple(rng.random() for _ in BODY_AXES))


def random_brain(rng: random.Random, tier=None) -> BrainGenes:
    tier = tier or rng.choice(BRAIN_TIERS)
    return BrainGenes(tier, tuple(rng.random() for _ in BRAIN_AXES))


def random_genome(slot_spec, rng: random.Random, plan=None, tier=None) -> Genome:
    """slot_spec: iterable of (slot_name, homolog_class)."""
    return Genome(tuple((s, random_allele(h, rng)) for s, h in slot_spec),
                  random_body(rng, plan), random_brain(rng, tier))


def _mutate_brain(brain, rng, rate, sigma, tier_shift=0.06):
    if brain is None:
        return None
    tier = brain.tier
    if rng.random() < tier_shift:                     # brain quality can drift one tier
        i = BRAIN_TIERS.index(tier)
        i = max(0, min(len(BRAIN_TIERS) - 1, i + rng.choice((-1, 1))))
        tier = BRAIN_TIERS[i]
    params = tuple(
        _clamp(p + rng.gauss(0.0, sigma)) if rng.random() < rate else p
        for p in brain.params
    )
    return BrainGenes(tier, params)


def _mutate_body(body, rng, rate, sigma, plan_jump=0.03):
    if body is None:
        return None
    plan = body.plan
    if rng.random() < plan_jump:                      # rare cross-plan jump
        others = [p for p in sorted(BODY_PLANS) if p != plan]
        plan = rng.choice(others)
    params = tuple(
        _clamp(p + rng.gauss(0.0, sigma)) if rng.random() < rate else p
        for p in body.params
    )
    return BodyGenes(plan, params)


def mutate(g: Genome, rng: random.Random, rate=0.45, sigma=0.16,
           family_jump=0.10, bias_slot=None) -> Genome:
    """One-parent mutation: Gaussian drift on axes, rare family jumps.

    bias_slot models component feeding (docs/06): feeding e.g. an arm makes
    the hand slot 3x as likely to change and to jump family.
    """
    new = []
    for slot, allele in g.slots:
        if origin_of(allele.family) == "tech":
            new.append((slot, allele))    # issued equipment: flesh mutates, steel doesn't
            continue
        boost = 3.0 if slot == bias_slot else 1.0
        family = allele.family
        if rng.random() < family_jump * boost:
            # jumps stay within the allele's origin: organic stays flesh,
            # biotech stays grown-tech
            choices = [f for f in families_in_class(homolog_of(family), (origin_of(family),))
                       if f != family]
            if choices:
                family = rng.choice(choices)
        params = tuple(
            _clamp(p + rng.gauss(0.0, sigma)) if rng.random() < rate * boost else p
            for p in allele.params
        )
        new.append((slot, PartAllele(family, params)))
    return Genome(tuple(new), _mutate_body(g.body, rng, rate, sigma),
                  _mutate_brain(g.brain, rng, rate, sigma))


def splice(a: Genome, b: Genome, rng: random.Random, noise=0.05) -> Genome:
    """Two-parent crossover: family from one parent per slot, axes blended.

    Because the axes are shared semantics, blending is meaningful even when
    the family comes from the other parent: a claw child of a tentacle
    parent inherits its long, thin, curling build -- expressed as a claw.
    """
    new = []
    for (slot, al_a), (_, al_b) in zip(a.slots, b.slots):
        src = al_a if rng.random() < 0.5 else al_b
        if origin_of(src.family) == "tech":
            # you don't gene-splice a rifle: the issued item passes whole
            new.append((slot, src))
            continue
        params = tuple(
            _clamp(pa + rng.random() * (pb - pa) + rng.gauss(0.0, noise))
            for pa, pb in zip(al_a.params, al_b.params)
        )
        new.append((slot, PartAllele(src.family, params)))
    body = None
    if a.body is not None and b.body is not None:
        plan = a.body.plan if rng.random() < 0.5 else b.body.plan
        bparams = tuple(
            _clamp(pa + rng.random() * (pb - pa) + rng.gauss(0.0, noise))
            for pa, pb in zip(a.body.params, b.body.params)
        )
        body = BodyGenes(plan, bparams)
    brain = None
    if a.brain is not None and b.brain is not None:
        tier = a.brain.tier if rng.random() < 0.5 else b.brain.tier
        nparams = tuple(
            _clamp(pa + rng.random() * (pb - pa) + rng.gauss(0.0, noise))
            for pa, pb in zip(a.brain.params, b.brain.params)
        )
        brain = BrainGenes(tier, nparams)
    return Genome(tuple(new), body, brain)


def graft(g: Genome, slot: str, family: str, params: tuple) -> Genome:
    """Deterministic slot replacement -- the player-control valve."""
    if homolog_of(family) != homolog_of(g.get(slot).family):
        raise ValueError(f"{family} does not fit the {slot} slot (homolog grammar)")
    return g.replace(slot, PartAllele(family, tuple(_clamp(p) for p in params)))
