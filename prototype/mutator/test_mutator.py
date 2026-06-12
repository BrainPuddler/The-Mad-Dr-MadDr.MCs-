"""Property tests for the Mutator prototype. Run: python3 test_mutator.py"""

import random

from genome import AXES, BODY_AXES, Genome, PartAllele
from catalog import BODY_PLANS, FAMILIES, homolog_of, families_in_class, express
from operators import random_genome, mutate, splice, graft

SLOT_SPEC = (("hand", "hand"), ("sensor", "sensor"), ("eye", "eye"))


def test_closure_and_bounds():
    """Operators are closed: children are always valid, in-range genomes."""
    rng = random.Random(7)
    pop = [random_genome(SLOT_SPEC, rng) for _ in range(40)]
    for _ in range(300):
        g = splice(rng.choice(pop), rng.choice(pop), rng) if rng.random() < 0.5 \
            else mutate(rng.choice(pop), rng, bias_slot=rng.choice(["hand", None]))
        for slot, allele in g.slots:
            assert allele.family in FAMILIES, allele.family
            assert homolog_of(allele.family) == slot, (slot, allele.family)
            assert len(allele.params) == len(AXES)
            assert all(0.0 <= p <= 1.0 for p in allele.params), allele.params
        assert g.body.plan in BODY_PLANS, g.body.plan
        assert len(g.body.params) == len(BODY_AXES)
        assert all(0.0 <= p <= 1.0 for p in g.body.params), g.body.params
        pop.append(g)
    print("ok: closure & bounds incl. body plans (340 genomes)")


def test_body_renderable():
    """Every genome the operators can produce renders without error."""
    from render_creature import render_creature_cell
    rng = random.Random(5)
    g = random_genome(SLOT_SPEC, rng)
    for i in range(120):
        g = splice(g, random_genome(SLOT_SPEC, rng), rng) if i % 2 else mutate(g, rng)
        svg = render_creature_cell(g, 0, 0)
        assert svg.startswith("<g") and g.body.plan in BODY_PLANS
    print("ok: 120 mutated/spliced creatures all render")


def test_homolog_grammar():
    """Family jumps never leave the homolog class; graft enforces it."""
    rng = random.Random(11)
    g = random_genome(SLOT_SPEC, rng)
    for _ in range(500):
        g = mutate(g, rng, family_jump=0.9)
        assert homolog_of(g.get("hand").family) == "hand"
        assert homolog_of(g.get("eye").family) == "eye"
    try:
        graft(g, "eye", "claw_hand", (0.5,) * 6)
        raise AssertionError("graft accepted a hand part in the eye slot")
    except ValueError:
        pass
    print("ok: homolog grammar (500 high-jump mutations + graft rejection)")


def test_determinism():
    """Same seed -> identical lineage (the server-seeded RNG requirement)."""
    def lineage(seed):
        rng = random.Random(seed)
        g = random_genome(SLOT_SPEC, rng)
        for _ in range(20):
            g = mutate(g, rng)
        return g
    assert lineage(42) == lineage(42)
    assert lineage(42) != lineage(43)
    print("ok: determinism")


def test_canalization():
    """Expression maps full genotype range into authored phenotype bounds."""
    for fam, spec in FAMILIES.items():
        for axis, (lo, hi) in spec["bounds"].items():
            assert express(fam, axis, 0.0) == lo and express(fam, axis, 1.0) == hi
    print("ok: canalized expression bounds")


def test_shared_axis_inheritance():
    """A claw child of tentacle x claw parents lands between them on shared axes."""
    rng = random.Random(3)
    tent = PartAllele("tentacle", (0.95, 0.05, 0.9, 0.9, 0.5, 0.2))   # long, thin, curled
    claw = PartAllele("claw_hand", (0.1, 0.9, 0.2, 0.2, 0.8, 0.7))    # stubby, thick
    a = Genome((("hand", tent),))
    b = Genome((("hand", claw),))
    hits = 0
    for _ in range(200):
        child = splice(a, b, rng).get("hand")
        for pa, pb, pc in zip(tent.params, claw.params, child.params):
            lo, hi = min(pa, pb) - 0.16, max(pa, pb) + 0.16   # noise margin
            assert lo <= pc <= hi
        hits += child.family == "claw_hand"
    assert 60 < hits < 140   # family from either parent, ~50/50
    print(f"ok: shared-axis inheritance (claw child {hits}/200 times)")


if __name__ == "__main__":
    test_closure_and_bounds()
    test_homolog_grammar()
    test_determinism()
    test_canalization()
    test_shared_axis_inheritance()
    test_body_renderable()
    print("all tests passed")
