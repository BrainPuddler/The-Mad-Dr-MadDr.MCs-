using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "Add Strong Visual Representation for
/// Area Attacks and Psionics -- Area Attacks and Psionic abilities are
/// functioning mechanically but have little or no visible
/// representation"): the resolver + procedural effect components for
/// both. Reuses the existing Special Attack pipeline entirely --
/// `SpecialAttackDefinition`/`SpecialAttackEffectType`/`WebAttackAbility`/
/// `SpecialAttackResolver` are untouched except for the new
/// <see cref="SpecialAttackVfxStyle"/> tag on the definition itself
/// (see that enum's own doc comment for why it's a separate axis from
/// EffectType). No new combat/damage logic anywhere in this file --
/// `PlayImpact` is called AFTER an ability has already resolved its real
/// effect, from the exact two chokepoints that already had an (always-
/// null-until-now) `ImpactVfxPrefab` hook: `WebAttackAbility.ResolveImpact`
/// and `SpecialAttackResolver.ResolveInstant`.
///
/// Visual design, per creator brief (kept close to these exact words so
/// future tuning has the original intent to check against):
/// - Area Attack: "bright white-hot core, soft blue electrical energy,
///   arcing electrical fields... irregular arcs and varying lengths
///   rather than symmetrical radial lines... WHITE CORE -> BLUE ENERGY
///   FIELD -> ELECTRICAL ARCS -> FADE." -- <see cref="AreaAttackEffect"/>.
/// - Psionic: "a psychic sphere that radiates expanding ripples...
///   MIND -> FORCE -> RADIATION... 2-4 overlapping wave fronts... avoid
///   making it look like a conventional explosion" -- no hard-edged
///   ground ring (Area gets one, Psionic deliberately doesn't -- the
///   expanding ripple spheres themselves read as "reach," a flat disc
///   would read as a blast marker instead) -- <see cref="PsionicRippleEffect"/>.
/// - Both draw from "1950s-70s B-movie sci-fi" per the brief, which
///   already matches this project's own established tone (see the
///   maddr-aesthetic-preferences skill) -- hand-built primitive meshes,
///   irregular/imperfect jitter, no photorealism, no screen-filling
///   bloom.
///
/// Per-attack SCALING (creator brief #5, "do not make every Area Attack
/// look identical... use radius/duration/damage/... to influence the
/// VFX"): `definition.AreaOfEffect` drives every effect's physical size
/// directly; `EstimateStrength01` derives an intensity scalar from
/// whichever magnitude field is actually meaningful for that ability's
/// `EffectType` (Damage->DamageAmount, Stun->StunDuration, otherwise
/// AreaOfEffect itself as the only available proxy); `EstimateDuration`
/// derives playback length from `Cooldown` as a stand-in for "how
/// powerful/deliberate" this ability is -- see that method's own doc
/// comment for why Cooldown, not a literal charge-time field that
/// doesn't exist in this data model, and why this only ever lengthens
/// the POST-resolve playback, never delays the resolve itself (per the
/// brief's own "does not require changes to the underlying combat/
/// damage logic unless absolutely necessary").
///
/// PERFORMANCE (creator brief #6): no ParticleSystem anywhere (matching
/// this codebase's own firm, repeatedly-reaffirmed convention -- see
/// DamageFx.cs/TeslaArc.cs/BrainJarBubbles.cs's own headers), no real
/// Light components (glow is emissive-material only, Tier 1 in this
/// project's own lighting-budget terms -- see the maddr-lighting-system
/// skill), one shared `Material` per visual layer per style (never `new
/// Material` per attack) with all per-instance variation driven through
/// `MaterialPropertyBlock` (the SAME established idiom
/// EerieChamberGlow/EmissiveAnimator/HumanCharacterKit already use
/// throughout this codebase for exactly this reason), and a small
/// generic <see cref="VfxPool"/> so a burst of simultaneous attacks
/// reuses warm GameObjects instead of spawning/destroying fresh ones --
/// this codebase otherwise has NO GameObject pooling precedent
/// (DamageFx's own effects spawn-and-self-destroy), so this is a new,
/// deliberately small and self-contained addition, not a retrofit of
/// the existing damage-FX system.
///
/// Effect components (<see cref="AreaAttackEffect"/>, <see
/// cref="PsionicRippleEffect"/>) are sibling top-level classes in this
/// file, same shape as `DamageFx.cs`'s own `FirePlume`/`SmokePlume`/
/// `GroundStain` etc. -- not nested inside this static resolver class.
///
/// SCOPE NOTE: Flamethrower Burst (Damage, real AoE) and Ground Stomp
/// (Stun, real AoE) both get the Area (electric) look by default --
/// only Psionic Tractor Beam is tagged Psionic. The brief describes
/// exactly two visual languages and doesn't ask for a fire-specific
/// reskin of Flamethrower Burst; giving it one later is a one-line
/// change (tag it, add a `Fire` case) thanks to `VfxStyle` being a
/// per-definition tag rather than inferred from EffectType.
/// </summary>
public static class SpecialAttackVfx
{
    // ---- resolver entry points --------------------------------------

    /// <summary>Called once an ability has already resolved its real
    /// effect (damage/stun/capture already applied) -- purely additive,
    /// changes nothing about combat. `point` is the SAME impact/origin
    /// point the caller already computed (a target's position for a
    /// ranged AoE, the caster's own feet for a self-centered one like
    /// Ground Stomp) -- this naturally satisfies the brief's "a
    /// concentrated psychic sphere appears around the source OR target"
    /// with no special-casing here.</summary>
    public static void PlayImpact(SpecialAttackDefinition definition, Vector3 point)
    {
        if (definition == null) return;
        var radius = Mathf.Max(0.5f, definition.AreaOfEffect);
        var strength = EstimateStrength01(definition);
        var duration = EstimateDuration(definition);

        switch (definition.VfxStyle)
        {
            case SpecialAttackVfxStyle.Psionic:
            {
                var go = VfxPool.Get(PsionicKey, BuildPsionicRoot);
                go.GetComponent<PsionicRippleEffect>().Begin(point, radius, strength, duration);
                break;
            }
            case SpecialAttackVfxStyle.Organic:
            {
                var go = VfxPool.Get(OrganicKey, BuildOrganicRoot);
                go.GetComponent<OrganicCloudEffect>().Begin(point, radius, strength, duration);
                break;
            }
            case SpecialAttackVfxStyle.Disruption:
            {
                var go = VfxPool.Get(DisruptionKey, BuildDisruptionRoot);
                go.GetComponent<DisruptionPulseEffect>().Begin(point, radius, strength, duration);
                break;
            }
            default:
            {
                var go = VfxPool.Get(AreaKey, BuildAreaRoot);
                go.GetComponent<AreaAttackEffect>().Begin(point, radius, strength, duration);
                break;
            }
        }
    }

    /// <summary>2026-08 fix: `WebAttackAbility.Launch`'s bolt (used by
    /// both Web Attack's delivery and Psionic Tractor Beam) is a bare
    /// `GameObject` with only a `Projectile` component -- no mesh, no
    /// renderer, fully invisible in flight (confirmed by direct
    /// inspection: `WeaponFx.MakeShot` gives every REGULAR weapon shot a
    /// glowing sphere, this path never got the same treatment). Adds a
    /// small glowing core sized off the ability's own AreaOfEffect (a
    /// bigger blast telegraphs itself with a bigger bolt) and a
    /// `SlowSpin` for visible in-flight motion -- reusing that existing,
    /// trivial component rather than writing a new spin behaviour. Not
    /// pooled: the bolt `GameObject` itself is already created once per
    /// cast and `Object.Destroy`'d once on arrival/timeout by
    /// `Projectile` (unchanged, pre-existing lifecycle) -- this just
    /// parents one more small child onto an object that already has
    /// exactly that lifetime, no new churn.</summary>
    public static void AttachProjectileGlow(GameObject bolt, SpecialAttackDefinition definition)
    {
        if (bolt == null || definition == null) return;
        var psionic = definition.VfxStyle == SpecialAttackVfxStyle.Psionic;
        var mat = psionic ? PsionicBoltMaterial : AreaBoltMaterial;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BoltGlow";
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        go.transform.SetParent(bolt.transform, false);
        var size = Mathf.Clamp(definition.AreaOfEffect * 0.12f, 0.2f, 0.6f);
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var spin = go.AddComponent<SlowSpin>();
        spin.axis = Vector3.up;
        spin.degreesPerSecond = 260f;
    }

    // ---- per-attack scaling -------------------------------------------

    internal static float EstimateStrength01(SpecialAttackDefinition def)
    {
        switch (def.EffectType)
        {
            case SpecialAttackEffectType.Damage:
                return Mathf.Clamp01(def.DamageAmount / 40f);
            case SpecialAttackEffectType.Stun:
                return Mathf.Clamp01(def.StunDuration / 4f);
            default:
                // PullAndConsume/SlowStatus have no direct magnitude
                // field on SpecialAttackDefinition -- AreaOfEffect
                // itself is the only number available as a proxy.
                return Mathf.Clamp01(def.AreaOfEffect / 8f);
        }
    }

    /// <summary>"A longer charge should allow the visual effect to build
    /// before release... slow powerful attacks should have more
    /// anticipation and energy buildup." This data model has no literal
    /// wind-up/charge-time field for special attacks -- they resolve
    /// INSTANTLY the moment `TickSpecialAttack` triggers them (confirmed
    /// by inspection: no charge state anywhere in `MonsterAgent`), and
    /// adding one would be a real combat-timing change the brief
    /// explicitly says to avoid unless absolutely necessary. `Cooldown`
    /// is used as the closest available proxy for "how deliberate/
    /// powerful this ability is" instead -- every definition already has
    /// one, and the three that exist today already correlate with
    /// weight (Ground Stomp 14s > Psionic Tractor Beam 12s > Flamethrower
    /// Burst 10s). A heavier-cooldown ability gets a longer, more
    /// elaborate VISUAL PLAYBACK after the instant resolve -- this only
    /// ever stretches what happens AFTER the (unchanged) instant
    /// trigger, never delays the trigger itself.</summary>
    internal static float EstimateDuration(SpecialAttackDefinition def)
    {
        var t = Mathf.InverseLerp(6f, 16f, def.Cooldown);
        return Mathf.Lerp(0.55f, 1.3f, Mathf.Clamp01(t));
    }

    // ---- shared materials (one per visual layer, never per-attack) ----

    internal static readonly Color AreaCoreColor = new Color(1f, 1f, 1f);
    internal static readonly Color AreaGlowColor = new Color(0.35f, 0.65f, 1f);
    internal static readonly Color AreaArcColor = new Color(0.65f, 0.85f, 1f);
    internal static readonly Color AreaRingColor = new Color(0.3f, 0.6f, 1f);

    // Violet/magenta -- distinct from Area's reserved blue-white and
    // from fire's orange/red, and unclaimed elsewhere in this codebase
    // (the Big Brain jar's own "eerie glow" is green, a different prop
    // in a different context -- see BrainJarBubbles.cs/EerieChamberGlow.cs).
    // A very standard, immediately-readable "psychic power" association.
    internal static readonly Color PsionicCoreColor = new Color(0.85f, 0.72f, 1f);
    internal static readonly Color PsionicRippleColor = new Color(0.55f, 0.28f, 0.88f);

    // 2026-08 ("Expand Secondary Attack Variety Across Races"): sickly
    // yellow-green -- a biological/toxic read, distinct from Area's
    // blue-white and Psionic's violet, and from HazardZoneEffect's own
    // pure green (a lingering GROUND patch reads differently from a
    // drifting AIR cloud, so a slightly different hue keeps the two
    // "biological" effects visually distinguishable from each other).
    internal static readonly Color OrganicCoreColor = new Color(0.7f, 0.85f, 0.4f);
    internal static readonly Color OrganicCloudColor = new Color(0.55f, 0.72f, 0.32f);

    // Pale, stark yellow-white -- a "scream/psychic shock" read, sharp
    // and punchy rather than glowing, distinct from every other style.
    internal static readonly Color DisruptionColor = new Color(1f, 0.95f, 0.65f);

    internal const float AreaCoreEmission = 6f;
    internal const float AreaGlowEmission = 1.6f;
    internal const float AreaArcEmission = 3f;
    internal const float AreaRingEmission = 1.2f;
    internal const float PsionicCoreEmission = 5f;
    internal const float PsionicRippleEmission = 2.2f;
    internal const float OrganicCoreEmission = 2.4f;
    internal const float OrganicCloudEmission = 1f;
    internal const float DisruptionEmission = 4.5f;

    private static Material _areaCoreMat, _areaGlowMat, _areaArcMat, _areaRingMat;
    private static Material _psionicCoreMat, _psionicRippleMat;
    private static Material _areaBoltMat, _psionicBoltMat;
    private static Material _organicCoreMat, _organicCloudMat, _disruptionMat;

    internal static Material AreaCoreMaterial { get { return _areaCoreMat ?? (_areaCoreMat = MakeGlowMaterial(AreaCoreColor, AreaCoreEmission, 3002)); } }
    internal static Material AreaGlowMaterial { get { return _areaGlowMat ?? (_areaGlowMat = MakeGlowMaterial(AreaGlowColor, AreaGlowEmission, 3001)); } }
    internal static Material AreaArcMaterial { get { return _areaArcMat ?? (_areaArcMat = MakeGlowMaterial(AreaArcColor, AreaArcEmission, 3002)); } }
    internal static Material AreaRingMaterial { get { return _areaRingMat ?? (_areaRingMat = MakeGlowMaterial(AreaRingColor, AreaRingEmission, 3000)); } }
    internal static Material PsionicCoreMaterial { get { return _psionicCoreMat ?? (_psionicCoreMat = MakeGlowMaterial(PsionicCoreColor, PsionicCoreEmission, 3002)); } }
    internal static Material PsionicRippleMaterial { get { return _psionicRippleMat ?? (_psionicRippleMat = MakeGlowMaterial(PsionicRippleColor, PsionicRippleEmission, 3001)); } }
    internal static Material OrganicCoreMaterial { get { return _organicCoreMat ?? (_organicCoreMat = MakeGlowMaterial(OrganicCoreColor, OrganicCoreEmission, 3002)); } }
    internal static Material OrganicCloudMaterial { get { return _organicCloudMat ?? (_organicCloudMat = MakeGlowMaterial(OrganicCloudColor, OrganicCloudEmission, 3001)); } }
    internal static Material DisruptionMaterial { get { return _disruptionMat ?? (_disruptionMat = MakeGlowMaterial(DisruptionColor, DisruptionEmission, 3002)); } }
    private static Material AreaBoltMaterial { get { return _areaBoltMat ?? (_areaBoltMat = MakeGlowMaterial(AreaArcColor, 2.2f, 3002)); } }
    private static Material PsionicBoltMaterial { get { return _psionicBoltMat ?? (_psionicBoltMat = MakeGlowMaterial(PsionicCoreColor, 2.2f, 3002)); } }

    /// <summary>Same recipe every transparent glow material in this
    /// codebase already uses (ShaderUtil.FindRenderableShader +
    /// LabMeshBuilder.MakeTransparent + EnableKeyword("_EMISSION")).
    /// `renderQueue` is bumped explicitly per layer -- same "force an
    /// explicit composite order between overlapping transparent shells"
    /// precedent DamageFx.cs's FirePlume already established (fire drawn
    /// over smoke) -- so a bright core always reads on top of its own
    /// dimmer glow shell/ring rather than depending on draw order being
    /// whatever it happens to be.</summary>
    private static Material MakeGlowMaterial(Color color, float emission, int renderQueue)
    {
        var mat = new Material(ShaderUtil.FindRenderableShader());
        mat.color = color;
        LabMeshBuilder.MakeTransparent(mat);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
        }
        mat.renderQueue = renderQueue;
        return mat;
    }

    internal static void SetAlpha(Renderer r, MaterialPropertyBlock block, Color baseColor, float alpha, float emissionMul)
    {
        r.GetPropertyBlock(block);
        var c = baseColor;
        c.a = Mathf.Clamp01(alpha);
        block.SetColor("_BaseColor", c);
        block.SetColor("_EmissionColor", baseColor * emissionMul);
        r.SetPropertyBlock(block);
    }

    internal static Transform MakePrimitiveChild(Transform parent, PrimitiveType type, string name, Material sharedMat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = sharedMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    // ---- pooling --------------------------------------------------------

    internal const string AreaKey = "SpecialAttackVfx.Area";
    internal const string PsionicKey = "SpecialAttackVfx.Psionic";
    internal const string OrganicKey = "SpecialAttackVfx.Organic";
    internal const string DisruptionKey = "SpecialAttackVfx.Disruption";

    private static GameObject BuildAreaRoot()
    {
        var go = new GameObject("AreaAttackVfx");
        go.AddComponent<AreaAttackEffect>();
        return go;
    }

    private static GameObject BuildPsionicRoot()
    {
        var go = new GameObject("PsionicVfx");
        go.AddComponent<PsionicRippleEffect>();
        return go;
    }

    private static GameObject BuildOrganicRoot()
    {
        var go = new GameObject("OrganicCloudVfx");
        go.AddComponent<OrganicCloudEffect>();
        return go;
    }

    private static GameObject BuildDisruptionRoot()
    {
        var go = new GameObject("DisruptionPulseVfx");
        go.AddComponent<DisruptionPulseEffect>();
        return go;
    }
}

/// <summary>2026-08 (creator brief #6, "prefer... object pooling...
/// avoid... unnecessary GameObject creation/destruction"): a small
/// generic pool keyed by a string tag. This codebase has no existing
/// GameObject-pooling precedent to extend (DamageFx's own effects
/// spawn-and-self-destroy on a timer -- see that file's own header), so
/// this is a new, deliberately minimal addition scoped to these two new
/// effect types only; it does not touch DamageFx or any other existing
/// VFX. Grows lazily to whatever the real peak concurrent-effect count
/// turns out to be, then stays warm at that level -- no further
/// allocation once warmed up.</summary>
internal static class VfxPool
{
    private static readonly Dictionary<string, Stack<GameObject>> _free = new Dictionary<string, Stack<GameObject>>();

    public static GameObject Get(string key, System.Func<GameObject> factory)
    {
        if (_free.TryGetValue(key, out var stack) && stack.Count > 0)
        {
            var go = stack.Pop();
            go.SetActive(true);
            return go;
        }
        var created = factory();
        created.SetActive(true);
        return created;
    }

    public static void Release(string key, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (!_free.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _free[key] = stack;
        }
        stack.Push(go);
    }
}

/// <summary>"WHITE CORE -> BLUE ENERGY FIELD -> ELECTRICAL ARCS -> FADE":
/// a small flashing-then-shrinking white core, a soft blue glow shell
/// that expands toward the ability's real AreaOfEffect radius, 3-5
/// short jittering electrical arcs (irregular direction/length,
/// periodically re-snapped rather than smoothly tracking two fixed
/// points -- "somewhat unstable," not TeslaArc's steady beam-between-
/// two-anchors look), and a flat ground ring sized to the real radius
/// so the area/radius component the brief asks for is literally,
/// visibly true to the underlying data. Pooled -- see <see
/// cref="VfxPool"/> -- so <see cref="Begin"/> must fully reset every
/// piece of per-instance state, never assume a freshly-constructed
/// object.</summary>
public class AreaAttackEffect : MonoBehaviour
{
    private const int MaxArcs = 5;
    private const int ArcSegments = 5;

    private Transform _coreT, _glowT, _ringT;
    private Renderer _coreR, _glowR, _ringR;
    private MaterialPropertyBlock _coreBlock, _glowBlock, _ringBlock;
    private LineRenderer[] _arcs;
    private MaterialPropertyBlock[] _arcBlocks;
    private Vector3[] _arcScratch;
    private Vector3[] _arcEnd;
    private float[] _arcPhase;

    private float _age, _duration, _radius, _strength, _nextReroll;
    private int _activeArcs;
    private System.Random _rng;

    private void Awake()
    {
        _coreT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Core", SpecialAttackVfx.AreaCoreMaterial);
        _coreR = _coreT.GetComponent<Renderer>();
        _coreBlock = new MaterialPropertyBlock();

        _glowT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Glow", SpecialAttackVfx.AreaGlowMaterial);
        _glowR = _glowT.GetComponent<Renderer>();
        _glowBlock = new MaterialPropertyBlock();

        _ringT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Cylinder, "Ring", SpecialAttackVfx.AreaRingMaterial);
        _ringR = _ringT.GetComponent<Renderer>();
        _ringBlock = new MaterialPropertyBlock();

        _arcs = new LineRenderer[MaxArcs];
        _arcBlocks = new MaterialPropertyBlock[MaxArcs];
        _arcEnd = new Vector3[MaxArcs];
        _arcPhase = new float[MaxArcs];
        _arcScratch = new Vector3[ArcSegments + 1];
        for (var i = 0; i < MaxArcs; i++)
        {
            var arcGo = new GameObject("Arc");
            arcGo.transform.SetParent(transform, false);
            var lr = arcGo.AddComponent<LineRenderer>();
            lr.positionCount = ArcSegments + 1;
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.12f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = SpecialAttackVfx.AreaArcMaterial;
            _arcs[i] = lr;
            _arcBlocks[i] = new MaterialPropertyBlock();
        }
    }

    public void Begin(Vector3 point, float radius, float strength01, float duration)
    {
        transform.position = point;
        _radius = radius;
        _strength = strength01;
        _duration = Mathf.Max(0.05f, duration);
        _age = 0f;
        // reseeded each use (not once in Awake) so a REUSED pooled
        // instance still crackles differently attack to attack --
        // mixing in Time.time, not UnityEngine.Random, so this never
        // touches the project's shared/gameplay RNG stream (same
        // "display-only jitter" posture BrainJarBubbles.cs already
        // documents for its own System.Random usage).
        _rng = new System.Random((GetInstanceID() * 397) ^ Mathf.RoundToInt(Time.time * 1000f));
        _activeArcs = Mathf.Clamp(3 + Mathf.RoundToInt(strength01 * 2f), 3, MaxArcs);
        for (var i = 0; i < MaxArcs; i++) _arcs[i].gameObject.SetActive(i < _activeArcs);
        _nextReroll = 0f;
        RerollArcs();
        gameObject.SetActive(true);
    }

    private void RerollArcs()
    {
        for (var i = 0; i < _activeArcs; i++)
        {
            var angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
            var elevation = (float)(_rng.NextDouble() * 0.6f - 0.1f);
            var length = _radius * (0.45f + (float)_rng.NextDouble() * 0.55f);
            var dir = new Vector3(Mathf.Cos(angle), elevation, Mathf.Sin(angle)).normalized;
            _arcEnd[i] = dir * length;
            _arcPhase[i] = (float)_rng.NextDouble() * 20f;
        }
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _duration);

        var coreT = Mathf.Clamp01(t / 0.3f);
        var coreScale = Mathf.Lerp(_radius * 0.35f, _radius * 0.08f, coreT) * (1f + _strength * 0.4f);
        _coreT.localScale = Vector3.one * coreScale;
        SpecialAttackVfx.SetAlpha(_coreR, _coreBlock, SpecialAttackVfx.AreaCoreColor, Mathf.Lerp(1f, 0f, coreT), SpecialAttackVfx.AreaCoreEmission * (1f + _strength));

        var glowScale = Mathf.Lerp(_radius * 0.3f, _radius * (0.9f + _strength * 0.3f), Mathf.SmoothStep(0f, 1f, t));
        _glowT.localScale = Vector3.one * glowScale;
        SpecialAttackVfx.SetAlpha(_glowR, _glowBlock, SpecialAttackVfx.AreaGlowColor, Mathf.Lerp(0.55f, 0f, t), SpecialAttackVfx.AreaGlowEmission);

        var ringScale = _radius * 2f;
        _ringT.localScale = new Vector3(ringScale, 0.05f, ringScale);
        SpecialAttackVfx.SetAlpha(_ringR, _ringBlock, SpecialAttackVfx.AreaRingColor, Mathf.Lerp(0.4f, 0f, t), SpecialAttackVfx.AreaRingEmission);

        if (t < 0.6f && _age >= _nextReroll)
        {
            RerollArcs();
            _nextReroll = _age + 0.09f;
        }
        UpdateArcs(t);

        if (t >= 1f) VfxPool.Release(SpecialAttackVfx.AreaKey, gameObject);
    }

    private void UpdateArcs(float t)
    {
        var fade = Mathf.Clamp01(1f - t);
        for (var i = 0; i < _activeArcs; i++)
        {
            var lr = _arcs[i];
            var end = _arcEnd[i];
            for (var s = 0; s <= ArcSegments; s++)
            {
                var frac = s / (float)ArcSegments;
                var p = Vector3.Lerp(Vector3.zero, end, frac);
                if (s > 0 && s < ArcSegments)
                {
                    var seed = Time.time * 14f + _arcPhase[i] + s * 2.3f;
                    p += new Vector3(
                        Mathf.PerlinNoise(seed, 0f) - 0.5f,
                        Mathf.PerlinNoise(0f, seed) - 0.5f,
                        Mathf.PerlinNoise(seed, seed) - 0.5f) * (_radius * 0.12f);
                }
                _arcScratch[s] = p;
            }
            lr.SetPositions(_arcScratch);
            var flicker = 0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 9f + _arcPhase[i], 0f);
            SpecialAttackVfx.SetAlpha(lr, _arcBlocks[i], SpecialAttackVfx.AreaArcColor, fade * flicker, SpecialAttackVfx.AreaArcEmission);
        }
    }
}

/// <summary>"MIND -> FORCE -> RADIATION": the centre briefly brightens,
/// then 3 overlapping translucent spheres expand outward at slightly
/// different speeds/scales (staggered onset, not synchronized) toward
/// the ability's real AreaOfEffect radius, softly fading as each one
/// grows -- "pressure moving through space," deliberately with no
/// hard-edged ring/flash geometry (see this file's own header for why
/// Psionic and Area intentionally don't share a ground-ring
/// treatment).</summary>
public class PsionicRippleEffect : MonoBehaviour
{
    private const int RippleCount = 3;
    private static readonly float[] RippleStartFrac = { 0f, 0.18f, 0.36f };
    private static readonly float[] RippleSpeedMul = { 1f, 0.85f, 1.15f };

    private Transform _coreT;
    private Renderer _coreR;
    private MaterialPropertyBlock _coreBlock;
    private readonly Transform[] _rippleT = new Transform[RippleCount];
    private readonly Renderer[] _rippleR = new Renderer[RippleCount];
    private readonly MaterialPropertyBlock[] _rippleBlock = new MaterialPropertyBlock[RippleCount];

    private float _age, _duration, _radius, _strength;

    private void Awake()
    {
        _coreT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Core", SpecialAttackVfx.PsionicCoreMaterial);
        _coreR = _coreT.GetComponent<Renderer>();
        _coreBlock = new MaterialPropertyBlock();

        for (var i = 0; i < RippleCount; i++)
        {
            _rippleT[i] = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Ripple", SpecialAttackVfx.PsionicRippleMaterial);
            _rippleR[i] = _rippleT[i].GetComponent<Renderer>();
            _rippleBlock[i] = new MaterialPropertyBlock();
        }
    }

    public void Begin(Vector3 point, float radius, float strength01, float duration)
    {
        transform.position = point;
        _radius = radius;
        _strength = strength01;
        _duration = Mathf.Max(0.05f, duration);
        _age = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _duration);

        var coreFlashT = Mathf.Clamp01(t / 0.2f);
        var coreScale = Mathf.Lerp(_radius * 0.18f, _radius * 0.12f, coreFlashT);
        _coreT.localScale = Vector3.one * coreScale;
        var coreFade = Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.5f) / 0.5f));
        SpecialAttackVfx.SetAlpha(_coreR, _coreBlock, SpecialAttackVfx.PsionicCoreColor, Mathf.Lerp(1f, 0.35f, coreFlashT) * coreFade,
            SpecialAttackVfx.PsionicCoreEmission * (1f + _strength) * Mathf.Lerp(1.6f, 1f, coreFlashT));

        for (var i = 0; i < RippleCount; i++)
        {
            var localT = Mathf.Clamp01((t - RippleStartFrac[i]) / (1f - RippleStartFrac[i]));
            var maxScale = _radius * (0.85f + 0.25f * RippleSpeedMul[i] + _strength * 0.2f);
            var scale = Mathf.Lerp(_radius * 0.12f, maxScale, Mathf.SmoothStep(0f, 1f, localT));
            _rippleT[i].localScale = Vector3.one * scale;
            var alpha = t < RippleStartFrac[i] ? 0f : Mathf.Lerp(0.5f, 0f, localT);
            SpecialAttackVfx.SetAlpha(_rippleR[i], _rippleBlock[i], SpecialAttackVfx.PsionicRippleColor, alpha, SpecialAttackVfx.PsionicRippleEmission);
        }

        if (t >= 1f) VfxPool.Release(SpecialAttackVfx.PsionicKey, gameObject);
    }
}

/// <summary>2026-08 ("Expand Secondary Attack Variety Across Races" --
/// Spore Cloud/Defensive Spore Burst/Toxic Sac's own burst moment): a
/// small central core plus 3 translucent puffs that drift outward on
/// independent, slightly wandering paths (Perlin-noise lateral offset,
/// not a straight radial line) -- "an expanding translucent particulate
/// cloud... organic drifting motion," per the brief. Deliberately only
/// 3 puffs ("avoid excessive particle counts") rather than a real
/// particle-system emitter.</summary>
public class OrganicCloudEffect : MonoBehaviour
{
    private const int PuffCount = 3;

    private Transform _coreT;
    private Renderer _coreR;
    private MaterialPropertyBlock _coreBlock;
    private readonly Transform[] _puffT = new Transform[PuffCount];
    private readonly Renderer[] _puffR = new Renderer[PuffCount];
    private readonly MaterialPropertyBlock[] _puffBlock = new MaterialPropertyBlock[PuffCount];
    private readonly Vector3[] _puffDir = new Vector3[PuffCount];
    private readonly float[] _puffPhase = new float[PuffCount];
    private readonly float[] _puffReach = new float[PuffCount];

    private float _age, _duration, _radius, _strength;
    private System.Random _rng;

    private void Awake()
    {
        _coreT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Core", SpecialAttackVfx.OrganicCoreMaterial);
        _coreR = _coreT.GetComponent<Renderer>();
        _coreBlock = new MaterialPropertyBlock();

        for (var i = 0; i < PuffCount; i++)
        {
            _puffT[i] = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Puff", SpecialAttackVfx.OrganicCloudMaterial);
            _puffR[i] = _puffT[i].GetComponent<Renderer>();
            _puffBlock[i] = new MaterialPropertyBlock();
        }
    }

    public void Begin(Vector3 point, float radius, float strength01, float duration)
    {
        transform.position = point;
        _radius = radius;
        _strength = strength01;
        _duration = Mathf.Max(0.05f, duration);
        _age = 0f;
        // reseeded each use (pooled instance) -- same "mix in Time.time,
        // never touch the shared/gameplay RNG stream" posture
        // AreaAttackEffect.Begin already documents.
        _rng = new System.Random((GetInstanceID() * 397) ^ Mathf.RoundToInt(Time.time * 1000f));
        for (var i = 0; i < PuffCount; i++)
        {
            var angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
            var elevation = (float)(_rng.NextDouble() * 0.5f);
            _puffDir[i] = new Vector3(Mathf.Cos(angle), elevation, Mathf.Sin(angle)).normalized;
            _puffPhase[i] = (float)(_rng.NextDouble() * 10f);
            _puffReach[i] = 0.65f + (float)_rng.NextDouble() * 0.35f;
        }
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _duration);

        var coreScale = Mathf.Lerp(_radius * 0.15f, _radius * 0.3f, Mathf.SmoothStep(0f, 1f, t)) * (1f + _strength * 0.3f);
        _coreT.localScale = Vector3.one * coreScale;
        SpecialAttackVfx.SetAlpha(_coreR, _coreBlock, SpecialAttackVfx.OrganicCoreColor, Mathf.Lerp(0.7f, 0f, t), SpecialAttackVfx.OrganicCoreEmission);

        var expand = Mathf.SmoothStep(0f, 1f, t);
        for (var i = 0; i < PuffCount; i++)
        {
            var wander = Mathf.PerlinNoise(_age * 0.6f + _puffPhase[i], 0f) - 0.5f;
            var travel = expand * _radius * _puffReach[i];
            _puffT[i].localPosition = _puffDir[i] * travel + Vector3.up * (wander * _radius * 0.15f);
            _puffT[i].localScale = Vector3.one * Mathf.Lerp(_radius * 0.2f, _radius * 0.45f, t);
            var alpha = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.45f;   // fades in, peaks mid-life, fades out
            SpecialAttackVfx.SetAlpha(_puffR[i], _puffBlock[i], SpecialAttackVfx.OrganicCloudColor, alpha, SpecialAttackVfx.OrganicCloudEmission);
        }

        if (t >= 1f) VfxPool.Release(SpecialAttackVfx.OrganicKey, gameObject);
    }
}

/// <summary>2026-08 ("Expand Secondary Attack Variety Across Races" --
/// Panic Shriek/Neural Disruption/Mutagenic Pulse): a fast, sharp
/// central flash plus one quickly-expanding thin ring -- "strong
/// audiovisual feedback, but keep the implementation performant" and
/// "concentric/radiating energy waves" per the brief. Deliberately much
/// shorter-lived than the other three styles (capped well under a
/// second) -- a shriek/disruption reads as instantaneous, not a
/// lingering effect.</summary>
public class DisruptionPulseEffect : MonoBehaviour
{
    private Transform _flashT, _ringT;
    private Renderer _flashR, _ringR;
    private MaterialPropertyBlock _flashBlock, _ringBlock;

    private float _age, _duration, _radius, _strength;

    private void Awake()
    {
        _flashT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Sphere, "Flash", SpecialAttackVfx.DisruptionMaterial);
        _flashR = _flashT.GetComponent<Renderer>();
        _flashBlock = new MaterialPropertyBlock();

        _ringT = SpecialAttackVfx.MakePrimitiveChild(transform, PrimitiveType.Cylinder, "Ring", SpecialAttackVfx.DisruptionMaterial);
        _ringR = _ringT.GetComponent<Renderer>();
        _ringBlock = new MaterialPropertyBlock();
    }

    public void Begin(Vector3 point, float radius, float strength01, float duration)
    {
        transform.position = point;
        _radius = radius;
        _strength = strength01;
        _duration = Mathf.Clamp(duration * 0.5f, 0.15f, 0.5f);
        _age = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        var t = Mathf.Clamp01(_age / _duration);

        var flashT = Mathf.Clamp01(t / 0.3f);
        var flashScale = Mathf.Lerp(_radius * 0.25f, _radius * 0.05f, flashT) * (1f + _strength * 0.5f);
        _flashT.localScale = Vector3.one * flashScale;
        SpecialAttackVfx.SetAlpha(_flashR, _flashBlock, SpecialAttackVfx.DisruptionColor, Mathf.Lerp(1f, 0f, flashT), SpecialAttackVfx.DisruptionEmission);

        var ringScale = Mathf.Lerp(_radius * 0.1f, _radius * 2f, Mathf.SmoothStep(0f, 1f, t));
        _ringT.localScale = new Vector3(ringScale, 0.03f, ringScale);
        SpecialAttackVfx.SetAlpha(_ringR, _ringBlock, SpecialAttackVfx.DisruptionColor, Mathf.Lerp(0.6f, 0f, t), SpecialAttackVfx.DisruptionEmission * 0.6f);

        if (t >= 1f) VfxPool.Release(SpecialAttackVfx.DisruptionKey, gameObject);
    }
}
