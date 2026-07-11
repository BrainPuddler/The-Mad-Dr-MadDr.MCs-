using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>
    /// The procedural city generator, docs/18 SS2: seeded road-network
    /// growth, block subdivision, landmark allocation (emitter XOR
    /// Community Hub per node), and building-footprint placement -- all on
    /// the hex index, in integers, so the result is a pure function of
    /// (seed, preset): both match clients generate the identical city from
    /// the seed alone and the city itself is never transmitted.
    ///
    /// Determinism rules obeyed throughout (same discipline as
    /// genome-core): the only randomness is the seeded Rng passed through;
    /// no wall-clock, no System.Random, and no iteration of hash
    /// containers ever affects output or RNG draw order -- every loop that
    /// matters walks an explicitly (R, Q)-sorted list.
    ///
    /// The skin pass and prop/dressing pass from docs/18 SS2's pipeline are
    /// deliberately absent: those consume this model renderer-side using
    /// the preset's art kits. This class is geometry and allocation only.
    /// </summary>
    public static class CityGenerator
    {
        public static CityModel Generate(uint seed, CityPreset preset)
        {
            var rng = new Rng(seed);
            var width = preset.WidthHexes;
            var height = preset.HeightHexes;
            var center = HexCoord.FromOffset(width / 2, height / 2);

            // -- 1. region + roads (pattern is pure geometry, no RNG) ----
            var region = new HashSet<HexCoord>();
            var roads = new HashSet<HexCoord>();
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var hex = HexCoord.FromOffset(col, row);
                    region.Add(hex);
                    if (IsRoad(preset, col, row, hex, center)) roads.Add(hex);
                }
            }

            // -- 2. blocks: connected components of the non-road field ---
            var blocks = FindBlocks(region, roads, width, height);

            // -- 3. landmarks: emitter XOR Community Hub per node --------
            var occupied = new HashSet<HexCoord>();
            var landmarks = new List<Landmark>();
            var buildings = new List<Building>();
            PlaceLandmarks(preset, center, blocks, occupied, landmarks, buildings);

            // -- 4. ordinary buildings fill the remaining block space ----
            PlaceBuildings(preset, rng, blocks, occupied, buildings);

            var roadList = new List<HexCoord>(roads);
            roadList.Sort(CompareRQ);
            return new CityModel(seed, preset.Name, width, height, roadList, buildings, landmarks);
        }

        // ---- roads ----------------------------------------------------

        private static bool IsRoad(CityPreset preset, int col, int row, HexCoord hex, HexCoord center)
        {
            var pitch = preset.BlockPitch;
            switch (preset.Pattern)
            {
                case RoadPattern.Grid:
                    // Dense grid: streets both ways at the block pitch.
                    return row % pitch == 0 || col % pitch == 0;

                case RoadPattern.MainStreet:
                    // One arterial through the middle + perpendicular
                    // streets at the pitch + sparser parallel residentials.
                    return row == preset.HeightHexes / 2
                        || col % pitch == 0
                        || row % (pitch * 2) == 0;

                case RoadPattern.Radial:
                {
                    // Ring roads every pitch hexes; 6 spokes along the hex
                    // axes, starting at distance 2 so the central plaza
                    // block (center + ring 1, 7 hexes) stays whole.
                    var d = center.DistanceTo(hex);
                    if (d > 0 && d % pitch == 0) return true;
                    if (d < 2) return false;
                    var dq = hex.Q - center.Q;
                    var dr = hex.R - center.R;
                    return dr == 0 || dq == 0 || dq == -dr; // the 3 axial lines (both signs each)
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset));
            }
        }

        // ---- blocks -----------------------------------------------------

        private static List<List<HexCoord>> FindBlocks(
            HashSet<HexCoord> region, HashSet<HexCoord> roads, int width, int height)
        {
            var seen = new HashSet<HexCoord>(roads);
            var blocks = new List<List<HexCoord>>();

            // Seed scan in offset order (row, then col) == (R, Q) order,
            // so component discovery order is deterministic.
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var seed = HexCoord.FromOffset(col, row);
                    if (seen.Contains(seed)) continue;

                    var block = new List<HexCoord>();
                    var queue = new Queue<HexCoord>();
                    queue.Enqueue(seed);
                    seen.Add(seed);
                    while (queue.Count > 0)
                    {
                        var hex = queue.Dequeue();
                        block.Add(hex);
                        for (var e = 0; e < 6; e++)
                        {
                            var n = hex.Neighbor((HexEdge)e);
                            if (!region.Contains(n) || seen.Contains(n)) continue;
                            seen.Add(n);
                            queue.Enqueue(n);
                        }
                    }
                    block.Sort(CompareRQ);
                    blocks.Add(block);
                }
            }

            // Biggest blocks first (they host landmarks); first-hex order
            // breaks ties so equal-sized blocks rank deterministically.
            blocks.Sort((a, b) =>
            {
                if (a.Count != b.Count) return b.Count - a.Count;
                return CompareRQ(a[0], b[0]);
            });
            return blocks;
        }

        // ---- landmarks --------------------------------------------------

        private static void PlaceLandmarks(
            CityPreset preset,
            HexCoord center,
            List<List<HexCoord>> blocks,
            HashSet<HexCoord> occupied,
            List<Landmark> landmarks,
            List<Building> buildings)
        {
            // Emitters: 1-2 per km^2 (docs/18 SS2), capped at docs/02's
            // 6-10-per-map ceiling -- at Big City scale the per-km^2 rate
            // alone would demand ~38, which docs/18 itself walks back with
            // "preserving the existing 6-10-per-map density". Hubs: ~1 per
            // 2 km^2, floor 1 so the Collection Station mechanic exists on
            // every map (docs/18 SS2 / docs/20).
            var emitterCount = Clamp(RoundAway(1.5 * preset.AreaKm2), 2, 10);
            var hubCount = Clamp(RoundAway(0.5 * preset.AreaKm2), 1, 6);

            // Order blocks for allocation: on a radial map the plaza block
            // (the one holding the center) comes first regardless of size
            // -- docs/18 SS1: "organic/radial streets around a central
            // plaza"; the plaza IS the anchor landmark.
            var ordered = new List<List<HexCoord>>(blocks);
            if (preset.Pattern == RoadPattern.Radial)
            {
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (!ordered[i].Contains(center)) continue;
                    var plaza = ordered[i];
                    ordered.RemoveAt(i);
                    ordered.Insert(0, plaza);
                    break;
                }
            }

            var total = Math.Min(emitterCount + hubCount, ordered.Count);
            var emittersLeft = emitterCount;
            var hubsLeft = hubCount;
            var emitterIndex = 0;
            var hubIndex = 0;

            for (var i = 0; i < total; i++)
            {
                var block = ordered[i];
                var site = CentralHex(block);

                // Alternate emitter/hub across the size-ranked blocks so
                // neither kind monopolizes the biggest ones; whichever
                // runs out first cedes the remainder to the other.
                bool isEmitter;
                if (emittersLeft == 0) isEmitter = false;
                else if (hubsLeft == 0) isEmitter = true;
                else isEmitter = i % 2 == 0;

                string archetype;
                LandmarkKind kind;
                if (isEmitter)
                {
                    kind = LandmarkKind.Emitter;
                    archetype = preset.EmitterArchetypes[emitterIndex % preset.EmitterArchetypes.Length];
                    emitterIndex++;
                    emittersLeft--;
                }
                else
                {
                    kind = LandmarkKind.CommunityHub;
                    archetype = preset.HubArchetypes[hubIndex % preset.HubArchetypes.Length];
                    hubIndex++;
                    hubsLeft--;
                }

                // Landmark building: the site plus its in-block neighbors
                // (up to 7 hexes) -- Landmark tier reuses docs/18 SS3's
                // 3000 HP / 8 armor row unchanged.
                var footprint = new List<HexCoord> { site };
                for (var e = 0; e < 6; e++)
                {
                    var n = site.Neighbor((HexEdge)e);
                    if (BlockContains(block, n)) footprint.Add(n);
                }
                foreach (var h in footprint) occupied.Add(h);

                landmarks.Add(new Landmark(kind, archetype, site));
                buildings.Add(new Building(footprint, BuildingTier.Landmark, archetype));
            }
        }

        /// <summary>The block hex nearest the block's (integer) centroid;
        /// (R, Q) order breaks distance ties. Deterministic "middle of the
        /// block" without any float geometry.</summary>
        private static HexCoord CentralHex(List<HexCoord> block)
        {
            long sumQ = 0, sumR = 0;
            foreach (var h in block) { sumQ += h.Q; sumR += h.R; }
            var target = new HexCoord(
                (int)Math.Round(sumQ / (double)block.Count, MidpointRounding.AwayFromZero),
                (int)Math.Round(sumR / (double)block.Count, MidpointRounding.AwayFromZero));

            var best = block[0];
            var bestDist = int.MaxValue;
            foreach (var h in block) // block is (R,Q)-sorted: ties resolve deterministically
            {
                var d = h.DistanceTo(target);
                if (d < bestDist) { best = h; bestDist = d; }
            }
            return best;
        }

        // ---- ordinary buildings ----------------------------------------

        private static void PlaceBuildings(
            CityPreset preset,
            Rng rng,
            List<List<HexCoord>> blocks,
            HashSet<HexCoord> occupied,
            List<Building> buildings)
        {
            // Footprint sizes per tier: Small 1 hex, Medium 2, Large 4 --
            // v0.1 shapes; a tier that can't gather enough free in-block
            // neighbors downgrades rather than overlapping anything.
            foreach (var block in blocks) // size-ranked order, deterministic
            {
                var blockSet = new HashSet<HexCoord>(block);
                foreach (var hex in block) // (R,Q)-sorted
                {
                    if (occupied.Contains(hex)) continue;
                    if (!rng.Bool(preset.BuildDensity)) continue;

                    var roll = rng.Next();
                    BuildingTier tier;
                    if (roll < preset.SmallWeight) tier = BuildingTier.Small;
                    else if (roll < preset.SmallWeight + preset.MediumWeight) tier = BuildingTier.Medium;
                    else tier = BuildingTier.Large;

                    var wanted = tier == BuildingTier.Large ? 3 : tier == BuildingTier.Medium ? 1 : 0;
                    var footprint = new List<HexCoord> { hex };
                    for (var e = 0; e < 6 && footprint.Count <= wanted; e++)
                    {
                        var n = hex.Neighbor((HexEdge)e);
                        if (blockSet.Contains(n) && !occupied.Contains(n)) footprint.Add(n);
                    }

                    // Downgrade to what actually fit.
                    if (tier == BuildingTier.Large && footprint.Count < 4)
                        tier = footprint.Count >= 2 ? BuildingTier.Medium : BuildingTier.Small;
                    if (tier == BuildingTier.Medium && footprint.Count < 2)
                        tier = BuildingTier.Small;

                    // Trim any surplus gathered beyond the (possibly
                    // downgraded) tier's footprint size.
                    var size = tier == BuildingTier.Large ? 4 : tier == BuildingTier.Medium ? 2 : 1;
                    if (footprint.Count > size) footprint.RemoveRange(size, footprint.Count - size);

                    foreach (var h in footprint) occupied.Add(h);
                    buildings.Add(new Building(footprint, tier));
                }
            }
        }

        // ---- helpers ----------------------------------------------------

        private static int CompareRQ(HexCoord a, HexCoord b)
        {
            if (a.R != b.R) return a.R.CompareTo(b.R);
            return a.Q.CompareTo(b.Q);
        }

        /// <summary>Binary search over the (R,Q)-sorted block list --
        /// avoids allocating a set per landmark for a 7-hex lookup.</summary>
        private static bool BlockContains(List<HexCoord> sortedBlock, HexCoord hex)
        {
            var lo = 0;
            var hi = sortedBlock.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var cmp = CompareRQ(sortedBlock[mid], hex);
                if (cmp == 0) return true;
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        private static int RoundAway(double x)
        {
            return (int)Math.Round(x, MidpointRounding.AwayFromZero);
        }

        private static int Clamp(int value, int lo, int hi)
        {
            return value < lo ? lo : value > hi ? hi : value;
        }
    }
}
