using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/40 §3 item 2: ambient falling-rain streaks + ground splashes,
/// gated entirely by <see cref="WeatherController.Wetness"/> (item 1) --
/// this file owns no weather state of its own, it's a pure visual
/// consumer of that value, same "one shared toggle, several visual
/// systems react to it" shape the day/night phase already has with
/// `LumenCycleController`/`NeonRegistry`/`DynamicLightBudget`.
///
/// GPU-instanced via `Graphics.DrawMeshInstanced`, the same technique
/// `LowPolyFireSystem` already ships for exterior fire (its own doc
/// header names the API correctly; an earlier draft of docs/40 called it
/// `Graphics.RenderMeshInstanced`, a different, newer overload this
/// codebase doesn't use -- corrected there once this file was written
/// against the real precedent instead of the doc's own paraphrase of
/// it). One shared streak mesh + one shared splash mesh, each with one
/// shared material -- matrices are the only per-instance data, batched
/// in ≤1023-instance chunks (the hard per-call cap the same precedent
/// already works around via `FlushBucket`).
///
/// Deliberately does NOT sample `GlowPointRegistry` per splash to tint
/// individual puddles toward nearby lamp colors -- that's docs/40 §3
/// item 4's own job ("fake-reflection puddle decals AT PLAZAS," hand-
/// placed and lamp-aware). This file's splashes are a cheap, generic,
/// citywide ambient effect (a fixed cool-toned emissive tint reads as
/// "catching some light" without querying anything), same distinction
/// docs/39 §9 already draws between ambient plumes and a hero VFX
/// instance.
///
/// Rain streaks fall perfectly vertically -- no wind lean, unlike
/// `LowPolyFireSystem`'s flames -- since nothing asked for weather wind
/// and adding one now would be scope no one requested.
/// </summary>
public class RainSystem : MonoBehaviour
{
    // docs/39 §1.1: the visible ground at the default 70 m zoom is
    // roughly 190x180 m -- this spawn square is sized generously past
    // that (with margin for the Overview band, the widest band this
    // system still renders in) rather than tuned to the exact number,
    // since it only needs to always cover the frame, not be minimal.
    private const float SpawnHalfExtent = 140f;
    private const float SpawnTopHeight = 45f;   // clears every building tier (docs/18: tallest is 40 m)

    private const int MaxStreaks = 260;
    private const float StreakWidth = 0.05f;
    private const float StreakLengthMin = 1.1f, StreakLengthMax = 1.9f;
    private const float FallSpeedMin = 16f, FallSpeedMax = 24f;

    private const int MaxSplashes = 40;
    private const float SplashLifeSeconds = 0.35f;
    private const float SplashMaxRadius = 0.55f;
    private const float SplashHeight = 0.02f;

    private float[] _streakX, _streakZ, _streakY, _streakSpeed, _streakLen;
    private float[] _splashX, _splashZ, _splashAge;
    private bool[] _splashActive;

    private Mesh _boxMesh;
    private Material _streakMat, _splashMat;
    private readonly List<Matrix4x4> _streakMatrices = new List<Matrix4x4>();
    private readonly List<Matrix4x4> _splashMatrices = new List<Matrix4x4>();
    private readonly Matrix4x4[] _drawScratch = new Matrix4x4[1023];

    private void Awake()
    {
        _boxMesh = BuildBoxMesh();
        _streakMat = BuildStreakMaterial();
        _splashMat = BuildSplashMaterial();

        _streakX = new float[MaxStreaks];
        _streakZ = new float[MaxStreaks];
        _streakY = new float[MaxStreaks];
        _streakSpeed = new float[MaxStreaks];
        _streakLen = new float[MaxStreaks];
        // GroundFocusPoint() itself falls back to Vector3.zero if
        // Camera.main isn't ready yet at Awake time -- harmless either
        // way, since rain defaults off (WeatherController.IsRaining
        // false) and nothing draws or reuses this initial scatter until
        // the player turns it on, by which point every active streak
        // has already respawned at least once against the real camera.
        var initialCenter = GroundFocusPoint();
        for (var i = 0; i < MaxStreaks; i++) RespawnStreak(i, initialCenter, aboveGround: true);

        _splashX = new float[MaxSplashes];
        _splashZ = new float[MaxSplashes];
        _splashAge = new float[MaxSplashes];
        _splashActive = new bool[MaxSplashes];
    }

    private void Update()
    {
        _streakMatrices.Clear();
        _splashMatrices.Clear();

        // docs/39 §9: "No effect renders in the Map band" -- and at
        // Wetness == 0 there is nothing to draw at all, so both checks
        // bail before touching a single streak/splash for near-zero
        // cost while dry or fully zoomed out.
        var wetness = WeatherController.Wetness;
        if (wetness <= 0.001f || AnimationLodBudget.CurrentBand == AnimationLodBudget.Band.Map)
            return;

        var center = GroundFocusPoint();
        var dt = Time.deltaTime;
        var activeStreaks = Mathf.RoundToInt(MaxStreaks * wetness);

        for (var i = 0; i < activeStreaks; i++)
        {
            _streakY[i] -= _streakSpeed[i] * dt;
            if (_streakY[i] - _streakLen[i] * 0.5f <= 0f)
            {
                SpawnSplash(_streakX[i], _streakZ[i]);
                RespawnStreak(i, center, aboveGround: false);
                continue;
            }
            var pos = new Vector3(_streakX[i], _streakY[i], _streakZ[i]);
            var scale = new Vector3(StreakWidth, _streakLen[i], StreakWidth);
            _streakMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        }

        for (var i = 0; i < MaxSplashes; i++)
        {
            if (!_splashActive[i]) continue;
            _splashAge[i] += dt;
            var t = _splashAge[i] / SplashLifeSeconds;
            if (t >= 1f) { _splashActive[i] = false; continue; }
            var radius = Mathf.Lerp(0.05f, SplashMaxRadius, t);
            var pos = new Vector3(_splashX[i], SplashHeight * 0.5f, _splashZ[i]);
            var scale = new Vector3(radius, SplashHeight, radius);
            _splashMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        }

        FlushInstances(_boxMesh, _streakMat, _streakMatrices);
        FlushInstances(_boxMesh, _splashMat, _splashMatrices);
    }

    /// <summary>Re-places one streak at a fresh random XZ within the
    /// spawn square around `center` and a fresh random height/speed/
    /// length -- called both at startup (`aboveGround: true`, scattered
    /// through the fall so the first frame doesn't show every streak
    /// starting from the same height) and on landing (`aboveGround:
    /// false`, always from the top).</summary>
    private void RespawnStreak(int i, Vector3 center, bool aboveGround)
    {
        _streakX[i] = center.x + Random.Range(-SpawnHalfExtent, SpawnHalfExtent);
        _streakZ[i] = center.z + Random.Range(-SpawnHalfExtent, SpawnHalfExtent);
        _streakLen[i] = Random.Range(StreakLengthMin, StreakLengthMax);
        _streakSpeed[i] = Random.Range(FallSpeedMin, FallSpeedMax);
        _streakY[i] = aboveGround ? Random.Range(0f, SpawnTopHeight) : SpawnTopHeight;
    }

    private void SpawnSplash(float x, float z)
    {
        // Reuse the oldest/first inactive slot; if the pool is genuinely
        // full (every one of MaxSplashes already live -- only possible
        // at max Wetness with unlucky timing), silently drop the new
        // splash rather than growing the pool. Docs/39 §9's own
        // "hard particle caps" rule: a dropped splash in a downpour is
        // invisible, a growing pool is not.
        for (var i = 0; i < MaxSplashes; i++)
        {
            if (_splashActive[i]) continue;
            _splashActive[i] = true;
            _splashAge[i] = 0f;
            _splashX[i] = x;
            _splashZ[i] = z;
            return;
        }
    }

    /// <summary>Ray-plane intersection against y=0 from the main camera
    /// -- gives the actual ground point the camera is looking at
    /// regardless of `SimpleCameraRig`'s current height/yaw, without
    /// depending on that rig's own internals (this file only needs
    /// `Camera.main`, the same convention `DynamicLightBudget`/
    /// `AnimationLodBudget` already read directly rather than going
    /// through the rig).</summary>
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

    /// <summary>Same batching idiom as `LowPolyFireSystem.FlushBucket`
    /// -- Graphics.DrawMeshInstanced's hard per-call cap is 1023
    /// instances, so anything above that (never actually reached here:
    /// MaxStreaks + MaxSplashes is 300) is split across multiple calls.</summary>
    private void FlushInstances(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        var count = matrices.Count;
        if (count == 0) return;
        var i = 0;
        while (i < count)
        {
            var n = Mathf.Min(1023, count - i);
            for (var k = 0; k < n; k++) _drawScratch[k] = matrices[i + k];
            Graphics.DrawMeshInstanced(mesh, 0, material, _drawScratch, n, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            i += n;
        }
    }

    /// <summary>A plain unit cube (-0.5..0.5 on every axis, 8 verts/12
    /// tris) -- no existing helper in `ProceduralMeshKit`/`PropLibrary`
    /// builds a bare box (its shapes are all more specialized), and this
    /// is cheap and simple enough to hand-author here rather than route
    /// through one of them. Used stretched thin for a streak and
    /// flattened for a splash -- both are the same mesh, only the scale
    /// in the instance matrix differs.</summary>
    private static Mesh BuildBoxMesh()
    {
        var vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
        };
        var triangles = new[]
        {
            0, 2, 1, 0, 3, 2,   // back
            5, 7, 4, 5, 6, 7,   // front
            4, 3, 0, 4, 7, 3,   // left
            1, 6, 5, 1, 2, 6,   // right
            3, 6, 2, 3, 7, 6,   // top
            4, 1, 5, 4, 0, 1,   // bottom
        };
        var mesh = new Mesh { vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Translucent cool grey-blue, unlit-reading (low
    /// smoothness, no texture) -- docs/39 §7 sanctions VFX as one of the
    /// few classes allowed alpha-blended geometry. `LabMeshBuilder.
    /// MakeTransparent` is the existing shared transparency idiom (the
    /// mastermind's glass dome, water surfaces) reused as-is.</summary>
    private static Material BuildStreakMaterial()
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.75f, 0.8f, 0.85f, 0.35f);
        LabMeshBuilder.MakeTransparent(mat);
        ApplyDoubleSidedSafetyNet(mat);
        return mat;
    }

    /// <summary>A faint cool emissive so a splash reads as "catching
    /// light" against dark wet pavement at night without querying any
    /// specific nearby light -- see this file's own header for why that
    /// query is deliberately NOT built here.</summary>
    private static Material BuildSplashMaterial()
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.8f, 0.85f, 0.9f, 0.25f);
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.3f, 0.4f, 0.5f, 1f));
        }
        ApplyDoubleSidedSafetyNet(mat);
        return mat;
    }

    /// <summary>Same "hand-authored mesh, winding unverifiable without a
    /// live Editor, so don't risk backface culling hiding it" mitigation
    /// `PropLibrary.Spawn`/`RoofPortraitHologram` already apply -- docs/28
    /// rows 6/7 are the real incident this guards against (a correctly-
    /// re-wound mesh still vanished because Unity's own culling
    /// disagreed with what "outward" meant, in an environment with no
    /// Editor to catch it before shipping).</summary>
    private static void ApplyDoubleSidedSafetyNet(Material mat)
    {
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
    }
}
