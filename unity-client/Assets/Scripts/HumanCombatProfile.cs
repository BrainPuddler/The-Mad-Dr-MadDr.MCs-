using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// 2026-08 (creator brief: "Refactor Human Soldiers & Armed Citizens into
/// Monster Variants" -- goal condensed: "new human enemy variant = data,
/// not new code"). Researched before writing anything (an Explore agent,
/// read-only) whether the brief's literal ask -- fold armed humans into
/// `MonsterAgent`, Unity's genome-driven creature system, via
/// ScriptableObject-authored variants, a generic `MonsterSpawner`, object
/// pooling, an "existing LOD system," Burst, and general GPU instancing --
/// matches this codebase. It doesn't, on several load-bearing points:
///
///  - `MonsterAgent` REQUIRES a bred `StoredGenomeDto` -- locomotion,
///    combat stats, and the mesh itself all derive from genome, with no
///    optional/pluggable non-genome path. Both `Worker.cs` and
///    `HumanSoldier` (its own predecessor, see below) already explicitly,
///    deliberately rejected this pattern -- "a Worker has no creature DNA
///    behind it" -- confirmed with the creator directly for HumanSoldier
///    via AskUserQuestion. Folding armed humans into genome would reverse
///    that, and conflicts with this project's own Origins/breeding
///    invariants (CLAUDE.md) -- a human with a gun was never bred.
///  - ScriptableObject exists in this codebase (`MonsterCombatProfile`,
///    `SpecialAttackDefinition`) but only ever via `CreateInstance<T>()`
///    with in-code defaults -- there is no Unity Editor in this
///    environment to author a `.asset` file, so "designer edits an
///    Inspector asset" isn't achievable here regardless of base class.
///  - `MonsterSpawner`, object pooling, and an LOD system do not exist
///    anywhere in this codebase -- LOD is a documented, acknowledged GAP
///    (`RuntimeCityBuilder.cs`'s own comment: "zero static batching, mesh
///    combining, or LOD anywhere in the city-building path"), not hidden
///    infrastructure to hook into. Burst/DOTS aren't used at all; "GPU
///    instancing" today means `Material.enableInstancing = true` on a
///    few shared Materials (already true for this system too, see
///    `HumanCharacterKit.SharedMaterial`).
///
/// Confirmed with the creator (AskUserQuestion) before building: the
/// brief's underlying GOAL -- shared AI/nav/combat/animation, new
/// variants as data -- is achieved instead by extending the ALREADY-
/// EXISTING non-genome kit this project built for exactly this purpose
/// (docs/34's character-system overhaul): `HumanCharacterKit`/
/// `HumanCharacterAnimator` for body/animation, plus this file
/// (`HumanCombatProfile`, the gameplay data) and `HumanoidCombatant.cs`
/// (the one shared AI/combat/movement implementation every armed-human
/// variant runs on) for the rest. `MonsterAgent.cs` is untouched --
/// zero risk to already-working genome creature combat.
///
/// This struct is deliberately the gameplay/behavior layer ONLY --
/// `Visual` composes in a `HumanCharacterProfile` (the pre-existing pure-
/// geometry layer) rather than duplicating body/color/posture knobs here,
/// keeping the two concerns (what it looks like vs. how it fights)
/// separable the same way `HumanCharacterKit` already separates geometry
/// from `HumanCharacterAnimator`'s animation.
/// </summary>
public struct HumanCombatProfile
{
    public HumanCharacterProfile Visual;

    // "monster" | "human" | "hostile_civilian" -- see UnitCombat.Faction's
    // own doc comment for the first two's existing meaning ("monster" =
    // any player's own creature/economy-side units, "human" = Tank's
    // fixed environmental defender faction, NOT a real per-player
    // alignment at this legacy Unity-side layer). "hostile_civilian" is
    // NEW here: a third bucket for a genuinely neutral, hostile-to-
    // everyone threat -- Grandma/Armed Civilian should endanger a
    // Worker's "monster"-faction economy AND Tank's "human" side alike,
    // not take either one's side.
    public string Faction;

    public float MaxHealth;
    public float Radius;       // separation + body half-width, UnitCombat.Configure's own param
    public float AimHeight;
    public float Mass;
    public float MoveSpeed;
    public WeaponProfile Weapon;

    public float AggroRadius;
    // Proactive vs reactive -- docs/19's own aggression-band spirit
    // (Passive/Defensive/Aggressive), simplified to a bool here rather
    // than porting that whole system: true seeks out and engages any
    // enemy within AggroRadius; false only fights back once an enemy is
    // already close (see HumanoidCombatant.DefensiveEngageFraction).
    public bool Aggressive;

    // 2026-08 (Grandma, creator direction verbatim: "Rotate realistically
    // before changing direction"): true makes HumanoidCombatant turn IN
    // PLACE to face a new heading before it starts translating, instead
    // of turning while already moving (GroundPathFollower's normal,
    // continuous slerp-while-walking behavior) -- a wheelchair's real
    // maneuver, and a deliberate silhouette/personality trait, not a
    // universal "more realistic" default every variant should want.
    public bool TurnBeforeMove;

    /// <summary>"Grandma in a wheelchair carrying a shotgun" -- the
    /// brief's own named, fan-favourite variant, previously only a
    /// worked-example paragraph in docs/19 §4 (an elderly, mobility-
    /// limited Citizen that rolls Aggressive + the 1% shotgun-tier weapon
    /// roll), never actually built as a real, encounterable unit until
    /// now. Creator direction, this variant specifically: "high Armour
    /// class basically a tank" -- this engine has no separate armor/
    /// damage-reduction stat at the UnitCombat layer (only MaxHealth,
    /// confirmed before choosing this approach), so "basically a tank" is
    /// expressed as a large health pool (190, above Tank's own 150-210
    /// range) rather than a mechanic that doesn't exist here. "And
    /// scary": slow (1.6 m/s, well under every other variant here) but
    /// with a WIDE aggro radius (32 m, the widest of the three variants)
    /// -- she notices and starts rolling toward a threat from well
    /// outside her own weapon's 9 m range, which is the actual source of
    /// dread the brief asks for ("feel dangerous despite her slow
    /// speed"), not raw speed or reach. `TurnBeforeMove` true, seated
    /// (`Visual.SeatedHeight`) rather than legless-and-hovering like the
    /// Alien Worker profile.</summary>
    public static HumanCombatProfile Grandma()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.55f, 0.42f, 0.48f),    // faded housedress
                AccentColor = new Color(0.75f, 0.72f, 0.65f),  // worn cream shawl
                HeightScale = 0.85f,
                LimbThicknessScale = 0.85f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 0.78f,   // frail-LOOKING -- contrast with her actual toughness is the point
                HunchDegrees = 11f,           // an elderly stoop, not a monster hunch -- "memorable without becoming cartoonish"
                Asymmetric = false,
                HasLegs = false,
                SeatedHeight = 0.5f,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = false,
                Headgear = HeadgearKind.None,
                Twitchy = true,   // reused as her "amusing but believable" idle fidget -- see HumanoidCombatant's own note on this being a v0.1 reuse, not a bespoke elderly idle
            },
            Faction = "hostile_civilian",
            MaxHealth = 190f,
            Radius = 0.9f,       // a wheelchair + rider is wider than a standing human
            AimHeight = 0.9f,    // seated -- lower than a standing combatant's aim point
            Mass = 4.5f,
            MoveSpeed = 1.6f,
            Weapon = WeaponProfile.Shotgun(),
            AggroRadius = 32f,
            Aggressive = true,
            TurnBeforeMove = true,
        };
    }

    /// <summary>"Armed Civilian" -- an ordinary bystander who happens to
    /// be carrying a handgun, not a trained combatant: defensive rather
    /// than proactive (only fights once approached), unremarkable stats,
    /// plain civilian dress. Deliberately the LOW-drama contrast to
    /// Grandma's memorable, proactive threat -- demonstrates the same
    /// shared kit producing a forgettable-on-purpose variant just as
    /// easily as a flagship one.</summary>
    public static HumanCombatProfile ArmedCivilian()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.38f, 0.45f, 0.48f),
                AccentColor = new Color(0.22f, 0.26f, 0.3f),
                HeightScale = 1f,
                LimbThicknessScale = 1f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 1f,
                HunchDegrees = 0f,
                Asymmetric = false,
                HasLegs = true,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = false,
                Headgear = HeadgearKind.None,
                Twitchy = false,
            },
            Faction = "hostile_civilian",
            MaxHealth = 55f,
            Radius = 0.5f,
            AimHeight = 1.3f,
            Mass = 1f,
            MoveSpeed = 3.6f,
            Weapon = WeaponProfile.Revolver(),
            AggroRadius = 20f,
            Aggressive = false,
            TurnBeforeMove = false,
        };
    }

    /// <summary>2026-08 (creator direction: "start building Citizen with
    /// guns, army etc" -- the deferred "Police/SWAT/Hunter/Militia" pair
    /// from docs/35 §6's original scope cut, picked as "Police + SWAT" by
    /// the creator's own follow-up). A fast, proactive responder --
    /// notices trouble and moves in quickly (highest MoveSpeed of any
    /// hostile_civilian variant) rather than the wide-aggro/slow-approach
    /// read Grandma or Hunter use. Ordinary sidearm (reuses Revolver --
    /// a real police-issue handgun, not a reason to invent a near-
    /// identical third pistol profile) -- the LOW-drama, first-responder
    /// contrast to SWAT's heavy tactical escalation right below.</summary>
    public static HumanCombatProfile Police()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.16f, 0.2f, 0.32f),      // dark navy uniform
                AccentColor = new Color(0.72f, 0.72f, 0.7f),    // silver badge/trim
                HeightScale = 1f,
                LimbThicknessScale = 1f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 1.02f,
                HunchDegrees = 0f,
                Asymmetric = false,
                HasLegs = true,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = false,
                Headgear = HeadgearKind.None,
                Twitchy = false,
            },
            Faction = "hostile_civilian",
            MaxHealth = 60f,
            Radius = 0.5f,
            AimHeight = 1.3f,
            Mass = 1.1f,
            MoveSpeed = 3.8f,
            Weapon = WeaponProfile.Revolver(),
            AggroRadius = 22f,
            Aggressive = true,
            TurnBeforeMove = false,
        };
    }

    /// <summary>SWAT -- the heavy-armored tactical escalation Police
    /// calls in. Broad, gear-laden silhouette (`HasBackpack` true,
    /// `Headgear` Helmet -- a tactical helmet, not Soldier's military
    /// one, but the same geometry reads close enough at RTS camera
    /// distance that a bespoke third headgear kind wasn't worth adding),
    /// the largest health pool of any hostile_civilian variant next to
    /// Grandma's own "basically a tank" number (this engine has no
    /// separate Armor stat -- same "toughness = health pool" translation
    /// her own profile already established), and
    /// <see cref="WeaponProfile.TacticalCarbine"/>'s sustained fast-
    /// cadence fire.</summary>
    public static HumanCombatProfile Swat()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.12f, 0.13f, 0.14f),     // matte tactical black
                AccentColor = new Color(0.28f, 0.3f, 0.24f),    // olive gear webbing
                HeightScale = 1.04f,
                LimbThicknessScale = 1.2f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 1.28f,   // the broadest silhouette of any hostile_civilian variant -- reads as armored bulk
                HunchDegrees = 0f,
                Asymmetric = false,
                HasLegs = true,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = true,
                Headgear = HeadgearKind.Helmet,
                Twitchy = false,
            },
            Faction = "hostile_civilian",
            MaxHealth = 160f,
            Radius = 0.6f,
            AimHeight = 1.35f,
            Mass = 1.6f,
            MoveSpeed = 3.0f,   // slowest of the four new variants -- weighed down by gear, matching the "heavy" read
            Weapon = WeaponProfile.TacticalCarbine(),
            AggroRadius = 26f,
            Aggressive = true,
            TurnBeforeMove = false,
        };
    }

    /// <summary>Hunter -- a rifle-armed civilian-tier threat built around
    /// reach, not toughness: the WIDEST aggro radius of any
    /// hostile_civilian variant (34m, matching <see
    /// cref="WeaponProfile.HuntingRifle"/>'s own 34m range exactly -- he
    /// notices and engages from the absolute edge of his weapon's own
    /// envelope, never needing to close distance the way Grandma's
    /// wide-aggro/short-weapon mismatch forces her to). Plain earthy
    /// dress, ordinary health -- the threat is positional (you're in his
    /// sightline) not physical (he's not a brawler).</summary>
    public static HumanCombatProfile Hunter()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.32f, 0.3f, 0.2f),       // drab canvas/hunting jacket
                AccentColor = new Color(0.48f, 0.22f, 0.16f),   // blaze-orange-adjacent trim, muted
                HeightScale = 1f,
                LimbThicknessScale = 1f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 1f,
                HunchDegrees = 0f,
                Asymmetric = false,
                HasLegs = true,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = true,   // a game bag/pack, not military webbing -- the same generic backpack prop reads fine for either
                Headgear = HeadgearKind.None,
                Twitchy = false,
            },
            Faction = "hostile_civilian",
            MaxHealth = 65f,
            Radius = 0.5f,
            AimHeight = 1.3f,
            Mass = 1.1f,
            MoveSpeed = 3.3f,
            Weapon = WeaponProfile.HuntingRifle(),
            AggroRadius = 34f,
            Aggressive = true,
            TurnBeforeMove = false,
        };
    }

    /// <summary>Militia -- an irregular, aggressive threat built around
    /// <see cref="WeaponProfile.ServiceRifle"/> (orphaned when the old
    /// cosmetic-parallel "Soldier" variant was superseded by a real
    /// Barracks-trained Rifleman -- see this file's own header on that
    /// -- and reused here rather than left dead: a trained rifle in an
    /// untrained irregular's hands is exactly the "drilled hardware,
    /// undisciplined user" read this variant wants). Patchwork,
    /// asymmetric dress (mismatched surplus gear, not a uniform) and
    /// the highest MaxHealth of the four non-SWAT new variants -- a
    /// scrappy, hard-to-put-down brawler rather than a precise
    /// marksman.</summary>
    public static HumanCombatProfile Militia()
    {
        return new HumanCombatProfile
        {
            Visual = new HumanCharacterProfile
            {
                BodyColor = new Color(0.36f, 0.34f, 0.24f),     // mismatched surplus khaki
                AccentColor = new Color(0.5f, 0.18f, 0.14f),    // a scrap of red cloth -- the "irregular," not issued, tell
                HeightScale = 1.02f,
                LimbThicknessScale = 1.05f,
                ArmLengthScale = 1f,
                ShoulderWidthScale = 1.05f,
                HunchDegrees = 4f,   // a slight slouch -- undisciplined posture, contrasting SWAT/Rifleman's drilled stance
                Asymmetric = true,   // patchwork gear, deliberately not uniform
                HasLegs = true,
                HasHands = true,
                OversizedHands = false,
                HasBackpack = false,
                Headgear = HeadgearKind.None,
                Twitchy = false,
            },
            Faction = "hostile_civilian",
            MaxHealth = 85f,
            Radius = 0.55f,
            AimHeight = 1.3f,
            Mass = 1.3f,
            MoveSpeed = 3.5f,
            Weapon = WeaponProfile.ServiceRifle(),
            AggroRadius = 26f,
            Aggressive = true,
            TurnBeforeMove = false,
        };
    }
}
