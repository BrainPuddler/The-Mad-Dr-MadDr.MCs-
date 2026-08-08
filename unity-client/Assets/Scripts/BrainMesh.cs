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
/// instead (see AddFoldedUvSphere's own comment).
///
/// 2026-08 ("Major Improvement" creator direction: "improve the brain
/// model so it looks much more anatomically believable and organic...
/// add convincing gyri and sulci... irregular, organic, naturally
/// distributed, not repetitive or procedurally uniform... establish
/// recognizable left and right hemispheres, with a subtle central
/// division... vary the scale and depth of the folds... avoid perfectly
/// symmetrical or mirrored patterns... the underside and edges should
/// also have believable brain structure"): the ORIGINAL mesh (a plain
/// squashed UV-sphere pair) leaned entirely on BrainTextureKit's normal
/// map for fold detail -- fine for a head-on lit render, but a normal
/// map is a lighting trick with zero actual depth: it can't change the
/// SILHOUETTE, and does nothing at all for the underside/edges the brief
/// specifically calls out. `AddFoldedUvSphere` below adds REAL per-
/// vertex displacement on top of the same squashed-sphere base shape --
/// genuine 3D gyri/sulci, visible in silhouette and from any angle,
/// layered UNDER the existing normal/AO/albedo map (which still adds its
/// own finer micro-detail on top, at zero extra triangle cost) rather
/// than replacing it. Segment counts bumped (14x10 -> 32x28 per
/// hemisphere) to give the displacement enough vertex density to read as
/// organic ridges instead of a faceted mess; triangle budget landed
/// around 3,500 total (see the constants below) -- comfortably cheap for
/// a single hero prop that exists on at most a handful of buildings any
/// one match (BuildingDef's own 20-Brain cost keeps Big Brain rare), well
/// past this file's original 500-2,000 ceiling but nowhere near a real
/// performance concern.
/// </summary>
public static class BrainMesh
{
    // 2026-08: bumped from the original 14x10 specifically so the new
    // fold displacement has enough vertices to read as organic ridges
    // rather than faceting -- see this file's own class header for the
    // full triangle-budget accounting.
    private const int LatSegments = 32;
    private const int LonSegments = 28;

    // Different, unrelated salt families per hemisphere -- the whole
    // point being that left and right run the SAME fold algorithm but
    // land on genuinely different noise, so they read as two organs of
    // the same kind rather than one mirrored across the fissure (2026-08
    // creator direction: "avoid perfectly symmetrical or mirrored
    // patterns"). Kept well clear of BrainTextureKit's own salts
    // (10/20/30) and PbrTextureAtlas's (1-14) purely so nobody
    // mentally conflates "this noise field" with "that one" while
    // reading either file -- there's no numeric requirement they differ,
    // just a documentation-clarity one.
    private const int LeftFoldSaltBase = 110;
    private const int RightFoldSaltBase = 210;

    // Fraction of a hemisphere's own average radius that the DEEPEST
    // combined fold (broad+medium+ridged all leaning the same direction
    // at once, a rare tail case -- see FoldOffset's own comment for the
    // worked-out bound) can displace a vertex by. ~11% at the extreme,
    // typically much less in practice -- enough to read as real
    // convolution without ever pushing a vertex through the mesh's own
    // opposite side (the shallowest hemiRadii axis is 0.62 * radius, an
    // order of magnitude larger than the worst-case displacement).
    private const float FoldStrength = 0.105f;

    /// <summary>The two hemispheres, flattened toward the central
    /// fissure and offset apart to leave it visible as a groove, merged
    /// into ONE mesh sharing one equirectangular UV layout per lobe -- so
    /// one material/texture set reads consistently across the whole
    /// brain mass. No cerebellum lobe (see this file's own class header
    /// for why it was cut). The brainstem is deliberately NOT included
    /// here (see BuildBrainstem below): it's plain flesh-toned geometry,
    /// not brain-surface tissue, so it gets its own simple primitive and
    /// material instead of sharing this mesh's detailed PBR set for no
    /// visual benefit -- and anatomically correct besides, since the
    /// brainstem isn't folded cortex.
    ///
    /// 2026-08: hemisphere proportions are now SLIGHTLY different from
    /// each other (a stylized nod to real petalia -- brain hemispheres
    /// are never quite mirror images even before folding is considered),
    /// on top of each getting its own independent fold-noise seed in
    /// AddFoldedUvSphere.</summary>
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
        var leftRadii = new Vector3(radius * 0.62f, radius * 0.72f, radius * 0.80f);
        var rightRadii = new Vector3(radius * 0.645f, radius * 0.705f, radius * 0.835f);

        AddFoldedUvSphere(verts, uvs, tris,
            new Vector3(-gap - leftRadii.x * 0.35f, radius * 0.05f, 0f), leftRadii,
            LatSegments, LonSegments, LeftFoldSaltBase, -1f);
        AddFoldedUvSphere(verts, uvs, tris,
            new Vector3(gap + rightRadii.x * 0.35f, radius * 0.05f, 0f), rightRadii,
            LatSegments, LonSegments, RightFoldSaltBase, 1f);

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
    /// `radii`, offset to `center`, and displaced along its own per-
    /// vertex radial direction by <see cref="FoldOffset"/> -- real
    /// geometric gyri/sulci, not just a normal map (see this file's own
    /// class header). Wound CORRECTLY BY CONSTRUCTION (per-quad winding
    /// `(a,b,d)`/`(a,d,c)`, hand-verified by cross-product against
    /// Unity's own left-handed convention -- an earlier `(a,d,b)`/
    /// `(a,c,d)` ordering looked plausible but was actually backwards,
    /// caught by a standalone numeric verification harness before this
    /// ever reached the Editor) rather than corrected after the fact by
    /// a shared-centroid pass, since this mesh merges multiple lobes
    /// offset well away from any single shared center -- see this file's
    /// own class header for why ProceduralMeshKit.FaceOutward's
    /// technique doesn't apply here.
    ///
    /// Two things the fold displacement has to get right or the mesh
    /// breaks in a way pure math review alone (no Editor here to render
    /// it) wouldn't necessarily catch, both handled explicitly:
    /// (1) POLES -- north (y==0) and south (y==lat) each collapse every
    /// longitude step to one coincident vertex; if displacement varied
    /// per longitude step there too, those "coincident" vertices would
    /// pull apart into a tiny jittered crown instead of one clean point,
    /// and the degenerate-triangle skip below (needed because
    /// RecalculateNormals chokes on zero-area triangles -- the exact
    /// docs/28 failure mode) would silently stop being valid. `poleFactor`
    /// forces displacement to EXACTLY zero at both poles regardless of
    /// noise value, ramping to full strength within ~1/6 of the latitude
    /// range. (2) THE LONGITUDE SEAM -- x==0 and x==lon sit at the same
    /// pre-displacement 3D position (theta==0 and theta==2*PI) but carry
    /// DIFFERENT UV values (u==0 vs u==1) for the equirectangular unwrap;
    /// sampling fold noise from a raw (u, v) pair would land those two
    /// seam vertex rings on two UNRELATED noise-grid cells and pull them
    /// apart into a visible crack down one meridian. FoldOffset instead
    /// samples from (cos(theta), sin(theta), phi) -- a genuinely periodic
    /// embedding where theta==0 and theta==2*PI are the SAME input by
    /// construction, closing the seam exactly rather than approximately.
    ///
    /// UV is a plain equirectangular unwrap (u = longitude, v =
    /// latitude), good enough for a tileable organic surface texture,
    /// not a claimed seamless/distortion-free unwrap -- unrelated to (and
    /// unaffected by) the geometric seam fix above, which only concerns
    /// vertex POSITIONS, not UVs.</summary>
    private static void AddFoldedUvSphere(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector3 center, Vector3 radii, int lat, int lon, int foldSaltBase, float fissureSign)
    {
        var baseIndex = verts.Count;
        var avgRadius = (radii.x + radii.y + radii.z) / 3f;
        for (var y = 0; y <= lat; y++)
        {
            var v = y / (float)lat;
            var phi = v * Mathf.PI;
            var sinPhi = Mathf.Sin(phi);
            var cosPhi = Mathf.Cos(phi);
            var poleFactor = Mathf.Clamp01(Mathf.Min(v, 1f - v) * 6f);
            for (var x = 0; x <= lon; x++)
            {
                var u = x / (float)lon;
                var theta = u * 2f * Mathf.PI;
                var cosTheta = Mathf.Cos(theta);
                var sinTheta = Mathf.Sin(theta);
                var px = sinPhi * cosTheta;
                var py = cosPhi;
                var pz = sinPhi * sinTheta;

                var offset = 0f;
                if (poleFactor > 0f)
                {
                    var fold = FoldOffset(cosTheta, sinTheta, phi, foldSaltBase);
                    // 2026-08 ("recognizable hemispheres, subtle central
                    // division"): damp folding toward zero on the side
                    // facing the OTHER hemisphere, so noise never bulges
                    // into (and visually closes up) the longitudinal
                    // fissure the `gap`/offset in BuildBrainMass already
                    // carves out.
                    var innerness = Mathf.Clamp01(-fissureSign * px);
                    var fissureDamp = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01((innerness - 0.25f) / 0.5f));
                    offset = fold * FoldStrength * avgRadius * poleFactor * fissureDamp;
                }

                var pos = center + new Vector3(px * radii.x, py * radii.y, pz * radii.z)
                    + new Vector3(px, py, pz) * offset;
                verts.Add(pos);
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

    /// <summary>Multi-octave fold field, roughly in [-1.1, 1.1] (bulges
    /// AND grooves -- unlike BrainTextureKit.Height01, which stays 0..1
    /// for texture-map purposes, this needs to push a vertex outward for
    /// a gyrus and inward for a sulcus). Frequencies are chosen against
    /// this mesh's OWN vertex density (32x28 segments), not
    /// BrainTextureKit's 256px texture -- reusing that file's texture
    /// frequencies here would alias badly (well under 2 vertices per
    /// noise cycle) and read as random jitter instead of folds. Three
    /// bands, different amplitude weights: broad (freq ~2.3, weight 0.5)
    /// gives a couple of wide shallow undulations per hemisphere; medium
    /// (freq ~5.6, weight 0.65) adds mid-scale branching structure;
    /// the FINE band (freq ~10, weight 1.0 -- the largest weight despite
    /// being the narrowest feature) is additionally passed through a
    /// ridged transform (`1 - |2n-1|`, the standard turbulence-to-canyon
    /// trick) instead of staying a plain smooth bump, turning it into
    /// sharp, narrow, comparatively DEEP creases -- "some broad shallow
    /// ridges, some narrow deep creases" is two different amplitude/
    /// sharpness profiles at two different scales, not one noise field
    /// turned up or down.
    ///
    /// Worst-case bound (all three bands simultaneously at their own
    /// extreme, in the same direction -- vanishingly unlikely across
    /// three independent noise fields, but worth knowing the ceiling
    /// of): broad in [-0.25, 0.25], medium in [-0.325, 0.325], ridged in
    /// [-0.5, 0.5] -- sums to [-1.075, 1.075].</summary>
    private static float FoldOffset(float cosTheta, float sinTheta, float phi, int saltBase)
    {
        var broad = ValueNoise3(cosTheta * 2.3f, sinTheta * 2.3f, phi * 2.3f, saltBase) - 0.5f;
        var med = ValueNoise3(cosTheta * 5.6f, sinTheta * 5.6f, phi * 5.6f, saltBase + 1) - 0.5f;
        var fineN = ValueNoise3(cosTheta * 10f, sinTheta * 10f, phi * 10f, saltBase + 2);
        var ridged = (1f - Mathf.Abs(2f * fineN - 1f)) - 0.5f;
        return broad * 0.5f + med * 0.65f + ridged * 1.0f;
    }

    /// <summary>3D counterpart of BrainTextureKit's hash/value-noise pair
    /// -- needed (and used ONLY here) so fold noise can be sampled from a
    /// genuinely periodic (cos(theta), sin(theta), phi) embedding instead
    /// of a raw (u, v) pair that wraps discontinuously at the longitude
    /// seam (see AddFoldedUvSphere's own comment for why that distinction
    /// matters here specifically). Same hash/smoothstep-interpolation
    /// technique as BrainTextureKit.Jitter/ValueNoise, extended to three
    /// axes.</summary>
    private static float Jitter3(int x, int y, int z, int salt)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + z * 2147483647 + salt * 3266489917;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static float ValueNoise3(float x, float y, float z, int salt)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var z0 = Mathf.FloorToInt(z);
        var tx = x - x0;
        var ty = y - y0;
        var tz = z - z0;
        var sx = tx * tx * (3f - 2f * tx);
        var sy = ty * ty * (3f - 2f * ty);
        var sz = tz * tz * (3f - 2f * tz);

        var c000 = Jitter3(x0, y0, z0, salt);
        var c100 = Jitter3(x0 + 1, y0, z0, salt);
        var c010 = Jitter3(x0, y0 + 1, z0, salt);
        var c110 = Jitter3(x0 + 1, y0 + 1, z0, salt);
        var c001 = Jitter3(x0, y0, z0 + 1, salt);
        var c101 = Jitter3(x0 + 1, y0, z0 + 1, salt);
        var c011 = Jitter3(x0, y0 + 1, z0 + 1, salt);
        var c111 = Jitter3(x0 + 1, y0 + 1, z0 + 1, salt);

        var x00 = Mathf.Lerp(c000, c100, sx);
        var x10 = Mathf.Lerp(c010, c110, sx);
        var x01 = Mathf.Lerp(c001, c101, sx);
        var x11 = Mathf.Lerp(c011, c111, sx);
        var y0v = Mathf.Lerp(x00, x10, sy);
        var y1v = Mathf.Lerp(x01, x11, sy);
        return Mathf.Lerp(y0v, y1v, sz);
    }
}
