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
///
/// WINGED FLIGHT (creator direction, 2026-07): a winged creature can
/// walk OR fly to a destination, deciding per order at path-compute time
/// (DecideFlight) -- "far" (the straight-line hex distance clears a
/// threshold) or "high up" (no ground route exists, or the ground route
/// is a heavy detour around buildings/water compared to flying it
/// direct). Flying still runs the SAME A* over the SAME hex grid, just
/// with the amphibious-style blocked set (buildings block, water
/// doesn't) -- "same navigation rules apply as walking, no going through
/// buildings" is the explicit rule, so this is never a straight-line
/// ignore-everything flight. A unit that flew to its target stays
/// airborne while it fights (an aerial attack) instead of landing first;
/// it only lands once its order is fully done (GoIdle).
/// </summary>
public class MonsterAgent : MonoBehaviour
{
    private enum OrderKind { Idle, Move, AttackBuilding, AttackUnit, EatCitizen }

    private RuntimeCityBuilder _builder;
    private StoredGenomeDto _creature;
    private MonsterBody _body;
    private UnitCombat _fighter;
    private LocomotionProfile _profile;
    private bool _amphibious;
    private bool _canFly;
    private bool _flying;   // current order's mode -- decided once per path-compute, see DecideFlight

    private OrderKind _order = OrderKind.Idle;
    private readonly Queue<HexCoord> _waypoints = new Queue<HexCoord>();
    private Building _targetBuilding;
    private Citizen _targetCitizen;
    private UnitCombat _targetUnit;
    private Vector3? _settleTarget;   // shared cluster point to creep toward once idle (group moves only)

    private List<Vector3> _path;      // world-space nodes for the current leg
    private int _pathIndex;
    private int _pathCityVersion = -1;
    private HexCoord _pathGoalHex;
    private float _attackCooldown;
    private Transform _selectionRing;

    public string DisplayName { get; private set; } = "";
    public bool Selected { get; private set; }

    /// <summary>This creature's combat state (health + genome-derived
    /// weapon). RuntimeCityBuilder registers it so tanks can target it and
    /// its health bar shows while it fights.</summary>
    public UnitCombat Fighter { get { return _fighter; } }

    /// <summary>The creature's body plan -- the "type" for SC2-style
    /// double-click "select all of this type on screen."</summary>
    public string BodyPlan { get { return _creature.Genome.Body.Plan; } }

    public string OrderDescription
    {
        get
        {
            var air = _flying ? " (airborne)" : "";
            switch (_order)
            {
                case OrderKind.Move: return "moving" + air + " (" + (_waypoints.Count + (_path != null ? 1 : 0)) + " waypoint leg(s))";
                case OrderKind.AttackBuilding: return "ATTACKING" + air + " " + (_targetBuilding != null ? _targetBuilding.Tier.ToString() : "?") + " building";
                case OrderKind.AttackUnit: return "ATTACKING" + air + " a unit";
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

    public string CombatDescription
    {
        get
        {
            if (_fighter == null) return "";
            var w = _fighter.Weapon;
            return "HP " + Mathf.CeilToInt(_fighter.Health) + "/" + Mathf.CeilToInt(_fighter.MaxHealth)
                + " · " + (w != null ? w.Name : "unarmed");
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
        _canFly = plan == "winged";

        // Position BEFORE building the body: Build() plants feet as
        // world-locked positions at the CURRENT transform (the no-skate
        // rule) -- the original order built at the world origin and then
        // teleported here, leaving every foot planted back at (0,0,0)
        // and every leg rendered as a hundreds-of-meters line to it.
        transform.position = _builder.WorldOf(homeHex);
        _body = gameObject.AddComponent<MonsterBody>();
        _body.Build(creature);

        // combat: health + weapon from the genome (Combat.Profile), the
        // same physiology-to-numbers discipline as Locomotion. The builder
        // registers this so the unit joins the fight (targetable, killable,
        // health-barred).
        var prof = MadDr.RosterClient.Combat.Profile(creature.Genome);
        _fighter = gameObject.AddComponent<UnitCombat>();
        var aim = Mathf.Max(1f, _body.BodyHeight);
        _fighter.Configure("monster", (float)prof.MaxHealth, Mathf.Max(1f, _body.BodyHeight * 0.55f),
            aim, prof.Weapon, OnDied);

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
        OrderMove(hex, queue, null);
    }

    /// <summary>Move order that also remembers a shared cluster point to
    /// creep toward once this unit finishes pathing to `hex` and goes
    /// idle -- the commander's group-move formation uses this so a group
    /// ends up packed close together once stopped instead of parked a
    /// full hex apart (FormationHexes only guarantees distinct WALKING
    /// slots, not a tight rest formation). Single-unit moves pass null
    /// and never drift after arriving.</summary>
    public void OrderMove(HexCoord hex, bool queue, Vector3? settleTarget)
    {
        ClearTargets();
        if (!queue) { _waypoints.Clear(); _path = null; }
        _waypoints.Enqueue(hex);
        _order = OrderKind.Move;
        _settleTarget = settleTarget;
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

    /// <summary>Target an enemy unit (a tank): close to weapon range, then
    /// fire on cadence. Issued by the commander (right-click a tank) or
    /// self-issued by AcquireTarget (retaliation / auto-engage).</summary>
    public void OrderAttackUnit(UnitCombat unit)
    {
        if (unit == null) return;
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _targetUnit = unit;
        _order = OrderKind.AttackUnit;
    }

    private void ClearTargets()
    {
        _targetBuilding = null;
        _targetCitizen = null;
        _targetUnit = null;
        _settleTarget = null;   // any fresh order cancels a pending group-settle creep
    }

    /// <summary>The single place _order becomes Idle. Flight is only ever
    /// for transit/aerial-attack, never for standing around -- landing
    /// here (rather than scattered across every early-return) means a
    /// winged unit always comes down once its order is genuinely done,
    /// whether that's a normal arrival, an unreachable path, a destroyed
    /// target, or death mid-order.</summary>
    private void GoIdle()
    {
        _order = OrderKind.Idle;
        if (_flying) { _flying = false; if (_body != null) _body.SetFlying(false); }
    }

    // ---- per-frame ----------------------------------------------------------

    private void Update()
    {
        var dt = Time.deltaTime;
        if (_fighter != null && !_fighter.Alive)
        {
            if (_body != null) _body.UpdateLocomotion(Vector3.zero, dt);   // dead: stand still
            return;
        }

        // idle units auto-acquire -- retaliate against whoever hit them, or
        // engage the nearest enemy in aggro range -- so the tank fight is
        // self-driving without the player microing every unit
        if (_order == OrderKind.Idle) AcquireTarget();

        var velocity = Vector3.zero;
        switch (_order)
        {
            case OrderKind.Move: velocity = TickMove(dt); break;
            case OrderKind.AttackBuilding: velocity = TickAttack(dt); break;
            case OrderKind.AttackUnit: velocity = TickAttackUnit(dt); break;
            case OrderKind.EatCitizen: velocity = TickEat(dt); break;
            case OrderKind.Idle: velocity = TickSettle(dt); break;
        }

        if (_body != null) _body.UpdateLocomotion(velocity, dt);
        if (_fighter != null && _builder != null) _builder.ApplySeparation(_fighter);
    }

    private void AcquireTarget()
    {
        if (_fighter == null || _fighter.Weapon == null || !_fighter.Weapon.CanAttack) return;
        var attacker = _fighter.LastAttacker;
        if (attacker != null && attacker.Alive) { OrderAttackUnit(attacker); return; }
        var enemy = _builder.NearestEnemyOf(_fighter, 130f);
        if (enemy != null) OrderAttackUnit(enemy);
    }

    private Vector3 TickMove(float dt)
    {
        if (_path == null)
        {
            if (_waypoints.Count == 0) { GoIdle(); return Vector3.zero; }
            var goal = _waypoints.Dequeue();
            _path = ComputePath(goal);
            if (_path == null) { GoIdle(); return Vector3.zero; } // unreachable: stop rather than pretend
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    private const float SettleSpeed = 1.3f;    // a slow shuffle, not a march
    private const float SettleArriveDist = 0.6f;

    /// <summary>Idle creep toward a shared group-move cluster point
    /// (see OrderMove's settleTarget overload). ApplySeparation -- already
    /// called unconditionally every frame below -- stops the creep the
    /// moment neighbors are touching, so the group packs down from its
    /// loose one-hex-apart walking spacing to combined-radius spacing once
    /// everyone's stopped, without any unit ever overlapping another.
    ///
    /// Terrain-aware on purpose ("must be cognizant of buildings and
    /// natural features"): each step is checked against the SAME blocked-
    /// hex set pathfinding uses (buildings, water) before committing, so a
    /// unit settling toward the group never clips into one -- it just
    /// stops dead at the boundary and gives up the creep for good.</summary>
    private Vector3 TickSettle(float dt)
    {
        if (!_settleTarget.HasValue || _builder == null) return Vector3.zero;
        var target = _settleTarget.Value;
        var to = target - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist < SettleArriveDist) { _settleTarget = null; return Vector3.zero; }

        var dir = to / dist;
        var step = Mathf.Min(SettleSpeed * dt, dist);
        var next = transform.position + dir * step;

        var hex = _builder.HexAt(next);
        if (!_builder.City.Contains(hex) || Blocked().Contains(hex))
        {
            _settleTarget = null;   // a building or water is in the way -- stop right here
            return Vector3.zero;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), dt * 4f);
        transform.position = next;
        return dir * SettleSpeed;
    }

    private Vector3 TickAttack(float dt)
    {
        if (_targetBuilding == null) { GoIdle(); return Vector3.zero; }
        if (_builder.IsDestroyed(_targetBuilding)) { _targetBuilding = null; GoIdle(); return Vector3.zero; }

        var armed = _fighter != null && _fighter.Weapon != null && _fighter.Weapon.CanAttack;
        // "ground units without projectile weapons must be near the
        // building to attack it" (creator direction, 2026-07): a weapon
        // with real reach (Beam/Bolt/Bullet/Spore -- WeaponFor's Range)
        // fights from that range; Melee weapons carry a tiny Range by
        // construction (6-9m, WeaponFor) and an unarmed stump gets a
        // flat bash distance -- both land inside the Mathf.Max(6f, ...)
        // floor, so "no projectile weapon" already collapses to "must be
        // adjacent" without a separate WeaponKind check here. Flying
        // doesn't change this range at all -- an aerial attacker just
        // fights from the same XZ distance while staying airborne.
        var reach = armed ? Mathf.Max(6f, (float)_fighter.Weapon.Range) : 32f;
        var bp = NearestFootprintPoint(_targetBuilding);
        var flat = bp - transform.position;
        flat.y = 0f;

        if (flat.magnitude <= reach)
        {
            _path = null;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(flat.normalized, Vector3.up), dt * 4f);
            if (armed)
            {
                // the shot that plays the FX is the one that dents the
                // building: cadence-gated by TryFireAtPoint, weapon-scaled
                if (_fighter.TryFireAtPoint(bp, Muzzle()))
                    _builder.ApplyBuildingDamage(_targetBuilding, Mathf.RoundToInt((float)_fighter.Weapon.Damage * 3f + 25f));
            }
            else
            {
                // unarmed bash: bulk-scaled flat damage (docs/04's real
                // formula needs statGenes the v2 schema never shipped)
                _attackCooldown -= dt;
                if (_attackCooldown <= 0f)
                {
                    _attackCooldown = 1f;
                    var bulk = _creature.Genome.Body.Params.Length > 1 ? (float)_creature.Genome.Body.Params[1] : 0.5f;
                    _builder.ApplyBuildingDamage(_targetBuilding, Mathf.RoundToInt(40f + bulk * 120f));
                }
            }
            return Vector3.zero;
        }

        if (_path == null)
        {
            _path = ComputeApproachPath(_targetBuilding);
            if (_path == null) { GoIdle(); return Vector3.zero; }
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    private Vector3 TickAttackUnit(float dt)
    {
        if (_targetUnit == null || !_targetUnit.Alive) { _targetUnit = null; GoIdle(); return Vector3.zero; }
        var w = _fighter.Weapon;
        var to = _targetUnit.AimPoint - transform.position;
        to.y = 0f;
        var dist = to.magnitude;

        if (dist <= (float)w.Range * 0.9f)
        {
            _path = null;
            var dir = dist > 0.01f ? to / dist : transform.forward;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up), dt * 6f);
            _fighter.TryFire(_targetUnit, Muzzle());
            return Vector3.zero;
        }

        // close in -- re-path when the target drifts a hex from where we
        // were headed (a rolling tank), the same trick as chasing a citizen
        var targHex = _builder.HexAt(_targetUnit.transform.position);
        if (_path == null || targHex.DistanceTo(_pathGoalHex) > 1)
        {
            _path = ComputePath(targHex);
            if (_path == null) return Vector3.zero;
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    private Vector3 Muzzle()
    {
        var h = _body != null ? _body.BodyHeight : 1.5f;
        return transform.position + Vector3.up * (h * 1.15f)
            + transform.forward * (h * 0.5f) + transform.right * (h * 0.35f);
    }

    private Vector3 NearestFootprintPoint(Building building)
    {
        var pos = transform.position;
        var best = pos;
        var bestSq = float.MaxValue;
        foreach (var hex in building.Footprint)
        {
            var w = _builder.WorldOf(hex);
            var d = w - pos;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = w; }
        }
        return best;
    }

    private void OnDied()
    {
        GoIdle();
        _targetUnit = null;
        _targetBuilding = null;
        _targetCitizen = null;
        _path = null;
        if (_selectionRing != null) _selectionRing.gameObject.SetActive(false);
        if (_builder != null) _builder.OnCombatantDied(_fighter);
        Object.Destroy(gameObject, 0.15f);
    }

    private Vector3 TickEat(float dt)
    {
        if (_targetCitizen == null) { GoIdle(); return Vector3.zero; }

        var toTarget = _targetCitizen.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 3f)
        {
            _builder.OnCitizenEaten(_targetCitizen);
            _targetCitizen = null;
            GoIdle();
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
        // arc around a unit sitting ahead so a faster creature overtakes a
        // slower one instead of piling into its back (no-op when clear)
        var steer = _builder != null && _fighter != null ? _builder.AvoidanceDir(_fighter, dir) : dir;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(steer, Vector3.up), dt * 5f);
        var step = Mathf.Min(speed * dt, dist);
        transform.position += steer * step;
        return steer * speed;
    }

    private const int FarHexThreshold = 5;      // ~100m straight-line (20m hex) counts as "far"
    private const float DetourFactor = 1.8f;    // ground route this much longer than a straight line = "high up" territory

    /// <summary>Decide walk vs fly for a winged unit's NEXT path, per the
    /// creator's rule: "if the distance is far or high up." Non-flyers
    /// always return false. "Far" is a straight-line hex-distance
    /// threshold; "high up" is inferred from the ground route itself --
    /// unreachable on foot at all (a river with no bridge), or a route
    /// so much longer than a straight line that it's clearly detouring
    /// hard around buildings/water -- exactly the situations flying over
    /// is the obviously better call. This costs one extra A* run (the
    /// ground-only check), but only once per path-compute, never per
    /// frame.</summary>
    private bool DecideFlight(HexCoord start, HexCoord goal)
    {
        if (!_canFly) return false;
        var straight = start.DistanceTo(goal);
        if (straight == 0) return false;
        if (straight >= FarHexThreshold) return true;
        var groundPath = HexPathfinder.FindPath(start, goal, _builder.City, _builder.BlockedFor(false));
        if (groundPath == null) return true;   // no ground route at all
        return groundPath.Count > straight * DetourFactor;
    }

    /// <summary>The blocked set for whatever this unit is doing RIGHT
    /// NOW: flying uses the same rule as an amphibious plan (only
    /// buildings block -- water doesn't), regardless of the creature's
    /// own amphibious flag, since that's exactly what "no going through
    /// buildings" plus free passage over everything else means for a
    /// flyer. Grounded movement keeps the creature's normal ground/
    /// amphibious rule.</summary>
    private HashSet<HexCoord> Blocked()
    {
        return _builder.BlockedFor(_flying || _amphibious);
    }

    private void SetFlyingFor(HexCoord start, HexCoord goal)
    {
        _flying = DecideFlight(start, goal);
        if (_body != null) _body.SetFlying(_flying);
    }

    private List<Vector3> ComputePath(HexCoord goal)
    {
        var start = _builder.HexAt(transform.position);
        SetFlyingFor(start, goal);
        var hexPath = HexPathfinder.FindPath(start, goal, _builder.City, Blocked());
        _pathGoalHex = goal;
        return ToWorldPath(hexPath);
    }

    private List<Vector3> ComputeApproachPath(Building building)
    {
        var start = _builder.HexAt(transform.position);
        // the fly/walk call needs ONE representative target hex; the
        // closest footprint hex to home is a fine proxy -- the actual
        // approach hex FindPathToBuilding lands on is picked below, from
        // the SAME (now-decided) blocked set
        SetFlyingFor(start, _builder.HexAt(NearestFootprintPoint(building)));
        var hexPath = HexPathfinder.FindPathToBuilding(start, building.Footprint, _builder.City, Blocked());
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
}
