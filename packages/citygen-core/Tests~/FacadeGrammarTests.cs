using System.Collections.Generic;
using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>docs/30: acceptance for the facade grammar. The solver is the
/// only part of the WFC work that can be verified at all in an
/// environment with no Unity Editor, so it carries the whole verification
/// burden -- determinism, constraint satisfaction, and graceful failure.</summary>
public class FacadeGrammarTests
{
    private static ISet<HexCoord> Set(params HexCoord[] hexes) => new HashSet<HexCoord>(hexes);
    private static ISet<HexCoord> Empty() => new HashSet<HexCoord>();

    // ---- face classification -------------------------------------------

    [Fact]
    public void Face_touching_a_road_reads_as_street()
    {
        var hex = new HexCoord(5, 5);
        var roadNeighbor = hex.Neighbor(HexEdge.E);
        var roles = FacadeGrammar.ClassifyFaces(hex, Set(hex), Set(roadNeighbor), Empty());

        Assert.Contains(FaceRole.Street, roles);
    }

    [Fact]
    public void Face_touching_another_building_reads_as_party_wall()
    {
        var hex = new HexCoord(0, 0);
        var neighborBuilding = hex.Neighbor(HexEdge.E);
        var roles = FacadeGrammar.ClassifyFaces(hex, Set(hex), Empty(), Set(neighborBuilding));

        Assert.Contains(FaceRole.PartyWall, roles);
        Assert.DoesNotContain(FaceRole.Street, roles);
    }

    [Fact]
    public void Road_beats_neighbouring_building_on_the_same_face()
    {
        // A hex whose E neighbour is a road and whose (same-arc) neighbour
        // is a building must still read Street -- street frontage is what
        // the player sees, so it wins ties.
        var hex = new HexCoord(0, 0);
        var e = hex.Neighbor(HexEdge.E);
        var ne = hex.Neighbor(HexEdge.NE);
        var roles = FacadeGrammar.ClassifyFaces(hex, Set(hex), Set(e), Set(ne));

        Assert.Contains(FaceRole.Street, roles);
    }

    [Fact]
    public void Interior_face_of_a_multi_hex_building_is_never_street()
    {
        var a = new HexCoord(0, 0);
        var b = a.Neighbor(HexEdge.E);
        var footprint = Set(a, b);
        var roles = FacadeGrammar.ClassifyFaces(a, footprint, Empty(), Empty());

        // the face toward `b` must be PartyWall (interior), not dressed
        Assert.Contains(FaceRole.PartyWall, roles);
    }

    [Fact]
    public void Isolated_building_has_no_street_and_no_party_wall()
    {
        var hex = new HexCoord(9, 9);
        var roles = FacadeGrammar.ClassifyFaces(hex, Set(hex), Empty(), Empty());
        Assert.All(roles, r => Assert.Equal(FaceRole.Alley, r));
    }

    // ---- structural guarantees ------------------------------------------

    [Fact]
    public void Party_wall_is_entirely_blank()
    {
        var sol = FacadeGrammar.Solve(FaceRole.PartyWall, 3, FacadeStyle.Residential, new Rng(1u));
        Assert.All(sol.Cells, c => Assert.Equal(FacadeModule.Blank, c));
        Assert.False(sol.IsFallback);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void Strip_length_is_always_floors_plus_ground_plus_crown(int floors)
    {
        var sol = FacadeGrammar.Solve(FaceRole.Street, floors, FacadeStyle.Commercial, new Rng(7u));
        Assert.Equal(floors + 2, sol.Cells.Count);
    }

    [Fact]
    public void Ground_cell_is_always_a_ground_module_and_crown_always_a_crown_module()
    {
        for (var seed = 1u; seed <= 60u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Commercial, new Rng(seed));

            Assert.Contains(sol.Ground, new[]
            {
                FacadeModule.Shopfront, FacadeModule.RecessedEntrance, FacadeModule.StoopEntrance,
                FacadeModule.LoadingDock, FacadeModule.BlankPlinth,
            });

            Assert.Contains(sol.Crown, new[]
            {
                FacadeModule.Cornice, FacadeModule.Parapet, FacadeModule.SetbackCrown,
            });
        }
    }

    [Fact]
    public void Upper_cells_are_never_ground_or_crown_modules()
    {
        for (var seed = 1u; seed <= 60u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Street, 5, FacadeStyle.Residential, new Rng(seed));
            for (var i = 1; i < sol.Cells.Count - 1; i++)
            {
                Assert.Contains(sol.Cells[i], new[]
                {
                    FacadeModule.WindowBay, FacadeModule.BlindBay,
                    FacadeModule.FireEscapeBay, FacadeModule.OrielBay,
                });
            }
        }
    }

    /// <summary>The one genuinely architectural adjacency rule: a fire
    /// escape is a continuous vertical run, never a scatter of
    /// disconnected platforms.</summary>
    [Fact]
    public void Fire_escapes_are_vertically_continuous()
    {
        var sawOne = false;
        for (var seed = 1u; seed <= 400u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Residential, new Rng(seed));
            var uppers = new List<FacadeModule>();
            for (var i = 1; i < sol.Cells.Count - 1; i++) uppers.Add(sol.Cells[i]);

            var hasEscape = uppers.Contains(FacadeModule.FireEscapeBay);
            if (!hasEscape) continue;
            sawOne = true;

            // if any floor has it, ALL upper floors must have it
            Assert.All(uppers, m => Assert.Equal(FacadeModule.FireEscapeBay, m));
        }
        Assert.True(sawOne, "expected at least one fire escape across 400 seeds");
    }

    [Fact]
    public void Fire_escape_never_appears_when_disallowed()
    {
        for (var seed = 1u; seed <= 200u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Residential, new Rng(seed), allowFireEscape: false);
            Assert.DoesNotContain(FacadeModule.FireEscapeBay, sol.Cells);
        }
    }

    [Fact]
    public void Street_frontage_never_gets_a_loading_dock_unless_industrial()
    {
        foreach (var style in new[] { FacadeStyle.Commercial, FacadeStyle.Residential, FacadeStyle.Civic })
        {
            for (var seed = 1u; seed <= 120u; seed++)
            {
                var sol = FacadeGrammar.Solve(FaceRole.Street, 3, style, new Rng(seed));
                Assert.NotEqual(FacadeModule.LoadingDock, sol.Ground);
            }
        }
    }

    [Fact]
    public void Alley_never_gets_a_shopfront_or_an_oriel()
    {
        for (var seed = 1u; seed <= 200u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Alley, 4, FacadeStyle.Commercial, new Rng(seed));
            Assert.NotEqual(FacadeModule.Shopfront, sol.Ground);
            Assert.DoesNotContain(FacadeModule.OrielBay, sol.Cells);
        }
    }

    // ---- determinism -----------------------------------------------------

    [Fact]
    public void Same_seed_and_inputs_produce_an_identical_strip()
    {
        for (var seed = 1u; seed <= 40u; seed++)
        {
            var a = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Commercial, new Rng(seed));
            var b = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Commercial, new Rng(seed));
            Assert.Equal(a.Cells, b.Cells);
            Assert.Equal(a.Role, b.Role);
            Assert.Equal(a.IsFallback, b.IsFallback);
        }
    }

    [Fact]
    public void Different_seeds_actually_produce_variety()
    {
        var seen = new HashSet<string>();
        for (var seed = 1u; seed <= 120u; seed++)
        {
            var sol = FacadeGrammar.Solve(FaceRole.Street, 4, FacadeStyle.Commercial, new Rng(seed));
            seen.Add(string.Join(",", sol.Cells));
        }
        // A grammar that always emits the same strip is not variety -- but
        // one that emits 120 different strips is noise. Expect a real,
        // bounded spread.
        Assert.InRange(seen.Count, 4, 60);
    }

    // ---- robustness -------------------------------------------------------

    [Fact]
    public void Solver_never_throws_and_never_returns_an_empty_strip()
    {
        foreach (FaceRole role in System.Enum.GetValues(typeof(FaceRole)))
        foreach (FacadeStyle style in System.Enum.GetValues(typeof(FacadeStyle)))
        for (var floors = 0; floors <= 14; floors++)
        for (var seed = 1u; seed <= 8u; seed++)
        {
            var sol = FacadeGrammar.Solve(role, floors, style, new Rng(seed));
            Assert.NotNull(sol);
            Assert.Equal(floors + 2, sol.Cells.Count);
        }
    }

    [Fact]
    public void StyleFor_maps_tiers_the_same_way_the_dresser_dispatches()
    {
        Assert.Equal(FacadeStyle.Industrial, FacadeGrammar.StyleFor(BuildingTier.Medium, industrial: true, suburb: false));
        Assert.Equal(FacadeStyle.Civic, FacadeGrammar.StyleFor(BuildingTier.Landmark, false, false));
        Assert.Equal(FacadeStyle.Commercial, FacadeGrammar.StyleFor(BuildingTier.Large, false, false));
        Assert.Equal(FacadeStyle.Residential, FacadeGrammar.StyleFor(BuildingTier.Medium, false, suburb: true));
        Assert.Equal(FacadeStyle.Commercial, FacadeGrammar.StyleFor(BuildingTier.Medium, false, suburb: false));
        Assert.Equal(FacadeStyle.Residential, FacadeGrammar.StyleFor(BuildingTier.Small, false, false));
    }
}
