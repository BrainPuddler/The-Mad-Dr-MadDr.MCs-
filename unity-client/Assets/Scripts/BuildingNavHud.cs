using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Creator direction, verbatim: "Like starcraft 2 I need icons that quick
/// navigate to my building. When you click on the building icon it will
/// hilite icon quick scroll to the building. then on the hilited icon the
/// &lt;&gt; keys will jump to the next building of that type if there are
/// more than one. or use arrow icon below the building to do the same.
/// All building that have been built will show up in small / medium
/// icons on the bottom of the screen."
///
/// One icon per COMPLETE (docs/23 §2 -- UnderConstruction/Destroyed
/// buildings don't count as "have been built" yet/anymore) building this
/// player owns, bottom-center of the screen. Icons are sorted by
/// (Kind, EntityId) so same-kind icons sit physically adjacent -- makes
/// "jump to the next of this type" read as "the next icon over," not a
/// jump to a random spot in the row. Clicking an icon highlights it and
/// glides the camera to that building via the SAME <see
/// cref="SimpleCameraRig.FocusOn"/> the existing G-key jump-to-nearest-
/// unit uses (<see cref="WaypointCommander.JumpToNearestUnit"/>'s own
/// precedent) -- "quick scroll," not an instant cut. While an icon is
/// highlighted, the &lt;/&gt; keys (unshifted comma/period -- SC2-style
/// paging shouldn't need a held Shift) or the small arrow buttons drawn
/// below the highlighted icon cycle the highlight+camera among every
/// OTHER building of that SAME <see cref="BuildingKind"/>, wrapping both
/// directions; a no-op (arrows simply don't draw) when there's nothing
/// else of that kind to jump to.
///
/// IMGUI, same layer as every other HUD element in this project. New
/// Input System API exclusively for the keyboard paging (see
/// WaypointCommander's own header for why the legacy Input class is
/// off-limits in this project).
///
/// A no-op (renders nothing) until <see cref="Init"/> is called with a
/// live <see cref="SimBridge"/> that <see cref="SimBridge.HasMatch"/> --
/// same discipline every other SimBridge-reading HUD element in this
/// project already follows.
///
/// 2026-08 (creator direction: "Update the ui to reflect each theme,
/// faction style... All user Building must have thumbnail icons"): icons
/// used to be a flat per-kind color plus a 2-3 letter text abbreviation
/// -- this project's own doc comments were explicit that there was no
/// real icon sprite art anywhere. <see cref="DrawIcon"/> now draws a
/// real procedurally-baked silhouette pictogram per kind (<see
/// cref="BuildingIconKit"/>) tinted with the local player's own faction
/// accent (<see cref="BuildingFactionSkin"/>) instead -- shape carries
/// kind, color carries faction, the same split <see cref="BuildMenuHud"/>'s
/// own command-card tiles now use.
/// </summary>
public class BuildingNavHud : MonoBehaviour
{
    [Header("Data source")]
    public SimBridge bridge;
    public RuntimeCityBuilder builder;
    public int localPlayerIndex = 0;

    [Header("Placement (small/medium icons, per the creator's own wording)")]
    public float iconSize = 44f;
    public float iconGap = 6f;
    public float bottomMarginPixels = 14f;
    public float arrowSize = 16f;

    /// <summary>Same "OnGUI's event queue and the New Input System's
    /// Mouse.current are two separate, non-communicating input paths"
    /// guard <see cref="Minimap.PointerOver"/>/<see cref="BuildMenuHud.
    /// PointerOverPanel"/> already exist for -- <see cref="WaypointCommander"/>
    /// and <see cref="BuildGhostCursor"/> both check this so a nav-bar
    /// icon click doesn't ALSO land as a world-space select/order/
    /// placement click underneath it.</summary>
    public static bool PointerOver { get; private set; }

    /// <summary>The entity ID of the currently highlighted building, or
    /// null if nothing has been clicked yet this match.</summary>
    public uint? HighlightedEntityId { get; private set; }

    public void Init(SimBridge simBridge, RuntimeCityBuilder cityBuilder, int playerIndex)
    {
        bridge = simBridge;
        builder = cityBuilder;
        localPlayerIndex = playerIndex;
    }

    /// <summary>This player's own COMPLETE buildings, sorted by (Kind,
    /// EntityId) -- the one shared source both <see cref="OnGUI"/> (draws
    /// the row) and <see cref="CycleSameKind"/> (pages within it) read,
    /// so the two can never disagree about ordering.</summary>
    private List<SimBuilding> GatherMine()
    {
        var mine = new List<SimBuilding>();
        if (bridge == null || !bridge.HasMatch) return mine;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.PlayerIndex == localPlayerIndex && b.State == BuildingState.Complete) mine.Add(b);
        }
        mine.Sort((a, c) => a.Kind != c.Kind ? ((int)a.Kind).CompareTo((int)c.Kind) : a.EntityId.CompareTo(c.EntityId));
        return mine;
    }

    private void Update()
    {
        if (bridge == null || !bridge.HasMatch || HighlightedEntityId == null) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // "<" ">" are the UNSHIFTED comma/period keys -- paging through a
        // command roster shouldn't need a held Shift.
        if (keyboard.commaKey.wasPressedThisFrame) CycleSameKind(-1);
        if (keyboard.periodKey.wasPressedThisFrame) CycleSameKind(1);
    }

    private void SelectAndFocus(SimBuilding b)
    {
        HighlightedEntityId = b.EntityId;

        var cam = Camera.main;
        if (cam == null || builder == null) return;
        var rig = cam.GetComponent<SimpleCameraRig>();
        if (rig == null) return;

        var hexWorld = builder.WorldOf(b.Hex);
        var groundY = builder.GroundHeightAt(hexWorld);
        rig.FocusOn(new Vector3(hexWorld.x, groundY, hexWorld.z));
    }

    /// <summary>Move the highlight (and camera) to the next/previous
    /// building sharing the CURRENTLY highlighted building's own Kind,
    /// wrapping in both directions. Silent no-op if the highlighted
    /// building no longer exists (destroyed mid-match) or nothing else of
    /// its kind is left -- same bad-input-is-a-no-op discipline
    /// match-core's own commands follow.</summary>
    private void CycleSameKind(int direction)
    {
        if (HighlightedEntityId == null) return;
        var mine = GatherMine();
        var currentIndex = mine.FindIndex(b => b.EntityId == HighlightedEntityId.Value);
        if (currentIndex < 0) return;

        var kind = mine[currentIndex].Kind;
        var sameKind = mine.FindAll(b => b.Kind == kind);
        if (sameKind.Count <= 1) return;

        var idxWithinKind = sameKind.FindIndex(b => b.EntityId == HighlightedEntityId.Value);
        var nextIdx = ((idxWithinKind + direction) % sameKind.Count + sameKind.Count) % sameKind.Count;
        SelectAndFocus(sameKind[nextIdx]);
    }

    private void OnGUI()
    {
        if (bridge == null || !bridge.HasMatch || builder == null) { PointerOver = false; return; }

        var mine = GatherMine();
        if (mine.Count == 0) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;

        // 2026-08 (creator report: "buildings icons... are still off the
        // screen"): a large base could ask for more row width than the
        // screen has -- iconSize/iconGap were fixed Inspector constants
        // with no cap on `mine.Count`, so `totalWidth` (and therefore
        // `startX`) could go negative, pushing the leftmost icons off
        // BOTH edges regardless of UiScale's own fix (which only
        // corrects RESOLUTION scaling, not a row that's simply asking
        // for more space than any screen has). Shrink icon size/gap
        // uniformly, proportionally, whenever the row would overflow a
        // safe usable width -- same "scale to fit, never overflow"
        // philosophy UiScale itself already applies one level up, just
        // local to this one row instead of the whole screen. Below the
        // shrink threshold (the common case, a handful of buildings)
        // this is a no-op: effIconSize/effIconGap equal the Inspector
        // values exactly, zero behavior change.
        var usableWidth = screenW - RowSideMargin * 2f;
        var rawWidth = mine.Count * iconSize + (mine.Count - 1) * iconGap;
        var shrink = rawWidth > usableWidth && rawWidth > 0f ? usableWidth / rawWidth : 1f;
        var effIconSize = iconSize * shrink;
        var effIconGap = iconGap * shrink;

        var totalWidth = mine.Count * effIconSize + (mine.Count - 1) * effIconGap;
        var startX = (screenW - totalWidth) * 0.5f;
        var y = UiScale.Height - bottomMarginPixels - effIconSize;

        var barRect = new Rect(startX - effIconGap, y - effIconGap - arrowSize - 18f,
            totalWidth + effIconGap * 2f, effIconSize + effIconGap * 2f + arrowSize + 18f);
        var e = Event.current;
        PointerOver = e != null && barRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        SimBuilding highlighted = null;
        var x = startX;
        for (var i = 0; i < mine.Count; i++)
        {
            var b = mine[i];
            var rect = new Rect(x, y, effIconSize, effIconSize);
            var isHighlighted = HighlightedEntityId == b.EntityId;
            if (isHighlighted) highlighted = b;

            DrawIcon(rect, b, isHighlighted);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) SelectAndFocus(b);

            x += effIconSize + effIconGap;
        }

        if (highlighted != null) DrawHighlightDetail(highlighted, mine, startX, y, effIconSize, effIconGap);

        UiScale.End(prevMatrix);
    }

    // side margin kept clear of the screen edge even at max shrink.
    private const float RowSideMargin = 24f;

    /// <summary>Above the row: the highlighted building's real name and
    /// its position within its own kind ("Factory 2/3") -- confirms at a
    /// glance that paging is moving through the right group. Below the
    /// row: the two cycle arrows, only drawn when there's actually
    /// something else of this kind to jump to (an arrow that does nothing
    /// reads as broken, not as "disabled").</summary>
    private void DrawHighlightDetail(SimBuilding highlighted, List<SimBuilding> mine, float rowStartX, float rowY, float effIconSize, float effIconGap)
    {
        var sameKind = mine.FindAll(b => b.Kind == highlighted.Kind);
        var indexWithinKind = sameKind.FindIndex(b => b.EntityId == highlighted.EntityId);
        // 2026-08 ("reflect each theme, faction style"): the local
        // player's own faction-skinned name (BuildingFactionSkin), not
        // the sim's generic BuildingDef.Name -- the same name this
        // building shows in BuildMenuHud's own info line.
        var name = BuildingFactionSkin.NameFor(highlighted.Kind, bridge.PlayerFaction(localPlayerIndex));

        var label = sameKind.Count > 1
            ? name + " " + (indexWithinKind + 1) + "/" + sameKind.Count
            : name;
        DrawShadowedLabel(new Rect(rowStartX, rowY - 18f, effIconSize * mine.Count + effIconGap * (mine.Count - 1), 16f), label, new Color(0.95f, 0.9f, 0.7f, 1f), TextAnchor.MiddleCenter);

        if (sameKind.Count <= 1) return;

        var highlightIndex = mine.FindIndex(b => b.EntityId == highlighted.EntityId);
        var iconCenterX = rowStartX + highlightIndex * (effIconSize + effIconGap) + effIconSize * 0.5f;
        var arrowY = rowY + effIconSize + 4f;

        var prevRect = new Rect(iconCenterX - arrowSize - 2f, arrowY, arrowSize, arrowSize);
        var nextRect = new Rect(iconCenterX + 2f, arrowY, arrowSize, arrowSize);

        // plain ASCII, not Unicode triangle glyphs -- guaranteed to render
        // in any GUISkin font (no way to verify glyph coverage without a
        // real Editor), and it visually echoes the same "<"/">" keys this
        // whole feature is built around.
        if (GUI.Button(prevRect, "<")) CycleSameKind(-1);
        if (GUI.Button(nextRect, ">")) CycleSameKind(1);
    }

    /// <summary>2026-08 (creator direction: "Update the ui to reflect
    /// each theme, faction style... All user Building must have
    /// thumbnail icons"): replaced the old per-kind flat color + text
    /// abbreviation with the local player's own faction accent (<see
    /// cref="BuildingFactionSkin.AccentColorFor"/>) plus a real
    /// silhouette pictogram (<see cref="BuildingIconKit.IconFor"/>) --
    /// same "shape=kind, color=faction" split <see cref="BuildMenuHud"/>'s
    /// own command-card tiles now use, so the SAME building reads as the
    /// same icon/color in both places.</summary>
    private void DrawIcon(Rect rect, SimBuilding b, bool highlighted)
    {
        GUI.color = highlighted ? new Color(0.9f, 0.75f, 0.2f, 1f) : new Color(0.15f, 0.15f, 0.18f, 0.95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        var inset = highlighted ? 4f : 2f;
        var innerRect = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        var faction = bridge.PlayerFaction(localPlayerIndex);
        GUI.color = BuildingFactionSkin.AccentColorFor(faction);
        GUI.DrawTexture(innerRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var iconInset = innerRect.width * 0.14f;
        var iconRect = new Rect(innerRect.x + iconInset, innerRect.y + iconInset, innerRect.width - iconInset * 2f, innerRect.height - iconInset * 2f);
        GUI.color = new Color(0.92f, 0.88f, 0.78f, 1f);
        GUI.DrawTexture(iconRect, BuildingIconKit.IconFor(b.Kind));
        GUI.color = Color.white;

        if (b.IsDamaged)
        {
            GUI.color = new Color(0.9f, 0.15f, 0.15f, 0.45f);
            GUI.DrawTexture(innerRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
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
