using MadDr.CityGen;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse orders, RTS-style. Uses the NEW Input System API exclusively --
/// this project's activeInputHandler is set to Input System Package
/// only, so the legacy UnityEngine.Input class would throw at runtime.
///
///   Left click  : select a monster (its ring lights up)
///   Right click : with a selection --
///                   on a citizen  -> target-lock: chase and eat it
///                   on a building -> target-lock: walk to it and attack
///                   on the ground -> waypoint (hold Shift to queue)
///   G           : glide the camera to the monster nearest the cursor
///                 (the camera rig owns the actual pan; this just finds
///                  the unit -- it has the monster list, the rig doesn't)
/// </summary>
public class WaypointCommander : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private MonsterAgent _selected;

    public MonsterAgent SelectedAgent { get { return _selected; } }

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

        if (mouse.leftButton.wasPressedThisFrame)
        {
            var hit = RaycastCursor(cam, mouse);
            if (hit.HasValue)
            {
                var agent = hit.Value.collider != null
                    ? hit.Value.collider.GetComponentInParent<MonsterAgent>() : null;
                if (agent != null)
                {
                    if (_selected != null) _selected.SetSelected(false);
                    _selected = agent;
                    _selected.SetSelected(true);
                }
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && _selected != null)
        {
            var hit = RaycastCursor(cam, mouse);
            if (!hit.HasValue || hit.Value.collider == null) return;

            var citizen = hit.Value.collider.GetComponentInParent<Citizen>();
            if (citizen != null)
            {
                _selected.OrderEat(citizen);
                return;
            }

            var building = _builder.BuildingFromCollider(hit.Value.collider);
            if (building != null)
            {
                _selected.OrderAttack(building);
                return;
            }

            // ground: a waypoint. Shift queues instead of replacing.
            var hex = _builder.HexAt(hit.Value.point);
            if (_builder.City.Contains(hex))
            {
                var keyboard = Keyboard.current;
                var shift = keyboard != null && keyboard.leftShiftKey.isPressed;
                _selected.OrderMove(hex, shift);
                _builder.SpawnWaypointMarker(_builder.WorldOf(hex));
            }
        }
    }

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
}
