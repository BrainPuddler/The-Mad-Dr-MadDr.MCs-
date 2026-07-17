using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Real rubble silhouette for destroyed buildings (docs/21 batch 2, item
/// 5): a deterministic scatter of tumbled, unevenly-sized chunks laid
/// over the crushed massing pancake, instead of one flat slab reading as
/// "squashed cube". Colliderless -- rubble hexes are already open to
/// pathing via the battlefield state; these chunks are visual only.
/// </summary>
public static class RubbleDresser
{
    private static int Hash(HexCoord hex, int salt)
    {
        unchecked
        {
            var h = hex.Q * 374761393 + hex.R * 668265263 + salt * 974711;
            h = (h ^ (h >> 13)) * 1274126177;
            return h & 0x7FFFFFFF;
        }
    }

    public static void Scatter(RuntimeCityBuilder builder, Building building, Material rubbleMat, Transform parent)
    {
        var host = new GameObject("Rubble").transform;
        host.SetParent(parent, false);

        foreach (var hex in building.Footprint)
        {
            var center = builder.WorldOf(hex);
            var h = Hash(hex, 41);
            var chunks = 4 + h % 4;
            for (var i = 0; i < chunks; i++)
            {
                var hi = Hash(hex, 100 + i);
                var off = new Vector3((hi % 13) - 6f, 0f, ((hi >> 4) % 13) - 6f) * 0.7f;
                var pos = center + off;
                pos.y = builder.GroundHeightAt(pos) + 0.3f + (hi % 5) * 0.12f;
                var size = 1.4f + (hi % 5) * 0.55f;
                var scale = new Vector3(size, size * (0.5f + (hi % 3) * 0.2f), size * (0.7f + (hi % 4) * 0.15f));
                var chunk = builder.SpawnPrim(PrimitiveType.Cube, pos, scale, rubbleMat, host);
                chunk.transform.rotation = Quaternion.Euler((hi % 23) - 11f, (hi % 47) * 7.6f, (hi % 19) - 9f);
            }
        }
    }

    /// <summary>Replaces a footprint hex's massing cube -- destroyed by
    /// the caller just before this runs -- with several big broken slab
    /// pieces instead of squishing the whole 18m-wide cube flat in
    /// place. A uniform full-width slab pancaked to a fraction of its
    /// height reads as a flat stain (a "radiating puddle", per report)
    /// from the RTS camera, not a collapsed building; a handful of
    /// varied-size, steeply-tilted wall-section-scale slabs reads as
    /// actual broken masonry. `Scatter`'s smaller debris chunks layer on
    /// top of this, same as before.</summary>
    public static void Shatter(RuntimeCityBuilder builder, HexCoord hex, Vector3 originalCenter, Material rubbleMat, Transform parent)
    {
        var host = new GameObject("Collapsed").transform;
        host.SetParent(parent, false);

        var h = Hash(hex, 61);
        var pieces = 3 + h % 3;
        for (var i = 0; i < pieces; i++)
        {
            var hi = Hash(hex, 200 + i);
            var off = new Vector3((hi % 11) - 5f, 0f, ((hi >> 4) % 11) - 5f) * 0.9f;
            var pos = originalCenter + off;
            pos.y = builder.GroundHeightAt(pos) + 0.6f + (hi % 4) * 0.4f;
            var width = 5f + (hi % 4) * 1.6f;         // 5-9.8m: a broken wall-section scale
            var thickness = 0.8f + (hi % 3) * 0.4f;   // 0.8-2.0m thick, not a paper-thin sheet
            var depth = 3f + ((hi >> 3) % 4) * 1.2f;
            var slab = builder.SpawnPrim(PrimitiveType.Cube, pos,
                new Vector3(width, thickness, depth), rubbleMat, host);
            // tilted like a fallen wall section -- some lie nearly flat,
            // others lean steeply, never all at the same uniform angle
            slab.transform.rotation = Quaternion.Euler(
                ((hi % 7) - 3f) * 6f + (hi % 2) * 30f,
                (hi * 37) % 360,
                (((hi >> 2) % 7) - 3f) * 6f);
        }
    }
}
