using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural mesh generation for the standalone low-poly fire system.
/// Deliberately self-contained -- its own hash function, own winding/
/// normal handling -- rather than calling into ProceduralMeshKit (the
/// smoke plume's CloudShard and the OLD fire's FlameShard both live
/// there). Nothing in this file references ProceduralMeshKit, DamageFx,
/// or any smoke type.
///
/// Every mesh here is a single "flame tongue": a faceted, tapered blade
/// rooted at local origin (y=0) and reaching to local y=1, meant to be
/// scaled/rotated per-instance by the caller (LowPolyFireManager) rather
/// than varied by regenerating geometry every frame -- shape variety
/// comes from a handful of seeded variants built once at startup, motion
/// comes from the caller's per-frame instance transform, not from
/// deforming these vertices live.
/// </summary>
public static class LowPolyFireMeshKit
{
    /// <summary>Deterministic 0..1 hash off two integer salts -- same GLSL-
    /// sine-hash shape as the rest of this codebase's "cosmetic jitter, no
    /// gameplay meaning" convention, reimplemented locally so this file
    /// has no call-out to ProceduralMeshKit's copy.</summary>
    public static float Hash01(int a, int b)
    {
        var v = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    /// <summary>Builds one faceted flame-tongue mesh. `sides` controls how
    /// angular the blade reads (low counts, e.g. 4-6, keep the "polygonal
    /// triangles / strong silhouette" low-poly look); `seed` picks the
    /// specific jitter/lean/twist for this variant. Flat shading comes
    /// from never sharing a vertex between two faces -- each quad/tri
    /// below adds its own fresh verts, so RecalculateNormals has nothing
    /// to average and every triangle keeps a uniform per-face normal.</summary>
    public static Mesh BuildTongueMesh(int sides, float seed)
    {
        sides = Mathf.Max(3, sides);
        var seedI = Mathf.FloorToInt(seed * 104729f);

        // three rings (base -> mid bulge -> upper taper) plus a single
        // apex vertex -- a licking flame's classic base-bulge-point profile.
        var ringY = new[] { 0f, 0.34f, 0.7f };
        var ringR = new[] { 0.5f, 0.58f, 0.26f };

        // the whole blade leans toward one side, growing with height --
        // real flicker/lean baked into the static shape, animated lean is
        // layered on top per-instance by the caller.
        var bendX = (Hash01(seedI, 97) - 0.5f) * 0.75f;
        var bendZ = (Hash01(seedI, 131) - 0.5f) * 0.75f;

        var ringPos = new Vector3[ringY.Length][];
        for (var r = 0; r < ringY.Length; r++)
        {
            ringPos[r] = new Vector3[sides];
            var leanT = ringY[r] * ringY[r]; // quadratic -- lean grows faster near the tip
            for (var s = 0; s < sides; s++)
            {
                var ang = s / (float)sides * Mathf.PI * 2f + Hash01(seedI, r * 10 + s) * 0.5f;
                var jitter = 0.72f + Hash01(seedI, r * 20 + s + 5) * 0.56f;
                var radius = ringR[r] * jitter;
                var x = Mathf.Cos(ang) * radius + bendX * leanT;
                var z = Mathf.Sin(ang) * radius + bendZ * leanT;
                ringPos[r][s] = new Vector3(x, ringY[r], z);
            }
        }
        var apex = new Vector3(bendX * 1.3f, 1f, bendZ * 1.3f);

        var verts = new List<Vector3>(sides * (ringY.Length - 1) * 4 + sides * 3);
        var tris = new List<int>(verts.Capacity);

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var b0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2);
            tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 3);
        }

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            var b0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2);
        }

        for (var r = 0; r < ringY.Length - 1; r++)
        {
            for (var s = 0; s < sides; s++)
            {
                var s2 = (s + 1) % sides;
                AddQuad(ringPos[r][s], ringPos[r][s2], ringPos[r + 1][s2], ringPos[r + 1][s]);
            }
        }
        var topRing = ringPos[ringY.Length - 1];
        for (var s = 0; s < sides; s++)
        {
            var s2 = (s + 1) % sides;
            AddTri(topRing[s], topRing[s2], apex);
        }
        // no base cap -- the tongue's root sits flush against whatever
        // surface it's anchored to and is never seen from below.

        // Safety net: rather than hand-verify winding without an Editor
        // to look at, flip any triangle whose vertex-order normal points
        // toward the shape's own local axis instead of away from it.
        FixOutwardWinding(verts, tris, new Vector3(bendX * 0.4f, 0.4f, bendZ * 0.4f));

        var mesh = new Mesh { name = "LowPolyFireTongue" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void FixOutwardWinding(List<Vector3> verts, List<int> tris, Vector3 pivot)
    {
        for (var i = 0; i < tris.Count; i += 3)
        {
            var a = verts[tris[i]];
            var b = verts[tris[i + 1]];
            var c = verts[tris[i + 2]];
            var faceNormal = Vector3.Cross(b - a, c - a);
            var center = (a + b + c) / 3f;
            if (Vector3.Dot(faceNormal, center - pivot) < 0f)
            {
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
        }
    }
}
