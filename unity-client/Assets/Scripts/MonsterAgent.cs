using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// One commanded monster: waypoint navigation over the hex grid
/// (HexPathfinder A*, so paths go AROUND buildings -- unless the order
/// IS the building), target locking (attack a building until Destroyed;
/// chase and eat a Citizen), speeds from the creature's own
/// LocomotionProfile (physiology, docs/11 numbers via the tested
/// roster-client port), walk vs run picked by distance and gated by the
/// sprint headroom rule.
///
/// Movement is kinematic along hex-center path nodes; MonsterBody turns
/// the resulting real velocity into footsteps (no skating -- see its
/// header).
/// </summary>
public class MonsterAgent : MonoBehaviour
{
    private enum OrderKind { Idle, Move, AttackBuilding, EatCitizen }

    private RuntimeCityBuilder _builder;
    private StoredGenomeDto _creature;
    private MonsterBody _body;
    private LocomotionProfile _profile;
    private bool _amphibious;

    private OrderKind _order = OrderKind.Idle;
    private readonly Queue<HexCoord> _waypoints = new Queue<HexCoord>();
    private Building _targetBuilding;
    private Citizen _targetCitizen;

    private List<Vector3> _path;      // world-space nodes for the current leg
    private int _pathIndex;
    private int _pathCityVersion = -1;
    private HexCoord _pathGoalHex;
    private float _attackCooldown;
    private Transform _selectionRing;

    public string DisplayName { get; private set; } = "";
    public bool Selected { get; private set; }

    public string OrderDescription
    {
        get
        {
            switch (_order)
            {
                case OrderKind.Move: return "moving (" + (_waypoints.Count + (_path != null ? 1 : 0)) + " waypoint leg(s))";
                case OrderKind.AttackBuilding: return "ATTACKING " + (_targetBuilding != null ? _targetBuilding.Tier.ToString() : "?") + " building";
                case OrderKind.EatCitizen: return "hunting a citizen";
                default: return "idle";
            }
        }
    }

    public string SpeedDescription
    {
        get
        {
            return "walk " + _profile.WalkSpeedHexPerMin + " / run " + _profile.RunSpeedHexPerMin
                + " hex/min, sprint " + _profile.Sprint;
        }
    }

    public void Init(RuntimeCityBuilder builder, StoredGenomeDto creature, HexCoord homeHex)
    {
        _builder = builder;
        _creature = creature;
        DisplayName = creature.Id;
        _profile = Locomotion.Profile(creature.Genome);
        var plan = creature.Genome.Body.Plan;
        _amphibious = plan == "crab" || plan == "serpentine";

        // Position BEFORE building the body: Build() plants feet as
        // world-locked positions at the CURRENT transform (the no-skate
        // rule) -- the original order built at the world origin and then
        // teleported here, leaving every foot planted back at (0,0,0)
        // and every leg rendered as a hundreds-of-meters line to it.
        transform.position = _builder.WorldOf(homeHex);
        _body = gameObject.AddComponent<MonsterBody>();
        _body.Build(creature);

        // selection ring: a flat disc at the feet, toggled by the commander
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SelectionRing";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.transform.localScale = new Vector3(3.2f, 0.03f, 3.2f);
        var ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null) Object.Destroy(ringCollider);
        var ringRenderer = ring.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            var mat = new Material(ShaderUtil.FindRenderableShader());
            mat.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            ringRenderer.sharedMaterial = mat;
        }
        _selectionRing = ring.transform;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        Selected = selected;
        if (_selectionRing != null) _selectionRing.gameObject.SetActive(selected);
    }

    // ---- orders -------------------------------------------------------------

    public void OrderMove(HexCoord hex, bool queue)
    {
        ClearTargets();
        if (!queue) { _waypoints.Clear(); _path = null; }
        _waypoints.Enqueue(hex);
        _order = OrderKind.Move;
    }

    public void OrderAttack(Building building)
    {
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _targetBuilding = building;
        _order = OrderKind.AttackBuilding;
    }

    public void OrderEat(Citizen citizen)
    {
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _targetCitizen = citizen;
        _order = OrderKind.EatCitizen;
    }

    private void ClearTargets()
    {
        _targetBuilding = null;
        _targetCitizen = null;
    }

    // ---- per-frame ----------------------------------------------------------

    private void Update()
    {
        var dt = Time.deltaTime;
        var velocity = Vector3.zero;

        switch (_order)
        {
            case OrderKind.Move: velocity = TickMove(dt); break;
            case OrderKind.AttackBuilding: velocity = TickAttack(dt); break;
            case OrderKind.EatCitizen: velocity = TickEat(dt); break;
        }

        if (_body != null) _body.UpdateLocomotion(velocity, dt);
    }

    private Vector3 TickMove(float dt)
    {
        if (_path == null)
        {
            if (_waypoints.Count == 0) { _order = OrderKind.Idle; return Vector3.zero; }
            var goal = _waypoints.Dequeue();
            _path = ComputePath(goal);
            if (_path == null) { _order = OrderKind.Idle; return Vector3.zero; } // unreachable: stop rather than pretend
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    private Vector3 TickAttack(float dt)
    {
        if (_targetBuilding == null) { _order = OrderKind.Idle; return Vector3.zero; }
        if (_builder.IsDestroyed(_targetBuilding)) { _targetBuilding = null; _order = OrderKind.Idle; return Vector3.zero; }

        if (InRangeOfBuilding(_targetBuilding))
        {
            _path = null;
            _attackCooldown -= dt;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = 1f;
                // Placeholder damage: docs/04's real formula needs the
                // statGenes block that never shipped in the v2 schema
                // (found porting Cannibalize) -- bulk-scaled flat damage
                // until real combat stats exist.
                var bulk = _creature.Genome.Body.Params.Length > 1 ? (float)_creature.Genome.Body.Params[1] : 0.5f;
                _builder.ApplyBuildingDamage(_targetBuilding, Mathf.RoundToInt(60f + bulk * 140f));
            }
            return Vector3.zero;
        }

        if (_path == null)
        {
            _path = ComputeApproachPath(_targetBuilding);
            if (_path == null) { _order = OrderKind.Idle; return Vector3.zero; }
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    private Vector3 TickEat(float dt)
    {
        if (_targetCitizen == null) { _order = OrderKind.Idle; return Vector3.zero; }

        var toTarget = _targetCitizen.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 3f)
        {
            _builder.OnCitizenEaten(_targetCitizen);
            _targetCitizen = null;
            _order = OrderKind.Idle;
            _path = null;
            return Vector3.zero;
        }

        // a fleeing citizen invalidates its own path constantly; re-path
        // when it has moved a hex away from where we were headed
        var citizenHex = _builder.HexAt(_targetCitizen.transform.position);
        if (_path == null || citizenHex.DistanceTo(_pathGoalHex) > 1)
        {
            _path = ComputePath(citizenHex);
            if (_path == null) return Vector3.zero; // cornered somewhere unreachable; wait
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, (float)_profile.RunMetersPerSecond(_builder.speedDisplayMultiplier)); // always chase at a run
    }

    // ---- movement mechanics --------------------------------------------------

    private float RunOrWalkSpeed()
    {
        // run for long hauls when the heart has headroom; walk otherwise
        var farToGo = _path != null && _path.Count - _pathIndex > 6;
        var mayRun = _profile.Sprint != "none";
        var speed = farToGo && mayRun
            ? _profile.RunMetersPerSecond(_builder.speedDisplayMultiplier)
            : _profile.WalkMetersPerSecond(_builder.speedDisplayMultiplier);
        return (float)speed;
    }

    private Vector3 FollowPath(float dt, float speed)
    {
        if (_path == null || _pathIndex >= _path.Count)
        {
            _path = null;
            _pathIndex = 0;
            return Vector3.zero;
        }

        var target = _path[_pathIndex];
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < 0.6f)
        {
            _pathIndex++;
            return FollowPath(dt, speed);
        }

        var dir = to / dist;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
        var step = Mathf.Min(speed * dt, dist);
        transform.position += dir * step;
        return dir * speed;
    }

    private List<Vector3> ComputePath(HexCoord goal)
    {
        var start = _builder.HexAt(transform.position);
        var hexPath = HexPathfinder.FindPath(start, goal, _builder.City, _builder.BlockedFor(_amphibious));
        _pathGoalHex = goal;
        return ToWorldPath(hexPath);
    }

    private List<Vector3> ComputeApproachPath(Building building)
    {
        var start = _builder.HexAt(transform.position);
        var hexPath = HexPathfinder.FindPathToBuilding(start, building.Footprint, _builder.City, _builder.BlockedFor(_amphibious));
        if (hexPath != null && hexPath.Count > 0) _pathGoalHex = hexPath[hexPath.Count - 1];
        return ToWorldPath(hexPath);
    }

    private List<Vector3> ToWorldPath(List<HexCoord> hexPath)
    {
        if (hexPath == null) return null;
        _pathIndex = 0;
        _pathCityVersion = _builder.CityVersion;
        var world = new List<Vector3>(hexPath.Count);
        foreach (var hex in hexPath) world.Add(_builder.WorldOf(hex));
        return world;
    }

    private void RecomputeIfCityChanged()
    {
        // destruction changed passability (rubble opens, a dead bridge
        // closes) -- recompute the current leg against the new city
        if (_path == null || _pathCityVersion == _builder.CityVersion) return;
        if (_order == OrderKind.AttackBuilding && _targetBuilding != null)
            _path = ComputeApproachPath(_targetBuilding);
        else
            _path = ComputePath(_pathGoalHex);
    }

    private bool InRangeOfBuilding(Building building)
    {
        var pos = transform.position;
        foreach (var hex in building.Footprint)
        {
            var w = _builder.WorldOf(hex);
            var d = w - pos;
            d.y = 0f;
            if (d.magnitude < 32f) return true; // ~1.5 hexes: adjacent-and-swinging
        }
        return false;
    }
}
