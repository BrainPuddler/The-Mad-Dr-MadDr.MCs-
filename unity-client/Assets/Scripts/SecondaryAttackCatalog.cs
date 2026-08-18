using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/26 Phase 9: builds every unit's equipped secondary attack(s) in
/// code via `ScriptableObject.CreateInstance&lt;T&gt;()` -- a normal
/// runtime API, NOT an Editor-only operation. docs/26 originally assumed
/// equipping a `SpecialAttackDefinition` required dragging an asset onto
/// a creature in the Editor (unavailable in this environment); that
/// assumption was wrong for CODE-authored definitions specifically, only
/// for hand-authored Inspector content. This catalog is how "every
/// monster and Tank actually carries secondary attacks" gets equipped
/// without any Editor step at all, closing that long-flagged gap.
///
/// One definition instance per kind, built lazily and reused (matching
/// SpecialAttackDefinition's own doc comment: "one object shared by
/// every unit that references it").
///
/// 2026-08 follow-up (creator direction, verbatim: "Expand Secondary
/// Attack Variety Across Races... We currently have too much repetition
/// around SECONDARY ATTACK behavior... I want to expand the secondary-
/// attack system so that each race has at least 4-5 additional
/// secondary abilities, rather than every race repeatedly relying on
/// the same Ground Stomp-style interaction"): the four `ForXxx` methods
/// at the bottom of this file each return a POOL (`IReadOnlyList`) of
/// 5-6 abilities instead of the single one every race used to get --
/// `UnitCombat.Abilities` was ALREADY a `List&lt;SpecialAttackInstance&gt;`
/// (docs/26 Phase 8 built `EvaluateBestAbility` to compete among
/// however many are equipped from day one; only the CATALOG side ever
/// equipped just one), so this is genuinely an extension of the
/// existing architecture, not a rebuild -- confirmed by inspection
/// before writing a single line here, per the brief's own "inspect
/// before coding" instruction. `MonsterAgent.Init`/`Tank.Init` now loop
/// over a pool and add every entry, instead of adding one.
///
/// "The Lab Gnome" in the creator's own brief is this file's existing
/// `GroundStomp()` doc comment's own "the Mad Doctor's default
/// creature" -- confirmed by the verbatim match between the brief's
/// "mad dr Ground stomp stun effect" and this file's own 2026-07
/// creator-direction quote below; there is no separate "Gnome" entity
/// anywhere in this codebase (confirmed by a repo-wide search before
/// writing this pass).
///
/// Design mixture per race (offensive/defensive/CC/debuff/buff/area-
/// denial/possession, per the brief's own explicit list of tactical
/// categories to cover) -- see docs/12's entry for this pass for the
/// full per-ability reasoning and the balance assumptions behind each
/// number:
/// - Mad Doctor default ("Lab Gnome"): Ground Stomp (Stun) + Spore Cloud
///   (Possess) + Defensive Spore Burst (Fear, defensive) + Toxic Sac
///   (Hazard) + Panic Shriek (Fear) + Mutagenic Pulse (Weaken).
/// - Alien/Psionic hands: Psionic Tractor Beam (PullAndConsume) +
///   Psychic Pulse (Weaken) + Neural Disruption (Fear) + Psychic Shield
///   (Fear, defensive) + Mind Control (Possess).
/// - Electric/Tech hand: Area Shock (Stun) + EMP Pulse (Weaken) +
///   Discharge Burst (Fear, defensive) + Arc Lance (Damage) + Magnetic
///   Tether (PullAndConsume).
/// - Human Army (Tank): Flamethrower Burst (Damage) + Smoke Grenade
///   (Fear, defensive) + Suppressive Fire (Weaken) + Combat Stim
///   (Boost, defensive) + Frag Grenade (Damage, a tighter/punchier
///   profile than Flamethrower Burst's wide cone -- two genuinely
///   different damage tools, not a rename).
/// </summary>
public static class SecondaryAttackCatalog
{
    private static SpecialAttackDefinition _flamethrower;
    private static SpecialAttackDefinition _psionicTractorBeam;
    private static SpecialAttackDefinition _groundStomp;
    private static SpecialAttackDefinition _areaShock;
    private static SpecialAttackDefinition _sporeCloud;
    private static SpecialAttackDefinition _defensiveSporeBurst;
    private static SpecialAttackDefinition _toxicSac;
    private static SpecialAttackDefinition _panicShriek;
    private static SpecialAttackDefinition _mutagenicPulse;
    private static SpecialAttackDefinition _psychicPulse;
    private static SpecialAttackDefinition _neuralDisruption;
    private static SpecialAttackDefinition _psychicShield;
    private static SpecialAttackDefinition _mindControl;
    private static SpecialAttackDefinition _empPulse;
    private static SpecialAttackDefinition _dischargeBurst;
    private static SpecialAttackDefinition _arcLance;
    private static SpecialAttackDefinition _magneticTether;
    private static SpecialAttackDefinition _smokeGrenade;
    private static SpecialAttackDefinition _suppressiveFire;
    private static SpecialAttackDefinition _combatStim;
    private static SpecialAttackDefinition _fragGrenade;

    // ---- Human Army (Tank) --------------------------------------------

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

    /// <summary>2026-08 ("Humans/Soldiers... Smoke grenade"): a defensive
    /// deterrent, not a damage tool -- a Tank under threat throws smoke
    /// to break nearby attackers' aim (Fear: can't fire for a duration)
    /// and buy itself time to reposition, rather than always answering
    /// threat with more fire.</summary>
    public static SpecialAttackDefinition SmokeGrenade()
    {
        if (_smokeGrenade == null)
        {
            _smokeGrenade = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _smokeGrenade.AbilityName = "Smoke Grenade";
            _smokeGrenade.Description = "A thick smoke screen that blinds anything trying to draw a bead nearby.";
            _smokeGrenade.Cooldown = 12f;
            _smokeGrenade.Range = 0f;   // self-centered -- the smoke breaks out around the Tank itself
            _smokeGrenade.AreaOfEffect = 5f;
            _smokeGrenade.ValidTargets = TargetFilter.Monster;
            _smokeGrenade.EffectType = SpecialAttackEffectType.Fear;
            _smokeGrenade.FearDuration = 3f;
            _smokeGrenade.IsDefensive = true;
            _smokeGrenade.BloodCost = 1;
            _smokeGrenade.BonesCost = 2;
        }
        return _smokeGrenade;
    }

    /// <summary>2026-08 ("Suppressive fire"): pins nearby attackers down
    /// -- a real Weaken (slower reload), not damage, matching "keep
    /// their heads down" over "hurt them" as this ability's whole
    /// point.</summary>
    public static SpecialAttackDefinition SuppressiveFire()
    {
        if (_suppressiveFire == null)
        {
            _suppressiveFire = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _suppressiveFire.AbilityName = "Suppressive Fire";
            _suppressiveFire.Description = "A raking burst that keeps enemy heads down and their aim off.";
            _suppressiveFire.Cooldown = 11f;
            _suppressiveFire.Range = 14f;
            _suppressiveFire.AreaOfEffect = 4f;
            _suppressiveFire.ValidTargets = TargetFilter.Monster;
            _suppressiveFire.EffectType = SpecialAttackEffectType.Weaken;
            _suppressiveFire.TempoMultiplier = 1.6f;
            _suppressiveFire.TempoDuration = 4f;
            _suppressiveFire.BloodCost = 2;
            _suppressiveFire.BonesCost = 2;
        }
        return _suppressiveFire;
    }

    /// <summary>2026-08 ("Combat stim"): a genuine self-buff, not an
    /// enemy debuff -- the SAME `Weaken` machinery
    /// (`UnitCombat.ApplyTempoModifier`) run in reverse (a multiplier
    /// &lt;1, applied to the caster itself by `SpecialAttackResolver`'s
    /// own Boost branch) instead of a second, parallel buff system.
    /// Flagged defensive/reactive -- a crew shooting themselves up
    /// under real threat, not a routine damage-race opener.</summary>
    public static SpecialAttackDefinition CombatStim()
    {
        if (_combatStim == null)
        {
            _combatStim = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _combatStim.AbilityName = "Combat Stim";
            _combatStim.Description = "A jolt of battlefield stimulant -- the crew fires faster while it lasts.";
            _combatStim.Cooldown = 16f;
            _combatStim.Range = 0f;   // self-only -- see EffectType.Boost's own doc comment
            _combatStim.AreaOfEffect = 0f;
            _combatStim.ValidTargets = TargetFilter.Monster;
            _combatStim.EffectType = SpecialAttackEffectType.Boost;
            _combatStim.TempoMultiplier = 0.6f;   // faster reload
            _combatStim.TempoDuration = 5f;
            _combatStim.IsDefensive = true;
            _combatStim.MinTargetsInArea = 0;   // a self-buff has no "targets caught" to require
            _combatStim.BloodCost = 2;
            _combatStim.BonesCost = 1;
        }
        return _combatStim;
    }

    /// <summary>2026-08 ("Grenade"): a second, genuinely different Damage
    /// tool from Flamethrower Burst -- tighter radius, punchier single
    /// hit, much shorter cooldown (a quick follow-up throw, not a
    /// sustained burn), so the Tank has an actual tactical CHOICE
    /// between "wide burn" and "sharp burst," not a same-effect
    /// rename.</summary>
    public static SpecialAttackDefinition FragGrenade()
    {
        if (_fragGrenade == null)
        {
            _fragGrenade = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _fragGrenade.AbilityName = "Frag Grenade";
            _fragGrenade.Description = "A hand-thrown charge -- a sharp, close blast rather than a sustained burn.";
            _fragGrenade.Cooldown = 9f;
            _fragGrenade.Range = 12f;
            _fragGrenade.AreaOfEffect = 3f;
            _fragGrenade.ValidTargets = TargetFilter.Monster;
            _fragGrenade.EffectType = SpecialAttackEffectType.Damage;
            _fragGrenade.DamageAmount = 22f;
            _fragGrenade.BloodCost = 2;
            _fragGrenade.BonesCost = 3;
        }
        return _fragGrenade;
    }

    /// <summary>2026-08 (creator direction: "Expand Secondary Attack
    /// Variety Across Races... Humans / Soldiers"): every Tank's full
    /// equipped pool -- `Tank.Init` adds every entry, not just
    /// Flamethrower Burst.</summary>
    public static IReadOnlyList<SpecialAttackDefinition> ForTank()
    {
        return new[] { Flamethrower(), SmokeGrenade(), SuppressiveFire(), CombatStim(), FragGrenade() };
    }

    // ---- Alien-tech-handed monsters (Psionic) --------------------------

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
            // 2026-08 ("Add Strong Visual Representation for Area Attacks
            // and Psionics"): the ONLY ability tagged Psionic today --
            // see SpecialAttackVfxStyle's own doc comment for why this
            // is a separate tag from EffectType rather than inferred
            // from PullAndConsume (a future non-psionic PullAndConsume
            // ability, e.g. a real Web Attack, would want the Area look
            // instead).
            _psionicTractorBeam.VfxStyle = SpecialAttackVfxStyle.Psionic;
        }
        return _psionicTractorBeam;
    }

    /// <summary>2026-08 ("Aliens / Psionic creatures... Psychic
    /// pulse"): a radiating mental shockwave that scrambles enemy
    /// timing -- Weaken, not damage, matching "disorientation" over
    /// "harm."</summary>
    public static SpecialAttackDefinition PsychicPulse()
    {
        if (_psychicPulse == null)
        {
            _psychicPulse = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _psychicPulse.AbilityName = "Psychic Pulse";
            _psychicPulse.Description = "A radiating wave of alien mental force that scrambles nearby minds.";
            _psychicPulse.Cooldown = 13f;
            _psychicPulse.Range = 0f;   // self-centered
            _psychicPulse.AreaOfEffect = 5f;
            _psychicPulse.ValidTargets = TargetFilter.All;
            _psychicPulse.EffectType = SpecialAttackEffectType.Weaken;
            _psychicPulse.TempoMultiplier = 1.6f;
            _psychicPulse.TempoDuration = 4f;
            _psychicPulse.BloodCost = 3;
            _psychicPulse.BonesCost = 1;
            _psychicPulse.VfxStyle = SpecialAttackVfxStyle.Psionic;
        }
        return _psychicPulse;
    }

    /// <summary>2026-08 ("Fear wave... Neural disruption"): a ranged
    /// psychic strike that overloads a target's own nervous system --
    /// Fear, thrown at a target point rather than self-centered (the
    /// alien reaches out with this one, unlike Psychic Pulse).</summary>
    public static SpecialAttackDefinition NeuralDisruption()
    {
        if (_neuralDisruption == null)
        {
            _neuralDisruption = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _neuralDisruption.AbilityName = "Neural Disruption";
            _neuralDisruption.Description = "A focused psychic strike that overloads a target's own nervous system.";
            _neuralDisruption.Cooldown = 14f;
            _neuralDisruption.Range = 9f;
            _neuralDisruption.AreaOfEffect = 3f;
            _neuralDisruption.ValidTargets = TargetFilter.All;
            _neuralDisruption.EffectType = SpecialAttackEffectType.Fear;
            _neuralDisruption.FearDuration = 3f;
            _neuralDisruption.BloodCost = 3;
            _neuralDisruption.BonesCost = 1;
            _neuralDisruption.VfxStyle = SpecialAttackVfxStyle.Psionic;
        }
        return _neuralDisruption;
    }

    /// <summary>2026-08 ("Psychic shield"): defensive -- a warping
    /// mental deterrent rather than a literal damage-reduction shield
    /// (this codebase has no damage-reduction stat to hook -- see
    /// docs/12's entry for this pass for why that was deliberately not
    /// added). Reuses Fear the same way Defensive Spore Burst/Discharge
    /// Burst/Smoke Grenade do, applied to nearby attackers rather than a
    /// buff on the caster.</summary>
    public static SpecialAttackDefinition PsychicShield()
    {
        if (_psychicShield == null)
        {
            _psychicShield = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _psychicShield.AbilityName = "Psychic Shield";
            _psychicShield.Description = "A warping mental deterrent that unnerves anything closing in.";
            _psychicShield.Cooldown = 12f;
            _psychicShield.Range = 0f;
            _psychicShield.AreaOfEffect = 5f;
            _psychicShield.ValidTargets = TargetFilter.All;
            _psychicShield.EffectType = SpecialAttackEffectType.Fear;
            _psychicShield.FearDuration = 2.5f;
            _psychicShield.IsDefensive = true;
            _psychicShield.BloodCost = 2;
            _psychicShield.BonesCost = 1;
            _psychicShield.VfxStyle = SpecialAttackVfxStyle.Psionic;
        }
        return _psychicShield;
    }

    /// <summary>2026-08 ("Mind control"): a focused, single-point
    /// Possess -- smaller area and a higher per-target chance than
    /// Spore Cloud's diffuse cloud (still within the brief's own
    /// "1-5% chance" ceiling), reflecting a precise alien psychic strike
    /// rather than a drifting biological spore.</summary>
    public static SpecialAttackDefinition MindControl()
    {
        if (_mindControl == null)
        {
            _mindControl = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _mindControl.AbilityName = "Mind Control";
            _mindControl.Description = "A focused psychic intrusion that can seize a weak mind for a few seconds.";
            _mindControl.Cooldown = 18f;
            _mindControl.Range = 8f;
            _mindControl.AreaOfEffect = 2f;
            _mindControl.ValidTargets = TargetFilter.All;
            _mindControl.EffectType = SpecialAttackEffectType.Possess;
            _mindControl.PossessChancePercent = 5f;
            _mindControl.PossessDuration = 4f;
            _mindControl.BloodCost = 4;
            _mindControl.BonesCost = 2;
            _mindControl.VfxStyle = SpecialAttackVfxStyle.Psionic;
        }
        return _mindControl;
    }

    /// <summary>2026-08 (creator direction: "Expand Secondary Attack
    /// Variety Across Races... Aliens / Psionic creatures"): every
    /// alien-tech-handed monster's full equipped pool.</summary>
    public static IReadOnlyList<SpecialAttackDefinition> ForAlien()
    {
        return new[] { PsionicTractorBeam(), PsychicPulse(), NeuralDisruption(), PsychicShield(), MindControl() };
    }

    // ---- electric_arc hand (Electric/Tech) -----------------------------

    /// <summary>2026-08 (creator direction, verbatim: "Area shock stuns
    /// enemy units for 10 seconds"): the electric_arc hand family's own
    /// secondary attack -- self-centered like Ground Stomp (a discharge
    /// radiating out from the caster, not a thrown/aimed effect), but a
    /// much longer stun (10s vs Ground Stomp's 2s) at a smaller radius,
    /// so the tradeoff is real: a bigger area OR a much longer lockdown,
    /// never both. `VfxStyle` is left at its default (`Area`) -- already
    /// an explicit white-core/blue-electric look (SpecialAttackVfx.cs's
    /// own header), which fits this ability without any new VFX
    /// code.</summary>
    public static SpecialAttackDefinition AreaShock()
    {
        if (_areaShock == null)
        {
            _areaShock = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _areaShock.AbilityName = "Area Shock";
            _areaShock.Description = "A crackling discharge of electrical current that locks up anything caught within range.";
            _areaShock.Cooldown = 18f;   // heaviest cooldown of the four -- the payoff is the longest stun in the game
            _areaShock.Range = 0f;       // self-centered -- no approach distance to close
            _areaShock.AreaOfEffect = 5f;
            _areaShock.ValidTargets = TargetFilter.All;
            _areaShock.EffectType = SpecialAttackEffectType.Stun;
            _areaShock.StunDuration = 10f;
            // an electrical discharge over physical mass -- heavier Blood
            // draw than Bones, same reasoning Psionic Tractor Beam's own
            // comment gives for "energy over physical material."
            _areaShock.BloodCost = 4;
            _areaShock.BonesCost = 2;
        }
        return _areaShock;
    }

    /// <summary>2026-08 ("Machines / technological... EMP pulse"): an
    /// overload burst that slows nearby systems -- Weaken, self-centered,
    /// the electric-race counterpart to Psychic Pulse.</summary>
    public static SpecialAttackDefinition EmpPulse()
    {
        if (_empPulse == null)
        {
            _empPulse = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _empPulse.AbilityName = "EMP Pulse";
            _empPulse.Description = "A short-range electromagnetic surge that overloads nearby systems.";
            _empPulse.Cooldown = 14f;
            _empPulse.Range = 0f;
            _empPulse.AreaOfEffect = 5f;
            _empPulse.ValidTargets = TargetFilter.All;
            _empPulse.EffectType = SpecialAttackEffectType.Weaken;
            _empPulse.TempoMultiplier = 1.7f;
            _empPulse.TempoDuration = 4f;
            _empPulse.BloodCost = 3;
            _empPulse.BonesCost = 2;
        }
        return _empPulse;
    }

    /// <summary>2026-08 ("Defensive field"): a defensive electrical
    /// discharge that startles anything closing in -- Fear, the
    /// electric-race counterpart to Defensive Spore Burst/Psychic
    /// Shield/Smoke Grenade.</summary>
    public static SpecialAttackDefinition DischargeBurst()
    {
        if (_dischargeBurst == null)
        {
            _dischargeBurst = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _dischargeBurst.AbilityName = "Discharge Burst";
            _dischargeBurst.Description = "A defensive arc of current that startles anything closing in.";
            _dischargeBurst.Cooldown = 12f;
            _dischargeBurst.Range = 0f;
            _dischargeBurst.AreaOfEffect = 4f;
            _dischargeBurst.ValidTargets = TargetFilter.All;
            _dischargeBurst.EffectType = SpecialAttackEffectType.Fear;
            _dischargeBurst.FearDuration = 2.5f;
            _dischargeBurst.IsDefensive = true;
            _dischargeBurst.BloodCost = 2;
            _dischargeBurst.BonesCost = 2;
        }
        return _dischargeBurst;
    }

    /// <summary>2026-08 ("a direct Electric arc attack on opponents and
    /// buildings" -- the PRIMARY weapon half of this request lives in
    /// `Combat.WeaponFor`'s own "electric_arc" case, roster-client; this
    /// is the SECONDARY-attack counterpart): a precise, ranged Damage
    /// tool, giving the electric race a real damage option beyond its
    /// two crowd-control abilities.</summary>
    public static SpecialAttackDefinition ArcLance()
    {
        if (_arcLance == null)
        {
            _arcLance = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _arcLance.AbilityName = "Arc Lance";
            _arcLance.Description = "A focused lance of electrical current punched straight through a target.";
            _arcLance.Cooldown = 11f;
            _arcLance.Range = 12f;
            _arcLance.AreaOfEffect = 2f;
            _arcLance.ValidTargets = TargetFilter.All;
            _arcLance.EffectType = SpecialAttackEffectType.Damage;
            _arcLance.DamageAmount = 25f;
            _arcLance.BloodCost = 3;
            _arcLance.BonesCost = 2;
        }
        return _arcLance;
    }

    /// <summary>2026-08 ("Telekinetic knockback" read through an
    /// electromagnetic lens): reuses PullAndConsume exactly like
    /// Psionic Tractor Beam -- an electromagnetic pull instead of a
    /// psychic one, same mechanic, distinct flavor and (via the default
    /// Area VfxStyle) distinct color.</summary>
    public static SpecialAttackDefinition MagneticTether()
    {
        if (_magneticTether == null)
        {
            _magneticTether = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _magneticTether.AbilityName = "Magnetic Tether";
            _magneticTether.Description = "A short-range magnetic pulse that hauls light targets in and pins down anything too heavy to lift.";
            _magneticTether.Cooldown = 13f;
            _magneticTether.Range = 9f;
            _magneticTether.AreaOfEffect = 2f;
            _magneticTether.ValidTargets = TargetFilter.All;
            _magneticTether.EffectType = SpecialAttackEffectType.PullAndConsume;
            _magneticTether.BloodCost = 3;
            _magneticTether.BonesCost = 1;
        }
        return _magneticTether;
    }

    /// <summary>2026-08 (creator direction: "Expand Secondary Attack
    /// Variety Across Races... Machines / technological enemies"):
    /// every electric_arc-handed monster's full equipped pool.</summary>
    public static IReadOnlyList<SpecialAttackDefinition> ForElectric()
    {
        return new[] { AreaShock(), EmpPulse(), DischargeBurst(), ArcLance(), MagneticTether() };
    }

    // ---- Mad Doctor default creature ("the Lab Gnome") -----------------

    /// <summary>The Mad Doctor's default creature (every monster whose
    /// hand family isn't alien tech): creator direction, 2026-07: "mad dr
    /// Ground stomp stun effect." Self-centered -- resolves at the
    /// caster's own feet the instant it triggers, no projectile travel
    /// (see MonsterAgent.TickSpecialAttack's Range&lt;=0 dispatch).</summary>
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

    /// <summary>2026-08 ("Spore Cloud... Debuff / temporary possession...
    /// enemies caught inside have a 1-5% chance of becoming temporarily
    /// possessed... lasts 2-4 seconds... do not make this a guaranteed
    /// effect"): a self-centered cloud, the diffuse/wide-area sibling to
    /// Mind Control's focused single-point strike -- same `Possess`
    /// mechanic, tuned toward "catches many, rarely" rather than
    /// "targets one, more reliably."</summary>
    public static SpecialAttackDefinition SporeCloud()
    {
        if (_sporeCloud == null)
        {
            _sporeCloud = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _sporeCloud.AbilityName = "Spore Cloud";
            _sporeCloud.Description = "A drifting cloud of mind-altering spores -- rarely, something caught inside loses itself for a moment.";
            _sporeCloud.Cooldown = 16f;
            _sporeCloud.Range = 0f;
            _sporeCloud.AreaOfEffect = 5f;
            _sporeCloud.ValidTargets = TargetFilter.All;
            _sporeCloud.EffectType = SpecialAttackEffectType.Possess;
            _sporeCloud.PossessChancePercent = 3f;
            _sporeCloud.PossessDuration = 3f;
            _sporeCloud.BloodCost = 2;
            _sporeCloud.BonesCost = 2;
            _sporeCloud.VfxStyle = SpecialAttackVfxStyle.Organic;
        }
        return _sporeCloud;
    }

    /// <summary>2026-08 ("Defensive Spore Burst... Active defence /
    /// escape... a biological defence mechanism rather than another
    /// damage attack"): reuses Fear (deters nearby attackers) rather
    /// than Possess -- a defensive burst isn't trying to convert an
    /// enemy, it's trying to buy an opening.</summary>
    public static SpecialAttackDefinition DefensiveSporeBurst()
    {
        if (_defensiveSporeBurst == null)
        {
            _defensiveSporeBurst = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _defensiveSporeBurst.AbilityName = "Defensive Spore Burst";
            _defensiveSporeBurst.Description = "A choking burst of spores that drives back anything pressing the attack.";
            _defensiveSporeBurst.Cooldown = 12f;
            _defensiveSporeBurst.Range = 0f;
            _defensiveSporeBurst.AreaOfEffect = 5f;
            _defensiveSporeBurst.ValidTargets = TargetFilter.All;
            _defensiveSporeBurst.EffectType = SpecialAttackEffectType.Fear;
            _defensiveSporeBurst.FearDuration = 3f;
            _defensiveSporeBurst.IsDefensive = true;
            _defensiveSporeBurst.BloodCost = 1;
            _defensiveSporeBurst.BonesCost = 2;
            _defensiveSporeBurst.VfxStyle = SpecialAttackVfxStyle.Organic;
        }
        return _defensiveSporeBurst;
    }

    /// <summary>2026-08 ("Toxic Sac... Area denial... drops or throws a
    /// biological sac that bursts after a short delay... creates a
    /// small hazardous area... persists briefly"): the only `Hazard`-
    /// type ability in this pass -- resolves via `HazardZoneEffect`
    /// instead of an instant per-target application (see
    /// `SpecialAttackResolver`'s own doc comment).</summary>
    public static SpecialAttackDefinition ToxicSac()
    {
        if (_toxicSac == null)
        {
            _toxicSac = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _toxicSac.AbilityName = "Toxic Sac";
            _toxicSac.Description = "A thrown biological sac that bursts into a lingering toxic patch.";
            _toxicSac.Cooldown = 16f;
            _toxicSac.Range = 12f;
            _toxicSac.AreaOfEffect = 4f;
            _toxicSac.ValidTargets = TargetFilter.All;
            _toxicSac.EffectType = SpecialAttackEffectType.Hazard;
            _toxicSac.HazardDuration = 6f;
            _toxicSac.HazardTickInterval = 0.6f;
            // the zone's own periodic Weaken -- refreshed each tick a
            // target is still standing inside, so TempoDuration only
            // needs to outlast one tick interval, not the whole zone.
            _toxicSac.TempoMultiplier = 1.6f;
            _toxicSac.TempoDuration = 1.2f;
            _toxicSac.BloodCost = 2;
            _toxicSac.BonesCost = 3;
            _toxicSac.VfxStyle = SpecialAttackVfxStyle.Organic;
        }
        return _toxicSac;
    }

    /// <summary>2026-08 ("Panic Shriek... Crowd disruption... causes
    /// nearby enemies to briefly stagger, flee, or lose their current
    /// attack target... particularly useful when the Gnome is
    /// surrounded"): a wide-radius, offensive Fear -- unlike Defensive
    /// Spore Burst (flagged `IsDefensive`, only fires when THIS creature
    /// is threatened), Panic Shriek competes normally on catch-count and
    /// is picked when it'd disrupt the MOST enemies, whether or not the
    /// caster itself is in danger.</summary>
    public static SpecialAttackDefinition PanicShriek()
    {
        if (_panicShriek == null)
        {
            _panicShriek = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _panicShriek.AbilityName = "Panic Shriek";
            _panicShriek.Description = "A sudden bone-deep shriek that scatters attention in every direction.";
            _panicShriek.Cooldown = 14f;
            _panicShriek.Range = 0f;
            _panicShriek.AreaOfEffect = 7f;
            _panicShriek.ValidTargets = TargetFilter.All;
            _panicShriek.EffectType = SpecialAttackEffectType.Fear;
            _panicShriek.FearDuration = 2.5f;
            _panicShriek.BloodCost = 2;
            _panicShriek.BonesCost = 2;
            _panicShriek.VfxStyle = SpecialAttackVfxStyle.Disruption;
        }
        return _panicShriek;
    }

    /// <summary>2026-08 ("Mutagenic Pulse... Short-range status
    /// attack... reduced movement speed, attack speed, or accuracy"):
    /// Weaken -- this pass scopes the debuff to fire rate specifically
    /// (see `SpecialAttackDefinition.TempoMultiplier`'s own doc comment
    /// for why accuracy/miss-chance wasn't built), the offensive
    /// counterpart to Psychic Pulse/EMP Pulse for this race.</summary>
    public static SpecialAttackDefinition MutagenicPulse()
    {
        if (_mutagenicPulse == null)
        {
            _mutagenicPulse = ScriptableObject.CreateInstance<SpecialAttackDefinition>();
            _mutagenicPulse.AbilityName = "Mutagenic Pulse";
            _mutagenicPulse.Description = "A pulsing wave of unstable biology that fouls muscle and nerve alike.";
            _mutagenicPulse.Cooldown = 13f;
            _mutagenicPulse.Range = 0f;
            _mutagenicPulse.AreaOfEffect = 5f;
            _mutagenicPulse.ValidTargets = TargetFilter.All;
            _mutagenicPulse.EffectType = SpecialAttackEffectType.Weaken;
            _mutagenicPulse.TempoMultiplier = 1.5f;
            _mutagenicPulse.TempoDuration = 4f;
            _mutagenicPulse.BloodCost = 2;
            _mutagenicPulse.BonesCost = 2;
            _mutagenicPulse.VfxStyle = SpecialAttackVfxStyle.Disruption;
        }
        return _mutagenicPulse;
    }

    /// <summary>2026-08 (creator direction, verbatim: "Give the Lab
    /// Gnome a pool of secondary abilities... Do not stop after adding
    /// six Lab Gnome attacks"): every non-alien-handed monster's full
    /// equipped pool -- Ground Stomp plus the five new ones above.</summary>
    public static IReadOnlyList<SpecialAttackDefinition> ForMadDoctorDefault()
    {
        return new[] { GroundStomp(), SporeCloud(), DefensiveSporeBurst(), ToxicSac(), PanicShriek(), MutagenicPulse() };
    }

    /// <summary>docs/26 Phase 9 (creator follow-up, verbatim: "add [an
    /// electric attack] into the lab... Area shock stuns enemy units for
    /// 10 seconds and a direct Electric arc attack on opponents and
    /// buildings"; 2026-08 follow-up: "Expand Secondary Attack Variety
    /// Across Races"): which secondary-attack POOL a MONSTER is equipped
    /// with, derived purely from its existing genome-derived hand family
    /// -- no new gene. Mirrors `Combat.WeaponFor`'s own family switch
    /// (roster-client): the same hand families the creator called out as
    /// alien tech ("aliens laser and photonic blasters") read as
    /// alien-flavored here too, so a monster's weapon and its secondary
    /// attacks are always thematically consistent for free. Everything
    /// else -- organic/biotech hands -- gets the Mad Doctor's default
    /// pool. Extensible: a future hand family with its own signature
    /// pool is one more case here, no other wiring changes.</summary>
    public static IReadOnlyList<SpecialAttackDefinition> ForMonster(string handFamily)
    {
        switch (handFamily)
        {
            case "laser_array":
            case "photon_blaster":
            case "plasma_lance":
                return ForAlien();
            case "electric_arc":
                return ForElectric();
            default:
                return ForMadDoctorDefault();
        }
    }
}
