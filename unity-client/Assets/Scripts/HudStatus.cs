using MadDr.MatchCore;
using UnityEngine;

/// <summary>On-screen session status: the harvest wallet (docs/20 yields
/// from eaten citizens), the selected monster's order and physiology
/// speeds. IMGUI -- fine alongside the new Input System, which only
/// replaces the Input class, not OnGUI.
///
/// 2026-08 (docs/12 "eating citizens" fix): the wallet line now reads
/// the REAL match-core wallet (<see cref="SimBridge.PlayerWallet"/>)
/// instead of a disconnected client-side counter -- so this line always
/// agrees with <see cref="ResourceHud"/>'s own top-right panel, which
/// reads the same source. The two panels are intentionally redundant
/// (creator direction, choosing "mirror the real wallet" over
/// simplifying this line to just the eaten-citizen tally): this one
/// stays a fast at-a-glance readout next to the selection info,
/// ResourceHud is the full six-currency-plus-supply breakdown.</summary>
public class HudStatus : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private WaypointCommander _commander;
    private bool _showHelp;

    // 2026-08 (creator report: "instructions and icons in the top left
    // ... overlapping"): HudStatus's own line count is dynamic (traffic
    // line only once cars exist, 1 vs 3 lines depending on selection),
    // so anything anchored below it with a FIXED offset (BuildMenuHud
    // was hardcoded to y=140) can land inside HudStatus's own text once
    // enough lines are showing. Published here, in reference-space
    // pixels (the same space every Rect in this file is in), so other
    // top-left panels can read it and stay clear without duplicating
    // HudStatus's own line-counting logic.
    public static float ContentBottom { get; private set; }

    // 2026-08 follow-up (creator report: "collector lab is hidden under
    // a lot of text"): the 5 always-on control-reference lines that used
    // to print here permanently -- static text, never depends on match
    // state, but still ate ~100px of the top-left corner every match,
    // which is exactly the real estate every other top-left panel
    // (WindowLightsHud, BuildMenuHud, CollectorLabHud) stacks below via
    // ContentBottom. Moved behind a single (i) button into an on-demand,
    // centered popup instead -- shrinks HudStatus's default footprint
    // (and therefore ContentBottom) down to just the LIVE status lines,
    // and the popup itself is centered on screen (not corner-stacked)
    // so it never competes with any button row for space even while open.
    public static bool PointerOver { get; private set; }

    private static readonly string[] HelpLines =
    {
        "Left-click a monster · left-drag box-select · double-click all of a type (Shift adds)",
        "Right-click (or Ctrl+click) orders the group: ground = waypoint (Shift queues) · citizen = eat · tank/wall = attack · roof = winged land on it",
        "A + click: attack-move (auto-engage en route) · P + click: patrol back and forth · hold Ctrl too if A/P is also panning the camera",
        "Camera: WASD pan (Ctrl held disables it) · Q/E rotate · scroll zoom · middle-drag / screen-edge scroll",
        "G: jump to the unit nearest the cursor",
    };

    public void Init(RuntimeCityBuilder builder, WaypointCommander commander)
    {
        _builder = builder;
        _commander = commander;
    }

    private void OnGUI()
    {
        if (_builder == null) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();
        var e = Event.current;
        PointerOver = false;

        var y = 8f;
        var bridge = _builder.SimBridge;
        var haveMatch = bridge != null && bridge.HasMatch;
        var blood = haveMatch ? bridge.PlayerWallet(0, ResourceKind.Blood) : 0;
        var bones = haveMatch ? bridge.PlayerWallet(0, ResourceKind.Bones) : 0;
        var brains = haveMatch ? bridge.PlayerWallet(0, ResourceKind.Brains) : 0;
        Line(ref y, "🩸 " + blood + "   🦴 " + bones + "   🧠 " + brains
            + "   (eaten citizens: " + _builder.CitizensEaten + ")");

        if (_builder.TrafficCarCount > 0)
        {
            var live = Mathf.RoundToInt(_builder.TrafficMovingFraction * 100f);
            var target = Mathf.RoundToInt(_builder.trafficMovingPercent * 100f);
            Line(ref y, "🚗 traffic: " + live + "% moving (target " + target + "%, " + _builder.TrafficCarCount + " cars)");
        }

        var selected = _commander != null ? _commander.SelectedAgent : null;
        var count = _commander != null ? _commander.SelectedCount : 0;
        if (selected != null && count > 1)
        {
            Line(ref y, "▶ " + count + " units selected — lead: " + selected.DisplayName
                + " — " + selected.OrderDescription);
        }
        else if (selected != null)
        {
            Line(ref y, "▶ " + selected.DisplayName + " — " + selected.OrderDescription);
            Line(ref y, "   " + selected.CombatDescription);
            Line(ref y, "   " + selected.SpeedDescription);
        }

        var helpRect = new Rect(12f, y, 28f, 24f);
        if (e != null && helpRect.Contains(e.mousePosition)) PointerOver = true;
        if (GUI.Button(helpRect, "(i)")) _showHelp = !_showHelp;
        y += 28f;

        ContentBottom = y;

        if (_showHelp) DrawHelpPopup(e);

        UiScale.End(prevMatrix);
    }

    /// <summary>Centered on screen (deliberately NOT corner-stacked like
    /// every other panel) -- a popup should never compete with a button
    /// row for space, and centering it guarantees that regardless of how
    /// tall it is or how the top-left stack below HudStatus changes in
    /// the future.</summary>
    private void DrawHelpPopup(Event e)
    {
        const float width = 760f;
        const float lineHeight = 22f;
        const float padding = 10f;
        var height = HelpLines.Length * lineHeight + padding * 2f + 8f;
        var rect = new Rect((UiScale.Width - width) * 0.5f, 60f, width, height);
        if (e != null && rect.Contains(e.mousePosition)) PointerOver = true;

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var closeRect = new Rect(rect.xMax - 30f, rect.y + 6f, 24f, 24f);
        if (GUI.Button(closeRect, "X")) _showHelp = false;

        var ty = rect.y + padding;
        foreach (var line in HelpLines)
        {
            Line(ref ty, line, rect.x + padding);
        }
    }

    private static void Line(ref float y, string text, float x = 12f)
    {
        // draw a cheap shadow so it reads over any city color below
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(x + 1f, y + 1f, 900f, 24f), text);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 900f, 24f), text);
        y += 20f;
    }
}
