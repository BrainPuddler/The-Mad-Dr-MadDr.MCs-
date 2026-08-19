using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Provide a hud above the mini map showing
/// win game % as red and green text. For all three win game states.",
/// then: "move the WinProgressHud to the top of the screen Centered
/// make it one line."): docs/37/docs/12's win/loss-states pass
/// (Elimination/Dominion/TimeCap) gave match-core a real, queryable
/// verdict-in-progress for each condition, but nothing showed it to the
/// player before the match actually ended (<see cref="MatchEndHud"/>
/// only appears once it's already over). This is that live readout --
/// one line, top-center, three segments (one per docs/02 victory
/// condition), each a percentage colored green (&gt;=50%, favoring the
/// local player) or red (&lt;50%).
///
/// **Placement, and the standing rule for any future top-of-screen HUD
/// addition in this project**: top-center was picked because it's the
/// one region of the screen NOTHING else claims. A survey of every
/// OnGUI panel anchored near the top (2026-08) found: top-left is a
/// chained stack (`HudStatus` -> `WindowLightsHud` -> `BuildMenuHud` ->
/// `CollectorLabHud`/`LabBattalionHud`, each docking off the previous
/// one's own published `Bottom`, roughly x 12-900, y 8 down to 250+);
/// top-right is `AnalogClockHud` (default corner, ~108px square) with
/// `ResourceHud` docked directly below it (`topOffsetPixels = 210`,
/// explicitly there to clear the clock's own footprint). Top-center had
/// no occupant at all. **Before adding or moving any HUD element to a
/// screen edge, re-check what already lives there** (grep for
/// `ScreenCorner`/hardcoded small x or y offsets/`topOffsetPixels`-style
/// clearance fields) rather than assuming a corner is free -- this file
/// itself used to sit above <see cref="Minimap"/> for exactly that
/// reason (a real, occupied bottom-left region), and moved here only
/// once top-center was confirmed genuinely empty.
///
/// Every percentage is a flagged v0.1 heuristic, same standing policy as
/// docs/37's own TerritoryScore weighting -- none of these are a real
/// modeled win probability (no AI/statistical model exists anywhere in
/// this project for that); they're simple, honest SHARE metrics built
/// entirely from state <see cref="MatchState"/>/<see cref="SimBridge"/>
/// already expose:
///
/// - **Army %** (Elimination proxy) = the local player's own live unit
///   count as a share of (itself + the single STRONGEST opponent's live
///   unit count) -- the same intuition as any RTS "army value"
///   comparison; whoever's ahead here is the one more likely to win the
///   war of attrition that ends in an Hq falling.
/// - **Dominion %** = <see cref="PlayerState.DominionStreakTicks"/> as a
///   share of the full <see cref="LumenClock.CycleTicks"/> hold a
///   Dominion win requires. Reads 0% whenever the local player currently
///   holds under 60% of the map's emitters -- the streak itself resets
///   then (docs/37 explains why that's correct behavior, not a display
///   bug, and a different rule from an individual emitter's own
///   capture-contest freeze).
/// - **Territory %** = the local player's own <see
///   cref="MatchState.TerritoryScore"/> as a share of (itself + the
///   strongest opponent's) -- same "share vs. the best rival" shape as
///   Army %, but the actual number that decides the 15-minute time cap.
///
/// Self-contained IMGUI (see HudStatus's own header for why every panel
/// in this project is OnGUI, not uGUI), own DrawShadowedLabel, no shared
/// utility class -- same idiom every HUD file here already follows.
/// Invisible once <see cref="SimBridge.IsMatchOver"/> goes true --
/// <see cref="MatchEndHud"/> takes over at that point, and a live
/// "still climbing toward Dominion" readout stops meaning anything once
/// the match is already decided.
/// </summary>
public class WinProgressHud : MonoBehaviour
{
    private SimBridge _bridge;
    private int _localPlayerIndex;

    public void Init(SimBridge simBridge, int localPlayerIndex)
    {
        _bridge = simBridge;
        _localPlayerIndex = localPlayerIndex;
    }

    private const float TopMarginPixels = 16f;
    private const float RowHeight = 24f;
    private const float SegmentWidth = 170f;
    private const float SegmentCount = 3f;
    private const float LabelWidth = 68f;
    private const float SidePad = 8f;

    private static readonly Color GoodColor = new Color(0.35f, 0.95f, 0.4f);
    private static readonly Color BadColor = new Color(0.95f, 0.3f, 0.25f);
    private static readonly Color PanelColor = new Color(0.02f, 0.02f, 0.02f, 0.65f);

    private void OnGUI()
    {
        if (_bridge == null || !_bridge.HasMatch || _bridge.IsMatchOver) return;
        var prevMatrix = UiScale.Begin();

        var totalWidth = SegmentWidth * SegmentCount;
        var panelRect = new Rect(UiScale.Width * 0.5f - totalWidth * 0.5f, TopMarginPixels, totalWidth, RowHeight);

        GUI.color = PanelColor;
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = panelRect.x;
        x = DrawSegment(x, panelRect.y, "Army", EliminationPercent());
        x = DrawSegment(x, panelRect.y, "Dominion", DominionPercent());
        DrawSegment(x, panelRect.y, "Territory", TerritoryPercent());

        UiScale.End(prevMatrix);
    }

    private float DrawSegment(float x, float y, string label, float percent)
    {
        var labelRect = new Rect(x + SidePad, y, LabelWidth, RowHeight);
        GUI.Label(labelRect, label);

        var valueRect = new Rect(labelRect.xMax, y, SegmentWidth - LabelWidth - SidePad, RowHeight);
        var color = percent >= 50f ? GoodColor : BadColor;
        DrawShadowedLabel(valueRect, Mathf.RoundToInt(percent) + "%", color);
        return x + SegmentWidth;
    }

    private float EliminationPercent()
    {
        var playerCount = _bridge.PlayerCount;
        if (playerCount <= 0 || _localPlayerIndex >= playerCount) return 50f;

        // Small fixed-size array, not a Dictionary -- same "player count
        // is always small (2-8) and fixed for the match" idiom
        // PlayerState's own per-resource wallet array already uses.
        var counts = new int[playerCount];
        for (var i = 0; i < _bridge.UnitCount; i++)
        {
            var u = _bridge.UnitAt(i);
            if (u == null || !u.IsAlive) continue;
            if (u.PlayerIndex >= 0 && u.PlayerIndex < playerCount) counts[u.PlayerIndex]++;
        }
        return SharePercent(counts[_localPlayerIndex], StrongestOpponent(counts));
    }

    private float TerritoryPercent()
    {
        var playerCount = _bridge.PlayerCount;
        if (playerCount <= 0 || _localPlayerIndex >= playerCount) return 50f;

        var scores = new int[playerCount];
        for (var i = 0; i < playerCount; i++) scores[i] = _bridge.TerritoryScore(i);
        return SharePercent(scores[_localPlayerIndex], StrongestOpponent(scores));
    }

    private float DominionPercent()
    {
        var streak = _bridge.PlayerDominionStreakTicks(_localPlayerIndex);
        return Mathf.Clamp01((float)streak / LumenClock.CycleTicks) * 100f;
    }

    private int StrongestOpponent(int[] values)
    {
        var best = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (i == _localPlayerIndex) continue;
            if (values[i] > best) best = values[i];
        }
        return best;
    }

    /// <summary>mine / (mine + best), as a percentage -- 50% (neutral;
    /// neither color reads as meaningfully wrong) when both are exactly
    /// zero, e.g. before either side has fielded anything or captured
    /// any territory yet.</summary>
    private static float SharePercent(int mine, int best)
    {
        var total = mine + best;
        return total <= 0 ? 50f : mine * 100f / total;
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = Color.white;
    }
}
