using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Scene-view smoke test for the com.maddr.citygen-core package reference:
/// draws the docs/18 hex grid (1 hex = 20 m) around this object, plus the
/// 5-hex Collection Station radius (docs/18/20) as a highlighted disc.
/// Gizmos only -- nothing at runtime. If this compiles and draws, the
/// engine-agnostic package is wired up correctly.
/// </summary>
public class HexGridGizmo : MonoBehaviour
{
    [Tooltip("Grid radius to draw, in hexes, around this object.")]
    [Range(1, 12)] public int gridRadius = 8;

    [Tooltip("Highlight radius in hexes: 5 = Collection Station (docs/18), 3 = emitter aura (docs/03).")]
    [Range(0, 8)] public int highlightRadius = 5;

    private void OnDrawGizmos()
    {
        var origin = new HexCoord(0, 0);

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        foreach (var hex in origin.Range(gridRadius))
        {
            DrawHexOutline(hex);
        }

        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
        foreach (var hex in origin.Ring(highlightRadius))
        {
            DrawHexOutline(hex);
        }
    }

    private void DrawHexOutline(HexCoord hex)
    {
        var (cx, cz) = hex.ToWorld();
        var center = transform.position + new Vector3((float)cx, 0f, (float)cz);

        // Pointy-top corner circumradius: center spacing = size * sqrt(3).
        var size = (float)(HexCoord.HexMeters / Mathf.Sqrt(3f));

        var prev = Corner(center, size, 0);
        for (var k = 1; k <= 6; k++)
        {
            var next = Corner(center, size, k % 6);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private static Vector3 Corner(Vector3 center, float size, int k)
    {
        // Pointy-top: corners at 60deg * k - 30deg.
        var angle = (60f * k - 30f) * Mathf.Deg2Rad;
        return center + new Vector3(size * Mathf.Cos(angle), 0f, size * Mathf.Sin(angle));
    }
}
