using UnityEngine;

/// <summary>
/// docs/21 batch 2 (damage feedback): the fire/smoke SIZE knobs, gathered
/// into ONE Inspector-editable asset instead of hardcoded constants deep
/// inside DamageFx.cs -- same "give me an inspector setting... or a
/// scriptable object I can change" answer <see cref="CityLightingProfile"/>
/// already gave for lighting (this session's own repeated smoke/fire
/// size back-and-forth -- 0.7 resize, then a 1.6x up-size that had to be
/// reverted, then 0.2 -- is exactly the kind of churn an Inspector knob
/// is for). Create one via Assets > Create > MadDr > Damage Fx Profile,
/// drag it onto RuntimeCityBuilder's `Damage Fx Profile` field, and
/// retune without touching code or waiting on a chat round-trip.
///
/// Nothing REQUIRES an assigned asset. <see cref="DamageFx"/> reads
/// <see cref="Active"/>, which falls back to <see cref="Default"/>'s own
/// safe values (the numbers already baked into DamageFx.cs at the time
/// this profile was introduced) when nothing is assigned -- same
/// "unassigned = unchanged behavior" contract CityLightingProfile uses.
///
/// **Live in Play mode:** unlike CityLightingProfile (whose values only
/// take effect at city-BUILD time), DamageFx reads these fields fresh
/// every time a puff spawns -- so dragging a slider while a building is
/// already on fire changes the NEXT puff's size immediately, without a
/// rebuild.
/// </summary>
[CreateAssetMenu(fileName = "DamageFxProfile", menuName = "MadDr/Damage Fx Profile")]
public class DamageFxProfile : ScriptableObject
{
    [Header("Smoke")]
    [Tooltip("Flat multiplier on every smoke puff's OWN starting size (SmokePlume.SpawnPuff's own ResizePct). 2026-08 default: 0.2, per creator direction \"smoke way way smaller. 0.2 resize.\" 2026-08 follow-up fix: this used to only affect the ADDITIONAL growth amount, not the puff's actual starting scale (SmokePuff._baseScale was a fixed 0.8 constant no smoke code path ever touched) -- now genuinely drives the start size, see SmokeGrowthMultiplier below for the (separate, now-capped) growth-over-life knob.")]
    [Range(0.02f, 2f)]
    public float SmokeResizePct = 0.2f;

    [Tooltip("How large a puff grows by the END of its life, as a multiplier on its OWN starting size (SmokeResizePct above) -- 1 means it never grows at all, 2 means it doubles. 2026-08 (creator direction: \"growth in size should never exceed 2 times the size of the original\"): hard-clamped to [1, 2] in DamageFx.cs regardless of what's typed here, so this genuinely can't exceed the ceiling the creator asked for. Replaces the prior growth formula, which added a SEPARATE fixed amount on top of a puff's DISCONNECTED base scale and could reach roughly 9-10x the puff's actual starting size.")]
    [Range(1f, 2f)]
    public float SmokeGrowthMultiplier = 2f;

    [Tooltip("Vertical rise speed (world units/sec) each smoke puff climbs at, before the sideways wind drift below is added on top. 2026-08 default: 1.4 (unchanged from the prior hardcoded constant every puff kind shared) -- now Inspector-tunable per creator direction \"give me inspector setting to alter drift.\"")]
    [Range(0f, 6f)]
    public float SmokeRiseSpeed = 1.4f;

    [Tooltip("Outward drift speed (world units/sec) each smoke puff picks up on top of its own upward rise, carrying it away from the building it spawned on. Read ONCE per building when its SmokePlume is created (like a build-time value, not live per-puff like the size knobs above) -- an Inspector change takes effect on the NEXT building that catches fire, not an already-burning one. 2026-08 default: 5.\n\n2026-08 follow-up (creator direction: \"smoke may spawn from any place on a building BUT IT MUST start on the building and must travel out radially from the building so it is always seen\"): this used to be a SHARED compass direction (N/S/E/W) every building's plume leaned the same way -- REMOVED. Each plume now spawns at its own random point around the building and drifts straight outward (away from the building's own center) at this speed instead, so it can never drift back across/behind the structure it just escaped, regardless of where on the building it started. See DamageFx.AttachSmoke's own doc comment for the full writeup.")]
    [Range(0f, 12f)]
    public float SmokeWindSpeed = 5f;

    [Header("Fire")]
    [Tooltip("Multiplier on fire's point-light range/intensity only. 2026-08 history: 1.0 -> 3.0 -> 6.0, each bump after \"I STILL CAN'T SEE THE FIRE\" survived the previous one -- but the flame-shard MESH itself stayed unseen through all three, so this knob alone was never proven to be the actual lever. 2026-08 follow-up (creator direction: \"still no visible fire... for now make it the size of the smoke\"): the flame-shard puff's own size no longer reads this field at all -- FirePlume.SpawnPuff now sizes it off SmokeResizePct instead (smoke IS confirmed visible at its own size), as a diagnostic step to rule size in or out for good. This field still only controls the glow light.")]
    [Range(0.02f, 10f)]
    public float FireResizePct = 6.0f;

    [Tooltip("Flat multiplier on the flame-shard MESH's own size (FirePlume.SpawnPuff), on top of the SmokeResizePct base it's borrowed from -- a fire-only knob, doesn't touch smoke. 2026-08 default: 1.18, per creator direction \"Increase the size of fire by 18%\" once fire's placement (on-building) and visibility were both confirmed correct -- this is a size-only bump, not another visibility diagnostic.")]
    [Range(0.5f, 4f)]
    public float FireSizeBoostPct = 1.18f;

    [Tooltip("Multiplier on how fast NEW fire points ignite (FireCluster.CurrentSimTickInterval) -- how many points a burning building has, not how big any one of them is. 1.0 = unchanged default pacing; above 1 ignites points faster, below 1 slower. Separate from (and multiplies on TOP of) the existing urgency/hit-rate speedup, which already reacts to how close a building is to destruction and how many attackers are hitting it -- this is a flat creator-facing override on top of that automatic behavior. 2026-08 default: 1.0, per creator direction \"give me an inspector setting for spawn rate of fires.\"")]
    [Range(0.25f, 4f)]
    public float FireSpawnRateMultiplier = 1f;

    private static DamageFxProfile _default;

    /// <summary>Safe, in-code fallback so any reader that hasn't assigned
    /// a real asset still behaves sanely -- never null-refs. Same
    /// lazy-CreateInstance idiom as CityLightingProfile.Default.</summary>
    public static DamageFxProfile Default
    {
        get
        {
            if (_default == null) _default = CreateInstance<DamageFxProfile>();
            return _default;
        }
    }

    private static DamageFxProfile _active;

    /// <summary>The profile currently in effect, set once at city-build
    /// time by RuntimeCityBuilder -- same loose static-holder idiom
    /// CityLightingProfile.Active/NeonRegistry/StreetLampRegistry already
    /// use so DamageFx's static methods (no MonoBehaviour instance to
    /// hang an Inspector field off of) can read tunable values.</summary>
    public static DamageFxProfile Active
    {
        get { return _active != null ? _active : Default; }
        set { _active = value; }
    }
}
