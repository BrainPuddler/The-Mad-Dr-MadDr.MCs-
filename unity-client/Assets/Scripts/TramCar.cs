using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The mood-board's streetcar (see <see cref="TramDresser"/>'s own
/// header for the full "why"). Deliberately a much simpler vehicle than
/// <see cref="TrafficCar"/> -- a streetcar is rail-bound: no wander, no
/// park/depart cycle, no roundabout circulation, no swerving around a
/// threat (it physically can't leave the rails). It just runs the fixed
/// <see cref="TramDresser.Build"/> path back and forth forever, at a
/// slower, steadier speed than road traffic, and pauses in place --
/// rather than driving through -- if something is standing in its way,
/// since rerouting isn't an option for a vehicle on rails.
/// </summary>
public class TramCar : MonoBehaviour
{
    private const float CruiseSpeed = 4.5f;      // slower and steadier than TrafficCar's 6.5
    private const float ArriveRadius = 0.75f;
    private const float BlockCheckRadius = 6f;   // pause if a monster is at least this close, directly ahead

    private RuntimeCityBuilder _builder;
    private List<Vector3> _path;
    private int _index;
    private int _dir = 1;   // +1 = walking the path forward, -1 = reverse

    public void Init(RuntimeCityBuilder builder, List<Vector3> path, int startIndex)
    {
        _builder = builder;
        _path = path;
        _index = Mathf.Clamp(startIndex, 0, path.Count - 1);
        transform.position = path[_index];
        BuildBody();
    }

    private void BuildBody()
    {
        // a real streetcar: tall, boxy, single-unit -- closer to
        // TrafficCar's delivery-truck body than its sedan, since a
        // period streetcar reads as a tall rectangular car, not a
        // sloped-roof automobile
        transform.localScale = new Vector3(2.7f, 3.1f, 11f);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = new Color(0.62f, 0.14f, 0.13f);   // period streetcar red livery
        var renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        var windowMat = new Material(ShaderUtil.FindRenderableShader());
        windowMat.color = new Color(0.14f, 0.16f, 0.2f);
        var windowBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        windowBand.name = "WindowBand";
        windowBand.transform.SetParent(transform, false);
        windowBand.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        windowBand.transform.localScale = new Vector3(1.03f, 0.32f, 0.94f);
        var windowRenderer = windowBand.GetComponent<Renderer>();
        if (windowRenderer != null) windowRenderer.sharedMaterial = windowMat;
        var windowCollider = windowBand.GetComponent<Collider>();
        if (windowCollider != null) Object.Destroy(windowCollider);
    }

    private void Update()
    {
        if (_builder == null || _path == null || _path.Count < 2) return;
        var dt = Time.deltaTime;

        var nextIndex = _index + _dir;
        if (nextIndex < 0 || nextIndex >= _path.Count)
        {
            _dir = -_dir;
            nextIndex = _index + _dir;
        }
        var target = _path[nextIndex];

        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < ArriveRadius) { _index = nextIndex; return; }
        var dir3 = to / dist;

        // rail-bound: pause rather than swerve if a monster is standing
        // in the way just ahead -- resumes on its own once clear.
        var lookAhead = transform.position + dir3 * (BlockCheckRadius * 0.6f);
        if (_builder.NearestMonsterTo(lookAhead, BlockCheckRadius) != null) return;

        transform.position += dir3 * Mathf.Min(CruiseSpeed * dt, dist);
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p) + 0.95f, p.z);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir3, Vector3.up), dt * 3f);
    }
}
