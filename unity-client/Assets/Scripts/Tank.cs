using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A 1950s human-faction tank -- the test dummy that fights the monsters.
/// Drives toward the nearest monster, faces it, and opens up once it's in
/// range; half of them carry a flamethrower (because it's cool), the rest
/// a 75mm cannon. Simple straight-line steering (no A* -- these are combat
/// targets, not navigators) plus the shared no-overlap separation. Has a
/// UnitCombat like everything else, so monsters can target and kill it and
/// it shows a health bar.
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
                transform.position += dir * _speed * dt;   // close the distance
            else
                _combat.TryFire(target, _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f);
        }

        _builder.ApplySeparation(_combat);
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
