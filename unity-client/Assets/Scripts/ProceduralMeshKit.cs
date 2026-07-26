using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/23 Phase 10.4 (Meshes): hand-authored placeholder geometry for a
/// couple of shapes CreatePrimitive doesn't offer (a tapered pole, a
/// lean-to awning) -- the same manual vertex/triangle authoring
/// LabMeshBuilder already uses to turn creature-mesh chunks into live
/// Unity geometry, applied here to small architectural shapes instead of
/// a creature body. Real generated meshes, not imported assets -- this
/// environment has no DCC/Editor pipeline to author real ones.
///
/// Every face is emitted in BOTH triangle windings (see Quad/Tri) rather
/// than risking an invisible or wrong-shaded face from a winding-order
/// mistake this environment has no Editor to visually catch. That
/// doubles the triangle count -- fine for these small, few-per-scene
/// props; not a pattern to copy for anything performance-sensitive.
/// </summary>
public static class ProceduralMeshKit
{
    private static void Tri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(b);   // reverse-wound twin
    }

    private static void Quad(List<int> tris, int a, int b, int c, int d)
    {
        Tri(tris, a, b, c);
        Tri(tris, a, c, d);
    }

    /// <summary>A tapered cylinder -- centered at local origin like
    /// CreatePrimitive's own shapes (extends -0.5..0.5 in Y before
    /// scale), so it drops into the same position/scale calling
    /// convention as a primitive (see RuntimeCityBuilder.SpawnPrim).</summary>
    public static Mesh Frustum(float bottomRadius, float topRadius, int segments)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var bottomCenter = verts.Count; verts.Add(new Vector3(0f, -0.5f, 0f));
        var topCenter = verts.Count; verts.Add(new Vector3(0f, 0.5f, 0f));

        var bottomRing = new int[segments];
        var topRing = new int[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * 2f * Mathf.PI;
            var x = Mathf.Sin(a);
            var z = Mathf.Cos(a);
            bottomRing[i] = verts.Count; verts.Add(new Vector3(x * bottomRadius, -0.5f, z * bottomRadius));
            topRing[i] = verts.Count; verts.Add(new Vector3(x * topRadius, 0.5f, z * topRadius));
        }

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            Quad(tris, bottomRing[i], topRing[i], topRing[next], bottomRing[next]);
            Tri(tris, bottomCenter, bottomRing[next], bottomRing[i]);
            Tri(tris, topCenter, topRing[i], topRing[next]);
        }

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A lean-to awning wedge -- centered at local origin,
    /// extends -0.5..0.5 on every axis. Flat bottom and back, sloping
    /// from the back-top edge down to the front-bottom edge (local +Z is
    /// the open/sloped side).</summary>
    public static Mesh Wedge()
    {
        var v = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),  // 0 back-bottom-left
            new Vector3(0.5f, -0.5f, -0.5f),   // 1 back-bottom-right
            new Vector3(-0.5f, 0.5f, -0.5f),   // 2 back-top-left
            new Vector3(0.5f, 0.5f, -0.5f),    // 3 back-top-right
            new Vector3(-0.5f, -0.5f, 0.5f),   // 4 front-bottom-left
            new Vector3(0.5f, -0.5f, 0.5f),    // 5 front-bottom-right
        };
        var tris = new List<int>();
        Quad(tris, 0, 4, 5, 1);   // bottom
        Quad(tris, 0, 1, 3, 2);   // back (vertical)
        Quad(tris, 2, 3, 5, 4);   // sloped top/front
        Tri(tris, 0, 2, 4);       // left end cap
        Tri(tris, 1, 5, 3);       // right end cap

        var mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
