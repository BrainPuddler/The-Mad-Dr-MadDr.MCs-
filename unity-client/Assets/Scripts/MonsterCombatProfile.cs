using UnityEngine;

/// <summary>
/// 2026-08 (creator direction: "give me an inspector setting for that
/// too" -- the mob-mentality bonus below): Inspector-editable combat/AI
/// behavior knobs, same "gather into ONE ScriptableObject instead of a
/// hardcoded constant" pattern <see cref="DamageFxProfile"/> and <see
/// cref="CityLightingProfile"/> already establish for their own domains
/// (visual FX, lighting) -- this is the first entry in the equivalent
/// domain for monster combat AI behavior. Create one via Assets > Create
/// > MadDr > Monster Combat Profile, assign it to `RuntimeCityBuilder`'s
/// `Monster Combat Profile` field. Unassigned, `MonsterAgent` reads
/// `Default`'s own safe value instead -- same "unassigned = unchanged
/// behavior" contract every other profile asset in this project uses.
/// </summary>
[CreateAssetMenu(fileName = "MonsterCombatProfile", menuName = "MadDr/Monster Combat Profile")]
public class MonsterCombatProfile : ScriptableObject
{
    [Header("Attack hierarchy")]
    [Tooltip("2026-08 (creator direction: \"add a roll for increased probability for all units, mob mentality. It shouldn't happen all the time but if one starts it the others follow\"): when a monster is deciding whether to collaterally attack a building blocking its shot (MonsterAgent's own genome-fury aggression roll -- see TickAttack's attack-hierarchy doc comment), this is added ON TOP of that roll's effective aggression IF another monster within MobMentalityRadius is already mid collateral-attack. 0 = no mob effect at all (pure individual aggression); higher values make a mob more likely to pile on once ONE member starts, without ever guaranteeing it -- the roll itself can still fail. 2026-08 default: 0.3.")]
    [Range(0f, 1f)]
    public float MobMentalityBonus = 0.3f;

    [Tooltip("How close (metres) another monster's own ongoing collateral attack needs to be to grant this monster's roll the MobMentalityBonus above. 2026-08 default: 30 -- close enough to read as \"nearby pack,\" not \"anywhere on the map.\"")]
    [Range(5f, 100f)]
    public float MobMentalityRadius = 30f;

    private static MonsterCombatProfile _default;

    /// <summary>Safe, in-code fallback so any reader that hasn't assigned
    /// a real asset still behaves sanely -- never null-refs. Same
    /// lazy-CreateInstance idiom every other profile asset in this
    /// project already uses.</summary>
    public static MonsterCombatProfile Default
    {
        get
        {
            if (_default == null) _default = CreateInstance<MonsterCombatProfile>();
            return _default;
        }
    }

    private static MonsterCombatProfile _active;

    /// <summary>The profile currently in effect, set once at city-build
    /// time by RuntimeCityBuilder -- same loose static-holder idiom
    /// DamageFxProfile.Active/CityLightingProfile.Active already use.</summary>
    public static MonsterCombatProfile Active
    {
        get { return _active != null ? _active : Default; }
        set { _active = value; }
    }
}
