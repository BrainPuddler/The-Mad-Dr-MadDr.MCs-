using UnityEngine;

/// <summary>
/// Big Brain jar -- creator direction (2026-08, "Major Improvement"):
/// "Add a very subtle, slow-pulsing light source at the bottom of the
/// chamber... illuminate the underside and lower folds of the brain...
/// Create an eerie green glow that rises through the liquid and catches
/// the lower brain surface. The pulse should be slow, organic, and
/// understated. Avoid obvious flickering or a generic video-game point
/// light."
///
/// A real (but tiny, shadowless, short-range) <see cref="Light"/> so it
/// actually illuminates NEIGHBORING geometry (the brain mesh's own
/// underside/lower folds) -- an emissive-material trick
/// (EmissiveAnimator's usual technique) only brightens its OWN surface,
/// it can't cast light onto anything else, so this genuinely needs a
/// live Light component, not just a brighter material.
///
/// Deliberately NOT routed through GlowPointRegistry/DynamicLightBudget
/// (see DynamicLightBudget.cs): that system gates every real light
/// behind DayNightState.NightAmount (a flat 0 all day) and competes it
/// against every streetlamp/window/neon in the whole city for one shared
/// nearest-N budget. The jar's glow is a permanent feature of THIS
/// building, not a city streetlight -- it should read exactly the same
/// at noon as at midnight. There are at most a handful of Big Brain
/// buildings in any one match (a real, expensive, roughly one-per-player
/// structure -- see BuildingDef's own 20-Brain cost), so one dedicated
/// Update() per instance is trivially cheap, unlike the "hundreds of
/// windows" case DynamicLightBudget exists to solve.
///
/// "Avoid... a generic video-game point light" is read as: pair the real
/// Light with an actual visible emissive SOURCE object (BaseDresser's
/// glow-disc prop) rather than a bare invisible light floating in space,
/// keep intensity/range modest (this is meant to be found, not to
/// dominate the jar), and give the pulse itself two slightly-detuned
/// sine waves instead of one -- a single sine breathes with an
/// identical rhythm every cycle, which reads as mechanical/synthetic
/// once watched for more than a few seconds; summing a second, slower,
/// smaller wave at a non-integer frequency ratio means the pulse's exact
/// shape and peak timing drift cycle to cycle, which is what "slow,
/// organic" actually looks like without needing true Perlin-noise-driven
/// modulation for something this subtle.
/// </summary>
public class EerieChamberGlow : MonoBehaviour
{
    private Light _light;
    private Renderer _glowRenderer;
    private MaterialPropertyBlock _block;
    private Color _baseEmission;
    private float _baseIntensity;
    private float _phaseSeed;

    private const float PrimaryPeriod = 5.2f;
    private const float SecondaryPeriod = 7.9f;
    private const float SecondaryWeight = 0.35f;
    // Never dips below this fraction of peak -- "subtle" pulsing, not a
    // blink/strobe; the brain's underside should always read as at
    // least dimly lit, just breathing in intensity.
    private const float FloorMultiplier = 0.55f;

    /// <summary>`glowRenderer` may be null (a light with no visible
    /// source prop still works, just loses the "pair it with something
    /// visible" cue this file's own header explains) -- everything else
    /// is required.</summary>
    public void Init(Light light, Renderer glowRenderer, Color eerieGreen, float lightIntensity)
    {
        _light = light;
        _glowRenderer = glowRenderer;
        _baseEmission = eerieGreen;
        _baseIntensity = lightIntensity;
        _block = new MaterialPropertyBlock();
        // Per-instance phase so multiple Big Brain jars in the same
        // match don't all pulse in lockstep -- same "hash the instance
        // ID into a phase offset" idiom DamageFx.SmokePuff.Init already
        // uses in this codebase.
        _phaseSeed = (GetInstanceID() & 1023) / 1023f * PrimaryPeriod;
    }

    private void Update()
    {
        if (_light == null) return;
        var t = Time.time + _phaseSeed;
        var primary = 0.5f + 0.5f * Mathf.Sin(t * (Mathf.PI * 2f / PrimaryPeriod));
        var secondary = 0.5f + 0.5f * Mathf.Sin(t * (Mathf.PI * 2f / SecondaryPeriod) + 1.7f);
        var wave = Mathf.Lerp(primary, secondary, SecondaryWeight);
        var mult = Mathf.Lerp(FloorMultiplier, 1f, wave);

        _light.intensity = _baseIntensity * mult;

        if (_glowRenderer != null)
        {
            _glowRenderer.GetPropertyBlock(_block);
            _block.SetColor("_EmissionColor", _baseEmission * mult);
            _glowRenderer.SetPropertyBlock(_block);
        }
    }
}
