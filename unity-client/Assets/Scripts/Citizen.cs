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

    /// <summary>docs/26 Phase 6: this citizen's own CaptureState -- a
    /// Citizen has no UnitCombat component (confirmed via grep, not
    /// assumed), so it can't share UnitCombat's copy and owns one
    /// directly instead. Null until something calls Capture().</summary>
    private CaptureState _capture;

    // 2026-07 creator direction: buildings that get destroyed "disgorge
    // their human occupants that flee." A disgorged occupant should read
    // as fleeing THE COLLAPSE, not calmly window-shopping until a monster
    // happens to wander within FleeRadius -- so it gets a short forced
    // panic sprint away from the origin point on spawn, independent of
    // live monster proximity, before falling back to the normal
    // proximity-based flee/walk AI below.
    private const float ForcedFleeDuration = 4f;
    private Vector3 _forcedFleeOrigin;
    private float _forcedFleeTimer;

    /// <summary>Begin being dragged toward `captor`, overriding this
    /// citizen's own flee/walk AI in Update() until the captor dies
    /// (auto-release) or it arrives and is eaten (docs/26 Phase 7).</summary>
    public void Capture(UnitCombat captor, float speed)
    {
        if (_capture == null) _capture = new CaptureState();
        _capture.Begin(captor, speed);
    }

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

    /// <summary>Call right after <see cref="Init"/> for a citizen spawned
    /// by a destroyed building's occupant disgorge (<see
    /// cref="RuntimeCityBuilder.SpawnFleeingOccupant"/>) -- starts it in a
    /// forced panic sprint away from `origin` (the wreck) for a few
    /// seconds before it falls back to normal AI.</summary>
    public void InitFleeingFrom(Vector3 origin)
    {
        _forcedFleeOrigin = origin;
        _forcedFleeTimer = ForcedFleeDuration;
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        // captured overrides everything, even fleeing -- a caught citizen
        // is being dragged, not choosing to run (docs/26 Phase 6); once it
        // arrives at its captor, it's eaten (Phase 7) -- reusing the same
        // OnCitizenEaten a chased-and-caught citizen already goes through,
        // so wallet credit/gore FX/despawn are identical either way. It
        // also credits the capturing monster's harvest tank exactly like a
        // direct chase-and-eat order does (docs/26 follow-up) -- found via
        // GetComponent since a Citizen only has a reference to the
        // UnitCombat it was dragged toward, not the MonsterAgent that
        // captured it; the two are always on the same GameObject.
        if (_capture != null)
        {
            if (!_capture.Active)
            {
                _capture = null;   // captor died mid-drag -- released, resume normal AI below
            }
            else
            {
                var arrived = _capture.TickPull(transform, dt);
                var cp = transform.position;
                transform.position = new Vector3(cp.x, _builder.GroundHeightAt(cp) + 0.9f, cp.z);
                if (arrived)
                {
                    var captorAgent = _capture.Captor != null ? _capture.Captor.GetComponent<MonsterAgent>() : null;
                    if (captorAgent != null) captorAgent.NotifyCapturedCitizenEaten();
                    _builder.OnCitizenEaten(this);
                }
                return;
            }
        }

        // forced panic sprint away from a building that just collapsed
        // under this citizen -- same movement shape as the proximity flee
        // below, just sourced from the fixed wreck point instead of a
        // live monster; expires on its own and falls through to normal AI.
        if (_forcedFleeTimer > 0f)
        {
            _forcedFleeTimer -= dt;
            _hasDestination = false;
            var awayFromWreck = transform.position - _forcedFleeOrigin;
            awayFromWreck.y = 0f;
            // degenerate case: this citizen spawned exactly on the wreck's
            // own hex center (a real scenario -- the hex unblocks the
            // instant the building dies, the same frame occupants spawn),
            // so there's no real "away" direction yet -- pick a
            // deterministic fallback angle off this citizen's own instance
            // ID (same idiom Init already uses for its color hue) rather
            // than leaving it stuck with a zero-length flee vector.
            if (awayFromWreck.sqrMagnitude <= 0.01f)
            {
                var angle = (GetInstanceID() % 360) * Mathf.Deg2Rad;
                awayFromWreck = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            // the raw fleeTo point, NOT snapped to its hex's center like
            // the proximity-flee block below does -- a hex's inradius
            // (~10m) is bigger than this 6m step, so a citizen spawned
            // dead-center on its wreck's own hex (every disgorged
            // occupant, always) would otherwise round right back to the
            // SAME hex center it started at and never move a single unit
            // for the whole forced-flee window. Still gated by the same
            // city/blocked legality check, just without the re-snap.
            var fleeTo = transform.position + awayFromWreck.normalized * 6f;
            var fleeHex = _builder.HexAt(fleeTo);
            if (_builder.City.Contains(fleeHex) && !_builder.BlockedFor(false).Contains(fleeHex))
                _target = fleeTo;
            MoveToward(_target, FleeSpeed, dt);
            return;
        }

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
