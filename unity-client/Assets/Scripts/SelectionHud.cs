using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SC2-style selection panel: one icon per distinct creature TYPE
/// (<see cref="MonsterAgent.BodyPlan"/> -- this codebase's own established
/// "type" concept, already used by WaypointCommander's double-click
/// "select all of this type on screen") currently selected, each with a
/// count badge when more than one of that type is selected, docked
/// immediately to the right of the minimap the same way SC2's own command
/// card sits beside its minimap.
///
/// IMGUI, same colored-swatch-plus-shadowed-label "icon" idiom
/// <see cref="BuildingNavHud"/> established -- this repo has no real icon
/// sprite/texture art anywhere. The swatch color is <see
/// cref="MonsterBody.PlanColor"/>, the same per-plan palette the actual
/// creature bodies render with, so a type's HUD icon and its in-world
/// silhouette color always agree.
///
/// Clicking an icon narrows the current selection down to just the
/// members of that type (<see cref="WaypointCommander.SetSelection"/>) --
/// useful the moment a mixed army is selected and an order should target
/// only one type in it.
///
/// A no-op (renders nothing) whenever the selection is empty or <see
/// cref="Init"/> hasn't been called with a live commander/minimap yet.
/// </summary>
public class SelectionHud : MonoBehaviour
{
    [Header("Data source")]
    public WaypointCommander commander;
    public Minimap minimap;

    [Header("Placement (docked to the minimap's right edge)")]
    public float iconSize = 40f;
    public float iconGap = 6f;
    public float dockGapPixels = 10f;

    /// <summary>Same "OnGUI's event queue and the New Input System's
    /// Mouse.current are two separate, non-communicating input paths"
    /// guard every other HUD panel in this project already exposes -- see
    /// <see cref="Minimap.PointerOver"/>/<see cref="BuildingNavHud.
    /// PointerOver"/>.</summary>
    public static bool PointerOver { get; private set; }

    private sealed class TypeGroup
    {
        public string Plan;
        public readonly List<MonsterAgent> Members = new List<MonsterAgent>();
    }

    public void Init(WaypointCommander waypointCommander, Minimap minimapRef)
    {
        commander = waypointCommander;
        minimap = minimapRef;
    }

    /// <summary>Buckets the live selection by BodyPlan, sorted
    /// alphabetically for a stable row order across frames.</summary>
    private List<TypeGroup> GatherGroups()
    {
        var groups = new List<TypeGroup>();
        if (commander == null) return groups;

        var byPlan = new Dictionary<string, TypeGroup>();
        foreach (var a in commander.Selected)
        {
            if (a == null) continue;
            var plan = a.BodyPlan ?? "";
            TypeGroup g;
            if (!byPlan.TryGetValue(plan, out g))
            {
                g = new TypeGroup { Plan = plan };
                byPlan[plan] = g;
                groups.Add(g);
            }
            g.Members.Add(a);
        }
        groups.Sort((x, y) => string.Compare(x.Plan, y.Plan, System.StringComparison.Ordinal));
        return groups;
    }

    private void OnGUI()
    {
        if (commander == null || minimap == null) { PointerOver = false; return; }

        var groups = GatherGroups();
        if (groups.Count == 0) { PointerOver = false; return; }
        var prevMatrix = UiScale.Begin();

        var mapRect = minimap.ScreenRect;
        var totalWidth = groups.Count * iconSize + (groups.Count - 1) * iconGap;
        var startX = mapRect.xMax + dockGapPixels;
        var y = mapRect.yMax - iconSize;

        var barRect = new Rect(startX - iconGap, y - iconGap, totalWidth + iconGap * 2f, iconSize + iconGap * 2f);
        var e = Event.current;
        PointerOver = e != null && barRect.Contains(e.mousePosition);

        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var x = startX;
        foreach (var g in groups)
        {
            var rect = new Rect(x, y, iconSize, iconSize);
            DrawIcon(rect, g);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) commander.SetSelection(g.Members);
            x += iconSize + iconGap;
        }

        UiScale.End(prevMatrix);
    }

    private void DrawIcon(Rect rect, TypeGroup g)
    {
        GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        const float inset = 2f;
        var innerRect = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        GUI.color = MonsterBody.PlanColor(g.Plan);
        GUI.DrawTexture(innerRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowedLabel(new Rect(rect.x, rect.y + rect.height - 14f, rect.width, 14f), AbbrevFor(g.Plan), Color.white, TextAnchor.MiddleCenter);

        if (g.Members.Count > 1)
            DrawShadowedLabel(new Rect(rect.x, rect.y - 2f, rect.width, 14f), "x" + g.Members.Count, new Color(0.95f, 0.9f, 0.7f, 1f), TextAnchor.UpperRight);
    }

    /// <summary>Matches genome-core's catalog.ts BODY_PLANS keys exactly
    /// (docs/06's normative-schema rule -- this HUD reads the same plan
    /// strings the genome schema itself defines).</summary>
    private static string AbbrevFor(string plan)
    {
        switch (plan)
        {
            case "tetrapod": return "Tet";
            case "blob": return "Blb";
            case "serpentine": return "Srp";
            case "winged": return "Wng";
            case "crab": return "Crb";
            case "arachnid": return "Arc";
            case "avian": return "Avn";
            case "treant": return "Tre";
            case "floater": return "Flt";
            default: return "?";
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
