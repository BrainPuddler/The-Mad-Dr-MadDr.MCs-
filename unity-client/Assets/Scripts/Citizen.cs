using System.Collections.Generic;
using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// A Citizen (docs/19): client-side cosmetic crowd, not a synced combat
/// entity -- exactly the doc's own scoping ("Citizens run client-side
/// cosmetic/crowd AI, not server-synced"); edible. Eating one pays
/// docs/20's per-citizen yield (Blood 2 / Bones 1 / Brains 1) into the
/// session wallet.
///
/// Movement (creator direction, 2026-07): a citizen has a DESTINATION (a
/// sidewalk hex somewhere across town) rather than aimless wander, and
/// MUST stay on the sidewalk -- the walkable strip bordering the streets
/// (RuntimeCityBuilder.IsSidewalkHex) -- stepping onto a road hex only at
/// a corner to CROSS. The one exception is fleeing a monster (docs/19 SS3
/// Passive band, always flees): panic ignores sidewalks entirely and it
/// runs anywhere open.
/// </summary>
public class Citizen : MonoBehaviour
{
    private const float WalkSpeed = 1.4f;   // docs/19 SS4: civilian amble, ~0.07 hex/s scale
    private const float FleeSpeed = 4.2f;   // panic sprint
    private const float FleeRadius = 14f;
    private const int DestinationRadius = 40;   // hexes -- a real cross-town errand, not a shuffle in place

    private RuntimeCityBuilder _builder;
    private Vector3 _target;      // the immediate next-step point
    private HexCoord _destination;
    private bool _hasDestination;
    private float _repickTimer;
    private int _stepSalt;

    public void Init(RuntimeCityBuilder builder, HexCoord home)
    {
        _builder = builder;
        transform.position = _builder.WorldOf(home) + new Vector3(0f, 0.9f, 0f);
        _target = transform.position;

        // a little person: capsule body + head, tinted civilian colors
        transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(ShaderUtil.FindRenderableShader());
            var hue = (GetInstanceID() % 100 + 100) % 100 / 100f;
            mat.color = Color.HSVToRGB(hue, 0.35f, 0.8f);
            renderer.sharedMaterial = mat;
        }
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        // flee: any monster close by overrides everything -- panic runs
        // anywhere open, ignoring sidewalks (creator: "avoiding monsters,
        // then they can run anywhere")
        var threat = _builder.NearestMonsterTo(transform.position, FleeRadius);
        if (threat != null)
        {
            _hasDestination = false;   // errand abandoned; re-plan once safe
            var away = transform.position - threat.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                var fleeTo = transform.position + away.normalized * 6f;
                var fleeHex = _builder.HexAt(fleeTo);
                if (_builder.City.Contains(fleeHex) && !_builder.BlockedFor(false).Contains(fleeHex))
                    _target = _builder.WorldOf(fleeHex) + new Vector3(0f, 0.9f, 0f);
            }
            MoveToward(_target, FleeSpeed, dt);
            return;
        }

        _repickTimer -= dt;
        var toStep = _target - transform.position;
        toStep.y = 0f;
        if (toStep.magnitude < 0.5f || _repickTimer <= 0f)
        {
            _repickTimer = 1.5f + (GetInstanceID() % 20) / 10f;
            StepTowardDestination();
        }
        MoveToward(_target, WalkSpeed, dt);
    }

    /// <summary>Advance one hex toward the current destination, staying
    /// on the sidewalk. Picks (or re-picks) a destination when it has
    /// none or has arrived. Each step is the sidewalk (or corner-road
    /// crossing) neighbor that gets closest to the destination -- a
    /// greedy walk, which for cosmetic crowd is plenty and self-heals by
    /// re-picking a destination whenever it can't make progress.</summary>
    private void StepTowardDestination()
    {
        var here = _builder.HexAt(transform.position);

        if (!_hasDestination || here.DistanceTo(_destination) <= 1)
        {
            _stepSalt++;
            _destination = _builder.RandomSidewalkNear(here, DestinationRadius, GetInstanceID() + _stepSalt * 101);
            _hasDestination = true;
        }

        var roads = _builder.RoadNetworkHexes();
        var blocked = _builder.BlockedFor(false);

        HexCoord bestStep = here;
        var found = false;
        var bestDist = int.MaxValue;
        foreach (var n in here.Neighbors())
        {
            if (!_builder.City.Contains(n) || blocked.Contains(n)) continue;
            // legal footing: a sidewalk hex, or a road hex ONLY at a
            // corner (a crossing point) -- never a mid-block road hex
            var onRoad = roads.Contains(n);
            if (onRoad && !_builder.IsRoadCorner(n)) continue;
            if (!onRoad && !_builder.IsSidewalkHex(n)) continue;

            var d = n.DistanceTo(_destination);
            if (d < bestDist) { bestDist = d; bestStep = n; found = true; }
        }

        if (found && bestDist < here.DistanceTo(_destination))
        {
            SetTarget(bestStep);
        }
        else if (found)
        {
            // no strictly-closer legal step (greedy dead end) -- take the
            // best available anyway to keep moving, and pick a fresh
            // destination next tick
            SetTarget(bestStep);
            _hasDestination = false;
        }
        else
        {
            // boxed in: give up this errand, re-plan next tick
            _hasDestination = false;
        }
    }

    private void SetTarget(HexCoord hex)
    {
        _target = _builder.WorldOf(hex) + new Vector3(0f, 0.9f, 0f);
    }

    private void MoveToward(Vector3 target, float speed, float dt)
    {
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 0.05f) return;
        var dir = to / dist;
        transform.position += dir * Mathf.Min(speed * dt, dist);
        // terrain-follow the sculpted ground (docs/21), keeping the
        // capsule's own 0.9 body offset above it
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p) + 0.9f, p.z);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 6f);
    }
}
