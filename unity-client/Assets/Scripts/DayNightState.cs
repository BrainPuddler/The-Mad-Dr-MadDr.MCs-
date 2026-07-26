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
}
