using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §2's common building roster -- one generic slot per
    /// row of that section's table. Per-faction display SKINS (Blood Bank
    /// / Plasma Reserve / Ichor Cistern, ...) are a Unity/display concern
    /// (<see cref="FactionDef.BaseName"/>'s own precedent); the sim only
    /// ever reasons about the generic kind, since stats are shared across
    /// factions and only names are themed.</summary>
    public enum BuildingKind
    {
        /// <summary>Generator-placed at match start, never player-built --
        /// see <see cref="MatchState.SpawnHqForPlayer"/>. Not a valid
        /// target for <see cref="CommandKind.BuildStructure"/>.</summary>
        Hq = 0,
        BloodStorage = 1,
        FuelPump = 2,
        FuelStorage = 3,
        PartsStorage = 4,
        HarvestPost = 5,
        Factory = 6,
        Defense = 7,
    }

    /// <summary>Static per-building-kind data (docs/23 §2 Phase 2 tasks:
    /// "BuildingDef data table... costs... build time... HP/armor reusing
    /// docs/18 tier table"). DATA read by the sim, never simulation state
    /// (same convention as <see cref="FactionDef"/>) -- not part of the
    /// tick hash.
    ///
    /// Numbers, honestly sourced: <see cref="BloodStorage"/>'s cost (20
    /// Bones + 10 Blood) and cap bonus (+100) are docs/22 §6's real "Blood
    /// Bank" numbers, reused verbatim -- the clearest 1:1 mapping between
    /// docs/22's original storage design and docs/23 §2's later,
    /// per-faction-skinned roster table. HP/Armor for every kind reuse
    /// docs/18 §3's real structure tiers (Small 300/2, Medium 600/4, Large
    /// 1500/6, Landmark 3000/8) rather than invented numbers. Every OTHER
    /// cost/build-time figure below has no real number anywhere in the
    /// design docs yet (docs/23 §2 itself only says "costs in Blood/Fuel/
    /// Ichor/Bones" without a table) -- flagged here as v0.1 placeholders,
    /// the same standing policy this project already applies to every
    /// other economy number (CLAUDE.md: "v0.1 economy/upkeep numbers
    /// everywhere are placeholders; real balance is a Phase-2 sandbox
    /// pass"), not silently guessed as if they were real. See
    /// docs/12-open-questions.md's Phase 2 entry for the specific open
    /// questions this raises (a real cost table, and which resource
    /// `BloodStorage`'s cap bonus targets per faction -- Phase 3's job,
    /// per docs/23 §3's own task list, not resolved here).</summary>
    public sealed class BuildingDef
    {
        public BuildingKind Kind { get; }

        /// <summary>Generic name (Unity applies the per-faction skin on
        /// top -- docs/23 §2's table).</summary>
        public string Name { get; }

        public IReadOnlyList<(ResourceKind Resource, int Amount)> Cost { get; }
        public int BuildTimeTicks { get; }
        public int MaxHp { get; }
        public int Armor { get; }

        /// <summary>Wallet cap this building raises once complete, or null
        /// for kinds with no storage function (docs/23 §2's Function
        /// column). Data only -- Phase 2 does not enforce wallet caps at
        /// all (that's Phase 3's job per docs/23 §3's own task list:
        /// "storage caps from buildings" is listed there, not here).
        /// `BloodStorage`'s target resource is left as the literal `Blood`
        /// named in docs/22 -- whether it should generalize to "whichever
        /// energy resource the OWNING faction actually uses" (Fuel for
        /// Army, Ichor for Hive) is an open question for whoever wires
        /// cap enforcement in Phase 3, not decided here.</summary>
        public (ResourceKind Resource, int Amount)? StorageCapBonus { get; }

        private BuildingDef(BuildingKind kind, string name, (ResourceKind, int)[] cost,
            int buildTimeTicks, int maxHp, int armor, (ResourceKind, int)? storageCapBonus)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
            BuildTimeTicks = buildTimeTicks;
            MaxHp = maxHp;
            Armor = armor;
            StorageCapBonus = storageCapBonus;
        }

        // docs/18 §3 tiers, reused verbatim rather than invented:
        private const int SmallHp = 300, SmallArmor = 2;     // house
        private const int MediumHp = 600, MediumArmor = 4;   // storefront
        private const int LargeHp = 1500, LargeArmor = 6;    // block/tower
        private const int LandmarkHp = 3000, LandmarkArmor = 8;

        private static readonly BuildingDef[] All =
        {
            // HQ: generator-placed, never paid for -- BuildTimeTicks 0,
            // no Cost. Landmark tier (docs/18): a player's HQ is exactly
            // the kind of structure that tier describes.
            new BuildingDef(BuildingKind.Hq, "Headquarters",
                new (ResourceKind, int)[0], buildTimeTicks: 0,
                maxHp: LandmarkHp, armor: LandmarkArmor, storageCapBonus: null),

            // docs/22 §6's real "Blood Bank" numbers.
            new BuildingDef(BuildingKind.BloodStorage, "Blood Storage",
                new[] { (ResourceKind.Bones, 20), (ResourceKind.Blood, 10) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: (ResourceKind.Blood, 100)),

            // v0.1 placeholder: no real number exists yet for the income
            // building; shaped like BloodStorage's cost as a reasonable
            // starting guess, not a balance claim.
            new BuildingDef(BuildingKind.FuelPump, "Fuel Pump",
                new[] { (ResourceKind.Bones, 20), (ResourceKind.Fuel, 10) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null),

            // v0.1 placeholder cost, shaped like docs/22's Bone Pile (15
            // Bones only) as the closest existing analog; cap bonus
            // mirrors BloodStorage's +100 for the same resource class.
            new BuildingDef(BuildingKind.FuelStorage, "Fuel Storage",
                new[] { (ResourceKind.Bones, 15) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: (ResourceKind.Fuel, 100)),

            // v0.1 placeholder cost. No cap bonus -- docs/23 §2's Function
            // column for Parts storage is "enables grafting," not a
            // wallet-cap raise.
            new BuildingDef(BuildingKind.PartsStorage, "Parts Storage",
                new[] { (ResourceKind.Bones, 15) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null),

            // v0.1 placeholder cost. docs/20's Collection Stations are a
            // pre-existing, hands-free CITY feature -- this is the
            // player-BUILDABLE version docs/23 §2 adds to the roster.
            new BuildingDef(BuildingKind.HarvestPost, "Harvest Post",
                new[] { (ResourceKind.Bones, 15) },
                buildTimeTicks: 80, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null),

            // v0.1 placeholder cost, pricier than storage (docs/22 §7:
            // "a forward Stitchworks... is a massive tempo investment").
            // Medium tier: sturdier than basic storage, matching that
            // higher stake.
            new BuildingDef(BuildingKind.Factory, "Factory",
                new[] { (ResourceKind.Bones, 30), (ResourceKind.Blood, 15) },
                buildTimeTicks: 150, maxHp: MediumHp, armor: MediumArmor,
                storageCapBonus: null),

            // v0.1 placeholder cost. Medium tier: a defensive structure
            // sturdier than basic storage, matching its role.
            new BuildingDef(BuildingKind.Defense, "Defense",
                new[] { (ResourceKind.Bones, 25), (ResourceKind.Blood, 10) },
                buildTimeTicks: 120, maxHp: MediumHp, armor: MediumArmor,
                storageCapBonus: null),
        };

        public static BuildingDef Get(BuildingKind kind) => All[(int)kind];
        public static IReadOnlyList<BuildingDef> AllDefs => All;
    }
}
