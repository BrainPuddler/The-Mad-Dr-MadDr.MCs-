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
            mat.SetColor("_EmissionColor", new Color(r, g, b) * emissive);
        }
        Cache[key] = mat;
        return mat;
    }

    private static Material Asphalt() { return M(0.17f, 0.17f, 0.18f); }
    private static Material Sidewalk() { return M(0.58f, 0.56f, 0.52f); }
    private static Material LanePaint() { return M(0.85f, 0.7f, 0.2f); }
    private static Material CrossPaint() { return M(0.85f, 0.84f, 0.8f); }
    private static Material PoleWood() { return M(0.35f, 0.26f, 0.18f); }
    private static Material PoleMetal() { return M(0.45f, 0.48f, 0.5f); }
    private static Material Bulb() { return M(1f, 0.9f, 0.6f, 1.4f); }
    private static Material HydrantRed() { return M(0.75f, 0.15f, 0.12f); }
    private static Material CanGray() { return M(0.4f, 0.42f, 0.44f); }
    private static Material ChromeTrim() { return M(0.8f, 0.82f, 0.85f); }

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

    public static void Build(RuntimeCityBuilder builder, CityModel city, Transform parent)
    {
        var host = new GameObject("Roads").transform;
        host.SetParent(parent, false);

        // connectivity truth: roads join other roads AND bridge decks
        var network = new HashSet<HexCoord>(city.Roads);
        foreach (var bridge in city.Bridges)
            foreach (var hex in bridge.Footprint) network.Add(hex);

        foreach (var hex in city.Roads)
        {
            var center = builder.WorldOf(hex);
            var connectors = new List<(Vector3 dir, float angle)>();
            foreach (var n in hex.Neighbors())
            {
                if (!network.Contains(n)) continue;
                var to = builder.WorldOf(n) - center;
                to.y = 0f;
                var dir = to.normalized;
                connectors.Add((dir, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg));
            }

            DressHex(builder, hex, center, connectors, host);
        }
    }

    private const float HalfSpan = 10f;      // hex center-to-edge along a neighbor direction
    private const float RoadWidth = 7.5f;

    private static void DressHex(RuntimeCityBuilder b, HexCoord hex, Vector3 center,
        List<(Vector3 dir, float angle)> connectors, Transform host)
    {
        // concrete apron under everything, then the asphalt pad -- the
        // apron rim reads as the surrounding sidewalk/curb ring
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.12f,
            new Vector3(6.8f, 0.12f, 6.8f), Sidewalk(), host);
        b.SpawnPrim(PrimitiveType.Cylinder, center + Vector3.up * 0.24f,
            new Vector3(5.2f, 0.1f, 5.2f), Asphalt(), host);

        foreach (var (dir, angle) in connectors)
        {
            var rot = Quaternion.Euler(0f, angle, 0f);
            var mid = center + dir * (HalfSpan * 0.5f);

            // sidewalk under-slab first (wider), asphalt strip on top
            var walk = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.12f,
                new Vector3(RoadWidth + 3.4f, 0.24f, HalfSpan + 1.2f), Sidewalk(), host);
            walk.transform.rotation = rot;
            var strip = b.SpawnPrim(PrimitiveType.Cube, mid + Vector3.up * 0.24f,
                new Vector3(RoadWidth, 0.2f, HalfSpan + 0.8f), Asphalt(), host);
            strip.transform.rotation = rot;

            // yellow center dashes
            for (var d = 0; d < 3; d++)
            {
                var dash = b.SpawnPrim(PrimitiveType.Cube,
                    center + dir * (2.2f + d * 2.9f) + Vector3.up * 0.36f,
                    new Vector3(0.35f, 0.05f, 1.5f), LanePaint(), host);
                dash.transform.rotation = rot;
            }

            // crosswalk stripes where three or more streets meet
            if (connectors.Count >= 3)
            {
                var perp = new Vector3(dir.z, 0f, -dir.x);
                for (var k = -2; k <= 2; k++)
                {
                    var stripe = b.SpawnPrim(PrimitiveType.Cube,
                        center + dir * 7.2f + perp * (k * 1.5f) + Vector3.up * 0.36f,
                        new Vector3(0.75f, 0.05f, 1.7f), CrossPaint(), host);
                    stripe.transform.rotation = rot;
                }
            }
        }

        // street furniture: quiet streets only (straights/corners/dead
        // ends), never the middle of an intersection
        if (connectors.Count > 2 || connectors.Count == 0) return;
        var axis = connectors[0].dir;
        var side = new Vector3(axis.z, 0f, -axis.x);
        var h = Hash(hex, 5);

        // parked car on true straights, hugging the curb lane (road is
        // 7.5 wide -> curb at 3.75; a 2.2-wide body centered at 2.5
        // sits in the parking lane without straddling the sidewalk)
        if (connectors.Count == 2 && h % 3 == 0)
            SpawnCar(b, hex, center + side * (h % 2 == 0 ? 2.5f : -2.5f), connectors[0].angle, host);

        // pole or hydrant or trash can on the sidewalk line
        var sideSign = (h >> 3) % 2 == 0 ? 1f : -1f;
        var propSpot = center + side * (sideSign * 6.2f) + axis * (((h >> 5) % 7) - 3f);
        switch ((h >> 8) % 5)
        {
            case 0:   // streetlight: pole, arm reaching back over the road, warm bulb
            {
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.4f,
                    new Vector3(0.16f, 2.4f, 0.16f), PoleMetal(), host);
                var arm = b.SpawnPrim(PrimitiveType.Cube,
                    propSpot + Vector3.up * 4.7f - side * (sideSign * 1.2f),
                    new Vector3(0.14f, 0.14f, 2.4f), PoleMetal(), host);
                arm.transform.rotation = Quaternion.LookRotation(-side * sideSign, Vector3.up);
                b.SpawnPrim(PrimitiveType.Sphere, propSpot + Vector3.up * 4.55f - side * (sideSign * 2.2f),
                    new Vector3(0.5f, 0.35f, 0.5f), Bulb(), host);
                break;
            }
            case 1:   // telephone pole with crossarm
            {
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 2.9f,
                    new Vector3(0.18f, 2.9f, 0.18f), PoleWood(), host);
                var arm = b.SpawnPrim(PrimitiveType.Cube, propSpot + Vector3.up * 5.2f,
                    new Vector3(2.6f, 0.15f, 0.15f), PoleWood(), host);
                arm.transform.rotation = Quaternion.Euler(0f, connectors[0].angle, 0f);
                break;
            }
            case 2:   // fire hydrant
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 0.4f,
                    new Vector3(0.35f, 0.4f, 0.35f), HydrantRed(), host);
                b.SpawnPrim(PrimitiveType.Sphere, propSpot + Vector3.up * 0.85f,
                    new Vector3(0.4f, 0.25f, 0.4f), HydrantRed(), host);
                break;
            case 3:   // trash can
                b.SpawnPrim(PrimitiveType.Cylinder, propSpot + Vector3.up * 0.55f,
                    new Vector3(0.45f, 0.55f, 0.45f), CanGray(), host);
                break;
            // case 4: nothing -- empty sidewalk is a look too
        }
    }

    /// <summary>A 1950s parked car: pastel slab body, cabin, chrome
    /// bumpers, and the little rear tail fins that date it precisely.</summary>
    private static void SpawnCar(RuntimeCityBuilder b, HexCoord hex, Vector3 at, float angle, Transform host)
    {
        var h = Hash(hex, 11);
        var body = M(CarPastels[h % CarPastels.Length].r, CarPastels[h % CarPastels.Length].g,
            CarPastels[h % CarPastels.Length].b);
        var rot = Quaternion.Euler(0f, angle, 0f);

        var chassis = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 0.75f,
            new Vector3(2.2f, 0.8f, 5.2f), body, host);
        chassis.transform.rotation = rot;
        var cabin = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 1.5f + rot * new Vector3(0f, 0f, -0.3f),
            new Vector3(1.9f, 0.7f, 2.4f), body, host);
        cabin.transform.rotation = rot;
        foreach (var end in new[] { 2.7f, -2.7f })
        {
            var bumper = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, end),
                new Vector3(2.3f, 0.25f, 0.3f), ChromeTrim(), host);
            bumper.transform.rotation = rot;
        }
        foreach (var fx in new[] { 0.95f, -0.95f })
        {
            var fin = b.SpawnPrim(PrimitiveType.Cube, at + Vector3.up * 1.35f + rot * new Vector3(fx, 0f, -2.3f),
                new Vector3(0.15f, 0.5f, 1.1f), body, host);
            fin.transform.rotation = rot;
        }
    }
}
