using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "factories, like in StarCraft, make x number
/// of units... there is a cued icons with numbers in the lower right hand
/// corner that specify the number of units to make of each type that
/// includes battalions and individual units... a small icon will float
/// over top [of the factory] with a number showing where we are in the
/// build process... click on the factory and can abort all builds"):
///
/// a row of IMGUI tiles anchored to the screen's bottom-right corner --
/// every screen corner but this one is already claimed (minimap +
/// RecallHud/BattalionHud bottom-left, BuildMenuHud/BuildingNavHud
/// top-left/bottom-center) -- one tile per <see
/// cref="GrabCursor.ProductionQueue"/> entry, in build order, each showing
/// its remaining count as a corner badge. The FRONT tile (index 0, the one
/// actually being timed by <see cref="GrabCursor.TickProduction"/>) gets a
/// fill bar underneath showing live progress toward its next spawn.
///
/// Two requested pieces are deliberately folded into this single panel
/// rather than built as separate UI, since there is currently no click-to-
/// select machinery for RTS/SimBuilding buildings at all (<see
/// cref="RuntimeCityBuilder"/>'s own `_buildingByCollider` registry only
/// tracks procedural CityGen buildings, never SimBuilding roster entries --
/// confirmed by reading that registration code, not assumed): (1) "click
/// the factory, queue pops up next to the minimap" becomes this same
/// always-visible panel (there is normally only ever one queue to show, so
/// a second copy gated behind a building click would just be a redundant
/// path to the identical list); (2) "click the factory to abort all
/// builds" becomes this panel's own "Cancel All" button rather than a 3D
/// raycast against a building that isn't selectable yet. Both are scope
/// simplifications, not literal readings of the request -- flagged here so
/// a future pass building real building-selection can revisit them.
///
/// The floating per-Factory progress badge IS built as literally described:
/// a small billboard (same world-to-screen idiom as <see
/// cref="HarvesterMarkerHud"/>/<see cref="HealthBars"/>) hovering over
/// <see cref="GrabCursor.FindAnyOwnCompleteFactory"/>'s position whenever
/// the queue is non-empty, showing the front item's remaining count.
///
/// 2026-08 (creator direction: "When Building a battalion it should show
/// a image of the monster being built, and the battalion name
/// underneath... use the portrait created in the lab. Export that with
/// the monster"): each tile now fills with the item's real portrait
/// (<see cref="GrabCursor.ProductionQueue"/>'s own new `PortraitPng`
/// field -- the Lab's actual WebGL-rendered thumbnail, decoded via <see
/// cref="PortraitTexture"/>'s shared cache so the SAME base64 blob is
/// never decoded twice, even across the roof hologram which reads the
/// same field) instead of the flat tinted square
/// the old text-abbreviation tile used, with the item's `Label`
/// (genome id for a single unit, the battalion's own saved name for a
/// Battalion/LabBattalion) drawn as a real text strip BELOW the tile --
/// "the battalion name underneath," read literally, not squeezed inside
/// the tile itself where a real name would overflow a 44px square. Falls
/// back to the old flat-tile-plus-abbreviation look whenever a portrait
/// is unavailable (an older genome saved before portraits existed, or a
/// failed client-side bake) -- never a hard error, never a blank
/// tile.
///
/// 2026-08 follow-up (creator direction: "Add a progress bar at the
/// bottom of the current monster build to factory for both the
/// individual monsters and battalion"; bug report: "the battalion
/// build portrait is still not visible above the factory. Even the
/// Battalion label... is not visible"): <see cref="DrawCurrentBuildAtRoof"/>
/// floats a name label + progress bar at the exact world position <see
/// cref="RoofPortraitHologram"/> anchors its own spinning portrait at,
/// gated on that class's new `HasTarget` rather than on its portrait
/// having actually decoded -- the fix for the label being invisible
/// too: before this pass, everything above the Factory (image AND any
/// label) depended on the SAME portrait decode succeeding, so a
/// portrait-less genome (an older battalion member, a failed Lab
/// upload) blanked the whole display with nothing left on screen to
/// diagnose from.</summary>
public class ProductionQueueHud : MonoBehaviour
{
    public GrabCursor grabCursor;

    /// <summary>2026-08 (creator report: "the battalion build portrait is
    /// still not visible above the factory. Even the Battalion label
    /// that should [be] with the portrait is not visible"): so the new
    /// world-anchored build label + progress bar (<see
    /// cref="DrawCurrentBuildAtRoof"/>) can float at the EXACT same spot
    /// the hologram itself uses (<see
    /// cref="RoofPortraitHologram.CurrentWorldAnchor"/>), rather than
    /// re-deriving that world position a third time.</summary>
    public RoofPortraitHologram roofHologram;

    [Header("Placement (docked to the screen's bottom-right corner)")]
    public float tileSize = 44f;
    public float tileGap = 6f;
    public float marginPixels = 12f;
    public float cancelButtonHeight = 20f;
    public int maxVisibleTiles = 8;
    [Tooltip("Extra strip below each tile for the item's name (\"the battalion name underneath\") -- 0 would silently omit it, so this stays a real, non-zero default rather than an opt-in.")]
    public float labelStripHeight = 14f;
    [Tooltip("2026-08 (Factory Build Queue / Order Clipboard): gap between this tile row and FactoryOrdersHud's own popup, which docks directly above it -- same dockGapPixels convention BattalionHud/LabBattalionHud already use for the opposite corner's stack.")]
    public float dockGapPixels = 8f;

    private static Texture2D _tex;

    public static bool PointerOver { get; private set; }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard): the Y
    /// (screen space, GUI top-left origin) a panel wanting to dock right
    /// ABOVE this one's own tile row should treat as its own bottom edge
    /// -- same "read a neighbour's own dynamic height" contract <see
    /// cref="BattalionHud.StackTop"/> already established for the
    /// opposite corner's stack, exposed so <see cref="FactoryOrdersHud"/>
    /// never overlaps this row. Defaults to the screen's own bottom-right
    /// margin (i.e. "nothing to dock above") whenever the tile row itself
    /// isn't drawn this frame -- set at the very TOP of `OnGUI`, before
    /// any early return, and only ever raised (never lowered) once the
    /// real panel height is known further down.</summary>
    public float TileRowTop { get; private set; }

    /// <summary>2026-08 (creator direction: "I would also like to be able
    /// to drop monster on the factory HUD display on the bottom right"):
    /// which tile column the cursor is currently over -- mirrors <see
    /// cref="FactoryOrdersHud.HoveredSlotIndex"/>'s own contract exactly
    /// (-1 if not hovering a real tile, or this panel isn't even drawn
    /// this frame). Index 0 is the SAME "current slot" concept the Order
    /// Sheet already uses (a drop there promotes/interrupts); any other
    /// index appends. <see cref="GrabCursor"/> reads this every frame
    /// while Carrying, exactly like it already does for the Order
    /// Sheet.</summary>
    public int HoveredTileIndex { get; private set; } = -1;

    public void Init(GrabCursor grabCursorRef, RoofPortraitHologram roofHologramRef)
    {
        grabCursor = grabCursorRef;
        roofHologram = roofHologramRef;
    }

    private const float ProgressBarHeight = 4f;

    private void OnGUI()
    {
        TileRowTop = UiScale.Height - marginPixels;
        if (grabCursor == null) { PointerOver = false; return; }

        // 2026-08 (creator direction: "if the user presses the space bar
        // a number on the grab increments"; "the user may also click on
        // the factory... press space or + or - keys to increase or
        // decrease the number of monsters to build"; follow-up: "add an
        // indicator of cost showing up as you increase or decrease the
        // number of units to build"): these two can both be live with an
        // otherwise EMPTY queue (a fresh carry that hasn't been dropped
        // yet; a selected Factory with nothing queued), so they're read
        // and gated BEFORE the old "nothing queued, draw nothing" early
        // return below, not folded inside it.
        var pendingCount = grabCursor.PendingBuildCount;
        var selectedBuild = grabCursor.SelectedFactoryBuild;
        var carryingOrder = grabCursor.IsCarryingOrder;

        if (!grabCursor.HasQueuedProduction && pendingCount <= 0 && !selectedBuild.HasSelection && !carryingOrder)
        {
            PointerOver = false;
            HoveredTileIndex = -1;
            return;
        }
        if (_tex == null) _tex = Texture2D.whiteTexture;

        if (pendingCount > 0) DrawPendingBuildBadge(pendingCount);
        // 2026-08 (Factory Build Queue / Order Clipboard, creator
        // direction: "Grab a Battalion Group from the existing bottom-
        // left menu. Drag it toward a Factory"): a Battalion/LabBattalion
        // carry has no physical MonsterAgent to follow the cursor, so
        // this badge -- the SAME cursor-following visual/position
        // DrawPendingBuildBadge already established for a carried
        // monster's dialed-in count -- is the only feedback the player
        // gets that something is actually in hand.
        if (carryingOrder) DrawCarriedOrderBadge(grabCursor.CarriedOrderLabel, grabCursor.CarriedOrderCount);
        if (selectedBuild.HasSelection) DrawSelectedFactoryBadge(selectedBuild.Label, selectedBuild.Count, selectedBuild.WorldPos);

        // 2026-08 (creator direction: "I would also like to be able to
        // drop monster on the factory HUD display on the bottom right"):
        // the anchor Factory this tile row already shows (or would show)
        // -- while actively carrying something, the panel stays drawn
        // (with an extra empty placeholder tile if the real queue is
        // currently empty) purely to give the drag a real Rect to drop
        // onto, even before anything's actually queued here yet.
        var dropTargetFactory = (pendingCount > 0 || carryingOrder) ? grabCursor.FindAnyOwnCompleteFactory() : null;
        if (!grabCursor.HasQueuedProduction && dropTargetFactory == null) { PointerOver = false; HoveredTileIndex = -1; return; }

        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        var items = new List<(string Label, int Remaining, float Progress, string PortraitPng)>(grabCursor.ProductionQueue);
        var minShown = dropTargetFactory != null ? 1 : 0;
        var shown = Mathf.Min(Mathf.Max(items.Count, minShown), maxVisibleTiles);

        var panelWidth = shown * tileSize + Mathf.Max(0, shown - 1) * tileGap;
        // 2026-08 (portrait tiles, "the battalion name underneath"): the
        // label strip and its own gap now sit between the tile row and
        // the progress bar/Cancel button -- every row below the tiles
        // shifted down by exactly labelStripHeight + tileGap from the
        // old layout, nothing else about the stacking order changed.
        var panelHeight = tileSize + tileGap + labelStripHeight + ProgressBarHeight + tileGap + cancelButtonHeight;
        var panelX = screenW - marginPixels - panelWidth;
        var panelY = screenH - marginPixels - panelHeight;
        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
        TileRowTop = panelY - dockGapPixels;

        var e = Event.current;
        var mouse = e != null ? e.mousePosition : new Vector2(-1f, -1f);
        PointerOver = e != null && panelRect.Contains(mouse);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(panelRect, _tex);
        GUI.color = Color.white;

        var hoveredTile = -1;
        for (var i = 0; i < shown; i++)
        {
            var tileRect = new Rect(panelX + i * (tileSize + tileGap), panelY, tileSize, tileSize);
            var hitRect = new Rect(tileRect.x, tileRect.y, tileSize, tileSize + labelStripHeight);
            if (e != null && hitRect.Contains(mouse)) hoveredTile = i;

            if (i >= items.Count)
            {
                DrawEmptyDropTile(tileRect);
                continue;
            }

            var item = items[i];
            var portrait = PortraitTexture.For(item.PortraitPng);

            if (portrait != null)
            {
                // fills the tile edge-to-edge -- no tint (the portrait IS
                // the visual, unlike the old flat-color placeholder tile)
                GUI.DrawTexture(tileRect, portrait, ScaleMode.ScaleToFit);
            }
            else
            {
                // fallback: the original flat tinted tile + text
                // abbreviation, for an item whose genome has no portrait
                // (an older save, or a failed client-side bake).
                GUI.color = new Color(0.32f, 0.28f, 0.38f, 1f);
                GUI.DrawTexture(tileRect, _tex);
                GUI.color = Color.white;
                GUI.Label(tileRect, Abbrev(item.Label));
            }

            var badgeRect = new Rect(tileRect.xMax - 18f, tileRect.yMax - 16f, 18f, 16f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(badgeRect, _tex);
            GUI.color = Color.white;
            GUI.Label(badgeRect, item.Remaining.ToString());

            // "the battalion name underneath" -- a real text strip below
            // the tile, shadowed for legibility against whatever the
            // portrait's own background happens to be (a flat color tile
            // never needed this; a photographic-ish portrait does).
            var labelRect = new Rect(tileRect.x, tileRect.yMax + 1f, tileSize, labelStripHeight);
            DrawShadowedNameLabel(labelRect, item.Label);

            if (i == 0)
            {
                var barRect = new Rect(tileRect.x, labelRect.yMax + 1f, tileSize, ProgressBarHeight);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(barRect, _tex);
                GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * item.Progress, barRect.height), _tex);
                GUI.color = Color.white;
            }
        }
        HoveredTileIndex = hoveredTile;

        if (items.Count > shown)
            GUI.Label(new Rect(panelX, panelY - 16f, panelWidth, 14f), "+" + (items.Count - shown) + " more");

        if (items.Count > 0)
        {
            var cancelRect = new Rect(panelX, panelY + tileSize + tileGap + labelStripHeight + ProgressBarHeight + tileGap, panelWidth, cancelButtonHeight);
            if (GUI.Button(cancelRect, "Cancel All")) grabCursor.CancelAllProduction();
        }

        // 2026-08 (creator direction: "the ui is not scaling properly to
        // screen sizes"): UiScale.End happens HERE, before the factory
        // badge -- that badge is projected from a real 3D world position
        // via Camera.WorldToScreenPoint, which already returns true
        // screen pixels regardless of any GUI.matrix. Drawing it INSIDE
        // the scaled block above would double-apply the scale and detach
        // it from the Factory it's meant to float over -- see this
        // method's own note just below.
        UiScale.End(prevMatrix);
        if (items.Count > 0)
        {
            DrawFactoryBadge(items[0].Remaining);
            DrawCurrentBuildAtRoof(items[0].Label, items[0].Progress);
        }
    }

    /// <summary>2026-08 ("drop monster on the factory HUD display"): the
    /// trailing empty tile drawn past the last real queued item whenever
    /// something's being carried -- same "dashed-look outline with a
    /// centered +" affordance <see cref="FactoryOrdersHud.DrawEmptySlot"/>
    /// already established for its own trailing slot, so both drop
    /// targets read as the same kind of thing.</summary>
    private void DrawEmptyDropTile(Rect tileRect)
    {
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        GUI.DrawTexture(tileRect, _tex);
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

    /// <summary>Billboards the front item's remaining count over the
    /// Factory it's actually draining into -- a no-op if the player
    /// somehow has queued production with no live Factory (lost it mid-
    /// match) or the camera can't see it. Deliberately drawn in REAL
    /// screen-pixel space (called after UiScale.End, not before) -- its
    /// position comes from Camera.WorldToScreenPoint, already true
    /// screen pixels, not the reference-resolution canvas the rest of
    /// this panel is authored against.</summary>
    private void DrawFactoryBadge(int remaining)
    {
        var cam = Camera.main;
        if (cam == null || grabCursor.builder == null) return;

        var factory = grabCursor.FindAnyOwnCompleteFactory();
        if (factory == null) return;

        var world = grabCursor.builder.WorldOf(factory.Hex) + Vector3.up * 4f;
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return;

        const float badgeSize = 20f;
        var x = sp.x - badgeSize * 0.5f;
        var y = Screen.height - sp.y - badgeSize * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x - 1f, y - 1f, badgeSize + 2f, badgeSize + 2f), _tex);
        GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(x, y, badgeSize, badgeSize), _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 1f, badgeSize, badgeSize), remaining.ToString());
    }

    /// <summary>2026-08 (creator direction: "if the user presses the
    /// space bar a number on the grab increments, denoting the number of
    /// that monster you want to build"; follow-up: "add an indicator of
    /// cost showing up as you increase or decrease the number of units
    /// to build"): follows the real OS cursor while carrying (<see
    /// cref="Event.current"/>'s own mousePosition, same screen-pixel
    /// space <see cref="DrawFactoryBadge"/> already draws in) rather than
    /// UiScale's scaled canvas -- called before <see cref="UiScale.
    /// Begin"/> even runs, so this and <see
    /// cref="DrawSelectedFactoryBadge"/> both stay in that same real-
    /// pixel space without needing their own End/Begin bracket.</summary>
    private void DrawPendingBuildBadge(int count)
    {
        var e = Event.current;
        if (e == null) return;
        var pos = e.mousePosition;

        const float w = 96f, h = 34f;
        var rect = new Rect(pos.x + 18f, pos.y + 18f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f), "Build x" + count);
        GUI.color = new Color(0.85f, 0.85f, 0.55f, 1f);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 17f, rect.width - 8f, 16f), CostText(count));
        GUI.color = Color.white;
    }

    /// <summary>2026-08 (Factory Build Queue / Order Clipboard, creator
    /// direction: "Grab a Battalion Group from the existing bottom-left
    /// menu. Drag it toward a Factory"): the SAME cursor-following badge
    /// shape <see cref="DrawPendingBuildBadge"/> uses for a carried
    /// monster's dialed-in count, showing the carried Battalion/
    /// LabBattalion's own name + member count + cost instead -- "Build
    /// orders and cost outline also apply to battalions," and this is
    /// the ONLY visual feedback an abstract order (no physical transform
    /// to move) gets while being dragged.</summary>
    private void DrawCarriedOrderBadge(string label, int count)
    {
        var e = Event.current;
        if (e == null) return;
        var pos = e.mousePosition;

        const float w = 110f, h = 34f;
        var rect = new Rect(pos.x + 18f, pos.y + 18f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f), (string.IsNullOrEmpty(label) ? "?" : label) + " x" + count);
        GUI.color = new Color(0.85f, 0.85f, 0.55f, 1f);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 17f, rect.width - 8f, 16f), CostText(count));
        GUI.color = Color.white;
    }

    /// <summary>2026-08 (creator direction: "the user may also click on
    /// the factory... press space or + or - keys to increase or
    /// decrease the number of monsters to build"; follow-up: "Build
    /// orders and cost outline also apply to battalions"): floats above
    /// the selected Factory, the exact same world-to-screen billboard
    /// idiom <see cref="DrawFactoryBadge"/> already uses, showing the
    /// front queue item's own label/count/cost live as
    /// `GrabCursor.TickFactorySelectionKeys` mutates it.</summary>
    private void DrawSelectedFactoryBadge(string label, int count, Vector3 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var sp = cam.WorldToScreenPoint(worldPos + Vector3.up * 6f);
        if (sp.z <= 0f) return;

        const float w = 130f, h = 40f;
        var x = sp.x - w * 0.5f;
        var y = Screen.height - sp.y - h * 0.5f;
        var rect = new Rect(x, y, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f), (string.IsNullOrEmpty(label) ? "?" : label) + " x" + count);
        GUI.color = new Color(0.85f, 0.85f, 0.55f, 1f);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 18f, rect.width - 8f, 16f), CostText(count));
        GUI.color = Color.white;
    }

    /// <summary>"An indicator of cost" -- `count * grabCursor.
    /// cloneCostBlood`, the SAME flat per-clone rate <see
    /// cref="GrabCursor.TickProduction"/> actually spends via
    /// `TrySpendBlood` (this project has no per-genome cost model yet,
    /// see that class's own header), so this can never drift from what
    /// production will actually charge.</summary>
    private string CostText(int count)
    {
        return count * grabCursor.cloneCostBlood + " Blood";
    }

    /// <summary>2026-08 (creator direction: "Add a progress bar at the
    /// bottom of the current monster build to factory for both the
    /// individual monsters and battalion"; follow-up bug report: "the
    /// battalion build portrait is still not visible above the factory.
    /// Even the Battalion label that should [be] with the portrait is
    /// not visible"): the label + progress bar the roof hologram was
    /// always supposed to carry, drawn HERE instead of by <see
    /// cref="RoofPortraitHologram"/> itself (that class is explicitly a
    /// pure 3D-world component, no OnGUI -- see its own header) at the
    /// EXACT same world anchor the hologram uses
    /// (`roofHologram.CurrentWorldAnchor`), so the two always sit
    /// together regardless of which Factory/faction is currently live.
    /// Gated on `HasTarget`, NOT on the hologram's own quad being
    /// visible -- this is precisely the fix for "even the label is not
    /// visible": before this pass the whole display (image AND any
    /// label) depended on the portrait itself decoding, so a genuinely
    /// portrait-less genome (an old battalion member, a failed Lab
    /// upload) blanked everything with nothing on screen to debug from.
    /// `progress` (0..1, from <see cref="GrabCursor.ProductionQueue"/>'s
    /// own tuple) already comes from the SAME shared `_productionTimer`
    /// regardless of item Kind, so this needs no SingleUnit/Battalion
    /// branch to cover both.</summary>
    private void DrawCurrentBuildAtRoof(string label, float progress)
    {
        if (roofHologram == null || !roofHologram.HasTarget) return;
        var cam = Camera.main;
        if (cam == null) return;

        var sp = cam.WorldToScreenPoint(roofHologram.CurrentWorldAnchor + Vector3.up * 1.8f);
        if (sp.z <= 0f) return;

        const float w = 120f, h = 34f, barH = 5f;
        var x = sp.x - w * 0.5f;
        var y = Screen.height - sp.y - h * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x, y, w, h), _tex);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 3f, y + 1f, w - 6f, 16f), string.IsNullOrEmpty(label) ? "?" : label);

        // "a progress bar at the bottom" -- literally the bottom strip of
        // this same badge, under the name.
        var barRect = new Rect(x + 3f, y + h - barH - 3f, w - 6f, barH);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(barRect, _tex);
        GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(progress), barRect.height), _tex);
        GUI.color = Color.white;
    }

    private static string Abbrev(string label)
    {
        if (string.IsNullOrEmpty(label)) return "?";
        return label.Length <= 3 ? label : label.Substring(0, 3);
    }

    /// <summary>"The battalion name underneath" -- the FULL label (a
    /// genome id or a real saved battalion name), not the 3-letter
    /// `Abbrev` the fallback-tile text used, since this strip has real
    /// horizontal room a cramped 44px tile never did. IMGUI wraps/clips
    /// to the Rect automatically at small sizes, so an unusually long
    /// name degrades to a clipped tail rather than overflowing into a
    /// neighboring tile.</summary>
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
