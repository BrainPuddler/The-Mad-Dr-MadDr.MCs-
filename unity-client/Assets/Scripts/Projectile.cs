using UnityEngine;

/// <summary>
/// A flying shot -- photon bolt, bullet, or spore. Homes to a target
/// combatant's aim point and applies damage on arrival; if the target
/// dies mid-flight it coasts to the last-known point and fizzles.
/// Point shots (at a building) carry no target and are purely cosmetic --
/// the firing unit already applied the structural damage. Spawned and
/// configured by WeaponFx.
/// </summary>
public class Projectile : MonoBehaviour
{
    private UnitCombat _source;
    private UnitCombat _target;
    private Vector3 _point;      // fallback / point-target destination
    private bool _homing;
    private float _damage;
    private float _speed;
    private float _life = 4f;    // backstop so a stray shot never lives forever

    public void Init(UnitCombat source, UnitCombat target, Vector3 point, float damage, float speed)
    {
        _source = source;
        _target = target;
        _point = point;
        _homing = target != null;
        _damage = damage;
        _speed = Mathf.Max(6f, speed);
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        _life -= dt;

        var goal = (_homing && _target != null && _target.Alive) ? _target.AimPoint : _point;
        var to = goal - transform.position;
        var dist = to.magnitude;
        var step = _speed * dt;

        if (dist <= step || dist < 0.5f || _life <= 0f)
        {
            if (_homing && _target != null && _target.Alive) _target.TakeDamage(_damage, _source);
            Object.Destroy(gameObject);
            return;
        }

        var dir = to / dist;
        transform.position += dir * step;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
