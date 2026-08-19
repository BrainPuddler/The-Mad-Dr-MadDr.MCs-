using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2026-08 (creator direction: "What is the win/loose states?" -> "Yes do
/// that"): the missing other half of docs/02's "Victory conditions" --
/// match-core's own <see cref="MatchState.CheckMatchEnd"/> now decides a
/// real winner/loser/draw and WHY (Elimination/Dominion/TimeCap), but
/// nothing in Unity ever showed the player any of it; a decided match
/// just kept sitting there with no visible acknowledgment. This is that
/// screen -- invisible the entire match, then a full-screen overlay the
/// instant <see cref="SimBridge.IsMatchOver"/> goes true, reporting the
/// verdict from `localPlayerIndex`'s own perspective (Victory/Defeat/
/// Draw, never raw player-index numbers) plus which of the three
/// documented conditions actually fired, and a Play Again button.
///
/// "Play Again" reloads the active scene (`SceneManager.LoadScene`)
/// rather than hand-writing a reset path through every one of this
/// project's dozens of live HUD/economy/monster/city components -- none
/// of them expose a Reset() method, and a full scene reload is the
/// standard, safe way to guarantee EVERY piece of state (match-core's
/// MatchState included, since a fresh scene load re-runs BeginMatch from
/// scratch) actually returns to a clean start rather than risking a
/// half-reset scene with stale GameObjects left over from the finished
/// match.
///
/// Same self-contained IMGUI HUD idiom every other panel in this project
/// already uses (own private DrawShadowedLabel, no shared utility class)
/// -- see MatchSetupHud.cs for the closest sibling in look and feel.
/// </summary>
public class MatchEndHud : MonoBehaviour
{
    private SimBridge _bridge;
    private int _localPlayerIndex;

    public void Init(SimBridge simBridge, int localPlayerIndex)
    {
        _bridge = simBridge;
        _localPlayerIndex = localPlayerIndex;
    }

    private const float PanelWidth = 480f;
    private const float PanelHeight = 260f;
    private const float Gap = 12f;
    private const float TitleHeight = 64f;
    private const float ReasonHeight = 40f;
    private const float ButtonHeight = 44f;

    private void OnGUI()
    {
        if (_bridge == null || !_bridge.IsMatchOver) return;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;
        var panelRect = new Rect((screenW - PanelWidth) * 0.5f, (screenH - PanelHeight) * 0.5f, PanelWidth, PanelHeight);

        // Dim the whole screen behind the panel -- same "this is a modal,
        // not a corner widget" treatment MatchSetupHud's own full-screen
        // pickers use, just darker (a decided match is a harder stop than
        // a pre-match menu).
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var winner = _bridge.WinnerPlayerIndex;
        string title;
        Color titleColor;
        if (winner == _localPlayerIndex) { title = "VICTORY"; titleColor = new Color(0.35f, 0.85f, 0.4f); }
        else if (winner == null) { title = "DRAW"; titleColor = new Color(0.85f, 0.8f, 0.4f); }
        else { title = "DEFEAT"; titleColor = new Color(0.85f, 0.3f, 0.3f); }

        var y = panelRect.y + Gap;
        var titleRect = new Rect(panelRect.x, y, panelRect.width, TitleHeight);
        var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        DrawShadowedLabel(titleRect, title, titleColor, titleStyle);
        y += TitleHeight + Gap;

        var reasonRect = new Rect(panelRect.x + Gap, y, panelRect.width - Gap * 2f, ReasonHeight);
        var reasonStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        DrawShadowedLabel(reasonRect, ReasonText(_bridge.EndReason, winner, _localPlayerIndex), new Color(0.85f, 0.85f, 0.85f), reasonStyle);
        y += ReasonHeight + Gap * 2f;

        var buttonRect = new Rect(panelRect.x + panelRect.width * 0.5f - 90f, y, 180f, ButtonHeight);
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.35f);
        if (GUI.Button(buttonRect, "Play Again"))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        GUI.backgroundColor = prevBg;

        UiScale.End(prevMatrix);
    }

    /// <summary>docs/02's own three named conditions, put in plain
    /// language from `localPlayerIndex`'s perspective -- "you held" vs.
    /// "the enemy held" rather than a raw player index, matching the
    /// title's own Victory/Defeat/Draw framing.</summary>
    private static string ReasonText(MatchEndReason reason, int? winner, int localPlayerIndex)
    {
        var youWon = winner == localPlayerIndex;
        switch (reason)
        {
            case MatchEndReason.Elimination:
                return winner == null
                    ? "Both headquarters fell in the same instant."
                    : youWon ? "The enemy Headquarters was destroyed." : "Your Headquarters was destroyed.";
            case MatchEndReason.Dominion:
                return youWon
                    ? "Dominion: you held the majority of the map's emitters for a full Lumen Cycle."
                    : "Dominion: the enemy held the majority of the map's emitters for a full Lumen Cycle.";
            case MatchEndReason.TimeCap:
                return winner == null
                    ? "Time expired -- territory was exactly tied."
                    : youWon ? "Time expired -- you held more territory." : "Time expired -- the enemy held more territory.";
            default:
                return string.Empty;
        }
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color, GUIStyle style)
    {
        var shadowStyle = new GUIStyle(style);
        var shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(shadowRect, text, shadowStyle);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = Color.white;
    }
}
