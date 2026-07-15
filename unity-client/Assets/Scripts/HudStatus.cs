using UnityEngine;

/// <summary>On-screen session status: the harvest wallet (docs/20 yields
/// from eaten citizens), the selected monster's order and physiology
/// speeds, and the control reference. IMGUI -- fine alongside the new
/// Input System, which only replaces the Input class, not OnGUI.</summary>
public class HudStatus : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private WaypointCommander _commander;

    public void Init(RuntimeCityBuilder builder, WaypointCommander commander)
    {
        _builder = builder;
        _commander = commander;
    }

    private void OnGUI()
    {
        if (_builder == null) return;

        var y = 8f;
        Line(ref y, "🩸 " + _builder.WalletBlood + "   🦴 " + _builder.WalletBones + "   🧠 " + _builder.WalletBrains
            + "   (eaten citizens: " + _builder.CitizensEaten + ")");

        var selected = _commander != null ? _commander.SelectedAgent : null;
        if (selected != null)
        {
            Line(ref y, "▶ " + selected.DisplayName + " — " + selected.OrderDescription);
            Line(ref y, "   " + selected.SpeedDescription);
        }
        else
        {
            Line(ref y, "Left-click a monster to select it.");
        }
        Line(ref y, "Right-click: ground = waypoint (Shift queues) · citizen = eat · building = attack");
        Line(ref y, "Camera: WASD pan · Q/E rotate · scroll zoom · middle-drag / screen-edge scroll");
        Line(ref y, "G: jump to the unit nearest the cursor");
    }

    private static void Line(ref float y, string text)
    {
        // draw a cheap shadow so it reads over any city color below
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(13f, y + 1f, 900f, 24f), text);
        GUI.color = Color.white;
        GUI.Label(new Rect(12f, y, 900f, 24f), text);
        y += 20f;
    }
}
