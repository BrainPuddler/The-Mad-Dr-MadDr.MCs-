using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>
/// Geometry note: walking k hexes in a straight line along one HexEdge
/// direction from the origin lands at Euclidean distance exactly
/// k * HexCoord.HexMeters (20 m) -- adjacent-center spacing is constant
/// in every direction on this grid, and a straight run just accumulates
/// it. HexCoord(k, 0) is exactly that run along HexEdge.E, so these
/// tests use it directly for exact, non-flaky boundary distances instead
/// of picking hexes and hoping.
/// </summary>
public class EngagementZoneTests
{
    private static readonly EngagementZoneConfig Config = EngagementZoneConfig.Default; // 175 m / 1000 m

    [Fact]
    public void Default_config_matches_docs18_numbers()
    {
        Assert.Equal(175.0, EngagementZoneConfig.Default.EngagementRadiusMeters);
        Assert.Equal(1000.0, EngagementZoneConfig.Default.LocalCityRadiusMeters);
    }

    [Theory]
    [InlineData(-1.0, 1000.0)]
    [InlineData(0.0, 1000.0)]
    [InlineData(175.0, 175.0)]  // local-city radius must strictly exceed engagement radius
    [InlineData(175.0, 100.0)]
    public void Config_rejects_nonsensical_radii(double engagement, double localCity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngagementZoneConfig(engagement, localCity));
    }

    [Fact]
    public void No_engagements_anywhere_means_everything_is_distant_skyline()
    {
        var empty = Array.Empty<HexCoord>();
        Assert.Equal(EngagementZone.DistantSkyline, EngagementZoneManager.ClassifyHex(new HexCoord(0, 0), empty, Config));
        Assert.Equal(EngagementZone.DistantSkyline, EngagementZoneManager.ClassifyHex(new HexCoord(500, 500), empty, Config));
    }

    [Fact]
    public void A_hex_at_the_engagement_center_is_the_engagement_zone()
    {
        var center = new HexCoord(0, 0);
        Assert.Equal(EngagementZone.Engagement, EngagementZoneManager.ClassifyHex(center, new[] { center }, Config));
    }

    [Theory]
    [InlineData(8, EngagementZone.Engagement)]      // 160 m, inside 175 m engagement radius
    [InlineData(9, EngagementZone.LocalCity)]       // 180 m, outside engagement, inside 1000 m local-city
    [InlineData(49, EngagementZone.LocalCity)]      // 980 m, comfortably inside the local-city radius
    [InlineData(51, EngagementZone.DistantSkyline)] // 1020 m, past the local-city radius
    public void Zone_boundaries_match_docs18_radii_exactly(int hexesAway, EngagementZone expected)
    {
        var center = new HexCoord(0, 0);
        var position = new HexCoord(hexesAway, 0);
        Assert.Equal(expected, EngagementZoneManager.ClassifyHex(position, new[] { center }, Config));
    }

    [Fact]
    public void Classifies_by_the_nearest_of_multiple_engagement_centers()
    {
        var farCenter = new HexCoord(1000, 0);
        var nearCenter = new HexCoord(0, 0);
        var position = new HexCoord(8, 0); // 160 m from nearCenter, far from farCenter

        var zone = EngagementZoneManager.ClassifyHex(position, new[] { farCenter, nearCenter }, Config);
        Assert.Equal(EngagementZone.Engagement, zone);
    }

    [Fact]
    public void ClassifyBuilding_uses_the_footprints_closest_hex_not_its_centroid()
    {
        // One footprint hex at 160m (Engagement), one at 180m (would be
        // LocalCity alone) -- the building should get the better zone.
        var footprint = new[] { new HexCoord(8, 0), new HexCoord(9, 0) };
        var building = new Building(footprint, BuildingTier.Large);
        var center = new HexCoord(0, 0);

        var zone = EngagementZoneManager.ClassifyBuilding(building, new[] { center }, Config);
        Assert.Equal(EngagementZone.Engagement, zone);
    }

    [Fact]
    public void ClassifyBuilding_with_no_engagements_is_distant_skyline()
    {
        var building = new Building(new[] { new HexCoord(0, 0) }, BuildingTier.Small);
        var zone = EngagementZoneManager.ClassifyBuilding(building, Array.Empty<HexCoord>(), Config);
        Assert.Equal(EngagementZone.DistantSkyline, zone);
    }
}
