using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/23 Phase 10.4 (Meshes): "primitive-kit -> authored-mesh swap
/// points. Keep the deterministic dresser PLACEMENT logic; swap
/// CreatePrimitive calls for a PropLibrary lookup (mesh assets by key,
/// with primitive fallback so the game never breaks without assets)."
///
/// Today's registrations are ProceduralMeshKit placeholders (this
/// environment has no Editor/DCC pipeline to author real mesh assets);
/// the lookup-by-key + primitive-fallback SHAPE is the real, permanent
/// infrastructure the plan asks for -- swapping a key's registered
/// builder for a real imported mesh later is a one-line change here,
/// with zero changes needed at any dresser call site.
/// </summary>
public static class PropLibrary
{
    public delegate Mesh MeshBuilder();

    private static readonly Dictionary<string, MeshBuilder> Registry = new Dictionary<string, MeshBuilder>();
    private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

    static PropLibrary()
    {
        Register("ornate-lamppost-pole", () => ProceduralMeshKit.Frustum(1f, 0.55f, 10));
        Register("market-stall-canopy", ProceduralMeshKit.Wedge);
        // 2026-07 (Big Brain jar, replacing a pink-sphere-cluster
        // placeholder): built at a fixed unit radius, same "centered,
        // extends to a normalized size before the caller's own `scale`
        // parameter sizes it" convention Frustum/Wedge already follow --
        // see BrainMesh's own doc comment for the mesh itself.
        Register("big-brain-mass", () => BrainMesh.BuildBrainMass(1f));
        Register("big-brain-stem", () => BrainMesh.BuildBrainstem(12));
    }

    public static void Register(string key, MeshBuilder builder) { Registry[key] = builder; }

    /// <summary>Same call shape as RuntimeCityBuilder.SpawnPrim --
    /// `position` is the object's world center, `scale` its local scale.
    /// Falls back to a plain `fallbackType` primitive if `key` has no
    /// registered mesh (or never gets that far and its builder throws --
    /// the whole point of the fallback).</summary>
    public static GameObject Spawn(RuntimeCityBuilder b, string key, PrimitiveType fallbackType,
        Vector3 position, Vector3 scale, Material mat, Transform parent)
    {
        Mesh mesh;
        if (!Cache.TryGetValue(key, out mesh) || mesh == null)
        {
            MeshBuilder builder;
            if (!Registry.TryGetValue(key, out builder) || builder == null)
                return b.SpawnPrim(fallbackType, position, scale, mat, parent);
            mesh = builder();
            Cache[key] = mesh;
        }

        var go = new GameObject(key);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        // 2026-07 creator-found regression: ProceduralMeshKit switched
        // from double-winding every face (belt-and-braces against a
        // winding mistake, at the cost of cancelling every normal to
        // zero -- see that file's comment) to single, correctly-outward
        // winding. That fixed the normals but reopened exactly the risk
        // double-winding existed to prevent: whether Unity's front-face
        // culling agrees with "outward" as THIS code computes it can't be
        // verified without an Editor, and it turned out to disagree --
        // the props vanished entirely (back-face culled) instead of
        // rendering black. Disable culling on a per-instance CLONE
        // (not the shared cached `mat` -- that material is also used by
        // ordinary, correctly-wound stock primitives elsewhere, which
        // don't need this and shouldn't pay the double-sided fill-rate
        // cost) so these specific meshes render regardless of which way
        // the winding actually landed. Only reached for a REGISTERED
        // mesh -- the primitive fallback path below never hits this.
        var instanceMat = new Material(mat);
        instanceMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        go.AddComponent<MeshRenderer>().material = instanceMat;
        return go;
    }
}
