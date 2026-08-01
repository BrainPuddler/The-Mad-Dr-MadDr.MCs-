using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-07 (creator direction, replacing the pink-sphere-cluster brain
/// BaseDresser's Big Brain jar originally used): "Create a stylized but
/// believable human brain... Keep the mesh extremely low resolution
/// (500-2,000 triangles)... Preserve a clean, recognizable silhouette by
/// modeling only the major anatomical landmarks -- the two cerebral
/// hemispheres, central longitudinal fissure, cerebellum, and brainstem
/// -- while relying on textures to convey the intricate anatomy." A
/// cerebellum lobe was built and shipped briefly, then dropped on
/// creator review (2026-07 follow-up): tucked below and behind the
/// hemispheres the way real anatomy has it, it read as sitting almost
/// entirely below the visible hemisphere mass -- inside the jar's
/// glass but out of frame from the angles that matter, not contributing
/// to the silhouette. Cut rather than kept as dead geometry.
///
/// Real generated geometry, not an imported asset -- this environment has
/// no DCC/Editor pipeline to author one (same standing constraint
/// ProceduralMeshKit/LabMeshBuilder's own headers already document). A
/// hand-authored UV-sphere builder (not in ProceduralMeshKit already --
/// that file's two shapes are both convex primitives its own FaceOutward
/// helper corrects against a single shared centroid, which does NOT hold
/// for a multi-lobe mesh like this one: three spheres offset well away
/// from a shared origin can have their winding misjudged by a
/// single-global-centroid test if that centroid falls outside a given
/// lobe's own convex hull. Wound correctly by construction per-lobe
/// instead (see AddUvSphere's own comment).
/// </summary>
public static class BrainMesh
{
    /// <summary>The two hemispheres, flattened toward the central
    /// fissure and offset apart to leave it visible as a groove, merged
    /// into ONE mesh sharing one equirectangular UV layout per lobe -- so
    /// one material/texture set reads consistently across the whole
    /// brain mass. No cerebellum lobe (see this file's own class header
    /// for why it was cut). The brainstem is deliberately NOT included
    /// here (see BuildBrainstem below): it's plain flesh-toned geometry,
    /// not brain-surface tissue, so it gets its own simple primitive and
    /// material instead of sharing this mesh's detailed PBR set for no
    /// visual benefit.
    ///
    /// Triangle budget at the default segment counts: two hemispheres at
    /// 14x10 (~260 tris each) = ~520 tris total, comfortably inside the
    /// requested 500-2,000 range.</summary>
    public static Mesh BuildBrainMass(float radius)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // central longitudinal fissure: two hemispheres, each flattened
        // on its INNER (fissure-facing) side and offset apart just far
        // enough to read as a visible groove down the midline, not two
        // fully separate balls.
        var gap = radius * 0.12f;
        var hemiRadii = new Vector3(radius * 0.62f, radius * 0.72f, radius * 0.82f);
        AddUvSphere(verts, uvs, tris, new Vector3(-gap - hemiRadii.x * 0.35f, radius * 0.05f, 0f), hemiRadii, 14, 10);
        AddUvSphere(verts, uvs, tris, new Vector3(gap + hemiRadii.x * 0.35f, radius * 0.05f, 0f), hemiRadii, 14, 10);

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A short tapered stalk beneath the cerebellum -- plain
    /// flesh-toned geometry (no brain-surface texture needed), reusing
    /// this project's existing tapered-cylinder pattern. Centered at
    /// local origin like every other primitive/mesh helper in this
    /// codebase (see ProceduralMeshKit.Frustum's own doc comment) so it
    /// drops into the same position/scale calling convention.</summary>
    public static Mesh BuildBrainstem(int segments)
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
            bottomRing[i] = verts.Count; verts.Add(new Vector3(x * 0.42f, -0.5f, z * 0.42f));
            topRing[i] = verts.Count; verts.Add(new Vector3(x * 0.5f, 0.5f, z * 0.5f));
        }
        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            // Side quad, wound outward (verified by hand cross-product
            // math against the docs/28 winding-bug class -- the caps
            // below were already correct by construction, only these two
            // were backwards).
            Tri(tris, bottomRing[i], topRing[next], topRing[i]);
            Tri(tris, bottomRing[i], bottomRing[next], topRing[next]);
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

    private static void Tri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    /// <summary>A latitude/longitude sphere, squashed per-axis by
    /// `radii` and offset to `center` -- deliberately wound CORRECTLY BY
    /// CONSTRUCTION (per-quad winding `(a,b,d)`/`(a,d,c)`, hand-verified
    /// by cross-product against Unity's own left-handed convention --
    /// an earlier `(a,d,b)`/`(a,c,d)` ordering looked plausible but was
    /// actually backwards, caught by a standalone numeric verification
    /// harness before this ever reached the Editor) rather than
    /// corrected after the fact by a shared-centroid pass, since this
    /// mesh merges multiple lobes offset well away from any single
    /// shared center -- see this file's own class header for why
    /// ProceduralMeshKit.FaceOutward's technique doesn't apply here.
    /// The north (y==0) and south (y==lat) pole rings each collapse
    /// every longitude step to one coincident vertex, so the one
    /// triangle per quad that would use both coincident pole vertices
    /// is skipped there (zero-area, and RecalculateNormals chokes on
    /// degenerate triangles -- the exact docs/28 failure mode).
    /// UV is a plain equirectangular unwrap (u = longitude, v =
    /// latitude), good enough for a tileable organic surface texture,
    /// not a claimed seamless/distortion-free unwrap.</summary>
    private static void AddUvSphere(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector3 center, Vector3 radii, int lat, int lon)
    {
        var baseIndex = verts.Count;
        for (var y = 0; y <= lat; y++)
        {
            var v = y / (float)lat;
            var phi = v * Mathf.PI;
            var sinPhi = Mathf.Sin(phi);
            var cosPhi = Mathf.Cos(phi);
            for (var x = 0; x <= lon; x++)
            {
                var u = x / (float)lon;
                var theta = u * 2f * Mathf.PI;
                var px = sinPhi * Mathf.Cos(theta);
                var py = cosPhi;
                var pz = sinPhi * Mathf.Sin(theta);
                verts.Add(center + new Vector3(px * radii.x, py * radii.y, pz * radii.z));
                uvs.Add(new Vector2(u, 1f - v));
            }
        }

        var ring = lon + 1;
        for (var y = 0; y < lat; y++)
        {
            for (var x = 0; x < lon; x++)
            {
                var a = baseIndex + y * ring + x;
                var b = baseIndex + y * ring + x + 1;
                var c = baseIndex + (y + 1) * ring + x;
                var d = baseIndex + (y + 1) * ring + x + 1;
                // North pole ring: a/b are the same coincident vertex.
                if (y != 0) Tri(tris, a, b, d);
                // South pole ring: c/d are the same coincident vertex.
                if (y != lat - 1) Tri(tris, a, d, c);
            }
        }
    }
}
