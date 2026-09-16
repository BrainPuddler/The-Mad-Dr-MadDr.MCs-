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
/// `LowPolyFireSystem` already ships for exterior fire, INCLUDING that
/// file's `mat.enableInstancing = true` (missed on the first pass here,
/// caught by a real Editor exception -- see docs/36 entry 27's own
/// update). Two shared meshes (streak, ripple) + two shared materials --
/// matrices are the only per-instance data, batched in ≤1023-instance
/// chunks (the hard per-call cap the same precedent already works
/// around via `FlushBucket`).
///
/// 2026-09-16 (creator direction: "rain streaks should be longer,
/// motion blurred tip and tail. a rain impact splash and ripples on
/// surfaces"): streaks got a real UV-mapped alpha gradient (soft at
/// both ends, not a hard-edged box) instead of a flat-alpha box, and
/// the ground splash became a proper ripple -- a small bright impact
/// core plus an expanding ring, both from ONE static radial-gradient
/// texture whose apparent ring radius grows as the instance's own
/// world-space scale grows over its lifetime (no per-instance texture
/// animation needed).
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
///
/// 2026-09-16 (creator direction: "fall faster and be fast and
/// plentiful close to the camera"): base fall speed raised, and a real
/// depth cue added on top of it -- `NearSpawnBias` of every respawn
/// clusters within `NearCameraRadius` of the camera's own ground
/// position (not the point it's looking at) instead of spreading
/// uniformly, and any streak currently in that radius gets extra fall
/// speed, recomputed live each frame so it tracks a panning camera.
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

    // 2026-09-16 creator direction ("fast and plentiful close to the
    // camera"): raised back up from the 220 mid-pass trim now that a
    // chunk of the pool concentrates near the camera instead of
    // spreading evenly -- still comfortably under the 1023
    // DrawMeshInstanced per-call cap.
    private const int MaxStreaks = 280;
    private const float StreakWidth = 0.06f;
    // 2026-09-16 creator direction ("longer"): roughly doubled from the
    // original 1.1-1.9 m -- long enough to read as a fast-falling
    // streak at the default 70 m zoom instead of a short dash.
    private const float StreakLengthMin = 2.6f, StreakLengthMax = 4.2f;
    // 2026-09-16 creator direction ("fall faster"): raised from the
    // original 16-24. NearSpeedMultiplier below stacks ON TOP of this
    // for streaks close to the camera specifically.
    private const float FallSpeedMin = 24f, FallSpeedMax = 34f;

    // 2026-09-16 creator direction ("fast and plentiful close to the
    // camera"): a real depth cue, not just a wider spread -- real rain
    // close to the lens reads as a blur of fast, dense streaks while
    // distant rain is a faint, sparser haze. `NearSpawnBias` fraction of
    // every respawn lands within `NearCameraRadius` of the camera's own
    // ground position (`CameraGroundXZ`) instead of uniformly across
    // the whole spawn square, and any streak currently within that
    // radius gets up to `NearSpeedMultiplier`x its own base fall speed,
    // recomputed live each frame so a panning camera doesn't leave
    // stale fast/dense streaks behind.
    private const float NearCameraRadius = 32f;
    private const float NearSpeedMultiplier = 1.7f;
    private const float NearSpawnBias = 0.6f;

    private const int MaxSplashes = 40;
    // 2026-09-16: splashes now show BOTH an impact flash and an
    // expanding ripple ring from one growing quad, so the lifetime is a
    // little longer than the old pure-growth blob needed -- enough time
    // for the ring to visibly separate from the impact core before it
    // fades.
    private const float SplashLifeSeconds = 0.6f;
    private const float SplashMaxRadius = 0.75f;

    private float[] _streakX, _streakZ, _streakY, _streakSpeed, _streakLen;
    private float[] _splashX, _splashZ, _splashAge;
    private bool[] _splashActive;

    private Mesh _streakMesh, _rippleMesh;
    private Material _streakMat, _splashMat;
    private readonly List<Matrix4x4> _streakMatrices = new List<Matrix4x4>();
    private readonly List<Matrix4x4> _splashMatrices = new List<Matrix4x4>();
    private readonly Matrix4x4[] _drawScratch = new Matrix4x4[1023];

    private void Awake()
    {
        _streakMesh = BuildStreakMesh();
        _rippleMesh = BuildRippleQuadMesh();
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
        var initialCamXZ = CameraGroundXZ();
        for (var i = 0; i < MaxStreaks; i++) RespawnStreak(i, initialCenter, initialCamXZ, aboveGround: true);

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
        var camXZ = CameraGroundXZ();
        var dt = Time.deltaTime;
        var activeStreaks = Mathf.RoundToInt(MaxStreaks * wetness);

        for (var i = 0; i < activeStreaks; i++)
        {
            var dist = Vector2.Distance(new Vector2(_streakX[i], _streakZ[i]), camXZ);
            var proximity = Mathf.Clamp01(1f - dist / NearCameraRadius);
            var speedMul = Mathf.Lerp(1f, NearSpeedMultiplier, proximity);
            _streakY[i] -= _streakSpeed[i] * speedMul * dt;
            if (_streakY[i] - _streakLen[i] * 0.5f <= 0f)
            {
                SpawnSplash(_streakX[i], _streakZ[i]);
                RespawnStreak(i, center, camXZ, aboveGround: false);
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
            // Eased growth (fast at first, slowing) reads as a real
            // ripple expanding outward and losing energy -- a linear
            // Lerp made every ripple's ring speed look identical and
            // mechanical.
            var eased = 1f - (1f - t) * (1f - t);
            var radius = Mathf.Lerp(0.04f, SplashMaxRadius, eased);
            var pos = new Vector3(_splashX[i], 0.015f, _splashZ[i]);
            var scale = new Vector3(radius, 1f, radius);
            _splashMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        }

        FlushInstances(_streakMesh, _streakMat, _streakMatrices);
        FlushInstances(_rippleMesh, _splashMat, _splashMatrices);
    }

    /// <summary>Re-places one streak at a fresh XZ and a fresh random
    /// height/speed/length -- called both at startup (`aboveGround:
    /// true`, scattered through the fall so the first frame doesn't
    /// show every streak starting from the same height) and on landing
    /// (`aboveGround: false`, always from the top). `NearSpawnBias` of
    /// all respawns land within `NearCameraRadius` of `camXZ` -- radius
    /// sampled as `sqrt(random)` (the standard uniform-DISC technique,
    /// compensating for area growing with r², not a naive `random *
    /// radius` which would under-fill the outer ring) so that patch
    /// itself reads as evenly dense, not artificially peaked at the
    /// exact camera point. The rest scatter uniformly across the full
    /// `center`-anchored square -- the "plentiful close to the camera,
    /// still present in the distance" split.</summary>
    private void RespawnStreak(int i, Vector3 center, Vector2 camXZ, bool aboveGround)
    {
        if (Random.value < NearSpawnBias)
        {
            var r = Mathf.Sqrt(Random.value) * NearCameraRadius;
            var a = Random.Range(0f, Mathf.PI * 2f);
            _streakX[i] = camXZ.x + Mathf.Sin(a) * r;
            _streakZ[i] = camXZ.y + Mathf.Cos(a) * r;
        }
        else
        {
            _streakX[i] = center.x + Random.Range(-SpawnHalfExtent, SpawnHalfExtent);
            _streakZ[i] = center.z + Random.Range(-SpawnHalfExtent, SpawnHalfExtent);
        }
        _streakLen[i] = Random.Range(StreakLengthMin, StreakLengthMax);
        _streakSpeed[i] = Random.Range(FallSpeedMin, FallSpeedMax);
        _streakY[i] = aboveGround ? Random.Range(0f, SpawnTopHeight) : SpawnTopHeight;
    }

    /// <summary>The camera's own horizontal position -- not the ground
    /// point it's looking AT (`GroundFocusPoint`, which sits ahead of a
    /// pitched-down camera), but literally under/near it, i.e. the
    /// near/foreground edge of the shot. That distinction is the whole
    /// point of the "close to the camera" bias above.</summary>
    private static Vector2 CameraGroundXZ()
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.zero;
        var pos = cam.transform.position;
        return new Vector2(pos.x, pos.z);
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
    /// MaxStreaks + MaxSplashes is 260) is split across multiple calls.</summary>
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

    /// <summary>A unit box (-0.5..0.5 on every axis, 8 verts/12 tris)
    /// with a per-vertex UV.y that tracks local Y directly (0 at the
    /// bottom, 1 at the top) -- every vertex has one unambiguous Y, so
    /// this mapping is seam-free across all six faces despite vertices
    /// being shared between faces. UV.x is left at 0 everywhere; the
    /// gradient texture this pairs with (<see cref="BuildStreakGradient
    /// Texture"/>) varies only by V, so U is never sampled meaningfully.
    /// Scaled thin and tall per-instance for the falling streak;
    /// stretched Y IS the direction of travel, so this UV mapping is
    /// exactly "fades out along the direction of motion at both ends" --
    /// the motion-blur look the creator asked for -- with zero per-
    /// instance cost, baked into one shared texture.</summary>
    private static Mesh BuildStreakMesh()
    {
        var positions = new[]
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
        var uvs = new Vector2[positions.Length];
        for (var i = 0; i < positions.Length; i++) uvs[i] = new Vector2(0f, positions[i].y + 0.5f);

        var mesh = new Mesh { vertices = positions, triangles = triangles, uv = uvs };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A flat quad in the XZ plane (y=0, -0.5..0.5 on X/Z) with
    /// standard corner UVs -- a ground decal, not a volume, so it never
    /// needs a box's thickness. Safe from the "vertical billboard can
    /// go edge-on and vanish under free yaw" risk a streak would have,
    /// since the camera here is always looking DOWN at a fixed pitch
    /// (docs/39 §1) -- a horizontal quad's +Y normal is never
    /// perpendicular to the view direction. `_Cull = Off` (applied to
    /// its material regardless, same safety net as every hand-authored
    /// mesh here) makes the exact winding moot either way.</summary>
    private static Mesh BuildRippleQuadMesh()
    {
        var positions = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
        };
        var triangles = new[] { 0, 2, 1, 0, 3, 2 };
        var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        var mesh = new Mesh { vertices = positions, triangles = triangles, uv = uvs };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>32x64 alpha gradient (RGB stays white -- `mat.color`
    /// tints it): fully transparent at both V=0 and V=1, full opacity
    /// across the middle band. Soft `SmoothStep`-eased edges on both
    /// ends is the whole "motion blurred tip and tail" ask -- a hard-
    /// edged box (the old geometry-only streak) reads as a solid rod,
    /// not a fast-moving smear.</summary>
    private static Texture2D BuildStreakGradientTexture()
    {
        const int w = 8, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[w * h];
        for (var y = 0; y < h; y++)
        {
            var v = y / (float)(h - 1);
            var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.22f, v));
            var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, v));
            var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(fadeIn * fadeOut) * 255f);
            for (var x = 0; x < w; x++) pixels[y * w + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>64x64 radial gradient (RGB white, `mat.color` tints):
    /// a small bright core near the center (the impact flash) plus a
    /// thin ring further out (the ripple). Both are baked into ONE
    /// static texture -- as a ripple instance's own world-space SCALE
    /// grows over its lifetime (see `Update`'s `radius`), the ring's
    /// FIXED uv-space position reads as an expanding ring in world
    /// space, and the core shrinks in relative (though not absolute)
    /// size, exactly like a real droplet impact settling into an
    /// outward ripple -- no per-instance texture animation needed.</summary>
    private static Texture2D BuildRippleTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = (x + 0.5f) / size - 0.5f;
            var v = (y + 0.5f) / size - 0.5f;
            var dist = Mathf.Sqrt(u * u + v * v);   // 0 at center, ~0.707 at the corners

            // impact core: bright, tight, fading out by dist 0.16
            var core = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.16f, dist));
            // ripple ring: a thin band centered at dist 0.38, fully
            // faded by 0.30 on the inside and 0.48 on the outside
            var ringIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.38f, dist));
            var ringOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.48f, dist));
            var ring = Mathf.Clamp01(ringIn * ringOut) * 0.8f;

            var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(core + ring) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>Translucent cool grey-blue, unlit-reading (low
    /// smoothness, no base color texture beyond the alpha gradient) --
    /// docs/39 §7 sanctions VFX as one of the few classes allowed
    /// alpha-blended geometry. `LabMeshBuilder.MakeTransparent` is the
    /// existing shared transparency idiom (the mastermind's glass dome,
    /// water surfaces) reused as-is.</summary>
    private static Material BuildStreakMaterial()
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.75f, 0.8f, 0.85f, 0.6f);
        // Real Editor exception caught this the first time it actually
        // ran: Graphics.DrawMeshInstanced throws InvalidOperationException
        // ("Material needs to enable instancing") without this --
        // LowPolyFireSystem.MakeFireMaterial already sets this same flag
        // right after construction, and this file's own header claims
        // that file as its precedent, but the flag itself got missed
        // when mirroring it. Confirmed the real, load-bearing gap; not a
        // style choice.
        mat.enableInstancing = true;
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", BuildStreakGradientTexture());
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
        mat.color = new Color(0.8f, 0.85f, 0.9f, 0.55f);
        mat.enableInstancing = true;   // see BuildStreakMaterial's own comment
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", BuildRippleTexture());
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
