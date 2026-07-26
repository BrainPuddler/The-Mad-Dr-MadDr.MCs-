using System;

namespace MadDr.MatchCore
{
    /// <summary>docs/03 "Unit affinity & the emitter aura": the Lumen
    /// coupling gene. Match-core never derives this from a genome --
    /// same "accept the genome-derived NUMBER as a spawn parameter, stay
    /// genome-agnostic" pattern already used for <see cref="SimUnit.Speed"/>/
    /// <see cref="SimUnit.Radius"/> (docs/27 Phase C).</summary>
    public enum UnitAffinity
    {
        Solar,
        Lunar,
        Neutral,
    }

    /// <summary>docs/04's eight-stat block, minus Speed (already
    /// <see cref="SimUnit.Speed"/>) -- the genome-derived numbers a
    /// combat-capable unit needs, supplied whole by the caller at spawn
    /// time (Unity's `packages/roster-client` `Combat.Profile` already
    /// computes these from a genome; match-core only ever consumes the
    /// resulting numbers, never a `GenomeDto`). A `SimUnit` spawned
    /// without `CombatStats` (the existing `SpawnUnit` overload/default)
    /// has none of this -- pure movement, exactly as before this
    /// phase.</summary>
    public readonly struct CombatStats
    {
        public readonly int MaxVitality;     // docs/04: 50-400
        public readonly int Power;           // docs/04: 5-40
        public readonly int Armor;           // docs/04: 0-10
        public readonly int Reach;           // docs/04: 1-4 hexes; 1 = melee
        public readonly double Ferocity;     // docs/04: 0.5-2.0 attacks/s
        public readonly int CunningPercent;  // docs/04: 0-25 (%crit chance)
        public readonly UnitAffinity Affinity;

        public CombatStats(int maxVitality, int power, int armor, int reach,
            double ferocity, int cunningPercent, UnitAffinity affinity)
        {
            if (maxVitality <= 0) throw new ArgumentOutOfRangeException(nameof(maxVitality));
            if (reach <= 0) throw new ArgumentOutOfRangeException(nameof(reach));
            if (ferocity <= 0.0) throw new ArgumentOutOfRangeException(nameof(ferocity));
            if (cunningPercent < 0 || cunningPercent > 100) throw new ArgumentOutOfRangeException(nameof(cunningPercent));
            MaxVitality = maxVitality;
            Power = power;
            Armor = armor;
            Reach = reach;
            Ferocity = ferocity;
            CunningPercent = cunningPercent;
            Affinity = affinity;
        }
    }

    /// <summary>docs/04's damage formula, kept as PURE, directly testable
    /// integer math -- deliberately separate from any RNG/position
    /// lookup so the formula itself can be pinned against docs/04's own
    /// worked examples with fixed inputs, independent of how posMod/
    /// emitterMod/luckRoll get computed elsewhere in `MatchState`.
    ///
    /// docs/04's own "Determinism requirements": *"the formula must be
    /// implementable in integer/fixed-point math (multipliers above are
    /// exact hundredths by design) so server and any future client-side
    /// prediction agree bit-for-bit across platforms."* Every multiplier
    /// here is therefore an integer PERCENT (100 = x1.00, 125 = x1.25) --
    /// no `double` anywhere in the actual damage computation, a stricter
    /// bar than the IEEE-double position math elsewhere in this
    /// project.</summary>
    public static class CombatMath
    {
        /// <summary>docs/04 posMod table, integer percent. Front x100,
        /// Flank x125, Rear x150 -- <see cref="MadDr.CityGen.Facing.ArcOf"/>
        /// already classifies the arc (melee/adjacency only; ranged
        /// attackers get a flat 100 today, a documented, deferred gap --
        /// see SimUnit.cs's own header). High-ground and extra-ally
        /// bonuses (+10 each, max +20 for allies) are ADDED on top of the
        /// arc base by the caller, not folded in here, since neither is a
        /// property of the arc itself.</summary>
        public static int PosModForArc(MadDr.CityGen.Arc arc)
        {
            switch (arc)
            {
                case MadDr.CityGen.Arc.Flank: return 125;
                case MadDr.CityGen.Arc.Rear: return 150;
                default: return 100;
            }
        }

        /// <summary>docs/03's affinity/phase/aura table, reused verbatim
        /// (docs/04: "Direct from 03-mana-system.md"). Not in an aura at
        /// all is always 100 regardless of affinity -- "no aura, or
        /// matching affinity outside its phase" both read as 1.00,
        /// docs/03's own table row.</summary>
        public static int EmitterModPercent(UnitAffinity affinity, LumenPhase phase, bool inAnyAura)
        {
            if (!inAnyAura) return 100;
            switch (affinity)
            {
                case UnitAffinity.Neutral:
                    return 110;
                case UnitAffinity.Solar:
                    if (phase == LumenPhase.Day) return 125;
                    if (phase == LumenPhase.Night) return 85;
                    return 100;   // Dusk/Dawn: neither Solar's strong nor weak phase
                case UnitAffinity.Lunar:
                    if (phase == LumenPhase.Night) return 125;
                    if (phase == LumenPhase.Day) return 85;
                    return 100;   // Dusk/Dawn
                default:
                    return 100;
            }
        }

        /// <summary>docs/04: uniform luck roll in [0.85, 1.15], as an
        /// integer percent in [85, 115] -- 31 possible values.</summary>
        public static int RollLuckPercent(SimRng rng) => rng.IntRange(31) + 85;

        /// <summary>docs/04: "Plus a Cunning% chance of a x1.5 critical
        /// (replaces, not stacks with, the uniform roll)." Draws exactly
        /// one RNG value regardless of outcome, so the RNG position
        /// after resolving one attack is always the same shape (one
        /// crit-check draw, one luck-roll draw only if not a
        /// crit) -- see <see cref="ResolveDamage"/>'s own doc comment for
        /// why draw COUNT determinism matters as much as draw VALUE.</summary>
        public static bool RollCrit(SimRng rng, int cunningPercent) => rng.IntRange(100) < cunningPercent;

        /// <summary>docs/04's damage formula, verbatim: <c>damage =
        /// max(1, round(Power x posMod x emitterMod x luckRoll) -
        /// Armor)</c>, extended by docs/23 §7's Lumen-cycle faction
        /// modifier (<paramref name="lumenModPercent"/>, default 100 = no
        /// change -- every pre-Phase-7 call site keeps compiling and
        /// producing the identical result it always did). All FOUR
        /// multiplier terms are integer PERCENTS (see this class's own
        /// header); the product is computed in a `long` to avoid overflow
        /// at the theoretical maxima (posMod up to 180 with the +10/+20
        /// bonuses added by the caller, emitterMod up to 125, luckRoll's
        /// crit replacement at 150, lumenMod up to 115 per docs/23 §7's own
        /// table) before dividing back down by 100^4 with round-half-up
        /// (docs/04 says "round," not "floor").</summary>
        public static int ResolveDamage(int power, int posModPercent, int emitterModPercent, int luckOrCritPercent, int armor, int lumenModPercent = 100)
        {
            var product = (long)power * posModPercent * emitterModPercent * luckOrCritPercent * lumenModPercent;
            var scale = 100L * 100L * 100L * 100L;
            var rounded = (product + scale / 2) / scale;   // round-half-up
            var damage = (int)rounded - armor;
            return Math.Max(1, damage);
        }
    }
}
