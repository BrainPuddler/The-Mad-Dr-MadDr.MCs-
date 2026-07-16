using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse orders, StarCraft 2 control model. New Input System API
/// exclusively -- this project's activeInputHandler is set to Input
/// System Package only, so the legacy UnityEngine.Input class would throw
/// at runtime.
///
///   Left click          : select one monster (empty ground = deselect)
///   Left DRAG            : marquee box-select every unit in the rectangle
///   Left DOUBLE-click    : select all units of that type on screen
///   Shift + left (any of : add to the current selection instead of
///     the above)           replacing it (double-click adds all of type)
///   Right click          : order the WHOLE selection --
///   (or Ctrl + left click,  on a citizen  -> chase and eat it
///    trackpad support)      on a building WALL -> walk to it and attack
///                            on a building ROOF -> winged units fly to it
///                              and land (perch); everyone else attacks
///                            on the ground -> waypoint (Shift queues)
///   G                    : glide the camera to the unit nearest the cursor
/// </summary>
public class WaypointCommander : MonoBehaviour
{
    private const float DragThresholdSq = 36f;    // 6 px: below this a press is a click, not a box
    private const float DoubleClickTime = 0.35f;
    private const float DoubleClickDistSq = 100f;  // 10 px: a second click must land near the first

    private RuntimeCityBuilder _builder;
    private readonly List<MonsterAgent> _selected = new List<MonsterAgent>();

    // left-drag marquee state
    private bool _leftDown;
    private Vector2 _dragStart;

    // double-click detection
    private float _lastClickTime = -1f;
    private Vector2 _lastClickPos;

    /// <summary>The selection's lead unit (first picked) -- what the HUD
    /// details. Null when nothing is selected.</summary>
    public MonsterAgent SelectedAgent { get { return _selected.Count > 0 ? _selected[0] : null; } }
    public int SelectedCount { get { return _selected.Count; } }

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _builder == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var keyboard = Keyboard.current;

        if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            JumpToNearestUnit(cam, mouse);

        // trackpad support: Ctrl+left-click stands in for a right click
        // (mirrors macOS's own Control-click-for-secondary-click
        // convention, but works the same on any OS/pointer that lacks a
        // real right button).
        var ctrlHeld = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

        HandleSelection(cam, mouse, keyboard, ctrlHeld);
        HandleOrders(cam, mouse, keyboard, ctrlHeld);
    }

    // ---- selection (left button) --------------------------------------------

    private void HandleSelection(Camera cam, Mouse mouse, Keyboard keyboard, bool ctrlHeld)
    {
        // Ctrl+left is claimed by the right-click stand-in above -- never
        // let it start a selection click/drag too.
        if (mouse.leftButton.wasPressedThisFrame && !ctrlHeld)
        {
            _leftDown = true;
            _dragStart = mouse.position.ReadValue();
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !_leftDown) return;
        _leftDown = false;

        var up = mouse.position.ReadValue();
        var additive = keyboard != null && keyboard.leftShiftKey.isPressed;

        if ((up - _dragStart).sqrMagnitude > DragThresholdSq)
        {
            // a drag: marquee box-select
            var hits = UnitsInBox(cam, ScreenRect(_dragStart, up));
            if (additive) AddToSelection(hits); else SetSelection(hits);
            return;
        }

        // a click: single-select, with double-click -> select-all-of-type
        var agent = AgentUnderCursor(cam, mouse);
        var now = Time.unscaledTime;
        var isDouble = agent != null
            && now - _lastClickTime < DoubleClickTime
            && (up - _lastClickPos).sqrMagnitude < DoubleClickDistSq;
        _lastClickTime = now;
        _lastClickPos = up;

        if (agent == null)
        {
            if (!additive) ClearSelection();   // Shift+click empty keeps the current group
            return;
        }
        if (isDouble)
        {
            var ofType = UnitsOfTypeOnScreen(cam, agent.BodyPlan);
            if (additive) AddToSelection(ofType); else SetSelection(ofType);
        }
        else if (additive)
        {
            ToggleSelection(agent);
        }
        else
        {
            SetSelection(new List<MonsterAgent> { agent });
        }
    }

    // ---- orders (right button, or Ctrl+left for trackpads) ------------------

    private void HandleOrders(Camera cam, Mouse mouse, Keyboard keyboard, bool ctrlHeld)
    {
        var ordered = mouse.rightButton.wasPressedThisFrame
            || (ctrlHeld && mouse.leftButton.wasPressedThisFrame);
        if (!ordered) return;
        PruneSelection();
        if (_selected.Count == 0) return;

        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return;

        var enemy = hit.Value.collider.GetComponentInParent<Tank>();
        if (enemy != null && enemy.Combat != null)
        {
            foreach (var a in _selected) a.OrderAttackUnit(enemy.Combat);
            return;
        }

        var citizen = hit.Value.collider.GetComponentInParent<Citizen>();
        if (citizen != null)
        {
            foreach (var a in _selected) a.OrderEat(citizen);
            return;
        }

        var building = _builder.BuildingFromCollider(hit.Value.collider);
        if (building != null)
        {
            // WHERE on the building you clicked matters: the flat ROOF
            // (upward-facing surface) sends winged units to land on it,
            // while a WALL is an attack order for everyone -- so both
            // verbs stay reachable with a plain right-click and no extra
            // modifier key. Ground units can't perch, so a roof-click is
            // still just an attack for them.
            var roofClick = hit.Value.normal.y > 0.5f;
            foreach (var a in _selected)
            {
                if (roofClick && a.IsFlyer) a.OrderPerch(building);
                else a.OrderAttack(building);
            }
            return;
        }

        // ground: a waypoint for the whole group. Shift queues. A group
        // spreads into a formation around the spot (one hex each) while
        // WALKING, then creeps in close together once everyone's stopped
        // (see OrderMove's settleTarget -- MonsterAgent.TickSettle).
        var hex = _builder.HexAt(hit.Value.point);
        if (_builder.City.Contains(hex))
        {
            var shift = keyboard != null && keyboard.leftShiftKey.isPressed;
            if (_selected.Count == 1)
                _selected[0].OrderMove(hex, shift);
            else
                AssignFormation(_builder.FormationHexes(hex, _selected.Count), shift, hit.Value.point);
            _builder.SpawnWaypointMarker(_builder.WorldOf(hex));
        }
    }

    /// <summary>Hand out formation slots to the selected group,
    /// nearest-slot-to-nearest-unit, so units mostly walk straight to
    /// their spot instead of crossing paths. `clusterPoint` is where each
    /// unit creeps toward once it arrives and stops -- the fix for a
    /// stopped group looking too spread out (FormationHexes only
    /// guarantees WALKING doesn't collide, a full hex apart).</summary>
    private void AssignFormation(System.Collections.Generic.List<MadDr.CityGen.HexCoord> slots, bool queue,
        Vector3 clusterPoint)
    {
        var remaining = new System.Collections.Generic.List<MonsterAgent>(_selected);
        foreach (var slot in slots)
        {
            if (remaining.Count == 0) break;
            var slotW = _builder.WorldOf(slot);
            var best = -1;
            var bestSq = float.MaxValue;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] == null) continue;
                var d = remaining[i].transform.position - slotW;
                d.y = 0f;
                if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = i; }
            }
            if (best < 0) break;
            var unit = remaining[best];
            remaining.RemoveAt(best);
            unit.OrderMove(slot, queue, clusterPoint);
        }
    }

    // ---- selection set management -------------------------------------------

    private void SetSelection(List<MonsterAgent> agents)
    {
        foreach (var a in _selected) if (a != null) a.SetSelected(false);
        _selected.Clear();
        foreach (var a in agents)
            if (a != null && !_selected.Contains(a)) { _selected.Add(a); a.SetSelected(true); }
    }

    private void AddToSelection(List<MonsterAgent> agents)
    {
        foreach (var a in agents)
            if (a != null && !_selected.Contains(a)) { _selected.Add(a); a.SetSelected(true); }
    }

    private void ToggleSelection(MonsterAgent agent)
    {
        if (_selected.Remove(agent)) { agent.SetSelected(false); return; }
        _selected.Add(agent);
        agent.SetSelected(true);
    }

    private void ClearSelection()
    {
        foreach (var a in _selected) if (a != null) a.SetSelected(false);
        _selected.Clear();
    }

    /// <summary>Drop any units that died/despawned since last frame so
    /// group orders never dereference a destroyed agent.</summary>
    private void PruneSelection()
    {
        for (var i = _selected.Count - 1; i >= 0; i--)
            if (_selected[i] == null) _selected.RemoveAt(i);
    }

    // ---- picking helpers -----------------------------------------------------

    private List<MonsterAgent> UnitsInBox(Camera cam, Rect boxBottomLeft)
    {
        var hits = new List<MonsterAgent>();
        foreach (var m in _builder.Monsters)
        {
            if (m == null) continue;
            var sp = cam.WorldToScreenPoint(m.transform.position);
            if (sp.z <= 0f) continue;   // behind the camera
            if (boxBottomLeft.Contains(new Vector2(sp.x, sp.y))) hits.Add(m);
        }
        return hits;
    }

    private List<MonsterAgent> UnitsOfTypeOnScreen(Camera cam, string plan)
    {
        var hits = new List<MonsterAgent>();
        foreach (var m in _builder.Monsters)
        {
            if (m == null || m.BodyPlan != plan) continue;
            var sp = cam.WorldToScreenPoint(m.transform.position);
            if (sp.z <= 0f) continue;
            if (sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height) hits.Add(m);
        }
        return hits;
    }

    private MonsterAgent AgentUnderCursor(Camera cam, Mouse mouse)
    {
        var hit = RaycastCursor(cam, mouse);
        if (!hit.HasValue || hit.Value.collider == null) return null;
        return hit.Value.collider.GetComponentInParent<MonsterAgent>();
    }

    /// <summary>Normalized (positive width/height) rect from two screen
    /// corners, in the bottom-left origin space both Mouse.position and
    /// Camera.WorldToScreenPoint use.</summary>
    private static Rect ScreenRect(Vector2 a, Vector2 b)
    {
        var x = Mathf.Min(a.x, b.x);
        var y = Mathf.Min(a.y, b.y);
        return new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    // ---- G: jump camera to nearest unit -------------------------------------

    /// <summary>G-key: find the monster closest to whatever the cursor is
    /// over and glide the camera to it. "Over" is the physics hit under the
    /// cursor when there is one (a unit, a building, the ground), falling
    /// back to the y=0 ground plane the ray crosses, then to the camera
    /// itself if the ray never dips below the horizon.</summary>
    private void JumpToNearestUnit(Camera cam, Mouse mouse)
    {
        var rig = cam.GetComponent<SimpleCameraRig>();
        if (rig == null) return;

        Vector3 aim;
        var hit = RaycastCursor(cam, mouse);
        if (hit.HasValue) aim = hit.Value.point;
        else if (!GroundUnderCursor(cam, mouse, out aim)) aim = cam.transform.position;

        // 1e6 is an effectively-unbounded search radius (the city is a few
        // hundred units across); NearestMonsterTo compares squared, which
        // stays well within float range.
        var nearest = _builder.NearestMonsterTo(aim, 1e6f);
        if (nearest != null) rig.FocusOn(nearest.transform.position);
    }

    private static bool GroundUnderCursor(Camera cam, Mouse mouse, out Vector3 world)
    {
        world = Vector3.zero;
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
        var t = -ray.origin.y / ray.direction.y;
        if (t <= 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }

    private RaycastHit? RaycastCursor(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5000f)) return hit;
        return null;
    }

    // ---- selection marquee overlay ------------------------------------------

    private static Texture2D _boxTex;

    private void OnGUI()
    {
        if (!_leftDown) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        var cur = mouse.position.ReadValue();
        if ((cur - _dragStart).sqrMagnitude <= DragThresholdSq) return;

        if (_boxTex == null) _boxTex = Texture2D.whiteTexture;

        // GUI space is top-left origin; screen space is bottom-left --
        // flip Y for the on-screen rectangle
        var r = ScreenRect(_dragStart, cur);
        var gui = new Rect(r.x, Screen.height - (r.y + r.height), r.width, r.height);

        var fill = new Color(0.3f, 1f, 0.5f, 0.12f);
        var edge = new Color(0.35f, 1f, 0.55f, 0.9f);
        GUI.color = fill;
        GUI.DrawTexture(gui, _boxTex);
        GUI.color = edge;
        const float t = 1.5f;
        GUI.DrawTexture(new Rect(gui.x, gui.y, gui.width, t), _boxTex);
        GUI.DrawTexture(new Rect(gui.x, gui.yMax - t, gui.width, t), _boxTex);
        GUI.DrawTexture(new Rect(gui.x, gui.y, t, gui.height), _boxTex);
        GUI.DrawTexture(new Rect(gui.xMax - t, gui.y, t, gui.height), _boxTex);
        GUI.color = Color.white;
    }
}
