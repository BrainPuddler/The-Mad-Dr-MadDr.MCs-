using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>
    /// The procedural city generator, docs/18 SS2 + terrain: seeded
    /// terrain (river, ponds, hills), road-network growth, block
    /// subdivision, landmark allocation (emitter XOR Community Hub per
    /// node), and building-footprint placement -- all on the hex index,
    /// in integers, so the result is a pure function of (seed, preset):
    /// both match clients generate the identical city from the seed
    /// alone and the city itself is never transmitted.
    ///
    /// Terrain is the natural-choke-point layer: the river severs the
    /// map into two banks joined only by a handful of destructible
    /// bridges (ground units must funnel; amphibious/airborne plans
    /// don't care); ponds are local walls you flow around; hills are
    /// the existing docs/04 high-ground ridge feature, placed.
    ///
    /// Determinism rules obeyed throughout (same discipline as
    /// genome-core): the only randomness is the seeded Rng passed
    /// through; no wall-clock, no System.Random, and no iteration of
    /// hash containers ever affects output or RNG draw order -- every
    /// loop that matters walks an explicitly (R, Q)-sorted list, and
    /// terrain draws happen in one fixed sequence (river, ponds, hills)
    /// before any road or building draw.
    ///
    /// The skin pass and prop/dressing pass from docs/18 SS2's pipeline
    /// are deliberately absent: those consume this model renderer-side.
    /// </summary>
    public static class CityGenerator
    {
        /// <summary>How many arterial intersections become roundabouts --
        /// the central few, so a North-American grid keeps its 4-way
        /// crosses everywhere else (creator direction, 2026-07).</summary>
        private const int MaxRoundabouts = 2;

        public static CityModel Generate(uint seed, CityPreset preset)
        {
            var rng = new Rng(seed);
            var width = preset.WidthHexes;
            var height = preset.HeightHexes;
            var center = HexCoord.FromOffset(width / 2, height / 2);

            // -- 0. region ----------------------------------------------
            var region = new HashSet<HexCoord>();
            for (var row = 0; row < height; row++)
                for (var col = 0; col < width; col++)
                    region.Add(HexCoord.FromOffset(col, row));

            // -- 1. terrain (fixed draw order: river, ponds, hills) ------
            var riverWater = CarveRiver(preset, rng);
            var pondWater = CarvePonds(preset, rng, region, center, riverWater);
            var allWater = new HashSet<HexCoord>(riverWater);
            allWater.UnionWith(pondWater);
            var ridgeCandidates = RaiseHills(preset, rng, region, center, allWater);

            // -- 2. road geometry (pure pattern, ignores water) ----------
            var roadGeom = new HashSet<HexCoord>();
            var arterialGeom = new HashSet<HexCoord>();
            var isMainStreet = preset.Pattern == RoadPattern.MainStreet;
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var hex = HexCoord.FromOffset(col, row);
                    if (!IsRoad(preset, col, row)) continue;
                    roadGeom.Add(hex);
                    // the ONE condition that makes a MainStreet hex the
                    // arterial itself, not a perpendicular/residential
                    // street -- see IsRoad's MainStreet case
                    if (isMainStreet && row == height / 2) arterialGeom.Add(hex);
                }
            }

            // -- 3. bridges: where roads meet the river ------------------
            var bridges = ChooseBridges(preset, roadGeom, riverWater);
            var bridgeHexes = new HashSet<HexCoord>();
            foreach (var b in bridges)
                foreach (var h in b.Footprint) bridgeHexes.Add(h);

            // Drowned road segments vanish; bridge decks survive as road.
            var roadSet = new HashSet<HexCoord>();
            foreach (var h in roadGeom)
                if (!allWater.Contains(h)) roadSet.Add(h);
            roadSet.UnionWith(bridgeHexes);

            // Same drown-or-bridge survival rule for the arterial subset,
            // so Main Street crossing the river on a bridge is still
            // Main Street on the far bank, not a downgrade to residential.
            var arterialSet = new HashSet<HexCoord>();
            foreach (var h in arterialGeom)
                if (!allWater.Contains(h) || bridgeHexes.Contains(h)) arterialSet.Add(h);

            // Roundabouts: the MAJOR arterial intersections, upgraded from
            // a plain 4-way cross to a proper circular junction (creator
            // direction, 2026-07). Only where Main Street (the arterial
            // row) crosses a full vertical street (col % pitch == 0), and
            // only the few nearest the town center -- a North-American
            // grid with a couple of elegant roundabouts at its heart, not
            // a roundabout maze. Never on a bridge deck (a roundabout on
            // open water makes no sense) and never drowned.
            var roundaboutSet = new HashSet<HexCoord>();
            if (isMainStreet)
            {
                var arterialRow = height / 2;
                var centerCol = width / 2;
                var pitch = preset.BlockPitch;
                var junctionCols = new List<int>();
                for (var col = 0; col < width; col += pitch)
                {
                    var hex = HexCoord.FromOffset(col, arterialRow);
                    if (roadSet.Contains(hex) && !bridgeHexes.Contains(hex)) junctionCols.Add(col);
                }
                // nearest-to-center first, take up to MaxRoundabouts
                junctionCols.Sort((a, b) => Math.Abs(a - centerCol).CompareTo(Math.Abs(b - centerCol)));
                var want = Math.Min(MaxRoundabouts, junctionCols.Count);
                for (var i = 0; i < want; i++)
                    roundaboutSet.Add(HexCoord.FromOffset(junctionCols[i], arterialRow));
            }

            // Ridges never coincide with roads or water.
            var ridgeSet = new HashSet<HexCoord>();
            foreach (var h in ridgeCandidates)
                if (!roadSet.Contains(h)) ridgeSet.Add(h);

            // -- 4. blocks: connected components of open land ------------
            var blocked = new HashSet<HexCoord>(roadSet);
            blocked.UnionWith(allWater);
            var blocks = FindBlocks(region, blocked, width, height);

            // -- 5. landmarks + buildings --------------------------------
            var occupied = new HashSet<HexCoord>();
            var landmarks = new List<Landmark>();
            var buildings = new List<Building>();
            PlaceLandmarks(preset, blocks, occupied, landmarks, buildings);
            PlaceBuildings(preset, rng, blocks, occupied, buildings);

            // -- 6. model (all lists sorted for element-wise identity) ---
            var waterList = new List<HexCoord>();
            foreach (var h in allWater)
                if (!bridgeHexes.Contains(h)) waterList.Add(h);
            waterList.Sort(CompareRQ);

            var roadList = new List<HexCoord>(roadSet);
            roadList.Sort(CompareRQ);

            var arterialList = new List<HexCoord>(arterialSet);
            arterialList.Sort(CompareRQ);

            var roundaboutList = new List<HexCoord>(roundaboutSet);
            roundaboutList.Sort(CompareRQ);

            var ridgeList = new List<HexCoord>(ridgeSet);
            ridgeList.Sort(CompareRQ);

            return new CityModel(seed, preset.Name, width, height,
                roadList, arterialList, roundaboutList, waterList, ridgeList, buildings, landmarks, bridges);
        }

        // ---- terrain ----------------------------------------------------

        /// <summary>A horizontal river band drifting across the full map
        /// width, confined to the upper or lower half (RNG-chosen) so it
        /// never swallows the map center -- the plaza block, the
        /// MainStreet arterial, and the landmark-rich middle stay dry.
        /// Guaranteed to touch both the left and right map edges, which
        /// is what makes it a full partition into two banks.</summary>
        private static HashSet<HexCoord> CarveRiver(CityPreset preset, Rng rng)
        {
            var water = new HashSet<HexCoord>();
            var w = preset.RiverWidthHexes;
            if (w <= 0) return water;

            var height = preset.HeightHexes;
            const int edgeMargin = 3;   // banks never pinch to nothing
            const int centerMargin = 4; // keep off the center row

            var lowerHalf = rng.Bool();
            int lo, hi; // bounds for the band's top row
            if (lowerHalf)
            {
                lo = height / 2 + centerMargin;
                hi = height - edgeMargin - w;
            }
            else
            {
                lo = edgeMargin;
                hi = height / 2 - centerMargin - w;
            }
            if (hi < lo) hi = lo;

            var row = lo + rng.IntRange(hi - lo + 1);
            var prevRow = row;
            for (var col = 0; col < preset.WidthHexes; col++)
            {
                // Staircase fill: span from the previous column's row to
                // this one's, sealing the drift seam. A straight width-1
                // row is already impermeable on a hex grid (offset rows 2
                // apart are never adjacent), but at every DRIFT step the
                // two banks touch diagonally -- without this fill, ground
                // units leak across the "river" at each bend, and the
                // choke-point property silently dies. Found by the
                // connectivity test, kept as a fill rather than a wider
                // river because it only pays the extra hex at bends.
                var top = Math.Min(prevRow, row);
                var bottom = Math.Max(prevRow, row) + w - 1;
                for (var k = top; k <= bottom; k++) water.Add(HexCoord.FromOffset(col, k));

                prevRow = row;
                row += rng.IntRange(3) - 1; // drift -1 / 0 / +1
                if (row < lo) row = lo;
                if (row > hi) row = hi;
            }
            return water;
        }

        /// <summary>Pond blobs: local impassable obstacles. Kept at least
        /// 4 hexes clear of the river (so combined water can never pinch
        /// a bank shut), 5 hexes off the map edge (so a pond can never
        /// isolate a corner pocket), and off the center (plaza).</summary>
        private static HashSet<HexCoord> CarvePonds(
            CityPreset preset, Rng rng, HashSet<HexCoord> region, HexCoord center,
            HashSet<HexCoord> riverWater)
        {
            var ponds = new HashSet<HexCoord>();
            for (var i = 0; i < preset.PondCount; i++)
            {
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    var col = 5 + rng.IntRange(Math.Max(1, preset.WidthHexes - 10));
                    var row = 5 + rng.IntRange(Math.Max(1, preset.HeightHexes - 10));
                    var radius = 1 + rng.IntRange(2); // 1..2
                    var hex = HexCoord.FromOffset(col, row);

                    if (hex.DistanceTo(center) < 8 + radius) continue;

                    var tooClose = false;
                    foreach (var probe in hex.Range(radius + 4))
                    {
                        if (riverWater.Contains(probe) || ponds.Contains(probe)) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    foreach (var h in hex.Range(radius))
                        if (region.Contains(h)) ponds.Add(h);
                    break;
                }
            }
            return ponds;
        }

        /// <summary>Hill blobs of ridge hexes -- the docs/02/04 high-ground
        /// feature (+0.10 posMod, winged fly over), placed on dry land.</summary>
        private static HashSet<HexCoord> RaiseHills(
            CityPreset preset, Rng rng, HashSet<HexCoord> region, HexCoord center,
            HashSet<HexCoord> water)
        {
            var ridges = new HashSet<HexCoord>();
            for (var i = 0; i < preset.HillCount; i++)
            {
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    var col = rng.IntRange(preset.WidthHexes);
                    var row = rng.IntRange(preset.HeightHexes);
                    var hex = HexCoord.FromOffset(col, row);
                    if (water.Contains(hex)) continue;
                    if (hex.DistanceTo(center) < 4) continue;

                    foreach (var h in hex.Range(preset.HillRadiusHexes))
                        if (region.Contains(h) && !water.Contains(h)) ridges.Add(h);
                    break;
                }
            }
            return ridges;
        }

        /// <summary>Bridges: connected runs of road-over-river, size-capped
        /// (a road running ALONG inside the band is a drowned road, not a
        /// map-wide bridge), spread evenly along the river, limited to the
        /// preset's count -- scarcity is the choke point.</summary>
        private static List<Bridge> ChooseBridges(
            CityPreset preset, HashSet<HexCoord> roadGeom, HashSet<HexCoord> riverWater)
        {
            var bridges = new List<Bridge>();
            if (preset.RiverWidthHexes <= 0 || preset.BridgeCount <= 0) return bridges;

            // Candidate hexes, discovered in sorted order.
            var candidates = new List<HexCoord>();
            foreach (var h in roadGeom)
                if (riverWater.Contains(h)) candidates.Add(h);
            candidates.Sort(CompareRQ);

            var candidateSet = new HashSet<HexCoord>(candidates);
            var seen = new HashSet<HexCoord>();
            var components = new List<List<HexCoord>>();
            foreach (var seedHex in candidates)
            {
                if (seen.Contains(seedHex)) continue;
                var component = new List<HexCoord>();
                var queue = new Queue<HexCoord>();
                queue.Enqueue(seedHex);
                seen.Add(seedHex);
                while (queue.Count > 0)
                {
                    var hex = queue.Dequeue();
                    component.Add(hex);
                    for (var e = 0; e < 6; e++)
                    {
                        var n = hex.Neighbor((HexEdge)e);
                        if (!candidateSet.Contains(n) || seen.Contains(n)) continue;
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
                }
                // A real crossing is short (roughly the river's width); a
                // long component is a road drowned along the channel.
                if (component.Count <= preset.RiverWidthHexes * 3)
                {
                    component.Sort(CompareRQ);
                    components.Add(component);
                }
            }
            if (components.Count == 0) return bridges;

            // Order candidates along the river (west to east), then pick
            // BridgeCount of them at even spacing.
            components.Sort((a, b) =>
            {
                var ca = ColOf(a[0]);
                var cb = ColOf(b[0]);
                if (ca != cb) return ca.CompareTo(cb);
                return CompareRQ(a[0], b[0]);
            });

            var want = Math.Min(preset.BridgeCount, components.Count);
            for (var i = 0; i < want; i++)
            {
                var idx = (int)(((i + 0.5) * components.Count) / want);
                bridges.Add(new Bridge(components[idx]));
            }
            return bridges;
        }

        /// <summary>Offset column of a hex (odd-r) -- the along-river
        /// coordinate for spacing bridges.</summary>
        private static int ColOf(HexCoord hex)
        {
            return hex.Q + (hex.R - (hex.R & 1)) / 2;
        }

        // ---- roads ------------------------------------------------------

        private static bool IsRoad(CityPreset preset, int col, int row)
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

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset));
            }
        }

        // ---- blocks -----------------------------------------------------

        private static List<List<HexCoord>> FindBlocks(
            HashSet<HexCoord> region, HashSet<HexCoord> blocked, int width, int height)
        {
            var seen = new HashSet<HexCoord>(blocked);
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

            // Biggest blocks first (already FindBlocks' own sort order) --
            // every preset is a Main-Street-or-grid town now, so the
            // biggest block earns the first landmark on its own merits,
            // no plaza-block special case.
            var ordered = blocks;

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
