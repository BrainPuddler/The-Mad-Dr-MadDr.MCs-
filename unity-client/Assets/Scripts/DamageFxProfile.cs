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
    [Tooltip("Flat multiplier on every smoke puff's spawn size, growth, and max size (SmokePlume.SpawnPuff's own ResizePct). 2026-08 default: 0.2, per creator direction \"smoke way way smaller. 0.2 resize.\"")]
    [Range(0.02f, 2f)]
    public float SmokeResizePct = 0.2f;

    [Header("Fire")]
    [Tooltip("Flat multiplier on fire's flame-shard puff size/growth AND its point-light range/intensity together -- ONE knob for both, so raising/lowering fire size can't accidentally leave the glow mismatched with the flame mesh it's supposed to be lighting. 2026-08 default: 0.35 (the 0.7 * 0.5 already baked into FirePlume before this profile existed).")]
    [Range(0.02f, 2f)]
    public float FireResizePct = 0.35f;

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
