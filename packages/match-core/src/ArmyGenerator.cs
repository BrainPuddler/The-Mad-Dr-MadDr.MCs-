using System;
using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>2026-08 (creator direction: "Create a faction based army
    /// generator. To start making opponents for the game"). The missing
    /// piece a docs/12 status audit found: `SkirmishCommander`/
    /// `CommanderPersonality` already exist and can DECIDE what an army
    /// does in battle, and `FactionRoster.cs`'s `UnitRosterDef` already
    /// prices every unit -- but nothing anywhere could look at a faction
    /// and produce an actual ARMY (a composition of units) to hand a
    /// commander in the first place. This class is that step: given a
    /// faction, a personality, and a resource budget, deterministically
    /// return how many of which <see cref="RosterUnitKind"/> to field.
    ///
    /// Deliberately pure and `MatchState`-free: no city, no RNG advanced
    /// as a side effect of ticking, nothing here is simulation state (same
    /// "DATA/logic, not part of the tick" contract <see
    /// cref="CommanderPersonality"/> and <see cref="FactionRoster"/> already
    /// establish). Placement (which hex each spawned unit lands on) is
    /// deliberately NOT this class's job -- the caller already owns hex-
    /// finding (`MatchState.FindOpenHexNear` sim-side, `RuntimeCityBuilder.
    /// FindOpenHexWide` in Unity); mixing "how many Tanks" with "which hex"
    /// would make this untestable without a live city.
    ///
    /// 2026-08 follow-up (creator direction: "the roster needs to be able
    /// to generate enemies for all races"): **all four factions are now
    /// supported.** `MadDoctor` draws from `FactionRoster.cs`'s new
    /// generic placeholder trio (`ShamblingGrunt`/`SporeBrute`/
    /// `Abomination` -- a flagged v0.1 stand-in, NOT the real genome-bred
    /// creature pipeline, see that enum's own header). `Mixed` draws from
    /// the UNION of every faction's roster -- `MatchState.SpawnRosterUnit`
    /// and `MatchState.CanTrainUnit` already special-case a Mixed player
    /// to field ANY faction's roster kind, each tagged with its own
    /// `SimUnit.RaceOverride` (`MixedFactionTests.cs`); this class reuses
    /// that exact contract rather than inventing a second one. `RosterFor`
    /// is the only method that changed to make this true -- <see
    /// cref="Generate"/>'s own knapsack loop already treated "the roster"
    /// as an opaque list, so it needed no changes. The throw below is now
    /// a pure defensive backstop (every faction has a non-empty roster as
    /// of this pass) rather than an expected path.</summary>
    public static class ArmyGenerator
    {
        /// <summary>Hard ceiling on total units returned, independent of
        /// budget -- a pure safety backstop against a budget so large the
        /// knapsack loop would otherwise run indefinitely; no v0.1 balance
        /// pass has ever suggested an army this size is desirable.</summary>
        public const int MaxUnits = 60;

        /// <summary>Given `faction`'s own roster (<see
        /// cref="UnitRosterDef.AllDefs"/>, filtered to `faction`), spend
        /// `budget` (a real wallet -- the SAME <see cref="ResourceKind"/>
        /// currency <see cref="UnitRosterDef.Cost"/> already prices units
        /// in, never an invented cross-faction "points" abstraction) on a
        /// composition weighted by `personality`.
        ///
        /// **Only <see cref="CommanderTrait.Aggression"/> and <see
        /// cref="CommanderTrait.Caution"/> drive unit choice here** --
        /// a deliberately narrow mapping, not an oversight. Those two
        /// trace directly to a real army-composition question ("glass
        /// cannons or durable line troops?"); the other four traits
        /// (Greed/Territoriality/Opportunism/Discipline) already have real
        /// meaning for <see cref="SkirmishCommander"/>'s IN-BATTLE
        /// decisions (loot priority, ground-holding, target selection,
        /// plan commitment) but have no natural "which units do I buy"
        /// translation -- inventing one would be exactly the kind of
        /// faked mapping this codebase's own standing policy (CLAUDE.md:
        /// "validation = failed experiment, never silent clamping"; the
        /// same honesty extends to not pretending an unused parameter is
        /// doing something) argues against. Left unused, documented as
        /// such, not silently threaded through for show.
        ///
        /// Algorithm: weighted-random knapsack fill off `rng` (never
        /// `Math.Random` -- the repo's determinism invariant). Each
        /// candidate kind's weight is its personality-scored appeal
        /// (favor high `Combat.Power`/`Combat.Ferocity` as Aggression
        /// rises, high `Combat.MaxVitality`/`Combat.Armor` as Caution
        /// rises, each normalized against `faction`'s own roster range so
        /// Army and Hive -- whose absolute stat numbers differ a lot --
        /// score comparably). One kind is drawn per iteration, weighted,
        /// among kinds that still fit EVERY remaining budget line; the
        /// loop stops when nothing affordable remains or <see
        /// cref="MaxUnits"/> is hit. Same `(faction, personality, budget,
        /// rng state)` always yields the same composition -- the same
        /// "draw COUNT and VALUE both matter" discipline <see
        /// cref="CommanderPersonality.Generate(SimRng)"/> already
        /// documents.</summary>
        public static IReadOnlyList<(RosterUnitKind Kind, int Count)> Generate(
            FactionId faction,
            CommanderPersonality personality,
            IReadOnlyDictionary<ResourceKind, int> budget,
            SimRng rng)
        {
            if (budget == null) throw new ArgumentNullException(nameof(budget));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var roster = RosterFor(faction);
            if (roster.Count == 0)
                throw new ArgumentException(
                    $"ArmyGenerator has no roster data at all for {faction} -- every FactionId should " +
                    "have at least one UnitRosterDef entry (or, for Mixed, the union should be " +
                    "non-empty). This should be unreachable; check FactionRoster.cs.",
                    nameof(faction));

            var remaining = new Dictionary<ResourceKind, int>();
            foreach (var kv in budget) remaining[kv.Key] = kv.Value;

            var (minPowerDensity, maxPowerDensity, minVitalityDensity, maxVitalityDensity) = StatRange(roster);
            var counts = new Dictionary<RosterUnitKind, int>();
            var totalUnits = 0;

            while (totalUnits < MaxUnits)
            {
                var affordable = new List<UnitRosterDef>();
                var weights = new List<int>();
                foreach (var def in roster)
                {
                    if (!Affordable(def, remaining)) continue;
                    affordable.Add(def);
                    weights.Add(Weight(def, personality, minPowerDensity, maxPowerDensity, minVitalityDensity, maxVitalityDensity));
                }
                if (affordable.Count == 0) break;

                var totalWeight = 0;
                foreach (var w in weights) totalWeight += w;
                var pick = totalWeight > 0 ? WeightedPick(rng, weights, totalWeight) : rng.IntRange(affordable.Count);

                var chosen = affordable[pick];
                foreach (var (resource, amount) in chosen.Cost) remaining[resource] -= amount;
                counts.TryGetValue(chosen.Kind, out var soFar);
                counts[chosen.Kind] = soFar + 1;
                totalUnits++;
            }

            // Stable output order (roster order), not insertion order --
            // deterministic and readable regardless of draw sequence.
            var result = new List<(RosterUnitKind, int)>();
            foreach (var def in roster)
                if (counts.TryGetValue(def.Kind, out var n))
                    result.Add((def.Kind, n));
            return result;
        }

        /// <summary>`Mixed` gets the union of every faction's roster --
        /// not filtered by `def.Faction == faction` at all -- matching
        /// `MatchState.SpawnRosterUnit`/`CanTrainUnit`'s own "a Mixed
        /// player can field/train ANY faction's roster kind" contract
        /// (`MixedFactionTests.cs`). Every other faction keeps the exact
        /// same single-faction filter as before.</summary>
        private static List<UnitRosterDef> RosterFor(FactionId faction)
        {
            var list = new List<UnitRosterDef>();
            foreach (var def in UnitRosterDef.AllDefs)
                if (faction == FactionId.Mixed || def.Faction == faction) list.Add(def);
            return list;
        }

        private static bool Affordable(UnitRosterDef def, Dictionary<ResourceKind, int> remaining)
        {
            foreach (var (resource, amount) in def.Cost)
            {
                if (!remaining.TryGetValue(resource, out var have) || have < amount) return false;
            }
            return true;
        }

        /// <summary>Cost in "generic budget units" a unit spends -- the
        /// flat sum of every <see cref="ResourceKind"/> line in its <see
        /// cref="UnitRosterDef.Cost"/>. v0.1 has no established exchange
        /// rate between Bones/Fuel/Ichor, so this is a deliberately crude
        /// stand-in (1 Bone == 1 Fuel == 1 Ichor for weighting purposes
        /// only -- it never touches the actual per-resource budget
        /// bookkeeping in <see cref="Generate"/>, which stays exact). Good
        /// enough for "which unit is a better BUY," not a claim about real
        /// economic value.</summary>
        private static int CostUnits(UnitRosterDef def)
        {
            var total = 0;
            foreach (var (_, amount) in def.Cost) total += amount;
            return Math.Max(1, total);
        }

        private static (double minPowerDensity, double maxPowerDensity, double minVitalityDensity, double maxVitalityDensity) StatRange(List<UnitRosterDef> roster)
        {
            double minPowerDensity = double.MaxValue, maxPowerDensity = double.MinValue;
            double minVitalityDensity = double.MaxValue, maxVitalityDensity = double.MinValue;
            foreach (var def in roster)
            {
                var cost = CostUnits(def);
                var powerDensity = (double)def.Combat.Power / cost;
                var vitalityDensity = (double)def.Combat.MaxVitality / cost;
                if (powerDensity < minPowerDensity) minPowerDensity = powerDensity;
                if (powerDensity > maxPowerDensity) maxPowerDensity = powerDensity;
                if (vitalityDensity < minVitalityDensity) minVitalityDensity = vitalityDensity;
                if (vitalityDensity > maxVitalityDensity) maxVitalityDensity = vitalityDensity;
            }
            return (minPowerDensity, maxPowerDensity, minVitalityDensity, maxVitalityDensity);
        }

        /// <summary>Personality-scored appeal. Scores PER-COST density
        /// (Power/cost, MaxVitality/cost -- see <see cref="CostUnits"/>),
        /// not raw stats: a knapsack fill should prefer whichever unit
        /// gives the most Power (or Vitality) per resource spent, not
        /// whichever unit has the single highest absolute stat --
        /// otherwise one strictly-best-on-paper unit (a real risk with
        /// v0.1's still-unbalanced placeholder numbers, see
        /// `FactionRoster.cs`'s own header) would dominate EVERY
        /// personality equally and Aggression/Caution would stop mattering.
        /// `double` math throughout: this class is explicitly not part of
        /// the tick (see its own header), the same "outside the tick, so
        /// double is fine" precedent <see cref="CommanderPersonality"/>
        /// already sets -- only the FINAL weight is rounded to an int for
        /// the RNG draw. Normalizes each density into [0,100] against the
        /// faction's own roster range (a 0-spread roster reads as 100
        /// everywhere rather than dividing by zero), blends by Aggression/
        /// Caution, and floors at 1 so no affordable kind is ever
        /// unreachable.</summary>
        private static int Weight(UnitRosterDef def, CommanderPersonality personality,
            double minPowerDensity, double maxPowerDensity, double minVitalityDensity, double maxVitalityDensity)
        {
            var cost = CostUnits(def);
            var powerDensity = (double)def.Combat.Power / cost;
            var vitalityDensity = (double)def.Combat.MaxVitality / cost;
            var powerScore = Normalize(powerDensity, minPowerDensity, maxPowerDensity);
            var vitalityScore = Normalize(vitalityDensity, minVitalityDensity, maxVitalityDensity);
            var weight = powerScore * personality.Aggression + vitalityScore * personality.Caution;
            return Math.Max(1, (int)Math.Round(weight));
        }

        private static double Normalize(double value, double min, double max) =>
            max <= min ? 100.0 : (value - min) * 100.0 / (max - min);

        private static int WeightedPick(SimRng rng, List<int> weights, int totalWeight)
        {
            var roll = rng.IntRange(totalWeight);
            var acc = 0;
            for (var i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (roll < acc) return i;
            }
            return weights.Count - 1; // unreachable given roll < totalWeight, kept as a safe fallback
        }
    }
}
