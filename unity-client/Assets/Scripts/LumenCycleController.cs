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
        // 2026-07 creator direction: "I need the sun to move across the
        // sky in a realistic manner, so shadows move and shift
        // realistically." This used to be a single fixed constant
        // (SunYawDeg = -35f, "only elevation/color animate") -- the sun
        // bobbed up and down in place but never swept the compass, so
        // shadow DIRECTION never changed, only shadow LENGTH. Per-phase
        // now, blended the same way SunElevationDeg is, tracing one
        // continuous sweep across the whole cycle (see the wrap-aware
        // math in ApplyBlend -- this field alone isn't quite enough,
        // since Night's blend target needs +360 to keep sweeping FORWARD
        // through the below-horizon side back to the next Dawn instead
        // of reversing back across the daytime side).
        public float SunYawDeg;
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

    // 2026-07 creator: "it should add a glow to the light diffusing it,
    // like real lights in the fog." bloomScale alone is a flat multiplier
    // independent of conditions -- this ADDS an extra, fog-density-driven
    // boost on top, so bloom (the mechanism that turns a small bright
    // point into a soft spread-out glow, per bloomScale's own tooltip)
    // gets stronger specifically when it's foggy. `1 + fogDensity *
    // fogGlowBoost` -- at fogDensity ~0.022 (this session's bumped Night
    // value) and the default 15, that's a ~1.33x bloom boost at full
    // night fog; scales down toward 1x (no boost) as fog clears.
    [Tooltip("How much CURRENT fog density boosts bloom on top of bloomScale -- final multiplier is (1 + fogDensity * this). 0 = fog has no effect on bloom.")]
    [Range(0f, 40f)]
    public float fogGlowBoost = 15f;

    // 2026-07 creator: "I want the lights to truly pop, bright and
    // diffuse through the fog" (referencing an HDRP volumetric-fog
    // technique -- see the maddr-lighting-system skill for why that
    // specific approach doesn't port to this project's URP pipeline, and
    // what a URP-native "light source fog" approximation looks like).
    //
    // Two DIFFERENT Bloom parameters were never explicitly set anywhere
    // in this file before now, both silently riding whatever URP's
    // Bloom component defaults to:
    //  - `scatter` controls how FAR/SOFT the blur spreads -- the actual
    //    "diffuse" knob (bigger = a wider, hazier glow), distinct from
    //    `intensity` (how much bloom gets ADDED, i.e. brightness).
    //    fogGlowBoost above only ever touched intensity.
    //  - `threshold` is the HDR brightness cutoff above which a pixel
    //    blooms at all -- lower means more of the scene contributes,
    //    which is the "pop" half: at a high default threshold, only the
    //    very brightest core of a light blooms; lowering it lets a
    //    bigger share of each light's own brightness read as glow.
    [Tooltip("Base Bloom scatter (spread/softness of the blur) at every time of day -- URP's own field, just never wired up here before. Higher = a wider, hazier glow.")]
    [Range(0f, 1f)]
    public float bloomScatter = 0.7f;

    [Tooltip("How much CURRENT fog density ADDS to bloomScatter (clamped to 1) -- fog specifically makes the glow more diffuse/spread-out, not just brighter. 0 = fog doesn't change scatter.")]
    [Range(0f, 50f)]
    public float fogDiffusionBoost = 8f;

    [Tooltip("HDR brightness cutoff above which a pixel blooms at all. LOWER this for lights to 'pop' more readily -- URP's own Bloom default (~0.9) is fairly conservative, letting only a light's brightest core bloom.")]
    [Range(0f, 2f)]
    public float bloomThreshold = 0.6f;

    // 2026-08 creator direction, reversing the earlier "ALL the lights
    // should turn off during the day" round (see neonBoost's own comment
    // in ApplyBlend): "I want to see lights on during daylight as well as
    // night." This is the LOW stop of the neonBoost Lerp below -- was a
    // hard 0f, now this live field, matching the original pre-"all off"
    // design (Day's own authored PhaseGrade.NeonBoost was 0.35, "barely
    // visible against daylight") and CityLightingProfile's own
    // DayNeonBoost field, which existed but was never actually wired up
    // to anything after that earlier round hardcoded 0f in its place.
    // The trapezoid TIMING (off through most of Day, ramp at the dial's
    // 5:00, hold through Dusk/Night, fade in Dawn) is unchanged -- this
    // only raises the floor of the "off" stretch so it reads as "faint,
    // barely-there daytime glow" instead of "completely dark."
    [Tooltip("Neon/bulb boost during full daylight (before the evening ramp) -- 0 means lights are fully off until the ramp; raise this so streetlamps/windows/neon stay faintly visible even at midday. Blends up to the full night boost as nightAmount ramps toward 1 -- same ramp timing as before, this only changes the low end.")]
    [Range(0f, 1f)]
    public float dayNeonBoost = 0.35f;

    // 2026-07 creator: "Give me both per phase and a multiplier fog
    // density." Fog density previously existed only as hardcoded literals
    // inside BuildGrades' per-phase PhaseGrade table (each phase's own
    // Exp2 fog thickness, cross-fading same as everything else in
    // ApplyBlend) -- not Inspector-editable, and with no live global
    // scale over the whole curve the way bloomScale/emissiveScale already
    // give every OTHER per-phase-authored value. These four now feed
    // BuildGrades directly (read once at Init/ApplyProfile time, same as
    // every other authored keyframe number) and fogDensityScale below
    // multiplies the already-blended result live every frame, same model
    // as bloomScale.
    [Header("Fog density per phase -- editable live in Play mode (rebuild the city for the per-phase values to take effect; the multiplier applies live)")]
    [Tooltip("Fog density at Dawn.")]
    [Range(0f, 0.1f)]
    public float fogDensityDawn = 0.010f;

    [Tooltip("Fog density at Day.")]
    [Range(0f, 0.1f)]
    public float fogDensityDay = 0.005f;

    [Tooltip("Fog density at Dusk.")]
    [Range(0f, 0.1f)]
    public float fogDensityDusk = 0.014f;

    [Tooltip("Fog density at full Night.")]
    [Range(0f, 0.1f)]
    public float fogDensityNight = 0.034f;

    // 2026-07 creator direction: explicit defaults for both fog knobs --
    // "set default fog density scale: 0.41. And night fog: 0.034."
    [Tooltip("Multiplies the blended fog density curve at EVERY time of day, live -- the quick 'more/less fog overall' knob, same model as bloomScale. 1 = the per-phase values above, unchanged.")]
    [Range(0f, 3f)]
    public float fogDensityScale = 0.41f;

    // 2026-07 creator, after the shadow-lift addition below: "still need
    // an ambient light so we can see in the darkest part of the night."
    // nightFillLift only touches the FINAL rendered pixel's color (a
    // post-process grade) -- it never changes how anything is actually
    // LIT, so it can't help depth/silhouette read the way real ambient
    // light does. Raised from the near-black 0.02 a few rounds back
    // (deliberately crushed so lamps would read as distinct pools) to
    // 0.08, then "still way too dark. triple it." -> 0.24, then "the
    // minimum nightAmbient should be at least 0.12" -- a durable floor,
    // not just another one-off bump, so a future profile asset or
    // Inspector edit can't silently regress back toward the original
    // near-black value this whole back-and-forth was about escaping.
    // MinNightAmbient below is enforced in ApplyBlend at the point of
    // use (Mathf.Max), not just this field's [Range] slider -- a
    // slider's min only guards direct Inspector drags, not
    // ApplyProfile() copying in a lower value from a CityLightingProfile
    // asset.
    // 2026-07 correction, same cinematic brief as nightAmbientTint above:
    // "do not globally brighten the scene." nightAmbient is a FLAT,
    // uniform light -- it raises the floor everywhere equally, unlit
    // gaps and already-bright lamp pools alike, which is the definition
    // of "globally brighten." Pulled back from 0.45 toward a more
    // moderate value; nightFillLift below (a SURGICAL near-black-only
    // lift, not global) now does more of the actual readability work,
    // which is what the brief is asking for by name ("shadows and
    // contrast," "avoid flattening").
    // 2026-07 CORRECTION, first real screenshot of this system: the row-31
    // combination (this at 0.35, Contrast 24, PostExposure -0.4) rendered
    // as near-TOTAL BLACK -- roads/terrain/buildings invisible, only the
    // HQ/Factory tint squares and unit blips legible at all. That's not
    // "moody," it's broken. Creator confirmed goal, plainly: "visibility/
    // playability." Raised back up, now past every prior round (0.24 ->
    // 0.45 -> 0.35 -> 0.55) -- readability is no longer being traded
    // against mood; the Contrast/PostExposure counterweight that likely
    // did the actual crushing is rolled back separately, in BuildGrades'
    // Night keyframe below.
    [Tooltip("Ambient light at full night. 0.02 (the original default) was near-black, deliberately, so the lamps would pool against real darkness -- raise this if the UNLIT parts of the night are too dark to read at all; lower it if the lamp 'pools of light' look gets washed out. Hard-floored at MinNightAmbient regardless of what's set here. Distinct from nightFillLift below (a post-process grade, doesn't affect actual scene lighting).")]
    [Range(0.12f, 1f)]
    public float nightAmbient = 0.55f;

    // 2026-07 creator: "the middle of the night is unplayable, it needs to
    // be significantly lighter, which is what actually happens due to
    // light pollution." First pass: nightAmbient's default raised
    // 0.24 -> 0.45, and this tint was made WARM amber (the OLD hardcoded
    // formula was blue-only, a moonlight read, not light-pollution
    // skyglow).
    //
    // 2026-07 CORRECTED, same session, with a fuller cinematic brief:
    // "1950's sci-fi cinema... darkness remains dominant... use a cool
    // night color palette... do not globally brighten the scene." A flat
    // WARM ambient was the wrong fix -- it washes the whole picture one
    // color (exactly the "flattening" the brief warns against) and reads
    // as dusk, not night. Real day-for-night cinematography (the actual
    // technique this brief is asking for) keeps ambient/fill COOL (blue
    // moonlight/skyglow) specifically so the WARM practical lights --
    // streetlamp/window/neon materials plus LampBoost/NeonBoost, both
    // already warm-toned and untouched here -- read as contrast against
    // it. That contrast, not raw brightness, is what makes a night scene
    // both legible and still feel like night. Back to cool, now
    // deliberately (not the same as the pre-this-session default: this
    // is a moodier teal-blue skyglow, not the old moonlight-blue).
    // 2026-07 correction (same screenshot-driven round as nightAmbient
    // above): kept the cool cast (still the right cinematic call -- see
    // the long comment above this field) but lightened it so it doesn't
    // multiply the now-higher nightAmbient magnitude back down toward
    // black. A deep, saturated tint at 0.55/0.72/1 was fighting the very
    // brightness increase it's applied to.
    [Tooltip("Color tint multiplied onto nightAmbient's brightness at full night. Default is a cool blue-teal -- day-for-night skyglow/moonlight, deliberately COOL so the warm practical lights (streetlamps/windows/neon) read as contrast against it rather than the whole frame washing one color. Raise B, lower R/G for a colder/moodier night; the reverse trends toward a (less cinematic, flatter) warm wash. Keep this reasonably close to (1,1,1) rather than deeply saturated -- a strong tint multiplies DOWN whatever nightAmbient's magnitude is, fighting visibility.")]
    public Color nightAmbientTint = new Color(0.68f, 0.8f, 1f);

    private const float MinNightAmbient = 0.12f;

    // 2026-07 creator: "the night is too dark, what we need is a hdr like
    // fill so everything isn't so dark." Raising nightAmbient itself was
    // the OTHER available knob, but it's a flat additive light -- pushing
    // it up brightens the unlit gaps AND the already-bright lamp pools by
    // the same amount, which is exactly the "lamps read as distinct pools
    // of light" look nightAmbient was deliberately dropped near-zero to
    // protect a few rounds back. An HDR-style shadow lift is a different
    // tool: it raises the floor specifically on near-black PIXELS (via
    // URP's ShadowsMidtonesHighlights grader, which is the standard "lift
    // shadow detail without touching highlights" post-processing tool --
    // same family as Lift/Gamma/Gain, just tonal-range-based instead of
    // exposure-based) so unlit streets/building faces stop reading as pure
    // crushed black while a lit lamp or window stays comparatively brighter
    // than its surroundings, preserving the pool-of-light contrast instead
    // of flattening it.
    // 2026-07: raised again as the PRIMARY readability tool (see
    // nightAmbient's own comment just above) -- this is the "lift shadow
    // detail without touching highlights/flattening the image" technique
    // the cinematic brief specifically asked to review (Lift/Gamma/Gain),
    // and it's the one post-process grade in this file that structurally
    // can't wash out the lamp-pool contrast the way a flat ambient bump
    // can: it only ever touches near-black pixels, so a bright lamp/
    // window/monster stays exactly as bright relative to its surroundings
    // as before, while the previously-crushed unlit gaps between them
    // gain real detail.
    // 2026-07 correction, same screenshot-driven round: raised further,
    // 0.6 -> 0.75, alongside nightAmbient above -- the actual crushing
    // culprit was very likely the Contrast/PostExposure bump in
    // BuildGrades' Night keyframe (rolled back below), not this field,
    // but pushing this higher too gives more margin now that visibility
    // is the stated priority.
    [Tooltip("HDR-style shadow lift at night: raises the floor of near-black areas (unlit streets, building shadows) without touching bright areas, so the picture doesn't read as crushed black -- unlike nightAmbient, which brightens EVERYTHING uniformly and can wash out the lamp 'pools of light' look. THIS is the primary night-readability knob -- prefer raising it over nightAmbient. 0 = no lift (old behavior).")]
    [Range(0f, 1f)]
    public float nightFillLift = 0.75f;

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
    private ShadowsMidtonesHighlights _shadowsMidtonesHighlights;

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
        fogGlowBoost = profile.FogGlowBoost;
        bloomScatter = profile.BloomScatter;
        fogDiffusionBoost = profile.FogDiffusionBoost;
        bloomThreshold = profile.BloomThreshold;
        nightAmbient = profile.NightAmbientBrightness;
        nightAmbientTint = profile.NightAmbientTint;
        nightFillLift = profile.NightFillLift;
        dayNeonBoost = profile.DayNeonBoost;
        fogDensityDawn = profile.FogDensityDawn;
        fogDensityDay = profile.FogDensityDay;
        fogDensityDusk = profile.FogDensityDusk;
        fogDensityNight = profile.FogDensityNight;
        fogDensityScale = profile.FogDensityScale;
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
        //
        // 2026-07 (cinematic-night brief, "review baked lighting settings
        // / improve indirect bounce lighting"): confirmed via direct scene
        // inspection (docs/28 row 24) that NOTHING in this project is
        // baked -- no GameObject sets `isStatic`/`staticEditorFlags`, and
        // `SampleScene.unity`'s `m_LightingSettings: {fileID: 0}` means no
        // Lighting Settings asset is even assigned. There is no lightmap
        // bake and no realtime GI solution running (URP's newer Adaptive
        // Probe Volumes aren't set up either) -- this Flat `ambientLight`
        // value IS the entire indirect/bounce-light stand-in for the
        // whole city. That's exactly why nightAmbient/nightFillLift carry
        // so much weight for readability: there's no actual light
        // bouncing off a building's lit face onto its neighbor to fall
        // back on. Setting up Adaptive Probe Volumes for real bounce
        // light is a genuine option but a substantial Editor-side task
        // (probe placement/baking) this environment can't do blind --
        // flagged as a real future avenue, not attempted here.
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
        _shadowsMidtonesHighlights = profile.Add<ShadowsMidtonesHighlights>(true);

        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.colorFilter.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;
        _vignette.intensity.overrideState = true;
        _bloom.intensity.overrideState = true;
        _bloom.threshold.overrideState = true;
        _bloom.scatter.overrideState = true;
        _filmGrain.intensity.overrideState = true;
        _tonemapping.mode.overrideState = true;
        _tonemapping.mode.value = TonemappingMode.ACES;   // filmic tonemapping, per §10's own target look
        // only the shadows wheel is ever driven -- midtones/highlights stay
        // at their neutral (1,1,1,0) default, i.e. this ONLY lifts dark
        // pixels, never touches mid/bright ones.
        _shadowsMidtonesHighlights.shadows.overrideState = true;

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
        //
        // 2026-07 creator: "the sun should be moving throughout the day,
        // not just sunrise and sunset." Unlike SunYawDeg (below), elevation
        // was still using the plain adjacent-phase Lerp -- which locks each
        // phase's elevation SWEEP to whatever its two authored keyframes
        // happen to differ by, with no regard for how long that phase
        // actually lasts. Dawn (30s) and Day (90s) both authored a
        // 27-degree swing, so elevation moved 3x faster during Dawn than
        // during Day -- a visible bob at the Dawn/Dusk transitions, a
        // near-flat crawl through the long Day/Night phases. See
        // ComputeSunElevationDeg below.
        var elevation = ComputeSunElevationDeg(cycleT);
        // Yaw sweeps the compass continuously across the whole cycle
        // (Dawn->Day->Dusk->Night->next Dawn), each phase's own
        // SunYawDeg a waypoint on ONE monotonic sweep, proportioned to
        // that phase's share of the cycle (see BuildGrades) so angular
        // speed stays constant rather than visibly speeding up during
        // the shorter Dawn/Dusk phases. Night is the one wrap point:
        // its target (next Dawn's own SunYawDeg, a SMALLER raw number
        // than Night's) needs +360 added before lerping, or the sweep
        // would reverse direction and cross back over the daytime side
        // of the sky instead of continuing forward through the
        // below-horizon side. Quaternion.Euler handles any angle > 360
        // fine (sin/cos are periodic) -- the +360 only matters for
        // getting the INTERPOLATION direction right, not the final angle.
        var targetYaw = phase == LumenPhase.Night ? b.SunYawDeg + 360f : b.SunYawDeg;
        var yaw = Mathf.Lerp(a.SunYawDeg, targetYaw, blend);
        _sun.transform.rotation = Quaternion.Euler(elevation, yaw, 0f);

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
        // near-zero. Round 3: "the lights should start turning on by
        // 5:00 pm on the clock" -- moved the ease-in from early Dusk to
        // the AnalogClockHud dial's 5:00 position (which lands inside
        // Day, ahead of Dusk), see ComputeNightIntensity's own header for
        // the exact tick derivation. Independent of the phase-blend
        // mood-grading above -- sun color/fog/vignette/etc keep their
        // previous 4-stop continuous cross-fade, since this request is
        // about "the lights," not the whole day/night mood.
        var nightAmount = ComputeNightIntensity(cycleT);
        DayNightState.CycleProgress = cycleT / (float)LumenClock.CycleTicks;

        var ambient = Color.Lerp(a.Ambient, b.Ambient, blend);
        var flooredNightAmbient = Mathf.Max(nightAmbient, MinNightAmbient);
        var nightAmbientColor = new Color(
            flooredNightAmbient * nightAmbientTint.r,
            flooredNightAmbient * nightAmbientTint.g,
            flooredNightAmbient * nightAmbientTint.b);
        RenderSettings.ambientLight = Color.Lerp(ambient, nightAmbientColor, nightAmount);
        // fogDensityScale is a true multiplier on the whole per-phase
        // curve, live every frame -- same model as bloomScale/
        // emissiveScale, not blended toward like the phase keyframes
        // themselves.
        var fogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, blend) * fogDensityScale;
        RenderSettings.fog = fogDensity > 0.0005f;
        RenderSettings.fogColor = Color.Lerp(a.Fog, b.Fog, blend);
        RenderSettings.fogDensity = fogDensity;

        // dayNeonBoost -> Night's own authored NeonBoost are the two
        // stops -- same reasoning as nightAmount just above, so the
        // glowing bulbs/signs/windows track the real lights and ambient
        // darkness instead of lagging behind on the slower 4-stop mood
        // curve. 2026-08 correction: this briefly used a hard 0f low stop
        // ("ALL the lights should turn off during the day"), then the
        // creator reversed that -- "I want to see lights on during
        // daylight as well as night" -- so the low stop is dayNeonBoost
        // (a live field) again, matching the original pre-"all off"
        // design (Day's own authored NeonBoost was 0.35).
        var nightNeonBoost = _grades[(int)LumenPhase.Night].NeonBoost;
        var neonBoost = Mathf.Lerp(dayNeonBoost, nightNeonBoost, nightAmount) * emissiveScale;
        NeonRegistry.SetBoost(neonBoost);
        DayNightState.NeonBoost = neonBoost;
        DayNightState.NightAmount = nightAmount;

        // HDR-style shadow lift, weighted by nightAmount the same way
        // neonBoost/lamp intensity already are -- 0 lift all through Day
        // (nightAmount==0, untouched daytime image), ramping to
        // nightFillLift's full value for the held Dusk/Night stretch. Only
        // the W component moves (a luminance-only offset on near-black
        // pixels); X/Y/Z stay at 1 so this never recolors anything, just
        // raises the floor.
        var shadowLift = Mathf.Lerp(0f, nightFillLift, nightAmount);
        _shadowsMidtonesHighlights.shadows.value = new Vector4(1f, 1f, 1f, shadowLift);

        _colorAdjustments.postExposure.value = Mathf.Lerp(a.PostExposure, b.PostExposure, blend);
        _colorAdjustments.saturation.value = Mathf.Lerp(a.Saturation, b.Saturation, blend);
        _colorAdjustments.colorFilter.value = Color.Lerp(a.ColorFilter, b.ColorFilter, blend);
        _colorAdjustments.contrast.value = Mathf.Lerp(a.Contrast, b.Contrast, blend);
        _vignette.intensity.value = Mathf.Lerp(a.VignetteIntensity, b.VignetteIntensity, blend);
        // bloomScale is a true multiplier on the whole authored bloom
        // curve at every time of day (same model as emissiveScale) --
        // NOT blended toward, so 0 is always exactly 0, never partially
        // mixed with the old per-phase baseline regardless of nightAmount.
        // fogGlowBoost adds an EXTRA boost riding the current fogDensity
        // (already computed above for RenderSettings.fogDensity) on top
        // of that -- "real lights in the fog" get a softer, more diffuse
        // glow specifically when it's foggy, not just a flat multiplier.
        var fogGlow = 1f + fogDensity * fogGlowBoost;
        _bloom.intensity.value = Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, blend) * bloomScale * fogGlow;
        // threshold/scatter were both previously left at whatever URP's
        // Bloom component defaults to -- never assigned despite threshold
        // having overrideState=true since Phase 1. scatter is the actual
        // "diffuse" lever (spread of the blur); threshold is the "pop"
        // lever (how much of each light's own brightness counts at all).
        // Fog specifically widens scatter -- "diffuse through the fog" --
        // clamped to 1 since it's a normalized URP parameter, not an
        // open-ended multiplier like intensity.
        _bloom.threshold.value = bloomThreshold;
        _bloom.scatter.value = Mathf.Clamp01(bloomScatter + fogDensity * fogDiffusionBoost);
        _filmGrain.intensity.value = Mathf.Lerp(a.FilmGrainIntensity, b.FilmGrainIntensity, blend);
    }

    // 2026-07 correction: this used to be "fraction of DAY spent easing
    // out," at 0.5 (a leisurely 45s). Creator: "turn shortly after dawn"
    // -- the fade-off belongs in Dawn, not lingering into Day, and
    // should read as prompt, not leisurely. Fraction of DAWN'S duration
    // now (Dawn is 30s/300 ticks -- 0.35 is a ~10.5s fade, starting
    // right at the Dawn boundary where Night's hold ends).
    private const float FadeOutFraction = 0.35f;

    // 2026-07 creator: "The lights should start turning on by 5:00 pm
    // on the clock." Ties directly to AnalogClockHud's own dial mapping
    // (one full Lumen cycle = one 12-hour dial revolution --
    // HourHandDeg = cycleProgress * 360) -- dial position 5:00 is 5/12
    // of a full revolution, i.e. cycle tick 2400 * 5/12 = 1000 exactly.
    // That lands solidly inside DAY (300-1200), well before Dusk even
    // starts (1200 -- which happens to land EXACTLY on the dial's 6:00,
    // a clean coincidence worth noting, but not what was asked for).
    // Ramp duration itself is unchanged from the old Dusk-relative fast
    // fade-in (25% of Dusk's 300 ticks = 75 ticks, ~7.5s) -- just
    // detached from Dusk's own length now that the ramp starts earlier,
    // so lights are fully on by dial ~5:22, comfortably ahead of Dusk.
    private const int LightsOnStartTick = 1000;   // dial 5:00 (CycleTicks * 5/12)
    private const float LightsOnRampTicks = 75f;

    /// <summary>"The lights" trapezoid: off through Dawn + most of Day,
    /// a fast ease-in starting at the dial's 5:00 (well before Dusk),
    /// flat 1.0 from there through the rest of Day + all of Dusk + all
    /// of Night, an eased fade-out over the first part of Dawn. Independent
    /// of the continuous per-phase cross-fade `ApplyBlend` still uses for
    /// sun/fog/color-grading -- this is specifically the curve for "the
    /// lights holding through the night, fading off shortly after dawn,
    /// starting by 5pm on the dial" (2026-07 creator direction, refined
    /// across several rounds -- see docs/12 for the earlier shapes this
    /// went through), not a general mood blend.</summary>
    private static float ComputeNightIntensity(int cycleT)
    {
        var dawnEnd = LumenClock.DawnTicks;

        if (cycleT < dawnEnd)                // Dawn: fade out promptly, then hold off
        {
            var fadeTicks = LumenClock.DawnTicks * FadeOutFraction;
            if (cycleT >= fadeTicks) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, cycleT / fadeTicks);
        }

        if (cycleT < LightsOnStartTick) return 0f;   // rest of Dawn + most of Day: off

        var rampT = cycleT - LightsOnStartTick;
        if (rampT < LightsOnRampTicks)               // dial 5:00 -> ~5:22: fast fade in
            return Mathf.SmoothStep(0f, 1f, rampT / LightsOnRampTicks);

        return 1f;   // rest of Day, all of Dusk, all of Night: fully on
    }

    /// <summary>Sun elevation as one continuous arc across the whole
    /// cycle, instead of ApplyBlend's old plain adjacent-phase Lerp
    /// (which SunYawDeg's own comment already explains the same problem
    /// for: sweep magnitude locked to two authored keyframes' raw
    /// difference, with no regard for how long the phase between them
    /// actually lasts). 2026-07 creator: "the sun should be moving
    /// throughout the day, not just sunrise and sunset."
    ///
    /// Fix: put the peak and trough at each long phase's MIDPOINT (solar
    /// noon, solar midnight) instead of only at phase boundaries, so the
    /// climb from Dawn's low anchor to Day's peak spans Dawn + the first
    /// half of Day, and the descent from that peak to Dusk's low anchor
    /// spans the second half of Day + Dusk -- every tick of Day sits
    /// inside an actively-moving segment, none of it a flat crawl. Same
    /// shape mirrored for Night's trough. Reuses BuildGrades' own
    /// authored elevation keyframes (Dawn/Dusk both low, Day the peak,
    /// Night the trough) -- no new tuning numbers, just a different
    /// interpolation between the same four values.
    ///
    /// Each of the 4 segments still eases with SmoothStep (matching this
    /// controller's "unhurried" style everywhere else) -- the two real
    /// turning points (midday peak, midnight trough) get a genuine
    /// rounded top/bottom from that; the two pass-through points (Dawn's
    /// own anchor at the wrap, Dusk's own anchor) get a momentary, brief
    /// softening right at that instant rather than a true stop -- a small
    /// trade-off, and a far smaller one than the 3x sustained rate
    /// mismatch this replaces.</summary>
    private float ComputeSunElevationDeg(int cycleT)
    {
        var dawnElev = _grades[(int)LumenPhase.Dawn].SunElevationDeg;
        var dayPeakElev = _grades[(int)LumenPhase.Day].SunElevationDeg;
        var duskElev = _grades[(int)LumenPhase.Dusk].SunElevationDeg;
        var nightTroughElev = _grades[(int)LumenPhase.Night].SunElevationDeg;

        var dawnEnd = LumenClock.DawnTicks;
        var midDay = dawnEnd + LumenClock.DayTicks / 2;
        var dayEnd = dawnEnd + LumenClock.DayTicks;
        var duskEnd = dayEnd + LumenClock.DuskTicks;
        var midNight = duskEnd + LumenClock.NightTicks / 2;

        if (cycleT < midDay)
        {
            var progress = cycleT / (float)midDay;
            return Mathf.Lerp(dawnElev, dayPeakElev, Mathf.SmoothStep(0f, 1f, progress));
        }
        if (cycleT < dayEnd)
        {
            var progress = (cycleT - midDay) / (float)(dayEnd - midDay);
            return Mathf.Lerp(dayPeakElev, duskElev, Mathf.SmoothStep(0f, 1f, progress));
        }
        if (cycleT < midNight)
        {
            var progress = (cycleT - dayEnd) / (float)(midNight - dayEnd);
            return Mathf.Lerp(duskElev, nightTroughElev, Mathf.SmoothStep(0f, 1f, progress));
        }
        var wrapProgress = (cycleT - midNight) / (float)(LumenClock.CycleTicks - midNight);
        return Mathf.Lerp(nightTroughElev, dawnElev, Mathf.SmoothStep(0f, 1f, wrapProgress));
    }

    private PhaseGrade[] BuildGrades(CityRegion region)
    {
        // 2026-07: FogDensity across all four phases bumped ~1.5-1.7x
        // (creator: "make it thicker/more present overall" -- half of a
        // two-part fog request, the other half being fogGlowBoost/
        // fogDimReferenceDensity above and on DynamicLightBudget). Chosen
        // to stay readable rather than a total whiteout -- Night's new
        // 0.022 against Exp2 falloff puts ~70% fog blend at a 50m view
        // distance, noticeably present without erasing the RTS camera's
        // own view of the action.
        //
        // 2026-07 follow-up: these four values (and the fogDensityScale
        // multiplier applied later in ApplyBlend) are now the live
        // fogDensityDawn/Day/Dusk/Night Inspector fields above instead of
        // hardcoded literals -- BuildGrades became an instance method
        // (was static) specifically so it can read them. Only takes
        // effect at Init/city-build time (these bake into the per-phase
        // keyframe table, unlike fogDensityScale which applies live every
        // frame) -- same "rebuild to see it" caveat every other
        // BuildGrades-baked value already has.
        //
        // 2026-07: SunYawDeg values below trace ONE continuous compass
        // sweep across the whole cycle (creator: "the sun to move across
        // the sky in a realistic manner, so shadows move and shift
        // realistically" -- yaw used to be a single fixed constant).
        // Each phase's share of the 360 degree sweep is proportioned to
        // its OWN share of the 2400-tick cycle (LumenClock: Dawn/Dusk
        // 300 ticks each, Day/Night 900 each) so angular speed -- and
        // therefore how fast shadows visibly swing -- stays constant
        // instead of the sun looking like it speeds up during the
        // shorter Dawn/Dusk phases: Dawn 60->105 (45 deg), Day 105->240
        // (135 deg), Dusk 240->285 (45 deg), Night 285->(285+135=420=)
        // 60 (135 deg, wrapping seamlessly back to Dawn's own 60 -- see
        // the Night-specific +360 handling in ApplyBlend). Also: Dawn/
        // Dusk's own SunElevationDeg dropped from 8/4 to a matching 3 --
        // "I want those long shadows at sunrise and sunset" -- lower and
        // now symmetric, so both transitions start (Dawn) or end (Dusk)
        // right at the dramatic near-horizon angle rather than Dawn
        // already being partway risen.
        var grades = new[]
        {
            // Dawn
            new PhaseGrade
            {
                SunColor = new Color(1f, 0.75f, 0.55f), SunIntensity = 0.6f, SunElevationDeg = 3f, SunYawDeg = 60f,
                Ambient = new Color(0.35f, 0.32f, 0.38f), Fog = new Color(0.4f, 0.32f, 0.38f), FogDensity = fogDensityDawn,
                NeonBoost = 0.9f, LampBoost = 0.5f,
                PostExposure = 0f, Saturation = -5f, ColorFilter = new Color(1f, 0.93f, 0.85f), Contrast = 5f,
                VignetteIntensity = 0.25f, BloomIntensity = 0.6f, FilmGrainIntensity = 0.15f,
            },
            // Day -- sun-baked, dusty, desaturated-sepia warmth (2026-07 daytime mood-board addition), NOT a clean blue-sky render
            new PhaseGrade
            {
                SunColor = new Color(1f, 0.97f, 0.88f), SunIntensity = 1.1f, SunElevationDeg = 30f, SunYawDeg = 105f,
                Ambient = new Color(0.55f, 0.53f, 0.5f), Fog = new Color(0.55f, 0.5f, 0.42f), FogDensity = fogDensityDay,
                NeonBoost = 0.35f, LampBoost = 0.05f,
                PostExposure = 0.15f, Saturation = -18f, ColorFilter = new Color(1.05f, 0.95f, 0.78f), Contrast = 8f,
                VignetteIntensity = 0.15f, BloomIntensity = 0.4f, FilmGrainIntensity = 0.22f,
            },
            // Dusk
            new PhaseGrade
            {
                SunColor = new Color(0.95f, 0.5f, 0.35f), SunIntensity = 0.5f, SunElevationDeg = 3f, SunYawDeg = 240f,
                Ambient = new Color(0.32f, 0.22f, 0.3f), Fog = new Color(0.35f, 0.22f, 0.28f), FogDensity = fogDensityDusk,
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
            //
            // 2026-07 follow-up (cinematic-night brief: "shadows are deep
            // but readable... avoid flattening the image"): Contrast
            // raised 18 -> 24 and PostExposure darkened -0.35 -> -0.4 as a
            // deliberate counterweight to nightFillLift's higher value
            // above -- a shadow lift alone reduces apparent contrast (it
            // literally raises the black point), so without this the
            // extra readability would come at the cost of the "deep
            // shadows" half of the brief. **ROLLED BACK the same session,
            // once a real screenshot showed the combined effect (this +
            // the previous nightAmbient/nightFillLift/tint round) was
            // near-TOTAL BLACK, not moody -- roads/terrain/buildings
            // invisible.** A steeper contrast curve stacked with a darker
            // exposure baseline was almost certainly crushing the shadow
            // lift's gains right back down. Creator confirmed the goal
            // plainly: "visibility/playability" -- this is the prime
            // suspect for the regression, so it's reverted first, past
            // even its ORIGINAL 18/-0.35 (down to 10/-0.15), leaving
            // nightAmbient (0.55) and nightFillLift (0.75) above to do
            // the readability work without a same-frame contrast fight.
            // VignetteIntensity also eased 0.42 -> 0.3 in the same pass --
            // a strong vignette darkens the frame edges on top of
            // everything else, working against the same visibility goal.
            new PhaseGrade
            {
                SunColor = new Color(0.35f, 0.4f, 0.65f), SunIntensity = 0.05f, SunElevationDeg = -8f, SunYawDeg = 285f,
                Ambient = new Color(0.02f, 0.02f, 0.05f), Fog = new Color(0.18f, 0.15f, 0.28f), FogDensity = fogDensityNight,
                NeonBoost = 2.2f, LampBoost = 1f,
                PostExposure = -0.15f, Saturation = 22f, ColorFilter = new Color(0.85f, 0.85f, 1.05f), Contrast = 10f,
                VignetteIntensity = 0.3f, BloomIntensity = 1.3f, FilmGrainIntensity = 0.3f,
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
