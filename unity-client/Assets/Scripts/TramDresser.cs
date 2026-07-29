using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// The mood-board's "streetcar/tram running on visibly embedded rail
/// lines down a cobblestone/brick street" (docs/23-rts-master-build-plan.md
/// §10 daytime mood-board addition), deferred at Phase 10 with three
/// reasons named explicitly: a moving vehicle (see <see cref="TramCar"/>),
/// a distinct embedded-rail street treatment (this file), and
/// region-gating logic that didn't exist anywhere yet. The mood-board's
/// own language hedges with "likely" every time it names a region, but
/// is consistent about which one: New York specifically (§8's own entry
/// calls out "elevated-rail segments on the railyard system" as part of
/// that region's identity) -- so <see cref="RuntimeCityBuilder"/> only
/// calls this for <see cref="CityRegion.NewYork"/>, not city-wide.
///
/// This is a DISTINCT concept from <c>RoadDresser.DressRailSiding</c>
/// (the existing freight-rail siding near a rail_depot landmark): siding
/// runs PARALLEL to a road, offset to the side, gated by proximity to a
/// cargo landmark; a tram line runs embedded IN the street itself,
/// gated by tracing an actual route through downtown. They happen to
/// share a rail material (<see cref="RoadDresser.RailSteel"/>, now
/// public for exactly this reuse) and nothing else.
///
/// No per-hex district/region tag exists anywhere in citygen-core or
/// any Unity dresser to key a tram line off of (confirmed by research
/// before writing this file) -- and New York's own `Grid` road pattern
/// has no distinguished-arterial subset either (`CityModel.ArterialRoads`
/// is empty for `Grid`, per <see cref="CityPreset"/>'s own generation).
/// So <see cref="TraceLine"/> computes a real route itself: the single
/// longest straight cardinal corridor of road hexes through downtown,
/// found by walking outward from the city center in each of the four
/// world-cardinal directions (the SAME offset-column/row stepping the
/// 2026-07 cardinal road rewrite already established -- see
/// <see cref="RoadDresser.Offset"/>) and keeping whichever opposing pair
/// (East+West or North+South) combines into the longer total run. This
/// mirrors how Paris's own diagonal boulevards are traced as fixed-hex-
/// direction walks (<see cref="CityPreset.Paris"/>) -- a deterministic
/// walk over the generated network, not a new generator feature.
/// </summary>
public static class TramDresser
{
    /// <summary>Below this many hexes, a "tram line" would read as an
    /// embarrassing two-block stub rather than a real route -- no line
    /// at all reads better than a token one. A city whose downtown
    /// street grid genuinely doesn't have a long enough straight run
    /// simply gets no tram, same honesty the region gate above already
    /// applies.</summary>
    private const int MinLineLengthHexes = 8;

    /// <summary>Upper bound on the traced line's total length -- see
    /// <see cref="TraceLine"/>'s own comment for why this exists (a
    /// downtown grid's straight run can otherwise reach the map edge,
    /// far longer than a player would ever see a tram actually
    /// traverse). ~40 hexes is 800 m -- a few minutes end to end at
    /// TramCar's cruise speed, a loop a player can plausibly watch
    /// complete.</summary>
    private const int MaxLineLengthHexes = 40;

    /// <summary>Half the rail gauge, in meters -- real streetcar gauge
    /// is close to standard rail (~1.435 m), so each rail sits roughly
    /// this far from the track centerline.</summary>
    private const float GaugeHalfWidth = 0.72f;

    /// <summary>How far the rail bars sit above the road surface --
    /// barely proud, reading as embedded rather than a raised siding
    /// (contrast <c>DressRailSiding</c>'s own 0.12 m bar height, a real
    /// trackside rail sitting clear of the ground next to ballast).</summary>
    private const float RailBarHeight = 0.03f;

    /// <summary>Traces the tram line for this city, or an empty list if
    /// none is long enough to be worth drawing (see
    /// <see cref="MinLineLengthHexes"/>). Deterministic: the same
    /// generated city always traces the same line.
    ///
    /// Capped at <see cref="MaxLineLengthHexes"/>, centered on the
    /// starting hex -- a real finding from verifying this against actual
    /// generated cities (docs/12): BigCity/NewYork's own downtown grid
    /// keeps a straight run going for the city's ENTIRE width (250+
    /// hexes measured, ~5 km) with nothing to naturally stop it before
    /// the map edge. At TramCar's cruise speed that's roughly an
    /// 18-minute one-way trip -- with only a couple of cars on a line
    /// that long, a player would essentially never see one complete a
    /// loop. Capping keeps the line a length a player can actually watch
    /// run its course within a match, without changing anything about
    /// HOW it's traced.</summary>
    public static List<HexCoord> TraceLine(CityModel city)
    {
        var network = new HashSet<HexCoord>(city.Roads);
        var start = NearestRoadHex(network, city.CenterHex);
        if (!start.HasValue) return new List<HexCoord>();

        // -1 before halving: each arm plus the shared start hex must
        // together stay at or under MaxLineLengthHexes (west-arm + start
        // + east-arm, or north-arm + start + south-arm) -- halving
        // MaxLineLengthHexes directly overshoots that bound by exactly
        // one hex (the start hex itself), caught by verifying this
        // against real generated cities rather than assumed correct.
        var halfCap = (MaxLineLengthHexes - 1) / 2;
        var east = Cap(Walk(start.Value, 1, 0, network), halfCap);
        var west = Cap(Walk(start.Value, -1, 0, network), halfCap);
        var north = Cap(Walk(start.Value, 0, -1, network), halfCap);
        var south = Cap(Walk(start.Value, 0, 1, network), halfCap);

        var horizontal = new List<HexCoord>();
        for (var i = west.Count - 1; i >= 0; i--) horizontal.Add(west[i]);
        horizontal.Add(start.Value);
        horizontal.AddRange(east);

        var vertical = new List<HexCoord>();
        for (var i = north.Count - 1; i >= 0; i--) vertical.Add(north[i]);
        vertical.Add(start.Value);
        vertical.AddRange(south);

        var best = horizontal.Count >= vertical.Count ? horizontal : vertical;
        return best.Count >= MinLineLengthHexes ? best : new List<HexCoord>();
    }

    /// <summary>Dresses embedded rail bars along `line` (already-traced,
    /// non-empty) and returns the exact world points -- the same
    /// straightened centerline <see cref="RoadDresser.CardinalAnchor"/>
    /// gives every road hex's own strip -- so <see cref="TramCar"/>'s
    /// path lines up with the rails it's visually running on, not a
    /// second, independently-computed set of points that could drift
    /// apart from them.</summary>
    public static List<Vector3> Build(RuntimeCityBuilder b, CityModel city, List<HexCoord> line, Transform parent)
    {
        var points = new List<Vector3>();
        if (line == null || line.Count < 2) return points;

        var host = new GameObject("TramRail").transform;
        host.SetParent(parent, false);
        var network = new HashSet<HexCoord>(city.Roads);

        for (var i = 0; i < line.Count; i++)
        {
            var vertical = RoadDresser.CardinalNeighbors(line[i], network).Vertical;
            points.Add(RoadDresser.CardinalAnchor(b, line[i], vertical));
        }

        for (var i = 0; i < points.Count; i++)
        {
            var prev = points[Mathf.Max(0, i - 1)];
            var next = points[Mathf.Min(points.Count - 1, i + 1)];
            var dir = next - prev;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) continue;
            DressTrackHex(b, points[i], dir.normalized, host);
        }

        return points;
    }

    private static void DressTrackHex(RuntimeCityBuilder b, Vector3 center, Vector3 dir, Transform host)
    {
        var perp = new Vector3(dir.z, 0f, -dir.x);
        var rot = Quaternion.LookRotation(dir, Vector3.up);
        foreach (var rail in new[] { GaugeHalfWidth, -GaugeHalfWidth })
        {
            var bar = b.SpawnPrim(PrimitiveType.Cube, center + perp * rail + Vector3.up * RailBarHeight,
                new Vector3(0.09f, 0.05f, (float)HexCoord.HexMeters), RoadDresser.RailSteel(), host);
            bar.transform.rotation = rot;
        }
    }

    private static HexCoord? NearestRoadHex(HashSet<HexCoord> network, HexCoord from)
    {
        if (network.Contains(from)) return from;
        for (var r = 1; r <= 20; r++)
            foreach (var h in from.Ring(r))
                if (network.Contains(h)) return h;
        return null;
    }

    /// <summary>Steps by ONE offset column/row at a time (the same
    /// world-cardinal stepping <see cref="RoadDresser.Offset"/>/
    /// CardinalNeighbors already use) for as long as the network keeps
    /// going in that direction, collecting every hex walked THROUGH --
    /// including a busy 3-4 way junction hex along the way, which is
    /// correct: a real streetcar runs straight through an intersection,
    /// it doesn't stop tracing there.</summary>
    private static List<HexCoord> Walk(HexCoord start, int dCol, int dRow, HashSet<HexCoord> network)
    {
        var (col, row) = RoadDresser.Offset(start);
        var result = new List<HexCoord>();
        while (true)
        {
            col += dCol;
            row += dRow;
            var next = HexCoord.FromOffset(col, row);
            if (!network.Contains(next)) break;
            result.Add(next);
        }
        return result;
    }

    /// <summary>First `max` entries of `list` -- `Walk`'s own results
    /// are already ordered nearest-start-first, so this keeps the hexes
    /// CLOSEST to the tram line's center hex, not an arbitrary slice.</summary>
    private static List<HexCoord> Cap(List<HexCoord> list, int max)
    {
        return list.Count <= max ? list : list.GetRange(0, max);
    }
}
