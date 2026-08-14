using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Human Army is from army barracks --
/// part of the basic kit for Human army"; full production building
/// confirmed, not a cosmetic prop). A REAL Barracks that no one can ever
/// queue training at isn't done -- this closes that gap with the
/// smallest real affordance, same "collapsed tab + small panel" shape
/// <see cref="CollectorLabHud"/> already established for the exact same
/// problem (a real production building with no way to use it from the
/// UI).
///
/// Human Army only (Barracks is that faction's own building -- see
/// <see cref="BuildingKind.Barracks"/>'s own doc comment), a no-op for
/// any other local faction or before a match exists, same gating
/// discipline <see cref="CollectorLabHud.IsMadDoctor"/> already uses for
/// its own faction-specific panel. Collapsed to a small corner tab by
/// default, toggled by the 'B' key (mnemonic: Barracks) or a click on
/// the tab itself -- deliberately a DIFFERENT hotkey from CollectorLabHud's
/// 'L', so a Mixed-faction player who somehow has both a Big Brain and a
/// Barracks (a real if unlikely case) can toggle either independently.
///
/// Deliberately minimal: two Train buttons (Rifleman, Flamethrower
/// Trooper) against the player's own nearest idle Complete Barracks --
/// no queue depth/priority UI, no multi-Barracks selection (picks the
/// first idle one, same "candidate" search shape TryTrain already uses
/// for Big Brain). A real, functioning affordance, not full production-
/// game UI polish -- that's a genuine, separate follow-up, not attempted
/// here.
/// </summary>
public class BarracksHud : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private int _playerIndex;

    [Header("Placement (top-left corner, stacked below CollectorLabHud)")]
    public float leftMarginPixels = 12f;
    public float dockGapPixels = 8f;
    public float tabWidth = 150f;
    public float tabHeight = 24f;
    public float panelWidth = 260f;
    public float rowHeight = 22f;

    public static bool PointerOver { get; private set; }

    private bool _open;
    private string _statusMessage = "";
    private float _statusMessageTimer;

    public void Init(RuntimeCityBuilder builder, int playerIndex)
    {
        _builder = builder;
        _playerIndex = playerIndex;
    }

    private void Update()
    {
        if (_statusMessageTimer > 0f) _statusMessageTimer -= Time.deltaTime;
        if (_builder == null || _builder.SimBridge == null || !_builder.SimBridge.HasMatch) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.bKey.wasPressedThisFrame) _open = !_open;
    }

    private bool IsHumanArmy()
    {
        return _builder != null && _builder.SimBridge != null && _builder.SimBridge.HasMatch
            && _builder.SimBridge.PlayerFaction(_playerIndex) == FactionId.HumanArmy;
    }

    private void OnGUI()
    {
        if (!IsHumanArmy()) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        // Chains off the same corner stack CollectorLabHud's own tab
        // does -- CollectorLabHud doesn't publish its own Bottom (its
        // collapsed tab height is fixed and it doesn't get chained off
        // today), so this adds one extra fixed tab-height step on top of
        // the same WindowLightsHud/BuildMenuHud anchor rather than
        // guessing a brand-new corner. A real Bottom-publishing chain
        // (matching CollectorLabHud's own upstream precedent) would be
        // the more robust fix if a FIFTH panel ever needs to stack here
        // too -- flagged, not attempted for one panel.
        var upstreamBottom = BuildMenuHud.Bottom > 0f ? BuildMenuHud.Bottom : WindowLightsHud.Bottom;
        var tabY = upstreamBottom + dockGapPixels + tabHeight + dockGapPixels;
        var tabRect = new Rect(leftMarginPixels, tabY, tabWidth, tabHeight);
        var e = Event.current;
        PointerOver = e != null && tabRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(tabRect, "Barracks (B)")) _open = !_open;

        if (_open)
        {
            var panelRect = new Rect(leftMarginPixels, tabRect.yMax + 4f, panelWidth, PanelHeight());
            if (e != null && panelRect.Contains(e.mousePosition)) PointerOver = true;
            DrawPanel(panelRect);
        }

        UiScale.End(prevMatrix);
    }

    private float PanelHeight()
    {
        var statusRows = _statusMessageTimer > 0f ? 1 : 0;
        const int trainRows = 2;   // Rifleman, Flamethrower Trooper
        return rowHeight * (trainRows + statusRows) + (statusRows > 0 ? 4f : 0f) + 16f;
    }

    private void DrawPanel(Rect panelRect)
    {
        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.78f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = panelRect.x + 8f;
        var y = panelRect.y + 6f;
        var innerWidth = panelRect.width - 16f;

        DrawTrainRow(new Rect(x, y, innerWidth, rowHeight - 2f), RosterUnitKind.Rifleman, "Rifleman");
        y += rowHeight;
        DrawTrainRow(new Rect(x, y, innerWidth, rowHeight - 2f), RosterUnitKind.FlamethrowerTrooper, "Flamethrower Trooper");
        y += rowHeight;

        if (_statusMessageTimer > 0f)
        {
            y += 4f;
            var statusColor = _statusMessage.StartsWith("Training") ? new Color(0.55f, 0.9f, 0.55f, 1f) : new Color(0.95f, 0.55f, 0.4f, 1f);
            DrawShadowedLabel(new Rect(x, y, innerWidth, rowHeight), _statusMessage, statusColor);
        }
    }

    private void DrawTrainRow(Rect rect, RosterUnitKind kind, string label)
    {
        var labelRect = new Rect(rect.x, rect.y, rect.width - 68f, rect.height);
        DrawShadowedLabel(labelRect, label);
        var trainRect = new Rect(rect.x + rect.width - 66f, rect.y, 66f, rect.height);
        if (GUI.Button(trainRect, "Train")) TryTrain(kind, label);
    }

    /// <summary>Picks the player's own idle (no <see
    /// cref="SimBuilding.TrainingKind"/> already set) Complete Barracks
    /// and queues training there -- same "diagnose the exact reason,
    /// don't fail silently" discipline <see cref="CollectorLabHud.TryTrain"/>
    /// already established for the identical Big-Brain-battalion
    /// problem.</summary>
    private void TryTrain(RosterUnitKind kind, string label)
    {
        var bridge = _builder.SimBridge;
        if (bridge == null || !bridge.HasMatch) { SetStatus("No live match."); return; }

        uint? candidate = null;
        var hasCompleteBarracks = false;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex != _playerIndex || b.Kind != BuildingKind.Barracks || b.State != BuildingState.Complete) continue;
            hasCompleteBarracks = true;
            if (b.TrainingKind == null) { candidate = b.EntityId; break; }
        }

        if (!hasCompleteBarracks) { SetStatus("Need a Complete Barracks first."); return; }
        if (candidate == null) { SetStatus("Every Barracks is already training."); return; }
        if (!bridge.CanTrainUnit(_playerIndex, candidate.Value, kind)) { SetStatus("Can't afford " + label + " right now."); return; }

        bridge.QueueTrainCommand(_playerIndex, candidate.Value, kind);
        SetStatus("Training " + label + " started.");
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        _statusMessageTimer = 4f;
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color? color = null)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color ?? new Color(0.92f, 0.88f, 0.78f, 1f);
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
