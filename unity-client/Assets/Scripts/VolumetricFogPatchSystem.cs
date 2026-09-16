using UnityEngine;

/// <summary>
/// docs/40 §3 item 5 follow-up (creator direction 2026-09-16: "add the
/// volumetric fog patches", then "lets implement fluffy volumetric fog
/// bank. replacing the ugly fake ones"). This is NOT the real URP
/// volumetric-fog package docs/28 row 19 evaluated and the creator chose
/// not to integrate for performance -- that decision stands untouched,
/// and a genuine ray-marched volumetric fog Renderer Feature still needs
/// the same Editor-only registration step every other item-5 option does
/// (see docs/40 §3 item 5's own text). This is still the classic cheap
/// approximation, not a real 3D density field or raymarching -- just a
/// better-looking one.
///
/// 2026-09-16 rewrite: the original version stacked 3 flat, hard-edged,
/// UV-less <see cref="ProceduralMeshKit.CloudShard"/> layers per patch --
/// a solid low-poly mesh with a UNIFORM alpha across its surface reads as
/// exactly what it is, a faceted geometric blob, not a soft cloud. That's
/// what the creator meant by "ugly fake." The fix mirrors `RainSystem`'s
/// own proven cure for the identical problem (its streaks/splashes went
/// from "flat-alpha box" to "real UV + soft procedural alpha-gradient
/// texture" on the same creator complaint): each patch is now a CLUSTER
/// of camera-facing billboard quads ("puffs"), each carrying a soft,
/// organically-lumpy alpha texture (several overlapping soft radial
/// lobes unioned together, not one perfect circle) instead of hard mesh
/// geometry. Puffs are laid out taller/wider near a patch's own center
/// and shorter/thinner toward its edges -- a real "bank" silhouette
/// (rises in the middle, tapers at the flanks) instead of a uniform
/// stack. Billboarding (each puff's quad turns to face the camera every
/// frame) makes this look correct at any pan/zoom, unlike the old
/// ground-fixed geometry.
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
    private const int PatchCount = 6;
    private const int PuffsPerPatch = 5;
    private const int TextureVariants = 3;
    // Same "cover the visible frame, don't chase the exact number"
    // margin logic RainSystem/MistSystem's own spawn extents use.
    private const float SpawnHalfExtent = 120f;
    private const float RecenterMargin = 35f;
    private const float MinPatchRadius = 9f, MaxPatchRadius = 16f;

    private struct Puff
    {
        public Transform T;
        public MeshRenderer R;
        public Vector3 LocalOffset;   // relative to the patch's own Center
        public float Scale;
        public float RollDeg;
        public float BaseAlpha;
    }

    private struct Patch
    {
        public Vector3 Center;
        public float Radius;
        public Puff[] Puffs;
    }

    private Patch[] _patches;
    private Mesh _puffMesh;
    private Material[] _matVariants;
    private MaterialPropertyBlock _block;

    private void Awake()
    {
        _puffMesh = BuildPuffQuadMesh();
        _matVariants = new Material[TextureVariants];
        for (var i = 0; i < TextureVariants; i++)
            _matVariants[i] = BuildFogMaterial(BuildFluffyPuffTexture(64, i * 71.3f + 4.2f));
        _block = new MaterialPropertyBlock();

        var center = GroundFocusPoint();
        _patches = new Patch[PatchCount];
        for (var i = 0; i < PatchCount; i++) _patches[i] = BuildPatch(center, i);
    }

    /// <summary>Creates the <see cref="PuffsPerPatch"/> billboard-quad
    /// GameObjects for one patch ONCE -- `Update`'s recenter logic below
    /// only ever moves the same objects, it never calls this again, so a
    /// patch never leaks GameObjects over a long match.</summary>
    private Patch BuildPatch(Vector3 areaCenter, int index)
    {
        var patch = new Patch
        {
            Center = areaCenter + new Vector3(
                Random.Range(-SpawnHalfExtent, SpawnHalfExtent), 0f,
                Random.Range(-SpawnHalfExtent, SpawnHalfExtent)),
            Radius = Random.Range(MinPatchRadius, MaxPatchRadius),
            Puffs = new Puff[PuffsPerPatch],
        };

        for (var j = 0; j < PuffsPerPatch; j++)
        {
            var go = new GameObject("FogPuff");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _puffMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _matVariants[(index * PuffsPerPatch + j) % TextureVariants];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            patch.Puffs[j] = BuildPuffLayout(go.transform, renderer, patch.Radius, j);
        }

        RepositionPuffs(ref patch);
        return patch;
    }

    /// <summary>Rolls one puff's LOCAL layout (offset/scale/roll/alpha)
    /// relative to its patch center -- taller and wider near the middle
    /// of the spread (`centerFactor` near 1), shorter and thinner toward
    /// the flanks, which is what actually reads as a "bank" silhouette
    /// instead of a uniform row of identical puffs.</summary>
    private static Puff BuildPuffLayout(Transform t, MeshRenderer r, float radius, int index)
    {
        var spanT = PuffsPerPatch <= 1 ? 0f : index / (float)(PuffsPerPatch - 1) * 2f - 1f; // -1..1
        var jitterX = Random.Range(-0.18f, 0.18f);
        var offsetX = (spanT + jitterX) * radius * 0.9f;
        var offsetZ = Random.Range(-0.3f, 0.3f) * radius;
        var centerFactor = 1f - Mathf.Clamp01(Mathf.Abs(spanT));

        var height = Mathf.Lerp(radius * 0.3f, radius * 0.8f, centerFactor) * Random.Range(0.85f, 1.15f);
        var scale = Mathf.Lerp(radius * 1.5f, radius * 2.6f, centerFactor) * Random.Range(0.9f, 1.1f);

        return new Puff
        {
            T = t,
            R = r,
            LocalOffset = new Vector3(offsetX, height, offsetZ),
            Scale = scale,
            RollDeg = Random.Range(0f, 360f),
            BaseAlpha = Random.Range(0.16f, 0.26f),
        };
    }

    /// <summary>Places every puff's world position/scale from its
    /// already-rolled local layout plus the patch's current `Center` --
    /// shared by both initial construction and the recenter path in
    /// `Update`. Rotation is NOT set here -- that's `Update`'s per-frame
    /// billboard job, since it depends on the live camera position.</summary>
    private static void RepositionPuffs(ref Patch patch)
    {
        for (var j = 0; j < patch.Puffs.Length; j++)
        {
            var p = patch.Puffs[j];
            p.T.position = patch.Center + p.LocalOffset;
            p.T.localScale = new Vector3(p.Scale, p.Scale, p.Scale);
        }
    }

    private void Update()
    {
        // docs/39 §9: "No effect renders in the Map band."
        if (AnimationLodBudget.CurrentBand == AnimationLodBudget.Band.Map)
        {
            for (var i = 0; i < _patches.Length; i++)
                foreach (var puff in _patches[i].Puffs) puff.R.enabled = false;
            return;
        }

        // Fog and rain read as the same weather system -- thick at
        // night regardless of rain, with rain adding a smaller boost of
        // its own (both contributions capped before summing so heavy
        // rain at night can't push past a sane ceiling).
        var visibility = Mathf.Clamp01(DayNightState.NightAmount * 0.85f + WeatherController.Wetness * 0.4f);
        var center = GroundFocusPoint();
        var cam = Camera.main;

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
                for (var j = 0; j < p.Puffs.Length; j++)
                    p.Puffs[j] = BuildPuffLayout(p.Puffs[j].T, p.Puffs[j].R, p.Radius, j);
                RepositionPuffs(ref p);
                _patches[i] = p;
            }

            for (var j = 0; j < p.Puffs.Length; j++)
            {
                var puff = p.Puffs[j];
                var alpha = puff.BaseAlpha * visibility;
                puff.R.enabled = alpha > 0.004f;
                if (!puff.R.enabled) continue;

                if (cam != null)
                {
                    var toCam = cam.transform.position - puff.T.position;
                    if (toCam.sqrMagnitude > 0.0001f)
                        puff.T.rotation = Quaternion.LookRotation(toCam, Vector3.up) * Quaternion.Euler(0f, 0f, puff.RollDeg);
                }

                _block.SetColor("_BaseColor", new Color(0.75f, 0.78f, 0.8f, alpha));
                puff.R.SetPropertyBlock(_block);
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

    /// <summary>A flat unit quad in the local XY plane (-0.5..0.5, normal
    /// along +Z) with standard corner UVs -- the billboard card every
    /// puff shares. `Update` rotates each instance so local +Z points at
    /// the camera every frame (see <see cref="Quaternion.LookRotation"/>
    /// call above), which is what actually turns a flat card into
    /// something that reads as a soft 3D puff from any pan/zoom angle.</summary>
    private static Mesh BuildPuffQuadMesh()
    {
        var positions = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
        };
        var triangles = new[] { 0, 1, 2, 0, 2, 3 };
        var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        var mesh = new Mesh { vertices = positions, triangles = triangles, uv = uvs };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>64x64 soft alpha puff (RGB white -- `mat.color` tints):
    /// several overlapping soft-edged radial lobes (`SmoothStep` falloff
    /// each) unioned together via `Mathf.Max`, instead of one perfect
    /// circle -- the union of a few offset soft circles reads as a lumpy,
    /// organic cloud silhouette, which one centered gradient never could
    /// no matter how soft its single edge was. This IS the fix for the
    /// "ugly fake" complaint: the old system had zero alpha variation
    /// across a hard mesh surface; this has a genuinely wispy, non-
    /// circular soft edge baked into the texture itself. Built once per
    /// variant at Awake and shared by every puff using that variant --
    /// same "one static procedural texture, many instances" idiom
    /// `RainSystem.BuildStreakGradientTexture`/`BuildRippleTexture`
    /// already established for the identical hard-edge complaint.</summary>
    private static Texture2D BuildFluffyPuffTexture(int size, float seed)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        const int lobes = 5;
        var lobeX = new float[lobes];
        var lobeY = new float[lobes];
        var lobeR = new float[lobes];
        var rng = new System.Random((int)(seed * 1000f));
        for (var i = 0; i < lobes; i++)
        {
            var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            var dist = (float)rng.NextDouble() * 0.24f;
            lobeX[i] = 0.5f + Mathf.Sin(angle) * dist;
            lobeY[i] = 0.5f + Mathf.Cos(angle) * dist;
            lobeR[i] = 0.24f + (float)rng.NextDouble() * 0.16f;
        }

        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = (x + 0.5f) / size;
            var v = (y + 0.5f) / size;
            var best = 0f;
            for (var i = 0; i < lobes; i++)
            {
                var dist = Vector2.Distance(new Vector2(u, v), new Vector2(lobeX[i], lobeY[i]));
                var lobeAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lobeR[i] * 0.5f, lobeR[i], dist));
                if (lobeAlpha > best) best = lobeAlpha;
            }
            var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(best) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    private static Material BuildFogMaterial(Texture2D puffTexture)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.75f, 0.78f, 0.8f, 0.2f);
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", puffTexture);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return mat;
    }
}
