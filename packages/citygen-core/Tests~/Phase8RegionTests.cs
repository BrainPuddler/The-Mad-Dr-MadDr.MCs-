using System;
using System.Collections.Generic;
using System.Text;
using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>docs/23 §8 Phase 8: `CityPreset.NewYork()`/`Paris()`/
/// `Montreal()`, the `Region` field on `CityModel`, and `RoadPattern.
/// Boulevard`'s two diagonal avenues + l'Étoile roundabout. Unity-side
/// dressing (BuildingDresser/RoadDresser region branches, palettes,
/// signature props, region picker) is a separate, not-yet-started slice
/// -- see docs/23's own Phase 8 status note for why.</summary>
public class Phase8RegionTests
{
    /// <summary>Same canonical-string idiom as `CityGeneratorTests.
    /// Canonical` (this file's own copy, since that one is private to its
    /// class) -- extended with Region and the Boulevard-only fields
    /// (already-existing ArterialRoads/Roundabouts cover those without
    /// changes).</summary>
    private static string Canonical(CityModel m)
    {
        var sb = new StringBuilder();
        sb.Append(m.Seed).Append('|').Append(m.PresetName).Append('|').Append(m.Region).Append('|');
        foreach (var r in m.Roads) sb.Append(r.Q).Append(',').Append(r.R).Append(';');
        sb.Append('|');
        foreach (var a in m.ArterialRoads) sb.Append(a.Q).Append(',').Append(a.R).Append(';');
        sb.Append('|');
        foreach (var o in m.Roundabouts) sb.Append(o.Q).Append(',').Append(o.R).Append(';');
        sb.Append('|');
        foreach (var w in m.Water) sb.Append(w.Q).Append(',').Append(w.R).Append(';');
        sb.Append('|');
        foreach (var b in m.Buildings)
        {
            sb.Append((int)b.Tier).Append(':');
            foreach (var h in b.Footprint) sb.Append(h.Q).Append(',').Append(h.R).Append(' ');
            sb.Append(';');
        }
        sb.Append('|');
        foreach (var l in m.Landmarks)
            sb.Append((int)l.Kind).Append(':').Append(l.Archetype).Append(':').Append(l.Site.Q).Append(',').Append(l.Site.R).Append(';');
        return sb.ToString();
    }

    public static readonly TheoryData<CityPreset, CityRegion> RegionPresets = new()
    {
        { CityPreset.NewYork(), CityRegion.NewYork },
        { CityPreset.Paris(), CityRegion.Paris },
        { CityPreset.Montreal(), CityRegion.Montreal },
    };

    // ---- acceptance: deterministic generation ----

    [Theory]
    [MemberData(nameof(RegionPresets))]
    public void Region_preset_generates_deterministically(CityPreset preset, CityRegion _)
    {
        var a = CityGenerator.Generate(2026u, preset);
        var b = CityGenerator.Generate(2026u, preset);
        Assert.Equal(Canonical(a), Canonical(b));
    }

    [Theory]
    [MemberData(nameof(RegionPresets))]
    public void Region_preset_produces_roads_and_buildings(CityPreset preset, CityRegion _)
    {
        var m = CityGenerator.Generate(11u, preset);
        Assert.NotEmpty(m.Roads);
        Assert.NotEmpty(m.Buildings);
        Assert.NotEmpty(m.Landmarks);
    }

    // ---- Region field ----

    [Theory]
    [MemberData(nameof(RegionPresets))]
    public void Generated_model_carries_the_presets_own_region(CityPreset preset, CityRegion expected)
    {
        var m = CityGenerator.Generate(5u, preset);
        Assert.Equal(expected, m.Region);
        Assert.Equal(expected, preset.Region);
    }

    [Theory]
    [InlineData("village")]
    [InlineData("small_town")]
    [InlineData("big_city")]
    public void Every_pre_Phase8_preset_is_generic(string presetName)
    {
        var preset = presetName switch
        {
            "village" => CityPreset.Village(),
            "small_town" => CityPreset.SmallTown(),
            _ => CityPreset.BigCity(),
        };
        Assert.Equal(CityRegion.Generic, preset.Region);
        var m = CityGenerator.Generate(6u, preset);
        Assert.Equal(CityRegion.Generic, m.Region);
    }

    // ---- Boulevard: diagonal avenues + l'Étoile roundabout ----

    [Fact]
    public void Paris_has_exactly_one_roundabout_and_it_is_the_map_center()
    {
        var preset = CityPreset.Paris();
        var m = CityGenerator.Generate(7u, preset);
        var center = HexCoord.FromOffset(preset.WidthHexes / 2, preset.HeightHexes / 2);

        Assert.Single(m.Roundabouts);
        Assert.Equal(center, m.Roundabouts[0]);
        Assert.Equal(center, m.CenterHex);
    }

    [Fact]
    public void Paris_diagonal_avenues_pass_through_the_roundabout_and_are_straight_in_world_space()
    {
        var preset = CityPreset.Paris();
        var m = CityGenerator.Generate(7u, preset);
        var center = m.Roundabouts[0];
        var (centerX, centerZ) = center.ToWorld();

        Assert.NotEmpty(m.ArterialRoads);
        foreach (var h in m.ArterialRoads) Assert.Contains(h, m.Roads);   // arterial is always a road subset

        // The étoile roundabout sits AT the crossing: every arterial hex
        // must be reachable from center by walking ONE fixed hex
        // direction repeatedly (never both axes at once) -- that's what
        // "the two diagonal avenues meet at the roundabout" means
        // geometrically. Collect each arterial hex's direction from
        // center and confirm there are exactly the two expected ones.
        // (NOT asserting every intermediate hex is itself arterial: the
        // river can drown an avenue hex that isn't one of the few chosen
        // bridge crossings, same "drowned road segments vanish" rule
        // every other road in this generator already follows -- a real
        // gap, not a broken avenue.)
        var seenDirections = new HashSet<(int dq, int dr)>();
        foreach (var h in m.ArterialRoads)
        {
            if (h.Equals(center)) continue;
            var steps = center.DistanceTo(h);
            Assert.True(steps > 0);
            var dq = (h.Q - center.Q) / steps;
            var dr = (h.R - center.R) / steps;
            Assert.Equal(h, new HexCoord(center.Q + dq * steps, center.R + dr * steps));
            seenDirections.Add((dq, dr));
        }
        // Each of the 2 avenues radiates in BOTH directions from center,
        // so 2 avenues show up as 4 distinct direction vectors -- but
        // they must form exactly 2 opposite PAIRS (a real avenue running
        // through the roundabout both ways), not 4 unrelated rays.
        Assert.Equal(4, seenDirections.Count);
        foreach (var (dq, dr) in seenDirections)
            Assert.Contains((-dq, -dr), seenDirections);

        // "Straight in world space": every arterial hex's world position,
        // relative to the roundabout's, must be a scalar multiple of ITS
        // avenue's own (fixed) world-space direction vector -- i.e. the
        // cross product of (hex - center) and (direction) is exactly
        // zero. Collinearity, not an approximation.
        var directionWorld = new Dictionary<(int, int), (double dx, double dz)>();
        foreach (var d in seenDirections)
        {
            var (dx, dz) = new HexCoord(center.Q + d.Item1, center.R + d.Item2).ToWorld();
            directionWorld[d] = (dx - centerX, dz - centerZ);
        }
        foreach (var h in m.ArterialRoads)
        {
            if (h.Equals(center)) continue;
            var steps = center.DistanceTo(h);
            var dq = (h.Q - center.Q) / steps;
            var dr = (h.R - center.R) / steps;
            var (hx, hz) = h.ToWorld();
            var relX = hx - centerX;
            var relZ = hz - centerZ;
            var (dirX, dirZ) = directionWorld[(dq, dr)];
            var cross = relX * dirZ - relZ * dirX;
            Assert.True(Math.Abs(cross) < 1e-6, $"hex {h} is not collinear with its avenue's direction (cross={cross})");
        }
    }

    [Fact]
    public void Grid_and_MainStreet_presets_are_unaffected_by_Boulevard()
    {
        // Regression: adding RoadPattern.Boulevard must not touch Grid's
        // "no arterial, no roundabouts" behavior or MainStreet's own
        // off-arterial roundabout rule -- both already covered by
        // CityGeneratorTests.cs, re-asserted here as this phase's own
        // explicit "off the arterial rule preserved" acceptance check.
        var grid = CityGenerator.Generate(8u, CityPreset.BigCity());
        Assert.Empty(grid.ArterialRoads);
        Assert.Empty(grid.Roundabouts);

        var mainStreet = CityGenerator.Generate(8u, CityPreset.SmallTown());
        Assert.NotEmpty(mainStreet.Roundabouts);
        foreach (var h in mainStreet.Roundabouts)
            Assert.DoesNotContain(h, mainStreet.ArterialRoads);   // MainStreet's rule: never on the arterial
    }

    [Fact]
    public void A_non_Boulevard_preset_places_no_letoile_style_roundabout_at_its_center()
    {
        // Montreal is MainStreet, not Boulevard -- its center hex is NOT
        // automatically a roundabout the way Paris's is.
        var preset = CityPreset.Montreal();
        var m = CityGenerator.Generate(9u, preset);
        var center = HexCoord.FromOffset(preset.WidthHexes / 2, preset.HeightHexes / 2);
        Assert.DoesNotContain(center, m.Roundabouts);
    }
}
