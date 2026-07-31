using UnityEngine;

/// <summary>
/// docs/28 (city lighting system): every tunable number for every light
/// in the city -- streetlamps, windows, neon, marquee chasers -- gathered
/// in ONE Inspector-editable asset instead of hardcoded constants
/// scattered across LumenCycleController/RoadDresser/BuildingDresser/
/// DynamicLightBudget. This is the direct answer to "give me an
/// inspector setting... or a scriptable object that I can change" --
/// create one via Assets > Create > MadDr > City Lighting Profile, drag
/// it onto RuntimeCityBuilder's `Lighting Profile` field, and retune
/// without touching code.
///
/// Nothing REQUIRES an assigned asset. This profile holds AUTHORED
/// DEFAULTS: when one is assigned, <see cref="RuntimeCityBuilder"/>
/// seeds <see cref="DynamicLightBudget"/>'s and
/// <see cref="LumenCycleController"/>'s own Inspector fields from it at
/// city-build time. When it's NOT assigned, those components simply keep
/// whatever values are already on them -- nothing is overwritten.
///
/// **Where to tune at runtime (2026-07):** the live knobs are the FIELDS
/// ON THOSE TWO COMPONENTS, not this asset -- they're read every frame
/// (or every ~0.35s refresh), so dragging them in Play mode changes the
/// picture immediately. Editing this asset mid-Play does nothing until
/// the city is rebuilt, by design. If lights look wrong:
///   * too BRIGHT a glowing ball  -> LumenCycleController.emissiveScale
///   * too LARGE a glowing ball   -> LumenCycleController.bloomScale
///   * night too crushed-black    -> LumenCycleController.nightFillLift (prefer this FIRST -- surgical, doesn't wash out lamp-pool contrast)
///   * still unreadable after that -> LumenCycleController.nightAmbient (flat/global, use sparingly)
///   * night ambient reads wrong color -> LumenCycleController.nightAmbientTint (default cool blue-teal, day-for-night)
///   * lit ground area too strong -> DynamicLightBudget.pointIntensityMax/spotIntensityMax
///   * lit ground area too wide   -> DynamicLightBudget.range
///   * lights don't dim in fog    -> DynamicLightBudget.fogDimReferenceDensity
///   * fog too thin/thick overall -> LumenCycleController.fogDensityScale (live)
///   * fog wrong at one time of day -> LumenCycleController.fogDensityDawn/Day/Dusk/Night (rebuild city)
///   * night lacks light-pollution glow -> LumenCycleController.nightAmbient/nightAmbientTint
/// </summary>
[CreateAssetMenu(fileName = "CityLightingProfile", menuName = "MadDr/City Lighting Profile")]
public class CityLightingProfile : ScriptableObject
{
    [Header("Real dynamic lights (budgeted, nearest-to-camera only)")]
    [Tooltip("Total real Light components shared across EVERY light kind combined (streetlamps, windows, neon, marquee) -- not per-kind. This is the whole city's real-light budget. Raise for a beefier machine, lower for a slower one.")]
    [Range(4, 128)]
    public int RealLightBudget = 24;

    // 2026-07: kept in sync with DynamicLightBudget's own fields of the
    // same names minus the "RealLight" prefix -- see that component for
    // the full history (the "make it 40-120" diagnostic that confirmed
    // real lights DO render, then the Max/Min-per-type + fog-dimming
    // redesign that replaced the old single RealLightPeakIntensity).
    [Tooltip("Point-type real-light intensity ceiling (clear conditions) at full night -- the ornate lamppost, roundabout bulb, windows: everything except the overhanging streetlight.")]
    [Range(0f, 150f)]
    public float RealLightPointIntensityMax = 22.8f;

    [Tooltip("Point-type real-light intensity floor in HEAVY fog at full night.")]
    [Range(0f, 150f)]
    public float RealLightPointIntensityMin = 7.6f;

    [Tooltip("Spot-type real-light intensity ceiling (clear conditions) at full night -- currently only the overhanging streetlight.")]
    [Range(0f, 300f)]
    public float RealLightSpotIntensityMax = 114f;

    [Tooltip("Spot-type real-light intensity floor in HEAVY fog at full night.")]
    [Range(0f, 300f)]
    public float RealLightSpotIntensityMin = 34.2f;

    [Tooltip("Fog density at which lights reach their fog-dimmed minimum -- see DynamicLightBudget.fogDimReferenceDensity.")]
    [Range(0.001f, 0.05f)]
    public float FogDimReferenceDensity = 0.016f;

    [Tooltip("How far each light reaches, in meters, as a straight-line radius from the light itself -- NOT a ground-projected pool size. Needs to comfortably exceed the tallest fixture's mount height (~5.9m for the ornate lamppost globes) or it can't reach the ground at all.")]
    [Range(1f, 25f)]
    public float RealLightRange = 8f;

    [Header("Emissive material brightness (the glow on the prop itself)")]
    [Tooltip("Base emissive multiplier for a lit bulb/window/sign BEFORE the night boost below. This is what clipped to solid white balls -- keep BulbEmissiveBase * MaxNightBoost comfortably under ~1.2 to avoid the prop itself rendering as a flat white blob.")]
    [Range(0f, 2f)]
    public float BulbEmissiveBase = 0.25f;

    [Tooltip("The ceiling NeonRegistry's boost reaches at full night. Multiplies BulbEmissiveBase (and every other registered neon material's own emissive).")]
    [Range(0f, 3f)]
    public float MaxNightBoost = 1.0f;

    [Tooltip("The boost at full Day -- kept low so neon/bulbs are barely visible against daylight, per the target look.")]
    [Range(0f, 1f)]
    public float DayNeonBoost = 0.35f;

    [Header("Post-processing (Night mood)")]
    [Tooltip("Multiplies URP Bloom intensity at EVERY time of day (not just night) -- a true scale on the authored curve, not an absolute target. High values are what turn a city full of lit windows/signs into a wall of white bloom.")]
    [Range(0f, 2f)]
    public float BloomScale = 0.3f;

    [Tooltip("How much extra Bloom the CURRENT fog density adds on top of BloomScale -- 'real lights in the fog' get a softer, more diffuse halo instead of just dimming. See LumenCycleController.fogGlowBoost.")]
    [Range(0f, 40f)]
    public float FogGlowBoost = 15f;

    [Tooltip("Base Bloom scatter (spread/softness of the blur) at every time of day. Higher = a wider, hazier glow -- the actual 'diffuse' knob, distinct from intensity.")]
    [Range(0f, 1f)]
    public float BloomScatter = 0.7f;

    [Tooltip("How much CURRENT fog density adds to BloomScatter (clamped to 1) -- fog specifically makes the glow more diffuse, not just brighter.")]
    [Range(0f, 50f)]
    public float FogDiffusionBoost = 8f;

    [Tooltip("HDR brightness cutoff above which a pixel blooms at all. Lower = lights 'pop' more readily.")]
    [Range(0f, 2f)]
    public float BloomThreshold = 0.6f;

    [Tooltip("Ambient light brightness at full Night -- 0.02 (near-black), then 0.08, then 0.24, then 0.45, all reported too dark or too flat/globally-brightened; 0.35 is the current default. Prefer raising NightFillLift below first -- it's the tool that doesn't also wash out the lamp 'pools of light' look. Hard-floored at 0.12 by LumenCycleController.MinNightAmbient regardless of what's set here.")]
    [Range(0.12f, 1f)]
    public float NightAmbientBrightness = 0.35f;

    [Tooltip("Color tint multiplied onto NightAmbientBrightness -- default is a cool blue-teal (day-for-night skyglow/moonlight), deliberately cool so the warm practical lights (streetlamps/windows/neon) read as contrast against it rather than washing the whole frame one color. See LumenCycleController.nightAmbientTint.")]
    public Color NightAmbientTint = new Color(0.55f, 0.72f, 1f);

    [Tooltip("HDR-style shadow lift at full Night -- raises the floor of near-black areas without touching bright ones (unlike NightAmbientBrightness, which is a flat uniform light). THIS is the primary night-readability knob. See LumenCycleController.nightFillLift.")]
    [Range(0f, 1f)]
    public float NightFillLift = 0.6f;

    [Header("Fog density per phase")]
    [Tooltip("Fog density at Dawn. See LumenCycleController.fogDensityDawn.")]
    [Range(0f, 0.1f)]
    public float FogDensityDawn = 0.010f;

    [Tooltip("Fog density at Day. See LumenCycleController.fogDensityDay.")]
    [Range(0f, 0.1f)]
    public float FogDensityDay = 0.005f;

    [Tooltip("Fog density at Dusk. See LumenCycleController.fogDensityDusk.")]
    [Range(0f, 0.1f)]
    public float FogDensityDusk = 0.014f;

    [Tooltip("Fog density at full Night. See LumenCycleController.fogDensityNight.")]
    [Range(0f, 0.1f)]
    public float FogDensityNight = 0.022f;

    [Tooltip("Multiplies the blended fog density curve at EVERY time of day, live. See LumenCycleController.fogDensityScale.")]
    [Range(0f, 3f)]
    public float FogDensityScale = 1f;

    [Header("Flicker (windows, occasional neon dropout)")]
    [Tooltip("Per-instance flicker cycle speed range (Hz-ish) -- each registered light picks its own speed in this range so hundreds of windows don't flicker in lockstep.")]
    public Vector2 FlickerSpeedRange = new Vector2(0.15f, 0.6f);

    [Tooltip("How dim a flicker's low point gets, as a fraction of full brightness. 0 = can go fully dark, 1 = never dims at all.")]
    [Range(0f, 1f)]
    public float FlickerFloor = 0.15f;

    [Header("Buzz (failing neon tube flutter)")]
    [Tooltip("Flutter frequency in Hz -- high and small-amplitude, simulating a neon tube's mains hum rather than a slow pulse.")]
    [Range(1f, 40f)]
    public float BuzzFrequencyHz = 14f;

    [Range(0f, 1f)]
    public float BuzzAmplitude = 0.25f;

    [Tooltip("How often (seconds, average) a buzzing sign has a brief full dropout -- the classic 'dying neon tube' stutter.")]
    [Range(0.5f, 30f)]
    public float BuzzDropoutIntervalSeconds = 6f;

    [Header("Chase (marquee 'clique' sequencer lights)")]
    [Tooltip("Seconds each step of the chase sequence holds before advancing to the next bulb.")]
    [Range(0.02f, 1f)]
    public float ChaseStepSeconds = 0.12f;

    [Range(0f, 1f)]
    public float ChaseOffFloor = 0.12f;

    private static CityLightingProfile _default;

    /// <summary>Safe, in-code fallback so any scene/reader that hasn't
    /// assigned a real asset still behaves sanely -- never null-refs.</summary>
    public static CityLightingProfile Default
    {
        get
        {
            if (_default == null) _default = CreateInstance<CityLightingProfile>();
            return _default;
        }
    }

    private static CityLightingProfile _active;

    /// <summary>The profile RuntimeCityBuilder is currently using, set
    /// once at city-build time -- the loose static-holder idiom this
    /// project already uses (NeonRegistry, StreetLampRegistry) so static
    /// generator classes (BuildingDresser/RoadDresser mint their cached
    /// materials once, not per-frame) can read tunable values without
    /// threading a profile reference through every dresser method
    /// signature.
    ///
    /// Deliberately NOT `= Default` as a field initializer -- Unity
    /// forbids calling ScriptableObject.CreateInstance from a
    /// ScriptableObject's own static field initializer/constructor
    /// ("CreateScriptableObjectInstanceFromType is not allowed to be
    /// called from a ScriptableObject constructor... call it in OnEnable
    /// instead"), since the type's static constructor can run during
    /// Unity's own serialization/domain-reload window, before it's safe
    /// to construct instances. The getter below only ever calls
    /// `Default` lazily, from ordinary runtime code (Start()/Update()),
    /// which is exactly what Unity expects.</summary>
    public static CityLightingProfile Active
    {
        get { return _active != null ? _active : Default; }
        set { _active = value; }
    }
}
