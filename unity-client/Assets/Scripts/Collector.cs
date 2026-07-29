using UnityEngine;

/// <summary>
/// Captures fleeing (or any) Citizens and possesses them into Workers
/// (2026-07 worker-economy epic) -- the unit that closes the "buildings
/// disgorge occupants that flee" -> "collector units that capture and
/// possess those humans, who become worker units, like SCV in starcraft"
/// chain the creator specced directly. Tank.cs's pattern (bespoke,
/// non-genome MonoBehaviour, plain `UnitCombat`), not MonsterAgent's.
///
/// Reuses the EXACT same pull mechanic a web/psionic special attack
/// already uses on a citizen (<see cref="Citizen.Capture"/>/<see
/// cref="CaptureState"/>) rather than building a parallel system --
/// only difference is the `possess: true` flag, which routes arrival to
/// <see cref="RuntimeCityBuilder.OnCitizenPossessed"/> instead of <see
/// cref="RuntimeCityBuilder.OnCitizenEaten"/>.
///
/// v0.1 scope: no weapon, no combat -- pure gatherer, closest in spirit
/// to the existing Ghoul's auto-scavenge role. Simple seek-and-capture,
/// no road-preference steering like Tank's SteerTank (a lighter, less
/// mechanically-constrained mover doesn't need it). Not yet wired into
/// any match-start spawn flow -- see <see
/// cref="RuntimeCityBuilder.SpawnCollector"/>'s own doc comment for why.
/// </summary>
public class Collector : MonoBehaviour
{
    private const float Scale = 0.9f;
    private const float SeekRadius = 45f;     // how far a Collector will notice a citizen worth chasing
    private const float CaptureRange = 3.5f;  // close enough to start reeling one in
    private const float MoveSpeed = 5.5f;
    private const float PullSpeed = 5f;

    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;
    private Citizen _target;

    public UnitCombat Combat { get { return _combat; } }

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        BuildModel();
        _combat = gameObject.AddComponent<UnitCombat>();
        _combat.Configure("monster", 90f, 1.1f * Scale, 1.3f * Scale, weapon: null, OnDied, mass: 3f);
    }

    private void Update()
    {
        if (_combat == null || !_combat.Alive || _builder == null) return;
        var dt = Time.deltaTime;

        if (_combat.IsCaptured)
        {
            _combat.TickCapture(dt);
            return;
        }

        if (_target == null || _target.IsCaptured)
        {
            _target = FindNearestFreeCitizen();
        }

        if (_target != null)
        {
            var to = _target.transform.position - transform.position;
            to.y = 0f;
            var dist = to.magnitude;
            if (dist <= CaptureRange)
            {
                _target.Capture(_combat, PullSpeed, possess: true);
            }
            else
            {
                var dir = to / Mathf.Max(dist, 0.0001f);
                transform.position += dir * (MoveSpeed * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), dt * 5f);
            }
        }

        var p = transform.position;
        var gy = _builder.GroundHeightAt(p);
        if (!Mathf.Approximately(p.y, gy)) transform.position = new Vector3(p.x, gy, p.z);
    }

    private Citizen FindNearestFreeCitizen()
    {
        Citizen best = null;
        var bestSq = SeekRadius * SeekRadius;
        foreach (var c in _builder.Citizens)
        {
            if (c == null || c.IsCaptured) continue;
            var d = c.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq)
            {
                bestSq = d.sqrMagnitude;
                best = c;
            }
        }
        return best;
    }

    private void BuildModel()
    {
        // a squat, boxy collection-cart silhouette with a funnel scoop up
        // front -- distinct from Worker's humanoid capsule and from any
        // combat unit's shape (aesthetic-preferences skill §5: shape
        // reads kind).
        var hull = new Color(0.32f, 0.28f, 0.38f);   // muted violet -- reads as Mad Doctor apparatus, not human/army hardware
        Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.9f, 1.6f) * Scale, hull, keepCollider: true);
        Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.7f, 0.9f) * Scale, new Vector3(0.7f, 0.5f, 0.7f) * Scale, new Color(0.5f, 0.45f, 0.55f))
            .localRotation = Quaternion.Euler(90f, 0f, 0f);
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

    private void OnDied()
    {
    }
}
