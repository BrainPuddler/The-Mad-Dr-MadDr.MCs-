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
/// Winding order is fixed up programmatically (see FaceOutward) rather
/// than trusted to hand-authoring, since this environment has no Editor
/// to visually catch an inside-out face.
///
/// **2026-07 -- why NOT double-winding (the bug this replaces):** these
/// helpers used to emit every face in BOTH windings, as belt-and-braces
/// against a winding mistake. That silently destroyed lighting.
/// `RecalculateNormals` sets each vertex normal to the average of the
/// face normals sharing it -- with every face present in both windings,
/// each contributes +N and -N, which cancel to EXACTLY ZERO. A zero
/// normal makes dot(N, L) zero for every light, so the prop renders pure
/// black no matter how bright/close/numerous the lights are. That is why
/// the ornate lamppost and market stall stayed solid black while stock
/// primitives around them lit correctly, and why no amount of tuning
/// light range/intensity/bloom ever moved them. Emit each face ONCE.
/// </summary>
public static class ProceduralMeshKit
{
    private static void Tri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    private static void Quad(List<int> tris, int a, int b, int c, int d)
    {
        Tri(tris, a, b, c);
        Tri(tris, a, c, d);
    }

    /// <summary>Re-winds any triangle that faces inward so every face
    /// points away from the mesh centroid -- the Editor-free replacement
    /// for the old double-winding safety net. Exact for CONVEX shapes
    /// (both shapes here are convex); a concave mesh would need real
    /// authored winding instead.
    ///
    /// Deliberately uses the SAME cross product Unity's own
    /// RecalculateNormals does, so the test is self-consistent with the
    /// normals that get generated straight after -- if this says a face
    /// points outward, RecalculateNormals agrees.</summary>
    private static void FaceOutward(List<Vector3> verts, List<int> tris)
    {
        var center = Vector3.zero;
        for (var i = 0; i < verts.Count; i++) center += verts[i];
        center /= Mathf.Max(1, verts.Count);

        for (var t = 0; t + 2 < tris.Count; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            var faceNormal = Vector3.Cross(b - a, c - a);
            var outward = (a + b + c) / 3f - center;
            if (Vector3.Dot(faceNormal, outward) >= 0f) continue;
            var swap = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = swap;
        }
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

        FaceOutward(verts, tris);

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Deterministic 0..1 hash off two float seeds -- the classic
    /// GLSL sine-hash trick. Cosmetic jitter only (facet irregularity),
    /// no gameplay meaning riding on it, same "GetInstanceID/seed hashing
    /// is fine for pure visual variety" precedent the rest of this
    /// codebase's dressers already use.</summary>
    private static float Hash01(float seedA, float seedB)
    {
        var s = Mathf.Sin(seedA * 12.9898f + seedB * 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    /// <summary>2026-08 (creator report: "the fire is too large... it
    /// should look like [reference: low-poly, faceted, angular fire]"):
    /// a small jagged shard tapering from an irregular low-poly base ring
    /// to an off-center apex -- the off-center tip is what reads as a
    /// "licking" flame lean instead of a symmetric party-hat cone, and
    /// the jittered ring radii (deterministic off `seed`, same building-
    /// to-building/puff-to-puff variety convention as every other jitter
    /// in this file) give it the angular, faceted silhouette the
    /// reference images show instead of DamageFx's previous smooth
    /// sphere puffs. Centered at local origin, extends -0.5..0.5 in Y,
    /// same calling convention as <see cref="Frustum"/>.</summary>
    public static Mesh FlameShard(int segments, float seed)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var baseRing = new int[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * 2f * Mathf.PI;
            var jitter = 0.7f + Hash01(seed, i * 7.13f) * 0.6f; // 0.7-1.3
            var r = 0.5f * jitter;
            baseRing[i] = verts.Count;
            verts.Add(new Vector3(Mathf.Sin(a) * r, -0.5f, Mathf.Cos(a) * r));
        }
        var baseCenter = verts.Count; verts.Add(new Vector3(0f, -0.5f, 0f));
        var bendX = (Hash01(seed, 97f) - 0.5f) * 0.5f;
        var bendZ = (Hash01(seed, 191f) - 0.5f) * 0.5f;
        var apex = verts.Count; verts.Add(new Vector3(bendX, 0.5f, bendZ));

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            Tri(tris, baseRing[i], apex, baseRing[next]);
            Tri(tris, baseCenter, baseRing[next], baseRing[i]);
        }

        FaceOutward(verts, tris);

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>2026-08 (creator direction, confirming a reference image:
    /// "1 angular" for smoke shape): a jittered, twisted low-poly barrel --
    /// two irregular rings (a wider bottom, a narrower top rotated by a
    /// random twist so the side facets aren't rectangular) capped top and
    /// bottom, unlike <see cref="FlameShard"/>'s single taper-to-a-point
    /// cone. A smoke puff is rounder/chunkier than a flame lick, so this
    /// reads as an angular cloud chunk rather than another flame shape.
    /// Same calling convention as every other shape here: centered at
    /// local origin, extends -0.5..0.5 in Y.</summary>
    public static Mesh CloudShard(int segments, float seed)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var bottomRing = new int[segments];
        var topRing = new int[segments];
        var topTwist = Hash01(seed, 311f) * (2f * Mathf.PI / segments);
        for (var i = 0; i < segments; i++)
        {
            var aBottom = i / (float)segments * 2f * Mathf.PI;
            var aTop = aBottom + topTwist;
            var jitterBottom = 0.75f + Hash01(seed, i * 5.31f) * 0.5f; // 0.75-1.25
            var jitterTop = 0.6f + Hash01(seed, i * 9.17f + 50f) * 0.55f; // 0.6-1.15
            var rBottom = 0.5f * jitterBottom;
            var rTop = 0.4f * jitterTop;
            bottomRing[i] = verts.Count;
            verts.Add(new Vector3(Mathf.Sin(aBottom) * rBottom, -0.25f, Mathf.Cos(aBottom) * rBottom));
            topRing[i] = verts.Count;
            verts.Add(new Vector3(Mathf.Sin(aTop) * rTop, 0.3f, Mathf.Cos(aTop) * rTop));
        }
        var bottomCap = verts.Count; verts.Add(new Vector3(0f, -0.5f, 0f));
        var topCap = verts.Count; verts.Add(new Vector3(0f, 0.5f, 0f));

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            Tri(tris, bottomCap, bottomRing[next], bottomRing[i]);
            Tri(tris, topCap, topRing[i], topRing[next]);
            Quad(tris, bottomRing[i], topRing[i], topRing[next], bottomRing[next]);
        }

        FaceOutward(verts, tris);

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>2026-08 (creator direction: "Add a real triangle
    /// primitive to roof generator NOT a Cube on it's side"): a real
    /// triangular prism -- a symmetric ridge/gable roof, replacing the
    /// old trick of rotating a `PrimitiveType.Cube` 45 degrees about Z
    /// and sinking half of it into the block so only the top "V" of the
    /// resulting diamond showed. That trick worked (a diamond's upper
    /// half IS visually a ridge), but it was never a real triangle mesh
    /// -- always six square cube faces underneath, four of them just
    /// hidden below the roof plane. This is the honest shape: two
    /// triangular gable-end caps and two rectangular sloped faces,
    /// nothing hidden.
    ///
    /// Centered at local origin, extends -0.5..0.5 on every axis -- same
    /// calling convention as every other shape here (<see cref="Wedge"/>,
    /// <see cref="Frustum"/>). The ridge runs along local Z (the "length"
    /// axis, matching how a building's own depth becomes the roof's
    /// ridge-run direction at every call site); the triangular
    /// cross-section is in the XY plane: base corners at Y=-0.5 (the
    /// eaves, sitting flush on the roof plane a caller positions this
    /// at), apex at Y=+0.5 (the ridge line).</summary>
    public static Mesh GableRoof()
    {
        var v = new List<Vector3>
        {
            new Vector3(-0.5f, -0.5f, -0.5f),  // 0 back-left eave
            new Vector3(0.5f, -0.5f, -0.5f),   // 1 back-right eave
            new Vector3(0f, 0.5f, -0.5f),      // 2 back ridge
            new Vector3(-0.5f, -0.5f, 0.5f),   // 3 front-left eave
            new Vector3(0.5f, -0.5f, 0.5f),    // 4 front-right eave
            new Vector3(0f, 0.5f, 0.5f),       // 5 front ridge
        };
        var tris = new List<int>();
        Quad(tris, 0, 1, 4, 3);   // bottom (eaves plane -- usually unseen, sat flush on the roof cap)
        Quad(tris, 0, 3, 5, 2);   // left slope
        Quad(tris, 1, 2, 5, 4);   // right slope
        Tri(tris, 0, 2, 1);       // back gable-end cap
        Tri(tris, 3, 4, 5);       // front gable-end cap

        FaceOutward(v, tris);

        var mesh = new Mesh();
        mesh.vertices = v.ToArray();
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
        var v = new List<Vector3>
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

        FaceOutward(v, tris);

        var mesh = new Mesh();
        mesh.vertices = v.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>2026-08 (faction gauntlet addendum, docs/31 §6: "the
    /// current Alien Factory/Control Centre massing... is the wrong
    /// starting point. A saucer IS the body, not something placed on top
    /// of one"): a real revolved flying-saucer profile -- a tapered
    /// underside cone up to a flat rim band, then a tapered dome cap
    /// above, lathed around Y the same way <see cref="Frustum"/> lathes
    /// its own two-ring profile, just with more profile rings. Centered
    /// at local origin, extends -0.5..0.5 on every axis -- same calling
    /// convention as every other shape here, so a caller sizes/positions
    /// it exactly like `Frustum`/`GableRoof`/`Wedge` via `scale` alone.
    ///
    /// `domeRadiusFrac` (0..1): how wide the dome's shoulder ring is
    /// relative to the full rim radius -- smaller reads pointier/more
    /// UFO-like, larger reads flatter/more lens-shaped.
    /// `rimHalfThickness` (fraction of total height): half the flat
    /// rim band's own vertical thickness -- the disc "edge" a saucer
    /// silhouette reads by.
    /// `elevation` (fraction of total height, roughly -0.3..0.3): where
    /// the rim band sits along the vertical axis -- 0 is dead center
    /// (equal dome/belly), positive lifts the rim toward the dome
    /// (a deeper tapered underside, the classic "flying saucer" read).</summary>
    public static Mesh Saucer(float domeRadiusFrac, float rimHalfThickness, float elevation, int segments)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        var bottomTip = verts.Count; verts.Add(new Vector3(0f, -0.5f, 0f));
        var apex = verts.Count; verts.Add(new Vector3(0f, 0.5f, 0f));

        var rimBottomY = elevation - rimHalfThickness;
        var rimTopY = elevation + rimHalfThickness;
        var domeShoulderY = Mathf.Lerp(rimTopY, 0.5f, 0.4f);
        var domeShoulderR = 0.5f * domeRadiusFrac;

        var rimBottomRing = new int[segments];
        var rimTopRing = new int[segments];
        var domeShoulderRing = new int[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * 2f * Mathf.PI;
            var x = Mathf.Sin(a);
            var z = Mathf.Cos(a);
            rimBottomRing[i] = verts.Count; verts.Add(new Vector3(x * 0.5f, rimBottomY, z * 0.5f));
            rimTopRing[i] = verts.Count; verts.Add(new Vector3(x * 0.5f, rimTopY, z * 0.5f));
            domeShoulderRing[i] = verts.Count; verts.Add(new Vector3(x * domeShoulderR, domeShoulderY, z * domeShoulderR));
        }

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            Tri(tris, bottomTip, rimBottomRing[next], rimBottomRing[i]);                          // tapered underside
            Quad(tris, rimBottomRing[i], rimTopRing[i], rimTopRing[next], rimBottomRing[next]);   // flat rim band edge
            Quad(tris, rimTopRing[i], domeShoulderRing[i], domeShoulderRing[next], rimTopRing[next]); // rim-to-dome taper
            Tri(tris, apex, domeShoulderRing[i], domeShoulderRing[next]);                          // dome cap
        }

        FaceOutward(verts, tris);

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
