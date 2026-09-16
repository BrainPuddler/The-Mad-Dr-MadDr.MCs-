using UnityEngine;

/// <summary>
/// docs/40 §3 item 5 follow-up (creator direction 2026-09-16: "add the
/// volumetric fog patches"). This is NOT the real URP volumetric-fog
/// package docs/28 row 19 evaluated and the creator chose not to
/// integrate for performance -- that decision stands untouched, and a
/// genuine ray-marched volumetric fog Renderer Feature still needs the
/// same Editor-only registration step every other item-5 option does
/// (see docs/40 §3 item 5's own text). This is the classic cheap
/// approximation instead: a handful of STATIONARY ground-fog patches,
/// each a short stack of 3 flat, soft, translucent <see
/// cref="ProceduralMeshKit.CloudShard"/> layers -- wide and relatively
/// opaque near the ground, smaller and fainter with height -- the
/// standard "billboard stack" trick for faking a fog volume's silhouette
/// without any real 3D density field or raymarching.
///
/// Intensity tracks `DayNightState.NightAmount` (thick at night, gone
/// by day -- matching docs/39 §2's own "atmospheric depth hides the far
/// LOD" reference) plus a smaller boost from <see
/// cref="WeatherController.Wetness"/> (fog and rain read as the same
/// weather system here, not two unrelated ones).
///
/// Patches are STATIONARY, unlike <see cref="MistSystem"/>'s drifting
/// clumps -- real ground fog collects and sits, it doesn't cruise
/// across the block -- and simply REPOSITION (not respawn -- the same
/// GameObjects move, nothing is destroyed/recreated) around the
/// camera's ground focus when the camera wanders far enough away.
/// Scattered around the visible area rather than tied to actual
/// low-lying terrain: this environment has no simple terrain-height
/// query available from a standalone MonoBehaviour, so this is an
/// honest simplification, not a claim of terrain-aware placement.
/// </summary>
public class VolumetricFogPatchSystem : MonoBehaviour
{
    private const int PatchCount = 8;
    private const int MeshVariants = 3;
    // Same "cover the visible frame, don't chase the exact number"
    // margin logic RainSystem/MistSystem's own spawn extents use.
    private const float SpawnHalfExtent = 120f;
    private const float RecenterMargin = 35f;
    private const float MinPatchRadius = 9f, MaxPatchRadius = 16f;

    // Three layers per patch: height above ground, radius relative to
    // the patch's own base radius, and base alpha before the live
    // night/wetness multiplier -- wide+opaque near the ground, smaller
    // and fainter as it rises, which is the whole "reads as a volume,
    // not a flat decal" trick.
    private static readonly float[] LayerHeight = { 0.3f, 1.4f, 2.8f };
    private static readonly float[] LayerRadiusScale = { 1f, 0.75f, 0.5f };
    private static readonly float[] LayerBaseAlpha = { 0.32f, 0.18f, 0.07f };

    private struct Patch
    {
        public Vector3 Center;
        public float Radius;
        public MeshRenderer[] Layers;
    }

    private Patch[] _patches;
    private Mesh[] _meshVariants;
    private Material _sharedMat;
    private MaterialPropertyBlock _block;

    private void Awake()
    {
        _meshVariants = new Mesh[MeshVariants];
        for (var i = 0; i < MeshVariants; i++)
            _meshVariants[i] = ProceduralMeshKit.CloudShard(6, i * 5.19f + 1.7f);
        _sharedMat = BuildFogMaterial();
        _block = new MaterialPropertyBlock();

        var center = GroundFocusPoint();
        _patches = new Patch[PatchCount];
        for (var i = 0; i < PatchCount; i++) _patches[i] = BuildPatch(center, i);
    }

    /// <summary>Creates the 3 layer GameObjects for one patch ONCE --
    /// `Update`'s recenter logic below only ever moves/rescales these
    /// same objects, it never calls this again, so a patch never leaks
    /// GameObjects over a long match.</summary>
    private Patch BuildPatch(Vector3 areaCenter, int index)
    {
        var patch = new Patch
        {
            Center = areaCenter + new Vector3(
                Random.Range(-SpawnHalfExtent, SpawnHalfExtent), 0f,
                Random.Range(-SpawnHalfExtent, SpawnHalfExtent)),
            Radius = Random.Range(MinPatchRadius, MaxPatchRadius),
            Layers = new MeshRenderer[LayerHeight.Length],
        };

        for (var l = 0; l < LayerHeight.Length; l++)
        {
            var go = new GameObject("FogPatchLayer");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _meshVariants[(index + l) % MeshVariants];
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _sharedMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            patch.Layers[l] = renderer;
        }

        PlacePatch(ref patch);
        return patch;
    }

    /// <summary>Moves/rescales a patch's already-existing layer
    /// GameObjects to match its current `Center`/`Radius` -- shared by
    /// both initial construction and the recenter path in `Update`.</summary>
    private static void PlacePatch(ref Patch patch)
    {
        for (var l = 0; l < LayerHeight.Length; l++)
        {
            var scale = patch.Radius * 2f * LayerRadiusScale[l];
            var t = patch.Layers[l].transform;
            t.position = patch.Center + Vector3.up * LayerHeight[l];
            t.localScale = new Vector3(scale, scale * 0.35f, scale);
        }
    }

    private void Update()
    {
        // docs/39 §9: "No effect renders in the Map band."
        if (AnimationLodBudget.CurrentBand == AnimationLodBudget.Band.Map)
        {
            for (var i = 0; i < _patches.Length; i++)
                foreach (var r in _patches[i].Layers) r.enabled = false;
            return;
        }

        // Fog and rain read as the same weather system -- thick at
        // night regardless of rain, with rain adding a smaller boost of
        // its own (both contributions capped before summing so heavy
        // rain at night can't push past a sane ceiling).
        var visibility = Mathf.Clamp01(DayNightState.NightAmount * 0.85f + WeatherController.Wetness * 0.4f);
        var center = GroundFocusPoint();

        for (var i = 0; i < _patches.Length; i++)
        {
            var p = _patches[i];
            var delta = p.Center - center;
            var flatDist = new Vector2(delta.x, delta.z).magnitude;
            if (flatDist > SpawnHalfExtent + RecenterMargin)
            {
                p.Center = center + new Vector3(
                    Random.Range(-SpawnHalfExtent, SpawnHalfExtent), 0f,
                    Random.Range(-SpawnHalfExtent, SpawnHalfExtent));
                p.Radius = Random.Range(MinPatchRadius, MaxPatchRadius);
                PlacePatch(ref p);
                _patches[i] = p;
            }

            for (var l = 0; l < p.Layers.Length; l++)
            {
                var alpha = LayerBaseAlpha[l] * visibility;
                var renderer = p.Layers[l];
                renderer.enabled = alpha > 0.004f;
                if (!renderer.enabled) continue;
                _block.SetColor("_BaseColor", new Color(0.75f, 0.78f, 0.8f, alpha));
                renderer.SetPropertyBlock(_block);
            }
        }
    }

    /// <summary>Same ray-plane-against-y=0 technique as `RainSystem`'s/
    /// `MistSystem`'s own copies -- kept independent per this project's
    /// existing tolerance for a few duplicated lines between sibling
    /// VFX files over a forced shared utility.</summary>
    private static Vector3 GroundFocusPoint()
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        var origin = cam.transform.position;
        var dir = cam.transform.forward;
        if (Mathf.Abs(dir.y) < 0.0001f) return origin;
        var t = -origin.y / dir.y;
        return origin + dir * Mathf.Max(0f, t);
    }

    private static Material BuildFogMaterial()
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.75f, 0.78f, 0.8f, 0.2f);
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return mat;
    }
}
