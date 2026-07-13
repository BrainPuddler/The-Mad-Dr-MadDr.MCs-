using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>The navigation layer for waypoint movement: paths must avoid
/// buildings (unless attacking them), respect the water rule per movement
/// class, cross the river only at bridges for ground units, and react to
/// live destruction (rubble opens routes; a destroyed bridge closes one).</summary>
public class HexPathfinderTests
{
    private static (CityModel city, BattlefieldState state) Fresh(uint seed = 42u)
    {
        var city = CityGenerator.Generate(seed, CityPreset.Village());
        return (city, BattlefieldState.FreshFrom(city));
    }

    [Fact]
    public void Straight_path_on_open_ground_has_length_distance_plus_one()
    {
        var (city, state) = Fresh();
        var blocked = state.BlockedToGround();

        // find two open hexes a few steps apart on the same row
        var center = city.CenterHex;
        HexCoord? a = null, b = null;
        foreach (var h in center.Range(8))
        {
            if (blocked.Contains(h) || !city.Contains(h)) continue;
            var h2 = new HexCoord(h.Q + 3, h.R);
            var mid1 = new HexCoord(h.Q + 1, h.R);
            var mid2 = new HexCoord(h.Q + 2, h.R);
            if (city.Contains(h2) && !blocked.Contains(h2) && !blocked.Contains(mid1) && !blocked.Contains(mid2))
            {
                a = h; b = h2; break;
            }
        }
        Assert.NotNull(a);

        var path = HexPathfinder.FindPath(a!.Value, b!.Value, city, blocked);
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count); // distance 3 -> 4 hexes inclusive
        Assert.Equal(a!.Value, path[0]);
        Assert.Equal(b!.Value, path[^1]);
    }

    [Fact]
    public void Paths_never_enter_blocked_hexes()
    {
        var (city, state) = Fresh();
        var blocked = state.BlockedToGround();
        var center = city.CenterHex;

        // path across a decent stretch of the town: from near center to a
        // far corner-ish open hex; every step must be legal
        HexCoord? start = null, goal = null;
        foreach (var h in center.Range(3)) if (city.Contains(h) && !blocked.Contains(h)) { start = h; break; }
        foreach (var h in HexCoord.FromOffset(8, 8).Range(4)) if (city.Contains(h) && !blocked.Contains(h)) { goal = h; break; }
        Assert.NotNull(start);
        Assert.NotNull(goal);

        var path = HexPathfinder.FindPath(start!.Value, goal!.Value, city, blocked);
        Assert.NotNull(path);
        foreach (var step in path!)
        {
            Assert.True(city.Contains(step));
            Assert.DoesNotContain(step, blocked);
        }
        // consecutive steps are always hex-adjacent -- no teleports
        for (var i = 1; i < path.Count; i++)
            Assert.Equal(1, path[i - 1].DistanceTo(path[i]));
    }

    [Fact]
    public void Ground_path_across_the_river_uses_a_bridge_deck()
    {
        var (city, state) = Fresh();
        var blocked = state.BlockedToGround();

        // Derive one open hex on each bank FROM a bridge itself -- its
        // deck spans the river band vertically, so the unblocked
        // neighbors just beyond its lowest-R and highest-R deck hexes sit
        // on opposite banks by construction. No guessing from row
        // numbers, which drift with the river.
        var bridge = city.Bridges[0];
        HexCoord top = bridge.Footprint[0], bottom = bridge.Footprint[0];
        foreach (var h in bridge.Footprint)
        {
            if (h.R < top.R) top = h;
            if (h.R > bottom.R) bottom = h;
        }
        HexCoord? north = null, south = null;
        foreach (var n in top.Neighbors())
            if (n.R < top.R && city.Contains(n) && !blocked.Contains(n)) { north = n; break; }
        foreach (var n in bottom.Neighbors())
            if (n.R > bottom.R && city.Contains(n) && !blocked.Contains(n)) { south = n; break; }
        Assert.NotNull(north);
        Assert.NotNull(south);

        var path = HexPathfinder.FindPath(north!.Value, south!.Value, city, blocked);
        Assert.NotNull(path); // banks connect only via bridges, so a path proves bridge use

        var bridgeHexes = new HashSet<HexCoord>();
        foreach (var b in city.Bridges) foreach (var h in b.Footprint) bridgeHexes.Add(h);
        Assert.Contains(path!, h => bridgeHexes.Contains(h));
    }

    [Fact]
    public void Amphibious_path_may_cross_open_water_ground_path_may_not()
    {
        var (city, state) = Fresh();
        var ground = state.BlockedToGround();
        var amphibious = state.BlockedToAmphibious();

        var water = new HashSet<HexCoord>(city.Water);
        // ground: no path step is ever water; amphibious: allowed to enter it
        HexCoord? start = null;
        foreach (var h in city.CenterHex.Range(3)) if (city.Contains(h) && !ground.Contains(h)) { start = h; break; }
        Assert.NotNull(start);

        // aim straight INTO the river: for amphibious that's a legal goal
        HexCoord? wetGoal = null;
        foreach (var w in city.Water) { wetGoal = w; break; }
        Assert.NotNull(wetGoal);

        Assert.Null(HexPathfinder.FindPath(start!.Value, wetGoal!.Value, city, ground));       // water is a wall
        var amphPath = HexPathfinder.FindPath(start!.Value, wetGoal!.Value, city, amphibious); // water is a highway
        Assert.NotNull(amphPath);
    }

    [Fact]
    public void Attacking_a_building_paths_to_its_rim_not_into_it()
    {
        var (city, state) = Fresh();
        var blocked = state.BlockedToGround();

        // Buildings[0] is a landmark: its footprint[0] is the SITE hex,
        // ringed entirely by the rest of its own footprint -- exactly the
        // case that makes single-hex "path adjacent to this" impossible
        // and the full-footprint query necessary.
        var building = city.Buildings[0];
        var center = building.Footprint[0];
        Assert.Contains(center, blocked); // standing building blocks its hexes

        HexCoord? start = null;
        foreach (var h in city.CenterHex.Range(3)) if (city.Contains(h) && !blocked.Contains(h)) { start = h; break; }
        Assert.NotNull(start);

        Assert.Null(HexPathfinder.FindPath(start!.Value, center, city, blocked)); // can't path INTO it
        var approach = HexPathfinder.FindPathToBuilding(start!.Value, building.Footprint, city, blocked);
        Assert.NotNull(approach);

        // ends adjacent to SOME footprint hex, on open ground
        var last = approach![^1];
        Assert.DoesNotContain(last, blocked);
        Assert.Contains(building.Footprint, f => f.DistanceTo(last) == 1);
    }

    [Fact]
    public void Destroying_a_building_opens_a_path_through_its_rubble()
    {
        var (city, state) = Fresh();
        var building = state.Buildings[0];
        var hex = building.Building.Footprint[0];

        Assert.Contains(hex, state.BlockedToGround());

        var destroyed = state.WithBuildingDamage(building.ApplyDamage(building.MaxHp));
        var blockedAfter = destroyed.BlockedToGround();
        Assert.DoesNotContain(hex, blockedAfter);

        // and the pathfinder can now route straight through the rubble
        HexCoord? nearStart = null;
        foreach (var n in hex.Neighbors()) if (city.Contains(n) && !blockedAfter.Contains(n)) { nearStart = n; break; }
        Assert.NotNull(nearStart);
        var through = HexPathfinder.FindPath(nearStart!.Value, hex, city, blockedAfter);
        Assert.NotNull(through);
    }

    [Fact]
    public void Same_query_always_returns_the_identical_path()
    {
        var (city, state) = Fresh();
        var blocked = state.BlockedToGround();
        HexCoord? start = null, goal = null;
        foreach (var h in city.CenterHex.Range(3)) if (city.Contains(h) && !blocked.Contains(h)) { start = h; break; }
        foreach (var h in HexCoord.FromOffset(40, 40).Range(5)) if (city.Contains(h) && !blocked.Contains(h)) { goal = h; break; }
        Assert.NotNull(start);
        Assert.NotNull(goal);

        var a = HexPathfinder.FindPath(start!.Value, goal!.Value, city, blocked);
        var b = HexPathfinder.FindPath(start!.Value, goal!.Value, city, blocked);
        Assert.NotNull(a);
        Assert.Equal(a!.Count, b!.Count);
        for (var i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }
}
