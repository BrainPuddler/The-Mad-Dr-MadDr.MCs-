"""Genome representation for the Mutator part-genetics prototype.

Implements the proposed v2 slot allele: {family, params[6]} where the six
parameters are SHARED SEMANTIC AXES, identically interpreted by every part
family. Shared axes are what make cross-family breeding meaningful: a child
of a tentacle and a claw can inherit "long, thin, strongly curled" and
express it as a claw. See docs/15-part-genetics.md.
"""

from dataclasses import dataclass

# The six shared semantic axes, each 0..1 in genotype space.
AXES = ("length", "girth", "taper", "curl", "count", "ornament")

# Body-plan axes, each 0..1. Axes are shared across plans but may carry
# plan-specific meaning (for blobs, "posture" expresses as wobble and
# "limb" as pseudopod reach) -- the same canalization idea as part axes.
BODY_AXES = ("posture", "bulk", "limb", "tail")


@dataclass(frozen=True)
class BodyGenes:
    plan: str             # "tetrapod" | "blob"
    params: tuple         # four floats in [0, 1], ordered as BODY_AXES

    def axis(self, name: str) -> float:
        return self.params[BODY_AXES.index(name)]


@dataclass(frozen=True)
class PartAllele:
    family: str           # part family id, e.g. "claw_hand"
    params: tuple         # six floats in [0, 1], ordered as AXES

    def axis(self, name: str) -> float:
        return self.params[AXES.index(name)]


@dataclass(frozen=True)
class Genome:
    """A creature is a body plan plus a mapping of homolog slots to alleles.

    Slots are homolog classes (hand, sensor, eye ...): crossover and family
    jumps only ever swap within a slot's homolog class, so every child is
    anatomically valid by construction (the Hox-grammar strategy).
    """
    slots: tuple          # tuple of (slot_name, PartAllele), stable order
    body: BodyGenes = None  # None for parts-only experiments

    def get(self, slot: str) -> PartAllele:
        return dict(self.slots)[slot]

    def replace(self, slot: str, allele: PartAllele) -> "Genome":
        return Genome(tuple((s, allele if s == slot else a) for s, a in self.slots),
                      self.body)
