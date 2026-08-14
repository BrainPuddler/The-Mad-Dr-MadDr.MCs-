using MadDr.CityGen;
using MadDr.MatchCore;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A possessed human (2026-07 worker-economy epic): what a Collector's
/// capture turns a Citizen into on arrival (<see
/// cref="RuntimeCityBuilder.OnCitizenPossessed"/>), the explicit
/// "SCV-from-StarCraft" analogy the creator named directly. Tank.cs's
/// pattern -- a bespoke, non-genome MonoBehaviour with a plain
/// `UnitCombat` -- not MonsterAgent's genome-driven one, since a Worker
/// has no creature DNA behind it.
///
/// 2026-08 (docs/12 "Zombie" entry, creator direction: "zombie could act
/// like SCV (starcraft) used to automatically scavenge resources, build
/// things and be cannon fodder I choose, could also inflict damage but a
/// lot less than full monsters. Zombie hord sort of thing. Monsters
/// would still collect resources."): "Zombie" is a display-only fiction
/// layer over this SAME class/field (`Worker`/`Workers`/`WorkerCount`
/// stay the identifiers everywhere -- match-core's own "stats generic,
/// names themed" convention, `BuildingDef.cs`'s own header) -- three
/// AskUserQuestion answers, all the recommended option, shaped this:
///
/// 1. Same unit as today's possessed-citizen Worker, upgraded -- not a
///    second, separate unit type.
/// 2. Auto-gathers ONLY destroyed-building debris (never citizens --
///    that stays a bred Monster's own job, per "monsters would still
///    collect resources").
/// 3. Real active construction: a Worker must be physically staffing an
///    UnderConstruction building for it to progress at all (<see
///    cref="SimBuilding.IsStaffed"/>, match-core) -- not just a
///    placement-time gate like the pre-existing `RequiresWorker` check.
///
/// Idle priority (2026-08 creator direction: "workers need to be
/// searching for and gathering resources, unless called back to build a
/// building" -- reversed from the original order): scavengeable debris
/// FIRST, an unstaffed friendly construction site only as the fallback
/// when there's nothing left to gather, then just stand there --
/// "monsters would still collect resources" is why citizens are never a
/// Worker target. There's still no separate player-issued "go build"
/// order (Worker's vocabulary stays move-only, see WaypointCommander's
/// own comment) -- a construction site only ever gets auto-staffed once
/// gathering runs dry, which is what "called back" means here: nothing
/// pulls a Worker off scavenging early, it just naturally falls through
/// to building once there's nothing nearby left to scavenge.
/// Combat is auto-aggro within a short leash (<see cref="AggroRadius"/>,
/// same `NearestEnemyOf`/`TryFire` pattern <see cref="Tank"/> already
/// uses) -- zombie-horde mindless violence overrides economic work the
/// instant an enemy is close, "I choose" is satisfied by the unit being
/// selectable/move-orderable (<see cref="WaypointCommander"/>'s own new
/// parallel `_selectedWorkers` path) even though attacks themselves
/// aren't a separate player-issued order in this v0.1 pass.
///
/// 2026-08 follow-up (docs/12 "scavenging-site" entry, creator direction:
/// "more workers assigned to a site must clear it faster... workers
/// should contribute concurrently, not wait for one another... avoid
/// fixed delays"): scavenging used to be one instant full-pile gulp the
/// moment a single Worker arrived (<see
/// cref="RuntimeCityBuilder.DrainBuildingScavenge"/> called with
/// `int.MaxValue`) -- meaning a SECOND Worker reaching the same wreck
/// found nothing left, so more Workers never cleared anything faster,
/// they just raced for who got there first. Reworked into a persistent
/// <see cref="ZombieState.Scavenging"/> state: each Worker independently
/// requests its own small `ScavengeRatePerSecond`-sized slice every
/// tick for as long as it stays in range. Concurrency needs no shared
/// "how many Workers are here" bookkeeping at all -- Unity's single-
/// threaded `Update()` order means N Workers' independent per-frame
/// drain requests against the SAME `BuildingRuntimeState` simply SUM
/// (superposition), so N Workers drain at N times the rate for free.
///
/// 2026-08 (creator brief: "Character System Overhaul -- Replace Capsule
/// Characters"): the old single-capsule-plus-hard-hat body is gone --
/// <see cref="BuildModel"/> now builds a <see cref="HumanCharacterKit"/>
/// rig and every movement/work/idle/death method below drives it via
/// <see cref="HumanCharacterAnimator"/>, in ADDITION to the exact same
/// `transform.position`/`transform.rotation` gameplay logic this class
/// already had -- visuals only, nothing about state machine, AI
/// priority, combat, or the economy above changed. This is also where
/// the brief's "Human Workers... Human Soldiers... Mad Doctor Workers...
/// Alien Workers" visual split actually lives for THIS unit: Worker is
/// still the one class for every faction (see this header's own
/// "AskUserQuestion answers" above), but <see cref="BuildModel"/> now
/// picks a <see cref="HumanCharacterProfile"/> -- Human/Mad-Doctor/Alien
/// -- from <see cref="RuntimeCityBuilder.chosenFaction"/> (Worker only
/// ever belongs to the local human player, see <see
/// cref="RuntimeCityBuilder.OnCitizenPossessed"/>'s own comment on why
/// there's no per-instance owner to look up instead). "Human Soldier" is
/// a distinct new unit, not a Worker skin -- see <see cref="HumanSoldier"/>.
/// </summary>
public class Worker : MonoBehaviour
{
    private const float MoveSpeed = 4f;
    private const float BuildReach = 3.5f;
    private const float ScavengeReach = 4f;
    private const float SearchRadius = 70f;
    private const float AggroRadius = 25f;   // short zombie leash -- Tank's own 150f is a patrolling combat unit's awareness, not cannon fodder's
    private const float ArriveThreshold = 1.5f;
    private const float DeathDestroyDelay = 0.5f;   // must exceed HumanCharacterAnimator's own collapse duration (0.35s) so the pose finishes settling before the GameObject vanishes

    // 2026-08 (scavenging-site redesign): per-Worker continuous drain
    // rate -- Small (100)/Medium (200)/Large (400)/Landmark (800) wrecks
    // (BuildingStats.ScavengeValue) clear solo in ~10/20/40/80s, N
    // Workers concurrently in ~1/N of that (pure superposition, see this
    // class's own header). `ScavengeTickInterval` throttles how often
    // the actual (relatively expensive, O(buildings-in-city)) <see
    // cref="RuntimeCityBuilder.DrainBuildingScavenge"/> lookup runs --
    // NOT a "wait": the Worker never stops or idles for it, it just
    // batches ~7 real drain calls/second into one slightly larger
    // request instead of 60/second into tiny ones, so a city with many
    // simultaneously-scavenging Workers doesn't re-scan the whole
    // destroyed-building list every single rendered frame per Worker.
    private const float ScavengeRatePerSecond = 10f;
    private const float ScavengeTickInterval = 0.15f;

    // 2026-08 (creator direction: "They also must deliver scavenged
    // parts to factories or whatever we have for collections centres"):
    // an onboard tank, same shape as MonsterAgent's harvester
    // (_carriedBlood/_carriedBones/_carriedBrains/_carriedParts,
    // capacity-gated, banked only on arrival at the Factory) -- a real
    // reversal of the scavenging-site redesign's own "no onboard tank on
    // a Worker... banks straight to the wallet" comment above, done
    // deliberately: draining still sums concurrently across every
    // Worker at a site exactly as before (this only changes what happens
    // AFTER a slice is drained, not the drain itself), so "more Workers
    // clear a site faster" still holds. `WorkerCarryCapacity` is a v0.1
    // invented number (CLAUDE.md's standing policy) -- not claimed
    // balanced, just enough that a delivery trip means something instead
    // of banking one drain-tick's worth every single time.
    private const float WorkerCarryCapacity = 50f;
    private float _carriedParts;

    private enum ZombieState { Idle, PlayerMove, SeekBuild, Staffing, SeekScavenge, Scavenging, Wander, SeekDeliver }
    private ZombieState _state = ZombieState.Idle;
    private Vector3 _moveTarget;
    private uint? _stationedBuildingId;
    private Building _scavengeTarget;
    private float _scavengeTickTimer;

    // 2026-08 (creator direction: "Add a herding behaviour to the
    // workers. in groups of 3 to 10 and a wander toggle enabled if there
    // is nothing to do radius around our buildings of 2 km") -- see
    // TickWander's own header for the full design. `WanderSpeedFraction`
    // is a v0.1 invented number (CLAUDE.md's standing policy) -- casual
    // wandering should read slower than purposeful walking, not claimed
    // balanced against anything.
    private const float WanderStepMin = 15f;
    private const float WanderStepMax = 35f;
    private const float WanderLeashRadius = 2000f;   // 2 km -- HexCoord.HexMeters confirms world units are meters
    private const float HerdJoinRadius = 12f;
    private const int MaxHerdSize = 10;
    private const float WanderRepickInterval = 4f;
    private const float WanderSpeedFraction = 0.55f;
    private int _wanderPickSalt;
    private float _wanderRepickTimer;

    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;

    // 2026-08 character-overhaul fields -- see this class's own header.
    // `_groundOffset` replaces the old fixed capsule-center height: 0 for
    // every walking profile (feet ON the ground, same as before), a real
    // hover height for the Alien profile ("no visible footsteps" is a
    // literal ground clearance here, not just an animation choice).
    private HumanCharacterRig _rig;
    private HumanCharacterAnimState _animState;
    private float _groundOffset;
    private bool _twitchyIdle;
    private float _frameMoveDistance;
    private bool _dying;
    private float _deathTimer;

    // 2026-08 (creator report: "same problem as the monsters, walking
    // directly toward each other and not wandering. Why can't you use
    // the monster navigation system?"): a Worker never had a real
    // reason not to -- see GroundPathFollower's own header. Every
    // movement Tick* method below (PlayerMove/SeekBuild/SeekScavenge/
    // Wander/SeekDeliver) now routes through this instead of a raw
    // straight-line step -- real HexPathfinder A* routing plus
    // MonsterAgent-grade local steering, not a new invention. TickCombat
    // (chasing a MOVING enemy at close range) deliberately still steps
    // directly -- full-repathing against a target that changes position
    // every frame would be wasted work for no benefit, the same reason
    // MonsterAgent's own combat-chase logic doesn't route through
    // FollowPath either.
    private readonly GroundPathFollower _pathFollower = new GroundPathFollower();

    public UnitCombat Combat { get { return _combat; } }

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        BuildModel();
        _combat = gameObject.AddComponent<UnitCombat>();
        // 2026-08: real but deliberately weak weapon (WeaponProfile.
        // ZombieClaws, the weakest concrete stat block in the codebase --
        // "cannon fodder... a lot less than full monsters") -- was
        // `weapon: null` (no combat at all) before this entry.
        _combat.Configure("monster", 40f, 0.5f, 1.1f, WeaponProfile.ZombieClaws(), OnDied, mass: 1f);
    }

    private void Update()
    {
        if (_builder == null) return;
        var dt = Time.deltaTime;

        // 2026-08: checked BEFORE the _combat.Alive guard below on
        // purpose -- once UnitCombat marks this Worker dead, `!Alive`
        // would hit that guard's early `return` every frame forever,
        // which is exactly what used to happen when OnDied() destroyed
        // the GameObject immediately. Now OnDied() defers destruction
        // instead (see its own comment) so "quick collapse, no
        // ragdolls" has time to actually play.
        if (_dying)
        {
            _deathTimer -= dt;
            HumanCharacterAnimator.TickDeath(_rig, _animState, dt);
            if (_deathTimer <= 0f) Object.Destroy(gameObject);
            return;
        }

        if (_combat == null || !_combat.Alive) return;

        if (_combat.IsCaptured)
        {
            _combat.TickCapture(dt);
            SnapToGround();
            return;
        }

        var enemy = _builder.NearestEnemyOf(_combat, AggroRadius);
        if (enemy != null)
        {
            _frameMoveDistance = 0f;
            TickCombat(enemy, dt);
            HumanCharacterAnimator.TickLocomotion(_rig, _animState, _frameMoveDistance, running: true, dt);
            _builder.ApplySeparation(_combat);
            SnapToGround();
            return;
        }

        _frameMoveDistance = 0f;
        switch (_state)
        {
            case ZombieState.PlayerMove: TickPlayerMove(dt); break;
            case ZombieState.SeekBuild: TickSeekBuild(dt); break;
            case ZombieState.Staffing: TickStaffing(dt); break;
            case ZombieState.SeekScavenge: TickSeekScavenge(dt); break;
            case ZombieState.Scavenging: TickScavenging(dt); break;
            case ZombieState.Wander: TickWander(dt); break;
            case ZombieState.SeekDeliver: TickSeekDeliver(dt); break;
            default: TickIdle(); break;
        }
        DriveIdleOrMoveAnimation(dt);

        _builder.ApplySeparation(_combat);
        SnapToGround();
    }

    /// <summary>Animates locomotion for every state that just walks
    /// somewhere (PlayerMove/SeekBuild/SeekScavenge/Wander) or stands
    /// doing nothing visual of its own (Idle) -- Staffing and Scavenging
    /// drive their OWN Build/Harvest animation directly from
    /// <see cref="TickStaffing"/>/<see cref="TickScavenging"/> instead,
    /// since those are stationary work poses, not locomotion. Wander's
    /// slower pace (see its own comment) needs no special case here --
    /// TickLocomotion is driven by actual distance moved this frame, so
    /// a smaller `_frameMoveDistance` already reads as a slower gait on
    /// its own. SeekDeliver gets its own branch -- "carry resources
    /// visibly" (the brief's own Human Worker line) means the delivery
    /// walk uses `TickCarry`'s raised-arms hold pose, not a plain
    /// walk-cycle, on legged rigs; a legless (Alien) rig has no "carry"
    /// pose of its own (nothing humanoid-armed to raise), so it keeps
    /// hovering normally instead.</summary>
    private void DriveIdleOrMoveAnimation(float dt)
    {
        switch (_state)
        {
            case ZombieState.PlayerMove:
            case ZombieState.SeekBuild:
            case ZombieState.SeekScavenge:
            case ZombieState.Wander:
                if (_rig.HasLegs) HumanCharacterAnimator.TickLocomotion(_rig, _animState, _frameMoveDistance, running: false, dt);
                else HumanCharacterAnimator.TickHover(_rig, _animState, _frameMoveDistance / Mathf.Max(dt, 0.0001f), dt);
                break;
            case ZombieState.SeekDeliver:
                if (_rig.HasLegs) HumanCharacterAnimator.TickCarry(_rig, _animState, _frameMoveDistance, dt);
                else HumanCharacterAnimator.TickHover(_rig, _animState, _frameMoveDistance / Mathf.Max(dt, 0.0001f), dt);
                break;
            default:
                if (_rig.HasLegs) HumanCharacterAnimator.TickIdle(_rig, _animState, _twitchyIdle, dt);
                else HumanCharacterAnimator.TickHover(_rig, _animState, 0f, dt);
                break;
        }
    }

    private void SnapToGround()
    {
        var p = transform.position;
        var gy = _builder.GroundHeightAt(p) + _groundOffset;
        if (!Mathf.Approximately(p.y, gy)) transform.position = new Vector3(p.x, gy, p.z);
    }

    private void TickCombat(UnitCombat enemy, float dt)
    {
        var to = enemy.AimPoint - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        var range = (float)_combat.Weapon.Range;
        if (dist > range * 0.85f)
        {
            var dir = to / Mathf.Max(dist, 0.0001f);
            var step = dir * (MoveSpeed * dt * _combat.SpeedMultiplier);
            transform.position += step;
            _frameMoveDistance = step.magnitude;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
        }
        else
        {
            _combat.TryFire(enemy, transform.position + Vector3.up * 1f);
        }
    }

    /// <summary>Player-issued move order (<see
    /// cref="WaypointCommander"/>'s new worker order path) -- abandons
    /// whatever auto-work this Worker was doing, unstaffing a
    /// construction site first so it doesn't stay falsely marked staffed
    /// forever.</summary>
    public void OrderMoveTo(Vector3 destination)
    {
        UnstaffIfStaffing();
        _stationedBuildingId = null;
        _scavengeTarget = null;
        _scavengeTickTimer = 0f;
        _moveTarget = destination;
        _state = ZombieState.PlayerMove;
    }

    private void TickPlayerMove(float dt)
    {
        var to = _moveTarget - transform.position;
        to.y = 0f;
        if (to.magnitude <= ArriveThreshold) { _pathFollower.Clear(); _state = ZombieState.Idle; return; }

        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(_moveTarget));
        _pathFollower.Tick(_builder, transform, _combat, dt, MoveSpeed);
        _frameMoveDistance = _pathFollower.LastStepDistance;
    }

    /// <summary>Idle-time decision: a carried load takes priority over
    /// everything else (deliver it before picking up more work -- this
    /// only fires if a player-issued move order interrupted a delivery
    /// trip mid-route, since <see cref="TickScavenging"/> itself always
    /// transitions straight to <see cref="ZombieState.SeekDeliver"/>,
    /// never back through Idle, whenever it's carrying anything). Then
    /// destroyed-building debris ("automatically scavenge resources,"
    /// the default so a Worker is always out searching for/gathering
    /// something rather than standing still), an unstaffed friendly
    /// construction site ("build things," only once there's nothing left
    /// to gather), and if NONE of those apply, wander instead of
    /// standing frozen (<see cref="BeginWander"/>) -- never a citizen
    /// search ("monsters would still collect resources," creator
    /// direction).</summary>
    private void TickIdle()
    {
        if (_carriedParts > 0.01f) { _state = ZombieState.SeekDeliver; return; }
        if (!TryFindRealWork()) BeginWander();
    }

    /// <summary>Shared by <see cref="TickIdle"/> (every frame while
    /// genuinely idle) and <see cref="TickWander"/> (every repick
    /// interval while wandering, so a herding Worker periodically "looks
    /// around" and drops out of the herd the instant real work is nearby
    /// -- "wander toggle enabled IF there is nothing to do" cuts both
    /// ways: it turns off again the moment there's something to
    /// do).</summary>
    private bool TryFindRealWork()
    {
        var debris = _builder.NearestScavengeableBuildingTo(transform.position, SearchRadius);
        if (debris != null)
        {
            _scavengeTarget = debris;
            _state = ZombieState.SeekScavenge;
            return true;
        }
        var site = _builder.NearestUnstaffedConstructionSite(transform.position, SearchRadius);
        if (site != null)
        {
            _stationedBuildingId = site.EntityId;
            _state = ZombieState.SeekBuild;
            return true;
        }
        return false;
    }

    /// <summary>2026-08 (creator direction: "Add a herding behaviour to
    /// the workers. in groups of 3 to 10 and a wander toggle enabled if
    /// there is nothing to do radius around our buildings of 2 km"):
    /// nothing to scavenge or build -- rather than stand frozen, this
    /// Worker either joins a nearby herd already wandering (<see
    /// cref="RuntimeCityBuilder.TryFindJoinableHerd"/> -- a decentralized,
    /// single-pass approximation; "3 to 10" is a SOFT target this nudges
    /// toward, not a hard-guaranteed range, see that method's own
    /// comment for why a real reservation system would be needed for
    /// more than that) or seeds a fresh wander point of its own within
    /// <see cref="WanderLeashRadius"/> of our own buildings.</summary>
    private void BeginWander()
    {
        _moveTarget = PickWanderTarget();
        _wanderRepickTimer = WanderRepickInterval;
        _state = ZombieState.Wander;
    }

    public bool IsWandering { get { return _state == ZombieState.Wander; } }
    public Vector3 WanderTarget { get { return _moveTarget; } }

    /// <summary>Walks the shared/seeded wander destination at a slower,
    /// casual pace (<see cref="WanderSpeedFraction"/>) than purposeful
    /// work-seeking. Re-checks for real work and re-picks (join-or-seed)
    /// every <see cref="WanderRepickInterval"/> or on arrival, rather
    /// than every frame -- cheap enough at hundreds of Workers, and a
    /// herd that re-evaluates every frame would never settle into a
    /// visually coherent group in the first place.</summary>
    private void TickWander(float dt)
    {
        _wanderRepickTimer -= dt;
        var to = _moveTarget - transform.position;
        to.y = 0f;
        var arrived = to.magnitude <= ArriveThreshold;
        if (arrived || _wanderRepickTimer <= 0f)
        {
            if (TryFindRealWork()) { _pathFollower.Clear(); return; }   // real work appeared nearby -- drop the herd, go do it
            _moveTarget = PickWanderTarget();
            _wanderRepickTimer = WanderRepickInterval;
            return;
        }

        // 2026-08 (creator report: "same problem as the monsters, walking
        // directly toward each other... why can't you use the monster
        // navigation system?"): real routing + local steering instead of
        // a raw straight-line step, same as every other Worker movement
        // state -- see GroundPathFollower's own header.
        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(_moveTarget));
        var pathDone = _pathFollower.Tick(_builder, transform, _combat, dt, MoveSpeed * WanderSpeedFraction);
        _frameMoveDistance = _pathFollower.LastStepDistance;
        // the wander target itself turned out unreachable (or the path
        // finished but left us short of ArriveThreshold, e.g. the goal-
        // substitution landed on a building's neighbour rather than the
        // exact point) -- re-pick immediately rather than sit doing
        // nothing for up to WanderRepickInterval. "Movement is preferred
        // over standing" (creator direction) applies here just as much
        // as it does to PickWanderTarget's own retry loop.
        if (pathDone && !arrived)
        {
            _moveTarget = PickWanderTarget();
            _wanderRepickTimer = WanderRepickInterval;
        }
    }

    // 2026-08 (creator report: "workers are not wandering. They wandered
    // for a few seconds then stopped" + "movement is preferred over
    // standing. Zombies wander."): the real bug -- a single random
    // candidate that happened to land on illegal ground (a real
    // likelihood in a dense BigCity block, buildDensity 0.65) made the
    // OLD version of this method return `transform.position` itself.
    // `TickWander` then set `_moveTarget` to that same point, so the
    // VERY NEXT frame saw `arrived == true` (distance 0) and called this
    // method again immediately -- not even waiting for the repick timer
    // -- and if that retry ALSO landed on illegal ground, the cycle
    // repeated every single frame, reading as "stopped" forever. Fixed
    // two ways: try several candidates (varying salt each time) before
    // giving up, and even the last-resort fallback always returns a
    // point strictly different from the current position (a short step
    // toward our own nearest building, which is guaranteed legal ground
    // to walk toward) -- this method can no longer return
    // `transform.position` itself under any circumstance.
    private const int WanderPickAttempts = 8;

    /// <summary>Join an existing nearby herd if there's room, otherwise
    /// seed a fresh short step (<see cref="WanderStepMin"/>-<see
    /// cref="WanderStepMax"/> from here) in a pseudo-random direction --
    /// deterministic per-instance hashing (same convention as every
    /// other cosmetic-ish AI pick in this codebase: Citizen's own
    /// destination re-picks, HumanSoldier's patrol angle), not
    /// `UnityEngine.Random`. A step that would cross the <see
    /// cref="WanderLeashRadius"/> (2 km) leash around our own buildings
    /// gets pulled back toward the nearest one instead of rejected
    /// outright. See this method's own preceding comment for why it now
    /// retries instead of ever standing still.</summary>
    private Vector3 PickWanderTarget()
    {
        if (_builder.TryFindJoinableHerd(transform.position, HerdJoinRadius, MaxHerdSize, out var herdTarget))
            return herdTarget;

        var seed = (GetInstanceID() % 1000) / 1000f;
        for (var attempt = 0; attempt < WanderPickAttempts; attempt++)
        {
            _wanderPickSalt++;
            var angle = Frac(seed * 53.1f + _wanderPickSalt * 8.3f) * Mathf.PI * 2f;
            var step = Mathf.Lerp(WanderStepMin, WanderStepMax, Frac(seed * 71.7f + _wanderPickSalt * 4.1f));
            var candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * step;

            if (!_builder.IsWithinRangeOfOwnBuildings(candidate, WanderLeashRadius))
            {
                var pull = _builder.NearestOwnBuildingPosition(transform.position) - transform.position;
                pull.y = 0f;
                if (pull.sqrMagnitude > 0.01f) candidate = transform.position + pull.normalized * step;
            }

            var hex = _builder.HexAt(candidate);
            if (_builder.City.Contains(hex) && !_builder.BlockedFor(false).Contains(hex))
                return candidate;
        }

        // every attempt landed on illegal ground -- step toward our own
        // nearest building instead of giving up. That direction is
        // always legal ground to head toward (a building's footprint is
        // itself real, reachable ground), so this branch can't repeat
        // the same failure, and it never returns transform.position.
        var home = _builder.NearestOwnBuildingPosition(transform.position);
        var toHome = home - transform.position;
        toHome.y = 0f;
        if (toHome.sqrMagnitude > 0.01f) return transform.position + toHome.normalized * WanderStepMin;
        return transform.position + transform.forward * WanderStepMin;   // degenerate: already at our only building
    }

    private static float Frac(float v) { return v - Mathf.Floor(v); }

    private SimBuilding FindStationedBuilding()
    {
        if (_stationedBuildingId == null) return null;
        var bridge = _builder.SimBridge;
        if (bridge == null || !bridge.HasMatch) return null;
        for (var i = 0; i < bridge.BuildingCount; i++)
        {
            var b = bridge.BuildingAt(i);
            if (b.EntityId == _stationedBuildingId.Value) return b;
        }
        return null;
    }

    private void TickSeekBuild(float dt)
    {
        var target = FindStationedBuilding();
        if (target == null || target.State != BuildingState.UnderConstruction || target.IsStaffed)
        {
            // gone, completed, destroyed, or already claimed by another
            // Worker -- give up and let TickIdle re-decide next frame.
            _stationedBuildingId = null;
            _pathFollower.Clear();
            _state = ZombieState.Idle;
            return;
        }

        var dest = _builder.WorldOf(target.Hex);
        var to = dest - transform.position;
        to.y = 0f;
        if (to.magnitude <= BuildReach)
        {
            _builder.SimBridge.QueueSetBuildingStaffedCommand(0, target.EntityId, true);
            _pathFollower.Clear();
            _state = ZombieState.Staffing;
            return;
        }

        _pathFollower.SetGoal(_builder, transform.position, target.Hex);
        _pathFollower.Tick(_builder, transform, _combat, dt, MoveSpeed);
        _frameMoveDistance = _pathFollower.LastStepDistance;
    }

    /// <summary>Stand still and keep this building staffed -- match-core's
    /// own `SimBuilding.Tick()` is what actually advances construction
    /// while `IsStaffed` stays true; this method's only job is noticing
    /// when to stop (completed, destroyed, or this Worker got reassigned/
    /// died elsewhere) and unstaffing cleanly. Drives the "repetitive
    /// hammering... lean into work" Build animation for as long as it
    /// stays in this state.</summary>
    private void TickStaffing(float dt)
    {
        HumanCharacterAnimator.TickBuild(_rig, _animState, dt);
        var b = FindStationedBuilding();
        if (b == null || b.State != BuildingState.UnderConstruction)
        {
            if (b != null) _builder.SimBridge.QueueSetBuildingStaffedCommand(0, b.EntityId, false);
            _stationedBuildingId = null;
            _state = ZombieState.Idle;
        }
    }

    private void TickSeekScavenge(float dt)
    {
        if (_scavengeTarget == null) { _pathFollower.Clear(); _state = ZombieState.Idle; return; }
        var bp = NearestFootprintPoint(_scavengeTarget);
        var flat = bp - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= ScavengeReach)
        {
            _scavengeTickTimer = 0f;
            _pathFollower.Clear();
            _state = ZombieState.Scavenging;
            return;
        }

        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(bp));
        _pathFollower.Tick(_builder, transform, _combat, dt, MoveSpeed);
        _frameMoveDistance = _pathFollower.LastStepDistance;
    }

    /// <summary>In reach, actively clearing -- continuously requests its
    /// own <see cref="ScavengeRatePerSecond"/> share every <see
    /// cref="ScavengeTickInterval"/>, same concurrent-drain math as
    /// before (still sums across every Worker at the same site, "more
    /// Workers here genuinely clears faster" is unchanged). 2026-08:
    /// each slice now accumulates into <see cref="_carriedParts"/>
    /// instead of banking instantly -- this Worker now hauls its load to
    /// a Factory before it counts (<see cref="TickSeekDeliver"/>), the
    /// same onboard-tank shape MonsterAgent's harvesters already use.
    /// Falls back to re-approaching (<see cref="ZombieState.SeekScavenge"/>)
    /// if separation/collision nudged this Worker out of reach, rather
    /// than draining from a distance.</summary>
    private void TickScavenging(float dt)
    {
        if (_scavengeTarget == null) { _state = ZombieState.Idle; return; }
        var bp = NearestFootprintPoint(_scavengeTarget);
        var flat = bp - transform.position;
        flat.y = 0f;
        if (flat.magnitude > ScavengeReach)
        {
            _state = ZombieState.SeekScavenge;
            return;
        }

        HumanCharacterAnimator.TickHarvest(_rig, _animState, dt);

        var room = WorkerCarryCapacity - _carriedParts;
        if (room <= 0.01f) { _scavengeTarget = null; _state = ZombieState.SeekDeliver; return; }

        _scavengeTickTimer += dt;
        if (_scavengeTickTimer < ScavengeTickInterval) return;
        var elapsed = _scavengeTickTimer;
        _scavengeTickTimer = 0f;

        var request = Mathf.Min(Mathf.CeilToInt(room), Mathf.Max(1, Mathf.RoundToInt(ScavengeRatePerSecond * elapsed)));
        var drained = _builder.DrainBuildingScavenge(_scavengeTarget, request);
        if (drained > 0) _carriedParts += drained;

        if (_carriedParts >= WorkerCarryCapacity - 0.01f)
        {
            _scavengeTarget = null;
            _state = ZombieState.SeekDeliver;
            return;
        }
        if (drained <= 0)
        {
            // pile's empty -- either this tick or an earlier one (by
            // this or another concurrently-scavenging Worker) finished
            // it. Deliver whatever's carried so far rather than let it
            // sit undelivered; go back to plain idle if this Worker
            // never actually got a slice (e.g. lost the race to others).
            _scavengeTarget = null;
            _state = _carriedParts > 0.01f ? ZombieState.SeekDeliver : ZombieState.Idle;
        }
    }

    /// <summary>2026-08 (creator direction: "They also must deliver
    /// scavenged parts to factories or whatever we have for collections
    /// centres"): walk the carried load to the nearest own Factory (<see
    /// cref="RuntimeCityBuilder.NearestOwnFactoryApproachPosition"/>)
    /// and bank it on arrival -- mirrors MonsterAgent's own harvester-
    /// delivery shape. Re-targets every tick rather than caching the
    /// destination once, so a newly-completed closer Factory (or the old
    /// one's destruction) is picked up automatically mid-route, same
    /// "recompute, don't cache" idiom <see cref="TickSeekBuild"/>/<see
    /// cref="TickSeekScavenge"/> already use for their own targets.</summary>
    private void TickSeekDeliver(float dt)
    {
        if (_carriedParts <= 0.01f) { _pathFollower.Clear(); _state = ZombieState.Idle; return; }
        var dest = _builder.NearestOwnFactoryApproachPosition(transform.position);
        var to = dest - transform.position;
        to.y = 0f;
        if (to.magnitude <= BuildReach)
        {
            _builder.BankHarvestLoad(0f, 0f, 0f, _carriedParts);
            _carriedParts = 0f;
            _pathFollower.Clear();
            _state = ZombieState.Idle;
            return;
        }

        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(dest));
        _pathFollower.Tick(_builder, transform, _combat, dt, MoveSpeed);
        _frameMoveDistance = _pathFollower.LastStepDistance;
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

    private void UnstaffIfStaffing()
    {
        if (_state != ZombieState.Staffing || _stationedBuildingId == null) return;
        var bridge = _builder != null ? _builder.SimBridge : null;
        if (bridge != null) bridge.QueueSetBuildingStaffedCommand(0, _stationedBuildingId.Value, false);
    }

    /// <summary>Selection highlight -- delegates to <see
    /// cref="HumanCharacterKit.SetSelected"/>, shared with every other
    /// rig-based unit.</summary>
    public void SetSelected(bool selected)
    {
        HumanCharacterKit.SetSelected(_rig, selected);
    }

    /// <summary>Picks the faction-appropriate visual profile -- Worker is
    /// always the local human player's own unit (see this class's own
    /// header), so `_builder.chosenFaction` is the right (and only)
    /// faction to read, no per-instance owner lookup needed. `Mixed`
    /// (no single origin) falls back to the plain Human Worker look,
    /// same as every profile-less default elsewhere in this codebase.</summary>
    private void BuildModel()
    {
        HumanCharacterProfile profile;
        switch (_builder.chosenFaction)
        {
            case FactionId.AlienHive: profile = HumanCharacterProfile.AlienWorker(); break;
            case FactionId.MadDoctor: profile = HumanCharacterProfile.MadDoctorWorker(); break;
            default: profile = HumanCharacterProfile.HumanWorker(); break;
        }

        _rig = HumanCharacterKit.Build(transform, profile);
        _twitchyIdle = profile.Twitchy;
        _groundOffset = profile.HasLegs ? 0f : 1.05f;   // "Aliens should never walk... no visible footsteps" -- a real ground clearance, not just an animation choice
        _animState = new HumanCharacterAnimState { Seed = (GetInstanceID() % 1000) / 1000f * 6.283f };
    }

    /// <summary>docs/12 tech-wing epic, Phase 1: previously a no-op --
    /// dead Workers just sat in `RuntimeCityBuilder.Workers` forever at
    /// `Alive == false`, silently inflating any caller counting that list
    /// (the ghost-cursor preview this same phase makes load-bearing,
    /// among others). Same "notify the builder, then destroy" shape
    /// Tank.cs's own OnDied already establishes for a bespoke combatant.
    /// 2026-08: also unstaffs whatever construction site this Worker was
    /// standing at, so a dead Worker doesn't leave a building falsely
    /// marked staffed (and therefore permanently progressing with nobody
    /// actually there) forever.
    ///
    /// 2026-08 character-overhaul follow-up ("Death: quick collapse, no
    /// ragdolls"): gameplay bookkeeping (unstaffing, dropping out of
    /// `RuntimeCityBuilder.Workers`/`WorkerCount`) still happens
    /// immediately, right here -- a dying Worker is already gone for
    /// every economic purpose. Only the GameObject itself lingers, for
    /// `DeathDestroyDelay` seconds of collapse animation (see Update()'s
    /// own `_dying` branch), before actually being destroyed.</summary>
    private void OnDied()
    {
        UnstaffIfStaffing();
        if (_builder != null) _builder.OnWorkerDied(this);
        _dying = true;
        _deathTimer = DeathDestroyDelay;
    }
}
