using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// 1950s miniature-set dressing for the generated buildings (docs/21
/// Phase 3). The collider cube stays the massing core and the click/
/// damage handle; this adds a colliderless DRESSING HOLDER per building
/// hex, registered into the same cubes list RuntimeCityBuilder's damage
/// pipeline walks -- so water towers crush into the rubble pancake and
/// signage tints with the cracked walls, exactly like the massing.
///
/// Style targets (creator brief): mid-century Americana read from RTS
/// camera height -- suburban gables and gas stations, brick walk-ups
/// with fire escapes, stepped deco office towers, marquee'd civic
/// landmarks -- with a rooftop kit (water towers, antenna masts, vents,
/// billboards) doing most of the silhouette work. Everything is
/// primitives + shared cached materials (SRP-batcher friendly, no asset
/// pipeline -- the project's established prefab-free workflow), and
/// every choice hashes off the building's first footprint hex, so the
/// same seed always dresses the same city.
/// </summary>
public static class BuildingDresser
{
    // ---- 1950s palette (shared cached materials) ------------------------------
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

    private static Material Brick() { return M(0.55f, 0.27f, 0.2f); }
    private static Material Cream() { return M(0.87f, 0.82f, 0.68f); }
    private static Material Seafoam() { return M(0.62f, 0.78f, 0.68f); }
    private static Material Mustard() { return M(0.82f, 0.66f, 0.25f); }
    private static Material Concrete() { return M(0.62f, 0.6f, 0.55f); }
    private static Material Chrome() { return M(0.78f, 0.8f, 0.82f); }
    private static Material WindowBand() { return M(0.16f, 0.2f, 0.28f); }
    private static Material RoofTar() { return M(0.24f, 0.23f, 0.21f); }
    private static Material RustRed() { return M(0.5f, 0.24f, 0.16f); }
    private static Material NeonRed() { return M(0.95f, 0.25f, 0.3f, 1.6f); }
    private static Material NeonTeal() { return M(0.3f, 0.9f, 0.85f, 1.6f); }
    private static Material SignWhite() { return M(0.92f, 0.9f, 0.82f, 0.6f); }
    private static Material GardenGreen() { return M(0.3f, 0.42f, 0.24f); }

    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    /// <summary>Dress one building. Adds one holder GameObject per
    /// footprint hex into `cubes` (the damage pipeline's list). The
    /// FIRST hex is the "primary": it carries the rooftop kit and
    /// signage; secondary hexes of multi-hex buildings get facade work
    /// only, so a 3-hex office doesn't sprout 3 water towers. `industrial`
    /// (docs/21 batch 2, item 6) re-skins Small/Medium tiers as warehouse
    /// stock inside a rail_depot landmark's radius -- Large/Landmark keep
    /// their usual look, since a factory reads fine as a stepped office
    /// shell and the depot itself already carries the archetype set piece.
    /// `suburb` (docs/21 batch 2, item 10 -- previously only tinted the
    /// massing cube; this extends the same bias one level deeper into the
    /// dressing's own wall/roof material choice) shifts Small/Medium's
    /// palette warmer toward the outskirts and cooler downtown, WITHOUT
    /// going monotone -- still hash-varied, just reweighted; skipped for
    /// `industrial` (a warehouse stays utilitarian regardless of district)
    /// and for Large/Landmark (they cluster near downtown by construction
    /// anyway, per the massing-tint precedent this mirrors).</summary>
    public static void Dress(RuntimeCityBuilder builder, Building building, float height,
        List<GameObject> cubes, Transform parent, bool industrial = false, bool suburb = false)
    {
        var footprint = building.Footprint;
        for (var i = 0; i < footprint.Count; i++)
        {
            var hex = footprint[i];
            var holder = new GameObject("Dressing_" + hex.Q + "_" + hex.R);
            holder.transform.SetParent(parent, false);
            holder.transform.position = builder.WorldOf(hex);
            cubes.Add(holder);

            var h = Hash(hex, 1);
            var primary = i == 0;
            switch (building.Tier)
            {
                case BuildingTier.Landmark:
                    DressLandmark(builder, building.Archetype, holder.transform, height, h, primary);
                    break;
                case BuildingTier.Large:
                    DressOffice(builder, holder.transform, height, h, primary);
                    break;
                case BuildingTier.Medium:
                    if (industrial) DressIndustrial(builder, holder.transform, height, h, primary);
                    else DressApartment(builder, holder.transform, height, h, primary, suburb);
                    break;
                default:
                    if (industrial) DressIndustrial(builder, holder.transform, height, h, primary);
                    else DressSmall(builder, holder.transform, height, h, primary, suburb);
                    break;
            }
        }
    }

    // massing cubes are hexSize*0.9 = 18m across; faces sit at +-9m
    private const float Half = 9f;

    // ---- small tier: suburbia / roadside America ------------------------------

    private static void DressSmall(RuntimeCityBuilder b, Transform t, float height, int h, bool primary, bool suburb = false)
    {
        var basePos = t.position;
        // suburb: house-heavy (60/20/20 house/gas/diner); downtown:
        // commercial-heavy (20/40/40) -- still hash-varied, just reweighted
        var pick = suburb
            ? (h % 5 < 3 ? 0 : h % 5 == 3 ? 1 : 2)
            : (h % 5 < 1 ? 0 : h % 5 < 3 ? 1 : 2);
        switch (pick)
        {
            case 0:   // suburban house: pitched gable roof + chimney
            {
                var roofMat = suburb
                    ? ((h / 3) % 3 != 0 ? RustRed() : M(0.35f, 0.42f, 0.5f))   // warm roof more often
                    : ((h / 3) % 3 == 0 ? RustRed() : M(0.35f, 0.42f, 0.5f));  // cool slate more often
                // a 45-degree diamond prism sunk into the block: only the
                // top V shows, reading as a pitched gable roof
                var gable = b.SpawnPrim(PrimitiveType.Cube,
                    basePos + Vector3.up * (height + 0.6f), new Vector3(11f, 11f, 17.5f), roofMat, t);
                gable.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(4f, height + 4.6f, 3f),
                    new Vector3(1.4f, 2.6f, 1.4f), Brick(), t);   // chimney, poking through the roof plane
                break;
            }
            case 1:   // gas station: forecourt canopy on poles + pylon sign
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.4f, 11f),
                    new Vector3(14f, 0.7f, 8f), Chrome(), t);   // canopy
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-5f, 2.7f, 13f),
                    new Vector3(0.5f, 2.7f, 0.5f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(5f, 2.7f, 13f),
                    new Vector3(0.5f, 2.7f, 0.5f), Concrete(), t);
                if (primary)
                {
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-8f, 4.5f, 8f),
                        new Vector3(0.4f, 4.5f, 0.4f), Concrete(), t);   // pylon
                    b.SpawnPrim(PrimitiveType.Sphere, basePos + new Vector3(-8f, 9.6f, 8f),
                        new Vector3(2.6f, 2.6f, 0.9f), NeonRed(), t);    // round sign
                }
                break;
            }
            default:  // diner: chrome band + rooftop sign
            {
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height - 0.8f),
                    new Vector3(18.6f, 0.9f, 18.6f), Chrome(), t);   // wraparound chrome trim
                if (primary)
                {
                    b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 2.2f, 0f),
                        new Vector3(9f, 3f, 0.6f), SignWhite(), t);      // roof sign board
                    b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 2.2f, 0.5f),
                        new Vector3(7.4f, 1.2f, 0.4f), NeonTeal(), t);   // neon script strip
                }
                break;
            }
        }
    }

    // ---- railyard/industrial re-skin (docs/21 batch 2, item 6) -----------------

    private static Material Corrugated() { return M(0.46f, 0.44f, 0.42f); }

    private static void DressIndustrial(RuntimeCityBuilder b, Transform t, float height, int h, bool primary)
    {
        var basePos = t.position;
        // flat corrugated roof band + a loading dock canopy out front --
        // reads as a warehouse from RTS height, not a walk-up
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.3f),
            new Vector3(19f, 0.5f, 19f), Corrugated(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 2.4f, Half * 1.1f),
            new Vector3(9f, 3.2f, 2.2f), RustRed(), t);
        b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-6f, 1.6f, 9.6f),
            new Vector3(0.35f, 1.6f, 0.35f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(6f, 1.6f, 9.6f),
            new Vector3(0.35f, 1.6f, 0.35f), Concrete(), t);

        if (primary)
        {
            // smokestack + roof vents -- the industrial silhouette
            b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-5f, height + 4.5f, -4f),
                new Vector3(0.9f, 4.5f, 0.9f), Concrete(), t);
            for (var i = 0; i < 2; i++)
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(3f + i * 3f, height + 0.9f, 3f),
                    new Vector3(1.3f, 1.1f, 1.3f), Corrugated(), t);
        }
    }

    // ---- medium tier: brick walk-up apartments ---------------------------------

    private static void DressApartment(RuntimeCityBuilder b, Transform t, float height, int h, bool primary, bool suburb = false)
    {
        var basePos = t.position;
        // suburb: warm-leaning (50% cream / 25% brick / 25% seafoam);
        // downtown: cool-leaning (50% seafoam / 25% cream / 25% brick)
        var wall = suburb
            ? ((h / 7) % 4 < 2 ? Cream() : (h / 7) % 4 == 2 ? Brick() : Seafoam())
            : ((h / 7) % 4 < 2 ? Seafoam() : (h / 7) % 4 == 2 ? Cream() : Brick());

        // window bands: dark strips proud of two opposite faces per floor
        var floors = Mathf.Max(2, Mathf.RoundToInt(height / 4f));
        for (var f = 0; f < floors; f++)
        {
            var y = (f + 0.55f) * (height / floors);
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, y, Half * 1.01f),
                new Vector3(15f, 1.5f, 0.35f), WindowBand(), t);
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, y, -Half * 1.01f),
                new Vector3(15f, 1.5f, 0.35f), WindowBand(), t);
        }
        // repaint accent: thin corner pilasters in the era wall color
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(Half * 0.98f, height / 2f, Half * 0.98f),
            new Vector3(1.2f, height, 1.2f), wall, t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(-Half * 0.98f, height / 2f, -Half * 0.98f),
            new Vector3(1.2f, height, 1.2f), wall, t);
        // cornice + tar roof
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.3f),
            new Vector3(19.4f, 0.6f, 19.4f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.65f),
            new Vector3(17.6f, 0.2f, 17.6f), RoofTar(), t);
        // fire escape: zig-zag ladder strip down one face
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(Half * 1.04f, height * 0.5f, 0f),
            new Vector3(0.25f, height * 0.86f, 3.2f), M(0.15f, 0.15f, 0.17f), t);

        if (primary) Rooftop(b, t, basePos, height, h);
    }

    // ---- large tier: stepped deco office ---------------------------------------

    private static void DressOffice(RuntimeCityBuilder b, Transform t, float height, int h, bool primary)
    {
        var basePos = t.position;
        var trim = (h / 5) % 2 == 0 ? Cream() : Mustard();

        // deco setbacks: two shrinking tiers on the roof
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 2.5f),
            new Vector3(13f, 5f, 13f), Concrete(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 6.5f),
            new Vector3(8f, 3f, 8f), Concrete(), t);
        // vertical pilaster strips -- the deco silhouette from a distance
        for (var i = -1; i <= 1; i++)
        {
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, height / 2f, Half * 1.01f),
                new Vector3(1.1f, height, 0.35f), trim, t);
            b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(i * 5.5f, height / 2f, -Half * 1.01f),
                new Vector3(1.1f, height, 0.35f), trim, t);
        }
        // window bands between pilasters (darker, recessed feel)
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height / 2f, Half * 0.995f),
            new Vector3(16.8f, height * 0.9f, 0.15f), WindowBand(), t);
        b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height / 2f, -Half * 0.995f),
            new Vector3(16.8f, height * 0.9f, 0.15f), WindowBand(), t);

        if (primary)
        {
            // antenna mast with a beacon -- the King-of-the-City silhouette
            b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 11f),
                new Vector3(0.3f, 3.2f, 0.3f), Chrome(), t);
            b.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * (height + 14.4f),
                new Vector3(0.9f, 0.9f, 0.9f), NeonRed(), t);
            Rooftop(b, t, basePos, height, h);
            // side billboard frame + period ad art
            var boardCenter = basePos + new Vector3(Half * 1.08f, height * 0.7f, 0f);
            b.SpawnPrim(PrimitiveType.Cube, boardCenter, new Vector3(0.4f, 6f, 10f), SignWhite(), t);
            DressPoster(b, t, boardCenter, h);
        }
    }

    // ---- billboard art: period-poster stripes on the office billboard --------

    private static Material AdRed() { return M(0.82f, 0.18f, 0.16f); }
    private static Material AdBlue() { return M(0.22f, 0.35f, 0.6f); }

    /// <summary>Fakes period ad graphics with flat color blocks -- no
    /// texture pipeline here, so the "ATOMIC COLA" bullseye and the movie
    /// one-sheet are read purely through primitive silhouette and color,
    /// same discipline as the rest of the primitive-kit dressing.</summary>
    private static void DressPoster(RuntimeCityBuilder b, Transform t, Vector3 boardCenter, int h)
    {
        switch (h % 3)
        {
            case 0:   // soda bullseye: a red disc over a mustard band
            {
                var disc = b.SpawnPrim(PrimitiveType.Cylinder, boardCenter + new Vector3(0.45f, 1.2f, 0f),
                    new Vector3(2.4f, 0.06f, 2.4f), AdRed(), t);
                disc.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, -1.8f, 0f),
                    new Vector3(0.1f, 1f, 8.5f), Mustard(), t);
                break;
            }
            case 1:   // movie one-sheet: stacked teal/mustard color blocks
            {
                for (var i = 0; i < 3; i++)
                    b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, 2f - i * 1.8f, 0f),
                        new Vector3(0.08f, 1.5f, 8.6f), i % 2 == 0 ? NeonTeal() : Mustard(), t);
                break;
            }
            default:  // headline bands: bold red over concrete gray
            {
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, 1f, 0f),
                    new Vector3(0.08f, 1.2f, 9f), AdRed(), t);
                b.SpawnPrim(PrimitiveType.Cube, boardCenter + new Vector3(0.45f, -1.2f, 0f),
                    new Vector3(0.08f, 0.8f, 9f), AdBlue(), t);
                break;
            }
        }
    }

    // ---- landmark tier: archetype-aware civic set pieces ------------------------

    private static void DressLandmark(RuntimeCityBuilder b, string archetype, Transform t,
        float height, int h, bool primary)
    {
        var basePos = t.position;
        // gold cornice band keeps the tier's RTS color read
        b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 0.4f),
            new Vector3(19.6f, 0.8f, 19.6f), Mustard(), t);
        if (!primary) return;

        switch (archetype)
        {
            case "church":
                // parish spire: tapering stacked boxes + a needle
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 4f),
                    new Vector3(7f, 8f, 7f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 10f),
                    new Vector3(4f, 5f, 4f), Concrete(), t);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 15f),
                    new Vector3(0.4f, 3.5f, 0.4f), Chrome(), t);
                break;
            case "cathedral":
                // grander than a parish church: twin flanking towers
                // (each taller than the single church spire) plus a rose
                // window on the front face -- the "downtown Notre Dame"
                // silhouette, so cathedral reads as an upgrade, not a
                // recolor of the same building
                foreach (var side in new[] { 1f, -1f })
                {
                    var towerBase = basePos + new Vector3(side * 6.5f, 0f, 0f);
                    b.SpawnPrim(PrimitiveType.Cube, towerBase + Vector3.up * (height + 5f),
                        new Vector3(5f, 10f, 5f), Concrete(), t);
                    b.SpawnPrim(PrimitiveType.Cube, towerBase + Vector3.up * (height + 12f),
                        new Vector3(3f, 4f, 3f), Concrete(), t);
                    b.SpawnPrim(PrimitiveType.Cylinder, towerBase + Vector3.up * (height + 16.5f),
                        new Vector3(0.4f, 4.5f, 0.4f), Chrome(), t);
                }
                var rose = b.SpawnPrim(PrimitiveType.Cylinder,
                    basePos + new Vector3(0f, height * 0.6f, Half * 1.02f),
                    new Vector3(2.6f, 0.1f, 2.6f), NeonTeal(), t);
                rose.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                break;
            case "town_hall":
                // columned portico + pediment + flagpole
                for (var i = -2; i <= 2; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3.4f, 5f, Half * 1.15f),
                        new Vector3(0.9f, 5f, 0.9f), Cream(), t);
                var pediment = b.SpawnPrim(PrimitiveType.Cube,
                    basePos + new Vector3(0f, 11.5f, Half * 1.15f), new Vector3(11f, 11f, 2.4f), Cream(), t);
                pediment.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3.5f),
                    new Vector3(0.25f, 3.5f, 0.25f), Chrome(), t);
                break;
            case "rail_depot":
                // long arched trainshed roof: a half-sunk lying cylinder
                var shed = b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * height,
                    new Vector3(9f, 9.6f, 9f), RustRed(), t);
                shed.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case "hospital":
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 4f, 0f),
                    new Vector3(6f, 1.8f, 1.2f), NeonRed(), t);   // red cross, horizontal bar
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 4f, 0f),
                    new Vector3(1.8f, 6f, 1.2f), NeonRed(), t);   // vertical bar
                break;
            case "plaza":
                // a grand civic building FRONTING the square, not the
                // square itself (the massing cube is still a 40m
                // landmark-tier block by construction -- this dresses it
                // as the formal building anchoring the plaza): a
                // colonnade, a clock cupola, and a small fountain out
                // front on the plaza pavement itself
                for (var i = -2; i <= 2; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3f, 6f, Half * 1.1f),
                        new Vector3(0.7f, 6f, 0.7f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 12.4f, Half * 1.1f),
                    new Vector3(16f, 0.8f, 2.6f), Cream(), t);   // entablature over the columns
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 2.5f),
                    new Vector3(2.2f, 2.5f, 2.2f), Concrete(), t);   // clock cupola drum
                b.SpawnPrim(PrimitiveType.Sphere, basePos + Vector3.up * (height + 5.4f),
                    new Vector3(1.6f, 1.6f, 1.6f), Chrome(), t);
                var fountainAt = basePos + new Vector3(0f, 0f, Half * 1.7f);
                b.SpawnPrim(PrimitiveType.Cylinder, fountainAt + Vector3.up * 0.3f,
                    new Vector3(2.6f, 0.3f, 2.6f), Concrete(), t);   // basin
                b.SpawnPrim(PrimitiveType.Cylinder, fountainAt + Vector3.up * 1.1f,
                    new Vector3(1.2f, 0.8f, 1.2f), Chrome(), t);     // pedestal
                b.SpawnPrim(PrimitiveType.Sphere, fountainAt + Vector3.up * 2f,
                    new Vector3(0.5f, 0.9f, 0.5f), NeonTeal(), t);   // stylized water jet
                break;
            case "school":
                // a modest columned entrance -- plainer and smaller than
                // the plaza/town_hall civic scale -- plus the schoolyard
                // silhouette: bell cupola and a flagpole
                for (var i = -1; i <= 1; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 3.6f, 4f, Half * 1.1f),
                        new Vector3(0.6f, 4f, 0.6f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 8.3f, Half * 1.1f),
                    new Vector3(13f, 0.6f, 2.2f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 1.8f),
                    new Vector3(2.4f, 2.2f, 2.4f), Concrete(), t);   // bell cupola housing
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + Vector3.up * (height + 3.6f),
                    new Vector3(0.15f, 0.6f, 0.15f), RustRed(), t);  // bell
                b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(-6f, height + 3f, -6f),
                    new Vector3(0.15f, 3f, 0.15f), Chrome(), t);     // flagpole
                break;
            case "old_age_home":
                // quieter and homelier than the other institutions: a
                // wraparound porch roof instead of a civic cornice, plus
                // a garden trellis on the ground -- reads residential
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.4f, Half * 1.15f),
                    new Vector3(17f, 0.5f, 4f), RustRed(), t);
                for (var i = -1; i <= 1; i++)
                    b.SpawnPrim(PrimitiveType.Cylinder, basePos + new Vector3(i * 5f, 2.6f, Half * 1.25f),
                        new Vector3(0.35f, 2.6f, 0.35f), Cream(), t);
                b.SpawnPrim(PrimitiveType.Cube, basePos + Vector3.up * (height + 1.5f),
                    new Vector3(6f, 1.6f, 4f), Cream(), t);   // dormer-ish roof projection
                var trellisAt = basePos + new Vector3(0f, 0f, Half * 1.6f);
                b.SpawnPrim(PrimitiveType.Cube, trellisAt + Vector3.up * 1.4f,
                    new Vector3(3.2f, 2.6f, 0.15f), GardenGreen(), t);
                break;
            default:
                // any future/unlisted archetype -> THE MOVIE PALACE: a
                // marquee slab out front and a big neon rooftop sign --
                // every b-movie city needs one theater downtown
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 6.5f, Half * 1.25f),
                    new Vector3(14f, 2.4f, 4.5f), SignWhite(), t);      // marquee
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, 5.2f, Half * 1.25f),
                    new Vector3(13f, 0.4f, 4.2f), NeonTeal(), t);       // marquee underglow
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 5f, 0f),
                    new Vector3(2f, 10f, 0.8f), SignWhite(), t);        // vertical blade sign
                b.SpawnPrim(PrimitiveType.Cube, basePos + new Vector3(0f, height + 5f, 0.7f),
                    new Vector3(1.2f, 8.6f, 0.4f), NeonRed(), t);       // neon letters strip
                break;
        }
        Rooftop(b, t, basePos, height, h + 3);
    }

    // ---- the rooftop kit: what sells the miniature from RTS height -------------

    private static void Rooftop(RuntimeCityBuilder b, Transform t, Vector3 basePos, float height, int h)
    {
        // water tower on stilts (the classic): ~45% of roofs
        if (h % 9 < 4)
        {
            var wt = basePos + new Vector3((h % 5) - 2f, 0f, (h % 7) - 3f);
            for (var i = 0; i < 4; i++)
            {
                var lx = i < 2 ? -1.1f : 1.1f;
                var lz = i % 2 == 0 ? -1.1f : 1.1f;
                b.SpawnPrim(PrimitiveType.Cylinder, wt + new Vector3(lx, height + 1.1f, lz),
                    new Vector3(0.22f, 1.1f, 0.22f), RustRed(), t);
            }
            b.SpawnPrim(PrimitiveType.Cylinder, wt + Vector3.up * (height + 3.4f),
                new Vector3(1.9f, 1.3f, 1.9f), RustRed(), t);            // tank
            b.SpawnPrim(PrimitiveType.Sphere, wt + Vector3.up * (height + 4.9f),
                new Vector3(2.5f, 1.2f, 2.5f), M(0.42f, 0.2f, 0.14f), t); // conical-ish cap
        }
        // vents / rooftop machinery
        var vents = 1 + h % 3;
        for (var i = 0; i < vents; i++)
        {
            var vp = basePos + new Vector3(((h + i * 37) % 11) - 5f, height + 0.7f, ((h + i * 61) % 11) - 5f);
            b.SpawnPrim(PrimitiveType.Cube, vp, new Vector3(1.4f, 1.2f, 1.4f), Concrete(), t);
        }
        // whip antenna
        if (h % 4 == 0)
            b.SpawnPrim(PrimitiveType.Cylinder,
                basePos + new Vector3(((h % 13) - 6f) * 0.8f, height + 2.4f, ((h % 17) - 8f) * 0.6f),
                new Vector3(0.08f, 2.4f, 0.08f), Chrome(), t);
    }
}
