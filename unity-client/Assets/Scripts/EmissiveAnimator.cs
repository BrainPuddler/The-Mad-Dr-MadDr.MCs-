using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/28 (city lighting system): batched, per-instance emissive
/// animation for windows/neon/marquee lights at scale -- "hundreds on
/// screen, turn on/off and fade, keep it performant." No per-object
/// Update(), no per-instance Material (which would break SRP batching
/// and multiply draw calls); ONE manager loops registered entries in a
/// plain array and pushes a scaled emission color into each renderer's
/// own <see cref="MaterialPropertyBlock"/> -- the standard Unity
/// technique for "many instances share one Material, each needs its own
/// per-instance tweak" (SRP Batcher-friendly: MaterialPropertyBlock
/// overrides on `_EmissionColor` stay in the per-object CBUFFER, so this
/// doesn't fragment batching the way N separate Material instances
/// would). Cost per registered instance per frame is a couple of
/// trigonometry calls and one SetColor -- trivially cheap even at a few
/// hundred instances; <see cref="LightBehaviorKind.Steady"/> entries
/// (the overwhelming majority of props in a 1950s city -- most windows
/// are just... windows) are set ONCE at registration and never touched
/// again, so the actual per-frame loop only walks the subset that's
/// genuinely animating.
/// </summary>
public enum LightBehaviorKind
{
    /// <summary>No animation -- the base emission color, set once. The
    /// overwhelming majority of registrations should be this.</summary>
    Steady,

    /// <summary>Windows/occasional neon dropout: a slow, per-instance,
    /// out-of-phase brightness wobble that can dip toward
    /// CityLightingProfile.FlickerFloor -- reads as "someone's home,
    /// someone isn't" across a building face, never in lockstep.</summary>
    Flicker,

    /// <summary>Failing neon tube: a fast, small-amplitude flutter plus
    /// occasional brief full dropouts -- the classic buzzing-sign
    /// stutter, per-instance so a whole street of neon doesn't flicker
    /// in perfect unison.</summary>
    Buzz,

    /// <summary>Marquee "clique" chaser: a shared clock advances a lit
    /// index along a sequence; each registered bulb compares its own
    /// SequenceIndex to the current step.</summary>
    Chase,
}

public static class EmissiveAnimator
{
    private class Entry
    {
        public Renderer Renderer;
        public MaterialPropertyBlock Block;
        public Color BaseEmission;
        public LightBehaviorKind Kind;
        public float Seed;          // per-instance phase/frequency offset
        public int SequenceIndex;   // Chase only: this bulb's slot
        public int SequenceLength;  // Chase only: how many bulbs share this sequence
        public float NextDropout;   // Buzz only: next scheduled full-dropout time
    }

    private static readonly List<Entry> Animated = new List<Entry>();

    /// <summary>Register a renderer's emissive material for animation.
    /// `baseEmission` is the color at full brightness BEFORE the day/night
    /// boost (DayNightState.NeonBoost is applied on top every tick, same
    /// as NeonRegistry does for un-registered materials). `seed` should
    /// differ per instance (e.g. a hash of the object's position/hex) so
    /// hundreds of Flicker/Buzz registrations don't all move in lockstep --
    /// a shared seed of 0 for everything would look robotic.
    /// `sequenceIndex`/`sequenceLength` only matter for Chase.
    ///
    /// kind == Steady is a deliberate no-op: it does NOT install a
    /// MaterialPropertyBlock override at all, so the renderer just shows
    /// the shared material's own plain NeonRegistry-driven color --
    /// installing a frozen one-time override here would actually be WORSE
    /// than not registering (a property-block override on a renderer
    /// takes priority over the shared material's color for that renderer,
    /// so a Steady snapshot would permanently ignore the day/night cycle
    /// instead of tracking it normally).</summary>
    public static void Register(Renderer renderer, Color baseEmission, LightBehaviorKind kind,
        float seed = 0f, int sequenceIndex = 0, int sequenceLength = 1)
    {
        if (renderer == null || kind == LightBehaviorKind.Steady) return;
        var entry = new Entry
        {
            Renderer = renderer,
            Block = new MaterialPropertyBlock(),
            BaseEmission = baseEmission,
            Kind = kind,
            Seed = seed,
            SequenceIndex = sequenceIndex,
            SequenceLength = Mathf.Max(1, sequenceLength),
            NextDropout = Time.time + seed * 3f + CityLightingProfile.Active.BuzzDropoutIntervalSeconds,
        };
        Apply(entry, 1f);
        Animated.Add(entry);
    }

    /// <summary>Call once per frame from a single driver (see
    /// EmissiveAnimatorDriver) -- NOT per registered instance.</summary>
    public static void Tick()
    {
        if (Animated.Count == 0) return;
        var profile = CityLightingProfile.Active;
        var t = Time.time;
        for (var i = Animated.Count - 1; i >= 0; i--)
        {
            var e = Animated[i];
            if (e.Renderer == null) { Animated.RemoveAt(i); continue; }   // destroyed prop drops out
            var mult = 1f;
            switch (e.Kind)
            {
                case LightBehaviorKind.Flicker:
                {
                    var speed = Mathf.Lerp(profile.FlickerSpeedRange.x, profile.FlickerSpeedRange.y, Frac(e.Seed));
                    var wave = 0.5f + 0.5f * Mathf.Sin(t * speed * Mathf.PI * 2f + e.Seed * 17.3f);
                    mult = Mathf.Lerp(profile.FlickerFloor, 1f, wave * wave);   // squared: mostly-on, occasional real dips
                    break;
                }
                case LightBehaviorKind.Buzz:
                {
                    var flutter = 1f + profile.BuzzAmplitude
                        * Mathf.Sin(t * profile.BuzzFrequencyHz * Mathf.PI * 2f + e.Seed * 31.7f);
                    mult = Mathf.Max(0f, flutter);
                    if (t >= e.NextDropout)
                    {
                        mult = 0.05f;
                        if (t >= e.NextDropout + 0.15f) e.NextDropout = t + profile.BuzzDropoutIntervalSeconds * (0.5f + Frac(e.Seed * 7f));
                    }
                    break;
                }
                case LightBehaviorKind.Chase:
                {
                    var step = Mathf.FloorToInt(t / Mathf.Max(0.02f, profile.ChaseStepSeconds));
                    var lit = ((step % e.SequenceLength) + e.SequenceLength) % e.SequenceLength == e.SequenceIndex;
                    mult = lit ? 1f : profile.ChaseOffFloor;
                    break;
                }
            }
            Apply(e, mult);
        }
    }

    private static void Apply(Entry e, float mult)
    {
        e.Renderer.GetPropertyBlock(e.Block);
        e.Block.SetColor("_EmissionColor", e.BaseEmission * mult * DayNightState.NeonBoost);
        e.Renderer.SetPropertyBlock(e.Block);
    }

    private static float Frac(float v) { return v - Mathf.Floor(v); }
}

/// <summary>The single MonoBehaviour that drives EmissiveAnimator.Tick()
/// -- one Update() call for every animated light in the city, not one
/// per light. RuntimeCityBuilder adds exactly one of these per scene.</summary>
public class EmissiveAnimatorDriver : MonoBehaviour
{
    private void Update() { EmissiveAnimator.Tick(); }
}
