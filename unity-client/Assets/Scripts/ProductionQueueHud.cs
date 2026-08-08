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
/// </summary>
public class ProductionQueueHud : MonoBehaviour
{
    public GrabCursor grabCursor;

    [Header("Placement (docked to the screen's bottom-right corner)")]
    public float tileSize = 44f;
    public float tileGap = 6f;
    public float marginPixels = 12f;
    public float cancelButtonHeight = 20f;
    public int maxVisibleTiles = 8;

    private static Texture2D _tex;

    public static bool PointerOver { get; private set; }

    public void Init(GrabCursor grabCursorRef)
    {
        grabCursor = grabCursorRef;
    }

    private void OnGUI()
    {
        if (grabCursor == null || !grabCursor.HasQueuedProduction) { PointerOver = false; return; }
        if (_tex == null) _tex = Texture2D.whiteTexture;
        var prevMatrix = UiScale.Begin();

        var screenW = UiScale.Width;
        var screenH = UiScale.Height;

        var items = new System.Collections.Generic.List<(string Label, int Remaining, float Progress)>(grabCursor.ProductionQueue);
        var shown = Mathf.Min(items.Count, maxVisibleTiles);

        var panelWidth = shown * tileSize + Mathf.Max(0, shown - 1) * tileGap;
        var panelHeight = tileSize + cancelButtonHeight + tileGap;
        var panelX = screenW - marginPixels - panelWidth;
        var panelY = screenH - marginPixels - panelHeight;
        var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        var e = Event.current;
        PointerOver = e != null && panelRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(panelRect, _tex);
        GUI.color = Color.white;

        for (var i = 0; i < shown; i++)
        {
            var item = items[i];
            var tileRect = new Rect(panelX + i * (tileSize + tileGap), panelY, tileSize, tileSize);

            GUI.color = new Color(0.32f, 0.28f, 0.38f, 1f);
            GUI.DrawTexture(tileRect, _tex);
            GUI.color = Color.white;
            GUI.Label(tileRect, Abbrev(item.Label));

            var badgeRect = new Rect(tileRect.xMax - 18f, tileRect.yMax - 16f, 18f, 16f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(badgeRect, _tex);
            GUI.color = Color.white;
            GUI.Label(badgeRect, item.Remaining.ToString());

            if (i == 0)
            {
                var barRect = new Rect(tileRect.x, tileRect.yMax + 1f, tileSize, 4f);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(barRect, _tex);
                GUI.color = new Color(0.75f, 0.35f, 0.35f, 1f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * item.Progress, barRect.height), _tex);
                GUI.color = Color.white;
            }
        }

        if (items.Count > shown)
            GUI.Label(new Rect(panelX, panelY - 16f, panelWidth, 14f), "+" + (items.Count - shown) + " more");

        var cancelRect = new Rect(panelX, panelY + tileSize + tileGap, panelWidth, cancelButtonHeight);
        if (GUI.Button(cancelRect, "Cancel All")) grabCursor.CancelAllProduction();

        // 2026-08 (creator direction: "the ui is not scaling properly to
        // screen sizes"): UiScale.End happens HERE, before the factory
        // badge -- that badge is projected from a real 3D world position
        // via Camera.WorldToScreenPoint, which already returns true
        // screen pixels regardless of any GUI.matrix. Drawing it INSIDE
        // the scaled block above would double-apply the scale and detach
        // it from the Factory it's meant to float over -- see this
        // method's own note just below.
        UiScale.End(prevMatrix);
        DrawFactoryBadge(items[0].Remaining);
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

    private static string Abbrev(string label)
    {
        if (string.IsNullOrEmpty(label)) return "?";
        return label.Length <= 3 ? label : label.Substring(0, 3);
    }
}
