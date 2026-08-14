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
        if (profile.Visual.SeatedHeight > 0f) BuildWheelchair();

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
    /// gameplay one).</summary>
    private void BuildWeaponProp()
    {
        if (_rig.RightElbow == null || _profile.Weapon == null || !_profile.Weapon.CanAttack) return;
        var isShotgun = _profile.Weapon.Range <= 10f;   // Shotgun() is the only short-range firearm profile today
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(_rig.RightElbow, false);
        go.transform.localPosition = new Vector3(0.05f, -0.32f, 0.16f);
        go.transform.localScale = isShotgun ? new Vector3(0.08f, 0.08f, 0.4f) : new Vector3(0.05f, 0.05f, 0.55f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = HumanCharacterKit.SharedMaterial();
        var mpb = new MaterialPropertyBlock();
        var color = new Color(0.15f, 0.13f, 0.11f);
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
    }

    /// <summary>"Drive a manual wheelchair" (Grandma, creator direction
    /// verbatim) -- a seat/frame plus two large side wheels, parented
    /// under the character root so it moves and turns with her exactly
    /// like a real chair would. Blocky cubes throughout, same low-poly
    /// register as every `HumanCharacterKit` part -- a true circular
    /// wheel isn't in this project's cube-only geometry vocabulary, but a
    /// wide flattened cube on each side reads as "wheelchair" at RTS
    /// camera distance, which is what actually matters (this project's
    /// own "good enough to read as X, not a finished look" convention,
    /// PbrTextureAtlas.cs's header).</summary>
    private void BuildWheelchair()
    {
        var mat = HumanCharacterKit.SharedMaterial();
        var frameColor = new Color(0.18f, 0.17f, 0.19f);
        var seatHeight = _profile.Visual.SeatedHeight > 0f ? _profile.Visual.SeatedHeight : 0.5f;

        WheelchairPart(new Vector3(0f, seatHeight * 0.5f, 0f), new Vector3(0.55f, seatHeight, 0.5f), frameColor, mat);
        WheelchairPart(new Vector3(-0.34f, seatHeight * 0.55f, 0f), new Vector3(0.08f, seatHeight * 1.1f, 0.62f), frameColor, mat);
        WheelchairPart(new Vector3(0.34f, seatHeight * 0.55f, 0f), new Vector3(0.08f, seatHeight * 1.1f, 0.62f), frameColor, mat);
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

        _pathFollower.SetGoal(_builder, transform.position, _builder.HexAt(_patrolTarget));
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
