using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>Which cardinal face of a building's axis-aligned massing
    /// cube this is. The cubes are never rotated (see
    /// `RuntimeCityBuilder.SpawnCube`), so four cardinal faces is the
    /// complete set.</summary>
    public enum FacadeFace
    {
        PlusX = 0,
        MinusX = 1,
        PlusZ = 2,
        MinusZ = 3,
    }

    /// <summary>What a face looks out onto. This is the single most
    /// load-bearing concept in the whole grammar: before it existed every
    /// hex was dressed identically on all four sides, which is why a row
    /// of buildings read as detached objects on a grid rather than a
    /// street wall.</summary>
    public enum FaceRole
    {
        /// <summary>Faces a road hex. Gets the shopfronts, the entrances,
        /// the fire escape, the best detail -- the face the player sees
        /// from the street.</summary>
        Street = 0,

        /// <summary>Faces open ground that isn't a road. Service side:
        /// loading docks, blank brick, the odd small window.</summary>
        Alley = 1,

        /// <summary>Abuts another building's footprint. In a real period
        /// block these are shared party walls -- no windows, no
        /// ornament, because there is physically another building
        /// pressed against them.</summary>
        PartyWall = 2,
    }

    /// <summary>Which weighting profile a building draws its modules
    /// from. Style reweights the SAME vocabulary rather than swapping in
    /// a different one -- the project's own faction principle ("an
    /// expression profile, not a new system", docs/17) applied to
    /// architecture.</summary>
    public enum FacadeStyle
    {
        Residential = 0,
        Commercial = 1,
        Industrial = 2,
        Civic = 3,
    }

    /// <summary>The module vocabulary. Deliberately small and reusable
    /// rather than hundreds of near-identical pieces. Each value is a
    /// mesh-swap point: the Unity side maps every one of these to a
    /// `PropLibrary` key with a primitive fallback, so authored meshes
    /// can replace primitives later without the solver changing at
    /// all.</summary>
    public enum FacadeModule
    {
        /// <summary>Nothing at all -- a bare party wall.</summary>
        Blank = 0,

        // ---- ground band (exactly one cell, at the bottom) ----
        Shopfront = 1,
        RecessedEntrance = 2,
        StoopEntrance = 3,
        LoadingDock = 4,
        BlankPlinth = 5,

        // ---- upper band (one cell per floor above ground) ----
        WindowBay = 6,
        BlindBay = 7,
        FireEscapeBay = 8,
        OrielBay = 9,

        // ---- crown (exactly one cell, at the top) ----
        Cornice = 10,
        Parapet = 11,
        SetbackCrown = 12,
    }

    /// <summary>One solved face: a vertical strip read bottom-to-top.
    /// `Cells[0]` is always the ground band, `Cells[Count-1]` always the
    /// crown, everything between is one cell per upper floor.</summary>
    public sealed class FacadeSolution
    {
        public FaceRole Role { get; }
        public IReadOnlyList<FacadeModule> Cells { get; }

        /// <summary>True when the solver hit a contradiction and this is
        /// the relaxed fallback rather than a real solve. The caller is
        /// expected to fall back to legacy dressing when this is set --
        /// it never leaves the caller without a usable result.</summary>
        public bool IsFallback { get; }

        public FacadeSolution(FaceRole role, IReadOnlyList<FacadeModule> cells, bool isFallback = false)
        {
            Role = role;
            Cells = cells;
            IsFallback = isFallback;
        }

        public FacadeModule Ground => Cells.Count > 0 ? Cells[0] : FacadeModule.Blank;
        public FacadeModule Crown => Cells.Count > 0 ? Cells[Cells.Count - 1] : FacadeModule.Blank;
    }

    /// <summary>docs/30: a constrained wave-function-collapse solver over
    /// a building face, expressed as a 1-D vertical strip of cells.
    ///
    /// **Why 1-D, and why this small.** The expensive, failure-prone form
    /// of WFC is a large 2-D/3-D solve with a big tile domain and real
    /// backtracking. Nothing about a period facade needs that: what
    /// actually reads at RTS camera height is the VERTICAL grammar --
    /// ground floor differs from upper floors, cornice sits at the top,
    /// a fire escape is a continuous column rather than a scatter of
    /// disconnected platforms. A face of a Medium building is three
    /// floors plus ground plus crown: a five-cell solve. That fits
    /// comfortably inside the loading-screen budget
    /// (`CityGeneratorTests.Big_city_generates_quickly_enough...`, 5000 ms
    /// for the whole city) with no Jobs, no Burst, and no native
    /// containers -- none of which this project uses anywhere today.
    ///
    /// It is nonetheless a real WFC and not a dressed-up weighted pick:
    /// every cell carries a domain (a bitmask of still-possible modules),
    /// constraints PROPAGATE between vertically adjacent cells until the
    /// wave is stable, collapse always targets the lowest-entropy
    /// undecided cell, and a genuine contradiction (empty domain) is
    /// detected and reported rather than papered over.
    ///
    /// **Determinism.** Draws come from <see cref="Rng"/> (the seeded
    /// sfc32 this package mandates -- never `System.Random`), and every
    /// iteration order here is over arrays and explicit index ranges,
    /// never a hash container, so a solve is byte-reproducible for a
    /// given (seed, inputs) pair. That is a hard requirement: the city
    /// generator's own determinism test compares whole models
    /// element-wise.</summary>
    public static class FacadeGrammar
    {
        public const int ModuleCount = 13;

        private const int GroundMask =
            (1 << (int)FacadeModule.Shopfront) |
            (1 << (int)FacadeModule.RecessedEntrance) |
            (1 << (int)FacadeModule.StoopEntrance) |
            (1 << (int)FacadeModule.LoadingDock) |
            (1 << (int)FacadeModule.BlankPlinth);

        private const int UpperMask =
            (1 << (int)FacadeModule.WindowBay) |
            (1 << (int)FacadeModule.BlindBay) |
            (1 << (int)FacadeModule.FireEscapeBay) |
            (1 << (int)FacadeModule.OrielBay);

        private const int CrownMask =
            (1 << (int)FacadeModule.Cornice) |
            (1 << (int)FacadeModule.Parapet) |
            (1 << (int)FacadeModule.SetbackCrown);

        private const int BlankOnly = 1 << (int)FacadeModule.Blank;

        /// <summary>Classify all four cardinal faces of one footprint hex.
        ///
        /// The mapping from four square faces to six hex neighbours is the
        /// project's existing square-tile-on-hex-grid mismatch (the same
        /// one `RuntimeCityBuilder.InsideBuildingFootprint` exists to work
        /// around). Rather than pretend it away, each face takes the role
        /// of whichever hex neighbours actually fall within its 90-degree
        /// outward arc, computed from real world-space directions via
        /// <see cref="HexCoord.ToWorld"/> -- not from an invented
        /// edge-to-face table. Severity wins ties: a face touching both a
        /// road and another building reads as Street, because the street
        /// frontage is the one the player sees.</summary>
        public static FaceRole[] ClassifyFaces(
            HexCoord hex,
            ISet<HexCoord> ownFootprint,
            ISet<HexCoord> roads,
            ISet<HexCoord> otherBuildingHexes)
        {
            if (ownFootprint == null) throw new ArgumentNullException(nameof(ownFootprint));
            if (roads == null) throw new ArgumentNullException(nameof(roads));
            if (otherBuildingHexes == null) throw new ArgumentNullException(nameof(otherBuildingHexes));

            // Default: an unobstructed face with no road is an alley/yard.
            var roles = new FaceRole[4];
            for (var i = 0; i < 4; i++) roles[i] = FaceRole.Alley;

            var (hx, hz) = hex.ToWorld();

            for (var e = 0; e < 6; e++)
            {
                var n = hex.Neighbor((HexEdge)e);
                var (nx, nz) = n.ToWorld();
                var dx = nx - hx;
                var dz = nz - hz;

                var face = FaceForDirection(dx, dz);
                var idx = (int)face;

                // Another hex of THIS SAME building is not a party wall --
                // it's interior, and interior faces get no dressing either
                // way. Treated as party wall so nothing is spawned there.
                if (ownFootprint.Contains(n))
                {
                    if (roles[idx] != FaceRole.Street) roles[idx] = FaceRole.PartyWall;
                    continue;
                }

                if (roads.Contains(n))
                {
                    roles[idx] = FaceRole.Street;   // highest severity, always wins
                    continue;
                }

                if (otherBuildingHexes.Contains(n))
                {
                    if (roles[idx] != FaceRole.Street) roles[idx] = FaceRole.PartyWall;
                }
            }

            return roles;
        }

        /// <summary>Which cardinal face a world-space direction belongs to
        /// -- the axis with the larger absolute component wins, so each
        /// face owns a 90-degree arc.</summary>
        private static FacadeFace FaceForDirection(double dx, double dz)
        {
            if (Math.Abs(dx) >= Math.Abs(dz))
                return dx >= 0 ? FacadeFace.PlusX : FacadeFace.MinusX;
            return dz >= 0 ? FacadeFace.PlusZ : FacadeFace.MinusZ;
        }

        /// <summary>Solve one face. `floors` is the number of UPPER floors
        /// (above the ground band); the returned strip is always
        /// `floors + 2` cells long (ground + uppers + crown).
        ///
        /// `allowFireEscape` lets the caller enforce the building-wide rule
        /// that a walk-up gets exactly one fire escape rather than one per
        /// face -- a constraint that spans faces and therefore cannot live
        /// inside a single-face solve.</summary>
        public static FacadeSolution Solve(
            FaceRole role,
            int floors,
            FacadeStyle style,
            Rng rng,
            bool allowFireEscape = true)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (floors < 0) throw new ArgumentOutOfRangeException(nameof(floors));

            var cellCount = floors + 2;

            // A party wall is fully determined -- no solve needed, and no
            // geometry will be spawned for it at all.
            if (role == FaceRole.PartyWall)
            {
                var blanks = new FacadeModule[cellCount];
                for (var i = 0; i < cellCount; i++) blanks[i] = FacadeModule.Blank;
                return new FacadeSolution(role, blanks);
            }

            var domains = new int[cellCount];
            domains[0] = GroundDomain(role, style);
            for (var i = 1; i < cellCount - 1; i++) domains[i] = UpperDomain(role, style, allowFireEscape);
            domains[cellCount - 1] = CrownDomain(style);

            var collapsed = new FacadeModule[cellCount];
            var decided = new bool[cellCount];

            if (!Propagate(domains, decided, collapsed)) return Fallback(role, cellCount);

            // Entropy-ordered collapse. Ties break on the lowest index, so
            // the sequence is a pure function of the domains -- never of
            // enumeration order.
            for (var step = 0; step < cellCount; step++)
            {
                var target = LowestEntropyIndex(domains, decided);
                if (target < 0) break;   // everything decided

                var pick = WeightedPick(domains[target], target, cellCount, style, rng);
                if (pick == null) return Fallback(role, cellCount);

                collapsed[target] = pick.Value;
                domains[target] = 1 << (int)pick.Value;
                decided[target] = true;

                if (!Propagate(domains, decided, collapsed)) return Fallback(role, cellCount);
            }

            for (var i = 0; i < cellCount; i++)
            {
                if (!decided[i])
                {
                    var only = SingleModule(domains[i]);
                    if (only == null) return Fallback(role, cellCount);
                    collapsed[i] = only.Value;
                }
            }

            return new FacadeSolution(role, collapsed);
        }

        // ---- domains -------------------------------------------------------

        private static int GroundDomain(FaceRole role, FacadeStyle style)
        {
            if (role == FaceRole.Street)
            {
                // No loading docks on a street frontage -- deliveries came
                // off the alley, and a dock where a shopfront belongs is
                // exactly the "plausible period building?" test failing.
                switch (style)
                {
                    case FacadeStyle.Commercial:
                        return Bit(FacadeModule.Shopfront) | Bit(FacadeModule.RecessedEntrance);
                    case FacadeStyle.Residential:
                        return Bit(FacadeModule.StoopEntrance) | Bit(FacadeModule.RecessedEntrance) | Bit(FacadeModule.Shopfront);
                    case FacadeStyle.Civic:
                        return Bit(FacadeModule.RecessedEntrance) | Bit(FacadeModule.BlankPlinth);
                    default: // Industrial
                        return Bit(FacadeModule.LoadingDock) | Bit(FacadeModule.BlankPlinth) | Bit(FacadeModule.RecessedEntrance);
                }
            }

            // Alley: service side.
            switch (style)
            {
                case FacadeStyle.Industrial:
                    return Bit(FacadeModule.LoadingDock) | Bit(FacadeModule.BlankPlinth);
                case FacadeStyle.Civic:
                    return Bit(FacadeModule.BlankPlinth);
                default:
                    return Bit(FacadeModule.BlankPlinth) | Bit(FacadeModule.LoadingDock);
            }
        }

        private static int UpperDomain(FaceRole role, FacadeStyle style, bool allowFireEscape)
        {
            var d = Bit(FacadeModule.WindowBay) | Bit(FacadeModule.BlindBay);

            if (role == FaceRole.Street)
            {
                if (allowFireEscape && (style == FacadeStyle.Residential || style == FacadeStyle.Commercial))
                    d |= Bit(FacadeModule.FireEscapeBay);
                if (style == FacadeStyle.Residential || style == FacadeStyle.Civic)
                    d |= Bit(FacadeModule.OrielBay);
            }
            else
            {
                // Alley walls are mostly blind brick with the occasional
                // small window -- and never an oriel, which is a street-
                // facing display element by definition.
                d = Bit(FacadeModule.BlindBay) | Bit(FacadeModule.WindowBay);
            }

            return d;
        }

        private static int CrownDomain(FacadeStyle style)
        {
            switch (style)
            {
                case FacadeStyle.Industrial:
                    return Bit(FacadeModule.Parapet);
                case FacadeStyle.Civic:
                    return Bit(FacadeModule.Cornice);
                case FacadeStyle.Commercial:
                    return Bit(FacadeModule.Cornice) | Bit(FacadeModule.SetbackCrown) | Bit(FacadeModule.Parapet);
                default:
                    return Bit(FacadeModule.Cornice) | Bit(FacadeModule.Parapet);
            }
        }

        // ---- propagation ---------------------------------------------------

        /// <summary>Reduce domains until stable. Returns false on a
        /// contradiction (some cell's domain went empty).
        ///
        /// The one genuinely architectural adjacency rule: a fire escape is
        /// a CONTINUOUS vertical run. If any upper cell is fixed to
        /// FireEscapeBay, every other upper cell on that face must be one
        /// too -- a period building does not have a fire escape that stops
        /// at the third floor and resumes at the fifth. Conversely, once
        /// any upper cell is fixed to something else, no other upper cell
        /// may become a fire escape.</summary>
        private static bool Propagate(int[] domains, bool[] decided, FacadeModule[] collapsed)
        {
            var changed = true;
            var guard = 0;

            while (changed)
            {
                changed = false;
                if (++guard > 64) break;   // structural safety net; domains are tiny

                var anyFireEscapeFixed = false;
                var anyNonFireEscapeFixed = false;

                for (var i = 1; i < domains.Length - 1; i++)
                {
                    var only = SingleModule(domains[i]);
                    if (only == null) continue;
                    if (only.Value == FacadeModule.FireEscapeBay) anyFireEscapeFixed = true;
                    else anyNonFireEscapeFixed = true;
                }

                if (anyFireEscapeFixed && anyNonFireEscapeFixed) return false;   // real contradiction

                for (var i = 1; i < domains.Length - 1; i++)
                {
                    var before = domains[i];

                    if (anyFireEscapeFixed)
                        domains[i] &= Bit(FacadeModule.FireEscapeBay);
                    else if (anyNonFireEscapeFixed)
                        domains[i] &= ~Bit(FacadeModule.FireEscapeBay);

                    if (domains[i] == 0) return false;
                    if (domains[i] != before) changed = true;
                }

                for (var i = 0; i < domains.Length; i++)
                    if (domains[i] == 0) return false;
            }

            return true;
        }

        // ---- collapse ------------------------------------------------------

        private static int LowestEntropyIndex(int[] domains, bool[] decided)
        {
            var best = -1;
            var bestCount = int.MaxValue;
            for (var i = 0; i < domains.Length; i++)
            {
                if (decided[i]) continue;
                var c = PopCount(domains[i]);
                if (c <= 1) continue;   // already forced; handled after the loop
                if (c < bestCount) { bestCount = c; best = i; }
            }
            return best;
        }

        /// <summary>Weighted draw from a cell's remaining domain. Weights
        /// are v0.1 placeholders like every other tuning number in this
        /// project -- what they encode is only the relative floor before
        /// style reweights them.</summary>
        private static FacadeModule? WeightedPick(int domain, int index, int cellCount, FacadeStyle style, Rng rng)
        {
            Span<int> candidates = stackalloc int[ModuleCount];
            Span<int> weights = stackalloc int[ModuleCount];
            var n = 0;
            var total = 0;

            for (var m = 0; m < ModuleCount; m++)
            {
                if ((domain & (1 << m)) == 0) continue;
                var w = Weight((FacadeModule)m, index, cellCount, style);
                if (w <= 0) w = 1;
                candidates[n] = m;
                weights[n] = w;
                total += w;
                n++;
            }

            if (n == 0) return null;

            var roll = rng.IntRange(total);
            var acc = 0;
            for (var i = 0; i < n; i++)
            {
                acc += weights[i];
                if (roll < acc) return (FacadeModule)candidates[i];
            }
            return (FacadeModule)candidates[n - 1];
        }

        private static int Weight(FacadeModule m, int index, int cellCount, FacadeStyle style)
        {
            switch (m)
            {
                case FacadeModule.WindowBay: return 60;
                case FacadeModule.BlindBay: return style == FacadeStyle.Industrial ? 45 : 14;
                // Deliberately low: a fire escape on most faces of most
                // buildings is the kind of over-application that reads as
                // procedural. One per block is period-correct; one per
                // building is not.
                case FacadeModule.FireEscapeBay: return 10;
                case FacadeModule.OrielBay: return 8;

                case FacadeModule.Shopfront: return style == FacadeStyle.Commercial ? 65 : 25;
                case FacadeModule.RecessedEntrance: return 30;
                case FacadeModule.StoopEntrance: return style == FacadeStyle.Residential ? 45 : 12;
                case FacadeModule.LoadingDock: return style == FacadeStyle.Industrial ? 55 : 20;
                case FacadeModule.BlankPlinth: return 22;

                case FacadeModule.Cornice: return 55;
                case FacadeModule.Parapet: return 30;
                case FacadeModule.SetbackCrown: return 18;

                default: return 1;
            }
        }

        // ---- fallback ------------------------------------------------------

        /// <summary>The relaxed, always-valid strip a contradiction falls
        /// back to. Flagged `IsFallback` so the caller can drop to legacy
        /// dressing instead -- a failed solve must never leave a building
        /// half-dressed or the game in a broken state.</summary>
        private static FacadeSolution Fallback(FaceRole role, int cellCount)
        {
            var cells = new FacadeModule[cellCount];
            cells[0] = role == FaceRole.Street ? FacadeModule.RecessedEntrance : FacadeModule.BlankPlinth;
            for (var i = 1; i < cellCount - 1; i++) cells[i] = FacadeModule.WindowBay;
            if (cellCount > 1) cells[cellCount - 1] = FacadeModule.Cornice;
            return new FacadeSolution(role, cells, isFallback: true);
        }

        // ---- bit helpers ---------------------------------------------------

        private static int Bit(FacadeModule m) => 1 << (int)m;

        private static FacadeModule? SingleModule(int domain)
        {
            if (domain == 0 || PopCount(domain) != 1) return null;
            for (var m = 0; m < ModuleCount; m++)
                if ((domain & (1 << m)) != 0) return (FacadeModule)m;
            return null;
        }

        private static int PopCount(int v)
        {
            var c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }

        /// <summary>Style for a tier + district, so callers don't each
        /// invent their own mapping. Mirrors the existing dresser's own
        /// tier dispatch rather than introducing a second, competing
        /// notion of what a building is.</summary>
        public static FacadeStyle StyleFor(BuildingTier tier, bool industrial, bool suburb)
        {
            if (industrial) return FacadeStyle.Industrial;
            switch (tier)
            {
                case BuildingTier.Landmark: return FacadeStyle.Civic;
                case BuildingTier.Large: return FacadeStyle.Commercial;
                case BuildingTier.Medium: return suburb ? FacadeStyle.Residential : FacadeStyle.Commercial;
                default: return FacadeStyle.Residential;
            }
        }
    }
}
