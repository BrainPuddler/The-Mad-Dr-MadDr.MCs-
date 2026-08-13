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
/// Idle priority: seek an unstaffed friendly construction site first,
/// then scavengeable debris, then just stand there -- "monsters would
/// still collect resources" is why citizens are never a Worker target.
/// Combat is auto-aggro within a short leash (<see cref="AggroRadius"/>,
/// same `NearestEnemyOf`/`TryFire` pattern <see cref="Tank"/> already
/// uses) -- zombie-horde mindless violence overrides economic work the
/// instant an enemy is close, "I choose" is satisfied by the unit being
/// selectable/move-orderable (<see cref="WaypointCommander"/>'s own new
/// parallel `_selectedWorkers` path) even though attacks themselves
/// aren't a separate player-issued order in this v0.1 pass.
/// </summary>
public class Worker : MonoBehaviour
{
    private const float Scale = 0.55f;   // a little smaller than a Citizen's own capsule -- reads as "person", not "monster"
    private const float MoveSpeed = 4f;
    private const float BuildReach = 3.5f;
    private const float ScavengeReach = 4f;
    private const float SearchRadius = 70f;
    private const float AggroRadius = 25f;   // short zombie leash -- Tank's own 150f is a patrolling combat unit's awareness, not cannon fodder's
    private const float ArriveThreshold = 1.5f;

    private enum ZombieState { Idle, PlayerMove, SeekBuild, Staffing, SeekScavenge }
    private ZombieState _state = ZombieState.Idle;
    private Vector3 _moveTarget;
    private uint? _stationedBuildingId;
    private Building _scavengeTarget;

    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;
    private Renderer _hullRenderer;
    private Color _hullBaseColor;

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
        if (_combat == null || !_combat.Alive || _builder == null) return;
        var dt = Time.deltaTime;

        if (_combat.IsCaptured)
        {
            _combat.TickCapture(dt);
            SnapToGround();
            return;
        }

        var enemy = _builder.NearestEnemyOf(_combat, AggroRadius);
        if (enemy != null)
        {
            TickCombat(enemy, dt);
            _builder.ApplySeparation(_combat);
            SnapToGround();
            return;
        }

        switch (_state)
        {
            case ZombieState.PlayerMove: TickPlayerMove(dt); break;
            case ZombieState.SeekBuild: TickSeekBuild(dt); break;
            case ZombieState.Staffing: TickStaffing(); break;
            case ZombieState.SeekScavenge: TickSeekScavenge(dt); break;
            default: TickIdle(); break;
        }

        _builder.ApplySeparation(_combat);
        SnapToGround();
    }

    private void SnapToGround()
    {
        var p = transform.position;
        var gy = _builder.GroundHeightAt(p);
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
            transform.position += dir * (MoveSpeed * dt * _combat.SpeedMultiplier);
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
        _moveTarget = destination;
        _state = ZombieState.PlayerMove;
    }

    private void TickPlayerMove(float dt)
    {
        var to = _moveTarget - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist <= ArriveThreshold) { _state = ZombieState.Idle; return; }
        var dir = to / Mathf.Max(dist, 0.0001f);
        transform.position += dir * (MoveSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
    }

    /// <summary>Idle-time decision: an unstaffed friendly construction
    /// site first ("build things"), destroyed-building debris second
    /// ("automatically scavenge resources") -- never a citizen search
    /// ("monsters would still collect resources," creator direction).</summary>
    private void TickIdle()
    {
        var site = _builder.NearestUnstaffedConstructionSite(transform.position, SearchRadius);
        if (site != null)
        {
            _stationedBuildingId = site.EntityId;
            _state = ZombieState.SeekBuild;
            return;
        }
        var debris = _builder.NearestScavengeableBuildingTo(transform.position, SearchRadius);
        if (debris != null)
        {
            _scavengeTarget = debris;
            _state = ZombieState.SeekScavenge;
        }
    }

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
            _state = ZombieState.Idle;
            return;
        }

        var dest = _builder.WorldOf(target.Hex);
        var to = dest - transform.position;
        to.y = 0f;
        if (to.magnitude <= BuildReach)
        {
            _builder.SimBridge.QueueSetBuildingStaffedCommand(0, target.EntityId, true);
            _state = ZombieState.Staffing;
            return;
        }
        var dir = to.normalized;
        transform.position += dir * (MoveSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
    }

    /// <summary>Stand still and keep this building staffed -- match-core's
    /// own `SimBuilding.Tick()` is what actually advances construction
    /// while `IsStaffed` stays true; this method's only job is noticing
    /// when to stop (completed, destroyed, or this Worker got reassigned/
    /// died elsewhere) and unstaffing cleanly.</summary>
    private void TickStaffing()
    {
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
        if (_scavengeTarget == null) { _state = ZombieState.Idle; return; }
        var bp = NearestFootprintPoint(_scavengeTarget);
        var flat = bp - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= ScavengeReach)
        {
            // no onboard tank on a Worker (unlike a harvester Monster) --
            // one gulp banks the whole remaining pile straight to the
            // real wallet via the SAME delivery path harvesters use.
            var drained = _builder.DrainBuildingScavenge(_scavengeTarget, int.MaxValue);
            if (drained > 0) _builder.BankHarvestLoad(0f, 0f, 0f, drained);
            _scavengeTarget = null;
            _state = ZombieState.Idle;
            return;
        }
        var dir = flat.normalized;
        transform.position += dir * (MoveSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
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

    /// <summary>Selection highlight -- brightens the hull, same "shape
    /// carries kind, color carries state" split every other selectable
    /// unit in this project follows (aesthetic-preferences skill §5).</summary>
    public void SetSelected(bool selected)
    {
        if (_hullRenderer == null) return;
        _hullRenderer.sharedMaterial.color = selected
            ? Color.Lerp(_hullBaseColor, Color.white, 0.55f)
            : _hullBaseColor;
    }

    private void BuildModel()
    {
        // dull industrial khaki -- distinct from a Citizen's random
        // civilian hue and from either faction's own combat palette,
        // reading as "reassigned labor" (aesthetic-preferences skill §5:
        // shape carries kind -- the capsule-plus-hard-hat silhouette --
        // this color carries owner/state, not kind, same split every
        // other dresser in this project already follows).
        var khaki = new Color(0.55f, 0.5f, 0.32f);
        _hullBaseColor = khaki;
        var hull = Prim(PrimitiveType.Capsule, transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.45f, 0.5f), khaki, keepCollider: true);
        _hullRenderer = hull.GetComponent<Renderer>();
        // a small "hard hat" -- the one silhouette cue that reads as
        // "worker" rather than "citizen" or "monster" at a glance.
        Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.3f, 0.06f, 0.3f), new Color(0.85f, 0.62f, 0.15f));
    }

    private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
        Color color, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (!keepCollider)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var m = new Material(ShaderUtil.FindRenderableShader());
            m.color = color;
            r.sharedMaterial = m;
        }
        return go.transform;
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
    /// actually there) forever.</summary>
    private void OnDied()
    {
        UnstaffIfStaffing();
        if (_builder != null) _builder.OnWorkerDied(this);
        Object.Destroy(gameObject);
    }
}
