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
/// v0.1 scope: exists, has HP, can be targeted/killed like any other
/// combatant, stands where it was possessed. No move orders, no
/// construction behavior yet -- Phase 3 of the same epic (worker-gated
/// building construction) is what actually puts it to work; this class
/// is the unit itself, not yet the job.
/// </summary>
public class Worker : MonoBehaviour
{
    private const float Scale = 0.55f;   // a little smaller than a Citizen's own capsule -- reads as "person", not "monster"

    private RuntimeCityBuilder _builder;
    private UnitCombat _combat;

    public UnitCombat Combat { get { return _combat; } }

    public void Init(RuntimeCityBuilder builder)
    {
        _builder = builder;
        BuildModel();
        _combat = gameObject.AddComponent<UnitCombat>();
        // low HP, no weapon -- a Worker is economic infrastructure, not a
        // combatant, matching docs/22's own "medics/harvesters are frail"
        // precedent rather than inventing a new tier.
        _combat.Configure("monster", 40f, 0.5f, 1.1f, weapon: null, OnDied, mass: 1f);
    }

    private void Update()
    {
        if (_combat == null || !_combat.Alive || _builder == null) return;
        var dt = Time.deltaTime;

        if (_combat.IsCaptured) _combat.TickCapture(dt);

        var p = transform.position;
        var gy = _builder.GroundHeightAt(p);
        if (!Mathf.Approximately(p.y, gy)) transform.position = new Vector3(p.x, gy, p.z);
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
        Prim(PrimitiveType.Capsule, transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.45f, 0.5f), khaki, keepCollider: true);
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
    /// Tank.cs's own OnDied already establishes for a bespoke combatant.</summary>
    private void OnDied()
    {
        if (_builder != null) _builder.OnWorkerDied(this);
        Object.Destroy(gameObject);
    }
}
