using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// The 1950s street network (docs/21 Phase 4). Replaces the old
/// disconnected dark squares with hub-and-spoke road tiles: every road
/// hex gets a center pad plus one connector strip toward each road/
/// bridge neighbor -- so straights, corners, T-junctions, crossroads,
/// and dead ends all EMERGE from adjacency, seamlessly, with no tile
/// catalog to desync. On top: concrete sidewalks with a curb step,
/// yellow center dashes, crosswalk stripes at 3+ way intersections, and
/// deterministic street furniture (streetlights, telephone poles,
/// hydrants, trash cans, pastel tail-finned parked cars).
///
/// Roads sit slightly PROUD of the ground (a raised roadbed) so the
/// sculpted terrain's dips at riverbanks read as embankments instead of
/// exposing a floating slab edge. Everything is colliderless -- clicks
/// fall through to the ground plane, pathing is untouched (the road hex
/// set is still gameplay truth) -- and hashed off (hex, salt) so the
/// same seed always furnishes the same streets.
/// </summary>
public static class RoadDresser
{
    private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

    private static Material M(float r, float g, float b, float emissive = 0f)
    {
        var key = ((int)(r * 255) << 20) | ((int)(g * 255) << 10) | (int)(b * 255) | ((int)(emissive * 3) << 30);
        Material mat;
        if (Cache.TryGetValue(key, out mat) && mat != null) return mat;
        mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(r, g, b);
        if (emissive > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            var baseEmission = new Color(r, g, b) * emissive;
            mat.SetColor("_EmissionColor", baseEmission);
            NeonRegistry.Register(mat, baseEmission);
        }
        Cache[key] = mat;
        return mat;
    }

    private static Material Asphalt() { return M(0.17f, 0.17f, 0.18f); }
    private static Material Sidewalk() { return M(0.58f, 0.56f, 0.52f); }
    private static Material LanePaint() { return M(0.85f, 0.7f, 0.2f); }
    private static Material CrossPaint() { return M(0.85f, 0.84f, 0.8f); }
    private static Material RoundaboutCurb() { return M(0.62f, 0.6f, 0.56f); }
    private static Material RoundaboutGrass() { return M(0.30f, 0.44f, 0.21f); }
    private static Material PoleWood() { return M(0.35f, 0.26f, 0.18f); }
    private static Material PoleMetal() { return M(0.45f, 0.48f, 0.5f); }
    private static Material Bulb() { return M(1f, 0.9f, 0.6f, 1.4f); }
    private static Material HydrantRed() { return M(0.75f, 0.15f, 0.12f); }
    private static Material CanGray() { return M(0.4f, 0.42f, 0.44f); }
    private static Material ChromeTrim() { return M(0.8f, 0.82f, 0.85f); }
    private static Material AdRed() { return M(0.82f, 0.18f, 0.16f); }

    private static readonly Color[] CarPastels =
    {
        new Color(0.72f, 0.85f, 0.82f),   // seafoam
        new Color(0.9f, 0.75f, 0.78f),    // pink
        new Color(0.75f, 0.78f, 0.88f),   // powder blue
        new Color(0.9f, 0.85f, 0.65f),    // cream yellow
        new Color(0.45f, 0.12f, 0.12f),   // dark cherry
    };

    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    public const int RailyardRadius = 4;

    public static void Build(RuntimeCityBuilder builder, CityModel city, Transform parent, HexCoord? railyardCenter = null)
    {
        var host = new GameObject("Roads").transform;
        host.SetParent(parent, false);

        // connectivity truth: city.Roads already includes bridge deck
        // hexes (CityGenerator unions them in), so `network` needs no
        // separate merge -- but RoadDresser still needs to know WHICH
        // road hexes are bridge deck, to skip dressing them itself
        var network = new HashSet<HexCoord>(city.Roads);
        var arterial = new HashSet<HexCoord>(city.ArterialRoads);
        var bridgeHexes = new HashSet<HexCoord>();
        foreach (var bridge in city.Bridges)
            foreach (var hex in bridge.Footprint) bridgeHexes.Add(hex);

        foreach (var hex in city.Roads)
        {
            // bridge deck hexes are dressed entirely by BridgeDresser
            // (deck, rails, truss, piers, sized and raised for a water
            // crossing) -- RoadDresser's own thin street pad/strips/
            // dashes on the SAME hex would z-fight with, or poke up
            // through, that deck's surface. The hex stays in `network`
            // above, so a bank hex's own connector strip still reaches
            // into the bridge threshold instead of stopping short of it.
            if (bridgeHexes.Contains(hex)) continue;

            var center = builder.WorldOf(hex);
            var connectors = new List<(Vector3 dir, float angle, bool arterial)>();
            foreach (var n in hex.Neighbors())
            {
                if (!network.Contains(n)) continue;
                var to = builder.WorldOf(n) - center;
                to.y = 0f;
                var dir = to.normalized;
                // a connector is part of the arterial only if BOTH ends
                // are -- a residential cross-street stub off a Main
                // Street junction stays ordinary width even though the
                // hex itself is arterial (creator direction, 2026-07:
                // Main Street gets a 3-4 lane road, side streets don't)
                var isArterialConnector = arterial.Contains(hex) && arterial.Contains(n);
                connectors.Add((dir, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, isArterialConnector));
            }

            // un-zigzag Grid/MainStreet's "vertical" streets (see doc
            // comment) -- straight-line-of-sight through-hexes render at
            // a corrected anchor with a due-north/south bearing instead
            // of their true kinked diagonal neighbor angle
            if (TryStraightenCardinal(hex, network, connectors, out var correction))
                center += correction;

            DressHex(builder, hex, center, connectors, arterial.Contains(hex), host);

            // railyard siding (docs/21 batch 2, item 6): a parallel rail
            // track alongside straight road hexes near a rail_depot
            // landmark, tying the depot into a small industrial district
            if (railyardCenter.HasValue && connectors.Count == 2 && hex.DistanceTo(railyardCenter.Value) <= RailyardRadius)
                DressRailSiding(builder, center, connectors[0].dir, host);
        }
    }

    /// <summary>Offset-column coordinates approximate "south" by
    /// alternating between two different diagonal hex edges every row
    /// (SE from an even row, SW from an odd row) -- on a pointy-top hex
    /// grid, no single edge points due south, so a literal "vertical"
    /// road (Grid's `col % pitch == 0` streets, MainStreet's
    /// perpendiculars) saws left-right by HexMeters/2 (10m) at EVERY
    /// hex if rendered at raw hex centers with true neighbor bearings --
    /// this is the "roads running north south are zig-zagging" report.
    /// A hex whose ONLY road connections are that row-parity-specific
    /// diagonal pair is a pure through-segment of such a corridor (a
    /// real turn or junction always has a different connector set, and
    /// is deliberately left alone by the exact-count check below): for
    /// those hexes, and only those, this rewrites `connectors` to a due
    /// north/south bearing and returns an x correction of +-HexMeters/4
    /// -- the exact midpoint between the two alternating raw offsets, so
    /// a hex's corrected anchor lands on precisely the same x as its
    /// corridor neighbors' corrected anchors, turning the sawtooth into
    /// one continuous straight street. Row-based E/W streets never
    /// trigger this (they're already exactly straight: z depends only on
    /// R), and neither do corners/dead-ends against a cross street or
    /// 3+-way intersections.
    ///
    /// Public so `BridgeDresser` can apply the IDENTICAL correction to
    /// bridge deck hexes -- bridge hexes are themselves ordinary members
    /// of `city.Roads` (CityGenerator unions them in), so a "vertical"
    /// corridor that happens to cross a river zigzags exactly the same
    /// way through its bridge hexes as through its road hexes. Sharing
    /// this one function instead of a second copy is what guarantees the
    /// two dressers agree on the SAME corrected anchor for a hex that
    /// sits on the seam between them, so a bridge lines up with its
    /// approach roads and with its own neighboring bridge hexes -- not
    /// just internally consistent with itself.</summary>
    public static bool TryStraightenCardinal(HexCoord hex, HashSet<HexCoord> network,
        List<(Vector3 dir, float angle, bool arterial)> connectors, out Vector3 correction)
    {
        correction = Vector3.zero;
        var rEven = (hex.R & 1) == 0;
        var hasNorth = network.Contains(hex.Neighbor(rEven ? HexEdge.NE : HexEdge.NW));
        var hasSouth = network.Contains(hex.Neighbor(rEven ? HexEdge.SE : HexEdge.SW));
        var throughCount = (hasNorth ? 1 : 0) + (hasSouth ? 1 : 0);
        if (throughCount == 0 || throughCount != connectors.Count) return false;

        // a pure vertical corridor is always a residential/perpendicular
        // street, never Main Street itself (the arterial's own row runs
        // east/west by construction) -- never arterial-wide
        connectors.Clear();
        if (hasNorth)
        {
            var d = new Vector3(0f, 0f, -1f);
            connectors.Add((d, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, false));
        }
        if (hasSouth)
        {
            var d = new Vector3(0f, 0f, 1f);
            connectors.Add((d, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, false));
        }
        correction = new Vector3((rEven ? 1f : -1f) * (float)HexCoord.HexMeters / 4f, 0f, 0f);
        return true;
    }

    private static Material RailSteel() { return M(0.32f, 0.33f, 0.35f); }
    private static Material Tie() { return M(0.28f, 0.2f, 0.13f); }

    private static void DressRailSiding(RuntimeCityBuilder b, Vector3 center, Vector3 dir, Transform host)
    {
        var perp = new Vector3(dir.z, 0f, -dir.x);
        var rot = Quaternion.LookRotation(dir, Vector3.up);
        var trackCenter = center + perp * 6.5f;

        foreach (var rail in new[] { 0.35f, -0.35f })
        {
            var bar = b.SpawnPrim(PrimitiveType.Cube, trackCenter + perp * rail + Vector3.up * 0.12f,
                new Vector3(0.12f, 0.12f, (float)HexCoord.HexMeters), RailSteel(), host);
            bar.transform.rotation = rot;
        }
        for (var i = -2; i <= 2; i++)
        {
            var tie = b.SpawnPrim(PrimitiveType.Cube, trackCenter + dir * (i * 3.6f) + Vector3.up * 0.05f,
                new Vector3(1.4f, 0.08f, 0.35f), Tie(), host);
            tie.transform.rotation = rot;
        }
    }

    private const float HalfSpan = 10f;      // hex center-to-edge along a neighbor direction
    private const float RoadWidth = 7.5f;
    private const float ArterialRoadWidth = 14f;   // a real 3-4 lane arterial (creator direction, 2026-07), not a residential street

    // Junction hexes (3+ connectors) render as a roundabout instead of a
    // small pad with crosswalk stripes (creator direction, 2026-07:
    // "Replace the Y cross roads with Cross or T configurations or for
    // European styling proper Roundabouts") -- deliberately the
    // roundabout branch, not a forced Cross/T: this hex grid's
    // "vertical" street direction is a genuine diagonal (see
    // TryStraightenCardinal's doc comment), not exactly perpendicular to
    // an east/west arterial, so any attempt to force true 90 degree
    // symmetry onto that bearing would either kink (a Y) or require
    // corrupting the straight-through alignment fixed earlier. A circular
    // hub has no such constraint -- arms can meet it at any angle and
    // still read as a clean, intentional junction.
    private const float RoundaboutRadius = 9.5f;
    private const float RoundaboutIslandRadius = 3.5f;

    private static void DressHex(RuntimeCityBuilder b, HexCoord hex, Vector3 center,
        List<(Vector3 dir, float angle, bool arterial)> connectors, bool isArterialHex, Transform host)
    {
        if (connectors.Count >= 3)
        {
            // roundabout: apron, asphalt ring, raised curb + grass island
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.12f,
                new Vector3(RoundaboutRadius + 1.6f, 0.12f, RoundaboutRadius + 1.6f), Sidewalk(), host);
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.24f,
                new Vector3(RoundaboutRadius, 0.1f, RoundaboutRadius), Asphalt(), host);
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.3f,
                new Vector3(RoundaboutIslandRadius + 0.3f, 0.06f, RoundaboutIslandRadius + 0.3f), RoundaboutCurb(), host);
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.55f,
                new Vector3(RoundaboutIslandRadius, 0.5f, RoundaboutIslandRadius), RoundaboutGrass(), host);
        }
        else
        {
            // concrete apron under everything, then the asphalt pad --
            // the apron rim reads as the surrounding sidewalk/curb ring.
            // Sized off the SAME formula the connector strips below use
            // (roadHalf + margin), so an arterial hex's pad/apron
            // widens right along with its strips instead of pinching.
            var padWidth = isArterialHex ? ArterialRoadWidth : RoadWidth;
            var padRadius = padWidth * 0.5f + 1.45f;
            var apronRadius = (padWidth + 3.4f) * 0.5f + 1.35f;
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.12f,
                new Vector3(apronRadius, 0.12f, apronRadius), Sidewalk(), host);
            b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.24f,
                new Vector3(padRadius, 0.1f, padRadius), Asphalt(), host);
        }

        foreach (var (dir, angle, isArterialConnector) in connectors)
        {
            var rot = Quaternion.Euler(0f, angle, 0f);
            var mid = center + dir * (HalfSpan * 0.5f);
            var roadWidth = isArterialConnector ? ArterialRoadWidth : RoadWidth;

            // sidewalk under-slab first (wider), asphalt strip on top
            var walk = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.12f,
                new Vector3(roadWidth + 3.4f, 0.24f, HalfSpan + 1.2f), Sidewalk(), host);
            walk.transform.rotation = rot;
            var strip = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.24f,
                new Vector3(roadWidth, 0.2f, HalfSpan + 0.8f), Asphalt(), host);
            strip.transform.rotation = rot;

            if (isArterialConnector)
            {
                // double yellow no-passing line down the true centre,
                // plus a dashed white lane divider on each side -- the
                // "3-4 lane" arterial read, not just a wider single lane
                var perp = new Vector3(dir.z, 0f, -dir.x);
                foreach (var yOff in new[] { 0.3f, -0.3f })
                    for (var d = 0; d < 3; d++)
                    {
                        var dash = b.SpawnPrim(PrimitiveType.Cube,
                            center + dir * (2.2f + d * 2.9f) + perp * yOff + Vector3.up * 0.36f,
                            new Vector3(0.28f, 0.05f, 1.5f), LanePaint(), host);
                        dash.transform.rotation = rot;
                    }
                foreach (var wOff in new[] { 3.4f, -3.4f })
                    for (var d = 0; d < 3; d++)
                    {
                        var dash = b.SpawnPrim(PrimitiveType.Cube,
                            center + dir * (1.6f + d * 3.1f) + perp * wOff + Vector3.up * 0.36f,
                            new Vector3(0.3f, 0.05f, 1.2f), CrossPaint(), host);
                        dash.transform.rotation = rot;
                    }
            }
            else
            {
                // ordinary single yellow center dashes
                for (var d = 0; d < 3; d++)
                {
                    var dash = b.SpawnPrim(PrimitiveType.Cube,
                        center + dir * (2.2f + d * 2.9f) + Vector3.up * 0.36f,
                        new Vector3(0.35f, 0.05f, 1.5f), LanePaint(), host);
                    dash.transform.rotation = rot;
                }
            }
        }

        // street furniture: quiet streets only (straights/corners/dead
        // ends), never the middle of a roundabout
        if (connectors.Count > 2 || connectors.Count == 0) return;
        var axis = connectors[0].dir;
        var side = new Vector3(axis.z, 0f, -axis.x);
        var h = Hash(hex, 5);

        // curb/sidewalk offsets derived from the SAME road width this
        // hex actually rendered at, so furniture sits at the curb
        // whether this is a residential street or the wide arterial
        // (previously fixed constants tuned only for the 7.5 m street --
        // on a 14 m arterial a parked car would have sat mid-lane)
        var hexRoadWidth = isArterialHex ? ArterialRoadWidth : RoadWidth;
        var parkOffset = hexRoadWidth / 3f;
        var curbLineOffset = (hexRoadWidth + 3.4f) * 0.5f + 0.75f;

        // parked car on true straights, hugging the curb lane
        if (connectors.Count == 2 && h % 3 == 0)
            SpawnCar(b, hex, center + side * (h % 2 == 0 ? parkOffset : -parkOffset), connectors[0].angle, host);

        // pole or hydrant or trash can on the sidewalk line
        var sideSign = (h >> 3) % 2 == 0 ? 1f : -1f;
        var propSpot = center + side * (sideSign * curbLineOffset) + axis * (((h >> 5) % 7) - 3f);
        switch ((h >> 8) % 6)
        {
            case 0:   // streetlight: pole, arm reaching back over the road, warm bulb
            {
                var holder = KnockHolder(propSpot, host);
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.4f,
                    new Vector3(0.16f, 2.4f, 0.16f), PoleMetal(), holder);
                var arm = b.SpawnPrim(PrimitiveType.Cube,
                    propSpot + Vector3.up * 4.7f - side * (sideSign * 1.2f),
                    new Vector3(0.14f, 0.14f, 2.4f), PoleMetal(), holder);
                arm.transform.rotation = Quaternion.LookRotation(-side * sideSign, Vector3.up);
                b.SpawnPrim(PrimitiveType.Sphere, propSpot + Vector3.up * 4.55f - side * (sideSign * 2.2f),
                    new Vector3(0.5f, 0.35f, 0.5f), Bulb(), holder);
                MakeKnockable(b, holder.gameObject, 1.6f);
                break;
            }
            case 1:   // telephone pole with crossarm
            {
                var holder = KnockHolder(propSpot, host);
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.9f,
                    new Vector3(0.18f, 2.9f, 0.18f), PoleWood(), holder);
                var arm = b.SpawnPrim(PrimitiveType.Cube, propSpot + Vector3.up * 5.2f,
                    new Vector3(2.6f, 0.15f, 0.15f), PoleWood(), holder);
                arm.transform.rotation = Quaternion.Euler(0f, connectors[0].angle, 0f);
                MakeKnockable(b, holder.gameObject, 1.6f);
                break;
            }
            case 2:   // fire hydrant -- shears off with a water jet
            {
                var holder = KnockHolder(propSpot, host);
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 0.4f,
                    new Vector3(0.35f, 0.4f, 0.35f), HydrantRed(), holder);
                b.SpawnPrim(PrimitiveType.Sphere, propSpot + Vector3.up * 0.85f,
                    new Vector3(0.4f, 0.25f, 0.4f), HydrantRed(), holder);
                MakeKnockable(b, holder.gameObject, 1.3f).SpawnsWaterJet = true;
                break;
            }
            case 3:   // trash can
            {
                var can = b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 0.55f,
                    new Vector3(0.45f, 0.55f, 0.45f), CanGray(), host);
                MakeKnockable(b, can, 1.2f);
                break;
            }
            case 4:   // roadside billboard on double stilts, period ad art
            {
                var boardCenter = propSpot + Vector3.up * 5.2f;
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.6f - side * (sideSign * 0.9f),
                    new Vector3(0.25f, 2.6f, 0.25f), PoleMetal(), host);
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.6f + side * (sideSign * 0.9f),
                    new Vector3(0.25f, 2.6f, 0.25f), PoleMetal(), host);
                var board = b.SpawnPrim(PrimitiveType.Cube, boardCenter, new Vector3(6.5f, 3.2f, 0.3f), CrossPaint(), host);
                board.transform.rotation = Quaternion.Euler(0f, connectors[0].angle, 0f);
                var stripe = b.SpawnPrim(PrimitiveType.Cube, boardCenter + Vector3.up * 0.2f,
                    new Vector3(5.6f, 1f, 0.35f), h % 2 == 0 ? AdRed() : LanePaint(), host);
                stripe.transform.rotation = board.transform.rotation;
                break;
            }
            // case 5: nothing -- empty sidewalk is a look too
        }
    }

    /// <summary>A colliderless origin transform a multi-piece prop's
    /// primitives parent under, so KnockableProp can tip the whole
    /// assembly as one rigid unit instead of its parts falling apart
    /// independently.</summary>
    private static Transform KnockHolder(Vector3 at, Transform host)
    {
        var holder = new GameObject("Knockable").transform;
        holder.SetParent(host, false);
        holder.position = at;
        return holder;
    }

    private static KnockableProp MakeKnockable(RuntimeCityBuilder b, GameObject go, float radius)
    {
        var prop = go.AddComponent<KnockableProp>();
        prop.Init(b, radius);
        return prop;
    }

    /// <summary>A 1950s parked car: pastel slab body, cabin, chrome
    /// bumpers, and the little rear tail fins that date it precisely.</summary>
    private static void SpawnCar(RuntimeCityBuilder b, HexCoord hex, Vector3 at, float angle, Transform host)
    {
        var h = Hash(hex, 11);
        var body = M(CarPastels[h % CarPastels.Length].r, CarPastels[h % CarPastels.Length].g,
            CarPastels[h % CarPastels.Length].b);
        var rot = Quaternion.Euler(0f, angle, 0f);
        var holder = KnockHolder(at, host);

        var chassis = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 0.75f,
            new Vector3(2.2f, 0.8f, 5.2f), body, holder);
        chassis.transform.rotation = rot;
        var cabin = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 1.5f + rot * new Vector3(0f, 0f, -0.3f),
            new Vector3(1.9f, 0.7f, 2.4f), body, holder);
        cabin.transform.rotation = rot;
        foreach (var end in new[] { 2.7f, -2.7f })
        {
            var bumper = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, end),
                new Vector3(2.3f, 0.25f, 0.3f), ChromeTrim(), holder);
            bumper.transform.rotation = rot;
        }
        foreach (var fx in new[] { 0.95f, -0.95f })
        {
            var fin = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 1.35f + rot * new Vector3(fx, 0f, -2.3f),
                new Vector3(0.15f, 0.5f, 1.1f), body, holder);
            fin.transform.rotation = rot;
        }
        MakeKnockable(b, holder.gameObject, 2.6f);
    }
}
