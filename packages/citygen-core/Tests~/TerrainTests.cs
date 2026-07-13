using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>Terrain layer: river, ponds, hills, and destructible
/// bridges (docs/18 terrain; docs/04 water rule). The headline test is
/// the choke-point proof: with bridges the map is one connected walk
/// for ground units; delete the bridges and the two banks disconnect.</summary>
public class TerrainTests
{
    public static readonly TheoryData<string> PresetNames = new() { "village", "small_town", "big_city" };

    private static CityPreset PresetByName(string name) => name switch
    {
        "village" => CityPreset.Village(),
        "small_town" => CityPreset.SmallTown(),
        _ => CityPreset.BigCity(),
    };

    private static HashSet<HexCoord> Region(CityPreset preset)
    {
        var region = new HashSet<HexCoord>();
        for (var row = 0; row < preset.HeightHexes; row++)
            for (var col = 0; col < preset.WidthHexes; col++)
                region.Add(HexCoord.FromOffset(col, row));
        return region;
    }

    private static int ColOf(HexCoord h) => h.Q + (h.R - (h.R & 1)) / 2;

    /// <summary>Flood-fill count from an arbitrary start over a passable set.</summary>
    private static int Reachable(HashSet<HexCoord> passable)
    {
        if (passable.Count == 0) return 0;
        HexCoord start = default;
        var found = false;
        foreach (var h in passable) { start = h; found = true; break; }
        Assert.True(found);

        var seen = new HashSet<HexCoord> { start };
        var queue = new Queue<HexCoord>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var hex = queue.Dequeue();
            foreach (var n in hex.Neighbors())
            {
                if (!passable.Contains(n) || seen.Contains(n)) continue;
                seen.Add(n);
                queue.Enqueue(n);
            }
        }
        return seen.Count;
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void River_spans_the_full_map_width(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(42u, preset);
        Assert.NotEmpty(m.Water);

        // Water (river + bridges-over-it, which count as crossings not
        // water) must reach both the west and east map edges.
        var waterCols = m.Water.Select(ColOf).ToHashSet();
        foreach (var b in m.Bridges)
            foreach (var h in b.Footprint) waterCols.Add(ColOf(h));
        Assert.Contains(0, waterCols);
        Assert.Contains(preset.WidthHexes - 1, waterCols);
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Bridges_exist_at_the_preset_count_and_sit_in_the_river(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(42u, preset);

        Assert.Equal(preset.BridgeCount, m.Bridges.Count);

        var water = new HashSet<HexCoord>(m.Water);
        var roads = new HashSet<HexCoord>(m.Roads);
        foreach (var bridge in m.Bridges)
        {
            Assert.NotEmpty(bridge.Footprint);
            // Deck hexes are road, not water...
            foreach (var h in bridge.Footprint)
            {
                Assert.Contains(h, roads);
                Assert.DoesNotContain(h, water);
                // ...but every deck hex touches the water it spans.
                Assert.Contains(h.Neighbors(), n => water.Contains(n));
            }
        }
    }

    [Fact]
    public void Bridges_are_destructible_at_the_large_building_tier()
    {
        var m = CityGenerator.Generate(42u, CityPreset.SmallTown());
        foreach (var b in m.Bridges)
        {
            Assert.Equal(BuildingTier.Large, b.Tier);
            Assert.Equal(1500, BuildingStats.StructureHp(b.Tier));
            Assert.Equal(6, BuildingStats.Armor(b.Tier));
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Bridges_are_the_choke_points_ground_connectivity_dies_with_them(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(42u, preset);

        // Ground-passable = everything that isn't water (bridge decks
        // included, since they're road).
        var water = new HashSet<HexCoord>(m.Water);
        var passable = Region(preset);
        passable.ExceptWith(water);

        // With bridges standing: one connected walk, bank to bank.
        Assert.Equal(passable.Count, Reachable(passable));

        // All bridges destroyed: their hexes revert to water, and the
        // two banks must no longer reach each other.
        foreach (var b in m.Bridges)
            foreach (var h in b.Footprint) passable.Remove(h);
        Assert.True(Reachable(passable) < passable.Count,
            "destroying every bridge should sever the map into disconnected banks");
    }

    [Fact]
    public void The_choke_point_property_holds_across_many_seeds_not_one_lucky_one()
    {
        // The width-1 village stream is the hardest case: a straight
        // offset row is impermeable, but every drift bend used to leak
        // diagonally until the staircase fill. Sweep seeds to keep that
        // fixed forever.
        var preset = CityPreset.Village();
        for (var seed = 0u; seed < 10u; seed++)
        {
            var m = CityGenerator.Generate(seed, preset);
            var water = new HashSet<HexCoord>(m.Water);
            var passable = Region(preset);
            passable.ExceptWith(water);
            Assert.Equal(passable.Count, Reachable(passable));

            foreach (var b in m.Bridges)
                foreach (var h in b.Footprint) passable.Remove(h);
            Assert.True(Reachable(passable) < passable.Count,
                $"seed {seed}: banks still connected after destroying all bridges");
        }
    }

    [Fact]
    public void Village_plaza_stays_dry_across_many_seeds()
    {
        // The river is confined to the upper or lower half specifically
        // so the center (plaza, arterial, landmark belt) survives -- check
        // across a spread of seeds, not one lucky one.
        var preset = CityPreset.Village();
        var center = HexCoord.FromOffset(preset.WidthHexes / 2, preset.HeightHexes / 2);
        for (var seed = 0u; seed < 25u; seed++)
        {
            var m = CityGenerator.Generate(seed, preset);
            var water = new HashSet<HexCoord>(m.Water);
            foreach (var h in center.Range(2))
            {
                Assert.DoesNotContain(h, water);
            }
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Ridges_exist_on_dry_open_land_only(string presetName)
    {
        var preset = PresetByName(presetName);
        var m = CityGenerator.Generate(42u, preset);

        Assert.NotEmpty(m.Ridges);
        var water = new HashSet<HexCoord>(m.Water);
        var roads = new HashSet<HexCoord>(m.Roads);
        foreach (var g in m.Ridges)
        {
            Assert.DoesNotContain(g, water);
            Assert.DoesNotContain(g, roads);
        }
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Water_and_roads_never_overlap(string presetName)
    {
        var m = CityGenerator.Generate(9u, PresetByName(presetName));
        var roads = new HashSet<HexCoord>(m.Roads);
        foreach (var w in m.Water) Assert.DoesNotContain(w, roads);
    }
}
