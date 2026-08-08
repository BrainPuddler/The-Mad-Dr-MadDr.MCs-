using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/23 §13 amendment B (Phase 3.5) named this trio "the Unity moon-
/// dial/capture-bar/mana HUD" and deferred all three -- the sim-side data
/// they read (LumenClock/SimEmitter/PlayerState.Mana) has been done and
/// tested since Phase 3.5/4/7, but nothing in Unity ever displayed it.
/// This is that HUD: current Lumen phase + a pre-transition warning (the
/// "moon dial," docs' own glossary term -- "the always-public HUD clock
/// showing the current Lumen phase and the 10-second transition
/// warning"), the local player's mana balance against its 100 cap
/// (docs/03), and a progress bar for every emitter currently mid-capture
/// (docs/03: "progress bar visible to both players," the exact use SimEmitter.
/// CaptureProgressTicks' own doc comment names).
///
/// IMGUI, same as every other HUD element in this project (see
/// HudStatus's header for why). A no-op until <see cref="Init"/> is
/// called with a live <see cref="SimBridge"/> AND that bridge has an
/// active match (<see cref="SimBridge.HasMatch"/>) -- same "absent until
/// a scene explicitly opts in" discipline SimBridge's own header
/// describes; nothing here assumes a match is running.
///
/// Deliberately does NOT share Minimap's <c>ScreenCorner</c> enum type
/// (unlike AnalogClockHud, which does) -- this file has no other
/// dependency on Minimap.cs, and staying self-contained keeps its (much
/// larger) camera/input/fog-of-war surface out of this HUD's compile
/// graph. Same four-corner shape and field names regardless, so tuning
/// feels identical.
///
/// Placement default is bottom-right -- HudStatus already owns the fixed
/// top-left corner, AnalogClockHud defaults top-right, Minimap defaults
/// bottom-left, so this is the one open corner. All fully
/// developer-repositionable via the same corner/margin/useCustomPosition
/// fields those two already use.
///
/// 2026-07 follow-up: capture bars now hover directly over the actual
/// emitter hex in 3D space (<see cref="Camera.WorldToScreenPoint"/> +
/// the top-left/Y-down flip OnGUI's own coordinate space needs), instead
/// of a fixed on-screen list -- the literal reading of docs/03's
/// "progress bar visible to both players," not just a same-information
/// substitute. The fixed panel below now only ever holds the phase/mana
/// pair (fixed height -- it no longer grows with capture count).
///
/// Honesty note (2026-07, no Unity Editor in this environment): the
/// panel's pure formatting/threshold logic below (<see cref="PhaseLabel"/>,
/// <see cref="IsTransitionWarning"/>, <see cref="FormatCountdown"/>,
/// <see cref="Fraction"/>, <see cref="FlashOn"/>) was verified by a real
/// standalone compile+run against match-core's actual built DLL (no
/// UnityEngine stubs needed for that part -- none of those methods touch
/// a UnityEngine type). The OnGUI layout/draw calls compiled clean
/// against a minimal UnityEngine stub set but have not been seen in a
/// real render, same caveat AnalogClockHud's own header already flags
/// for itself.
/// </summary>
public class LumenHud : MonoBehaviour
{
    public enum ScreenCorner { BottomLeft, BottomRight, TopLeft, TopRight }

    private const int WarningSeconds = 10;   // docs' glossary: "the 10-second transition warning"

    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;
    public int localPlayerIndex = 0;

    [Header("World-space capture marker")]
    [Tooltip("How far above ground level a capture marker floats, in meters.")]
    public float captureMarkerHeight = 12f;
    public float captureMarkerWidth = 150f;

    [Header("Placement (default bottom-right; developer-tunable anywhere)")]
    public ScreenCorner corner = ScreenCorner.BottomRight;
    public Vector2 marginPixels = new Vector2(16f, 16f);
    public float panelWidth = 260f;
    [Tooltip("Bypasses the corner presets entirely for pixel-exact placement anywhere on screen.")]
    public bool useCustomPosition = false;
    public Vector2 customTopLeftPixels = new Vector2(16f, 16f);

    [Header("Colors")]
    public Color backingColor = new Color(0.02f, 0.02f, 0.02f, 0.65f);
    public Color phaseColor = new Color(0.85f, 0.85f, 0.95f, 1f);
    public Color warningColor = new Color(1f, 0.35f, 0.2f, 1f);
    public Color manaTextColor = new Color(0.75f, 0.6f, 1f, 1f);
    public Color manaFillColor = new Color(0.55f, 0.3f, 0.95f, 1f);
    public Color captureTextColor = new Color(0.9f, 0.9f, 0.6f, 1f);
    public Color captureFillColor = new Color(0.9f, 0.75f, 0.2f, 1f);
    public Color barBackgroundColor = new Color(1f, 1f, 1f, 0.15f);

    private const float Padding = 10f;
    private const float PhaseLineHeight = 24f;
    private const float LineHeight = 18f;
    private const float BarHeight = 12f;
    private const float RowGap = 4f;

    // ---- pure logic (no UnityEngine types) -- real-compiled + run
    // standalone against match-core's actual DLL, see class header ----

    public static string PhaseLabel(LumenPhase phase)
    {
        switch (phase)
        {
            case LumenPhase.Dawn: return "Dawn";
            case LumenPhase.Day: return "Day";
            case LumenPhase.Dusk: return "Dusk";
            default: return "Night";
        }
    }

    /// <summary>True once fewer than <see cref="WarningSeconds"/> remain
    /// in the current phase.</summary>
    public static bool IsTransitionWarning(int ticksRemaining, int ticksPerSecond)
    {
        return ticksRemaining <= ticksPerSecond * WarningSeconds;
    }

    /// <summary>Floor to whole seconds -- a HUD countdown that ticks
    /// exactly once per real second, not a fractional readout.</summary>
    public static string FormatCountdown(int ticksRemaining, int ticksPerSecond)
    {
        var seconds = ticksPerSecond > 0 ? ticksRemaining / ticksPerSecond : 0;
        return seconds + "s";
    }

    /// <summary>Clamped [0,1] fill fraction for a bar -- defensive against
    /// a zero/negative cap rather than dividing by zero.</summary>
    public static float Fraction(int value, int cap)
    {
        if (cap <= 0) return 0f;
        var f = (float)value / cap;
        if (f < 0f) return 0f;
        if (f > 1f) return 1f;
        return f;
    }

    /// <summary>Discrete on/off flash (snap, not a smooth pulse -- same
    /// idiom AnalogClockHud's pendulum tick uses) for the warning state.</summary>
    public static bool FlashOn(float timeSeconds, float intervalSeconds)
    {
        if (intervalSeconds <= 0f) return true;
        var step = (int)(timeSeconds / intervalSeconds);
        return step % 2 == 0;
    }

    // ---- wiring ----

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder, int playerIndex)
    {
        bridge = simBridge;
        builder = cityBuilder;
        localPlayerIndex = playerIndex;
    }

    // ---- draw ----

    private void OnGUI()
    {
        if (bridge == null || !bridge.HasMatch) return;

        DrawPhaseManaPanel();
        DrawCaptureMarkers();
    }

    // 2026-08 (creator direction: "the ui is not scaling properly to
    // screen sizes"): scaling is applied HERE, wrapping only this fixed
    // panel -- NOT the whole OnGUI, since DrawCaptureMarkers below is
    // world-anchored (Camera.WorldToScreenPoint, already true screen
    // pixels) and must stay outside any GUI.matrix scale or its markers
    // would detach from the emitters they're meant to float over. See
    // UiScale.cs's own header for the full "screen-space panel vs
    // world-anchored" distinction this project's HUDs now follow.
    private void DrawPhaseManaPanel()
    {
        var prevMatrix = UiScale.Begin();
        var phase = bridge.CurrentLumenPhase;
        var ticksRemaining = bridge.TicksUntilNextLumenPhase;
        var warning = IsTransitionWarning(ticksRemaining, MatchState.TicksPerSecond);
        var mana = bridge.PlayerMana(localPlayerIndex);

        var height = Padding * 2f + PhaseLineHeight + (LineHeight + BarHeight + RowGap);
        var rect = GetPanelRect(height);
        if (rect.width <= 0f) { UiScale.End(prevMatrix); return; }

        GUI.color = backingColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = rect.x + Padding;
        var y = rect.y + Padding;
        var innerWidth = rect.width - Padding * 2f;

        var phaseText = "☽ " + PhaseLabel(phase) + " — " + FormatCountdown(ticksRemaining, MatchState.TicksPerSecond) + " to next";
        var phaseDrawColor = warning && !FlashOn(Time.time, 0.5f) ? Color.Lerp(warningColor, phaseColor, 0.5f) : (warning ? warningColor : phaseColor);
        DrawShadowedLabel(new Rect(x, y, innerWidth, PhaseLineHeight), phaseText, phaseDrawColor);
        y += PhaseLineHeight;

        DrawShadowedLabel(new Rect(x, y, innerWidth, LineHeight), "Mana " + mana + "/" + PlayerState.ManaCap, manaTextColor);
        y += LineHeight;
        DrawBar(new Rect(x, y, innerWidth, BarHeight), Fraction(mana, PlayerState.ManaCap), barBackgroundColor, manaFillColor);

        UiScale.End(prevMatrix);
    }

    /// <summary>docs/03's "progress bar visible to both players," drawn
    /// where it actually reads best: floating over the emitter itself.
    /// Skips silently (no fixed-panel fallback) when there's no camera or
    /// city to project against yet, or when an emitter's marker point
    /// falls behind the camera -- exactly the same "just don't draw it"
    /// contract every other screen-space overlay in this project already
    /// follows for an off-screen/undefined case.</summary>
    private void DrawCaptureMarkers()
    {
        var cam = Camera.main;
        if (cam == null || builder == null) return;

        for (var i = 0; i < bridge.EmitterCount; i++)
        {
            var e = bridge.EmitterAt(i);
            if (e.CapturingPlayer == null) continue;

            var hexWorld = builder.WorldOf(e.Hex);
            var groundY = builder.GroundHeightAt(hexWorld);
            var markerWorld = new Vector3(hexWorld.x, groundY + captureMarkerHeight, hexWorld.z);
            if (!TryWorldToGui(cam, markerWorld, out var gp)) continue;

            var frac = Fraction(e.CaptureProgressTicks, SimEmitter.CaptureChannelTicks);
            var h = LineHeight + BarHeight + 2f;
            var markerRect = new Rect(gp.x - captureMarkerWidth * 0.5f, gp.y - h, captureMarkerWidth, h);

            DrawShadowedLabel(new Rect(markerRect.x, markerRect.y, markerRect.width, LineHeight), "P" + e.CapturingPlayer.Value + " capturing", captureTextColor);
            DrawBar(new Rect(markerRect.x, markerRect.y + LineHeight, markerRect.width, BarHeight), frac, barBackgroundColor, captureFillColor);
        }
    }

    /// <summary>Camera.WorldToScreenPoint's own space is bottom-left
    /// origin, Y-up, with z = distance-in-front-of-camera (negative/zero
    /// means behind it -- must not draw, or the marker would appear to
    /// mirror to the wrong side of the screen). OnGUI's Rect space is
    /// top-left origin, Y-down -- the same flip AnalogClockHud/Minimap's
    /// own screen-corner math already accounts for, just via a different
    /// route (theirs never projects a 3D point, this is the first HUD
    /// element in this project that does).</summary>
    private static bool TryWorldToGui(Camera cam, Vector3 world, out Vector2 guiPoint)
    {
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) { guiPoint = default; return false; }
        guiPoint = new Vector2(sp.x, Screen.height - sp.y);
        return true;
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }

    private static void DrawBar(Rect rect, float fraction, Color bg, Color fill)
    {
        GUI.color = bg;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        if (fraction > 0f)
        {
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private Rect GetPanelRect(float height)
    {
        // Reads UiScale.Width/Height (the fixed reference canvas), not
        // real Screen.width/height -- DrawPhaseManaPanel wraps its whole
        // body in UiScale.Begin(), so this must return Rects in that same
        // reference space. UiScale's constants are never 0, so the old
        // zero-Screen-size fallback/warning this replaced no longer
        // applies.
        var screenW = UiScale.Width;
        var screenH = UiScale.Height;
        if (useCustomPosition) return new Rect(customTopLeftPixels.x, customTopLeftPixels.y, panelWidth, height);
        float x, y;
        switch (corner)
        {
            case ScreenCorner.BottomLeft: x = marginPixels.x; y = screenH - marginPixels.y - height; break;
            case ScreenCorner.BottomRight: x = screenW - marginPixels.x - panelWidth; y = screenH - marginPixels.y - height; break;
            case ScreenCorner.TopLeft: x = marginPixels.x; y = marginPixels.y; break;
            default: x = screenW - marginPixels.x - panelWidth; y = marginPixels.y; break;   // TopRight
        }
        return new Rect(x, y, panelWidth, height);
    }
}
