using UnityEngine;

/// <summary>
/// A Special Attack's authored data -- one ScriptableObject asset per
/// ability (Web Attack, etc.), shared read-only across every unit that
/// equips it. Per-unit runtime state (the actual cooldown timer) lives
/// separately in <see cref="SpecialAttackInstance"/>, never here -- an
/// asset is one object shared by every unit that references it, so it
/// can never hold a live timer without every equipper sharing one clock.
///
/// Deliberately a ScriptableObject, not a plain C# class like
/// WeaponProfile: this is designer-tunable content (cooldown/range/AoE
/// numbers a person will want to iterate on in the Inspector without a
/// recompile), where WeaponProfile is a value computed FROM the genome at
/// spawn time, not hand-authored. First ScriptableObject in this project
/// -- see docs/12, 2026-07, for why that tradeoff was made deliberately
/// here rather than matching WeaponProfile's plain-class convention.
/// </summary>
[CreateAssetMenu(fileName = "NewSpecialAttack", menuName = "MadDr/Special Attack")]
public class SpecialAttackDefinition : ScriptableObject
{
    [Header("Identity")]
    public string AbilityName = "Special Attack";
    [TextArea] public string Description;

    [Header("Timing & range")]
    [Tooltip("Seconds between uses -- ticked per-instance (SpecialAttackInstance), the same Time.deltaTime decrement-and-reload idiom UnitCombat._cooldown already uses. Not a global or shared-per-type cooldown: each unit that equips this ability tracks its own timer.")]
    public float Cooldown = 12f;
    [Tooltip("Meters, matching UnitCombat.Weapon.Range's convention -- combat ranges in this project are world-space meters, not hex counts (hex-count radii are the city-generation/dressing convention, e.g. landmark auras).")]
    public float Range = 24f;
    [Tooltip("Meters. 0 = single-target, no area effect.")]
    public float AreaOfEffect = 0f;

    [Header("Targeting")]
    public TargetFilter ValidTargets = TargetFilter.All;
    public SpecialAttackEffectType EffectType = SpecialAttackEffectType.Damage;

    [Header("Effect magnitude")]
    [Tooltip("Damage effect type only.")]
    public float DamageAmount = 20f;
    [Tooltip("Stun effect type only -- seconds a caught target is frozen (can't move or fire).")]
    public float StunDuration = 2f;

    [Header("AI use requirements")]
    [Tooltip("The AI won't use this ability unless at least this many valid, weighted targets are within the area of effect at the chosen impact point.")]
    public int MinTargetsInArea = 1;
    [Tooltip("If true, the AI requires an unobstructed line to the impact point before using this ability.")]
    public bool RequiresLineOfSight = true;

    [Header("Animation / event hooks")]
    [Tooltip("Animator trigger name fired on cast, if/when a real Animator exists on the body -- currently unused by MonsterBody's procedural rig, reserved for when one does.")]
    public string AnimationTrigger;

    [Header("VFX / SFX hooks")]
    public GameObject ProjectilePrefab;
    public GameObject ImpactVfxPrefab;
    public AudioClip CastSfx;
    public AudioClip ImpactSfx;
}

/// <summary>Which unit categories a special attack can affect. [Flags] so
/// an ability can target e.g. "Human | Monster" while excluding
/// Structure. Deliberately NOT the same thing as UnitCombat.Faction
/// (a free-form string) -- this is a closed, ability-authoring-time
/// enum, not a runtime faction identity.</summary>
[System.Flags]
public enum TargetFilter
{
    None = 0,
    Human = 1 << 0,
    Monster = 1 << 1,
    Structure = 1 << 2,
    Ground = 1 << 3,
    Air = 1 << 4,
    All = Human | Monster | Structure | Ground | Air,
}

/// <summary>What a special attack DOES once it resolves against a valid
/// target. Extensible -- add a case here, then a matching branch wherever
/// an ability resolver switches on EffectType: `WebAttackAbility` handles
/// `PullAndConsume` (its heavy-target sub-branch also applies
/// `SlowStatus` internally as a per-target classification, not a
/// separate effect type of its own); `SpecialAttackResolver` handles
/// `Damage` and `Stun` (docs/26 Phase 9) as instant, non-projectile AoE
/// resolutions. Adding a 5th kind is: one enum value here, one case in
/// `SpecialAttackResolver.ApplyEffect`, and (if it needs new tunable
/// numbers) a field on `SpecialAttackDefinition` above -- no new
/// ability class required unless the delivery mechanism itself differs
/// (projectile vs instant).</summary>
public enum SpecialAttackEffectType
{
    Damage,
    PullAndConsume,
    SlowStatus,
    Stun,
}
