/// <summary>
/// docs/23 Phase 10.2: the current day/night blend (0 = full day, 1 =
/// full night), published once per frame by LumenCycleController so
/// unrelated systems (StreetLampLightBudget today; anything else that
/// wants to fade in step with the Lumen cycle later) can read it without
/// a direct reference to the controller -- same loose-coupling idiom as
/// NeonRegistry's static boost value.
/// </summary>
public static class DayNightState
{
    public static float NightAmount;
}
