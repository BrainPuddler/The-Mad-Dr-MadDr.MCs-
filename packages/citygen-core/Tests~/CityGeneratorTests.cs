using System.Text;
using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

public class CityGeneratorTests
{
    /// <summary>Canonical string of a model: element-by-element, in the
    /// generator's own (already deterministic) output order. Two models
    /// from the same (seed, preset) must match char-for-char.</summary>
    private static string Canonical(CityModel m)
    {
        var sb = new StringBuilder();
        sb.Append(m.Seed).Append('|').Append(m.PresetName).Append('|');
        foreach (var r in m.Roads) sb.Append(r.Q).Append(',').Append(r.R).Append(';');
        sb.Append('|');
        foreach (var w in m.Water) sb.Append(w.Q).Append(',').Append(w.R).Append(';');
        sb.Append('|');
        foreach (var g in m.Ridges) sb.Append(g.Q).Append(',').Append(g.R).Append(';');
        sb.Append('|');
        foreach (var b in m.Buildings)
        {
            sb.Append((int)b.Tier).Append(':').Append(b.Archetype).Append(':');
            foreach (var h in b.Footprint) sb.Append(h.Q).Append(',').Append(h.R).Append(' ');
            sb.Append(';');
        }
        sb.Append('|');
        foreach (var l in m.Landmarks)
            sb.Append((int)l.Kind).Append(':').Append(l.Archetype).Append(':').Append(l.Site.Q).Append(',').Append(l.Site.R).Append(';');
        sb.Append('|');
        foreach (var br in m.Bridges)
        {
            foreach (var h in br.Footprint) sb.Append(h.Q).Append(',').Append(h.R).Append(' ');
            sb.Append(';');
        }
        return sb.ToString();
    }

    public static readonly TheoryData<string> PresetNames = new() { "village", "small_town", "big_city" };

    private static CityPreset PresetByName(string name) => name switch
    {
        "village" => CityPreset.Village(),
        "small_town" => CityPreset.SmallTown(),
        _ => CityPreset.BigCity(),
    };

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Same_seed_same_preset_produces_the_identical_city(string presetName)
    {
        var a = CityGenerator.Generate(42u, PresetByName(presetName));
        var b = CityGenerator.Generate(42u, PresetByName(presetName));
        Assert.Equal(Canonical(a), Canonical(b));
    }

    [Fact]
    public void Different_seeds_produce_different_cities()
    {
        var a = CityGenerator.Generate(1u, CityPreset.SmallTown());
        var b = CityGenerator.Generate(2u, CityPreset.SmallTown());
        Assert.NotEqual(Canonical(a), Canonical(b));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Buildings_never_overlap_roads_or_each_other_and_stay_in_bounds(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(7u, preset);

        var region = new HashSet<HexCoord>();
        for (var row = 0; row < preset.HeightHexes; row++)
            for (var col = 0; col < preset.WidthHexes; col++)
                region.Add(HexCoord.FromOffset(col, row));

        var roads = new HashSet<HexCoord>(m.Roads);
        var water = new HashSet<HexCoord>(m.Water);
        var taken = new HashSet<HexCoord>();
        foreach (var b in m.Buildings)
        {
            Assert.NotEmpty(b.Footprint);
            foreach (var h in b.Footprint)
            {
                Assert.Contains(h, region);
                Assert.DoesNotContain(h, roads);
                Assert.DoesNotContain(h, water);
                Assert.True(taken.Add(h), $"hex {h} used by two buildings");
            }
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Roads_and_buildings_both_exist(string presetName)
    {
        var m = CityGenerator.Generate(3u, PresetByName(presetName));
        Assert.NotEmpty(m.Roads);
        Assert.NotEmpty(m.Buildings);
    }

    [Theory]
    [InlineData("village", 2, 1)]     // 1 km2:  round(1.5)=2 emitters, max(1, round(0.5))=1 hub
    [InlineData("small_town", 6, 2)]  // 4 km2:  6 emitters, 2 hubs
    [InlineData("big_city", 10, 6)]   // 25 km2: capped at docs/02's 10, hubs capped at 6
    public void Landmark_counts_follow_the_docs_densities_and_caps(string presetName, int emitters, int hubs)
    {
        var m = CityGenerator.Generate(11u, PresetByName(presetName));
        Assert.Equal(emitters, m.Landmarks.Count(l => l.Kind == LandmarkKind.Emitter));
        Assert.Equal(hubs, m.Landmarks.Count(l => l.Kind == LandmarkKind.CommunityHub));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Every_landmark_node_is_emitter_XOR_hub_at_a_distinct_site(string presetName)
    {
        var m = CityGenerator.Generate(5u, PresetByName(presetName));
        var sites = m.Landmarks.Select(l => l.Site).ToList();
        Assert.Equal(sites.Count, sites.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Landmark_radii_are_3_for_emitters_and_5_for_collection_stations(string presetName)
    {
        var m = CityGenerator.Generate(5u, PresetByName(presetName));
        foreach (var l in m.Landmarks)
        {
            var expected = l.Kind == LandmarkKind.Emitter ? 3 : 5;
            Assert.Equal(expected, l.RadiusHexes);
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Landmark_archetypes_come_from_the_preset_lists(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(9u, preset);
        foreach (var l in m.Landmarks)
        {
            var pool = l.Kind == LandmarkKind.Emitter ? preset.EmitterArchetypes : preset.HubArchetypes;
            Assert.Contains(l.Archetype, pool);
        }
    }

    [Fact]
    public void Village_anchors_an_emitter_on_the_central_plaza()
    {
        var preset = CityPreset.Village();
        var center = HexCoord.FromOffset(preset.WidthHexes / 2, preset.HeightHexes / 2);
        var m = CityGenerator.Generate(21u, preset);

        var first = m.Landmarks[0];
        Assert.Equal(LandmarkKind.Emitter, first.Kind);
        // The plaza block is center + ring 1; the site is its central hex.
        Assert.True(first.Site.DistanceTo(center) <= 1,
            $"plaza landmark at {first.Site}, expected within 1 hex of center {center}");
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Every_landmark_gets_a_landmark_tier_building_with_docs18_stats(string presetName)
    {
        var m = CityGenerator.Generate(13u, PresetByName(presetName));
        var landmarkBuildings = m.Buildings.Where(b => b.Tier == BuildingTier.Landmark).ToList();
        Assert.Equal(m.Landmarks.Count, landmarkBuildings.Count);
        Assert.Equal(3000, BuildingStats.StructureHp(BuildingTier.Landmark));
        Assert.Equal(8, BuildingStats.Armor(BuildingTier.Landmark));
    }

    [Fact]
    public void Building_tier_stats_match_the_docs18_table_exactly()
    {
        Assert.Equal(300, BuildingStats.StructureHp(BuildingTier.Small));
        Assert.Equal(2, BuildingStats.Armor(BuildingTier.Small));
        Assert.Equal(600, BuildingStats.StructureHp(BuildingTier.Medium));
        Assert.Equal(4, BuildingStats.Armor(BuildingTier.Medium));
        Assert.Equal(1500, BuildingStats.StructureHp(BuildingTier.Large));
        Assert.Equal(6, BuildingStats.Armor(BuildingTier.Large));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Footprint_sizes_match_tiers(string presetName)
    {
        var m = CityGenerator.Generate(17u, PresetByName(presetName));
        foreach (var b in m.Buildings)
        {
            var size = b.Footprint.Count;
            switch (b.Tier)
            {
                case BuildingTier.Small: Assert.Equal(1, size); break;
                case BuildingTier.Medium: Assert.Equal(2, size); break;
                case BuildingTier.Large: Assert.Equal(4, size); break;
                case BuildingTier.Landmark: Assert.InRange(size, 1, 7); break;
            }
        }
    }

    [Fact]
    public void Big_city_generates_quickly_enough_to_live_in_a_loading_screen()
    {
        // docs/18 SS6: the city generates during the match-start loading
        // screen on both clients. Not a rigorous benchmark -- a smoke
        // ceiling so a quadratic regression fails loudly here rather than
        // surfacing as a mysterious mobile loading-screen stall later.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var m = CityGenerator.Generate(99u, CityPreset.BigCity());
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000, $"took {sw.ElapsedMilliseconds} ms");
        Assert.True(m.Buildings.Count > 1000, $"suspiciously sparse: {m.Buildings.Count} buildings");
    }
}
