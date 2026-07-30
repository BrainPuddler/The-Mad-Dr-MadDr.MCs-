using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.MatchCore;
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
    private enum OrderKind { Idle, Move, AttackBuilding, AttackUnit, EatCitizen, Perch, SpecialAttack, AttackMove }

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

    // 2026-07 creator direction ("press G... click on a monster, pick it
    // up... drop it onto the factory to clone"): grab-carry state, driven
    // externally by GrabCursor -- see IsHeld/BeginHeld/EndHeld/TickHeld.
    private bool _held;
    private float _wigglePhase;

    // 2026-07: roof "parking" -- the SPECIFIC footprint hex this perch
    // order was assigned to land on (WaypointCommander's AssignPerch),
    // distinct per unit so a group ordered onto one roof spreads across
    // its footprint instead of converging on one point. Null means "pick
    // your own nearest footprint hex" -- the original, single-unit
    // behavior, still used when nothing assigns one explicitly.
    private HexCoord? _perchTargetHex;

    // docs/23 Phase 5 follow-up: attack-move (walk toward `_attackMoveDestination`,
    // auto-detouring into a full AttackUnit fight on any spotted enemy) and
    // patrol (the same thing, but flipping between two ends forever). Both
    // ride the SAME OrderKind.AttackMove; `_isPatrolling` distinguishes a
    // one-shot arrival from "turn around and walk back." These deliberately
    // sit OUTSIDE ClearTargets()'s combat-detour set (TickAttackMove sets
    // _targetUnit/_order directly, not via OrderAttackUnit) so a fight
    // picked up mid-attack-move doesn't erase where the unit was headed --
    // but ClearTargets() itself still resets them, so any genuinely NEW
    // player order correctly cancels a pending attack-move/patrol.
    private HexCoord? _attackMoveDestination;
    private bool _isPatrolling;
    private HexCoord _patrolOtherEnd;

    // docs/27 Phase A: opt-in sim-driven movement. Null/false by default
    // for every existing scene (nothing calls EnableSimDriven today), so
    // this is fully inert until a dev/test scene explicitly wires it up
    // -- see docs/27 SS3/SS6.
    private SimBridge _simBridge;
    private SimUnitView _simView;
    private uint? _simEntityId;
    private int _simPlayerIndex;
    private bool SimDriven => _simEntityId.HasValue;

    // docs/26 (Special Attacks System) -- Phase 1-3: state-machine wiring
    // only, no ability effect resolution yet (that's WebAttackAbility,
    // a later phase). _activeSpecialAttack is the SAME SpecialAttackInstance
    // object as one entry in _fighter.Abilities (not a copy), so triggering
    // its cooldown here is the unit's actual, persistent cooldown state.
    private SpecialAttackInstance _activeSpecialAttack;
    private UnitCombat _targetSpecialAttackUnit;

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

    /// <summary>The genome this monster was built from -- 2026-07:
    /// GrabCursor's clone-onto-Factory feature needs the ORIGINAL genome
    /// to spawn copies of, the same way <see cref="RuntimeCityBuilder.
    /// SpawnMonster"/> already builds any monster from one.</summary>
    public StoredGenomeDto Creature { get { return _creature; } }

    /// <summary>Winged plan -- the commander routes a roof-click to a
    /// perch order for these, an attack order for everyone else.</summary>
    public bool IsFlyer { get { return _canFly; } }

    /// <summary>docs/25 Phase D: "wants to move and has a valid
    /// destination" -- the signal DeadlockManager polls to find a unit
    /// stalled in traffic. True while a grounded unit has an active path
    /// leg it's actively trying to walk (the Move order, or the approach
    /// leg of Attack/Eat/Perch before it's back in range/landed); false
    /// once idle, holding weapon range, settling, perched, or airborne --
    /// none of those are "stuck," they're doing exactly what they mean to
    /// do.</summary>
    public bool WantsToMove { get { return _path != null && !_flying; } }

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

    /// <summary>The building this unit is currently perched on, or null if
    /// it isn't perched right now (2026-07: roof "parking"/distribution --
    /// `RuntimeCityBuilder.AvailableRoofSlots` reads this off every
    /// monster live, rather than keeping a separate occupancy counter that
    /// could drift). `_targetBuilding` survives `GoIdle()` (only a NEW
    /// order clears it via `ClearTargets`), so it still correctly names
    /// the roof a unit landed on for as long as it holds that perch.</summary>
    public Building PerchedOn { get { return Perched ? _targetBuilding : null; } }

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
                case OrderKind.SpecialAttack: return "using " + (_activeSpecialAttack?.Definition != null ? _activeSpecialAttack.Definition.AbilityName : "a special attack");
                case OrderKind.AttackMove: return (_isPatrolling ? "patrolling" : "attack-moving") + air;
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
        // docs/26 Phase 9: every monster carries a secondary attack too,
        // chosen purely from its existing hand-family gene (no new
        // gene) -- alien-tech hands get the Psionic Tractor Beam, every
        // other creature (the Mad Doctor's default) gets Ground Stomp.
        _fighter.Abilities.Add(new SpecialAttackInstance(
            SecondaryAttackCatalog.ForMonster(creature.Genome.Slots.Hand.Family)));

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
        // docs/27 Phase A/B: single-unit moves (queued or not) are the
        // sim side's scope -- GROUP moves (settle target + GroupFacing)
        // never reach this overload at all (WaypointCommander's
        // AssignFormation calls the 4-arg OrderMove directly, below), so
        // they always stay on the legacy path regardless of SimDriven.
        // That's a deliberate, documented scope boundary (docs/27 SS3),
        // not an oversight.
        if (SimDriven) { OrderMoveViaSim(hex, queue); return; }
        OrderMove(hex, queue, null, null);
    }

    /// <summary>docs/27 Phase A/B: the sim-driven twin of the legacy
    /// OrderMove above -- queues a MoveTo (REPLACE) or MoveQueue (APPEND)
    /// command for the NEXT tick boundary (docs/27 SS5: one tick of input
    /// latency is correct lockstep behavior) instead of computing a path
    /// locally. `_order` still becomes Move so the existing Update()
    /// dispatch switch and OrderDescription/ClearTargets machinery all
    /// keep working unmodified; only WHICH TickX method runs changes (see
    /// TickMoveViaSim). A queued command on an already-Move unit does NOT
    /// re-clear `_order` or touch it further -- match-core owns whether
    /// this is "start now" or "wait behind what's in flight" (§27 Phase
    /// B), Unity just forwards the command.</summary>
    private void OrderMoveViaSim(HexCoord hex, bool queue)
    {
        if (!queue)
        {
            ClearTargets();
            _waypoints.Clear();
            _path = null;
            _simBridge.QueueMoveCommand(_simPlayerIndex, _simEntityId.Value, hex);
        }
        else
        {
            _simBridge.QueueWaypointCommand(_simPlayerIndex, _simEntityId.Value, hex);
        }
        _order = OrderKind.Move;
    }

    /// <summary>docs/27 Phase A: opt a spawned unit into sim-driven
    /// movement. Not called anywhere yet except the `simDrivenDemo` dev
    /// toggle; every other scene never calls it, so <see cref="SimDriven"/>
    /// stays false and every order kind (Move included) keeps running its
    /// legacy TickX method exactly as before. Called AFTER Init(), so
    /// `_fighter.Radius` (set by Init's own `Configure` call, from this
    /// creature's actual body size) is already meaningful -- docs/27 Phase
    /// C feeds it straight into match-core's own separation instead of
    /// falling back to a generic default, so a sim-driven unit's spacing
    /// matches its real body the same way the legacy path already
    /// does.</summary>
    public void EnableSimDriven(SimBridge bridge, int playerIndex, HexCoord atHex, double speed)
    {
        if (bridge == null || _simEntityId.HasValue) return;
        _simBridge = bridge;
        _simPlayerIndex = playerIndex;
        _simView = gameObject.AddComponent<SimUnitView>();
        var radius = _fighter != null ? (double)_fighter.Radius : MatchState.DefaultUnitRadius;
        _simEntityId = bridge.SpawnUnit(playerIndex, atHex, speed, _simView, radius);
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

    /// <summary>docs/23 Phase 5 follow-up: walk to `hex`, auto-engaging any
    /// enemy spotted within aggro range along the way (see TickAttackMove)
    /// instead of walking past it like a plain Move. Queueing works the
    /// same as OrderMove.</summary>
    public void OrderAttackMove(HexCoord hex, bool queue)
    {
        ClearTargets();
        if (!queue) { _waypoints.Clear(); _path = null; }
        _waypoints.Enqueue(hex);
        _attackMoveDestination = hex;
        _isPatrolling = false;
        _order = OrderKind.AttackMove;
    }

    /// <summary>docs/23 Phase 5 follow-up: attack-move back and forth
    /// forever between wherever this unit is standing right now and
    /// `destination`, auto-engaging along the way -- SC2-style patrol.
    /// Never queues (a patrol IS the whole order).</summary>
    public void OrderPatrol(HexCoord destination)
    {
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        var here = _builder != null ? _builder.HexAt(transform.position) : destination;
        _waypoints.Enqueue(destination);
        _attackMoveDestination = destination;
        _patrolOtherEnd = here;
        _isPatrolling = true;
        _order = OrderKind.AttackMove;
    }

    /// <summary>docs/26 (Special Attacks System): use `ability` on `unit`
    /// once in range. `ability` must be one of THIS unit's own
    /// `_fighter.Abilities` entries (same object, not a copy) so its
    /// cooldown actually persists -- called by `AcquireTarget` via
    /// `EvaluateBestAbility` (Phase 8) once an ability clears its own
    /// usefulness bar; callers are expected to have already checked
    /// `ability.IsReady` before issuing the order (EvaluateBestAbility
    /// does); TickSpecialAttack does not re-check it, the same way
    /// TickAttackUnit doesn't re-check ReadyToFire before approaching
    /// (only before actually firing).</summary>
    public void OrderSpecialAttack(SpecialAttackInstance ability, UnitCombat unit)
    {
        if (ability == null || ability.Definition == null || unit == null) return;
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _activeSpecialAttack = ability;
        _targetSpecialAttackUnit = unit;
        _order = OrderKind.SpecialAttack;
    }

    /// <summary>Fly to a building and land ON its roof (creator
    /// direction, 2026-07: "they should be able to land on the
    /// building"). Winged only -- the commander routes a roof-click here
    /// for flyers and to OrderAttack for everyone else, but guard anyway
    /// so a stray call can't strand a ground creature mid-wall.
    /// <paramref name="targetHex"/> (2026-07 roof "parking"): a SPECIFIC
    /// footprint hex to land on, assigned by
    /// `WaypointCommander.AssignPerch` when ordering a group so each unit
    /// gets its own spot instead of all converging on the same one hex.
    /// Null (the single-unit default) keeps the original behavior:
    /// `TickPerch` picks whichever footprint hex is nearest wherever this
    /// unit happens to be.</summary>
    public void OrderPerch(Building building, HexCoord? targetHex = null)
    {
        if (building == null) return;
        if (!_canFly) { OrderAttack(building); return; }
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _targetBuilding = building;
        _perchTargetHex = targetHex;
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
        _perchTargetHex = null;
        _activeSpecialAttack = null;
        _targetSpecialAttackUnit = null;
        _attackMoveDestination = null;  // any fresh order cancels a pending attack-move/patrol
        _isPatrolling = false;
        // 2026-07: any fresh order also ends the post-clone "resting on
        // the roof, slowly spinning" display beat -- same "any fresh
        // order cancels a pending X" law every other field here follows.
        if (_roofDisplay)
        {
            _roofDisplay = false;
            if (_body != null) _body.ForceTuckLegs = false;
        }
    }

    // ---- 2026-07: grab-carry (GrabCursor) ------------------------------------

    public bool IsHeld { get { return _held; } }

    /// <summary>Begin the grab-carry state: clears whatever this monster
    /// was doing (same as any fresh order) and hands control of its
    /// transform to <see cref="TickHeld"/>, called externally by
    /// GrabCursor every frame from here on -- this class's own Update()
    /// stops issuing orders/movement while held (see the top of Update()),
    /// though it still calls into <see cref="MonsterBody.UpdateLocomotion"/>
    /// with zero velocity so the legs stay properly synced to the
    /// wiggling torso instead of frozen. Creator direction: "disengage the
    /// feet from the ground when grabbed" -- <see cref="MonsterBody.
    /// ForceTuckLegs"/> folds them up the same way a flying creature's
    /// legs already tuck mid-air, since a grabbed creature has just as
    /// little ground under it.</summary>
    public void BeginHeld()
    {
        _held = true;
        _wigglePhase = 0f;
        ClearTargets();
        _waypoints.Clear();
        _path = null;
        _order = OrderKind.Idle;
        if (_fighter != null) _fighter.LastVelocity = Vector3.zero;
        if (_body != null) _body.ForceTuckLegs = true;
        EnsureGrabGlow();
        if (_grabGlow != null) _grabGlow.SetActive(true);
    }

    /// <summary>Ends the grab-carry state -- the very next Update() resumes
    /// normal per-frame ticking as a freshly-idle unit (AcquireTarget,
    /// TickSettle), exactly as if it had just arrived somewhere and
    /// stopped, since <see cref="BeginHeld"/> already left `_order` at
    /// Idle with no pending target. Re-engages the legs (see
    /// <see cref="BeginHeld"/>) -- the very next terrain-follow in
    /// Update() will settle it back onto the ground, and the next
    /// UpdateLocomotion call re-plants them from scratch there. Creator
    /// direction: "When the user drops the wiggling stops and the monster
    /// reset to normal orientation: body, arms and legs" -- root rotation
    /// snaps back to identity and the struggle phase resets to 0 (a calm,
    /// non-kicking tuck for the one frame before legs re-plant), not just
    /// "whatever it happened to be mid-squirm."</summary>
    public void EndHeld()
    {
        _held = false;
        transform.rotation = Quaternion.identity;
        _wigglePhase = 0f;
        if (_body != null)
        {
            _body.ForceTuckLegs = false;
            _body.StrugglePhase = 0f;
        }
        if (_grabGlow != null) _grabGlow.SetActive(false);
    }

    // 2026-07 refinement (creator direction: "the wiggling needs to be a
    // bit faster, with arm and legs struggling" -- a follow-up to the
    // earlier "slower, roll+pitch only" pass, not a reversion to the
    // original fast/spinning first draft). WiggleSpeed/Amplitude are the
    // slow overall squirm (roll+pitch only, no yaw); Thrash* layers a
    // faster, smaller jitter on top of THAT. Real independent limb
    // animation now exists for both: legs kick via MonsterBody.
    // StrugglePhase (the Airborne+ForceTuckLegs branch of
    // UpdateLocomotion), and the arm/weapon assembly swings up/down
    // around its own shoulder pivot via MonsterBody.UpdateShoulderSwing
    // (2026-07 follow-up: "rotate the shoulder joints up and down...
    // not over 30 degrees... NOT in the Y Axis") -- driven by the SAME
    // StrugglePhase so every limb reads as one struggling gesture, not
    // several independently-timed ones. The Thrash* jitter here stays as
    // an additional whole-body layer on top of that real limb motion.
    private const float WiggleSpeed = 3.0f;        // radians/sec -- up from the previous pass's 1.6
    private const float WiggleAmplitudeDeg = 16f;
    private const float ThrashSpeed = 11f;
    private const float ThrashAmplitudeDeg = 6f;

    /// <summary>Driven externally by GrabCursor once per frame while held
    /// -- creator direction: "pick it up... it will wiggle and squirm,"
    /// "with arm and legs struggling." Hovers the monster above
    /// `worldPos` (the cursor's current ground point, GrabCursor's own
    /// raycast) and layers a slow roll/pitch squirm plus a faster small
    /// thrash on top (no yaw component either way -- a creature straining
    /// against a grip, not spinning in place), drives <see
    /// cref="MonsterBody.StrugglePhase"/> so the legs kick in time with
    /// it, and keeps the grab-glow disc (see <see cref="TickGrabGlow"/>)
    /// tracking the ground point underneath.</summary>
    public void TickHeld(Vector3 worldPos, float dt)
    {
        _wigglePhase += dt * WiggleSpeed;
        var hoverHeight = _body != null ? Mathf.Max(1.5f, _body.BodyHeight * 0.6f) : 2f;
        transform.position = new Vector3(worldPos.x, worldPos.y + hoverHeight, worldPos.z);

        var pitch = Mathf.Sin(_wigglePhase) * WiggleAmplitudeDeg;
        var roll = Mathf.Sin(_wigglePhase * 0.77f + 1.3f) * WiggleAmplitudeDeg;   // different rate + phase offset so roll and pitch never lock into a single repeating figure
        var thrashPitch = Mathf.Sin(_wigglePhase * ThrashSpeed) * ThrashAmplitudeDeg;
        var thrashRoll = Mathf.Cos(_wigglePhase * ThrashSpeed * 1.1f) * ThrashAmplitudeDeg;
        transform.rotation = Quaternion.Euler(pitch + thrashPitch, 0f, roll + thrashRoll);

        if (_body != null) _body.StrugglePhase = _wigglePhase * 1.6f;

        TickGrabGlow(worldPos);
    }

    // ---- 2026-07: glowing pickup disc (GrabCursor) ----------------------------

    private GameObject _grabGlow;

    /// <summary>Creator direction: "Add a glowing disk under the monster
    /// with light that light up them model, make it luminous with a soft
    /// glow." Built once per agent, lazily on first grab (never for a
    /// monster that's never picked up), then just toggled active/inactive
    /// on every later grab/drop -- matches <see cref="GlowPointRegistry"/>'s
    /// own documented design: it's an APPEND-ONLY registry with no
    /// unregister lifecycle, gated by an `isEligible` predicate instead
    /// (the same pattern TrafficCar headlights already use for "only lit
    /// while driving at night"), so registering once and toggling
    /// eligibility via <see cref="IsHeld"/> is the intended shape here,
    /// not a workaround. Tier 1 (the disc's own emissive material) is
    /// always free/visible the instant it's active; Tier 2 (a real
    /// `Light` promoted from the registry) only lights the model up if
    /// this point wins the shared city-wide budget slot, same as every
    /// other glow point in the game -- a held creature doesn't get to
    /// skip the budget system other props already live under.</summary>
    private void EnsureGrabGlow()
    {
        if (_grabGlow != null) return;

        _grabGlow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _grabGlow.name = "GrabGlowDisc";
        // a CHILD of this agent (not a sibling under the shared monsters
        // host) specifically so Unity destroys it automatically if this
        // monster dies/is destroyed while never grabbed again -- TickGrabGlow
        // still positions/orients it in WORLD space every frame regardless,
        // so being parented under the wiggling root causes no visual drift.
        _grabGlow.transform.SetParent(transform, true);
        _grabGlow.transform.localScale = new Vector3(2.4f, 0.05f, 2.4f);   // Cylinder is 1 unit diameter x 2 tall at scale 1 -- squashed flat into a disc
        var collider = _grabGlow.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        var mat = new Material(ShaderUtil.FindRenderableShader());
        var glowColor = new Color(0.55f, 0.85f, 1f);   // cool energy-cyan -- a race-neutral "grabbed" cue, matching the mechanical (not gothic-red) claw redesign
        mat.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.55f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", glowColor * 2.2f);
        LabMeshBuilder.MakeTransparent(mat);
        var renderer = _grabGlow.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;

        GlowPointRegistry.Register(_grabGlow.transform, glowColor, isEligible: () => _held);

        _grabGlow.SetActive(false);
    }

    /// <summary>Keeps the glow disc under the cursor's current ground
    /// point while carried, a small offset above the terrain so it
    /// doesn't Z-fight with the ground it's sitting on. Sets WORLD
    /// position/rotation explicitly every frame (not local) -- being
    /// parented under this agent's own wiggling root (see
    /// <see cref="EnsureGrabGlow"/>'s own comment on why it's a child at
    /// all) would otherwise tilt the disc right along with the squirming
    /// torso; forcing world rotation to identity here keeps it flat on
    /// the ground regardless of whatever the root is doing above it.</summary>
    private void TickGrabGlow(Vector3 worldPos)
    {
        if (_grabGlow == null) return;
        _grabGlow.transform.position = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);
        _grabGlow.transform.rotation = Quaternion.identity;
    }

    // ---- 2026-07: post-clone roof display (GrabCursor) -----------------------

    private bool _roofDisplay;
    private const float RoofSpinDegPerSec = 40f;

    /// <summary>Creator direction: "When dropped into the factory, it
    /// should land on the roof and rotate slowly in the Y axis" -- AFTER
    /// resetting to normal orientation first (see <see cref="EndHeld"/>'s
    /// own doc comment for that reset; this method leaves the root
    /// rotation at identity too, same reset, before the spin below starts
    /// turning it). Called by GrabCursor instead of <see cref="EndHeld"/>
    /// when the drop landed on the player's own Factory -- legs stay
    /// tucked (nothing to plant a foot on up on a roof either) but
    /// STATIC, not kicking (`StrugglePhase` reset to 0 -- the struggle
    /// was specifically part of being carried, not part of resting on
    /// the roof). Persists until a real order is issued (see <see
    /// cref="ClearTargets"/>'s own new clause), the same "stays put until
    /// manually disturbed" contract <see cref="OrderPerch"/> already
    /// established for flyers resting on a roof.</summary>
    public void BeginRoofDisplay(Vector3 roofWorldPos)
    {
        _held = false;
        _roofDisplay = true;
        transform.position = roofWorldPos;
        transform.rotation = Quaternion.identity;
        _wigglePhase = 0f;
        if (_body != null)
        {
            _body.ForceTuckLegs = true;
            _body.StrugglePhase = 0f;
        }
        if (_fighter != null) _fighter.LastVelocity = Vector3.zero;
        if (_grabGlow != null) _grabGlow.SetActive(false);
    }

    private void TickRoofDisplay(float dt)
    {
        transform.Rotate(Vector3.up, RoofSpinDegPerSec * dt, Space.World);
        if (_body != null) _body.UpdateLocomotion(Vector3.zero, dt);
    }

    /// <summary>2026-07 (GrabCursor's clone-onto-Factory feature): give an
    /// already-idle unit a settle-creep destination directly, without
    /// issuing a real Move order -- "clones emerge and park themselves
    /// around the factory" reuses the SAME direct-line creep <see
    /// cref="TickSettle"/> already does for group-move arrival, just
    /// seeded here instead of via <see cref="OrderMove"/>'s own group-move
    /// path.</summary>
    public void SetSettleTarget(Vector3 worldPoint)
    {
        _settleTarget = worldPoint;
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

    private enum AttackMoveLegOutcome { Arrived, DetourEnded, Unreachable }

    /// <summary>docs/23 Phase 5 follow-up: stands in for a plain GoIdle()
    /// at the three points where a leg of movement completes -- arrival,
    /// an unreachable path, or a combat detour's target dying/vanishing.
    /// When there's no pending attack-move (`_attackMoveDestination` is
    /// null -- true for every ordinary Move/AttackUnit/etc. order), this
    /// IS exactly GoIdle(); every other order kind is completely
    /// unaffected. Otherwise: an unreachable leg gives up entirely; a
    /// one-shot arrival lands (GoIdle); a patrol arrival flips to the
    /// other end and keeps walking; and a detour ending (the enemy this
    /// unit chased down mid-attack-move died) resumes toward the original
    /// destination.</summary>
    private void GoIdleOrContinueAttackMove(AttackMoveLegOutcome outcome)
    {
        if (!_attackMoveDestination.HasValue) { GoIdle(); return; }

        if (outcome == AttackMoveLegOutcome.Unreachable)
        {
            _attackMoveDestination = null;
            _isPatrolling = false;
            GoIdle();
            return;
        }

        if (outcome == AttackMoveLegOutcome.Arrived && _isPatrolling)
        {
            var next = _patrolOtherEnd;
            _patrolOtherEnd = _attackMoveDestination.Value;
            _attackMoveDestination = next;
            _waypoints.Clear();
            _waypoints.Enqueue(next);
            _path = null;
            _order = OrderKind.AttackMove;
            return;
        }

        if (outcome == AttackMoveLegOutcome.Arrived)
        {
            _attackMoveDestination = null;
            GoIdle();
            return;
        }

        // DetourEnded: the enemy this unit turned aside to fight is gone --
        // resume toward the original attack-move/patrol destination.
        _waypoints.Clear();
        _waypoints.Enqueue(_attackMoveDestination.Value);
        _path = null;
        _order = OrderKind.AttackMove;
    }

    // ---- per-frame ----------------------------------------------------------

    private void Update()
    {
        var dt = Time.deltaTime;

        // 2026-07: grabbed by GrabCursor -- it drives this monster's
        // transform directly via TickHeld every frame; this Update() issues
        // no orders/separation/terrain-follow (GrabCursor owns the
        // transform outright) but still ticks the body's own locomotion
        // with zero velocity, purely so the tucked legs (ForceTuckLegs)
        // stay visually synced to the wiggling torso instead of frozen in
        // whatever pose they were in the instant before the grab.
        if (_held)
        {
            if (_body != null) _body.UpdateLocomotion(Vector3.zero, dt);
            return;
        }

        // 2026-07: resting on a Factory roof after a clone-drop -- see
        // BeginRoofDisplay's own doc comment. Same "own the transform
        // outright" contract as held, just spinning in place instead of
        // following a cursor.
        if (_roofDisplay)
        {
            TickRoofDisplay(dt);
            return;
        }

        if (_fighter != null && !_fighter.Alive)
        {
            if (_body != null) _body.UpdateLocomotion(Vector3.zero, dt);   // dead: stand still
            return;
        }

        // docs/26 Phase 6: a captured (non-heavy) monster is dragged
        // toward its captor instead of running its own order -- reachable
        // today only via a POSSESSED monster caught by its own side's web
        // (ordinary monsters are never valid targets of their own
        // faction's attack; see docs/26 "Possessed units and friendly
        // fire"). The paused order resumes automatically once released
        // (captor dies or a future phase ends the capture), since this
        // never touches `_order`.
        if (_fighter != null && _fighter.IsCaptured)
        {
            var moved = TickCaptured(dt);
            _fighter.LastVelocity = moved;
            if (_builder != null)
            {
                var pos = transform.position;
                var gy = _builder.GroundHeightAt(pos);
                if (!Mathf.Approximately(pos.y, gy)) transform.position = new Vector3(pos.x, gy, pos.z);
            }
            if (_body != null) _body.UpdateLocomotion(moved, dt);
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
            case OrderKind.Move: velocity = SimDriven ? TickMoveViaSim(dt) : TickMove(dt); break;
            case OrderKind.AttackMove: velocity = TickAttackMove(dt); break;
            case OrderKind.AttackBuilding: velocity = TickAttack(dt); break;
            case OrderKind.AttackUnit: velocity = TickAttackUnit(dt); break;
            case OrderKind.EatCitizen: velocity = TickEat(dt); break;
            case OrderKind.Perch: velocity = TickPerch(dt); break;
            case OrderKind.SpecialAttack: velocity = TickSpecialAttack(dt); break;
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
        //
        // docs/27 Phase C: also exempt a sim-driven unit -- match-core is
        // now the sole authoritative writer of its position (its own
        // per-tick separation pass, Flocking.Separate, already ran inside
        // SimBridge.Pump before this Update() call). This hard correction
        // writing transform.position directly would otherwise fight
        // SimUnitView's interpolated render every frame -- exactly the
        // "two positions disagreeing" risk docs/27 §5 flagged and
        // deliberately accepted until this phase closed it. Every
        // non-sim-driven unit (still every unit in every real scene) is
        // completely unaffected: SimDriven is false for them, so this
        // condition reduces to exactly what it was before.
        if (_fighter != null && _builder != null && !_flying && !Perched && !SimDriven)
            _builder.ApplySeparation(_fighter);
    }

    // shared with TickAttackMove -- how far an idle/attack-moving unit
    // looks for something to engage
    private const float AggroRangeMeters = 130f;

    private void AcquireTarget()
    {
        if (_fighter == null) return;

        // docs/26 Phase 8: a special attack that clears its own
        // usefulness bar takes priority over plain retaliation/engage --
        // a real tactical opportunity (several targets in one AoE) is
        // presumably worth more than a single regular shot at whoever
        // last hit this unit or is merely nearest.
        SpecialAttackInstance bestAbility;
        UnitCombat bestAnchor;
        if (EvaluateBestAbility(out bestAbility, out bestAnchor))
        {
            OrderSpecialAttack(bestAbility, bestAnchor);
            return;
        }

        if (_fighter.Weapon != null && _fighter.Weapon.CanAttack)
        {
            var attacker = _fighter.LastAttacker;
            if (attacker != null && attacker.Alive) { OrderAttackUnit(attacker); return; }
            var enemy = _builder.NearestEnemyOf(_fighter, AggroRangeMeters);
            if (enemy != null) { OrderAttackUnit(enemy); return; }
        }

        // 2026-07 creator direction: "if not attacking and humans are
        // around monsters will chase and consume them." Combat still
        // wins -- an armed unit that found a retaliation target or a
        // nearby enemy above already returned before reaching here; this
        // is only the fallback once there's genuinely nothing to fight.
        // Deliberately NOT gated behind the Weapon check above (unlike
        // that block): OrderEat/TickEat never touch _fighter/Weapon at
        // all, so a weaponless (or on-cooldown) monster still gets to
        // eat -- gating this behind the weapon check would silently
        // starve exactly the units least able to also fight back.
        if (_builder != null)
        {
            var citizen = _builder.NearestCitizenTo(transform.position, AggroRangeMeters);
            if (citizen != null) OrderEat(citizen);
        }
    }

    /// <summary>docs/26 Phase 8: picks the best ready special attack +
    /// anchor target to use right now, or false if none clears its own
    /// usefulness bar. For each equipped ability that's off cooldown
    /// (cooldown state), scans combatants within the ability's own Range
    /// of this unit (distance) and considers each one as a candidate
    /// anchor -- an enemy (or possessed "ally") this ability could be
    /// aimed at, using `WebAttackAbility.ShouldCatchCombatant` at the
    /// candidate's OWN position (impactPoint = candidate.position) to
    /// decide whether firing there would even catch it at all. For every
    /// anchor that passes, `WebAttackAbility.CountCatchable` scores it by
    /// how many combatants + citizens the AoE would actually catch there
    /// (weighted target count) -- reusing the resolver's own query/
    /// classification logic so this heuristic can never pick a target the
    /// resolver would then fail to catch. Only a score that clears the
    /// ability's own `Definition.MinTargetsInArea` (minimum usefulness
    /// threshold, a Phase 1 field left unused until now) is accepted; the
    /// highest-scoring ability+anchor across every equipped ability
    /// wins.
    ///
    /// docs/26 Phase 9: a SELF-CENTERED ability (Stun -- e.g. Ground
    /// Stomp, which resolves at this unit's own feet, not a target's) is
    /// scored differently: there's no target position to anchor on, so
    /// it's scored once against this unit's OWN position and, if it
    /// passes, `anchor` is set to `_fighter` itself -- not a real target,
    /// just a non-null UnitCombat so `OrderSpecialAttack`'s existing
    /// contract (which requires one) is satisfied. `TickSpecialAttack`'s
    /// approach-distance check against `_targetSpecialAttackUnit` then
    /// naturally reads as "already in range" (distance to self is
    /// zero) and fires immediately, with no separate no-travel-needed
    /// special case required there.</summary>
    private bool EvaluateBestAbility(out SpecialAttackInstance ability, out UnitCombat anchor)
    {
        ability = null;
        anchor = null;
        if (_fighter == null || _builder == null) return false;

        var bestScore = 0;
        var nearby = new List<UnitCombat>();
        for (var i = 0; i < _fighter.Abilities.Count; i++)
        {
            var candidate = _fighter.Abilities[i];
            if (candidate == null || !candidate.IsReady || candidate.Definition == null) continue;
            var def = candidate.Definition;

            if (def.EffectType == SpecialAttackEffectType.Stun)
            {
                var selfScore = WebAttackAbility.CountCatchable(_builder, _fighter, def, transform.position);
                if (selfScore > bestScore && selfScore >= def.MinTargetsInArea)
                {
                    bestScore = selfScore;
                    ability = candidate;
                    anchor = _fighter;
                }
                continue;
            }

            _builder.QueryCombatantsInRadius(transform.position, (float)def.Range, nearby);
            foreach (var enemy in nearby)
            {
                if (!WebAttackAbility.ShouldCatchCombatant(enemy, _fighter, def, enemy.transform.position)) continue;
                var score = WebAttackAbility.CountCatchable(_builder, _fighter, def, enemy.transform.position);
                if (score > bestScore && score >= def.MinTargetsInArea)
                {
                    bestScore = score;
                    ability = candidate;
                    anchor = enemy;
                }
            }
        }
        return ability != null;
    }

    /// <summary>docs/23 Phase 5 follow-up: like TickMove, but scans for an
    /// enemy in aggro range every tick first. Finding one detours straight
    /// into a real AttackUnit fight -- `_targetUnit`/`_order` are set
    /// DIRECTLY here, bypassing OrderAttackUnit's ClearTargets() call, so
    /// `_attackMoveDestination`/`_isPatrolling` survive the detour and
    /// TickAttackUnit's own dead-target branch (via
    /// GoIdleOrContinueAttackMove) knows to resume this order once the
    /// fight ends. No SimDriven equivalent yet (docs/23 §5: match-core has
    /// no sim-side attack-move concept) -- a sim-driven unit never reaches
    /// this method (Update()'s Move case still goes through
    /// TickMoveViaSim), so attack-move is legacy-path only for now.</summary>
    private Vector3 TickAttackMove(float dt)
    {
        var enemy = _builder != null ? _builder.NearestEnemyOf(_fighter, AggroRangeMeters) : null;
        if (enemy != null)
        {
            _targetUnit = enemy;
            _order = OrderKind.AttackUnit;
            return TickAttackUnit(dt);
        }
        return TickMove(dt);
    }

    private Vector3 TickMove(float dt)
    {
        if (_path == null)
        {
            if (_waypoints.Count == 0) { GoIdleOrContinueAttackMove(AttackMoveLegOutcome.Arrived); return Vector3.zero; }
            var goal = _waypoints.Dequeue();
            _path = ComputePath(goal);
            if (_path == null) { GoIdleOrContinueAttackMove(AttackMoveLegOutcome.Unreachable); return Vector3.zero; } // unreachable: stop rather than pretend
        }
        RecomputeIfCityChanged();
        return FollowPath(dt, RunOrWalkSpeed());
    }

    /// <summary>docs/27 Phase A: the sim-driven twin of TickMove above.
    /// Renders this frame's interpolated position (SimUnitView.Advance
    /// writes transform.position and returns the ACTUAL measured
    /// velocity, same idiom as TickCaptured, docs/26 Phase 6) and asks
    /// the sim -- not local state -- whether the order has completed.
    /// Deliberately does NOT call ApplySeparation/steering (docs/27 SS5:
    /// a known, documented capability gap for this first cutover --
    /// separation has no sim-side equivalent yet, and patching it onto
    /// the interpolation layer would just fight the sim's own authority
    /// over position).</summary>
    private Vector3 TickMoveViaSim(float dt)
    {
        var velocity = _simView.Advance(_simBridge.Alpha, dt, transform);
        if (_simBridge.OrderOf(_simEntityId.Value) == UnitOrderKind.Idle) GoIdle();
        return velocity;
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
            // 2026-07 roof "parking": land on the SPECIFIC hex this order
            // was assigned (AssignPerch), if any -- otherwise fall back to
            // the original single-unit behavior of picking whichever
            // footprint hex is nearest right now.
            var goal = _perchTargetHex ?? _builder.HexAt(NearestFootprintPoint(_targetBuilding));
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
    /// stops dead at the boundary and gives up the creep for good. Hex
    /// membership alone isn't quite enough, though: a building's rendered
    /// footprint (RuntimeCityBuilder.InsideBuildingFootprint's header
    /// explains why) is wider than its own hex and can overhang into a
    /// NEIGHBOURING hex that the blocked-hex set never flags -- exactly the
    /// gap a ring-settle target (its radius grows with group size,
    /// unbounded by anything in the city) can walk a creeping unit into.
    /// So a step is also rejected once it lands inside that overhang, not
    /// just once it lands in a blocked hex. Ground-only (`!_flying`) --
    /// a flyer's own altitude-aware Blocked() already decides what it can
    /// clear; this XZ footprint check has no notion of altitude and would
    /// wrongly ground a flyer passing safely above a short building.</summary>
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
                var clipsBuilding = !_flying && _builder.InsideBuildingFootprint(next);
                if (_builder.City.Contains(hex) && !Blocked().Contains(hex) && !clipsBuilding)
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
        if (_targetUnit == null || !_targetUnit.Alive)
        {
            _targetUnit = null;
            GoIdleOrContinueAttackMove(AttackMoveLegOutcome.DetourEnded);
            return Vector3.zero;
        }
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

    /// <summary>docs/26 (Special Attacks System), Phase 1-3: approach into
    /// range exactly like TickAttackUnit, then trigger the ability's
    /// cooldown and return to idle. Deliberately NO effect resolution
    /// yet -- no projectile, no area query, no damage/status/capture --
    /// this phase only proves the OrderKind/Tick/velocity-return contract
    /// and that a cooldown started here actually persists on
    /// _fighter.Abilities through the order returning to Idle afterward.
    /// The real effect (WebAttackAbility) hooks in here later by replacing
    /// the single TriggerCooldown() line below with an actual cast.</summary>
    private Vector3 TickSpecialAttack(float dt)
    {
        if (_activeSpecialAttack == null || _activeSpecialAttack.Definition == null
            || _targetSpecialAttackUnit == null || !_targetSpecialAttackUnit.Alive)
        {
            _activeSpecialAttack = null;
            _targetSpecialAttackUnit = null;
            GoIdle();
            return Vector3.zero;
        }

        var range = _activeSpecialAttack.Definition.Range;
        var to = _targetSpecialAttackUnit.AimPoint - transform.position;
        to.y = 0f;
        var dist = to.magnitude;

        if (dist <= range * 0.9f)
        {
            _path = null;
            var dir = dist > 0.01f ? to / dist : transform.forward;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up), dt * 6f);
            // docs/26 Phase 4/9: cast the actual ability -- cooldown
            // starts at DEPLOYMENT (this moment), not at impact, matching
            // UnitCombat.TryFire only reloading _cooldown once a shot
            // actually goes out. Which resolver runs is keyed on
            // Definition.EffectType: PullAndConsume (Web Attack, Psionic
            // Tractor Beam) launches a travelling projectile that resolves
            // on arrival; Damage (Flamethrower) and Stun (Ground Stomp)
            // resolve INSTANTLY, no projectile -- Stun centers on this
            // unit's own position (a stomp happens at the caster's feet,
            // not the target's), everything else centers on the target's.
            // An unrecognised EffectType still consumes the cooldown (the
            // cast attempt happened) but has no effect, rather than
            // silently doing nothing forever.
            var def = _activeSpecialAttack.Definition;
            if (def.EffectType == SpecialAttackEffectType.PullAndConsume)
                WebAttackAbility.Launch(_builder, _fighter, def, _targetSpecialAttackUnit.transform.position, Muzzle());
            else if (def.EffectType == SpecialAttackEffectType.Damage)
                SpecialAttackResolver.ResolveInstant(_builder, _fighter, def, _targetSpecialAttackUnit.transform.position);
            else if (def.EffectType == SpecialAttackEffectType.Stun)
                SpecialAttackResolver.ResolveInstant(_builder, _fighter, def, transform.position);
            // docs/26 Phase 10: every cast draws down the wallet -- soft,
            // never blocks the cast itself (see SpendWalletForCast).
            if (_builder != null) _builder.SpendWalletForCast(def.BloodCost, def.BonesCost);
            _activeSpecialAttack.TriggerCooldown();
            _activeSpecialAttack = null;
            _targetSpecialAttackUnit = null;
            GoIdle();
            return Vector3.zero;
        }

        var targHex = _builder.HexAt(_targetSpecialAttackUnit.transform.position);
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
        _activeSpecialAttack = null;
        _targetSpecialAttackUnit = null;
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
            CreditHarvestForEatenCitizen();
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

    /// <summary>Harvest the onboard tank's share of an eaten citizen
    /// (docs/22): a real harvest tool strips far more per body than teeth
    /// do -- the gathered load is the citizen's yield scaled by this
    /// creature's blood-gather rate, capped at what its vessel can hold.
    /// This is what makes a lamprey-and-tank build a hauler and slows it
    /// as it fills. Shared by TickEat (a direct chase-and-eat order) and
    /// <see cref="NotifyCapturedCitizenEaten"/> (docs/26 follow-up: a web-
    /// captured citizen eaten on arrival) so both count as the same real
    /// kill instead of a web catch being a silently lesser one.</summary>
    private void CreditHarvestForEatenCitizen()
    {
        if (_harvest != null && _harvest.Capacity > 0.01f)
        {
            _carriedLoad = Mathf.Min((float)_harvest.Capacity,
                _carriedLoad + 3f * (float)_harvest.GatherBlood);
        }
    }

    /// <summary>docs/26 follow-up: called by a Citizen this unit captured
    /// (via a special attack, e.g. a Web Attack) once it's actually eaten
    /// on arrival -- Citizen has no reference back to the MonsterAgent
    /// that captured it, only to the UnitCombat it was dragged toward, so
    /// it finds this via `GetComponent&lt;MonsterAgent&gt;()` on that same
    /// GameObject (MonsterAgent.Init adds `_fighter` to `gameObject`
    /// itself, so the two are always co-located). Mirrors TickEat's own
    /// harvest credit exactly, so a web-captured citizen fills the
    /// harvest tank the same as a directly chased-and-eaten one.</summary>
    public void NotifyCapturedCitizenEaten()
    {
        CreditHarvestForEatenCitizen();
    }

    // ---- movement mechanics --------------------------------------------------

    /// <summary>docs/26 Phase 6: advances this unit's capture-pull and
    /// derives a velocity from the actual displacement (rather than an
    /// intended direction*speed, like the Tick* methods above) since
    /// `CaptureState.TickPull` moves the transform directly -- matching
    /// how Citizen.MoveToward's callers never separately compute a
    /// velocity either.</summary>
    private Vector3 TickCaptured(float dt)
    {
        var before = transform.position;
        _fighter.TickCapture(dt);
        if (dt <= 0.0001f) return Vector3.zero;
        var moved = transform.position - before;
        moved.y = 0f;
        return moved / dt;
    }

    private float RunOrWalkSpeed()
    {
        // run for long hauls when the heart has headroom; walk otherwise
        var farToGo = _path != null && _path.Count - _pathIndex > 6;
        var mayRun = _profile.Sprint != "none";
        var speed = farToGo && mayRun
            ? _profile.RunMetersPerSecond(_builder.speedDisplayMultiplier)
            : _profile.WalkMetersPerSecond(_builder.speedDisplayMultiplier);
        // docs/26 Phase 5: a caught heavy target reads its slow status
        // straight off _fighter -- generic to every monster, no per-type
        // wiring needed (see UnitCombat.SpeedMultiplier).
        var slow = _fighter != null ? _fighter.SpeedMultiplier : 1f;
        return (float)speed * LoadFactor() * slow;
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
            // docs/23 §9 hardening audit: clamp the per-frame step to
            // FlightArriveDist (8m, already "well under a hex" by this
            // constant's own comment above) -- a no-op at any normal frame
            // timing (speed*dt is far smaller than 8m for any sane cruise
            // speed), but it bounds the worst case if dt ever spikes (a
            // frame hitch) so a fast flyer can't cut through a hex-corner
            // obstacle in one oversized step. Magnitude only -- the nose/
            // banking direction this phase's own "carve, don't strafe"
            // comment describes is untouched.
            var noseStep = Mathf.Min(speed * dt, FlightArriveDist);
            transform.position += nose * noseStep;
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
