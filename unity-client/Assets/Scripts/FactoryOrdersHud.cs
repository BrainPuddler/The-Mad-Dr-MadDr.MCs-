using System.Collections.Generic;
using MadDr.MatchCore;
using UnityEngine;

/// <summary>2026-08 follow-up (creator report, verbatim: "the clipboard
/// interface isn't working disable it. Replace it with If the user press
/// the C key near the factory a order sheet will open with slots for
/// build orders, the current monster being built is in the current slot
/// and new monster or battalion can be dropped in a slot. the user can
/// increase or decrease or delete the order or the quantity"; follow-up:
/// "very much like starcraft 2"):
///
/// Opened/closed/switched by <see cref="Toggle"/>, called directly from
/// <see cref="GrabCursor"/>'s own C-key handler (no more event
/// subscription -- see <see cref="GrabCursor.orderSheetHud"/>'s own doc
/// comment for why the relationship is now a direct, bidirectional
/// reference instead). A horizontal row of StarCraft-2-style portrait
/// slots -- reusing <see cref="GrabCursor.ProductionQueueFor"/> directly
/// (the SAME Label/Remaining/Progress/PortraitPng data <see
/// cref="ProductionQueueHud"/>'s own always-visible tile row already
/// shows) rather than a separate, poorer text-row shape. Slot 0 is "the
/// current slot" (the active build, with its own live progress bar);
/// every other filled slot is the queue, in build order; one extra empty
/// "+" slot past the last filled one is always drawn as a direct drop
/// target for a brand-new order.
///
/// 2026-08 follow-up (creator report, verbatim: "Cued Monster are still
/// NOT completing I think because any monster dropped on top of roof,
/// becomes the current build and therefore stop the current one. Builds
/// must be completed in order not pushed onto the stack but put at the
/// bottom"): dropping into ANY slot, including slot 0, now always
/// appends at the bottom of the queue (<see cref="GrabCursor.DropIntoSlot"/>)
/// -- it never interrupts or resets whatever is already building. Slot 0
/// still shows the active build (that's just whatever the queue's own
/// front happens to be), it's just no longer a special drop target.
///
/// Docked directly ABOVE <see cref="ProductionQueueHud"/>'s own tile row
/// (reading its live <see cref="ProductionQueueHud.TileRowTop"/>, same
/// "read a neighbour's own dynamic anchor" contract this panel already
/// used before this pivot). Per-slot controls (^/v reorder, -/+ quantity,
/// X cancel) still call straight into <see cref="GrabCursor"/>'s own
/// queue mutators -- this class owns no queue state of its own, only
/// which Factory is currently open and which slot the cursor is
/// currently over.</summary>
public class FactoryOrdersHud : MonoBehaviour
{
    public GrabCursor grabCursor;
    public ProductionQueueHud productionQueueHud;

    [Header("Placement (docks above ProductionQueueHud's own tile row)")]
    public float tileSize = 52f;
    public float tileGap = 6f;
    public float labelStripHeight = 13f;
    public float progressBarHeight = 4f;
    public float buttonRowHeight = 16f;
    public float headerHeight = 20f;
    public float marginPixels = 12f;
    public float closeButtonSize = 18f;
    public int maxVisibleSlots = 8;

    private SimBuilding _openFactory;

    public static bool PointerOver { get; private set; }

    /// <summary>Which slot column (0 = current build, higher = queue, one
    /// past the last filled slot = the empty "add new order" slot) the
    /// cursor is currently over -- -1 if the sheet is closed or the
    /// cursor isn't over any slot. Recomputed every OnGUI frame; <see
    /// cref="GrabCursor"/> reads it every Update() while Carrying to
    /// decide whether a click drops directly into a slot instead of the
    /// normal 3D world.</summary>
    public int HoveredSlotIndex { get; private set; } = -1;

    /// <summary>The Factory this sheet is currently open for, or null if
    /// closed -- <see cref="GrabCursor"/> reads this every frame to know
    /// whether a slot-drop is even possible right now.</summary>
    public SimBuilding OpenFactory { get { return _openFactory; } }

    public void Init(GrabCursor grabCursorRef, ProductionQueueHud productionQueueHudRef)
    {
        grabCursor = grabCursorRef;
        productionQueueHud = productionQueueHudRef;
    }

    /// <summary>2026-08 ("press the C key near the factory... will
    /// open"): a plain toggle -- C pressed again near the SAME open
    /// Factory closes it, C pressed near a DIFFERENT Factory switches
    /// straight to that one. Called directly by <see
    /// cref="GrabCursor.ToggleOrderSheetNearCursor"/>.</summary>
    public void Toggle(SimBuilding factory)
    {
        _openFactory = _openFactory == factory ? null : factory;
    }

    private void OnGUI()
    {
        if (grabCursor == null || _openFactory == null) { PointerOver = false; HoveredSlotIndex = -1; return; }

        var prevMatrix = UiScale.Begin();

        var items = new List<(string Label, int Remaining, float Progress, string PortraitPng)>(grabCursor.ProductionQueueFor(_openFactory));
        var shown = Mathf.Min(items.Count + 1, maxVisibleSlots);   // +1 = the trailing empty "add new order" slot

        var panelWidth = shown * tileSize + Mathf.Max(0, shown - 1) * tileGap;
        var panelHeight = headerHeight + tileSize + labelStripHeight + progressBarHeight + buttonRowHeight + marginPixels;

        var screenW = UiScale.Width;
        var defaultBottom = UiScale.Height - marginPixels;
        var bottom = productionQueueHud != null ? productionQueueHud.TileRowTop : defaultBottom;
        var panelX = screenW - marginPixels - panelWidth;
        var panelY = bottom - panelHeight;
        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        var e = Event.current;
        var mouse = e != null ? e.mousePosition : new Vector2(-1f, -1f);
        PointerOver = e != null && panelRect.Contains(mouse);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelRect.y;

        GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth - closeButtonSize - 8f, headerHeight),
            grabCursor.FactoryDisplayName(_openFactory) + " ORDERS");
        var closeRect = new Rect(panelRect.xMax - closeButtonSize - 4f, y + 1f, closeButtonSize, closeButtonSize);
        if (GUI.Button(closeRect, "X")) _openFactory = null;
        y += headerHeight;

        var hovered = -1;
        for (var i = 0; i < shown; i++)
        {
            var colX = panelRect.x + i * (tileSize + tileGap);
            var isEmptySlot = i >= items.Count;

            // hit-test the icon+label+progress area (not the button row
            // below it, which has its own real buttons) -- this IS what
            // reads as "the slot" to drop something onto.
            var hitRect = new Rect(colX, y, tileSize, tileSize + labelStripHeight + progressBarHeight);
            if (e != null && hitRect.Contains(mouse)) hovered = i;

            if (isEmptySlot) DrawEmptySlot(colX, y);
            else DrawSlot(colX, y, i, items[i]);
        }
        HoveredSlotIndex = hovered;

        UiScale.End(prevMatrix);
    }

    /// <summary>One filled slot: portrait tile (falling back to a flat
    /// tinted square + text abbreviation, same as <see
    /// cref="ProductionQueueHud"/>'s own tile row, for a genome with no
    /// baked portrait) with a corner quantity badge, the item's full name
    /// underneath, a progress bar (index 0 only -- the active build is
    /// the only one actually being timed), and a StarCraft-2-style button
    /// strip (^/v reorder, -/+ quantity, X cancel) -- all four calling
    /// straight into <see cref="GrabCursor"/>'s own queue mutators, no
    /// duplicated queue logic here.</summary>
    private void DrawSlot(float x, float y, int index, (string Label, int Remaining, float Progress, string PortraitPng) item)
    {
        var tileRect = new Rect(x, y, tileSize, tileSize);
        var portrait = PortraitTexture.For(item.PortraitPng);
        if (portrait != null)
        {
            GUI.DrawTexture(tileRect, portrait, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.color = new Color(0.32f, 0.28f, 0.38f, 1f);
            GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(tileRect, Abbrev(item.Label));
        }

        var badgeRect = new Rect(tileRect.xMax - 18f, tileRect.yMax - 16f, 18f, 16f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(badgeRect, item.Remaining.ToString());

        var labelRect = new Rect(x, tileRect.yMax + 1f, tileSize, labelStripHeight);
        DrawShadowedNameLabel(labelRect, item.Label);

        var barRect = new Rect(x, labelRect.yMax + 1f, tileSize, progressBarHeight);
        if (index == 0)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(item.Progress), barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // plain ASCII glyphs (same reasoning the header's own "X" close
        // button and the claw cursor's design note both give -- unicode
        // arrow/cross glyph coverage in Unity's default IMGUI font can't
        // be confirmed without a real Editor/Player render here).
        var buttonWidth = tileSize / 5f;
        var by = barRect.yMax + 1f;
        var bx = x;
        if (GUI.Button(new Rect(bx, by, buttonWidth, buttonRowHeight), "^")) grabCursor.MoveOrderUp(_openFactory, index);
        bx += buttonWidth;
        if (GUI.Button(new Rect(bx, by, buttonWidth, buttonRowHeight), "v")) grabCursor.MoveOrderDown(_openFactory, index);
        bx += buttonWidth;
        if (GUI.Button(new Rect(bx, by, buttonWidth, buttonRowHeight), "-")) grabCursor.AdjustOrderQuantity(_openFactory, index, false);
        bx += buttonWidth;
        if (GUI.Button(new Rect(bx, by, buttonWidth, buttonRowHeight), "+")) grabCursor.AdjustOrderQuantity(_openFactory, index, true);
        bx += buttonWidth;
        if (GUI.Button(new Rect(bx, by, tileSize - bx + x, buttonRowHeight), "X")) grabCursor.CancelQueueItem(_openFactory, index);
    }

    /// <summary>The trailing "add a new order" slot -- always drawn one
    /// past the last filled slot (creator direction: "new monster or
    /// battalion can be dropped in a slot"), a plain dashed-look outline
    /// with a centered "+" so it reads as an empty target rather than a
    /// broken/missing tile. No buttons -- there's nothing queued here yet
    /// to reorder/adjust/cancel.</summary>
    private void DrawEmptySlot(float x, float y)
    {
        var tileRect = new Rect(x, y, tileSize, tileSize);
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.35f);
        var prevAlignment = GUI.skin.label.alignment;
        var prevFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 22;
        GUI.Label(tileRect, "+");
        GUI.skin.label.alignment = prevAlignment;
        GUI.skin.label.fontSize = prevFontSize;
        GUI.color = Color.white;
    }

    private static string Abbrev(string label)
    {
        if (string.IsNullOrEmpty(label)) return "?";
        return label.Length <= 3 ? label : label.Substring(0, 3);
    }

    private static void DrawShadowedNameLabel(Rect rect, string label)
    {
        var text = string.IsNullOrEmpty(label) ? "?" : label;
        var prevAlignment = GUI.skin.label.alignment;
        var prevFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.alignment = TextAnchor.UpperCenter;
        GUI.skin.label.fontSize = 10;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text);
        GUI.color = new Color(0.92f, 0.9f, 0.85f, 1f);
        GUI.Label(rect, text);
        GUI.color = Color.white;
        GUI.skin.label.alignment = prevAlignment;
        GUI.skin.label.fontSize = prevFontSize;
    }
}
