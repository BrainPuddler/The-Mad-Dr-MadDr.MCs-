using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Expand Secondary Attack Variety Across
/// Races" -- Toxic Sac, "area denial... drops or throws a biological
/// sac that bursts after a short delay... creates a small hazardous
/// area... enemies entering it receive a temporary debuff... area
/// persists briefly... telegraph the danger clearly"): a persistent,
/// visible ground patch spawned by `SpecialAttackResolver.ResolveInstant`
/// for any `SpecialAttackEffectType.Hazard` ability, in place of that
/// resolver's normal instant per-target effect (a Hazard catches nobody
/// at the moment it lands -- see that resolver's own doc comment).
///
/// Reuses `SpecialAttackVfx`'s existing pool/material/helper
/// infrastructure directly (same assembly, no asmdef split -- confirmed
/// before writing this) rather than building a second, parallel pooling
/// system: `VfxPool.Get`/`Release`, `SpecialAttackVfx.MakePrimitiveChild`/
/// `SetAlpha`/`MakeGlowMaterial`. This is a genuinely different SHAPE
/// from `AreaAttackEffect`/`PsionicRippleEffect` though -- those play
/// once over well under two seconds and release; a hazard zone
/// persists for several seconds AND does real per-target work on its
/// own schedule (`HazardTickInterval`, not every frame -- see
/// `SpecialAttackDefinition`'s own field comment), so it earns its own
/// class instead of forcing it into that file's existing two effect
/// shapes.
///
/// The debuff applied to anyone caught inside is the SAME
/// `UnitCombat.ApplyTempoModifier` a Weaken ability uses (slower fire
/// rate) -- reused, not duplicated, per the brief's own "do not
/// duplicate existing attack logic when a reusable ability/effect
/// component can be created" instruction. Re-applying it every tick to
/// a target that's still standing inside just refreshes the duration
/// (ApplyTempoModifier's own reapplication rule), so lingering in the
/// zone keeps the debuff topped up; leaving it lets the debuff expire
/// normally on its own timer.
/// </summary>
public class HazardZoneEffect : MonoBehaviour
{
    private const string PoolKey = "HazardZoneEffect";

    private Transform _diskT;
    private Renderer _diskR;
    private MaterialPropertyBlock _diskBlock;
    private static Material _diskMat;

    private RuntimeCityBuilder _builder;
    private UnitCombat _caster;
    private SpecialAttackDefinition _definition;
    private readonly List<UnitCombat> _scratch = new List<UnitCombat>();

    private float _age;
    private float _duration;
    private float _radius;
    private float _nextTick;

    /// <summary>Entry point -- called by `SpecialAttackResolver.ResolveInstant`
    /// instead of its normal per-target loop.</summary>
    public static void Spawn(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 point)
    {
        if (builder == null || definition == null) return;
        var go = VfxPool.Get(PoolKey, BuildRoot);
        go.GetComponent<HazardZoneEffect>().Begin(builder, caster, definition, point);
    }

    private static GameObject BuildRoot()
    {
        var go = new GameObject("HazardZoneEffect");
        go.AddComponent<HazardZoneEffect>();
        return go;
    }

    private static Material DiskMaterial
    {
        get
        {
            if (_diskMat == null)
            {
                // Toxic/organic green -- distinct from Area's blue-white
                // and Psionic's violet, matching the "biological hazard"
                // read the brief asks for. A hand-tuned color literal
                // rather than reusing SpecialAttackVfx's own internal
                // palette fields (those are private to that file's
                // effect classes) -- kept intentionally simple since
                // this is the only consumer.
                var color = new Color(0.45f, 0.85f, 0.3f);
                _diskMat = new Material(ShaderUtil.FindRenderableShader());
                _diskMat.color = color;
                LabMeshBuilder.MakeTransparent(_diskMat);
                if (_diskMat.HasProperty("_EmissionColor"))
                {
                    _diskMat.EnableKeyword("_EMISSION");
                    _diskMat.SetColor("_EmissionColor", color * 1.4f);
                }
                _diskMat.renderQueue = 3000;
            }
            return _diskMat;
        }
    }

    private void Awake()
    {
        _diskT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Cylinder, "HazardDisk", DiskMaterial);
        _diskR = _diskT.GetComponent<Renderer>();
        _diskBlock = new MaterialPropertyBlock();
    }

    private void Begin(RuntimeCityBuilder builder, UnitCombat caster, SpecialAttackDefinition definition, Vector3 point)
    {
        _builder = builder;
        _caster = caster;
        _definition = definition;
        transform.position = point;
        _radius = Mathf.Max(0.5f, definition.AreaOfEffect);
        _duration = Mathf.Max(0.1f, definition.HazardDuration);
        _age = 0f;
        _nextTick = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var lifeT = Mathf.Clamp01(_age / _duration);

        // telegraphed, obviously "still active" pulsing patch -- a slow
        // breathing scale/alpha, not a static decal, so it reads as a
        // live hazard rather than a scorch mark. Fades out only in the
        // final 20% of its life so the danger stays clearly visible for
        // almost its whole duration.
        var pulse = 0.85f + 0.15f * Mathf.Sin(_age * 4f);
        var diameter = _radius * 2f * pulse;
        _diskT.localScale = new Vector3(diameter, 0.04f, diameter);
        var fadeOut = lifeT < 0.8f ? 1f : Mathf.Lerp(1f, 0f, (lifeT - 0.8f) / 0.2f);
        SpecialAttackVfx.SetAlpha(_diskR, _diskBlock, new Color(0.45f, 0.85f, 0.3f), 0.35f * fadeOut, 1.4f);

        if (_age >= _nextTick)
        {
            _nextTick = _age + Mathf.Max(0.1f, _definition.HazardTickInterval);
            TickCatch();
        }

        if (_age >= _duration) VfxPool.Release(PoolKey, gameObject);
    }

    /// <summary>Periodic (not per-frame) re-check of who's currently
    /// standing inside the zone. Reuses `WebAttackAbility.ShouldCatchCombatant`
    /// (the same catch/classify decision every other ability already
    /// uses) so a Hazard's targeting rules are never a second, drifting
    /// copy of the normal rules.</summary>
    private void TickCatch()
    {
        if (_builder == null || _definition == null) return;
        _scratch.Clear();
        _builder.QueryCombatantsInRadius(transform.position, _radius, _scratch);
        foreach (var c in _scratch)
        {
            if (!WebAttackAbility.ShouldCatchCombatant(c, _caster, _definition, transform.position)) continue;
            c.ApplyTempoModifier(_definition.TempoMultiplier, _definition.TempoDuration);
        }
    }
}
