using MadDr.CreatureMesh;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns a CreatureMeshResult (the engine-agnostic port of the Lab's
/// creature renderer, packages/creature-mesh) into live Unity geometry.
/// The creature body (<see cref="Attach"/>/<see cref="AttachLodded"/>)
/// merges chunks into 2-3 renderers per docs/39 §11 item 2
/// (<see cref="AttachChunksMerged"/>'s own header comment); the raw
/// chunk-list path (<see cref="AttachChunks"/>, one GameObject+Renderer
/// per material chunk, URP/Lit materials mapped from each chunk's own
/// color/gloss/emissive/alpha) still backs legs/wings, which are few
/// chunks each and independently positioned by the gait rig. This is
/// the in-game monster regenerator's display half -- the DNA the Lab
/// generates becomes the same stitched b-movie body on the battlefield.
/// </summary>
public static class LabMeshBuilder
{
    /// <summary>Builds the chunks under `parent` at `localPos`, uniformly
    /// scaled by `scale` (lab units to world units). Merged per docs/39
    /// §11 item 2 (<see cref="AttachChunksMerged"/>) -- 2-3 renderers,
    /// not one per material chunk. Returns the holder so callers can
    /// strip or restyle it later.</summary>
    public static Transform Attach(CreatureMeshResult lab, Transform parent, Vector3 localPos, float scale)
    {
        var holder = AttachChunksMerged(lab.Chunks, parent, "LabBody", scale);
        holder.localPosition = localPos;
        return holder;
    }

    /// <summary>docs/39 SS11 item 1 / SS5.1: standard-monster LOD
    /// thresholds (screen-height fraction of the object's own bounds) --
    /// LOD0 while >= 6% (camera height below ~25 m), LOD1 while >= 1.5%
    /// (below ~110 m, the default-zoom band), LOD2 while >= 0.6% (below
    /// ~250 m), culled entirely below that (SS5.3 option 1: zero render
    /// cost, the minimap blip carries the information). Not yet split by
    /// hero vs. standard tier (SS5.1's own hero row is more generous) --
    /// every monster uses the standard-tier thresholds for now; a
    /// hero/mastermind distinction is a follow-up, not this item's job.</summary>
    public static readonly float[] StandardMonsterLodScreenHeights = { 0.06f, 0.015f, 0.006f };

    /// <summary>Same as <see cref="Attach"/> but for three pre-built LODs
    /// of the SAME creature (docs/39 SS11 item 1: Detail = 1 / 0.55 / 0.3),
    /// wired into one <see cref="LODGroup"/> so Unity swaps between them
    /// by the object's own on-screen size instead of rendering full detail
    /// at every distance. `lodLabs` must have exactly 3 entries, LOD0
    /// first. Leg/Wing sockets and framing heights are geometry-position
    /// data, not tessellation-resolution data, so only `lodLabs[0]`'s are
    /// meaningful -- callers should keep reading those off the LOD0
    /// result, not this method's return value.</summary>
    public static Transform AttachLodded(System.Collections.Generic.IReadOnlyList<CreatureMeshResult> lodLabs,
        Transform parent, Vector3 localPos, float scale, float[] screenHeights)
    {
        if (lodLabs.Count != screenHeights.Length)
            throw new System.ArgumentException(
                $"AttachLodded needs one screen-height threshold per LOD ({lodLabs.Count} lods, {screenHeights.Length} thresholds)");

        var holder = new GameObject("LabBody").transform;
        holder.SetParent(parent, false);
        holder.localPosition = localPos;

        var lods = new LOD[lodLabs.Count];
        for (var i = 0; i < lodLabs.Count; i++)
        {
            // docs/39 §11 item 2: merged (2-3 renderers), not one
            // GameObject+Renderer per material chunk (12-23) -- see
            // AttachChunksMerged's own header comment.
            var lodHolder = AttachChunksMerged(lodLabs[i].Chunks, holder, "LOD" + i, scale);
            lods[i] = new LOD(screenHeights[i], lodHolder.GetComponentsInChildren<Renderer>());
        }

        var group = holder.gameObject.AddComponent<LODGroup>();
        // mass units pop between LODs rather than cross-fading (docs/39
        // SS5.1: "Use LODGroup cross-fade only on hero units; mass units
        // pop, and at 26 px nobody sees a pop") -- LODFadeMode.None is
        // the LODGroup default, set explicitly so this doesn't silently
        // change if that default ever does.
        group.fadeMode = LODFadeMode.None;
        group.SetLODs(lods);
        group.RecalculateBounds();
        return holder;
    }

    /// <summary>Same conversion for a raw chunk list -- leg-kit pieces
    /// (hip hardware, upper/lower segments, feet) that the gait rig
    /// positions itself.</summary>
    public static Transform AttachChunks(System.Collections.Generic.IReadOnlyList<MeshChunk> chunks,
        Transform parent, string name, float scale)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localScale = Vector3.one * scale;
        foreach (var chunk in chunks)
        {
            if (chunk.Triangles.Count == 0) continue;
            var go = new GameObject("Chunk");
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(chunk);
            go.AddComponent<MeshRenderer>().sharedMaterial = ToMaterial(chunk);
        }
        return holder;
    }

    // Emission strength baked into the shared emissive-group material
    // (CreatureVertexColor.shader's _EmissionStrength). One fixed value
    // for every creature's emissive chunks, chosen to split the
    // difference of the real range observed across a busy genome's own
    // chunks (0.30-1.00, dotnet test diagnostic 2026-09-15) -- the
    // per-chunk Emissive field still exists in creature-mesh and still
    // varies; this is the one number this shared material can't
    // reproduce individually any more (docs/39 §11 item 2's documented
    // trade, same "cheaper read at 70 m" logic as the rest of docs/39).
    private const float SharedEmissiveStrength = 0.6f;

    private static Material _sharedOpaqueVertexColorMat;
    private static Material _sharedEmissiveVertexColorMat;

    private static Material SharedVertexColorMaterial(bool emissiveGroup)
    {
        if (emissiveGroup && _sharedEmissiveVertexColorMat != null) return _sharedEmissiveVertexColorMat;
        if (!emissiveGroup && _sharedOpaqueVertexColorMat != null) return _sharedOpaqueVertexColorMat;

        var shader = Shader.Find("MadDr/CreatureVertexColor");
        if (shader == null)
        {
            // Falls back to a renderable shader so a creature still
            // shows SOMETHING (flat, no per-vertex hue) rather than an
            // invisible/pink mesh if the custom shader somehow isn't in
            // the build -- loud in the console either way.
            Debug.LogError("MadDr/CreatureVertexColor shader not found -- creature bodies will render without per-chunk color. Check Assets/Shaders/CreatureVertexColor.shader is imported.");
            shader = ShaderUtil.FindRenderableShader();
        }
        var mat = new Material(shader);
        if (mat.HasProperty("_EmissionStrength"))
            mat.SetFloat("_EmissionStrength", emissiveGroup ? SharedEmissiveStrength : 0f);

        if (emissiveGroup) _sharedEmissiveVertexColorMat = mat;
        else _sharedOpaqueVertexColorMat = mat;
        return mat;
    }

    /// <summary>docs/39 §11 item 2: merges a creature's per-material
    /// chunks into as few renderers as the doc's own opaque/translucent
    /// split allows, instead of one Chunk GameObject+Renderer per
    /// distinct (color, gloss, emissive, alpha) chunk (12-23 of them per
    /// creature, docs/39 §4.1's measured baseline). Every fully-opaque
    /// non-emissive chunk merges into ONE mesh sharing ONE material
    /// (CreatureVertexColor.shader bakes each chunk's flat color into
    /// per-vertex color, since URP/Lit itself has no vertex-color
    /// input); every emissive-but-opaque chunk (eyes, neon, heart bolts)
    /// merges into a SECOND such mesh/material; any translucent chunk
    /// (Alpha &lt; 0.99 -- the mastermind's glass dome, the blob's
    /// gelatin shell) is kept exactly as before, one small unmerged
    /// renderer per chunk via the original <see cref="ToMaterial"/> path
    /// -- docs/39 item 2's own explicit exception ("the translucent
    /// blob shell stays a second renderer"). Typical result: 2-3
    /// renderers instead of 12-23.</summary>
    public static Transform AttachChunksMerged(System.Collections.Generic.IReadOnlyList<MeshChunk> chunks,
        Transform parent, string name, float scale)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localScale = Vector3.one * scale;

        var opaque = new System.Collections.Generic.List<MeshChunk>();
        var emissive = new System.Collections.Generic.List<MeshChunk>();
        foreach (var chunk in chunks)
        {
            if (chunk.Triangles.Count == 0) continue;
            if (chunk.Alpha < 0.99)
            {
                var go = new GameObject("Translucent");
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = ToMesh(chunk);
                go.AddComponent<MeshRenderer>().sharedMaterial = ToMaterial(chunk);
                continue;
            }
            (chunk.Emissive > 0.01 ? emissive : opaque).Add(chunk);
        }

        if (opaque.Count > 0) AttachMergedGroup(opaque, holder, "Opaque", SharedVertexColorMaterial(false));
        if (emissive.Count > 0) AttachMergedGroup(emissive, holder, "Emissive", SharedVertexColorMaterial(true));

        return holder;
    }

    private static void AttachMergedGroup(System.Collections.Generic.IReadOnlyList<MeshChunk> group,
        Transform parent, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = ToMergedMesh(group);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    /// <summary>Concatenates every chunk in the group into one Mesh,
    /// index-offsetting each chunk's triangles by the running vertex
    /// count and baking each chunk's own flat Color into every one of
    /// its vertices' vertex-color channel -- the only thing that lets
    /// chunks of DIFFERENT colors share one mesh/material.</summary>
    private static Mesh ToMergedMesh(System.Collections.Generic.IReadOnlyList<MeshChunk> group)
    {
        var totalVerts = 0;
        var totalTris = 0;
        foreach (var c in group) { totalVerts += c.VertexCount; totalTris += c.Triangles.Count; }

        var verts = new Vector3[totalVerts];
        var norms = new Vector3[totalVerts];
        var colors = new Color[totalVerts];
        var tris = new int[totalTris];

        var vOff = 0;
        var tOff = 0;
        foreach (var c in group)
        {
            var count = c.VertexCount;
            var col = new Color((float)c.Color.R / 255f, (float)c.Color.G / 255f, (float)c.Color.B / 255f, 1f);
            for (var i = 0; i < count; i++)
            {
                verts[vOff + i] = new Vector3((float)c.Positions[i * 3],
                    (float)c.Positions[i * 3 + 1], (float)c.Positions[i * 3 + 2]);
                norms[vOff + i] = new Vector3((float)c.Normals[i * 3],
                    (float)c.Normals[i * 3 + 1], (float)c.Normals[i * 3 + 2]);
                colors[vOff + i] = col;
            }
            for (var i = 0; i < c.Triangles.Count; i++) tris[tOff + i] = c.Triangles[i] + vOff;
            vOff += count;
            tOff += c.Triangles.Count;
        }

        var mesh = new Mesh();
        if (totalVerts > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh ToMesh(MeshChunk chunk)
    {
        var mesh = new Mesh();
        var count = chunk.VertexCount;
        if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        var verts = new Vector3[count];
        var norms = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            verts[i] = new Vector3((float)chunk.Positions[i * 3],
                (float)chunk.Positions[i * 3 + 1], (float)chunk.Positions[i * 3 + 2]);
            norms[i] = new Vector3((float)chunk.Normals[i * 3],
                (float)chunk.Normals[i * 3 + 1], (float)chunk.Normals[i * 3 + 2]);
        }
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.triangles = chunk.Triangles.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material ToMaterial(MeshChunk chunk)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        var col = new Color((float)chunk.Color.R / 255f, (float)chunk.Color.G / 255f,
            (float)chunk.Color.B / 255f, (float)chunk.Alpha);
        mat.color = col;

        // gloss -> URP smoothness (Standard shares the property name in
        // newer Unity; harmless no-op where absent)
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", (float)chunk.Gloss);
        else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", (float)chunk.Gloss);

        if (chunk.Emissive > 0.01)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(col.r, col.g, col.b) * (float)chunk.Emissive * 2f);
        }

        if (chunk.Alpha < 0.99) MakeTransparent(mat);
        return mat;
    }

    /// <summary>Best-effort transparency for the mastermind's glass dome.
    /// URP/Lit needs the full surface-type dance; if a fallback shader
    /// ignores these the dome renders opaque, which was the declared
    /// acceptable v1 degradation (the brass collar and ribs still sell
    /// the structure). Public: the battlefield's water surfaces use the
    /// same dance (docs/21).</summary>
    public static void MakeTransparent(Material mat)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // URP: transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // alpha blend
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }
}
