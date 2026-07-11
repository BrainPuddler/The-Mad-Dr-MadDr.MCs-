using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

public class FacingTests
{
    private static readonly HexCoord Defender = new(0, 0);

    [Fact]
    public void Attacker_on_the_faced_edge_is_front()
    {
        var facing = HexEdge.E;
        var attacker = Defender.Neighbor(facing);
        Assert.Equal(Arc.Front, Facing.ArcOf(attacker, Defender, facing));
    }

    [Theory]
    [InlineData(HexEdge.E, HexEdge.NE)] // faced edge + 1
    [InlineData(HexEdge.E, HexEdge.SE)] // faced edge - 1
    public void Attackers_one_edge_either_side_of_faced_edge_are_still_front(HexEdge facing, HexEdge approach)
    {
        var attacker = Defender.Neighbor(approach);
        Assert.Equal(Arc.Front, Facing.ArcOf(attacker, Defender, facing));
    }

    [Fact]
    public void Attacker_on_the_exact_opposite_edge_is_rear()
    {
        var facing = HexEdge.E;
        var opposite = HexEdge.W; // E and W are opposite (offset 3 of 6)
        var attacker = Defender.Neighbor(opposite);
        Assert.Equal(Arc.Rear, Facing.ArcOf(attacker, Defender, facing));
    }

    [Theory]
    [InlineData(HexEdge.E, HexEdge.NW)]
    [InlineData(HexEdge.E, HexEdge.SW)]
    public void The_remaining_two_edges_are_flank(HexEdge facing, HexEdge approach)
    {
        var attacker = Defender.Neighbor(approach);
        Assert.Equal(Arc.Flank, Facing.ArcOf(attacker, Defender, facing));
    }

    [Fact]
    public void All_six_approach_directions_partition_into_3_front_2_flank_1_rear()
    {
        var facing = HexEdge.NE;
        var counts = new Dictionary<Arc, int> { [Arc.Front] = 0, [Arc.Flank] = 0, [Arc.Rear] = 0 };
        foreach (var approach in Enum.GetValues<HexEdge>())
        {
            var attacker = Defender.Neighbor(approach);
            counts[Facing.ArcOf(attacker, Defender, facing)]++;
        }
        Assert.Equal(3, counts[Arc.Front]);
        Assert.Equal(2, counts[Arc.Flank]);
        Assert.Equal(1, counts[Arc.Rear]);
    }

    [Fact]
    public void Non_adjacent_attacker_throws_because_arcs_are_a_melee_adjacency_concept()
    {
        var farAway = new HexCoord(5, 5);
        Assert.Throws<ArgumentException>(() => Facing.ArcOf(farAway, Defender, HexEdge.E));
    }
}
