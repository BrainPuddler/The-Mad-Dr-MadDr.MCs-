using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "give me a monster recall button that will
/// gather my troupes in one place"): a single IMGUI button, docked directly
/// above the minimap's own top edge (<see cref="SelectionHud"/> already
/// claims the minimap's right edge for its per-type icon row, so this
/// stays out of its way). Clicking it calls
/// <see cref="WaypointCommander.RecallAll"/>, which selects and orders
/// every currently-alive monster to rally at the player's own base.
///
/// Same "OnGUI's event queue and the New Input System's Mouse.current are
/// two separate, non-communicating input paths" guard every other HUD
/// panel in this project already exposes (<see cref="Minimap.PointerOver"/>,
/// <see cref="SelectionHud.PointerOver"/>, <see cref="BuildingNavHud.
/// PointerOver"/>) -- WaypointCommander's own Update() already checks all
/// three before treating a click as a world-space select/order.
///
/// A no-op (renders nothing) until <see cref="Init"/> has a live
/// commander/minimap -- same "don't draw before there's anything to draw"
/// contract every other HUD panel in this project already follows.
/// </summary>
public class RecallHud : MonoBehaviour
{
    public WaypointCommander commander;
    public Minimap minimap;

    [Header("Placement (docked above the minimap's top edge)")]
    public float width = 90f;
    public float height = 26f;
    public float dockGapPixels = 8f;

    public static bool PointerOver { get; private set; }

    public void Init(WaypointCommander waypointCommander, Minimap minimapRef)
    {
        commander = waypointCommander;
        minimap = minimapRef;
    }

    private void OnGUI()
    {
        if (commander == null || minimap == null) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var mapRect = minimap.ScreenRect;
        var rect = new Rect(mapRect.x, mapRect.y - height - dockGapPixels, width, height);

        var e = Event.current;
        PointerOver = e != null && rect.Contains(e.mousePosition);

        if (GUI.Button(rect, "Recall")) commander.RecallAll();

        UiScale.End(prevMatrix);
    }
}
