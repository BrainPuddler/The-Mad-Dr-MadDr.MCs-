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
    // 2026-08 perf (Tier 0 of the graphics-upgrade plan, docs/12): one
    // double-sided clone PER SOURCE MATERIAL, not per Spawn call. Every
    // facade module spawned with the same source `mat` (BuildingDresser's
    // ~21 cached materials) now shares one Material instance, so Unity's
    // SRP batcher can still batch them -- the old per-call clone minted a
    // brand-new instance every single Spawn, defeating batching by
    // construction on the newest prop path.
    private static readonly Dictionary<Material, Material> DoubleSidedCache = new Dictionary<Material, Material>();

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
        // 2026-08 (per-faction Factory/Control Centre "Major Improvement"
        // pass): a narrow-tipped spike for the Alien faction's
        // crystalline growths/antennae (6 segments -- fewer than the
        // lamppost pole's 10, deliberately faceted rather than smooth,
        // reading as "crystal" instead of "turned metal"), and a wide
        // gentler taper for the Human Alliance's cooling towers.
        Register("alien-crystal-spike", () => ProceduralMeshKit.Frustum(1f, 0.15f, 6));
        Register("human-cooling-tower", () => ProceduralMeshKit.Frustum(1f, 0.7f, 12));
        // 2026-08 (faction gauntlet, docs/31 §5, "1950s-70s B-movie
        // mechanical apparatus... horn antennas"): a wide-mouth funnel,
        // the inverse taper of alien-crystal-spike (wide end forward,
        // narrow throat at the mount) -- same Frustum generator, a
        // different pair of radii is the whole difference.
        Register("alien-horn-antenna", () => ProceduralMeshKit.Frustum(1f, 0.35f, 10));
        // 2026-08 (faction gauntlet, docs/31 §7 Phase 3, "massive stone
        // construction, towers"): a witch-hat conical tower cap -- wide
        // base, narrow-almost-to-a-point top, same Frustum generator as
        // every other tapered prop here.
        Register("castle-tower-cap", () => ProceduralMeshKit.Frustum(1f, 0.03f, 12));
        // 2026-08 (faction gauntlet, docs/31 §7 Phase 3, "buttresses"):
        // a SEPARATE cache entry from "market-stall-canopy" even though
        // both use the same ProceduralMeshKit.Wedge generator -- the
        // mesh itself is identical (a lean-to wedge), it's the SCALE and
        // MOUNTING (flush-back against a wall, sloped face outward) that
        // makes it read as a buttress instead of an awning, not a
        // different mesh, so no new generator is needed.
        Register("castle-buttress", ProceduralMeshKit.Wedge);
        // 2026-08 (faction gauntlet addendum, docs/31 §6, "Larger Alien
        // buildings... connect 2+ saucer modules via a passage-tube
        // connector -- a Frustum-style tapered cylinder with visible
        // docking-collar rings"): a gentle taper (0.85 top:bottom, not a
        // straight cylinder) so the tube itself reads as organic/grown
        // rather than a machined pipe, matching the rest of this
        // faction's crystal-taper vocabulary.
        Register("alien-passage-tube", () => ProceduralMeshKit.Frustum(1f, 0.85f, 12));
        // 2026-08 (creator direction: "Streetlight should cast moody
        // volumetric light rays"): a wide-based, narrow-topped cone --
        // the light-shaft silhouette hanging from a streetlamp bulb down
        // to its own pool of light on the road. Low segment count (8) --
        // this is a translucent haze shape seen from a distance, not a
        // hard-edged prop that needs a smooth profile.
        Register("streetlight-beam", () => ProceduralMeshKit.Frustum(1f, 0.15f, 8));

        // 2026-08 docs/30 (facade grammar): the module mesh-swap points.
        // Deliberately left UNREGISTERED -- FacadeKit spawns each of these
        // through PropLibrary.Spawn with a primitive fallback type, and an
        // unregistered key takes exactly that fallback path (see Spawn's
        // own contract below). That is the whole design: every facade
        // module already routes through the asset seam, so authoring a
        // real mesh later is a one-line Register(...) here per key, with
        // zero changes at any FacadeKit call site and zero changes to the
        // solver in citygen-core.
        //
        // The keys FacadeKit asks for, for whoever authors them:
        //   facade-shopfront, facade-entrance-recessed, facade-entrance-stoop,
        //   facade-loading-dock, facade-window-bay, facade-oriel-bay,
        //   facade-fire-escape, facade-cornice, facade-parapet
        //
        // Registering a placeholder ProceduralMeshKit shape for each would
        // be worse than the primitive fallback, not better: a hand-authored
        // stand-in that only approximates a cornice reads as a wrong mesh,
        // whereas the primitive reads as an honest block. "Flag, don't
        // fake" -- this project's own standing rule.
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
        go.AddComponent<MeshRenderer>().sharedMaterial = GetDoubleSidedVariant(mat);
        return go;
    }

    private static Material GetDoubleSidedVariant(Material mat)
    {
        Material variant;
        if (DoubleSidedCache.TryGetValue(mat, out variant) && variant != null) return variant;
        variant = new Material(mat);
        variant.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        DoubleSidedCache[mat] = variant;
        return variant;
    }
}
