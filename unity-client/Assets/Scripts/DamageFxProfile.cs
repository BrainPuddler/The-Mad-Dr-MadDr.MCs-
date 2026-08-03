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

    [Tooltip("Sideways drift speed (world units/sec) each smoke puff picks up on top of its own upward rise, giving the whole plume a coherent wind-blown diagonal lean instead of climbing straight up. Read ONCE per building when its SmokePlume is created (like a build-time value, not live per-puff like the size knobs above) -- an Inspector change takes effect on the NEXT building that catches fire, not an already-burning one. 2026-08 default: 5 (up from a prior 1.8, then 0.55 before that -- creator report: \"the camera is above the smoke, the smoke must travel far to get the correct angle... as if in a very strong fast wind\" -- an RTS camera looking mostly straight down reads horizontal (X/Z) drift far more clearly than vertical rise, so this needs to be dramatic, not subtle, to register at all from that angle).")]
    [Range(0f, 12f)]
    public float SmokeWindSpeed = 5f;

    /// <summary>North/South/East/West only -- deliberately NOT a free
    /// angle. 2026-08 (creator report: smoke needs to read as blowing "at
    /// the correct angle N, S, E or W" from an overhead RTS camera): a
    /// per-building random angle (the prior approach) scatters plumes in
    /// every direction, which from near-directly-overhead reads as noisy
    /// scribbling rather than "there is wind." Locking every building's
    /// plume to the SAME one of 4 compass directions reads as one
    /// coherent city-wide wind instead. North = world +Z, East = +X (the
    /// same north-up convention <see cref="Minimap"/>'s default
    /// orientation already uses).</summary>
    public enum CompassDirection { North, East, South, West }

    [Tooltip("The one direction EVERY building's smoke blows -- see CompassDirection's own doc comment for why this is locked to N/S/E/W instead of a free angle.")]
    public CompassDirection SmokeWindDirection = CompassDirection.North;

    /// <summary>Degrees clockwise from North, matching the
    /// Mathf.Sin(angle)*x/Mathf.Cos(angle)*z convention DamageFx.AttachSmoke
    /// and SmokePlume.Init already use for every other per-building angle
    /// (0 rad = pure +Z/North; 90 deg = pure +X/East).</summary>
    public float SmokeWindAngleRadians
    {
        get
        {
            switch (SmokeWindDirection)
            {
                case CompassDirection.East: return 90f * Mathf.Deg2Rad;
                case CompassDirection.South: return 180f * Mathf.Deg2Rad;
                case CompassDirection.West: return 270f * Mathf.Deg2Rad;
                default: return 0f; // North
            }
        }
    }

    [Header("Fire")]
    [Tooltip("Flat multiplier on fire's flame-shard puff size/growth AND its point-light range/intensity together -- ONE knob for both, so raising/lowering fire size can't accidentally leave the glow mismatched with the flame mesh it's supposed to be lighting. 2026-08 default: 1.0 -- REVERTED from 0.35 (0.7 * 0.5, two separate \"a lot smaller\" passes stacked on top of each other without re-checking fire's OWN visibility independently of smoke's) after creator report \"I still do not see the fire\": at 0.35 the flame-shard puff was roughly 0.1 world units across, well under a tenth the size of a smoke puff from the SAME era -- likely below the actual visibility threshold, not a matter of taste. 1.0 restores the size from the FIRST shard-mesh pass (\"small, angular, faceted\" per the original reference images), the last point in this history the size itself was actually confirmed acceptable rather than immediately re-shrunk again.")]
    [Range(0.02f, 2f)]
    public float FireResizePct = 1.0f;

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
