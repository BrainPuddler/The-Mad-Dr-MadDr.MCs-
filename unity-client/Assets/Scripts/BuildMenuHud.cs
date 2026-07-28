using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/23 §2 Phase 2's "build-menu IMGUI panel (HudStatus conventions)"
/// -- the buildable roster (<see cref="BuildingDef.AllDefs"/>, everything
/// except <see cref="BuildingKind.Hq"/>, which is generator-placed, never
/// player-built) as a clickable list of hotkey-numbered rows, each
/// showing its cost per <see cref="ResourceKind"/> and grayed out (still
/// clickable, just visibly unaffordable) when the local player can't
/// currently afford it. Clicking a row -- or its number-key hotkey --
/// hands the kind to <see cref="BuildGhostCursor"/> to start a placement
/// preview; this script owns selection state, not placement itself,
/// mirroring how <see cref="WaypointCommander"/> owns unit selection
/// separately from order issuing.
///
/// IMGUI, same layer as every other HUD element in this project (see
/// HudStatus's header for why). Fixed top-left-BELOW-HudStatus by
/// default -- HudStatus's own stacked status lines already own the very
/// top-left corner, so this panel starts far enough down to clear them
/// (`topOffsetPixels`, developer-tunable) rather than fighting over the
/// same corner presets <see cref="LumenHud"/>/Minimap/AnalogClockHud use.
///
/// A no-op (renders nothing) until <see cref="Init"/> is called with a
/// live <see cref="SimBridge"/> that <see cref="SimBridge.HasMatch"/> --
/// same discipline every other SimBridge-reading HUD element in this
/// project already follows.
/// </summary>
public class BuildMenuHud : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public int localPlayerIndex = 0;

    [Header("Placement")]
    public Vector2 topLeftPixels = new Vector2(12f, 140f);
    public float rowWidth = 260f;

    private const float RowHeight = 22f;
    private const float Padding = 8f;

    /// <summary>The kind currently selected for placement, or null if the
    /// menu isn't in "place a building" mode. <see cref="BuildGhostCursor"/>
    /// reads this every frame; set back to null once placement completes
    /// or is cancelled.</summary>
    public BuildingKind? SelectedKind { get; private set; }

    /// <summary>True while the mouse is over this panel's own rect --
    /// same "OnGUI's event queue and the New Input System's Mouse.current
    /// are two separate, non-communicating input paths" guard Minimap's
    /// own <c>PointerOver</c> flag exists for, so a menu-row click doesn't
    /// ALSO land as a world-space build-placement click underneath it.</summary>
    public bool PointerOverPanel { get; private set; }

    public void Init(SimBridge simBridge, int playerIndex)
    {
        bridge = simBridge;
        localPlayerIndex = playerIndex;
    }

    /// <summary>Clears the current placement selection -- called by
    /// <see cref="BuildGhostCursor"/> once a placement is confirmed or
    /// cancelled, so the menu's own highlight stays in sync.</summary>
    public void ClearSelection() => SelectedKind = null;

    /// <summary>True if every cost line in <paramref name="def"/> is
    /// currently affordable for <paramref name="playerIndex"/>. Pure
    /// enough to unit-test without a live match: given wallet lookups,
    /// no UnityEngine/SimBridge dependency of its own.</summary>
    public static bool CanAfford(BuildingDef def, System.Func<ResourceKind, int> walletOf)
    {
        foreach (var (resource, amount) in def.Cost)
            if (walletOf(resource) < amount) return false;
        return true;
    }

    private void Update()
    {
        if (bridge == null || !bridge.HasMatch) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        var defs = BuildingDef.AllDefs;
        var hotkey = 1;
        for (var i = 0; i < defs.Count; i++)
        {
            if (defs[i].Kind == BuildingKind.Hq) continue;
            if (hotkey <= 9 && DigitKeyPressed(keyboard, hotkey)) ToggleSelect(defs[i].Kind);
            hotkey++;
        }
    }

    private static bool DigitKeyPressed(UnityEngine.InputSystem.Keyboard keyboard, int digit)
    {
        switch (digit)
        {
            case 1: return keyboard.digit1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame;
            case 4: return keyboard.digit4Key.wasPressedThisFrame;
            case 5: return keyboard.digit5Key.wasPressedThisFrame;
            case 6: return keyboard.digit6Key.wasPressedThisFrame;
            case 7: return keyboard.digit7Key.wasPressedThisFrame;
            case 8: return keyboard.digit8Key.wasPressedThisFrame;
            default: return keyboard.digit9Key.wasPressedThisFrame;
        }
    }

    private void ToggleSelect(BuildingKind kind)
    {
        SelectedKind = SelectedKind == kind ? (BuildingKind?)null : kind;
    }

    private void OnGUI()
    {
        if (bridge == null || !bridge.HasMatch) { PointerOverPanel = false; return; }

        var defs = BuildingDef.AllDefs;
        var buildableCount = 0;
        for (var i = 0; i < defs.Count; i++) if (defs[i].Kind != BuildingKind.Hq) buildableCount++;

        var height = Padding * 2f + RowHeight * (buildableCount + 1);   // +1 for the title row
        var rect = new Rect(topLeftPixels.x, topLeftPixels.y, rowWidth, height);
        var e = Event.current;
        PointerOverPanel = e != null && rect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = rect.y + Padding;
        var x = rect.x + Padding;
        var innerWidth = rect.width - Padding * 2f;

        DrawShadowedLabel(new Rect(x, y, innerWidth, RowHeight), "Build (1-9, click to select, Esc cancels)", Color.white);
        y += RowHeight;

        var hotkey = 1;
        for (var i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def.Kind == BuildingKind.Hq) continue;

            var affordable = CanAfford(def, resource => bridge.PlayerWallet(localPlayerIndex, resource));
            var selected = SelectedKind == def.Kind;
            var rowRect = new Rect(x, y, innerWidth, RowHeight);

            if (selected)
            {
                GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.25f);
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            var label = hotkey + ") " + def.Name + " — " + CostLabel(def);
            var color = !affordable ? new Color(0.6f, 0.6f, 0.6f, 1f) : selected ? new Color(0.95f, 0.85f, 0.4f, 1f) : Color.white;
            DrawShadowedLabel(rowRect, label, color);

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none)) ToggleSelect(def.Kind);

            y += RowHeight;
            hotkey++;
        }
    }

    private static string CostLabel(BuildingDef def)
    {
        var parts = new System.Text.StringBuilder();
        for (var i = 0; i < def.Cost.Count; i++)
        {
            if (i > 0) parts.Append(", ");
            parts.Append(def.Cost[i].Amount).Append(' ').Append(def.Cost[i].Resource);
        }
        return parts.Length > 0 ? parts.ToString() : "free";
    }

    private static void DrawShadowedLabel(Rect rect, string text, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
    }
}
