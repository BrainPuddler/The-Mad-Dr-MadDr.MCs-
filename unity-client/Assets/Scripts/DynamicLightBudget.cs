using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/28 (city lighting system): every "this prop glows and could
/// justify a real light" point in the whole city -- streetlamp bulbs,
/// lit windows, neon signs, marquee bulbs -- registers here with its own
/// color, regardless of which dresser or prop kind spawned it. ONE
/// shared registry so <see cref="DynamicLightBudget"/> can spend its
/// single city-wide light budget on whichever points are actually
/// nearest the camera RIGHT NOW, across every kind at once, instead of
/// each kind getting its own separate (and separately wasteful) budget.
/// Loose static registry, same idiom as NeonRegistry -- static generator
/// classes (BuildingDresser/RoadDresser) never need to know this budget
/// system exists.
/// </summary>
public static class GlowPointRegistry
{
    private struct Point
    {
        public Transform Transform;
        public Color Color;
        public LightType LightType;
        public bool SpotAimsWithTransform;
        public System.Func<bool> IsEligible;
    }

    private static readonly List<Point> Points = new List<Point>();

    /// <summary>Register a glow point. `color` is what a REAL light
    /// promoted at this point should be tinted -- independent of
    /// whatever emissive material color the prop itself uses (a window
    /// might glow warm-white while its material is a cooler blue-glass
    /// tint, for instance). `lightType` defaults to Point (an
    /// omnidirectional pool of light, the right choice for most fixtures
    /// -- a bare bulb, a window, a sign). Pass `LightType.Spot` for a
    /// fixture that should read as directional (2026-07: the overhanging
    /// streetlight's bulb, aimed straight down at the road) --
    /// DynamicLightBudget aims a Spot-type promoted light straight down
    /// by default and applies its own shared cone angle.
    ///
    /// `spotAimsWithTransform` (2026-07, traffic headlights): a second
    /// Spot use case that does NOT want the shared straight-down aim --
    /// a moving car's beam needs to track wherever the car is currently
    /// FACING, not the road below it. When true, the promoted Light's
    /// rotation is copied from `point`'s own live world rotation every
    /// refresh instead of the shared down-aim constant -- give `point` a
    /// child transform whose LOCAL rotation already encodes the fixed
    /// "slightly down" tilt, so its WORLD rotation combines that tilt
    /// with wherever its parent (the car) is currently facing.
    ///
    /// `isEligible` (2026-07, traffic headlights): an optional per-point
    /// predicate checked every refresh BEFORE this point is even
    /// considered a budget candidate -- default null means "always
    /// eligible," the exact behavior every prior call site (streetlamps,
    /// windows, neon, marquee -- registered once for the city's whole
    /// lifetime) already has. A car's headlight is only relevant while
    /// driving at night; rather than adding true register/unregister
    /// lifecycle to this otherwise append-only registry, an ineligible
    /// point (parked, or daylight) simply never competes for or holds a
    /// budget slot, so a real Light never ends up shining from a car
    /// that (from the player's read of the scene) has its headlights
    /// off.</summary>
    public static void Register(Transform point, Color color, LightType lightType = LightType.Point,
        bool spotAimsWithTransform = false, System.Func<bool> isEligible = null)
    {
        Points.Add(new Point
        {
            Transform = point,
            Color = color,
            LightType = lightType,
            SpotAimsWithTransform = spotAimsWithTransform,
            IsEligible = isEligible,
        });
    }

    public static int Count { get { return Points.Count; } }
    public static Transform TransformAt(int i) { return Points[i].Transform; }
    public static Color ColorAt(int i) { return Points[i].Color; }
    public static LightType LightTypeAt(int i) { return Points[i].LightType; }
    public static bool SpotAimsWithTransformAt(int i) { return Points[i].SpotAimsWithTransform; }
    public static bool IsEligibleAt(int i) { var f = Points[i].IsEligible; return f == null || f(); }
}

/// <summary>
/// docs/28 (city lighting system): "actual pixel lights on a budget
/// (nearest-N to camera)," generalized from streetlamps to every
/// registered <see cref="GlowPointRegistry"/> point. A 1950s-dense city
/// can register many hundreds of glow points (a bulb per streetlamp, a
/// window per lit floor, a neon sign, a marquee); a live Light per point
/// would tank performance, so only <see cref="CityLightingProfile.
/// RealLightBudget"/> nearest-to-camera points -- ACROSS EVERY KIND
/// COMBINED, not per-kind -- get a real Light. Everything else stays lit
/// by its own emissive material only (NeonRegistry/EmissiveAnimator),
/// same as before this ever existed. A small fixed pool of Light
/// components is repositioned/recolored/toggled each refresh rather than
/// created and destroyed, and refreshes on a timer rather than every
/// frame -- deliberately a coarse, cheap budget system, not per-frame
/// precision. Re-reads CityLightingProfile.Active every refresh, so
/// Budget/PeakIntensity/Range tune LIVE in Play mode.
/// </summary>
public class DynamicLightBudget : MonoBehaviour
{
    // 2026-07 creator report: "nothing changes when I alter the
    // DynamicLight." Two reasons, both fixed here. (1) These numbers used
    // to live ONLY on CityLightingProfile -- and with no profile asset
    // assigned, `CityLightingProfile.Default` is a runtime-created object
    // that appears nowhere in the Inspector, so there was literally
    // nothing to edit. They are now plain fields on this component:
    // visible, and editable LIVE in Play mode. (2) The pooled
    // "DynamicLight" GameObjects this spawns are overwritten every
    // refresh (~3x/second), so hand-editing THOSE in the hierarchy never
    // sticks -- edit these fields instead, they're the actual source.
    [Header("Real dynamic lights -- editable live in Play mode")]
    [Tooltip("Total real Light components across the WHOLE city (streetlamps + windows + neon combined), spent on whichever glow points are nearest the camera.")]
    [Range(0, 128)]
    public int budget = 24;

    // 2026-07, two rounds. Round 1 diagnostic: creator confirmed real
    // lights ARE visible at a deliberately-blown-out 80 (ruled out "not
    // rendering at all"), backed off to an untuned middle-ground 12.
    //
    // Round 2 direction: "give me a setting in the editor to set the max
    // and minimum ON setting for the overhang streetlights, and all
    // others streetlights" -- plus fog should actively throttle real
    // lights (see fogDimReferenceDensity below). The old single
    // `peakIntensity` (shared by every Point-type light) and
    // `spotIntensityMultiplier` (a flat extra factor for Spot) are
    // replaced by an explicit Max/Min pair PER TYPE: Max is the ceiling
    // in clear conditions, Min is the floor in heavy fog -- fog dims a
    // light's core, it doesn't extinguish it, so Min should stay above
    // 0. "All others streetlights" maps directly onto the existing
    // Point/Spot split (GlowPointRegistry's LightType) -- the ornate
    // lamppost, roundabout bulb, and windows are all Point; the
    // overhanging streetlight is the only Spot. Untuned starting values,
    // same status as the fields they replace -- nudge live once visible.
    // 2026-07: creator confirmed the fog pass reads better ("it's
    // better"), then: "make all the street lights 90% brighter." Applied
    // as a flat x1.9 across BOTH types' Max and Min -- "all the street
    // lights" is both halves of the Point/Spot split this system already
    // uses for "overhang vs all others."
    [Tooltip("Point-type real-light intensity ceiling (clear conditions) at full night -- the ornate lamppost, roundabout bulb, windows: everything except the overhanging streetlight.")]
    [Range(0f, 150f)]
    public float pointIntensityMax = 22.8f;

    [Tooltip("Point-type real-light intensity floor in HEAVY fog at full night. Should stay above 0 -- fog dims a light, it doesn't extinguish it.")]
    [Range(0f, 150f)]
    public float pointIntensityMin = 7.6f;

    [Tooltip("Spot-type real-light intensity ceiling (clear conditions) at full night -- currently only the overhanging streetlight. Higher baseline than the Point max: a Spot's cone concentrates the same intensity into a narrower solid angle and reads dimmer per-pixel at an equal value for a wide-ish cone like 48 degrees.")]
    [Range(0f, 300f)]
    public float spotIntensityMax = 114f;

    [Tooltip("Spot-type real-light intensity floor in HEAVY fog at full night.")]
    [Range(0f, 300f)]
    public float spotIntensityMin = 34.2f;

    // 2026-07 creator: "it should add a glow to the light diffusing it,
    // like real lights in the fog." Basic RenderSettings fog only fades
    // a SURFACE's rendered color toward the fog color by camera
    // distance -- it has no concept of a light source's own reach, so it
    // can't make a lamp look "swallowed" by fog on its own. This
    // normalizes the CURRENT RenderSettings.fogDensity (already driven
    // by LumenCycleController's day/night grades) into a 0..1 "how foggy
    // right now" factor used to blend each light's own Max->Min above --
    // the crude, non-volumetric approximation of light scattering this
    // session's toolset can actually deliver. See LumenCycleController.
    // fogGlowBoost for the companion "diffuse halo" half of the effect
    // (bloom, not light intensity).
    [Tooltip("Fog density at which lights reach their fog-dimmed MINIMUM -- current RenderSettings.fogDensity is divided by this and clamped 0..1 to blend each light between its Max and Min. Lower = lights dim out in lighter fog.")]
    [Range(0.001f, 0.05f)]
    public float fogDimReferenceDensity = 0.016f;

    // 2026-07 correction: an earlier pass here cut this to 3f on the
    // theory that 7f was washing over neighboring props. Wrong call --
    // the ornate lamppost's globes sit 5.9m up (RoadDresser.cs, the
    // "case 6" globeSpot), and a Point light's range is a straight-line
    // radius from its own position, not a ground-projected radius. 3m of
    // range from a light mounted 5.9m up cannot reach the ground AT ALL
    // (screenshot confirmed: no pool on the ground, and the pole itself
    // went pure black -- ambient is near-zero at night and nothing else
    // was lighting it either). Restored well above the tallest street
    // fixture's mount height so it can actually reach the pavement, with
    // a real pool radius left over (sqrt(8^2 - 5.9^2) =~ 5.4m ground
    // diameter for the globes specifically). If the on-screen glow still
    // looks oversized, that's bloom (LumenCycleController.bloomScale),
    // not this -- this field is about ground reach, not screen-space
    // halo size.
    [Tooltip("How far each light reaches, in meters, as a straight-line radius from the light itself -- NOT a ground-projected pool size. Needs to comfortably exceed the tallest fixture's mount height (~5.9m for the ornate lamppost globes) or it can't reach the ground at all.")]
    [Range(1f, 25f)]
    public float range = 8f;

    [Tooltip("Turn every real light off entirely -- the fastest way to check whether the lights or the emissive bulb geometry is what you're actually seeing.")]
    public bool enableRealLights = true;

    // 2026-07 creator direction: "make the overhanging street lights
    // spotlights, pointing down at the road. Make cone angle 48 degrees
    // wide." GlowPointRegistry now carries a LightType per point
    // (RoadDresser's overhanging-streetlight bulb registers as Spot;
    // everything else stays the previous Point default). This is the
    // ONE shared cone angle every promoted Spot light uses -- there's no
    // per-fixture angle yet since only one fixture kind asks for Spot
    // today; if a second one wants a different cone, this needs to move
    // onto GlowPointRegistry's per-point data instead of staying a
    // single shared field here.
    [Tooltip("Cone angle (degrees) for any promoted light registered as a Spot (currently: the overhanging streetlight, aimed straight down at the road). Ignored for Point lights.")]
    [Range(1f, 179f)]
    public float spotConeAngle = 48f;

    private const float RefreshInterval = 0.35f;
    // Unity's forward-is-local-+Z convention: an X-euler of 90 points
    // straight down (same convention LumenCycleController's sun rotation
    // already relies on and documents).
    private static readonly Quaternion SpotDownRotation = Quaternion.Euler(90f, 0f, 0f);

    private readonly List<Light> _pool = new List<Light>();
    private readonly List<int> _picked = new List<int>();
    private readonly List<float> _pickedSq = new List<float>();
    private float _timer;

    /// <summary>Seed the live fields from an authored profile asset --
    /// called by RuntimeCityBuilder ONLY when one is actually assigned,
    /// so an unassigned profile leaves this component's own Inspector
    /// values (what the creator typed) alone instead of silently
    /// overwriting them with defaults.</summary>
    public void ApplyProfile(CityLightingProfile profile)
    {
        if (profile == null) return;
        budget = profile.RealLightBudget;
        pointIntensityMax = profile.RealLightPointIntensityMax;
        pointIntensityMin = profile.RealLightPointIntensityMin;
        spotIntensityMax = profile.RealLightSpotIntensityMax;
        spotIntensityMin = profile.RealLightSpotIntensityMin;
        fogDimReferenceDensity = profile.FogDimReferenceDensity;
        range = profile.RealLightRange;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = RefreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var camPos = cam.transform.position;
        var activeBudget = enableRealLights ? budget : 0;

        _picked.Clear();
        _pickedSq.Clear();
        var count = GlowPointRegistry.Count;
        for (var i = 0; i < count; i++)
        {
            var t = GlowPointRegistry.TransformAt(i);
            if (t == null) continue;   // a knocked-over/destroyed prop simply drops out
            if (!GlowPointRegistry.IsEligibleAt(i)) continue;   // e.g. a parked/daylight car's headlight
            var d = (t.position - camPos).sqrMagnitude;
            if (_picked.Count < activeBudget)
            {
                _picked.Add(i);
                _pickedSq.Add(d);
            }
            // 2026-07 creator-found crash: with activeBudget == 0 (either
            // enableRealLights off, or budget dragged to 0), the "is this
            // better than my worst pick" fallback below ran on the very
            // first glow point with _pickedSq still empty -- indexing
            // [0] into an empty list threw ArgumentOutOfRangeException,
            // every refresh, forever. Guard it: with nothing picked yet,
            // there's nothing to replace.
            else if (_pickedSq.Count > 0)
            {
                var worst = 0;
                for (var j = 1; j < _pickedSq.Count; j++) if (_pickedSq[j] > _pickedSq[worst]) worst = j;
                if (d < _pickedSq[worst]) { _picked[worst] = i; _pickedSq[worst] = d; }
            }
        }

        while (_pool.Count < _picked.Count)
        {
            // 2026-07 creator confusion: these pooled lights have no
            // tunable fields of their own -- position/color/intensity/
            // range are overwritten from THIS component's own fields
            // every refresh (~3x/sec), so hand-editing one directly in
            // the Hierarchy never sticks. Named defensively so that's
            // obvious instead of discovered the hard way.
            var go = new GameObject("(auto) pooled light -- edit DynamicLightBudget instead");
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;   // budget fill lights -- never shadow casters
            // A freshly AddComponent'd Light defaults to Mixed bake mode,
            // which is what the "Realtime indirect bounce shadowing is
            // only supported for Directional" console warning is about --
            // Mixed asks for baked/GI participation these pooled lights
            // (repositioned every refresh, never baked) can't meaningfully
            // provide. Explicit Realtime silences it and matches what
            // these actually are.
            light.lightmapBakeType = LightmapBakeType.Realtime;
            _pool.Add(light);
        }

        // Real "pools of light" come from a MODEST light plus a genuinely
        // dark night around it (LumenCycleController's Night ambient), not
        // from cranking the light -- a bright light against an
        // already-bright scene doesn't read as a pool, it just reads as
        // more brightness everywhere. Read from the live fields above
        // every refresh, so dragging them in Play mode takes effect
        // within one refresh interval (~0.35s).
        //
        // 2026-07 correction: the low end used to be a 0.02 floor, not a
        // hard 0 -- "ALL the lights should turn off during the day" means
        // exactly that, not "very dim." NightAmount itself now genuinely
        // reaches 0 for the whole Day (LumenCycleController's
        // ComputeNightIntensity), so Lerp(0f, ceiling, 0) is exactly 0,
        // not a residual glow.
        //
        // 2026-07 round 2: "give me a setting... for the max and minimum
        // ON setting" + fog should actively throttle real lights. Each
        // type's ceiling is no longer a flat peakIntensity -- it's
        // Lerp(Max, Min, fogT), where fogT is how close the CURRENT fog
        // density is to fogDimReferenceDensity. Heavier fog pulls the
        // ceiling down toward Min; the night boost above still separately
        // gates the whole thing to 0 during the day.
        var boost = DayNightState.NightAmount;
        var fogT = fogDimReferenceDensity > 0f
            ? Mathf.Clamp01(RenderSettings.fogDensity / fogDimReferenceDensity) : 0f;
        var pointCeiling = Mathf.Lerp(pointIntensityMax, pointIntensityMin, fogT);
        var spotCeiling = Mathf.Lerp(spotIntensityMax, spotIntensityMin, fogT);
        var pointIntensity = Mathf.Lerp(0f, pointCeiling, boost);
        var spotIntensity = Mathf.Lerp(0f, spotCeiling, boost);
        for (var i = 0; i < _pool.Count; i++)
        {
            if (i < _picked.Count)
            {
                var idx = _picked[i];
                var pooled = _pool[i];
                pooled.gameObject.SetActive(true);
                pooled.transform.position = GlowPointRegistry.TransformAt(idx).position;
                pooled.color = GlowPointRegistry.ColorAt(idx);
                pooled.range = range;
                // Which registered point lands on which pooled slot can
                // change refresh to refresh (nearest-to-camera reshuffles
                // as the camera moves), so type/rotation/cone/intensity
                // all have to be re-applied here too, not just set once
                // at creation.
                var kind = GlowPointRegistry.LightTypeAt(idx);
                pooled.type = kind;
                if (kind == LightType.Spot)
                {
                    // most Spot registrants (the overhanging streetlight)
                    // want the shared straight-down aim; a car's headlight
                    // instead tracks wherever it's currently facing --
                    // see GlowPointRegistry.Register's own doc comment.
                    pooled.transform.rotation = GlowPointRegistry.SpotAimsWithTransformAt(idx)
                        ? GlowPointRegistry.TransformAt(idx).rotation
                        : SpotDownRotation;
                    pooled.spotAngle = spotConeAngle;
                    pooled.intensity = spotIntensity;
                }
                else
                {
                    pooled.intensity = pointIntensity;
                }
            }
            else
            {
                _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
