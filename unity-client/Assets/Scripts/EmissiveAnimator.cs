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
    /// dark outside that window, lit inside it, a hard instant flip at
    /// each edge (2026-08 creator direction: "Always like a light switch
    /// NOT a dimmer" -- no fade, no per-instance wobble while lit; used
    /// to share Flicker's sine wobble, removed). A held-out fraction
    /// never gets a bedtime at all (lit the whole night through) -- the
    /// "not all lights go off" case. Distinct from Flicker: Flicker
    /// wobbles brightness within an already-on/off state driven by the
    /// GLOBAL day/night boost; Window adds its OWN per-instance on/off
    /// schedule on top of that, since nightAmount itself now holds
    /// perfectly flat through the whole night (see
    /// LumenCycleController.ComputeNightIntensity) and can't tell "just
    /// got dark" apart from "3am" the way a per-window schedule
    /// needs to.</summary>
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
        public float ActivityThreshold;   // Window only (non-AlwaysOn): this window only shows lit while DayNightState.LightActivity is at or above this
    }

    // Window occupancy timing, as fractions of the FULL day/night cycle
    // (DayNightState.CycleProgress: 0 at Dawn's start, 1 at Night's end).
    // Ticks, for reference (LumenClock, 10 ticks/s): Dawn [0,300)
    // Day [300,1200) Dusk [1200,1500) Night [1500,2400). The global
    // "lights on" ramp (LumenCycleController.LightsOnStartTick) sits at
    // tick 1000 (the dial's 5:00).
    //
    // 2026-08 creator direction: "I want to see more window lights at
    // night [that turn on] earlier before 5pm." OnRangeStart used to be
    // 0.375 (tick 900, only 100 ticks -- 10 sim-seconds -- before the
    // ramp), so only the earliest sliver of arrivals could beat 5pm.
    // Pulled back to 0.2 (tick 480, solidly mid-Day) so a much larger
    // share of windows have already "arrived home" (and, since
    // dayNeonBoost/dayIntensityFraction now keep lights faintly visible
    // through the day too, actually READ as lit) well before the evening
    // ramp, not just right at it.
    private const float OnRangeStart = 0.2f;     // 480 ticks -- mid-Day: earliest arrivals
    private const float OnRangeEnd = 0.75f;      // 1800 ticks -- 1/3 into Night: latest arrivals
    private const float OffRangeStart = 0.75f;   // 1800 ticks -- earliest bedtimes
    private const float OffRangeEnd = 0.98f;     // 2352 ticks -- latest bedtimes, just shy of Dawn
    private const float AlwaysOnProbability = 0.15f;
    // 2026-08 creator direction: "Always like a light switch NOT a
    // dimmer" + "The window lights should NEVER flash on and off in
    // short intervals." Used to smoothstep the on/off/activity gates
    // across a short band (a ~2.4s fade -- "a person flipping a switch
    // reads as a beat of motion, not a single-frame pop") and wobble
    // lit windows with a sine flicker on top. Both removed: every gate
    // below is now a hard, instant step, and there is no more per-window
    // wobble while lit -- see OccupancyGate and the Window case above.
    // The arrival/bedtime/activity-threshold SCHEDULE itself (still
    // read against seed-derived per-window OnCycleFrac/OffCycleFrac/
    // ActivityThreshold below) is exactly what makes the switch flip at
    // a different moment for every window rather than all at once --
    // that's the "motivated as a human being" part this direction asked
    // to keep, not remove.

    private static readonly List<Entry> Animated = new List<Entry>();

    /// <summary>2026-08 (creator direction: "add a toggle window lights
    /// on off"). Default true (the naturalistic schedule is the
    /// standing behavior). When false, every `Window`-kind registration
    /// reverts to the SAME plain, uniform day/night-driven brightness
    /// every other `Steady` emissive prop in the city already uses
    /// (see <see cref="OccupancyGate"/>) -- not total darkness, which
    /// would read as broken city lighting rather than an intentional
    /// preference toggle; this only turns off the PER-WINDOW randomized
    /// "someone's home"/"someone went to bed" variation, not the city's
    /// own night lighting as a whole.</summary>
    public static bool WindowScheduleEnabled = true;

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
            // A fourth decorrelated draw on the same seed -- this
            // window's own "how much city-wide activity does it take for
            // someone to be home and awake here" threshold, uniform in
            // [0,1). Compared live against DayNightState.LightActivity in
            // OccupancyGate, so a LOW-threshold window is one of the
            // "always seems occupied" ones (lit even at the 0.5 daytime
            // floor), a HIGH-threshold window only lights up during
            // peak evening activity and is among the first to go dark as
            // LightActivity decays late in the night -- different
            // windows crossing their own threshold at different activity
            // levels is what makes the city's lights turn on/off
            // staggered across the whole building stock rather than all
            // at once, on top of the per-window arrival/bedtime spread
            // OnCycleFrac/OffCycleFrac already give.
            entry.ActivityThreshold = Frac(seed * 113.7f + 19f);
        }
        Apply(entry, 1f);
        Animated.Add(entry);
    }

    /// <summary>Call once per frame from a single driver (see
    /// EmissiveAnimatorDriver) -- NOT per registered instance.
    ///
    /// 2026-08 perf (Tier 0 of the graphics-upgrade plan, docs/12): this
    /// class's own doc comment above assumed "a few hundred" registrations
    /// at "trivially cheap" cost -- BigCity-scale window counts land in the
    /// tens of thousands (docs/30's finding), all walked unconditionally
    /// every frame regardless of whether the camera is anywhere near them.
    /// Entries farther than <see cref="CityLightingProfile.
    /// EmissiveTickRangeMeters"/> from the main camera are skipped entirely
    /// -- they keep whatever MaterialPropertyBlock override they last had
    /// (visually inert either way at that distance) instead of paying the
    /// trig + SetPropertyBlock cost for something nobody can see.</summary>
    public static void Tick()
    {
        if (Animated.Count == 0) return;
        var profile = CityLightingProfile.Active;
        var t = Time.time;
        var cam = Camera.main;
        var hasCam = cam != null;
        var camPos = hasCam ? cam.transform.position : default(Vector3);
        var maxRangeSqr = profile.EmissiveTickRangeMeters * profile.EmissiveTickRangeMeters;
        for (var i = Animated.Count - 1; i >= 0; i--)
        {
            var e = Animated[i];
            if (e.Renderer == null) { Animated.RemoveAt(i); continue; }   // destroyed prop drops out
            if (hasCam && (e.Renderer.transform.position - camPos).sqrMagnitude > maxRangeSqr) continue;
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
                    // 2026-08: no more wobble-while-lit -- a window is
                    // either fully on or fully off, gated by its OWN
                    // randomized arrival/bedtime schedule -- see
                    // OccupancyGate. Reads DayNightState.CycleProgress
                    // rather than NeonBoost/NightAmount specifically
                    // because those hold flat through the whole night now
                    // (the "hold for the duration of the night" fix) and
                    // can't distinguish "just got dark" from "3am" the way
                    // a bedtime schedule needs to.
                    mult = OccupancyGate(e, DayNightState.CycleProgress);
                    break;
                }
            }
            Apply(e, mult);
        }
    }

    /// <summary>1 while this window's cycle-relative "on" state is
    /// active, 0 otherwise -- a hard, instant flip at each edge (2026-08
    /// creator direction: "Always like a light switch NOT a dimmer"; used
    /// to be a short smoothstep fade instead, see this file's history).
    /// AlwaysOn skips the off half AND the activity gate below entirely
    /// -- "not all lights go off," a fixed always-occupied subset
    /// unaffected by how quiet the city gets.</summary>
    private static float OccupancyGate(Entry e, float cycleProgress)
    {
        if (!WindowScheduleEnabled || e.AlwaysOn) return 1f;
        var onGate = cycleProgress >= e.OnCycleFrac ? 1f : 0f;
        var offGate = cycleProgress < e.OffCycleFrac ? 1f : 0f;
        // 2026-08: on top of this window's OWN arrival/bedtime span above,
        // it also needs city-wide LightActivity to have climbed at least
        // to its own ActivityThreshold -- fewer windows clear a HIGH
        // threshold during the low-activity daytime/late-night floors,
        // more do once activity ramps to its evening peak. Different
        // windows have different thresholds, so this produces a staggered
        // spread of on/off transitions across the building stock as
        // activity rises and falls, not a single city-wide flip -- each
        // individual window's own flip is still instant, only WHICH
        // instant differs window to window.
        var activityGate = DayNightState.LightActivity >= e.ActivityThreshold ? 1f : 0f;
        return Mathf.Min(onGate, Mathf.Min(offGate, activityGate));
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
