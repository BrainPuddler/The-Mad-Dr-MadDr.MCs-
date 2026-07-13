using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal RTS camera so the battlefield is actually explorable in Play
/// mode: WASD/arrows pan (relative to view yaw), Q/E rotate, scroll
/// zooms. New Input System API only (see WaypointCommander's header for
/// why the legacy Input class is off-limits in this project).
/// </summary>
public class SimpleCameraRig : MonoBehaviour
{
    public float panSpeed = 60f;
    public float rotateSpeed = 90f;
    public float zoomSpeed = 8f;

    private float _yaw;

    public void SnapTo(Vector3 focus, float distance)
    {
        _yaw = 0f;
        transform.position = focus + new Vector3(0f, distance, -distance * 0.8f);
        transform.rotation = Quaternion.Euler(50f, _yaw, 0f);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var dt = Time.deltaTime;

        if (keyboard != null)
        {
            var pan = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.z -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;

            if (keyboard.qKey.isPressed) _yaw -= rotateSpeed * dt;
            if (keyboard.eKey.isPressed) _yaw += rotateSpeed * dt;

            if (pan.sqrMagnitude > 0.01f)
            {
                var yawRot = Quaternion.Euler(0f, _yaw, 0f);
                var move = yawRot * new Vector3(pan.x, 0f, pan.z);
                // pan faster when zoomed out -- height is the zoom proxy
                var scale = Mathf.Max(0.4f, transform.position.y / 60f);
                transform.position += move * panSpeed * scale * dt;
            }
            transform.rotation = Quaternion.Euler(50f, _yaw, 0f);
        }

        if (mouse != null)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var newPos = transform.position + transform.forward * scroll * zoomSpeed * 0.02f;
                if (newPos.y > 8f && newPos.y < 400f) transform.position = newPos;
            }
        }
    }
}
