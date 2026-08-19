using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// 2026-08 (creator brief: "Refactor Human Soldiers & Armed Citizens into
/// Monster Variants"): the ONE shared AI/movement/combat implementation
/// every armed-human variant (Human Soldier, Armed Civilian, Grandma-in-
/// a-wheelchair, and any future one) runs on -- driven entirely by a
/// <see cref="HumanCombatProfile"/>, never a per-variant subclass or a
/// second copy of this logic. See `HumanCombatProfile.cs`'s own header
/// for why this is a standalone non-genome kit rather than a MonsterAgent
/// variant (researched and confirmed with the creator before building).
///
/// Real combat via <see cref="UnitCombat"/>/<see cref="WeaponProfile"/> --
/// unlike docs/34's `HumanSoldier`, which this class replaces, aim/fire
/// here deals real damage. Real navigation via <see cref="GroundPathFollower"/>
/// for non-combat movement (guard/patrol) -- the same real HexPathfinder-
/// routed, locally-steered movement `Worker` already got (docs/25 Phase
/// F). Combat approach/chase deliberately does NOT route through the
/// path follower, same reasoning `Worker.TickCombat`/`MonsterAgent.
/// TickAttackUnit` already established: full-repathing against a target
/// that moves every frame is wasted work, a direct steer-and-close is
/// what every other close-range chase in this codebase already does.
///
/// Body/animation via `HumanCharacterKit`/`HumanCharacterAnimator`
/// (docs/34) -- the same shared rig every human-shaped unit in this game
/// uses. Death mirrors `Worker.OnDied`'s own "bookkeeping now, defer
/// GameObject destruction for the collapse animation" shape.
/// </summary>
public class HumanoidCombatant : MonoBehaviour
{
    private const float ArriveThreshold = 1f;
    private const float PatrolRadius = 10f;
    private const float DeathDestroyDelay = 0.5f;

    // Non-Aggressive variants (Armed Civilian) don't hunt -- they only
    // fight back once a threat is already this close, a fraction of their
    // own AggroRadius. Aggressive variants (Grandma, Soldier) ignore this
    // and engage anything within the full AggroRadius -- see
    // HumanCombatProfile.Aggressive's own comment.
    private const float DefensiveEngageFraction = 0.4f;

    // "Rotate realistically before changing direction" (Grandma, creator
    // direction verbatim) -- how far off (degrees) the desired heading
    // has to be before TurnBeforeMove stops translating and turns in
    // place first, and how fast (deg/s) that in-place turn happens.
    private const float TurnBeforeMoveThresholdDeg = 35f;
    private const float TurnBeforeMoveRateDegPerSec = 90f;

    private enum State { Guard, Patrol, Combat }
    private State _state = State.Guard;

    private RuntimeCityBuilder _builder;
    private HumanCombatProfile _profile;
    private Vector3 _post;
    private Vector3 _patrolTarget;
    private float _stateTimer;
    private float _seed;
    private int _pickSalt;

    private UnitCombat _combat;
    private readonly GroundPathFollower _pathFollower = new GroundPathFollower();
    private HumanCharacterRig _rig;
    private HumanCharacterAnimState _animState;
    private float _frameMoveDistance;
    private bool _dying;
    private float _deathTimer;

    public void Init(RuntimeCityBuilder builder, HumanCombatProfile profile, Vector3 post)
    {
        _builder = builder;
        _profile = profile;
        _post = post;
        transform.position = post;

        _rig = HumanCharacterKit.Build(transform, profile.Visual);
        _seed = (GetInstanceID() % 1000) / 1000f;
        _animState = new HumanCharacterAnimState { Seed = _seed * 6.283f };
        BuildWeaponProp();
        // SeatedHeight > 0, not just "no legs" -- a legless HOVER profile
        // (the Alien Worker preset's own use of HasLegs=false) is a
        // different thing entirely and must not get a wheelchair bolted
        // onto it. SeatedHeight is Grandma-specific today but the check
        // is written against the flag's actual meaning, not "today's only
        // caller," so it stays correct if a second seated variant shows up.
        if (profile.Visual.SeatedHeight > 0f)
        {
            BuildSeatedLegs();
            BuildWheelchair();
        }

        _combat = gameObject.AddComponent<UnitCombat>();
        _combat.Configure(profile.Faction, profile.MaxHealth, profile.Radius, profile.AimHeight,
            profile.Weapon, OnDied, profile.Mass);
        if (_builder != null) _builder.RegisterCombatant(_combat);

        _stateTimer = NextGuardDuration();
    }

    /// <summary>A simple thin-cube weapon silhouette parented to the
    /// right forearm's elbow pivot, same pattern `HumanSoldier.BuildRifle`
    /// established -- tracks the arm through every pose for free. Sized a
    /// little larger/shorter than a rifle when this variant's weapon
    /// reads as a shotgun (`WeaponKind.Bullet` doesn't itself distinguish
    /// "rifle" from "shotgun" -- this is a silhouette cue only, not a
    /// gameplay one).
    ///
    /// 2026-08 (Angry Civilian Mob, creator direction: "Visually
    /// appealing tho"): `WeaponKind.Melee`/`Spore` variants (ThrownRock/
    /// MolotovCocktail) get a genuinely different prop shape here --
    /// a small fist-sized rock, or a bottle with a lit rag tint -- rather
    /// than falling through to the gun-barrel silhouette every Bullet-
    /// kind variant uses, which would read wrong for something that's
    /// explicitly not a firearm.</summary>
    private void BuildWeaponProp()
    {
        if (_rig.RightElbow == null || _profile.Weapon == null || !_profile.Weapon.CanAttack) return;

        if (_profile.Weapon.Kind == WeaponKind.Melee)
        {
            SpawnWeaponPropCube(new Vector3(0.06f, -0.3f, 0.1f), new Vector3(0.12f, 0.1f, 0.11f), new Color(0.42f, 0.4f, 0.38f));
            return;
        }
        if (_profile.Weapon.Kind == WeaponKind.Spore)
        {
            SpawnWeaponPropCube(new Vector3(0.06f, -0.3f, 0.12f), new Vector3(0.08f, 0.22f, 0.08f), new Color(0.16f, 0.32f, 0.14f));
            SpawnWeaponPropCube(new Vector3(0.06f, -0.17f, 0.12f), new Vector3(0.05f, 0.06f, 0.05f), new Color(0.9f, 0.55f, 0.1f));   // the lit rag, poking out the top
            return;
        }

        var isShotgun = _profile.Weapon.Range <= 10f;   // Shotgun() is the only short-range firearm profile today
        var scale = isShotgun ? new Vector3(0.08f, 0.08f, 0.4f) : new Vector3(0.05f, 0.05f, 0.55f);
        SpawnWeaponPropCube(new Vector3(0.05f, -0.32f, 0.16f), scale, new Color(0.15f, 0.13f, 0.11f));
    }

    private void SpawnWeaponPropCube(Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(_rig.RightElbow, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = HumanCharacterKit.SharedMaterial();
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
    }

    /// <summary>"Drive a manual wheelchair" (Grandma, creator direction
    /// verbatim) -- a seat/frame plus two large circular side wheels,
    /// parented under the character root so it moves and turns with her
    /// exactly like a real chair would. The seat/frame stays a blocky
    /// cube (same low-poly register as every `HumanCharacterKit` part),
    /// but the wheels are real `PrimitiveType.Cylinder` discs, not a
    /// flattened cube -- Cylinder is already this project's own vocabulary
    /// for round props (Collector.cs's wheel/vent trim, MonsterAgent's
    /// glow rings), so "cube-only" was never a hard constraint on a prop
    /// built here in HumanoidCombatant, only on the shared
    /// HumanCharacterKit body parts. A wide flattened cube read as a
    /// fender panel, not a wheel -- fixed per creator report (2026-08).</summary>
    private void BuildWheelchair()
    {
        var mat = HumanCharacterKit.SharedMaterial();
        var frameColor = new Color(0.18f, 0.17f, 0.19f);
        var seatHeight = _profile.Visual.SeatedHeight > 0f ? _profile.Visual.SeatedHeight : 0.5f;

        WheelchairPart(new Vector3(0f, seatHeight * 0.5f, 0f), new Vector3(0.55f, seatHeight, 0.5f), frameColor, mat);

        // Large enough to visibly extend above the seat and touch the
        // ground -- a real wheelchair's rear wheels read as the chair's
        // dominant silhouette feature, not an incidental detail.
        const float wheelDiameter = 0.64f;
        const float wheelThickness = 0.06f;
        WheelchairWheel(new Vector3(-0.34f, wheelDiameter * 0.5f, 0f), wheelDiameter, wheelThickness, frameColor, mat);
        WheelchairWheel(new Vector3(0.34f, wheelDiameter * 0.5f, 0f), wheelDiameter, wheelThickness, frameColor, mat);
    }

    private void WheelchairPart(Vector3 localCenter, Vector3 size, Color color, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
    }

    /// <summary>A genuinely circular wheel: Unity's stock Cylinder mesh
    /// (radius 0.5, height 2 along its own local Y before scaling) scaled
    /// to (diameter, thickness, diameter) and then rotated 90 degrees
    /// around Z, which swings its height axis (the thickness) out to the
    /// parent's X axis and leaves its circular caps facing along X -- so
    /// at `localCenter = (+-x, diameter/2, 0)` the disc's flat round face
    /// is what a side-on RTS camera actually sees, exactly like a real
    /// wheel mounted on an axle running left-right through the chair.</summary>
    private void WheelchairWheel(Vector3 localCenter, float diameter, float thickness, Color color, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        go.transform.localScale = new Vector3(diameter, thickness, diameter);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
    }

    /// <summary>Grandma's `HumanCharacterProfile.HasLegs` is false --
    /// deliberately, since her animation routes through `TickWheelchair`
    /// rather than `TickLocomotion` (see `DriveMoveAnimation`'s own
    /// comment), and the shared `HumanCharacterKit` leg-building path is
    /// only for a standing walk cycle. But "no legs" alone reads as a
    /// floating legless torso, not a person SITTING in the chair -- so
    /// this builds a static seated silhouette directly (thigh projecting
    /// forward from the hip, shin dropping to the footrest) independent
    /// of the shared rig, the same "self-contained prop built here, not a
    /// shared-rig change" shape `BuildWheelchair` itself already uses.
    /// Scoped to this one variant on purpose -- nothing about the shared
    /// walking-leg path for every other human unit is touched.</summary>
    private void BuildSeatedLegs()
    {
        var mat = HumanCharacterKit.SharedMaterial();
        var v = _profile.Visual;
        var s = v.HeightScale;
        var color = v.BodyColor;   // housedress fabric covers the legs, same as the torso
        var seatTopY = v.SeatedHeight > 0f ? v.SeatedHeight : 0.5f;

        const float legOffsetX = 0.14f;
        const float thighLength = 0.4f;
        const float thighThickness = 0.16f;
        const float shinThickness = 0.14f;
        const float footClearance = 0.12f;

        var kneeZ = thighLength * s;
        var kneeY = seatTopY - thighThickness * s * 0.5f;
        var footY = footClearance * s;

        for (var side = -1; side <= 1; side += 2)
        {
            var x = side * legOffsetX * s;
            WheelchairPart(new Vector3(x, kneeY, kneeZ * 0.5f),
                new Vector3(thighThickness * s, thighThickness * s, thighLength * s), color, mat);
            WheelchairPart(new Vector3(x, (kneeY + footY) * 0.5f, kneeZ),
                new Vector3(shinThickness * s, kneeY - footY, shinThickness * s), color, mat);
        }
    }

    private void Update()
    {
        if (_builder == null) return;
        var dt = Time.deltaTime;

        if (_dying)
        {
            _deathTimer -= dt;
            HumanCharacterAnimator.TickDeath(_rig, _animState, dt);
            if (_deathTimer <= 0f) Object.Destroy(gameObject);
            return;
        }

        if (_combat == null || !_combat.Alive) return;

        var enemy = _builder.NearestEnemyOf(_combat, _profile.AggroRadius);
        var engageRadius = _profile.Aggressive ? _profile.AggroRadius : _profile.AggroRadius * DefensiveEngageFraction;
        if (enemy != null && (enemy.transform.position - transform.position).sqrMagnitude <= engageRadius * engageRadius)
        {
            _state = State.Combat;
            TickCombat(enemy, dt);
        }
        else
        {
            switch (_state)
            {
                case State.Patrol: TickPatrol(dt); break;
                default: TickGuard(dt); break;
            }
        }

        _builder.ApplySeparation(_combat);
        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p), p.z);
    }

    private void TickGuard(float dt)
    {
        _pathFollower.Clear();
        // 2026-08: a seated (wheelchair) rig still uses the normal idle
        // twitch, not TickHover -- see DriveMoveAnimation's own comment
        // on why TickHover's floating read is wrong for anything with
        // wheels, not wings. TickIdle already safely no-ops its leg-
        // specific half on a legless rig (checked at its own call site),
        // so this is a plain two-way split, not three.
        if (_rig.HasLegs || _profile.Visual.SeatedHeight > 0f)
            HumanCharacterAnimator.TickIdle(_rig, _animState, _profile.Visual.Twitchy, dt);
        else
            HumanCharacterAnimator.TickHover(_rig, _animState, 0f, dt);

        _stateTimer -= dt;
        if (_stateTimer <= 0f)
        {
            var angle = Frac(_seed * 41.3f + _pickSalt * 7.9f) * Mathf.PI * 2f;
            _patrolTarget = _post + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * PatrolRadius;
            _state = State.Patrol;
        }
    }

    private void TickPatrol(float dt)
    {
        var to = _patrolTarget - transform.position;
        to.y = 0f;
        if (to.magnitude <= ArriveThreshold)
        {
            _pathFollower.Clear();
            _state = State.Guard;
            _pickSalt++;
            _stateTimer = NextGuardDuration();
            return;
        }

        if (TurnInPlaceIfNeeded(to.normalized, dt)) { DriveMoveAnimation(0f, dt); return; }

        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(_patrolTarget), GroundPathFollower.LocalMoveMaxExpansions);
        var pathDone = _pathFollower.Tick(_builder, transform, _combat, dt, _profile.MoveSpeed);
        _frameMoveDistance = _pathFollower.LastStepDistance;
        DriveMoveAnimation(_frameMoveDistance, dt);
        if (pathDone && to.magnitude > ArriveThreshold * 2f)
        {
            var angle = Frac(_seed * 41.3f + (++_pickSalt) * 7.9f) * Mathf.PI * 2f;
            _patrolTarget = _post + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * PatrolRadius;
        }
    }

    /// <summary>Direct steer-and-close against a moving target, then fire
    /// once in range -- deliberately not routed through GroundPathFollower,
    /// see this class's own header. `TurnBeforeMove` still applies here:
    /// Grandma turns to face a new threat before rolling toward it, same
    /// as she would mid-patrol.</summary>
    private void TickCombat(UnitCombat enemy, float dt)
    {
        _pathFollower.Clear();
        var to = enemy.AimPoint - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        var range = _profile.Weapon != null ? (float)_profile.Weapon.Range : 0f;

        if (dist > range * 0.85f)
        {
            var dir = to / Mathf.Max(dist, 0.0001f);
            if (TurnInPlaceIfNeeded(dir, dt)) { DriveMoveAnimation(0f, dt); return; }

            var step = dir * (_profile.MoveSpeed * dt);
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
            DriveMoveAnimation(step.magnitude, dt);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), dt * 6f);
            _combat.TryFire(enemy, transform.position + Vector3.up * _profile.AimHeight);
            DriveMoveAnimation(0f, dt);
        }
    }

    /// <summary>Rotates in place toward `dir` when this variant's
    /// `TurnBeforeMove` is set and the heading change is large enough to
    /// bother with -- returns true (and does NOT move `transform`
    /// otherwise) while still mid-turn, so the caller skips translation
    /// for this frame. Every other variant (TurnBeforeMove false) always
    /// returns false immediately -- normal continuous turn-while-moving,
    /// unaffected.</summary>
    private bool TurnInPlaceIfNeeded(Vector3 dir, float dt)
    {
        if (!_profile.TurnBeforeMove || dir.sqrMagnitude < 0.0001f) return false;
        var angle = Vector3.Angle(transform.forward, dir);
        if (angle <= TurnBeforeMoveThresholdDeg) return false;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up), TurnBeforeMoveRateDegPerSec * dt);
        return true;
    }

    // 2026-08 (Grandma-in-a-wheelchair): a SEATED legless rig
    // (HumanCharacterProfile.SeatedHeight > 0, today only Grandma) rolls
    // on the ground -- TickHover's vertical bob/side-drift/forward-lean
    // is built for the Alien Worker's genuine hover and would read as
    // levitating on anything with wheels, so seated rigs get
    // TickWheelchair instead. A THIRD, true-hover case (no legs, not
    // seated) still falls through to TickHover.
    private void DriveMoveAnimation(float moveDistance, float dt)
    {
        if (_rig.HasLegs)
            HumanCharacterAnimator.TickLocomotion(_rig, _animState, moveDistance, running: _state == State.Combat, dt);
        else if (_profile.Visual.SeatedHeight > 0f)
            HumanCharacterAnimator.TickWheelchair(_rig, _animState, moveDistance, dt);
        else
            HumanCharacterAnimator.TickHover(_rig, _animState, moveDistance / Mathf.Max(dt, 0.0001f), dt);
    }

    private float NextGuardDuration() { _pickSalt++; return 4f + Frac(_seed * 59.7f + _pickSalt * 3.1f) * 5f; }

    private static float Frac(float v) { return v - Mathf.Floor(v); }

    /// <summary>Same "bookkeeping now, defer GameObject destruction for
    /// the collapse animation" shape as `Worker.OnDied` (docs/34).</summary>
    private void OnDied()
    {
        if (_builder != null) _builder.OnCombatantDied(_combat);
        _dying = true;
        _deathTimer = DeathDestroyDelay;
    }
}
