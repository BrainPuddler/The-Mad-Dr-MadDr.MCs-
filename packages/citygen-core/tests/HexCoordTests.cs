using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

public class HexCoordTests
{
    [Fact]
    public void Self_distance_is_zero()
    {
        var h = new HexCoord(3, -2);
        Assert.Equal(0, h.DistanceTo(h));
    }

    [Fact]
    public void Every_neighbor_is_exactly_one_hex_away()
    {
        var origin = new HexCoord(0, 0);
        foreach (var n in origin.Neighbors())
        {
            Assert.Equal(1, origin.DistanceTo(n));
        }
    }

    [Fact]
    public void Neighbors_are_six_distinct_hexes()
    {
        var origin = new HexCoord(5, -5);
        var neighbors = origin.Neighbors().ToList();
        Assert.Equal(6, neighbors.Count);
        Assert.Equal(6, neighbors.Distinct().Count());
        Assert.DoesNotContain(origin, neighbors);
    }

    [Theory]
    [InlineData(0, 0, 3, 0, 3)]
    [InlineData(0, 0, 0, 4, 4)]
    [InlineData(0, 0, -2, -2, 4)]
    [InlineData(1, 1, 4, 1, 3)]
    public void Distance_matches_known_hex_geometry(int q1, int r1, int q2, int r2, int expected)
    {
        var a = new HexCoord(q1, r1);
        var b = new HexCoord(q2, r2);
        Assert.Equal(expected, a.DistanceTo(b));
        Assert.Equal(expected, b.DistanceTo(a)); // symmetric
    }

    [Fact]
    public void Ring_radius_zero_is_just_the_center()
    {
        var origin = new HexCoord(1, 1);
        Assert.Equal(new[] { origin }, origin.Ring(0));
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 12)]
    [InlineData(3, 18)]
    [InlineData(5, 30)] // Collection Station radius, docs/18/20
    public void Ring_count_is_6_times_radius(int radius, int expectedCount)
    {
        var origin = new HexCoord(0, 0);
        var ring = origin.Ring(radius).ToList();
        Assert.Equal(expectedCount, ring.Count);
        Assert.All(ring, h => Assert.Equal(radius, origin.DistanceTo(h)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(3, 37)] // an emitter's 3-hex aura, docs/03
    [InlineData(5, 91)] // a Collection Station's 5-hex radius, docs/18/20
    public void Range_is_a_filled_disc_1_plus_3r_r_plus_1(int radius, int expectedCount)
    {
        var origin = new HexCoord(0, 0);
        var range = origin.Range(radius).ToList();
        Assert.Equal(expectedCount, range.Count);
        Assert.Equal(range.Count, range.Distinct().Count()); // no duplicates
        Assert.All(range, h => Assert.True(origin.DistanceTo(h) <= radius));
    }

    [Fact]
    public void Adjacent_hex_centers_are_exactly_HexMeters_apart()
    {
        var origin = new HexCoord(0, 0);
        var (x0, z0) = origin.ToWorld();
        foreach (var n in origin.Neighbors())
        {
            var (x1, z1) = n.ToWorld();
            var dist = Math.Sqrt(Math.Pow(x1 - x0, 2) + Math.Pow(z1 - z0, 2));
            Assert.Equal(HexCoord.HexMeters, dist, precision: 6);
        }
    }

    [Fact]
    public void HexMeters_is_20_per_docs18()
    {
        Assert.Equal(20.0, HexCoord.HexMeters);
    }
}
