using System;
using UnityEngine;

/// <summary>
/// A player-defined, non-genome Collector loadout ("class") -- creator
/// direction: "let's fix that, create collectors. I wanted to define
/// them in the lab, as a class. Like a battalion." Deliberately NOT a
/// bred creature: Collector is Mad-Doctor apparatus (see Collector.cs's
/// own header, "Tank.cs's pattern, not MonsterAgent's"), so "define a
/// class in the Lab" here means picking from a small fixed catalog
/// (speed/range/trim tiers) plus a batch size -- closer to how Human
/// Army/Alien Hive roster units work than to genome breeding. Keeps the
/// Mad Doctor's own design law intact ("the Doctor's identity is CUSTOM
/// BRED creatures," <c>FactionRoster.cs</c>) -- a Collector is
/// apparatus, not a creation, so it never touches that law.
///
/// "Battalion" = a batch production count: defining a class also sets
/// how many Collectors a single Big Brain training order produces in
/// one go (<see cref="RuntimeCityBuilder.BeginCollectorBattalion"/>).
///
/// <c>[Serializable]</c> so <c>JsonUtility</c> can round-trip a whole
/// saved list of these through <c>PlayerPrefs</c>
/// (<see cref="RuntimeCityBuilder.CollectorClasses"/>) -- unlike a Lab
/// battalion CREATURE template (<c>BattalionTemplateDto</c>), a
/// Collector class has no genome id to fetch from the Mutator, so it
/// needs no server round trip at all; it's pure local loadout data, the
/// same tier as a saved control-scheme preset.
/// </summary>
[Serializable]
public class CollectorClassDef
{
    public string Name = "Ravagers";
    public CollectorSpeedTier Speed = CollectorSpeedTier.Standard;
    public CollectorRangeTier Range = CollectorRangeTier.Standard;
    public CollectorTrim Trim = CollectorTrim.Standard;
    public int BatchSize = 3;

    public const int MinBatchSize = 1;
    public const int MaxBatchSize = 5;

    // v0.1 placeholder numbers -- CLAUDE.md's standing "flag the invented
    // number, don't pretend it's balanced" policy, same status as every
    // other cost table in this codebase.
    private const int BaseBonesCostPerUnit = 10;
    private const int SwiftSpeedSurcharge = 5;
    private const int ExtendedRangeSurcharge = 5;
    private const float BaseTrainSecondsPerUnit = 6f;

    // Collector.cs's own today's-hardcoded defaults (SeekRadius 45,
    // MoveSpeed 5.5, PullSpeed 5) are exactly the Standard/Standard tier
    // values here, so a class saved with every tier at Standard produces
    // an UNCHANGED Collector -- picking tiers up is a real upgrade, never
    // a downgrade from what already shipped.
    public int BonesCostPerUnit =>
        BaseBonesCostPerUnit
        + (Speed == CollectorSpeedTier.Swift ? SwiftSpeedSurcharge : 0)
        + (Range == CollectorRangeTier.Extended ? ExtendedRangeSurcharge : 0);

    public int TotalBonesCost => BonesCostPerUnit * Mathf.Clamp(BatchSize, MinBatchSize, MaxBatchSize);

    public float TrainSecondsPerUnit => BaseTrainSecondsPerUnit;

    public float SeekSpeed => Speed == CollectorSpeedTier.Swift ? 7.5f : 5.5f;
    public float PullSpeed => Speed == CollectorSpeedTier.Swift ? 6.5f : 5f;
    public float SeekRadius => Range == CollectorRangeTier.Extended ? 70f : 45f;
}

public enum CollectorSpeedTier { Standard, Swift }
public enum CollectorRangeTier { Standard, Extended }

/// <summary>Cosmetic-only hull-trim pick -- never changes the base
/// muted-violet Mad Doctor apparatus hull color (aesthetic-preferences
/// skill: shape carries kind, color carries faction/state), only the
/// funnel-scoop accent, so every Collector still reads as Mad Doctor
/// apparatus at a glance regardless of which class trained it.</summary>
public enum CollectorTrim { Standard, Brass, Bone }
