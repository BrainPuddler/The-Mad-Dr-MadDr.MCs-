using UnityEngine;

/// <summary>
/// 2026-08 (docs/12 "Lab stable" half of battalion grouping -- creator
/// direction: "in the lab, the stable area, where can shift plus quick
/// select monsters and hit G key. It will then pop up with the name
/// requester those will show up in the game that battalion group and I
/// can make the factory build that battalion group of monsters"): lists
/// every named battalion template this account has saved in the Lab
/// (<see cref="RuntimeCityBuilder.LabBattalions"/>, fetched via GET
/// /battalions), each with a "Build" button that resolves its genome ids
/// against the player's own fetched roster and queues the whole group at
/// the Factory (<see cref="GrabCursor.BuildLabBattalion"/>) -- the SAME
/// staggered production queue <see cref="ProductionQueueHud"/> already
/// shows, not a separate build path.
///
/// Docked directly above <see cref="BattalionHud"/> (itself above
/// RecallHud, above the minimap), reading its live <see
/// cref="BattalionHud.StackTop"/> rather than hardcoding a height --
/// BattalionHud's own row count is dynamic, so this panel's anchor has
/// to be too. A no-op (renders nothing) whenever there are no Lab
/// battalions loaded yet, or before <see cref="Init"/> has a live
/// builder/minimap -- same "don't draw before there's anything to draw"
/// contract every other HUD panel in this project already follows.
///
/// 2026-08 follow-up (Factory Build Queue / Order Clipboard, creator
/// direction: "Grab a Battalion Group from the existing bottom-left
/// menu. Drag it toward a Factory"): the name row is now a real button
/// that picks the template up (<see
/// cref="GrabCursor.BeginCarryingLabOrder"/>) while Grab Mode is armed
/// and empty-handed -- the "Build" button is untouched, still an
/// immediate one-click build at the nearest own Factory.
/// </summary>
public class LabBattalionHud : MonoBehaviour
{
    public RuntimeCityBuilder builder;
    public Minimap minimap;
    public BattalionHud battalionHud;
    public GrabCursor grabCursor;

    [Header("Placement (docked above BattalionHud, above the minimap)")]
    public float labelWidth = 130f;
    public float buttonWidth = 40f;
    public float rowHeight = 20f;
    public float dockGapPixels = 8f;

    public static bool PointerOver { get; private set; }

    public void Init(RuntimeCityBuilder cityBuilder, Minimap minimapRef, BattalionHud battalionHudRef, GrabCursor grabCursorRef)
    {
        builder = cityBuilder;
        minimap = minimapRef;
        battalionHud = battalionHudRef;
        grabCursor = grabCursorRef;
    }

    private void OnGUI()
    {
        if (builder == null || minimap == null) { PointerOver = false; return; }

        var templates = builder.LabBattalions;
        if (templates == null || templates.Length == 0) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var mapRect = minimap.ScreenRect;
        var stackTop = battalionHud != null ? battalionHud.StackTop : mapRect.y;

        var rows = templates.Length;
        var panelWidth = labelWidth + buttonWidth;
        var panelRect = new Rect(mapRect.x, stackTop - rows * rowHeight, panelWidth, rows * rowHeight);
        var e = Event.current;
        PointerOver = e != null && panelRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelRect.y;
        foreach (var t in templates)
        {
            var labelRect = new Rect(mapRect.x, y, labelWidth, rowHeight);
            // 2026-08 (creator direction: "Grab a Battalion Group from
            // the existing bottom-left menu. Drag it toward a Factory"):
            // this row was a plain GUI.Label before (no click behavior
            // at all -- Lab templates have no in-game "select" concept
            // the way live battalions do) -- now a real button so it can
            // be picked up while Grab Mode is armed and empty-handed,
            // same one-click pick-up-and-carry BattalionHud's own row
            // just gained. A click while NOT armed is a silent no-op,
            // same as the old Label doing nothing.
            if (GUI.Button(labelRect, t.Name + " (" + t.CreatureIds.Length + ")")
                && grabCursor != null && grabCursor.IsGrabModeActive)
                grabCursor.BeginCarryingLabOrder(t.Name, t.CreatureIds);

            var buildRect = new Rect(mapRect.x + labelWidth, y, buttonWidth, rowHeight);
            if (GUI.Button(buildRect, "Build") && grabCursor != null)
                grabCursor.BuildLabBattalion(t.Name, t.CreatureIds);

            y += rowHeight;
        }

        UiScale.End(prevMatrix);
    }
}
