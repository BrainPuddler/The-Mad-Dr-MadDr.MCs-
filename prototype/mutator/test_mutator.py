"""Property tests for the Mutator prototype. Run: python3 test_mutator.py"""

import random

from genome import (AXES, BODY_AXES, BRAIN_AXES, BRAIN_TIERS, BrainGenes,
                    Genome, PartAllele)
from catalog import BODY_PLANS, FAMILIES, homolog_of, families_in_class, express
from operators import random_genome, mutate, splice, graft
import command as cmd

SLOT_SPEC = (("hand", "hand"), ("sensor", "sensor"), ("eye", "eye"), ("leg", "leg"))


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
        assert g.brain.tier in BRAIN_TIERS, g.brain.tier
        assert len(g.brain.params) == len(BRAIN_AXES)
        assert all(0.0 <= p <= 1.0 for p in g.brain.params), g.brain.params
        pop.append(g)
    print("ok: closure & bounds incl. body & brain (340 genomes)")


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


def _brain(tier, command, will, temperament, guile, fury=0.05):
    return BrainGenes(tier, (command, will, temperament, guile, fury))


def test_brain_expression():
    """Capacity rises with command & size; cost rises with will & size; radius with command."""
    big = _brain("mastermind", 0.9, 0.1, 0.2, 0.2)
    small = _brain("dim", 0.1, 0.1, 0.2, 0.2)
    assert cmd.capacity(big) > cmd.capacity(small)
    assert cmd.capacity(_brain("dim", 0.9, 0, 0, 0)) > cmd.capacity(_brain("dim", 0.1, 0, 0, 0))
    assert cmd.cost(_brain("dim", 0, 0.9, 0, 0)) > cmd.cost(_brain("dim", 0, 0.1, 0, 0))
    assert cmd.radius(_brain("dim", 0.9, 0, 0, 0)) > cmd.radius(_brain("dim", 0.1, 0, 0, 0))
    print("ok: brain expression (capacity/cost/radius monotonic)")


def test_loyalty_stable_when_matched():
    """A strong commander + a docile, nearby subordinate stays controlled."""
    rng = random.Random(1)
    boss = cmd.Unit("boss", _brain("gifted", 0.85, 0.1, 0.1, 0.1), pos=(0, 0))
    sub = cmd.Unit("sub", _brain("dim", 0.1, 0.10, 0.10, 0.1), pos=(1, 0))
    cmd.assign(boss, sub, loyalty=0.85)
    for _ in range(60):
        cmd.step([boss, sub], rng)
    assert sub.state == cmd.CONTROLLED and sub.commander is boss, (sub.state, sub.loyalty)
    print(f"ok: loyalty stable when matched (final {sub.loyalty:.2f})")


def test_rebellion_when_overstretched():
    """A willful subordinate, far out of a weak commander's range, breaks free."""
    rng = random.Random(2)
    boss = cmd.Unit("boss", _brain("dim", 0.15, 0.1, 0.1, 0.1), pos=(0, 0))
    sub = cmd.Unit("sub", _brain("gifted", 0.2, 0.95, 0.2, 0.1), pos=(40, 0))  # far, willful
    cmd.assign(boss, sub, loyalty=0.7)
    broke = False
    for _ in range(40):
        cmd.step([boss, sub], rng)
        if sub.commander is None:
            broke = True
            break
    assert broke and sub.state in (cmd.FERAL, cmd.REBEL_STATE), (sub.state, sub.loyalty)
    print(f"ok: rebellion when overstretched ({sub.state})")


def test_decapitation_shock():
    """Killing a commander drops every direct subordinate's loyalty."""
    rng = random.Random(3)
    boss = cmd.Unit("boss", _brain("mastermind", 0.9, 0.1, 0.1, 0.1), pos=(0, 0))
    subs = [cmd.Unit(f"s{i}", _brain("dim", 0.1, 0.2, 0.1, 0.1), pos=(1, i)) for i in range(3)]
    for s in subs:
        cmd.assign(boss, s, loyalty=0.85)
    for _ in range(10):
        cmd.step([boss] + subs, rng)
    before = [s.loyalty for s in subs]
    cmd.kill(boss, [boss] + subs, [])
    assert all(s.loyalty < b for s, b in zip(subs, before)), (before, [s.loyalty for s in subs])
    print("ok: decapitation shock (all subordinates' loyalty dropped)")


def test_command_determinism():
    """Same seed -> identical loyalty trajectory and final states."""
    def trajectory(seed):
        rng = random.Random(seed)
        boss = cmd.Unit("boss", _brain("gifted", 0.6, 0.2, 0.4, 0.2), pos=(0, 0))
        sub = cmd.Unit("sub", _brain("dim", 0.2, 0.6, 0.5, 0.3), pos=(8, 0))
        cmd.assign(boss, sub)
        out = []
        for _ in range(50):
            cmd.step([boss, sub], rng)
            out.append(round(sub.loyalty, 6))
        return out, sub.state
    assert trajectory(9) == trajectory(9)
    assert trajectory(9) != trajectory(10)
    print("ok: command determinism")


def test_berserk_buffs_monotonic():
    """Berserk power/armor rise with fury; threshold falls (snaps easier)."""
    hi = _brain("average", 0.2, 0.2, 0.2, 0.2, fury=0.95)
    lo = _brain("average", 0.2, 0.2, 0.2, 0.2, fury=0.10)
    assert cmd.berserk_power_mult(hi) > cmd.berserk_power_mult(lo)
    assert cmd.berserk_armor_bonus(hi) > cmd.berserk_armor_bonus(lo)
    assert cmd.berserk_threshold(hi) < cmd.berserk_threshold(lo)
    print("ok: berserk buffs monotonic in fury")


def test_berserk_friendly_fire_and_recovery():
    """Under night stress a high-fury unit goes berserk, damages its own
    side, burns out, and its commander re-asserts control."""
    rng = random.Random(6)
    boss = cmd.Unit("boss", _brain("gifted", 0.85, 0.1, 0.1, 0.1), pos=(0, 0))
    wolf = cmd.Unit("wolf", _brain("average", 0.2, 0.4, 0.4, 0.2, fury=0.95), pos=(2, 0))
    pack = cmd.Unit("pack", _brain("dim", 0.1, 0.1, 0.2, 0.1), pos=(2.5, 0))
    units = [boss, wolf, pack]
    cmd.assign(boss, wolf); cmd.assign(boss, pack)
    went_berserk = recovered = False
    for t in range(80):
        stress = {"wolf": 0.06, "pack": 0.01} if t < 25 else {}
        cmd.step(units, rng, stress=stress, lumen="night" if t < 40 else "day")
        if wolf.state == cmd.BERSERK:
            went_berserk = True
        # recovered = frenzy over, back under its commander (may still waver)
        if went_berserk and wolf.state in (cmd.CONTROLLED, cmd.WAVERING) \
                and wolf.commander is boss:
            recovered = True
            break
    assert went_berserk, "high-fury unit never went berserk under night stress"
    assert pack.hp < 100.0 or not pack.alive, "berserker never hurt its own side"
    assert recovered, f"wolf never recovered (state {wolf.state})"
    print(f"ok: berserk friendly fire (pack hp {max(0, pack.hp):.0f}) and recovery")


def test_low_fury_never_berserks():
    """The same stress leaves a low-fury brain merely disloyal, not berserk."""
    rng = random.Random(6)
    boss = cmd.Unit("boss", _brain("gifted", 0.85, 0.1, 0.1, 0.1), pos=(0, 0))
    ox = cmd.Unit("ox", _brain("average", 0.2, 0.4, 0.4, 0.2, fury=0.05), pos=(2, 0))
    cmd.assign(boss, ox)
    for t in range(80):
        stress = {"ox": 0.06} if t < 25 else {}
        cmd.step([boss, ox], rng, stress=stress, lumen="night" if t < 40 else "day")
        assert ox.state != cmd.BERSERK
    print("ok: low fury never berserks under identical stress")


def test_brain_survives_breeding():
    """Mutate and splice keep a valid brain on the genome."""
    rng = random.Random(4)
    g = random_genome(SLOT_SPEC, rng)
    for i in range(60):
        g = splice(g, random_genome(SLOT_SPEC, rng), rng) if i % 2 else mutate(g, rng)
        assert g.brain.tier in BRAIN_TIERS
        assert all(0.0 <= p <= 1.0 for p in g.brain.params)
    print("ok: brain survives 60 breeding ops")


if __name__ == "__main__":
    test_closure_and_bounds()
    test_homolog_grammar()
    test_determinism()
    test_canalization()
    test_shared_axis_inheritance()
    test_body_renderable()
    test_brain_expression()
    test_loyalty_stable_when_matched()
    test_rebellion_when_overstretched()
    test_decapitation_shock()
    test_command_determinism()
    test_berserk_buffs_monotonic()
    test_berserk_friendly_fire_and_recovery()
    test_low_fury_never_berserks()
    test_brain_survives_breeding()
    print("all tests passed")
