using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "battalion grouping system... assign
/// battalion groups to the number keys zero through 9 for quick
/// selection... I can make the factory build that battalion group of
/// monsters"): a small IMGUI list -- one row per currently-defined
/// battalion (<see cref="WaypointCommander.Battalions"/>), showing its
/// bound digit, auto-incremented name, and live member count, with two
/// buttons: "select" (same effect as its Alt+digit hotkey, for players who
/// forget the binding or prefer the mouse) and "build" (<see
/// cref="GrabCursor.BuildBattalionAtOwnFactory"/> -- clones one new
/// specimen per live member at the player's own Factory). Docked directly
/// above <see cref="RecallHud"/> (itself docked above the minimap),
/// continuing the same vertical stack rather than claiming a new screen
/// corner.
///
/// A no-op (renders nothing) whenever there are no battalions defined yet,
/// or before <see cref="Init"/> has a live commander/minimap -- same "don't
/// draw before there's anything to draw" contract every other HUD panel in
/// this project already follows.
///
/// 2026-08 follow-up (Factory Build Queue / Order Clipboard, creator
/// direction: "Grab a Battalion Group from the existing bottom-left
/// menu. Drag it toward a Factory"): the "select" click on each row now
/// picks the battalion up (<see cref="GrabCursor.BeginCarryingOrder"/>)
/// instead, while Grab Mode is armed and empty-handed -- the "Build"
/// button is untouched, still an immediate one-click build at the
/// nearest own Factory.
/// </summary>
public class BattalionHud : MonoBehaviour
{
    public WaypointCommander commander;
    public Minimap minimap;
    public RecallHud recallHud;
    public GrabCursor grabCursor;

    [Header("Placement (docked above RecallHud, above the minimap)")]
    public float labelWidth = 130f;
    public float buttonWidth = 40f;
    public float rowHeight = 20f;
    public float dockGapPixels = 8f;

    public static bool PointerOver { get; private set; }

    /// <summary>Y-coordinate (screen space, GUI top-left origin) that a
    /// HUD panel wanting to stack directly above THIS one should treat
    /// as its own bottom edge -- the same "read a neighbour's own
    /// height/gap" contract this class already uses to stack above
    /// RecallHud, exposed so a third panel (<see cref="LabBattalionHud"/>)
    /// can stack above this one without hardcoding its dynamic,
    /// row-count-dependent height.</summary>
    public float StackTop { get; private set; }

    public void Init(WaypointCommander waypointCommander, Minimap minimapRef, RecallHud recallHudRef, GrabCursor grabCursorRef)
    {
        commander = waypointCommander;
        minimap = minimapRef;
        recallHud = recallHudRef;
        grabCursor = grabCursorRef;
    }

    private void OnGUI()
    {
        if (commander == null || minimap == null) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var mapRect = minimap.ScreenRect;
        // stack above RecallHud's own row if it's present, else straight
        // above the minimap -- same fallback shape every docked-HUD panel
        // in this project uses when an optional neighbour isn't wired up.
        var stackTop = recallHud != null
            ? mapRect.y - recallHud.height - recallHud.dockGapPixels
            : mapRect.y;

        var rows = 0;
        foreach (var unused in commander.Battalions) rows++;
        if (rows == 0) { PointerOver = false; StackTop = stackTop - dockGapPixels; UiScale.End(prevMatrix); return; }

        var panelWidth = labelWidth + buttonWidth;
        var panelRect = new Rect(mapRect.x, stackTop - rows * rowHeight - dockGapPixels, panelWidth, rows * rowHeight);
        StackTop = panelRect.y - dockGapPixels;
        var e = Event.current;
        PointerOver = e != null && panelRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var y = panelRect.y;
        foreach (var battalion in commander.Battalions)
        {
            var labelRect = new Rect(mapRect.x, y, labelWidth, rowHeight);
            var label = battalion.Slot + ": " + battalion.Name + " (" + battalion.Count + ")";
            if (GUI.Button(labelRect, label))
            {
                // 2026-08 (creator direction: "Grab a Battalion Group
                // from the existing bottom-left menu. Drag it toward a
                // Factory"): the SAME click this row already used for
                // "select" now picks the battalion UP instead, while
                // Grab Mode is armed and empty-handed -- matches the
                // rest of Grab Mode's own click-to-pick-up/click-to-drop
                // convention exactly (no new hold-and-drag gesture; a
                // live monster in the world is already picked up the
                // same way, one click, not a press-hold-release). Outside
                // Grab Mode the row keeps its original meaning
                // unchanged. `BeginCarryingOrder` itself no-ops if
                // something is already being carried, so this never
                // double-picks.
                if (grabCursor != null && grabCursor.IsGrabModeActive)
                    grabCursor.BeginCarryingOrder(commander.BattalionMembers(battalion.Slot));
                else
                    commander.SelectBattalion(battalion.Slot);
            }

            var buildRect = new Rect(mapRect.x + labelWidth, y, buttonWidth, rowHeight);
            if (GUI.Button(buildRect, "Build") && grabCursor != null)
                grabCursor.BuildBattalionAtOwnFactory(commander.BattalionMembers(battalion.Slot));

            y += rowHeight;
        }

        UiScale.End(prevMatrix);
    }
}
