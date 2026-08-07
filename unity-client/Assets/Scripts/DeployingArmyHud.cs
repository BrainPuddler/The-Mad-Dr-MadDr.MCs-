using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Add a loading bar, when the game starts.
/// Call it 'Deploying Army' 0-100%"). Investigated before building this:
/// <see cref="RuntimeCityBuilder.BeginMatch"/> is one big synchronous
/// call (city generation, terrain, buildings, camera setup) with no
/// coroutines/yields anywhere in it -- Unity doesn't render a frame or
/// run OnGUI mid-call, so nothing could actually ANIMATE a bar during
/// that phase no matter what this script did; the whole city just
/// appears in one stall once <c>BeginMatch</c> returns. The one
/// genuinely async, frame-observable phase in the whole startup sequence
/// is <see cref="RosterFetcher"/>'s own fetch coroutine -- which is also,
/// conveniently, the literal "deploying army" moment (each creature
/// fetched here gets spawned onto the battlefield the instant the roster
/// resolves). So this bar covers that phase specifically, with REAL
/// progress, not a faked timer: <see cref="RosterFetcher.OnFetchProgress"/>
/// already advances one step per creature (each is its own yield-
/// separated web request inside <c>FetchRosterCoroutine</c>), so
/// `fetched/total` genuinely reflects how much of the roster has arrived.
///
/// Only exists in a scene once <see cref="RuntimeCityBuilder.BeginMatch"/>
/// starts the roster fetch (added/`Init`'d right alongside it) -- same
/// "opt-in component, centered OnGUI panel, self-destroys when done"
/// shape <see cref="RegionPickerHud"/>/<see cref="FactionPickerHud"/>
/// already establish for a full-screen pre-match overlay, and the same
/// IMGUI approach every HUD in this project uses (see HudStatus's own
/// header for why).
///
/// Monster spawning itself (<see cref="RuntimeCityBuilder.HandleRosterReady"/>'s
/// own spawn loop) is synchronous and cheap -- it runs to completion in
/// the same frame `OnRosterReady` fires, with no further yields to
/// observe. So "100%" is reported the instant the fetch resolves (ready
/// OR failed -- a failed/offline fetch still ends the loading phase, it
/// just doesn't add any monsters), which lines up exactly with the real
/// moment deployment actually happens, even though the spawn loop itself
/// isn't sub-divided into further visible steps.
/// </summary>
public class DeployingArmyHud : MonoBehaviour
{
    private const float PanelWidth = 420f;
    private const float PanelHeight = 84f;
    private const float BarHeight = 22f;
    private const float Padding = 18f;

    // How long the bar holds at a visible 100% before this component
    // removes itself -- long enough to actually register as "done," not
    // so long it lingers over gameplay that's already fully playable.
    private const float HoldAfterCompleteSeconds = 0.5f;

    private RosterFetcher _roster;
    private int _fetched;
    private int _total = -1; // -1 = menagerie response not back yet, denominator still unknown
    private bool _done;
    private float _doneAt;

    public void Init(RosterFetcher roster)
    {
        _roster = roster;
        _fetched = 0;
        _total = -1;
        _done = false;
        if (_roster != null)
        {
            _roster.OnFetchProgress += HandleFetchProgress;
            _roster.OnRosterReady += HandleDone;
            _roster.OnRosterFailed += HandleDoneFailed;
        }
    }

    private void HandleFetchProgress(int fetched, int total)
    {
        _fetched = fetched;
        _total = total;
    }

    private void HandleDone(RosterCache cache, bool wasFromCache)
    {
        MarkDone();
    }

    private void HandleDoneFailed(string reason)
    {
        MarkDone();
    }

    private void MarkDone()
    {
        if (_done) return;
        _done = true;
        _doneAt = Time.time;
    }

    private void Update()
    {
        if (_done && Time.time - _doneAt >= HoldAfterCompleteSeconds) Destroy(this);
    }

    private void OnDestroy()
    {
        if (_roster == null) return;
        _roster.OnFetchProgress -= HandleFetchProgress;
        _roster.OnRosterReady -= HandleDone;
        _roster.OnRosterFailed -= HandleDoneFailed;
    }

    /// <summary>0 while the menagerie response (and therefore `_total`)
    /// hasn't arrived yet -- there's genuinely nothing to divide by until
    /// then, so this reads as "just started" rather than guessing. Once
    /// `_total` is known, a `_total == 0` roster (no creatures at all)
    /// reads as fully complete immediately, since there's nothing left to
    /// fetch. Forced to exactly 1 once `_done`, regardless of the last
    /// real fetch count, so a failed/offline fetch still visibly
    /// completes the bar instead of freezing partway.</summary>
    private float Fraction()
    {
        if (_done) return 1f;
        if (_total < 0) return 0f;
        if (_total == 0) return 1f;
        return Mathf.Clamp01(_fetched / (float)_total);
    }

    private void OnGUI()
    {
        if (_roster == null) return;

        var screenW = Screen.width > 0 ? Screen.width : 1920;
        var screenH = Screen.height > 0 ? Screen.height : 1080;
        var panelRect = new Rect((screenW - PanelWidth) * 0.5f, (screenH - PanelHeight) * 0.5f, PanelWidth, PanelHeight);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var titleRect = new Rect(panelRect.x + Padding, panelRect.y + Padding * 0.5f, panelRect.width - Padding * 2f, 24f);
        DrawShadowedLabel(titleRect, "Deploying Army", new Color(0.95f, 0.9f, 0.7f, 1f));

        var fraction = Fraction();
        var barRect = new Rect(panelRect.x + Padding, titleRect.y + titleRect.height + 6f, panelRect.width - Padding * 2f, BarHeight);
        DrawBar(barRect, fraction, new Color(1f, 1f, 1f, 0.15f), new Color(0.75f, 0.25f, 0.2f, 1f));

        var pctLabel = Mathf.RoundToInt(fraction * 100f) + "%";
        var pctStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(barRect.x + 1f, barRect.y + 1f, barRect.width, barRect.height), pctLabel, pctStyle);
        GUI.color = Color.white;
        GUI.Label(barRect, pctLabel, pctStyle);
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
}
