using UnityEngine;

/// <summary>2026-08 (creator direction: "Implement and integrate a
/// Factory Build Queue / Order Clipboard system... When the player
/// clicks/taps the Factory clipboard, open an Order Queue popup anchored
/// toward the bottom-right of the screen... Follow the game's existing
/// UI conventions rather than introducing a completely different UI
/// style"): the SAME IMGUI dark-panel-plus-buttons idiom every other
/// HUD in this project already uses (`GUI.color = new Color(0.02f,
/// 0.02f, 0.02f, 0.65f)` panel backing, plain `GUI.Button`/`GUI.Label`
/// rows, <see cref="UiScale.Begin"/>/`End`), docked directly ABOVE <see
/// cref="ProductionQueueHud"/>'s own always-visible tile row (reading
/// its live <see cref="ProductionQueueHud.TileRowTop"/> rather than
/// hardcoding a height, same "read a neighbour's own dynamic anchor"
/// contract <see cref="LabBattalionHud"/> already uses for the opposite
/// corner's stack) -- the one remaining unclaimed edge of that same
/// corner.
///
/// Opened by <see cref="GrabCursor.OpenOrdersPopup"/>, fired when a
/// raycast hits a Factory's own <see cref="FactoryClipboard"/> prop
/// (<see cref="GrabCursor.TryPickUp"/>) -- clicking the SAME Factory's
/// clipboard again closes it (a plain toggle); clicking a DIFFERENT
/// Factory's clipboard switches straight to that one. All of this
/// panel's own queue-mutating buttons (<see
/// cref="GrabCursor.MoveOrderUp"/>/`MoveOrderDown`/
/// `AdjustOrderQuantity`/`CancelQueueItem`) read/write the SAME
/// per-Factory queue data <see cref="GrabCursor.TickProduction"/>
/// actually drains -- this class owns no queue state of its own, only
/// which Factory is currently open, so closing and reopening the popup
/// can never lose anything.</summary>
public class FactoryOrdersHud : MonoBehaviour
{
    public GrabCursor grabCursor;
    public ProductionQueueHud productionQueueHud;

    [Header("Placement (docks above ProductionQueueHud's own tile row)")]
    public float panelWidth = 240f;
    public float rowHeight = 22f;
    public float headerHeight = 20f;
    public float sectionLabelHeight = 16f;
    public float marginPixels = 12f;
    public float closeButtonSize = 18f;
    public float smallButtonWidth = 20f;

    private SimBuilding _openFactory;

    public static bool PointerOver { get; private set; }

    public void Init(GrabCursor grabCursorRef, ProductionQueueHud productionQueueHudRef)
    {
        grabCursor = grabCursorRef;
        productionQueueHud = productionQueueHudRef;
        if (grabCursor != null) grabCursor.OpenOrdersPopup += HandleOpenOrdersPopup;
    }

    /// <summary>2026-08 ("click on the Factory clipboard... open an
    /// Order Queue popup"): a plain toggle -- the SAME clipboard clicked
    /// again closes its own popup (no separate close gesture needed for
    /// the common case), a DIFFERENT Factory's clipboard switches
    /// straight to it without requiring an explicit close first.</summary>
    private void HandleOpenOrdersPopup(SimBuilding factory)
    {
        _openFactory = _openFactory == factory ? null : factory;
    }

    private void OnGUI()
    {
        if (grabCursor == null || _openFactory == null) { PointerOver = false; return; }

        var prevMatrix = UiScale.Begin();

        var orders = grabCursor.OrdersFor(_openFactory);
        var queueRows = Mathf.Max(0, orders.Count - 1);
        // header + "NOW BUILDING" label + its own row (or an "idle" row
        // if nothing's building) + "QUEUE" label + one row per queued
        // item (or an "empty" row if the queue itself is empty).
        var rows = 1 /* now-building row or idle row */ + Mathf.Max(1, queueRows);
        var panelHeight = headerHeight + sectionLabelHeight + sectionLabelHeight + rows * rowHeight + marginPixels;

        var screenW = UiScale.Width;
        var defaultBottom = UiScale.Height - marginPixels;
        var bottom = productionQueueHud != null ? productionQueueHud.TileRowTop : defaultBottom;
        var panelX = screenW - marginPixels - panelWidth;
        var panelY = bottom - panelHeight;
        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        var e = Event.current;
        PointerOver = e != null && panelRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelRect.y;

        // "Factory identity/name if the existing UI supports this" --
        // GrabCursor.FactoryDisplayName reads the same faction lookup
        // Drop/HoverTargetFor already do. A close ("X") button sits at
        // the header's own right edge -- explicit close, in addition to
        // the clipboard-toggle above.
        GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth - closeButtonSize - 8f, headerHeight),
            grabCursor.FactoryDisplayName(_openFactory) + " ORDERS");
        var closeRect = new Rect(panelRect.xMax - closeButtonSize - 4f, y + 1f, closeButtonSize, closeButtonSize);
        if (GUI.Button(closeRect, "X")) _openFactory = null;
        y += headerHeight;

        GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth, sectionLabelHeight), "NOW BUILDING");
        y += sectionLabelHeight;

        if (orders.Count > 0)
        {
            DrawOrderRow(panelRect, y, "", orders[0].Label, orders[0].Remaining, 0);
        }
        else
        {
            GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth, rowHeight), "(idle)");
        }
        y += rowHeight;

        GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth, sectionLabelHeight), "QUEUE");
        y += sectionLabelHeight;

        if (queueRows == 0)
        {
            GUI.Label(new Rect(panelRect.x + 4f, y, panelWidth, rowHeight), "(empty)");
            y += rowHeight;
        }
        else
        {
            for (var i = 1; i < orders.Count; i++)
            {
                DrawOrderRow(panelRect, y, i + ".", orders[i].Label, orders[i].Remaining, i);
                y += rowHeight;
            }
        }

        UiScale.End(prevMatrix);
    }

    /// <summary>2026-08 ("Allow queued orders to be moved up/down... Each
    /// queued order should support increasing or decreasing its
    /// quantity... Allow the player to cancel an order"): one row --
    /// index/label/remaining-count text plus four buttons, all of them
    /// calling straight into `GrabCursor`'s own queue mutators (no
    /// duplicated queue logic here). Index 0 (the active build) still
    /// gets the SAME row shape -- `MoveOrderUp`/`MoveOrderDown` already
    /// no-op safely at either end of the list, so nothing needs a
    /// separate "is this the front row" branch here.</summary>
    private void DrawOrderRow(Rect panelRect, float y, string prefix, string label, int remaining, int index)
    {
        var textWidth = panelWidth - closeButtonSize - smallButtonWidth * 4f - 12f;
        var textRect = new Rect(panelRect.x + 4f, y, textWidth, rowHeight);
        GUI.Label(textRect, prefix + " " + (string.IsNullOrEmpty(label) ? "?" : label) + "  x" + remaining);

        // plain ASCII glyphs throughout (same as the header's own "X"
        // close button) -- Unity's default IMGUI font's coverage of
        // arrow/cross unicode glyphs can't be confirmed without a real
        // Editor/Player render in this environment, so this avoids the
        // risk of a blank/tofu button label entirely.
        var bx = panelRect.x + 4f + textWidth;
        if (GUI.Button(new Rect(bx, y, smallButtonWidth, rowHeight), "^")) grabCursor.MoveOrderUp(_openFactory, index);
        bx += smallButtonWidth;
        if (GUI.Button(new Rect(bx, y, smallButtonWidth, rowHeight), "v")) grabCursor.MoveOrderDown(_openFactory, index);
        bx += smallButtonWidth;
        if (GUI.Button(new Rect(bx, y, smallButtonWidth, rowHeight), "-")) grabCursor.AdjustOrderQuantity(_openFactory, index, false);
        bx += smallButtonWidth;
        if (GUI.Button(new Rect(bx, y, smallButtonWidth, rowHeight), "+")) grabCursor.AdjustOrderQuantity(_openFactory, index, true);
        bx += smallButtonWidth;
        if (GUI.Button(new Rect(bx, y, closeButtonSize, rowHeight), "X")) grabCursor.CancelQueueItem(_openFactory, index);
    }
}
