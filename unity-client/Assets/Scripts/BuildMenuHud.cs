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
/// hotkey-numbered TEXT list. Full name + cost, which used to live
/// inline on every row, now shows in a single info line below the grid
/// for whichever icon the mouse is CURRENTLY hovering (SC2's own command
/// card has the same "hover for detail" shape) -- clicking or hovering
/// still grays out anything currently unaffordable.
///
/// 2026-08 follow-up (creator direction: "Update the ui to reflect each
/// theme, faction style... All user Building must have thumbnail
/// icons"): a tile's "icon" used to be a flat per-kind color plus a 2-3
/// letter text abbreviation -- this project had no real icon sprite art
/// anywhere. Each tile now draws a real procedurally-baked silhouette
/// pictogram per kind (<see cref="BuildingIconKit"/>) tinted with the
/// local player's own faction accent (<see cref="BuildingFactionSkin"/>)
/// instead -- shape carries kind, color carries faction, and the info
/// line's own building name is now faction-skinned too ("Blood Bank" /
/// "Plasma Reserve" / "Ichor Cistern" for the same generic
/// BuildingKind.BloodStorage, depending who's playing).
///
/// Clicking a tile -- or its digit hotkey -- hands the kind to
/// <see cref="BuildGhostCursor"/> to start a placement preview; this
/// script owns selection state, not placement itself, mirroring how
/// <see cref="WaypointCommander"/> owns unit selection separately from
/// order issuing.
///
/// IMGUI, same layer as every other HUD element in this project (see
/// HudStatus's header for why). Top-left-BELOW-HudStatus by default --
/// HudStatus's own stacked status lines already own the very top-left
/// corner, so this panel starts far enough down to clear them, rather
/// than fighting over the same corner presets <see cref="LumenHud"/>/
/// Minimap/AnalogClockHud use.
///
/// 2026-08 (creator report: "instructions and icons in the top left ...
/// overlapping"): `topLeftPixels.y` used to be a fixed guess (140) that
/// only cleared HudStatus's SHORTEST possible state (nothing selected,
/// no traffic yet) -- HudStatus's own line count grows with traffic
/// present and with a single unit selected (3 lines instead of 1), and
/// at its tallest its text runs well past 140, straight into this
/// panel's title bar. Reads <see cref="WindowLightsHud.Bottom"/> (which
/// itself chains off <see cref="HudStatus.ContentBottom"/> -- 2026-08
/// follow-up, creator report "collector lab is hidden under a lot of
/// text": WindowLightsHud always sits between HudStatus and this panel,
/// so chaining off ITS bottom instead of HudStatus's directly is what
/// actually stays clear of both, not just the one this panel used to
/// check) and only falls back to `topLeftPixels.y` as a floor, so it
/// tracks the real stack height instead of guessing at any one panel's
/// tallest case up front. Publishes its own <see cref="Bottom"/> in
/// turn, for <see cref="CollectorLabHud"/> to chain off next.
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
    private const float HudStatusGap = 12f;

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

    /// <summary>This panel's own bottom edge, published for <see
    /// cref="CollectorLabHud"/> to stack below -- same chained-anchor
    /// idiom as <see cref="HudStatus.ContentBottom"/>/<see
    /// cref="WindowLightsHud.Bottom"/>, so the top-left column never
    /// relies on two panels independently guessing the same offset.
    /// Stale (last frame's value) whenever this panel didn't render --
    /// harmless, since every consumer shares this panel's own
    /// <c>bridge.HasMatch</c> gate.</summary>
    public static float Bottom { get; private set; }

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
        var prevMatrix = UiScale.Begin();

        var defs = BuildingDef.AllDefs;
        var buildableCount = 0;
        for (var i = 0; i < defs.Count; i++) if (defs[i].Kind != BuildingKind.Hq) buildableCount++;

        // 2026-08 ("Update the ui to reflect each theme, faction style"):
        // the local player's own faction drives both the tile accent
        // color and which display name shows in the title/info line --
        // see BuildingFactionSkin's own header for why this skin never
        // existed on the Unity side before despite BuildingDef.cs's own
        // comment naming the intended design directly.
        var faction = bridge.PlayerFaction(localPlayerIndex);

        var rowsNeeded = Mathf.CeilToInt(buildableCount / (float)columns);
        var gridWidth = columns * tileSize + (columns - 1) * tileGap;
        var gridHeight = rowsNeeded * tileSize + (rowsNeeded - 1) * tileGap;
        var panelWidth = gridWidth + Padding * 2f;

        // 2026-08 creator direction ("Resource requirements for the
        // building. Text is not clipped."): the info line used to be a
        // fixed `InfoHeight` (20px) no matter how long the name+cost
        // string was -- "Factory — 30 Bones, 15 Blood, 50 Parts" at the
        // grid's own ~176px width (3 columns) genuinely doesn't fit one
        // 20px line, and GUI.Label doesn't wrap by default, so the tail
        // of a long cost line was cropped by/spilling past the panel's
        // own backing box. Fixed by reserving the WORST-CASE wrapped
        // height across every real def up front (not just whichever tile
        // happens to be hovered right now) -- the panel's own height
        // never jumps around as the mouse moves between tiles, and
        // there's ALWAYS room for the longest actual info line, not just
        // today's. Sized against the CURRENT faction's own skinned names
        // (not the generic BuildingDef.Name) since "Ichor Cistern" and
        // "Blood Storage" aren't the same length -- sizing against the
        // wrong name table could silently reintroduce the exact clipping
        // this fix exists to prevent.
        var infoHeight = WorstCaseInfoHeight(gridWidth, faction);
        var panelHeight = Padding * 2f + TitleHeight + gridHeight + infoHeight;

        var topY = Mathf.Max(topLeftPixels.y, WindowLightsHud.Bottom + HudStatusGap);
        var rect = new Rect(topLeftPixels.x, topY, panelWidth, panelHeight);
        var e = Event.current;
        var mousePos = e != null ? e.mousePosition : new Vector2(-1f, -1f);
        PointerOverPanel = rect.Contains(mousePos);
        Bottom = rect.yMax;

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

            DrawTile(tileRect, def, faction, hotkey, affordable, selected);
            if (GUI.Button(tileRect, GUIContent.none, GUIStyle.none)) ToggleSelect(def.Kind);

            hotkey++;
            col++;
            if (col >= columns) { col = 0; row++; }
        }

        var infoRect = new Rect(x, gridTop + gridHeight + 4f, gridWidth, infoHeight);
        if (hovered != null)
        {
            var infoAffordable = CanAfford(hovered, resource => bridge.PlayerWallet(localPlayerIndex, resource));
            var infoColor = infoAffordable ? new Color(0.85f, 0.85f, 0.85f, 1f) : new Color(0.9f, 0.4f, 0.35f, 1f);
            DrawWrappedShadowedLabel(infoRect, BuildingFactionSkin.NameFor(hovered.Kind, faction) + " — " + CostLabel(hovered), infoColor);
        }

        UiScale.End(prevMatrix);
    }

    /// <summary>Tallest wrapped height (in reference-space pixels, at the
    /// grid's own width) any real def's own "Name — cost" info line could
    /// ever need -- computed fresh each call (defs are static, so this is
    /// cheap and stays correct if a def's name/cost ever changes) rather
    /// than cached, since it's only ONE `CalcHeight` per def per frame,
    /// not per-pixel work. Floored at `InfoHeight` so a short line still
    /// gets its old minimum footprint.</summary>
    private static float WorstCaseInfoHeight(float width, FactionId faction)
    {
        var defs = BuildingDef.AllDefs;
        var style = GUI.skin.label;
        var prevWrap = style.wordWrap;
        style.wordWrap = true;
        var tallest = InfoHeight;
        for (var i = 0; i < defs.Count; i++)
        {
            if (defs[i].Kind == BuildingKind.Hq) continue;
            var text = BuildingFactionSkin.NameFor(defs[i].Kind, faction) + " — " + CostLabel(defs[i]);
            var h = style.CalcHeight(new GUIContent(text), width);
            if (h > tallest) tallest = h;
        }
        style.wordWrap = prevWrap;
        return tallest;
    }

    /// <summary>One command-card tile. 2026-08 (creator direction:
    /// "Update the ui to reflect each theme, faction style... Including
    /// All user Building must have thumbnail icons"): the swatch is now
    /// the LOCAL PLAYER'S OWN faction accent (<see
    /// cref="BuildingFactionSkin.AccentColorFor"/>) rather than a flat
    /// per-kind color that ignored which faction was playing, and a real
    /// silhouette pictogram (<see cref="BuildingIconKit.IconFor"/>) draws
    /// on top instead of the old 2-3 letter text abbreviation -- shape
    /// now carries KIND, color carries FACTION, the same split this
    /// project's own doctrine already applies everywhere else (see the
    /// Minimap legibility pass, docs/12, for the same "shape=kind,
    /// color=state" fix applied there). Hotkey digit still badged top-
    /// left (SC2's own corner-number convention), grayed when
    /// unaffordable, bright border when this is the currently selected
    /// placement kind.</summary>
    private static void DrawTile(Rect rect, BuildingDef def, FactionId faction, int hotkey, bool affordable, bool selected)
    {
        var swatch = BuildingFactionSkin.AccentColorFor(faction);
        if (!affordable) swatch = Color.Lerp(swatch, new Color(0.2f, 0.2f, 0.2f), 0.6f);

        GUI.color = swatch;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // thumbnail icon, inset from the tile edge, tinted ivory (the
        // same high-contrast light-face-on-dark idiom AnalogClockHud's
        // own face already established) so the silhouette reads clearly
        // against any of the three faction accent colors.
        var inset = rect.width * 0.14f;
        var iconRect = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        GUI.color = affordable ? new Color(0.92f, 0.88f, 0.78f, 1f) : new Color(0.62f, 0.6f, 0.56f, 1f);
        GUI.DrawTexture(iconRect, BuildingIconKit.IconFor(def.Kind));
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

    /// <summary>Same drop-shadow trick as <see cref="DrawShadowedLabel"/>,
    /// but with word-wrap ON -- used only for the info line's own "Name —
    /// cost" text, which `rect`'s height (from <see
    /// cref="WorstCaseInfoHeight"/>) is ALREADY sized to fit wrapped, so
    /// this never needs to truncate/clip regardless of how long a given
    /// building's cost list is.</summary>
    private static void DrawWrappedShadowedLabel(Rect rect, string text, Color color)
    {
        var prevAnchor = GUI.skin.label.alignment;
        var prevWrap = GUI.skin.label.wordWrap;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.wordWrap = true;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = color;
        GUI.Label(rect, text);
        GUI.color = Color.white;
        GUI.skin.label.alignment = prevAnchor;
        GUI.skin.label.wordWrap = prevWrap;
    }
}
