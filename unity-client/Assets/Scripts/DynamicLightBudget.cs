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
    private const float RefreshInterval = 0.35f;

    private readonly List<Light> _pool = new List<Light>();
    private readonly List<int> _picked = new List<int>();
    private readonly List<float> _pickedSq = new List<float>();
    private float _timer;

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
        var profile = CityLightingProfile.Active;
        var budget = profile.RealLightBudget;

        _picked.Clear();
        _pickedSq.Clear();
        var count = GlowPointRegistry.Count;
        for (var i = 0; i < count; i++)
        {
            var t = GlowPointRegistry.TransformAt(i);
            if (t == null) continue;   // a knocked-over/destroyed prop simply drops out
            var d = (t.position - camPos).sqrMagnitude;
            if (_picked.Count < budget)
            {
                _picked.Add(i);
                _pickedSq.Add(d);
            }
            else
            {
                var worst = 0;
                for (var j = 1; j < _pickedSq.Count; j++) if (_pickedSq[j] > _pickedSq[worst]) worst = j;
                if (d < _pickedSq[worst]) { _picked[worst] = i; _pickedSq[worst] = d; }
            }
        }

        while (_pool.Count < _picked.Count)
        {
            var go = new GameObject("DynamicLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;   // budget fill lights -- never shadow casters
            _pool.Add(light);
        }

        // 2026-07 creator correction: the original hardcoded range read as
        // "too bright and default" -- a harsh, blown-out light doing all
        // the work by itself. Real "pools of light" come from a MODEST
        // light plus a genuinely dark night around it (LumenCycleController's
        // Night ambient), not from cranking the light -- a bright light
        // against an already-bright scene doesn't read as a pool, it just
        // reads as more brightness everywhere. All three of Budget/Peak/
        // Range now live on CityLightingProfile so this never needs a
        // repeat blind numeric guess.
        var boost = DayNightState.NightAmount;
        var intensity = Mathf.Lerp(0.02f, profile.RealLightPeakIntensity, boost);
        var range = profile.RealLightRange;
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
