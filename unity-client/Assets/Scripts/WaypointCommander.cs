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

    private RaycastHit? RaycastCursor(Camera cam, Mouse mouse)
    {
        var pos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5000f)) return hit;
        return null;
    }
}
