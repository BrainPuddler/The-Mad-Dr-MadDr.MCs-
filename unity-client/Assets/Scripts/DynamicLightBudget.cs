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
    }

    private static readonly List<Point> Points = new List<Point>();

    /// <summary>Register a glow point. `color` is what a REAL light
    /// promoted at this point should be tinted -- independent of
    /// whatever emissive material color the prop itself uses (a window
    /// might glow warm-white while its material is a cooler blue-glass
    /// tint, for instance).</summary>
    public static void Register(Transform point, Color color)
    {
        Points.Add(new Point { Transform = point, Color = color });
    }

    public static int Count { get { return Points.Count; } }
    public static Transform TransformAt(int i) { return Points[i].Transform; }
    public static Color ColorAt(int i) { return Points[i].Color; }
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

    // 2026-07: the diagnostic worked -- creator confirmed real lights
    // ARE visible at the deliberately-blown-out 80 default, which rules
    // out "not rendering at all" and narrows this to plain intensity
    // tuning. The old 0.7 default (this component's original number)
    // apparently really was just too dim to read on the creator's setup;
    // no config bug found to explain the gap (checked: no Physical Light
    // Units flag on the Pipeline Asset, which would've explained an
    // intensity/lumens scale mismatch -- it's just off).
    //
    // Backed off from the 80 diagnostic extreme to a middle-ground
    // starting point, NOT a confirmed-good final value -- the true
    // "looks right" threshold between the old invisible 0.7 and the
    // blown-out 80 hasn't been narrowed down by testing intermediate
    // values yet. This field is read live every ~0.35s refresh
    // specifically so it can be nudged in Play mode without a code
    // round-trip -- that's the fastest way to find the real number now
    // that visibility itself is confirmed working.
    [Tooltip("Intensity a promoted light reaches at full night. Turn this DOWN if lit areas look blown out. Note: this does NOT control the glowing bulb spheres themselves -- those are emissive geometry, see LumenCycleController's emissive/bloom fields.")]
    [Range(0f, 150f)]
    public float peakIntensity = 12f;

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

    private const float RefreshInterval = 0.35f;

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
        peakIntensity = profile.RealLightPeakIntensity;
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
        var boost = DayNightState.NightAmount;
        var intensity = Mathf.Lerp(0.02f, peakIntensity, boost);
        for (var i = 0; i < _pool.Count; i++)
        {
            if (i < _picked.Count)
            {
                var idx = _picked[i];
                _pool[i].gameObject.SetActive(true);
                _pool[i].transform.position = GlowPointRegistry.TransformAt(idx).position;
                _pool[i].color = GlowPointRegistry.ColorAt(idx);
                _pool[i].intensity = intensity;
                _pool[i].range = range;
            }
            else
            {
                _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
