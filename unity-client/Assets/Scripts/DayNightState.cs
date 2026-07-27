/// <summary>
/// docs/23 Phase 10.2 / docs/28: the current day/night blend, published
/// once per frame by LumenCycleController so unrelated systems
/// (DynamicLightBudget, EmissiveAnimator) can read it without a direct
/// reference to the controller -- same loose-coupling idiom as
/// NeonRegistry's static boost value.
/// </summary>
public static class DayNightState
{
    /// <summary>0 (full day) .. 1 (full night).</summary>
    public static float NightAmount;

    /// <summary>The SAME value NeonRegistry.SetBoost was just called
    /// with -- docs/28: EmissiveAnimator-registered materials (windows,
    /// neon, marquee) need this too, since a MaterialPropertyBlock
    /// override on a renderer takes priority over the shared Material's
    /// own NeonRegistry-driven color for that renderer. Without
    /// re-applying it here, an animated light would ignore day/night
    /// entirely and stay at a fixed brightness around the clock.</summary>
    public static float NeonBoost = 1f;

    /// <summary>0 (start of Dawn) .. 1 (end of Night), wrapping every
    /// LumenClock.CycleTicks. 2026-07: added for per-window occupancy
    /// scheduling (EmissiveAnimator's Window behavior kind) -- NightAmount
    /// deliberately holds FLAT through the whole night now (the "hold for
    /// the duration of the night" fix), so it can't tell a per-instance
    /// "this window's residents go to bed at some random point late at
    /// night" apart from any other point in the night. This raw position
    /// can.</summary>
    public static float CycleProgress;
}
