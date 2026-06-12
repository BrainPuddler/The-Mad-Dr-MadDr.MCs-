"""Faction templates (docs/17): the same genome and the same behavior sim,
expressed three ways.

A faction is an EXPRESSION PROFILE, not a new system:
  - a behavior profile (command.py): what a control snap means (feral/rebel
    vs rout-and-rally vs hive frenzy), decapitation severity, cohesion
  - brain canalization: the gene ranges its members can have (drilled
    soldiers have low will; drones have almost none; queens have vast command)
  - a part-origin policy: monsters are organic; humans carry issued TECH
    (graft-only -- the quartermaster, not the vat); aliens mix flesh with
    BIOTECH -- grown machines that breed like flesh
"""

import random

from genome import BodyGenes, BrainGenes, Genome, PartAllele
from command import (Unit, assign, MONSTER_PROFILE, HUMAN_PROFILE, ALIEN_PROFILE)


def _genes(rng, **ranges):
    """Canalized brain genes: each axis drawn from the faction's range."""
    order = ("command", "will", "temperament", "guile", "fury")
    return tuple(rng.uniform(*ranges.get(a, (0.1, 0.4))) for a in order)


def _slots(hand, sensor, eye, leg, rng, jitter=0.18):
    def allele(family, base=0.5):
        return PartAllele(family, tuple(
            max(0.0, min(1.0, base + rng.uniform(-jitter, jitter))) for _ in range(6)))
    return (("hand", allele(hand)), ("sensor", allele(sensor)),
            ("eye", allele(eye)), ("leg", allele(leg)))


# ---- humans: drilled, equipped, mortal --------------------------------------

def human_trooper(name, rng, pos=(0, 0)):
    brain = BrainGenes("dim", _genes(rng, command=(0.05, 0.15), will=(0.10, 0.30),
                                     temperament=(0.2, 0.5), guile=(0.05, 0.30),
                                     fury=(0.0, 0.05)))   # drilled out of them
    g = Genome(_slots("rifle_arm", "sensor_mast", "optic_visor", "piston_leg", rng),
               BodyGenes("tetrapod", (0.02 + rng.random() * 0.06, 0.25 + rng.random() * 0.2,
                                      0.35 + rng.random() * 0.2, 0.0)),
               brain)
    u = Unit(name, brain, side="humans", pos=pos, profile=HUMAN_PROFILE)
    u.genome = g
    return u


def human_officer(name, rng, tier="average", pos=(0, 0)):
    brain = BrainGenes(tier, _genes(rng, command=(0.55, 0.80), will=(0.30, 0.50),
                                    temperament=(0.1, 0.3), guile=(0.10, 0.35),
                                    fury=(0.0, 0.10)))
    u = Unit(name, brain, side="humans", pos=pos, profile=HUMAN_PROFILE)
    u.genome = Genome(_slots("rifle_arm", "sensor_mast", "optic_visor", "piston_leg", rng),
                      BodyGenes("tetrapod", (0.04, 0.4, 0.45, 0.0)), brain)
    return u


def human_platoon(rng, base=(50.0, 50.0)):
    """A capacity-sound chain: captain -> 2 sergeants -> 3 privates each."""
    captain = human_officer("Capt. Hale", rng, tier="gifted", pos=base)
    sgt_a = human_officer("Sgt. Brock", rng, tier="average", pos=(base[0] + 2, base[1] + 1))
    sgt_b = human_officer("Sgt. Vance", rng, tier="average", pos=(base[0] - 2, base[1] + 1))
    squad_a = [human_trooper(f"Pvt-A{i+1}", rng, pos=(base[0] + 3 + i, base[1] + 2))
               for i in range(3)]
    squad_b = [human_trooper(f"Pvt-B{i+1}", rng, pos=(base[0] - 3 - i, base[1] + 2))
               for i in range(3)]
    assign(captain, sgt_a)
    assign(captain, sgt_b)
    for t in squad_a:
        assign(sgt_a, t)
    for t in squad_b:
        assign(sgt_b, t)
    return [captain, sgt_a, sgt_b] + squad_a + squad_b, captain, (squad_a, squad_b)


# ---- aliens: the hive -- flesh and grown machines ---------------------------

def alien_queen(name, rng, pos=(0, 0)):
    brain = BrainGenes("mastermind", _genes(rng, command=(0.92, 1.0), will=(0.85, 1.0),
                                            temperament=(0.0, 0.15), guile=(0.4, 0.7),
                                            fury=(0.0, 0.2)))
    u = Unit(name, brain, side="aliens", pos=pos, profile=ALIEN_PROFILE)
    u.genome = Genome(_slots("plasma_lance", "antenna", "bug_eyes", "insect_leg", rng),
                      BodyGenes("blob", (0.6, 0.95, 0.5, 0.6)), brain)
    return u


def alien_drone(name, rng, pos=(0, 0)):
    # high temperament is the hive's strength, not weakness: with pheromone
    # cohesion pinning equilibrium at 1.0, volatile integration means drones
    # snap BACK to devotion every tick -- twitchy but unbreakable
    brain = BrainGenes("dim", _genes(rng, command=(0.0, 0.05), will=(0.0, 0.06),
                                     temperament=(0.3, 0.5), guile=(0.0, 0.10),
                                     fury=(0.0, 0.05)))
    # the mix the fiction demands: chitin legs and compound eyes (flesh)
    # carrying a grown plasma lance (biotech)
    g = Genome(_slots("plasma_lance", "antenna", "bug_eyes", "insect_leg", rng),
               BodyGenes("tetrapod", (0.75 + rng.random() * 0.2, 0.2 + rng.random() * 0.2,
                                      0.5, 0.4 + rng.random() * 0.4)),
               brain)
    u = Unit(name, brain, side="aliens", pos=pos, profile=ALIEN_PROFILE)
    u.genome = g
    return u


def alien_hive(rng, n_drones=10, base=(50.0, 50.0)):
    queen = alien_queen("The Queen", rng, pos=base)
    drones = [alien_drone(f"Drone-{i+1:02d}", rng,
                          pos=(base[0] + 1 + i % 4, base[1] + 1 + i // 4))
              for i in range(n_drones)]
    for d in drones:
        assign(queen, d, loyalty=0.95)
    return [queen] + drones, queen, drones


# ---- monsters: the baseline (docs/16) ---------------------------------------

def monster_brute(name, rng, pos=(0, 0)):
    brain = BrainGenes(rng.choice(("dim", "average")),
                       _genes(rng, command=(0.1, 0.3), will=(0.3, 0.7),
                              temperament=(0.3, 0.7), guile=(0.1, 0.6),
                              fury=(0.2, 0.9)))
    u = Unit(name, brain, side="monsters", pos=pos, profile=MONSTER_PROFILE)
    u.genome = Genome(_slots("claw_hand", "horn", "cyclops_eye", "hoofed_leg", rng),
                      BodyGenes("tetrapod", (rng.random(), 0.6, 0.5, 0.3)), brain)
    return u
