using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// RTS camera, StarCraft-style. New Input System API only (see
/// WaypointCommander's header for why the legacy Input class is off-limits
/// in this project).
///
///   WASD / arrows      : pan (relative to view yaw)
///   SHIFT + up/down    : move the camera straight up/down (world Y),
///                        clamped so it can never go below MinHeight --
///                        overrides the plain up/down-arrow pan for as
///                        long as Shift is held
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
    public float verticalMoveSpeed = 40f;   // Shift+up/down, in units/sec
    public float edgeScrollMargin = 14f;   // pixels from an edge that trigger auto-scroll
    public float focusLerp = 9f;            // how snappily FocusOn glides to its target

    // 2026-07 creator direction: "shift+down/up arrow moves the camera
    // down/up. Do not allow moving below the ground." Shared with zoom's
    // own existing height clamp (previously two separate `8f`/`400f`
    // literals) so both ways of changing height agree on the same floor/
    // ceiling instead of drifting apart. MinHeight isn't 0 -- ground is
    // y=0 (docs/18: "feet stand on y=0"), and a camera sitting exactly
    // ON the ground plane is degenerate (near-zero vertical FOV, mostly
    // clipping) -- 8 units up is the practical "as low as this camera
    // can usefully go," already the zoom code's own floor before this.
    private const float MinHeight = 8f;
    private const float MaxHeight = 400f;

    // 2026-07 creator direction: "limit the shadows to objects in the
    // camera view and close to the visible area." Shadow distance was a
    // fixed value in the URP Pipeline Asset (docs/28 row 22 -- sized for
    // the DEFAULT camera framing), which means it was either too short
    // for a zoomed-out overview or wastefully long for the much more
    // common close/medium zoom levels most RTS play actually happens at
    // -- URP has to cull/draw shadow casters out to whatever that
    // distance is, every frame, regardless of how much of it is actually
    // useful at the current zoom. Tied to camera height (this
    // component's own existing zoom proxy, see panSpeed's `scale` below)
    // via the SAME geometry SnapTo itself uses -- camera offset is
    // `(0, h, -h*0.8)`, so straight-line camera-to-focus distance is
    // `h * sqrt(1 + 0.8^2) = h * 1.28` at any height reached that way
    // (scroll-zoom moves along a very similar ~50 deg pitch, close
    // enough to reuse the same ratio). `shadowDistancePerHeight` is that
    // 1.28 with margin on top, since the visible ground extends past
    // just the exact center point out to the frustum's far edge.
    //
    // Deliberately proportional-with-a-cap, NOT a straight Lerp across
    // the full [MinHeight, MaxHeight] range: distance-to-ground is
    // linear in height all the way out to MaxHeight=400 (h*1.28=512),
    // but shadow distance shouldn't keep growing that far -- individual
    // shadows are visually tiny at extreme zoom-out anyway, and URP's
    // cascades would spread thinner (blockier) over an ever-larger area
    // for no visible benefit. `shadowDistanceCap` holds the line past
    // whatever height that cap works out to (~124 with these defaults).
    [Tooltip("Shadow distance (meters) per meter of camera height -- ~1.28 (the exact SnapTo camera-to-ground ratio) plus margin so the covered area extends to the actual visible frustum, not just the exact center point.")]
    public float shadowDistancePerHeight = 1.9f;
    [Tooltip("Flat floor added on top, so extreme close zoom doesn't shrink shadow distance to something so tight it flickers/pops in and out.")]
    public float shadowDistanceFloor = 15f;
    [Tooltip("Hard cap regardless of zoom -- keeps shadow distance (and cascade quality) from degrading at extreme zoom-out, where individual shadows are visually tiny anyway.")]
    public float shadowDistanceCap = 250f;

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
        UpdateShadowDistance();   // avoid a one-frame flash of whatever the asset's own default was
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

        var shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

        // yaw-relative pan: keyboard plus edge-scroll fold into one vector.
        // Up/down arrows are excluded while Shift is held -- Shift steals
        // them for the vertical move below instead of also panning
        // forward/back at the same time.
        var pan = Vector3.zero;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || (keyboard.upArrowKey.isPressed && !shiftHeld)) pan.z += 1f;
            if (keyboard.sKey.isPressed || (keyboard.downArrowKey.isPressed && !shiftHeld)) pan.z -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
        }

        // Shift + up/down arrow: move the camera straight up/down (world
        // Y), clamped to the same [MinHeight, MaxHeight] band zoom uses --
        // "do not allow moving below the ground" (2026-07 creator
        // direction).
        if (keyboard != null && shiftHeld)
        {
            var vertical = 0f;
            if (keyboard.upArrowKey.isPressed) vertical += 1f;
            if (keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (Mathf.Abs(vertical) > 0.01f)
            {
                var pos = transform.position;
                pos.y = Mathf.Clamp(pos.y + vertical * verticalMoveSpeed * dt, MinHeight, MaxHeight);
                transform.position = pos;
                manual = true;
            }
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
                if (newPos.y > MinHeight && newPos.y < MaxHeight) { transform.position = newPos; manual = true; }
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

        UpdateShadowDistance();
    }

    /// <summary>Shrinks/grows URP's shadow distance with the camera's
    /// current zoom (height) instead of leaving it at one fixed value
    /// sized for the worst case -- see the field comments above. A plain
    /// float write on the active pipeline asset every frame; cheap next
    /// to the pan/zoom math already running here, no need to throttle.</summary>
    private void UpdateShadowDistance()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;
        var proportional = transform.position.y * shadowDistancePerHeight + shadowDistanceFloor;
        urpAsset.shadowDistance = Mathf.Min(proportional, shadowDistanceCap);
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
