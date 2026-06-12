"""Command, loyalty, and rebellion simulation (docs/16-brains-behavior-command.md).

The same genotype->expression split as parts and bodies: a brain's genes
express into BEHAVIOR. Brain genes (genome.BRAIN_AXES) plus brain size (tier)
determine who can command whom, how firmly, and when control snaps.

All randomness is from a caller-supplied seeded RNG -- the server-authoritative
determinism requirement (docs/09): same seed, same uprising.

Three expressed quantities, all pure functions of the brain:

  capacity(brain)  control points a commander can project   -> size*(0.4+0.8*command)
  cost(brain)      control points a subordinate consumes    -> size*(0.3+0.7*will)
  radius(brain)    control range of a commander (units)     -> 4 + 8*command

A commander can hold subordinates whose summed cost <= capacity; pushing past
capacity ("overload") erodes everyone's loyalty. Distance beyond radius,
willfulness, ambition (guile vs the commander's command), and battlefield
stress all pull loyalty down; spare capacity and a commanding presence hold it
up. Temperament sets how fast and how noisily loyalty swings.
"""

from dataclasses import dataclass, field
import math

WAVER = 0.45      # below this, a subordinate wavers (obeys unreliably)
REBEL = 0.20      # below this, control snaps: feral or open rebellion
RAGE_EXIT = 0.35  # berserk burns out when rage falls below this
EXHAUST_TICKS = 6

CONTROLLED, WAVERING, FERAL, REBEL_STATE = "controlled", "wavering", "feral", "rebel"
BERSERK, EXHAUSTED = "berserk", "exhausted"


def capacity(brain):
    return brain.size * (0.4 + 0.8 * brain.axis("command"))


def cost(brain):
    return brain.size * (0.3 + 0.7 * brain.axis("will"))


def radius(brain):
    return 4.0 + 8.0 * brain.axis("command")


# --- berserk expression (docs/16): power and toughness up, control gone ----

def berserk_threshold(brain):
    """Rage level at which the unit snaps. For low fury the threshold sits
    above the rage cap (1.0) -- a placid brain can NEVER berserk."""
    return 1.05 - 0.6 * brain.axis("fury")


def berserk_power_mult(brain):
    return 1.3 + 0.5 * brain.axis("fury")


def berserk_armor_bonus(brain):
    return 2.0 + 4.0 * brain.axis("fury")


@dataclass
class Unit:
    name: str
    brain: object                 # genome.BrainGenes
    side: str = "player"
    pos: tuple = (0.0, 0.0)
    hp: float = 100.0
    alive: bool = True
    commander: "Unit" = None      # who controls me (None = independent/own will)
    loyalty: float = 0.8          # to my commander
    state: str = CONTROLLED
    rage: float = 0.0             # builds under stress; berserk past threshold
    exhaust_timer: int = 0
    subordinates: list = field(default_factory=list)
    history: list = field(default_factory=list)        # loyalty per tick
    rage_history: list = field(default_factory=list)   # rage per tick
    hp_history: list = field(default_factory=list)     # hp per tick

    # convenience gene accessors
    @property
    def command(self): return self.brain.axis("command")
    @property
    def will(self): return self.brain.axis("will")
    @property
    def temperament(self): return self.brain.axis("temperament")
    @property
    def guile(self): return self.brain.axis("guile")
    @property
    def fury(self): return self.brain.axis("fury")


def _dist(a, b):
    return math.hypot(a.pos[0] - b.pos[0], a.pos[1] - b.pos[1])


def assign(commander, subordinate, loyalty=0.85):
    subordinate.commander = commander
    subordinate.loyalty = loyalty
    subordinate.state = CONTROLLED
    if subordinate not in commander.subordinates:
        commander.subordinates.append(subordinate)


def _load(commander):
    """Fraction of capacity consumed by current living subordinates."""
    cap = capacity(commander.brain)
    used = sum(cost(s.brain) for s in commander.subordinates if s.alive)
    return used / cap if cap > 0 else 99.0


def loyalty_equilibrium(sub, commander):
    """The loyalty value this subordinate's situation pulls toward."""
    load = _load(commander)
    overload = max(0.0, load - 1.0)
    out_of_range = max(0.0, (_dist(sub, commander) - radius(commander.brain)) / 6.0)
    out_of_range = min(out_of_range, 1.0)
    # ambition: a guileful subordinate as commanding as its master eyes the throne
    ambition = sub.guile * max(0.0, (sub.command - commander.command) + 0.15)

    eq = (0.55
          + 0.30 * (1.0 - load)          # spare capacity reassures
          - 0.45 * overload              # being over-extended is corrosive
          - 0.25 * sub.will              # the willful chafe at any leash
          - 0.30 * out_of_range          # out of sight, out of control
          - 0.35 * ambition              # the ambitious plot
          + 0.20 * (commander.command - 0.5))  # a commanding presence holds them
    return max(0.0, min(1.0, eq))


def step(units, rng, stress=None, lumen="day"):
    """Advance one tick. Returns a list of human-readable events.

    stress: optional dict {unit_name: loyalty_hit} for combat damage etc.
            (the same stress also feeds rage)
    lumen:  "day" | "night" -- Night amplifies fury (docs/03 x docs/16:
            the werewolf coupling).
    """
    stress = stress or {}
    events = []

    # 0) rage & berserk dynamics
    for u in units:
        if not u.alive:
            continue
        hit = stress.get(u.name, 0.0)
        gain = hit * (0.4 + 3.0 * u.fury)
        if lumen == "night":
            gain *= 1.0 + 1.2 * u.fury          # the moon stirs the blood
        u.rage = max(0.0, min(1.0, u.rage + gain - (0.025 if hit == 0.0 else 0.0)))

        if u.state == BERSERK:
            u.rage = max(0.0, u.rage - 0.05)    # the frenzy burns itself out
            _berserk_attack(u, units, events)
            if u.rage < RAGE_EXIT:
                u.state = EXHAUSTED
                u.exhaust_timer = EXHAUST_TICKS
                events.append(f"{u.name} collapses, EXHAUSTED, the fury spent")
        elif u.state == EXHAUSTED:
            u.exhaust_timer -= 1
            if u.exhaust_timer <= 0:
                if u.commander is not None and u.commander.alive:
                    u.state = CONTROLLED
                    u.loyalty = max(u.loyalty, 0.5)
                    events.append(f"{u.commander.name} re-asserts control over {u.name}")
                else:
                    u.state = FERAL
                    events.append(f"{u.name} rises with no master -- feral")
        elif u.rage > berserk_threshold(u.brain):
            u.state = BERSERK
            events.append(
                f"{u.name} goes BERSERK ({'+%d%%' % round((berserk_power_mult(u.brain) - 1) * 100)} power, "
                f"+{berserk_armor_bonus(u.brain):.0f} armor) -- strikes at friend and foe alike")

        u.rage_history.append(u.rage)
        u.hp_history.append(u.hp if u.alive else 0.0)

    # 1) loyalty integration toward equilibrium (only for the controlled;
    #    berserk/exhausted units are beyond loyalty -- it is frozen)
    for u in units:
        if not u.alive or u.commander is None or u.state in (BERSERK, EXHAUSTED):
            if u.alive:
                u.history.append(u.loyalty)
            continue
        if not u.commander.alive:
            u.history.append(u.loyalty)
            continue
        eq = loyalty_equilibrium(u, u.commander)
        speed = 0.15 + 0.30 * u.temperament
        noise = rng.gauss(0.0, 0.04 + 0.10 * u.temperament)
        u.loyalty = max(0.0, min(1.0, u.loyalty + (eq - u.loyalty) * speed + noise))
        u.loyalty = max(0.0, u.loyalty - stress.get(u.name, 0.0))
        u.history.append(u.loyalty)

    # 2) state transitions + control snaps
    for u in units:
        if not u.alive or u.commander is None or u.state in (BERSERK, EXHAUSTED):
            continue
        prev = u.state
        if u.loyalty >= WAVER:
            u.state = CONTROLLED
        elif u.loyalty >= REBEL:
            u.state = WAVERING
        else:
            # control snaps. Ambition/guile -> open rebellion; otherwise feral.
            u.state = REBEL_STATE if u.guile > 0.5 else FERAL

        if u.state == prev:
            continue
        if u.state == WAVERING:
            events.append(f"{u.name} wavers (loyalty {u.loyalty:.2f}) under {u.commander.name}")
        elif u.state == FERAL:
            # A mindless beast: abandons its own troops and attacks the nearest.
            events.append(f"{u.name} breaks free and goes FERAL -- abandons its troops, attacks the nearest")
            _orphan(u, events)
            _unlink(u)
        elif u.state == REBEL_STATE:
            # An ambitious coup: marches off WITH its own troops, may seize more.
            events.append(f"{u.name} REBELS against {u.commander.name}!")
            _try_usurp(u, events)
            _unlink(u)              # keeps its own subordinates -- a breakaway warband

    return events


def _unlink(u):
    """Sever u's link to its own commander (u keeps its subordinates)."""
    if u.commander and u in u.commander.subordinates:
        u.commander.subordinates.remove(u)
    u.commander = None


def _orphan(u, events):
    """u abandons its subordinates -- a leadership vacuum, loyalty shock each."""
    for s in list(u.subordinates):
        s.loyalty = max(0.0, s.loyalty - 0.40)
        s.commander = None
        events.append(f"  -> {s.name} is abandoned by {u.name} (loyalty -> {s.loyalty:.2f})")
    u.subordinates = []


def _try_usurp(rebel, events):
    """A rebel that out-commands its master seizes the master's other troops."""
    master = rebel.commander
    if master is None:
        return False
    if rebel.command > master.command and rebel.guile > 0.55:
        seized = [s for s in list(master.subordinates) if s is not rebel and s.alive]
        for s in seized:
            master.subordinates.remove(s)
            assign(rebel, s, loyalty=0.6)
        if seized:
            events.append(f"  -> {rebel.name} USURPS {master.name}, seizing {len(seized)} of its troops")
        return True
    return False


def _berserk_attack(u, units, events):
    """A berserk unit savages whatever is nearest -- friend or foe."""
    targets = [t for t in units if t is not u and t.alive]
    if not targets:
        return
    target = min(targets, key=lambda t: _dist(u, t))
    armor = berserk_armor_bonus(target.brain) if target.state == BERSERK else 0.0
    dmg = max(1.0, 11.0 * berserk_power_mult(u.brain) - armor)
    target.hp -= dmg
    if target.hp <= 0:
        same = "its own" if target.side == u.side else "the enemy"
        events.append(f"{u.name} MAULS {target.name} to death -- {same} unit")
        kill(target, units, events)
    else:
        # surviving allies nearby are shaken
        if target.side == u.side and target.commander is not None:
            target.loyalty = max(0.0, target.loyalty - 0.08)


def kill(unit, units, events):
    """Kill a unit. Its subordinates suffer a leadership-vacuum loyalty shock;
    decapitating a commander can collapse a whole branch."""
    unit.alive = False
    unit.state = "dead"
    orphans = [s for s in units if s.commander is unit and s.alive]
    for s in orphans:
        s.loyalty = max(0.0, s.loyalty - 0.45)
        events.append(f"{s.name} loses its commander {unit.name} (loyalty -> {s.loyalty:.2f})")
    return orphans


def can_command(commander, subordinate):
    """Could this commander, in principle, hold this subordinate? (capacity gate)"""
    return capacity(commander.brain) >= cost(subordinate.brain)
