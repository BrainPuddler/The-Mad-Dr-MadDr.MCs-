using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RTS camera, StarCraft-style. New Input System API only (see
/// WaypointCommander's header for why the legacy Input class is off-limits
/// in this project).
///
///   WASD / arrows      : pan (relative to view yaw)
///   Q / E              : rotate
///   scroll wheel       : zoom
///   MIDDLE-mouse drag  : grab-pan -- the ground point you grab stays
///                        pinned under the cursor while you drag (map
///                        drags with the mouse, like SC2's middle-drag)
///   screen edges       : auto-scroll when the cursor rests near an edge
///   (the G-to-nearest-unit jump lives in WaypointCommander, which has
///    the monster list, and calls FocusOn here)
/// </summary>
public class SimpleCameraRig : MonoBehaviour
{
    public float panSpeed = 60f;
    public float rotateSpeed = 90f;
    public float zoomSpeed = 8f;
    public float edgeScrollMargin = 14f;   // pixels from an edge that trigger auto-scroll
    public float focusLerp = 9f;            // how snappily FocusOn glides to its target

    private float _yaw;
    private Camera _cam;

    private bool _dragging;
    private Vector3 _grabWorld;      // the ground point pinned under the cursor mid-drag

    private bool _focusing;
    private Vector3 _focusTarget;    // XZ goal the camera is gliding toward (Y ignored)

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    public void SnapTo(Vector3 focus, float distance)
    {
        _yaw = 0f;
        _focusing = false;
        _dragging = false;
        transform.position = focus + new Vector3(0f, distance, -distance * 0.8f);
        transform.rotation = Quaternion.Euler(50f, _yaw, 0f);
    }

    /// <summary>Glide the camera so `worldPoint` ends up centered, keeping
    /// the current yaw, pitch, and height. Used by the G-key jump-to-
    /// nearest-unit.</summary>
    public void FocusOn(Vector3 worldPoint)
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        Vector3 groundAtCenter;
        if (!GroundUnderScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out groundAtCenter))
        {
            groundAtCenter = transform.position;
            groundAtCenter.y = 0f;
        }
        var delta = worldPoint - groundAtCenter;
        _focusTarget = new Vector3(transform.position.x + delta.x, transform.position.y,
            transform.position.z + delta.z);
        _focusing = true;
    }

    private void Update()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var dt = Time.deltaTime;
        var manual = false;

        if (keyboard != null)
        {
            if (keyboard.qKey.isPressed) { _yaw -= rotateSpeed * dt; manual = true; }
            if (keyboard.eKey.isPressed) { _yaw += rotateSpeed * dt; manual = true; }
        }

        // yaw-relative pan: keyboard plus edge-scroll fold into one vector
        var pan = Vector3.zero;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.z -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
        }

        // edge auto-scroll -- suppressed while grab-dragging (the drag owns
        // the mouse) and while the cursor is outside the window
        if (!_dragging && mouse != null && Application.isFocused)
        {
            var p = mouse.position.ReadValue();
            if (p.x >= 0f && p.x <= Screen.width && p.y >= 0f && p.y <= Screen.height)
            {
                if (p.x < edgeScrollMargin) pan.x -= 1f;
                else if (p.x > Screen.width - edgeScrollMargin) pan.x += 1f;
                if (p.y < edgeScrollMargin) pan.z -= 1f;
                else if (p.y > Screen.height - edgeScrollMargin) pan.z += 1f;
            }
        }

        if (pan.sqrMagnitude > 0.01f)
        {
            manual = true;
            if (pan.sqrMagnitude > 1f) pan = pan.normalized;   // no diagonal speed-up
            var move = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(pan.x, 0f, pan.z);
            // pan faster when zoomed out -- height is the zoom proxy
            var scale = Mathf.Max(0.4f, transform.position.y / 60f);
            transform.position += move * panSpeed * scale * dt;
        }

        if (mouse != null)
        {
            // MIDDLE-mouse grab-pan: pin the grabbed ground point under the
            // cursor. Recomputed every frame against the just-panned camera,
            // so it composes cleanly with keyboard/edge motion above.
            if (mouse.middleButton.wasPressedThisFrame)
                _dragging = GroundUnderScreen(mouse.position.ReadValue(), out _grabWorld);
            if (!mouse.middleButton.isPressed)
                _dragging = false;
            if (_dragging)
            {
                Vector3 cur;
                if (GroundUnderScreen(mouse.position.ReadValue(), out cur))
                {
                    var delta = _grabWorld - cur;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > 1e-8f)
                    {
                        transform.position += delta;
                        manual = true;
                    }
                }
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var newPos = transform.position + transform.forward * scroll * zoomSpeed * 0.02f;
                if (newPos.y > 8f && newPos.y < 400f) { transform.position = newPos; manual = true; }
            }
        }

        // any hand-driven motion cancels an in-flight FocusOn glide
        if (manual) _focusing = false;
        if (_focusing)
        {
            var cur = transform.position;
            var goal = new Vector3(_focusTarget.x, cur.y, _focusTarget.z);
            transform.position = Vector3.Lerp(cur, goal, 1f - Mathf.Exp(-focusLerp * dt));
            var dx = transform.position.x - _focusTarget.x;
            var dz = transform.position.z - _focusTarget.z;
            if (dx * dx + dz * dz < 0.04f)
            {
                transform.position = new Vector3(_focusTarget.x, cur.y, _focusTarget.z);
                _focusing = false;
            }
        }

        transform.rotation = Quaternion.Euler(50f, _yaw, 0f);
    }

    /// <summary>Where the ray through a screen point meets the y=0 ground
    /// plane (feet stand on y=0, docs/18). False for a ray that never dips
    /// below the horizon.</summary>
    private bool GroundUnderScreen(Vector2 screenPos, out Vector3 world)
    {
        world = Vector3.zero;
        if (_cam == null) return false;
        var ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
        var t = -ray.origin.y / ray.direction.y;
        if (t <= 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }
}
