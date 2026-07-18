using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// The 1950s street network (docs/21 Phase 4). Every road hex gets a
/// center pad plus a strip toward each CARDINAL (N/S/E/W) road neighbor,
/// built from OFFSET-coordinate adjacency, not hex adjacency -- so
/// streets are axis-aligned, parallel, and straight, and a full junction
/// is a clean North-American 4-way cross, not a zig-zag or a diagonal Y
/// (the whole point of the 2026-07 cardinal rewrite; see Build). On top:
/// concrete sidewalks with a curb step, yellow center dashes (double +
/// lane dividers on the wide Main Street arterial), crosswalk stripes at
/// crosses, a fully detailed European roundabout at the major arterial
/// junctions (DrawRoundabout), and deterministic street furniture
/// (streetlights, telephone poles, hydrants, trash cans, pastel
/// tail-finned parked cars).
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
    private static Material Shrub() { return M(0.20f, 0.36f, 0.16f); }
    private static Material IslandStone() { return M(0.58f, 0.58f, 0.6f); }
    private static Material SignBlue() { return M(0.10f, 0.26f, 0.7f); }
    private static Material SignRed() { return M(0.78f, 0.16f, 0.13f); }
    private static Material PostGray() { return M(0.5f, 0.52f, 0.54f); }
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
        var roundabouts = new HashSet<HexCoord>(city.Roundabouts);
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

            // CARDINAL rendering (creator direction, 2026-07: the roads
            // were still zig-zagging / making Y intersections). The whole
            // fix: build a hex's road arms from its OFFSET-coordinate
            // neighbors (col/row +-1 = due E/W/N/S in world space), NOT
            // its hex-adjacency neighbors (which sit at 60 degree
            // diagonals and are exactly why a "vertical" street sawed
            // left-right and junctions fanned into Y's). Streets are now
            // axis-aligned and parallel; a full junction is a clean 4-way
            // cross. See CardinalNeighbors / CardinalAnchor.
            var card = CardinalNeighbors(hex, network);
            var center = CardinalAnchor(builder, hex, card.Vertical);
            var (col, row) = Offset(hex);

            var connectors = new List<(Vector3 dir, bool arterial, float half)>();
            void AddArm(bool present, int dc, int dr, Vector3 dir, float half)
            {
                if (!present) return;
                var nb = HexCoord.FromOffset(col + dc, row + dr);
                var art = arterial.Contains(hex) && arterial.Contains(nb);
                connectors.Add((dir, art, half));
            }
            AddArm(card.E, 1, 0, new Vector3(1f, 0f, 0f), HalfSpanEW);
            AddArm(card.W, -1, 0, new Vector3(-1f, 0f, 0f), HalfSpanEW);
            AddArm(card.N, 0, -1, new Vector3(0f, 0f, -1f), HalfSpanNS);
            AddArm(card.S, 0, 1, new Vector3(0f, 0f, 1f), HalfSpanNS);

            DressHex(builder, hex, center, connectors, arterial.Contains(hex), roundabouts.Contains(hex), host);

            // railyard siding (docs/21 batch 2, item 6): a parallel rail
            // track alongside straight road hexes near a rail_depot
            // landmark, tying the depot into a small industrial district
            if (railyardCenter.HasValue && connectors.Count == 2 && hex.DistanceTo(railyardCenter.Value) <= RailyardRadius)
                DressRailSiding(builder, center, connectors[0].dir, host);
        }
    }

    /// <summary>Odd-r offset (col, row) of a hex -- the world-cardinal
    /// index. Stepping col+-1 is due E/W and row+-1 due N/S in world
    /// space (unlike the hex's own 6 diagonal neighbors), which is what
    /// makes the cardinal rendering straight and axis-aligned.</summary>
    public static (int col, int row) Offset(HexCoord h)
    {
        var row = h.R;
        return (h.Q + (row - (row & 1)) / 2, row);
    }

    /// <summary>Which of the four world-cardinal directions this hex has
    /// a road neighbor in, by OFFSET step. `Vertical` = on a north/south
    /// street, `Count` = arm count (4 = full crossroads).</summary>
    public struct Cardinals
    {
        public bool N, S, E, W;
        public int Count { get { return (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0); } }
        public bool Vertical { get { return N || S; } }
    }

    public static Cardinals CardinalNeighbors(HexCoord hex, HashSet<HexCoord> network)
    {
        var (col, row) = Offset(hex);
        return new Cardinals
        {
            E = network.Contains(HexCoord.FromOffset(col + 1, row)),
            W = network.Contains(HexCoord.FromOffset(col - 1, row)),
            N = network.Contains(HexCoord.FromOffset(col, row - 1)),
            S = network.Contains(HexCoord.FromOffset(col, row + 1)),
        };
    }

    /// <summary>The world anchor a road hex renders at: its hex center,
    /// nudged onto its vertical street's straight centerline when it's on
    /// one, so every hex sharing an offset column lands on ONE x. On a
    /// pointy-top odd-r grid a fixed offset column alternates x by
    /// HexMeters/2 every row (the sawtooth); the +-HexMeters/4 nudge is
    /// the exact midpoint, cancelling it. Shared with BridgeDresser so a
    /// bridge lands on the same centerline as its approach road.</summary>
    public static Vector3 CardinalAnchor(RuntimeCityBuilder b, HexCoord hex, bool vertical)
    {
        var c = b.WorldOf(hex);
        if (vertical) c.x += ((hex.R & 1) == 0 ? 1f : -1f) * (float)HexCoord.HexMeters / 4f;
        return c;
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

    private const float HalfSpanEW = 10f;        // E/W: offset cols are 20 m apart, edge at 10 m
    private static readonly float HalfSpanNS = (float)(HexCoord.HexMeters * 1.5 / 1.7320508 / 2.0); // N/S: rows ~17.32 m apart, edge at ~8.66 m
    private const float RoadWidth = 7.5f;
    private const float ArterialRoadWidth = 14f;   // a real 3-4 lane arterial (creator direction, 2026-07), not a residential street

    private static void DressHex(RuntimeCityBuilder b, HexCoord hex, Vector3 center,
        List<(Vector3 dir, bool arterial, float half)> connectors, bool isArterialHex, bool isRoundabout, Transform host)
    {
        // 1. APPROACH ARMS -- straight cardinal strips (drawn first so
        //    the center treatment overlays their inner ends)
        foreach (var (dir, isArterialConnector, half) in connectors)
        {
            var angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, angle, 0f);
            var mid = center + dir * (half * 0.5f);
            var roadWidth = isArterialConnector ? ArterialRoadWidth : RoadWidth;

            // sidewalk under-slab first (wider), asphalt strip on top
            var walk = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.12f,
                new Vector3(roadWidth + 3.4f, 0.24f, half + 1.2f), Sidewalk(), host);
            walk.transform.rotation = rot;
            var strip = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.24f,
                new Vector3(roadWidth, 0.2f, half + 0.8f), Asphalt(), host);
            strip.transform.rotation = rot;

            // lane markings -- skipped for a roundabout arm (its circular
            // ring markings take over, and a straight centerline running
            // into the island reads wrong)
            if (isRoundabout) continue;

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

        // 2. CENTER TREATMENT
        if (isRoundabout)
        {
            DrawRoundabout(b, hex, center, connectors, host);
            return;   // no street furniture in the middle of a roundabout
        }
        if (connectors.Count >= 3)
        {
            DrawCrossPad(b, center, connectors, host);   // clean North-American 4-way / T
            return;
        }
        // straights / corners / dead ends: a small pad, then furniture
        var padWidth = isArterialHex ? ArterialRoadWidth : RoadWidth;
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.12f,
            new Vector3((padWidth + 3.4f) * 0.5f + 1.35f, 0.12f, (padWidth + 3.4f) * 0.5f + 1.35f), Sidewalk(), host);
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.25f,
            new Vector3(padWidth * 0.5f + 1.45f, 0.1f, padWidth * 0.5f + 1.45f), Asphalt(), host);

        // 3. street furniture: quiet streets only (straights/corners/dead
        // ends), never a junction
        if (connectors.Count == 0) return;
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
        var axisAngle = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;

        // parked car on true straights, hugging the curb lane
        if (connectors.Count == 2 && h % 3 == 0)
            SpawnCar(b, hex, center + side * (h % 2 == 0 ? parkOffset : -parkOffset), axisAngle, host);

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
                arm.transform.rotation = Quaternion.Euler(0f, axisAngle, 0f);
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
                board.transform.rotation = Quaternion.Euler(0f, axisAngle, 0f);
                var stripe = b.SpawnPrim(PrimitiveType.Cube, boardCenter + Vector3.up * 0.2f,
                    new Vector3(5.6f, 1f, 0.35f), h % 2 == 0 ? AdRed() : LanePaint(), host);
                stripe.transform.rotation = board.transform.rotation;
                break;
            }
            // case 5: nothing -- empty sidewalk is a look too
        }
    }

    /// <summary>A clean North-American 4-way cross / T junction (creator
    /// direction, 2026-07: "the 4 way cross in north american roads").
    /// The two perpendicular arm strips already form the +; this just
    /// fills the middle with asphalt (no gap where they meet) and lays
    /// crosswalk zebra stripes set back at each arm mouth.</summary>
    private static void DrawCrossPad(RuntimeCityBuilder b, Vector3 center,
        List<(Vector3 dir, bool arterial, float half)> connectors, Transform host)
    {
        var widest = RoadWidth;
        foreach (var c in connectors) if (c.arterial) widest = ArterialRoadWidth;
        var padR = widest * 0.5f + 1.6f;

        // square-ish infill: an asphalt cylinder covers the junction core;
        // a slightly larger sidewalk apron under it fills the outer corners
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.12f,
            new Vector3(padR + 1.6f, 0.12f, padR + 1.6f), Sidewalk(), host);
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.25f,
            new Vector3(padR, 0.1f, padR), Asphalt(), host);

        // crosswalk stripes, set back at each arm's mouth
        foreach (var (dir, isArt, _) in connectors)
        {
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var perp = new Vector3(dir.z, 0f, -dir.x);
            var back = padR + 1.0f;
            var lanes = isArt ? 4 : 2;
            for (var k = -lanes; k <= lanes; k++)
            {
                var stripe = b.SpawnPrim(PrimitiveType.Cube,
                    center + dir * back + perp * (k * 0.9f) + Vector3.up * 0.36f,
                    new Vector3(0.6f, 0.05f, 1.7f), CrossPaint(), host);
                stripe.transform.rotation = rot;
            }
        }
    }

    // Roundabout geometry (creator direction, 2026-07: a proper European
    // roundabout -- "THEY ARE NOT GREEN CYLINDER DOTS"). A circular
    // circulating roadway around a landscaped central island, raised
    // curb, dashed circular lane markings, flared entries with give-way
    // triangles, set-back pedestrian crossings, evenly spaced streetlamps,
    // and blue circular / red triangular European signs at each entry.
    private const float RndAsphalt = 10.5f;   // outer edge of the circulating lane
    private const float RndCurb = 4.6f;       // raised curb ring radius
    private const float RndIsland = 4.2f;     // grass island radius
    private const float RndLaneMark = 7.4f;   // radius the dashed lane line follows

    private static void DrawRoundabout(RuntimeCityBuilder b, HexCoord hex, Vector3 c,
        List<(Vector3 dir, bool arterial, float half)> connectors, Transform host)
    {
        // perimeter sidewalk apron (pedestrians + cyclists share it)
        b.SpawnPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.12f,
            new Vector3(RndAsphalt + 2.2f, 0.12f, RndAsphalt + 2.2f), Sidewalk(), host);
        // the circulating asphalt roadway
        b.SpawnPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.26f,
            new Vector3(RndAsphalt, 0.1f, RndAsphalt), Asphalt(), host);
        // raised curb ring around the island
        b.SpawnPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.36f,
            new Vector3(RndCurb, 0.18f, RndCurb), RoundaboutCurb(), host);
        // landscaped grass island, domed slightly
        b.SpawnPrim(PrimitiveType.Cylinder, c + Vector3.up * 0.5f,
            new Vector3(RndIsland, 0.5f, RndIsland), RoundaboutGrass(), host);

        // shrubs around the island + a small central sculpture (obelisk)
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6f * 2f * Mathf.PI;
            var p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (RndIsland * 0.6f) + Vector3.up * 0.9f;
            b.SpawnPrim(PrimitiveType.Sphere, p, new Vector3(1.1f, 0.9f, 1.1f), Shrub(), host);
        }
        b.SpawnPrim(PrimitiveType.Cube, c + Vector3.up * 2.1f, new Vector3(0.7f, 3.2f, 0.7f), IslandStone(), host);
        b.SpawnPrim(PrimitiveType.Sphere, c + Vector3.up * 3.9f, new Vector3(1.0f, 1.0f, 1.0f), IslandStone(), host);

        // dashed white lane markings following the circle
        const int dashes = 20;
        for (var i = 0; i < dashes; i++)
        {
            var a = i / (float)dashes * 2f * Mathf.PI;
            var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            var p = c + radial * RndLaneMark + Vector3.up * 0.36f;
            var dash = b.SpawnPrim(PrimitiveType.Cube, p, new Vector3(0.35f, 0.05f, 1.1f), CrossPaint(), host);
            dash.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        // streetlamps evenly spaced around the outer edge
        for (var i = 0; i < 5; i++)
        {
            var a = (i + 0.5f) / 5f * 2f * Mathf.PI;
            var p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (RndAsphalt + 1.2f);
            b.SpawnPrim(PrimitiveType.Cylinder, p + Vector3.up * 2.4f, new Vector3(0.16f, 2.4f, 0.16f), PoleMetal(), host);
            b.SpawnPrim(PrimitiveType.Sphere, p + Vector3.up * 4.7f, new Vector3(0.45f, 0.35f, 0.45f), Bulb(), host);
        }

        // per-entry treatment: flared apron, give-way triangles, set-back
        // pedestrian crossing, and European signs (blue circular
        // roundabout sign + red triangular yield sign)
        foreach (var (dir, isArt, _) in connectors)
        {
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var perp = new Vector3(dir.z, 0f, -dir.x);

            // flared entry: a wider short asphalt apron where the arm
            // meets the circle (entry lanes flare before joining)
            var flareW = (isArt ? ArterialRoadWidth : RoadWidth) + 4f;
            var flare = b.SpawnPrim(PrimitiveType.Cube, c + dir * (RndAsphalt + 1.0f) + Vector3.up * 0.25f,
                new Vector3(flareW, 0.1f, 4.5f), Asphalt(), host);
            flare.transform.rotation = rot;

            // give-way "shark teeth": a row of small white triangles at
            // the yield line (approximated as narrow tapered boxes)
            for (var k = -2; k <= 2; k++)
            {
                var tooth = b.SpawnPrim(PrimitiveType.Cube,
                    c + dir * (RndAsphalt - 0.3f) + perp * (k * 0.9f) + Vector3.up * 0.36f,
                    new Vector3(0.5f, 0.05f, 0.9f), CrossPaint(), host);
                tooth.transform.rotation = rot;
            }

            // pedestrian crossing set back several metres from the circle
            var cross = c + dir * (RndAsphalt + 3.5f);
            for (var k = -2; k <= 2; k++)
            {
                var stripe = b.SpawnPrim(PrimitiveType.Cube,
                    cross + perp * (k * 0.9f) + Vector3.up * 0.36f,
                    new Vector3(0.55f, 0.05f, 2.0f), CrossPaint(), host);
                stripe.transform.rotation = rot;
            }

            // European signage on a post just outside the crossing
            var signPost = c + dir * (RndAsphalt + 4.2f) + perp * (isArt ? 8f : 5.5f);
            b.SpawnPrim(PrimitiveType.Cylinder, signPost + Vector3.up * 1.3f, new Vector3(0.12f, 1.3f, 0.12f), PostGray(), host);
            // blue circular roundabout sign
            var blue = b.SpawnPrim(PrimitiveType.Cylinder, signPost + Vector3.up * 2.7f, new Vector3(0.75f, 0.06f, 0.75f), SignBlue(), host);
            blue.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // small white circulating arrow hint on it
            b.SpawnPrim(PrimitiveType.Cube, signPost + Vector3.up * 2.7f + dir * 0.08f, new Vector3(0.5f, 0.09f, 0.16f), CrossPaint(), host);
            // red triangular yield sign just below (point-down diamond as a stand-in)
            var yield = b.SpawnPrim(PrimitiveType.Cube, signPost + Vector3.up * 1.9f, new Vector3(0.5f, 0.5f, 0.05f), SignRed(), host);
            yield.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 45f);
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
