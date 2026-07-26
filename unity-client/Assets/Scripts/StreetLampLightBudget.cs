using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/23 Phase 10.2: every streetlamp bulb RoadDresser spawns (both the
/// per-street prop and the roundabout ring) registers its transform here
/// -- a loose registry, same idiom as NeonRegistry, so RoadDresser (a
/// static generator with no MonoBehaviour of its own) never needs to know
/// this budget system exists.
/// </summary>
public static class StreetLampRegistry
{
    private static readonly List<Transform> Bulbs = new List<Transform>();
    public static void Register(Transform bulb) { Bulbs.Add(bulb); }
    public static IReadOnlyList<Transform> All { get { return Bulbs; } }
}

/// <summary>
/// docs/23 Phase 10.2: "street lamps as actual pixel lights on a budget
/// (nearest-N to camera)." A 1950s-dense city can register hundreds of
/// lamp bulbs (RoadDresser furnishes one on most quiet-street hexes plus
/// a ring per roundabout); a live Point Light per bulb would tank
/// performance, so only the <see cref="Budget"/> nearest-to-camera bulbs
/// get a real light -- everything else stays lit by its existing emissive
/// bulb material only (NeonRegistry), same as before this phase. A small
/// fixed pool of Light components is repositioned/toggled each refresh
/// rather than created and destroyed, and refreshes on a timer rather
/// than every frame -- this is deliberately a coarse, cheap budget system,
/// not per-frame precision.
/// </summary>
public class StreetLampLightBudget : MonoBehaviour
{
    public int Budget = 24;
    private const float RefreshInterval = 0.35f;
    private static readonly Color LampColor = new Color(1f, 0.85f, 0.55f);   // warm sodium bulb

    private readonly List<Light> _pool = new List<Light>();
    private readonly List<Transform> _picked = new List<Transform>();
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

        _picked.Clear();
        _pickedSq.Clear();
        var bulbs = StreetLampRegistry.All;
        for (var i = 0; i < bulbs.Count; i++)
        {
            var bulb = bulbs[i];
            if (bulb == null) continue;   // a knocked-over/destroyed prop simply drops out
            var d = (bulb.position - camPos).sqrMagnitude;
            if (_picked.Count < Budget)
            {
                _picked.Add(bulb);
                _pickedSq.Add(d);
            }
            else
            {
                var worst = 0;
                for (var j = 1; j < _pickedSq.Count; j++) if (_pickedSq[j] > _pickedSq[worst]) worst = j;
                if (d < _pickedSq[worst]) { _picked[worst] = bulb; _pickedSq[worst] = d; }
            }
        }

        while (_pool.Count < _picked.Count)
        {
            var go = new GameObject("StreetLampLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;   // budget fill lights -- never shadow casters
            light.range = 9f;
            light.color = LampColor;
            _pool.Add(light);
        }

        var boost = DayNightState.NightAmount;
        var intensity = Mathf.Lerp(0.05f, 3.2f, boost);
        for (var i = 0; i < _pool.Count; i++)
        {
            if (i < _picked.Count)
            {
                _pool[i].gameObject.SetActive(true);
                _pool[i].transform.position = _picked[i].position;
                _pool[i].intensity = intensity;
            }
            else
            {
                _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
