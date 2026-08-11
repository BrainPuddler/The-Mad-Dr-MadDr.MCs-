using UnityEngine;

/// <summary>2026-08 (creator direction: "add a toggle window lights on
/// off for a naturalistic windows light up at night based on a human
/// schedule and movement within a building"). A single always-on IMGUI
/// button flipping <see cref="EmissiveAnimator.WindowScheduleEnabled"/>
/// -- same OnGUI/UiScale convention every other HUD element in this
/// project uses (see HudStatus's own header for why that's fine
/// alongside the New Input System).
///
/// Positioned right below HudStatus's own dynamic content
/// (<see cref="HudStatus.ContentBottom"/>, published for exactly this
/// "don't hardcode an offset that might overlap" reason -- see that
/// field's own doc comment) rather than claiming a new screen corner:
/// all four are already spoken for (HudStatus top-left, AnalogClockHud
/// top-right, Minimap bottom-left, LumenHud bottom-right).
///
/// Not gated behind a match/bridge the way LumenHud is -- the
/// naturalistic window schedule runs on any city (RuntimeCityBuilder
/// adds this alongside HudStatus, unconditionally), so the toggle
/// should always be reachable.</summary>
public class WindowLightsHud : MonoBehaviour
{
    private void OnGUI()
    {
        var prevMatrix = UiScale.Begin();

        var y = HudStatus.ContentBottom + 8f;
        var on = EmissiveAnimator.WindowScheduleEnabled;
        var label = (on ? "🏠 Window lights: ON" : "🏠 Window lights: OFF");
        var rect = new Rect(12f, y, 200f, 24f);
        if (GUI.Button(rect, label)) EmissiveAnimator.WindowScheduleEnabled = !on;

        UiScale.End(prevMatrix);
    }
}
