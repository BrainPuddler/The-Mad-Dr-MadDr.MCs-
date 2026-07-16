using MadDr.CityGen;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A 1950s human-faction tank -- the test dummy that fights the monsters.
/// Drives toward the nearest monster, faces it, and opens up once it's in
/// range; half of them carry a flamethrower (because it's cool), the rest
/// a 75mm cannon. Straight-line steering toward the target (no A* -- these
/// are combat targets, not navigators), but with a lightweight look-ahead
/// deflection so it never actually drives ONTO a building hex (creator
/// direction, 2026-07: "tanks can NOT spawn within building" -- spawn
/// placement was already building-aware; a tank driving straight through
/// one mid-chase is what that actually looked like in play). Plus the
/// shared no-overlap separation. Has a UnitCombat like everything else, so
/// monsters can target and kill it and it shows a health bar.
/// </summary>
public class Tank : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;
    private Transform _turret;
    private Transform _muzzle;
    private float _speed = 9f;
    private float _aggro = 150f;

    public UnitCombat Combat { get { return _combat; } }

    public void Init(RuntimeCityBuilder builder, bool flame)
    {
        _builder = builder;
        BuildModel(flame);
        _combat = gameObject.AddComponent<UnitCombat>();
        var weapon = flame ? WeaponProfile.TankFlamethrower() : WeaponProfile.TankCannon();
        _combat.Configure("human", flame ? 150f : 210f, 2.6f, 1.4f, weapon, OnDied);
    }

    private void Update()
    {
        if (_combat == null || !_combat.Alive || _builder == null) return;
        var dt = Time.deltaTime;

        var target = _builder.NearestEnemyOf(_combat, _aggro);
        if (target != null)
        {
            var to = target.AimPoint - transform.position;
            to.y = 0f;
            var dist = to.magnitude;
            var dir = dist > 0.01f ? to / dist : transform.forward;

            var look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 2.2f);
            if (_turret != null) _turret.rotation = Quaternion.Slerp(_turret.rotation, look, dt * 3.5f);

            var range = (float)_combat.Weapon.Range;
            if (dist > range * 0.85f)
            {
                var steer = SteerAroundBuildings(dir);
                if (steer.sqrMagnitude > 0.0001f) transform.position += steer * _speed * dt;
                // boxed in on every probed direction: hold position and
                // keep shooting range-checked as normal next frame rather
                // than ramming the one building in the way
            }
            else
                _combat.TryFire(target, _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f);
        }

        _builder.ApplySeparation(_combat);

        // terrain-follow the sculpted ground (docs/21): treads on the
        // hill, not floating over the valley
        var p = transform.position;
        var gy = _builder.GroundHeightAt(p);
        if (!Mathf.Approximately(p.y, gy)) transform.position = new Vector3(p.x, gy, p.z);
    }

    // deflection angles tried in order: straight first (the common case,
    // nothing in the way), then widening zig-zags each side -- a cheap
    // stand-in for real pathfinding that still guarantees the tank never
    // commits to a step that lands it on a building hex
    private static readonly float[] DeflectAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };
    private const float ProbeDistance = 6f;

    private Vector3 SteerAroundBuildings(Vector3 desiredDir)
    {
        if (_builder == null || _builder.City == null) return desiredDir;
        var blocked = _builder.BlockedFor(false);
        foreach (var angle in DeflectAngles)
        {
            var candidate = Quaternion.Euler(0f, angle, 0f) * desiredDir;
            var probe = transform.position + candidate * ProbeDistance;
            var hex = _builder.HexAt(probe);
            if (_builder.City.Contains(hex) && !blocked.Contains(hex)) return candidate.normalized;
        }
        return Vector3.zero;   // hemmed in on every side -- hold rather than drive into the only building left
    }

    private void OnDied()
    {
        _builder.OnCombatantDied(_combat);
        // brief death read: sink and tint dark, then remove
        transform.position += Vector3.down * 0.4f;
        Object.Destroy(gameObject, 0.6f);
    }

    // ---- model ---------------------------------------------------------------

    private void BuildModel(bool flame)
    {
        var hullCol = flame ? new Color(0.42f, 0.28f, 0.20f) : new Color(0.30f, 0.36f, 0.24f);
        var metal = new Color(0.20f, 0.22f, 0.22f);

        // hull KEEPS its collider so left/right-click raycasts can pick the
        // tank (GetComponentInParent<Tank> climbs from it to this root)
        Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 1.0f, 4.0f), hullCol, true);
        Prim(PrimitiveType.Cube, transform, new Vector3(-1.5f, 0.5f, 0f), new Vector3(0.6f, 0.7f, 4.3f), metal);
        Prim(PrimitiveType.Cube, transform, new Vector3(1.5f, 0.5f, 0f), new Vector3(0.6f, 0.7f, 4.3f), metal);

        _turret = new GameObject("Turret").transform;
        _turret.SetParent(transform, false);
        _turret.localPosition = new Vector3(0f, 1.5f, -0.2f);
        Prim(PrimitiveType.Sphere, _turret, Vector3.zero, new Vector3(2.0f, 1.1f, 2.2f), hullCol);

        if (flame)
        {
            // stubby wide nozzle + tanks on the back
            var noz = Prim(PrimitiveType.Cylinder, _turret, new Vector3(0f, 0.1f, 1.7f), new Vector3(0.4f, 0.7f, 0.4f), metal);
            noz.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 1.5f, -1.7f), new Vector3(0.5f, 0.6f, 0.5f), new Color(0.5f, 0.35f, 0.1f));
        }
        else
        {
            var barrel = Prim(PrimitiveType.Cylinder, _turret, new Vector3(0f, 0.1f, 1.6f), new Vector3(0.24f, 1.5f, 0.24f), metal);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        _muzzle = new GameObject("Muzzle").transform;
        _muzzle.SetParent(_turret, false);
        _muzzle.localPosition = new Vector3(0f, 0.1f, flame ? 2.4f : 3.1f);
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
}
