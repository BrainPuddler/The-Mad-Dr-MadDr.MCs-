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
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
