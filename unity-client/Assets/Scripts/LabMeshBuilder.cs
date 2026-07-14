using MadDr.CreatureMesh;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns a CreatureMeshResult (the engine-agnostic port of the Lab's
/// creature renderer, packages/creature-mesh) into live Unity geometry:
/// one child GameObject per material chunk, meshes built straight from
/// the chunk's positions/normals/triangles, URP/Lit materials mapped
/// from the chunk's color/gloss/emissive/alpha. This is the in-game
/// monster regenerator's display half -- the DNA the Lab generates
/// becomes the same stitched b-movie body on the battlefield.
/// </summary>
public static class LabMeshBuilder
{
    /// <summary>Builds the chunks under `parent` at `localPos`, uniformly
    /// scaled by `scale` (lab units to world units). Returns the holder
    /// so callers can strip or restyle it later.</summary>
    public static Transform Attach(CreatureMeshResult lab, Transform parent, Vector3 localPos, float scale)
    {
        var holder = AttachChunks(lab.Chunks, parent, "LabBody", scale);
        holder.localPosition = localPos;
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
    /// the structure).</summary>
    private static void MakeTransparent(Material mat)
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
