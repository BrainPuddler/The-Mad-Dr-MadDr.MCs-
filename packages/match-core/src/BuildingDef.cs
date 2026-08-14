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

        /// <summary>2026-07 worker-economy epic, Phase 4: the Mad
        /// Doctor's "Big Brain control unit" ("must make a big brain
        /// control unit requiring harvesting 20 brain units, that can
        /// control 100 humans" -- creator's own words). Modeled as a
        /// BUILDING, not a <see cref="RosterUnitKind"/>, because
        /// `FactionRoster.cs`'s own header already establishes the Doctor
        /// gets NO fixed roster (custom-bred creatures only) -- this is a
        /// control STRUCTURE, not a trainable unit, the same category as
        /// <see cref="Hq"/>. "Controls 100 humans" is modeled as <see
        /// cref="BuildingDef.SupplyCapBonus"/>, reusing <see
        /// cref="PlayerState.RaiseSupplyCap"/> (existing, previously
        /// unused).</summary>
        BigBrain = 8,

        /// <summary>2026-08 (creator direction: "Human Army is from army
        /// barracks -- part of the basic kit for Human army"): the
        /// production building <see cref="RosterUnitKind"/> infantry
        /// (Rifleman, FlamethrowerTrooper) train from, via the existing
        /// generic <see cref="MatchState.CanTrainUnit"/>/<see
        /// cref="CommandKind.TrainUnit"/> machinery -- that pipeline was
        /// already building-kind-agnostic (any Complete building the
        /// training player owns, one open slot), it just had no second
        /// producer kind to point at besides <see cref="Factory"/> until
        /// now. A real, buildable, per-faction-basic-kit member exactly
        /// like <see cref="Factory"/> is, not a cosmetic prop.</summary>
        Barracks = 9,
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

        /// <summary>Human garrison/crew count housed once Complete -- data
        /// only, not simulated (no decay, no per-tick change). Read by
        /// Unity the moment a building's <see cref="SimBuilding.State"/>
        /// flips to Destroyed, to know how many fleeing Citizens to
        /// disgorge near the wreck (2026-07 creator direction: "when they
        /// are destroyed they disgorge their human occupants that flee").
        /// v0.1 placeholder counts, same standing policy as every other
        /// number in this file -- <see cref="Factory"/>'s 6 is the one
        /// deliberately-sized figure (it's the number feeding the new
        /// Collector/Worker/possession chain this epic builds toward),
        /// everything else is a small flat garrison scaled loosely by
        /// tier.</summary>
        public int Occupants { get; }

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

        /// <summary>2026-07 worker-economy epic, Phase 4: Supply cap this
        /// building raises once Complete, or null for every kind that
        /// isn't one of the epic's population-control structures (today,
        /// only <see cref="BuildingKind.BigBrain"/>). Same "raise-only,
        /// applied once on the Complete transition" contract as <see
        /// cref="StorageCapBonus"/> -- deliberately a SEPARATE field, not
        /// folded into it, since Supply and wallet resources are
        /// different currencies with different caps (<see
        /// cref="PlayerState.SupplyCap"/> vs <see
        /// cref="PlayerState.WalletCap"/>).</summary>
        public int? SupplyCapBonus { get; }

        /// <summary>2026-08 (creator direction: "the debris field is
        /// scavenged for any usable metal by the zombie workers, and
        /// monsters"): total <see cref="ResourceKind.Parts"/> value this
        /// building's wreck yields once destroyed -- the building-side
        /// twin of <see cref="SimUnit.SalvageValue"/>, rolled down to
        /// <see cref="SimBuilding.ScavengeRemaining"/> via the same
        /// <see cref="SalvageMath.RollAmount"/> 40-60% curve corpses
        /// already use. Flat per-tier placeholder (Small 100 / Medium 200
        /// / Large 400 / Landmark 800, doubling per tier), same "no real
        /// number exists yet, scaled loosely by tier" policy as every
        /// other v0.1 figure in this file -- NOT derived from <see
        /// cref="Cost"/>, since summing mixed resource lanes into one
        /// Parts figure would be its own unverified guess.</summary>
        public int ScavengeValue { get; }

        private BuildingDef(BuildingKind kind, string name, (ResourceKind, int)[] cost,
            int buildTimeTicks, int maxHp, int armor, (ResourceKind, int)? storageCapBonus,
            int occupants, int? supplyCapBonus = null, int scavengeValue = 0)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
            BuildTimeTicks = buildTimeTicks;
            MaxHp = maxHp;
            Armor = armor;
            StorageCapBonus = storageCapBonus;
            Occupants = occupants;
            SupplyCapBonus = supplyCapBonus;
            ScavengeValue = scavengeValue;
        }

        // Flat per-tier scavenge placeholders, doubling per tier -- same
        // tier bands as SmallHp/MediumHp/LargeHp/LandmarkHp just below,
        // reused for a second, independent v0.1 number rather than
        // inventing a third scale.
        private const int SmallScavenge = 100, MediumScavenge = 200, LargeScavenge = 400, LandmarkScavenge = 800;

        // docs/18 §3 tiers as a base, bumped 50% in an earlier pass
        // (2026-07: "buildings need larger hitpoints"), then bumped AGAIN
        // here, roughly 2.2x on top of that (2026-08 creator direction:
        // "give buildings much larger hit points" -- a stronger ask than
        // the first pass, so a bigger jump than the first pass's own 50%).
        // ~5x the original docs/18 baseline overall. Armor still
        // unchanged, only HP grows -- same reasoning as the first bump:
        // more hits to fell, no harder to actually damage per hit. v0.1
        // rebalance, same placeholder policy as every other number in
        // this file.
        private const int SmallHp = 1000, SmallArmor = 2;     // house
        private const int MediumHp = 2000, MediumArmor = 4;   // storefront
        private const int LargeHp = 5000, LargeArmor = 6;    // block/tower
        private const int LandmarkHp = 10000, LandmarkArmor = 8;

        private static readonly BuildingDef[] All =
        {
            // HQ: generator-placed, never paid for -- BuildTimeTicks 0,
            // no Cost. Landmark tier (docs/18): a player's HQ is exactly
            // the kind of structure that tier describes.
            new BuildingDef(BuildingKind.Hq, "Headquarters",
                new (ResourceKind, int)[0], buildTimeTicks: 0,
                maxHp: LandmarkHp, armor: LandmarkArmor, storageCapBonus: null,
                occupants: 10, scavengeValue: LandmarkScavenge),

            // docs/22 §6's real "Blood Bank" numbers, plus a Parts line
            // (2026-08 creator direction: "make metal and other building
            // resources one of the requirement for making new buildings
            // structures; it's a plentiful resource" -- every buildable
            // kind below now costs SOME Parts, deliberately a small
            // fraction of a single Small wreck's own ScavengeValue [100]
            // so "plentiful" reads as true: one scavenged house's rubble
            // easily funds several of these).
            new BuildingDef(BuildingKind.BloodStorage, "Blood Storage",
                new[] { (ResourceKind.Bones, 20), (ResourceKind.Blood, 10), (ResourceKind.Parts, 30) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: (ResourceKind.Blood, 100), occupants: 2, scavengeValue: SmallScavenge),

            // 2026-08 follow-up: this building's own income logic is real
            // now (MatchState.GrantFuelPumpIncome, HumanArmy/Mixed only,
            // once per second) -- closed the exact gap this comment used
            // to flag. Cost itself is still a v0.1 placeholder, shaped
            // like BloodStorage's as a reasonable starting guess, not a
            // balance claim.
            new BuildingDef(BuildingKind.FuelPump, "Fuel Pump",
                new[] { (ResourceKind.Bones, 20), (ResourceKind.Fuel, 10), (ResourceKind.Parts, 30) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null, occupants: 2, scavengeValue: SmallScavenge),

            // v0.1 placeholder cost, shaped like docs/22's Bone Pile (15
            // Bones only) as the closest existing analog; cap bonus
            // mirrors BloodStorage's +100 for the same resource class.
            new BuildingDef(BuildingKind.FuelStorage, "Fuel Storage",
                new[] { (ResourceKind.Bones, 15), (ResourceKind.Parts, 30) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: (ResourceKind.Fuel, 100), occupants: 2, scavengeValue: SmallScavenge),

            // v0.1 placeholder cost. No cap bonus -- docs/23 §2's Function
            // column for Parts storage is "enables grafting," not a
            // wallet-cap raise.
            new BuildingDef(BuildingKind.PartsStorage, "Parts Storage",
                new[] { (ResourceKind.Bones, 15), (ResourceKind.Parts, 30) },
                buildTimeTicks: 100, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null, occupants: 2, scavengeValue: SmallScavenge),

            // v0.1 placeholder cost. docs/20's Collection Stations are a
            // pre-existing, hands-free CITY feature -- this is the
            // player-BUILDABLE version docs/23 §2 adds to the roster.
            new BuildingDef(BuildingKind.HarvestPost, "Harvest Post",
                new[] { (ResourceKind.Bones, 15), (ResourceKind.Parts, 30) },
                buildTimeTicks: 80, maxHp: SmallHp, armor: SmallArmor,
                storageCapBonus: null, occupants: 3, scavengeValue: SmallScavenge),

            // v0.1 placeholder cost, pricier than storage (docs/22 §7:
            // "a forward Stitchworks... is a massive tempo investment").
            // Medium tier: sturdier than basic storage, matching that
            // higher stake.
            new BuildingDef(BuildingKind.Factory, "Factory",
                new[] { (ResourceKind.Bones, 30), (ResourceKind.Blood, 15), (ResourceKind.Parts, 50) },
                buildTimeTicks: 150, maxHp: MediumHp, armor: MediumArmor,
                storageCapBonus: null, occupants: 6, scavengeValue: MediumScavenge),

            // v0.1 placeholder cost. Medium tier: a defensive structure
            // sturdier than basic storage, matching its role.
            new BuildingDef(BuildingKind.Defense, "Defense",
                new[] { (ResourceKind.Bones, 25), (ResourceKind.Blood, 10), (ResourceKind.Parts, 50) },
                buildTimeTicks: 120, maxHp: MediumHp, armor: MediumArmor,
                storageCapBonus: null, occupants: 3, scavengeValue: MediumScavenge),

            // 2026-07 epic: 20 Brains, per the creator's own number --
            // the one deliberately-sized cost in this whole table, not a
            // placeholder -- left untouched. Large tier (sturdier than a
            // basic storage/production building, matching a one-off
            // strategic structure's stakes) and zero occupants (a control
            // apparatus, not a staffed building -- nothing to disgorge
            // if it falls). The Parts line alongside it is the SAME
            // 2026-08 "every buildable kind costs some Parts" addition as
            // every other entry in this table, scaled to Large tier.
            new BuildingDef(BuildingKind.BigBrain, "Big Brain",
                new[] { (ResourceKind.Brains, 20), (ResourceKind.Parts, 80) },
                buildTimeTicks: 200, maxHp: LargeHp, armor: LargeArmor,
                storageCapBonus: null, occupants: 0, supplyCapBonus: 100, scavengeValue: LargeScavenge),

            // v0.1 placeholder cost, shaped like UnitRosterDef's own
            // Human Army cost lines (Bones + Fuel -- "recruiting
            // volunteers... drilled infantry needs matériel"), plus the
            // same standing Parts line every buildable kind carries.
            // Medium tier, matching Factory's own stakes (a real
            // production building, not basic storage) -- see this kind's
            // own enum doc comment for why it reuses Factory's existing
            // CanTrainUnit/TrainUnit machinery rather than a new one.
            new BuildingDef(BuildingKind.Barracks, "Barracks",
                new[] { (ResourceKind.Bones, 25), (ResourceKind.Fuel, 15), (ResourceKind.Parts, 50) },
                buildTimeTicks: 140, maxHp: MediumHp, armor: MediumArmor,
                storageCapBonus: null, occupants: 6, scavengeValue: MediumScavenge),
        };

        public static BuildingDef Get(BuildingKind kind) => All[(int)kind];
        public static IReadOnlyList<BuildingDef> AllDefs => All;
    }
}
