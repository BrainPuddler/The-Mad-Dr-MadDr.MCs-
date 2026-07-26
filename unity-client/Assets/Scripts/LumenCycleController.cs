using MadDr.CityGen;
using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// docs/23 Phase 10 sub-phases 1 (post stack) + 2 (lighting): supersedes
/// the old binary day/dusk NightMode with a continuous four-phase cycle
/// (Dawn/Day/Dusk/Night) driven by the SAME pure Lumen Cycle math
/// match-core's own <see cref="LumenClock"/> uses (docs/03/docs/23 §7) --
/// this controller keeps its own cosmetic tick counter (10 ticks/s,
/// matching MatchState.TicksPerSecond) rather than reading a live
/// MatchState/SimBridge, so every scene gets a real day/night cycle even
/// when nothing has opted into sim-driven movement. This is Unity-layer
/// presentation ONLY -- nothing here ever feeds back into sim state (the
/// phase's own acceptance bar).
///
/// Sun color/intensity/elevation, ambient light, fog, the existing
/// NeonRegistry boost, and a runtime-built URP Volume (color grading,
/// film grain, vignette, bloom, tonemapping) all cross-fade between the
/// current phase's keyframe and the next, rather than snapping -- so the
/// cycle reads as one continuous day, not four hard cuts. Region grading
/// (docs/23 §8's NY/Paris/Montreal) is a small tint layered on the
/// baseline grade's color filter/saturation/contrast, NOT an authored
/// per-region LUT texture (URP's ColorLookup + a baked 3D LUT asset would
/// need real DCC/Editor work this environment can't do) -- a documented,
/// deliberate substitution, not the "LUT" the plan's prose literally
/// names.
///
/// Every numeric keyframe below is an invented v0.1 placeholder (docs/23
/// gives a mood/target description, not real numbers) -- tune in the
/// Editor once one exists for this project.
/// </summary>
public class LumenCycleController : MonoBehaviour
{
    private struct PhaseGrade
    {
        public Color SunColor;
        public float SunIntensity;
        public float SunElevationDeg;   // kept LOW even at Day's peak -- docs/23 Phase 10's own daytime mood-board note: long, legible cast shadows, never a high overhead noon angle
        public Color Ambient;
        public Color Fog;
        public float FogDensity;
        public float NeonBoost;         // feeds the existing NeonRegistry (emissive signage/bulb materials)
        public float LampBoost;         // feeds StreetLampLightBudget's real point-light intensity
        public float PostExposure;
        public float Saturation;
        public Color ColorFilter;
        public float Contrast;
        public float VignetteIntensity;
        public float BloomIntensity;
        public float FilmGrainIntensity;
    }

    private const float SunYawDeg = -35f;  // fixed cast-shadow direction across the whole cycle -- only elevation/color animate
    private const float TimeLapseMultiplier = 20f;
    private static readonly double TickInterval = 1.0 / MatchState.TicksPerSecond;

    private PhaseGrade[] _grades;   // indexed by (int)LumenPhase
    private int _frame;
    private double _tickAccumulator;
    private bool _timeLapse;

    private Light _sun;
    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private Bloom _bloom;
    private FilmGrain _filmGrain;
    private Tonemapping _tonemapping;

    /// <summary>Call once, right after AddComponent -- region is known to
    /// the caller (RuntimeCityBuilder already generated the CityModel) but
    /// not to this component itself, same shape as MonsterAgent.Init.</summary>
    public void Init(CityRegion region)
    {
        _grades = BuildGrades(region);
    }

    private void Start()
    {
        if (_grades == null) _grades = BuildGrades(CityRegion.Generic);

        var sunGo = new GameObject("LumenCycleSun");
        _sun = sunGo.AddComponent<Light>();
        _sun.type = LightType.Directional;
        _sun.shadows = LightShadows.Soft;

        BuildVolume();
        EnsurePostProcessingOnMainCamera();

        ApplyBlend();   // paint frame 0's state immediately -- no one-frame flash of default values
    }

    private void EnsurePostProcessingOnMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
    }

    private void BuildVolume()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _colorAdjustments = profile.Add<ColorAdjustments>(true);
        _vignette = profile.Add<Vignette>(true);
        _bloom = profile.Add<Bloom>(true);
        _filmGrain = profile.Add<FilmGrain>(true);
        _tonemapping = profile.Add<Tonemapping>(true);

        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.colorFilter.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;
        _vignette.intensity.overrideState = true;
        _bloom.intensity.overrideState = true;
        _bloom.threshold.overrideState = true;
        _filmGrain.intensity.overrideState = true;
        _tonemapping.mode.overrideState = true;
        _tonemapping.mode.value = TonemappingMode.ACES;   // filmic tonemapping, per §10's own target look

        var volumeGo = new GameObject("LumenCyclePostStack");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.profile = profile;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        // the old NightMode's N-key was a binary day/dusk override -- with
        // a real auto-cycling clock that no longer makes sense, so N now
        // means "time-lapse" (a dev/demo speed-up), not "pick a mode."
        if (kb != null && kb.nKey.wasPressedThisFrame) _timeLapse = !_timeLapse;

        _tickAccumulator += Time.deltaTime * (_timeLapse ? TimeLapseMultiplier : 1f);
        while (_tickAccumulator >= TickInterval)
        {
            _frame++;
            _tickAccumulator -= TickInterval;
        }

        ApplyBlend();
    }

    /// <summary>Cross-fades between the current Lumen phase's keyframe and
    /// the NEXT phase's, by how far this frame's tick sits through the
    /// current phase -- eased (SmoothStep) rather than linear so the
    /// transition itself reads as unhurried, matching this phase's own
    /// "unhurried noir" target line.</summary>
    private void ApplyBlend()
    {
        var t = _frame % LumenClock.CycleTicks;
        LumenPhase phase;
        LumenPhase next;
        float progress;

        if (t < LumenClock.DawnTicks)
        {
            phase = LumenPhase.Dawn; next = LumenPhase.Day;
            progress = t / (float)LumenClock.DawnTicks;
        }
        else if ((t -= LumenClock.DawnTicks) < LumenClock.DayTicks)
        {
            phase = LumenPhase.Day; next = LumenPhase.Dusk;
            progress = t / (float)LumenClock.DayTicks;
        }
        else if ((t -= LumenClock.DayTicks) < LumenClock.DuskTicks)
        {
            phase = LumenPhase.Dusk; next = LumenPhase.Night;
            progress = t / (float)LumenClock.DuskTicks;
        }
        else
        {
            t -= LumenClock.DuskTicks;
            phase = LumenPhase.Night; next = LumenPhase.Dawn;
            progress = t / (float)LumenClock.NightTicks;
        }

        var a = _grades[(int)phase];
        var b = _grades[(int)next];
        var blend = Mathf.SmoothStep(0f, 1f, progress);

        _sun.color = Color.Lerp(a.SunColor, b.SunColor, blend);
        _sun.intensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, blend);
        // Unity's directional-light convention: local +Z is the direction
        // light TRAVELS, so an X-euler of 0 is horizontal (elevation 0,
        // sunrise/sunset) and 90 points straight down (elevation 90, noon
        // overhead) -- X-euler == elevation directly, no 90-minus flip.
        var elevation = Mathf.Lerp(a.SunElevationDeg, b.SunElevationDeg, blend);
        _sun.transform.rotation = Quaternion.Euler(elevation, SunYawDeg, 0f);

        RenderSettings.ambientLight = Color.Lerp(a.Ambient, b.Ambient, blend);
        var fogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, blend);
        RenderSettings.fog = fogDensity > 0.0005f;
        RenderSettings.fogColor = Color.Lerp(a.Fog, b.Fog, blend);
        RenderSettings.fogDensity = fogDensity;

        var neonBoost = Mathf.Lerp(a.NeonBoost, b.NeonBoost, blend);
        NeonRegistry.SetBoost(neonBoost);
        DayNightState.NeonBoost = neonBoost;
        DayNightState.NightAmount = Mathf.Lerp(a.LampBoost, b.LampBoost, blend);

        _colorAdjustments.postExposure.value = Mathf.Lerp(a.PostExposure, b.PostExposure, blend);
        _colorAdjustments.saturation.value = Mathf.Lerp(a.Saturation, b.Saturation, blend);
        _colorAdjustments.colorFilter.value = Color.Lerp(a.ColorFilter, b.ColorFilter, blend);
        _colorAdjustments.contrast.value = Mathf.Lerp(a.Contrast, b.Contrast, blend);
        _vignette.intensity.value = Mathf.Lerp(a.VignetteIntensity, b.VignetteIntensity, blend);
        _bloom.intensity.value = Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, blend);
        _filmGrain.intensity.value = Mathf.Lerp(a.FilmGrainIntensity, b.FilmGrainIntensity, blend);
    }

    private static PhaseGrade[] BuildGrades(CityRegion region)
    {
        var grades = new[]
        {
            // Dawn
            new PhaseGrade
            {
                SunColor = new Color(1f, 0.75f, 0.55f), SunIntensity = 0.6f, SunElevationDeg = 8f,
                Ambient = new Color(0.35f, 0.32f, 0.38f), Fog = new Color(0.4f, 0.32f, 0.38f), FogDensity = 0.006f,
                NeonBoost = 0.9f, LampBoost = 0.5f,
                PostExposure = 0f, Saturation = -5f, ColorFilter = new Color(1f, 0.93f, 0.85f), Contrast = 5f,
                VignetteIntensity = 0.25f, BloomIntensity = 0.6f, FilmGrainIntensity = 0.15f,
            },
            // Day -- sun-baked, dusty, desaturated-sepia warmth (2026-07 daytime mood-board addition), NOT a clean blue-sky render
            new PhaseGrade
            {
                SunColor = new Color(1f, 0.97f, 0.88f), SunIntensity = 1.1f, SunElevationDeg = 30f,
                Ambient = new Color(0.55f, 0.53f, 0.5f), Fog = new Color(0.55f, 0.5f, 0.42f), FogDensity = 0.003f,
                NeonBoost = 0.35f, LampBoost = 0.05f,
                PostExposure = 0.15f, Saturation = -18f, ColorFilter = new Color(1.05f, 0.95f, 0.78f), Contrast = 8f,
                VignetteIntensity = 0.15f, BloomIntensity = 0.4f, FilmGrainIntensity = 0.22f,
            },
            // Dusk
            new PhaseGrade
            {
                SunColor = new Color(0.95f, 0.5f, 0.35f), SunIntensity = 0.5f, SunElevationDeg = 4f,
                Ambient = new Color(0.32f, 0.22f, 0.3f), Fog = new Color(0.35f, 0.22f, 0.28f), FogDensity = 0.008f,
                NeonBoost = 1.4f, LampBoost = 0.8f,
                PostExposure = -0.1f, Saturation = 5f, ColorFilter = new Color(1f, 0.85f, 0.8f), Contrast = 10f,
                VignetteIntensity = 0.3f, BloomIntensity = 0.9f, FilmGrainIntensity = 0.18f,
            },
            // Night -- warm sodium nights, saturated neon-and-noir palette (the target look's own headline).
            // 2026-07 creator correction: ambient (and the moonlit sun)
            // were bright enough that everything stayed visibly lit
            // regardless of the street lamps -- so the lamps could never
            // read as distinct "pools of light," just extra brightness on
            // top of an already-lit scene. Full night now goes close to
            // black ambient with only the faintest moonlit sun, so the
            // lamps/signage/windows are close to the only visible light.
            new PhaseGrade
            {
                SunColor = new Color(0.35f, 0.4f, 0.65f), SunIntensity = 0.05f, SunElevationDeg = -8f,
                Ambient = new Color(0.02f, 0.02f, 0.05f), Fog = new Color(0.18f, 0.15f, 0.28f), FogDensity = 0.014f,
                NeonBoost = 2.2f, LampBoost = 1f,
                PostExposure = -0.35f, Saturation = 22f, ColorFilter = new Color(0.85f, 0.85f, 1.05f), Contrast = 18f,
                VignetteIntensity = 0.42f, BloomIntensity = 1.3f, FilmGrainIntensity = 0.3f,
            },
        };

        ApplyRegionTint(grades, region);

        // docs/28: the specific fields that were causing "way too bright"
        // now come from CityLightingProfile instead of another hardcoded
        // guess -- the creator's actual tuning knob. Everything else (sun
        // color/elevation, fog, color grading, vignette/grain) stays the
        // authored mood-board keyframe; only Night's ambient/bloom/boost
        // ceiling and Day's boost ceiling are profile-driven.
        var profile = CityLightingProfile.Active;
        var night = grades[(int)LumenPhase.Night];
        night.Ambient = new Color(profile.NightAmbientBrightness, profile.NightAmbientBrightness, profile.NightAmbientBrightness * 2f);
        night.BloomIntensity = profile.NightBloomIntensity;
        night.NeonBoost = profile.MaxNightBoost;
        grades[(int)LumenPhase.Night] = night;

        var day = grades[(int)LumenPhase.Day];
        day.NeonBoost = profile.DayNeonBoost;
        grades[(int)LumenPhase.Day] = day;

        return grades;
    }

    /// <summary>Per-region grade tint (docs/23 §8/§10: NY steel-blue noir,
    /// Paris warm cream, Montreal cold pastel) layered on the baseline as a
    /// multiply/add on color filter + saturation/contrast deltas only --
    /// sun/fog/lighting physics stay identical across regions, only the
    /// COLOR mood shifts, matching the plan's own "graphics ladder" framing
    /// (region is a paint job on the same rig, not a different rig).</summary>
    private static void ApplyRegionTint(PhaseGrade[] grades, CityRegion region)
    {
        Color filterMul;
        float saturationDelta;
        float contrastDelta;
        switch (region)
        {
            case CityRegion.NewYork:
                filterMul = new Color(0.92f, 0.96f, 1.08f); saturationDelta = -6f; contrastDelta = 6f;
                break;
            case CityRegion.Paris:
                filterMul = new Color(1.05f, 1f, 0.9f); saturationDelta = -4f; contrastDelta = -4f;
                break;
            case CityRegion.Montreal:
                filterMul = new Color(0.88f, 0.94f, 1.1f); saturationDelta = -12f; contrastDelta = -2f;
                break;
            default:
                return;   // Generic: baseline grade, untouched
        }

        for (var i = 0; i < grades.Length; i++)
        {
            var g = grades[i];
            g.ColorFilter = new Color(g.ColorFilter.r * filterMul.r, g.ColorFilter.g * filterMul.g, g.ColorFilter.b * filterMul.b);
            g.Saturation += saturationDelta;
            g.Contrast += contrastDelta;
            grades[i] = g;
        }
    }
}
