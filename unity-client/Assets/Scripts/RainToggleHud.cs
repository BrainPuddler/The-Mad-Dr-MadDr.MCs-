using UnityEngine;

/// <summary>docs/40 §3 item 1: a single always-on IMGUI button flipping
/// <see cref="WeatherController.IsRaining"/> -- same OnGUI/UiScale
/// convention and same "is this environmental effect on" idea
/// `WindowLightsHud` already establishes, so the two sit stacked
/// together as a pair rather than one being reachable and the other
/// not.
///
/// Chains directly off <see cref="WindowLightsHud.Bottom"/> (same
/// "don't hardcode an offset that might overlap" idiom that field
/// itself uses against `HudStatus.ContentBottom`) and publishes its own
/// <see cref="Bottom"/> in turn -- `BuildMenuHud` now reads THIS
/// instead of `WindowLightsHud.Bottom` directly, since this panel
/// always sits between the two once both exist.
///
/// Not gated behind a match/bridge, same reasoning as `WindowLightsHud`
/// -- weather is a whole-city visual state `RuntimeCityBuilder` sets up
/// unconditionally, so the toggle should always be reachable.</summary>
public class RainToggleHud : MonoBehaviour
{
    /// <summary>Published for the same reason <see
    /// cref="WindowLightsHud.Bottom"/> is -- <see cref="BuildMenuHud"/>
    /// stacks below this instead of jumping straight from
    /// `WindowLightsHud`.</summary>
    public static float Bottom { get; private set; }

    private void OnGUI()
    {
        var prevMatrix = UiScale.Begin();

        var y = WindowLightsHud.Bottom + 4f;
        var on = WeatherController.IsRaining;
        var label = (on ? "🌧 Rain: ON" : "🌧 Rain: OFF");
        var rect = new Rect(12f, y, 200f, 24f);
        if (GUI.Button(rect, label)) WeatherController.IsRaining = !on;
        Bottom = rect.yMax;

        UiScale.End(prevMatrix);
    }
}
