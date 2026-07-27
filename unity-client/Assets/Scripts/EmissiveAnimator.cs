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

    /// <summary>Occupied-window behavior (2026-07 creator direction):
    /// "the building can turn on randomly approaching night time, as if
    /// real humans were in the room and realize it's getting too dark...
    /// the same goes for late at night -- imagine people going to bed
    /// and shutting off their house light, this can vary greatly, but
    /// not all lights go off." Each registration gets its OWN randomized
    /// (deterministic from `seed`) "someone gets home" time somewhere
    /// across late day/dusk/early night, and most get their own randomized
    /// "someone goes to bed" time somewhere in the back half of night --
    /// dark outside that window, lit (with the same Flicker-style wobble)
    /// inside it. A held-out fraction never gets a bedtime at all (lit
    /// the whole night through) -- the "not all lights go off" case.
    /// Distinct from Flicker: Flicker just wobbles brightness within an
    /// already-on/off state driven by the GLOBAL day/night boost; Window
    /// adds its OWN per-instance on/off schedule on top of that, since
    /// nightAmount itself now holds perfectly flat through the whole
    /// night (see LumenCycleController.ComputeNightIntensity) and can't
    /// tell "just got dark" apart from "3am" the way a per-window
    /// schedule needs to.</summary>
    Window,
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
        public float OnCycleFrac;   // Window only: this window's "someone's home" time (0..1 of the full cycle)
        public float OffCycleFrac;  // Window only: this window's "lights out" time (0..1 of the full cycle)
        public bool AlwaysOn;       // Window only: skips OffCycleFrac entirely -- "not all lights go off"
    }

    // Window occupancy timing, as fractions of the FULL day/night cycle
    // (DayNightState.CycleProgress: 0 at Dawn's start, 1 at Night's end).
    // Ticks, for reference (LumenClock, 10 ticks/s): Dawn [0,300)
    // Day [300,1200) Dusk [1200,1500) Night [1500,2400).
    private const float OnRangeStart = 0.375f;   // 900 ticks -- 3/4 through Day: earliest arrivals
    private const float OnRangeEnd = 0.75f;      // 1800 ticks -- 1/3 into Night: latest arrivals
    private const float OffRangeStart = 0.75f;   // 1800 ticks -- earliest bedtimes
    private const float OffRangeEnd = 0.98f;     // 2352 ticks -- latest bedtimes, just shy of Dawn
    private const float AlwaysOnProbability = 0.15f;
    // How long the on/off transition itself takes, as a fraction of the
    // cycle -- a hard instant snap would read as a glitch, not a person
    // flipping a switch. ~0.01 of a 240s cycle is ~2.4s.
    private const float OccupancyTransitionFrac = 0.01f;

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
        if (kind == LightBehaviorKind.Window)
        {
            // Deterministic from `seed` (a hash of the window's own hex/
            // floor/slot, per BuildingDresser) rather than UnityEngine.
            // Random, matching this codebase's "same seed always
            // furnishes the same city" ethos everywhere else -- three
            // DIFFERENT multipliers on the same seed decorrelate the
            // three draws (same trick Frac(seed * K) already uses for
            // Flicker/Buzz elsewhere in this file).
            entry.OnCycleFrac = Mathf.Lerp(OnRangeStart, OnRangeEnd, Frac(seed * 41.3f));
            entry.OffCycleFrac = Mathf.Lerp(OffRangeStart, OffRangeEnd, Frac(seed * 59.7f + 3f));
            entry.AlwaysOn = Frac(seed * 91.1f + 7f) < AlwaysOnProbability;
        }
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
                case LightBehaviorKind.Window:
                {
                    // Same wobble-while-lit as Flicker (a window that's
                    // "occupied" still isn't perfectly steady), gated by
                    // this window's OWN randomized on/off schedule on top
                    // -- see OccupancyGate. Reads DayNightState.CycleProgress
                    // rather than NeonBoost/NightAmount specifically
                    // because those hold flat through the whole night now
                    // (the "hold for the duration of the night" fix) and
                    // can't distinguish "just got dark" from "3am" the way
                    // a bedtime schedule needs to.
                    var speed = Mathf.Lerp(profile.FlickerSpeedRange.x, profile.FlickerSpeedRange.y, Frac(e.Seed));
                    var wave = 0.5f + 0.5f * Mathf.Sin(t * speed * Mathf.PI * 2f + e.Seed * 17.3f);
                    var wobble = Mathf.Lerp(profile.FlickerFloor, 1f, wave * wave);
                    mult = wobble * OccupancyGate(e, DayNightState.CycleProgress);
                    break;
                }
            }
            Apply(e, mult);
        }
    }

    /// <summary>1 while this window's cycle-relative "on" state is
    /// active, 0 otherwise, with a short smoothstep transition at each
    /// edge instead of an instant snap (a person flipping a switch reads
    /// as a beat of motion, not a single-frame pop). AlwaysOn skips the
    /// off half entirely -- "not all lights go off."</summary>
    private static float OccupancyGate(Entry e, float cycleProgress)
    {
        if (e.AlwaysOn) return 1f;
        var onGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            e.OnCycleFrac - OccupancyTransitionFrac, e.OnCycleFrac + OccupancyTransitionFrac, cycleProgress));
        var offGate = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            e.OffCycleFrac - OccupancyTransitionFrac, e.OffCycleFrac + OccupancyTransitionFrac, cycleProgress));
        return Mathf.Min(onGate, offGate);
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
