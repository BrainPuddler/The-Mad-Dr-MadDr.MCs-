using System;

namespace MadDr.RosterClient
{
    /// <summary>
    /// Physiology -> movement numbers: a line-by-line C# port of
    /// site/creature-renderer.js's locomotionProfile (the display-side
    /// twin of genome-core's energy math). The heart is the engine
    /// (power), mass is the brake, and sprint is gated by circulatory
    /// headroom -- a strained heart barely runs. Speeds are docs/11 v0.1
    /// hex/min placeholders, same as the JS.
    ///
    /// Verified against golden values captured from the real JS
    /// implementation running in node (Tests~/LocomotionTests.cs), the
    /// same discipline as the Rng port: the Lab's gait preview and the
    /// Unity battlefield must agree on how fast a given genome moves, or
    /// the same creature would visibly move differently in the two
    /// places it appears.
    /// </summary>
    public sealed class LocomotionProfile
    {
        public double Mass { get; }
        public double Power { get; }
        public double Margin { get; }
        public double WalkSpeedHexPerMin { get; }
        public double RunSpeedHexPerMin { get; }
        public double WalkHz { get; }
        public double RunHz { get; }
        public string Sprint { get; } // "strong" | "limited" | "none"

        public LocomotionProfile(double mass, double power, double margin,
            double walkSpeedHexPerMin, double runSpeedHexPerMin, double walkHz, double runHz, string sprint)
        {
            Mass = mass;
            Power = power;
            Margin = margin;
            WalkSpeedHexPerMin = walkSpeedHexPerMin;
            RunSpeedHexPerMin = runSpeedHexPerMin;
            WalkHz = walkHz;
            RunHz = runHz;
            Sprint = sprint;
        }

        /// <summary>hex/min -> m/s at docs/18's 20 m hex. The raw v0.1
        /// numbers read slowly at real-world scale (docs/04's own Speed
        /// stat is hex/SEC -- a known inconsistency between the two docs'
        /// placeholder scales, worth a future doc pass); callers pass a
        /// display multiplier until real tuning lands (docs/11 Phase 2).</summary>
        public double WalkMetersPerSecond(double displayMultiplier = 1.0)
        {
            return WalkSpeedHexPerMin * 20.0 / 60.0 * displayMultiplier;
        }

        public double RunMetersPerSecond(double displayMultiplier = 1.0)
        {
            return RunSpeedHexPerMin * 20.0 / 60.0 * displayMultiplier;
        }
    }

    public static class Locomotion
    {
        public static LocomotionProfile Profile(GenomeDto g)
        {
            var plan = g.Body.Plan;
            var bulk = At(g.Body.Params, 1, 0.5);
            var build = At(g.Body.Params, 2, 0.5);
            var legFam = g.Slots.Leg.Family;
            var legCount = At(g.Slots.Leg.Params, 4, 0.5);
            var handFam = g.Slots.Hand.Family;
            var brainSize = BrainSize(g.Brain.Tier);

            // energy output: the heart's pumping capacity
            var vigor = At(g.Heart.Params, 0, 0.5);
            var power = HeartOut(g.Heart.Tier) * (0.7 + 0.6 * vigor);

            // mass: plan volume, bulk, build, and metal lower bodies weigh in
            var planMass = PlanMass(plan);
            var mass = (1.5 + bulk * 2.4) * planMass * (1 + build * 0.35)
                + (legFam == "piston_leg" || legFam == "jet_leg" ? 0.9 : 0);

            // approximate upkeep load (display-side twin of energy.ts)
            var load = 6 + bulk * 8 + brainSize * 1.5 + 10;
            var margin = power - load;
            var p2w = power / (mass * 10);

            var legBase =
                plan == "serpentine" ? 1.8 :
                plan == "blob" ? 0.9 :
                plan == "treant" ? 0.5 :          // rooted; barely shuffles
                plan == "floater" ? 2.8 :         // thruster-driven, built for speed
                plan == "avian" ? 2.9 :           // built to run
                legFam == "talon_leg" ? 2.6 :
                legFam == "insect_leg" ? 2.0 :
                legFam == "piston_leg" ? (legCount < 0.45 ? 2.8 : 2.3) :
                legFam == "jet_leg" ? 2.7 :
                legFam == "tendril_leg" ? 1.6 :
                legFam == "leg_stump" ? 0.6 : 2.2;

            // crab/arachnid forelimbs brace and shove during the low
            // scuttle -- a working hand adds push, a healed stump doesn't
            var armAssist = (plan == "crab" || plan == "arachnid")
                && handFam != null && handFam != "hand_stump" ? 1.1 : 1.0;
            var legBaseA = legBase * armAssist;

            var walkSpeed = legBaseA * Clamp(0.55 + p2w * 0.85, 0.5, 1.8);
            // sprint gate: headroom decides whether "run" means anything
            var sprint = margin > power * 0.25 ? "strong" : margin > 0 ? "limited" : "none";
            var sprintMult = sprint == "strong" ? 1.9 + Math.Min(margin / power, 0.6) * 0.5
                           : sprint == "limited" ? 1.35 : 1.1;
            var runSpeed = walkSpeed * sprintMult;
            var walkHz = Clamp(0.9 + p2w * 0.8 - mass * 0.05, 0.5, 1.6);

            return new LocomotionProfile(
                Round2(mass), Round1(power), Round1(margin),
                Round2(walkSpeed), Round2(runSpeed),
                Round2(walkHz), Round2(walkHz * 1.7), sprint);
        }

        /// <summary>How many legs the Unity body builds for a plan --
        /// presentation-side, matching the Lab renderer's silhouettes:
        /// legless plans (serpentine/blob/floater) slither/bounce/hover,
        /// treant shuffles on root-stumps.</summary>
        public static int LegsFor(string plan)
        {
            switch (plan)
            {
                case "tetrapod": return 4;
                case "winged": return 2;
                case "avian": return 2;
                case "crab": return 6;
                case "arachnid": return 8;
                case "treant": return 2;
                default: return 0; // serpentine, blob, floater
            }
        }

        private static double At(double[] arr, int i, double fallback)
        {
            return arr != null && i < arr.Length ? arr[i] : fallback;
        }

        private static int BrainSize(string tier)
        {
            switch (tier)
            {
                case "dim": return 1;
                case "average": return 2;
                case "gifted": return 3;
                case "mastermind": return 4;
                default: return 2;
            }
        }

        private static double HeartOut(string tier)
        {
            switch (tier)
            {
                case "faint": return 14;
                case "steady": return 26;
                case "strong": return 42;
                case "titan": return 64;
                default: return 26;
            }
        }

        private static double PlanMass(string plan)
        {
            switch (plan)
            {
                case "tetrapod": return 1.0;
                case "winged": return 0.8;
                case "serpentine": return 1.15;
                case "blob": return 1.55;
                case "crab": return 1.3;
                case "arachnid": return 1.1;
                case "avian": return 0.85;
                case "treant": return 1.7;
                case "floater": return 0.6;
                default: return 1.0;
            }
        }

        private static double Clamp(double x, double lo, double hi)
        {
            return x < lo ? lo : x > hi ? hi : x;
        }

        private static double Round1(double x) { return Math.Round(x, 1, MidpointRounding.AwayFromZero); }
        private static double Round2(double x) { return Math.Round(x, 2, MidpointRounding.AwayFromZero); }
    }
}
