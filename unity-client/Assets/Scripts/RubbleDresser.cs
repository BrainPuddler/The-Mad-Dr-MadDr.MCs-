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
}
