using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/33 (window glow GPU system): the replacement for one Cube
/// GameObject-per-window (the old <c>BuildingDresser.SpawnWindowStrip</c> /
/// <c>FacadeKit.RegisterWindowGlow</c> path). One instance lives on each
/// building's dressing holder (the same "Dressing_Q_R" GameObject
/// <see cref="BuildingDresser.Dress"/> already creates per footprint hex);
/// every window on that holder is a quad appended to ONE shared mesh, so a
/// building with dozens of windows costs exactly one draw call, one
/// MeshRenderer, one MeshFilter -- zero per-window GameObjects, zero
/// per-window Renderers, zero per-window Lights.
///
/// Per-window IDENTITY and STATE split the same way <c>WindowGrid.shader</c>'s
/// own header describes:
///  - Everything about a window's ambient behavior (its own randomized
///    "someone's home"/"lights out" schedule, whether it flickers, its
///    warm/cool tint, brightness variance, whether it can ever glow at
///    all) is a deterministic function of a `seed` the CALLER already
///    computes (same convention <c>EmissiveAnimator.Register</c> used --
///    a hash of the window's own hex/floor/slot) and gets baked into that
///    window's 4 vertices ONCE, at <see cref="AddWindow"/> time. It never
///    changes again and costs nothing at runtime beyond ordinary vertex
///    data.
///  - The mandatory gameplay ON/OFF override (<see cref="SetWindowOn"/>/
///    <see cref="SetWindowOff"/>/<see cref="ClearWindowOverride"/>) is the
///    only thing that's genuinely dynamic per window, and it's rare (most
///    windows are never individually toggled by gameplay) -- so it lives
///    in a small per-building single-channel texture, one texel per
///    window, uploaded via a plain <see cref="Texture2D.Apply"/> only when
///    a caller actually changes a window. For the handful-to-low-hundreds
///    of windows a single building has, that's already trivially cheap;
///    see this class's own header note in <c>WindowGrid.shader</c> for why
///    a GraphicsBuffer/ComputeBuffer wasn't reached for instead.
/// </summary>
[DisallowMultipleComponent]
public class BuildingWindowGrid : MonoBehaviour
{
    // ---- static per-window random-draw tuning. `internal` (not
    // `private`) so BuildingWindowGridDriver can push the SAME numbers to
    // the shader as globals rather than duplicating them as a second set
    // of magic literals that could silently drift out of sync -- exactly
    // the failure shape docs/28's own bug-history table warns about
    // repeatedly. Values mirror what EmissiveAnimator.cs's
    // LightBehaviorKind.Window used to use for city buildings (that kind
    // still exists and is still used by BaseDresser.cs's faction-building
    // windows, a separate, much smaller system this task deliberately
    // left alone -- see docs/33's own scope note). ----
    internal const float OnRangeStart = 0.2f;
    internal const float OnRangeEnd = 0.75f;
    internal const float OffRangeStart = 0.75f;
    internal const float OffRangeEnd = 0.98f;
    internal const float AlwaysOnProbability = 0.15f;
    // 2026-08 (docs/33, Visual Requirements: "only a small percentage of
    // windows should flicker"): EmissiveAnimator's old Window kind wobbled
    // EVERY lit window; this is a deliberate, small behavior change from
    // that (flagged here, not silently carried over) to match the task's
    // explicit ask for flicker as a rare accent, not a universal wobble.
    internal const float FlickerProbability = 0.12f;
    // Shader-only tuning (Build() doesn't need these itself, but they're
    // declared here too so the driver has exactly one place to read every
    // window-schedule constant from).
    internal const float OccupancyTransitionFrac = 0.01f;
    internal const float ActivityGateBand = 0.08f;

    private struct PendingWindow
    {
        public Vector3 Center, Tangent, Normal;
        public float Width, Height;
        public float Seed;
        public bool CanGlow;
        public Color GlowColor;
    }

    private readonly List<PendingWindow> _pending = new List<PendingWindow>();
    private bool _built;
    private int _texSize;
    private Texture2D _overrideTex;
    private MaterialPropertyBlock _mpb;

    private static Material _sharedMaterial;

    /// <summary>Get-or-add the grid for a building's dressing holder --
    /// the one lookup every window-spawning call site needs, so
    /// BuildingDresser/FacadeKit don't have to thread a grid reference
    /// through their own call chains (both already receive the holder
    /// Transform as a parameter today).</summary>
    public static BuildingWindowGrid For(Transform holder)
    {
        var grid = holder.GetComponent<BuildingWindowGrid>();
        if (grid == null) grid = holder.gameObject.AddComponent<BuildingWindowGrid>();
        return grid;
    }

    private static Material SharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        var shader = Shader.Find("MadDr/WindowGrid");
        if (shader == null) return null;
        _sharedMaterial = new Material(shader) { enableInstancing = true };
        _sharedMaterial.SetTexture("_BaseMap", PbrTextureAtlas.Glass);
        return _sharedMaterial;
    }

    private static float Frac(float v) { return v - Mathf.Floor(v); }

    /// <summary>Queue one window quad. `seed` should be a ~uniform [0,1)
    /// hash of the window's own position in the building (same "hash of
    /// hex/floor/slot" convention every caller already derives for
    /// EmissiveAnimator today) -- every per-window random draw below comes
    /// from decorrelated multiplies of this ONE value, same trick
    /// EmissiveAnimator.OnCycleFrac/OffCycleFrac/ActivityThreshold already
    /// used. `canGlow` is the caller's own existing "is this window lit
    /// tonight at all" roll (BuildingDresser's per-strip hash, or
    /// FacadeMaterials.RollGlow) -- kept as a caller decision rather than
    /// re-derived here, so each call site keeps its own tuned density.
    /// Geometry is a single outward-facing quad (not a thin cube like the
    /// old per-window prims) -- half the vertex count, and no back face
    /// ever gets seen on an exterior building wall.
    /// Returns this window's stable index on THIS grid, for
    /// SetWindowOn/SetWindowOff.</summary>
    public int AddWindow(Vector3 center, Vector3 tangent, Vector3 normal, float width, float height,
        float seed, bool canGlow, Color glowColor)
    {
        if (_built)
        {
            Debug.LogWarning("BuildingWindowGrid.AddWindow called after Build() -- window ignored.", this);
            return -1;
        }
        _pending.Add(new PendingWindow
        {
            Center = center,
            Tangent = tangent.normalized,
            Normal = normal.normalized,
            Width = width,
            Height = height,
            Seed = seed,
            CanGlow = canGlow,
            GlowColor = glowColor,
        });
        return _pending.Count - 1;
    }

    /// <summary>Finalize the mesh, allocate the override-state texture, and
    /// attach the renderer. Call once after every AddWindow for this
    /// building's holder has been made (BuildingDresser.Dress does this
    /// once per footprint hex, after all faces of that hex are dressed).
    /// A no-op if no windows were queued (a building whose facade rolled
    /// zero windows this time) or if this grid was already built.</summary>
    public void Build()
    {
        if (_built || _pending.Count == 0) return;
        _built = true;

        var count = _pending.Count;
        _texSize = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(count)));

        var verts = new List<Vector3>(count * 4);
        var normals = new List<Vector3>(count * 4);
        var uv0 = new List<Vector2>(count * 4);
        var packed1 = new List<Vector4>(count * 4);
        var packed2 = new List<Vector4>(count * 4);
        var overrideUV = new List<Vector4>(count * 4);
        var tris = new List<int>(count * 6);

        for (var i = 0; i < count; i++)
        {
            var w = _pending[i];
            var right = w.Tangent * (w.Width * 0.5f);
            var up = Vector3.up * (w.Height * 0.5f);

            // AddWindow's Center/Normal/Tangent are all WORLD space (every
            // caller already works in world space, matching SpawnPrim's
            // own convention) -- but Mesh vertices are local to whatever
            // GameObject renders them, and this grid lives on the SAME
            // dressing-holder Transform BuildingDresser.Dress positions at
            // the hex's world center (non-zero, non-identity). Convert
            // here, once, at Build() time, rather than making every call
            // site do its own transform.InverseTransformPoint math.
            var v0 = transform.InverseTransformPoint(w.Center - right - up);
            var v1 = transform.InverseTransformPoint(w.Center + right - up);
            var v2 = transform.InverseTransformPoint(w.Center + right + up);
            var v3 = transform.InverseTransformPoint(w.Center - right + up);
            var localNormal = transform.InverseTransformDirection(w.Normal);

            var baseIdx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            for (var k = 0; k < 4; k++) normals.Add(localNormal);
            uv0.Add(new Vector2(0, 0)); uv0.Add(new Vector2(1, 0));
            uv0.Add(new Vector2(1, 1)); uv0.Add(new Vector2(0, 1));

            // Decorrelated draws off the same seed -- fresh multipliers
            // (137.9/151.7/167.3) chosen not to collide with the
            // On/Off/Activity multipliers below or with each other.
            var tintT = Frac(w.Seed * 137.9f + 5f);
            var brightnessVar = Mathf.Lerp(0.8f, 1.2f, Frac(w.Seed * 151.7f + 11f));
            var flickerOn = w.CanGlow && Frac(w.Seed * 167.3f + 23f) < FlickerProbability;

            var onFrac = Mathf.Lerp(OnRangeStart, OnRangeEnd, Frac(w.Seed * 41.3f));
            var offFrac = Mathf.Lerp(OffRangeStart, OffRangeEnd, Frac(w.Seed * 59.7f + 3f));
            var alwaysOn = w.CanGlow && Frac(w.Seed * 91.1f + 7f) < AlwaysOnProbability;
            var activityThreshold = Frac(w.Seed * 113.7f + 19f);

            float flags = (w.CanGlow ? 1f : 0f) + (alwaysOn ? 2f : 0f) + (flickerOn ? 4f : 0f);
            var p1 = new Vector4(w.Seed, tintT, brightnessVar, flags);
            var p2 = new Vector4(onFrac, offFrac, activityThreshold, 0f);
            // Texel-center UV into the override texture, sized once the
            // final window count is known -- every one of this window's 4
            // verts shares the same texel.
            var tx = i % _texSize;
            var ty = i / _texSize;
            var ou = new Vector4((tx + 0.5f) / _texSize, (ty + 0.5f) / _texSize, 0f, 0f);

            for (var k = 0; k < 4; k++) { packed1.Add(p1); packed2.Add(p2); overrideUV.Add(ou); }

            Tri(tris, baseIdx, baseIdx + 1, baseIdx + 2);
            Tri(tris, baseIdx, baseIdx + 2, baseIdx + 3);

            // Tier-2 real-light candidacy: same "every glow-capable window
            // competes for the shared city-wide budget" rule the old
            // per-window GlowPointRegistry.Register call used -- but a
            // fixed WORLD POSITION instead of a Transform, since these
            // windows no longer have one of their own (see
            // GlowPointRegistry.RegisterPosition's own doc comment for why
            // that's a real, deliberate extension, not a workaround).
            if (w.CanGlow) GlowPointRegistry.RegisterPosition(w.Center, w.GlowColor);
        }
        _pending.Clear();

        var mesh = new Mesh { name = "WindowGrid" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, packed1);
        mesh.SetUVs(2, packed2);
        mesh.SetUVs(3, overrideUV);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        _overrideTex = new Texture2D(_texSize, _texSize, TextureFormat.R8, false, true)
        {
            name = "WindowOverride",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var fill = new Color32[_texSize * _texSize];
        for (var i = 0; i < fill.Length; i++) fill[i] = new Color32(128, 128, 128, 128);   // 0.5 == "no override"
        _overrideTex.SetPixels32(fill);
        _overrideTex.Apply(false, false);

        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = SharedMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        _mpb = new MaterialPropertyBlock();
        _mpb.SetTexture("_OverrideStateTex", _overrideTex);
        renderer.SetPropertyBlock(_mpb);
    }

    private static void Tri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    /// <summary>Force this window on, overriding its ambient schedule --
    /// the mandatory per-window gameplay API (docs/33). A single-texel
    /// write on a texture sized to this building's own window count (a
    /// handful to a few hundred texels), re-uploaded via one `Apply` --
    /// "a very small GPU-data update," not a mesh rebuild, not a new
    /// Material, not a new GameObject.</summary>
    public void SetWindowOn(int windowId) { WriteOverride(windowId, 255); }

    public void SetWindowOff(int windowId) { WriteOverride(windowId, 0); }

    /// <summary>Revert to the ambient day/night occupancy schedule.</summary>
    public void ClearWindowOverride(int windowId) { WriteOverride(windowId, 128); }

    private void WriteOverride(int windowId, byte value)
    {
        if (_overrideTex == null || windowId < 0) return;
        var x = windowId % _texSize;
        var y = windowId / _texSize;
        if (y >= _texSize) return;
        _overrideTex.SetPixel(x, y, new Color32(value, value, value, value));
        _overrideTex.Apply(false, false);
    }
}

/// <summary>Pushes the handful of GLOBAL shader uniforms every
/// `MadDr/WindowGrid` material instance reads (day/night cycle position,
/// light activity, neon boost, the window-schedule toggle, and
/// CityLightingProfile's flicker/occupancy tunables) once per frame --
/// not per building, not per window. RuntimeCityBuilder adds exactly one
/// of these per scene, same pattern as EmissiveAnimatorDriver.</summary>
public class BuildingWindowGridDriver : MonoBehaviour
{
    private static readonly int CycleProgressId = Shader.PropertyToID("_MadDrWinCycleProgress");
    private static readonly int LightActivityId = Shader.PropertyToID("_MadDrWinLightActivity");
    private static readonly int NeonBoostId = Shader.PropertyToID("_MadDrWinNeonBoost");
    private static readonly int ScheduleEnabledId = Shader.PropertyToID("_MadDrWinScheduleEnabled");
    // NOTE: onFrac/offFrac/activityThreshold are already fully computed
    // per-window at Build() time and baked into vertex data (TEXCOORD2) --
    // OnRangeStart/OnRangeEnd/OffRangeStart/OffRangeEnd are consumed
    // ENTIRELY on the C# side and never needed again at runtime, so
    // there's no matching shader global for them (would be dead/unused
    // uniform plumbing). TransitionFrac/ActivityGateBand below ARE live
    // shader-side knobs -- they shape the smoothstep transition and the
    // activity gate band at runtime, not baked per-window.
    private static readonly int TransitionFracId = Shader.PropertyToID("_MadDrWinTransitionFrac");
    private static readonly int ActivityGateBandId = Shader.PropertyToID("_MadDrWinActivityGateBand");
    private static readonly int FlickerSpeedMinId = Shader.PropertyToID("_MadDrWinFlickerSpeedMin");
    private static readonly int FlickerSpeedMaxId = Shader.PropertyToID("_MadDrWinFlickerSpeedMax");
    private static readonly int FlickerFloorId = Shader.PropertyToID("_MadDrWinFlickerFloor");

    private void Update()
    {
        var profile = CityLightingProfile.Active;
        Shader.SetGlobalFloat(CycleProgressId, DayNightState.CycleProgress);
        Shader.SetGlobalFloat(LightActivityId, DayNightState.LightActivity);
        Shader.SetGlobalFloat(NeonBoostId, DayNightState.NeonBoost);
        Shader.SetGlobalFloat(ScheduleEnabledId, EmissiveAnimator.WindowScheduleEnabled ? 1f : 0f);
        Shader.SetGlobalFloat(TransitionFracId, BuildingWindowGrid.OccupancyTransitionFrac);
        Shader.SetGlobalFloat(ActivityGateBandId, BuildingWindowGrid.ActivityGateBand);
        Shader.SetGlobalFloat(FlickerSpeedMinId, profile.FlickerSpeedRange.x);
        Shader.SetGlobalFloat(FlickerSpeedMaxId, profile.FlickerSpeedRange.y);
        Shader.SetGlobalFloat(FlickerFloorId, profile.FlickerFloor);
    }
}
