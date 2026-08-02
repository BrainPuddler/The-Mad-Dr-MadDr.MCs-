using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// docs/23 §2 Phase 2's "build-menu IMGUI panel (HudStatus conventions)"
/// -- the buildable roster (<see cref="BuildingDef.AllDefs"/>, everything
/// except <see cref="BuildingKind.Hq"/>, which is generator-placed, never
/// player-built).
///
/// 2026-08 (creator direction: "let's use menu icon system for which
/// building to build. Like in StarCraft"): rebuilt as a StarCraft-style
/// command-card grid -- fixed-size square icons in rows/columns, each with
/// its hotkey digit badged in the corner, instead of the original
/// hotkey-numbered TEXT list. This project has no real icon sprite art
/// anywhere, so each "icon" is the same colored-swatch-plus-abbreviation
/// idiom <see cref="SelectionHud"/>/<see cref="BuildingNavHud"/> already
/// established -- reusing <see cref="BuildingNavHud.IconColorFor"/>/
/// <see cref="BuildingNavHud.IconAbbrevFor"/> directly (made public for
/// this) rather than inventing a second, possibly-drifting color mapping
/// for the same building kinds. Full name + cost, which used to live
/// inline on every row, now shows in a single info line below the grid
/// for whichever icon the mouse is CURRENTLY hovering (SC2's own command
/// card has the same "hover for detail" shape) -- clicking or hovering
/// still grays out anything currently unaffordable.
///
/// Clicking a tile -- or its digit hotkey -- hands the kind to
/// <see cref="BuildGhostCursor"/> to start a placement preview; this
/// script owns selection state, not placement itself, mirroring how
/// <see cref="WaypointCommander"/> owns unit selection separately from
/// order issuing.
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
    public int columns = 3;
    public float tileSize = 56f;
    public float tileGap = 4f;

    private const float Padding = 8f;
    private const float TitleHeight = 20f;
    private const float InfoHeight = 20f;

    /// <summary>The kind currently selected for placement, or null if the
    /// menu isn't in "place a building" mode. <see cref="BuildGhostCursor"/>
    /// reads this every frame; set back to null once placement completes
    /// or is cancelled.</summary>
    public BuildingKind? SelectedKind { get; private set; }

    /// <summary>True while the mouse is over this panel's own rect --
    /// same "OnGUI's event queue and the New Input System's Mouse.current
    /// are two separate, non-communicating input paths" guard Minimap's
    /// own <c>PointerOver</c> flag exists for, so a menu-tile click doesn't
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

        // 2026-08 (creator report: "you need to disable the build orders
        // when the control key is pressed"): plain digit1Key-digit9Key
        // were already claimed here unconditionally BEFORE
        // WaypointCommander's own Ctrl+[0-9]/Alt+[0-9] battalion
        // assign/select hotkeys were added on top of the same keys (a
        // deliberate choice at the time -- plain digits were already
        // taken, so battalion binding had to use a modifier instead of
        // fighting over the same keys). That left a real conflict this
        // class's own Update() never accounted for: holding Ctrl or Alt
        // to hit a battalion slot ALSO fired a build-menu toggle on the
        // SAME keypress, since this check never looked at modifier state
        // at all. Bail out entirely while either is held -- both
        // combinations belong to the battalion system now, not this one.
        var ctrlOrAltHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
            || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        if (ctrlOrAltHeld) return;

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

        var rowsNeeded = Mathf.CeilToInt(buildableCount / (float)columns);
        var gridWidth = columns * tileSize + (columns - 1) * tileGap;
        var gridHeight = rowsNeeded * tileSize + (rowsNeeded - 1) * tileGap;
        var panelWidth = gridWidth + Padding * 2f;
        var panelHeight = Padding * 2f + TitleHeight + gridHeight + InfoHeight;

        var rect = new Rect(topLeftPixels.x, topLeftPixels.y, panelWidth, panelHeight);
        var e = Event.current;
        var mousePos = e != null ? e.mousePosition : new Vector2(-1f, -1f);
        PointerOverPanel = rect.Contains(mousePos);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = rect.x + Padding;
        var y = rect.y + Padding;
        DrawShadowedLabel(new Rect(x, y, gridWidth, TitleHeight), "Build (1-9, click, Esc cancels)", Color.white, TextAnchor.MiddleLeft);
        y += TitleHeight;
        var gridTop = y;

        BuildingDef hovered = null;
        var hotkey = 1;
        var col = 0;
        var row = 0;
        for (var i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def.Kind == BuildingKind.Hq) continue;

            var tileRect = new Rect(x + col * (tileSize + tileGap), gridTop + row * (tileSize + tileGap), tileSize, tileSize);
            var affordable = CanAfford(def, resource => bridge.PlayerWallet(localPlayerIndex, resource));
            var selected = SelectedKind == def.Kind;
            if (tileRect.Contains(mousePos)) hovered = def;

            DrawTile(tileRect, def, hotkey, affordable, selected);
            if (GUI.Button(tileRect, GUIContent.none, GUIStyle.none)) ToggleSelect(def.Kind);

            hotkey++;
            col++;
            if (col >= columns) { col = 0; row++; }
        }

        var infoRect = new Rect(x, gridTop + gridHeight + 4f, gridWidth, InfoHeight);
        if (hovered != null)
        {
            var infoAffordable = CanAfford(hovered, resource => bridge.PlayerWallet(localPlayerIndex, resource));
            var infoColor = infoAffordable ? new Color(0.85f, 0.85f, 0.85f, 1f) : new Color(0.9f, 0.4f, 0.35f, 1f);
            DrawShadowedLabel(infoRect, hovered.Name + " — " + CostLabel(hovered), infoColor, TextAnchor.MiddleLeft);
        }
    }

    /// <summary>One command-card tile: a colored swatch (<see
    /// cref="BuildingNavHud.IconColorFor"/>) with its abbreviation (<see
    /// cref="BuildingNavHud.IconAbbrevFor"/>) centered, the hotkey digit
    /// badged top-left (SC2's own corner-number convention), grayed when
    /// unaffordable, and a bright border when this is the currently
    /// selected placement kind.</summary>
    private static void DrawTile(Rect rect, BuildingDef def, int hotkey, bool affordable, bool selected)
    {
        var swatch = BuildingNavHud.IconColorFor(def.Kind);
        if (!affordable) swatch = Color.Lerp(swatch, new Color(0.2f, 0.2f, 0.2f), 0.6f);

        GUI.color = swatch;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (selected)
        {
            const float t = 2f;
            GUI.color = new Color(0.95f, 0.85f, 0.4f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        var labelColor = affordable ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
        DrawShadowedLabel(rect, BuildingNavHud.IconAbbrevFor(def.Kind), labelColor, TextAnchor.MiddleCenter);
        DrawShadowedLabel(new Rect(rect.x + 3f, rect.y + 1f, rect.width, 14f), hotkey.ToString(), new Color(0.95f, 0.9f, 0.7f, 1f), TextAnchor.UpperLeft);
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

    private static void DrawShadowedLabel(Rect rect, string text, Color color, TextAnchor anchor)
    {
        var prevAnchor = GUI.skin.label.alignment;
        GUI.skin.label.alignment = anchor;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
        GUI.skin.label.alignment = prevAnchor;
    }
}
