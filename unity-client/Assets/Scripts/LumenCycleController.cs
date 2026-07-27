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

    // 2026-07 creator report: "lights are too big and too bright on the
    // screen... nothing changes when I alter the DynamicLight." The
    // glowing BALLS are not the dynamic lights at all -- they're the
    // emissive bulb geometry, amplified by Bloom, which is what spreads a
    // small bright sphere into a big soft blob that can wash out the
    // playfield. These are THE two knobs for that, and unlike the
    // previous profile-only versions they're read live every frame, so
    // dragging them in Play mode changes the picture immediately.
    [Header("Glow brightness -- editable live in Play mode")]
    [Tooltip("Master multiplier on every emissive material in the city (bulbs, windows, neon). THIS is what controls how bright the glowing balls themselves look. Drop toward 0 to kill the glow entirely and see what's left.")]
    [Range(0f, 3f)]
    public float emissiveScale = 1f;

    // 2026-07 creator report: "some effect but even set to 0 way too
    // large." Real bug -- this used to be a value the code blended
    // TOWARD, weighted by how far into night it was (`nightAmount`),
    // which decays continuously from 1.0 back down through the whole
    // second half of the night phase (as it blends onward toward Dawn).
    // So 0 only ever meant "no bloom" at the single instant nightAmount
    // hit exactly 1.0 -- for most of "night" a real chunk of the OLD
    // hardcoded per-phase baseline (0.4 to 1.3) was still mixed in
    // regardless of this field. Now a genuine multiplier on the WHOLE
    // bloom curve at every time of day, same model emissiveScale already
    // correctly used -- 0 means zero bloom, always, full stop.
    [Tooltip("Multiplies bloom intensity at EVERY time of day (not just night). Bloom is what turns a small bright sphere into a BIG soft ball of light -- if the lights look too LARGE (rather than too bright), this is the knob, not emissiveScale. 0 = no bloom, ever.")]
    [Range(0f, 2f)]
    public float bloomScale = 0.3f;

    [Tooltip("Ambient light at full night. Near 0 gives a genuinely dark night the lamps can pool against.")]
    [Range(0f, 0.3f)]
    public float nightAmbient = 0.02f;

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

    /// <summary>Seed the live fields above from an authored profile asset
    /// -- called by RuntimeCityBuilder ONLY when one is actually
    /// assigned, so an unassigned profile leaves whatever was typed into
    /// this component's own Inspector alone instead of silently
    /// overwriting it.</summary>
    public void ApplyProfile(CityLightingProfile profile)
    {
        if (profile == null) return;
        bloomScale = profile.BloomScale;
        nightAmbient = profile.NightAmbientBrightness;
    }

    private void Start()
    {
        // 2026-07 creator report ("objects still black, no light I can
        // see"): RenderSettings.ambientLight is a no-op unless ambientMode
        // is Flat -- a fresh Unity scene defaults to Skybox mode, where
        // ambient instead comes from the skybox material (which, for a
        // procedural sky tied to the Sun, can go dark/unpredictable the
        // moment the directional light is disabled or aimed below the
        // horizon for Night). Every ambient value this controller computes
        // below -- day brightness, nightAmbient, the whole blend -- was
        // silently doing nothing without this. Set once; nothing else in
        // this project ever sets ambient mode.
        RenderSettings.ambientMode = AmbientMode.Flat;

        if (_grades == null) _grades = BuildGrades(CityRegion.Generic);

        // 2026-07 creator confusion: these auto-created objects have no
        // tunable fields of their own -- everything here gets overwritten
        // from LumenCycleController's OWN fields (on the RuntimeCityBuilder
        // GameObject) every frame, so hand-editing THESE never sticks.
        // Named defensively so that's obvious in the Hierarchy instead of
        // discovered the hard way.
        var sunGo = new GameObject("(auto) Sun -- edit LumenCycleController instead");
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

        // 2026-07 correction: the previous commit added a
        // `UnityEngine.Rendering.Universal.Exposure` VolumeComponent
        // override here to answer "does it have anything to do with
        // autoexposure?" -- that type does not exist. Unlike HDRP, URP
        // has no general scene-referred auto-exposure/eye-adaptation
        // Volume component at all; this compiled cleanly against
        // flightcheck's OWN hand-written stub (which had no way to catch
        // an invented type -- a real gap in that harness's reliability
        // for URP-specific code, noted in docs/12) but failed for real in
        // the Editor with CS0246. The real, only exposure control URP's
        // Volume stack offers is `ColorAdjustments.postExposure` above (a
        // manual EV offset, already wired since Phase 10.1) -- there is
        // no "automatic" mode to accidentally leave enabled here, so the
        // autoexposure theory for the brightness bug does not apply.
        // (A camera with "Use Physical Properties" enabled has its own
        // fixed, non-adaptive Aperture/Shutter/ISO exposure math, but
        // this project doesn't touch that either.)

        var volumeGo = new GameObject("(auto) Post Stack -- edit LumenCycleController instead");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        // high priority so this volume's overrides win over any
        // pre-existing default Volume the URP project template may have
        // shipped with -- this is the AUTHORED look, it should never lose
        // a priority tie to a template default.
        volume.priority = 100f;
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
        // kept immutable (unlike the phase-detection `t` below, which
        // subtracts as it walks phases) -- ComputeNightIntensity needs
        // the raw cycle-relative tick position.
        var cycleT = _frame % LumenClock.CycleTicks;
        var t = cycleT;
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

        // night-driven fields read the LIVE Inspector values above rather
        // than the baked keyframe, so Play-mode edits take effect this
        // frame -- `nightAmount` is how far into full night we are, which
        // is exactly the weight each of those live knobs should carry.
        //
        // 2026-07 creator direction, in two rounds. Round 1: "make the
        // lights fade a lot faster and hold for duration of the night,
        // then fade off during the daytime." The OLD nightAmount came
        // from the SAME continuous per-phase cross-fade as the sun/fog/
        // color-grading above -- it never held steady anywhere, drifting
        // toward Dawn's dimmer value across the ENTIRE Night phase (this
        // is literally the bug bloomScale was invented to route around,
        // see docs/12). Round 2 correction: "ALL the lights should turn
        // off during the day" + "ramp on quickly, hold for the duration
        // of the night, and turn shortly after dawn" -- round 1's shape
        // held through all of Dawn and faded out gradually across half of
        // Day, which wasn't what "shortly after dawn" meant. The fade-off
        // now happens WITHIN Dawn itself (fast, not lingering into Day at
        // all), and Day is a flat, hard 0 -- not the old 0.02-floor
        // near-zero. ComputeNightIntensity: fast ease-in early in Dusk,
        // flat hold through the rest of Dusk + all of Night, a fade-out
        // over the first part of Dawn, flat 0 for the rest of Dawn + all
        // of Day. Independent of the phase-blend mood-grading above --
        // sun color/fog/vignette/etc keep their previous 4-stop
        // continuous cross-fade, since this request is about "the
        // lights," not the whole day/night mood.
        var nightAmount = ComputeNightIntensity(cycleT);
        DayNightState.CycleProgress = cycleT / (float)LumenClock.CycleTicks;

        var ambient = Color.Lerp(a.Ambient, b.Ambient, blend);
        var nightAmbientColor = new Color(nightAmbient, nightAmbient, nightAmbient * 2f);
        RenderSettings.ambientLight = Color.Lerp(ambient, nightAmbientColor, nightAmount);
        var fogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, blend);
        RenderSettings.fog = fogDensity > 0.0005f;
        RenderSettings.fogColor = Color.Lerp(a.Fog, b.Fog, blend);
        RenderSettings.fogDensity = fogDensity;

        // Hard 0 -> Night's own authored NeonBoost are the two stops --
        // same reasoning as nightAmount just above, so the glowing bulbs/
        // signs/windows snap on and off in step with the real lights and
        // ambient darkness instead of lagging behind on the slower
        // 4-stop mood curve. 2026-07 correction: this used to blend
        // toward Day's own authored NeonBoost (0.35 -- "barely visible
        // against daylight," the ORIGINAL design intent), not all the way
        // to 0. "ALL the lights should turn off during the day" supersedes
        // that -- 0 is the low stop now, so nightAmount == 0 (all of Day)
        // means genuinely off, not just dim.
        var nightNeonBoost = _grades[(int)LumenPhase.Night].NeonBoost;
        var neonBoost = Mathf.Lerp(0f, nightNeonBoost, nightAmount) * emissiveScale;
        NeonRegistry.SetBoost(neonBoost);
        DayNightState.NeonBoost = neonBoost;
        DayNightState.NightAmount = nightAmount;

        _colorAdjustments.postExposure.value = Mathf.Lerp(a.PostExposure, b.PostExposure, blend);
        _colorAdjustments.saturation.value = Mathf.Lerp(a.Saturation, b.Saturation, blend);
        _colorAdjustments.colorFilter.value = Color.Lerp(a.ColorFilter, b.ColorFilter, blend);
        _colorAdjustments.contrast.value = Mathf.Lerp(a.Contrast, b.Contrast, blend);
        _vignette.intensity.value = Mathf.Lerp(a.VignetteIntensity, b.VignetteIntensity, blend);
        // bloomScale is a true multiplier on the whole authored bloom
        // curve at every time of day (same model as emissiveScale) --
        // NOT blended toward, so 0 is always exactly 0, never partially
        // mixed with the old per-phase baseline regardless of nightAmount.
        _bloom.intensity.value = Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, blend) * bloomScale;
        _filmGrain.intensity.value = Mathf.Lerp(a.FilmGrainIntensity, b.FilmGrainIntensity, blend);
    }

    // Fraction of Dusk's duration spent easing 0->1 ("a lot faster" per
    // the creator's own words -- Dusk is 30s/300 ticks, so 0.25 is a
    // 7.5s fade-in, roughly 4x quicker than the old full-phase ramp).
    private const float FadeInFraction = 0.25f;
    // 2026-07 correction: this used to be "fraction of DAY spent easing
    // out," at 0.5 (a leisurely 45s). Creator: "turn shortly after dawn"
    // -- the fade-off belongs in Dawn, not lingering into Day, and
    // should read as prompt, not leisurely. Fraction of DAWN'S duration
    // now (Dawn is 30s/300 ticks -- 0.35 is a ~10.5s fade, starting
    // right at the Dawn boundary where Night's hold ends).
    private const float FadeOutFraction = 0.35f;

    /// <summary>"The lights" trapezoid: fast ease-in early in Dusk, flat
    /// 1.0 through the rest of Dusk + all of Night, an eased fade-out
    /// over the first part of Dawn, flat 0.0 for the rest of Dawn + all
    /// of Day. Independent of the continuous per-phase cross-fade
    /// `ApplyBlend` still uses for sun/fog/color-grading -- this is
    /// specifically the curve for "the lights holding through the
    /// night, fading off shortly after dawn, off all day" (2026-07
    /// creator direction, corrected in a second round -- see this
    /// method's git history / docs/12 for the shape round 1 actually
    /// had), not a general mood blend.</summary>
    private static float ComputeNightIntensity(int cycleT)
    {
        var dawnEnd = LumenClock.DawnTicks;
        var dayEnd = dawnEnd + LumenClock.DayTicks;
        var duskEnd = dayEnd + LumenClock.DuskTicks;

        if (cycleT < dawnEnd)                // Dawn: fade out promptly, then hold off
        {
            var fadeTicks = LumenClock.DawnTicks * FadeOutFraction;
            if (cycleT >= fadeTicks) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, cycleT / fadeTicks);
        }

        if (cycleT < dayEnd) return 0f;      // Day: held fully off, no exceptions

        if (cycleT < duskEnd)                // Dusk: fast fade in, then hold on
        {
            var duskT = cycleT - dayEnd;
            var fadeTicks = LumenClock.DuskTicks * FadeInFraction;
            if (duskT >= fadeTicks) return 1f;
            return Mathf.SmoothStep(0f, 1f, duskT / fadeTicks);
        }

        return 1f;   // Night: fully on the whole phase
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

        // NOTE: Night's ambient/bloom and the neon boost ceiling are NOT
        // baked in here any more -- ApplyBlend reads the live
        // `nightAmbient`/`bloomScale`/`emissiveScale` fields instead, so
        // they're tunable in Play mode. Baking them here was the reason
        // "nothing changes when I alter" anything (2026-07 creator report).

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
