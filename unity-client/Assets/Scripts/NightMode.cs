using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Neon night mode (docs/21 batch 2, item 4): a dusk lighting preset the
/// registered signage/bulb materials pop against, toggled with N.
/// Entirely code-driven -- this environment has no Editor to hand-place
/// a scene Light, so it creates its own directional "sun" on a fresh
/// GameObject (deliberately NOT touching RuntimeCityBuilder's own
/// transform, which is the parent of the whole generated city -- rotating
/// THAT would rotate every building with it) and eases it, the ambient
/// color, fog, and every NeonRegistry material between day and dusk.
/// </summary>
public class NightMode : MonoBehaviour
{
    private Light _sun;
    private bool _night;
    private float _t; // 0 = day, 1 = night

    private static readonly Color DaySun = new Color(1f, 0.97f, 0.9f);
    private static readonly Color DuskSun = new Color(0.85f, 0.45f, 0.35f);
    private const float DaySunIntensity = 1.15f;
    private const float DuskSunIntensity = 0.32f;
    private static readonly Color DayAmbient = new Color(0.55f, 0.58f, 0.6f);
    private static readonly Color DuskAmbient = new Color(0.14f, 0.13f, 0.28f);
    private static readonly Color DuskFog = new Color(0.2f, 0.16f, 0.3f);
    private const float DayNeonBoost = 0.35f;   // barely visible against daylight
    private const float NightNeonBoost = 2.2f;  // the whole point of neon at night

    private void Start()
    {
        var sunGo = new GameObject("DuskSun");
        _sun = sunGo.AddComponent<Light>();
        _sun.type = LightType.Directional;
        _sun.color = DaySun;
        _sun.intensity = DaySunIntensity;
        _sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        RenderSettings.ambientLight = DayAmbient;
        RenderSettings.fog = false;
        NeonRegistry.SetBoost(DayNeonBoost);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame) _night = !_night;

        var target = _night ? 1f : 0f;
        _t = Mathf.MoveTowards(_t, target, Time.deltaTime * 0.6f);

        _sun.color = Color.Lerp(DaySun, DuskSun, _t);
        _sun.intensity = Mathf.Lerp(DaySunIntensity, DuskSunIntensity, _t);
        RenderSettings.ambientLight = Color.Lerp(DayAmbient, DuskAmbient, _t);
        RenderSettings.fog = _t > 0.05f;
        RenderSettings.fogColor = Color.Lerp(DayAmbient, DuskFog, _t);
        RenderSettings.fogDensity = Mathf.Lerp(0f, 0.012f, _t);
        NeonRegistry.SetBoost(Mathf.Lerp(DayNeonBoost, NightNeonBoost, _t));
    }
}
