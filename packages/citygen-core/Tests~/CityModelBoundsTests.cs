using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>Regression tests for the first live monster spawn wandering
/// OUTSIDE the city: axial (0,0) is the offset rectangle's top-left
/// corner, not its middle, and nothing bounded spawn/wander hexes to the
/// map. CityModel.Contains/CenterHex are the primitives that fix it.</summary>
public class CityModelBoundsTests
{
    [Fact]
    public void Contains_covers_exactly_the_generated_region()
    {
        var preset = CityPreset.Village();
        var m = CityGenerator.Generate(42u, preset);

        // Every hex the generator enumerated is in bounds...
        for (var row = 0; row < preset.HeightHexes; row++)
            for (var col = 0; col < preset.WidthHexes; col++)
                Assert.True(m.Contains(HexCoord.FromOffset(col, row)), $"({col},{row}) should be in bounds");

        // ...and one step past every edge is out.
        for (var row = -1; row <= preset.HeightHexes; row++)
        {
            Assert.False(m.Contains(HexCoord.FromOffset(-1, row)));
            Assert.False(m.Contains(HexCoord.FromOffset(preset.WidthHexes, row)));
        }
        for (var col = -1; col <= preset.WidthHexes; col++)
        {
            Assert.False(m.Contains(HexCoord.FromOffset(col, -1)));
            Assert.False(m.Contains(HexCoord.FromOffset(col, preset.HeightHexes)));
        }
    }

    [Fact]
    public void Axial_origin_is_the_corner_not_the_center()
    {
        var m = CityGenerator.Generate(42u, CityPreset.Village());

        // (0,0) IS on the map -- that's what made the bug quiet: spawning
        // there doesn't fail, it just puts monsters at the map corner.
        Assert.True(m.Contains(new HexCoord(0, 0)));
        Assert.Equal(HexCoord.FromOffset(0, 0), new HexCoord(0, 0));

        // CenterHex is genuinely central: max distance to any corner is
        // roughly half the map diagonal, not the full diagonal.
        var center = m.CenterHex;
        Assert.Equal(HexCoord.FromOffset(25, 25), center);
        Assert.True(m.Contains(center));

        var corner = HexCoord.FromOffset(0, 0);
        var farCorner = HexCoord.FromOffset(49, 49);
        Assert.True(center.DistanceTo(corner) < corner.DistanceTo(farCorner),
            "center should be markedly closer to a corner than the corners are to each other");
    }

    [Fact]
    public void Center_range_stays_fully_on_the_map()
    {
        var m = CityGenerator.Generate(42u, CityPreset.Village());
        foreach (var hex in m.CenterHex.Range(6))
            Assert.True(m.Contains(hex), $"{hex} within Range(6) of center should be in bounds");
    }
}
