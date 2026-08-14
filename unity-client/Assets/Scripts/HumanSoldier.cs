using UnityEngine;

/// <summary>
/// 2026-08 (creator brief: "Character System Overhaul," Human Soldiers
/// section): a lightweight ATMOSPHERE unit for Army-faction bases --
/// "helmet, backpack, simple rifle silhouette, boots, broader shoulders...
/// disciplined... confident... military posture." Built on <see
/// cref="HumanCharacterKit"/>/<see cref="HumanCharacterAnimator"/>, the
/// same shared rig+animation system <see cref="Worker"/>'s three faction
/// skins use.
///
/// Deliberately modeled on <see cref="Citizen"/>'s pattern, not <see
/// cref="Worker"/>'s or a genome <see cref="MonsterAgent"/>'s: client-
/// side cosmetic only, never registered with match-core, no real combat
/// stats, no production/roster entry -- confirmed with the creator
/// (AskUserQuestion) before building, since the brief's own "aim pose /
/// fire pose" language reads like real ranged combat that nothing in
/// this codebase has an infantry-unit slot for yet. "Aim"/"fire" here
/// are flavor animation only (<see cref="HumanCharacterAnimator.TickAim"/>)
/// -- no projectile, no damage, matching Citizen's own "cosmetic crowd,
/// not a synced combat entity" scoping (docs/19).
///
/// Spawned only near the LOCAL HUMAN player's own HQ, only when they
/// picked the Human Army faction (<see
/// cref="RuntimeCityBuilder.SpawnStartingSoldiers"/>) -- AI-opponent
/// Army bases don't get this dressing in this pass, an explicit scope
/// cut (see that method's own comment), same as how Worker itself only
/// ever belongs to the local human today.
/// </summary>
public class HumanSoldier : MonoBehaviour
{
    private const float MarchSpeed = 2.6f;      // "disciplined marching walk" -- brisker than a Citizen's civilian amble, slower than a sprint
    private const float AlertRadius = 22f;       // notices a nearby monster and switches to aim/fire flavor
    private const float FireRange = 14f;
    private const float PatrolRadius = 10f;      // short loop around its own post, not a cross-town errand like a Citizen
    private const float ArriveThreshold = 1f;

    private enum SoldierState { Guard, Patrol }
    private SoldierState _state = SoldierState.Guard;

    private RuntimeCityBuilder _builder;
    private Vector3 _post;
    private Vector3 _patrolTarget;
    private float _stateTimer;
    private bool _firingPulse;
    private float _firingPulseTimer;
    private float _seed;
    private int _pickSalt;

    private HumanCharacterRig _rig;
    private HumanCharacterAnimState _animState;

    public void Init(RuntimeCityBuilder builder, Vector3 post)
    {
        _builder = builder;
        _post = post;
        transform.position = post;

        var profile = HumanCharacterProfile.HumanSoldier();
        _rig = HumanCharacterKit.Build(transform, profile);
        _seed = (GetInstanceID() % 1000) / 1000f;
        _animState = new HumanCharacterAnimState { Seed = _seed * 6.283f };
        BuildRifle();

        _stateTimer = NextGuardDuration();
    }

    /// <summary>"Simple rifle silhouette" -- a single thin cube parented
    /// to the right forearm's own elbow pivot, so it tracks the arm
    /// through every pose (guard/aim/fire) for free instead of being
    /// posed separately. Not part of <see cref="HumanCharacterKit"/>
    /// itself -- a weapon prop isn't a body part every profile needs,
    /// see that class's own <see cref="HumanCharacterProfile.HumanSoldier"/>
    /// doc comment.</summary>
    private void BuildRifle()
    {
        if (_rig.RightElbow == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(_rig.RightElbow, false);
        go.transform.localPosition = new Vector3(0.05f, -0.32f, 0.16f);
        go.transform.localScale = new Vector3(0.05f, 0.05f, 0.55f);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = HumanCharacterKit.SharedMaterial();
        var mpb = new MaterialPropertyBlock();
        var rifleColor = new Color(0.15f, 0.13f, 0.11f);
        mpb.SetColor("_BaseColor", rifleColor);
        mpb.SetColor("_Color", rifleColor);
        renderer.SetPropertyBlock(mpb);
    }

    private void Update()
    {
        if (_builder == null) return;
        var dt = Time.deltaTime;

        var threat = _builder.NearestMonsterTo(transform.position, AlertRadius);
        if (threat != null)
        {
            TickAlert(threat, dt);
        }
        else
        {
            _firingPulse = false;
            switch (_state)
            {
                case SoldierState.Patrol: TickPatrol(dt); break;
                default: TickGuard(dt); break;
            }
        }

        var p = transform.position;
        transform.position = new Vector3(p.x, _builder.GroundHeightAt(p), p.z);
    }

    /// <summary>Standing guard at its post most of the time -- "standing
    /// guard idle" -- with an occasional short patrol loop (<see
    /// cref="TickPatrol"/>) so a whole line of Soldiers doesn't read as
    /// frozen statues.</summary>
    private void TickGuard(float dt)
    {
        HumanCharacterAnimator.TickIdle(_rig, _animState, twitchy: false, dt);
        _stateTimer -= dt;
        if (_stateTimer <= 0f)
        {
            var angle = Frac(_seed * 41.3f + _pickSalt * 7.9f) * Mathf.PI * 2f;
            _patrolTarget = _post + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * PatrolRadius;
            _state = SoldierState.Patrol;
        }
    }

    /// <summary>"Disciplined marching walk" out to a nearby point around
    /// its own post and back to Guard -- reuses the shared walk cycle
    /// (<see cref="HumanCharacterAnimator.TickLocomotion"/>), the same
    /// "no skating" distance-synced stride every other legged rig
    /// uses.</summary>
    private void TickPatrol(float dt)
    {
        var to = _patrolTarget - transform.position;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist <= ArriveThreshold)
        {
            _state = SoldierState.Guard;
            _pickSalt++;
            _stateTimer = NextGuardDuration();
            return;
        }
        var dir = to / Mathf.Max(dist, 0.0001f);
        var step = dir * (MarchSpeed * dt);
        transform.position += step;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
        HumanCharacterAnimator.TickLocomotion(_rig, _animState, step.magnitude, running: false, dt);
    }

    /// <summary>A monster is within <see cref="AlertRadius"/> -- turn to
    /// face it and hold the aim pose; once it's within <see
    /// cref="FireRange"/>, add the fire-pose recoil pulse on a short
    /// repeating timer. Pure flavor (see this class's own header) --
    /// nothing here deals damage or reads combat stats.</summary>
    private void TickAlert(MonsterAgent threat, float dt)
    {
        _state = SoldierState.Guard;   // resume guard/patrol from its own post once the threat clears
        var to = threat.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), dt * 6f);

        var inRange = to.magnitude <= FireRange;
        if (inRange)
        {
            _firingPulseTimer -= dt;
            if (_firingPulseTimer <= 0f) { _firingPulse = !_firingPulse; _firingPulseTimer = 0.35f; }
        }
        else
        {
            _firingPulse = false;
        }
        HumanCharacterAnimator.TickAim(_rig, _animState, aiming: true, firing: inRange && _firingPulse, dt);
    }

    // Deterministic per-instance "randomness," same convention as every
    // other cosmetic unit in this codebase (Citizen's own per-instance
    // hue/repick-timer hashing, BuildingWindowGrid's per-window seed
    // draws) -- not a genuine re-roll each call, a fresh SALT-shifted
    // hash of this Soldier's own seed instead, so a whole squad doesn't
    // patrol/pause in lockstep.
    private float NextGuardDuration() { _pickSalt++; return 4f + Frac(_seed * 59.7f + _pickSalt * 3.1f) * 5f; }

    private static float Frac(float v) { return v - Mathf.Floor(v); }
}
