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
/// Nothing REQUIRES an assigned asset -- every reader falls back to
/// <see cref="Default"/> (this class's own field defaults) so a scene
/// that never assigns one keeps working exactly as before.
///
/// Live-tuning notes: <see cref="DynamicLightBudget"/> re-reads this
/// profile every refresh cycle (a few times a second), so
/// RealLightBudget/Peak/Range changes take effect immediately in Play
/// mode. The Night ambient/bloom/neon-boost ceiling and the emissive
/// base brightness are baked into the generated city/grades once at
/// scene start (the same "cached shared material"/"computed once"
/// convention every dresser in this project already uses) -- changing
/// those needs a stop/tweak/replay, not a special case worth the extra
/// complexity of true live cross-fade re-blending.
/// </summary>
[CreateAssetMenu(fileName = "CityLightingProfile", menuName = "MadDr/City Lighting Profile")]
public class CityLightingProfile : ScriptableObject
{
    [Header("Real dynamic lights (budgeted, nearest-to-camera only)")]
    [Tooltip("Total real Light components shared across EVERY light kind combined (streetlamps, windows, neon, marquee) -- not per-kind. This is the whole city's real-light budget. Raise for a beefier machine, lower for a slower one.")]
    [Range(4, 128)]
    public int RealLightBudget = 24;

    [Tooltip("Peak intensity a promoted real light reaches at full night. THE fix for 'lights are too bright' if it recurs -- turn this down first.")]
    [Range(0f, 5f)]
    public float RealLightPeakIntensity = 0.9f;

    [Range(1f, 25f)]
    public float RealLightRange = 8f;

    [Header("Emissive material brightness (the glow on the prop itself)")]
    [Tooltip("Base emissive multiplier for a lit bulb/window/sign BEFORE the night boost below. This is what clipped to solid white balls -- keep BulbEmissiveBase * MaxNightBoost comfortably under ~1.2 to avoid the prop itself rendering as a flat white blob.")]
    [Range(0f, 2f)]
    public float BulbEmissiveBase = 0.45f;

    [Tooltip("The ceiling NeonRegistry's boost reaches at full night. Multiplies BulbEmissiveBase (and every other registered neon material's own emissive).")]
    [Range(0f, 3f)]
    public float MaxNightBoost = 1.5f;

    [Tooltip("The boost at full Day -- kept low so neon/bulbs are barely visible against daylight, per the target look.")]
    [Range(0f, 1f)]
    public float DayNeonBoost = 0.35f;

    [Header("Post-processing (Night mood)")]
    [Tooltip("URP Bloom intensity at full Night. High values are what turn a city full of lit windows/signs into a wall of white bloom.")]
    [Range(0f, 2f)]
    public float NightBloomIntensity = 0.5f;

    [Tooltip("Ambient light brightness at full Night -- near 0 for a genuinely dark night the lamps/windows/signs can stand out against.")]
    [Range(0f, 1f)]
    public float NightAmbientBrightness = 0.02f;

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

    /// <summary>The profile RuntimeCityBuilder is currently using, set
    /// once at city-build time -- the loose static-holder idiom this
    /// project already uses (NeonRegistry, StreetLampRegistry) so static
    /// generator classes (BuildingDresser/RoadDresser mint their cached
    /// materials once, not per-frame) can read tunable values without
    /// threading a profile reference through every dresser method
    /// signature.</summary>
    public static CityLightingProfile Active = Default;
}
