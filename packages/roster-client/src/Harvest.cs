using System.Collections.Generic;

namespace MadDr.RosterClient
{
    /// <summary>
    /// Harvest & carry -- the display-side C# twin of genome-core's
    /// harvest.ts (docs/22 harvester morphology), the same discipline as
    /// Locomotion/Weapon: the Lab and the Unity battlefield must agree on
    /// how much a given genome gathers, carries, and how much its load
    /// slows it, or the same harvester would behave differently in the two
    /// places it appears. Verified against golden values captured from the
    /// real harvest.ts running in node (Tests~/HarvestTests.cs).
    ///
    /// Nothing here is a new gene: gather comes from the HAND family, carry
    /// capacity from a SENSOR-slot storage vessel plus body bulk, and
    /// weight slows the carrier -- twice as hard for flyers (winged /
    /// floater), because flight pays for every unit carried.
    /// </summary>
    public sealed class HarvestProfile
    {
        public double GatherBlood { get; }
        public double GatherBone { get; }
        public double GatherBrain { get; }
        /// <summary>The hand tool can drain a target that is still ALIVE
        /// (the lamprey/siphon fantasy), not just corpses.</summary>
        public bool DrainsLiving { get; }
        /// <summary>Total onboard carry capacity, all lanes pooled.</summary>
        public double Capacity { get; }
        /// <summary>The sensor slot carries a dedicated storage vessel.</summary>
        public bool HasVessel { get; }
        /// <summary>winged/floater plans -- the ones the weight rule bites.</summary>
        public bool Flies { get; }

        public HarvestProfile(double gatherBlood, double gatherBone, double gatherBrain,
            bool drainsLiving, double capacity, bool hasVessel, bool flies)
        {
            GatherBlood = gatherBlood;
            GatherBone = gatherBone;
            GatherBrain = gatherBrain;
            DrainsLiving = drainsLiving;
            Capacity = capacity;
            HasVessel = hasVessel;
            Flies = flies;
        }
    }

    public static class Harvest
    {
        // Gather-rate multipliers by hand family, per resource lane -- must
        // match harvest.ts HARVEST_TOOLS. Families absent here use TOOLLESS.
        private struct Tool
        {
            public double Blood, Bone, Brain;
            public bool Living;
            public Tool(double bl, double bo, double br, bool living) { Blood = bl; Bone = bo; Brain = br; Living = living; }
        }

        private static readonly Tool Toolless = new Tool(0.4, 0.4, 0.2, false);

        private static readonly Dictionary<string, Tool> Tools = new Dictionary<string, Tool>
        {
            { "lamprey_maw", new Tool(3.0, 0.3, 0.4, true) },
            { "ichor_siphon", new Tool(2.4, 0.3, 0.8, true) },
            { "bone_saw", new Tool(0.5, 3.0, 0.6, false) },
            { "claw_hand", new Tool(1.0, 1.0, 0.5, false) },
            { "pincer", new Tool(0.8, 1.4, 0.5, false) },
            { "chain_blade", new Tool(0.7, 1.8, 0.3, false) },
            { "tentacle", new Tool(1.2, 0.4, 0.6, false) },
        };

        // Storage-vessel families (sensor homolog) and full-expression
        // capacity -- must match harvest.ts STORAGE_FAMILIES.
        private static readonly Dictionary<string, double> Storage = new Dictionary<string, double>
        {
            { "storage_bladder", 60 },
            { "steel_tank", 70 },
            { "amber_vesicle", 55 },
        };

        // v0.1 tuning knobs (mirror harvest.ts) ------------------------------
        private const double BaseCapacity = 10;
        private const double BulkCapacity = 15;
        private const double BlobBonus = 1.5;
        private const double GroundPenalty = 0.25;
        private const double FlightPenalty = 0.5;
        private const double GroundFloor = 0.6;
        private const double FlightFloor = 0.4;

        public static HarvestProfile Profile(GenomeDto g)
        {
            var plan = g.Body.Plan;
            var ignored = IgnoredSlots(plan);

            // gather: from the hand tool, scaled by its expressed working size
            double gb = 0, go = 0, gr = 0;
            var drainsLiving = false;
            if (!ignored.Contains("hand"))
            {
                var hand = g.Slots.Hand;
                if (!IsVestigial(hand.Family))
                {
                    Tool spec;
                    if (!Tools.TryGetValue(hand.Family, out spec)) spec = Toolless;
                    var length = Express(hand.Family, Axis.Length, At(hand.Params, 0, 0.5));
                    var girth = Express(hand.Family, Axis.Girth, At(hand.Params, 1, 0.5));
                    var size = 0.5 + (length + girth) / 2.0; // [0.5, 1.5]
                    gb = spec.Blood * size;
                    go = spec.Bone * size;
                    gr = spec.Brain * size;
                    drainsLiving = spec.Living;
                }
            }

            // capacity: body bulk + the storage vessel, times the blob bonus
            var capacity = BaseCapacity + BulkCapacity * At(g.Body.Params, 1, 0.5);
            var hasVessel = false;
            if (!ignored.Contains("sensor"))
            {
                var sensor = g.Slots.Sensor;
                double vesselMax;
                if (Storage.TryGetValue(sensor.Family, out vesselMax))
                {
                    hasVessel = true;
                    var length = Express(sensor.Family, Axis.Length, At(sensor.Params, 0, 0.5));
                    var girth = Express(sensor.Family, Axis.Girth, At(sensor.Params, 1, 0.5));
                    capacity += vesselMax * ((length + girth) / 2.0);
                }
            }
            if (plan == "blob") capacity *= BlobBonus;

            return new HarvestProfile(gb, go, gr, drainsLiving, capacity, hasVessel, Flies(plan));
        }

        /// <summary>Speed multiplier for a carrier at `fill` (0..1) on the
        /// ground -- linear, floored: laden trudging, never stalling.</summary>
        public static double GroundSpeedFactor(double fill)
        {
            var f = Clamp01(fill);
            var factor = 1 - GroundPenalty * f;
            return factor < GroundFloor ? GroundFloor : factor;
        }

        /// <summary>Speed multiplier for a FLYING carrier at `fill` -- twice
        /// the ground sensitivity (every unit aloft is paid for) but
        /// floored: a full tank makes a slow flyer, never a grounded one
        /// (docs/22 SS1 never-annoying contract).</summary>
        public static double FlightSpeedFactor(double fill)
        {
            var f = Clamp01(fill);
            var factor = 1 - FlightPenalty * f;
            return factor < FlightFloor ? FlightFloor : factor;
        }

        public static bool Flies(string plan)
        {
            return plan == "winged" || plan == "floater";
        }

        // ---- canalized expression (subset of catalog.ts bounds) -------------
        // Only length/girth are read here, and only families whose bounds
        // DIFFER from the [0,1] default need an entry; everything else
        // expresses as identity. Kept in lockstep with catalog.ts by the
        // golden test.
        private enum Axis { Length, Girth }

        private static double Express(string family, Axis axis, double gene)
        {
            double lo = 0, hi = 1;
            switch (family)
            {
                case "lamprey_maw": if (axis == Axis.Girth) { lo = 0.25; hi = 0.85; } break;
                case "ichor_siphon": if (axis == Axis.Girth) { lo = 0.2; hi = 0.7; } break;
                case "bone_saw": if (axis == Axis.Girth) { lo = 0.3; hi = 0.9; } break;
                case "chain_blade": if (axis == Axis.Girth) { lo = 0.3; hi = 0.9; } break;
                case "tentacle": if (axis == Axis.Girth) { lo = 0.0; hi = 0.7; } break;
                case "spore_launcher": if (axis == Axis.Girth) { lo = 0.35; hi = 1.0; } break;
                case "photon_blaster": if (axis == Axis.Girth) { lo = 0.4; hi = 1.0; } break;
                case "storage_bladder":
                    if (axis == Axis.Girth) { lo = 0.4; hi = 1.0; } else { lo = 0.2; hi = 0.8; }
                    break;
                case "steel_tank":
                    if (axis == Axis.Girth) { lo = 0.35; hi = 0.9; } else { lo = 0.3; hi = 0.9; }
                    break;
                case "amber_vesicle": if (axis == Axis.Girth) { lo = 0.3; hi = 0.9; } break;
            }
            return lo + gene * (hi - lo);
        }

        private static bool IsVestigial(string family)
        {
            return family == "hand_stump" || family == "sensor_stub"
                || family == "eye_socket" || family == "leg_stump";
        }

        private static HashSet<string> IgnoredSlots(string plan)
        {
            switch (plan)
            {
                case "blob": return new HashSet<string> { "leg" };
                case "serpentine": return new HashSet<string> { "leg" };
                case "treant": return new HashSet<string> { "leg" };
                case "floater": return new HashSet<string> { "leg" };
                default: return new HashSet<string>();
            }
        }

        private static double At(double[] arr, int i, double fallback)
        {
            return arr != null && i < arr.Length ? arr[i] : fallback;
        }

        private static double Clamp01(double x)
        {
            return x < 0 ? 0 : x > 1 ? 1 : x;
        }
    }
}
