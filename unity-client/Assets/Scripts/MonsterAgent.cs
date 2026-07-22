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
    private enum OrderKind { Idle, Move, AttackBuilding, AttackUnit, EatCitizen, Perch }

    /// <summary>Shared "which way does the group face" token for a squad
    /// move order (see OrderMove's groupFacing overload). Stays unlocked
    /// until the first unit in the group finishes pathing to its slot,
    /// which locks it to that unit's heading -- creator direction,
    /// 2026-07: "determined by the first creature that reaches the
    /// waypoint." Every other unit in the group then turns to match once
    /// it arrives, instead of holding whatever direction it happened to
    /// be walking. A class (not a struct) so every unit in the group
    /// shares the exact same instance.</summary>
    public sealed class GroupFacing
    {
        public Quaternion? Locked;
    }

    private RuntimeCityBuilder _builder;
    private StoredGenomeDto _creature;
    private MonsterBody _body;
    private UnitCombat _fighter;
    private LocomotionProfile _profile;
    private bool _amphibious;
    private bool _canFly;
    private bool _flying;      // current order's mode -- decided once per path-compute, see DecideFlight
    private bool _flyingHigh;  // which cruise tier, when flying -- see DecideFlightTier
    private float _flightLowAlt;   // cached from _body once Build() has computed them (MonsterBody.LowFlightAltitude)
    private float _flightHighAlt;  // (MonsterBody.HighFlightAltitude)
    private int _surfaceSyncVersion = -1;  // last CityVersion the body's ground height was synced at
    private bool _perchApproach;           // perch order's final line-up-over-the-roof phase

    private OrderKind _order = OrderKind.Idle;
    private readonly Queue<HexCoord> _waypoints = new Queue<HexCoord>();
    private Building _targetBuilding;
    private Citizen _targetCitizen;
    private UnitCombat _targetUnit;
    private Vector3? _settleTarget;   // shared cluster point to creep toward once idle (group moves only)
    private GroupFacing _groupFacing; // shared arrival-facing token for a group move (see GroupFacing)

    private List<Vector3> _path;      // world-space nodes for the current leg
    private int _pathIndex;
    private int _pathCityVersion = -1;
    private HexCoord _pathGoalHex;
    private float _attackCooldown;
    private Transform _selectionRing;

    // harvest & carry (docs/22 harvester morphology). Every unit has a
    // profile; it only matters for creatures actually built to harvest --
    // a lamprey maw + a storage tank. Carried load slows the carrier
    // (weight, floored per docs/22 so it never strands anyone), doubly so
    // for flyers; it banks to the wallet when the unit idles near home.
    private HarvestProfile _harvest;
    private float _carriedLoad;       // 0.._harvest.Capacity, pooled resource units
    private HexCoord _homeHex;         // spawn = this harvester's unload point (a Vat stand-in)

    public string DisplayName { get; private set; } = "";
    public bool Selected { get; private set; }

    /// <summary>This creature's combat state (health + genome-derived
    /// weapon). RuntimeCityBuilder registers it so tanks can target it and
    /// its health bar shows while it fights.</summary>
    public UnitCombat Fighter { get { return _fighter; } }

    /// <summary>The creature's body plan -- the "type" for SC2-style
    /// double-click "select all of this type on screen."</summary>
    public string BodyPlan { get { return _creature.Genome.Body.Plan; } }

    /// <summary>Winged plan -- the commander routes a roof-click to a
    /// perch order for these, an attack order for everyone else.</summary>
    public bool IsFlyer { get { return _canFly; } }

    /// <summary>Standing (not flying) on an elevated surface -- a rooftop
    /// perch. Perched units hold their roost (no auto-engage) and any new
    /// move starts with a takeoff, since there's no walking off a roof.</summary>
    private bool Perched
    {
        get
        {
            return _canFly && !_flying && _builder != null
                && _builder.SurfaceHeightAt(transform.position) > 0.1f;
        }
    }

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
                case OrderKind.Perch: return "flying to a rooftop perch";
                default: return Perched ? "perched on a rooftop" : "idle";
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
        _harvest = Harvest.Profile(creature.Genome);
        _homeHex = homeHex;
        var plan = creature.Genome.Body.Plan;
        _amphibious = plan == "crab" || plan == "serpentine";
        _canFly = plan == "winged";

        // Position BEFORE building the body: Build() plants feet as
        // world-locked positions at the CURRENT transform (the no-skate
        // rule) -- the original order built at the world origin and then
        // teleported here, leaving every foot planted back at (0,0,0)
        // and every leg rendered as a hundreds-of-meters line to it.
        var home = _builder.WorldOf(homeHex);
        home.y = _builder.GroundHeightAt(home);   // spawn ON the sculpted terrain, not at sea level
        transform.position = home;
        _body = gameObject.AddComponent<MonsterBody>();
        // feet must plant on the slope under each foot, not a flat y=0
        // plane -- the body samples the same terrain the world renders
        _body.GroundSampler = p => _builder.GroundHeightAt(p);
        _body.Build(creature);
        if (_canFly) { _flightLowAlt = _body.LowFlightAltitude; _flightHighAlt = _body.HighFlightAltitude; }

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
        OrderMove(hex, queue, null, null);
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
        OrderMove(hex, queue, settleTarget, null);
    }

    /// <summary>Move order for a squad, additionally sharing a
    /// GroupFacing token so the whole group ends up looking the same
    /// direction once everyone's arrived (see GroupFacing). Pass null for
    /// a single-unit move -- no group to agree on a facing with.</summary>
    public void OrderMove(HexCoord hex, bool queue, Vector3? settleTarget, GroupFacing groupFacing)
    {
        ClearTargets();
        if (!queue) { _waypoints.Clear(); _path = null; }
        _waypoints.Enqueue(hex);
        _order = OrderKind.Move;
        _settleTarget = settleTarget;
        _groupFacing = groupFacing;
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

    /// <summary>Fly to a building and land ON its roof (creator
    /// direction, 2026-07: "they should be able to land on the
    /// building"). Winged only -- the commander routes a roof-click here
    /// for flyers and to OrderAttack for everyone else, but guard anyway
    /// so a stray call can't strand a ground creature mid-wall.</summary>
    public void OrderPerch(Building building)
    {
        if (building == null) return;
        if (!_canFly) { OrderAttack(building); return; }
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _targetBuilding = building;
        _order = OrderKind.Perch;
    }

    private void ClearTargets()
    {
        _targetBuilding = null;
        _targetCitizen = null;
        _targetUnit = null;
        _settleTarget = null;   // any fresh order cancels a pending group-settle creep
        _groupFacing = null;    // and any pending group-arrival facing agreement
        _perchApproach = false;
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
        // group-move arrival facing: the first unit of the group to
        // finish pathing to its waypoint locks the shared token to ITS
        // heading right now, before any settle creep can turn it further
        // -- "reaches the waypoint" means finishing the path, not the
        // creep afterward. Every other unit in the group (including this
        // one, if it drifts while creeping) turns to match in TickSettle.
        if (_groupFacing != null && _groupFacing.Locked == null)
            _groupFacing.Locked = transform.rotation;
        if (_flying)
        {
            _flying = false;
            if (_body != null)
            {
                // land onto whatever is directly below: street level
                // normally, a roof if this order ended over a building
                // (the perch order's whole point -- but also any flight
                // that happens to finish over a short building it was
                // legally cruising above)
                _body.SetGroundHeight(_builder != null ? _builder.SurfaceHeightAt(transform.position) : 0f);
                _body.SetFlying(false, false);
            }
        }
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
        // self-driving without the player microing every unit. A PERCHED
        // unit deliberately does NOT: it holds the roost the player chose
        // (auto-engaging would make every perch instantly dissolve into a
        // tank chase); attack it manually to send it back into the fight.
        if (_order == OrderKind.Idle && !Perched) AcquireTarget();

        // unload the harvest tank when idle near home (docs/22): the
        // player hauls a laden harvester back toward its spawn -- its Vat
        // stand-in -- and it banks automatically on arrival, no button,
        // and its speed recovers. Auto-first, but the HAULING is the
        // player's decision (no unprompted walk-off), so it never yanks a
        // unit away from where it was parked.
        if (_order == OrderKind.Idle && _carriedLoad > 0.01f && _builder != null)
        {
            var toHome = _builder.WorldOf(_homeHex) - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude < 2.5f * (float)HexCoord.HexMeters)
            {
                _builder.BankHarvestLoad(_carriedLoad);
                _carriedLoad = 0f;
            }
        }

        // destruction can change the surface under a standing flyer (the
        // roof it perched on collapses to rubble): re-sync the body's
        // ground height whenever the city changes, and it eases itself
        // down to whatever is left
        if (_canFly && !_flying && _builder != null && _surfaceSyncVersion != _builder.CityVersion)
        {
            _surfaceSyncVersion = _builder.CityVersion;
            if (_body != null) _body.SetGroundHeight(_builder.SurfaceHeightAt(transform.position));
        }

        var velocity = Vector3.zero;
        switch (_order)
        {
            case OrderKind.Move: velocity = TickMove(dt); break;
            case OrderKind.AttackBuilding: velocity = TickAttack(dt); break;
            case OrderKind.AttackUnit: velocity = TickAttackUnit(dt); break;
            case OrderKind.EatCitizen: velocity = TickEat(dt); break;
            case OrderKind.Perch: velocity = TickPerch(dt); break;
            case OrderKind.Idle: velocity = TickSettle(dt); break;
        }
        // docs/25 Phase C: publish this frame's velocity for neighbours'
        // predictive avoidance to read (MonsterSteeringController.
        // PredictiveAvoidance) -- set unconditionally, including zero while
        // idle, so a stopped unit correctly reads as stationary rather than
        // still carrying its last frame of movement.
        if (_fighter != null) _fighter.LastVelocity = velocity;

        // terrain-follow: the movement ticks steer in XZ; the sculpted
        // ground supplies Y (docs/21). Flat-locked plots keep this 0 near
        // every building, so flight/perch math is untouched; flyers'
        // base follows the terrain too (lift rides on top of it).
        if (_builder != null)
        {
            var pos = transform.position;
            var gy = _builder.GroundHeightAt(pos);
            if (!Mathf.Approximately(pos.y, gy))
                transform.position = new Vector3(pos.x, gy, pos.z);
        }

        // clear-path-to-descend: the tallest thing directly below the
        // creature RIGHT NOW. The body won't ease its altitude below this,
        // so it can never descend through a roof -- it holds height until
        // it has moved horizontally clear (takeoff off a tall roof, a
        // descent passing over a shorter building). The horizontal
        // pathfinder already routes AROUND tall buildings at each cruise
        // tier; this covers the VERTICAL dimension the ease used to ignore.
        if (_canFly && _body != null)
            _body.SetDescentFloor(_builder != null ? _builder.SurfaceHeightAt(transform.position) : 0f);

        if (_body != null) _body.UpdateLocomotion(velocity, dt);
        // separation is a GROUND-plane push (transform y is always 0 --
        // altitude lives on the torso), so exempt units that aren't
        // actually standing in the crowd: an airborne flyer passing
        // overhead shouldn't shove ground units around, and a ground unit
        // walking past a building's base shouldn't shove a PERCHED flyer
        // sideways off its roof. Still unconditional even for a unit that
        // just ran SteerFollowPath this frame (docs/25 Phase B) -- Combine's
        // separation term is a SOFT early nudge folded into the heading, not
        // a substitute for this hard "never actually overlap" correction; a
        // standalone numeric harness confirmed dropping this call for
        // path-following units lets two units driving straight at a shared
        // destination interpenetrate, since a heading blend alone has no
        // floor on how much overlap it will tolerate.
        if (_fighter != null && _builder != null && !_flying && !Perched)
            _builder.ApplySeparation(_fighter);
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

    /// <summary>Fly to the target building's nearest footprint hex, then
    /// land on the roof (GoIdle's surface-aware landing does the actual
    /// touch-down; the roof IS the surface there). Always flies -- there
    /// is no walking onto a roof -- and manages its own path/flight state
    /// rather than going through ComputePath, whose DecideFlight could
    /// legitimately choose to WALK a short hop and then be standing in
    /// front of a wall with nowhere to go. Cruise tier must clear the
    /// TARGET building itself at minimum (landing approach comes from
    /// above); en-route obstacles taller than that tier bump it to High,
    /// which nothing blocks.</summary>
    private Vector3 TickPerch(float dt)
    {
        if (_targetBuilding == null) { GoIdle(); return Vector3.zero; }
        if (_builder.IsDestroyed(_targetBuilding))
        {
            // nothing left to land on -- abort wherever we are
            _targetBuilding = null;
            GoIdle();
            return Vector3.zero;
        }

        if (!_perchApproach && (_path == null || _pathCityVersion != _builder.CityVersion))
        {
            var start = _builder.HexAt(transform.position);
            var goal = _builder.HexAt(NearestFootprintPoint(_targetBuilding));
            _flying = true;
            _flyingHigh = _builder.BuildingHeight(_targetBuilding) + 2f > _flightLowAlt;
            if (_body != null) _body.SetFlying(true, _flyingHigh);
            var hexPath = HexPathfinder.FindPath(start, goal, _builder.City, Blocked());
            if (hexPath == null && !_flyingHigh)
            {
                // boxed in at low cruise -- climb; nothing blocks High
                _flyingHigh = true;
                if (_body != null) _body.SetFlying(true, true);
                hexPath = HexPathfinder.FindPath(start, goal, _builder.City, Blocked());
            }
            _pathGoalHex = goal;
            _path = ToWorldPath(hexPath);
            if (_path == null) { GoIdle(); return Vector3.zero; }
        }

        if (!_perchApproach)
        {
            var v = FollowPath(dt, RunOrWalkSpeed());
            if (_path == null) _perchApproach = true;   // main route done; line up over the roof hex
            return v;
        }

        // final approach: FollowPath's wide flying arrive-radius can leave
        // us up to ~8m off the roof-hex center -- close that gap before
        // touching down, so the surface under the landing is actually the
        // roof and not the street at the building's base
        var goalW = _builder.WorldOf(_pathGoalHex);
        var toGoal = goalW - transform.position;
        toGoal.y = 0f;
        if (toGoal.magnitude > 1.5f)
        {
            var dir = toGoal.normalized;
            var speed = RunOrWalkSpeed();
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up), dt * FlightTurnRate);
            transform.position += dir * Mathf.Min(speed * dt, toGoal.magnitude);
            return dir * speed;
        }
        _perchApproach = false;
        GoIdle();   // centered over the roof: land on it
        return Vector3.zero;
    }

    private const float SettleArriveDist = 0.6f;

    /// <summary>Idle creep toward a shared group-move cluster point
    /// (see OrderMove's settleTarget overload). ApplySeparation -- already
    /// called unconditionally every frame below -- stops the creep once
    /// RuntimeCityBuilder.groupSpacing of daylight opens up (an
    /// Inspector-tunable field, creator direction 2026-07), so the group
    /// packs down from its loose one-hex-apart walking spacing to a close
    /// (but never overlapping) rest formation once everyone's stopped.
    ///
    /// Speed is the creature's own physiology (a fraction of its walk
    /// speed, with a floor) rather than one flat "slow shuffle" constant
    /// for every creature -- creator direction, 2026-07: settling read as
    /// "extremely slow" at the old fixed 1.3 m/s regardless of how fast
    /// the creature actually walks.
    ///
    /// Terrain-aware on purpose ("must be cognizant of buildings and
    /// natural features"): each step is checked against the SAME blocked-
    /// hex set pathfinding uses (buildings, water) before committing, so a
    /// unit settling toward the group never clips into one -- it just
    /// stops dead at the boundary and gives up the creep for good.</summary>
    private Vector3 TickSettle(float dt)
    {
        if (_settleTarget.HasValue && _builder != null)
        {
            var target = _settleTarget.Value;
            var to = target - transform.position;
            to.y = 0f;
            var dist = to.magnitude;
            if (dist >= SettleArriveDist)
            {
                var settleSpeed = Mathf.Max(3.5f, (float)_profile.WalkMetersPerSecond(_builder.speedDisplayMultiplier) * 0.9f);
                var dir = to / dist;
                var step = Mathf.Min(settleSpeed * dt, dist);
                var next = transform.position + dir * step;

                var hex = _builder.HexAt(next);
                if (_builder.City.Contains(hex) && !Blocked().Contains(hex))
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir, Vector3.up), dt * 4f);
                    transform.position = next;
                    return dir * settleSpeed;
                }
            }
            _settleTarget = null;   // arrived, or a building/water blocked the last step
        }

        // group-move arrival facing (see GroupFacing / GoIdle): once the
        // creep above is done, turn to match whichever unit in this
        // group locked the shared facing first -- everyone ends up
        // looking the same direction, not just wherever their own last
        // step happened to point.
        if (_groupFacing != null && _groupFacing.Locked.HasValue)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                _groupFacing.Locked.Value, dt * 4f);
        }
        return Vector3.zero;
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
            // harvest into the onboard tank (docs/22): a real harvest tool
            // strips far more per body than teeth do -- the gathered load
            // is the citizen's yield scaled by this creature's blood-gather
            // rate, capped at what its vessel can hold. This is what makes a
            // lamprey-and-tank build a hauler and slows it as it fills.
            if (_harvest != null && _harvest.Capacity > 0.01f)
            {
                _carriedLoad = Mathf.Min((float)_harvest.Capacity,
                    _carriedLoad + 3f * (float)_harvest.GatherBlood);
            }
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
        // always chase at a run -- but a laden harvester still pays the
        // weight tax (a full blood-tanker can't sprint down a fresh victim)
        return FollowPath(dt, (float)_profile.RunMetersPerSecond(_builder.speedDisplayMultiplier) * LoadFactor());
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
        return (float)speed * LoadFactor();
    }

    /// <summary>Speed multiplier for the carried harvest load (docs/22):
    /// a laden carrier trudges, and a laden FLYER pays double for the
    /// weight -- both floored (never grounded/stalled) by the harvest
    /// math. Returns 1 when empty, so it's a no-op for everything that
    /// isn't hauling a full tank.</summary>
    private float LoadFactor()
    {
        if (_harvest == null || _carriedLoad <= 0.01f || _harvest.Capacity <= 0.01f) return 1f;
        var fill = _carriedLoad / (float)_harvest.Capacity;
        return (float)(_flying ? Harvest.FlightSpeedFactor(fill) : Harvest.GroundSpeedFactor(fill));
    }

    // flying units round hex-grid corners into arcs instead of snapping
    // heading-first at each waypoint: a much bigger "close enough, aim at
    // the NEXT leg now" radius cuts the corner early (a simple, cheap
    // stand-in for spline-fitting the path), and a slower yaw catch-up
    // sweeps through the turn instead of whipping the heading around --
    // creator direction, 2026-07: "turns are too sharp, they should be
    // more arcs." Ground creatures keep the tight/snappy original values;
    // this is purely a flight-feel change.
    private const float GroundArriveDist = 0.6f;
    private const float FlightArriveDist = 8f;     // well under a hex (20m) so it never cuts through a corner obstacle
    private const float GroundTurnRate = 5f;
    private const float FlightTurnRate = 1.8f;

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
        var arriveDist = _flying ? FlightArriveDist : GroundArriveDist;
        if (dist < arriveDist)
        {
            _pathIndex++;
            return FollowPath(dt, speed);
        }

        var dir = to / dist;
        // docs/25 Phase B/C: SteerFollowPath blends seek against a softened
        // separation nudge (ApplySeparation below still owns the hard
        // never-overlap guarantee) and time-to-collision predictive
        // avoidance -- a faster creature still overtakes a slower one
        // instead of piling into its back (no-op when clear), and a heavily
        // deflected unit's speedScale eases off instead of shoving at full
        // speed (Phase C's speed-modulation requirement). Airborne units
        // skip it entirely (steer = dir, speedScale = 1): everyone else is
        // on the ground plane far below, and dodging their shadows would
        // bend flight paths for no reason.
        var steer = dir;
        var speedScale = 1f;
        if (!_flying && _builder != null && _fighter != null)
        {
            var result = _builder.SteerFollowPath(_fighter, dir, speed);
            steer = result.Direction;
            speedScale = result.SpeedScale;
        }
        var turnRate = _flying ? FlightTurnRate : GroundTurnRate;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(steer, Vector3.up), dt * turnRate);

        if (_flying)
        {
            // CARVE, don't strafe: while airborne, velocity follows the
            // NOSE while the nose chases the target -- so a fresh order in
            // a new direction (even straight behind) sweeps through a
            // smooth banked arc instead of instantly translating sideways
            // with the heading lagging behind (creator direction, 2026-07:
            // "when the change of direction order is issued they must
            // transition to the new heading smoothly"). Turn radius =
            // speed / turn rate, a few meters -- comfortably inside the
            // flying arrive radius, so it can never orbit a waypoint.
            var nose = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            transform.position += nose * (speed * dt);
            return nose * speed;
        }

        var scaledSpeed = speed * speedScale;
        var step = Mathf.Min(scaledSpeed * dt, dist);
        transform.position += steer * step;
        return steer * scaledSpeed;
    }

    private const int FarHexThreshold = 5;      // ~100m straight-line (20m hex) counts as "far"
    private const float DetourFactor = 1.8f;    // ground route this much longer than a straight line = "high up" territory

    // energy weights for the low-vs-high cruise decision below: tuned so
    // a modest weave around one tall building beats climbing over it, but
    // a long detour (several buildings, or a wide one) loses to the climb
    // -- creator direction, 2026-07: "decide to fly up and over others
    // depending on what would take less energy." Units are arbitrary
    // (this is a comparison, not a real physics simulation); what matters
    // is their RATIO, so a bigger climb only pays off against a
    // proportionally bigger detour.
    private const float FlightEnergyPerHex = 1f;
    private const float FlightClimbEnergyPerUnit = 0.05f;

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
        // standing on a roof: every move starts with a takeoff -- there
        // is no walking down the side of a building
        if (_builder.SurfaceHeightAt(transform.position) > 0.1f) return true;
        var straight = start.DistanceTo(goal);
        if (straight == 0) return false;
        if (straight >= FarHexThreshold) return true;
        var groundPath = HexPathfinder.FindPath(start, goal, _builder.City, _builder.BlockedFor(false));
        if (groundPath == null) return true;   // no ground route at all
        return groundPath.Count > straight * DetourFactor;
    }

    /// <summary>Once flying, decide LOW (cruise under short buildings,
    /// weave around anything taller -- cheap climb, maybe a longer route)
    /// vs HIGH (climb above every building, fly the direct line -- a
    /// bigger climb, but zero detour) by comparing total energy: hex
    /// distance actually flown, plus the one-time cost of climbing to
    /// that tier's altitude. "Fly over low buildings and decide to fly up
    /// and over others depending on what would take less energy" --
    /// creator direction, 2026-07. A boxed-in low route (no path at all
    /// under the tall-building blocked set) always loses to climbing,
    /// since the high route is never blocked by anything.</summary>
    private bool DecideFlightTier(HexCoord start, HexCoord goal)
    {
        var straight = start.DistanceTo(goal);
        var lowPath = HexPathfinder.FindPath(start, goal, _builder.City, _builder.BlockedForFlight(_flightLowAlt));
        var highCost = straight * FlightEnergyPerHex + _flightHighAlt * FlightClimbEnergyPerUnit;
        var lowCost = lowPath != null
            ? lowPath.Count * FlightEnergyPerHex + _flightLowAlt * FlightClimbEnergyPerUnit
            : float.PositiveInfinity;
        return highCost < lowCost;
    }

    /// <summary>The blocked set for whatever this unit is doing RIGHT
    /// NOW. Flying ignores buildings shorter than its current cruise
    /// tier (BlockedForFlight) -- water never blocks flight either way,
    /// same as amphibious ground movement. Grounded movement keeps the
    /// creature's normal ground/amphibious rule.</summary>
    private HashSet<HexCoord> Blocked()
    {
        if (_flying) return _builder.BlockedForFlight(_flyingHigh ? _flightHighAlt : _flightLowAlt);
        return _builder.BlockedFor(_amphibious);
    }

    private void SetFlyingFor(HexCoord start, HexCoord goal)
    {
        _flying = DecideFlight(start, goal);
        _flyingHigh = _flying && DecideFlightTier(start, goal);
        if (_body != null) _body.SetFlying(_flying, _flyingHigh);
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
