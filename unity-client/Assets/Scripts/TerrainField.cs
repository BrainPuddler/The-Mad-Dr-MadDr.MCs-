using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// The battlefield's height field (docs/21): deterministic, presentation-
/// side elevation that sculpts the terrain data the GENERATOR already
/// has instead of inventing new noise --
///
///   ridge hexes  -> smooth ~3m mounds (CityModel.Ridges IS the docs/04
///                   high-ground gameplay set; the old renderer showed it
///                   as green blocks)
///   water hexes  -> carved ~-1.4m river/pond beds
///   shoreline    -> open-ground hexes touching water get a shallow
///                   recessed lip (~-0.55m, gently varied) instead of
///                   their normal roll -- the smoothing below then
///                   blends open ground -> recessed shore -> bed as one
///                   continuous indented bank, not a straight ramp
///   flat-locked  -> exactly 0 under buildings, roads, and bridges, so
///                   every gameplay-vertical assumption (roof heights,
///                   flight tiers, descent floors, bridge decks, rubble)
///                   keeps its existing math unchanged (docs/21 rule)
///   open ground  -> gentle 2-octave value-noise rolls, <= ~1.5m
///
/// Sampling: inverse-distance weighting over the containing hex and its
/// six neighbors turns those per-hex targets into continuous slopes --
/// shorelines, hill skirts, valley edges -- with no seams. Everything is
/// seeded integer hashing (the codebase's determinism idiom); never
/// UnityEngine.Random.
/// </summary>
public sealed class TerrainField
{
    public const float RidgeHeight = 3.0f;      // matches the old ridge block, so high-ground reads the same
    public const float WaterBedDepth = -1.4f;   // carved bed; water surface slabs sit above this
    public const float RollAmplitude = 1.5f;    // open-ground rolling hills ceiling
    public const float BankRecess = -0.55f;     // shoreline lip, +-0.2 varied -- shallower than the bed

    private readonly Dictionary<HexCoord, float> _hexHeight = new Dictionary<HexCoord, float>();
    private readonly CityModel _city;
    private readonly Vector3 _origin;

    public TerrainField(CityModel city, Vector3 origin, uint seed)
    {
        _city = city;
        _origin = origin;

        // flat-locked: pavement and foundations hold the table level
        var flat = new HashSet<HexCoord>();
        foreach (var hex in city.Roads) flat.Add(hex);
        foreach (var b in city.Buildings)
            foreach (var hex in b.Footprint) flat.Add(hex);
        foreach (var br in city.Bridges)
            foreach (var hex in br.Footprint) flat.Add(hex);

        var water = new HashSet<HexCoord>(city.Water);
        var ridges = new HashSet<HexCoord>(city.Ridges);

        foreach (var hex in AllHexes(city))
        {
            float h;
            if (flat.Contains(hex)) h = 0f;
            else if (water.Contains(hex)) h = WaterBedDepth;
            else if (ridges.Contains(hex)) h = RidgeHeight;
            else if (IsShoreline(hex, water)) h = BankRecess + (Roll(hex, seed) / RollAmplitude - 0.5f) * 0.4f;
            else h = Roll(hex, seed);
            _hexHeight[hex] = h;
        }
    }

    /// <summary>Ground height (world y) at a world position. Continuous:
    /// inverse-distance blend of the containing hex's target height and
    /// its six neighbors'. Off-map hexes weigh in at 0, so the table
    /// fades level at its edges.</summary>
    public float HeightAt(Vector3 world)
    {
        var center = NearestHex(world);
        var wx = world.x - _origin.x;
        var wz = world.z - _origin.z;

        float sum = 0f, weight = 0f;
        Accumulate(center, wx, wz, ref sum, ref weight);
        foreach (var n in center.Neighbors())
            Accumulate(n, wx, wz, ref sum, ref weight);
        return weight > 0f ? sum / weight : 0f;
    }

    private void Accumulate(HexCoord hex, float wx, float wz, ref float sum, ref float weight)
    {
        var (hx, hz) = hex.ToWorld();
        var dx = wx - (float)hx;
        var dz = wz - (float)hz;
        // inverse-square falloff; +12 keeps the containing hex from
        // becoming a spike at its exact center (softens toward plateaus)
        var w = 1f / (dx * dx + dz * dz + 12f);
        float h;
        if (!_hexHeight.TryGetValue(hex, out h)) h = 0f;
        sum += h * w;
        weight += w;
    }

    /// <summary>Any of the hex's six neighbors is water -- open ground
    /// touching a river/pond gets the recessed shoreline treatment
    /// instead of its normal roll.</summary>
    private static bool IsShoreline(HexCoord hex, HashSet<HexCoord> water)
    {
        foreach (var n in hex.Neighbors())
            if (water.Contains(n)) return true;
        return false;
    }

    /// <summary>Gentle rolling hills for open ground: 2-octave value
    /// noise over a coarse lattice of axial coordinates (features span
    /// several hexes -- rolls, not spikes), seeded so the same city seed
    /// always grows the same landscape.</summary>
    private static float Roll(HexCoord hex, uint seed)
    {
        var a = LatticeNoise(hex.Q / 5f, hex.R / 5f, seed) * 1.0f;
        var b = LatticeNoise(hex.Q / 11f, hex.R / 11f, seed ^ 0x9E3779B9u) * 0.5f;
        return Mathf.Clamp((a + b) / 1.5f, 0f, 1f) * RollAmplitude;
    }

    /// <summary>Bilinear value noise over an integer lattice, hashed from
    /// (i, j, seed) -- platform-stable, no Perlin tables, no UnityEngine
    /// randomness.</summary>
    private static float LatticeNoise(float x, float y, uint seed)
    {
        var xi = Mathf.FloorToInt(x);
        var yi = Mathf.FloorToInt(y);
        var fx = x - xi;
        var fy = y - yi;
        // smoothstep the blend so lattice corners don't show as creases
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        var h00 = Hash01(xi, yi, seed);
        var h10 = Hash01(xi + 1, yi, seed);
        var h01 = Hash01(xi, yi + 1, seed);
        var h11 = Hash01(xi + 1, yi + 1, seed);
        var top = h00 + (h10 - h00) * fx;
        var bottom = h01 + (h11 - h01) * fx;
        return top + (bottom - top) * fy;
    }

    private static float Hash01(int x, int y, uint seed)
    {
        unchecked
        {
            var h = seed;
            h ^= (uint)x * 0x85EBCA6Bu;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE35u;
            h *= 0x27D4EB2Fu;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>All hexes of the city's rectangular region -- same
    /// axial-rectangle walk the generator itself uses (odd rows offset),
    /// reproduced here so we can seed every hex's target height.</summary>
    private static IEnumerable<HexCoord> AllHexes(CityModel city)
    {
        // CityModel exposes Contains(hex) + Width/Height; walk a generous
        // axial window and keep what the city claims. The window uses the
        // offset-rectangle relation r in [0,H), q in [-r/2, W-r/2).
        for (var r = 0; r < city.HeightHexes; r++)
            for (var q = -(r / 2); q < city.WidthHexes - r / 2; q++)
            {
                var hex = new HexCoord(q, r);
                if (city.Contains(hex)) yield return hex;
            }
    }

    private HexCoord NearestHex(Vector3 world)
    {
        var local = world - _origin;
        var size = (float)(HexCoord.HexMeters / 1.7320508075688772);
        var fq = (0.57735026918962576f * local.x - local.z / 3.0f) / size;
        var fr = (2.0f / 3.0f * local.z) / size;
        var fs = -fq - fr;
        var q = Mathf.Round(fq);
        var r = Mathf.Round(fr);
        var s = Mathf.Round(fs);
        var dq = Mathf.Abs(q - fq);
        var dr = Mathf.Abs(r - fr);
        var ds = Mathf.Abs(s - fs);
        if (dq > dr && dq > ds) q = -r - s;
        else if (dr > ds) r = -q - s;
        return new HexCoord((int)q, (int)r);
    }
}
