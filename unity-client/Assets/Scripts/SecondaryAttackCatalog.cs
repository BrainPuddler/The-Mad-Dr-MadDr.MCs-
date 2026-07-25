using UnityEngine;

/// <summary>
/// docs/26 Phase 9: builds every unit's equipped secondary attack in
/// code via `ScriptableObject.CreateInstance&lt;T&gt;()` -- a normal
/// runtime API, NOT an Editor-only operation. docs/26 originally assumed
/// equipping a `SpecialAttackDefinition` required dragging an asset onto
/// a creature in the Editor (unavailable in this environment); that
/// assumption was wrong for CODE-authored definitions specifically, only
/// for hand-authored Inspector content. This catalog is how "every
/// monster and Tank actually carries a secondary attack" gets equipped
/// without any Editor step at all, closing that long-flagged gap.
///
/// One definition instance per kind, built lazily and reused (matching
/// SpecialAttackDefinition's own doc comment: "one object shared by
/// every unit that references it").
/// </summary>
public static class SecondaryAttackCatalog
{
    private static SpecialAttackDefinition _flamethrower;
    private static SpecialAttackDefinition _psionicTractorBeam;
    private static SpecialAttackDefinition _groundStomp;

    /// <summary>Humans (Tank.cs): damage only, no pull/slow/stun --
    /// creator direction, 2026-07: "Humans get flamethrowers (damage
    /// only)." A wider, harder-hitting burst than either primary tank
    /// weapon (WeaponProfile.TankFlamethrower/TankCannon) -- this is the
    /// human faction's SECONDARY attack, equipped on every Tank
    /// regardless of which primary weapon it rolled.</summary>
    public static SpecialAttackDefinition Flamethrower()
    {
        if (_flamethrower == null)
        {
            _flamethrower = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _flamethrower.AbilityName = "Flamethrower Burst";
            _flamethrower.Description = "A wide gout of fire that scorches everything caught in it.";
            _flamethrower.Cooldown = 10f;
            _flamethrower.Range = 14f;
            _flamethrower.AreaOfEffect = 4f;
            _flamethrower.ValidTargets = TargetFilter.Monster;
            _flamethrower.EffectType = SpecialAttackEffectType.Damage;
            _flamethrower.DamageAmount = 30f;
            _flamethrower.MinTargetsInArea = 1;
            // docs/22 "fun first, never annoying": a soft wallet draw,
            // not an ammo gate -- see SpecialAttackDefinition.BloodCost.
            // A wide fuel-burning burst costs more Blood than Bones.
            _flamethrower.BloodCost = 4;
            _flamethrower.BonesCost = 2;
        }
        return _flamethrower;
    }

    /// <summary>Alien-tech-handed monsters: creator direction, 2026-07:
    /// "Aliens get psionic attack, short tractor beam is the same as
    /// web." Literally reuses WebAttackAbility's PullAndConsume delivery
    /// (the mechanic never was web-specific -- see WebAttackAbility.cs's
    /// header) with a shorter range/AoE than a dedicated Web Attack would
    /// tune to, and a psionic name/flavor instead of an arachnid one.</summary>
    public static SpecialAttackDefinition PsionicTractorBeam()
    {
        if (_psionicTractorBeam == null)
        {
            _psionicTractorBeam = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _psionicTractorBeam.AbilityName = "Psionic Tractor Beam";
            _psionicTractorBeam.Description = "A short-range beam of alien mental force that hauls light targets in and pins down anything too heavy to lift.";
            _psionicTractorBeam.Cooldown = 12f;
            _psionicTractorBeam.Range = 10f;
            _psionicTractorBeam.AreaOfEffect = 2f;
            _psionicTractorBeam.ValidTargets = TargetFilter.All;
            _psionicTractorBeam.EffectType = SpecialAttackEffectType.PullAndConsume;
            // psionic energy over physical material -- lighter Bones draw
            // than the other two.
            _psionicTractorBeam.BloodCost = 3;
            _psionicTractorBeam.BonesCost = 1;
        }
        return _psionicTractorBeam;
    }

    /// <summary>The Mad Doctor's default creature (every monster whose
    /// hand family isn't alien tech): creator direction, 2026-07: "mad dr
    /// Ground stomp stun effect." Self-centered -- resolves at the
    /// caster's own feet the instant it triggers, no projectile travel
    /// (see MonsterAgent.TickSpecialAttack's Stun-type dispatch).</summary>
    public static SpecialAttackDefinition GroundStomp()
    {
        if (_groundStomp == null)
        {
            _groundStomp = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _groundStomp.AbilityName = "Ground Stomp";
            _groundStomp.Description = "A seismic slam that freezes anything standing too close.";
            _groundStomp.Cooldown = 14f;
            _groundStomp.Range = 0f;   // self-centered -- no approach distance to close
            _groundStomp.AreaOfEffect = 6f;
            _groundStomp.ValidTargets = TargetFilter.All;
            _groundStomp.EffectType = SpecialAttackEffectType.Stun;
            _groundStomp.StunDuration = 2f;
            // joint/frame strain over blood loss -- heavier Bones draw
            // than the other two.
            _groundStomp.BloodCost = 2;
            _groundStomp.BonesCost = 4;
        }
        return _groundStomp;
    }

    /// <summary>docs/26 Phase 9: which secondary attack (if any) a MONSTER
    /// is equipped with, derived purely from its existing genome-derived
    /// hand family -- no new gene. Mirrors `Combat.WeaponFor`'s own family
    /// switch (roster-client): the same hand families the creator called
    /// out as alien tech ("aliens laser and photonic blasters") read as
    /// alien-flavored here too, so a monster's weapon and its secondary
    /// attack are always thematically consistent for free. Everything
    /// else -- organic/biotech hands -- gets the Mad Doctor's default,
    /// Ground Stomp. Extensible: a future hand family with its own
    /// signature attack is one more case here, no other wiring
    /// changes.</summary>
    public static SpecialAttackDefinition ForMonster(string handFamily)
    {
        switch (handFamily)
        {
            case "laser_array":
            case "photon_blaster":
            case "plasma_lance":
                return PsionicTractorBeam();
            default:
                return GroundStomp();
        }
    }
}
