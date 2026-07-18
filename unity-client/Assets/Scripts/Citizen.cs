using MadDr.CityGen;
using UnityEngine;

/// <summary>
/// A Citizen (docs/19): client-side cosmetic crowd, not a synced combat
/// entity -- exactly the doc's own scoping ("Citizens run client-side
/// cosmetic/crowd AI, not server-synced"). Wanders between adjacent
/// passable hexes; flees any monster that gets close (the Passive band,
/// docs/19 SS3 -- always flees; the armed/aggressive bands are future
/// content); edible. Eating one pays docs/20's per-citizen yield
/// (Blood 2 / Bones 1 / Brains 1) into the session wallet.
/// </summary>
public class Citizen : MonoBehaviour
{
    private const float WalkSpeed = 1.4f;   // docs/19 SS4: civilian amble, ~0.07 hex/s scale
    private const float FleeSpeed = 4.2f;   // panic sprint
    private const float FleeRadius = 14f;

    private RuntimeCityBuilder _builder;
    private Vector3 _target;
    private float _repickTimer;

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

        // flee: any monster close by overrides the amble
        var threat = _builder.NearestMonsterTo(transform.position, FleeRadius);
        if (threat != null)
        {
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
        var to = _target - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.5f || _repickTimer <= 0f)
        {
            _repickTimer = 2f + (GetInstanceID() % 30) / 10f;
            PickSidewalkTarget();
        }
        MoveToward(_target, WalkSpeed, dt);
    }

    /// <summary>Docs/19 crowd wander, sidewalk-aware (creator direction,
    /// 2026-07: "Humans should prefer walk on sidewalks. Cross at
    /// corners. unless fleeing from monster" -- the flee branch above is
    /// deliberately untouched by any of this, panic ignores sidewalks
    /// entirely). Strongly prefers a neighbor OFF the road network; if
    /// every off-road neighbor is unavailable, it'll step onto the road
    /// only at a corner/junction hex (RuntimeCityBuilder.IsRoadCorner) --
    /// never a mid-block jaywalk. A last-resort fallback (any open
    /// neighbor at all, road or not) keeps a citizen boxed in against a
    /// long straight from freezing in place.</summary>
    private void PickSidewalkTarget()
    {
        var here = _builder.HexAt(transform.position);
        var blocked = _builder.BlockedFor(false);
        var roads = _builder.RoadNetworkHexes();

        HexCoord? fallback = null;
        foreach (var n in here.Neighbors())
        {
            if (!_builder.City.Contains(n) || blocked.Contains(n)) continue;
            if (fallback == null) fallback = n;
            if (!roads.Contains(n)) { SetTarget(n); return; }        // off the road: always fine
            if (_builder.IsRoadCorner(n)) { SetTarget(n); return; }   // a corner: a legal crossing
            // else: a mid-block road hex -- skip it, keep looking
        }
        if (fallback.HasValue) SetTarget(fallback.Value);
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
