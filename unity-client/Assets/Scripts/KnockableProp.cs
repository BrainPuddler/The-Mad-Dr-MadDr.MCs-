using UnityEngine;

/// <summary>
/// Street furniture that topples when a monster or tank walks through it
/// (docs/21 batch 2, item 2). Attach to the HOLDER transform a prop's
/// pieces are parented under, not to an individual primitive, so the
/// whole assembly (pole + arm + bulb, chassis + cabin + fins) tips as one
/// rigid unit. Distance checks are throttled to a few times a second per
/// prop -- cheap at city scale -- rather than every frame; once knocked
/// it stays down, a timed tween like the rest of the codebase's
/// animation, no physics engine involved.
/// </summary>
public class KnockableProp : MonoBehaviour
{
    private RuntimeCityBuilder _builder;
    private float _knockRadiusSq;
    private float _checkTimer;
    private bool _knocked;
    private float _tipProgress;
    private Vector3 _tipAxis;
    private Vector3 _restPosition;
    private Quaternion _restRotation;

    /// <summary>Set for fire hydrants: the knock also shears the valve,
    /// so a water jet erupts from where the prop stood -- the classic
    /// B-movie street beat.</summary>
    public bool SpawnsWaterJet;

    private const float TipDuration = 0.35f;
    private const float TipAngle = 82f;
    private const float SinkDepth = 0.35f;

    public void Init(RuntimeCityBuilder builder, float knockRadius)
    {
        _builder = builder;
        _knockRadiusSq = knockRadius * knockRadius;
        _restPosition = transform.localPosition;
        _restRotation = transform.localRotation;
        // stagger check timing across props so they don't all poll the
        // combatant list on the same frame
        _checkTimer = (GetInstanceID() & 15) * 0.02f;
    }

    private void Update()
    {
        if (_knocked)
        {
            if (_tipProgress >= 1f) return;
            _tipProgress = Mathf.Clamp01(_tipProgress + Time.deltaTime / TipDuration);
            transform.localRotation = _restRotation * Quaternion.AngleAxis(TipAngle * _tipProgress, _tipAxis);
            transform.localPosition = _restPosition + Vector3.down * (SinkDepth * _tipProgress);
            return;
        }

        if (_builder == null) return;
        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = 0.2f;

        var pos = transform.position;
        foreach (var c in _builder.Combatants)
        {
            if (c == null || !c.Alive) continue;
            var d = c.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude > _knockRadiusSq) continue;

            _knocked = true;
            // tip AWAY from the approaching unit: rotate about the
            // horizontal axis perpendicular to the approach line
            _tipAxis = new Vector3(d.z, 0f, -d.x).normalized;
            if (_tipAxis.sqrMagnitude < 1e-4f) _tipAxis = Vector3.forward;
            if (SpawnsWaterJet) DamageFx.WaterJet(pos, transform.parent);
            break;
        }
    }
}
