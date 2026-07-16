using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// Docs/19 traffic (docs/21 batch 2, item 9): a car that drives the road
/// network -- hopping between adjacent road/bridge-deck hexes -- and
/// panics like a Citizen when a monster gets close, peeling off toward
/// whichever reachable hex is farthest from the threat instead of its
/// usual wander pick. Not a combatant: no collider, doesn't fight,
/// doesn't block movement -- purely cosmetic crowd dressing for the
/// streets RoadDresser paints, same scoping Citizen.cs already
/// established for pedestrians.
/// </summary>
public class TrafficCar : MonoBehaviour
{
    private const float CruiseSpeed = 6.5f;
    private const float FleeSpeed = 11f;
    private const float FleeRadius = 16f;
    private const float ArriveRadius = 1.5f;

    private RuntimeCityBuilder _builder;
    private HashSet<HexCoord> _network;
    private HexCoord _from;
    private HexCoord _to;
    private Vector3 _target;
    private bool _fleeing;

    public void Init(RuntimeCityBuilder builder, HashSet<HexCoord> network, HexCoord start, Color body)
    {
        _builder = builder;
        _network = network;
        _from = start;
        _to = start;
        transform.position = _builder.WorldOf(start) + Vector3.up * 0.75f;
        _target = transform.position;
        BuildBody(body);
        PickNext();
    }

    private void BuildBody(Color body)
    {
        // this component sits on the chassis primitive itself -- one
        // extra cube for the cabin, no bumpers/fins (moving fast enough
        // that the extra parked-car detail wouldn't read from RTS height)
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = body;
        var renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        transform.localScale = new Vector3(2.2f, 0.8f, 5.2f);

        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(transform, false);
        cabin.transform.localPosition = new Vector3(0f, 0.5f, -0.25f);
        cabin.transform.localScale = new Vector3(0.85f, 0.85f, 0.42f);
        var cabinRenderer = cabin.GetComponent<Renderer>();
        if (cabinRenderer != null) cabinRenderer.sharedMaterial = mat;
        var cabinCollider = cabin.GetComponent<Collider>();
        if (cabinCollider != null) Object.Destroy(cabinCollider);
    }

    /// <summary>Pick the next network hex from `_to`. Wandering (no
    /// avoid/threat) picks a stable pseudo-random neighbor; fleeing picks
    /// whichever reachable neighbor is farthest from the threat position,
    /// preferring not to double back over `avoid` unless it's the only
    /// way out.</summary>
    private void PickNext(HexCoord? avoid = null, Vector3 awayFrom = default(Vector3))
    {
        var best = _to;
        var bestScore = float.NegativeInfinity;
        var found = false;
        foreach (var n in _to.Neighbors())
        {
            if (!_network.Contains(n)) continue;
            if (avoid.HasValue && n == avoid.Value) continue;
            var score = avoid.HasValue
                ? (_builder.WorldOf(n) - awayFrom).sqrMagnitude
                : ((n.Q * 928371 + n.R * 128371 + GetInstanceID()) & 0xFFFF);
            if (!found || score > bestScore) { bestScore = score; best = n; found = true; }
        }
        if (!found)
        {
            // dead end, or fully boxed by the avoid filter -- fall back to
            // any network neighbor, doubling back if that's the only option
            foreach (var n in _to.Neighbors())
                if (_network.Contains(n)) { best = n; found = true; break; }
        }
        if (!found) return; // isolated hex -- shouldn't happen on a generated road network
        _from = _to;
        _to = best;
        _target = _builder.WorldOf(_to) + Vector3.up * 0.75f;
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        var threat = _builder.NearestMonsterTo(transform.position, FleeRadius);
        var speed = CruiseSpeed;

        if (threat != null)
        {
            speed = FleeSpeed;
            if (!_fleeing) PickNext(_from, threat.transform.position); // threat just appeared: redirect now
            _fleeing = true;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < ArriveRadius) PickNext(_from, threat.transform.position);
        }
        else
        {
            _fleeing = false;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude < ArriveRadius) PickNext();
        }

        MoveToward(_target, speed, dt);
    }

    private void MoveToward(Vector3 target, float speed, float dt)
    {
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 0.05f) return;
        var dir = to / dist;
        transform.position += dir * Mathf.Min(speed * dt, dist);
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p) + 0.75f, p.z);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 4f);
    }
}
